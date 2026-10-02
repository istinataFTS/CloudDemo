using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garage.Pages.Posts;

public class DetailModel(GarageDbContext db) : PageModel
{
    public Post Post { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var post = await db.Posts
            .Include(p => p.Author)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post is null)
        {
            return NotFound();
        }

        Post = post;
        return Page();
    }
}
