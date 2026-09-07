# POS Print Agent

Local printing agent for POS (Point of Sale) systems. A free, self-hosted alternative to QZ Tray built with .NET 8.

## Overview

This agent runs as a Windows Service on the cashier/kitchen machine and exposes an HTTP API on `http://127.0.0.1:9100` that your web-based POS system can call to print receipts, kitchen orders, and invoices directly to local thermal printers.

## Features

- **Free & Open Source** - No licensing fees
- **Windows Service** - Runs in background, auto-starts with Windows
- **ESC/POS Support** - Native support for thermal receipt printers
- **Arabic Support** - Windows-1256 encoding for Arabic text
- **ZATCA QR Code** - Native QR code printing for Saudi e-invoicing
- **Multi-Printer** - Route different job types to different printers (cashier/kitchen/bar)
- **API Key Authentication** - Secure your agent endpoints
- **Dynamic Configuration** - Per-request printer settings from your DB
- **Cash Drawer** - Open cash drawer via API

## Project Structure

```
POS.PrintAgent/
├── POS.PrintAgent.Core/           Models, Interfaces, Enums
├── POS.PrintAgent.Service/        Worker Service (main executable)
├── POS.PrintAgent.Installer/      CLI tool for install/uninstall
├── docs/                          Documentation
├── build.bat                      Build & publish script
├── install.bat                    Installation script (run as admin)
└── uninstall.bat                  Uninstallation script (run as admin)
```

## Quick Start

### Prerequisites

- Windows 10/11 or Windows Server 2016+
- .NET 8.0 SDK (for building from source)
- Administrator privileges (for service installation)

### Build from Source

```bash
# Clone or download the repo
cd POS.PrintAgent

# Build and publish
build.bat
```

### Install the Service

1. Copy the `publish` folder to your target machine (e.g., `C:\POSPrintAgent\`)
2. Edit `appsettings.json` - change the `ApiKey` and add your POS domain to `AllowedOrigins`
3. Open CMD as Administrator in that folder
4. Run: `install.bat`

The service will start automatically and run in the background.

### Verify Installation

Open a browser and visit:
```
http://127.0.0.1:9100/health
```

You should see: `{"status":"healthy","timestamp":"...","version":"1.0.0"}`

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check (no auth) |
| GET | `/printers` | List installed printers |
| GET | `/printers/{name}/status` | Check specific printer status |
| POST | `/print` | Print invoice/receipt |
| POST | `/test` | Print a test receipt |
| POST | `/cash-drawer/open` | Open cash drawer |

See [docs/API.md](docs/API.md) for full API reference.

## Usage from Frontend

```javascript
const AGENT_URL = 'http://127.0.0.1:9100';
const API_KEY = 'your-api-key';

// Get available printers
const printers = await fetch(`${AGENT_URL}/printers`, {
  headers: { 'X-Api-Key': API_KEY }
}).then(r => r.json());

// Print receipt
await fetch(`${AGENT_URL}/print`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Api-Key': API_KEY
  },
  body: JSON.stringify({
    printerName: 'EPSON TM-T20',
    jobType: 0,
    printerConfig: { /* from DB */ },
    invoice: { /* invoice data */ },
    copies: 1,
    openCashDrawer: true
  })
});
```

## Documentation

- [Setup Guide](docs/SETUP.md) - Detailed installation and configuration
- [API Reference](docs/API.md) - Complete API documentation
- [Integration Guide](docs/INTEGRATION.md) - Frontend integration examples
- [Troubleshooting](docs/TROUBLESHOOTING.md) - Common issues and solutions

## Configuration

Edit `appsettings.json`:

```json
{
  "AgentSettings": {
    "Port": 9100,
    "ApiKey": "CHANGE-THIS-TO-A-SECURE-KEY",
    "AllowedOrigins": [
      "https://your-pos-domain.com",
      "http://localhost:3000"
    ]
  }
}
```

## Service Management

```bash
# Check status
POS.PrintAgent.Installer.exe status

# Start/Stop/Restart
POS.PrintAgent.Installer.exe start
POS.PrintAgent.Installer.exe stop
POS.PrintAgent.Installer.exe restart
```

Or use Windows Services manager: `services.msc` → Find "POS Print Agent Service"

## Logs

Logs are stored in the service directory:
```
<service-dir>/logs/agent-YYYY-MM-DD.log
```

Logs are rotated daily and kept for 14 days.

## License

MIT

## Support

For issues and questions, check the [Troubleshooting Guide](docs/TROUBLESHOOTING.md).
