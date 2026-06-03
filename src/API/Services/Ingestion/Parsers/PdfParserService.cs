using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Ingestion;
using System.Diagnostics;
using UglyToad.PdfPig;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class PdfParserService(
    IOptions<IngestionOptions> options,
    ImageCaptioningService imageCaptioningService,
    ConfiguredPathResolver pathResolver,
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
        var parseFilePath = await ResolveTextReadablePdfAsync(filePath, ct);
        var chunks = new List<ParsedChunk>();
        var fileName = sourceFile ?? Path.GetFileName(filePath);
        var docName = Path.GetFileNameWithoutExtension(fileName);
        var chunkIndex = 0;

        using var document = PdfDocument.Open(parseFilePath);
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

    private async Task<string> ResolveTextReadablePdfAsync(string filePath, CancellationToken ct)
    {
        if (!_options.EnablePdfOcrFallback)
        {
            return filePath;
        }

        var quality = AnalyzeTextQuality(filePath);
        if (!quality.NeedsOcr(_options))
        {
            return filePath;
        }

        logger.LogInformation(
            "PDF text quality is low for {FilePath}. Characters: {CharacterCount}; average words/page: {AverageWordsPerPage}. Running OCRmyPDF.",
            filePath,
            quality.CharacterCount,
            quality.AverageWordsPerPage);

        try
        {
            return await RunOcrMyPdfAsync(filePath, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "OCRmyPDF failed for {FilePath}. Falling back to original PDF.", filePath);
            return filePath;
        }
    }

    private static PdfTextQuality AnalyzeTextQuality(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var pageCount = 0;
        var wordCount = 0;
        var characterCount = 0;

        foreach (var page in document.GetPages())
        {
            pageCount++;
            var words = page.GetWords().Select(word => word.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToList();
            wordCount += words.Count;
            characterCount += words.Sum(word => word.Length);
        }

        return new PdfTextQuality(pageCount, wordCount, characterCount);
    }

    private async Task<string> RunOcrMyPdfAsync(string filePath, CancellationToken ct)
    {
        var tempRoot = pathResolver.TempPath;
        Directory.CreateDirectory(tempRoot);

        var outputDirectory = Path.Combine(tempRoot, "ocr");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}_ocr_{Guid.NewGuid():N}.pdf");

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.OcrMyPdfExecutable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("--skip-text");
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start OCRmyPDF.");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger.LogError("OCRmyPDF failed. stdout: {Stdout}; stderr: {Stderr}", stdout, stderr);
            throw new InvalidOperationException($"OCRmyPDF failed with exit code {process.ExitCode}.");
        }

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException("OCRmyPDF did not produce an output PDF.", outputPath);
        }

        return outputPath;
    }

    private async Task<string> SaveImageAsync(string docName, int pageNumber, int imageIndex, string extension, byte[] imageBytes, CancellationToken ct)
    {
        var imageRoot = pathResolver.ImageStorePath;
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

    private sealed record PdfTextQuality(int PageCount, int WordCount, int CharacterCount)
    {
        public int AverageWordsPerPage => PageCount == 0 ? 0 : WordCount / PageCount;

        public bool NeedsOcr(IngestionOptions options)
        {
            return CharacterCount < options.PdfOcrMinimumTextCharacters
                || AverageWordsPerPage < options.PdfOcrMinimumAverageWordsPerPage;
        }
    }
}
