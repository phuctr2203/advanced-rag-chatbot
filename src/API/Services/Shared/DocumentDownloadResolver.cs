using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Shared;

public class DocumentDownloadResolver(ConfiguredPathResolver pathResolver)
{
    public string Resolve(string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile) || !Directory.Exists(pathResolver.UploadedDocumentsPath))
        {
            return string.Empty;
        }

        var expectedSuffix = "_" + sourceFile;
        var match = Directory
            .EnumerateFiles(pathResolver.UploadedDocumentsPath, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(match))
        {
            return string.Empty;
        }

        var relativePath = Path.GetRelativePath(pathResolver.UploadedDocumentsPath, match).Replace('\\', '/');
        return "/documents/" + Uri.EscapeDataString(relativePath).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
    }
}
