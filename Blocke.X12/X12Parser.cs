using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12;

public static class X12Parser
{
    public static ParsedTransactionValidationResult ParseTransaction(X12Types.X12_ST tx, X12Specification spec) =>
        X12Validator.ParseTransaction(tx, spec);

    public static Ack997 ParseAck997(X12Types.X12_ST tx) =>
        Ack997Parser.Parse(tx);

    public static X12Envelope Parse(string rawX12, ParseMode mode = ParseMode.Normal)
    {
        var (isa, rawSegments) = ISAParser.ParseIsaSegment(rawX12);
        var ctx = new ParseContext(isa, rawSegments);
        var allowCountMismatch = mode == ParseMode.Permissive;

        var parsedGroups = ScanInterchange(ctx, allowCountMismatch, 1);
        var ackState = BuildPass1AckState(parsedGroups);

        var groups = parsedGroups
            .Select(g => new X12FunctionalGroup(
                g.FunctionalId,
                g.AppSender,
                g.AppReceiver,
                g.Version,
                g.ControlNumber,
                g.Transactions.Select(t => t.Raw).ToArray()))
            .ToArray();

        return new X12Envelope(isa, groups, Render997StOnly(ctx, ackState));
    }

    private static IReadOnlyList<ParsedGroup> ScanInterchange(ParseContext ctx, bool allowCountMismatch, int startIdx)
    {
        var groups = new List<ParsedGroup>();
        var idx = startIdx;

        while (true)
        {
            if (idx >= ctx.RawSegments.Count)
            {
                throw new X12ParseError("IEA", "Missing IEA segment", ctx.RawSegments.Count, 0, ctx.RawSegments.LastOrDefault() ?? string.Empty);
            }

            var segId = ctx.SegId(ctx.RawSegments[idx]);
            switch (segId)
            {
                case "GS":
                    var (group, nextIdx) = ScanGroup(ctx, allowCountMismatch, idx);
                    groups.Add(group);
                    idx = nextIdx;
                    break;
                case "IEA":
                    ValidateIEA(ctx, allowCountMismatch, idx, groups.Count);
                    if (idx + 1 < ctx.RawSegments.Count)
                    {
                        throw new X12ParseError(
                            ctx.SegId(ctx.RawSegments[idx + 1]),
                            "Unexpected segment after IEA",
                            idx + 1,
                            0,
                            ctx.RawSegments[idx + 1]);
                    }

                    return groups;
                default:
                    throw new X12ParseError(segId, "Unexpected segment at interchange level", idx, 0, ctx.RawSegments[idx]);
            }
        }
    }

    private static (ParsedGroup Group, int NextIdx) ScanGroup(ParseContext ctx, bool allowCountMismatch, int gsIdx)
    {
        var gsRaw = ctx.RawSegments[gsIdx];
        var gsFields = ctx.Fields(gsRaw);
        if (gsFields.Length < 9)
        {
            throw new X12ParseError("GS", $"GS requires 8 elements, found {gsFields.Length - 1}", gsIdx, 0, gsRaw);
        }

        if (!FunctionalIdExtensions.TryParse(gsFields[1], out var functionalId) ||
            !X12Version.TryParse(gsFields[8], out var version))
        {
            throw new X12ParseError("GS", $"Invalid GS01 '{gsFields[1]}' or GS08 '{gsFields[8]}'", gsIdx, 0, gsRaw);
        }

        var txns = new List<ParsedTxn>();
        var idx = gsIdx + 1;

        while (true)
        {
            if (idx >= ctx.RawSegments.Count)
            {
                throw new X12ParseError("GE", "Missing GE at end of functional group", gsIdx, 0, ctx.RawSegments[gsIdx]);
            }

            var segId = ctx.SegId(ctx.RawSegments[idx]);
            switch (segId)
            {
                case "ST":
                    var (txn, nextIdx) = ScanTransaction(ctx, allowCountMismatch, idx);
                    txns.Add(txn);
                    idx = nextIdx;
                    break;
                case "GE":
                    var geCountMismatch = ValidateGE(ctx, allowCountMismatch, idx, gsFields[6], txns.Count);
                    return (new ParsedGroup(functionalId, gsFields[2], gsFields[3], version!, gsFields[6], txns, geCountMismatch), idx + 1);
                case "IEA":
                    throw new X12ParseError("GE", "Missing GE before IEA", idx, 0, ctx.RawSegments[idx]);
                default:
                    throw new X12ParseError(segId, "Unexpected segment inside functional group", idx, 0, ctx.RawSegments[idx]);
            }
        }
    }

