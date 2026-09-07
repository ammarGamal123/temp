using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Core.Interfaces;

public interface IReceiptBuilder
{
    void Configure(PrinterConfiguration config);
    byte[] BuildReceipt(InvoiceData invoice);
    byte[] BuildKitchenOrder(InvoiceData invoice);
    byte[] BuildCashDrawerCommand();
}
