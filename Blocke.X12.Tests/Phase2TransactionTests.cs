using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class Phase2TransactionTests
{
    private static readonly X12Specification NestedSpec =
        SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|O|AN|1|10|",
            "LOOP|A|O|10|0",
            "ELM|A01|O|AN|1|10|",
            "SEG|X1|O|10",
            "ELM|X101|O|AN|1|10|",
            "NLOOP|B|O|10|0|A01",
            "ELM|B01|O|AN|1|10|",
            "SEG|Y1|O|10",
            "ELM|Y101|O|AN|1|10|",
            "END",
            "SEG|A9|O|10",
            "ELM|A901|O|AN|1|10|",
            "END",
            "LOOP|C|O|10|0",
            "ELM|C01|O|AN|1|10|",
            "SEG|Z1|O|10",
            "ELM|Z101|O|AN|1|10|",
            "END",
            "SEG|TRL|O|1",
            "ELM|TRL01|O|AN|1|10|"
        ]);

    [Fact]
    public void Structure_pass_builds_loop_and_nested_loop_hierarchy()
    {
        var raw = new X12Types.X12_ST("ST*999*0001~HDR*H~A*1~X1*foo~B*b1~Y1*y~A9*z~C*c1~Z1*q~TRL*t~SE*11*0001~");

        var res = STTransactionParser.Parse(raw, NestedSpec);

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        Assert.Equal(["HDR", "A", "C", "TRL"], tx.Segments.Children.Select(c => c.Id));

        var aLoop = Assert.IsType<LogicalNode>(tx.Segments.Children[1]);
        Assert.Equal(["X1", "B", "A9"], aLoop.Children.Select(c => c.Id));

        var bLoop = Assert.IsType<LogicalNode>(aLoop.Children[1]);
        Assert.Equal(["Y1"], bLoop.Children.Select(c => c.Id));
    }

    [Fact]
    public void Loop_ends_on_sibling_loop_start()
    {
        var raw = new X12Types.X12_ST("ST*999*0002~HDR*H~A*1~X1*first~C*c1~Z1*z~TRL*t~SE*8*0002~");

        var res = STTransactionParser.Parse(raw, NestedSpec);

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        Assert.Equal(["HDR", "A", "C", "TRL"], tx.Segments.Children.Select(c => c.Id));
        var aLoop = Assert.IsType<LogicalNode>(tx.Segments.Children[1]);
        Assert.Equal(["X1"], aLoop.Children.Select(c => c.Id));
    }

    [Fact]
    public void Repeated_loop_start_self_terminates_prior_instance()
    {
        var raw = new X12Types.X12_ST("ST*999*0003~HDR*H~A*1~X1*first~A*2~X1*second~TRL*t~SE*8*0003~");

        var res = STTransactionParser.Parse(raw, NestedSpec);

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        var aLoops = tx.Segments.Children.OfType<LogicalNode>().Where(l => l.Id == "A").ToArray();
        Assert.Equal(2, aLoops.Length);
        Assert.Equal(["X1"], aLoops[0].Children.Select(c => c.Id));
        Assert.Equal(["X1"], aLoops[1].Children.Select(c => c.Id));
    }

    [Fact]
    public void Fails_when_se_is_missing()
    {
        var raw = new X12Types.X12_ST("ST*999*0004~HDR*H~A*1~X1*x~");

        var res = STTransactionParser.Parse(raw, NestedSpec);

        Assert.False(res.Validation.Passed);
        Assert.Null(res.Transaction);
        Assert.Contains(res.Validation.Diagnostics, d => d is MissingSegmentDiagnostic msd && msd.SegmentId == "SE");
    }

    [Fact]
    public void Fails_structural_pass_when_hl_hierarchy_is_invalid()
    {
        var hlSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "LOOP|HL|O|200|0",
            "ELM|HL01|M|AN|1|10|",
            "ELM|HL02|O|AN|1|10|",
            "ELM|HL03|M|ID|1|2|",
            "END"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0005~HL*1**S~HL*2*9*O~SE*4*0005~");
        var res = STTransactionParser.Parse(raw, hlSpec);

        Assert.False(res.Validation.Passed);
        Assert.Null(res.Transaction);
        Assert.Contains(res.Validation.Diagnostics, d => d is TransactionSyntaxDiagnostic tsd && tsd.Error == "StructuralError");
    }

    [Fact]
    public void ParseTransaction_wiring_includes_semantic_validation_diagnostics()
    {
        var semanticSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "CODE|HDR01|AA,BB"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0100~HDR*ZZ~SE*3*0100~");
        var res = X12Parser.ParseTransaction(raw, semanticSpec);

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is InvalidElementCodeDiagnostic { SegmentId: "HDR", ElementIndex: 1 });
    }

    [Fact]
    public void Emits_InvalidElementCodeDiagnostic_with_location()
    {
        var semanticSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|ID|2|2|HDR01",
            "CODE|HDR01|AA,BB"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0100~HDR*ZZ~SE*3*0100~");
        var res = X12Parser.ParseTransaction(raw, semanticSpec);

        var d = Assert.Single(res.Validation.Diagnostics.OfType<InvalidElementCodeDiagnostic>());
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void Emits_InvalidElementTypeDiagnostic_with_location()
    {
        var semanticSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|N0|1|3|"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0101~HDR*AB~SE*3*0101~");
        var res = X12Parser.ParseTransaction(raw, semanticSpec);

        var d = Assert.Single(res.Validation.Diagnostics.OfType<InvalidElementTypeDiagnostic>());
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void Emits_InvalidElementLengthDiagnostic_with_location()
    {
        var semanticSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|2|2|"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0102~HDR*A~SE*3*0102~");
        var res = X12Parser.ParseTransaction(raw, semanticSpec);

        var d = Assert.Single(res.Validation.Diagnostics.OfType<InvalidElementLengthDiagnostic>());
        Assert.Equal(1, d.Location.ElementIndex);
        Assert.True(d.Location.RawOffset >= 0);
    }

    [Fact]
    public void Emits_LoopRepeatExceeded_when_loop_max_occurs_is_exceeded()
    {
        var loopLimitedSpec = SpecPack.Unpack(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "LOOP|A|O|1|0",
            "SEG|X1|O|10",
            "END",
            "SEG|TRL|O|1"
        ]);

        var raw = new X12Types.X12_ST("ST*999*0200~HDR*H~A*1~X1*first~A*2~X1*second~TRL*t~SE*8*0200~");
        var res = X12Parser.ParseTransaction(raw, loopLimitedSpec);

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is LoopRepeatExceeded lre && lre.Actual > lre.MaxAllowed && lre.LoopId == "A");
    }
}
