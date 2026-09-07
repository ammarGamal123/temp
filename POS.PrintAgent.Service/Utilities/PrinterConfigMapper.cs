using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Service.Utilities;

/// <summary>
/// بيحوّل الـ PrinterConfiguration اللي جاية من الـ DB لقيم ESC/POS قابلة للاستخدام
/// </summary>
public static class PrinterConfigMapper
{
    /// <summary>
    /// حساب عرض السطر بالحروف حسب عرض الورق والـ margins
    /// Standard: 80mm = 48 chars، 58mm = 32 chars
    /// </summary>
    public static int GetLineWidth(PrinterConfiguration config)
    {
        var effectiveMargins = config.MarginLeft + config.MarginRight;

        return config.PaperWidth switch
        {
            58 => Math.Max(32 - effectiveMargins, 20),
            80 => Math.Max(48 - effectiveMargins, 30),
            _ => Math.Max(48 - effectiveMargins, 30)
        };
    }

    /// <summary>
    /// تحويل font size (points) لـ ESC/POS multiplier
    /// </summary>
    public static EscPosFontSize MapFontSize(int pointSize)
    {
        return pointSize switch
        {
            <= 8 => new EscPosFontSize(1, 1),
            <= 10 => new EscPosFontSize(1, 1),
            <= 12 => new EscPosFontSize(1, 2),
            <= 14 => new EscPosFontSize(2, 2),
            <= 18 => new EscPosFontSize(2, 3),
            _ => new EscPosFontSize(3, 3)
        };
    }

    /// <summary>
    /// تحويل QR size (pixels) لـ ESC/POS module size (1-16)
    /// </summary>
    public static byte MapQrSize(int pixelSize)
    {
        var module = (byte)Math.Clamp(pixelSize / 10, 1, 16);
        return module;
    }

    /// <summary>
    /// تحويل Barcode height (pixels) لـ ESC/POS height
    /// </summary>
    public static byte MapBarcodeHeight(int pixelSize)
    {
        return (byte)Math.Clamp(pixelSize, 1, 255);
    }
}

public record EscPosFontSize(byte WidthMultiplier, byte HeightMultiplier)
{
    public byte ToEscPosByte()
    {
        var w = (byte)Math.Clamp(WidthMultiplier - 1, 0, 7);
        var h = (byte)Math.Clamp(HeightMultiplier - 1, 0, 7);
        return (byte)((w << 4) | h);
    }
}
