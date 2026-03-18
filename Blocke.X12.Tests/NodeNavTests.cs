using Blocke.X12.Models;
using Blocke.X12;

namespace Blocke.X12.Tests;

public sealed class NodeNavTests
{
    private static readonly LogicalNode Tree =
        new(
            "TX",
            [new Field("TX01", "root")],
            [
                new SegmentNode("HDR", [new Field("HDR01", "H")], new SegmentMeta(0, "HDR*H")),
                new LogicalNode(
                    "A",
                    [new Field("A01", "alpha")],
                    [
                        new SegmentNode("X1", [new Field("X101", "foo")], new SegmentMeta(1, "X1*foo")),
                        new HLRef("2"),
                        new LogicalNode(
                            "B",
                            [new Field("B01", "beta")],
                            [new SegmentNode("Y1", [new Field("Y101", "bar")], new SegmentMeta(2, "Y1*bar"))],
                            new LogicalMeta(LogicalOrigin.LoopDefined, HLRole.Order, 2))
                    ],
                    new LogicalMeta(LogicalOrigin.LoopDefined, HLRole.Shipment, 1)),
                new SegmentNode("TRL", [new Field("TRL01", "T")], new SegmentMeta(3, "TRL*T"))
            ],
            new LogicalMeta(LogicalOrigin.SpecDefined));

    [Fact]
    public void FlattenedNodes_returns_depth_first_nodes_excluding_hlref_markers()
    {
        Assert.Equal(["HDR", "A", "X1", "B", "Y1", "TRL"], NodeNav.FlattenedNodes(Tree).Select(n => n.Id));
    }

    [Fact]
    public void FlattenedLogicalNodes_returns_nested_logical_nodes_only()
    {
        Assert.Equal(["A", "B"], NodeNav.FlattenedLogicalNodes(Tree).Select(n => n.Id));
    }

    [Fact]
    public void FlattenedSegments_returns_nested_segments_only()
    {
        Assert.Equal(["HDR", "X1", "Y1", "TRL"], NodeNav.FlattenedSegments(Tree).Select(n => n.Id));
    }

    [Fact]
    public void DirectSegments_and_directLogical_stay_at_immediate_child_scope()
    {
        Assert.Equal(["HDR", "TRL"], NodeNav.DirectSegments(Tree).Select(n => n.Id));
        Assert.Equal(["A"], NodeNav.DirectLogical(Tree).Select(n => n.Id));
    }

    [Fact]
    public void FindFirstSegment_and_findSegments_search_recursively()
    {
        Assert.Equal("bar", NodeNav.FindFirstSegment(Tree, "Y1")?.Field("Y101"));
        Assert.Equal(["X1"], NodeNav.FindSegments(Tree, "X1").Select(n => n.Id));
    }

    [Fact]
    public void FindLogical_can_filter_by_id_and_optional_hl_role()
    {
        Assert.Equal(["A"], NodeNav.FindLogical(Tree, "A").Select(n => n.Id));
        Assert.Equal(["A"], NodeNav.FindLogical(Tree, "A", HLRole.Shipment).Select(n => n.Id));
        Assert.Empty(NodeNav.FindLogical(Tree, "A", HLRole.Order));
        Assert.Equal(["B"], NodeNav.FindLogical(Tree, "B", HLRole.Order).Select(n => n.Id));
    }

    [Fact]
    public void FindFirstField_reads_only_local_node_fields_while_findFirstFieldDeep_descends()
    {
        Assert.Null(NodeNav.FindFirstField(Tree, "Y101"));
        Assert.Equal("bar", NodeNav.FindFirstFieldDeep(Tree, "Y101"));
        Assert.Equal("alpha", NodeNav.FindFirstFieldDeep(Tree, "A01"));
        Assert.Null(NodeNav.FindFirstFieldDeep(new HLRef("1"), "A01"));
    }

    [Fact]
    public void FindSegmentWhere_and_findSegmentsWhere_search_only_immediate_children()
    {
        Assert.Equal("HDR", NodeNav.FindSegmentWhere(Tree, "HDR", "HDR01", "H")?.Id);
        Assert.Null(NodeNav.FindSegmentWhere(Tree, "Y1", "Y101", "bar"));
        Assert.Equal(["TRL"], NodeNav.FindSegmentsWhere(Tree, "TRL", "TRL01", "T").Select(n => n.Id));
    }

    [Fact]
    public void FindSegmentBy_searches_recursively_using_custom_predicate()
    {
        Assert.Equal("Y1", NodeNav.FindSegmentBy(Tree, "Y1", seg => seg.Field("Y101") == "bar")?.Id);
        Assert.Null(NodeNav.FindSegmentBy(Tree, "Y1", seg => seg.Field("Y101") == "nope"));
    }
}
