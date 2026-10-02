using Garage.Configuration;
using Garage.Media;
using Microsoft.Extensions.Options;

namespace Garage.Tests;

public class PresignServiceTests
{
    public PresignServiceTests()
    {
        // Signing needs credentials to sign with; it never sends them.
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
    }

    [Fact]
    public void Locally_the_url_names_the_host_the_browser_can_reach_over_plain_http()
    {
        var presign = new PresignService(Options.Create(new AwsSettings
        {
            Region = "eu-central-1",
            BucketName = "garage-local-media",
            ServiceUrl = "http://localstack:4566",
            PublicServiceUrl = "http://localhost:4566",
            ForcePathStyle = true
        }));

        var url = presign.CreateUploadUrl("originals/a/b.jpg", "image/jpeg", TimeSpan.FromMinutes(15));

        Assert.StartsWith("http://localhost:4566/garage-local-media/originals/a/b.jpg?", url);
    }

    [Fact]
    public void In_aws_the_url_is_https_on_the_buckets_own_host()
    {
        var presign = new PresignService(Options.Create(new AwsSettings
        {
            Region = "eu-central-1",
            BucketName = "garage-dev-media"
        }));

        var url = presign.CreateDownloadUrl("derived/a/b_display.jpg", TimeSpan.FromHours(1));

        Assert.StartsWith("https://garage-dev-media.s3.eu-central-1.amazonaws.com/derived/a/b_display.jpg?", url);
    }
}
