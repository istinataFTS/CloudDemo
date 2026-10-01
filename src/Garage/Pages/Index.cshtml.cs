using Garage.Mockup;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Garage.Pages;

public class IndexModel : PageModel
{
    public IReadOnlyList<SamplePost> Posts { get; private set; } = [];

    public void OnGet() => Posts = SampleData.Feed;
}
