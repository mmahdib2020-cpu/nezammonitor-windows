using System.Globalization;
using System.Text;

namespace NezamMonitor.Core;

/// <summary>
/// Persian Solar Hijri date utility using .NET's built-in PersianCalendar.
/// </summary>
public static class PersianDateHelper
{
    private static readonly PersianCalendar _pc = new();

    /// <summary>
    /// Convert Gregorian DateTime to Persian Solar Hijri date string (yyyy/MM/dd).
    /// </summary>
    public static string ToPersianDate(DateTime date)
    {
        int y = _pc.GetYear(date);
        int m = _pc.GetMonth(date);
        int d = _pc.GetDayOfMonth(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    /// <summary>
    /// Convert to Persian date with Persian digits (۱۴۰۵/۰۶/۰۵).
    /// </summary>
    public static string ToPersianDateDigits(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date));
    }

    /// <summary>
    /// Convert to filename-safe format: ۱۴۰۵-۰۶-۰۵ (no slash, Persian digits).
    /// </summary>
    public static string ToPersianDateFile(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date).Replace("/", "-"));
    }
}
