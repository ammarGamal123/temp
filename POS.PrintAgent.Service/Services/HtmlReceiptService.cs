using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using POS.PrintAgent.Core.Enums;
using POS.PrintAgent.Core.Models;
using POS.PrintAgent.Service.Utilities;
using PuppeteerSharp;

namespace POS.PrintAgent.Service.Services;

public interface IHtmlReceiptService
{
    Task<byte[]> RenderReceiptAsync(InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default);
    Task<byte[]> RenderReceiptPngAsync(InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default);
    Task<string> RenderReceiptHtmlAsync(InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default);
    Task<byte[]> RenderRawHtmlAsync(string html, PrinterConfiguration config, CancellationToken ct = default);
}

[SupportedOSPlatform("windows")]
public class HtmlReceiptService : IHtmlReceiptService
{
    private readonly ILogger<HtmlReceiptService> _logger;
    private readonly string _templateDir;
    private readonly Dictionary<string, string> _templateCache = new();
    private IBrowser? _browser;
    private readonly SemaphoreSlim _browserLock = new(1, 1);

    private static readonly string[] ChromePaths =
    [
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\Application\chrome.exe")
    ];

    public HtmlReceiptService(ILogger<HtmlReceiptService> logger)
    {
        _logger = logger;
        _templateDir = Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    public async Task<byte[]> RenderReceiptAsync(
        InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default)
    {
        var html = await RenderReceiptHtmlAsync(invoice, config, jobType, ct);
        var pngData = await RenderHtmlToPngAsync(html, config, ct);
        return EscapeGSImageHelper.ConvertPngToEscPosRaster(pngData, config.PaperWidth, config.Dpi);
    }

    public async Task<byte[]> RenderReceiptPngAsync(
        InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default)
    {
        var html = await RenderReceiptHtmlAsync(invoice, config, jobType, ct);
        return await RenderHtmlToPngAsync(html, config, ct);
    }

    public async Task<string> RenderReceiptHtmlAsync(
        InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType, CancellationToken ct = default)
    {
        var templateFile = jobType switch
        {
            PrintJobType.Receipt => "receipt-cashier.html",
            PrintJobType.KitchenOrder or PrintJobType.BarOrder => "receipt-kitchen.html",
            _ => "receipt-cashier.html"
        };

        var templatePath = Path.Combine(_templateDir, templateFile);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Receipt template not found: {templatePath}");

        var template = LoadTemplate(templatePath);
        var html = HydrateTemplate(template, invoice, config, jobType);

        _logger.LogDebug("Hydrated HTML length: {Length} chars for {JobType}", html.Length, jobType);

        return html;
    }

    public async Task<byte[]> RenderRawHtmlAsync(string html, PrinterConfiguration config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML content is required", nameof(html));

        var pngData = await RenderHtmlToPngAsync(html, config, ct);
        return EscapeGSImageHelper.ConvertPngToEscPosRaster(pngData, config.PaperWidth, config.Dpi);
    }

    private string LoadTemplate(string path)
    {
        if (_templateCache.TryGetValue(path, out var cached))
            return cached;

        var content = File.ReadAllText(path, Encoding.UTF8);
        _templateCache[path] = content;
        return content;
    }

    private string HydrateTemplate(string template, InvoiceData invoice, PrinterConfiguration config, PrintJobType jobType)
    {
        var replacements = new Dictionary<string, string>
        {
            ["{{paperWidth}}"] = config.PaperWidth.ToString(),
            ["{{marginTop}}"] = config.MarginTop.ToString(),
            ["{{marginBottom}}"] = config.MarginBottom.ToString(),
            ["{{marginLeft}}"] = config.MarginLeft.ToString(),
            ["{{marginRight}}"] = config.MarginRight.ToString(),
            ["{{fontSizeSmall}}"] = config.FontSizeSmall.ToString(),
            ["{{fontSizeMedium}}"] = config.FontSizeMedium.ToString(),
            ["{{fontSizeLarge}}"] = config.FontSizeLarge.ToString(),
            ["{{qrCodeWidth}}"] = config.QrCodeSize.ToString(),
        };

        if (jobType == PrintJobType.Receipt)
        {
            replacements["{{CompanyNameAr}}"] = Escape(invoice.CompanyNameAr);
            replacements["{{CompanyNameEn}}"] = Escape(invoice.CompanyNameEn);
            replacements["{{VatNumber}}"] = Escape(invoice.VatNumber);
            replacements["{{CrNumber}}"] = Escape(invoice.CrNumber);
            replacements["{{Address}}"] = Escape(invoice.Address);
            replacements["{{Phone}}"] = Escape(invoice.Phone);
            replacements["{{InvoiceNumber}}"] = Escape(invoice.InvoiceNumber);
            replacements["{{InvoiceDate}}"] = invoice.InvoiceDate.ToString("yyyy-MM-dd HH:mm");
            replacements["{{CashierName}}"] = Escape(invoice.CashierName);
            replacements["{{TableRow}}"] = string.IsNullOrEmpty(invoice.TableNumber)
                ? ""
                : $"<div class=\"info-row\"><span class=\"info-label\">Table:</span><span class=\"info-value\">{Escape(invoice.TableNumber)}</span></div>";
            replacements["{{ItemsHtml}}"] = BuildCashierItemsHtml(invoice);
            replacements["{{SubTotal}}"] = invoice.SubTotal.ToString("N2");
            replacements["{{TotalTax}}"] = invoice.TotalTax.ToString("N2");
            replacements["{{GrandTotal}}"] = invoice.GrandTotal.ToString("N2");
            replacements["{{PaymentMethod}}"] = Escape(invoice.PaymentMethod);
            replacements["{{PaidAmount}}"] = invoice.PaidAmount.ToString("N2");
            replacements["{{DiscountRow}}"] = invoice.TotalDiscount > 0
                ? $"<div class=\"total-row\"><span>الخصم:</span><span class=\"left\">-{invoice.TotalDiscount:N2}</span></div>"
                : "";
            replacements["{{ChangeRow}}"] = invoice.ChangeAmount > 0
                ? $"<div class=\"payment-row\"><span>الباقي:</span><span class=\"left\">{invoice.ChangeAmount:N2}</span></div>"
                : "";
            replacements["{{FooterMessage}}"] = Escape(invoice.FooterMessage ?? "");
            replacements["{{LogoHtml}}"] = !string.IsNullOrEmpty(invoice.LogoBase64)
                ? $"<div style=\"text-align:center;margin-bottom:4px\"><img src=\"data:image/png;base64,{invoice.LogoBase64}\" style=\"max-width:60%;max-height:20mm\"></div>"
                : "";
            replacements["{{QrCodeImg}}"] = !string.IsNullOrEmpty(invoice.QrCodeBase64)
                ? GenerateQrImgTag(invoice.QrCodeBase64)
                : "<div style=\"height:2px\"></div>";
        }
        else
        {
            replacements["{{InvoiceNumber}}"] = Escape(invoice.InvoiceNumber);
            replacements["{{InvoiceTime}}"] = invoice.InvoiceDate.ToString("HH:mm:ss");
            replacements["{{CashierName}}"] = Escape(invoice.CashierName);
            replacements["{{TableBlock}}"] = !string.IsNullOrEmpty(invoice.TableNumber)
                ? $"<div class=\"table-num\">الطاولة: {Escape(invoice.TableNumber)}</div>"
                : "";
            replacements["{{OrderTypeRow}}"] = !string.IsNullOrEmpty(invoice.OrderType)
                ? $"<div class=\"order-info\">النوع: {Escape(invoice.OrderType)}</div>"
                : "";
            replacements["{{ItemsHtml}}"] = BuildKitchenItemsHtml(invoice);
        }

        var result = template;
        foreach (var kvp in replacements)
        {
            result = result.Replace(kvp.Key, kvp.Value, StringComparison.Ordinal);
        }
        return result;
    }

    private static string BuildCashierItemsHtml(InvoiceData invoice)
    {
        var sb = new StringBuilder();
        foreach (var item in invoice.Items)
        {
            var name = !string.IsNullOrEmpty(item.NameAr) && !string.IsNullOrEmpty(item.NameEn)
                ? $"{Escape(item.NameAr)} ({Escape(item.NameEn)})"
                : !string.IsNullOrEmpty(item.NameAr)
                    ? Escape(item.NameAr)
                    : Escape(item.NameEn);

            sb.AppendLine($"<div class=\"item-name\">{name}</div>");
            sb.AppendLine($"<div class=\"item-detail\"><span class=\"left\">{item.Quantity:0.##} x {item.UnitPrice:N2} = {item.Total:N2}</span></div>");

            if (!string.IsNullOrEmpty(item.Notes))
                sb.AppendLine($"<div class=\"item-notes\">&gt;&gt; {Escape(item.Notes)}</div>");
        }
        return sb.ToString();
    }

    private static string BuildKitchenItemsHtml(InvoiceData invoice)
    {
        var sb = new StringBuilder();
        foreach (var item in invoice.Items)
        {
            var name = !string.IsNullOrEmpty(item.NameAr) && !string.IsNullOrEmpty(item.NameEn)
                ? $"{Escape(item.NameAr)} ({Escape(item.NameEn)})"
                : !string.IsNullOrEmpty(item.NameAr)
                    ? Escape(item.NameAr)
                    : Escape(item.NameEn);

            sb.AppendLine($"<div class=\"item\">{item.Quantity:0} x {name}</div>");

            if (!string.IsNullOrEmpty(item.Notes))
                sb.AppendLine($"<div class=\"item-notes\">&gt;&gt; {Escape(item.Notes)}</div>");
        }
        return sb.ToString();
    }

    private static string GenerateQrImgTag(string qrCodeBase64)
    {
        var qrGenerator = new QRCoder.QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(qrCodeBase64, QRCoder.QRCodeGenerator.ECCLevel.M);
        var qrCode = new QRCoder.PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(10);
        var base64 = Convert.ToBase64String(pngBytes);
        return $"<img src=\"data:image/png;base64,{base64}\" style=\"width:80px;height:80px\">";
    }

    private async Task<byte[]> RenderHtmlToPngAsync(string html, PrinterConfiguration config, CancellationToken ct)
    {
        var browser = await GetBrowserAsync(ct);
        var page = await browser.NewPageAsync();

        try
        {
            var viewportWidthPx = (int)(config.PaperWidth * config.Dpi / 25.4);
            var viewportHeightPx = 800;

            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = viewportWidthPx,
                Height = viewportHeightPx,
                DeviceScaleFactor = 1
            });

            await page.SetContentAsync(html, new SetContentOptions { WaitUntil = [WaitUntilNavigation.Load] });

            var screenshotOptions = new ScreenshotOptions
            {
                FullPage = true,
                Type = ScreenshotType.Png,
                Clip = null,
                CaptureBeyondViewport = false
            };

            var pngData = await page.ScreenshotDataAsync(screenshotOptions);

            _logger.LogInformation("HTML rendered to PNG: {Width}px x {Length} bytes", viewportWidthPx, pngData.Length);

            return pngData;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser != null && _browser.IsConnected)
            return _browser;

        await _browserLock.WaitAsync(ct);
        try
        {
            if (_browser != null && _browser.IsConnected)
                return _browser;

            _browser?.Dispose();

            var chromePath = ChromePaths.FirstOrDefault(File.Exists);

            LaunchOptions launchOptions;
            if (chromePath != null)
            {
                _logger.LogInformation("Using Chrome at {Path}", chromePath);
                launchOptions = new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = chromePath,
                    Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu"]
                };
            }
            else
            {
                _logger.LogInformation("Chrome not found, using PuppeteerSharp bundled Chromium");
                launchOptions = new LaunchOptions
                {
                    Headless = true,
                    Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu"]
                };
            }

            _browser = await Puppeteer.LaunchAsync(launchOptions);
            _logger.LogInformation("Browser launched successfully");

            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")    
            .Replace("'", "&#39;");
    }
}
