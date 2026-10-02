using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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

        builder.Services.AddRazorPages(options =>
        {
            // The only pages a stranger may open. Everything else falls
            // under the fallback policy below.
            options.Conventions.AllowAnonymousToPage("/Account/Register");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Error");
        });

        builder.Services
            .AddIdentityCore<GarageUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // The username is the {username} in /u/{username}. A slash
                // or a space in it would break the link to its own profile.
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyz0123456789_";

                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = false;

                // Five wrong passwords lock the account for five minutes.
                // Turns password guessing from minutes into years.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<GarageDbContext>()
            .AddSignInManager();

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // Lax: the browser does not send this cookie on a POST that
            // starts on another site. That is what protects the JSON
            // endpoints in Task 8, which carry no antiforgery token.
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        builder.Services.AddAuthorization(options =>
        {
            // Every endpoint requires a login unless it explicitly allows
            // anonymous access. A page added later is closed until someone
            // deliberately opens it; a forgotten attribute leaks nothing.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        // Before routing, so CSS and JS are served without a login. The
        // login page has to be able to load its own stylesheet.
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Liveness excludes every registered check on purpose. It answers
        // "is this process alive", not "can it serve traffic". A database
        // outage must not make every container restart itself.
        //
        // Both health checks are anonymous: a load balancer has no account.
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false
        }).AllowAnonymous();

        // Readiness includes the database. The load balancer uses this to
        // decide whether to send traffic here — not whether to kill it.
        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        app.MapRazorPages();

        await app.RunAsync();
    }
}
