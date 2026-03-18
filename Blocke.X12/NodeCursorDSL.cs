using Blocke.X12.Models;

namespace Blocke.X12;

public static class NodeCursorDSL
{
    public sealed record Cursor<T>(IReadOnlyList<T> Nodes);

    public static Cursor<Node> Nav(this LogicalNode root) => new([root]);

    public static T? First<T>(this Cursor<T> cursor) where T : class =>
        cursor.Nodes.FirstOrDefault();

    public static Cursor<Node> Children(this Cursor<Node> cursor) =>
        new(cursor.Nodes.OfType<LogicalNode>().SelectMany(l => l.Children).ToArray());

    public static Cursor<Node> Segments(this Cursor<Node> cursor, string id) =>
        new(cursor.Nodes.OfType<LogicalNode>().SelectMany(l => l.Children.Where(c => c.Id == id)).ToArray());

    public static Cursor<Node> Where(this Cursor<Node> cursor, Func<Node, bool> predicate) =>
        new(cursor.Nodes.Where(predicate).ToArray());

    public static IReadOnlyList<string?>? Field(this Cursor<Node> cursor, string id) =>
        cursor.Nodes.Count == 0 ? null : cursor.Nodes.Select(n => n.Field(id)).ToArray();

    public static Cursor<Node> SegmentsWhere(this Cursor<Node> cursor, string segId, string fieldId, string value) =>
        cursor.Segments(segId).Where(n => n.Field(fieldId) == value);

    public static IReadOnlyList<string?> SelectFields(this Node node, params string[] ids) =>
        node is null ? ids.Select(_ => (string?)null).ToArray() : ids.Select(node.Field).ToArray();

    public static string? Field(this Node node, string id) =>
        node is null ? null : node.Fields.FirstOrDefault(f => f.Id == id)?.Value;
}
