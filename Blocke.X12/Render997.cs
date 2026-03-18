using System.Globalization;
using Blocke.X12.Models;

namespace Blocke.X12;

public enum AckPolicy
{
    FullDocument,
    PerFunctionalGroup,
    PerTransaction
}

public sealed record ControlNumberSeed(
    int GroupControlNumber,
    int TransactionControlNumber,
    int GroupPadWidth = 6,
    int TransactionPadWidth = 4);

public sealed record Rendered997(
    string Payload,
    long InterchangeControlNumber,
    int GroupControlNumber,
    int TransactionControlNumber)
{
    public string Pretty() => Payload;
}

public static class Render997
{
    public static IReadOnlyList<Rendered997> Render(
        InterchangeEnvelope envelope,
        Ack997 ack,
        AckPolicy policy = AckPolicy.FullDocument,
        ControlNumberSeed? seed = null)
    {
        var actualSeed = seed ?? new ControlNumberSeed(1, 1);
        return policy switch
        {
            AckPolicy.FullDocument => [RenderSingle(envelope, ack.Groups, actualSeed)],
            AckPolicy.PerFunctionalGroup => ack.Groups.Select((g, i) => RenderSingle(
                envelope with { ControlNumber = envelope.ControlNumber + i },
                [g],
                actualSeed with { GroupControlNumber = actualSeed.GroupControlNumber + i, TransactionControlNumber = actualSeed.TransactionControlNumber + i })).ToArray(),
            AckPolicy.PerTransaction => RenderPerTransaction(envelope, ack, actualSeed),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
        };
    }

    private static IReadOnlyList<Rendered997> RenderPerTransaction(InterchangeEnvelope envelope, Ack997 ack, ControlNumberSeed seed)
    {
        var rendered = new List<Rendered997>();
        var interchangeOffset = 0;
        var groupNo = seed.GroupControlNumber;
        var txNo = seed.TransactionControlNumber;

        foreach (var group in ack.Groups)
        {
            foreach (var txn in group.TransactionAcks)
            {
                var partialGroup = group with { TransactionAcks = [txn], ReceivedTransactionCount = 1, DeclaredTransactionCount = 1 };
                rendered.Add(RenderSingle(
                    envelope with { ControlNumber = envelope.ControlNumber + interchangeOffset },
                    [partialGroup],
                    new ControlNumberSeed(groupNo, txNo, seed.GroupPadWidth, seed.TransactionPadWidth)));
                interchangeOffset++;
                groupNo++;
                txNo++;
            }
        }

        return rendered;
    }

    private static Rendered997 RenderSingle(InterchangeEnvelope envelope, IReadOnlyList<Ack997Group> groups, ControlNumberSeed seed)
    {
        var isa = envelope.ToIsa();
        var e = isa.ElementSep;
        var t = isa.SegmentTerminator;
        var timestamp = isa.InterchangeDateTime ?? DateTimeOffset.UtcNow;
        var gsControl = seed.GroupControlNumber.ToString(CultureInfo.InvariantCulture).PadLeft(seed.GroupPadWidth, '0');
        var stControl = seed.TransactionControlNumber.ToString(CultureInfo.InvariantCulture).PadLeft(seed.TransactionPadWidth, '0');

        var segments = new List<string>
        {
            RenderIsa(isa),
            $"GS{e}FA{e}{PadRight(isa.InterchangeSenderId, 15).Trim()}{e}{PadRight(isa.InterchangeReceiverId, 15).Trim()}{e}{timestamp:yyyyMMdd}{e}{timestamp:HHmm}{e}{gsControl}{e}X{e}{NormalizeGsVersion(isa.InterchangeControlVersion)}{t}",
            $"ST{e}997{e}{stControl}{t}"
        };

        var segCount = 1;

        foreach (var group in groups)
        {
            segments.Add($"AK1{e}{group.FunctionalId}{e}{group.GroupControlNumber}{(string.IsNullOrEmpty(group.Version) ? string.Empty : $"{e}{group.Version}")}{t}");
            segCount++;

            foreach (var txn in group.TransactionAcks)
            {
                segments.Add($"AK2{e}{txn.TransactionSetId}{e}{txn.ControlNumber}{t}");
                segCount++;

                var txTail = txn.TransactionErrors.Skip(1).ToArray();
                var ak5Tail = txTail.Length > 0 ? $"{e}{string.Join(e, txTail)}" : string.Empty;
                segments.Add($"AK5{e}{txn.Disposition.Code()}{ak5Tail}{t}");
                segCount++;
            }

            var ak9Tail = group.GroupErrors.Count > 0 ? $"{e}{string.Join(e, group.GroupErrors)}" : string.Empty;
            segments.Add($"AK9{e}{group.Status.Code()}{e}{group.DeclaredTransactionCount}{e}{group.ReceivedTransactionCount}{e}{group.TransactionAcks.Count(tx => tx.Disposition == AckStatus.Rejected)}{ak9Tail}{t}");
            segCount++;
        }

        segments.Add($"SE{e}{segCount + 1}{e}{stControl}{t}");
        segments.Add($"GE{e}1{e}{gsControl}{t}");
        segments.Add($"IEA{e}1{e}{isa.InterchangeControlNumber}{t}");

        return new Rendered997(string.Concat(segments), envelope.ControlNumber, seed.GroupControlNumber, seed.TransactionControlNumber);
    }

    private static string RenderIsa(ISA isa)
    {
        var ts = isa.InterchangeDateTime ?? DateTimeOffset.UtcNow;
        var e = isa.ElementSep;
        var t = isa.SegmentTerminator;
        return string.Concat(
            "ISA", e,
            PadRight(isa.AuthInfoQual.Code(), 2), e,
            PadRight(isa.AuthInfo ?? string.Empty, 10), e,
            PadRight(isa.SecurityInfoQual.Code(), 2), e,
            PadRight(isa.SecurityInfo ?? string.Empty, 10), e,
            PadRight(isa.InterchangeSenderIdQual.Code(), 2), e,
            PadRight(isa.InterchangeSenderId, 15), e,
            PadRight(isa.InterchangeReceiverIdQual.Code(), 2), e,
            PadRight(isa.InterchangeReceiverId, 15), e,
            ts.ToString("yyMMdd", CultureInfo.InvariantCulture), e,
            ts.ToString("HHmm", CultureInfo.InvariantCulture), e,
            isa.RepetitionSep, e,
            PadRight(isa.InterchangeControlVersion, 5), e,
            PadLeft(isa.InterchangeControlNumber, 9), e,
            isa.AckRequested ? '1' : '0', e,
            isa.UsageIndicator.Code(), e,
            isa.SubElementSep, t);
    }

    private static string NormalizeGsVersion(string isaVersion) =>
        isaVersion.Length switch
        {
            4 => $"00{isaVersion}",
            5 => $"{isaVersion}0",
            _ => isaVersion
        };

    private static string PadRight(string value, int width) => (value ?? string.Empty).PadRight(width, ' ');

    private static string PadLeft(string value, int width) => (value ?? string.Empty).PadLeft(width, '0');
}
