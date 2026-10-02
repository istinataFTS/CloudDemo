using Garage.Hosting;

var role = Environment.GetEnvironmentVariable("ROLE") ?? "web";

switch (role)
{
    case "web":
        await WebHost.RunAsync(args);
        break;

    case "worker":
        throw new NotImplementedException("The worker role arrives in Task 9.");

    default:
        throw new InvalidOperationException(
            $"ROLE must be 'web' or 'worker', not '{role}'.");
}

// Named so WebApplicationFactory<Program> can find the entry point from tests.
public partial class Program;
