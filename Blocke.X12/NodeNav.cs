using Blocke.X12.Models;

namespace Blocke.X12;

public static class NodeNav
{
    public static IReadOnlyList<Node> FlattenedNodes(LogicalNode logical) =>
        logical.Children.SelectMany(child => child switch
        {
            SegmentNode seg => [seg],
            LogicalNode nested => ((IReadOnlyList<Node>)[nested]).Concat(FlattenedNodes(nested)).ToArray(),
            HLRef => Array.Empty<Node>(),
            _ => Array.Empty<Node>()
        }).ToArray();

    public static IReadOnlyList<LogicalNode> FlattenedLogicalNodes(LogicalNode root) =>
        FlattenedNodes(root).OfType<LogicalNode>().ToArray();

    public static IReadOnlyList<SegmentNode> FlattenedSegments(LogicalNode root) =>
        FlattenedNodes(root).OfType<SegmentNode>().ToArray();

    public static IReadOnlyList<SegmentNode> DirectSegments(LogicalNode root) =>
        root.Children.OfType<SegmentNode>().ToArray();

    public static IReadOnlyList<LogicalNode> DirectLogical(LogicalNode root) =>
        root.Children.OfType<LogicalNode>().ToArray();

    public static SegmentNode? FindFirstSegment(LogicalNode root, string id) =>
        FlattenedSegments(root).FirstOrDefault(s => s.Id == id);

    public static IReadOnlyList<SegmentNode> FindSegments(LogicalNode root, string id) =>
        FlattenedSegments(root).Where(s => s.Id == id).ToArray();

    public static IReadOnlyList<LogicalNode> FindLogical(LogicalNode root, string id, HLRole? role = null) =>
        FlattenedLogicalNodes(root).Where(l => l.Id == id && (!role.HasValue || l.Meta.Role == role)).ToArray();

    public static string? FindFirstField(Node node, string fieldId) =>
        node.Fields.FirstOrDefault(f => f.Id == fieldId)?.Value;

    public static string? FindFirstFieldDeep(Node node, string fieldId) =>
        node switch
        {
            SegmentNode seg => FindFirstField(seg, fieldId),
            LogicalNode logical => FindFirstField(logical, fieldId) ?? logical.Children.Select(child => FindFirstFieldDeep(child, fieldId)).FirstOrDefault(v => v is not null),
            _ => null
        };

    public static Node? FindSegmentWhere(LogicalNode root, string segmentId, string fieldId, string value) =>
        root.Children.FirstOrDefault(seg => seg.Id == segmentId && FindFirstField(seg, fieldId) == value);

    public static IReadOnlyList<Node> FindSegmentsWhere(LogicalNode root, string segmentId, string fieldId, string value) =>
        root.Children.Where(seg => seg.Id == segmentId && FindFirstField(seg, fieldId) == value).ToArray();

    public static SegmentNode? FindSegmentBy(LogicalNode root, string segmentId, Func<SegmentNode, bool> predicate) =>
        FlattenedSegments(root).FirstOrDefault(seg => seg.Id == segmentId && predicate(seg));
}
