using Amazon.S3;
using Amazon.S3.Model;
using Garage.Configuration;
using Garage.Data;
using Garage.Data.Entities;
using Garage.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garage.Worker;

public sealed class PhotoProcessor(
    GarageDbContext db,
    IAmazonS3 s3,
    IOptions<AwsSettings> awsSettings,
    ILogger<PhotoProcessor> logger) : IPhotoProcessor
{
    /// <summary>Must match maxReceiveCount on the queue's redrive policy.</summary>
    private const int MaxReceiveCount = 3;

    private readonly string _bucket = awsSettings.Value.BucketName;

    public async Task ProcessAsync(
        S3ObjectCreated created, int receiveCount, CancellationToken cancellationToken)
    {
        var photo = await db.Photos
            .Include(p => p.Post!.Author)
            .FirstOrDefaultAsync(p => p.Id == created.PhotoId, cancellationToken);

        if (photo is null)
        {
            // An object exists in S3 with no row behind it — usually a post
            // deleted while its photo was in the queue. Re-delivering will
            // not bring the row back, so this returns normally and the
            // worker deletes the message.
            logger.LogWarning(
                "No photo row for {PhotoId} (key {Key}); ignoring", created.PhotoId, created.Key);
            return;
        }

        if (photo.Status == PhotoStatus.Ready)
        {
            // SQS delivers at least once, so this branch WILL run in
            // production. See Task 12.
            logger.LogInformation(
                "Photo {PhotoId} is already ready; skipping duplicate", photo.Id);
            return;
        }

        photo.Status = PhotoStatus.Processing;
        photo.Attempts += 1;
        photo.SizeBytes = created.SizeBytes;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            // The size comes from the S3 event, so this costs nothing. A
            // presigned PUT cannot cap the upload size; this is where the
            // cap lives instead, before the bytes are pulled into memory.
            if (created.SizeBytes > ImagePipeline.MaxOriginalBytes)
            {
                throw new InvalidOperationException(
                    $"{created.SizeBytes} bytes is too large; the limit is {ImagePipeline.MaxOriginalBytes}.");
            }

            using var original = await DownloadAsync(created.Key, cancellationToken);

            // The author's handle, so a photo copied off the site still
            // says whose car it is.
            var processed = ImagePipeline.Process(original, $"@{photo.Post!.Author!.UserName}");

            var thumbKey = S3Keys.Thumb(photo.PostId, photo.Id);
            var displayKey = S3Keys.Display(photo.PostId, photo.Id);

            await UploadAsync(thumbKey, processed.Thumb, cancellationToken);
            await UploadAsync(displayKey, processed.Display, cancellationToken);

            photo.S3KeyThumb = thumbKey;
            photo.S3KeyDisplay = displayKey;
            photo.Status = PhotoStatus.Ready;
            photo.ErrorMessage = null;
            photo.ProcessedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Photo {PhotoId} ready", photo.Id);
        }
        catch (Exception ex)
        {
            if (receiveCount >= MaxReceiveCount)
            {
                // Last chance before SQS moves it to the dead-letter
                // queue. Record why, so the post page can say something
                // better than "it did not work".
                photo.Status = PhotoStatus.Failed;
                photo.ErrorMessage = Truncate(ex.Message, 500);
            }
            else
            {
                photo.Status = PhotoStatus.Queued;
            }

            // CancellationToken.None: this write must land even if the
            // container is shutting down.
            await db.SaveChangesAsync(CancellationToken.None);

            // Rethrow either way, and never delete the message. SQS owns
            // both the retry and the move to the DLQ.
            throw;
        }
    }

    private async Task<Stream> DownloadAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(_bucket, key, cancellationToken);

        // Copy into memory: the pipeline seeks, and the S3 response
        // stream cannot.
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return buffer;
    }

    private async Task UploadAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream(bytes);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = body,
            ContentType = "image/jpeg"
        }, cancellationToken);
    }

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];
}
