using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class ValidatorRuleSemanticsTests
{
    private static ParsedTransactionValidationResult Parse(string[] specLines, string raw) =>
        X12Parser.ParseTransaction(new X12Types.X12_ST(raw), SpecPack.Unpack(specLines));

    [Fact]
    public void Warning_rule_violation_emits_diagnostic_but_transaction_still_passes()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "RULE|WARNING|hdr-warning",
            "MUST|HDR01=\"OK\""
        ], "ST*999*2001~HDR*BAD~SE*3*2001~");

        Assert.True(res.Validation.Passed);
        var d = Assert.Single(res.Validation.Diagnostics.OfType<RuleViolationDiagnostic>().Where(x => x.SegmentId == "HDR" && x.RuleId == "hdr-warning" && x.Severity == "WARNING"));
        Assert.True(d.Location.SegmentIndex >= 0);
    }

    [Fact]
    public void When_false_suppresses_rule_violation()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "ELM|HDR02|O|AN|1|5|",
            "RULE|ERROR|conditional-rule",
            "WHEN|HDR02=\"TRIGGER\"",
            "MUST|HDR01=\"OK\""
        ], "ST*999*2002~HDR*BAD*SKIP~SE*3*2002~");

        Assert.True(res.Validation.Passed);
        Assert.DoesNotContain(res.Validation.Diagnostics, d => d is RuleViolationDiagnostic rvd && rvd.SegmentId == "HDR" && rvd.RuleId == "conditional-rule");
    }

    [Fact]
    public void Not_predicate_participates_in_must_evaluation()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "RULE|ERROR|not-rule",
            "MUST|NOT(HDR01=\"BAD\")"
        ], "ST*999*2003~HDR*BAD~SE*3*2003~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is RuleViolationDiagnostic rvd && rvd.SegmentId == "HDR" && rvd.RuleId == "not-rule" && rvd.Severity == "ERROR");
    }

    [Fact]
    public void Xor_predicate_requires_exactly_one_branch_to_match()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "ELM|HDR02|O|AN|1|5|",
            "RULE|ERROR|xor-rule",
            "MUST|XOR(EXISTS(HDR01),EXISTS(HDR02))"
        ], "ST*999*2004~HDR*A*B~SE*3*2004~");

        Assert.False(res.Validation.Passed);
        Assert.Contains(res.Validation.Diagnostics, d => d is RuleViolationDiagnostic rvd && rvd.SegmentId == "HDR" && rvd.RuleId == "xor-rule" && rvd.Severity == "ERROR");
    }

    [Fact]
    public void Nested_and_or_rule_succeeds_when_predicate_tree_evaluates_true()
    {
        var res = Parse(
        [
            "X|999|005010",
            "SEG|HDR|M|1",
            "ELM|HDR01|M|AN|1|5|",
            "ELM|HDR02|O|AN|1|5|",
            "RULE|ERROR|complex-rule",
            "MUST|AND(EXISTS(HDR01),OR(HDR01=\"A\",HDR02=\"B\"))"
        ], "ST*999*2005~HDR*A*Z~SE*3*2005~");

        Assert.True(res.Validation.Passed);
        Assert.DoesNotContain(res.Validation.Diagnostics, d => d is RuleViolationDiagnostic rvd && rvd.SegmentId == "HDR" && rvd.RuleId == "complex-rule");
    }
}
