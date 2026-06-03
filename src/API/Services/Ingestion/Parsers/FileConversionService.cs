using System.Diagnostics;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class FileConversionService(IOptions<IngestionOptions> options, ILogger<FileConversionService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    public async Task<string> ToPdfAsync(string filePath, CancellationToken ct = default)
    {
        var tempRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.TempPath));
        Directory.CreateDirectory(tempRoot);

        var outputDirectory = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(filePath) + "_pdf");
        Directory.CreateDirectory(outputDirectory);

        await RunLibreOfficeAsync("pdf", outputDirectory, filePath, ct);

        var expectedPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(filePath) + ".pdf");
        if (File.Exists(expectedPath))
        {
            return expectedPath;
        }

        var converted = Directory.GetFiles(outputDirectory, "*.pdf").FirstOrDefault();
        return converted ?? throw new FileNotFoundException("LibreOffice did not produce a PDF file.", expectedPath);
    }

    private async Task RunLibreOfficeAsync(string format, string outputDirectory, string filePath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "libreoffice",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(format);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add(filePath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start LibreOffice.");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger.LogError("LibreOffice conversion failed. stdout: {Stdout}; stderr: {Stderr}", stdout, stderr);
            throw new InvalidOperationException($"LibreOffice conversion failed with exit code {process.ExitCode}.");
        }
    }
}
