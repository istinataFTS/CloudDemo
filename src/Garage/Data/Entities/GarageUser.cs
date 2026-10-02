using Microsoft.AspNetCore.Identity;

namespace Garage.Data.Entities;

/// <summary>
/// Identity's user plus the three columns a profile needs. UserName is
/// the handle in /u/{username}; Email is what you log in with.
/// </summary>
public class GarageUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Post> Posts { get; set; } = [];
}
