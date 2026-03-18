using Blocke.X12.Models;

namespace Blocke.X12.Parsing;

public static class ISAParser
{
    private const int IsaLength = 106;

    public static (ISA Isa, IReadOnlyList<string> Segments) ParseIsaSegment(string rawX12)
    {
        var isaStart = rawX12.IndexOf("ISA", StringComparison.Ordinal);
        if (isaStart < 0)
        {
            throw new X12ParseError("ISA", "Missing ISA segment", 0, 0, rawX12.Length > 200 ? rawX12[..200] : rawX12);
        }

        var prefix = rawX12[..isaStart];
        if (prefix.Any(ch => !char.IsWhiteSpace(ch)))
        {
            throw new X12ParseError("ISA", "Unexpected non-whitespace before ISA", 0, 0, prefix.Length > 200 ? prefix[^200..] : prefix);
        }

        if (rawX12.Length < isaStart + IsaLength)
        {
            throw new X12ParseError("ISA", "ISA segment truncated", 0, isaStart, rawX12[isaStart..]);
        }

        var isaRaw = rawX12.Substring(isaStart, IsaLength);
        var isa = Parse(isaRaw);
        var segments = rawX12[isaStart..]
            .Split(isa.SegmentTerminator)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        return (isa, segments);
    }

    public static ISA Parse(string isaRaw)
    {
        if (isaRaw.Length < IsaLength)
        {
            throw new X12ParseError("ISA", $"ISA segment must be exactly 106 characters, got {isaRaw.Length}", 0, 0, isaRaw);
        }

        if (!isaRaw.StartsWith("ISA", StringComparison.Ordinal))
        {
            var got = isaRaw.Length >= 3 ? isaRaw[..3] : isaRaw;
            throw new X12ParseError("ISA", $"ISA segment must start with 'ISA', got '{got}'", 0, 0, isaRaw);
        }

        var elementSep = isaRaw[3];
        var subElementSep = isaRaw[104];
        var segmentTerminator = isaRaw[105];
        var fields = isaRaw.Split(elementSep).Select(f => f.Trim()).ToArray();

        if (fields.Length != 17)
        {
            throw new X12ParseError("ISA", $"ISA segment must have 17 fields, got {fields.Length}", 0, 0, isaRaw);
        }

        UsageIndicatorExtensions.TryParse(fields[15], out var usageIndicator);

        return new ISA(
            ElementSep: elementSep,
            SubElementSep: subElementSep,
            SegmentTerminator: segmentTerminator,
            RepetitionSep: fields[11].FirstOrDefault('U'),
            AuthInfoQual: AuthInfoQualifierExtensions.Parse(fields[1]),
            AuthInfo: string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2],
            SecurityInfoQual: SecurityInfoQualifierExtensions.Parse(fields[3]),
            SecurityInfo: string.IsNullOrWhiteSpace(fields[4]) ? null : fields[4],
            InterchangeSenderIdQual: InterchangeIdQualifierExtensions.Parse(fields[5]),
            InterchangeSenderId: fields[6],
            InterchangeReceiverIdQual: InterchangeIdQualifierExtensions.Parse(fields[7]),
            InterchangeReceiverId: fields[8],
            InterchangeDateTime: ParseInterchangeDateTime(fields[9], fields[10]),
            InterchangeControlVersion: fields[12],
            InterchangeControlNumber: fields[13],
            AckRequested: fields[14] == "1",
            UsageIndicator: usageIndicator);
    }

    private static DateTimeOffset? ParseInterchangeDateTime(string date, string time)
    {
        try
        {
            var year = 2000 + int.Parse(date[..2]);
            var month = int.Parse(date.Substring(2, 2));
            var day = int.Parse(date.Substring(4, 2));
            var hour = int.Parse(time[..2]);
            var minute = int.Parse(time.Substring(2, 2));
            var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
            return new DateTimeOffset(local);
        }
        catch
        {
            return null;
        }
    }
}
