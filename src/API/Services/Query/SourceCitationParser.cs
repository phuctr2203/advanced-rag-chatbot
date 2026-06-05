using System.Text.RegularExpressions;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public partial class SourceCitationParser
{
    public List<SourceRef> Parse(string response, IReadOnlyList<ScoredChunk> searchResults)
    {
        var sourceLine = response
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault(line => line.TrimStart().StartsWith("SOURCES:", StringComparison.OrdinalIgnoreCase));

        if (sourceLine is null)
        {
            return [];
        }

        var sources = new List<SourceRef>();
        foreach (Match match in CitationRegex().Matches(sourceLine))
        {
            var file = match.Groups["file"].Value.Trim();
            var page = int.TryParse(match.Groups["page"].Value, out var parsedPage) ? parsedPage : 0;
            var result = FindMatchingResult(searchResults, file, page);
            if (result is null)
            {
                sources.Add(new SourceRef { File = file, Page = page });
                continue;
            }

            var chunk = result.Chunk;
            sources.Add(new SourceRef
            {
                File = chunk.SourceFile,
                Page = chunk.PageNumber,
                ChunkType = chunk.ChunkType,
                ImagePath = chunk.ChunkType.Equals("image_caption", StringComparison.OrdinalIgnoreCase)
                    ? chunk.ImagePath
                    : string.Empty,
                FormDownload = CreateFormDownload(chunk)
            });
        }

        return sources
            .GroupBy(source => new { source.File, source.Page, source.ChunkType, source.ImagePath })
            .Select(group => group.First())
            .ToList();
    }

    private static ScoredChunk? FindMatchingResult(IReadOnlyList<ScoredChunk> searchResults, string file, int page)
    {
        var fileName = Path.GetFileName(file);
        return searchResults
            .Where(result =>
                result.Chunk.PageNumber == page
                && (string.Equals(result.Chunk.SourceFile, file, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(result.Chunk.SourceFile), fileName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(result => result.Chunk.ChunkType.Equals("image_caption", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(result => result.Score)
            .FirstOrDefault();
    }

    private static FormDownloadRef? CreateFormDownload(ParsedChunk chunk)
    {
        if (!chunk.IsFormTemplate || string.IsNullOrWhiteSpace(chunk.TemplatePath))
        {
            return null;
        }

        return new FormDownloadRef
        {
            FormName = Path.GetFileNameWithoutExtension(chunk.SourceFile),
            DownloadPath = chunk.TemplatePath
        };
    }

    [GeneratedRegex(@"(?<file>[^,:\r\n]+?\.[A-Za-z0-9]+)\s*\(page\s+(?<page>\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CitationRegex();
}
