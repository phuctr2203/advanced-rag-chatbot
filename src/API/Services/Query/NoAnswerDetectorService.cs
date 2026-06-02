namespace PolicyBot.Api.Services.Query;

public class NoAnswerDetectorService
{
    private static readonly string[] NoAnswerPatterns =
    [
        "provided context does not contain",
        "provided documents do not contain",
        "provided context does not describe",
        "provided documents do not describe",
        "context does not contain",
        "documents do not specify",
        "does not specify",
        "couldn't find relevant",
        "could not find relevant",
        "no relevant information",
        "no information",
        "không tìm thấy",
        "không có thông tin",
        "tài liệu không",
        "ngữ cảnh không",
        "không nêu",
        "không đề cập",
        "je n'ai pas trouvé",
        "je n’ai pas trouvé",
        "ne contient pas",
        "aucune information",
        "konnte keine",
        "enthält keine",
        "keine relevanten"
    ];

    public bool IsNoAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }

        var normalized = answer.Trim().ToLowerInvariant();
        return NoAnswerPatterns.Any(pattern => normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
