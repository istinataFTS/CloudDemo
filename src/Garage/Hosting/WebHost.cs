namespace Garage.Hosting;

public static class WebHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(25));

        builder.Services.AddRazorPages();
        builder.Services.AddHealthChecks();

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

        app.MapRazorPages();

        await app.RunAsync();
    }
}
