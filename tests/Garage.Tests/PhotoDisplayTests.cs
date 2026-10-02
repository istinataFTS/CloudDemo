using Garage.Data.Entities;
using Garage.Media;

namespace Garage.Tests;

[Collection("postgres")]
public class PhotoDisplayTests(PostgresFixture postgres)
{
    private async Task<Guid> PostWithPhotoAsync(HttpClient author, PhotoStatus status)
    {
        var postId = await GarageAppFactory.WritePostAsync(author, $"Photo is {status}.");

        var photo = new Photo { PostId = postId, ContentType = "image/jpeg", Status = status };
        photo.S3KeyOriginal = S3Keys.Original(postId, photo.Id, "jpg");

        await using var db = postgres.NewDbContext();
        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        return postId;
    }

    [Theory]
    [InlineData(PhotoStatus.Queued)]
    [InlineData(PhotoStatus.Processing)]
    public async Task A_photo_on_its_way_shows_as_processing_to_everyone(PhotoStatus status)
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (author, _) = await factory.RegisterAsync();
        var (reader, _) = await factory.RegisterAsync();
        var postId = await PostWithPhotoAsync(author, status);

        var html = await reader.GetStringAsync($"/posts/{postId}");

        Assert.Contains("<small>processing</small>", html);
    }

    [Theory]
    [InlineData(PhotoStatus.AwaitingUpload)]
    [InlineData(PhotoStatus.Failed)]
    public async Task A_photo_that_never_arrived_or_failed_is_not_shown(PhotoStatus status)
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (author, _) = await factory.RegisterAsync();
        var (reader, _) = await factory.RegisterAsync();
        var postId = await PostWithPhotoAsync(author, status);

        var html = await reader.GetStringAsync($"/posts/{postId}");

        Assert.DoesNotContain("<small>processing</small>", html);
        Assert.DoesNotContain("<img", html);
    }
}
