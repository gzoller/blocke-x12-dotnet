using Blocke.X12.Models;

namespace Blocke.X12;

public static class SpecPack
{
    public static X12Specification Unpack(IEnumerable<string> rawLines)
    {
        var lines = rawLines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
            .ToArray();

        if (lines.Length == 0)
        {
            throw new ArgumentException("Empty packed spec", nameof(rawLines));
        }

        var header = lines[0].Split('|');
        if (header.Length != 3 || header[0] != "X")
        {
            throw new ArgumentException($"Invalid spec header: {lines[0]}", nameof(rawLines));
        }

        var root = new List<NodeSpec>();
        var stack = new Stack<LoopFrame>();
        SegmentFrame? currentSegment = null;
        RuleFrame? currentRule = null;
        var codeLists = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        string? cttCountSegmentId = null;

        List<NodeSpec> CurrentBody() => stack.Count > 0 ? stack.Peek().Body : root;
        List<ElementSpec> CurrentElements()
        {
            if (currentSegment is not null)
            {
                return currentSegment.Elements;
            }

            if (stack.Count > 0)
            {
                return stack.Peek().Elements;
            }

            throw new ArgumentException("ELM with no active SEG/LOOP/NLOOP", nameof(rawLines));
        }

        void CloseCurrentSegment()
        {
            CloseCurrentRule();
            if (currentSegment is null)
            {
                return;
            }

            CurrentBody().Add(new SegmentNodeSpec(
                currentSegment.Id,
                currentSegment.Presence,
                currentSegment.MaxOccurs,
                currentSegment.Elements.ToArray(),
                currentSegment.Rules.ToArray()));
            currentSegment = null;
        }

        void CloseCurrentRule()
        {
            if (currentRule is null)
            {
                return;
            }

            var targetRules = currentSegment is not null ? currentSegment.Rules : stack.Count > 0 ? stack.Peek().Rules : null;
            if (currentRule.Must is not null)
            {
                targetRules?.Add(new RuleSpec(currentRule.Severity, currentRule.Id, currentRule.Must, currentRule.When));
            }
            currentRule = null;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var parts = line.Split('|');
            switch (parts[0])
            {
                case "SEG":
                    CloseCurrentSegment();
                    currentSegment = new SegmentFrame(parts[1], PresenceCodec.Decode(parts[2]), int.Parse(parts[3]));
                    break;
                case "LOOP":
                    CloseCurrentSegment();
                    stack.Push(new LoopFrame(parts[1], PresenceCodec.Decode(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]), null));
                    break;
                case "NLOOP":
                    CloseCurrentSegment();
                    stack.Push(new LoopFrame(parts[1], PresenceCodec.Decode(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]), parts[5]));
                    break;
                case "END":
                    CloseCurrentSegment();
                    var frame = stack.Pop();
                    NodeSpec loop = frame.NestingKey is null
                        ? new LoopSegmentSpec(frame.Id, frame.Presence, frame.MaxOccurs, frame.MinOccurs, frame.Body.ToArray(), frame.Elements.ToArray(), frame.Rules.ToArray())
                        : new NestedLoopSegmentSpec(frame.Id, frame.Presence, frame.MaxOccurs, frame.MinOccurs, frame.NestingKey, frame.Body.ToArray(), frame.Elements.ToArray(), frame.Rules.ToArray());
                    CurrentBody().Add(loop);
                    break;
                case "ELM":
                    CloseCurrentRule();
                    CurrentElements().Add(new ElementSpec(
                        parts[1],
                        PresenceCodec.Decode(parts[2]),
                        X12TypeCodec.Decode(parts[3]),
                        int.Parse(parts[4]),
                        int.Parse(parts[5]),
                        parts.Length > 6 && !string.IsNullOrEmpty(parts[6]) ? parts[6] : null));
                    break;
                case "CODE":
                    var codes = parts.Length > 2
                        ? parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal);
                    if (codes.Count == 0)
                    {
                        throw new ArgumentException($"CODE list '{parts[1]}' must specify at least one valid code value", nameof(rawLines));
                    }

