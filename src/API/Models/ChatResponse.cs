namespace PolicyBot.Api.Models;

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceRef> Sources { get; set; } = [];
}
