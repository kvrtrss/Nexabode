using Nexabode.Services;
using Xunit;

public class ImpactWordingTests
{
    [Theory]
    [InlineData(400, false, false, "The tenant will owe \u20B1400.00 more in rent.")]
    [InlineData(-1050, false, false, "The tenant will owe \u20B11,050.00 less in rent.")]
    [InlineData(0, false, false, "The rent owed does not change.")]
    public void TheSummary_SaysMoreLessOrNoChange(double net, bool decision, bool utility, string expected) =>
        Assert.Equal(expected, ImpactWording.NetRentSentence((decimal)net, decision, utility));

    [Fact]
    public void TheSummary_PutsADecisionFirst_AndMentionsUtilities()
    {
        var text = ImpactWording.NetRentSentence(400m, needsDecision: true, hasUtilityChanges: true);
        Assert.StartsWith("Some items below need your decision", text);
        Assert.Contains("Utility shares change too", text);
        Assert.DoesNotContain("more in rent", text);
    }

    [Fact]
    public void MoreRentDue_ExplainsTheNewTotal_AndTheAdjustment()
    {
        var text = ImpactWording.MoreRentDue("November 2026", 1500m, "Rent (full month)", 400m, 1100m, 1100m,
                                             "billable days changed from 22 to 1 of 30", fullMonthPolicyApplies: false);
        Assert.Contains("the rent for this month becomes \u20B11,500.00 (Rent (full month))", text);
        Assert.Contains("\u20B1400.00 more than the \u20B11,100.00 already billed", text);
        Assert.Contains("(\u20B11,100.00 paid)", text);
        Assert.Contains("adjustment charge of \u20B1400.00", text);
        Assert.DoesNotContain("full month's rent when a stay", text);
    }

    [Fact]
    public void MoreRentDue_ForAShorterStay_SaysWhyItCostsMore()
    {
        var text = ImpactWording.MoreRentDue("November 2026", 1500m, null, 400m, 1100m, 0m,
                                             "billable days changed from 22 to 1 of 30", fullMonthPolicyApplies: true);
        Assert.Contains("A shorter stay normally costs less", text);
        Assert.DoesNotContain("paid)", text);
    }

    [Fact]
    public void TheLineMarkers_AreDistinct()
    {
        var markers = new[] { ImpactWording.Heading, ImpactWording.SubHeading, ImpactWording.Summary, ImpactWording.Note };
        Assert.Equal(markers.Length, markers.Distinct().Count());
        // "### " also starts with "##", so the window must check the longer marker first or after an exact "## " test.
        Assert.False(ImpactWording.SubHeading.StartsWith(ImpactWording.Heading));
    }
}
