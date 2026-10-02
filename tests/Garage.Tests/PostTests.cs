using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("postgres")]
public class PostTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_post_is_written_as_whoever_is_logged_in()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, username) = await factory.RegisterAsync();

        var postId = await GarageAppFactory.WritePostAsync(client, "Picked up a W124 today.");

        await using var db = postgres.NewDbContext();
        var post = await db.Posts.Include(p => p.Author).SingleAsync(p => p.Id == postId);

        Assert.Equal("Picked up a W124 today.", post.Body);
        Assert.Equal(username, post.Author!.UserName);
    }

    [Fact]
    public async Task The_author_cannot_be_chosen_by_the_form()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (victim, victimName) = await factory.RegisterAsync();
        var (attacker, _) = await factory.RegisterAsync();

        string victimId;
        await using (var db = postgres.NewDbContext())
        {
            victimId = (await db.Users.SingleAsync(u => u.UserName == victimName)).Id;
        }

        // Extra fields a hand-crafted request could carry. Neither name
        // exists on the input model, so neither is bound.
        var page = await attacker.GetAsync("/posts/new");
        var response = await attacker.PostAsync("/posts/new",
            await TestForms.FromAsync(page, new Dictionary<string, string>
            {
                ["Input.Body"] = "Posted in someone else's name?",
                ["Input.AuthorId"] = victimId,
                ["AuthorId"] = victimId
            }));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        await using (var db = postgres.NewDbContext())
        {
            var post = await db.Posts.SingleAsync(p => p.Body == "Posted in someone else's name?");
            Assert.NotEqual(victimId, post.AuthorId);
        }
    }

    [Fact]
    public async Task An_empty_post_is_refused()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, username) = await factory.RegisterAsync();

        var page = await client.GetAsync("/posts/new");
        var response = await client.PostAsync("/posts/new",
            await TestForms.FromAsync(page, new Dictionary<string, string>
            {
                ["Input.Body"] = "   "
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.False(await db.Posts.AnyAsync(p => p.Author!.UserName == username));
    }

    [Fact]
    public async Task A_post_is_shown_as_text_never_as_html()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();

        var postId = await GarageAppFactory.WritePostAsync(
            client, "<script>alert('stolen')</script>");

        var html = await client.GetStringAsync($"/posts/{postId}");

        Assert.DoesNotContain("<script>alert('stolen')</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task A_post_that_does_not_exist_is_a_404()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();

        var response = await client.GetAsync($"/posts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
