namespace POS.PrintAgent.Core.Models;

/// <summary>
/// إعدادات الطابعة - مطابقة تماماً للـ JSON اللي بيتخزن في الـ DB
/// </summary>
public class PrinterConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string PrinterType { get; set; } = "thermal";  // thermal | a4
    public string PrintMode { get; set; } = "auto";       // auto | driver | raw-escpos
    public int PaperWidth { get; set; } = 80;             // 58 | 80 | 210 (A4)
    public int PaperHeight { get; set; } = 200;
    public int Dpi { get; set; } = 203;

    // الـ Margins
    public int MarginTop { get; set; } = 2;
    public int MarginBottom { get; set; } = 2;
    public int MarginLeft { get; set; } = 1;
    public int MarginRight { get; set; } = 1;

    // Font Sizes
    public int FontSizeSmall { get; set; } = 8;
    public int FontSizeMedium { get; set; } = 10;
    public int FontSizeLarge { get; set; } = 14;

    // QR and Barcode
    public int QrCodeSize { get; set; } = 80;
    public int BarcodeSize { get; set; } = 60;

    // Business Logic
    public int CategoryId { get; set; } = 0;              // 0 = كاشير، 1 = مطبخ، 2 = بار
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
