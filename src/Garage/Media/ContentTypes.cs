using System.Diagnostics.CodeAnalysis;

namespace Garage.Media;

public static class ContentTypes
{
    private static readonly Dictionary<string, string> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/webp"] = "webp"
        };

    /// <summary>
    /// An allowlist, never a blocklist. A blocklist is a promise that you
    /// thought of every dangerous format, and nobody has ever kept it.
    /// </summary>
    public static bool TryGetExtension(
        string? contentType, [NotNullWhen(true)] out string? extension)
    {
        extension = null;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return Allowed.TryGetValue(contentType.Trim(), out extension);
    }
}
