using Blocke.X12.Models;
using Blocke.X12.Parsing.Model;
using Blocke.X12.Validation;

namespace Blocke.X12;

public static class X12Validator
{
    public static TransactionValidationResult ValidateTransaction(
        X12Transaction transaction,
        X12Specification spec,
        bool debug = false) =>
        ValidateTransaction(transaction, spec, BuildTokensBySegmentIndex(transaction), debug);

    public static TransactionValidationResult ValidateTransaction(
        X12Transaction transaction,
        X12Specification spec,
        IReadOnlyDictionary<int, X12Token> tokensBySegmentIndex,
        bool debug = false) =>
        TransactionValidator.Validate(transaction, spec, tokensBySegmentIndex, debug: debug);

    public static ParsedTransactionValidationResult ParseTransaction(
        X12Types.X12_ST transaction,
        X12Specification spec,
        bool debug = false) =>
        STTransactionParser.Parse(transaction, spec, debug);

    private static IReadOnlyDictionary<int, X12Token> BuildTokensBySegmentIndex(X12Transaction transaction)
    {
        var tokens = new Dictionary<int, X12Token>();
        var offset = 0;

        void AddNode(Node node)
        {
            switch (node)
            {
                case SegmentNode segment:
                    tokens[segment.Meta.Position] = BuildToken(segment.Id, segment.Fields, segment.Meta.Position, segment.Meta.Source, ref offset);
                    break;
                case LogicalNode logical:
                    tokens[logical.Meta.Position] = BuildToken(logical.Id, logical.Fields, logical.Meta.Position, null, ref offset);
                    foreach (var child in logical.Children)
                    {
                        AddNode(child);
                    }
                    break;
            }
        }

        foreach (var child in transaction.Segments.Children)
        {
            AddNode(child);
        }

        return tokens;
    }

    private static X12Token BuildToken(string segmentId, IReadOnlyList<Field> fields, int segmentIndex, string? rawSource, ref int runningOffset)
    {
        var raw = string.IsNullOrEmpty(rawSource)
            ? $"{segmentId}{string.Concat(fields.Select(f => $"*{f.Value}"))}"
            : rawSource.TrimEnd('~');

        var elements = new List<X12Element>();
        var elementOffset = runningOffset + segmentId.Length + 1;
        for (var i = 0; i < fields.Count; i++)
        {
            var value = fields[i].Value;
            elements.Add(new X12Element(value, i + 1, elementOffset, Array.Empty<string>()));
            elementOffset += value.Length + 1;
        }

        var token = new X12Token(segmentId, elements, segmentIndex, runningOffset, raw);
        runningOffset += raw.Length + 1;
        return token;
    }
}
