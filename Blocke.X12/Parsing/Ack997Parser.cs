using Blocke.X12.Models;

namespace Blocke.X12.Parsing;

public static class Ack997Parser
{
    public static Ack997 Parse(X12Types.X12_ST st)
    {
        var raw = st.Value.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new X12ParseError("ST", "Empty 997 payload", 0, 0, st.Value);
        }

        var segmentTerm = raw[^1];
        var elementSep = InferElementSep(raw);
        var segments = raw
            .Split(segmentTerm, StringSplitOptions.None)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        if (segments.Length == 0)
        {
            throw new X12ParseError("ST", "No segments in 997 payload", 0, 0, st.Value);
        }

        var first = Split(segments[0], elementSep);
        if (first.ElementAtOrDefault(0) != "ST" || first.ElementAtOrDefault(1) != "997")
        {
            throw new X12ParseError(first.ElementAtOrDefault(0) ?? "(unknown)", "Payload is not ST*997", 0, 0, segments[0]);
        }

        var groups = new List<Ack997Group>();
        Ack997GroupBuilder? currentGroup = null;
        Ack997TxnBuilder? currentTxn = null;
        var idx = 1;
        var done = false;

        while (idx < segments.Length && !done)
        {
            var seg = segments[idx];
            var parts = Split(seg, elementSep);
            var segId = parts.ElementAtOrDefault(0) ?? string.Empty;

            switch (segId)
            {
                case "AK1":
                    FlushTxnIntoGroup(currentGroup, currentTxn, idx, seg);
                    currentTxn = null;
                    if (currentGroup is not null)
                    {
                        groups.Add(currentGroup.Build(idx, seg));
                    }
                    currentGroup = Ack997GroupBuilder.FromAk1(parts, idx, seg);
                    break;
                case "AK2":
                    if (currentGroup is null)
                    {
                        throw new X12ParseError("AK2", "AK2 without AK1 group context", idx, 0, seg);
                    }
                    if (currentTxn is not null)
                    {
                        currentGroup.AddTxn(currentTxn);
                    }
                    currentTxn = Ack997TxnBuilder.FromAk2(parts, idx, seg);
                    break;
                case "AK3":
                    (currentTxn ?? throw new X12ParseError("AK3", "AK3 without AK2 transaction context", idx, 0, seg)).AddAk3(parts);
                    break;
                case "AK4":
                    (currentTxn ?? throw new X12ParseError("AK4", "AK4 without AK2 transaction context", idx, 0, seg)).AddAk4(parts);
                    break;
                case "AK5":
                    (currentTxn ?? throw new X12ParseError("AK5", "AK5 without AK2 transaction context", idx, 0, seg)).SetAk5(parts, idx, seg);
                    break;
                case "AK9":
                    if (currentGroup is null)
                    {
                        throw new X12ParseError("AK9", "AK9 without AK1 group context", idx, 0, seg);
                    }
                    if (currentTxn is not null)
                    {
                        currentGroup.AddTxn(currentTxn);
                        currentTxn = null;
                    }
                    currentGroup.SetAk9(parts, idx, seg);
                    break;
                case "SE":
                    FlushTxnIntoGroup(currentGroup, currentTxn, idx, seg);
                    currentTxn = null;
                    if (currentGroup is not null)
                    {
                        groups.Add(currentGroup.Build(idx, seg));
                    }
                    currentGroup = null;
                    done = true;
                    break;
            }

            idx++;
        }

        if (!done)
        {
            throw new X12ParseError("SE", "Missing SE in 997 payload", segments.Length - 1, 0, segments[^1]);
        }

