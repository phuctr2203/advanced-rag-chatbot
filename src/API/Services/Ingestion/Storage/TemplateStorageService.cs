using System.Security.Cryptography;

namespace PolicyBot.Api.Services.Ingestion.Storage;

public class TemplateStorageService(ConfiguredPathResolver pathResolver)
{
    public async Task<StoredTemplate> SaveAsync(string sourcePath, string originalFileName, CancellationToken ct)
    {
        var templateRoot = pathResolver.TemplatesStorePath;
        Directory.CreateDirectory(templateRoot);

        var safeFileName = SanitizeFileName(Path.GetFileName(originalFileName));
        await using var inputStream = File.OpenRead(sourcePath);
        var hashBytes = await SHA256.HashDataAsync(inputStream, ct);
        var sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var storedFileName = $"{sha256[..16]}_{safeFileName}";
        var physicalPath = Path.Combine(templateRoot, storedFileName);
        var alreadyExisted = File.Exists(physicalPath);

        if (!alreadyExisted)
        {
            inputStream.Position = 0;
            await using var outputStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await inputStream.CopyToAsync(outputStream, ct);
        }

        return new StoredTemplate
        {
            OriginalFileName = safeFileName,
            StoredFileName = storedFileName,
            PhysicalPath = physicalPath,
            UrlPath = $"/templates/{storedFileName}",
            Sha256 = sha256,
            AlreadyExisted = alreadyExisted
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var fileName = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(fileName) ? "template.docx" : fileName;
    }
}
