namespace PolicyBot.Api.Models;

public class SourceRef
{
    public string File { get; set; } = string.Empty;
    public int Page { get; set; }
    public List<string> ImagePaths { get; set; } = [];
}
