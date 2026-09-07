using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Core.Interfaces;

public interface IPrinterService
{
    Task<PrintResponse> PrintAsync(PrintRequest request, CancellationToken ct = default);
    List<PrinterInfo> GetAvailablePrinters();
    bool IsPrinterOnline(string printerName);
}
