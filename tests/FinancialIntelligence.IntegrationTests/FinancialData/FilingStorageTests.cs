using FinancialIntelligence.Domain.FinancialData;
using Microsoft.EntityFrameworkCore;

namespace FinancialIntelligence.IntegrationTests.FinancialData;

public sealed class FilingStorageTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Retrieved = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private static Filing Petrobras2024() =>
        new(9512, FilingForm.Dfp, new DateOnly(2024, 12, 31), 1, AccountingBasis.Consolidated,
            StatementLayout.Industrial, "dfp_cia_aberta_2024.zip", Retrieved);

    [Fact]
    public async Task A_filing_and_its_facts_are_saved_together()
    {
        await _database.AddPetrobrasAsync();
        await using (var db = _database.CreateContext())
        {
            var filing = Petrobras2024();
            db.Filings.Add(filing);
            db.FinancialFacts.Add(new FinancialFact(filing, "1", null, new DateOnly(2024, 12, 31), 1_124_797_000_000m));
            await db.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var stored = await read.Filings.SingleAsync();
        var fact = await read.FinancialFacts.SingleAsync();
        Assert.Equal(stored.Id, fact.FilingId);
        Assert.Equal(StatementLayout.Industrial, stored.Layout);
        Assert.Null(fact.PeriodStart);
    }

    [Fact]
    public async Task The_same_balance_cannot_be_stored_twice()
    {
        // Postgres treats NULLs as different from each other by default, so
        // without NULLS NOT DISTINCT two balance rows (null start) would both
        // get past the unique index.
        await _database.AddPetrobrasAsync();
        await using var db = _database.CreateContext();
        var filing = Petrobras2024();
        db.Filings.Add(filing);
        db.FinancialFacts.Add(new FinancialFact(filing, "1", null, new DateOnly(2024, 12, 31), 1m));
        db.FinancialFacts.Add(new FinancialFact(filing, "1", null, new DateOnly(2024, 12, 31), 1m));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_filing_cannot_be_stored_twice()
    {
        await _database.AddPetrobrasAsync();
        await using var db = _database.CreateContext();
        db.Filings.Add(Petrobras2024());
        db.Filings.Add(Petrobras2024());

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_filing_for_an_unregistered_company_is_refused()
    {
        await using var db = _database.CreateContext();
        db.Filings.Add(Petrobras2024());

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
