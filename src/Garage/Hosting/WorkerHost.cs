using Garage.Worker;

namespace Garage.Hosting;

public static class WorkerHost
{
    public static async Task RunAsync(string[] args)
    {
        // Host, not WebApplication: the worker opens no socket and has
        // no reason to carry Kestrel, routing or the Razor Pages engine.
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<HostOptions>(options =>
        {
            // ECS sends SIGTERM and waits 30 seconds. Exiting at 25
            // means we finish on our own terms instead of being killed
            // halfway through an S3 write.
            options.ShutdownTimeout = TimeSpan.FromSeconds(25);

            // A crash in the poll loop should stop the container so ECS
            // replaces it, not leave a healthy-looking task that has
            // silently stopped working.
            options.BackgroundServiceExceptionBehavior =
                BackgroundServiceExceptionBehavior.StopHost;
        });

        builder.Services.AddGarageCore(builder.Configuration);
        builder.Services.AddScoped<IPhotoProcessor, LoggingPhotoProcessor>();
        builder.Services.AddHostedService<PhotoWorker>();

        var host = builder.Build();

        await host.RunAsync();
    }
}
