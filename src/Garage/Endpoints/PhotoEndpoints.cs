using Garage.Data;
using Garage.Data.Entities;
using Garage.Media;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Garage.Endpoints;

public static class PhotoEndpoints
{
    public sealed record PresignRequest(string ContentType);
    public sealed record PresignResponse(Guid PhotoId, string UploadUrl);
    public sealed record PhotoStatusRow(
        Guid Id, string Status, string? ErrorMessage, int Attempts);

    /// <summary>
    /// A presigned PUT cannot cap the file size, so cap the count instead:
    /// one stranger cannot sign a thousand uploads onto one post.
    /// </summary>
    public const int MaxPhotosPerPost = 10;

    private static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromMinutes(15);

    public static void MapPhotoEndpoints(this IEndpointRouteBuilder routes)
    {
        // No RequireAuthorization() here: the fallback policy from Task 3
        // already closes every endpoint that does not say otherwise.
        //
        // Cookie auth plus JSON bodies, no antiforgery token. The cookie is
        // SameSite=Lax (Task 3), so a POST that starts on another site
        // arrives without it, and this application sends no CORS headers.
        routes.MapPost("/posts/{postId:guid}/photos", CreateUploadUrlAsync);
        routes.MapPost("/photos/{id:guid}/uploaded", MarkUploadedAsync);
        routes.MapGet("/posts/{postId:guid}/photos/status", GetStatusAsync);
    }

    private static async Task<IResult> CreateUploadUrlAsync(
        Guid postId,
        [FromBody] PresignRequest request,
        ClaimsPrincipal user,
        UserManager<GarageUser> users,
        GarageDbContext db,
        PresignService presign)
    {
        if (!ContentTypes.TryGetExtension(request.ContentType, out var extension))
        {
            return Results.BadRequest(new
            {
                error = "Only JPEG, PNG and WebP images are accepted."
            });
        }

        // Ownership in the query, exactly as on the delete page.
        var me = users.GetUserId(user);
        if (!await db.Posts.AnyAsync(p => p.Id == postId && p.AuthorId == me))
        {
            return Results.NotFound();
        }

        if (await db.Photos.CountAsync(p => p.PostId == postId) >= MaxPhotosPerPost)
        {
            return Results.BadRequest(new
            {
                error = $"A post takes at most {MaxPhotosPerPost} photos."
            });
        }

        var photo = new Photo
        {
            PostId = postId,
            ContentType = request.ContentType,
            Status = PhotoStatus.AwaitingUpload
        };

        photo.S3KeyOriginal = S3Keys.Original(postId, photo.Id, extension);

        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        var url = presign.CreateUploadUrl(
            photo.S3KeyOriginal, request.ContentType, UploadUrlLifetime);

        return Results.Ok(new PresignResponse(photo.Id, url));
    }

    private static async Task<IResult> MarkUploadedAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<GarageUser> users,
        GarageDbContext db)
    {
        var me = users.GetUserId(user);
        if (!await db.Photos.AnyAsync(p => p.Id == id && p.Post!.AuthorId == me))
        {
            return Results.NotFound();
        }

        // Best effort. The worker never waits for this call — it exists so
        // the post page can tell "the tab was closed mid-upload" apart
        // from "uploaded, waiting for the worker".
        //
        // One UPDATE ... WHERE status = 'awaiting_upload', not a read and
        // then a write. The worker may finish between a read and a write,
        // and a slow tab must never drag a ready photo back to queued.
        await db.Photos
            .Where(p => p.Id == id && p.Status == PhotoStatus.AwaitingUpload)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, PhotoStatus.Queued));

        return Results.NoContent();
    }

    private static async Task<IResult> GetStatusAsync(
        Guid postId,
        ClaimsPrincipal user,
        UserManager<GarageUser> users,
        GarageDbContext db)
    {
        var me = users.GetUserId(user);
        if (!await db.Posts.AnyAsync(p => p.Id == postId && p.AuthorId == me))
        {
            return Results.NotFound();
        }

        var rows = await db.Photos
            .Where(p => p.PostId == postId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PhotoStatusRow(
                p.Id,
                p.Status.ToString(),
                p.ErrorMessage,
                p.Attempts))
            .AsNoTracking()
            .ToListAsync();

        return Results.Ok(rows);
    }
}
