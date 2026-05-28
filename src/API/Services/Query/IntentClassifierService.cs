namespace PolicyBot.Api.Services.Query;

public class IntentClassifierService(LlmService llmService)
{
    private static readonly HashSet<string> SmalltalkExactMatches = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "hello", "hey", "thanks", "thank you", "bye", "goodbye", "ok", "okay", "sure", "great",
        "chào", "xin chào", "cảm ơn", "tạm biệt", "ổn", "được", "vâng", "dạ",
        "bonjour", "salut", "merci", "au revoir", "bonsoir", "d'accord",
        "hallo", "guten tag", "danke", "tschüss", "auf wiedersehen", "gut"
    };

    private static readonly string[] PolicyKeywords =
    [
        "policy", "policies", "leave", "annual", "sick", "overtime", "salary", "benefit",
        "allowance", "procedure", "form", "request", "approval", "hr", "facility", "cii", "tower",
        "chính sách", "nghỉ phép", "lương", "phúc lợi", "quy trình", "tăng ca", "biểu mẫu",
        "politique", "congé", "salaire", "avantage", "procédure", "formulaire",
        "richtlinie", "urlaub", "gehalt", "verfahren", "überstunden", "formular"
    ];

    public async Task<QueryIntent> ClassifyAsync(string message, CancellationToken ct = default)
    {
        var trimmed = message.Trim();
        var fastPath = ClassifyFastPath(trimmed);
        if (fastPath is not null)
        {
            return fastPath.Value;
        }

        var response = await llmService.CompleteAsync(BuildPrompt(trimmed), maxTokens: 10, ct);
        return ParseIntent(response);
    }

    private static QueryIntent? ClassifyFastPath(string message)
    {
        if (SmalltalkExactMatches.Contains(message))
        {
            return QueryIntent.Smalltalk;
        }

        if (message.Length < 10)
        {
            return QueryIntent.Smalltalk;
        }

        if (PolicyKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return QueryIntent.PolicyQuery;
        }

        return null;
    }

    private static string BuildPrompt(string message)
    {
        return $"""
            Classify the user message into exactly one category:
            - SMALLTALK: greetings, thanks, casual conversation
            - POLICY_QUERY: any question about company policies, HR, leave, overtime,
              salary, benefits, IT, facilities, procedures, forms, CII Tower
            - OUT_OF_SCOPE: questions unrelated to company policies

            When in doubt between POLICY_QUERY and OUT_OF_SCOPE, choose POLICY_QUERY.
            Respond with ONLY the category name.

            User message: {message}
            """;
    }

    private static QueryIntent ParseIntent(string response)
    {
        var normalized = response.Trim().ToUpperInvariant();
        return normalized switch
        {
            "SMALLTALK" => QueryIntent.Smalltalk,
            "OUT_OF_SCOPE" => QueryIntent.OutOfScope,
            "POLICY_QUERY" => QueryIntent.PolicyQuery,
            _ => QueryIntent.PolicyQuery
        };
    }
}
