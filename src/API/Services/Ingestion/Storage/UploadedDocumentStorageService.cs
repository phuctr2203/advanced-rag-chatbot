using System.Security.Cryptography;

namespace PolicyBot.Api.Services.Ingestion.Storage;

public class UploadedDocumentStorageService(ConfiguredPathResolver pathResolver)
{
    public async Task<StoredDocument> SaveAsync(IFormFile file, CancellationToken ct)
    {
        var safeFileName = SanitizeFileName(Path.GetFileName(file.FileName));
        var uploadRoot = pathResolver.UploadedDocumentsPath;
        var dateSegment = DateTime.UtcNow.ToString("yyyyMMdd");
        var targetDirectory = Path.Combine(uploadRoot, dateSegment);
        Directory.CreateDirectory(targetDirectory);

        await using var inputStream = file.OpenReadStream();
        return await SaveStreamAsync(inputStream, safeFileName, targetDirectory, dateSegment, ct);
    }

    public async Task<StoredDocument> SaveAsync(string filePath, CancellationToken ct)
    {
        var safeFileName = SanitizeFileName(Path.GetFileName(filePath));
        var uploadRoot = pathResolver.UploadedDocumentsPath;
        var dateSegment = DateTime.UtcNow.ToString("yyyyMMdd");
        var targetDirectory = Path.Combine(uploadRoot, dateSegment);
        Directory.CreateDirectory(targetDirectory);

        await using var inputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await SaveStreamAsync(inputStream, safeFileName, targetDirectory, dateSegment, ct);
    }

    private static async Task<StoredDocument> SaveStreamAsync(
        Stream inputStream,
        string safeFileName,
        string targetDirectory,
        string dateSegment,
        CancellationToken ct)
    {
        var hashBytes = await SHA256.HashDataAsync(inputStream, ct);
        var sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var storedFileName = $"{sha256[..16]}_{safeFileName}";
        var physicalPath = Path.Combine(targetDirectory, storedFileName);
        var alreadyExisted = File.Exists(physicalPath);

        if (!alreadyExisted)
        {
            inputStream.Position = 0;
            await using var outputStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await inputStream.CopyToAsync(outputStream, ct);
        }

        return new StoredDocument
        {
            OriginalFileName = safeFileName,
            StoredFileName = storedFileName,
            PhysicalPath = physicalPath,
            UrlPath = $"/documents/{dateSegment}/{storedFileName}",
            Sha256 = sha256,
            AlreadyExisted = alreadyExisted
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var fileName = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(fileName) ? "document" : fileName;
    }
}
