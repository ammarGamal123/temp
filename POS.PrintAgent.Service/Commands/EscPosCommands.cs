namespace POS.PrintAgent.Service.Commands;

/// <summary>
/// ESC/POS Commands - الأوامر الأساسية للطابعات الحرارية
/// Reference: https://reference.epson-biz.com/modules/ref_escpos/
/// </summary>
public static class EscPosCommands
{
    // Control Characters
    public const byte ESC = 0x1B;
    public const byte GS = 0x1D;
    public const byte LF = 0x0A;
    public const byte CR = 0x0D;
    public const byte FF = 0x0C;
    public const byte FS = 0x1C;

    // Initialize
    public static readonly byte[] Initialize = { ESC, (byte)'@' };

    // Alignment
    public static readonly byte[] AlignLeft = { ESC, (byte)'a', 0x00 };
    public static readonly byte[] AlignCenter = { ESC, (byte)'a', 0x01 };
    public static readonly byte[] AlignRight = { ESC, (byte)'a', 0x02 };

    // Text Size
    public static readonly byte[] NormalSize = { GS, (byte)'!', 0x00 };
    public static readonly byte[] DoubleHeight = { GS, (byte)'!', 0x01 };
    public static readonly byte[] DoubleWidth = { GS, (byte)'!', 0x10 };
    public static readonly byte[] DoubleSize = { GS, (byte)'!', 0x11 };

    // Text Style
    public static readonly byte[] BoldOn = { ESC, (byte)'E', 0x01 };
    public static readonly byte[] BoldOff = { ESC, (byte)'E', 0x00 };
    public static readonly byte[] UnderlineOn = { ESC, (byte)'-', 0x01 };
    public static readonly byte[] UnderlineOff = { ESC, (byte)'-', 0x00 };

    // Line Feeds
    public static readonly byte[] NewLine = { LF };
    public static byte[] FeedLines(byte count) => new byte[] { ESC, (byte)'d', count };

    // Cut
    public static readonly byte[] FullCut = { GS, (byte)'V', 0x00 };
    public static readonly byte[] PartialCut = { GS, (byte)'V', 0x01 };

    // Cash Drawer
    public static readonly byte[] OpenCashDrawer = { ESC, (byte)'p', 0x00, 0x19, 0xFA };

    // Code Page
    public static readonly byte[] SetCodePageArabic = { ESC, (byte)'t', 22 };       // CP864
    public static readonly byte[] SetCodePageWindows1256 = { ESC, (byte)'t', 32 };

    // Character Set
    public static readonly byte[] SetArabicMode = { FS, (byte)'&' };
    public static readonly byte[] SetLatinMode = { FS, (byte)'.' };
}
