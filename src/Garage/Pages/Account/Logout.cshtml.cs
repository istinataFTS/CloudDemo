using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages.Account;

public class LogoutModel(SignInManager<GarageUser> signInManager) : PageModel
{
    // POST only. A GET that logs you out can be triggered by any page on
    // the internet with <img src="https://garage.example/logout">.
    public IActionResult OnGet() => LocalRedirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }
}
