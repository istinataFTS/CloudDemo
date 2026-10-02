namespace Garage.Data.Entities;

/// <summary>
/// A thought, or a car — a car is a post with a photo. Never edited,
/// so there is no UpdatedAt.
/// </summary>
public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AuthorId { get; set; } = "";
    public GarageUser? Author { get; set; }

    public string Body { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Photo> Photos { get; set; } = [];
}
