using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion;

public class ConfiguredPathResolver(IOptions<IngestionOptions> options, IWebHostEnvironment environment)
{
    private readonly IngestionOptions _options = options.Value;

    public string UploadedDocumentsPath => Resolve(_options.UploadedDocumentsPath);
    public string ImageStorePath => Resolve(_options.ImageStorePath);
    public string TemplatesStorePath => Resolve(_options.TemplatesStorePath);
    public string TempPath => Resolve(_options.TempPath);

    private string Resolve(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }
}
