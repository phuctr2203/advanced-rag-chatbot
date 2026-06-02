using System.Diagnostics;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class FileConversionService(IOptions<IngestionOptions> options, IWebHostEnvironment environment, ILogger<FileConversionService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    public Task<string> ToPdfAsync(string filePath, CancellationToken ct = default)
    {
        return ConvertAsync(filePath, "pdf", ct);
    }

    public Task<string> ToDocxAsync(string filePath, CancellationToken ct = default)
    {
        return ConvertAsync(filePath, "docx", ct);
    }

    private async Task<string> ConvertAsync(string filePath, string format, CancellationToken ct)
    {
        var tempRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.TempPath));
        Directory.CreateDirectory(tempRoot);

        var outputDirectory = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(filePath) + "_" + format);
        Directory.CreateDirectory(outputDirectory);

        await RunLibreOfficeAsync(format, outputDirectory, filePath, ct);

        var expectedPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(filePath) + "." + format);
        if (File.Exists(expectedPath))
        {
            return expectedPath;
        }

        var converted = Directory.GetFiles(outputDirectory, "*." + format).FirstOrDefault();
        return converted ?? throw new FileNotFoundException($"LibreOffice did not produce a {format.ToUpperInvariant()} file.", expectedPath);
    }

    private async Task RunLibreOfficeAsync(string format, string outputDirectory, string filePath, CancellationToken ct)
    {
        var profileDirectory = Path.Combine(outputDirectory, "libreoffice-profile");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = GetLibreOfficeExecutable(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add($"-env:UserInstallation={new Uri(profileDirectory).AbsoluteUri}");
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--nofirststartwizard");
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

    private static string GetLibreOfficeExecutable()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var windowsPath = Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe");
        return File.Exists(windowsPath) ? windowsPath : "libreoffice";
    }
}
