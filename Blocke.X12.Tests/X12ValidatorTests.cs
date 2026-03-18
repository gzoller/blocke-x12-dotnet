using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public class X12ValidatorTests
{
    private static readonly X12Specification Spec = SpecPack.Unpack([
        "X|999|005010",
        "SEG|HDR|M|1",
        "ELM|HDR01|M|AN|1|3|",
        "SEG|DTL|O|99",
        "ELM|DTL01|M|ID|1|5|CODE_LIST",
        "CODE|CODE_LIST|A,B"
    ]);

    [Fact]
    public void ParseTransaction_matches_x12parser_surface()
    {
        var raw = new X12Types.X12_ST("ST*999*1001~HDR*ABC~DTL*A~SE*4*1001~");

        var viaParser = X12Parser.ParseTransaction(raw, Spec);
        var viaValidator = X12Validator.ParseTransaction(raw, Spec);

        Assert.NotNull(viaParser.Transaction);
        Assert.NotNull(viaValidator.Transaction);
        Assert.Equal(viaParser.Transaction!.TransactionSetId, viaValidator.Transaction!.TransactionSetId);
        Assert.Equal(viaParser.Transaction.ControlNumber, viaValidator.Transaction.ControlNumber);
        Assert.Equal(viaParser.Transaction.Segments.Children.Select(n => n.Id), viaValidator.Transaction.Segments.Children.Select(n => n.Id));
        Assert.Equal(viaParser.Validation.TransactionSetId, viaValidator.Validation.TransactionSetId);
        Assert.Equal(viaParser.Validation.ControlNumber, viaValidator.Validation.ControlNumber);
        Assert.Equal(viaParser.Validation.Passed, viaValidator.Validation.Passed);
        Assert.Equal(viaParser.Validation.Diagnostics.Count, viaValidator.Validation.Diagnostics.Count);
    }

    [Fact]
    public void ValidateTransaction_validates_existing_transaction_tree()
    {
        var parsed = X12Parser.ParseTransaction(new X12Types.X12_ST("ST*999*1002~HDR*ABC~DTL*Z~SE*4*1002~"), Spec);

        var validation = X12Validator.ValidateTransaction(parsed.Transaction!, Spec);

        Assert.False(validation.Passed);
        var diag = Assert.IsType<InvalidElementCodeDiagnostic>(Assert.Single(validation.Diagnostics));
        Assert.Equal("DTL", diag.SegmentId);
        Assert.Equal(1, diag.ElementIndex);
    }

    [Fact]
    public void ValidationResult_aggregates_transaction_results()
    {
        var passed = new TransactionValidationResult("999", "1", true, Array.Empty<object>());
        var failed = new TransactionValidationResult("999", "2", false, [new MissingSegmentDiagnostic("SE", new DiagnosticLocation())]);

        var result = new ValidationResult([passed, failed]);

        Assert.False(result.Passed);
        Assert.Single(result.Diagnostics);
    }
}
