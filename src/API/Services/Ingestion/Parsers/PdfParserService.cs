using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Ingestion;
using UglyToad.PdfPig;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class PdfParserService(
    IOptions<IngestionOptions> options,
    ImageCaptioningService imageCaptioningService,
    ILogger<PdfParserService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    public async Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, string? sourceFile = null, string agent = "ELCA_GENERAL", CancellationToken ct = default)
    {
        var chunks = new List<ParsedChunk>();
        var fileName = sourceFile ?? Path.GetFileName(filePath);
        var docName = Path.GetFileNameWithoutExtension(fileName);
        var chunkIndex = 0;

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var pageText = string.Join(' ', page.GetWords().Select(word => word.Text));
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                chunks.Add(new ParsedChunk
                {
                    Text = pageText,
                    SourceFile = fileName,
                    PageNumber = page.Number,
                    ChunkIndex = chunkIndex++,
                    ChunkType = "text",
                    FileType = "pdf",
                    Agent = agent
                });
            }

            var imageIndex = 0;
            foreach (var image in page.GetImages())
            {
                ct.ThrowIfCancellationRequested();

                if (image.Bounds.Width < 100 || image.Bounds.Height < 100)
                {
                    continue;
                }

                if (!image.TryGetPng(out var imageBytes))
                {
                    continue;
                }

                var imagePath = await SaveImageAsync(docName, page.Number, imageIndex++, imageBytes, ct);
                var caption = await imageCaptioningService.CaptionAsync(imageBytes, fileName, page.Number, pageText, ct);
                if (string.IsNullOrWhiteSpace(caption))
                {
                    continue;
                }

                chunks.Add(new ParsedChunk
                {
                    Text = caption,
                    SourceFile = fileName,
                    PageNumber = page.Number,
                    ChunkIndex = chunkIndex++,
                    ChunkType = "image_caption",
                    FileType = "pdf",
                    Agent = agent,
                    ImagePath = imagePath,
                    ImagePaths = [imagePath]
                });
            }
        }

        return chunks;
    }

    private async Task<string> SaveImageAsync(string docName, int pageNumber, int imageIndex, byte[] imageBytes, CancellationToken ct)
    {
        var imageRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.ImageStorePath));
        var docDirectory = Path.Combine(imageRoot, SanitizePathSegment(docName));
        Directory.CreateDirectory(docDirectory);

        var fileName = $"page{pageNumber}_img{imageIndex}.png";
        var physicalPath = Path.Combine(docDirectory, fileName);
        await File.WriteAllBytesAsync(physicalPath, imageBytes, ct);

        return $"/images/{SanitizePathSegment(docName)}/{fileName}";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
