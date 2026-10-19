namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>A metric's value for one period, with every fact it was worked out from.</summary>
public sealed record Reading(decimal Value, bool IsCalculated, IReadOnlyList<FinancialFact> Sources);
