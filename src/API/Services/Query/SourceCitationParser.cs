using System.Text.RegularExpressions;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public partial class SourceCitationParser(FormRegistryService formRegistry)
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
            foreach (var page in ParsePages(match.Groups["pages"].Value))
            {
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
                    FormDownload = chunk.IsFormTemplate
                        ? formRegistry.FindByFile(chunk.SourceFile) ?? CreateFormDownloadFallback(chunk)
                        : null
                });
            }
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

    private static IReadOnlyList<int> ParsePages(string value)
    {
        return PageNumberRegex()
            .Matches(value)
            .Select(match => int.TryParse(match.Value, out var page) ? page : 0)
            .Where(page => page > 0)
            .Distinct()
            .ToList();
    }

    private static FormDownloadRef? CreateFormDownloadFallback(ParsedChunk chunk)
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

    [GeneratedRegex(@"(?<file>[^,:\r\n]+?\.[A-Za-z0-9]+)\s*\(pages?\s+(?<pages>\d+(?:\s*[,;/&]\s*\d+)*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CitationRegex();

    [GeneratedRegex(@"\d+", RegexOptions.Compiled)]
    private static partial Regex PageNumberRegex();
}
