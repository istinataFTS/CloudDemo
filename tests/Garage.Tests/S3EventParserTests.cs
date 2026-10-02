using Garage.Worker;

namespace Garage.Tests;

public class S3EventParserTests
{
    private const string PhotoId = "22222222-2222-2222-2222-222222222222";
    private const string PostId = "11111111-1111-1111-1111-111111111111";

    private static string EventFor(string key, long size = 204800) => $$"""
    {
      "Records": [
        {
          "eventVersion": "2.1",
          "eventSource": "aws:s3",
          "awsRegion": "eu-central-1",
          "eventName": "ObjectCreated:Put",
          "s3": {
            "bucket": { "name": "garage-local-media" },
            "object": { "key": "{{key}}", "size": {{size}} }
          }
        }
      ]
    }
    """;

    [Fact]
    public void A_real_upload_yields_the_photo_id_and_size()
    {
        var body = EventFor($"originals/{PostId}/{PhotoId}.jpg");

        Assert.True(S3EventParser.TryParse(body, out var created));
        Assert.Equal(Guid.Parse(PhotoId), created.PhotoId);
        Assert.Equal($"originals/{PostId}/{PhotoId}.jpg", created.Key);
        Assert.Equal(204800, created.SizeBytes);
    }

    [Fact]
    public void The_test_event_S3_sends_when_a_notification_is_created_is_ignored()
    {
        // S3 posts this once, the moment you attach the notification.
        // A worker that throws on it crash-loops before it ever sees
        // a real photo.
        var body = """
        {"Service":"Amazon S3","Event":"s3:TestEvent","Bucket":"garage-local-media"}
        """;

        Assert.False(S3EventParser.TryParse(body, out _));
    }

    [Fact]
    public void Keys_are_url_decoded_before_they_are_read()
    {
        // S3 encodes spaces as "+" in event keys. A worker that skips
        // this looks for an object that does not exist and fails a
        // photo that uploaded perfectly.
        var body = EventFor("originals/some+folder/name%3Dvalue.jpg");

        Assert.False(S3EventParser.TryParse(body, out var created));
        Assert.Equal("originals/some folder/name=value.jpg", created.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("""{"Records":[]}""")]
    [InlineData("""{"Records":[{}]}""")]
    [InlineData("""{"Records":[{"s3":{"object":{"key":"originals/a/b.jpg","size":"big"}}}]}""")]
    public void Malformed_bodies_are_rejected_without_throwing(string body)
    {
        Assert.False(S3EventParser.TryParse(body, out _));
    }

    [Fact]
    public void A_derived_key_is_rejected_so_the_worker_cannot_feed_itself()
    {
        var body = EventFor($"derived/{PostId}/{PhotoId}_thumb.jpg");

        Assert.False(S3EventParser.TryParse(body, out _));
    }
}
