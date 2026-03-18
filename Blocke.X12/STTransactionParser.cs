using Blocke.X12.Models;
using Blocke.X12.Parsing;
using Blocke.X12.Parsing.Model;
using Blocke.X12.Validation;

namespace Blocke.X12;

public static class STTransactionParser
{
    public static ParsedTransactionValidationResult Parse(X12Types.X12_ST tx, X12Specification spec, bool debug = false)
    {
        var tokens = X12Tokenizer.Tokenize(tx, X12Tokenizer.Delimiters.Default);
        var diagnostics = new List<object>();
        var tokensByIndex = tokens.ToDictionary(t => t.SegmentIndex);

        if (tokens.Count == 0)
        {
            diagnostics.Add(new TransactionSyntaxDiagnostic("StructuralError", new DiagnosticLocation()));
            return new ParsedTransactionValidationResult(null, new TransactionValidationResult(spec.TransactionSetId, string.Empty, false, diagnostics));
        }

        var stToken = tokens[0];
        var seIdx = tokens.ToList().FindLastIndex(t => t.SegmentId == "SE");
        if (seIdx < 0)
        {
            diagnostics.Add(new MissingSegmentDiagnostic("SE", new DiagnosticLocation()));
            return new ParsedTransactionValidationResult(null, new TransactionValidationResult(stToken.Element(1)?.Raw ?? spec.TransactionSetId, stToken.Element(2)?.Raw ?? string.Empty, false, diagnostics));
        }

        var seToken = tokens[seIdx];
        var observedCount = seIdx + 1;
        if (int.TryParse(seToken.Element(1)?.Raw, out var declaredCount) && declaredCount != observedCount)
        {
            diagnostics.Add(new TransactionSyntaxDiagnostic("SegmentCountMismatch", new DiagnosticLocation(seToken.SegmentIndex, null, seToken.Offset, seToken.Raw.Length)));
        }

        var st02 = stToken.Element(2)?.Raw ?? string.Empty;
        var se02 = seToken.Element(2)?.Raw ?? string.Empty;
        if (!string.IsNullOrEmpty(st02) && !string.IsNullOrEmpty(se02) && !string.Equals(st02, se02, StringComparison.Ordinal))
        {
            diagnostics.Add(new TransactionSyntaxDiagnostic("ControlNumberMismatch", new DiagnosticLocation(seToken.SegmentIndex, null, seToken.Offset, seToken.Raw.Length)));
        }

        if (seIdx < tokens.Count - 1)
        {
            var trailing = tokens[seIdx + 1];
            diagnostics.Add(new UnexpectedSegmentDiagnostic(trailing.SegmentId, new DiagnosticLocation(trailing.SegmentIndex, null, trailing.Offset, trailing.Raw.Length)));
        }

        var bodyTokens = tokens.Skip(1).Take(seIdx - 1).ToArray();
        var bodyNodes = bodyTokens.Select(ToSegmentNode).Cast<Node>().ToList();
        var assembled = Assemble(bodyNodes, spec.Root);
        var assembledRoot = new LogicalNode("Transaction", Array.Empty<Field>(), assembled, new LogicalMeta(LogicalOrigin.ParserInferred));
        if (HasInvalidHlHierarchy(assembledRoot))
        {
            diagnostics.Add(new TransactionSyntaxDiagnostic("StructuralError", new DiagnosticLocation()));
            return new ParsedTransactionValidationResult(null, new TransactionValidationResult(stToken.Element(1)?.Raw ?? spec.TransactionSetId, stToken.Element(2)?.Raw ?? string.Empty, false, diagnostics));
        }

        var segments = RewriteHlHierarchy(assembled);
        var transaction = new X12Transaction(
            stToken.Element(1)?.Raw ?? spec.TransactionSetId,
            stToken.Element(2)?.Raw ?? string.Empty,
            new LogicalNode("Transaction", Array.Empty<Field>(), segments, new LogicalMeta(LogicalOrigin.ParserInferred)));

        var validation = TransactionValidator.Validate(transaction, spec, tokensByIndex, diagnostics, debug);
        return new ParsedTransactionValidationResult(transaction, validation);
    }

