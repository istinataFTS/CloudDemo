using Garage.Feed;

namespace Garage.Tests;

public class PostDisplayTests
{
    [Theory]
    [InlineData("Ivan Petrov", "IP")]
    [InlineData("ivan", "I")]
    [InlineData("  Ivan   Petrov  Jr ", "IP")]
    [InlineData("😀 Ivan", "😀I")]
    public void Initials_are_the_first_letter_of_the_first_two_words(string name, string expected)
    {
        Assert.Equal(expected, PostDisplay.Initials(name));
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(12 * 60, "12m")]
    [InlineData(5 * 3600, "5h")]
    [InlineData(3 * 86400, "3d")]
    [InlineData(45 * 86400, "17 Aug 2026")]
    public void Time_is_shown_relative_until_it_is_a_month_old(int secondsAgo, string expected)
    {
        Assert.Equal(expected, PostDisplay.Relative(Now.AddSeconds(-secondsAgo), Now));
    }
}
