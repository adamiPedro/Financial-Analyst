namespace FinancialIntelligence.Domain.Companies;

/// <summary>
/// A Brazilian CNPJ: the Receita Federal registration number for a legal entity.
/// </summary>
public sealed record Cnpj
{
    private const int Length = 14;

    // Receita Federal's mod-11 weights. The second digit is computed over
    // thirteen positions, so its weight run is one longer and starts at 6.
    private static readonly int[] FirstDigitWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondDigitWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cnpj(string value) => Value = value;

    /// <summary>Fourteen digits, unpunctuated.</summary>
    public string Value { get; }

    /// <summary>The punctuated form Brazilians expect to read: 00.000.000/0000-00.</summary>
    public string Formatted =>
        $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    public static Cnpj Parse(string? input)
    {
        if (!TryParse(input, out var cnpj))
        {
            throw new ArgumentException($"'{input}' is not a valid CNPJ.", nameof(input));
        }

        return cnpj;
    }

    public static bool TryParse(string? input, out Cnpj cnpj)
    {
        cnpj = null!;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // CVM publishes CNPJ punctuated; most other sources don't. Stripping
        // non-digits rather than demanding one format keeps that choice at the
        // boundary instead of in every importer.
        Span<char> digits = stackalloc char[Length];
        var count = 0;

        foreach (var c in input)
        {
            if (!char.IsAsciiDigit(c))
            {
                continue;
            }

            if (count == Length)
            {
                return false;
            }

            digits[count++] = c;
        }

        if (count != Length)
        {
            return false;
        }

        // An all-zero CNPJ genuinely satisfies the mod-11 maths (every weighted
        // sum is zero), so the check digits alone would accept it. Repeated-digit
        // strings are rejected by convention as placeholders; they turn up in
        // test fixtures and in rows where a real value was unavailable.
        var allSame = true;
        for (var i = 1; i < Length; i++)
        {
            if (digits[i] != digits[0])
            {
                allSame = false;
                break;
            }
        }

        if (allSame || !HasValidCheckDigits(digits))
        {
            return false;
        }

        cnpj = new Cnpj(new string(digits));
        return true;
    }

    /// <summary>
    /// Validating check digits rather than only length means a transcription
    /// error in a hand-curated seed list fails at parse time, instead of as an
    /// empty result set three layers away.
    ///
    /// NOTE: Receita Federal is introducing an alphanumeric CNPJ format. Confirm
    /// the current rules before relying on this beyond the historical data CVM
    /// publishes - an alphanumeric registration would need a different routine.
    /// </summary>
    private static bool HasValidCheckDigits(ReadOnlySpan<char> digits)
    {
        if (digits[12] - '0' != CheckDigit(digits[..12], FirstDigitWeights))
        {
            return false;
        }

        return digits[13] - '0' == CheckDigit(digits[..13], SecondDigitWeights);
    }

    private static int CheckDigit(ReadOnlySpan<char> digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    public override string ToString() => Value;
}
