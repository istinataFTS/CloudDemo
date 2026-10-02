using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garage.Pages.Posts;

public class DetailModel(GarageDbContext db, UserManager<GarageUser> users) : PageModel
{
    public Post Post { get; private set; } = new();

    /// <summary>
    /// Decides what the page shows, nothing more. Hiding the delete button
    /// is a courtesy; the Delete page's own query is what actually stops
    /// someone deleting a post that is not theirs.
    /// </summary>
    public bool IsMine { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var post = await db.Posts
            .Include(p => p.Author)
            .Include(p => p.Photos
                .Where(photo => photo.Status == PhotoStatus.Queued
                             || photo.Status == PhotoStatus.Processing
                             || photo.Status == PhotoStatus.Ready)
                .OrderBy(photo => photo.CreatedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post is null)
        {
            return NotFound();
        }

        Post = post;
        IsMine = post.AuthorId == users.GetUserId(User);

        return Page();
    }
}
