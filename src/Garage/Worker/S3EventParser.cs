using System.Text.Json;
using Garage.Media;

namespace Garage.Worker;

public readonly record struct S3ObjectCreated(Guid PhotoId, string Key, long SizeBytes);

public static class S3EventParser
{
    /// <summary>
    /// Turns an S3 event notification body into something the worker can
    /// act on. Returns false — never throws — for anything it does not
    /// recognise, because a throw here means the message goes round the
    /// queue three times and lands on the DLQ for no reason.
    /// </summary>
    public static bool TryParse(string? body, out S3ObjectCreated created)
    {
        created = default;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("Records", out var records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
            {
                return false;
            }

            // Check every hop. S3 never sends these shapes, but a body that
            // throws here stops the worker each time SQS delivers it.
            var record = records[0];
            if (record.ValueKind != JsonValueKind.Object
                || !record.TryGetProperty("s3", out var s3)
                || s3.ValueKind != JsonValueKind.Object
                || !s3.TryGetProperty("object", out var objectNode)
                || objectNode.ValueKind != JsonValueKind.Object
                || !objectNode.TryGetProperty("key", out var keyNode)
                || keyNode.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var rawKey = keyNode.GetString()!;

            // S3 writes keys form-encoded: "+" for space, percent escapes
            // for the rest.
            var key = Uri.UnescapeDataString(rawKey.Replace("+", " "));

            var size = 0L;
            if (objectNode.TryGetProperty("size", out var sizeNode)
                && (sizeNode.ValueKind != JsonValueKind.Number
                    || !sizeNode.TryGetInt64(out size)))
            {
                return false;
            }

            if (!S3Keys.TryParsePhotoId(key, out var photoId))
            {
                // Still hand the key back: a rejected message on the DLQ
                // is much easier to explain when you can see what it was.
                created = new S3ObjectCreated(Guid.Empty, key, size);
                return false;
            }

            created = new S3ObjectCreated(photoId, key, size);
            return true;
        }
    }
}
