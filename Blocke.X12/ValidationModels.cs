namespace Blocke.X12.Models;

public sealed record TransactionValidationResult(
    string TransactionSetId,
    string ControlNumber,
    bool Passed,
    IReadOnlyList<object> Diagnostics);

public sealed record ParsedTransactionValidationResult(
    X12Transaction? Transaction,
    TransactionValidationResult Validation);

public sealed record ValidationResult(
    IReadOnlyList<TransactionValidationResult> Transactions)
{
    public bool Passed => Transactions.All(t => t.Passed);

    public IReadOnlyList<object> Diagnostics => Transactions.SelectMany(t => t.Diagnostics).ToArray();
}

public sealed record DiagnosticLocation(
    int SegmentIndex = -1,
    int? ElementIndex = null,
    int RawOffset = -1,
    int RawLength = 0)
{
    public static DiagnosticLocation Unknown => new();

    public static DiagnosticLocation AtSegment(int segmentIndex, int rawOffset = -1, int rawLength = 0) =>
        new(segmentIndex, null, rawOffset, rawLength);

    public static DiagnosticLocation AtElement(int segmentIndex, int elementIndex, int rawOffset = -1, int rawLength = 0) =>
        new(segmentIndex, elementIndex, rawOffset, rawLength);
}

public abstract record ValidationDiagnostic(IReadOnlyList<LoopCtx> Path, DiagnosticLocation Location)
{
    public string Message => DiagnosticFormatter.Render(this);
}

public sealed record MissingSegmentDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ExpectedPosition { get; init; }

    public MissingSegmentDiagnostic(string segmentId, DiagnosticLocation location) : this([], segmentId, -1, location) { }
    public MissingSegmentDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int expectedPosition, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ExpectedPosition = expectedPosition;
    }
}

public sealed record UnexpectedSegmentDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }

    public UnexpectedSegmentDiagnostic(string segmentId, DiagnosticLocation location) : this([], segmentId, location) { }
    public UnexpectedSegmentDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, DiagnosticLocation location) : base(path, location) =>
        SegmentId = segmentId;
}

public sealed record InvalidElementDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ElementIndex { get; init; }
    public ElementSyntaxError Error { get; init; }

    public InvalidElementDiagnostic(string segmentId, int elementIndex, ElementSyntaxError error, DiagnosticLocation location) : this([], segmentId, elementIndex, error, location) { }
    public InvalidElementDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int elementIndex, ElementSyntaxError error, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementIndex = elementIndex;
        Error = error;
    }
}

public sealed record MissingElementDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ElementIndex { get; init; }

    public MissingElementDiagnostic(string segmentId, int elementIndex, DiagnosticLocation location) : this([], segmentId, elementIndex, location) { }
    public MissingElementDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int elementIndex, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementIndex = elementIndex;
    }
}

public sealed record InvalidElementTypeDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ElementIndex { get; init; }
    public X12Type ExpectedType { get; init; }
    public string ActualValue { get; init; }

    public InvalidElementTypeDiagnostic(string segmentId, int elementIndex, X12Type expectedType, string actualValue, DiagnosticLocation location) : this([], segmentId, elementIndex, expectedType, actualValue, location) { }
    public InvalidElementTypeDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int elementIndex, X12Type expectedType, string actualValue, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementIndex = elementIndex;
        ExpectedType = expectedType;
        ActualValue = actualValue;
    }
}

public sealed record InvalidElementLengthDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ElementIndex { get; init; }
    public int ActualLength { get; init; }
    public int MinLength { get; init; }
    public int MaxLength { get; init; }

    public InvalidElementLengthDiagnostic(string segmentId, int elementIndex, int actualLength, int minLength, int maxLength, DiagnosticLocation location) : this([], segmentId, elementIndex, actualLength, minLength, maxLength, location) { }
    public InvalidElementLengthDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int elementIndex, int actualLength, int minLength, int maxLength, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementIndex = elementIndex;
        ActualLength = actualLength;
        MinLength = minLength;
        MaxLength = maxLength;
    }
}

