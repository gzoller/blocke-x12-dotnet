using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class ValidatorCorpusTests
{
    private static ParsedTransactionValidationResult Parse(string[] specLines, string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), SpecPack.Unpack(specLines));

    [Fact]
    public void Clean_purchasing_style_document_passes_with_nested_loops_and_matching_ctt()
    {
        var res = Parse(
        [
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "ELM|BEG02|M|AN|1|8|",
            "LOOP|PO1|O|10|0",
            "ELM|PO101|M|AN|1|6|",
            "SEG|PID|O|10",
            "ELM|PID01|M|ID|1|2|PID01",
            "ELM|PID05|O|AN|1|30|",
            "NLOOP|N9|O|5|0|PO101",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|O|AN|1|20|",
            "END",
            "END",
            "SEG|CTT|O|1",
            "ELM|CTT01|M|N0|1|6|",
            "CTT|PID",
            "CODE|BEG01|00,05",
            "CODE|PID01|F,S",
            "CODE|N901|PO,VN"
        ], "ST*850*7101~BEG*00*PO123~PO1*1~PID*F****ITEM1~N9*PO*AAA~PO1*2~PID*F****ITEM2~N9*VN*BBB~CTT*2~SE*10*7101~");

        Assert.True(res.Validation.Passed);
        Assert.Empty(res.Validation.Diagnostics);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        Assert.Equal(["BEG", "PO1", "PO1", "CTT"], tx.Segments.Children.Select(c => c.Id));
    }

    [Fact]
    public void Purchasing_document_with_mixed_semantic_failures_emits_multiple_diagnostic_families()
    {
        var res = Parse(
        [
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "ELM|BEG02|M|AN|3|8|",
            "LOOP|PO1|O|10|0",
            "ELM|PO101|M|AN|1|6|",
            "SEG|PID|O|10",
            "ELM|PID01|M|ID|1|2|PID01",
            "END",
            "SEG|CTT|O|1",
            "ELM|CTT01|M|N0|1|6|",
            "CTT|PID",
            "CODE|BEG01|00,05",
            "CODE|PID01|F,S"
        ], "ST*850*7102~BEG*ZZ*P~PO1*1~PID*Q~CTT*2~SE*6*7102~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is InvalidElementCodeDiagnostic);
        Assert.Contains(res.Validation.Diagnostics, d => d is InvalidElementLengthDiagnostic);
        Assert.Contains(res.Validation.Diagnostics, d => d is TransactionSyntaxDiagnostic tsd && tsd.Error == "StructuralError");
    }
}
