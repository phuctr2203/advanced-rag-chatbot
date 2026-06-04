using System.Text.Json;
using System.Text.Json.Serialization;
using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion.Classification;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Ingestion.Forms;

public class FormMentionExtractorService(
    ILlmProvider llmProvider,
    ConfiguredPathResolver pathResolver,
    ILogger<FormMentionExtractorService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task ExtractPdfFormMentionsAsync(
        string sourceFile,
        IReadOnlyList<ParsedChunk> chunks,
        string agent,
        CancellationToken ct = default)
    {
        var fullDocumentText = string.Join(Environment.NewLine, chunks
            .Where(chunk => chunk.ChunkType.Equals("text", StringComparison.OrdinalIgnoreCase))
            .Select(chunk => chunk.Text));

        if (string.IsNullOrWhiteSpace(fullDocumentText))
        {
            return;
        }

        var prompt = $$"""
            You are analyzing a company policy document.
            Extract all form names or template names mentioned in this document.
            For each form found, provide:
            - The official form name in English
            - All aliases or alternative names used (in any language found in the document)

            Respond in JSON only. No explanation. Format:
            [
              {
                "form_name": "Payment Request Form",
                "aliases": ["payment request", "de nghi thanh toan", "giay de nghi thanh toan"]
              }
            ]

            Document text:
            {{fullDocumentText}}
            """;

        var response = await llmProvider.CompleteAsync(prompt, maxTokens: 500, ct);
        var json = StripMarkdownFences(response);

        List<FormMention>? mentions;
        try
        {
            mentions = JsonSerializer.Deserialize<List<FormMention>>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "[FormExtractor] Failed to parse form mentions for {SourceFile}.", sourceFile);
            return;
        }

        if (mentions is null || mentions.Count == 0)
        {
            logger.LogInformation("[FormExtractor] Found 0 form mentions in {SourceFile}.", sourceFile);
            return;
        }

        var draftEntries = mentions
            .Where(mention => !string.IsNullOrWhiteSpace(mention.FormName))
            .Select(mention => new FormRegistryDraftEntry
            {
                FormName = mention.FormName.Trim(),
                Aliases = (mention.Aliases ?? [])
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Agent = agent
            })
            .ToList();

        if (draftEntries.Count == 0)
        {
            return;
        }

        var draftPath = GetDraftPath();
        Directory.CreateDirectory(Path.GetDirectoryName(draftPath)!);

        var existingEntries = await ReadExistingDraftEntriesAsync(draftPath, ct);
        foreach (var entry in draftEntries)
        {
            UpsertDraftEntry(existingEntries, entry);
        }

        await File.WriteAllTextAsync(draftPath, JsonSerializer.Serialize(existingEntries, JsonOptions), ct);

        logger.LogInformation(
            "[FormExtractor] Found {Count} form mentions in {SourceFile}. Draft written to data/form-registry-draft.json - review and link docx_file manually.",
            draftEntries.Count,
            sourceFile);
    }

    private async Task<List<FormRegistryDraftEntry>> ReadExistingDraftEntriesAsync(string draftPath, CancellationToken ct)
    {
        if (!File.Exists(draftPath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(draftPath);
            return await JsonSerializer.DeserializeAsync<List<FormRegistryDraftEntry>>(stream, JsonOptions, ct) ?? [];
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "[FormExtractor] Existing draft registry could not be parsed; replacing draft.");
            return [];
        }
    }

    private static void UpsertDraftEntry(List<FormRegistryDraftEntry> entries, FormRegistryDraftEntry entry)
    {
        var existing = entries.FirstOrDefault(candidate =>
            candidate.FormName.Equals(entry.FormName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            entries.Add(entry);
            return;
        }

        existing.Agent = entry.Agent;
        existing.Aliases = (existing.Aliases ?? [])
            .Concat(entry.Aliases ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetDraftPath()
    {
        return Path.Combine(pathResolver.DataPath, "form-registry-draft.json");
    }

    private static string StripMarkdownFences(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0)
        {
            return trimmed.Trim('`').Trim();
        }

        var content = trimmed[(firstNewLine + 1)..];
        var lastFence = content.LastIndexOf("```", StringComparison.Ordinal);
        return (lastFence >= 0 ? content[..lastFence] : content).Trim();
    }

    private class FormMention
    {
        [JsonPropertyName("form_name")]
        public string FormName { get; set; } = string.Empty;

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; } = [];
    }

}
