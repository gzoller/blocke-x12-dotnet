using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12.Tests;

public sealed class Ack997ParserTests
{
    [Fact]
    public void ParseAck997_parses_a_simple_997_st_payload()
    {
        var raw = new X12Types.X12_ST(
            "ST*997*0456~" +
            "AK1*PO*1*004010~" +
            "AK2*850*0001~" +
            "AK5*A~" +
            "AK9*A*1*1*0~" +
            "SE*6*0456~");

        var ack = X12Parser.ParseAck997(raw);

        Assert.Single(ack.Groups);
        var group = ack.Groups[0];
        Assert.Equal("PO", group.FunctionalId);
        Assert.Equal("1", group.GroupControlNumber);
        Assert.Equal(AckStatus.Accepted, group.Status);
        Assert.Equal(1, group.ReceivedTransactionCount);

        var tx = Assert.Single(group.TransactionAcks);
        Assert.Equal("850", tx.TransactionSetId);
        Assert.Equal("0001", tx.ControlNumber);
        Assert.Equal(AckStatus.Accepted, tx.Disposition);
    }

    [Fact]
    public void ParseAck997_fails_malformed_structure_missing_ak9()
    {
        var raw = new X12Types.X12_ST(
            "ST*997*0456~" +
            "AK1*PO*1*004010~" +
            "AK2*850*0001~" +
            "AK5*A~" +
            "SE*5*0456~");

        var err = Assert.Throws<X12ParseError>(() => X12Parser.ParseAck997(raw));

        Assert.Equal("AK9", err.SegmentId);
    }
}
