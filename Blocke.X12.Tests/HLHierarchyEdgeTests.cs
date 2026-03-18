using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class HLHierarchyEdgeTests
{
    private static readonly X12Specification HlSpec =
        SpecPack.Unpack(
        [
            "X|856|005010",
            "SEG|HDR|O|1",
            "ELM|HDR01|O|AN|1|10|",
            "LOOP|HL|O|200|0",
            "ELM|HL01|M|AN|1|10|",
            "ELM|HL02|O|AN|1|10|",
            "ELM|HL03|M|ID|1|2|HL03",
            "SEG|LIN|O|20",
            "ELM|LIN01|O|AN|1|10|",
            "END",
            "SEG|CTT|O|1",
            "ELM|CTT01|O|N0|1|6|",
            "CODE|HL03|S,O,P,I"
        ]);

    private static ParsedTransactionValidationResult Parse(string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), HlSpec);

    [Fact]
    public void Multi_level_hl_hierarchy_validates_when_spec_shape_and_se_count_are_correct()
    {
        var res = Parse("ST*856*5001~HDR*H~HL*1**S~HL*2*1*O~HL*3*2*I~LIN*sku~CTT*1~SE*8*5001~");

        Assert.True(res.Validation.Passed);
        Assert.Empty(res.Validation.Diagnostics);

        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        var hls = tx.Segments.Children.OfType<LogicalNode>().Where(l => l.Id == "HL").ToArray();
        Assert.Single(hls);
        Assert.Equal("S", hls[0].Fields.First(f => f.Id == "HL03").Value);

        var child = Assert.Single(hls[0].Children.OfType<LogicalNode>().Where(l => l.Id == "HL"));
        Assert.Equal("O", child.Fields.First(f => f.Id == "HL03").Value);
    }

    [Fact]
    public void Multiple_hl_roots_are_preserved_as_sibling_hl_logical_nodes()
    {
        var res = Parse("ST*856*5002~HL*1**S~HL*2**S~SE*4*5002~");

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        Assert.Equal(2, tx.Segments.Children.OfType<LogicalNode>().Count(l => l.Id == "HL"));
    }

    [Fact]
    public void Duplicate_hl01_values_duplicate_the_last_matching_child_record_under_the_parent()
    {
        var res = Parse("ST*856*5003~HL*1**S~HL*2*1*O~HL*2*1*I~SE*5*5003~");

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        var root = Assert.Single(tx.Segments.Children.OfType<LogicalNode>().Where(l => l.Id == "HL"));
        var childHls = root.Children.OfType<LogicalNode>().Where(l => l.Id == "HL").ToArray();
        Assert.Equal(2, childHls.Length);
        Assert.Equal(["I", "I"], childHls.Select(h => h.Fields.First(f => f.Id == "HL03").Value));
    }

    [Fact]
    public void Hl_terminator_segment_ends_hl_area_and_keeps_following_segments_at_transaction_scope()
    {
        var res = Parse("ST*856*5004~HDR*H~HL*1**S~LIN*sku~CTT*1~SE*6*5004~");

        Assert.True(res.Validation.Passed);
        var tx = Assert.IsType<X12Transaction>(res.Transaction);
        Assert.Equal(["HDR", "HL", "CTT"], tx.Segments.Children.Select(c => c.Id));
        var hl = Assert.IsType<LogicalNode>(tx.Segments.Children[1]);
        Assert.Equal(["LIN"], hl.Children.Select(c => c.Id));
    }
}
