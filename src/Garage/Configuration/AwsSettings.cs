namespace Garage.Configuration;

/// <summary>Bound from the AWS section: AWS__Region, AWS__BucketName and so on.</summary>
public sealed class AwsSettings
{
    public string Region { get; set; } = "eu-central-1";
    public string BucketName { get; set; } = "";
    public string QueueUrl { get; set; } = "";

    /// <summary>Set to LocalStack locally. Null in AWS, where the SDK finds S3 itself.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// The host a browser can reach. LocalStack is "localstack:4566" from
    /// another container and "localhost:4566" from the browser; a URL
    /// signed for the wrong one is signed correctly and still useless.
    /// </summary>
    public string? PublicServiceUrl { get; set; }

    /// <summary>LocalStack's S3 needs path-style addressing. Real S3 does not.</summary>
    public bool ForcePathStyle { get; set; }
}