public sealed record InvalidElementCodeDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int ElementIndex { get; init; }
    public string Value { get; init; }

    public InvalidElementCodeDiagnostic(string segmentId, int elementIndex, string value, DiagnosticLocation location) : this([], segmentId, elementIndex, value, location) { }
    public InvalidElementCodeDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, int elementIndex, string value, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementIndex = elementIndex;
        Value = value;
    }
}

public sealed record EmptySegmentSpecDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }

    public EmptySegmentSpecDiagnostic(string segmentId, DiagnosticLocation location) : this([], segmentId, location) { }
    public EmptySegmentSpecDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, DiagnosticLocation location) : base(path, location) =>
        SegmentId = segmentId;
}

public sealed record IdElementMissingCodeListDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public string ElementId { get; init; }

    public IdElementMissingCodeListDiagnostic(string segmentId, string elementId, DiagnosticLocation location) : this([], segmentId, elementId, location) { }
    public IdElementMissingCodeListDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, string elementId, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        ElementId = elementId;
    }
}

public sealed record SegmentRepeatExceeded : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public int Actual { get; init; }
    public int MaxAllowed { get; init; }

    public SegmentRepeatExceeded(string segmentId, int actual, int maxAllowed, DiagnosticLocation location) : this([], segmentId, actual, maxAllowed, location) { }
    public SegmentRepeatExceeded(IReadOnlyList<LoopCtx> path, string segmentId, int actual, int maxAllowed, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        Actual = actual;
        MaxAllowed = maxAllowed;
    }
}

public sealed record LoopRepeatExceeded : ValidationDiagnostic
{
    public string LoopId { get; init; }
    public int Actual { get; init; }
    public int MaxAllowed { get; init; }

    public LoopRepeatExceeded(string loopId, int actual, int maxAllowed, DiagnosticLocation location) : this([], loopId, actual, maxAllowed, location) { }
    public LoopRepeatExceeded(IReadOnlyList<LoopCtx> path, string loopId, int actual, int maxAllowed, DiagnosticLocation location) : base(path, location)
    {
        LoopId = loopId;
        Actual = actual;
        MaxAllowed = maxAllowed;
    }
}

public sealed record RuleViolationDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public string RuleId { get; init; }
    public string Severity { get; init; }

    public RuleViolationDiagnostic(string segmentId, string ruleId, string severity, DiagnosticLocation location) : this([], segmentId, ruleId, severity, location) { }
    public RuleViolationDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, string ruleId, string severity, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        RuleId = ruleId;
        Severity = severity;
    }
}

public sealed record TransactionSyntaxDiagnostic : ValidationDiagnostic
{
    public string Error { get; init; }

    public TransactionSyntaxDiagnostic(string error, DiagnosticLocation location) : this([], error, location) { }
    public TransactionSyntaxDiagnostic(TransactionSyntaxError error, DiagnosticLocation location) : this([], error, location) { }
    public TransactionSyntaxDiagnostic(IReadOnlyList<LoopCtx> path, string error, DiagnosticLocation location) : base(path, location) =>
        Error = error;
    public TransactionSyntaxDiagnostic(IReadOnlyList<LoopCtx> path, TransactionSyntaxError error, DiagnosticLocation location) : base(path, location) =>
        Error = error.ToString();
}

public sealed record ElementSyntaxDiagnostic : ValidationDiagnostic
{
    public string SegmentId { get; init; }
    public ElementSyntaxError Error { get; init; }
    public string? ElementValue { get; init; }
    public string? SegmentSource { get; init; }

    public ElementSyntaxDiagnostic(string segmentId, ElementSyntaxError error, string? elementValue, string? segmentSource, DiagnosticLocation location) : this([], segmentId, error, elementValue, segmentSource, location) { }
    public ElementSyntaxDiagnostic(IReadOnlyList<LoopCtx> path, string segmentId, ElementSyntaxError error, string? elementValue, string? segmentSource, DiagnosticLocation location) : base(path, location)
    {
        SegmentId = segmentId;
        Error = error;
        ElementValue = elementValue;
        SegmentSource = segmentSource;
    }
}
