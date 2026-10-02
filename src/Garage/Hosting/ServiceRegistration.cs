using Amazon;
using Amazon.S3;
using Amazon.SQS;
using Garage.Configuration;
using Garage.Data;
using Garage.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garage.Hosting;

public static class ServiceRegistration
{
    /// <summary>
    /// Everything both roles need. The web host and the worker host each
    /// call this, so there is exactly one definition of how this
    /// application talks to Postgres and to AWS.
    /// </summary>
    public static IServiceCollection AddGarageCore(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default is not set.");

        services.AddDbContext<GarageDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.Configure<AwsSettings>(configuration.GetSection("AWS"));

        // No credentials passed anywhere. The default chain reads
        // AWS_ACCESS_KEY_ID locally and the ECS task role in AWS.
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<AwsSettings>>().Value;
            return new AmazonS3Client(
                PresignService.BuildConfig(settings, settings.ServiceUrl));
        });

        services.AddSingleton<IAmazonSQS>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<AwsSettings>>().Value;

            var config = new AmazonSQSConfig();
            if (string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }
            else
            {
                config.ServiceURL = settings.ServiceUrl;
                config.AuthenticationRegion = settings.Region;
            }

            return new AmazonSQSClient(config);
        });

        services.AddSingleton<PresignService>();

        return services;
    }
}
