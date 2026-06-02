namespace PolicyBot.Api.Providers;

public interface IVisionProvider
{
    Task<string> DescribeImageAsync(byte[] imageBytes, string surroundingText, CancellationToken ct = default);
}
