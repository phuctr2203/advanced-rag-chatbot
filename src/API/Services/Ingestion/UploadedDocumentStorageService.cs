using System.Security.Cryptography;

namespace PolicyBot.Api.Services.Ingestion;

public class UploadedDocumentStorageService(ConfiguredPathResolver pathResolver)
{
    public async Task<StoredDocument> SaveAsync(IFormFile file, CancellationToken ct)
    {
        var uploadRoot = pathResolver.UploadedDocumentsPath;
        var dateSegment = DateTime.UtcNow.ToString("yyyyMMdd");
        var targetDirectory = Path.Combine(uploadRoot, dateSegment);
        Directory.CreateDirectory(targetDirectory);

        var safeFileName = SanitizeFileName(Path.GetFileName(file.FileName));
        await using var inputStream = file.OpenReadStream();
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
