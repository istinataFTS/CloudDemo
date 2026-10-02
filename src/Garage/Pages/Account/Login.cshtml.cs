using System.ComponentModel.DataAnnotations;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages.Account;

public class LoginModel(
    UserManager<GarageUser> users,
    SignInManager<GarageUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Identity signs in by username; people log in by email. Look the
        // user up first, then hand the user object to the sign-in manager.
        var user = await users.FindByEmailAsync(Input.Email);

        var signedIn = user is not null
            && (await signInManager.PasswordSignInAsync(
                    user, Input.Password, isPersistent: true, lockoutOnFailure: true))
                .Succeeded;

        if (!signedIn)
        {
            // Deliberately vague. "No such email" tells an attacker which
            // addresses are worth guessing passwords for, and "locked out"
            // tells them the guessing is working.
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        // Only ever a path on this site. A crafted ?ReturnUrl=https://evil.example
        // must not bounce a freshly logged-in user somewhere else.
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
