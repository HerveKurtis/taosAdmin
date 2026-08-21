using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class IbanFormatTests
{
    [Theory]
    [InlineData("be68 5390 0754 7034", "BE68539007547034")]
    [InlineData("  FR7630006000011234567890189  ", "FR7630006000011234567890189")]
    [InlineData("BE68\t5390\n0754 7034", "BE68539007547034")]
    public void Normalize_uppercases_and_strips_whitespace(string raw, string expected)
        => Assert.Equal(expected, IbanFormat.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_turns_blank_into_null(string? raw)
        => Assert.Null(IbanFormat.Normalize(raw));

    [Fact]
    public void An_absent_iban_is_acceptable()
        => Assert.True(IbanFormat.IsAcceptable(null));

    [Theory]
    [InlineData("BE68539007547034")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("NL91ABNA0417164300")]
    public void A_structurally_valid_iban_is_acceptable(string value)
        => Assert.True(IbanFormat.IsAcceptable(value));

    [Theory]
    [InlineData("1234567890123456")]
    [InlineData("BEXX539007547034")]
    [InlineData("BE68")]
    [InlineData("BE68-5390-0754-7034")]
    public void A_malformed_iban_is_rejected(string value)
        => Assert.False(IbanFormat.IsAcceptable(value));

    [Fact]
    public void A_new_account_carries_no_contact_details()
    {
        var a = new Account();
        Assert.Null(a.Phone);
        Assert.Null(a.PostalAddress);
        Assert.Null(a.Iban);
    }
}
