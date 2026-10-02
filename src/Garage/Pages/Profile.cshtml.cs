using Garage.Mockup;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages;

public class ProfileModel : PageModel
{
    public SampleProfile Profile { get; private set; } = SampleData.Me;
    public IReadOnlyList<SamplePost> Posts { get; private set; } = [];

    public void OnGet(string? username)
    {
        // An unknown username falls back to the signed-in user, because
        // there is no sign-in yet. Task 6 turns this into a real lookup
        // and a 404.
        var who = string.IsNullOrWhiteSpace(username) ? SampleData.Me.Username : username;

        Profile = SampleData.ProfileFor(who);
        Posts = SampleData.PostsBy(Profile.Username);
    }
}
