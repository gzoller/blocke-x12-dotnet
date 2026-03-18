using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12.Tests;

public sealed class PublicApiAuditTests
{
    [Fact]
    public void Isa_parser_exposes_typed_qualifiers()
    {
        var raw = "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *250101*0800*U*00401*000000001*0*T*:~";

        var isa = ISAParser.Parse(raw);

        Assert.Equal(AuthInfoQualifier.NoAuthPresent, isa.AuthInfoQual);
        Assert.Equal(SecurityInfoQualifier.NoSecurity, isa.SecurityInfoQual);
        Assert.Equal(InterchangeIdQualifier.MutuallyDefined, isa.InterchangeSenderIdQual);
        Assert.Equal(InterchangeIdQualifier.MutuallyDefined, isa.InterchangeReceiverIdQual);
    }

    [Fact]
    public void Interchange_envelope_builds_typed_isa()
    {
        var env = new InterchangeEnvelope(
            new PartyId(InterchangeIdQualifier.MutuallyDefined, "SENDER"),
            new PartyId(InterchangeIdQualifier.Duns, "RECEIVER"),
            12,
            UsageIndicator.Production,
            "00501");

        var isa = env.ToIsa();

        Assert.Equal(InterchangeIdQualifier.MutuallyDefined, isa.InterchangeSenderIdQual);
        Assert.Equal(InterchangeIdQualifier.Duns, isa.InterchangeReceiverIdQual);
        Assert.Equal(AuthInfoQualifier.NoAuthPresent, isa.AuthInfoQual);
        Assert.Equal(SecurityInfoQualifier.NoSecurity, isa.SecurityInfoQual);
    }

    [Fact]
    public void Qualifier_helpers_round_trip_known_codes()
    {
        Assert.Equal("ZZ", InterchangeIdQualifier.MutuallyDefined.Code());
        Assert.Equal(InterchangeIdQualifier.MutuallyDefined, InterchangeIdQualifierExtensions.Parse("ZZ"));
        Assert.Equal("00", AuthInfoQualifier.NoAuthPresent.Code());
        Assert.Equal(AuthInfoQualifier.NoAuthPresent, AuthInfoQualifierExtensions.Parse("00"));
        Assert.Equal("01", SecurityInfoQualifier.Password.Code());
        Assert.Equal(SecurityInfoQualifier.Password, SecurityInfoQualifierExtensions.Parse("01"));
    }

    [Fact]
    public void Iea_model_exists_with_scala_shape()
    {
        var iea = new IEA(2, "000000123");

        Assert.Equal(2, iea.NumberOfFunctionalGroups);
        Assert.Equal("000000123", iea.InterchangeControlNumber);
    }
}
