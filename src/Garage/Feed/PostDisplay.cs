using System.Globalization;
using System.Text;

namespace Garage.Feed;

/// <summary>
/// The small bits of formatting the post card needs. Pure functions, so
/// they get unit tests instead of being buried in a view.
/// </summary>
public static class PostDisplay
{
    /// <summary>
    /// "Ivan Petrov" -> "IP". The avatar, until there are profile photos.
    /// A Rune rather than a char, so a name that starts with an emoji is
    /// not cut in half.
    /// </summary>
    public static string Initials(string displayName)
        => string.Concat(displayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => Rune.GetRuneAt(part, 0).ToString()))
            .ToUpperInvariant();

    /// <summary>
    /// "12m", "5h", "3d", then a date. The current time is a parameter,
    /// not DateTimeOffset.UtcNow, so a test can say what "now" is.
    /// </summary>
    public static string Relative(DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;

        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes}m";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours}h";
        if (elapsed < TimeSpan.FromDays(30)) return $"{(int)elapsed.TotalDays}d";

        return when.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }
}
