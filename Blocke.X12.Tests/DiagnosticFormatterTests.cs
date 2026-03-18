using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class DiagnosticFormatterTests
{
    private static readonly LoopCtx[] Path = [new("<ROOT>", 0)];

    [Fact]
    public void Missing_segment_diagnostics_render_location_and_corrective_action()
    {
        var rendered = DiagnosticFormatter.Render(
            new MissingSegmentDiagnostic("N1", new DiagnosticLocation(4, null, 27, 3)));

        Assert.Contains("At segment #4", rendered, StringComparison.Ordinal);
        Assert.Contains("Missing required segment N1", rendered, StringComparison.Ordinal);
        Assert.Contains("Action: Insert N1", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_element_type_diagnostics_render_element_and_expected_type()
    {
        var rendered = DiagnosticFormatter.Render(
            new InvalidElementTypeDiagnostic("DTM", 2, X12Type.DT, "20241340", new DiagnosticLocation(2, 2, 10, 8)));

        Assert.Contains("DTM element #2", rendered, StringComparison.Ordinal);
        Assert.Contains("expected DT", rendered, StringComparison.Ordinal);
        Assert.Contains("raw offset 10", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule_violation_diagnostics_render_rule_id_and_severity()
    {
        var rendered = DiagnosticFormatter.Render(
            new RuleViolationDiagnostic("N1", "ship-to-required", "WARNING", new DiagnosticLocation(6, null, 42, 8)));

        Assert.Contains("Rule 'ship-to-required' violated", rendered, StringComparison.Ordinal);
        Assert.Contains("(WARNING)", rendered, StringComparison.Ordinal);
        Assert.Contains("At segment #6", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_locations_render_as_unknown_rather_than_synthetic_indexes()
    {
        var rendered = DiagnosticFormatter.Render(
            new EmptySegmentSpecDiagnostic("HDR", new DiagnosticLocation()));

        Assert.Contains("At unknown location", rendered, StringComparison.Ordinal);
        Assert.Contains("Spec definition for segment HDR has no elements", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void X12Version_pretty_preserves_implementation_suffix_when_present()
    {
        Assert.Equal("005010/X222A1", new X12Version("005010", "X222A1").Pretty());
        Assert.Equal("004010", new X12Version("004010", null).Pretty());
        _ = Path;
    }

    [Fact]
    public void Validation_diagnostics_expose_message_and_path()
    {
        ValidationDiagnostic diagnostic = new MissingSegmentDiagnostic(Path, "N1", 2, DiagnosticLocation.AtSegment(4, 27, 3));

        Assert.Same(Path, diagnostic.Path);
        Assert.Contains("Missing required segment N1", diagnostic.Message, StringComparison.Ordinal);
    }
}
