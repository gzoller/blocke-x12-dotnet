namespace Blocke.X12.Models;

public sealed record Ack997(
    IReadOnlyList<Ack997Group> Groups);

public sealed record Ack997Group(
    string FunctionalId,
    string GroupControlNumber,
    string Version,
    int DeclaredTransactionCount,
    int ReceivedTransactionCount,
    AckStatus Status,
    IReadOnlyList<TransactionSetAck997> TransactionAcks,
    IReadOnlyList<string> GroupErrors);

public sealed record TransactionSetAck997(
    string TransactionSetId,
    string ControlNumber,
    AckStatus Disposition,
    IReadOnlyList<string> TransactionErrors,
    IReadOnlyList<SegmentAckError997> SegmentErrors,
    IReadOnlyList<ElementAckError997> ElementErrors);

public sealed record SegmentAckError997(
    string SegmentId,
    int SegmentPosition,
    string? LoopId,
    string ErrorCode);

public sealed record ElementAckError997(
    string SegmentId,
    int SegmentPosition,
    int ElementPosition,
    int? ComponentPosition,
    string ErrorCode,
    string? BadValue);
