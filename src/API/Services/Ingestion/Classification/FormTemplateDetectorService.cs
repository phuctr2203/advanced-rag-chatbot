using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Ingestion.Classification;

public class FormTemplateDetectorService(ILlmProvider llmProvider, ILogger<FormTemplateDetectorService> logger)
{
    public async Task<FormTemplateDetectionResult> DetectAsync(string fileName, string documentText, CancellationToken ct = default)
    {
        var excerpt = FirstWords(documentText, 300);
        if (string.IsNullOrWhiteSpace(excerpt) && !LooksLikeTemplateName(fileName))
        {
            return new FormTemplateDetectionResult { IsTemplate = false, Reason = "No content or filename signal." };
        }

        var prompt = $"""
            You classify company documents.
            Decide whether this DOCX file is a blank form template that employees fill in, or a policy/procedure document.

            Consider BOTH the filename and content.
            FORM examples: request form, application form, declaration, registration form, reimbursement form, checklist/template with blanks or fields to fill.
            POLICY examples: rules, regulations, procedures, guidelines, announcements, decisions, handbooks.

            Reply with ONLY one word: FORM or POLICY.

            Filename:
            {fileName}

            Document excerpt:
            {excerpt}
            """;

        try
        {
            var response = await llmProvider.CompleteAsync(prompt, maxTokens: 5, ct);
            var normalized = response.Trim().ToUpperInvariant();

            if (normalized.Contains("FORM", StringComparison.Ordinal))
            {
                return new FormTemplateDetectionResult { IsTemplate = true, Reason = "LLM classified as FORM." };
            }

            if (normalized.Contains("POLICY", StringComparison.Ordinal))
            {
                return new FormTemplateDetectionResult { IsTemplate = false, Reason = "LLM classified as POLICY." };
            }

            logger.LogWarning("Unexpected form-template detector response for {FileName}: {Response}", fileName, response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Form-template detector failed for {FileName}. Falling back to filename/content heuristics.", fileName);
        }

        var heuristicResult = LooksLikeTemplateName(fileName) || LooksLikeTemplateContent(excerpt);
        return new FormTemplateDetectionResult
        {
            IsTemplate = heuristicResult,
            Reason = heuristicResult ? "Heuristic classified as FORM." : "Heuristic classified as POLICY."
        };
    }

    private static bool LooksLikeTemplateName(string fileName)
    {
        var value = fileName.ToLowerInvariant();
        return value.Contains("form", StringComparison.Ordinal)
            || value.Contains("template", StringComparison.Ordinal)
            || value.Contains("request", StringComparison.Ordinal)
            || value.Contains("application", StringComparison.Ordinal)
            || value.Contains("declaration", StringComparison.Ordinal)
            || value.Contains("registration", StringComparison.Ordinal);
    }

    private static bool LooksLikeTemplateContent(string excerpt)
    {
        var value = excerpt.ToLowerInvariant();
        return value.Contains("employee name", StringComparison.Ordinal)
            || value.Contains("full name", StringComparison.Ordinal)
            || value.Contains("signature", StringComparison.Ordinal)
            || value.Contains("date:", StringComparison.Ordinal)
            || value.Contains("approved by", StringComparison.Ordinal)
            || value.Contains("requested by", StringComparison.Ordinal)
            || value.Contains("please fill", StringComparison.Ordinal);
    }

    private static string FirstWords(string text, int maxWords)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Take(maxWords));
    }
}
