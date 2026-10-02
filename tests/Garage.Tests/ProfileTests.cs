using System.Net;
using Garage.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("postgres")]
public class ProfileTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_profile_shows_that_persons_posts_and_nobody_elses()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (alice, aliceName) = await factory.RegisterAsync();
        var (bob, _) = await factory.RegisterAsync();

        var hers = $"by alice {Guid.NewGuid()}";
        var his = $"by bob {Guid.NewGuid()}";
        await GarageAppFactory.WritePostAsync(alice, hers);
        await GarageAppFactory.WritePostAsync(bob, his);

        // Bob looks at Alice's profile.
        var html = await bob.GetStringAsync($"/u/{aliceName}");

        Assert.Contains($"Driver {aliceName}", html);
        Assert.Contains(hers, html);
        Assert.DoesNotContain(his, html);
    }

    [Fact]
    public async Task A_profile_pages_twenty_posts_at_a_time()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, username) = await factory.RegisterAsync();

        // 21 posts, a minute apart, written straight to the database:
        // through the form they would share a timestamp to the millisecond.
        await using (var db = postgres.NewDbContext())
        {
            var me = await db.Users.SingleAsync(u => u.UserName == username);
            var start = DateTimeOffset.UtcNow.AddDays(-1);

            for (var i = 1; i <= 21; i++)
            {
                db.Posts.Add(new Post
                {
                    AuthorId = me.Id,
                    Body = $"post number {i:00}",
                    CreatedAt = start.AddMinutes(i)
                });
            }
            await db.SaveChangesAsync();
        }

        var first = await client.GetStringAsync($"/u/{username}");
        var second = await client.GetStringAsync($"/u/{username}?page=2");

        Assert.Contains("post number 21", first);
        Assert.Contains("post number 02", first);
        Assert.DoesNotContain("post number 01", first);
        Assert.Contains("?page=2", first);

        Assert.Contains("post number 01", second);
        Assert.DoesNotContain("post number 02", second);
    }

    [Fact]
    public async Task An_unknown_username_is_a_404()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();

        var response = await client.GetAsync("/u/nobody_by_this_name");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
