using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class X12TypeAndVersionTests
{
    [Fact]
    public void An_and_id_reject_empty_strings_and_invalid_characters()
    {
        Assert.Equal(ElementSyntaxError.MandatoryElementMissing, X12Type.AN.Validate(string.Empty));
        Assert.Equal(ElementSyntaxError.MandatoryElementMissing, X12Type.ID.Validate(string.Empty));
        Assert.Null(X12Type.AN.Validate("ok"));
        Assert.Null(X12Type.ID.Validate("AA"));
        Assert.Equal(ElementSyntaxError.InvalidCharacter, X12Type.AN.Validate("bad\n"));
        Assert.Equal(ElementSyntaxError.InvalidCharacter, X12Type.ID.Validate("bad\t"));
    }

    [Fact]
    public void Numeric_and_real_types_enforce_numeric_syntax()
    {
        Assert.Null(X12Type.N0.Validate("123"));
        Assert.Equal(ElementSyntaxError.InvalidNumericValue, X12Type.N2.Validate("12A"));
        Assert.Equal(ElementSyntaxError.MandatoryElementMissing, X12Type.N1.Validate(string.Empty));
        Assert.Null(X12Type.R.Validate("12.34"));
        Assert.Null(X12Type.R.Validate("-7"));
        Assert.Equal(ElementSyntaxError.InvalidNumericValue, X12Type.R.Validate("12."));
    }

    [Fact]
    public void Date_and_time_types_enforce_formatting_rules()
    {
        Assert.Null(X12Type.DT.Validate("20240229"));
        Assert.Equal(ElementSyntaxError.InvalidDate, X12Type.DT.Validate("20230229"));
        Assert.Equal(ElementSyntaxError.InvalidDate, X12Type.DT.Validate("202401"));
        Assert.Null(X12Type.TM.Validate("2359"));
        Assert.Null(X12Type.TM.Validate("235959"));
        Assert.Equal(ElementSyntaxError.InvalidCharacter, X12Type.TM.Validate("23A0"));
        Assert.Equal(ElementSyntaxError.InvalidTime, X12Type.TM.Validate("123"));
    }

    [Fact]
    public void Binary_type_requires_a_non_empty_printable_value()
    {
        Assert.Equal(ElementSyntaxError.MandatoryElementMissing, X12Type.B.Validate(string.Empty));
        Assert.Null(X12Type.B.Validate("ABC123+/="));
        Assert.Equal(ElementSyntaxError.InvalidCharacter, X12Type.B.Validate("\u0001bad"));
    }

    [Fact]
    public void Supported_versions_accept_only_known_majors()
    {
        Assert.Equal(SupportedX12Version.V4010, SupportedX12VersionExtensions.FromMajor("004010"));
        Assert.Equal(SupportedX12Version.V5010, SupportedX12VersionExtensions.FromMajor("005010"));
        Assert.True(SupportedVersion.TryFromParsed(new X12Version("004010", null), out var v4010, out var err4010));
        Assert.Equal(SupportedX12Version.V4010, v4010);
        Assert.Null(err4010);
        Assert.True(SupportedVersion.TryFromParsed(new X12Version("005010", "X222"), out var v5010, out var err5010));
        Assert.Equal(SupportedX12Version.V5010, v5010);
        Assert.Null(err5010);
        Assert.False(SupportedVersion.TryFromParsed(new X12Version("007040", null), out _, out var unsupported));
        Assert.Contains("Unsupported X12 version", unsupported, StringComparison.Ordinal);
    }

    [Fact]
    public void X12Version_parsing_normalizes_4_digit_input_and_preserves_implementations()
    {
        Assert.True(X12Version.TryParse("4010", out var v4010));
        Assert.Equal(new X12Version("004010", null), v4010);
        Assert.True(X12Version.TryParse("005010X222A1", out var v5010));
        Assert.Equal(new X12Version("005010", "222A1"), v5010);
        Assert.False(X12Version.TryParse("bogus", out _));
    }

    [Fact]
    public void Unknown_type_codes_throw_helpful_errors()
    {
        var versionEx = Assert.Throws<ArgumentException>(() => SupportedX12VersionExtensions.FromMajor("007040"));
        Assert.Contains("Unsupported X12 version", versionEx.Message, StringComparison.Ordinal);

        var ex = Assert.Throws<ArgumentException>(() => X12TypeCodec.FromValue("ZZ"));
        Assert.Contains("Unknown X12 data type", ex.Message, StringComparison.Ordinal);
    }
}
