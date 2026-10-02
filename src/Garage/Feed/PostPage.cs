using Garage.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garage.Feed;

/// <summary>
/// One page of posts, newest first. The feed and a profile are the same
/// list with a different filter, so they share this.
/// </summary>
public sealed class PostPage
{
    public const int Size = 20;

    /// <summary>Far past any real page. Stops ?page=2147483647 overflowing the offset.</summary>
    private const int LastPage = 10_000;

    public required IReadOnlyList<Post> Posts { get; init; }
    public required int Number { get; init; }
    public required bool HasNext { get; init; }
    public bool HasPrevious => Number > 1;

    public static async Task<PostPage> LoadAsync(IQueryable<Post> posts, int page)
    {
        var number = Math.Clamp(page, 1, LastPage);

        var rows = await posts
            .Include(p => p.Author)
            // Ready photos, and the ones on their way — the card shows those
            // as "processing". Not a row still waiting for its upload (the
            // tab may have closed) and not one that failed.
            .Include(p => p.Photos
                .Where(photo => photo.Status == PhotoStatus.Queued
                             || photo.Status == PhotoStatus.Processing
                             || photo.Status == PhotoStatus.Ready)
                .OrderBy(photo => photo.CreatedAt))
            // Id breaks ties. Two posts in the same microsecond must not
            // swap places between page 1 and page 2, or one is shown twice
            // and the other never.
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((number - 1) * Size)
            // One more than a page: if it comes back, there is a next page.
            // One query instead of a separate COUNT.
            .Take(Size + 1)
            .AsNoTracking()
            .ToListAsync();

        return new PostPage
        {
            Posts = rows.Take(Size).ToList(),
            Number = number,
            HasNext = rows.Count > Size
        };
    }
}
