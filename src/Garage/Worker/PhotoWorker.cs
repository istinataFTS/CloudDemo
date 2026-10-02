using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using Garage.Configuration;

namespace Garage.Worker;

public sealed class PhotoWorker(
    IAmazonSQS sqs,
    IOptions<AwsSettings> awsSettings,
    IServiceProvider services,
    ILogger<PhotoWorker> logger) : BackgroundService
{
    private const int BatchSize = 5;

    private readonly string _queueUrl = awsSettings.Value.QueueUrl;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_queueUrl))
        {
            throw new InvalidOperationException("AWS__QueueUrl is not set.");
        }

        logger.LogInformation("Worker polling {QueueUrl}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            List<Message> messages;

            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = BatchSize,

                    // No WaitTimeSeconds and no VisibilityTimeout here, on
                    // purpose. The queue owns both: long polling (20s) and
                    // a 60s visibility timeout in init.sh and in Terraform,
                    // 1s and 5s in the tests. A value here would silently
                    // override all three places.
                    MessageSystemAttributeNames = ["ApproximateReceiveCount"]
                }, stoppingToken);

                messages = response.Messages ?? [];
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not receive from SQS; backing off");
                await SafeDelayAsync(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            foreach (var message in messages)
            {
                // CancellationToken.None on purpose. SIGTERM means stop
                // *polling*; a message already in hand gets finished.
                // HostOptions.ShutdownTimeout (25s) is the real ceiling.
                await HandleAsync(message, CancellationToken.None);
            }
        }

        logger.LogInformation("Worker stopped polling.");
    }

    private async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var receiveCount = ReadReceiveCount(message);

        if (!S3EventParser.TryParse(message.Body, out var created))
        {
            // Nothing we can act on, and re-delivering will not change
            // that. Delete it rather than filling the DLQ with S3's own
            // test event.
            logger.LogInformation(
                "Discarding message {MessageId}: not a processable upload", message.MessageId);
            await DeleteAsync(message, cancellationToken);
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPhotoProcessor>();

            await processor.ProcessAsync(created, receiveCount, cancellationToken);

            await DeleteAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not delete. SQS makes it visible again after the
            // visibility timeout, and moves it to the DLQ once
            // maxReceiveCount is reached. That is the retry policy;
            // this code does not get a second one.
            logger.LogError(ex,
                "Photo {PhotoId} failed on receive #{Count}", created.PhotoId, receiveCount);
        }
    }

    private static int ReadReceiveCount(Message message)
    {
        if (message.Attributes is not null
            && message.Attributes.TryGetValue("ApproximateReceiveCount", out var raw)
            && int.TryParse(raw, out var count))
        {
            return count;
        }

        return 1;
    }

    private async Task DeleteAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
        }
        catch (Exception ex)
        {
            // The work is done; the delete is not. SQS will redeliver,
            // and the idempotency check in Task 12 is what makes that
            // harmless rather than a duplicate.
            logger.LogWarning(ex, "Could not delete message {MessageId}", message.MessageId);
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
