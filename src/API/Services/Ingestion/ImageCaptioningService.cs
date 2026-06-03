using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Ingestion;

public class ImageCaptionResult
{
    public string Caption { get; set; } = string.Empty;
}

public class ImageCaptioningService(IVisionProvider visionProvider, ILogger<ImageCaptioningService> logger)
{
    public async Task<ImageCaptionResult?> ProcessImageAsync(
        byte[] imageBytes,
        int width,
        int height,
        string sourceFile,
        int page,
        string pageText,
        string mimeType = "image/png",
        CancellationToken ct = default)
    {
        // if (width < 100 || height < 100)
        // {
        //     return null;
        // }

        // var ratio = (float)width / height;
        // if (ratio > 5.0f || ratio < 0.2f)
        // {
        //     return null;
        // }

        try
        {
            var classification = await visionProvider.DescribeImageAsync(
                imageBytes,
                "Is this image meaningful policy content such as an org chart, process diagram, form layout, table, tools, workplace safety equipment, facility equipment or instructional graphic? Or is it decorative such as a logo, banner, background, divider, or icon? Reply with ONLY one word: CONTENT or DECORATIVE.",
                maxTokens: 5,
                mimeType,
                ct);

            if (!classification.Trim().Equals("CONTENT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var surroundingText = pageText.Length > 150 ? pageText[..150] : pageText;
            var caption = await visionProvider.DescribeImageAsync(
                imageBytes,
                $"This image appears in a company policy document called \"{sourceFile}\", page {page}. The surrounding text on this page discusses: \"{surroundingText}\". Describe what this image shows in 2-3 sentences. Focus on content relevant to company policies.",
                mimeType: mimeType,
                ct: ct);

            return string.IsNullOrWhiteSpace(caption) ? null : new ImageCaptionResult { Caption = caption };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Image captioning failed for {SourceFile} page {PageNumber}.", sourceFile, page);
            return null;
        }
    }
}
