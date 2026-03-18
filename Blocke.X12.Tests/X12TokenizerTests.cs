using Blocke.X12.Models;
using Blocke.X12.Parsing;

namespace Blocke.X12.Tests;

public sealed class X12TokenizerTests
{
    [Fact]
    public void Tokenizer_splits_ST_payload_into_segments_and_elements()
    {
        var tx = new X12Types.X12_ST("ST*850*0001~BEG*00*NE*PO123**20250101~SE*3*0001~");

        var tokens = X12Tokenizer.Tokenize(tx, X12Tokenizer.Delimiters.Default);

        Assert.Equal(new[] { "ST", "BEG", "SE" }, tokens.Select(t => t.SegmentId));
        Assert.Equal(new[] { "850", "0001" }, tokens[0].Elements.Select(e => e.Raw));
        Assert.Equal(new[] { "00", "NE", "PO123", "", "20250101" }, tokens[1].Elements.Select(e => e.Raw));
        Assert.Equal(new[] { "3", "0001" }, tokens[2].Elements.Select(e => e.Raw));
    }

    [Fact]
    public void Tokenizer_parses_composite_sub_elements()
    {
        var tx = new X12Types.X12_ST("ST*850*0001~REF*ZZ*ABC:123~SE*3*0001~");

        var tokens = X12Tokenizer.Tokenize(tx, X12Tokenizer.Delimiters.Default);

        Assert.Equal("REF", tokens[1].SegmentId);
        Assert.Equal("ABC:123", tokens[1].Elements[1].Raw);
        Assert.Equal(new[] { "ABC", "123" }, tokens[1].Elements[1].SubElements);
    }

    [Fact]
    public void Tokenizer_supports_non_standard_separators()
    {
        var tx = new X12Types.X12_ST("ST|850|0001!REF|ZZ|AA^BB!SE|3|0001!");
        var delimiters = new X12Tokenizer.Delimiters('|', '^', '!');

        var tokens = X12Tokenizer.Tokenize(tx, delimiters);

        Assert.Equal(new[] { "ST", "REF", "SE" }, tokens.Select(t => t.SegmentId));
        Assert.Equal("AA^BB", tokens[1].Elements[1].Raw);
        Assert.Equal(new[] { "AA", "BB" }, tokens[1].Elements[1].SubElements);
    }

    [Fact]
    public void Tokenizer_errors_when_separators_are_wrong_for_payload()
    {
        var tx = new X12Types.X12_ST("ST*850*0001~BEG*00*NE*PO123~SE*3*0001~");
        var wrong = new X12Tokenizer.Delimiters('S', ':', '~');

        var ex = Assert.Throws<X12ParseError>(() => X12Tokenizer.Tokenize(tx, wrong));

        Assert.Contains("invalid segment id", ex.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tokenizer_errors_on_invalid_segment_handling_empty_segment_id()
    {
        var tx = new X12Types.X12_ST("*850*0001~SE*2*0001~");

        var ex = Assert.Throws<X12ParseError>(() => X12Tokenizer.Tokenize(tx, X12Tokenizer.Delimiters.Default));

        Assert.Equal("UNKNOWN", ex.SegmentId);
        Assert.Contains("invalid segment id", ex.Problem, StringComparison.OrdinalIgnoreCase);
    }
}
