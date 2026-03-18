using System.Text.RegularExpressions;
using Blocke.X12.Models;

namespace Blocke.X12.Validation;

internal static partial class ElementOrdinals
{
    [GeneratedRegex("([A-Z0-9]+?)(\\d{2})$")]
    private static partial Regex ElementIdRegex();

    public static int? OrdinalFromId(string id)
    {
        var match = ElementIdRegex().Match(id);
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups[2].Value);
    }

    public static IReadOnlyList<(ElementSpec Element, int Ordinal)> Ordered(string segmentId, IReadOnlyList<ElementSpec> elements)
    {
        var resolved = new List<(ElementSpec, int)>(elements.Count);
        foreach (var element in elements)
        {
            var ordinal = OrdinalFromId(element.Id)
                ?? throw new ArgumentException($"Invalid element id '{element.Id}' in segment {segmentId}: expected trailing 2-digit ordinal");
            resolved.Add((element, ordinal));
        }

        var prev = 0;
        foreach (var (_, ordinal) in resolved)
        {
            if (ordinal <= prev)
            {
                throw new ArgumentException($"Invalid element ordering in segment {segmentId}: {segmentId} is not strictly increasing");
            }

            prev = ordinal;
        }

        return resolved;
    }

    public static IReadOnlyDictionary<int, Field> FieldByOrdinal(IReadOnlyList<Field> fields)
    {
        var map = new Dictionary<int, Field>();
        foreach (var field in fields)
        {
            var ordinal = OrdinalFromId(field.Id);
            if (ordinal.HasValue)
            {
                map[ordinal.Value] = field;
            }
        }

        return map;
    }
}
