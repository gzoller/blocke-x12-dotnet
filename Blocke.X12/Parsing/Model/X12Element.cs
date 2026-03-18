namespace Blocke.X12.Parsing.Model;

public sealed record X12Element(
    string Raw,
    int Ordinal,
    int Offset,
    IReadOnlyList<string> SubElements);
