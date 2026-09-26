using FinancialIntelligence.Domain.Companies;

namespace FinancialIntelligence.UnitTests.Companies;

public class CnpjTests
{
    // Real registrations, so the check-digit maths is verified against actual
    // values rather than numbers invented to satisfy it.
    [Theory]
    [InlineData("33.000.167/0001-01", "33000167000101")]   // Petrobras
    [InlineData("33000167000101", "33000167000101")]       // already unpunctuated
    [InlineData("  33000167000101  ", "33000167000101")]
    [InlineData("33.592.510/0001-54", "33592510000154")]   // Vale
    public void Parse_strips_punctuation_and_keeps_fourteen_digits(string input, string expected)
    {
        Assert.Equal(expected, Cnpj.Parse(input).Value);
    }

    [Fact]
    public void Punctuated_and_bare_forms_are_the_same_entity()
    {
        Assert.Equal(Cnpj.Parse("33.000.167/0001-01"), Cnpj.Parse("33000167000101"));
    }

    [Fact]
    public void Formatted_produces_the_form_a_brazilian_reader_expects()
    {
        Assert.Equal("33.000.167/0001-01", Cnpj.Parse("33000167000101").Formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("3300016700010")]           // thirteen digits
    [InlineData("330001670001011")]         // fifteen digits
    [InlineData("33000167000102")]          // valid length, wrong check digit
    [InlineData("11111111111111")]          // repeated digits pass mod-11, not real
    [InlineData("00000000000000")]
    [InlineData("PETR4")]
    public void Invalid_input_is_rejected(string? input)
    {
        Assert.False(Cnpj.TryParse(input, out _));
        Assert.Throws<ArgumentException>(() => Cnpj.Parse(input));
    }
}
