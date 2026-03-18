using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class GoldenValidationOutputTests
{
    private static ParsedTransactionValidationResult Parse(string[] specLines, string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), SpecPack.Unpack(specLines));

    private static (string Type, int SegmentIndex, int? ElementIndex) Signature(object diagnostic) =>
        diagnostic switch
        {
            MissingSegmentDiagnostic x => (nameof(MissingSegmentDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            UnexpectedSegmentDiagnostic x => (nameof(UnexpectedSegmentDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            MissingElementDiagnostic x => (nameof(MissingElementDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            TransactionSyntaxDiagnostic x => (nameof(TransactionSyntaxDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            InvalidElementCodeDiagnostic x => (nameof(InvalidElementCodeDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            InvalidElementTypeDiagnostic x => (nameof(InvalidElementTypeDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            InvalidElementLengthDiagnostic x => (nameof(InvalidElementLengthDiagnostic), x.Location.SegmentIndex, x.Location.ElementIndex),
            LoopRepeatExceeded x => (nameof(LoopRepeatExceeded), x.Location.SegmentIndex, x.Location.ElementIndex),
            SegmentRepeatExceeded x => (nameof(SegmentRepeatExceeded), x.Location.SegmentIndex, x.Location.ElementIndex),
            _ => (diagnostic.GetType().Name, -999, null)
        };

    [Fact]
    public void Golden_invalid_code_and_short_element_length_produce_stable_diagnostic_signatures()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "ELM|HDR02|M|AN|3|5|",
            "CODE|HDR01|AA,BB"
        ], "ST*999*7301~HDR*ZZ*A~SE*3*7301~");

        Assert.False(res.Validation.Passed);
        Assert.Equal(
        [
            (nameof(InvalidElementCodeDiagnostic), 1, (int?)1),
            (nameof(InvalidElementLengthDiagnostic), 1, (int?)2)
        ], res.Validation.Diagnostics.Select(Signature));
    }

    [Fact]
    public void Golden_missing_trailer_and_repeated_segment_produce_stable_diagnostic_signatures()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|O|AN|1|5|",
            "SEG|REF|O|1",
            "ELM|REF01|O|AN|1|5|",
            "SEG|TRL|M|1",
            "ELM|TRL01|O|AN|1|5|"
        ], "ST*999*7302~HDR*H~REF*A~REF*B~SE*5*7302~");

        Assert.False(res.Validation.Passed);
        Assert.Equal(
        [
            (nameof(SegmentRepeatExceeded), -1, (int?)null),
            (nameof(MissingSegmentDiagnostic), -1, (int?)null)
        ], res.Validation.Diagnostics.Select(Signature));
    }

    [Fact]
    public void Golden_malformed_transaction_control_data_yields_parse_level_signatures_in_order()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|O|AN|1|5|"
        ], "ST*999*7303~HDR*H~SE*4*NOPE~ZZZ*after~");

        Assert.NotNull(res.Transaction);
        Assert.False(res.Validation.Passed);
        Assert.Equal(
        [
            (nameof(TransactionSyntaxDiagnostic), 2, (int?)null),
            (nameof(TransactionSyntaxDiagnostic), 2, (int?)null),
            (nameof(UnexpectedSegmentDiagnostic), 3, (int?)null)
        ], res.Validation.Diagnostics.Select(Signature));
    }
}
