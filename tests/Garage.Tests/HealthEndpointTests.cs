using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Garage.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Live_returns_200_and_says_healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Port 1 is never listening, so Npgsql fails to connect immediately.
    /// No container needed — this test is about which endpoint reports
    /// the failure, not about Postgres.
    /// </summary>
    private WebApplicationFactory<Program> WithUnreachableDatabase() =>
        _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Host=127.0.0.1;Port=1;Database=garage;Username=x;Password=y;Timeout=2"));

    [Fact]
    public async Task Ready_returns_503_when_the_database_is_unreachable()
    {
        var client = WithUnreachableDatabase().CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Live_still_returns_200_when_the_database_is_unreachable()
    {
        var client = WithUnreachableDatabase().CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