        return new Ack997(groups);
    }

    private static void FlushTxnIntoGroup(Ack997GroupBuilder? group, Ack997TxnBuilder? txn, int idx, string raw)
    {
        if (txn is null)
        {
            return;
        }

        if (group is null)
        {
            throw new X12ParseError("AK2", "Transaction context without group context", idx, 0, raw);
        }

        group.AddTxn(txn);
    }

    private static string[] Split(string segment, char elementSep) =>
        segment.Split(elementSep, StringSplitOptions.None);

    private static char InferElementSep(string raw)
    {
        var star = raw.IndexOf('*');
        if (star > 1)
        {
            return '*';
        }

        return raw.Length >= 4 ? raw[3] : '*';
    }

    private sealed class Ack997GroupBuilder
    {
        private Ack997GroupBuilder(string functionalId, string groupControlNumber, string version)
        {
            FunctionalId = functionalId;
            GroupControlNumber = groupControlNumber;
            Version = version;
        }

        private string FunctionalId { get; }
        private string GroupControlNumber { get; }
        private string Version { get; }
        private int DeclaredTransactionCount { get; set; }
        private AckStatus Status { get; set; } = AckStatus.Accepted;
        private bool SawAk9 { get; set; }
        private List<TransactionSetAck997> Txns { get; } = new();
        private List<string> GroupErrors { get; } = new();

        public static Ack997GroupBuilder FromAk1(string[] parts, int idx, string raw)
        {
            if (parts.Length < 3)
            {
                throw new X12ParseError("AK1", "AK1 requires functionalId and groupControlNumber", idx, 0, raw);
            }

            return new Ack997GroupBuilder(parts[1], parts[2], parts.ElementAtOrDefault(3) ?? string.Empty);
        }

        public void AddTxn(Ack997TxnBuilder txn) => Txns.Add(txn.Build());

        public void SetAk9(string[] parts, int idx, string raw)
        {
            SawAk9 = true;
            var statusCode = parts.ElementAtOrDefault(1) ?? string.Empty;
            if (!AckStatusExtensions.TryParse(statusCode, out var status))
            {
                throw new X12ParseError("AK9", $"Invalid AK9 status '{statusCode}'", idx, 0, raw);
            }

            Status = status;
            DeclaredTransactionCount = int.TryParse(parts.ElementAtOrDefault(2), out var declared) ? declared : 0;
            foreach (var err in parts.Skip(5).Where(p => !string.IsNullOrEmpty(p)))
            {
                GroupErrors.Add(err);
            }
        }

        public Ack997Group Build(int idx, string raw)
        {
            if (!SawAk9)
            {
                throw new X12ParseError("AK9", "Missing AK9 for AK1 group", idx, 0, raw);
            }

            return new Ack997Group(
                FunctionalId,
                GroupControlNumber,
                Version,
                DeclaredTransactionCount,
                Txns.Count,
                Status,
                Txns.ToArray(),
                GroupErrors.ToArray());
        }
    }

    private sealed class Ack997TxnBuilder
    {
        private Ack997TxnBuilder(string transactionSetId, string controlNumber)
        {
            TransactionSetId = transactionSetId;
            ControlNumber = controlNumber;
        }

        private string TransactionSetId { get; }
        private string ControlNumber { get; }
        private AckStatus Disposition { get; set; } = AckStatus.Accepted;
        private bool SawAk5 { get; set; }
        private List<string> TransactionErrors { get; } = new();
        private List<SegmentAckError997> SegmentErrors { get; } = new();
        private List<ElementAckError997> ElementErrors { get; } = new();

        public static Ack997TxnBuilder FromAk2(string[] parts, int idx, string raw)
        {
            if (parts.Length < 3)
            {
                throw new X12ParseError("AK2", "AK2 requires transactionSetId and controlNumber", idx, 0, raw);
            }

            return new Ack997TxnBuilder(parts[1], parts[2]);
        }

        public void SetAk5(string[] parts, int idx, string raw)
        {
            SawAk5 = true;
            var code = parts.ElementAtOrDefault(1) ?? string.Empty;
            if (!AckStatusExtensions.TryParse(code, out var disposition))
            {
                throw new X12ParseError("AK5", $"Invalid AK5 status '{code}'", idx, 0, raw);
            }

            Disposition = disposition;
            TransactionErrors.Add(code);
            foreach (var err in parts.Skip(2).Where(p => !string.IsNullOrEmpty(p)))
            {
                TransactionErrors.Add(err);
            }
        }

        public void AddAk3(string[] parts)
        {
            SegmentErrors.Add(new SegmentAckError997(
                parts.ElementAtOrDefault(1) ?? string.Empty,
                int.TryParse(parts.ElementAtOrDefault(2), out var pos) ? pos : 0,
                string.IsNullOrEmpty(parts.ElementAtOrDefault(3)) ? null : parts[3],
                parts.ElementAtOrDefault(4) ?? string.Empty));
        }

        public void AddAk4(string[] parts)
        {
            ElementErrors.Add(new ElementAckError997(
                string.Empty,
                int.TryParse(parts.ElementAtOrDefault(1), out var segPos) ? segPos : 0,
                int.TryParse(parts.ElementAtOrDefault(2), out var elemPos) ? elemPos : 0,
                int.TryParse(parts.ElementAtOrDefault(3), out var compPos) ? compPos : null,
                parts.ElementAtOrDefault(4) ?? string.Empty,
                string.IsNullOrEmpty(parts.ElementAtOrDefault(5)) ? null : parts[5]));
        }

        public TransactionSetAck997 Build()
        {
            if (!SawAk5)
            {
                throw new X12ParseError("AK5", $"Missing AK5 for AK2 {TransactionSetId}:{ControlNumber}", 0, 0, string.Empty);
            }

            return new TransactionSetAck997(
                TransactionSetId,
                ControlNumber,
                Disposition,
                TransactionErrors.ToArray(),
                SegmentErrors.ToArray(),
                ElementErrors.ToArray());
        }
    }
}
