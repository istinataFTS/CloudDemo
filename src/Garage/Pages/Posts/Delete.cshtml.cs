using Amazon.S3;
using Amazon.S3.Model;
using Garage.Configuration;
using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garage.Pages.Posts;

public class DeleteModel(
    GarageDbContext db,
    UserManager<GarageUser> users,
    IAmazonS3 s3,
    IOptions<AwsSettings> aws,
    ILogger<DeleteModel> logger) : PageModel
{
    public IActionResult OnGet(Guid id) => LocalRedirect($"/posts/{id}");

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var me = users.GetUserId(User);

        // Read the photos' keys first: once the rows are gone, nothing
        // remembers which objects in S3 belonged to this post. Same
        // ownership condition as the delete itself.
        var photos = await db.Photos
            .Where(photo => photo.PostId == id && photo.Post!.AuthorId == me)
            .Select(photo => new { photo.S3KeyOriginal, photo.S3KeyThumb, photo.S3KeyDisplay })
            .ToListAsync();

        // The ownership check IS the query. "Delete the post with this id
        // whose author is me" — someone else's post and a post that does
        // not exist are the same thing to it, so there is no second step
        // to forget, and the answer does not tell a stranger which ids
        // are real. The database deletes the photo rows by cascade.
        var deleted = await db.Posts
            .Where(p => p.Id == id && p.AuthorId == me)
            .ExecuteDeleteAsync();

        if (deleted == 0)
        {
            return NotFound();
        }

        var keys = photos
            .SelectMany(photo => new[] { photo.S3KeyOriginal, photo.S3KeyThumb, photo.S3KeyDisplay })
            .OfType<string>()
            .ToList();

        await DeleteObjectsAsync(keys);

        return LocalRedirect($"/u/{User.Identity!.Name}");
    }

    /// <summary>
    /// Best effort. The post is already gone from every page; if S3 is
    /// unreachable, what is left is a private object nothing links to.
    /// Failing the request now would only make the user think the post
    /// was not deleted.
    /// </summary>
    private async Task DeleteObjectsAsync(List<string> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        try
        {
            // One request for all of them, rather than one per object.
            var response = await s3.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = aws.Value.BucketName,
                Objects = keys.Select(key => new KeyVersion { Key = key }).ToList()
            });

            // DeleteObjects reports a refused key in the response rather
            // than by throwing.
            if (response.DeleteErrors is { Count: > 0 } errors)
            {
                logger.LogWarning("S3 kept {Count} objects of a deleted post: {Keys}",
                    errors.Count, string.Join(", ", errors.Select(e => e.Key)));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete {Count} objects of a deleted post", keys.Count);
        }
    }
}
