# API Reference

All endpoints (except `/health`) require an `X-Api-Key` header matching the value in `appsettings.json`.

Base URL: `http://127.0.0.1:9100`

## Authentication

Include the API key in every request:
```
X-Api-Key: your-api-key-here
```

## Endpoints

### 1. Health Check

**GET** `/health`

No authentication required.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-04-19T10:30:00Z",
  "version": "1.0.0"
}
```

---

### 2. List Printers

**GET** `/printers`

Returns all installed printers on the machine.

**Response:**
```json
[
  {
    "name": "EPSON TM-T20",
    "isDefault": true,
    "status": "Online",
    "isOnline": true
  },
  {
    "name": "Microsoft Print to PDF",
    "isDefault": false,
    "status": "Online",
    "isOnline": true
  }
]
```

---

### 3. Check Printer Status

**GET** `/printers/{name}/status`

**Response:**
```json
{
  "name": "EPSON TM-T20",
  "isOnline": true
}
```

---

### 4. Print Receipt/Invoice

**POST** `/print`

**Request Body:**
```json
{
  "printerName": "EPSON TM-T20",
  "jobType": 0,
  "printerConfig": {
    "name": "EPSON TM-T20",
    "printerType": "thermal",
    "paperWidth": 80,
    "paperHeight": 200,
    "dpi": 203,
    "marginTop": 2,
    "marginBottom": 2,
    "marginLeft": 1,
    "marginRight": 1,
    "fontSizeSmall": 8,
    "fontSizeMedium": 10,
    "fontSizeLarge": 14,
    "qrCodeSize": 80,
    "barcodeSize": 60,
    "categoryId": 0,
    "isDefault": true,
    "isActive": true
  },
  "invoice": {
    "companyNameAr": "شركة عريب",
    "companyNameEn": "Araib Group",
    "vatNumber": "300000000000003",
    "crNumber": "1010000000",
    "address": "Riyadh, Saudi Arabia",
    "phone": "+966 50 000 0000",
    "invoiceNumber": "INV-2025-0001",
    "invoiceDate": "2025-04-19T10:30:00",
    "cashierName": "Ahmed",
    "tableNumber": "5",
    "orderType": "Dine-in",
    "items": [
      {
        "nameAr": "برجر",
        "nameEn": "Burger",
        "quantity": 2,
        "unitPrice": 25.00,
        "discount": 0,
        "taxRate": 15,
        "total": 50.00,
        "notes": "No onions"
      },
      {
        "nameAr": "بيبسي",
        "nameEn": "Pepsi",
        "quantity": 2,
        "unitPrice": 5.00,
        "discount": 0,
        "taxRate": 15,
        "total": 10.00
      }
    ],
    "subTotal": 60.00,
    "totalDiscount": 0,
    "totalTax": 9.00,
    "grandTotal": 69.00,
    "paidAmount": 70.00,
    "changeAmount": 1.00,
    "paymentMethod": "Cash",
    "qrCodeBase64": "AQhBcmFpYiBDby...",
    "footerMessage": "Visit us again!"
  },
  "copies": 1,
  "openCashDrawer": true
}
```

**Job Types:**
| Value | Type |
|-------|------|
| 0 | Receipt (default cashier receipt) |
| 1 | KitchenOrder |
| 2 | BarOrder |
| 3 | Invoice (A4) |
| 4 | Report |

**Response (Success):**
```json
{
  "success": true,
  "message": "Print job sent successfully",
  "jobId": "a1b2c3d4",
  "timestamp": "2025-04-19T10:30:15Z"
}
```

**Response (Error):**
```json
{
  "type": "...",
  "title": "An error occurred...",
  "status": 500,
  "detail": "Printer 'Unknown Printer' not found"
}
```

---

### 5. Test Print

**POST** `/test`

Sends a simple test receipt to verify printer is working.

**Request Body:**
```json
{
  "printerName": "EPSON TM-T20",
  "printerConfig": {
    "paperWidth": 80,
    "marginTop": 2,
    "marginBottom": 2,
    "marginLeft": 1,
    "marginRight": 1,
    "fontSizeSmall": 8,
    "fontSizeMedium": 10,
    "fontSizeLarge": 14,
    "qrCodeSize": 80
  }
}
```

The `printerConfig` is optional - defaults will be used if omitted.

**Response:**
Same as `/print` endpoint.

---

### 6. Open Cash Drawer

**POST** `/cash-drawer/open`

Opens the cash drawer connected to the specified printer.

**Request Body:**
```json
{
  "printerName": "EPSON TM-T20"
}
```

**Response:**
```json
{
  "success": true
}
```

---

## PrinterConfiguration Fields

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Printer name (must match Windows printer name) |
| `printerType` | string | `thermal` or `a4` |
| `paperWidth` | int | Paper width in mm (58 or 80 for thermal) |
| `paperHeight` | int | Paper height in mm (ignored for thermal - continuous) |
| `dpi` | int | 203 for thermal, 300 for A4 |
| `marginTop` | int | Top margin in line feeds |
| `marginBottom` | int | Bottom margin in line feeds |
| `marginLeft` | int | Left margin in spaces |
| `marginRight` | int | Right margin in spaces |
| `fontSizeSmall` | int | Small font size in points (8-10) |
| `fontSizeMedium` | int | Medium font size (10-12) |
| `fontSizeLarge` | int | Large font size (14+) |
| `qrCodeSize` | int | QR code size in pixels (40-160) |
| `barcodeSize` | int | Barcode height in pixels |
| `categoryId` | int | 0=Cashier, 1=Kitchen, 2=Bar (for routing) |
| `isDefault` | bool | Is this the default printer |
| `isActive` | bool | Is this printer active |

## Error Codes

| HTTP Status | Meaning |
|-------------|---------|
| 200 | Success |
| 400 | Bad Request (missing required fields) |
| 401 | Unauthorized (invalid/missing API key) |
| 500 | Internal Server Error (printer error, etc.) |
