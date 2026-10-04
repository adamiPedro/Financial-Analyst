using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinancialIntelligence.Infrastructure.Companies;

public sealed record CompanyImportResult(int Added, int Updated, int CnpjChanged);

/// <summary>
/// Loads CVM's company register into the companies table. Registry details are
/// updated in place - unlike financial facts, they carry no history worth
/// keeping. Nothing is deleted, and tickers (which CAD doesn't have) are left
/// alone.
/// </summary>
public sealed partial class CompanyImporter(FinancialIntelligenceDbContext db, ILogger<CompanyImporter> logger)
{
    public async Task<CompanyImportResult> ImportAsync(Stream csv, CancellationToken cancellationToken)
    {
        var fromFile = await CadReader.ReadAsync(csv, cancellationToken);

        // Tracked on purpose. EF snapshots each row as it loads, and on save it
        // writes only what differs from the snapshot - so an unchanged file
        // writes nothing, without comparing fields by hand.
        var existing = await db.Companies.ToDictionaryAsync(c => c.CvmCode, cancellationToken);

        var added = 0;
        var cnpjChanged = 0;
        foreach (var row in fromFile)
        {
            if (!existing.TryGetValue(row.CvmCode, out var company))
            {
                db.Companies.Add(new Company(
                    row.CvmCode, row.Cnpj, row.Name, sector: row.Sector, registrationStatus: row.Status));
                added++;
                continue;
            }

            if (company.Cnpj != row.Cnpj)
            {
                LogCnpjChanged(logger, row.CvmCode, company.Cnpj.Formatted, row.Cnpj.Formatted);
                company.ChangeCnpj(row.Cnpj);
                cnpjChanged++;
            }

            company.Rename(row.Name);
            company.UpdateRegistry(row.Sector, row.Status);
        }

        // Read from EF's own view so the count can't drift from what is written.
        // It has to happen before saving, which resets every entry to Unchanged.
        db.ChangeTracker.DetectChanges();
        var updated = db.ChangeTracker.Entries<Company>().Count(e => e.State == EntityState.Modified);

        // One SaveChanges is one transaction: if any row fails, none is written.
        await db.SaveChangesAsync(cancellationToken);

        return new CompanyImportResult(added, updated, cnpjChanged);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "CNPJ for CVM code {CvmCode} changed from {OldCnpj} to {NewCnpj}.")]
    private static partial void LogCnpjChanged(ILogger logger, int cvmCode, string oldCnpj, string newCnpj);
}
