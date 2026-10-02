using System.ComponentModel.DataAnnotations;
using Garage.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages.Account;

public class RegisterModel(
    UserManager<GarageUser> users,
    SignInManager<GarageUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        // Checked here for a friendly message. Identity checks the same
        // characters again in AllowedUserNameCharacters.
        [Required, RegularExpression("^[a-z0-9_]{3,30}$",
            ErrorMessage = "3 to 30 characters: lowercase letters, digits and underscore.")]
        public string Username { get; set; } = "";

        [Required, StringLength(60)]
        public string DisplayName { get; set; } = "";

        [StringLength(300)]
        public string? Bio { get; set; }

        [Required, DataType(DataType.Password), StringLength(200, MinimumLength = 12)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new GarageUser
        {
            UserName = Input.Username,
            Email = Input.Email,
            DisplayName = Input.DisplayName.Trim(),
            Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim()
        };

        var result = await users.CreateAsync(user, Input.Password);

        if (!result.Succeeded)
        {
            // "Username 'ivan' is already taken" and friends. Usernames
            // are public anyway — they are in every profile URL.
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        return LocalRedirect("/");
    }
}
