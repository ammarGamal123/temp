namespace POS.PrintAgent.Core.Models;

public class AgentSettings
{
    public int Port { get; set; } = 9100;
    public List<string> AllowedOrigins { get; set; } = new();
    public string ApiKey { get; set; } = string.Empty;
}
