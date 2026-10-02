using System.Text.Json;
using Amazon.S3.Model;
using Amazon.SQS.Model;
using Garage.Data;
using Garage.Data.Entities;
using Garage.Media;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Garage.Tests;

/// <summary>
/// Seeding, uploading and waiting, shared by every test that runs the
/// worker against the real containers.
/// </summary>
public sealed class PhotoTestHelpers(PostgresFixture postgres, AwsFixture aws)
{
    public GarageDbContext NewDbContext() => postgres.NewDbContext();

    /// <summary>
    /// A user, a post and a photo row waiting for its upload — what the
    /// presign endpoint leaves behind, without going through the web app.
    /// </summary>
    public async Task<(Guid PostId, Guid PhotoId, string Key)> SeedAwaitingPhotoAsync()
    {
        await using var db = NewDbContext();

        var username = "w" + Guid.NewGuid().ToString("N")[..12];
        var author = new GarageUser
        {
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@example.com",
            NormalizedEmail = $"{username}@EXAMPLE.COM",
            DisplayName = "Worker test"
        };
        var post = new Post { Author = author, Body = "Photo incoming." };
        var photo = new Photo
        {
            Post = post,
            ContentType = "image/jpeg",
            Status = PhotoStatus.AwaitingUpload
        };
        photo.S3KeyOriginal = S3Keys.Original(post.Id, photo.Id, "jpg");

        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        return (post.Id, photo.Id, photo.S3KeyOriginal);
    }

    public static byte[] JpegBytes(int width = 2400, int height = 1800)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(context => context.BackgroundColor(Color.DarkSlateGray));

        using var buffer = new MemoryStream();
        image.SaveAsJpeg(buffer, new JpegEncoder { Quality = 90 });

        return buffer.ToArray();
    }

    public Task PutOriginalAsync(string key) => PutAsync(key, JpegBytes());

    /// <summary>An object that will never decode, no matter how often it is retried.</summary>
    public Task PutGarbageAsync(string key) => PutAsync(key, "this is definitely not a jpeg"u8.ToArray());

    public async Task PutAsync(string key, byte[] bytes)
    {
        using var body = new MemoryStream(bytes);

        await aws.S3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = AwsFixture.BucketName,
            Key = key,
            InputStream = body,
            ContentType = "image/jpeg"
        });
    }

    /// <summary>The shape of the JSON S3 puts on the queue, for replaying a delivery by hand.</summary>
    public static string EventBodyFor(string key, long size) => JsonSerializer.Serialize(new
    {
        Records = new[]
        {
            new
            {
                eventName = "ObjectCreated:Put",
                s3 = new
                {
                    bucket = new { name = AwsFixture.BucketName },
                    // "object" is a C# keyword; the @ lets it be a name.
                    @object = new { key, size }
                }
            }
        }
    });

    public async Task<Photo> WaitForStatusAsync(
        Guid photoId, PhotoStatus expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var db = NewDbContext();
            var photo = await db.Photos.AsNoTracking().SingleAsync(p => p.Id == photoId);

            if (photo.Status == expected)
            {
                return photo;
            }

            await Task.Delay(500);
        }

        await using var final = NewDbContext();
        var last = await final.Photos.AsNoTracking().SingleAsync(p => p.Id == photoId);

        throw new TimeoutException(
            $"Photo {photoId} was {last.Status} after {timeout.TotalSeconds}s, " +
            $"expected {expected}. Error: {last.ErrorMessage ?? "none"}");
    }

    public async Task<Image> LoadDerivedAsync(string key)
    {
        using var response = await aws.S3.GetObjectAsync(AwsFixture.BucketName, key);

        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer);
        buffer.Position = 0;

        return Image.Load(buffer);
    }

    /// <summary>Every object under a prefix, for "did it get deleted" checks.</summary>
    public async Task<List<string>> ListKeysAsync(string prefix)
    {
        var response = await aws.S3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = AwsFixture.BucketName,
            Prefix = prefix
        });

        // The SDK leaves S3Objects null, not empty, when nothing matches.
        return response.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }

    /// <summary>
    /// How many messages on the dead-letter queue are about this key.
    /// Counting only this test's key keeps tests that share the fixture's
    /// one DLQ from seeing each other's leftovers.
    /// </summary>
    public async Task<int> DeadLettersMentioningAsync(string key)
    {
        var response = await aws.Sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = aws.DlqUrl,
            MaxNumberOfMessages = 10,

            // Zero: look without taking. The messages stay visible.
            VisibilityTimeout = 0
        });

        return (response.Messages ?? []).Count(m => m.Body.Contains(key));
    }

    /// <summary>Polls the DLQ until a message about this key arrives, or gives up.</summary>
    public async Task<int> WaitForDeadLetterAsync(string key, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var count = await DeadLettersMentioningAsync(key);
            if (count > 0)
            {
                return count;
            }

            await Task.Delay(1000);
        }

        return 0;
    }
}
