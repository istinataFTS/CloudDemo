using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garage.Data;

/// <summary>
/// Stores enums as the snake_case text the spec's tables call for:
/// PhotoStatus.AwaitingUpload becomes "awaiting_upload" in the column.
/// Text is readable in psql; an int is not.
/// </summary>
public sealed class SnakeCaseEnumConverter<T> : ValueConverter<T, string>
    where T : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(value => SnakeCase.From(value.ToString()),
               text => FromSnakeCase(text))
    {
    }

    private static T FromSnakeCase(string text)
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (SnakeCase.From(value.ToString()) == text)
            {
                return value;
            }
        }
        throw new InvalidOperationException(
            $"'{text}' is not a valid {typeof(T).Name}.");
    }
}
