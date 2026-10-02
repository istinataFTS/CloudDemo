using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("postgres")]
public class OwnershipTests(PostgresFixture postgres)
{
    [Fact]
    public async Task You_can_delete_your_own_post()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(client, "Sold the car. Deleting this.");

        var page = await client.GetAsync($"/posts/{postId}");
        var response = await client.PostAsync($"/posts/{postId}/delete",
            await TestForms.FromAsync(page, []));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.False(await db.Posts.AnyAsync(p => p.Id == postId));
    }

    [Fact]
    public async Task You_cannot_delete_someone_elses_post()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var (stranger, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "Mine. Hands off.");

        // A real token from the stranger's own session — this is a
        // logged-in user trying it on, not a forged request.
        var page = await stranger.GetAsync("/");
        var response = await stranger.PostAsync($"/posts/{postId}/delete",
            await TestForms.FromAsync(page, []));

        // 404, not 403: "not yours" and "does not exist" look the same.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.True(await db.Posts.AnyAsync(p => p.Id == postId));
    }

    [Fact]
    public async Task The_delete_button_is_only_shown_to_the_author()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (owner, _) = await factory.RegisterAsync();
        var (stranger, _) = await factory.RegisterAsync();
        var postId = await GarageAppFactory.WritePostAsync(owner, "Button test.");

        var asOwner = await owner.GetStringAsync($"/posts/{postId}");
        var asStranger = await stranger.GetStringAsync($"/posts/{postId}");

        Assert.Contains("Delete post", asOwner);
        Assert.DoesNotContain("Delete post", asStranger);
    }
}
