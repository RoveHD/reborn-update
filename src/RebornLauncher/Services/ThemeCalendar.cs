using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace PokemonRebornLauncher.Services;

/// <summary>Recurring local-calendar seasons; no stored year or network clock required.</summary>
public static class ThemeCalendar
{
    public static readonly string[] ThemeIds = ["standard", "halloween", "winter", "spring", "summer", "harvest", "newyear", "arceus"];

    public static string Resolve(DateOnly day, string? discoverySeed = null)
    {
        if (IsArceusDay(day, discoverySeed)) return "arceus";
        int md = day.Month * 100 + day.Day;
        if (md >= 1231 || md <= 102) return "newyear";
        if (md >= 1201 || md <= 106) return "winter";
        if (md >= 1015 && md <= 1102) return "halloween";
        if (day.Month is >= 3 and <= 5) return "spring";
        if (day.Month is >= 6 and <= 8) return "summer";
        if (day.Month is >= 9 and <= 11) return "harvest";
        return "standard";
    }

    public static bool IsArceusDay(DateOnly day, string? seed)
    {
        if (!Guid.TryParse(seed, out var identity)) return false;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"reborn-arceus-v1|{identity:D}|{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"));
        // One independent opportunity per local calendar day. Restarting cannot reroll.
        return hash[0] == 0 && (hash[1] & 3) == 0;
    }

    public static bool IsEasterSeason(DateOnly day) => Math.Abs(day.DayNumber - EasterSunday(day.Year).DayNumber) <= 14;

    public static DateOnly EasterSunday(int year)
    {
        if (year < 1583 || year > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        int value = h + l - 7 * m + 114;
        return new DateOnly(year, value / 31, value % 31 + 1);
    }
}
