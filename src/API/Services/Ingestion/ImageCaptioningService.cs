using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Ingestion;

public class ImageCaptioningService(IVisionProvider visionProvider, ILogger<ImageCaptioningService> logger)
{
    public async Task<string> CaptionAsync(byte[] imageBytes, string sourceFile, int pageNumber, string pageText, CancellationToken ct = default)
    {
        try
        {
            var surroundingText = pageText.Length > 150 ? pageText[..150] : pageText;
            var promptContext = $"This image appears in a company policy document called \"{sourceFile}\", page {pageNumber}. The surrounding text on this page discusses: \"{surroundingText}\".";
            return await visionProvider.DescribeImageAsync(imageBytes, promptContext, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Image captioning failed for {SourceFile} page {PageNumber}.", sourceFile, pageNumber);
            return string.Empty;
        }
    }
}
