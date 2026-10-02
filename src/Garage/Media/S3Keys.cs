namespace Garage.Media;

/// <summary>
/// Every S3 key in the system is built here. No string interpolation of
/// keys anywhere else — a key built in two places drifts in two places.
/// </summary>
public static class S3Keys
{
    public const string OriginalsPrefix = "originals/";
    public const string DerivedPrefix = "derived/";

    public static string Original(Guid postId, Guid photoId, string extension)
        => $"{OriginalsPrefix}{postId}/{photoId}.{extension}";

    public static string Thumb(Guid postId, Guid photoId)
        => $"{DerivedPrefix}{postId}/{photoId}_thumb.jpg";

    public static string Display(Guid postId, Guid photoId)
        => $"{DerivedPrefix}{postId}/{photoId}_display.jpg";

    /// <summary>
    /// originals/{postId}/{photoId}.{ext} -> photoId. Returns false for
    /// anything else, including derived keys and hand-uploaded junk,
    /// so the worker can discard a message instead of throwing.
    /// </summary>
    public static bool TryParsePhotoId(string? key, out Guid photoId)
    {
        photoId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(OriginalsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = key.Split('/');
        if (segments.Length != 3)
        {
            return false;
        }

        var fileName = segments[2];
        var dot = fileName.LastIndexOf('.');
        var stem = dot < 0 ? fileName : fileName[..dot];

        return Guid.TryParse(stem, out photoId);
    }
}
