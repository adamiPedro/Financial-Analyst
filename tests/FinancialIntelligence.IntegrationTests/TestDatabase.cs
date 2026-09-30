using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinancialIntelligence.IntegrationTests;

/// <summary>
/// A fresh, fully migrated Postgres database for one test, dropped afterwards.
/// Real Postgres rather than the in-memory provider, because the in-memory one
/// ignores unique indexes and foreign keys - the things these tests are about.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    // CI sets ConnectionStrings__Postgres; locally this matches docker-compose.
    private static readonly string ServerConnectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Port=5432;Database=findb;Username=findb;Password=findb";

    private readonly string _name = $"findb_test_{Guid.NewGuid():N}";

    public string ConnectionString => new NpgsqlConnectionStringBuilder(ServerConnectionString)
    {
        Database = _name
    }.ConnectionString;

    public FinancialIntelligenceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FinancialIntelligenceDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task AddPetrobrasAsync()
    {
        await using var db = CreateContext();
        db.Companies.Add(new Company(9512, Cnpj.Parse("33.000.167/0001-01"), "PETRÓLEO BRASILEIRO S.A. - PETROBRAS"));
        await db.SaveChangesAsync();
    }

    public async Task InitializeAsync()
    {
        await ExecuteOnServerAsync($"CREATE DATABASE {_name}");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)");
    }

    private static async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ServerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
