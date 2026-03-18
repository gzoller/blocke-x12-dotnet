using Blocke.X12.Models;

namespace Blocke.X12.Validation;

internal static class NavWalker
{
    public static IReadOnlyList<NavEvent> Traverse(LogicalNode root, X12Specification spec)
    {
        var rootSpec = new LoopSegmentSpec("<ROOT>", Presence.Required, 1, 1, spec.Root, Array.Empty<ElementSpec>(), Array.Empty<RuleSpec>());
        var events = new List<NavEvent>();
        var loopIters = new Dictionary<string, int>(StringComparer.Ordinal);
        var ctx = new TraversalContext(rootSpec, root.Children, [new LoopCtx("<ROOT>", 0)], events, loopIters);
        TraverseLoop(ctx);
        return events;
    }

    private static TraversalContext TraverseLoop(TraversalContext ctx)
    {
        if (ctx.SpecIndex >= ctx.Spec.Body.Count || ctx.NodeIndex >= ctx.Nodes.Count)
        {
            ctx.DrainRemaining();
            return ctx;
        }

        if (!ctx.StateIsValid)
        {
            ctx.DrainRemaining();
            return ctx;
        }

        var progressMade = false;
        if (ctx.CurSpec.Id == ctx.CurNode.Id)
        {
            var spec = ctx.CurSpec;
            var startNodeIdx = ctx.NodeIndex;
            var run = ctx.CountRun(ctx.NodeIndex, spec.Id);
            var max = spec.MaxOccurs;
            var emitCount = Math.Min(run, max);

            for (var i = 0; i < emitCount; i++)
            {
                var node = ctx.Nodes[startNodeIdx + i];
                ctx.Emit(new MatchedEvent(ctx.LoopStack, spec, node, ctx.SpecIndex, startNodeIdx + i));
                EvalRules(ctx, spec, node, ctx.SpecIndex, startNodeIdx + i);

                if (spec is LoopNodeSpec loopSpec && node is LogicalNode logical)
                {
                    var selector = loopSpec is NestedLoopSegmentSpec nested
                        ? SelectorOf(logical, nested.NestingKey)
                        : null;
                    var iter = ctx.NextIter(loopSpec.Id);
                    var childCtx = new TraversalContext(loopSpec, logical.Children, [.. ctx.LoopStack, new LoopCtx(loopSpec.Id, iter, selector)], ctx.Events, ctx.LoopIters);
                    TraverseLoop(childCtx);
                }
            }

            if (run > max)
            {
                ctx.Emit(new MaxOccursExceededEvent(ctx.LoopStack, spec.Id, max, run, ctx.SpecIndex, startNodeIdx));
            }

            ctx.NodeIndex += run;
            ctx.SpecIndex += 1;
            progressMade = true;
        }
        else if (ctx.CurNode is LogicalNode logical && logical.Id == ctx.Spec.Id)
        {
            var selector = ctx.Spec is NestedLoopSegmentSpec nested
                ? SelectorOf(logical, nested.NestingKey)
                : null;
            var iter = ctx.NextIter(ctx.Spec.Id);
            var childCtx = new TraversalContext(ctx.Spec, logical.Children, [.. ctx.LoopStack, new LoopCtx(ctx.Spec.Id, iter, selector)], ctx.Events, ctx.LoopIters);
            TraverseLoop(childCtx);
            ctx.NodeIndex += 1;
            progressMade = true;
        }
        else
        {
            var nodeCanMatchLaterSpec = ctx.SpecIndex + 1 <= ctx.SpecSuffixIds.Count - 1 && ctx.SpecSuffixIds[ctx.SpecIndex + 1].Contains(ctx.CurNode.Id);
            var specCanMatchLaterNode = ctx.NodeIndex + 1 <= ctx.NodeSuffixIds.Count - 1 && ctx.NodeSuffixIds[ctx.NodeIndex + 1].Contains(ctx.CurSpec.Id);

            if (ctx.CurSpec.Presence == Presence.Required)
            {
                if (specCanMatchLaterNode)
                {
                    ctx.Emit(new UnexpectedNodeEvent(ctx.LoopStack, ctx.CurNode, ctx.NodeIndex));
                    ctx.NodeIndex += 1;
                }
                else
                {
                    ctx.Emit(new MissingSpecEvent(ctx.LoopStack, ctx.CurSpec, ctx.SpecIndex));
                    ctx.SpecIndex += 1;
                }
            }
            else
            {
                if (nodeCanMatchLaterSpec)
                {
                    ctx.Emit(new SkippedOptionalSpecEvent(ctx.LoopStack, ctx.CurSpec, ctx.SpecIndex));
                    ctx.SpecIndex += 1;
                }
                else
                {
                    ctx.Emit(new UnexpectedNodeEvent(ctx.LoopStack, ctx.CurNode, ctx.NodeIndex));
                    ctx.NodeIndex += 1;
                }
            }

            progressMade = true;
        }

        if (progressMade && ctx.StateIsValid)
        {
            return TraverseLoop(ctx);
        }

        if (!ctx.StateIsValid)
        {
            ctx.DrainRemaining();
        }

        return ctx;
    }

