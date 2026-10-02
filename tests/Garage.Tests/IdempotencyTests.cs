using Garage.Data.Entities;
using Garage.Media;

namespace Garage.Tests;

[Collection("aws")]
public class IdempotencyTests(PostgresFixture postgres, AwsFixture aws)
{
    private readonly PhotoTestHelpers _helpers = new(postgres, aws);

    [Fact]
    public async Task The_same_message_delivered_twice_processes_once()
    {
        var (_, photoId, key) = await _helpers.SeedAwaitingPhotoAsync();
        await _helpers.PutOriginalAsync(key);

        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();

        var first = await _helpers.WaitForStatusAsync(
            photoId, PhotoStatus.Ready, TimeSpan.FromSeconds(60));

        // SQS guarantees at-least-once delivery, so this is not a
        // contrived scenario: a visibility timeout that expires a few
        // milliseconds before the delete lands produces exactly this.
        await aws.Sqs.SendMessageAsync(
            aws.QueueUrl,
            PhotoTestHelpers.EventBodyFor(key, 123456));

        // Long enough for the worker to receive, look, and decide.
        await Task.Delay(TimeSpan.FromSeconds(10));

        await host.StopAsync();

        await using var db = _helpers.NewDbContext();
        var second = await db.Photos.FindAsync(photoId);

        Assert.NotNull(second);
        Assert.Equal(PhotoStatus.Ready, second!.Status);

        // The guard is what these two assertions are really about.
        // Without it the row would be processed again: attempts 2,
        // and a fresh ProcessedAt.
        Assert.Equal(first.Attempts, second.Attempts);
        Assert.Equal(first.ProcessedAt, second.ProcessedAt);
    }

    [Fact]
    public async Task A_message_for_a_photo_that_no_longer_exists_is_discarded_quietly()
    {
        // A post deleted while its photo was in the queue. Retrying can
        // never help, so the worker must delete the message rather than
        // fill the DLQ with it.
        var key = S3Keys.Original(Guid.NewGuid(), Guid.NewGuid(), "jpg");
        await _helpers.PutOriginalAsync(key);

        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();

        // Three visibility timeouts and then some: long enough for the
        // message to have reached the DLQ, had it been retried.
        await Task.Delay(TimeSpan.FromSeconds(20));

        await host.StopAsync();

        Assert.Equal(0, await _helpers.DeadLettersMentioningAsync(key));
    }
}
