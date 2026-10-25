using Nexabode.Services;
using Xunit;

public class RentAdjustmentRulesTests
{
    [Fact]
    public void UnpaidCharge_IsCorrectedInPlace()
    {
        var d = RentAdjustmentRules.Decide(1500m, 0m, false, 1000m);
        Assert.Equal(RentAction.UpdateInPlace, d.Action);
        Assert.Equal(1000m, d.NewAmount);
        Assert.Equal(-500m, d.Difference);
    }

    [Fact]
    public void UnpaidCharge_WithNoDaysLeft_IsVoided() =>
        Assert.Equal(RentAction.Void, RentAdjustmentRules.Decide(1500m, 0m, false, null).Action);

    [Fact]
    public void SameAmount_ChangesNothing()
    {
        Assert.Equal(RentAction.None, RentAdjustmentRules.Decide(1500m, 0m, false, 1500m).Action);
        Assert.Equal(RentAction.None, RentAdjustmentRules.Decide(1500m, 1500m, false, 1500m).Action);
    }

    [Fact]
    public void PaidCharge_GetsAnAdjustment_WhenMoreIsOwed()
    {
        var d = RentAdjustmentRules.Decide(1000m, 1000m, false, 1500m);
        Assert.Equal(RentAction.AddAdjustment, d.Action);
        Assert.Equal(500m, d.Difference);
    }

    [Fact]
    public void AdjustmentIsAddedEvenWhenAnEarlierAdjustmentExists()
    {
        var d = RentAdjustmentRules.Decide(1800m, 1800m, true, 2000m);
        Assert.Equal(RentAction.AddAdjustment, d.Action);
        Assert.Equal(200m, d.Difference);
    }

    [Fact]
    public void PartlyPaidCharge_IsReducedInPlace_WhenPaymentsStillFit() =>
        Assert.Equal(RentAction.UpdateInPlace, RentAdjustmentRules.Decide(1500m, 500m, false, 1000m).Action);

    [Fact]
    public void Overpayment_IsFlaggedNotChanged()
    {
        Assert.Equal(RentAction.FlagOverpaid, RentAdjustmentRules.Decide(1500m, 1500m, false, 1000m).Action);
        Assert.Equal(RentAction.FlagOverpaid, RentAdjustmentRules.Decide(1500m, 500m, false, null).Action);
    }

    [Fact]
    public void ReductionWithAnEarlierAdjustment_NeedsAPersonToLook() =>
        Assert.Equal(RentAction.NeedsReview, RentAdjustmentRules.Decide(1800m, 1000m, true, 1000m).Action);
}