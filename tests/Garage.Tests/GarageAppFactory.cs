using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garage.Tests;

/// <param name="aws">
/// Leave it out for tests that only presign. Pass it when the test needs
/// the web app to really talk to S3 — deleting a post deletes its objects.
/// </param>
public sealed class GarageAppFactory(string connectionString, AwsFixture? aws = null)
    : WebApplicationFactory<Program>
{
    public const string Password = "TestPassword123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);

        // Signing a URL is pure arithmetic — nothing is sent to AWS — so a
        // made-up bucket is fine for every test that only presigns. It
        // still needs *some* credentials to sign with, and the SDK reads
        // them from the environment, exactly as in docker-compose.yml.
        builder.UseSetting("AWS:Region", "eu-central-1");
        builder.UseSetting("AWS:BucketName", "garage-test");
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");

        if (aws is not null)
        {
            builder.UseSetting("AWS:Region", AwsFixture.Region);
            builder.UseSetting("AWS:BucketName", AwsFixture.BucketName);
            builder.UseSetting("AWS:ServiceUrl", aws.ServiceUrl);
            builder.UseSetting("AWS:ForcePathStyle", "true");
        }
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

    /// <summary>
    /// Writes a post through the real form as whoever the client is
    /// logged in as, and returns its id from the redirect.
    /// </summary>
    public static async Task<Guid> WritePostAsync(HttpClient client, string body)
    {
        var page = await client.GetAsync("/posts/new");
        page.EnsureSuccessStatusCode();

        var response = await client.PostAsync("/posts/new",
            await TestForms.FromAsync(page, new Dictionary<string, string>
            {
                ["Input.Body"] = body
            }));

        if (response.StatusCode != HttpStatusCode.Found)
        {
            throw new InvalidOperationException(
                $"Posting did not redirect; got {(int)response.StatusCode}.");
        }

        // Location is /posts/{id}.
        return Guid.Parse(response.Headers.Location!.OriginalString["/posts/".Length..]);
    }
}
