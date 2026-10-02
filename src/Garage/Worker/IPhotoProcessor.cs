namespace Garage.Worker;

public interface IPhotoProcessor
{
    /// <summary>
    /// Handles one uploaded original. Throwing means "retry me" — the
    /// message is left on the queue and SQS re-delivers it after the
    /// visibility timeout.
    /// </summary>
    Task ProcessAsync(
        S3ObjectCreated created, int receiveCount, CancellationToken cancellationToken);
}

/// <summary>Stand-in until Task 11. Proves the loop without touching images.</summary>
public sealed class LoggingPhotoProcessor(ILogger<LoggingPhotoProcessor> logger)
    : IPhotoProcessor
{
    public Task ProcessAsync(
        S3ObjectCreated created, int receiveCount, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would process photo {PhotoId} from {Key} ({Bytes} bytes), receive #{Count}",
            created.PhotoId, created.Key, created.SizeBytes, receiveCount);

        return Task.CompletedTask;
    }
}
