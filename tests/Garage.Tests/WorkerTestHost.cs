using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Garage.Hosting;
using Garage.Worker;

namespace Garage.Tests;

public static class WorkerTestHost
{
    /// <summary>
    /// The real worker — the real poll loop, the real processor —
    /// pointed at the test's containers. Nothing about the production
    /// path is stubbed.
    /// </summary>
    public static IHost Build(string connectionString, AwsFixture aws)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["AWS:Region"] = AwsFixture.Region,
            ["AWS:BucketName"] = AwsFixture.BucketName,
            ["AWS:QueueUrl"] = aws.QueueUrl,
            ["AWS:ServiceUrl"] = aws.ServiceUrl,
            ["AWS:ForcePathStyle"] = "true"
        });

        builder.Services.AddGarageCore(builder.Configuration);
        builder.Services.AddScoped<IPhotoProcessor, PhotoProcessor>();
        builder.Services.AddHostedService<PhotoWorker>();

        return builder.Build();
    }
}
