using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class SpecPackTests
{
    [Fact]
    public void Unpack_parses_nested_predicate_trees_in_must()
    {
        var spec = SpecPack.Unpack(
        [
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "RULE|ERROR|beg-rule",
            "MUST|AND(EXISTS(BEG01),OR(BEG01=\"00\",BEG01=\"NE\"))",
            "CODE|BEG01|00,NE"
        ]);

        var seg = Assert.IsType<SegmentNodeSpec>(Assert.Single(spec.Root));
        var rule = Assert.Single(seg.Rules);
        var must = Assert.IsType<AndPredicate>(rule.Must);
        Assert.Equal(2, must.Predicates.Count);
        Assert.IsType<ExistsPredicate>(must.Predicates[0]);
        var or = Assert.IsType<OrPredicate>(must.Predicates[1]);
        Assert.Equal(2, or.Predicates.Count);
        Assert.All(or.Predicates, p => Assert.IsType<EqualsPredicate>(p));
    }

    [Fact]
    public void Unpack_parses_in_predicate_values_correctly()
    {
        var spec = SpecPack.Unpack(
        [
            "X|850|005010",
            "SEG|N1|O|10",
            "ELM|N101|M|ID|2|3|N101",
            "RULE|WARNING|n1-in",
            "MUST|IN(N101,BT,ST,VN,SN)",
            "CODE|N101|BT,ST,VN,SN"
        ]);

        var seg = Assert.IsType<SegmentNodeSpec>(Assert.Single(spec.Root));
        var inSet = Assert.IsType<InSetPredicate>(Assert.Single(seg.Rules).Must);
        Assert.Equal(new HashSet<string>(["BT", "ST", "VN", "SN"]), inSet.Values);
    }

    [Fact]
    public void Unpack_handles_nested_rule_predicates_in_when_and_must()
    {
        var spec = SpecPack.Unpack(
        [
            "X|850|005010",
            "SEG|N1|O|10",
            "ELM|N101|M|ID|2|3|N101",
            "ELM|N103|O|ID|1|2|N103",
            "RULE|ERROR|n1-conditional",
            "WHEN|OR(IN(N101,BT,ST,VN,SN),EXISTS(N103))",
            "MUST|AND(EXISTS(N101),OR(N101=\"BT\",N101=\"ST\"))",
            "CODE|N101|BT,ST,VN,SN",
            "CODE|N103|1,9,91,92"
        ]);

        var seg = Assert.IsType<SegmentNodeSpec>(Assert.Single(spec.Root));
        var rule = Assert.Single(seg.Rules);
        var whenOr = Assert.IsType<OrPredicate>(Assert.IsType<OrPredicate>(rule.When));
        Assert.Equal(2, whenOr.Predicates.Count);
        Assert.IsType<InSetPredicate>(whenOr.Predicates[0]);
        Assert.IsType<ExistsPredicate>(whenOr.Predicates[1]);

        var mustAnd = Assert.IsType<AndPredicate>(rule.Must);
        Assert.Equal(2, mustAnd.Predicates.Count);
        Assert.IsType<ExistsPredicate>(mustAnd.Predicates[0]);
        Assert.IsType<OrPredicate>(mustAnd.Predicates[1]);
    }

    [Fact]
    public void Rule_severity_parse_is_case_insensitive_enough_for_storage()
    {
        var spec = SpecPack.Unpack(
        [
            "X|850|005010",
            "SEG|BEG|M|1",
            "ELM|BEG01|M|ID|2|2|BEG01",
            "RULE|warning|lowercase-severity",
            "MUST|EXISTS(BEG01)",
            "CODE|BEG01|00"
        ]);

        var seg = Assert.IsType<SegmentNodeSpec>(Assert.Single(spec.Root));
        Assert.Equal("warning", Assert.Single(seg.Rules).Severity);
    }
}
