using PolicyBot.Api.Models;
using UglyToad.PdfPig.Content;

namespace PolicyBot.Api.Services.Ingestion.Images;

public class PdfImageExtractorService(
    ImageCaptioningService imageCaptioningService,
    ImageStorageService imageStorageService,
    ILogger<PdfImageExtractorService> logger)
{
    public IReadOnlyList<PdfImageAnalysisResult> AnalyzeImages(Page page)
    {
        var results = new List<PdfImageAnalysisResult>();
        var imageIndex = 0;

        foreach (var image in page.GetImages())
        {
            var hasImageBytes = TryGetImageBytes(image, out var imageBytes, out var mimeType, out _);
            var width = (int)Math.Round(image.Bounds.Width);
            var height = (int)Math.Round(image.Bounds.Height);
            var ratio = height == 0 ? 0 : (float)width / height;

            results.Add(new PdfImageAnalysisResult
            {
                Page = page.Number,
                ImageIndex = imageIndex++,
                Width = width,
                Height = height,
                Ratio = ratio,
                HasImageBytes = hasImageBytes,
                MimeType = mimeType,
                ByteCount = imageBytes.Length,
                PassesSizeFilter = width >= 100 && height >= 100,
                PassesAspectRatioFilter = ratio <= 5.0f && ratio >= 0.2f,
                IsFullPageImage = IsFullPageImage(width, height, page.Width, page.Height)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<ParsedChunk>> ExtractCaptionChunksAsync(
        Page page,
        string documentName,
        string sourceFile,
        string pageText,
        string agent,
        int startingChunkIndex,
        CancellationToken ct)
    {
        var chunks = new List<ParsedChunk>();
        var savedImageIndex = 0;
        var chunkIndex = startingChunkIndex;

        foreach (var image in page.GetImages())
        {
            ct.ThrowIfCancellationRequested();

            if (!TryGetImageBytes(image, out var imageBytes, out var mimeType, out var extension))
            {
                continue;
            }

            var width = (int)Math.Round(image.Bounds.Width);
            var height = (int)Math.Round(image.Bounds.Height);
            if (IsFullPageImage(width, height, page.Width, page.Height))
            {
                logger.LogDebug("Skipping full-page PDF image on {SourceFile} page {PageNumber}.", sourceFile, page.Number);
                continue;
            }

            var result = await imageCaptioningService.ProcessImageAsync(
                imageBytes,
                width,
                height,
                sourceFile,
                page.Number,
                pageText,
                mimeType,
                ct);
            if (result is null)
            {
                continue;
            }

            var imagePath = await imageStorageService.SavePdfImageAsync(
                documentName,
                page.Number,
                savedImageIndex++,
                extension,
                imageBytes,
                ct);

            chunks.Add(new ParsedChunk
            {
                Text = result.Caption,
                SourceFile = sourceFile,
                PageNumber = page.Number,
                ChunkIndex = chunkIndex++,
                ChunkType = "image_caption",
                FileType = "pdf",
                Agent = agent,
                ImagePath = imagePath,
                ImagePaths = [imagePath]
            });
        }

        return chunks;
    }

    private static bool TryGetImageBytes(IPdfImage image, out byte[] imageBytes, out string mimeType, out string extension)
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

    private static bool IsFullPageImage(int imageWidth, int imageHeight, double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0 || pageHeight <= 0)
        {
            return false;
        }

        var widthCoverage = imageWidth / pageWidth;
        var heightCoverage = imageHeight / pageHeight;
        var imageRatio = imageHeight == 0 ? 0 : imageWidth / (double)imageHeight;
        var pageRatio = pageWidth / pageHeight;
        var ratioDelta = Math.Abs(imageRatio - pageRatio);

        return widthCoverage >= 0.85
            && heightCoverage >= 0.85
            && ratioDelta <= 0.15;
    }
}
