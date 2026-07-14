using System;

namespace RfcBuddy.App.Core;

/// <summary>
/// Provides helper extension methods for converting UTC date times to Pacific Time (PT), 
/// which is strictly UTC-7 year-round for British Columbia as of recent policy (no PST/PDT shifts).
/// </summary>
public static class DateTimeExtensions
{
    private const int PtOffsetHours = -7;

    /// <summary>
    /// Converts a UTC DateTime to Pacific Time (PT), which is UTC-7 year-round.
    /// </summary>
    public static DateTime ToPt(this DateTime utcDateTime)
    {
        if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue)
        {
            return utcDateTime;
        }

        DateTime utc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return utc.AddHours(PtOffsetHours);
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to Pacific Time (PT), which is UTC-7 year-round.
    /// </summary>
    public static DateTime? ToPt(this DateTime? utcDateTime)
    {
        if (utcDateTime == null)
        {
            return null;
        }

        return ToPt(utcDateTime.Value);
    }
}
