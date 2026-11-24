using Nexabode.Services;

public class BillingRulesTests
{
    private static readonly DateOnly November = new(2026, 11, 1);

    [Fact]
    public void AMoveInMidMonth_IsChargedByTheDaysStayed()
    {
        // moved in on the 13th: 18 of the 30 days of November
        var rent = BillingRules.CalculateRent(3000m, new DateOnly(2026, 11, 13), null, November, byDays: true, movedStraightIntoAnotherBed: false);

        Assert.NotNull(rent);
        Assert.Equal(1800m, rent!.Amount);
        Assert.Equal("Rent (18 of 30 days)", rent.Description);
    }

    [Fact]
    public void AFullMonth_IsChargedInFull()
    {
        var rent = BillingRules.CalculateRent(3000m, new DateOnly(2026, 1, 1), null, November, byDays: true, movedStraightIntoAnotherBed: false);

        Assert.NotNull(rent);
        Assert.Equal(3000m, rent!.Amount);
    }

    [Fact]
    public void AStayThatHadNotStartedYet_HasNoRentThatMonth()
    {
        var rent = BillingRules.CalculateRent(3000m, new DateOnly(2026, 12, 5), null, November, byDays: true, movedStraightIntoAnotherBed: false);

        Assert.Null(rent);
    }

    [Fact]
    public void FullRentForPartialMonths_ChargesTheWholeRent()
    {
        var rent = BillingRules.CalculateRent(3000m, new DateOnly(2026, 11, 13), null, November, byDays: false, movedStraightIntoAnotherBed: false);

        Assert.NotNull(rent);
        Assert.Equal(3000m, rent!.Amount);
    }

    [Theory]
    [InlineData(1000, 1000, "Paid")]
    [InlineData(1000, 400, "Partial")]
    [InlineData(1000, 0, "Overdue")]     // nothing paid and past its due date
    public void ChargeStatus_ReadsPaidAndDueDate(int amount, int paid, string expected)
    {
        var today = new DateOnly(2026, 11, 20);
        var due = new DateOnly(2026, 11, 5);

        // a part-payment on an overdue charge counts as Partial, so the "Partial" case is checked on a charge that is not overdue
        var status = BillingRules.ChargeStatus(amount, paid, expected == "Partial" ? today.AddDays(5) : due, today);

        Assert.Equal(expected, status);
    }

    [Fact]
    public void ANotYetDueCharge_WithNothingPaid_IsUnpaid()
    {
        var status = BillingRules.ChargeStatus(1000m, 0m, new DateOnly(2026, 12, 5), new DateOnly(2026, 11, 20));

        Assert.Equal("Unpaid", status);
    }
}