    private static (ParsedTxn Txn, int NextIdx) ScanTransaction(ParseContext ctx, bool allowCountMismatch, int stIdx)
    {
        var stRaw = ctx.RawSegments[stIdx];
        var stFields = ctx.Fields(stRaw);
        if (stFields.Length < 3)
        {
            throw new X12ParseError("ST", $"ST requires 2 elements, found {stFields.Length - 1}", stIdx, 0, stRaw);
        }

        var seIdx = FindSE(ctx, stIdx + 1);
        var seRaw = ctx.RawSegments[seIdx];
        var seFields = ctx.Fields(seRaw);
        if (seFields.Length < 3)
        {
            throw new X12ParseError("SE", $"SE requires 2 elements, found {seFields.Length - 1}", seIdx, 0, seRaw);
        }

        var observedSegCount = seIdx - stIdx + 1;
        var countMismatch = int.TryParse(seFields[1], out var declaredSegCount) && declaredSegCount != observedSegCount;

        if (countMismatch && !allowCountMismatch)
        {
            throw new X12ParseError("SE", $"SE01 ({seFields[1]}) does not match observed segment count ({observedSegCount})", seIdx, 0, seRaw);
        }

        if (seFields[2] != stFields[2])
        {
            throw new X12ParseError("SE", $"SE02 ({seFields[2]}) does not match ST02 ({stFields[2]})", seIdx, 0, seRaw);
        }

        var rawTxn = string.Concat(ctx.RawSegments.Skip(stIdx).Take(seIdx - stIdx + 1).Select(s => s + ctx.SegmentTerm));
        return (new ParsedTxn(stFields[1], stFields[2], new X12Types.X12_ST(rawTxn), countMismatch), seIdx + 1);
    }

    private static int FindSE(ParseContext ctx, int idx)
    {
        while (idx < ctx.RawSegments.Count)
        {
            var segId = ctx.SegId(ctx.RawSegments[idx]);
            if (segId == "SE")
            {
                return idx;
            }

            if (segId is "GE" or "IEA")
            {
                throw new X12ParseError("SE", "Missing SE for ST", idx, 0, ctx.RawSegments[idx]);
            }

            idx++;
        }

        throw new X12ParseError("SE", "Missing SE for ST", idx, 0, string.Empty);
    }

    private static bool ValidateGE(ParseContext ctx, bool allowCountMismatch, int geIdx, string expectedControl, int observedTxCount)
    {
        var geRaw = ctx.RawSegments[geIdx];
        var geFields = ctx.Fields(geRaw);
        if (geFields.Length < 3)
        {
            throw new X12ParseError("GE", $"GE requires 2 elements, found {geFields.Length - 1}", geIdx, 0, geRaw);
        }

        var countMismatch = int.TryParse(geFields[1], out var declaredCount) && declaredCount != observedTxCount;
        if (geFields[2] != expectedControl)
        {
            throw new X12ParseError("GE", $"GE02 ({geFields[2]}) does not match GS06 ({expectedControl})", geIdx, 0, geRaw);
        }

        if (countMismatch && !allowCountMismatch)
        {
            throw new X12ParseError("GE", $"GE01 ({geFields[1]}) does not match observed ST count ({observedTxCount})", geIdx, 0, geRaw);
        }

        return countMismatch;
    }

    private static bool ValidateIEA(ParseContext ctx, bool allowCountMismatch, int ieaIdx, int observedGroupCount)
    {
        var ieaRaw = ctx.RawSegments[ieaIdx];
        var ieaFields = ctx.Fields(ieaRaw);
        if (ieaFields.Length < 3)
        {
            throw new X12ParseError("IEA", $"IEA requires 2 elements, found {ieaFields.Length - 1}", ieaIdx, 0, ieaRaw);
        }

        var countMismatch = int.TryParse(ieaFields[1], out var declaredGroups) && declaredGroups != observedGroupCount;
        if (ieaFields[2] != ctx.Isa.InterchangeControlNumber)
        {
            throw new X12ParseError("IEA", $"IEA02 ({ieaFields[2]}) does not match ISA13 ({ctx.Isa.InterchangeControlNumber})", ieaIdx, 0, ieaRaw);
        }

        if (countMismatch && !allowCountMismatch)
        {
            throw new X12ParseError("IEA", $"IEA01 ({ieaFields[1]}) does not match observed GS count ({observedGroupCount})", ieaIdx, 0, ieaRaw);
        }

        return countMismatch;
    }

