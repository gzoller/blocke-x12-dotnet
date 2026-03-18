using Blocke.X12.Models;
using Blocke.X12;

namespace Blocke.X12.Tests;

public sealed class NodeCursorDSLTests
{
    private static readonly LogicalNode Root =
        new(
            "TX",
            [new Field("TX01", "root")],
            [
                new SegmentNode("HDR", [new Field("HDR01", "H")], new SegmentMeta(0, "HDR*H")),
                new LogicalNode(
                    "A",
                    [new Field("A01", "a1")],
                    [
                        new SegmentNode("REF", [new Field("REF01", "DP"), new Field("REF02", "57")], new SegmentMeta(1, "REF*DP*57")),
                        new SegmentNode("REF", [new Field("REF01", "VN"), new Field("REF02", "99")], new SegmentMeta(2, "REF*VN*99"))
                    ],
                    new LogicalMeta(LogicalOrigin.LoopDefined))
            ],
            new LogicalMeta(LogicalOrigin.SpecDefined));

    [Fact]
    public void Nav_starts_with_the_root_node_and_first_returns_the_first_cursor_item()
    {
        Assert.Equal("TX", Root.Nav().First()?.Id);
    }

    [Fact]
    public void Children_returns_immediate_child_nodes_across_all_logical_nodes_in_the_cursor()
    {
        Assert.Equal(["HDR", "A"], Root.Nav().Children().Nodes.Select(n => n.Id));
        Assert.Equal(["REF", "REF"], Root.Nav().Children().Segments("REF").Nodes.Select(n => n.Id));
    }

    [Fact]
    public void Segments_filters_immediate_child_segments_by_id()
    {
        var refs = Root.Nav().Children().Where(n => n.Id == "A").Segments("REF");
        Assert.Equal(["DP", "VN"], refs.Nodes.Select(n => n.Field("REF01")));
    }

    [Fact]
    public void Where_filters_the_current_cursor_and_field_returns_aligned_field_values()
    {
        var refs = Root.Nav().Children().Where(n => n.Id == "A").Segments("REF");
        Assert.Equal(["99"], refs.Where(n => n.Field("REF01") == "VN").Field("REF02"));
    }

    [Fact]
    public void Field_returns_null_for_an_empty_cursor()
    {
        Assert.Null(Root.Nav().Children().Where(n => n.Id == "A").Children().Segments("REF").Field("REF01"));
    }

    [Fact]
    public void SegmentsWhere_composes_segments_and_field_filtering()
    {
        var cursor = Root.Nav().Children().Where(n => n.Id == "A").SegmentsWhere("REF", "REF01", "DP");
        Assert.Equal(["57"], cursor.Nodes.Select(n => n.Field("REF02")));
    }

    [Fact]
    public void Node_and_option_field_helpers_expose_scalar_and_grouped_values()
    {
        var reference = Root.Nav().Children().Where(n => n.Id == "A").SegmentsWhere("REF", "REF01", "DP").First();
        Assert.NotNull(reference);
        Assert.Equal(["DP", "57", null], reference!.SelectFields("REF01", "REF02", "REF03"));
        Assert.Equal("57", reference.Field("REF02"));
        Assert.Equal(["DP", "57"], reference.SelectFields("REF01", "REF02"));
        Assert.Null(((Node?)null).Field("REF02"));
    }
}
