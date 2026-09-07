using System.Text.Json;
using POS.PrintAgent.Core.Enums;
using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Service.Utilities;

/// <summary>
/// Compatibility layer: accepts BOTH canonical payload (docs/sample-print-request.json)
/// and frontend legacy shape (details/productName/lineTotal/companyName/createdAt/remainingAmount...).
/// Keeps agent backward-compatible without breaking strict contract.
/// </summary>
public static class FlexibleRequestParser
{
    public static PrintRequest ParsePrintRequest(JsonElement root)
    {
        // Helpers: case-insensitive key lookup trying multiple aliases
        static bool TryGet(JsonElement el, out JsonElement value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (el.TryGetProperty(key, out value)) return true;
                // case-insensitive fallback
                foreach (var prop in el.EnumerateObject())
                {
                    if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        static string? GetString(JsonElement el, params string[] keys)
        {
            if (TryGet(el, out var v, keys))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.ToString();
                if (v.ValueKind == JsonValueKind.Null) return null;
            }
            return null;
        }

        static decimal? GetDecimal(JsonElement el, params string[] keys)
        {
            if (TryGet(el, out var v, keys))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var sd)) return sd;
            }
            return null;
        }

        static int? GetInt(JsonElement el, params string[] keys)
        {
            if (TryGet(el, out var v, keys))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si)) return si;
            }
            return null;
        }

        static bool? GetBool(JsonElement el, params string[] keys)
        {
            if (TryGet(el, out var v, keys))
            {
                if (v.ValueKind == JsonValueKind.True) return true;
                if (v.ValueKind == JsonValueKind.False) return false;
                if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var sb)) return sb;
            }
            return null;
        }

        static DateTime? GetDateTime(JsonElement el, params string[] keys)
        {
            if (TryGet(el, out var v, keys))
            {
                if (v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var dt)) return dt;
            }
            return null;
        }

        // --- Top level ---
        var printerName = GetString(root, "printerName", "PrinterName", "printer", "Printer") ?? string.Empty;
        var jobTypeInt = GetInt(root, "jobType", "JobType") ?? 0;
        var copies = GetInt(root, "copies", "Copies") ?? 1;
        var openCashDrawer = GetBool(root, "openCashDrawer", "OpenCashDrawer") ?? false;

        // PrinterConfig
        PrinterConfiguration printerConfig = new() { Name = printerName };
        if (TryGet(root, out var pcEl, "printerConfig", "PrinterConfig", "printer_config", "config"))
        {
            if (pcEl.ValueKind == JsonValueKind.Object)
            {
                printerConfig = new PrinterConfiguration
                {
                    Name = GetString(pcEl, "name", "Name", "printerName") ?? printerName,
                    PrinterType = GetString(pcEl, "printerType", "PrinterType", "type") ?? "thermal",
                    PaperWidth = GetInt(pcEl, "paperWidth", "PaperWidth") ?? 80,
                    PaperHeight = GetInt(pcEl, "paperHeight", "PaperHeight") ?? 200,
                    Dpi = GetInt(pcEl, "dpi", "Dpi") ?? 203,
                    MarginTop = GetInt(pcEl, "marginTop", "MarginTop") ?? 2,
                    MarginBottom = GetInt(pcEl, "marginBottom", "MarginBottom") ?? 2,
                    MarginLeft = GetInt(pcEl, "marginLeft", "MarginLeft") ?? 1,
                    MarginRight = GetInt(pcEl, "marginRight", "MarginRight") ?? 1,
                    FontSizeSmall = GetInt(pcEl, "fontSizeSmall", "FontSizeSmall") ?? 8,
                    FontSizeMedium = GetInt(pcEl, "fontSizeMedium", "FontSizeMedium") ?? 10,
                    FontSizeLarge = GetInt(pcEl, "fontSizeLarge", "FontSizeLarge") ?? 14,
                    QrCodeSize = GetInt(pcEl, "qrCodeSize", "QrCodeSize") ?? 80,
                    BarcodeSize = GetInt(pcEl, "barcodeSize", "BarcodeSize") ?? 60,
                    CategoryId = GetInt(pcEl, "categoryId", "CategoryId") ?? 0,
                    IsDefault = GetBool(pcEl, "isDefault", "IsDefault") ?? false,
                    IsActive = GetBool(pcEl, "isActive", "IsActive") ?? true,
                };
            }
        }

        // Invoice
        InvoiceData invoice = new();
        if (TryGet(root, out var invEl, "invoice", "Invoice", "data", "order"))
        {
            if (invEl.ValueKind == JsonValueKind.Object)
            {
                // Company names: support companyName -> both Ar/En
                var companyNameAr = GetString(invEl, "companyNameAr", "CompanyNameAr");
                var companyNameEn = GetString(invEl, "companyNameEn", "CompanyNameEn");
                var companyNameGeneric = GetString(invEl, "companyName", "CompanyName", "company", "storeName");
                if (string.IsNullOrEmpty(companyNameAr) && !string.IsNullOrEmpty(companyNameGeneric))
                    companyNameAr = companyNameGeneric;
                if (string.IsNullOrEmpty(companyNameEn) && !string.IsNullOrEmpty(companyNameGeneric))
                    companyNameEn = companyNameGeneric;

                var vatNumber = GetString(invEl, "vatNumber", "VatNumber", "companyVatNumber", "CompanyVatNumber", "vat", "taxNumber") ?? string.Empty;
                var crNumber = GetString(invEl, "crNumber", "CrNumber", "cr", "commercialRegister") ?? string.Empty;
                var address = GetString(invEl, "address", "Address") ?? string.Empty;
                var phone = GetString(invEl, "phone", "Phone", "tel", "telephone") ?? string.Empty;
                var logoBase64 = GetString(invEl, "logoBase64", "LogoBase64", "logo");

                var invoiceNumber = GetString(invEl, "invoiceNumber", "InvoiceNumber", "invoiceNo", "number", "orderNumber") ?? string.Empty;
                // Frontend uses invoiceNumber correctly, but also has orderNumber separate
                // Prefer invoiceNumber; if empty and orderNumber exists with value, use it
                if (string.IsNullOrEmpty(invoiceNumber))
                    invoiceNumber = GetString(invEl, "orderNumber", "OrderNumber") ?? string.Empty;

                var invoiceDate = GetDateTime(invEl, "invoiceDate", "InvoiceDate", "createdAt", "CreatedAt", "date", "Date", "created_at", "timestamp") ?? DateTime.Now;
                var cashierName = GetString(invEl, "cashierName", "CashierName", "cashier", "userName") ?? "Cashier";
                var tableNumber = GetString(invEl, "tableNumber", "TableNumber", "table");
                var orderType = GetString(invEl, "orderType", "OrderType", "type");

                // Items: support both items and details/lines
                List<InvoiceItem> items = new();  // we will check if support both items and details
                JsonElement itemsEl = default;
                bool hasItems = TryGet(invEl, out itemsEl, "items", "Items", "details", "Details", "lines", "Lines", "products", "Products");
                if (hasItems && itemsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in itemsEl.EnumerateArray())
                    {
                        if (it.ValueKind != JsonValueKind.Object) continue;

                        var nameAr = GetString(it, "nameAr", "NameAr", "arabicName");
                        var nameEn = GetString(it, "nameEn", "NameEn", "englishName");
                        var productName = GetString(it, "productName", "ProductName", "name", "Name", "itemName", "title", "product");
                        if (string.IsNullOrEmpty(nameAr) && !string.IsNullOrEmpty(productName))
                            nameAr = productName;
                        if (string.IsNullOrEmpty(nameEn) && !string.IsNullOrEmpty(productName))
                            nameEn = productName;
                        // fallback to empty if still null
                        nameAr ??= string.Empty;
                        nameEn ??= string.Empty;

                        var quantity = GetDecimal(it, "quantity", "Quantity", "qty", "Qty", "count") ?? 1;
                        var unitPrice = GetDecimal(it, "unitPrice", "UnitPrice", "price", "Price", "unit_price") ?? 0;
                        var discount = GetDecimal(it, "discount", "Discount", "discountAmount", "DiscountAmount") ?? 0;
                        // taxRate vs taxAmount: if taxRate missing but taxAmount present, keep rate 15 (or derive if possible)
                        var taxRate = GetDecimal(it, "taxRate", "TaxRate", "tax", "vatRate");
                        var taxAmount = GetDecimal(it, "taxAmount", "TaxAmount");
                        if (taxRate == null && taxAmount != null)
                        {
                            // If we have taxAmount and lineTotal, we could estimate, but keep standard 15
                            taxRate = 15;
                        }
                        taxRate ??= 15;

                        var total = GetDecimal(it, "total", "Total", "lineTotal", "LineTotal", "amount", "Amount", "line_total", "subtotal") ?? 0;
                        var notes = GetString(it, "notes", "Notes", "note", "Note", "remark", "description") ?? string.Empty;
                        var categoryName = GetString(it, "categoryName", "CategoryName", "category");

                        items.Add(new InvoiceItem
                        {
                            NameAr = nameAr,
                            NameEn = nameEn,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            Discount = discount,
                            TaxRate = taxRate.Value,
                            Total = total,
                            Notes = string.IsNullOrEmpty(notes) ? null : notes,
                            CategoryName = categoryName
                        });
                    }
                }

                // Totals: support total vs grandTotal/subTotal aliases
                var totalDiscount = GetDecimal(invEl, "totalDiscount", "TotalDiscount", "discount", "Discount") ?? 0;
                var totalTax = GetDecimal(invEl, "totalTax", "TotalTax", "tax", "Tax", "vat", "totalTaxAmount") ?? 0;
                var grandTotal = GetDecimal(invEl, "grandTotal", "GrandTotal", "total", "Total", "amount", "Amount", "netTotal");
                var subTotal = GetDecimal(invEl, "subTotal", "SubTotal", "sub_total", "subtotal", "netAmount");
                // Frontend sends total=100 as grandTotal, no subTotal. Derive if missing.
                if (grandTotal == null && subTotal != null)
                    grandTotal = subTotal + totalTax - totalDiscount;
                if (subTotal == null && grandTotal != null)
                    subTotal = grandTotal - totalTax + totalDiscount;
                grandTotal ??= 0;
                subTotal ??= grandTotal - totalTax;

                var paidAmount = GetDecimal(invEl, "paidAmount", "PaidAmount", "paid", "Paid", "amountPaid") ?? (grandTotal ?? 0);
                var changeAmount = GetDecimal(invEl, "changeAmount", "ChangeAmount", "remainingAmount", "RemainingAmount", "change", "Change", "balance");
                // Frontend: remainingAmount=0 is amount still due, not change. For receipt, change = paid - grandTotal
                // If remainingAmount provided and changeAmount missing, compute change as paid - grandTotal if paid >= grandTotal else 0
                if (changeAmount == null)
                {
                    var remaining = GetDecimal(invEl, "remainingAmount", "RemainingAmount");
                    if (remaining != null)
                    {
                        // If frontend sends remainingAmount, change is typically 0 when remaining=0, else paid - grandTotal
                        changeAmount = paidAmount - (grandTotal ?? 0);
                        if (changeAmount < 0) changeAmount = 0;
                    }
                    else
                        changeAmount = 0;
                }

                var paymentMethod = GetString(invEl, "paymentMethod", "PaymentMethod", "payment", "payMethod") ?? "Cash";
                var qrCodeBase64 = GetString(invEl, "qrCodeBase64", "QrCodeBase64", "qrCode", "zatcaQr", "qr");
                var footerMessage = GetString(invEl, "footerMessage", "FooterMessage", "footer", "notes");
                var customerName = GetString(invEl, "customerName", "CustomerName", "customer", "clientName");
                var customerPhone = GetString(invEl, "customerPhone", "CustomerPhone", "customer_phone", "phoneNumber");

                // Preserve original invoiceNumber if we overwrote with orderNumber fallback
                var finalInvoiceNumber = GetString(invEl, "invoiceNumber", "InvoiceNumber") ?? invoiceNumber;

                invoice = new InvoiceData
                {
                    CompanyNameAr = companyNameAr ?? string.Empty,
                    CompanyNameEn = companyNameEn ?? string.Empty,
                    VatNumber = vatNumber,
                    CrNumber = crNumber,
                    Address = address,
                    Phone = phone,
                    LogoBase64 = logoBase64,
                    InvoiceNumber = finalInvoiceNumber,
                    InvoiceDate = invoiceDate,
                    CashierName = cashierName,
                    TableNumber = tableNumber,
                    OrderType = orderType,
                    Items = items,
                    SubTotal = subTotal ?? 0,
                    TotalDiscount = totalDiscount,
                    TotalTax = totalTax,
                    GrandTotal = grandTotal ?? 0,
                    PaidAmount = paidAmount,
                    ChangeAmount = changeAmount ?? 0,
                    PaymentMethod = paymentMethod,
                    QrCodeBase64 = qrCodeBase64,
                    FooterMessage = footerMessage,
                    CustomerName = customerName,
                    CustomerPhone = customerPhone
                };
            }
        }

        // Ensure printerConfig name fallback
        if (string.IsNullOrEmpty(printerConfig.Name))
            printerConfig.Name = printerName;

        // Build PrintRequest
        PrintJobType jobType = Enum.IsDefined(typeof(PrintJobType), jobTypeInt) ? (PrintJobType)jobTypeInt : PrintJobType.Receipt;

        return new PrintRequest
        {
            PrinterName = printerName,
            JobType = jobType,
            PrinterConfig = printerConfig,
            Invoice = invoice,
            Copies = copies,
            OpenCashDrawer = openCashDrawer
        };
    }
}
