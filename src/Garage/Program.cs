using Garage.Hosting;

var role = Environment.GetEnvironmentVariable("ROLE") ?? "web";

switch (role)
{
    case "web":
        await WebHost.RunAsync(args);
        break;

    case "worker":
        await WorkerHost.RunAsync(args);
        break;

    default:
        throw new InvalidOperationException(
            $"ROLE must be 'web' or 'worker', not '{role}'.");
}

// Named so WebApplicationFactory<Program> can find the entry point from tests.
public partial class Program;
