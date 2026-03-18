using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12.Tests;

public sealed class PublicSurfaceGapTests
{
    [Fact]
    public void X12Specification_helpers_create_and_replace_segments()
    {
        var original = new SegmentNodeSpec("HDR", Presence.Required, 1, [X12SpecificationHelpers.E("HDR01", X12Type.AN, 1, 3)], []);
        var replacement = new SegmentNodeSpec("HDR", Presence.Required, 1, [X12SpecificationHelpers.E("HDR01", X12Type.ID, 2, 2, codeListRef: "HDR01")], []);

        var updated = X12SpecificationHelpers.ReplaceSegment([original], "HDR", replacement);

        var seg = Assert.IsType<SegmentNodeSpec>(Assert.Single(updated));
        Assert.Equal(X12Type.ID, Assert.Single(seg.Elements).DataType);
        Assert.Equal("HDR01", Assert.Single(seg.Elements).CodeListRef);
    }

    [Fact]
    public void X12ParseError_can_carry_ack_codes()
    {
        var ex = new X12ParseError(
            "REF",
            "Unexpected segment",
            3,
            42,
            "REF*AA~",
            SegmentSyntaxError.UnexpectedSegment,
            (2, ElementSyntaxError.InvalidCharacter));

        Assert.Equal(SegmentSyntaxError.UnexpectedSegment, ex.SegmentAckCode);
        Assert.Equal((2, ElementSyntaxError.InvalidCharacter), ex.ElementAckCode);
    }

    [Fact]
    public void Syntax_error_enums_expose_x12_codes()
    {
        Assert.Equal("2", SegmentSyntaxError.UnexpectedSegment.Code());
        Assert.Equal("3", TransactionSyntaxError.UnsupportedTransactionSet.Code());
        Assert.Equal("6", TransactionSyntaxError.TransactionRejected.Code());
    }

    [Fact]
    public void Specification_provider_interface_is_usable()
    {
        IX12SpecificationProvider provider = new Provider(new X12Specification("999", "005010", [], new Dictionary<string, IReadOnlySet<string>>()));

        Assert.Equal("999", provider.SpecData.TransactionSetId);
    }

    private sealed record Provider(X12Specification SpecData) : IX12SpecificationProvider;
}
