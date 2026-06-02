using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public class EvaluationQueryService(
    LanguageDetectionService languageDetection,
    IntentClassifierService intentClassifier,
    VectorStoreService vectorStore,
    PromptBuilderService promptBuilder,
    LlmService llmService,
    SourceCitationParser citationParser,
    FormRegistryService formRegistry,
    NoAnswerDetectorService noAnswerDetector)
{
    public async Task<EvaluationQueryResponse> QueryAsync(string question, CancellationToken ct = default)
    {
        var message = question.Trim();
        var languageResult = await languageDetection.DetectAsync(message, ct);
        var intent = await intentClassifier.ClassifyAsync(message, ct);
        var response = CreateResponse(message, languageResult, intent);

        if (intent == QueryIntent.Smalltalk)
        {
            response.Answer = IntentResponses.Smalltalk(languageResult.Language);
            return response;
        }

        if (intent == QueryIntent.OutOfScope)
        {
            response.Answer = IntentResponses.OutOfScope(languageResult.Language);
            return response;
        }

        response.RetrievalRan = true;
        var searchResults = await vectorStore.SearchAsync(message, limit: 6, ct);
        response.RetrievedContexts = searchResults
            .Select(ToRetrievedContext)
            .ToList();

        if (searchResults.Count == 0)
        {
            response.Answer = IntentResponses.NoResults(languageResult.Language);
            response.NoAnswer = true;
            return response;
        }

        var prompt = promptBuilder.Build(message, searchResults, languageResult.Language);
        response.Answer = await llmService.CompleteAsync(prompt, maxTokens: 2000, ct);
        response.NoAnswer = noAnswerDetector.IsNoAnswer(response.Answer);
        if (response.NoAnswer)
        {
            return response;
        }

        response.Sources = citationParser.Parse(response.Answer, searchResults);
        response.FormDownloads = BuildDownloadRefs(response.Answer, response.Sources);
        return response;
    }

    private List<FormDownloadRef> BuildDownloadRefs(string answer, IReadOnlyList<SourceRef> sources)
    {
        var download = formRegistry.FindByAlias(answer);
        if (download is null || sources.Any(source => source.FormDownload?.FormName == download.FormName))
        {
            return [];
        }

        return [download];
    }

    private static EvaluationQueryResponse CreateResponse(
        string question,
        LanguageDetectionResult languageResult,
        QueryIntent intent)
    {
        return new EvaluationQueryResponse
        {
            Question = question,
            Language = languageResult.Language,
            LanguageDetectionMethod = languageResult.Method,
            LanguageDetectionConfidence = languageResult.Confidence,
            Intent = ToIntentName(intent)
        };
    }

    private static EvaluationRetrievedContext ToRetrievedContext(VectorSearchResult result)
    {
        var chunk = result.Chunk;
        return new EvaluationRetrievedContext
        {
            Text = chunk.Text,
            SourceFile = chunk.SourceFile,
            Page = chunk.PageNumber,
            ChunkIndex = chunk.ChunkIndex,
            ChunkType = chunk.ChunkType,
            Agent = chunk.Agent,
            ImagePath = chunk.ImagePath,
            IsFormTemplate = chunk.IsFormTemplate,
            Score = result.Score
        };
    }

    private static string ToIntentName(QueryIntent intent)
    {
        return intent switch
        {
            QueryIntent.Smalltalk => "SMALLTALK",
            QueryIntent.OutOfScope => "OUT_OF_SCOPE",
            _ => "POLICY_QUERY"
        };
    }
}
