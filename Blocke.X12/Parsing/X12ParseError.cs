using Blocke.X12.Models;

namespace Blocke.X12.Parsing;

public sealed class X12ParseError : Exception
{
    public X12ParseError(
        string segmentId,
        string message,
        int segmentIndex,
        long offset,
        string raw,
        SegmentSyntaxError? segmentAckCode = null,
        (int Position, ElementSyntaxError Error)? elementAckCode = null) : base(
        $"X12 Parse Error in segment '{segmentId}':{Environment.NewLine}" +
        $"  Problem: {message}{Environment.NewLine}" +
        $"  Segment index: {segmentIndex}{Environment.NewLine}" +
        $"  Offset: {offset}{Environment.NewLine}" +
        $"  Raw: {raw}{Environment.NewLine}")
    {
        SegmentId = segmentId;
        Problem = message;
        SegmentIndex = segmentIndex;
        Offset = offset;
        Raw = raw;
        SegmentAckCode = segmentAckCode;
        ElementAckCode = elementAckCode;
    }

    public string SegmentId { get; }

    public string Problem { get; }

    public int SegmentIndex { get; }

    public long Offset { get; }

    public string Raw { get; }

    public SegmentSyntaxError? SegmentAckCode { get; }

    public (int Position, ElementSyntaxError Error)? ElementAckCode { get; }
}
