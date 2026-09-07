using System.Text;

namespace POS.PrintAgent.Service.Utilities;

/// <summary>
/// Helper لمعالجة النصوص العربية للطباعة الحرارية
/// </summary>
public static class ArabicTextHelper
{
    static ArabicTextHelper()
    {
        // تسجيل encoding provider (مطلوب في .NET Core/5+)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// تحويل النص العربي لـ bytes باستخدام Windows-1256
    /// </summary>
    public static byte[] EncodeArabic(string text)
    {
        var encoding = Encoding.GetEncoding("windows-1256");
        return encoding.GetBytes(text);
    }

    /// <summary>
    /// تحديد إذا كان النص يحتوي على حروف عربية
    /// </summary>
    public static bool ContainsArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Any(c => c >= 0x0600 && c <= 0x06FF);
    }

    /// <summary>
    /// Padding للمحاذاة في الطابعات الحرارية
    /// </summary>
    public static string PadLine(string left, string right, int width)
    {
        var space = width - left.Length - right.Length;
        if (space < 1) space = 1;
        return left + new string(' ', space) + right;
    }

    /// <summary>
    /// كتابة رقم بصيغة currency
    /// </summary>
    public static string FormatCurrency(decimal amount, string currency = "SAR")
    {
        return $"{amount:N2} {currency}";
    }

    /// <summary>
    /// تقصير النص لو أطول من العرض المسموح
    /// </summary>
    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength - 1) + "…";
    }
}
