using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Shared;

public class DocumentLibraryService(IVectorStoreService vectorStoreService)
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct = default)
    {
        var chunks = await vectorStoreService.ListAllChunksAsync(ct);

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.SourceFile))
            .GroupBy(chunk => chunk.SourceFile, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new DocumentSummary
                {
                    SourceFile = group.Key,
                    Agent = first.Agent,
                    FileType = first.FileType,
                    ChunkCount = group.Count(),
                    PageCount = group.Select(chunk => chunk.PageNumber).Where(page => page > 0).Distinct().Count(),
                    HasImages = group.Any(chunk =>
                        chunk.ChunkType.Equals("image_caption", StringComparison.OrdinalIgnoreCase) ||
                        !string.IsNullOrWhiteSpace(chunk.ImagePath) ||
                        chunk.ImagePaths.Count > 0),
                    HasFormTemplate = group.Any(chunk => chunk.IsFormTemplate)
                };
            })
            .OrderBy(summary => summary.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> DeleteAsync(string sourceFile, CancellationToken ct = default)
    {
        return await vectorStoreService.DeleteBySourceFileAsync(sourceFile, ct);
    }
}
