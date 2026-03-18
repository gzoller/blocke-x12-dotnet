namespace Blocke.X12.Models;

public enum FunctionalId
{
    PurchaseOrder,
    PurchaseAck,
    ShipNotice,
    Invoice,
    FuncAck,
    Unknown
}

public static class FunctionalIdExtensions
{
    public static string Code(this FunctionalId functionalId) =>
        functionalId switch
        {
            FunctionalId.PurchaseOrder => "PO",
            FunctionalId.PurchaseAck => "PR",
            FunctionalId.ShipNotice => "SH",
            FunctionalId.Invoice => "IN",
            FunctionalId.FuncAck => "FA",
            _ => "??"
        };

    public static bool TryParse(string code, out FunctionalId functionalId)
    {
        functionalId = code switch
        {
            "PO" => FunctionalId.PurchaseOrder,
            "PR" => FunctionalId.PurchaseAck,
            "SH" => FunctionalId.ShipNotice,
            "IN" => FunctionalId.Invoice,
            "FA" => FunctionalId.FuncAck,
            _ => FunctionalId.Unknown
        };

        return functionalId != FunctionalId.Unknown;
    }
}

public enum UsageIndicator
{
    Test,
    Production,
    Information,
    Unknown
}

public static class UsageIndicatorExtensions
{
    public static string Code(this UsageIndicator usageIndicator) =>
        usageIndicator switch
        {
            UsageIndicator.Test => "T",
            UsageIndicator.Production => "P",
            UsageIndicator.Information => "I",
            UsageIndicator.Unknown => "?",
            _ => throw new ArgumentOutOfRangeException(nameof(usageIndicator), usageIndicator, null)
        };

    public static bool TryParse(string code, out UsageIndicator usageIndicator)
    {
        usageIndicator = code switch
        {
            "T" => UsageIndicator.Test,
            "P" => UsageIndicator.Production,
            "I" => UsageIndicator.Information,
            _ => UsageIndicator.Unknown
        };

        return usageIndicator != UsageIndicator.Unknown;
    }
}

public enum InterchangeIdQualifier
{
    Duns,
    MutuallyDefined,
    Unknown
}

public static class InterchangeIdQualifierExtensions
{
    public static string Code(this InterchangeIdQualifier qualifier) =>
        qualifier switch
        {
            InterchangeIdQualifier.Duns => "01",
            InterchangeIdQualifier.MutuallyDefined => "ZZ",
            InterchangeIdQualifier.Unknown => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(qualifier), qualifier, null)
        };

    public static InterchangeIdQualifier Parse(string code) =>
        code switch
        {
            "01" => InterchangeIdQualifier.Duns,
            "ZZ" => InterchangeIdQualifier.MutuallyDefined,
            _ => InterchangeIdQualifier.Unknown
        };
}

public enum AuthInfoQualifier
{
    NoAuthPresent,
    UCSCommId,
    EDXCommId,
    AdditionalDataId,
    Unknown
}

public static class AuthInfoQualifierExtensions
{
    public static string Code(this AuthInfoQualifier qualifier) =>
        qualifier switch
        {
            AuthInfoQualifier.NoAuthPresent => "00",
            AuthInfoQualifier.UCSCommId => "01",
            AuthInfoQualifier.EDXCommId => "02",
            AuthInfoQualifier.AdditionalDataId => "03",
            AuthInfoQualifier.Unknown => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(qualifier), qualifier, null)
        };

    public static AuthInfoQualifier Parse(string code) =>
        code switch
        {
            "00" => AuthInfoQualifier.NoAuthPresent,
            "01" => AuthInfoQualifier.UCSCommId,
            "02" => AuthInfoQualifier.EDXCommId,
            "03" => AuthInfoQualifier.AdditionalDataId,
            _ => AuthInfoQualifier.Unknown
        };
}

public enum SecurityInfoQualifier
{
    NoSecurity,
    Password,
    Unknown
}

