using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Garage.Configuration;
using Microsoft.Extensions.Options;

namespace Garage.Media;

public sealed class PresignService
{
    private readonly IAmazonS3 _browserFacingS3;
    private readonly AwsSettings _settings;
    private readonly Protocol _protocol;

    public PresignService(IOptions<AwsSettings> settings)
    {
        _settings = settings.Value;

        var browserFacingUrl = _settings.PublicServiceUrl ?? _settings.ServiceUrl;

        // A second client, pointed at the host the browser can resolve.
        // The signature is computed from the URL, so this cannot be a
        // string replacement after the fact.
        _browserFacingS3 = new AmazonS3Client(BuildConfig(_settings, browserFacingUrl));

        // The SDK signs https:// URLs unless told otherwise — even for a
        // client configured with an http:// service URL. LocalStack speaks
        // plain HTTP, so an https:// link to it fails before it starts.
        _protocol = browserFacingUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? Protocol.HTTP
            : Protocol.HTTPS;
    }

    public static AmazonS3Config BuildConfig(AwsSettings settings, string? serviceUrl)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = settings.ForcePathStyle
        };

        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
        }
        else
        {
            config.ServiceURL = serviceUrl;
            config.AuthenticationRegion = settings.Region;
        }

        return config;
    }

    /// <summary>
    /// Signs a PUT. The content type is part of the signature, so a URL
    /// issued for a JPEG cannot be used to upload an HTML page.
    /// </summary>
    public string CreateUploadUrl(string key, string contentType, TimeSpan lifetime)
        => _browserFacingS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Protocol = _protocol,
            Expires = DateTime.UtcNow.Add(lifetime)
        });

    public string CreateDownloadUrl(string key, TimeSpan lifetime)
        => _browserFacingS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = DateTime.UtcNow.Add(lifetime)
        });
}
