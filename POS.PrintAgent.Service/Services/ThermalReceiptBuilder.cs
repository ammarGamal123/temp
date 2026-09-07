using POS.PrintAgent.Core.Interfaces;
using POS.PrintAgent.Core.Models;
using POS.PrintAgent.Service.Commands;
using POS.PrintAgent.Service.Utilities;
using System.Text;

namespace POS.PrintAgent.Service.Services;

public class ThermalReceiptBuilder : IReceiptBuilder
{
    private PrinterConfiguration _config;
    private int _width;

    public ThermalReceiptBuilder()
    {
        _config = new PrinterConfiguration();
        _width = PrinterConfigMapper.GetLineWidth(_config);
    }

    public void Configure(PrinterConfiguration config)
    {
        _config = config;
        _width = PrinterConfigMapper.GetLineWidth(config);
    }

    public byte[] BuildReceipt(InvoiceData invoice)
    {
        using var ms = new MemoryStream();

        Write(ms, EscPosCommands.Initialize);
        Write(ms, EscPosCommands.SetCodePageWindows1256);

        ApplyTopMargin(ms);

        BuildHeader(ms, invoice);
        BuildInvoiceInfo(ms, invoice);
        BuildItems(ms, invoice);
        BuildTotals(ms, invoice);
        BuildPaymentInfo(ms, invoice);

        if (!string.IsNullOrEmpty(invoice.QrCodeBase64))
            BuildQrCode(ms, invoice.QrCodeBase64);

        BuildFooter(ms, invoice);

        ApplyBottomMargin(ms);
        Write(ms, EscPosCommands.FullCut);

        return ms.ToArray();
    }

    public byte[] BuildKitchenOrder(InvoiceData invoice)
    {
        using var ms = new MemoryStream();

        Write(ms, EscPosCommands.Initialize);
        Write(ms, EscPosCommands.SetCodePageWindows1256);

        ApplyTopMargin(ms);

        Write(ms, EscPosCommands.AlignCenter);
        SetFontSize(ms, _config.FontSizeLarge);
        Write(ms, EscPosCommands.BoldOn);
        WriteText(ms, "KITCHEN ORDER");
        Write(ms, EscPosCommands.NewLine);
        ResetFontSize(ms);
        Write(ms, EscPosCommands.BoldOff);

        WriteText(ms, $"Order #: {invoice.InvoiceNumber}");
        Write(ms, EscPosCommands.NewLine);
        WriteText(ms, $"Time: {invoice.InvoiceDate:HH:mm:ss}");
        Write(ms, EscPosCommands.NewLine);

        if (!string.IsNullOrEmpty(invoice.TableNumber))
        {
            SetFontSize(ms, _config.FontSizeLarge);
            WriteText(ms, $"Table: {invoice.TableNumber}");
            Write(ms, EscPosCommands.NewLine);
            ResetFontSize(ms);
        }

        if (!string.IsNullOrEmpty(invoice.OrderType))
        {
            WriteText(ms, $"Type: {invoice.OrderType}");
            Write(ms, EscPosCommands.NewLine);
        }

        WriteSeparator(ms);
        Write(ms, EscPosCommands.AlignLeft);

        SetFontSize(ms, _config.FontSizeMedium);
        Write(ms, EscPosCommands.BoldOn);
        foreach (var item in invoice.Items)
        {
            WriteText(ms, $"{item.Quantity:0} x {item.NameEn}");
            Write(ms, EscPosCommands.NewLine);

            if (!string.IsNullOrEmpty(item.Notes))
            {
                ResetFontSize(ms);
                WriteText(ms, $"   >> {item.Notes}");
                Write(ms, EscPosCommands.NewLine);
                SetFontSize(ms, _config.FontSizeMedium);
            }
        }
        ResetFontSize(ms);
        Write(ms, EscPosCommands.BoldOff);

        WriteSeparator(ms);
        WriteText(ms, $"Cashier: {invoice.CashierName}");
        Write(ms, EscPosCommands.NewLine);

        ApplyBottomMargin(ms);
        Write(ms, EscPosCommands.FullCut);

        return ms.ToArray();
    }

    public byte[] BuildCashDrawerCommand() => EscPosCommands.OpenCashDrawer;

