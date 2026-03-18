using Blocke.X12.Models;
using Blocke.X12.Parsing.Model;

namespace Blocke.X12.Validation;

internal static class TransactionValidator
{
    internal static TransactionValidationResult Validate(
        X12Transaction transaction,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensByIndex,
        IEnumerable<object>? seedDiagnostics = null,
        bool debug = false)
    {
        var diagnostics = seedDiagnostics?.ToList() ?? new List<object>();

        AddSpecShapeDiagnostics(spec.Root, diagnostics);

        var navEvents = NavWalker.Traverse(transaction.Segments, spec);
        diagnostics.AddRange(EventToDiagnostics(navEvents, spec, tokensByIndex));
        ValidateMatchedElements(navEvents, spec, tokensByIndex, diagnostics);
        ApplyCttCheck(transaction, spec, tokensByIndex, diagnostics);

        var passed = diagnostics.All(d => !IsBlocking(d));
        return new TransactionValidationResult(transaction.TransactionSetId, transaction.ControlNumber, passed, diagnostics);
    }

    private static void ValidateMatchedElements(
        IReadOnlyList<NavEvent> navEvents,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensByIndex,
        List<object> diagnostics)
    {
        foreach (var matched in navEvents.OfType<MatchedEvent>())
        {
            switch (matched)
            {
                case { Spec: SegmentNodeSpec segSpec, Node: SegmentNode segNode }:
                    ValidateElements(segNode, segSpec, spec, tokensByIndex, diagnostics);
                    break;
                case { Spec: LoopNodeSpec loopSpec, Node: LogicalNode loopNode }:
                    ValidateElements(loopNode, loopSpec, spec, tokensByIndex, diagnostics);
                    break;
            }
        }
    }

    private static IEnumerable<object> EventToDiagnostics(
        IReadOnlyList<NavEvent> navEvents,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensByIndex)
    {
        foreach (var navEvent in navEvents)
        {
            switch (navEvent)
            {
                case MissingSpecEvent missing:
                    yield return new MissingSegmentDiagnostic(missing.Spec.Id, new DiagnosticLocation());
                    break;
                case UnexpectedNodeEvent unexpected:
                    yield return new UnexpectedSegmentDiagnostic(unexpected.Node.Id, SegmentLocationFromNode(unexpected.Node, tokensByIndex));
                    break;
                case MaxOccursExceededEvent max:
                    yield return IsLoopId(spec.Root, max.SegmentId)
                        ? new LoopRepeatExceeded(max.SegmentId, max.Actual, max.Max, new DiagnosticLocation())
                        : new SegmentRepeatExceeded(max.SegmentId, max.Actual, max.Max, new DiagnosticLocation());
                    break;
                case SegmentRuleViolatedEvent violated:
                    var loc = violated.SegmentPosition.HasValue && tokensByIndex.TryGetValue(violated.SegmentPosition.Value, out var tok)
                        ? new DiagnosticLocation(tok.SegmentIndex, null, tok.Offset, tok.Raw.Length)
                        : new DiagnosticLocation();
                    yield return new RuleViolationDiagnostic(violated.SegmentId, violated.Rule.Id, violated.Rule.Severity, loc);
                    break;
            }
        }
    }

    private static void ValidateElements(
        Node node,
        NodeSpec nodeSpec,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensByIndex,
        List<object> diagnostics)
    {
        if (!tokensByIndex.TryGetValue(node switch
            {
                SegmentNode seg => seg.Meta.Position,
                LogicalNode log => log.Meta.Position,
                _ => -1
            }, out var token))
        {
            return;
        }

        var valuesByOrdinal = ElementOrdinals.FieldByOrdinal(node.Fields);
        var orderedElements = ElementOrdinals.Ordered(nodeSpec.Id, nodeSpec.Elements);
        var presentOrdinals = orderedElements
            .Where(pair => node.Fields.Any(f => f.Id == pair.Element.Id))
            .Select(pair => pair.Ordinal)
            .ToHashSet();
        var lastDataOrdinal = presentOrdinals.Count == 0 ? -1 : presentOrdinals.Max();

        foreach (var (elementSpec, ordinal) in orderedElements)
        {
            valuesByOrdinal.TryGetValue(ordinal, out var field);
            if (field is null)
            {
                if (elementSpec.Presence == Presence.Required)
                {
                    diagnostics.Add(new MissingElementDiagnostic(node.Id, ordinal, ElementLocation(token, ordinal)));
                }
                continue;
            }

            if (string.IsNullOrEmpty(field.Value))
            {
                if (elementSpec.Presence == Presence.Required)
                {
                    diagnostics.Add(new MissingElementDiagnostic(node.Id, ordinal, ElementLocation(token, ordinal)));
                }

                continue;
            }

            var loc = ElementLocation(token, ordinal);
            if (!TypeMatches(elementSpec.DataType, field.Value))
            {
                diagnostics.Add(new InvalidElementTypeDiagnostic(node.Id, ordinal, elementSpec.DataType, field.Value, loc));
                continue;
            }

            if (field.Value.Length < elementSpec.MinLength || field.Value.Length > elementSpec.MaxLength)
            {
                diagnostics.Add(new InvalidElementLengthDiagnostic(node.Id, ordinal, field.Value.Length, elementSpec.MinLength, elementSpec.MaxLength, loc));
                continue;
            }

            if (elementSpec.DataType == X12Type.ID &&
                elementSpec.CodeListRef is not null &&
                spec.CodeLists.TryGetValue(elementSpec.CodeListRef, out var codes) &&
                !codes.Contains(field.Value))
            {
                diagnostics.Add(new InvalidElementCodeDiagnostic(node.Id, ordinal, field.Value, loc));
            }
        }
    }

