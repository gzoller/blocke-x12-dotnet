using Blocke.X12.Models;

namespace Blocke.X12;

public static class DiagnosticFormatter
{
    public static string Render(object diagnostic) =>
        diagnostic switch
        {
            MissingSegmentDiagnostic x => $"{Where(x.Location)} Missing required segment {x.SegmentId}. {Action($"Insert {x.SegmentId} at the expected position")}".Trim(),
            UnexpectedSegmentDiagnostic x => $"{Where(x.Location)} Unexpected segment {x.SegmentId}. {Action($"Remove {x.SegmentId} or update loop/segment order in the spec")}".Trim(),
            MissingElementDiagnostic x => $"{Where(x.Location)} {x.SegmentId}{Elem(x.ElementIndex)} is required but missing. {Action("Provide the missing element value")}".Trim(),
            InvalidElementTypeDiagnostic x => $"{Where(x.Location)} {x.SegmentId}{Elem(x.ElementIndex)} value '{x.ActualValue}' has wrong type (expected {x.ExpectedType}). {Action("Use a value matching the expected X12 type")}".Trim(),
            InvalidElementLengthDiagnostic x => $"{Where(x.Location)} {x.SegmentId}{Elem(x.ElementIndex)} length {x.ActualLength} is out of bounds (expected {x.MinLength}-{x.MaxLength}). {Action("Adjust element length to fit the allowed range")}".Trim(),
            InvalidElementCodeDiagnostic x => $"{Where(x.Location)} {x.SegmentId}{Elem(x.ElementIndex)} code '{x.Value}' is not allowed by the spec. {Action("Use one of the allowed code values for this element")}".Trim(),
            EmptySegmentSpecDiagnostic x => $"{Where(x.Location)} Spec definition for segment {x.SegmentId} has no elements. {Action("Define at least one ELM for this segment/loop in the spec")}".Trim(),
            IdElementMissingCodeListDiagnostic x => $"{Where(x.Location)} Spec definition {x.SegmentId}.{x.ElementId} is ID type but has no code list reference. {Action("Attach a CODE list key to this ID element and provide CODE values")}".Trim(),
            SegmentRepeatExceeded x => $"{Where(x.Location)} Segment {x.SegmentId} repeats {x.Actual} times (max {x.MaxAllowed}). {Action($"Reduce {x.SegmentId} repeats or increase maxUse in spec if intentional")}".Trim(),
            LoopRepeatExceeded x => $"{Where(x.Location)} Loop {x.LoopId} repeats {x.Actual} times (max {x.MaxAllowed}). {Action($"Reduce {x.LoopId} loop occurrences or adjust loop maxUse")}".Trim(),
            RuleViolationDiagnostic x => $"{Where(x.Location)} Rule '{x.RuleId}' violated in segment {x.SegmentId} ({x.Severity}). {Action("Review the rule condition and update the segment values")}".Trim(),
            InvalidElementDiagnostic x => $"{Where(x.Location)} {x.SegmentId}{Elem(x.ElementIndex)} is invalid ({x.Error}). {Action("Correct the element value to satisfy X12 syntax rules")}".Trim(),
            TransactionSyntaxDiagnostic x => $"{Where(x.Location)} Transaction structure error: {x.Error}. {Action("Check segment order, required loops, and control counts against the spec")}".Trim(),
            ElementSyntaxDiagnostic x => $"{Where(x.Location)} Segment {x.SegmentId} has element syntax error {x.Error}. {Action("Correct the offending element value in the segment")}".Trim(),
            _ => diagnostic.ToString() ?? string.Empty
        };

    private static string Where(DiagnosticLocation loc)
    {
        var seg = loc.SegmentIndex >= 0 ? $"At segment #{loc.SegmentIndex}" : "At unknown location";
        var elem = loc.ElementIndex.HasValue ? $", element #{loc.ElementIndex.Value}" : string.Empty;
        var raw = loc.RawOffset >= 0 ? $" (raw offset {loc.RawOffset})" : string.Empty;
        return $"{seg}{elem}{raw}.";
    }

    private static string Elem(int index) => $" element #{index}";
    private static string Action(string text) => $"Action: {text}.";
}
