using Microsoft.Extensions.Logging;
using POS.PrintAgent.Core.Enums;
using POS.PrintAgent.Core.Interfaces;
using POS.PrintAgent.Core.Models;
using System.Drawing.Printing;
using System.Runtime.Versioning;

namespace POS.PrintAgent.Service.Services;

[SupportedOSPlatform("windows")]
public class WindowsPrinterService : IPrinterService
{
    private readonly ILogger<WindowsPrinterService> _logger;
    private readonly IReceiptBuilder _builder;
    private readonly IHtmlReceiptService _htmlReceiptService;
    private readonly string _rendererMode;

    public WindowsPrinterService(
        ILogger<WindowsPrinterService> logger,
        IReceiptBuilder builder,
        IHtmlReceiptService htmlReceiptService,
        AgentSettings settings)
    {
        _logger = logger;
        _builder = builder;
        _htmlReceiptService = htmlReceiptService;
        _rendererMode = settings.RendererMode?.ToLowerInvariant() ?? "escpos";
    }

    public async Task<PrintResponse> PrintAsync(PrintRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Print request received. Printer: {Printer}, JobType: {JobType}, Invoice: {Invoice}",
                request.PrinterName, request.JobType, request.Invoice.InvoiceNumber);

            if (!PrinterExists(request.PrinterName))
            {
                _logger.LogWarning("Printer '{Printer}' not found", request.PrinterName);
                return new PrintResponse
                {
                    Success = false,
                    Message = $"Printer '{request.PrinterName}' not found"
                };
            }

            // طبّق الـ config على الـ builder
            _builder.Configure(request.PrinterConfig);

            byte[] bytes;

            if (_rendererMode == "html")
            {
                try
                {
                    bytes = await _htmlReceiptService.RenderReceiptAsync(
                        request.Invoice, request.PrinterConfig, request.JobType, ct);
                    if (bytes == null || bytes.Length == 0)
                        throw new Exception("HTML renderer returned empty data");

                    var cutCommand = new byte[] { 0x1D, 0x56, 0x00 };
                    var bytesWithCut = new byte[bytes.Length + cutCommand.Length];
                    Buffer.BlockCopy(bytes, 0, bytesWithCut, 0, bytes.Length);
                    Buffer.BlockCopy(cutCommand, 0, bytesWithCut, bytes.Length, cutCommand.Length);
                    bytes = bytesWithCut;

                    _logger.LogInformation("Printed via HTML renderer ({RasterBytes} raster bytes + cut)", bytes.Length - 3);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HTML rendering failed, using ESC/POS fallback");
                    bytes = request.JobType switch
                    {
                        PrintJobType.Receipt => _builder.BuildReceipt(request.Invoice),
                        PrintJobType.KitchenOrder or PrintJobType.BarOrder => _builder.BuildKitchenOrder(request.Invoice),
                        _ => _builder.BuildReceipt(request.Invoice)
                    };
                    _logger.LogInformation("Printed via ESC/POS fallback");
                }
            }
            else
            {
                bytes = request.JobType switch
                {
                    PrintJobType.Receipt => _builder.BuildReceipt(request.Invoice),
                    PrintJobType.KitchenOrder or PrintJobType.BarOrder => _builder.BuildKitchenOrder(request.Invoice),
                    _ => _builder.BuildReceipt(request.Invoice)
                };
                _logger.LogInformation("Printed via ESC/POS renderer ({Mode})", _rendererMode);
            }

            for (int i = 0; i < request.Copies; i++)
            {
                var success = await Task.Run(
                    () => RawPrinterHelper.SendBytesToPrinter(request.PrinterName, bytes),
                    ct);

                if (!success)
                {
                    _logger.LogError("Failed to send data to printer '{Printer}'", request.PrinterName);
                    return new PrintResponse
                    {
                        Success = false,
                        Message = $"Failed to send data to printer '{request.PrinterName}'"
                    };
                }
            }

            if (request.OpenCashDrawer)
            {
                var drawerBytes = _builder.BuildCashDrawerCommand();
                RawPrinterHelper.SendBytesToPrinter(request.PrinterName, drawerBytes);
            }

            var jobId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("Print job {JobId} completed successfully", jobId);

            return new PrintResponse
            {
                Success = true,
                Message = "Print job sent successfully",
                JobId = jobId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing invoice {Invoice}", request.Invoice.InvoiceNumber);
            return new PrintResponse
            {
                Success = false,
                Message = $"Print error: {ex.Message}"
            };
        }
    }

    public List<PrinterInfo> GetAvailablePrinters()
    {
        var printers = new List<PrinterInfo>();

        try
        {
            var defaultPrinter = new PrinterSettings().PrinterName;

            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                var settings = new PrinterSettings { PrinterName = printer };
                printers.Add(new PrinterInfo
                {
                    Name = printer,
                    IsDefault = printer.Equals(defaultPrinter, StringComparison.OrdinalIgnoreCase),
                    IsOnline = settings.IsValid,
                    Status = settings.IsValid ? "Online" : "Offline"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving printer list");
        }

        return printers;
    }

    public bool IsPrinterOnline(string printerName)
    {
        try
        {
            var settings = new PrinterSettings { PrinterName = printerName };
            return settings.IsValid;
        }
        catch
        {
            return false;
        }
    }

    private static bool PrinterExists(string name)
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            if (printer.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
