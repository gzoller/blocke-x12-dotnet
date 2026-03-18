using Blocke.X12.Models;
using Blocke.X12.Parsing.Model;

namespace Blocke.X12.Parsing;

public static class X12Tokenizer
{
    public readonly record struct Delimiters(char ElementSep, char SubElementSep, char SegmentTerm)
    {
        public static Delimiters Default => new('*', ':', '~');
    }

    public static IReadOnlyList<X12Token> Tokenize(X12Types.X12_ST tx, Delimiters delimiters)
    {
        var raw = tx.Value;
        var tokens = new List<X12Token>();
        var cursor = 0;
        var segmentIndex = 0;

        while (cursor < raw.Length)
        {
            while (cursor < raw.Length && char.IsWhiteSpace(raw[cursor]))
            {
                cursor++;
            }

            if (cursor >= raw.Length)
            {
                break;
            }

            var segStart = cursor;
            var termIdx = raw.IndexOf(delimiters.SegmentTerm, segStart);
            var segEnd = termIdx >= 0 ? termIdx : raw.Length;
            var segRaw = raw.Substring(segStart, segEnd - segStart);

            cursor = termIdx >= 0 ? termIdx + 1 : raw.Length;

            var trimmed = segRaw.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var leadingTrim = segRaw.IndexOf(trimmed, StringComparison.Ordinal);
            var absStart = segStart + (leadingTrim >= 0 ? leadingTrim : 0);
            var token = ParseSegment(
                trimmed,
                absStart,
                segmentIndex,
                delimiters.ElementSep,
                delimiters.SubElementSep);

            tokens.Add(token);
            segmentIndex++;
        }

        return tokens;
    }

    private static X12Token ParseSegment(
        string segmentRaw,
        int absStart,
        int segmentIndex,
        char elementSep,
        char subElementSep)
    {
        var firstSep = segmentRaw.IndexOf(elementSep);
        var segmentId = firstSep >= 0
            ? segmentRaw[..firstSep].Trim()
            : segmentRaw.Trim();

        if (segmentId.Length == 0 || !IsValidSegmentId(segmentId))
        {
            throw new X12ParseError(
                segmentId.Length == 0 ? "UNKNOWN" : segmentId,
                "Empty or invalid segment id",
                segmentIndex,
                absStart,
                segmentRaw);
        }

        var elements = firstSep < 0
            ? Array.Empty<X12Element>()
            : ParseElements(segmentRaw, absStart, elementSep, subElementSep, firstSep + 1);

        return new X12Token(segmentId, elements, segmentIndex, absStart, segmentRaw);
    }

    private static IReadOnlyList<X12Element> ParseElements(
        string segmentRaw,
        int absStart,
        char elementSep,
        char subElementSep,
        int elemStart)
    {
        var elements = new List<X12Element>();
        var ordinal = 1;
        var cursor = elemStart;

        while (cursor <= segmentRaw.Length)
        {
            var nextSep = segmentRaw.IndexOf(elementSep, cursor);
            var elemEnd = nextSep >= 0 ? nextSep : segmentRaw.Length;
            var rawValue = segmentRaw.Substring(cursor, elemEnd - cursor);

            elements.Add(
                new X12Element(
                    rawValue,
                    ordinal,
                    absStart + cursor,
                    rawValue.Split(subElementSep).ToArray()));

            ordinal++;
            cursor = nextSep < 0 ? segmentRaw.Length + 1 : nextSep + 1;
        }

        return elements;
    }

    private static bool IsValidSegmentId(string segmentId) =>
        segmentId.Length is >= 1 and <= 3 && segmentId.All(char.IsLetterOrDigit);
}
