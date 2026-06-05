using System.Runtime.CompilerServices;
using System.Text.Json;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public class ChatOrchestrator(
    LanguageDetectionService languageDetectionService,
    IntentClassifierService intentClassifierService,
    IVectorStoreService vectorStoreService,
    PromptBuilderService promptBuilderService,
    LlmService llmService,
    SourceCitationParser sourceCitationParser,
    FormDownloadEnrichmentService formDownloadEnrichmentService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var message = request.Message.Trim();
        var language = await languageDetectionService.DetectAsync(message, ct);
        var intent = await intentClassifierService.ClassifyAsync(message, ct);

        if (intent == QueryIntent.Smalltalk)
        {
            yield return IntentResponses.Smalltalk(language.Language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        if (intent == QueryIntent.OutOfScope)
        {
            yield return IntentResponses.OutOfScope(language.Language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        var searchResults = await vectorStoreService.SearchAsync(message, limit: 6, ct);
        if (searchResults.Count == 0)
        {
            yield return IntentResponses.NoResults(language.Language);
            yield return BuildSourcesEvent([], []);
            yield break;
        }

        var prompt = promptBuilderService.Build(message, searchResults, language.Language);
        var answerTokens = new List<string>();
        await foreach (var token in llmService.StreamAsync(prompt, ct))
        {
            answerTokens.Add(token);
            yield return token;
        }

        var fullAnswerText = string.Concat(answerTokens);
        var sources = sourceCitationParser.Parse(fullAnswerText, searchResults);
        var formDownloads = formDownloadEnrichmentService.FindDownloadRefs(fullAnswerText, sources);
        yield return BuildSourcesEvent(sources, formDownloads);
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
