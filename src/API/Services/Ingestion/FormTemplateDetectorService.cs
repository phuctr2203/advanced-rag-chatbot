using System.Text.RegularExpressions;
using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Ingestion;

public class FormTemplateDetectorService(ILlmProvider llmProvider, ILogger<FormTemplateDetectorService> logger)
{
    public async Task<bool> DetectAsync(string sourceFile, IReadOnlyList<ParsedChunk> chunks, CancellationToken ct = default)
    {
        var excerpt = FirstWords(string.Join(' ', chunks
            .Where(chunk => chunk.ChunkType.Equals("text", StringComparison.OrdinalIgnoreCase))
            .Select(chunk => chunk.Text)), 300);

        if (string.IsNullOrWhiteSpace(excerpt))
        {
            logger.LogInformation("[FormDetector] {SourceFile} detected as: POLICY", sourceFile);
            return false;
        }

        var prompt = $$"""
            Is this document a blank form template that employees fill in,
            or is it a policy/procedure document?
            Reply with ONLY one word: FORM or POLICY

            Document excerpt:
            {{excerpt}}
            """;

        var response = (await llmProvider.CompleteAsync(prompt, maxTokens: 5, ct)).Trim();
        var isForm = response.Equals("FORM", StringComparison.OrdinalIgnoreCase);
        var label = isForm ? "FORM" : "POLICY";

        if (!isForm && !response.Equals("POLICY", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[FormDetector] Unexpected response '{Response}' for {SourceFile}; defaulting to POLICY.", response, sourceFile);
        }

        logger.LogInformation("[FormDetector] {SourceFile} detected as: {Label}", sourceFile, label);
        return isForm;
    }

    public static void ApplyFormTemplateTag(IReadOnlyList<ParsedChunk> chunks, bool isFormTemplate)
    {
        foreach (var chunk in chunks)
        {
            chunk.IsFormTemplate = isFormTemplate;
        }
    }

    private static string FirstWords(string text, int wordCount)
    {
        return string.Join(' ', Regex.Matches(text, @"\S+").Select(match => match.Value).Take(wordCount));
    }
}
