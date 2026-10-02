using System.Net;
using Garage.Data;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

[Collection("postgres")]
public class AccountTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_stranger_is_sent_to_the_login_page()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var browser = factory.CreateBrowser();

        var response = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location!.AbsolutePath);
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/login")]
    [InlineData("/css/site.css")]
    public async Task The_way_in_is_open_to_strangers(string path)
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var browser = factory.CreateBrowser();

        var response = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Registering_logs_you_in_and_stores_your_profile()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);

        var (client, username) = await factory.RegisterAsync();

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);

        await using var db = postgres.NewDbContext();
        var user = await db.Users.SingleAsync(u => u.UserName == username);

        Assert.Equal($"Driver {username}", user.DisplayName);
        Assert.Equal($"{username}@example.com", user.Email);
    }

    [Fact]
    public async Task You_can_log_out_and_back_in()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (client, username) = await factory.RegisterAsync();

        // The log-out button lives in the layout, so any page carries a token.
        var home = await client.GetAsync("/");
        var logout = await client.PostAsync("/logout",
            await TestForms.FromAsync(home, []));
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);

        var afterLogout = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Found, afterLogout.StatusCode);

        var loginPage = await client.GetAsync("/login");
        var login = await client.PostAsync("/login",
            await TestForms.FromAsync(loginPage, new Dictionary<string, string>
            {
                ["Input.Email"] = $"{username}@example.com",
                ["Input.Password"] = GarageAppFactory.Password
            }));
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);

        var afterLogin = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, afterLogin.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_gets_a_vague_answer()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (_, username) = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        var loginPage = await browser.GetAsync("/login");
        var response = await browser.PostAsync("/login",
            await TestForms.FromAsync(loginPage, new Dictionary<string, string>
            {
                ["Input.Email"] = $"{username}@example.com",
                ["Input.Password"] = "not-the-password"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid login attempt.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_username_that_would_break_a_url_is_refused()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var browser = factory.CreateBrowser();

        var page = await browser.GetAsync("/register");
        var response = await browser.PostAsync("/register",
            await TestForms.FromAsync(page, new Dictionary<string, string>
            {
                ["Input.Email"] = "slash@example.com",
                ["Input.Username"] = "../admin",
                ["Input.DisplayName"] = "Slash",
                ["Input.Password"] = GarageAppFactory.Password
            }));

        // 200 means the form came back with an error; 302 would mean an
        // account was created.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = postgres.NewDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "slash@example.com"));
    }

    [Fact]
    public async Task Logging_in_never_redirects_off_site()
    {
        await using var factory = new GarageAppFactory(postgres.ConnectionString);
        var (_, username) = await factory.RegisterAsync();
        var browser = factory.CreateBrowser();

        var loginPage = await browser.GetAsync("/login");
        var response = await browser.PostAsync(
            "/login?ReturnUrl=https%3A%2F%2Fevil.example%2F",
            await TestForms.FromAsync(loginPage, new Dictionary<string, string>
            {
                ["Input.Email"] = $"{username}@example.com",
                ["Input.Password"] = GarageAppFactory.Password
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }
}
