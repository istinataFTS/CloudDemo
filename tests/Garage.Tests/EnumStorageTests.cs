using Garage.Data;
using Garage.Data.Entities;

namespace Garage.Tests;

public class EnumStorageTests
{
    [Theory]
    [InlineData(PhotoStatus.AwaitingUpload, "awaiting_upload")]
    [InlineData(PhotoStatus.Ready, "ready")]
    [InlineData(PhotoStatus.Failed, "failed")]
    public void Photo_status_is_stored_as_snake_case_text(PhotoStatus status, string expected)
    {
        var converter = new SnakeCaseEnumConverter<PhotoStatus>();

        Assert.Equal(expected, converter.ConvertToProviderExpression.Compile()(status));
    }

    [Fact]
    public void Text_round_trips_back_to_the_same_member()
    {
        var converter = new SnakeCaseEnumConverter<PhotoStatus>();

        var stored = converter.ConvertToProviderExpression.Compile()(PhotoStatus.AwaitingUpload);
        var loaded = converter.ConvertFromProviderExpression.Compile()(stored);

        Assert.Equal(PhotoStatus.AwaitingUpload, loaded);
    }

    [Fact]
    public void A_value_the_enum_does_not_have_is_an_error_not_a_default()
    {
        // Silently returning the zero member would turn a corrupt row
        // into a plausible-looking one, which is far worse than a throw.
        var converter = new SnakeCaseEnumConverter<PhotoStatus>();

        Assert.Throws<InvalidOperationException>(
            () => converter.ConvertFromProviderExpression.Compile()("shipped"));
    }
}
