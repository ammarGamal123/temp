namespace POS.PrintAgent.Core.Models;

public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
}
