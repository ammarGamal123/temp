# Troubleshooting Guide

## Service Won't Install

### Error: "Access is denied"
**Cause:** Not running as Administrator.

**Solution:** 
1. Right-click CMD → "Run as Administrator"
2. Then run the installer

### Error: "Service already exists"
**Cause:** Previous installation wasn't cleaned up.

**Solution:**
```bash
sc stop POSPrintAgent
sc delete POSPrintAgent
# Wait 5 seconds
POS.PrintAgent.Installer.exe install
```

### Error: "The service did not respond"
**Cause:** `appsettings.json` is missing or invalid.

**Solution:**
1. Check that `appsettings.json` exists in the service directory
2. Validate the JSON syntax
3. Check logs in `<service-dir>/logs/`

---

## Service Installed but Not Responding

### Check service is running
```bash
sc query POSPrintAgent
```
Should show `STATE: 4 RUNNING`

### Check if port is in use
```bash
netstat -ano | findstr :9100
```
If another process is using port 9100, either:
- Change the port in `appsettings.json`
- Stop the other process

### Check firewall
The agent listens on `127.0.0.1` only, so firewall shouldn't block it. But verify:
```bash
netsh advfirewall firewall show rule name=all | findstr 9100
```

### Check logs
```
<service-dir>\logs\agent-YYYY-MM-DD.log
```

Common log patterns:
- `Application started` → Service is up
- `Connection refused` → Service didn't bind to port
- `Access denied` → Permission issue

---

## Print Jobs Fail

### "Printer not found"
**Cause:** Printer name in request doesn't match Windows printer name exactly.

**Solution:**
1. Get exact names: `GET /printers`
2. Use the exact `name` value
3. Names are case-insensitive but must match otherwise

### Print job says "success" but nothing prints

**Possible causes:**

1. **Printer is offline**
   - Check Windows: Control Panel → Devices and Printers
   - Make sure printer is green/ready

2. **Print spooler service is stopped**
   - Services → Print Spooler → Start

3. **Printer uses different command set**
   - This agent uses ESC/POS (standard for most thermal printers)
   - If your printer uses a different language (e.g., ZPL for Zebra), the bytes won't be interpreted correctly

4. **USB disconnected or sleeping**
   - Unplug/replug the USB cable
   - Disable USB power saving: Device Manager → USB Controllers → Properties → Power Management

### Garbled Arabic text

**Cause:** Printer doesn't support Windows-1256 code page, or fonts are missing.

**Solutions:**
1. **Check printer supports Arabic** - Not all thermal printers do. Look for "Arabic support" in the printer specs.
2. **Update printer firmware** - Newer firmware often adds code page support.
3. **Change code page** - Edit `EscPosCommands.cs`:
   ```csharp
   // Try CP864 instead of Windows-1256
   public static readonly byte[] SetCodePageArabic = { ESC, (byte)'t', 22 };
   ```
4. **Use English only** - Fill `nameEn` fields and skip Arabic header.

### Receipt is too wide/narrow

**Cause:** `paperWidth` in config doesn't match actual paper.

**Solution:**
- 58mm thermal → `paperWidth: 58`
- 80mm thermal → `paperWidth: 80`

The agent calculates character width based on this (48 chars for 80mm, 32 chars for 58mm).

### QR code is cut off or too small

**Solutions:**
- Increase `qrCodeSize` (80-120 for 80mm paper, 40-60 for 58mm)
- Check printer has QR support (most modern thermal printers do)
- If QR support is missing, you'll need to render QR as an image (more complex)

---

## CORS Errors in Browser

### Error: "CORS policy: No 'Access-Control-Allow-Origin' header"

**Cause:** Your POS domain isn't in the `AllowedOrigins` list.

**Solution:** Edit `appsettings.json`:
```json
{
  "AgentSettings": {
    "AllowedOrigins": [
      "https://your-pos-domain.com",
      "http://localhost:3000"
    ]
  }
}
```

Then restart the service:
```bash
POS.PrintAgent.Installer.exe restart
```

### Error: "Mixed Content: ... requested insecure resource"

**Cause:** Your POS is on HTTPS but trying to call HTTP endpoints.

**Solution:**
- Use `http://127.0.0.1:9100` (not `localhost`) - modern browsers allow this
- Or set up HTTPS on the agent (advanced - requires a local certificate)

---

## 401 Unauthorized

**Cause:** API key is missing or incorrect.

**Solutions:**
1. Make sure you're sending `X-Api-Key` header (exact name, case-sensitive)
2. Value must match `ApiKey` in `appsettings.json` exactly
3. No extra spaces or quotes in the value
4. After changing the API key, restart the service

---

## Performance Issues

### Slow print response

**Causes:**
1. Printer driver is slow (especially USB-over-network)
2. Large QR codes take time to render
3. Disk I/O on logs

**Solutions:**
- Use dedicated printer (not shared network printer)
- Reduce `qrCodeSize` if too large
- Change log level to `Warning` in `appsettings.json`

### High memory usage

The agent should use ~50-100MB. If higher:
1. Check for memory leaks in logs
2. Restart the service daily (as a workaround)
3. Review custom code if you've modified the agent

---

## Debugging

### Enable debug logging

Edit `appsettings.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

Restart the service.

### Test manually with curl

```bash
# Health check
curl http://127.0.0.1:9100/health

# List printers
curl http://127.0.0.1:9100/printers -H "X-Api-Key: your-key"

# Test print to default printer
curl -X POST http://127.0.0.1:9100/test \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-key" \
  -d "{\"printerName\":\"Microsoft Print to PDF\"}"
```

### Run in console mode for debugging

Stop the service and run the exe directly:
```bash
sc stop POSPrintAgent
cd C:\POSPrintAgent
POS.PrintAgent.Service.exe
```

You'll see all logs in the console in real-time.

---

## Common Questions

### Q: Can the agent run on Linux or Mac?
**A:** Currently Windows-only because it uses the Windows Spooler API. Linux support would require using CUPS instead.

### Q: Can I print images/logos?
**A:** Not in the current version. Logo base64 is accepted but not yet implemented. Can be added by extending `ThermalReceiptBuilder`.

### Q: How many print jobs per second can it handle?
**A:** The bottleneck is the printer, not the agent. The agent can queue many requests; each printer can typically handle 1-2 receipts per second.

### Q: Does it support Bluetooth printers?
**A:** If the Bluetooth printer appears as a Windows printer (with a name visible in Devices and Printers), yes. Otherwise no.

### Q: Can I customize the receipt layout?
**A:** Yes, edit `ThermalReceiptBuilder.cs`. You can rearrange sections, add/remove fields, change formatting.

---

## Getting Help

1. Check logs: `<service-dir>\logs\agent-YYYY-MM-DD.log`
2. Run in console mode to see real-time output
3. Test endpoints individually with curl/Postman
4. Check Windows Event Viewer → Windows Logs → Application for service crash info
