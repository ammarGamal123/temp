using POS.PrintAgent.Core.Enums;

namespace POS.PrintAgent.Core.Models;

public class PrintRequest
{
    public string PrinterName { get; set; } = string.Empty;
    public PrintJobType JobType { get; set; }
    public PrinterConfiguration PrinterConfig { get; set; } = new();
    public InvoiceData Invoice { get; set; } = new();
    public int Copies { get; set; } = 1;
    public bool OpenCashDrawer { get; set; } = false;
}
