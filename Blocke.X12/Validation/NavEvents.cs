using Blocke.X12.Models;

namespace Blocke.X12.Validation;

internal abstract record NavEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    int? SpecIndex,
    int? DocIndex);

internal sealed record MatchedEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    NodeSpec Spec,
    Node Node,
    int? SpecIndex,
    int? DocIndex) : NavEvent(LoopPath, SpecIndex, DocIndex);

internal sealed record MaxOccursExceededEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    string SegmentId,
    int Max,
    int Actual,
    int? SpecIndex,
    int? DocIndex) : NavEvent(LoopPath, SpecIndex, DocIndex);

internal sealed record SegmentRuleViolatedEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    string SegmentId,
    RuleSpec Rule,
    int? SpecIndex,
    int? DocIndex,
    int? SegmentPosition = null) : NavEvent(LoopPath, SpecIndex, DocIndex);

internal sealed record SkippedOptionalSpecEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    NodeSpec Spec,
    int? SpecIndex) : NavEvent(LoopPath, SpecIndex, null);

internal sealed record MissingSpecEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    NodeSpec Spec,
    int? SpecIndex) : NavEvent(LoopPath, SpecIndex, null);

internal sealed record UnexpectedNodeEvent(
    IReadOnlyList<LoopCtx> LoopPath,
    Node Node,
    int? DocIndex) : NavEvent(LoopPath, null, DocIndex);
