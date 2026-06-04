using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Ingestion.Forms;

public class FormRegistrySuggestionService(
    ILlmProvider llmProvider,
    ConfiguredPathResolver pathResolver,
    ILogger<FormRegistrySuggestionService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<FormRegistrySuggestion?> GenerateSuggestionAsync(
        StoredTemplate template,
        IReadOnlyList<ParsedChunk> templateChunks,
        string agent,
        CancellationToken ct = default)
    {
        var draftEntries = await ReadDraftEntriesAsync(ct);
        if (draftEntries.Count == 0)
        {
            logger.LogInformation("[FormMapping] No draft form registry entries available for template {Template}.", template.StoredFileName);
            return null;
        }

        var candidates = draftEntries
            .Where(entry => string.IsNullOrWhiteSpace(entry.Agent) || entry.Agent.Equals(agent, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = draftEntries;
        }

        var templateExcerpt = FirstWords(
            string.Join(' ', templateChunks.Where(chunk => chunk.ChunkType.Equals("text", StringComparison.OrdinalIgnoreCase)).Select(chunk => chunk.Text)),
            500);

        var llmResult = await AskLlmForMatchAsync(template, templateExcerpt, candidates, ct)
            ?? BuildHeuristicMatch(template, templateExcerpt, candidates);
        if (llmResult is null || string.IsNullOrWhiteSpace(llmResult.MatchedFormName))
        {
            return null;
        }

        var matchedEntry = candidates.FirstOrDefault(candidate =>
            candidate.FormName.Equals(llmResult.MatchedFormName, StringComparison.OrdinalIgnoreCase));
        if (matchedEntry is null)
        {
            logger.LogWarning(
                "[FormMapping] LLM returned unknown form '{FormName}' for template {Template}.",
                llmResult.MatchedFormName,
                template.StoredFileName);
            return null;
        }

        var confidence = Math.Clamp(llmResult.Confidence, 0.0, 1.0);
        if (confidence < 0.60)
        {
            logger.LogInformation(
                "[FormMapping] Match confidence {Confidence} below threshold for template {Template}; leaving unmapped.",
                confidence,
                template.StoredFileName);
            return null;
        }

        var suggestion = new FormRegistrySuggestion
        {
            Id = CreateSuggestionId(matchedEntry.FormName, template.StoredFileName),
            FormName = matchedEntry.FormName,
            Aliases = matchedEntry.Aliases,
            CandidateTemplateFile = template.StoredFileName,
            CandidateDownloadPath = template.UrlPath,
            Agent = string.IsNullOrWhiteSpace(matchedEntry.Agent) ? agent : matchedEntry.Agent,
            Confidence = confidence,
            Reason = llmResult.Reason,
            Status = confidence >= 0.85
                ? FormRegistrySuggestionStatuses.PendingReview
                : FormRegistrySuggestionStatuses.NeedsManualReview
        };

        await UpsertSuggestionAsync(suggestion, ct);

        logger.LogInformation(
            "[FormMapping] Suggested {Template} for {FormName} with confidence {Confidence:P0}.",
            template.StoredFileName,
            suggestion.FormName,
            suggestion.Confidence);

        return suggestion;
    }

    public async Task<IReadOnlyList<FormRegistrySuggestion>> GetSuggestionsAsync(CancellationToken ct = default)
    {
        return await ReadSuggestionsAsync(ct);
    }

    public async Task<FormRegistrySuggestion?> AcceptAsync(string id, CancellationToken ct = default)
    {
        var suggestions = await ReadSuggestionsAsync(ct);
        var suggestion = suggestions.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (suggestion is null)
        {
            return null;
        }

        suggestion.Status = FormRegistrySuggestionStatuses.Accepted;
        await WriteSuggestionsAsync(suggestions, ct);
        await UpsertRegistryEntryAsync(suggestion, ct);
        return suggestion;
    }

    public async Task<FormRegistrySuggestion?> RejectAsync(string id, CancellationToken ct = default)
    {
        var suggestions = await ReadSuggestionsAsync(ct);
        var suggestion = suggestions.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (suggestion is null)
        {
            return null;
        }

        suggestion.Status = FormRegistrySuggestionStatuses.Rejected;
        await WriteSuggestionsAsync(suggestions, ct);
        return suggestion;
    }

    public async Task<FormRegistrySuggestion?> ChooseTemplateAsync(string id, ChooseTemplateRequest request, CancellationToken ct = default)
    {
        var suggestions = await ReadSuggestionsAsync(ct);
        var suggestion = suggestions.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (suggestion is null)
        {
            return null;
        }

        suggestion.CandidateTemplateFile = request.CandidateTemplateFile.Trim();
        suggestion.CandidateDownloadPath = request.CandidateDownloadPath.Trim();
        suggestion.Reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "User selected a different template."
            : request.Reason.Trim();
        suggestion.Confidence = 1.0;
        suggestion.Status = FormRegistrySuggestionStatuses.PendingReview;

        await WriteSuggestionsAsync(suggestions, ct);
        return suggestion;
    }

    private async Task<LlmMappingResult?> AskLlmForMatchAsync(
        StoredTemplate template,
        string templateExcerpt,
        IReadOnlyList<FormRegistryDraftEntry> candidates,
        CancellationToken ct)
    {
        var candidateText = string.Join(Environment.NewLine + Environment.NewLine, candidates.Select((candidate, index) =>
            $"{index + 1}. Form name: {candidate.FormName}{Environment.NewLine}   Aliases: {string.Join(", ", candidate.Aliases)}{Environment.NewLine}   Agent: {candidate.Agent}"));

        var prompt = $$"""
            You are linking an uploaded form template to exactly one form mentioned in a company policy.

            CRITICAL OUTPUT RULES:
            - Return ONLY one valid JSON object.
            - Do not use markdown fences.
            - Do not add explanations before or after the JSON.
            - Use double quotes for all JSON property names and string values.
            - Use null without quotes when there is no match.
            - confidence must be a number between 0 and 1.
            - status must be either "pending_review" or "needs_manual_review".
            - matched_form_name must exactly match one candidate Form name, or null.

            Template filename:
            {{template.OriginalFileName}}

            Template excerpt:
            {{templateExcerpt}}

            Candidate form mentions:
            {{candidateText}}

            If one candidate matches, return exactly this JSON shape:
            {
              "matched_form_name": "Payment Request Form",
              "confidence": 0.93,
              "reason": "Filename and template content both match Payment Request Form.",
              "status": "pending_review"
            }

            If no candidate matches, return exactly this JSON shape:
            {
              "matched_form_name": null,
              "confidence": 0.0,
              "reason": "No strong match found",
              "status": "needs_manual_review"
            }
            """;

        try
        {
            var response = await llmProvider.CompleteAsync(prompt, maxTokens: 300, ct);
            var json = ExtractJsonObject(StripMarkdownFences(response));
            var result = JsonSerializer.Deserialize<LlmMappingResult>(json, JsonOptions);
            if (result is null || string.IsNullOrWhiteSpace(result.MatchedFormName))
            {
                logger.LogInformation(
                    "[FormMapping] LLM did not return a matched form for {Template}. Response: {Response}",
                    template.StoredFileName,
                    response);
            }

            return result;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[FormMapping] LLM response for {Template} was not valid mapping JSON.", template.StoredFileName);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "[FormMapping] LLM request failed for {Template}.", template.StoredFileName);
            return null;
        }
    }

    private static LlmMappingResult? BuildHeuristicMatch(
        StoredTemplate template,
        string templateExcerpt,
        IReadOnlyList<FormRegistryDraftEntry> candidates)
    {
        var haystack = $"{template.OriginalFileName} {template.StoredFileName} {templateExcerpt}";
        var bestScore = 0.0;
        FormRegistryDraftEntry? bestEntry = null;

        foreach (var candidate in candidates)
        {
            var labels = new[] { candidate.FormName }
                .Concat(candidate.Aliases)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var score = labels.Select(label => ScoreSimilarity(haystack, label)).DefaultIfEmpty(0).Max();
            if (score > bestScore)
            {
                bestScore = score;
                bestEntry = candidate;
            }
        }

        if (bestEntry is null || bestScore < 0.60)
        {
            return null;
        }

        return new LlmMappingResult
        {
            MatchedFormName = bestEntry.FormName,
            Confidence = Math.Min(0.89, bestScore),
            Reason = "LLM did not return a usable mapping; filename/template text similarity suggested this match."
        };
    }

    private static double ScoreSimilarity(string haystack, string label)
    {
        var normalizedHaystack = Normalize(haystack);
        var normalizedLabel = Normalize(label);
        if (normalizedLabel.Length == 0)
        {
            return 0;
        }

        if (normalizedHaystack.Contains(normalizedLabel, StringComparison.Ordinal))
        {
            return 0.90;
        }

        var haystackTokens = Tokenize(normalizedHaystack).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labelTokens = Tokenize(normalizedLabel).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (haystackTokens.Count == 0 || labelTokens.Count == 0)
        {
            return 0;
        }

        var overlap = labelTokens.Count(token => haystackTokens.Contains(token));
        return overlap / (double)labelTokens.Count;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Regex.Matches(value, @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .Where(token => token.Length > 1);
    }

    private async Task<List<FormRegistryDraftEntry>> ReadDraftEntriesAsync(CancellationToken ct)
    {
        var path = GetDraftPath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<FormRegistryDraftEntry>>(stream, JsonOptions, ct) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[FormMapping] Draft registry could not be parsed.");
            return [];
        }
    }

    private async Task<List<FormRegistrySuggestion>> ReadSuggestionsAsync(CancellationToken ct)
    {
        var path = GetSuggestionsPath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<FormRegistrySuggestion>>(stream, JsonOptions, ct) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[FormMapping] Suggestion registry could not be parsed; replacing suggestions.");
            return [];
        }
    }

    private async Task UpsertSuggestionAsync(FormRegistrySuggestion suggestion, CancellationToken ct)
    {
        var suggestions = await ReadSuggestionsAsync(ct);
        var existing = suggestions.FirstOrDefault(candidate => candidate.Id.Equals(suggestion.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            suggestions.Add(suggestion);
        }
        else
        {
            existing.FormName = suggestion.FormName;
            existing.Aliases = suggestion.Aliases;
            existing.CandidateTemplateFile = suggestion.CandidateTemplateFile;
            existing.CandidateDownloadPath = suggestion.CandidateDownloadPath;
            existing.Agent = suggestion.Agent;
            existing.Confidence = suggestion.Confidence;
            existing.Reason = suggestion.Reason;
            existing.Status = suggestion.Status;
        }

        await WriteSuggestionsAsync(suggestions, ct);
    }

    private async Task WriteSuggestionsAsync(IReadOnlyList<FormRegistrySuggestion> suggestions, CancellationToken ct)
    {
        var path = GetSuggestionsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(suggestions, JsonOptions), ct);
    }

    private async Task UpsertRegistryEntryAsync(FormRegistrySuggestion suggestion, CancellationToken ct)
    {
        var path = GetRegistryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var entries = await ReadRegistryEntriesAsync(path, ct);
        var existing = entries.FirstOrDefault(candidate =>
            candidate.FormName.Equals(suggestion.FormName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            entries.Add(new FormRegistryEntry
            {
                FormName = suggestion.FormName,
                Aliases = suggestion.Aliases,
                DocxFile = suggestion.CandidateTemplateFile,
                DownloadPath = suggestion.CandidateDownloadPath,
                Agent = suggestion.Agent
            });
        }
        else
        {
            existing.Aliases = existing.Aliases
                .Concat(suggestion.Aliases)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            existing.DocxFile = suggestion.CandidateTemplateFile;
            existing.DownloadPath = suggestion.CandidateDownloadPath;
            existing.Agent = suggestion.Agent;
        }

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(entries, JsonOptions), ct);
    }

    private async Task<List<FormRegistryEntry>> ReadRegistryEntriesAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<FormRegistryEntry>>(stream, JsonOptions, ct) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[FormMapping] Final form registry could not be parsed; replacing registry.");
            return [];
        }
    }

    private string GetDraftPath()
    {
        return Path.Combine(pathResolver.DataPath, "form-registry-draft.json");
    }

    private string GetSuggestionsPath()
    {
        return Path.Combine(pathResolver.DataPath, "form-registry-suggestions.json");
    }

    private string GetRegistryPath()
    {
        return Path.Combine(pathResolver.DataPath, "form-registry.json");
    }

    private static string CreateSuggestionId(string formName, string templateFile)
    {
        var input = $"{Normalize(formName)}|{Normalize(templateFile)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static string FirstWords(string text, int maxWords)
    {
        return string.Join(' ', Regex.Matches(text, @"\S+").Select(match => match.Value).Take(maxWords));
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

    private static string ExtractJsonObject(string value)
    {
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new JsonException("LLM response did not contain a JSON object.");
        }

        return value[start..(end + 1)];
    }

    private class LlmMappingResult
    {
        [JsonPropertyName("matched_form_name")]
        public string? MatchedFormName { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
