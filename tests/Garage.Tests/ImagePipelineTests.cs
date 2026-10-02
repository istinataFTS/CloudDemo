using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Garage.Media;

namespace Garage.Tests;

public class ImagePipelineTests
{
    /// <summary>
    /// A 2000x1500 JPEG carrying the metadata a phone actually attaches:
    /// camera make, software, and the GPS reference that turns a photo
    /// of a car into a photo of your yard.
    /// </summary>
    private static MemoryStream PhoneStylePhoto(
        int width = 2000, int height = 1500, ushort orientation = 1)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(context => context.BackgroundColor(Color.CornflowerBlue));

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "TestPhone");
        exif.SetValue(ExifTag.Software, "TestCamera 1.0");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.Orientation, orientation);
        image.Metadata.ExifProfile = exif;

        var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
        stream.Position = 0;

        return stream;
    }

    [Fact]
    public void The_original_fixture_really_does_carry_gps_metadata()
    {
        // Guards the test itself. Without this, a change that silently
        // stops writing EXIF would make the next test pass for the
        // wrong reason forever.
        using var source = PhoneStylePhoto();
        using var loaded = Image.Load(source);

        Assert.NotNull(loaded.Metadata.ExifProfile);
        Assert.True(loaded.Metadata.ExifProfile!.TryGetValue(
            ExifTag.GPSLatitudeRef, out _));
    }

    [Fact]
    public void Both_outputs_have_no_exif_at_all()
    {
        using var source = PhoneStylePhoto();

        var result = ImagePipeline.Process(source, watermarkText: null);

        using var thumb = Image.Load(new MemoryStream(result.Thumb));
        using var display = Image.Load(new MemoryStream(result.Display));

        Assert.Null(thumb.Metadata.ExifProfile);
        Assert.Null(display.Metadata.ExifProfile);
    }

    [Fact]
    public void The_thumbnail_is_300_pixels_wide_and_keeps_its_aspect_ratio()
    {
        using var source = PhoneStylePhoto(2000, 1500);

        var result = ImagePipeline.Process(source, watermarkText: null);

        using var thumb = Image.Load(new MemoryStream(result.Thumb));

        Assert.Equal(300, thumb.Width);
        Assert.Equal(225, thumb.Height);
    }

    [Fact]
    public void A_photo_the_phone_saved_sideways_comes_out_upright()
    {
        // Phones often store a portrait shot as landscape pixels plus an
        // EXIF note: "rotate 90 degrees clockwise" (orientation 6). Strip
        // the note without applying it and the car lies on its side.
        using var source = PhoneStylePhoto(2000, 1500, orientation: 6);

        var result = ImagePipeline.Process(source, watermarkText: null);

        using var thumb = Image.Load(new MemoryStream(result.Thumb));

        Assert.Equal(300, thumb.Width);
        Assert.Equal(400, thumb.Height);
    }

    [Fact]
    public void The_display_size_is_1200_pixels_wide()
    {
        using var source = PhoneStylePhoto(2000, 1500);

        var result = ImagePipeline.Process(source, watermarkText: null);

        using var display = Image.Load(new MemoryStream(result.Display));

        Assert.Equal(1200, display.Width);
        Assert.Equal(900, display.Height);
    }

    [Fact]
    public void A_small_photo_is_never_blown_up()
    {
        // Upscaling turns a 400px snapshot into a blurry 1200px
        // snapshot and three times the bytes.
        using var source = PhoneStylePhoto(400, 300);

        var result = ImagePipeline.Process(source, watermarkText: null);

        using var display = Image.Load(new MemoryStream(result.Display));

        Assert.Equal(400, display.Width);
    }

    [Fact]
    public void The_watermark_changes_pixels_in_the_bottom_right_corner()
    {
        if (!ImagePipeline.FontsAvailable)
        {
            // No fonts installed on this machine. The container installs
            // fonts-dejavu-core, so CI and production still cover this.
            return;
        }

        using var plainSource = PhoneStylePhoto();
        using var markedSource = PhoneStylePhoto();

        var plain = ImagePipeline.Process(plainSource, watermarkText: null);
        var marked = ImagePipeline.Process(markedSource, watermarkText: "@ivan_w124");

        using var plainImage = Image.Load<Rgba32>(new MemoryStream(plain.Display));
        using var markedImage = Image.Load<Rgba32>(new MemoryStream(marked.Display));

        var differences = 0;
        for (var y = markedImage.Height - 60; y < markedImage.Height; y++)
        {
            for (var x = markedImage.Width - 300; x < markedImage.Width; x++)
            {
                if (!plainImage[x, y].Equals(markedImage[x, y]))
                {
                    differences++;
                }
            }
        }

        Assert.True(differences > 100,
            $"Expected the watermark to change the corner; {differences} pixels differ.");
    }

    [Fact]
    public void The_thumbnail_is_never_watermarked()
    {
        if (!ImagePipeline.FontsAvailable)
        {
            return;
        }

        using var plainSource = PhoneStylePhoto();
        using var markedSource = PhoneStylePhoto();

        var plain = ImagePipeline.Process(plainSource, watermarkText: null);
        var marked = ImagePipeline.Process(markedSource, watermarkText: "@ivan_w124");

        // 300px wide, so text would be unreadable noise.
        Assert.Equal(plain.Thumb, marked.Thumb);
    }

    [Fact]
    public void A_file_that_is_not_an_image_is_rejected()
    {
        using var nonsense = new MemoryStream("this is not a jpeg"u8.ToArray());

        Assert.ThrowsAny<Exception>(() => ImagePipeline.Process(nonsense, null));
    }

    [Fact]
    public void An_image_with_more_pixels_than_the_limit_is_refused_before_decoding()
    {
        // 2000 x 1500 is three million pixels; the limit here is one.
        // In production the limit is 60 million, but the check is the same.
        using var source = PhoneStylePhoto(2000, 1500);

        var error = Assert.Throws<InvalidOperationException>(
            () => ImagePipeline.Process(source, watermarkText: null, maxPixels: 1_000_000));

        Assert.Contains("2000x1500", error.Message);
    }
}
