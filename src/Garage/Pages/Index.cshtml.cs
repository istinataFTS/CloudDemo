using Garage.Data;
using Garage.Feed;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages;

public class IndexModel(GarageDbContext db) : PageModel
{
    public PostPage Feed { get; private set; } = null!;

    // [FromQuery] is not decoration. In Razor Pages "page" is also a route
    // value — the page's own path, "/Index" — and route values win over
    // the query string. Without it, ?page=2 is silently ignored.
    public async Task OnGetAsync([FromQuery] int page = 1)
    {
        // Everyone's posts. A profile is this same call with a Where.
        Feed = await PostPage.LoadAsync(db.Posts, page);
    }
}
