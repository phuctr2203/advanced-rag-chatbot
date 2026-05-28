using System.Runtime.CompilerServices;
using System.Text.Json;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public class ChatOrchestrator(
    LanguageDetectionService languageDetection,
    IntentClassifierService intentClassifier,
    VectorStoreService vectorStore,
    PromptBuilderService promptBuilder,
    LlmService llmService,
    SourceCitationParser citationParser,
    FormRegistryService formRegistry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        var language = languageDetection.Detect(message);
        var intent = await intentClassifier.ClassifyAsync(message, ct);

        if (intent == QueryIntent.Smalltalk)
        {
            yield return IntentResponses.Smalltalk(language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        if (intent == QueryIntent.OutOfScope)
        {
            yield return IntentResponses.OutOfScope(language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        var searchResults = await vectorStore.SearchAsync(message, limit: 6, ct);
        if (searchResults.Count == 0)
        {
            yield return IntentResponses.NoResults(language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        var prompt = promptBuilder.Build(message, searchResults, language);
        var fullAnswer = new List<string>();
        await foreach (var token in llmService.StreamAsync(prompt, ct))
        {
            fullAnswer.Add(token);
            yield return token;
        }

        var fullAnswerText = string.Concat(fullAnswer);
        var sources = citationParser.Parse(fullAnswerText, searchResults);
        var downloadRefs = BuildDownloadRefs(fullAnswerText, sources);
        yield return BuildSourcesEvent(sources, downloadRefs);
    }

    private List<FormDownloadRef> BuildDownloadRefs(string fullAnswerText, IReadOnlyList<SourceRef> sources)
    {
        var download = formRegistry.FindByAlias(fullAnswerText);
        if (download is null || sources.Any(source => source.FormDownload?.FormName == download.FormName))
        {
            return [];
        }

        return [download];
    }

    private static string BuildSourcesEvent(IReadOnlyList<SourceRef> sources, IReadOnlyList<FormDownloadRef> formDownloads)
    {
        return "[SOURCES]" + JsonSerializer.Serialize(new
        {
            sources,
            formDownloads
        }, JsonOptions);
    }
}