                    codeLists[parts[1]] = codes;
                    break;
                case "RULE":
                    CloseCurrentRule();
                    currentRule = new RuleFrame(parts[1], parts[2]);
                    break;
                case "WHEN":
                    if (currentRule is not null)
                    {
                        currentRule.When = ParsePredicate(parts[1]);
                    }
                    break;
                case "MUST":
                    if (currentRule is not null)
                    {
                        currentRule.Must = ParsePredicate(parts[1]);
                    }
                    break;
                case "CTT":
                    cttCountSegmentId = parts[1];
                    break;
                default:
                    throw new ArgumentException($"Unknown spec line: {line}", nameof(rawLines));
            }
        }

        CloseCurrentSegment();
        return new X12Specification(header[1], header[2], root.ToArray(), codeLists, cttCountSegmentId);
    }

    private sealed class LoopFrame
    {
        public LoopFrame(string id, Presence presence, int maxOccurs, int minOccurs, string? nestingKey)
        {
            Id = id;
            Presence = presence;
            MaxOccurs = maxOccurs;
            MinOccurs = minOccurs;
            NestingKey = nestingKey;
        }

        public string Id { get; }
        public Presence Presence { get; }
        public int MaxOccurs { get; }
        public int MinOccurs { get; }
        public string? NestingKey { get; }
        public List<NodeSpec> Body { get; } = new();
        public List<ElementSpec> Elements { get; } = new();
        public List<RuleSpec> Rules { get; } = new();
    }

    private sealed class SegmentFrame
    {
        public SegmentFrame(string id, Presence presence, int maxOccurs)
        {
            Id = id;
            Presence = presence;
            MaxOccurs = maxOccurs;
        }

        public string Id { get; }
        public Presence Presence { get; }
        public int MaxOccurs { get; }
        public List<ElementSpec> Elements { get; } = new();
        public List<RuleSpec> Rules { get; } = new();
    }

    private sealed class RuleFrame
    {
        public RuleFrame(string severity, string id)
        {
            Severity = severity;
            Id = id;
        }

        public string Severity { get; }
        public string Id { get; }
        public FieldPredicateSpec? Must { get; set; }
        public FieldPredicateSpec? When { get; set; }
    }

    private static FieldPredicateSpec ParsePredicate(string text)
    {
        if (text.StartsWith("AND(", StringComparison.Ordinal))
        {
            return new AndPredicate(ParseList(text));
        }

        if (text.StartsWith("OR(", StringComparison.Ordinal))
        {
            return new OrPredicate(ParseList(text));
        }

        if (text.StartsWith("XOR(", StringComparison.Ordinal))
        {
            return new XorPredicate(ParseList(text));
        }

        if (text.StartsWith("NOT(", StringComparison.Ordinal))
        {
            return new NotPredicate(ParsePredicate(StripParens(text)));
        }

        if (text.StartsWith("EXISTS(", StringComparison.Ordinal))
        {
            return new ExistsPredicate(StripParens(text));
        }

        if (text.StartsWith("IN(", StringComparison.Ordinal))
        {
            var inner = StripParens(text);
            var parts = SplitTopLevelCsv(inner);
            if (parts.Count == 0)
            {
                throw new ArgumentException($"Invalid IN predicate: {text}");
            }

            return new InSetPredicate(parts[0], parts.Skip(1).ToHashSet(StringComparer.Ordinal));
        }

        var eqIdx = text.IndexOf('=', StringComparison.Ordinal);
        if (eqIdx <= 0)
        {
            throw new ArgumentException($"Invalid predicate: {text}");
        }

        return new EqualsPredicate(text[..eqIdx], text[(eqIdx + 1)..].Trim('"'));
    }

    private static string StripParens(string text) =>
        text[(text.IndexOf('(') + 1)..text.LastIndexOf(')')];

    private static IReadOnlyList<FieldPredicateSpec> ParseList(string text) =>
        SplitTopLevelCsv(StripParens(text)).Select(ParsePredicate).ToArray();

    private static List<string> SplitTopLevelCsv(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var inQuote = false;

        foreach (var ch in text)
        {
            switch (ch)
            {
                case '"':
                    inQuote = !inQuote;
                    current.Append(ch);
                    break;
                case '(' when !inQuote:
                    depth++;
                    current.Append(ch);
                    break;
                case ')' when !inQuote:
                    depth--;
                    current.Append(ch);
                    break;
                case ',' when !inQuote && depth == 0:
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result.Where(x => x.Length > 0).ToList();
    }
}
