using System.Net.Http.Headers;
using System.Net.Http.Json;
using Garage.Data.Entities;
using Garage.Media;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("aws")]
public class PhotoPipelineIntegrationTests(PostgresFixture postgres, AwsFixture aws)
{
    private readonly PhotoTestHelpers _helpers = new(postgres, aws);

    private sealed record PresignResponse(Guid PhotoId, string UploadUrl);

    [Fact]
    public async Task An_uploaded_original_becomes_a_ready_photo_with_two_derived_files()
    {
        var (postId, photoId, key) = await _helpers.SeedAwaitingPhotoAsync();

        await _helpers.PutOriginalAsync(key);

        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();

        var photo = await _helpers.WaitForStatusAsync(photoId, PhotoStatus.Ready, TimeSpan.FromSeconds(60));

        await host.StopAsync();

        Assert.Equal(S3Keys.Thumb(postId, photoId), photo.S3KeyThumb);
        Assert.Equal(S3Keys.Display(postId, photoId), photo.S3KeyDisplay);
        Assert.NotNull(photo.ProcessedAt);
        Assert.Equal(1, photo.Attempts);

        using var thumb = await _helpers.LoadDerivedAsync(photo.S3KeyThumb!);
        Assert.Equal(300, thumb.Width);
        Assert.Null(thumb.Metadata.ExifProfile);

        using var display = await _helpers.LoadDerivedAsync(photo.S3KeyDisplay!);
        Assert.Equal(1200, display.Width);
    }

    [Fact]
    public async Task A_photo_uploaded_through_the_site_appears_in_the_feed()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString, aws);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "Fresh photo of the W124.");

        // 1. ask the site for a signed URL, exactly as the browser does
        var presign = await client.PostAsJsonAsync(
            $"/posts/{postId}/photos", new { contentType = "image/jpeg" });
        var signed = await presign.Content.ReadFromJsonAsync<PresignResponse>();

        // 2. PUT the bytes straight to S3 — a plain HttpClient, no AWS SDK,
        //    no credentials. The signature in the URL is the permission.
        using var s3 = new HttpClient();
        var bytes = new ByteArrayContent(PhotoTestHelpers.JpegBytes());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await s3.PutAsync(signed!.UploadUrl, bytes)).EnsureSuccessStatusCode();

        // 3. the worker does the rest
        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();
        await _helpers.WaitForStatusAsync(signed.PhotoId, PhotoStatus.Ready, TimeSpan.FromSeconds(60));
        await host.StopAsync();

        var feed = await client.GetStringAsync("/");

        Assert.Contains($"derived/{postId}/{signed.PhotoId}_display.jpg", feed);
    }

    [Fact]
    public async Task Deleting_a_post_removes_its_photos_from_s3()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString, aws);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "About to be deleted.");

        // A finished photo: its row, and all three of its objects.
        var photo = new Photo { PostId = postId, ContentType = "image/jpeg", Status = PhotoStatus.Ready };
        photo.S3KeyOriginal = S3Keys.Original(postId, photo.Id, "jpg");
        photo.S3KeyThumb = S3Keys.Thumb(postId, photo.Id);
        photo.S3KeyDisplay = S3Keys.Display(postId, photo.Id);

        await using (var db = _helpers.NewDbContext())
        {
            db.Photos.Add(photo);
            await db.SaveChangesAsync();
        }

        await _helpers.PutAsync(photo.S3KeyOriginal, [1]);
        await _helpers.PutAsync(photo.S3KeyThumb, [1]);
        await _helpers.PutAsync(photo.S3KeyDisplay, [1]);

        var page = await client.GetAsync($"/posts/{postId}");
        await client.PostAsync($"/posts/{postId}/delete", await TestForms.FromAsync(page, []));

        Assert.Empty(await _helpers.ListKeysAsync($"originals/{postId}/"));
        Assert.Empty(await _helpers.ListKeysAsync($"derived/{postId}/"));

        await using (var db = _helpers.NewDbContext())
        {
            Assert.False(await db.Photos.AnyAsync(p => p.Id == photo.Id));
        }
    }
}
