using System.Diagnostics;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public class EvaluationChatOrchestrator(
    LanguageDetectionService languageDetectionService,
    IntentClassifierService intentClassifierService,
    DenseRerankSearchService denseRerankSearchService,
    PromptBuilderService promptBuilderService,
    LlmService llmService,
    SourceCitationParser sourceCitationParser,
    FormDownloadEnrichmentService formDownloadEnrichmentService)
{
    private const float RagAnswerTemperature = 0.1f;

    public async Task<EvaluationChatResponse> CompleteAsync(
        EvaluationChatRequest request,
        CancellationToken ct = default)
    {
        var total = Stopwatch.StartNew();
        var message = request.Message.Trim();
        var timings = new EvaluationTimings();

        var step = Stopwatch.StartNew();
        var language = await languageDetectionService.DetectAsync(message, ct);
        timings.LanguageDetection = step.ElapsedMilliseconds;

        step.Restart();
        var intent = await intentClassifierService.ClassifyAsync(message, ct);
        timings.IntentClassification = step.ElapsedMilliseconds;

        if (intent == QueryIntent.Smalltalk)
        {
            timings.Total = total.ElapsedMilliseconds;
            return BuildResponse(
                message,
                IntentResponses.Smalltalk(language.Language),
                language,
                intent,
                [],
                [],
                [],
                timings,
                prompt: null);
        }

        if (intent == QueryIntent.OutOfScope)
        {
            timings.Total = total.ElapsedMilliseconds;
            return BuildResponse(
                message,
                IntentResponses.OutOfScope(language.Language),
                language,
                intent,
                [],
                [],
                [],
                timings,
                prompt: null);
        }

        step.Restart();
        var searchResults = await denseRerankSearchService.SearchAsync(message, request.TopK, ct);
        timings.Retrieval = step.ElapsedMilliseconds;

        if (searchResults.Count == 0)
        {
            timings.Total = total.ElapsedMilliseconds;
            return BuildResponse(
                message,
                IntentResponses.NoResults(language.Language),
                language,
                intent,
                [],
                [],
                [],
                timings,
                prompt: null);
        }

        var prompt = promptBuilderService.Build(message, searchResults, language.Language);

        step.Restart();
        var answerTokens = new List<string>();
        await foreach (var token in llmService.StreamAsync(prompt, RagAnswerTemperature, ct))
        {
            answerTokens.Add(token);
        }

        var answer = string.Concat(answerTokens);
        timings.Generation = step.ElapsedMilliseconds;

        step.Restart();
        var sources = sourceCitationParser.Parse(answer, searchResults);
        var formDownloads = formDownloadEnrichmentService.FindDownloadRefs(answer, sources);
        timings.CitationParsing = step.ElapsedMilliseconds;
        timings.Total = total.ElapsedMilliseconds;

        return BuildResponse(
            message,
            answer,
            language,
            intent,
            searchResults,
            sources,
            formDownloads,
            timings,
            request.IncludePrompt ? prompt : null);
    }

    private static EvaluationChatResponse BuildResponse(
        string question,
        string answer,
        LanguageDetectionResult language,
        QueryIntent intent,
        IReadOnlyList<ScoredChunk> searchResults,
        IReadOnlyList<SourceRef> sources,
        IReadOnlyList<FormDownloadRef> formDownloads,
        EvaluationTimings timings,
        string? prompt)
    {
        return new EvaluationChatResponse
        {
            Question = question,
            Answer = answer,
            Language = new LanguageDetectionResultDto
            {
                Language = language.Language,
                Method = language.Method,
                Confidence = language.Confidence
            },
            Intent = intent.ToString(),
            RetrievedContexts = searchResults
                .Select((result, index) => new EvaluationRetrievedContext
                {
                    Rank = index + 1,
                    Text = result.Chunk.Text,
                    Score = result.Score,
                    SourceFile = result.Chunk.SourceFile,
                    Page = result.Chunk.PageNumber,
                    ChunkIndex = result.Chunk.ChunkIndex,
                    ChunkType = result.Chunk.ChunkType,
                    FileType = result.Chunk.FileType,
                    Agent = result.Chunk.Agent,
                    ImagePath = result.Chunk.ImagePath,
                    ImagePaths = result.Chunk.ImagePaths,
                    IsFormTemplate = result.Chunk.IsFormTemplate,
                    TemplatePath = result.Chunk.TemplatePath
                })
                .ToList(),
            Sources = sources,
            FormDownloads = formDownloads,
            TimingsMs = timings,
            Prompt = prompt
        };
    }
}
