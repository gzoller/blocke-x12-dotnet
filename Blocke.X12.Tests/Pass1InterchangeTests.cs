using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12.Tests;

public sealed class Pass1InterchangeTests
{
    private const string Isa =
        "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *250101*0800*U*00401*000000001*0*T*:~";

    [Fact]
    public void Pass1_strict_parses_interchange_and_preserves_raw_transactions()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "BEG*00*NE*PO123**20250101~\n" +
                  "SE*3*0001~\n" +
                  "ST*850*0002~\n" +
                  "BEG*00*NE*PO124**20250101~\n" +
                  "SE*3*0002~\n" +
                  "GE*2*1~\n" +
                  "IEA*1*000000001~";

        var env = X12Parser.Parse(raw, ParseMode.Normal);

        Assert.Single(env.FunctionalGroups);
        var group = env.FunctionalGroups[0];
        Assert.Equal(FunctionalId.PurchaseOrder, group.FunctionalId);
        Assert.Equal("1", group.ControlNumber);
        Assert.Equal(2, group.Transactions.Count);
        Assert.Equal("ST*850*0001~BEG*00*NE*PO123**20250101~SE*3*0001~", group.Transactions[0].Value);
        Assert.Equal("ST*850*0002~BEG*00*NE*PO124**20250101~SE*3*0002~", group.Transactions[1].Value);
        Assert.Contains("ST*997*0001~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK1*PO*1~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK2*850*0001~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK2*850*0002~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK5*A~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK9*A*2*2*0~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("SE*", env.Ack.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_strict_fails_when_isa_is_missing()
    {
        var raw = "GS*PO*S*R*20250101*0800*1*X*004010~ST*850*0001~SE*2*0001~GE*1*1~IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("ISA", ex.SegmentId);
        Assert.Contains("Missing ISA", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_strict_fails_when_isa_is_truncated()
    {
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *250101*0800*U*00401*000000001*0*T*";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("ISA", ex.SegmentId);
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pass1_strict_fails_when_isa_field_count_is_invalid()
    {
        var badIsa = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *250101*0800*U*00401*000000001*0*T~";
        var raw = $"{badIsa}~GS*PO*S*R*20250101*0800*1*X*004010~ST*850*0001~SE*2*0001~GE*1*1~IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("ISA", ex.SegmentId);
        Assert.Contains("17 fields", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_strict_fails_when_ge_is_missing_before_iea()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "BEG*00*NE*PO123**20250101~\n" +
                  "SE*3*0001~\n" +
                  "IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("GE", ex.SegmentId);
        Assert.Contains("Missing GE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_permissive_still_fails_malformed_gs()
    {
        var raw = $"{Isa}\n" +
                  "GS*ZZ*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "SE*2*0001~\n" +
                  "GE*1*1~\n" +
                  "IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Permissive));

        Assert.Equal("GS", ex.SegmentId);
        Assert.Contains("Invalid GS01", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_permissive_allows_count_mismatch_only()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "SE*2*0001~\n" +
                  "GE*9*1~\n" +
                  "IEA*8*000000001~";

        var env = X12Parser.Parse(raw, ParseMode.Permissive);

        Assert.Single(env.FunctionalGroups);
        Assert.Equal("1", env.FunctionalGroups[0].ControlNumber);
        Assert.Single(env.FunctionalGroups[0].Transactions);
        Assert.Equal("ST*850*0001~SE*2*0001~", env.FunctionalGroups[0].Transactions[0].Value);
        Assert.Contains("AK5*A~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK9*E*1*1*0*4~", env.Ack.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_permissive_includes_ak5_count_warning_when_se01_mismatches()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "SE*9*0001~\n" +
                  "GE*1*1~\n" +
                  "IEA*1*000000001~";

        var env = X12Parser.Parse(raw, ParseMode.Permissive);

        Assert.Contains("AK5*E*4~", env.Ack.Value, StringComparison.Ordinal);
        Assert.Contains("AK9*E*1*1*0~", env.Ack.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_strict_fails_when_se02_does_not_match_st02()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "SE*2*9999~\n" +
                  "GE*1*1~\n" +
                  "IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("SE", ex.SegmentId);
        Assert.Contains("SE02", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pass1_strict_fails_on_unexpected_segment_between_ge_and_iea()
    {
        var raw = $"{Isa}\n" +
                  "GS*PO*S*R*20250101*0800*1*X*004010~\n" +
                  "ST*850*0001~\n" +
                  "SE*2*0001~\n" +
                  "GE*1*1~\n" +
                  "N9*XX*oops~\n" +
                  "IEA*1*000000001~";

        var ex = Assert.Throws<X12ParseError>(() => X12Parser.Parse(raw, ParseMode.Normal));

        Assert.Equal("N9", ex.SegmentId);
        Assert.Contains("Unexpected segment at interchange level", ex.Message, StringComparison.Ordinal);
    }
}
