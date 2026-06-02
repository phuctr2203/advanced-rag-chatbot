using System.Text.RegularExpressions;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public partial class SourceCitationParser(FormRegistryService formRegistry)
{
    public List<SourceRef> Parse(string response, IReadOnlyList<VectorSearchResult> searchResults)
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
                FormDownload = chunk.IsFormTemplate ? formRegistry.FindByFile(chunk.SourceFile) : null
            });
        }

        return sources
            .GroupBy(source => new { source.File, source.Page, source.ChunkType, source.ImagePath })
            .Select(group => group.First())
            .ToList();
    }

    private static VectorSearchResult? FindMatchingResult(IReadOnlyList<VectorSearchResult> searchResults, string file, int page)
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

    [GeneratedRegex(@"(?<file>[^,:\r\n]+?\.[A-Za-z0-9]+)\s*\(page\s+(?<page>\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CitationRegex();
}
