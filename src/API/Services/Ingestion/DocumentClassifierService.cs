using System.Text.RegularExpressions;
using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Ingestion;

public class DocumentClassifierService(ILlmProvider llmProvider, ILogger<DocumentClassifierService> logger)
{
    public const string DefaultAgent = "ELCA_GENERAL";
    private static readonly string[] ValidAgentNames =
    [
        "ELCA_HR",
        "ELCA_GENERAL",
        "CII_TOWER_SUPPORT"
    ];

    public static bool IsValidAgent(string? agent)
    {
        return NormalizeAgent(agent) is not null;
    }

    public async Task<string> DetermineAgentAsync(IReadOnlyList<ParsedChunk> chunks, string? requestedAgent, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(requestedAgent))
        {
            var normalizedAgent = NormalizeAgent(requestedAgent);
            if (normalizedAgent is null)
            {
                throw new ArgumentException($"Invalid agent '{requestedAgent}'. Valid values: {string.Join(", ", ValidAgentNames)}.", nameof(requestedAgent));
            }

            logger.LogInformation("[DocumentClassifier] Using manual agent: {Agent}", normalizedAgent);
            return normalizedAgent;
        }

        var excerpt = FirstWords(string.Join(' ', chunks.Select(chunk => chunk.Text)), 500);
        if (string.IsNullOrWhiteSpace(excerpt))
        {
            logger.LogInformation("[DocumentClassifier] Empty document excerpt; defaulting to {Agent}", DefaultAgent);
            return DefaultAgent;
        }

        var prompt = $$"""
            You are a document classifier for ELCA company.
            Classify the document excerpt into exactly one category:
            - ELCA_HR: HR policies, leave, recruitment, benefits, employee conduct, salary
            - ELCA_GENERAL: Company general policies, IT, security, operations, finance
            - CII_TOWER_SUPPORT: CII Tower building, facility, support services, maintenance
            Respond with ONLY the category name. Nothing else.

            Document excerpt:
            {{excerpt}}
            """;

        var response = await llmProvider.CompleteAsync(prompt, maxTokens: 10, ct);
        var agent = NormalizeAgent(response) ?? DefaultAgent;

        if (agent == DefaultAgent && !DefaultAgent.Equals(CleanAgent(response), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[DocumentClassifier] Unexpected classifier response '{Response}'; defaulting to {Agent}", response, agent);
        }

        logger.LogInformation("[DocumentClassifier] Chosen category: {Agent}", agent);
        return agent;
    }

    public static void ApplyAgent(IReadOnlyList<ParsedChunk> chunks, string agent)
    {
        foreach (var chunk in chunks)
        {
            chunk.Agent = agent;
        }
    }

    private static string? NormalizeAgent(string? agent)
    {
        var cleaned = CleanAgent(agent);
        return ValidAgentNames.FirstOrDefault(validAgent => validAgent.Equals(cleaned, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanAgent(string? agent)
    {
        return (agent ?? string.Empty).Trim().Trim('`', '"', '\'', '.', ',', ':', ';');
    }

    private static string FirstWords(string text, int wordCount)
    {
        return string.Join(' ', Regex.Matches(text, @"\S+").Select(match => match.Value).Take(wordCount));
    }
}
