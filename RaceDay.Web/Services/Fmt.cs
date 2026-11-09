using System.Globalization;

namespace RaceDay.Web.Services;

/// <summary>Small display helpers used by the views.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Date(DateOnly d) => d.ToString("ddd, d MMM yyyy", Inv);
    public static string Date(DateOnly? d) => d.HasValue ? Date(d.Value) : "-";
    public static string DateTimeShort(DateTime d) => d.ToString("d MMM yyyy, HH:mm", Inv);

    public static string Time(TimeSpan? t) =>
        t.HasValue ? $"{(int)t.Value.TotalHours}:{t.Value.Minutes:00}:{t.Value.Seconds:00}" : "-";

    /// <summary>1 -> 1st, 2 -> 2nd, 11 -> 11th, 47 -> 47th.</summary>
    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        var suffix = (lastTwo is >= 11 and <= 13) ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n + suffix;
    }

    /// <summary>"47th out of 312 finishers", or just "47th" when the total is unknown.</summary>
    public static string PositionOf(int? position, int? total)
    {
        if (position is null) return "-";
        return total is > 0 ? $"{Ordinal(position.Value)} out of {total} finishers" : Ordinal(position.Value);
    }

    /// <summary>Bootstrap-style colour class for an enrolment status.</summary>
    public static string StatusClass(string status) => status switch
    {
        "Completed" => "rd-status-green",
        "Registered" => "rd-status-amber",
        "Withdrawn" => "rd-status-grey",
        _ => "rd-status-grey"
    };

    public static string Distance(decimal d) => d.ToString("0.##", Inv) + " km";

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }
}
