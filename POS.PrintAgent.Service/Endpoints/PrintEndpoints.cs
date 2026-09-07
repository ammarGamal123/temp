using System.Text.Json;
using POS.PrintAgent.Core.Interfaces;
using POS.PrintAgent.Core.Models;
using POS.PrintAgent.Service.Services;
using POS.PrintAgent.Service.Utilities;
using System.Runtime.Versioning;

namespace POS.PrintAgent.Service.Endpoints;

[SupportedOSPlatform("windows")]
public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this WebApplication app)
    {
        // Health check
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.1.0"
        }))
        .WithTags("Health")
        .WithSummary("Health check")
        .WithDescription("No API key required. Returns service status and version.")
        ;

        // Get available printers
        app.MapGet("/printers", (IPrinterService printerService) =>
        {
            var printers = printerService.GetAvailablePrinters();
            return Results.Ok(printers);
        })
        .WithTags("Printers")
        .WithSummary("List installed printers")
        .WithDescription("Returns all Windows printers. Requires X-Api-Key header.")
        ;

        // Check specific printer status
        app.MapGet("/printers/{name}/status", (string name, IPrinterService printerService) =>
        {
            var isOnline = printerService.IsPrinterOnline(name);
            return Results.Ok(new { name, isOnline });
        })
        .WithTags("Printers")
        .WithSummary("Check printer online status")
        .WithDescription("Checks if a specific printer is online. Requires X-Api-Key.")
        ;

        // Print endpoint — flexible: accepts BOTH canonical (items/nameEn/total)
        // and legacy frontend shape (details/productName/lineTotal/companyName/createdAt/remainingAmount)
        // Parser is backward-compatible with docs/sample-print-request.json
        app.MapPost("/print", async (
            JsonElement raw,
            IPrinterService printerService,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("PrintEndpoints");
            PrintRequest request;
            try
            {
                request = FlexibleRequestParser.ParsePrintRequest(raw);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse print request");
                return Results.BadRequest(new { message = $"Invalid request format: {ex.Message}" });
            }

            if (string.IsNullOrEmpty(request.PrinterName))
                return Results.BadRequest(new { message = "PrinterName is required" });

            //if (request.Invoice == null || !request.Invoice.Items.Any()) 
            if (request.Invoice?.Items is not {Count: > 0 })
            {
                return Results.BadRequest(new { message = "Invoice data with items is required" });
            } // عدل طريقة الكتابة 
            // Log alias usage for observability (helps deprecate legacy shape)
            if (raw.TryGetProperty("invoice", out var invCheck) && invCheck.TryGetProperty("details", out _))
                logger.LogInformation("Print request used legacy 'details' alias for {Invoice}", request.Invoice.InvoiceNumber);

            var result = await printerService.PrintAsync(request, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.Problem(result.Message);
        })
        .WithTags("Printing")
        .WithSummary("Print receipt/invoice (flexible)")
        .WithDescription("Accepts BOTH canonical (invoice.items / nameEn / total) and legacy frontend (invoice.details / productName / lineTotal). PrinterConfig is per-request. Requires X-Api-Key. See docs/sample-print-request.json and docs/FRONTEND_FIX_400.md.")
        ;

        // Open cash drawer
        app.MapPost("/cash-drawer/open", (OpenDrawerRequest request) =>
        {
            if (string.IsNullOrEmpty(request.PrinterName))
                return Results.BadRequest(new { message = "PrinterName is required" });

            var bytes = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA };
            var success = RawPrinterHelper.SendBytesToPrinter(request.PrinterName, bytes);

            return success
                ? Results.Ok(new { success = true })
                : Results.Problem("Failed to open cash drawer");
        })
        .WithTags("Hardware")
        .WithSummary("Open cash drawer")
        .WithDescription("Sends ESC p drawer kick (0x1B 0x70 0x00 0x19 0xFA). Requires X-Api-Key.")
        ;

        // Test print - للتأكد من إن الطابعة شغالة
        app.MapPost("/test", async (
            TestPrintRequest request,
            IPrinterService printerService) =>
        {
            if (string.IsNullOrEmpty(request.PrinterName))
                return Results.BadRequest(new { message = "PrinterName is required" });

            var testInvoice = new PrintRequest
            {
                PrinterName = request.PrinterName,
                PrinterConfig = request.PrinterConfig ?? new PrinterConfiguration { Name = request.PrinterName },
                Invoice = new InvoiceData
                {
                    CompanyNameAr = "اختبار الطباعة",
                    CompanyNameEn = "Print Test",
                    VatNumber = "000000000000000",
                    Address = "Test Address",
                    Phone = "000000000",
                    InvoiceNumber = "TEST-001",
                    InvoiceDate = DateTime.Now,
                    CashierName = "Test User",
                    Items = new List<InvoiceItem>
                    {
                        new() { NameEn = "Test Item 1", Quantity = 1, UnitPrice = 10, Total = 10 },
                        new() { NameEn = "Test Item 2", Quantity = 2, UnitPrice = 5, Total = 10 }
                    },
                    SubTotal = 20,
                    TotalTax = 3,
                    GrandTotal = 23,
                    PaidAmount = 25,
                    ChangeAmount = 2,
                    PaymentMethod = "Cash",
                    FooterMessage = "This is a test receipt"
                }
            };

            var result = await printerService.PrintAsync(testInvoice);
            return result.Success ? Results.Ok(result) : Results.Problem(result.Message);
        })
        .WithTags("Printing")
        .WithSummary("Test print")
        .WithDescription("Prints a fixed test receipt (no invoice needed). Useful with Microsoft Print to PDF. Requires X-Api-Key.")
        ;

        // Test HTML print - للتأكد من إن HTML rendering شغال
        app.MapPost("/test/html", async (
            TestPrintRequest request,
            IHtmlReceiptService htmlReceiptService) =>
        {
            if (string.IsNullOrEmpty(request.PrinterName))
                return Results.BadRequest(new { message = "PrinterName is required" });

            var testInvoice = new InvoiceData
            {
                CompanyNameAr = "شركة عريب",
                CompanyNameEn = "Araib Group",
                VatNumber = "300000000000003",
                CrNumber = "1010000000",
                Address = "Riyadh, Saudi Arabia",
                Phone = "+966 50 000 0000",
                InvoiceNumber = "INV-2025-0001",
                InvoiceDate = DateTime.Now,
                CashierName = "Ahmed",
                TableNumber = "5",
                Items = new List<InvoiceItem>
                {
                    new() { NameAr = "برجر", NameEn = "Burger", Quantity = 2, UnitPrice = 25, Total = 50 },
                    new() { NameAr = "بيبسي", NameEn = "Pepsi", Quantity = 2, UnitPrice = 5, Total = 10 }
                },
                SubTotal = 60,
                TotalTax = 9,
                GrandTotal = 69,
                PaidAmount = 70,
                ChangeAmount = 1,
                PaymentMethod = "Cash",
                QrCodeBase64 = "VATCA QR Code Test Data",
                FooterMessage = "Visit us again!"
            };

            var printerConfig = request.PrinterConfig ?? new PrinterConfiguration
            {
                Name = request.PrinterName,
                PaperWidth = 80,
                Dpi = 203,
                QrCodeSize = 80
            };

            try
            {
                var bytes = await htmlReceiptService.RenderReceiptAsync(testInvoice, printerConfig, Core.Enums.PrintJobType.Receipt);
                return Results.Ok(new { success = true, message = "HTML receipt rendered successfully", bytesLength = bytes.Length });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, message = $"HTML rendering failed: {ex.Message}", fallback = "Will use ESC/POS on next /print call" });
            }
        })
        .WithTags("Printing")
        .WithSummary("Test HTML receipt rendering")
        .WithDescription("Renders a test receipt using HTML/PuppeteerSharp. Returns byte count on success. Requires X-Api-Key.")
        ;

        // Debug: get raw raster bytes for inspection
        app.MapPost("/debug/raster", async (
            TestPrintRequest request,
            IHtmlReceiptService htmlReceiptService) =>
        {
            if (string.IsNullOrEmpty(request.PrinterName))
                return Results.BadRequest(new { message = "PrinterName is required" });

            var testInvoice = new InvoiceData
            {
                CompanyNameAr = "شركة عريب",
                CompanyNameEn = "Araib Group",
                VatNumber = "300000000000003",
                CrNumber = "1010000000",
                Address = "Riyadh, Saudi Arabia",
                Phone = "+966 50 000 0000",
                InvoiceNumber = "INV-2025-0001",
                InvoiceDate = DateTime.Now,
                CashierName = "Ahmed",
                TableNumber = "5",
                Items = new List<InvoiceItem>
                {
                    new() { NameAr = "برجر", NameEn = "Burger", Quantity = 2, UnitPrice = 25, Total = 50 },
                    new() { NameAr = "بيبسي", NameEn = "Pepsi", Quantity = 2, UnitPrice = 5, Total = 10 }
                },
                SubTotal = 60,
                TotalTax = 9,
                GrandTotal = 69,
                PaidAmount = 70,
                ChangeAmount = 1,
                PaymentMethod = "Cash",
                QrCodeBase64 = "VATCA QR Code Test Data",
                FooterMessage = "Visit us again!"
            };

            var printerConfig = request.PrinterConfig ?? new PrinterConfiguration
            {
                Name = request.PrinterName,
                PaperWidth = 80,
                Dpi = 203,
                QrCodeSize = 80
            };

            try
            {
                var rasterBytes = await htmlReceiptService.RenderReceiptAsync(testInvoice, printerConfig, Core.Enums.PrintJobType.Receipt);
                var hexPreview = BitConverter.ToString(rasterBytes.Take(64).ToArray()).Replace("-", " ");
                return Results.Ok(new
                {
                    success = true,
                    totalBytes = rasterBytes.Length,
                    hexPreview,
                    stripCount = (rasterBytes.Length / (80 * 256 + 8)) + 1
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, message = ex.Message });
            }
        })
        .WithTags("Debug")
        .WithSummary("Debug: raw raster bytes inspection")
        .WithDescription("Returns raw ESC/POS raster bytes with hex preview. Requires X-Api-Key.")
        ;
    }

    public record OpenDrawerRequest(string PrinterName);
    public record TestPrintRequest(string PrinterName, PrinterConfiguration? PrinterConfig);
}

