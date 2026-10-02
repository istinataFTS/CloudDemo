using Garage.Media;

namespace Garage.Tests;

public class S3KeysTests
{
    private static readonly Guid Post = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Photo = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Original_key_carries_the_real_extension()
    {
        Assert.Equal(
            "originals/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.png",
            S3Keys.Original(Post, Photo, "png"));
    }

    [Fact]
    public void Derived_keys_are_always_jpg_because_the_worker_writes_jpeg()
    {
        Assert.Equal(
            "derived/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222_thumb.jpg",
            S3Keys.Thumb(Post, Photo));

        Assert.Equal(
            "derived/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222_display.jpg",
            S3Keys.Display(Post, Photo));
    }

    [Theory]
    [InlineData("originals/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.jpg")]
    [InlineData("originals/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.webp")]
    public void PhotoId_can_be_recovered_from_an_original_key(string key)
    {
        Assert.True(S3Keys.TryParsePhotoId(key, out var photoId));
        Assert.Equal(Photo, photoId);
    }

    [Theory]
    [InlineData("derived/aaa/bbb_thumb.jpg")]
    [InlineData("originals/not-a-guid.jpg")]
    [InlineData("originals/11111111-1111-1111-1111-111111111111/hello.jpg")]
    [InlineData("")]
    public void Nonsense_keys_are_rejected_rather_than_guessed(string key)
    {
        Assert.False(S3Keys.TryParsePhotoId(key, out _));
    }

    [Theory]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("image/png", "png")]
    [InlineData("image/webp", "webp")]
    [InlineData("IMAGE/JPEG", "jpg")]
    public void Allowed_content_types_map_to_an_extension(string contentType, string expected)
    {
        Assert.True(ContentTypes.TryGetExtension(contentType, out var extension));
        Assert.Equal(expected, extension);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("")]
    public void Everything_else_is_refused(string contentType)
    {
        Assert.False(ContentTypes.TryGetExtension(contentType, out _));
    }
}
