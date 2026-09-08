namespace POS.PrintAgent.Core.Models;

public class PrintResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public string? RenderMode { get; set; }
    public bool FallbackUsed { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
