using System.Net;

namespace Garage.Tests;

[Collection("postgres")]
public class FeedTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_feed_shows_everyones_posts_newest_first()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (alice, _) = await factory.RegisterAsync();
        var (bob, _) = await factory.RegisterAsync();

        var older = $"older {Guid.NewGuid()}";
        var newer = $"newer {Guid.NewGuid()}";
        await GarageAppFactory.WritePostAsync(alice, older);
        await GarageAppFactory.WritePostAsync(bob, newer);

        var html = await alice.GetStringAsync("/");

        Assert.Contains(older, html);
        Assert.Contains(newer, html);
        Assert.True(html.IndexOf(newer, StringComparison.Ordinal)
                    < html.IndexOf(older, StringComparison.Ordinal),
            "The newer post should be above the older one.");
    }

    [Fact]
    public async Task Every_post_in_the_feed_links_to_its_authors_profile()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (alice, aliceName) = await factory.RegisterAsync();
        var (bob, _) = await factory.RegisterAsync();
        await GarageAppFactory.WritePostAsync(alice, "Link me.");

        // Bob reads, so the only link to Alice's profile on the page can be
        // the one on her post. The header links to the *reader's* profile.
        var html = await bob.GetStringAsync("/");

        Assert.Contains($"href=\"/u/{aliceName}\"", html);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("banana")]
    public async Task A_page_number_below_one_means_the_first_page(string page)
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();
        var newest = $"newest {Guid.NewGuid()}";
        await GarageAppFactory.WritePostAsync(client, newest);

        var html = await client.GetStringAsync($"/?page={page}");

        Assert.Contains(newest, html);
    }

    [Fact]
    public async Task An_absurd_page_number_is_an_empty_page_not_an_error()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, _) = await factory.RegisterAsync();

        var response = await client.GetAsync("/?page=2147483647");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nothing here yet.", await response.Content.ReadAsStringAsync());
    }
}
