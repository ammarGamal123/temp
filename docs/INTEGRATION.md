# Frontend Integration Guide

## Overview

This guide shows how to integrate the Print Agent with your web-based POS frontend (React, Angular, Vue, or vanilla JS).

## Architecture

```
┌─────────────────┐       HTTPS        ┌─────────────────┐
│   POS Frontend  │ ◄────────────────► │   POS Backend   │
│   (Browser)     │                    │   (.NET API)    │
└────────┬────────┘                    └─────────────────┘
         │
         │ HTTP (localhost)
         ▼
┌─────────────────┐
│  Print Agent    │     USB/Network
│  127.0.0.1:9100 │ ◄────────────────► Printers
└─────────────────┘
```

## HTTPS Mixed Content Issue

If your POS is served over HTTPS (recommended), you'll face a "Mixed Content" issue when calling HTTP `localhost`. Modern browsers solve this by treating `http://127.0.0.1` and `http://localhost` as "secure contexts", so they're allowed from HTTPS pages.

**Use `127.0.0.1` instead of `localhost`** for consistency across browsers.

## JavaScript/TypeScript Service

Create a dedicated service file `printService.js`:

```javascript
const AGENT_URL = 'http://127.0.0.1:9100';
const API_KEY = 'your-api-key-from-env';

async function request(path, options = {}) {
  const response = await fetch(`${AGENT_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': API_KEY,
      ...(options.headers || {})
    }
  });
  
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || error.detail || `HTTP ${response.status}`);
  }
  
  return response.json();
}

export async function checkAgentHealth() {
  try {
    await request('/health');
    return true;
  } catch {
    return false;
  }
}

export async function getPrinters() {
  return request('/printers');
}

export async function printReceipt(printRequest) {
  return request('/print', {
    method: 'POST',
    body: JSON.stringify(printRequest)
  });
}

export async function testPrint(printerName, printerConfig) {
  return request('/test', {
    method: 'POST',
    body: JSON.stringify({ printerName, printerConfig })
  });
}

export async function openCashDrawer(printerName) {
  return request('/cash-drawer/open', {
    method: 'POST',
    body: JSON.stringify({ printerName })
  });
}
```

## TypeScript Types

```typescript
export interface PrinterConfiguration {
  name: string;
  printerType: 'thermal' | 'a4';
  paperWidth: number;
  paperHeight: number;
  dpi: number;
  marginTop: number;
  marginBottom: number;
  marginLeft: number;
  marginRight: number;
  fontSizeSmall: number;
  fontSizeMedium: number;
  fontSizeLarge: number;
  qrCodeSize: number;
  barcodeSize: number;
  categoryId: number;
  isDefault: boolean;
  isActive: boolean;
}

export interface InvoiceItem {
  nameAr: string;
  nameEn: string;
  quantity: number;
  unitPrice: number;
  discount: number;
  taxRate: number;
  total: number;
  notes?: string;
  categoryName?: string;
}

export interface InvoiceData {
  companyNameAr: string;
  companyNameEn: string;
  vatNumber: string;
  crNumber: string;
  address: string;
  phone: string;
  invoiceNumber: string;
  invoiceDate: string;
  cashierName: string;
  tableNumber?: string;
  orderType?: string;
  items: InvoiceItem[];
  subTotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
  paidAmount: number;
  changeAmount: number;
  paymentMethod: string;
  qrCodeBase64?: string;
  footerMessage?: string;
  customerName?: string;
  customerPhone?: string;
}

export enum PrintJobType {
  Receipt = 0,
  KitchenOrder = 1,
  BarOrder = 2,
  Invoice = 3,
  Report = 4
}

