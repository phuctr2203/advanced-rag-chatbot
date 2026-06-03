namespace PolicyBot.Api.Providers;

public interface IVisionProvider
{
    Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, int maxTokens = 300, string mimeType = "image/png", CancellationToken ct = default);
}
