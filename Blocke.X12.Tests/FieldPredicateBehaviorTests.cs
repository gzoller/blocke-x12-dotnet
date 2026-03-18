using Blocke.X12.Models;

namespace Blocke.X12.Tests;

public sealed class FieldPredicateBehaviorTests
{
    private static readonly SegmentNode Segment =
        new(
            "REF",
            [
                new Field("REF01", "DP"),
                new Field("REF02", "57"),
                new Field("REF03", string.Empty)
            ],
            new SegmentMeta(0, "REF*DP*57*"));

    [Fact]
    public void Simple_field_predicates_match_exact_values_existence_and_value_sets()
    {
        Assert.True(new FieldEquals(new SimpleFieldRef("REF", "REF01"), "DP").Matches(Segment));
        Assert.False(new FieldEquals(new SimpleFieldRef("REF", "REF01"), "VN").Matches(Segment));
        Assert.True(new FieldExists(new SimpleFieldRef("REF", "REF02")).Matches(Segment));
        Assert.False(new FieldExists(new SimpleFieldRef("REF", "REF03")).Matches(Segment));
        Assert.True(new FieldInSet(new SimpleFieldRef("REF", "REF01"), new HashSet<string>(["DP", "VN"])).Matches(Segment));
    }

    [Fact]
    public void Composite_field_predicates_currently_match_on_the_element_id_only()
    {
        Assert.True(new FieldEquals(new CompositeFieldRef("REF", "REF01", "1"), "DP").Matches(Segment));
        Assert.True(new FieldExists(new CompositeFieldRef("REF", "REF02", "9")).Matches(Segment));
        Assert.True(new FieldInSet(new CompositeFieldRef("REF", "REF01", "7"), new HashSet<string>(["DP"])).Matches(Segment));
    }

    [Fact]
    public void Logical_predicate_combinators_apply_boolean_semantics()
    {
        Assert.True(new And([new FieldExists(new SimpleFieldRef("REF", "REF01")), new FieldEquals(new SimpleFieldRef("REF", "REF01"), "DP")]).Matches(Segment));
        Assert.True(new Or([new FieldEquals(new SimpleFieldRef("REF", "REF01"), "VN"), new FieldEquals(new SimpleFieldRef("REF", "REF01"), "DP")]).Matches(Segment));
        Assert.False(new Xor([new FieldEquals(new SimpleFieldRef("REF", "REF01"), "DP"), new FieldEquals(new SimpleFieldRef("REF", "REF02"), "57")]).Matches(Segment));
        Assert.True(new Not(new FieldEquals(new SimpleFieldRef("REF", "REF01"), "VN")).Matches(Segment));
    }
}
