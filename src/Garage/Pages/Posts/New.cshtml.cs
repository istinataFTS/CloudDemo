using System.ComponentModel.DataAnnotations;
using Garage.Data;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages.Posts;

public class NewModel(GarageDbContext db, UserManager<GarageUser> users) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// Only the body. There is no AuthorId here on purpose: anything in
    /// this class can be set by whoever sends the form, and the author is
    /// not theirs to choose.
    /// </summary>
    public class InputModel
    {
        [Required, StringLength(2000)]
        public string Body { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var post = new Post
        {
            // From the login cookie, never from the form.
            AuthorId = users.GetUserId(User)!,
            Body = Input.Body.Trim()
        };

        db.Posts.Add(post);
        await db.SaveChangesAsync();

        return LocalRedirect($"/posts/{post.Id}");
    }
}
