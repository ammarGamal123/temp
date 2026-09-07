# Setup Guide

Complete guide for setting up POS Print Agent from scratch.

## Prerequisites

### Development Machine
- Visual Studio 2022 (Community/Professional/Enterprise)
- .NET 8.0 SDK
- Git (optional)

### Target Machine (where the service will run)
- Windows 10/11 or Windows Server 2016+
- Administrator privileges
- Thermal printer(s) installed with drivers

## Step 1: Open the Solution in Visual Studio

1. Open Visual Studio 2022
2. File → Open → Project/Solution
3. Navigate to `POS.PrintAgent.sln` and open it
4. You should see three projects:
   - `POS.PrintAgent.Core`
   - `POS.PrintAgent.Service`
   - `POS.PrintAgent.Installer`

## Step 2: Restore NuGet Packages

Either:
- Right-click on the solution → Restore NuGet Packages
- Or open Package Manager Console and run: `dotnet restore`

## Step 3: Configure the Service

Open `POS.PrintAgent.Service/appsettings.json` and update:

```json
{
  "AgentSettings": {
    "Port": 5050,
    "ApiKey": "generate-a-strong-random-key-here",
    "AllowedOrigins": [
      "https://your-actual-pos-domain.com"
    ]
  }
}
```

**Important:**
- Change `ApiKey` to a strong random value (32+ characters)
- Add only the domains that should be allowed to access the agent
- For local dev, you can add `http://localhost:3000` etc.

## Step 4: Test in Development

### Run as a regular console app (for testing)

1. Set `POS.PrintAgent.Service` as the startup project (right-click → Set as Startup Project)
2. Press F5 to run
3. Open browser: `http://127.0.0.1:9100/health`
4. You should see a healthy response

### Test printing

Use any HTTP client (Postman, curl, or a browser extension) to POST to `/test`:

```bash
curl -X POST http://127.0.0.1:5050/test \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d "{\"printerName\":\"Microsoft Print to PDF\"}"
```

## Step 5: Build for Production

### Option A: Using Visual Studio
1. Right-click `POS.PrintAgent.Service` → Publish
2. Choose Folder target
3. Configuration: Release
4. Target framework: net8.0
5. Deployment mode: Self-contained
6. Target runtime: win-x64
7. Click Publish

### Option B: Using Command Line
Open terminal in solution root and run:
```bash
build.bat
```

Or manually:
```bash
dotnet publish POS.PrintAgent.Service -c Release -r win-x64 --self-contained true -o ./publish
dotnet publish POS.PrintAgent.Installer -c Release -r win-x64 --self-contained true -o ./publish
```

## Step 6: Deploy to Target Machine

1. Copy the entire `publish` folder to the target machine
2. Recommended location: `C:\POSPrintAgent\`
3. Edit `appsettings.json` on the target machine if needed

## Step 7: Install as Windows Service

1. Open CMD as **Administrator**
2. Navigate to the folder: `cd C:\POSPrintAgent`
3. Run:
   ```
   POS.PrintAgent.Installer.exe install
   ```

Or use the batch script: `install.bat`

### Verify Installation

```bash
# Check service status
POS.PrintAgent.Installer.exe status

# Or via Windows Services
services.msc
```

Look for "POS Print Agent Service" - should be "Running"

### Test the endpoint

Open browser: `http://127.0.0.1:5050/health`

## Step 8: Configure Printers

The agent doesn't need pre-configured printers in its settings. Printer configuration is sent per-request from your POS application.

However, printers **must be installed in Windows** (with correct drivers) for the agent to see them.

To see available printers:
```bash
curl http://127.0.0.1:9100/printers -H "X-Api-Key: your-api-key"
```

## Step 9: Firewall Configuration

Since the agent listens only on `127.0.0.1` (localhost), Windows Firewall **should not** prompt or block it. No firewall rules needed.

If you need to access the agent from another machine (not recommended for security), you'd need to:
1. Change the URLs in `Program.cs` to include `http://0.0.0.0:5050`
2. Add a Windows Firewall rule for port 9100

**This is NOT recommended.** Instead, install the agent on each machine that has printers.

## Multiple Printers Setup (Restaurant Scenario)

Typical restaurant setup:
- **Cashier printer**: EPSON TM-T20 (80mm thermal)
- **Kitchen printer**: EPSON TM-T20 (80mm thermal, in kitchen)
- **Bar printer**: XPrinter XP-80 (80mm thermal, in bar area)

All these printers should be installed on the same machine (the cashier's PC), typically via:
- USB connection
- Network/IP connection (more common for remote kitchen/bar printers)

The agent will detect all of them and your POS can route print jobs appropriately.

## Updating the Service

1. Stop the service: `POS.PrintAgent.Installer.exe stop`
2. Replace the exe and DLL files
3. Start the service: `POS.PrintAgent.Installer.exe start`

## Uninstalling

```bash
# As Administrator
uninstall.bat
```

Or:
```bash
POS.PrintAgent.Installer.exe uninstall
```

## Auto-Start on Boot

The service is installed with `start=auto`, so it will automatically start on Windows boot. No additional configuration needed.