    #region Margin Helpers

    private void ApplyTopMargin(MemoryStream ms)
    {
        if (_config.MarginTop > 0)
            Write(ms, EscPosCommands.FeedLines((byte)_config.MarginTop));
    }

    private void ApplyBottomMargin(MemoryStream ms)
    {
        var total = (byte)(_config.MarginBottom + 3);
        Write(ms, EscPosCommands.FeedLines(total));
    }

    private string LeftMargin => new string(' ', _config.MarginLeft);

    #endregion

    #region Font Size Helpers

    private void SetFontSize(MemoryStream ms, int pointSize)
    {
    }

    private void ResetFontSize(MemoryStream ms)
    {
    }

    #endregion

    #region Build Sections

    private void BuildHeader(MemoryStream ms, InvoiceData invoice)
    {
        Write(ms, EscPosCommands.AlignCenter);

        SetFontSize(ms, _config.FontSizeLarge);
        Write(ms, EscPosCommands.BoldOn);
        if (!string.IsNullOrEmpty(invoice.CompanyNameEn))
        {
            WriteText(ms, invoice.CompanyNameEn);
            Write(ms, EscPosCommands.NewLine);
        }
        Write(ms, EscPosCommands.BoldOff);

        SetFontSize(ms, _config.FontSizeSmall);
        if (!string.IsNullOrEmpty(invoice.VatNumber))
        {
            WriteText(ms, $"VAT #: {invoice.VatNumber}");
            Write(ms, EscPosCommands.NewLine);
        }

        if (!string.IsNullOrEmpty(invoice.CrNumber))
        {
            WriteText(ms, $"CR #: {invoice.CrNumber}");
            Write(ms, EscPosCommands.NewLine);
        }

        if (!string.IsNullOrEmpty(invoice.Address))
        {
            WriteText(ms, invoice.Address);
            Write(ms, EscPosCommands.NewLine);
        }

        if (!string.IsNullOrEmpty(invoice.Phone))
        {
            WriteText(ms, $"Tel: {invoice.Phone}");
            Write(ms, EscPosCommands.NewLine);
        }
        ResetFontSize(ms);

        WriteSeparator(ms);
    }

    private void BuildInvoiceInfo(MemoryStream ms, InvoiceData invoice)
    {
        Write(ms, EscPosCommands.AlignCenter);
        Write(ms, EscPosCommands.BoldOn);
        SetFontSize(ms, _config.FontSizeMedium);
        WriteText(ms, "TAX INVOICE");
        Write(ms, EscPosCommands.NewLine);
        ResetFontSize(ms);
        Write(ms, EscPosCommands.BoldOff);
        WriteSeparator(ms);

        Write(ms, EscPosCommands.AlignLeft);
        SetFontSize(ms, _config.FontSizeSmall);

        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Invoice #:", invoice.InvoiceNumber, _width));
        Write(ms, EscPosCommands.NewLine);
        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Date:", invoice.InvoiceDate.ToString("yyyy-MM-dd HH:mm"), _width));
        Write(ms, EscPosCommands.NewLine);
        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Cashier:", invoice.CashierName, _width));
        Write(ms, EscPosCommands.NewLine);

        if (!string.IsNullOrEmpty(invoice.TableNumber))
        {
            WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Table:", invoice.TableNumber, _width));
            Write(ms, EscPosCommands.NewLine);
        }

        ResetFontSize(ms);
        WriteSeparator(ms);
    }

