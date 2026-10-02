using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garage.Pages.Posts;

public class DeleteModel(GarageDbContext db, UserManager<GarageUser> users) : PageModel
{
    public IActionResult OnGet(Guid id) => LocalRedirect($"/posts/{id}");

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var me = users.GetUserId(User);

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

        return LocalRedirect($"/u/{User.Identity!.Name}");
    }
}
