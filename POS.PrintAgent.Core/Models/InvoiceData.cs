using System.Text.Json.Serialization;

namespace POS.PrintAgent.Core.Models;

public class InvoiceData
{
    // بيانات الشركة
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string CrNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? LogoBase64 { get; set; }

    // بيانات الفاتورة
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public string CashierName { get; set; } = string.Empty;
    public string? TableNumber { get; set; }
    public string? OrderType { get; set; }

    // العناصر
    public List<InvoiceItem> Items { get; set; } = new();

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InvoiceItem> Details
    {
        get => null;
            set
        {
            if (value is { Count: > 0 } && 
                (Items == null || Items.Count == 0))
            {
                Items = value;
            }
        }
    }

    // المبالغ
    public decimal SubTotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;

    // ZATCA
    public string? QrCodeBase64 { get; set; }

    // إضافات
    public string? FooterMessage { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}