    private static AckBuildState BuildPass1AckState(IReadOnlyList<ParsedGroup> groups)
    {
        var groupStates = groups.Select(g =>
        {
            var txStates = g.Transactions
                .Select(t => new AckTxnState(
                    t.TransactionSetId,
                    t.ControlNumber,
                    t.CountMismatch ? new[] { TransactionSyntaxError.SegmentCountMismatch } : Array.Empty<TransactionSyntaxError>()))
                .ToArray();

            var groupErrors = g.GeCountMismatch
                ? new[] { TransactionSyntaxError.SegmentCountMismatch }
                : Array.Empty<TransactionSyntaxError>();

            var rejectedCount = txStates.Count(t => t.Status == AckStatus.Rejected);
            var acceptedCount = txStates.Length - rejectedCount;
            var groupStatus =
                rejectedCount == txStates.Length && txStates.Length > 0 ? AckStatus.Rejected :
                groupErrors.Length > 0 || txStates.Any(t => t.TransactionErrors.Length > 0) ? AckStatus.AcceptedWithErrors :
                AckStatus.Accepted;

            return new AckGroupState(
                g.FunctionalId.Code(),
                g.ControlNumber,
                txStates.Length,
                acceptedCount,
                rejectedCount,
                groupStatus,
                groupErrors,
                txStates);
        }).ToArray();

        return new AckBuildState(groupStates);
    }

    private static X12Types.X12_ST Render997StOnly(ParseContext ctx, AckBuildState ack)
    {
        var e = ctx.Isa.ElementSep;
        var t = ctx.Isa.SegmentTerminator;
        const string stControl = "0001";
        var segCount = 0;
        var parts = new List<string>();

        void Seg(string s)
        {
            parts.Add(s);
            segCount++;
        }

        Seg($"ST{e}997{e}{stControl}{t}");

        foreach (var gs in ack.FunctionalGroups)
        {
            Seg($"AK1{e}{gs.FunctionalId}{e}{gs.GroupControlNumber}{t}");
            foreach (var stx in gs.Transactions)
            {
                Seg($"AK2{e}{stx.TransactionSetId}{e}{stx.ControlNumber}{t}");
                var txCodes = stx.TransactionErrors.Select(err => err.Code()).ToArray();
                var ak5Tail = txCodes.Length > 0 ? $"{e}{string.Join(e, txCodes)}" : string.Empty;
                Seg($"AK5{e}{stx.Status.Code()}{ak5Tail}{t}");
            }

            var groupCodes = gs.GroupErrors.Select(err => err.Code()).ToArray();
            var ak9Tail = groupCodes.Length > 0 ? $"{e}{string.Join(e, groupCodes)}" : string.Empty;
            Seg($"AK9{e}{gs.Status.Code()}{e}{gs.ReceivedCount}{e}{gs.AcceptedCount}{e}{gs.RejectedCount}{ak9Tail}{t}");
        }

        var seCount = segCount + 1;
        parts.Add($"SE{e}{seCount}{e}{stControl}{t}");
        return new X12Types.X12_ST(string.Concat(parts));
    }

    private sealed record ParsedTxn(string TransactionSetId, string ControlNumber, X12Types.X12_ST Raw, bool CountMismatch);

    private sealed record ParsedGroup(
        FunctionalId FunctionalId,
        string AppSender,
        string AppReceiver,
        X12Version Version,
        string ControlNumber,
        IReadOnlyList<ParsedTxn> Transactions,
        bool GeCountMismatch);

    private sealed record AckTxnState(string TransactionSetId, string ControlNumber, TransactionSyntaxError[] TransactionErrors)
    {
        public AckStatus Status =>
            TransactionErrors.Any(err => err != TransactionSyntaxError.SegmentCountMismatch) ? AckStatus.Rejected :
            TransactionErrors.Length > 0 ? AckStatus.AcceptedWithErrors :
            AckStatus.Accepted;
    }

    private sealed record AckGroupState(
        string FunctionalId,
        string GroupControlNumber,
        int ReceivedCount,
        int AcceptedCount,
        int RejectedCount,
        AckStatus Status,
        TransactionSyntaxError[] GroupErrors,
        AckTxnState[] Transactions);

    private sealed record AckBuildState(AckGroupState[] FunctionalGroups);

    private sealed class ParseContext
    {
        public ParseContext(ISA isa, IReadOnlyList<string> rawSegments)
        {
            Isa = isa;
            RawSegments = rawSegments;
            SegmentTerm = isa.SegmentTerminator.ToString();
        }

        public ISA Isa { get; }

        public IReadOnlyList<string> RawSegments { get; }

        public string SegmentTerm { get; }

        public string SegId(string seg)
        {
            var idx = seg.IndexOf(Isa.ElementSep);
            return idx >= 0 ? seg[..idx].Trim() : seg.Trim();
        }

        public string[] Fields(string seg) =>
            seg.Split([Isa.ElementSep], StringSplitOptions.None);
    }
}
