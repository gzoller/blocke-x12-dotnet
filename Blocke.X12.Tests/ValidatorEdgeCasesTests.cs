using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class ValidatorEdgeCasesTests
{
    private static ParsedTransactionValidationResult Parse(string[] specLines, string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), SpecPack.Unpack(specLines));

    [Fact]
    public void Ctt_anchor_mismatch_emits_structural_diagnostic_with_location()
    {
        var res = Parse(
        [
            "X|850|005010",
            "SEG|HDR|M|1",
            "LOOP|PO1|O|10|0",
            "SEG|PID|O|10",
            "END",
            "SEG|CTT|O|1",
            "ELM|CTT01|M|N0|1|6|",
            "CTT|PID"
        ], "ST*850*3001~HDR*H~PO1*1~PID*F****ITEM1~PO1*2~PID*F****ITEM2~CTT*1~SE*8*3001~");

        Assert.False(res.Validation.Passed);
        var d = Assert.Single(res.Validation.Diagnostics.OfType<TransactionSyntaxDiagnostic>().Where(x => x.Error == "StructuralError"));
        Assert.True(d.Location.SegmentIndex >= 0);
    }

    [Fact]
    public void Ctt_anchor_match_does_not_emit_structural_diagnostic()
    {
        var res = Parse(
        [
            "X|850|005010",
            "SEG|HDR|M|1",
            "LOOP|PO1|O|10|0",
            "SEG|PID|O|10",
            "END",
            "SEG|CTT|O|1",
            "ELM|CTT01|M|N0|1|6|",
            "CTT|PID"
        ], "ST*850*3002~HDR*H~PO1*1~PID*F****ITEM1~PO1*2~PID*F****ITEM2~CTT*2~SE*8*3002~");

        Assert.DoesNotContain(res.Validation.Diagnostics, d => d is TransactionSyntaxDiagnostic tsd && tsd.Error == "StructuralError");
    }

    [Fact]
    public void Segment_repeat_exceeded_is_emitted_for_plain_segment_max_occurs()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "SEG|REF|O|1"
        ], "ST*999*3003~HDR*X~REF*A~REF*B~SE*5*3003~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is SegmentRepeatExceeded sre && sre.SegmentId == "REF" && sre.Actual > sre.MaxAllowed);
    }

    [Fact]
    public void Optional_segment_omitted_does_not_emit_missing_segment_diagnostic()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "SEG|REF|O|1",
            "SEG|TRL|M|1",
            "ELM|TRL01|O|AN|1|5|"
        ], "ST*999*3004~HDR*X~TRL*T~SE*4*3004~");

        Assert.DoesNotContain(res.Validation.Diagnostics, d => d is MissingSegmentDiagnostic msd && msd.SegmentId == "REF");
    }

    [Fact]
    public void Required_loop_root_element_missing_emits_missing_element_diagnostic_on_loop_id()
    {
        var res = Parse(
        [
            "X|999|005010",
            "LOOP|A|O|10|0",
            "ELM|A01|M|AN|1|5|",
            "SEG|X1|O|10",
            "ELM|X101|O|AN|1|5|",
            "END"
        ], "ST*999*3005~A*~X1*ok~SE*4*3005~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is MissingElementDiagnostic med && med.SegmentId == "A" && med.ElementIndex == 1);
    }

    [Fact]
    public void Empty_code_line_is_rejected_by_specpack()
    {
        var ex = Assert.Throws<ArgumentException>(() => SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "CODE|HDR01|"
        ]));

        Assert.Contains("must specify at least one valid code value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_element_ordering_in_spec_fails_validation_immediately()
    {
        var spec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR02|M|AN|1|5|",
            "ELM|HDR01|M|AN|1|5|"
        ]);

        var ex = Assert.Throws<ArgumentException>(() =>
            X12Parser.ParseTransaction(new X12Types.X12_ST("ST*999*3006~HDR*A*B~SE*3*3006~"), spec));

        Assert.Contains("not strictly increasing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_loop_invalid_code_list_violation_is_reported_for_child_loop_segment()
    {
        var res = Parse(
        [
            "X|850|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|O|AN|1|5|",
            "LOOP|A|O|10|0",
            "ELM|A01|O|AN|1|5|",
            "SEG|X1|O|10",
            "ELM|X101|O|AN|1|5|",
            "NLOOP|B|O|10|0|A01",
            "ELM|B01|M|ID|2|2|B01",
            "SEG|Y1|O|10",
            "ELM|Y101|O|AN|1|5|",
            "END",
            "END",
            "CODE|B01|AA,BB"
        ], "ST*850*3007~HDR*H~A*1~X1*foo~B*ZZ~Y1*y~SE*6*3007~");

        Assert.False(res.Validation.Passed);
        var d = Assert.Single(res.Validation.Diagnostics.OfType<InvalidElementCodeDiagnostic>().Where(x => x.SegmentId == "B" && x.ElementIndex == 1 && x.Value == "ZZ"));
        Assert.True(d.Location.SegmentIndex >= 0);
    }
}
