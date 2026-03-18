using Blocke.X12.Models;

namespace Blocke.X12;

public sealed record PartyId(
    InterchangeIdQualifier Qualifier,
    string Id)
{
    public PartyId(string qualifier, string id) : this(InterchangeIdQualifierExtensions.Parse(qualifier), id) { }
}

public sealed record Delimiters(
    char ElementSep,
    char SubElementSep,
    char SegmentTerminator,
    char RepetitionSep)
{
    public static Delimiters Default => new('*', ':', '~', 'U');
}

public sealed record InterchangeEnvelope(
    PartyId Sender,
    PartyId Receiver,
    long ControlNumber,
    UsageIndicator Usage = UsageIndicator.Test,
    string Version = "00401",
    DateTimeOffset? Timestamp = null,
    Delimiters? Delimiters = null)
{
    public ISA ToIsa()
    {
        var ts = Timestamp ?? DateTimeOffset.UtcNow;
        var delims = Delimiters ?? Blocke.X12.Delimiters.Default;
        return new ISA(
            ElementSep: delims.ElementSep,
            SubElementSep: delims.SubElementSep,
            SegmentTerminator: delims.SegmentTerminator,
            RepetitionSep: delims.RepetitionSep,
            AuthInfoQual: AuthInfoQualifier.NoAuthPresent,
            AuthInfo: null,
            SecurityInfoQual: SecurityInfoQualifier.NoSecurity,
            SecurityInfo: null,
            InterchangeSenderIdQual: Sender.Qualifier,
            InterchangeSenderId: Sender.Id,
            InterchangeReceiverIdQual: Receiver.Qualifier,
            InterchangeReceiverId: Receiver.Id,
            InterchangeDateTime: ts,
            InterchangeControlVersion: Version,
            InterchangeControlNumber: FormatControl(ControlNumber, 9),
            AckRequested: false,
            UsageIndicator: Usage);
    }

    private static string FormatControl(long n, int pad)
    {
        var s = n.ToString();
        if (s.Length > pad)
        {
            throw new ArgumentException($"Control number {s} exceeds pad width {pad}");
        }

        return s.PadLeft(pad, '0');
    }
}
