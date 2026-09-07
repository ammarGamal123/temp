namespace POS.PrintAgent.Core.Enums;

public enum PrintJobType
{
    Receipt = 0,         // إيصال الكاشير
    KitchenOrder = 1,    // أمر المطبخ
    BarOrder = 2,        // أمر البار
    Invoice = 3,         // فاتورة A4
    Report = 4           // تقرير
}
