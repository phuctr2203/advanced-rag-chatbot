using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Ingestion.Images;

public class ImageStorageService(ConfiguredPathResolver pathResolver)
{
    public async Task<string> SavePdfImageAsync(
        string documentName,
        int pageNumber,
        int imageIndex,
        string extension,
        byte[] imageBytes,
        CancellationToken ct)
    {
        var fileName = $"page{pageNumber}_img{imageIndex}{extension}";
        return await SaveAsync(documentName, fileName, imageBytes, ct);
    }

    public async Task<string> SaveDocxImageAsync(
        string documentName,
        int imageIndex,
        string extension,
        byte[] imageBytes,
        CancellationToken ct)
    {
        var fileName = $"doc_img{imageIndex}{extension}";
        return await SaveAsync(documentName, fileName, imageBytes, ct);
    }

    private async Task<string> SaveAsync(string documentName, string fileName, byte[] imageBytes, CancellationToken ct)
    {
        var sanitizedDocName = SanitizePathSegment(documentName);
        var docDirectory = Path.Combine(pathResolver.ImageStorePath, sanitizedDocName);
        Directory.CreateDirectory(docDirectory);

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
