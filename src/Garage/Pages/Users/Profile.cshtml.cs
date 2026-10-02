using Garage.Data;
using Garage.Data.Entities;
using Garage.Feed;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Garage.Pages.Users;

public class ProfileModel(GarageDbContext db, UserManager<GarageUser> users) : PageModel
{
    public GarageUser Owner { get; private set; } = new();
    public PostPage Posts { get; private set; } = null!;
    public int PostCount { get; private set; }

    // [FromQuery] for the same reason as on the feed: "page" is a route
    // value in Razor Pages.
    public async Task<IActionResult> OnGetAsync(string username, [FromQuery] int page = 1)
    {
        // FindByNameAsync compares the normalized name, so /u/Ivan and
        // /u/ivan are the same person.
        var owner = await users.FindByNameAsync(username);
        if (owner is null)
        {
            return NotFound();
        }

        Owner = owner;

        var theirs = db.Posts.Where(p => p.AuthorId == owner.Id);
        Posts = await PostPage.LoadAsync(theirs, page);
        PostCount = await theirs.CountAsync();

        return Page();
    }
}
