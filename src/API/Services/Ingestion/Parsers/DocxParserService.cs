using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Ingestion;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class DocxParserService(ImageCaptioningService imageCaptioningService, IOptions<IngestionOptions> options)
{
    private readonly IngestionOptions _options = options.Value;
    public async Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, string? sourceFile = null, string agent = "ELCA_GENERAL", CancellationToken ct = default)
    {
        var chunks = new List<ParsedChunk>();
        var fileName = sourceFile ?? Path.GetFileName(filePath);
        var docName = Path.GetFileNameWithoutExtension(fileName);
        var chunkIndex = 0;
        var pageNumber = 1;
        var paragraphCountOnPage = 0;

        using var document = WordprocessingDocument.Open(filePath, false);
        var mainPart = document.MainDocumentPart;
        if (mainPart?.Document?.Body is not { } body)
        {
            return chunks;
        }

        var documentText = new List<string>();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            ct.ThrowIfCancellationRequested();

            if (HasPageBreak(paragraph))
            {
                pageNumber++;
                paragraphCountOnPage = 0;
            }

            var text = paragraph.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            documentText.Add(text);
            chunks.Add(new ParsedChunk
            {
                Text = text,
                SourceFile = fileName,
                PageNumber = pageNumber,
                ChunkIndex = chunkIndex++,
                ChunkType = "text",
                FileType = "docx",
                Agent = agent
            });

            paragraphCountOnPage++;
            if (paragraphCountOnPage >= 8)
            {
                pageNumber++;
                paragraphCountOnPage = 0;
            }
        }

        var pageText = string.Join(' ', documentText);
        var imageIndex = 0;
        foreach (var imagePart in mainPart.ImageParts)
        {
            ct.ThrowIfCancellationRequested();

            await using var stream = imagePart.GetStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            var imageBytes = memory.ToArray();

            if (!TryGetImageInfo(imageBytes, imagePart.ContentType, out var width, out var height, out var extension))
            {
                continue;
            }

            var result = await imageCaptioningService.ProcessImageAsync(imageBytes, width, height, fileName, pageNumber, pageText, imagePart.ContentType, ct);
            if (result is null)
            {
                continue;
            }

            var imagePath = await SaveImageAsync(docName, imageIndex++, extension, imageBytes, ct);
            chunks.Add(new ParsedChunk
            {
                Text = result.Caption,
                SourceFile = fileName,
                PageNumber = pageNumber,
                ChunkIndex = chunkIndex++,
                ChunkType = "image_caption",
                FileType = "docx",
                Agent = agent,
                ImagePath = imagePath,
                ImagePaths = [imagePath]
            });
        }

        return chunks;
    }

    private static bool HasPageBreak(Paragraph paragraph)
    {
        return paragraph.Descendants<Break>().Any(breakElement => breakElement.Type?.Value == BreakValues.Page)
            || paragraph.Descendants<LastRenderedPageBreak>().Any();
    }

    private static bool TryGetImageInfo(byte[] bytes, string contentType, out int width, out int height, out string extension)
    {
        extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => string.Empty
        };

        if (TryGetPngSize(bytes, out width, out height))
        {
            extension = ".png";
            return true;
        }

        if (TryGetJpegSize(bytes, out width, out height))
        {
            extension = ".jpg";
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool TryGetPngSize(byte[] bytes, out int width, out int height)
    {
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool TryGetJpegSize(byte[] bytes, out int width, out int height)
    {
        var index = 2;
        while (index + 9 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
            {
                break;
            }

            var marker = bytes[index + 1];
            var length = (bytes[index + 2] << 8) + bytes[index + 3];
            if (length < 2 || index + length + 1 >= bytes.Length)
            {
                break;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                height = (bytes[index + 5] << 8) + bytes[index + 6];
                width = (bytes[index + 7] << 8) + bytes[index + 8];
                return true;
            }

            index += 2 + length;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    private async Task<string> SaveImageAsync(string docName, int imageIndex, string extension, byte[] imageBytes, CancellationToken ct)
    {
        var imageRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.ImageStorePath));
        var sanitizedDocName = SanitizePathSegment(docName);
        var docDirectory = Path.Combine(imageRoot, sanitizedDocName);
        Directory.CreateDirectory(docDirectory);

        var fileName = $"doc_img{imageIndex}{extension}";
        var physicalPath = Path.Combine(docDirectory, fileName);
        await File.WriteAllBytesAsync(physicalPath, imageBytes, ct);

        return $"/images/{sanitizedDocName}/{fileName}";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
