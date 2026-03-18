namespace Blocke.X12.Models;

public sealed record X12Envelope(
    ISA Isa,
    IReadOnlyList<X12FunctionalGroup> FunctionalGroups,
    X12Types.X12_ST Ack);

public sealed record X12FunctionalGroup(
    FunctionalId FunctionalId,
    string AppSender,
    string AppReceiver,
    X12Version Version,
    string ControlNumber,
    IReadOnlyList<X12Types.X12_ST> Transactions);
