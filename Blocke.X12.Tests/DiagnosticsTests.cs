using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class DiagnosticsTests
{
    private static IReadOnlyList<object> Parse(string[] specLines, string raw)
    {
        var spec = SpecPack.Unpack(specLines);
        return X12Parser.ParseTransaction(new X12Types.X12_ST(raw), spec).Validation.Diagnostics;
    }

    [Fact]
    public void MissingSegmentDiagnostic_uses_unknown_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*1001~HDR*X~");

        var d = Assert.Single(diags.OfType<MissingSegmentDiagnostic>().Where(x => x.SegmentId == "SE"));
        Assert.Equal(-1, d.Location.SegmentIndex);
        Assert.Null(d.Location.ElementIndex);
        Assert.Equal(-1, d.Location.RawOffset);
    }

    [Fact]
    public void UnexpectedSegmentDiagnostic_has_segment_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*1002~HDR*X~SE*3*1002~ZZZ*1~");

        var d = Assert.Single(diags.OfType<UnexpectedSegmentDiagnostic>().Where(x => x.SegmentId == "ZZZ"));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.True(d.Location.RawOffset >= 0);
        Assert.Null(d.Location.ElementIndex);
    }

    [Fact]
    public void SegmentRepeatExceeded_uses_unknown_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "SEG|REF|O|1"
        ], "ST*999*1003~HDR*X~REF*A~REF*B~SE*5*1003~");

        var d = Assert.Single(diags.OfType<SegmentRepeatExceeded>().Where(x => x.SegmentId == "REF"));
        Assert.Equal(-1, d.Location.SegmentIndex);
        Assert.Null(d.Location.ElementIndex);
        Assert.Equal(-1, d.Location.RawOffset);
    }

    [Fact]
    public void LoopRepeatExceeded_uses_unknown_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "LOOP|A|O|1|0",
            "SEG|X1|O|10",
            "END",
            "SEG|TRL|O|1"
        ], "ST*999*1004~HDR*X~A*1~X1*A~A*2~X1*B~TRL*T~SE*8*1004~");

        var d = Assert.Single(diags.OfType<LoopRepeatExceeded>().Where(x => x.LoopId == "A"));
        Assert.Equal(-1, d.Location.SegmentIndex);
        Assert.Null(d.Location.ElementIndex);
        Assert.Equal(-1, d.Location.RawOffset);
    }

    [Fact]
    public void RuleViolationDiagnostic_has_segment_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "RULE|ERROR|hdr-rule",
            "MUST|HDR01=\"OK\""
        ], "ST*999*1005~HDR*BAD~SE*3*1005~");

        var d = Assert.Single(diags.OfType<RuleViolationDiagnostic>().Where(x => x.RuleId == "hdr-rule"));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.True(d.Location.RawOffset >= 0);
        Assert.Null(d.Location.ElementIndex);
    }

    [Fact]
    public void MissingElementDiagnostic_has_element_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|"
        ], "ST*999*1006~HDR*~SE*3*1006~");

        var d = Assert.Single(diags.OfType<MissingElementDiagnostic>().Where(x => x.SegmentId == "HDR" && x.ElementIndex == 1));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void InvalidElementTypeDiagnostic_has_element_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|N0|1|3|"
        ], "ST*999*1007~HDR*AB~SE*3*1007~");

        var d = Assert.Single(diags.OfType<InvalidElementTypeDiagnostic>().Where(x => x.SegmentId == "HDR" && x.ElementIndex == 1));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void InvalidElementLengthDiagnostic_has_element_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|2|2|"
        ], "ST*999*1008~HDR*A~SE*3*1008~");

        var d = Assert.Single(diags.OfType<InvalidElementLengthDiagnostic>().Where(x => x.SegmentId == "HDR" && x.ElementIndex == 1));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void InvalidElementCodeDiagnostic_has_element_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "CODE|HDR01|AA,BB"
        ], "ST*999*1009~HDR*ZZ~SE*3*1009~");

        var d = Assert.Single(diags.OfType<InvalidElementCodeDiagnostic>().Where(x => x.SegmentId == "HDR" && x.ElementIndex == 1));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void TransactionSyntaxDiagnostic_has_segment_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*1010~HDR*X~SE*4*1010~");

        var d = Assert.Single(diags.OfType<TransactionSyntaxDiagnostic>().Where(x => x.Error == "SegmentCountMismatch"));
        Assert.True(d.Location.SegmentIndex >= 0);
        Assert.True(d.Location.RawOffset >= 0);
        Assert.Null(d.Location.ElementIndex);
    }

    [Fact]
    public void EmptySegmentSpecDiagnostic_uses_unknown_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "LOOP|N9|O|10|0",
            "END"
        ], "ST*999*1011~HDR*X~N9*AA~SE*4*1011~");

        var d = Assert.Single(diags.OfType<EmptySegmentSpecDiagnostic>().Where(x => x.SegmentId == "N9"));
        Assert.Equal(-1, d.Location.SegmentIndex);
        Assert.Null(d.Location.ElementIndex);
        Assert.Equal(-1, d.Location.RawOffset);
    }

    [Fact]
    public void IdElementMissingCodeListDiagnostic_uses_unknown_location()
    {
        var diags = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "SEG|N9|O|10",
            "ELM|N901|M|ID|2|3|"
        ], "ST*999*1012~HDR*X~N9*ZA~SE*4*1012~");

        var d = Assert.Single(diags.OfType<IdElementMissingCodeListDiagnostic>().Where(x => x.SegmentId == "N9" && x.ElementId == "N901"));
        Assert.Equal(-1, d.Location.SegmentIndex);
        Assert.Null(d.Location.ElementIndex);
        Assert.Equal(-1, d.Location.RawOffset);
    }
}
