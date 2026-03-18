namespace Blocke.X12.Models;

public sealed record X12Specification(
    string TransactionSetId,
    string Version,
    IReadOnlyList<NodeSpec> Root,
    IReadOnlyDictionary<string, IReadOnlySet<string>> CodeLists,
    string? CttCountSegmentId = null);

public interface IX12SpecificationProvider
{
    X12Specification SpecData { get; }
}

public static class X12SpecificationHelpers
{
    public static ElementSpec E(
        string id,
        X12Type type,
        int min,
        int max,
        Presence presence = Presence.Required,
        string? codeListRef = null) =>
        new(id, presence, type, min, max, codeListRef);

    public static IReadOnlyList<NodeSpec> ReplaceSegment(
        IReadOnlyList<NodeSpec> body,
        string id,
        SegmentNodeSpec replacement) =>
        body.Select(node => node is SegmentNodeSpec seg && seg.Id == id ? replacement : node).ToArray();
}

public abstract record NodeSpec(string Id, Presence Presence, int MaxOccurs, IReadOnlyList<ElementSpec> Elements, IReadOnlyList<RuleSpec> Rules);

public abstract record LoopNodeSpec(
    string Id,
    Presence Presence,
    int MaxOccurs,
    int MinOccurs,
    IReadOnlyList<NodeSpec> Body,
    IReadOnlyList<ElementSpec> Elements,
    IReadOnlyList<RuleSpec> Rules) : NodeSpec(Id, Presence, MaxOccurs, Elements, Rules);

public sealed record SegmentNodeSpec(
    string Id,
    Presence Presence,
    int MaxOccurs,
    IReadOnlyList<ElementSpec> Elements,
    IReadOnlyList<RuleSpec> Rules) : NodeSpec(Id, Presence, MaxOccurs, Elements, Rules);

public sealed record LoopSegmentSpec(
    string Id,
    Presence Presence,
    int MaxOccurs,
    int MinOccurs,
    IReadOnlyList<NodeSpec> Body,
    IReadOnlyList<ElementSpec> Elements,
    IReadOnlyList<RuleSpec> Rules) : LoopNodeSpec(Id, Presence, MaxOccurs, MinOccurs, Body, Elements, Rules);

public sealed record NestedLoopSegmentSpec(
    string Id,
    Presence Presence,
    int MaxOccurs,
    int MinOccurs,
    string NestingKey,
    IReadOnlyList<NodeSpec> Body,
    IReadOnlyList<ElementSpec> Elements,
    IReadOnlyList<RuleSpec> Rules) : LoopNodeSpec(Id, Presence, MaxOccurs, MinOccurs, Body, Elements, Rules);

public sealed record ElementSpec(
    string Id,
    Presence Presence,
    X12Type DataType,
    int MinLength,
    int MaxLength,
    string? CodeListRef);

public sealed record RuleSpec(string Severity, string Id, FieldPredicateSpec Must, FieldPredicateSpec? When = null);

public abstract record FieldPredicateSpec;

public abstract record X12FieldRef(string Segment, string Element);

public sealed record SimpleFieldRef(string Segment, string Element) : X12FieldRef(Segment, Element);

public sealed record CompositeFieldRef(string Segment, string Element, string Component) : X12FieldRef(Segment, Element);

public sealed record EqualsPredicate(string Field, string Value) : FieldPredicateSpec;

public sealed record ExistsPredicate(string Field) : FieldPredicateSpec;

public sealed record InSetPredicate(string Field, IReadOnlySet<string> Values) : FieldPredicateSpec;

public sealed record AndPredicate(IReadOnlyList<FieldPredicateSpec> Predicates) : FieldPredicateSpec;

public sealed record OrPredicate(IReadOnlyList<FieldPredicateSpec> Predicates) : FieldPredicateSpec;

public sealed record XorPredicate(IReadOnlyList<FieldPredicateSpec> Predicates) : FieldPredicateSpec;

public sealed record NotPredicate(FieldPredicateSpec Predicate) : FieldPredicateSpec;

public enum Presence
{
    Required,
    Optional,
    Conditional,
    Relational,
    Forbidden
}

public static class PresenceCodec
{
    public static Presence Decode(string value) =>
        value switch
        {
            "M" => Presence.Required,
            "O" => Presence.Optional,
            "C" => Presence.Conditional,
            "R" => Presence.Relational,
            "X" => Presence.Forbidden,
            _ => throw new ArgumentException($"Unknown Presence code: {value}", nameof(value))
        };
}

public enum X12Type
{
    ID,
    AN,
    N0,
    N1,
    N2,
    DT,
    TM,
    R,
    B
}

public static class X12TypeCodec
{
    public static ElementSyntaxError? Validate(this X12Type type, string value) =>
        type switch
        {
            X12Type.AN or X12Type.ID when string.IsNullOrEmpty(value) => ElementSyntaxError.MandatoryElementMissing,
            X12Type.AN or X12Type.ID when value.Any(ch => ch < ' ' || ch > '~') => ElementSyntaxError.InvalidCharacter,
            X12Type.AN or X12Type.ID => null,
            X12Type.N0 or X12Type.N1 or X12Type.N2 when string.IsNullOrEmpty(value) => ElementSyntaxError.MandatoryElementMissing,
            X12Type.N0 or X12Type.N1 or X12Type.N2 when value.Any(ch => !char.IsDigit(ch)) => ElementSyntaxError.InvalidNumericValue,
            X12Type.N0 or X12Type.N1 or X12Type.N2 => null,
            X12Type.R when !System.Text.RegularExpressions.Regex.IsMatch(value, @"^-?\d+(\.\d+)?$") => ElementSyntaxError.InvalidNumericValue,
            X12Type.R => null,
            X12Type.DT when !System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{8}$") => ElementSyntaxError.InvalidDate,
            X12Type.DT when !DateOnly.TryParseExact(value, "yyyyMMdd", out _) => ElementSyntaxError.InvalidDate,
            X12Type.DT => null,
            X12Type.TM when value.Any(ch => !char.IsDigit(ch)) => ElementSyntaxError.InvalidCharacter,
            X12Type.TM when !System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}(\d{2})?$") => ElementSyntaxError.InvalidTime,
            X12Type.TM => null,
            X12Type.B when string.IsNullOrEmpty(value) => ElementSyntaxError.MandatoryElementMissing,
            X12Type.B when value.Any(ch => ch < ' ' || ch > '~') => ElementSyntaxError.InvalidCharacter,
            X12Type.B => null,
            _ => null
        };

    public static X12Type Decode(string value) =>
        Enum.TryParse<X12Type>(value, ignoreCase: false, out var result)
            ? result
            : throw new ArgumentException($"Unknown X12 data type: {value}", nameof(value));

    public static X12Type FromValue(string value) => Decode(value);
}