public static class SecurityInfoQualifierExtensions
{
    public static string Code(this SecurityInfoQualifier qualifier) =>
        qualifier switch
        {
            SecurityInfoQualifier.NoSecurity => "00",
            SecurityInfoQualifier.Password => "01",
            SecurityInfoQualifier.Unknown => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(qualifier), qualifier, null)
        };

    public static SecurityInfoQualifier Parse(string code) =>
        code switch
        {
            "00" => SecurityInfoQualifier.NoSecurity,
            "01" => SecurityInfoQualifier.Password,
            _ => SecurityInfoQualifier.Unknown
        };
}

public enum AckStatus
{
    Accepted,
    Rejected,
    AcceptedWithErrors
}

public enum HLRole
{
    Shipment,
    Order,
    Pack,
    Item
}

public static class HLRoleExtensions
{
    public static string Code(this HLRole role) =>
        role switch
        {
            HLRole.Shipment => "S",
            HLRole.Order => "O",
            HLRole.Pack => "P",
            HLRole.Item => "I",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

    public static HLRole? FromCode(string code) =>
        code switch
        {
            "S" => HLRole.Shipment,
            "O" => HLRole.Order,
            "P" => HLRole.Pack,
            "I" => HLRole.Item,
            _ => null
        };
}

public static class AckStatusExtensions
{
    public static string Code(this AckStatus status) =>
        status switch
        {
            AckStatus.Accepted => "A",
            AckStatus.Rejected => "R",
            AckStatus.AcceptedWithErrors => "E",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public static bool TryParse(string code, out AckStatus status)
    {
        status = code switch
        {
            "A" => AckStatus.Accepted,
            "R" => AckStatus.Rejected,
            "E" => AckStatus.AcceptedWithErrors,
            _ => default
        };

        return code is "A" or "R" or "E";
    }
}

public enum TransactionSyntaxError
{
    SegmentCountMismatch,
    StructuralError,
    ControlNumberMismatch,
    UnsupportedTransactionSet,
    TransactionRejected
}

public static class TransactionSyntaxErrorExtensions
{
    public static string Code(this TransactionSyntaxError syntaxError) =>
        syntaxError switch
        {
            TransactionSyntaxError.SegmentCountMismatch => "4",
            TransactionSyntaxError.StructuralError => "5",
            TransactionSyntaxError.ControlNumberMismatch => "6",
            TransactionSyntaxError.UnsupportedTransactionSet => "3",
            TransactionSyntaxError.TransactionRejected => "6",
            _ => throw new ArgumentOutOfRangeException(nameof(syntaxError), syntaxError, null)
        };
}

public enum SegmentSyntaxError
{
    UnrecognizedSegment,
    UnexpectedSegment,
    MandatorySegmentMissing,
    LoopOccursOverMaxTimes,
    SegmentExceedsMaxUse,
    SegmentNotInTransactionSet,
    SegmentOutOfSequence,
    SegmentMissing
}

public static class SegmentSyntaxErrorExtensions
{
    public static string Code(this SegmentSyntaxError error) =>
        error switch
        {
            SegmentSyntaxError.UnrecognizedSegment => "1",
            SegmentSyntaxError.UnexpectedSegment => "2",
            SegmentSyntaxError.MandatorySegmentMissing => "3",
            SegmentSyntaxError.LoopOccursOverMaxTimes => "4",
            SegmentSyntaxError.SegmentExceedsMaxUse => "5",
            SegmentSyntaxError.SegmentNotInTransactionSet => "6",
            SegmentSyntaxError.SegmentOutOfSequence => "7",
            SegmentSyntaxError.SegmentMissing => "8",
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
        };
}

public enum ElementSyntaxError
{
    MandatoryElementMissing,
    ConditionalElementMissing,
    TooManyElements,
    InvalidCharacter,
    InvalidCodeValue,
    InvalidNumericValue,
    InvalidDate,
    InvalidTime
}

public enum SupportedX12Version
{
    V4010,
    V5010
}

public static class SupportedX12VersionExtensions
{
    public static string ToMajor(this SupportedX12Version version) =>
        version switch
        {
            SupportedX12Version.V4010 => "004010",
            SupportedX12Version.V5010 => "005010",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

    public static SupportedX12Version FromMajor(string major) =>
        major switch
        {
            "004010" => SupportedX12Version.V4010,
            "005010" => SupportedX12Version.V5010,
            _ => throw new ArgumentException($"Unsupported X12 version {major}", nameof(major))
        };
}
