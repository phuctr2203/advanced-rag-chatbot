using System.Text;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public class PromptBuilderService
{
    public string Build(string userQuery, IReadOnlyList<ScoredChunk> chunks, string language)
    {
        var languageName = GetLanguageName(language);
        var context = string.Join(
            Environment.NewLine + Environment.NewLine,
            chunks.Select(result =>
                $"Source: {result.Chunk.SourceFile} (page {result.Chunk.PageNumber}){Environment.NewLine}{result.Chunk.Text}"));

        var prompt = new StringBuilder();
        prompt.AppendLine("You are a helpful assistant for ELCA company policy questions.");
        prompt.AppendLine("Answer ONLY based on the provided context. Do not use outside knowledge.");
        prompt.AppendLine("Always respond in the same language as the user's question.");
        prompt.AppendLine($"The detected user language is {languageName} ({language}).");
        prompt.AppendLine($"You MUST write the full answer in {languageName}. Do not answer in another language.");
        prompt.AppendLine("Supported languages: English (en), Vietnamese (vi), French (fr), German (de).");
        prompt.AppendLine();
        prompt.AppendLine("After your answer, list the sources on a new line in this exact format:");
        prompt.AppendLine("SOURCES: filename.pdf (page N), filename2.docx (page M)");
        prompt.AppendLine();
        prompt.AppendLine("Context:");
        prompt.AppendLine(context);
        prompt.AppendLine();
        prompt.AppendLine($"Question: {userQuery}");

        return prompt.ToString();
    }

    private static string GetLanguageName(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "vi" => "Vietnamese",
            "fr" => "French",
            "de" => "German",
            _ => "English"
        };
    }
}
