using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace POS.PrintAgent.Service.Utilities;

/// <summary>
/// Converts PNG/raster images to ESC/POS bit-image bytes using GS v 0 command.
/// Supports strip-based banding (256px strips) for reliable thermal printer output.
/// </summary>
[SupportedOSPlatform("windows")]
public static class EscapeGSImageHelper
{
    private const int MaxStripHeight = 256;

    /// <summary>
    /// Converts a PNG byte array to ESC/POS raster bytes, resizing to paper width.
    /// Uses strip-based banding for reliable printer output.
    /// </summary>
    public static byte[] ConvertPngToEscPosRaster(byte[] pngData, int paperWidthMm, int dpi = 203)
    {
        var targetWidthPx = (int)(paperWidthMm * dpi / 25.4);
        targetWidthPx = (targetWidthPx + 7) / 8 * 8;

        using var inputStream = new MemoryStream(pngData);
        using var original = new Bitmap(inputStream);

        var width = targetWidthPx;
        var height = Math.Max(1, (int)((double)original.Height / original.Width * targetWidthPx));

        using var resized = new Bitmap(original, new Size(width, height));
        using var monochrome = ConvertToMonochrome(resized);

        return BuildBandedEscPosRaster(monochrome, width);
    }

    /// <summary>
    /// Converts PNG to ESC/POS without resizing (uses original dimensions).
    /// </summary>
    public static byte[] ConvertPngToEscPosRaster(byte[] pngData, int dpi = 203)
    {
        using var inputStream = new MemoryStream(pngData);
        using var original = new Bitmap(inputStream);

        var width = (original.Width + 7) / 8 * 8;
        var height = original.Height;

        using var monochrome = ConvertToMonochrome(original);

        return BuildBandedEscPosRaster(monochrome, width);
    }

    private static Bitmap ConvertToMonochrome(Bitmap source)
    {
        var width = source.Width;
        var height = source.Height;

        var mono = new Bitmap(width, height, PixelFormat.Format1bppIndexed);

        var sourceData = source.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        var monoData = mono.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format1bppIndexed);

        try
        {
            var srcStride = sourceData.Stride;
            var monoStride = monoData.Stride;

            unsafe
            {
                var pSrc = (byte*)sourceData.Scan0;
                var pDst = (byte*)monoData.Scan0;

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var srcOffset = y * srcStride + x * 3;
                        var b = pSrc[srcOffset];
                        var g = pSrc[srcOffset + 1];
                        var r = pSrc[srcOffset + 2];

                        var gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                        if (gray < 200)
                        {
                            var monoByteOffset = y * monoStride + (x >> 3);
                            pDst[monoByteOffset] |= (byte)(0x80 >> (x & 7));
                        }
                    }
                }
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
            mono.UnlockBits(monoData);
        }

        return mono;
    }

    /// <summary>
    /// Builds ESC/POS raster bytes using strip-based banding.
    /// Each strip is MaxStripHeight (256) dots tall, sent as separate GS v 0 commands.
    /// The printer concatenates them seamlessly as continuous output.
    /// </summary>
    private static byte[] BuildBandedEscPosRaster(Bitmap monochrome, int widthPx)
    {
        var widthBytes = (widthPx + 7) / 8;
        var height = monochrome.Height;

        using var ms = new MemoryStream();

        var totalStrips = (height + MaxStripHeight - 1) / MaxStripHeight;

        var bits = monochrome.LockBits(
            new Rectangle(0, 0, monochrome.Width, monochrome.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format1bppIndexed);

        try
        {
            var stride = bits.Stride;

            for (var stripIndex = 0; stripIndex < totalStrips; stripIndex++)
            {
                var stripY = stripIndex * MaxStripHeight;
                var stripHeight = Math.Min(MaxStripHeight, height - stripY);

                if (stripHeight <= 0) break;

                ms.WriteByte(0x1B);
                ms.WriteByte(0x40);

                ms.WriteByte(0x1D);
                ms.WriteByte(0x76);
                ms.WriteByte(0x30);
                ms.WriteByte(0x00);

                ms.WriteByte((byte)(widthBytes % 256));
                ms.WriteByte((byte)(widthBytes / 256));
                ms.WriteByte((byte)(stripHeight % 256));
                ms.WriteByte((byte)(stripHeight / 256));

                unsafe
                {
                    var pSrc = (byte*)bits.Scan0;

                    for (var y = 0; y < stripHeight; y++)
                    {
                        var srcRow = (stripY + y) * stride;

                        for (var x = 0; x < widthBytes; x++)
                        {
                            var b = pSrc[srcRow + x];
                            ms.WriteByte(b);
                        }
                    }
                }
            }
        }
        finally
        {
            monochrome.UnlockBits(bits);
        }

        return ms.ToArray();
    }
}