    private void BuildItems(MemoryStream ms, InvoiceData invoice)
    {
        Write(ms, EscPosCommands.AlignLeft);
        SetFontSize(ms, _config.FontSizeSmall);
        Write(ms, EscPosCommands.BoldOn);
        WriteText(ms, ArabicTextHelper.PadLine("Item", "Qty x Price = Total", _width));
        Write(ms, EscPosCommands.NewLine);
        Write(ms, EscPosCommands.BoldOff);
        WriteSeparator(ms);

        foreach (var item in invoice.Items)
        {
            var name = ArabicTextHelper.Truncate(item.NameEn, _width - 2);
            WriteText(ms, LeftMargin + name);
            Write(ms, EscPosCommands.NewLine);

            var line = $"{item.Quantity:0.##} x {item.UnitPrice:N2} = {item.Total:N2}";
            WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("", line, _width));
            Write(ms, EscPosCommands.NewLine);

            if (item.Discount > 0)
            {
                WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("  Discount:", $"-{item.Discount:N2}", _width));
                Write(ms, EscPosCommands.NewLine);
            }
        }
        ResetFontSize(ms);

        WriteSeparator(ms);
    }

    private void BuildTotals(MemoryStream ms, InvoiceData invoice)
    {
        SetFontSize(ms, _config.FontSizeMedium);

        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Subtotal:", $"{invoice.SubTotal:N2}", _width));
        Write(ms, EscPosCommands.NewLine);

        if (invoice.TotalDiscount > 0)
        {
            WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Discount:", $"-{invoice.TotalDiscount:N2}", _width));
            Write(ms, EscPosCommands.NewLine);
        }

        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("VAT 15%:", $"{invoice.TotalTax:N2}", _width));
        Write(ms, EscPosCommands.NewLine);
        ResetFontSize(ms);

        WriteSeparator(ms);

        SetFontSize(ms, _config.FontSizeLarge);
        Write(ms, EscPosCommands.BoldOn);
        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("TOTAL:", $"{invoice.GrandTotal:N2} SAR", _width / 2));
        Write(ms, EscPosCommands.NewLine);
        ResetFontSize(ms);
        Write(ms, EscPosCommands.BoldOff);

        WriteSeparator(ms);
    }

    private void BuildPaymentInfo(MemoryStream ms, InvoiceData invoice)
    {
        SetFontSize(ms, _config.FontSizeSmall);

        if (!string.IsNullOrEmpty(invoice.PaymentMethod))
        {
            WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Payment:", invoice.PaymentMethod, _width));
            Write(ms, EscPosCommands.NewLine);
        }

        WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Paid:", $"{invoice.PaidAmount:N2}", _width));
        Write(ms, EscPosCommands.NewLine);

        if (invoice.ChangeAmount > 0)
        {
            WriteText(ms, LeftMargin + ArabicTextHelper.PadLine("Change:", $"{invoice.ChangeAmount:N2}", _width));
            Write(ms, EscPosCommands.NewLine);
        }
        ResetFontSize(ms);

        WriteSeparator(ms);
    }

    private void BuildQrCode(MemoryStream ms, string qrData)
    {
        Write(ms, EscPosCommands.AlignCenter);

        WriteText(ms, $"[QR: {qrData}]");
        Write(ms, EscPosCommands.NewLine);

        WriteText(ms, "ZATCA QR Code");
        Write(ms, EscPosCommands.NewLine);
    }

    private byte[]? GenerateQrCodeBytes(string data, byte moduleSize)
    {
        try
        {
            var ms = new MemoryStream();
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var pL = (byte)((dataBytes.Length + 3) & 0xFF);
            var pH = (byte)(((dataBytes.Length + 3) >> 8) & 0xFF);

            // Model 2
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
            // Module Size
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, moduleSize });
            // Error correction M
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31 });
            // Store data
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30 });
            ms.Write(dataBytes);
            // Print
            ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private void BuildFooter(MemoryStream ms, InvoiceData invoice)
    {
        Write(ms, EscPosCommands.AlignCenter);
        SetFontSize(ms, _config.FontSizeSmall);

        if (!string.IsNullOrEmpty(invoice.FooterMessage))
        {
            WriteText(ms, invoice.FooterMessage);
            Write(ms, EscPosCommands.NewLine);
        }

        WriteText(ms, "Thank You");
        Write(ms, EscPosCommands.NewLine);
        ResetFontSize(ms);
    }

    #endregion

    #region Write Helpers

    private static void Write(MemoryStream ms, byte[] bytes) => ms.Write(bytes, 0, bytes.Length);

    private static void WriteText(MemoryStream ms, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static void WriteArabic(MemoryStream ms, string text)
    {
        var bytes = ArabicTextHelper.EncodeArabic(text);
        ms.Write(bytes, 0, bytes.Length);
    }

    private void WriteSeparator(MemoryStream ms)
    {
        WriteText(ms, new string('-', _width));
        Write(ms, EscPosCommands.NewLine);
    }

    #endregion
}