    private static void EvalRules(TraversalContext ctx, NodeSpec spec, Node node, int specIdx, int nodeIdx)
    {
        if (node is not SegmentNode segment)
        {
            return;
        }

        foreach (var rule in spec.Rules)
        {
            var whenOk = rule.When is null || PredicateMatches(rule.When, segment);
            if (!whenOk || PredicateMatches(rule.Must, segment))
            {
                continue;
            }

            ctx.Emit(new SegmentRuleViolatedEvent(ctx.LoopStack, segment.Id, rule, specIdx, nodeIdx, segment.Meta.Position));
        }
    }

    private static bool PredicateMatches(FieldPredicateSpec predicate, SegmentNode segment) =>
        predicate switch
        {
            EqualsPredicate eq => segment.Fields.Any(f => f.Id == eq.Field && f.Value == eq.Value),
            ExistsPredicate ex => segment.Fields.Any(f => f.Id == ex.Field && !string.IsNullOrEmpty(f.Value)),
            InSetPredicate set => segment.Fields.Any(f => f.Id == set.Field && set.Values.Contains(f.Value)),
            AndPredicate and => and.Predicates.All(p => PredicateMatches(p, segment)),
            OrPredicate or => or.Predicates.Any(p => PredicateMatches(p, segment)),
            XorPredicate xor => xor.Predicates.Count(p => PredicateMatches(p, segment)) == 1,
            NotPredicate not => !PredicateMatches(not.Predicate, segment),
            _ => false
        };

    private static string? SelectorOf(LogicalNode node, string fieldId) =>
        node.Fields.FirstOrDefault(f => f.Id == fieldId)?.Value;

    private sealed class TraversalContext(
        LoopNodeSpec spec,
        IReadOnlyList<Node> nodes,
        IReadOnlyList<LoopCtx> loopStack,
        List<NavEvent> events,
        Dictionary<string, int> loopIters)
    {
        public LoopNodeSpec Spec { get; } = spec;
        public IReadOnlyList<Node> Nodes { get; } = nodes;
        public IReadOnlyList<LoopCtx> LoopStack { get; } = loopStack;
        public List<NavEvent> Events { get; } = events;
        public Dictionary<string, int> LoopIters { get; } = loopIters;
        public int SpecIndex { get; set; }
        public int NodeIndex { get; set; }

        public NodeSpec CurSpec => Spec.Body[SpecIndex];
        public Node CurNode => Nodes[NodeIndex];
        public bool StateIsValid => SpecIndex < Spec.Body.Count && NodeIndex < Nodes.Count;

        public List<HashSet<string>> SpecSuffixIds { get; } = BuildSuffixIds(spec.Body.Select(n => n.Id).ToArray());
        public List<HashSet<string>> NodeSuffixIds { get; } = BuildSuffixIds(nodes.Select(n => n.Id).ToArray());

        public void Emit(NavEvent navEvent) => Events.Add(navEvent);

        public int NextIter(string loopId)
        {
            var key = string.Join("/", LoopStack.Select(x => x.LoopId).Append(loopId));
            LoopIters.TryGetValue(key, out var current);
            LoopIters[key] = current + 1;
            return current;
        }

        public void DrainRemaining()
        {
            if (NodeIndex >= Nodes.Count)
            {
                while (SpecIndex < Spec.Body.Count)
                {
                    var specNode = CurSpec;
                    Emit(specNode.Presence == Presence.Required
                        ? new MissingSpecEvent(LoopStack, specNode, SpecIndex)
                        : new SkippedOptionalSpecEvent(LoopStack, specNode, SpecIndex));
                    SpecIndex += 1;
                }
            }

            if (SpecIndex >= Spec.Body.Count)
            {
                while (NodeIndex < Nodes.Count)
                {
                    Emit(new UnexpectedNodeEvent(LoopStack, CurNode, NodeIndex));
                    NodeIndex += 1;
                }
            }
        }

        public int CountRun(int start, string id)
        {
            var i = start;
            while (i < Nodes.Count && Nodes[i].Id == id)
            {
                i += 1;
            }

            return i - start;
        }

        private static List<HashSet<string>> BuildSuffixIds(string[] ids)
        {
            var suffix = Enumerable.Range(0, ids.Length + 1).Select(_ => new HashSet<string>(StringComparer.Ordinal)).ToList();
            for (var i = ids.Length - 1; i >= 0; i--)
            {
                suffix[i] = new HashSet<string>(suffix[i + 1], StringComparer.Ordinal) { ids[i] };
            }

            return suffix;
        }
    }
}
