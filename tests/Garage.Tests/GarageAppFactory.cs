using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garage.Tests;

public sealed class GarageAppFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    public const string Password = "TestPassword123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }

    /// <summary>
    /// A client that shows you redirects instead of following them, so a
    /// test can see "302 to /login" rather than the login page's HTML.
    /// </summary>
    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    /// <summary>
    /// Registers a brand-new user through the real form and returns a
    /// client carrying their login cookie. A fresh username every call,
    /// because every test shares one database.
    /// </summary>
    public async Task<(HttpClient Client, string Username)> RegisterAsync()
    {
        var username = "u" + Guid.NewGuid().ToString("N")[..12];
        var client = CreateBrowser();

        var page = await client.GetAsync("/register");
        page.EnsureSuccessStatusCode();

        var form = await TestForms.FromAsync(page, new Dictionary<string, string>
        {
            ["Input.Email"] = $"{username}@example.com",
            ["Input.Username"] = username,
            ["Input.DisplayName"] = $"Driver {username}",
            ["Input.Password"] = Password
        });

        var response = await client.PostAsync("/register", form);

        if (response.StatusCode != HttpStatusCode.Found)
        {
            throw new InvalidOperationException(
                $"Registration did not redirect; got {(int)response.StatusCode}.");
        }

        return (client, username);
    }
}
