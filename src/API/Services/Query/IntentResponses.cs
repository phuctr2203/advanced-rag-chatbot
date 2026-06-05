namespace PolicyBot.Api.Services.Query;

public static class IntentResponses
{
    private static readonly Random Random = new();

    private static readonly Dictionary<string, string[]> SmalltalkResponses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] =
        [
            "Hello! I'm ELCA's policy assistant. Feel free to ask me anything about company policies.",
            "Hi there! How can I help you with company policies today?"
        ],
        ["vi"] =
        [
            "Xin chào! Tôi là trợ lý chính sách của ELCA. Bạn có thể hỏi tôi về các chính sách công ty.",
            "Chào bạn! Tôi có thể giúp gì cho bạn về chính sách và quy trình của công ty?"
        ],
        ["fr"] =
        [
            "Bonjour! Je suis l'assistant des politiques d'ELCA. Vous pouvez me poser des questions sur les politiques de l'entreprise.",
            "Bonjour! Comment puis-je vous aider avec les politiques de l'entreprise aujourd'hui?"
        ],
        ["de"] =
        [
            "Hallo! Ich bin der ELCA-Richtlinienassistent. Sie können mich zu Unternehmensrichtlinien befragen.",
            "Guten Tag! Wie kann ich Ihnen heute bei Unternehmensrichtlinien helfen?"
        ]
    };

    private static readonly Dictionary<string, string> OutOfScopeResponses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "I can only answer questions about ELCA company policies and procedures. I don't have information about that topic.",
        ["vi"] = "Tôi chỉ có thể trả lời các câu hỏi về chính sách và quy trình của công ty ELCA. Tôi không có thông tin về chủ đề này.",
        ["fr"] = "Je ne peux répondre qu'aux questions sur les politiques et procédures d'ELCA.",
        ["de"] = "Ich kann nur Fragen zu ELCA-Unternehmensrichtlinien beantworten."
    };

    private static readonly Dictionary<string, string> NoResultsResponses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "I couldn't find relevant information in the company documents for your question. Try rephrasing.",
        ["vi"] = "Tôi không tìm thấy thông tin liên quan trong tài liệu công ty. Hãy thử diễn đạt lại câu hỏi.",
        ["fr"] = "Je n'ai pas trouvé d'informations pertinentes. Essayez de reformuler votre question.",
        ["de"] = "Ich konnte keine relevanten Informationen finden. Versuchen Sie, die Frage umzuformulieren."
    };

    public static string Smalltalk(string language)
    {
        var responses = SmalltalkResponses.TryGetValue(language, out var languageResponses)
            ? languageResponses
            : SmalltalkResponses["en"];

        lock (Random)
        {
            return responses[Random.Next(responses.Length)];
        }
    }

    public static string OutOfScope(string language)
    {
        return Get(OutOfScopeResponses, language);
    }

    public static string NoResults(string language)
    {
        return Get(NoResultsResponses, language);
    }

    private static string Get(IReadOnlyDictionary<string, string> responses, string language)
    {
        return responses.TryGetValue(language, out var response) ? response : responses["en"];
    }
}