// المشكله دي بتحصل في كل انواع الطابعات ؟ اه
// تمام
//{
//    "printerName": "EPSON L3250 Series (النسخة 3)",
//    "jobType": 1,
//    "printerConfig": {
//        "name": "EPSON L3250 Series (النسخة 3)",
//        "printerType": "thermal",
//        "paperWidth": 80,
//        "paperHeight": 200,
//        "dpi": 203,
//        "marginTop": 2,
//        "marginBottom": 2,
//        "marginLeft": 1,
//        "marginRight": 1,
//        "fontSizeSmall": 8,
//        "fontSizeMedium": 10,
//        "fontSizeLarge": 14,
//        "qrCodeSize": 80,
//        "barcodeSize": 60,
//        "categoryId": 179,
//        "isDefault": true,
//        "isActive": true
//    },
//    "invoice": {
//    "invoiceNumber": "S20260907-12",
//        "orderNumber": "",
//        "createdAt": "2026-09-07T09:04:04.069351",
//        "customerName": "عميل نقدي",
//        "companyName": "",
//        "companyVatNumber": "",
//        "total": 115,
//        "totalTax": 15,
//        "totalDiscount": 0,
//        "paidAmount": 115,
//        "remainingAmount": 0,
//        "details": [ // instead of details use items but i will try it now 
                    // بص انا معايا opencode شايف اساسا المشروع ممكن يساعدك تعمل التغيير بتاعك
                    // لو حابب تستخدمه عشان يوفر عليك عرفني تمام؟؟
                    // تمام دقي? تمام
//            {
//        "lineNumber": 1,
//                "productName": "شاي",
//                "quantity": 1,
//                "unitPrice": 115,
//                "discountAmount": 0,
//                "taxAmount": 15,
//                "lineTotal": 115,
//                "note": "",
//                "categoryName": "مكسرات"
//            }
//        ]
//    },
//    "copies": 1,
//    "openCashDrawer": false
//}

// now can send details or items both are correct 
// okay, now i need to publish to send the published file to ammar right?
// done 
// great what should i send to ammar i think the installer right?
// which ammar db dev manager
// not the db manage , who is the head  
// gobran???
// ammar asked to send the file to test first the new installer
// send publish file 