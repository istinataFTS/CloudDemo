namespace Garage.Data.Entities;

public enum PhotoStatus
{
    AwaitingUpload,
    Queued,
    Processing,
    Ready,
    Failed
}

public class Photo
{
    /// <summary>Also the S3 object name, so the worker can go from key to row.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// No AuthorId here on purpose: a photo's owner is its post's author.
    /// Copying it would give one fact two homes that can disagree.
    /// </summary>
    public Guid PostId { get; set; }
    public Post? Post { get; set; }

    public string S3KeyOriginal { get; set; } = "";
    public string? S3KeyThumb { get; set; }
    public string? S3KeyDisplay { get; set; }

    public string ContentType { get; set; } = "";
    public long? SizeBytes { get; set; }

    public PhotoStatus Status { get; set; } = PhotoStatus.AwaitingUpload;
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Display only. SQS decides whether to retry, via ApproximateReceiveCount.
    /// Never branch on this column.
    /// </summary>
    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
