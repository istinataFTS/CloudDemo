using Garage.Data;
using Microsoft.EntityFrameworkCore;

namespace Garage.Hosting;

public static class WebHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(25));

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default is not set.");

        builder.Services.AddDbContext<GarageDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        builder.Services.AddRazorPages();
        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();

        // Liveness excludes every registered check on purpose. It answers
        // "is this process alive", not "can it serve traffic". A database
        // outage must not make every container restart itself.
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false
        });

        // Readiness includes the database. The load balancer uses this to
        // decide whether to send traffic here — not whether to kill it.
        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapRazorPages();

        await app.RunAsync();
    }
}
