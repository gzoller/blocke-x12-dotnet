using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class Render997Tests
{
    [Fact]
    public void Render997_renders_full_document_example_shape()
    {
        var envelope = new InterchangeEnvelope(
            new PartyId("ZZ", "THEM"),
            new PartyId("ZZ", "YOU"),
            501,
            UsageIndicator.Test,
            "00401",
            new DateTimeOffset(2025, 1, 5, 14, 30, 0, TimeSpan.Zero));

        var ack = new Ack997(
        [
            new Ack997Group(
                "IN",
                "1",
                "004010",
                1,
                1,
                AckStatus.Accepted,
                [
                    new TransactionSetAck997(
                        "810",
                        "0001",
                        AckStatus.Accepted,
                        ["A"],
                        [],
                        [])
                ],
                [])
        ]);

        var rendered = Assert.Single(Render997.Render(
            envelope,
            ack,
            AckPolicy.FullDocument,
            new ControlNumberSeed(123, 456, 6, 4)));

        Assert.Contains("ISA*00*          *00*          *ZZ*THEM", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("GS*FA*THEM*YOU*20250105*1430*000123*X*004010~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("ST*997*0456~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("AK1*IN*1*004010~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("AK2*810*0001~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("AK5*A~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("AK9*A*1*1*0~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("GE*1*000123~", rendered.Payload, StringComparison.Ordinal);
        Assert.Contains("IEA*1*000000501~", rendered.Payload, StringComparison.Ordinal);
    }
}
