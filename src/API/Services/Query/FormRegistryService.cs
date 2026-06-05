using System.Text.Json;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Query;

public class FormRegistryService(ConfiguredPathResolver pathResolver, ILogger<FormRegistryService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();
    private List<FormRegistryEntry> _entries = [];
    private string? _loadedPath;
    private DateTime _lastWriteUtc = DateTime.MinValue;
    private bool _loaded;

    public void Load(string registryPath)
    {
        lock (_sync)
        {
            LoadCore(registryPath);
        }
    }

    public FormDownloadRef? FindByFile(string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return null;
        }

        EnsureLoaded();

        lock (_sync)
        {
            var fileName = Path.GetFileName(sourceFile);
            var match = _entries.FirstOrDefault(entry =>
                entry.SourceFiles.Any(source =>
                    string.Equals(source, sourceFile, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(source), fileName, StringComparison.OrdinalIgnoreCase))
                || string.Equals(entry.DocxFile, sourceFile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(entry.DocxFile), fileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(entry.DownloadPath), fileName, StringComparison.OrdinalIgnoreCase));

            return match?.ToDownloadRef();
        }
    }

    public FormDownloadRef? FindByAlias(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        EnsureLoaded();

        lock (_sync)
        {
            var match = _entries.FirstOrDefault(entry =>
                entry.Aliases.Any(alias => text.Contains(alias, StringComparison.OrdinalIgnoreCase)));

            return match?.ToDownloadRef();
        }
    }

    public IReadOnlyList<FormDownloadRef> ListDownloads()
    {
        EnsureLoaded();

        lock (_sync)
        {
            return _entries.Select(entry => entry.ToDownloadRef()).ToList();
        }
    }

    private void EnsureLoaded()
    {
        var registryPath = _loadedPath ?? GetRegistryPath();
        var lastWriteUtc = File.Exists(registryPath)
            ? File.GetLastWriteTimeUtc(registryPath)
            : DateTime.MinValue;

        lock (_sync)
        {
            if (_loaded && lastWriteUtc == _lastWriteUtc)
            {
                return;
            }

            LoadCore(registryPath);
        }
    }

    private void LoadCore(string registryPath)
    {
        _loaded = true;
        _loadedPath = registryPath;
        _entries = [];
        _lastWriteUtc = File.Exists(registryPath)
            ? File.GetLastWriteTimeUtc(registryPath)
            : DateTime.MinValue;

        if (!File.Exists(registryPath))
        {
            logger.LogWarning("Form registry file was not found at {RegistryPath}.", registryPath);
            return;
        }

        try
        {
            using var stream = File.OpenRead(registryPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("Form registry file at {RegistryPath} must contain a JSON array.", registryPath);
                return;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var entry = ParseEntry(element);
                if (!string.IsNullOrWhiteSpace(entry.FormName) && !string.IsNullOrWhiteSpace(entry.DownloadPath))
                {
                    _entries.Add(entry);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to load form registry from {RegistryPath}.", registryPath);
        }
    }

    private string GetRegistryPath()
    {
        return Path.Combine(pathResolver.DataPath, "form-registry.json");
    }

    private static FormRegistryEntry ParseEntry(JsonElement element)
    {
        var formName = GetString(element, "formName")
            ?? GetString(element, "form_name")
            ?? GetString(element, "name")
            ?? GetString(element, "title")
            ?? string.Empty;
        var downloadPath = GetString(element, "downloadPath")
            ?? GetString(element, "download_path")
            ?? GetString(element, "templatePath")
            ?? GetString(element, "template_path")
            ?? GetString(element, "path")
            ?? string.Empty;
        var docxFile = GetString(element, "docxFile")
            ?? GetString(element, "docx_file")
            ?? GetString(element, "templateFile")
            ?? GetString(element, "template_file")
            ?? GetString(element, "file");

        var sourceFiles = GetStringArray(element, "sourceFiles")
            .Concat(GetStringArray(element, "source_files"))
            .Concat(GetStringArray(element, "files"))
            .Concat(GetOptionalString(element, "sourceFile"))
            .Concat(GetOptionalString(element, "source_file"))
            .Concat(GetOptionalString(element, "pdf_file"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aliases = GetStringArray(element, "aliases")
            .Concat(GetOptionalString(element, "alias"))
            .Concat(GetOptionalString(element, "formName"))
            .Concat(GetOptionalString(element, "form_name"))
            .Concat(GetOptionalString(element, "name"))
            .Concat(GetOptionalString(element, "title"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FormRegistryEntry(formName, downloadPath, docxFile, sourceFiles, aliases);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IEnumerable<string> GetOptionalString(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return string.IsNullOrWhiteSpace(value) ? [] : [value];
    }

    private static IEnumerable<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
    }

    private sealed record FormRegistryEntry(
        string FormName,
        string DownloadPath,
        string? DocxFile,
        IReadOnlyList<string> SourceFiles,
        IReadOnlyList<string> Aliases)
    {
        public FormDownloadRef ToDownloadRef() => new()
        {
            FormName = FormName,
            DownloadPath = DownloadPath
        };
    }
}
