# البدء السريع - Quick Start

دليل سريع بالعربي لتشغيل الـ Print Agent.

## الخطوات

### 1. تحميل .NET 8 SDK

حمّل من: https://dotnet.microsoft.com/download/dotnet/8.0

اختار **SDK** (مش Runtime).

### 2. فتح المشروع في Visual Studio

- افتح Visual Studio 2022
- File → Open → Project/Solution
- اختار `POS.PrintAgent.sln`

### 3. Restore Packages

- Right-click على الـ Solution
- Restore NuGet Packages
- استنى لحد ما يخلص

### 4. اختبار المشروع (Development Mode)

- اختار `POS.PrintAgent.Service` كـ Startup Project
- اضغط F5
- افتح المتصفح على: `http://127.0.0.1:9100/health`
- المفروض تشوف:
```json
{"status":"healthy","timestamp":"...","version":"1.0.0"}
```

### 5. اختبار الطباعة (بدون طابعة حقيقية)

استخدم Postman أو أي HTTP client:

**Request:**
```
POST http://127.0.0.1:9100/test
Content-Type: application/json
```

**Body:**
```json
{
  "printerName": "Microsoft Print to PDF"
}
```

الـ "Microsoft Print to PDF" موجودة افتراضياً في Windows، فهتطلع PDF تقدر تشوفه.

### 6. البناء للإنتاج

اقفل Visual Studio. افتح CMD في مجلد الـ Solution:

```bash
build.bat
```

أو يدوياً:
```bash
dotnet publish POS.PrintAgent.Service -c Release -r win-x64 --self-contained true -o ./publish
dotnet publish POS.PrintAgent.Installer -c Release -r win-x64 --self-contained true -o ./publish
```

### 7. النشر على جهاز العميل

1. انسخ مجلد `publish` بالكامل للجهاز المستهدف (مثلاً `C:\POSPrintAgent\`)
2. عدّل `appsettings.json`:
   - غيّر `ApiKey` لـ key قوي
   - ضيف domain الـ POS بتاعك في `AllowedOrigins`
3. افتح CMD **كـ Administrator**
4. `cd C:\POSPrintAgent`
5. شغّل: `install.bat`

### 8. التحقق من التشغيل

```bash
POS.PrintAgent.Installer.exe status
```

المفروض تلاقي `STATE: 4 RUNNING`

### 9. الاستخدام من الـ Frontend

```javascript
// مثال بسيط
async function printInvoice(invoice, printer) {
  const response = await fetch('http://127.0.0.1:9100/print', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': 'your-api-key'
    },
    body: JSON.stringify({
      printerName: printer.name,
      jobType: 0,
      printerConfig: printer,
      invoice: invoice,
      copies: 1,
      openCashDrawer: true
    })
  });
  
  return await response.json();
}
```

## أوامر مفيدة

```bash
# شغّل الخدمة
POS.PrintAgent.Installer.exe start

# أوقف الخدمة
POS.PrintAgent.Installer.exe stop

# إعادة تشغيل
POS.PrintAgent.Installer.exe restart

# حالة الخدمة
POS.PrintAgent.Installer.exe status

# إلغاء التثبيت
POS.PrintAgent.Installer.exe uninstall
```

## اللوجات

مكان اللوجات:
```
<مجلد الخدمة>\logs\agent-YYYY-MM-DD.log
```

بيتحفظ لمدة 14 يوم تلقائياً.

## أسئلة شائعة

**س: الخدمة مش شغالة، إيه الحل؟**

ج: شوف اللوج في `logs\agent-*.log`، وتأكد إن الـ port 9100 مش مشغول.

**س: الطباعة شغالة بس النص العربي غلط؟**

ج: طابعتك لازم تدعم Windows-1256 encoding. راجع `docs/TROUBLESHOOTING.md`.

**س: في CORS error في المتصفح؟**

ج: ضيف domain الـ POS بتاعك في `AllowedOrigins` في `appsettings.json`.

---

لمزيد من التفاصيل:
- **دليل التثبيت الكامل:** `docs/SETUP.md`
- **API Reference:** `docs/API.md`
- **دليل التكامل:** `docs/INTEGRATION.md`
- **حل المشاكل:** `docs/TROUBLESHOOTING.md`
