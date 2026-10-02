namespace Garage.Tests;

public static class TestForms
{
    /// <summary>
    /// Builds a form body carrying the antiforgery token from the page it
    /// was rendered on. Razor Pages rejects a POST without it, and so does
    /// a real browser. The matching cookie travels by itself: clients from
    /// WebApplicationFactory keep cookies, like a browser does.
    /// </summary>
    public static async Task<FormUrlEncodedContent> FromAsync(
        HttpResponseMessage page, Dictionary<string, string> fields)
    {
        var html = await page.Content.ReadAsStringAsync();

        fields["__RequestVerificationToken"] = ExtractToken(html);

        return new FormUrlEncodedContent(fields);
    }

    public static string ExtractToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var nameIndex = html.IndexOf(marker, StringComparison.Ordinal);
        if (nameIndex < 0)
        {
            throw new InvalidOperationException("No antiforgery token on the page.");
        }

        const string valueMarker = "value=\"";
        var valueIndex = html.IndexOf(valueMarker, nameIndex, StringComparison.Ordinal)
                         + valueMarker.Length;
        var end = html.IndexOf('"', valueIndex);

        return html[valueIndex..end];
    }
}