export interface PrintRequest {
  printerName: string;
  jobType: PrintJobType;
  printerConfig: PrinterConfiguration;
  invoice: InvoiceData;
  copies: number;
  openCashDrawer: boolean;
}
```

## Complete Example - Restaurant Order Flow

```javascript
async function completeOrder(order, printers) {
  // 1. Save invoice via your .NET API (get invoice + ZATCA QR)
  const savedInvoice = await fetch('/api/invoices', {
    method: 'POST',
    body: JSON.stringify(order)
  }).then(r => r.json());

  // 2. Get printer configs from your DB
  const cashierPrinter = printers.find(p => p.categoryId === 0 && p.isDefault);
  const kitchenPrinter = printers.find(p => p.categoryId === 1 && p.isActive);

  // 3. Print receipt for customer
  await printReceipt({
    printerName: cashierPrinter.name,
    jobType: 0, // Receipt
    printerConfig: cashierPrinter,
    invoice: {
      ...savedInvoice,
      qrCodeBase64: savedInvoice.zatcaQr
    },
    copies: 1,
    openCashDrawer: order.paymentMethod === 'Cash'
  });

  // 4. Send kitchen order (only food items)
  const kitchenItems = savedInvoice.items.filter(
    i => i.categoryName !== 'Drinks'
  );
  
  if (kitchenItems.length > 0 && kitchenPrinter) {
    await printReceipt({
      printerName: kitchenPrinter.name,
      jobType: 1, // KitchenOrder
      printerConfig: kitchenPrinter,
      invoice: {
        ...savedInvoice,
        items: kitchenItems
      },
      copies: 1,
      openCashDrawer: false
    });
  }
}
```

## React Hook Example

```jsx
import { useState, useEffect } from 'react';
import { checkAgentHealth, getPrinters, printReceipt } from './printService';

export function usePrintAgent() {
  const [isConnected, setIsConnected] = useState(false);
  const [printers, setPrinters] = useState([]);

  useEffect(() => {
    async function init() {
      const healthy = await checkAgentHealth();
      setIsConnected(healthy);
      if (healthy) {
        const list = await getPrinters();
        setPrinters(list);
      }
    }
    init();
  }, []);

  const print = async (request) => {
    if (!isConnected) throw new Error('Print agent not connected');
    return printReceipt(request);
  };

  return { isConnected, printers, print };
}
```

Usage in component:
```jsx
function CheckoutButton({ invoice, printerConfig }) {
  const { isConnected, print } = usePrintAgent();

  const handlePrint = async () => {
    try {
      await print({
        printerName: printerConfig.name,
        jobType: 0,
        printerConfig,
        invoice,
        copies: 1,
        openCashDrawer: true
      });
      alert('Printed!');
    } catch (err) {
      alert('Print failed: ' + err.message);
    }
  };

  return (
    <button 
      onClick={handlePrint}
      disabled={!isConnected}
    >
      {isConnected ? 'Print Receipt' : 'Agent Offline'}
    </button>
  );
}
```

## Routing to Multiple Printers

In your POS backend, store `DefaultPrinterId` on each `ProductCategory`:

```csharp
public class ProductCategory
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? DefaultPrinterId { get; set; } // FK to Printers table
}

public class Printer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; } // 0=Cashier, 1=Kitchen, 2=Bar
    public int PaperWidth { get; set; }
    // ... other config fields matching PrinterConfiguration
}
```

Then in your frontend, group items by their category's printer and send multiple print requests:

```javascript
async function printAllCopies(invoice, allPrinters) {
  // Group items by printer
  const groups = new Map();
  
  for (const item of invoice.items) {
    const printerId = item.categoryPrinterId || defaultCashierId;
    if (!groups.has(printerId)) groups.set(printerId, []);
    groups.get(printerId).push(item);
  }

  // Print to each printer
  for (const [printerId, items] of groups) {
    const printer = allPrinters.find(p => p.id === printerId);
    if (!printer) continue;

    await printReceipt({
      printerName: printer.name,
      jobType: printer.categoryId === 0 ? 0 : 1, // Receipt or Kitchen
      printerConfig: printer,
      invoice: { ...invoice, items },
      copies: 1,
      openCashDrawer: printer.categoryId === 0
    });
  }
}
```

## Error Handling Best Practices

```javascript
async function safePrint(request) {
  try {
    // Check agent first
    const healthy = await checkAgentHealth();
    if (!healthy) {
      throw new Error('Print agent is not running. Please start the service.');
    }

    // Attempt print
    return await printReceipt(request);
  } catch (error) {
    // Log to your backend for diagnostics
    console.error('Print failed:', error);
    
    // User-friendly messages
    if (error.message.includes('not found')) {
      throw new Error(`Printer "${request.printerName}" is not installed on this machine.`);
    }
    if (error.message.includes('Failed to send')) {
      throw new Error('Printer is offline or disconnected. Please check the printer.');
    }
    throw error;
  }
}
```

## Security Best Practices

1. **Never hardcode the API key** - load it from environment or config
2. **Restrict CORS origins** - only allow your POS domain
3. **Use HTTPS in production** - for your main POS app
4. **Keep the agent updated** - monitor for security patches
5. **Use a strong API key** - 32+ random characters
