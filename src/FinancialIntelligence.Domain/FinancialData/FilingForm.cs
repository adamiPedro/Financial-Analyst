namespace FinancialIntelligence.Domain.FinancialData;

public enum FilingForm
{
    /// <summary>Annual statements (Demonstrações Financeiras Padronizadas).</summary>
    Dfp = 1,

    /// <summary>
    /// Quarterly statements (Informações Trimestrais). There is no fourth-quarter
    /// ITR: the annual DFP covers that period.
    /// </summary>
    Itr = 2
}