    private static DiagnosticLocation SegmentLocationFromNode(Node node, IReadOnlyDictionary<int, X12Token> tokensByIndex) =>
        node switch
        {
            SegmentNode seg when tokensByIndex.TryGetValue(seg.Meta.Position, out var tok) => new DiagnosticLocation(tok.SegmentIndex, null, tok.Offset, tok.Raw.Length),
            LogicalNode log when tokensByIndex.TryGetValue(log.Meta.Position, out var tok) => new DiagnosticLocation(tok.SegmentIndex, null, tok.Offset, tok.Raw.Length),
            LogicalNode log => new DiagnosticLocation(log.Meta.Position, null, -1, 0),
            SegmentNode seg => new DiagnosticLocation(seg.Meta.Position, null, -1, 0),
            _ => new DiagnosticLocation()
        };

    private static bool IsLoopId(IReadOnlyList<NodeSpec> nodes, string id)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LoopNodeSpec loop when loop.Id == id:
                    return true;
                case LoopNodeSpec loop when IsLoopId(loop.Body, id):
                    return true;
            }
        }

        return false;
    }

    private static bool IsBlocking(object diagnostic) =>
        diagnostic switch
        {
            RuleViolationDiagnostic rule => !string.Equals(rule.Severity, "WARNING", StringComparison.OrdinalIgnoreCase),
            _ => true
        };

    private static bool TypeMatches(X12Type type, string value) =>
        type switch
        {
            X12Type.AN or X12Type.ID => value.All(ch => ch >= ' ' && ch <= '~'),
            X12Type.N0 or X12Type.N1 or X12Type.N2 => value.All(char.IsDigit),
            X12Type.R => decimal.TryParse(value, out _),
            X12Type.DT => value.Length == 8 && value.All(char.IsDigit),
            X12Type.TM => (value.Length == 4 || value.Length == 6) && value.All(char.IsDigit),
            X12Type.B => value.Length > 0,
            _ => true
        };

    private static DiagnosticLocation ElementLocation(X12Token token, int ordinal)
    {
        var element = token.Element(ordinal);
        return element is null
            ? new DiagnosticLocation(token.SegmentIndex, ordinal, token.Offset, token.Raw.Length)
            : new DiagnosticLocation(token.SegmentIndex, ordinal, element.Offset, element.Raw.Length);
    }

    private static void ApplyCttCheck(
        X12Transaction transaction,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensByIndex,
        List<object> diagnostics)
    {
        if (string.IsNullOrEmpty(spec.CttCountSegmentId))
        {
            return;
        }

        var itemCount = CountSegments(transaction.Segments, spec.CttCountSegmentId!);
        var ctt = FindFirstSegment(transaction.Segments, "CTT");
        if (ctt is null)
        {
            return;
        }

        var ctt01 = ctt.Fields.FirstOrDefault(f => f.Id == "CTT01")?.Value;
        if (!int.TryParse(ctt01, out var declaredCount) || declaredCount == itemCount)
        {
            return;
        }

        var loc = tokensByIndex.TryGetValue(ctt.Meta.Position, out var tok)
            ? new DiagnosticLocation(tok.SegmentIndex, null, tok.Offset, tok.Raw.Length)
            : new DiagnosticLocation(ctt.Meta.Position, null, -1, 0);

        diagnostics.Add(new TransactionSyntaxDiagnostic("StructuralError", loc));
    }

    private static int CountSegments(LogicalNode root, string id) =>
        FlattenSegments(root).Count(s => s.Id == id);

    private static SegmentNode? FindFirstSegment(LogicalNode root, string id) =>
        FlattenSegments(root).FirstOrDefault(s => s.Id == id);

    private static IEnumerable<SegmentNode> FlattenSegments(LogicalNode root)
    {
        foreach (var child in root.Children)
        {
            switch (child)
            {
                case SegmentNode segment:
                    yield return segment;
                    break;
                case LogicalNode logical:
                    foreach (var nested in FlattenSegments(logical))
                    {
                        yield return nested;
                    }
                    break;
            }
        }
    }

    private static void AddSpecShapeDiagnostics(IReadOnlyList<NodeSpec> specs, List<object> diagnostics)
    {
        foreach (var node in specs)
        {
            if (node.Elements.Count == 0)
            {
                diagnostics.Add(new EmptySegmentSpecDiagnostic(node.Id, new DiagnosticLocation()));
            }

            foreach (var element in node.Elements.Where(e => e.DataType == X12Type.ID && e.CodeListRef is null))
            {
                diagnostics.Add(new IdElementMissingCodeListDiagnostic(node.Id, element.Id, new DiagnosticLocation()));
            }

            if (node is LoopSegmentSpec loop)
            {
                AddSpecShapeDiagnostics(loop.Body, diagnostics);
            }
            else if (node is NestedLoopSegmentSpec nested)
            {
                AddSpecShapeDiagnostics(nested.Body, diagnostics);
            }
        }
    }
}
