using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Testcontainers.LocalStack;

namespace Garage.Tests;

/// <summary>
/// LocalStack with the same objects localstack/init.sh creates, built
/// through the SDK so the test owns its own environment.
/// </summary>
public sealed class AwsFixture : IAsyncLifetime
{
    private readonly LocalStackContainer _container = new LocalStackBuilder("localstack/localstack:3")
        .WithEnvironment("SERVICES", "s3,sqs")
        .Build();

    public const string BucketName = "garage-test-media";
    public const string Region = "us-east-1";

    public string ServiceUrl { get; private set; } = "";
    public string QueueUrl { get; private set; } = "";
    public string DlqUrl { get; private set; } = "";

    public IAmazonS3 S3 { get; private set; } = null!;
    public IAmazonSQS Sqs { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ServiceUrl = _container.GetConnectionString();

        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");

        S3 = new AmazonS3Client(new AmazonS3Config
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = Region,
            ForcePathStyle = true
        });

        Sqs = new AmazonSQSClient(new AmazonSQSConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = Region
        });

        await S3.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });

        DlqUrl = (await Sqs.CreateQueueAsync("garage-test-dlq")).QueueUrl;
        var dlqArn = await ArnOf(DlqUrl);

        var redrive = JsonSerializer.Serialize(new
        {
            deadLetterTargetArn = dlqArn,
            maxReceiveCount = "3"
        });

        QueueUrl = (await Sqs.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = "garage-test-photos",
            Attributes = new Dictionary<string, string>
            {
                ["VisibilityTimeout"] = "5",
                ["ReceiveMessageWaitTimeSeconds"] = "1",
                ["RedrivePolicy"] = redrive
            }
        })).QueueUrl;

        var queueArn = await ArnOf(QueueUrl);

        await Sqs.SetQueueAttributesAsync(QueueUrl, new Dictionary<string, string>
        {
            ["Policy"] = JsonSerializer.Serialize(new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Effect = "Allow",
                        Principal = new { Service = "s3.amazonaws.com" },
                        Action = "sqs:SendMessage",
                        Resource = queueArn
                    }
                }
            })
        });

        await S3.PutBucketNotificationAsync(new PutBucketNotificationRequest
        {
            BucketName = BucketName,
            QueueConfigurations =
            [
                new QueueConfiguration
                {
                    Queue = queueArn,
                    Events = [EventType.ObjectCreatedAll],
                    Filter = new Filter
                    {
                        S3KeyFilter = new S3KeyFilter
                        {
                            FilterRules =
                            [
                                new FilterRule { Name = "prefix", Value = "originals/" }
                            ]
                        }
                    }
                }
            ]
        });
    }

    private async Task<string> ArnOf(string queueUrl)
    {
        var attributes = await Sqs.GetQueueAttributesAsync(queueUrl, ["QueueArn"]);
        return attributes.QueueARN;
    }

    public async Task DisposeAsync()
    {
        S3?.Dispose();
        Sqs?.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("aws")]
public class AwsCollection : ICollectionFixture<AwsFixture>, ICollectionFixture<PostgresFixture>;
