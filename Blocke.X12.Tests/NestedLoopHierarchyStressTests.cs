using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class NestedLoopHierarchyStressTests
{
    private static readonly X12Specification DeepSpec =
        SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|O|AN|1|10|",
            "LOOP|A|O|5|0",
            "ELM|A01|M|AN|1|10|",
            "SEG|A1|O|5",
            "ELM|A101|O|AN|1|10|",
            "NLOOP|B|O|3|0|A01",
            "ELM|B01|M|ID|2|2|B01",
            "SEG|B1|O|5",
            "ELM|B101|O|AN|1|10|",
            "NLOOP|C|O|2|0|B01",
            "ELM|C01|M|ID|2|2|C01",
            "SEG|C1|O|5",
            "ELM|C101|O|AN|1|10|",
            "END",
            "END",
            "END",
            "SEG|TRL|O|1",
            "ELM|TRL01|O|AN|1|10|",
            "CODE|B01|AA,BB",
            "CODE|C01|X1,X2"
        ]);

    private static ParsedTransactionValidationResult Parse(string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), DeepSpec);

    [Fact]
    public void Three_level_nested_loops_assemble_in_depth_first_order_across_siblings()
    {
        var res = Parse("ST*999*7201~HDR*H~A*1~A1*a~B*AA~B1*b~C*X1~C1*c~B*BB~B1*b2~C*X2~C1*c2~TRL*t~SE*14*7201~");

        Assert.True(res.Validation.Passed);
        Assert.Empty(res.Validation.Diagnostics);

        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        var a = Assert.IsType<LogicalNode>(tx.Segments.Children[1]);
        Assert.Equal(["A1", "B", "B"], a.Children.Select(c => c.Id));

        var bLoops = a.Children.OfType<LogicalNode>().Where(l => l.Id == "B").ToArray();
        Assert.Equal(2, bLoops.Length);
        Assert.Equal([["B1", "C"], ["B1", "C"]], bLoops.Select(b => b.Children.Select(c => c.Id).ToArray()).ToArray());
    }

    [Fact]
    public void Grandchild_loop_self_terminates_when_repeated_under_the_same_parent()
    {
        var res = Parse("ST*999*7202~HDR*H~A*1~B*AA~C*X1~C1*c1~C*X2~C1*c2~TRL*t~SE*10*7202~");

        Assert.True(res.Validation.Passed);
        Assert.Empty(res.Validation.Diagnostics);

        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        var b = Assert.IsType<LogicalNode>(Assert.IsType<LogicalNode>(tx.Segments.Children[1]).Children.Single(c => c.Id == "B"));
        Assert.Equal(2, b.Children.OfType<LogicalNode>().Count(l => l.Id == "C"));
    }

    [Fact]
    public void Grandchild_loop_max_occurs_overflow_is_reported_at_the_loop_id()
    {
        var res = Parse("ST*999*7203~HDR*H~A*1~B*AA~C*X1~C1*c1~C*X2~C1*c2~C*X1~C1*c3~TRL*t~SE*11*7203~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is LoopRepeatExceeded lre && lre.LoopId == "C" && lre.Actual > lre.MaxAllowed);
    }

    [Fact]
    public void Invalid_grandchild_code_list_value_is_reported_on_the_deepest_loop_header()
    {
        var res = Parse("ST*999*7204~HDR*H~A*1~B*AA~C*ZZ~C1*c1~TRL*t~SE*7*7204~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is InvalidElementCodeDiagnostic ied && ied.SegmentId == "C" && ied.ElementIndex == 1 && ied.Value == "ZZ");
    }
}
