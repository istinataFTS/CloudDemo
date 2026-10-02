using Garage.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Garage.Tests;

/// <summary>
/// One throwaway Postgres for the whole test run, migrated once.
/// Real Postgres, not an in-memory provider: snake_case naming, uuid
/// columns and cascade deletes all behave differently in a fake.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("garage")
        .WithUsername("garage")
        .WithPassword("garage")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>A context straight onto the test database, for seeding and asserting.</summary>
    public GarageDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<GarageDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
