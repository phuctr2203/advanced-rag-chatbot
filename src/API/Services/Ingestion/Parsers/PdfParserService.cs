using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Ingestion;
using UglyToad.PdfPig;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class PdfParserService(
    IOptions<IngestionOptions> options,
    ImageCaptioningService imageCaptioningService,
    IWebHostEnvironment environment,
    ILogger<PdfParserService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    public IReadOnlyList<object> AnalyzeImages(string filePath)
    {
        var results = new List<object>();
        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            var imageIndex = 0;
            foreach (var image in page.GetImages())
            {
                var hasImageBytes = TryGetImageBytes(image, out var imageBytes, out var mimeType, out _);
                var width = (int)Math.Round(image.Bounds.Width);
                var height = (int)Math.Round(image.Bounds.Height);
                var ratio = height == 0 ? 0 : (float)width / height;
                results.Add(new
                {
                    page = page.Number,
                    imageIndex = imageIndex++,
                    width,
                    height,
                    ratio,
                    hasImageBytes,
                    mimeType,
                    byteCount = imageBytes.Length,
                    passesSizeFilter = width >= 100 && height >= 100,
                    passesAspectRatioFilter = ratio <= 5.0f && ratio >= 0.2f
                });
            }
        }

        return results;
    }

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

                if (!TryGetImageBytes(image, out var imageBytes, out var mimeType, out var extension))
                {
                    continue;
                }

                var width = (int)Math.Round(image.Bounds.Width);
                var height = (int)Math.Round(image.Bounds.Height);
                var result = await imageCaptioningService.ProcessImageAsync(imageBytes, width, height, fileName, page.Number, pageText, mimeType, ct);
                if (result is null)
                {
                    continue;
                }

                var imagePath = await SaveImageAsync(docName, page.Number, imageIndex++, extension, imageBytes, ct);
                chunks.Add(new ParsedChunk
                {
                    Text = result.Caption,
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

    private async Task<string> SaveImageAsync(string docName, int pageNumber, int imageIndex, string extension, byte[] imageBytes, CancellationToken ct)
    {
        var imageRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.ImageStorePath));
        var sanitizedDocName = SanitizePathSegment(docName);
        var docDirectory = Path.Combine(imageRoot, sanitizedDocName);
        Directory.CreateDirectory(docDirectory);

        var fileName = $"page{pageNumber}_img{imageIndex}{extension}";
        var physicalPath = Path.Combine(docDirectory, fileName);
        await File.WriteAllBytesAsync(physicalPath, imageBytes, ct);

        return $"/images/{sanitizedDocName}/{fileName}";
    }

    private static bool TryGetImageBytes(UglyToad.PdfPig.Content.IPdfImage image, out byte[] imageBytes, out string mimeType, out string extension)
    {
        if (image.TryGetPng(out imageBytes))
        {
            mimeType = "image/png";
            extension = ".png";
            return true;
        }

        if (IsJpeg(image.RawBytes))
        {
            imageBytes = image.RawBytes.ToArray();
            mimeType = "image/jpeg";
            extension = ".jpg";
            return true;
        }

        imageBytes = [];
        mimeType = string.Empty;
        extension = string.Empty;
        return false;
    }

    private static bool IsJpeg(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
