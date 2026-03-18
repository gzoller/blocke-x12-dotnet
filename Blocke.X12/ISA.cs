namespace Blocke.X12.Models;

public sealed record ISA(
    char ElementSep,
    char SubElementSep,
    char SegmentTerminator,
    char RepetitionSep,
    AuthInfoQualifier AuthInfoQual,
    string? AuthInfo,
    SecurityInfoQualifier SecurityInfoQual,
    string? SecurityInfo,
    InterchangeIdQualifier InterchangeSenderIdQual,
    string InterchangeSenderId,
    InterchangeIdQualifier InterchangeReceiverIdQual,
    string InterchangeReceiverId,
    DateTimeOffset? InterchangeDateTime,
    string InterchangeControlVersion,
    string InterchangeControlNumber,
    bool AckRequested,
    UsageIndicator UsageIndicator);

public sealed record IEA(
    int NumberOfFunctionalGroups,
    string InterchangeControlNumber);
