namespace Blocke.X12.Parsing.Model;

public sealed record X12Token(
    string SegmentId,
    IReadOnlyList<X12Element> Elements,
    int SegmentIndex,
    int Offset,
    string Raw)
{
    public X12Element? Element(int ordinal) =>
        Elements.FirstOrDefault(e => e.Ordinal == ordinal);
}
