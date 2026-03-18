namespace Blocke.X12.Models;

public sealed record X12Transaction(
    string TransactionSetId,
    string ControlNumber,
    LogicalNode Segments);

public abstract record Node(string Id, IReadOnlyList<Field> Fields);

public sealed record SegmentNode(
    string Id,
    IReadOnlyList<Field> Fields,
    SegmentMeta Meta) : Node(Id, Fields);

public sealed record LogicalNode(
    string Id,
    IReadOnlyList<Field> Fields,
    IReadOnlyList<Node> Children,
    LogicalMeta Meta) : Node(Id, Fields);

public sealed record HLRef(string Id) : Node(Id, Array.Empty<Field>());

public sealed record Field(string Id, string Value);

public sealed record SegmentMeta(int Position, string Source);

public enum LogicalOrigin
{
    ParserInferred,
    LoopDefined,
    SpecDefined
}

public sealed record LogicalMeta(LogicalOrigin Origin, HLRole? Role = null, int Position = -1);
