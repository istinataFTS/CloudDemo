using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Garage.Media;

public sealed record ProcessedImage(byte[] Thumb, byte[] Display);

public static class ImagePipeline
{
    public const int ThumbWidth = 300;
    public const int DisplayWidth = 1200;

    private const int JpegQuality = 82;

    /// <summary>
    /// Bigger than any phone's normal photo mode. Decoding costs width ×
    /// height × 4 bytes of memory: 240 MB here, 10 GB for a 50,000-pixel
    /// square — which a PNG of a few kilobytes can claim to be.
    /// </summary>
    public const long MaxPixels = 60_000_000;

    /// <summary>Checked by the worker before it downloads an original.</summary>
    public const long MaxOriginalBytes = 25 * 1024 * 1024;

    public static bool FontsAvailable => SystemFonts.Families.Any();

    /// <summary>
    /// Produces the two derived sizes. Both are JPEG regardless of what
    /// came in, which is why the derived S3 keys always end in .jpg.
    /// Throws on anything that is not a decodable image, or is too big to
    /// decode safely — the caller turns that into a failed photo.
    /// The stream must be seekable: it is read twice.
    /// </summary>
    public static ProcessedImage Process(
        Stream original, string? watermarkText, long maxPixels = MaxPixels)
    {
        ArgumentNullException.ThrowIfNull(original);

        // Read only the header first. It says how big the image claims to
        // be, without allocating a single pixel.
        original.Position = 0;
        var header = Image.Identify(original);

        if ((long)header.Width * header.Height > maxPixels)
        {
            throw new InvalidOperationException(
                $"{header.Width}x{header.Height} is more pixels than this site accepts.");
        }

        original.Position = 0;
        using var image = Image.Load<Rgba32>(original);

        // Phones often store a portrait shot as landscape pixels plus an
        // EXIF "rotate me" flag. Apply it now, because the next lines
        // throw the flag away with the rest of the metadata.
        image.Mutate(context => context.AutoOrient());

        // Every profile, not just EXIF. IPTC and XMP carry location and
        // author data too, and a phone writes whichever it likes.
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var thumb = Render(image, ThumbWidth, watermarkText: null);
        var display = Render(image, DisplayWidth, watermarkText);

        return new ProcessedImage(thumb, display);
    }

    private static byte[] Render(Image<Rgba32> source, int targetWidth, string? watermarkText)
    {
        using var copy = source.Clone(context =>
        {
            if (source.Width > targetWidth)
            {
                context.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, 0),
                    Mode = ResizeMode.Max
                });
            }
        });

        if (!string.IsNullOrWhiteSpace(watermarkText))
        {
            ApplyWatermark(copy, watermarkText);
        }

        var buffer = new MemoryStream();
        copy.SaveAsJpeg(buffer, new JpegEncoder { Quality = JpegQuality });

        return buffer.ToArray();
    }

    private static void ApplyWatermark(Image<Rgba32> image, string text)
    {
        if (!TryResolveFont(image.Width, out var font))
        {
            // No fonts on this machine. A missing watermark is a
            // cosmetic loss; a crashed worker is not.
            return;
        }

        const float margin = 16f;

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(image.Width - margin, image.Height - margin),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        image.Mutate(context => context.DrawText(
            options, text, Brushes.Solid(Color.White.WithAlpha(0.4f))));
    }

    private static bool TryResolveFont(int imageWidth, out Font font)
    {
        font = null!;

        var family = SystemFonts.TryGet("DejaVu Sans", out var dejaVu)
            ? dejaVu
            : SystemFonts.Families.FirstOrDefault();

        if (family == default)
        {
            return false;
        }

        // Scale with the image so the mark reads the same at any size.
        var size = Math.Max(14f, imageWidth * 0.035f);
        font = family.CreateFont(size, FontStyle.Bold);

        return true;
    }
}
