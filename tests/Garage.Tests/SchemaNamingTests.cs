using Garage.Data;
using Microsoft.EntityFrameworkCore;

namespace Garage.Tests;

public class SchemaNamingTests
{
    /// <summary>
    /// Building the model needs no database: the connection string is
    /// never opened.
    /// </summary>
    private static GarageDbContext ModelOnly() =>
        new(new DbContextOptionsBuilder<GarageDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public void Every_table_is_snake_case_including_identitys_own()
    {
        using var db = ModelOnly();

        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .ToList();

        Assert.Contains("asp_net_users", tables);
        Assert.Contains("posts", tables);
        Assert.Contains("photos", tables);
        Assert.All(tables, table => Assert.Equal(table.ToLowerInvariant(), table));
    }

    [Fact]
    public void Every_index_is_snake_case_including_identitys_own()
    {
        using var db = ModelOnly();

        var indexes = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Select(index => index.GetDatabaseName())
            .OfType<string>()
            .ToList();

        Assert.Contains("email_index", indexes);
        Assert.All(indexes, index => Assert.Equal(index.ToLowerInvariant(), index));
    }
}
