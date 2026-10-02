using Garage.Data.Entities;
using Garage.Media;

namespace Garage.Tests;

[Collection("aws")]
public class DeadLetterTests(PostgresFixture postgres, AwsFixture aws)
{
    private readonly PhotoTestHelpers _helpers = new(postgres, aws);

    [Fact]
    public async Task A_file_that_can_never_decode_ends_up_failed_and_on_the_dlq()
    {
        var (_, photoId, key) = await _helpers.SeedAwaitingPhotoAsync();

        // Uploaded with an image content type and a body that is not an
        // image. The pipeline throws every single time.
        await _helpers.PutGarbageAsync(key);

        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();

        // Visibility timeout is 5s in the fixture, maxReceiveCount is 3.
        var failed = await _helpers.WaitForStatusAsync(
            photoId, PhotoStatus.Failed, TimeSpan.FromSeconds(90));

        // SQS moves the message on the receive *after* the third, so the
        // worker has to keep polling until it lands.
        var deadLetters = await _helpers.WaitForDeadLetterAsync(key, TimeSpan.FromSeconds(30));

        await host.StopAsync();

        // Not "exactly 3". SQS counted three receives, but a receive can
        // go to a consumer that dies before doing anything — here, the
        // previous test's stopped worker; in ECS, a task killed mid-poll.
        // Our column counts only the tries that actually ran. That gap is
        // the whole reason the retry decision reads SQS's count, not ours.
        Assert.InRange(failed.Attempts, 1, 3);
        Assert.False(string.IsNullOrWhiteSpace(failed.ErrorMessage));

        // SQS moved it, not us. The worker never calls SendMessage on
        // the DLQ and never counts retries itself.
        Assert.True(deadLetters >= 1,
            "The poison message should have been redriven to the dead-letter queue.");
    }

    [Fact]
    public async Task An_original_over_the_size_limit_is_refused()
    {
        var (_, photoId, key) = await _helpers.SeedAwaitingPhotoAsync();

        // One byte over. Zeros are not an image either, but the size
        // check comes first — the error message proves which one fired.
        await _helpers.PutAsync(key, new byte[ImagePipeline.MaxOriginalBytes + 1]);

        using var host = WorkerTestHost.Build(postgres.ConnectionString, aws);
        await host.StartAsync();

        var failed = await _helpers.WaitForStatusAsync(
            photoId, PhotoStatus.Failed, TimeSpan.FromSeconds(90));

        await host.StopAsync();

        Assert.Contains("too large", failed.ErrorMessage);
    }
}
