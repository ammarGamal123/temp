namespace POS.PrintAgent.Core.Models;

public class InvoiceItem
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; } = 15m;
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? CategoryName { get; set; }
}