    private static IReadOnlyList<Node> Assemble(IReadOnlyList<Node> nodes, IReadOnlyList<NodeSpec> spec)
    {
        var result = new List<Node>();
        var i = 0;
        while (i < nodes.Count)
        {
            if (nodes[i] is not SegmentNode segment)
            {
                result.Add(nodes[i]);
                i++;
                continue;
            }

            var loopSpec = spec.OfType<LoopSegmentSpec>().FirstOrDefault(l => l.Id == segment.Id)
                ?? (NodeSpec?)spec.OfType<NestedLoopSegmentSpec>().FirstOrDefault(l => l.Id == segment.Id);

            if (loopSpec is null)
            {
                result.Add(segment);
                i++;
                continue;
            }

            var loopStartId = segment.Id;
            var siblingStarts = spec.OfType<NodeSpec>()
                .Where(s => s != loopSpec && s is LoopSegmentSpec or NestedLoopSegmentSpec)
                .Select(s => s.Id)
                .ToHashSet(StringComparer.Ordinal);

            var bodySpec = loopSpec switch
            {
                LoopSegmentSpec l => l.Body,
                NestedLoopSegmentSpec n => n.Body,
                _ => Array.Empty<NodeSpec>()
            };
            var bodyIds = bodySpec.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

            var body = new List<Node>();
            i++;
            while (i < nodes.Count)
            {
                if (nodes[i] is SegmentNode nextSeg &&
                    !bodyIds.Contains(nextSeg.Id) &&
                    (nextSeg.Id == loopStartId || siblingStarts.Contains(nextSeg.Id) || spec.Any(s => s is SegmentNodeSpec segSpec && segSpec.Id == nextSeg.Id && segSpec.Id != loopStartId)))
                {
                    break;
                }

                body.Add(nodes[i]);
                i++;
            }

            var rewrittenBody = Assemble(body, bodySpec);
            result.Add(new LogicalNode(loopStartId, segment.Fields, rewrittenBody, new LogicalMeta(LogicalOrigin.SpecDefined, null, segment.Meta.Position)));
        }

        return result;
    }

    private static bool HasInvalidHlHierarchy(LogicalNode root)
    {
        var hlNodes = root.Children.OfType<LogicalNode>().Where(n => n.Id == "HL").ToArray();
        if (hlNodes.Length == 0)
        {
            return false;
        }

        var ids = hlNodes
            .Select(n => n.Fields.FirstOrDefault(f => f.Id == "HL01")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToHashSet(StringComparer.Ordinal);

        return hlNodes.Any(n =>
        {
            var parent = n.Fields.FirstOrDefault(f => f.Id == "HL02")?.Value;
            return !string.IsNullOrEmpty(parent) && !ids.Contains(parent);
        });
    }

    private static IReadOnlyList<Node> RewriteHlHierarchy(IReadOnlyList<Node> nodes)
    {
        var pre = new List<Node>();
        var post = new List<Node>();
        var records = new List<HlRecord>();
        var inHl = false;
        var inPost = false;

        foreach (var node in nodes)
        {
            if (inPost)
            {
                post.Add(node);
                continue;
            }

            if (node is LogicalNode logical && logical.Id == "HL")
            {
                inHl = true;
                records.Add(new HlRecord(
                    logical.Fields.FirstOrDefault(f => f.Id == "HL01")?.Value ?? "<missing>",
                    logical.Fields.FirstOrDefault(f => f.Id == "HL02")?.Value,
                    logical.Fields,
                    logical.Children,
                    logical.Meta));
                continue;
            }

            if (!inHl)
            {
                pre.Add(node);
                continue;
            }

            inPost = true;
            post.Add(node);
        }

        if (records.Count == 0)
        {
            return nodes;
        }

        var byId = new Dictionary<string, HlRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            byId[record.Id] = record;
        }

        var childrenOf = records
            .Where(r => !string.IsNullOrEmpty(r.ParentId))
            .GroupBy(r => r.ParentId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        LogicalNode Build(HlRecord record)
        {
            var children = new List<Node>(record.Children);
            if (childrenOf.TryGetValue(record.Id, out var childRecords))
            {
                foreach (var child in childRecords)
                {
                    if (byId.TryGetValue(child.Id, out var actual))
                    {
                        children.Add(Build(actual));
                    }
                }
            }

            var role = HLRoleExtensions.FromCode(record.Fields.FirstOrDefault(f => f.Id == "HL03")?.Value ?? string.Empty);
            return new LogicalNode("HL", record.Fields, children, record.Meta with { Role = role });
        }

        var roots = records.Where(r => string.IsNullOrEmpty(r.ParentId)).ToArray();
        return pre.Concat(roots.Select(Build)).Concat(post).ToArray();
    }

    private sealed record HlRecord(
        string Id,
        string? ParentId,
        IReadOnlyList<Field> Fields,
        IReadOnlyList<Node> Children,
        LogicalMeta Meta);

    private static SegmentNode ToSegmentNode(X12Token token)
    {
        var lastDataIdx = token.Elements.ToList().FindLastIndex(e => !string.IsNullOrEmpty(e.Raw));
        var fields = lastDataIdx < 0
            ? Array.Empty<Field>()
            : Enumerable.Range(0, lastDataIdx + 1)
                .Select(idx => new Field($"{token.SegmentId}{idx + 1:00}", token.Elements[idx].Raw))
                .ToArray();

        return new SegmentNode(token.SegmentId, fields, new SegmentMeta(token.SegmentIndex, token.Raw));
    }
}
