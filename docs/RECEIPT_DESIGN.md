# Receipt Design Guide

## Overview

POS Print Agent v1.1 uses server-side HTML/CSS templates for receipt rendering.
Templates are static `.html` files in the `Templates/` folder. Edit them to change receipt design.

**No code rebuild needed** — just edit the `.html` file, restart the service.

## Template Files

| File | Purpose | Triggered by |
|---|---|---|
| `Templates/receipt-cashier.html` | Full cashier receipt (company, items, totals, QR, payment) | `jobType: 0` (Receipt) |
| `Templates/receipt-kitchen.html` | Kitchen order (order#, table, items, notes) | `jobType: 1` (KitchenOrder) or `jobType: 2` (BarOrder) |

## How to Edit

1. Open the `.html` file in any text editor
2. Edit the HTML/CSS
3. Save
4. Restart the service: `sc stop POSPrintAgent && sc start POSPrintAgent` (as Admin)
5. Test: `POST /test` or `POST /print` with sample payload

## How to Preview in Chrome

1. Open the template file directly in Chrome (drag & drop)
2. Use Chrome DevTools device toolbar (Ctrl+Shift+M) and set width to **302px** (80mm @ 96dpi)
3. Or press Ctrl+P to "Save as PDF" to see the print layout

## Template Variables

Variables use `{{placeholder}}` syntax and are replaced at runtime with invoice data.

### Common Variables (both templates)

| Variable | Source | Example |
|---|---|---|
| `{{paperWidth}}` | `config.PaperWidth` | `80` |
| `{{marginTop}}` | `config.MarginTop` | `2` |
| `{{marginBottom}}` | `config.MarginBottom` | `2` |
| `{{marginLeft}}` | `config.MarginLeft` | `1` |
| `{{marginRight}}` | `config.MarginRight` | `1` |
| `{{fontSizeSmall}}` | `config.FontSizeSmall` | `8` |
| `{{fontSizeMedium}}` | `config.FontSizeMedium` | `10` |
| `{{fontSizeLarge}}` | `config.FontSizeLarge` | `14` |
| `{{qrCodeWidth}}` | `config.QrCodeSize` | `80` |

### Cashier Receipt Variables

| Variable | Source | Example |
|---|---|---|
| `{{CompanyNameAr}}` | `invoice.CompanyNameAr` | شركة عريب |
| `{{CompanyNameEn}}` | `invoice.CompanyNameEn` | Araib Group |
| `{{VatNumber}}` | `invoice.VatNumber` | 300000000000003 |
| `{{CrNumber}}` | `invoice.CrNumber` | 1010000000 |
| `{{Address}}` | `invoice.Address` | Riyadh, Saudi Arabia |
| `{{Phone}}` | `invoice.Phone` | +966 50 000 0000 |
| `{{InvoiceNumber}}` | `invoice.InvoiceNumber` | INV-2025-0001 |
| `{{InvoiceDate}}` | `invoice.InvoiceDate` | 2025-04-19 10:30 |
| `{{CashierName}}` | `invoice.CashierName` | Ahmed |
| `{{TableRow}}` | Pre-rendered HTML | `<div class="info-row">...Table: 5...</div>` |
| `{{ItemsHtml}}` | Pre-rendered HTML loop | `<div class="item-name">...</div>` |
| `{{SubTotal}}` | `invoice.SubTotal` | 60.00 |
| `{{TotalTax}}` | `invoice.TotalTax` | 9.00 |
| `{{GrandTotal}}` | `invoice.GrandTotal` | 69.00 |
| `{{PaymentMethod}}` | `invoice.PaymentMethod` | Cash |
| `{{PaidAmount}}` | `invoice.PaidAmount` | 70.00 |
| `{{DiscountRow}}` | Pre-rendered HTML | `<div class="total-row">...` or empty |
| `{{ChangeRow}}` | Pre-rendered HTML | `<div class="payment-row">...` or empty |
| `{{FooterMessage}}` | `invoice.FooterMessage` | Visit us again! |
| `{{LogoHtml}}` | Pre-rendered HTML | `<img src="data:image/png;base64,...">` or empty |
| `{{QrCodeImg}}` | Generated QR PNG | `<img src="data:image/png;base64,...">` |

### Kitchen Receipt Variables

| Variable | Source | Example |
|---|---|---|
| `{{InvoiceNumber}}` | `invoice.InvoiceNumber` | INV-2025-0001 |
| `{{InvoiceTime}}` | `invoice.InvoiceDate` (HH:mm:ss) | 10:30:00 |
| `{{CashierName}}` | `invoice.CashierName` | Ahmed |
| `{{TableBlock}}` | Pre-rendered HTML | `<div class="table-num">...` or empty |
| `{{OrderTypeRow}}` | Pre-rendered HTML | `<div class="order-info">...` or empty |
| `{{ItemsHtml}}` | Pre-rendered HTML loop | `<div class="item">...</div>` |

## Adding a Logo

1. Set `logoBase64` in the invoice JSON payload (base64-encoded PNG)
2. The template includes `{{LogoHtml}}` which renders as:
   ```html
   <img src="data:image/png;base64,{base64data}" style="max-width:60%;max-height:20mm">
   ```
3. Recommended: PNG, max 200KB, width 300-600px

## Arabic RTL Support

- Templates use `dir="rtl"` on `<html>` element
- Arabic text renders natively in Chrome/PuppeteerSharp
- Font family: `'Segoe UI', Tahoma, Arial, sans-serif` (available on all Windows)
- No special encoding needed — HTML handles UTF-8 natively

## Paper Sizes

- **80mm thermal:** Set `paperWidth: 80` in `printerConfig`. CSS `@page { size: 80mm auto; }`
- **58mm thermal:** Set `paperWidth: 58` in `printerConfig`. CSS `@page { size: 58mm auto; }`
- **Paper height:** Auto (content-driven). The screenshot captures the full page.

## Fallback Behavior

If PuppeteerSharp/Chrome fails for any reason:
1. Service logs warning: `"HTML rendering failed, using ESC/POS fallback"`
2. Falls back to `ThermalReceiptBuilder` (text-mode ESC/POS)
3. Receipt still prints, just without HTML design

This ensures the service always works even if Chrome is not available.

## Testing

### Test HTML rendering only (no physical print):
```bash
curl -X POST http://localhost:5050/test/html \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"printerName":"Microsoft Print to PDF"}'
```

### Test full print (HTML → printer):
```bash
curl -X POST http://localhost:5050/print \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"printerName":"EPSON TM-T82","jobType":0,"invoice":{...}}'
```

## Troubleshooting

| Issue | Solution |
|---|---|
| Template not found | Check `Templates/` folder exists next to `appsettings.json` in publish |
| Arabic text shows `???` | Ensure `dir="rtl"` on `<html>` and UTF-8 charset |
| QR code not showing | Check `invoice.qrCodeBase64` is not empty |
| Chrome not found | Service auto-downloads Chromium on first run (~150MB) |
| Print too wide/narrow | Adjust `paperWidth` in `printerConfig` |
| Fallback to ESC/POS | Check logs for PuppeteerSharp errors |
