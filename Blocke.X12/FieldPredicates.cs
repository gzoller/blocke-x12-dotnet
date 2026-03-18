using Blocke.X12.Models;

namespace Blocke.X12;

public abstract record FieldPredicate
{
    public abstract bool Matches(SegmentNode segment);
}

public sealed record FieldEquals(X12FieldRef Ref, string Value) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) =>
        segment.Fields.Any(f => f.Id == Ref.Element && f.Value == Value);
}

public sealed record FieldExists(X12FieldRef Ref) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) =>
        segment.Fields.Any(f => f.Id == Ref.Element && !string.IsNullOrEmpty(f.Value));
}

public sealed record FieldInSet(X12FieldRef Ref, IReadOnlySet<string> Values) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) =>
        segment.Fields.Any(f => f.Id == Ref.Element && Values.Contains(f.Value));
}

public sealed record And(IReadOnlyList<FieldPredicate> Predicates) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) => Predicates.All(p => p.Matches(segment));
}

public sealed record Or(IReadOnlyList<FieldPredicate> Predicates) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) => Predicates.Any(p => p.Matches(segment));
}

public sealed record Xor(IReadOnlyList<FieldPredicate> Predicates) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) => Predicates.Count(p => p.Matches(segment)) == 1;
}

public sealed record Not(FieldPredicate Predicate) : FieldPredicate
{
    public override bool Matches(SegmentNode segment) => !Predicate.Matches(segment);
}
