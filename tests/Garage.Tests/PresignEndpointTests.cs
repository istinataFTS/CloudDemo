using System.Net;
using System.Net.Http.Json;
using Garage.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("postgres")]
public class PresignEndpointTests(PostgresFixture postgres)
{
    private sealed record PresignResponse(Guid PhotoId, string UploadUrl);

    private static Task<HttpResponseMessage> PresignAsync(
        HttpClient client, Guid postId, string contentType = "image/jpeg")
        => client.PostAsJsonAsync($"/posts/{postId}/photos", new { contentType });

    [Fact]
    public async Task Presign_creates_an_awaiting_upload_row_and_returns_a_signed_url()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "My car.");

        var response = await PresignAsync(client, postId);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PresignResponse>();

        Assert.NotNull(body);
        Assert.Contains("X-Amz-Signature", body!.UploadUrl);

        await using var db = postgres.NewDbContext();
        var photo = await db.Photos.SingleAsync(p => p.Id == body.PhotoId);

        Assert.Equal(PhotoStatus.AwaitingUpload, photo.Status);
        Assert.Equal($"originals/{postId}/{body.PhotoId}.jpg", photo.S3KeyOriginal);
        Assert.Equal("image/jpeg", photo.ContentType);
    }

    [Fact]
    public async Task A_disallowed_content_type_is_refused_before_a_row_is_written()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "My car.");

        var response = await PresignAsync(client, postId, "image/svg+xml");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.False(await db.Photos.AnyAsync(p => p.PostId == postId));
    }

    [Fact]
    public async Task Presign_requires_a_login()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "My car.");

        var response = await PresignAsync(factory.CreateBrowser(), postId);

        // 401, not a 302 to /login: since .NET 10 the login cookie answers
        // a JSON endpoint with a status code instead of an HTML redirect.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task You_cannot_add_a_photo_to_someone_elses_post()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var (stranger, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "My car, not yours.");

        var response = await PresignAsync(stranger, postId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.False(await db.Photos.AnyAsync(p => p.PostId == postId));
    }

    [Fact]
    public async Task A_post_takes_at_most_ten_photos()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "Lots of angles.");

        for (var i = 0; i < 10; i++)
        {
            (await PresignAsync(client, postId)).EnsureSuccessStatusCode();
        }

        var eleventh = await PresignAsync(client, postId);

        Assert.Equal(HttpStatusCode.BadRequest, eleventh.StatusCode);
    }

    [Fact]
    public async Task The_uploaded_callback_moves_the_row_to_queued()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "My car.");
        var body = await (await PresignAsync(client, postId))
            .Content.ReadFromJsonAsync<PresignResponse>();

        var callback = await client.PostAsync($"/photos/{body!.PhotoId}/uploaded", null);
        Assert.Equal(HttpStatusCode.NoContent, callback.StatusCode);

        await using var db = postgres.NewDbContext();
        var photo = await db.Photos.SingleAsync(p => p.Id == body.PhotoId);

        Assert.Equal(PhotoStatus.Queued, photo.Status);
    }

    [Fact]
    public async Task The_uploaded_callback_never_drags_a_finished_photo_backwards()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "My car.");
        var body = await (await PresignAsync(client, postId))
            .Content.ReadFromJsonAsync<PresignResponse>();

        // The worker got there first — a slow browser tab is about to
        // send a callback for a photo that is already done.
        await using (var db = postgres.NewDbContext())
        {
            var photo = await db.Photos.SingleAsync(p => p.Id == body!.PhotoId);
            photo.Status = PhotoStatus.Ready;
            await db.SaveChangesAsync();
        }

        var callback = await client.PostAsync($"/photos/{body!.PhotoId}/uploaded", null);
        Assert.Equal(HttpStatusCode.NoContent, callback.StatusCode);

        await using (var db = postgres.NewDbContext())
        {
            var photo = await db.Photos.SingleAsync(p => p.Id == body.PhotoId);
            Assert.Equal(PhotoStatus.Ready, photo.Status);
        }
    }

    [Fact]
    public async Task Someone_else_cannot_mark_your_photo_uploaded()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var (stranger, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "My car.");
        var body = await (await PresignAsync(owner, postId))
            .Content.ReadFromJsonAsync<PresignResponse>();

        var callback = await stranger.PostAsync($"/photos/{body!.PhotoId}/uploaded", null);

        Assert.Equal(HttpStatusCode.NotFound, callback.StatusCode);

        await using var db = postgres.NewDbContext();
        var photo = await db.Photos.SingleAsync(p => p.Id == body.PhotoId);
        Assert.Equal(PhotoStatus.AwaitingUpload, photo.Status);
    }

    [Fact]
    public async Task Only_the_author_can_see_a_posts_photo_status()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var (stranger, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "My car.");

        var asOwner = await owner.GetAsync($"/posts/{postId}/photos/status");
        var asStranger = await stranger.GetAsync($"/posts/{postId}/photos/status");

        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asStranger.StatusCode);
    }
}
