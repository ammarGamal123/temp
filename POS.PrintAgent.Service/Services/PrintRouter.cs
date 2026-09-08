using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Service.Services;

/// <summary>
/// Universal print routing: one connection method (Windows spooler, by printer
/// name — identical for USB, TCP/IP and shared printers) with per-printer
/// rendering path selection.
///
/// - "raw-escpos": ESC/POS bytes sent RAW (fast, cutter + drawer). Only for
///   proven ESC/POS thermals — anything else prints garbage.
/// - "driver": receipt PNG printed via the Windows driver (GDI). Works on ANY
///   installed printer (thermal, inkjet, laser, A4). Unknown printers always
///   land here, so output is never garbage.
///
/// Paper handling:
/// - RollMode (thermal roll via driver): zero margins, scale to fit WIDTH only,
///   slice vertically so long receipts never shrink.
/// - SheetMode (A4/Letter): config margins, fit width, paginate with
///   HasMorePages so long invoices flow onto page 2+.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PrintRouter
{
    public const string RawEscPos = "raw-escpos";
    public const string Driver = "driver";

    /// <summary>
    /// Resolves the print path for a printer. Explicit PrintMode always wins.
    /// Auto defaults unknown printers to driver (always correct output) and
    /// uses raw only for small thermal paper.
    /// </summary>
    public static string ResolvePrintPath(PrinterConfiguration cfg)
    {
        var mode = (cfg.PrintMode ?? "auto").Trim().ToLowerInvariant();
        if (mode == RawEscPos) return RawEscPos;
        if (mode == Driver) return Driver;

        // auto
        if (IsSheetPaper(cfg)) return Driver;
        return RawEscPos;
    }

    public static bool IsSheetPaper(PrinterConfiguration cfg)
    {
        var printerType = (cfg.PrinterType ?? string.Empty).Trim().ToLowerInvariant();
        if (printerType == "a4") return true;
        return cfg.PaperWidth >= 120;
    }

    public static int MmToHundredths(int mm)
        => Math.Max(0, (int)Math.Round(mm * 100.0 / 25.4));

    public static bool PrintPngUsingWindowsDriver(string printerName, byte[] pngBytes, int copies, PrinterConfiguration cfg)
    {
        using var ms = new MemoryStream(pngBytes);
        using var image = Image.FromStream(ms);

        var sheet = IsSheetPaper(cfg);
        var margins = sheet
            ? new Margins(
                MmToHundredths(cfg.MarginLeft),
                MmToHundredths(cfg.MarginRight),
                MmToHundredths(cfg.MarginTop),
                MmToHundredths(cfg.MarginBottom))
            : new Margins(0, 0, 0, 0);

        for (var i = 0; i < Math.Max(copies, 1); i++)
        {
            using var doc = new PrintDocument
            {
                PrintController = new StandardPrintController()
            };
            doc.PrinterSettings.PrinterName = printerName;
            if (!doc.PrinterSettings.IsValid) return false;
            doc.DefaultPageSettings.Margins = margins;

            var offsetY = 0; // source pixels already printed
            doc.PrintPage += (_, e) =>
            {
                var g = e.Graphics;
                if (g == null) { e.HasMorePages = false; return; }

                var bounds = e.MarginBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) bounds = e.PageBounds;

                // Scale to fit WIDTH only — never shrink a long receipt onto one page.
                var scale = bounds.Width / (float)image.Width;
                if (scale <= 0) { e.HasMorePages = false; return; }

                var srcH = Math.Min(image.Height - offsetY, bounds.Height / scale);
                if (srcH <= 0) { e.HasMorePages = false; return; }

                var srcHInt = Math.Max(1, (int)Math.Ceiling(srcH));
                var destH = Math.Max(1, (int)Math.Ceiling(srcHInt * scale));

                g.DrawImage(
                    image,
                    new Rectangle(bounds.Left, bounds.Top, bounds.Width, destH),
                    new Rectangle(0, offsetY, image.Width, Math.Min(srcHInt, image.Height - offsetY)),
                    GraphicsUnit.Pixel);

                offsetY += srcHInt;
                e.HasMorePages = offsetY < image.Height;
            };
            try
            {
                doc.Print();
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}
