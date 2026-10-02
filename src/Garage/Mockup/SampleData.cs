namespace Garage.Mockup;

// THROWAWAY. Hardcoded data so the feed and profile can be looked at before
// the database exists. Delete this whole folder at Task 5, when posts start
// coming from Postgres.

public sealed record SamplePhoto(string Gradient, string Status);

public sealed record SamplePost(
    string DisplayName,
    string Username,
    string Body,
    DateTimeOffset PostedAt,
    SamplePhoto? Photo);

public sealed record SampleProfile(
    string DisplayName,
    string Username,
    string Bio,
    DateTimeOffset JoinedAt);

public static class SampleData
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public static readonly SampleProfile Me = new(
        "Marin Dinchev",
        "marin",
        "W204 owner. Mostly fixing it, occasionally driving it.",
        Now.AddMonths(-7));

    public static IReadOnlyList<SamplePost> Feed =>
    [
        new("Marin Dinchev", "marin",
            "Finally got the headlight restoration done. Three evenings of wet sanding "
          + "and I would do it again.",
            Now.AddMinutes(-12),
            new SamplePhoto("linear-gradient(135deg,#1f2933,#3e4c59 55%,#7b8794)", "ready")),

        new("Elena Georgieva", "elena",
            "Does anyone actually understand the W211 air suspension or do we all just "
          + "replace the compressor and hope?",
            Now.AddHours(-2),
            null),

        new("Stefan Kolev", "stefan",
            "Picked her up this morning. 1994, 280k km, one owner before me.",
            Now.AddHours(-5),
            new SamplePhoto("linear-gradient(135deg,#8a5a2b,#c08457 60%,#e8d3b8)", "processing")),

        new("Elena Georgieva", "elena",
            "Reminder that the cheapest mod is tyre pressure.",
            Now.AddDays(-1),
            null),
    ];

    private static readonly Dictionary<string, SampleProfile> Profiles = new()
    {
        ["marin"] = Me,
        ["elena"] = new("Elena Georgieva", "elena",
            "W211 E320 CDI. Suspension sceptic.", Now.AddYears(-2)),
        ["stefan"] = new("Stefan Kolev", "stefan",
            "W124 and nothing else, ever.", Now.AddMonths(-3)),
    };

    public static SampleProfile ProfileFor(string username)
        => Profiles.TryGetValue(username, out var profile) ? profile : Me;

    public static IReadOnlyList<SamplePost> PostsBy(string username)
        => Feed.Where(post => post.Username == username).ToList();

    public static string Initials(string displayName)
        => string.Concat(displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => part[0]))
            .ToUpperInvariant();

    public static string Relative(DateTimeOffset when)
    {
        var elapsed = Now - when;

        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes}m";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours}h";
        if (elapsed < TimeSpan.FromDays(30)) return $"{(int)elapsed.TotalDays}d";

        return when.ToString("d MMM yyyy");
    }
}
