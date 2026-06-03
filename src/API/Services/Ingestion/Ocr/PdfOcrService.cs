using System.Diagnostics;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Services.Ingestion.Ocr;

public class PdfOcrService(
    IOptions<IngestionOptions> options,
    ConfiguredPathResolver pathResolver,
    PdfTextQualityAnalyzer textQualityAnalyzer,
    ILogger<PdfOcrService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    public async Task<string> ResolveTextReadablePdfAsync(string filePath, CancellationToken ct)
    {
        if (!_options.EnablePdfOcrFallback)
        {
            return filePath;
        }

        var quality = textQualityAnalyzer.Analyze(filePath);
        if (!quality.NeedsOcr(_options))
        {
            return filePath;
        }

        logger.LogInformation(
            "PDF text quality is low for {FilePath}. Characters: {CharacterCount}; average words/page: {AverageWordsPerPage}. Running OCRmyPDF.",
            filePath,
            quality.CharacterCount,
            quality.AverageWordsPerPage);

        var ocrExecutable = ExecutableResolver.Resolve(_options.OcrMyPdfExecutable);
        if (ocrExecutable is null)
        {
            logger.LogWarning(
                "OCRmyPDF fallback is enabled, but executable '{Executable}' was not found on PATH. Falling back to original PDF.",
                _options.OcrMyPdfExecutable);
            return filePath;
        }

        try
        {
            return await RunOcrMyPdfAsync(filePath, ocrExecutable, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "OCRmyPDF failed for {FilePath}. Falling back to original PDF.", filePath);
            return filePath;
        }
    }

    private async Task<string> RunOcrMyPdfAsync(string filePath, string ocrExecutable, CancellationToken ct)
    {
        var outputDirectory = Path.Combine(pathResolver.TempPath, "ocr");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}_ocr_{Guid.NewGuid():N}.pdf");

        var startInfo = new ProcessStartInfo
        {
            FileName = ocrExecutable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("--skip-text");
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start OCRmyPDF.");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger.LogError("OCRmyPDF failed. stdout: {Stdout}; stderr: {Stderr}", stdout, stderr);
            throw new InvalidOperationException($"OCRmyPDF failed with exit code {process.ExitCode}.");
        }

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException("OCRmyPDF did not produce an output PDF.", outputPath);
        }

        return outputPath;
    }
}
