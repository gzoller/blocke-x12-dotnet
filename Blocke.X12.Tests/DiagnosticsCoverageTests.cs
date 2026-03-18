using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class DiagnosticsCoverageTests
{
    private static IReadOnlyList<object> Parse(string[] specLines, string raw)
    {
        var spec = SpecPack.Unpack(specLines);
        return X12Parser.ParseTransaction(new X12Types.X12_ST(raw), spec).Validation.Diagnostics;
    }

    [Fact]
    public void MissingSegmentDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*0001~HDR*X~");

        Assert.Contains(diags, d => d is MissingSegmentDiagnostic m && m.SegmentId == "SE");
    }

    [Fact]
    public void UnexpectedSegmentDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*0002~HDR*X~SE*3*0002~ZZZ*1~");

        Assert.Contains(diags, d => d is UnexpectedSegmentDiagnostic u && u.SegmentId == "ZZZ");
    }

    [Fact]
    public void SegmentRepeatExceeded_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "SEG|REF|O|1"
        ], "ST*999*0003~HDR*X~REF*A~REF*B~SE*5*0003~");

        Assert.Contains(diags, d => d is SegmentRepeatExceeded s && s.SegmentId == "REF" && s.Actual > s.MaxAllowed);
    }

    [Fact]
    public void LoopRepeatExceeded_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "LOOP|A|O|1|0",
            "SEG|X1|O|10",
            "END",
            "SEG|TRL|O|1"
        ], "ST*999*0004~HDR*X~A*1~X1*A~A*2~X1*B~TRL*T~SE*8*0004~");

        Assert.Contains(diags, d => d is LoopRepeatExceeded l && l.LoopId == "A" && l.Actual > l.MaxAllowed);
    }

    [Fact]
    public void RuleViolationDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "RULE|ERROR|hdr-rule",
            "MUST|HDR01=\"OK\""
        ], "ST*999*0005~HDR*BAD~SE*3*0005~");

        Assert.Contains(diags, d => d is RuleViolationDiagnostic r && r.SegmentId == "HDR" && r.RuleId == "hdr-rule");
    }

    [Fact]
    public void MissingElementDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|"
        ], "ST*999*0006~HDR*~SE*3*0006~");

        Assert.Contains(diags, d => d is MissingElementDiagnostic m && m.SegmentId == "HDR" && m.ElementIndex == 1);
    }

    [Fact]
    public void InvalidElementTypeDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|N0|1|3|"
        ], "ST*999*0007~HDR*AB~SE*3*0007~");

        Assert.Contains(diags, d => d is InvalidElementTypeDiagnostic i && i.SegmentId == "HDR" && i.ElementIndex == 1);
    }

    [Fact]
    public void InvalidElementLengthDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|2|2|"
        ], "ST*999*0008~HDR*A~SE*3*0008~");

        Assert.Contains(diags, d => d is InvalidElementLengthDiagnostic i && i.SegmentId == "HDR" && i.ElementIndex == 1);
    }

    [Fact]
    public void InvalidElementCodeDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "CODE|HDR01|AA,BB"
        ], "ST*999*0009~HDR*ZZ~SE*3*0009~");

        Assert.Contains(diags, d => d is InvalidElementCodeDiagnostic i && i.SegmentId == "HDR" && i.ElementIndex == 1 && i.Value == "ZZ");
    }

    [Fact]
    public void TransactionSyntaxDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1"
        ], "ST*999*0010~HDR*X~SE*4*0010~");

        Assert.Contains(diags, d => d is TransactionSyntaxDiagnostic t && t.Error == "SegmentCountMismatch");
    }

    [Fact]
    public void EmptySegmentSpecDiagnostic_is_emitted_when_loop_has_no_elements()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "LOOP|N9|O|10|0",
            "END"
        ], "ST*999*0011~HDR*X~N9*AA~SE*4*0011~");

        Assert.Contains(diags, d => d is EmptySegmentSpecDiagnostic e && e.SegmentId == "N9");
    }

    [Fact]
    public void IdElementMissingCodeListDiagnostic_is_emitted()
    {
        var diags = Parse([
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "SEG|N9|O|10",
            "ELM|N901|M|ID|2|3|"
        ], "ST*999*0012~HDR*X~N9*ZA~SE*4*0012~");

        Assert.Contains(diags, d => d is IdElementMissingCodeListDiagnostic i && i.SegmentId == "N9" && i.ElementId == "N901");
    }

    [Fact]
    public void InvalidElementCodeDiagnostic_is_emitted_for_nested_n9_in_po1_loop()
    {
        var diags = Parse([
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "SEG|DTM|O|10",
            "ELM|DTM01|M|ID|3|3|DTM01",
            "LOOP|N9|O|1000|0",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|O|AN|1|30|",
            "END",
            "LOOP|PO1|O|100000|0",
            "ELM|PO101|O|AN|1|20|",
            "LOOP|N9|O|1000|0",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|O|AN|1|30|",
            "END",
            "END",
            "CODE|BEG01|00",
            "CODE|DTM01|002",
            "CODE|N901|PO,VN,IA,ZZ,CO,CR,IV,SI,CT,ZA"
        ], "ST*850*0001~BEG*00~DTM*002~N9*ZA*TOP~PO1*1~N9*DP*57~SE*7*0001~");

        Assert.Contains(diags, d => d is InvalidElementCodeDiagnostic i && i.SegmentId == "N9" && i.ElementIndex == 1 && i.Value == "DP");
    }

    [Fact]
    public void InvalidElementCodeDiagnostic_is_emitted_for_segment_n9_inside_po1()
    {
        var diags = Parse([
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "SEG|DTM|O|10",
            "ELM|DTM01|M|ID|3|3|DTM01",
            "LOOP|PO1|O|100000|0",
            "ELM|PO101|O|AN|1|20|",
            "SEG|N9|O|1000",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|O|AN|1|30|",
            "END",
            "CODE|BEG01|00",
            "CODE|DTM01|002",
            "CODE|N901|PO,VN,IA,ZZ,CO,CR,IV,SI,CT,ZA"
        ], "ST*850*0002~BEG*00~DTM*002~PO1*1~N9*DP*57~SE*6*0002~");

        Assert.Contains(diags, d => d is InvalidElementCodeDiagnostic i && i.SegmentId == "N9" && i.ElementIndex == 1 && i.Value == "DP");
    }

    [Fact]
    public void InvalidElementCodeDiagnostic_is_emitted_after_pid_and_po4()
    {
        var diags = Parse([
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "ELM|BEG02|M|ID|2|2|BEG02",
            "ELM|BEG03|M|AN|1|22|",
            "ELM|BEG05|M|DT|8|8|",
            "SEG|DTM|O|10",
            "ELM|DTM01|M|ID|3|3|DTM01",
            "ELM|DTM02|X|DT|8|8|",
            "LOOP|N9|O|1000|0",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|X|AN|1|30|",
            "SEG|MTX|O|999999",
            "ELM|MTX02|X|AN|1|4096|",
            "END",
            "LOOP|PO1|O|100000|0",
            "ELM|PO101|O|AN|1|20|",
            "ELM|PO102|X|R|1|15|",
            "ELM|PO103|O|ID|2|2|PO103",
            "LOOP|PID|O|1000|0",
            "ELM|PID01|M|ID|1|1|PID01",
            "ELM|PID05|X|AN|1|80|",
            "END",
            "SEG|PO4|O|1",
            "ELM|PO401|O|N0|1|6|",
            "SEG|N9|O|1000",
            "ELM|N901|M|ID|2|3|N901",
            "ELM|N902|X|AN|1|30|",
            "END",
            "LOOP|CTT|O|1|0",
            "ELM|CTT01|M|N0|1|6|",
            "END",
            "CODE|BEG01|00",
            "CODE|BEG02|NE,SA",
            "CODE|DTM01|002",
            "CODE|PO103|EA",
            "CODE|PID01|F",
            "CODE|N901|PO,VN,IA,ZZ,CO,CR,IV,SI,CT,ZA"
        ], "ST*850*0003~BEG*00*NE*PONUM**20260101~DTM*002*20260102~N9*ZA*TOP~PO1*1*2*EA~PID*F****ITEM~PO4*1~N9*DP*57~CTT*1~SE*10*0003~");

        Assert.Contains(diags, d => d is InvalidElementCodeDiagnostic i && i.SegmentId == "N9" && i.ElementIndex == 1 && i.Value == "DP");
    }
}
