using System.Reflection;

namespace FinancialIntelligence.UnitTests;

/// <summary>
/// The dependency rule in CLAUDE.md is only worth stating if something enforces it.
/// This is that something: Domain must reference nothing but the BCL.
/// </summary>
public class DependencyRuleTests
{
    [Fact]
    public void Domain_references_nothing_outside_the_base_class_library()
    {
        var domain = Assembly.Load("FinancialIntelligence.Domain");

        var forbidden = domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name =>
                name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
                || name.StartsWith("Npgsql", StringComparison.Ordinal)
                || name.StartsWith("FinancialIntelligence.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }
}
