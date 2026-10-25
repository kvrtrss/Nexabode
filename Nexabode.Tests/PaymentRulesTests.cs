using Nexabode.Services;
using Xunit;

public class PaymentRulesTests
{
    [Fact]
    public void ExactPayment_HasNoChange()
    {
        var s = PaymentRules.Settle(100m, new[] { (1, 100m) });
        Assert.Equal(100m, s.Applied);
        Assert.Equal(0m, s.Change);
        Assert.Single(s.Allocations);
    }

    [Fact]
    public void Overpayment_IsAcceptedAndTheExtraIsChange()
    {
        var s = PaymentRules.Settle(150m, new[] { (1, 100m) });
        Assert.Equal(100m, s.Applied);
        Assert.Equal(50m, s.Change);
        Assert.Equal(new Allocation(1, 100m), s.Allocations[0]);
    }

    [Fact]
    public void Overpayment_PaysEveryChargeOldestFirst_ThenGivesChange()
    {
        var s = PaymentRules.Settle(150m, new[] { (1, 60m), (2, 40m) });
        Assert.Equal(2, s.Allocations.Count);
        Assert.Equal(100m, s.Applied);
        Assert.Equal(50m, s.Change);
    }

    [Fact]
    public void Underpayment_IsSpreadOldestFirst_WithNoChange()
    {
        var s = PaymentRules.Settle(70m, new[] { (1, 60m), (2, 40m) });
        Assert.Equal(new Allocation(1, 60m), s.Allocations[0]);
        Assert.Equal(new Allocation(2, 10m), s.Allocations[1]);
        Assert.Equal(0m, s.Change);
    }

    [Fact]
    public void WithNothingToPay_EverythingIsChange()
    {
        var s = PaymentRules.Settle(50m, new (int, decimal)[0]);
        Assert.Empty(s.Allocations);
        Assert.Equal(0m, s.Applied);
        Assert.Equal(50m, s.Change);
    }

    [Fact]
    public void ChargesWithNothingLeft_AreSkipped()
    {
        var s = PaymentRules.Settle(80m, new[] { (1, 0m), (2, 50m), (3, 100m) });
        Assert.Equal(new Allocation(2, 50m), s.Allocations[0]);
        Assert.Equal(new Allocation(3, 30m), s.Allocations[1]);
        Assert.Equal(0m, s.Change);
    }
}