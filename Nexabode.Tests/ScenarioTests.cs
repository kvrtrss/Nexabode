using Nexabode.Services;
using Xunit;

// Each test follows one real-life correction from start to finish.
public class ScenarioTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);
    private static StayRange S(DateOnly moveIn, DateOnly? moveOut, int id = 1) => new(id, moveIn, moveOut);
    private static decimal Prorate(decimal rent, int days, int monthDays) => Math.Round(rent * days / monthDays, 2);

    [Fact]
    public void CorrectingAMoveOut_FreesTheBedForReuse_WithoutTouchingTheNextTenant()
    {
        var first = S(D(1, 1), D(3, 31), 1);
        var next = S(D(4, 1), null, 2);
        Assert.Null(StayRules.Overlap(first, next));   // back to back is fine

        // A mistaken correction that runs into the next tenant names exactly the clashing days.
        var tooLate = S(D(1, 1), D(4, 5), 1);
        Assert.Equal<DateSpan?>(new DateSpan(D(4, 1), D(4, 5)), StayRules.Overlap(tooLate, next));

        // The right correction frees March 16-31, and a newcomer fits there.
        var corrected = S(D(1, 1), D(3, 15), 1);
        var newcomer = S(D(3, 16), D(3, 31), 3);
        Assert.Null(StayRules.Overlap(newcomer, corrected));
        Assert.Null(StayRules.Overlap(newcomer, next));
        Assert.Equal(new[] { new DateSpan(D(3, 16), D(3, 31)) }, StayRules.Subtract(first, corrected).ToArray());

        // A newcomer who stays one day too long collides with the next tenant on April 1 only.
        Assert.Equal<DateSpan?>(new DateSpan(D(4, 1), D(4, 1)),
                                StayRules.Overlap(S(D(3, 16), D(4, 1), 3), next));
    }

    [Fact]
    public void MovingTheMoveInEarlier_BillsMoreRent_UnpaidIsUpdated_PaidGetsAnAdjustment()
    {
        var january = D(1, 1);
        var before = S(D(1, 10), null);
        var after = S(D(1, 1), null);

        Assert.Equal(22, StayRules.DaysIn(before, january));
        Assert.Equal(31, StayRules.DaysIn(after, january));

        var billed = Prorate(3100m, 22, 31);   // 2,200.00
        var correct = Prorate(3100m, 31, 31);  // 3,100.00

        var unpaid = RentAdjustmentRules.Decide(billed, 0m, false, correct);
        Assert.Equal(RentAction.UpdateInPlace, unpaid.Action);

        var paid = RentAdjustmentRules.Decide(billed, billed, false, correct);
        Assert.Equal(RentAction.AddAdjustment, paid.Action);
        Assert.Equal(900m, paid.Difference);
    }

    [Fact]
    public void MovingTheMoveInLater_ReducesRent_ButNeverTouchesAnOverpaidCharge()
    {
        var january = D(1, 1);
        Assert.Equal(12, StayRules.DaysIn(S(D(1, 20), null), january));
        var correct = Prorate(3100m, 12, 31);   // 1,200.00

        Assert.Equal(RentAction.FlagOverpaid, RentAdjustmentRules.Decide(2200m, 2200m, false, correct).Action);
        Assert.Equal(RentAction.UpdateInPlace, RentAdjustmentRules.Decide(2200m, 1000m, false, correct).Action);
        Assert.Equal(RentAction.UpdateInPlace, RentAdjustmentRules.Decide(2200m, 0m, false, correct).Action);
    }

    [Fact]
    public void ShorteningAStay_OnlyAffectsTheMonthsWhoseDaysChange()
    {
        var months = new[] { D(1, 1), D(2, 1), D(3, 1) };
        var ongoing = S(D(1, 1), null);
        var shortened = S(D(1, 1), D(2, 10));

        Assert.Equal(new[] { 31, 28, 31 }, months.Select(m => StayRules.DaysIn(ongoing, m)).ToArray());
        Assert.Equal(new[] { 31, 10, 0 }, months.Select(m => StayRules.DaysIn(shortened, m)).ToArray());

        // January is untouched, February is re-billed by days, March has no days left and is voided.
        Assert.Equal(RentAction.UpdateInPlace,
            RentAdjustmentRules.Decide(3000m, 0m, false, Prorate(3000m, 10, 28)).Action);
        Assert.Equal(RentAction.Void, RentAdjustmentRules.Decide(3100m, 0m, false, null).Action);
        Assert.Equal(RentAction.FlagOverpaid, RentAdjustmentRules.Decide(3100m, 3100m, false, null).Action);
    }

    [Fact]
    public void UtilityPeriodAcrossMonths_FollowsTheActualStayDates()
    {
        var period = UtilityRules.EffectivePeriod("2026-10", D(9, 15), D(10, 14))!.Value;

        var leftTheDayBefore = S(D(7, 1), D(9, 14), 1);
        var leftOnTheFirstDay = S(D(7, 1), D(9, 15), 2);
        var arrivedOnTheLastDay = S(D(10, 14), null, 3);
        var arrivedTheDayAfter = S(D(10, 15), null, 4);

        Assert.False(UtilityRules.Overlaps(leftTheDayBefore, period));
        Assert.True(UtilityRules.Overlaps(leftOnTheFirstDay, period));
        Assert.True(UtilityRules.Overlaps(arrivedOnTheLastDay, period));
        Assert.False(UtilityRules.Overlaps(arrivedTheDayAfter, period));

        // Correcting a move-out from Sep 14 to Sep 15 makes that tenant share the bill.
        var before = UtilityRules.Overlaps(leftTheDayBefore, period);
        var after = UtilityRules.Overlaps(S(D(7, 1), D(9, 15), 1), period);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void UtilityShares_WhenSomeoneJoinsOrLeaves_FollowTheSameSafeRulesAsRent()
    {
        // Three tenants paid 1,000 each of a 3,000 bill. A fourth joins, so shares become 750.
        Assert.Equal(RentAction.FlagOverpaid, RentAdjustmentRules.Decide(1000m, 1000m, false, 750m).Action);
        Assert.Equal(RentAction.UpdateInPlace, RentAdjustmentRules.Decide(1000m, 0m, false, 750m).Action);

        // One of three leaves, so the others owe 1,500 each.
        Assert.Equal(RentAction.Void, RentAdjustmentRules.Decide(1000m, 0m, false, null).Action);
        var raised = RentAdjustmentRules.Decide(1000m, 1000m, false, 1500m);
        Assert.Equal(RentAction.AddAdjustment, raised.Action);
        Assert.Equal(500m, raised.Difference);
    }

    [Fact]
    public void ATenantCannotHoldTwoBedsOnTheSameDay()
    {
        var bed1 = S(D(1, 1), D(1, 31), 1);
        Assert.Null(StayRules.Overlap(bed1, S(D(2, 1), null, 2)));   // the day after: fine
        Assert.Equal<DateSpan?>(new DateSpan(D(1, 31), D(1, 31)),
                                StayRules.Overlap(bed1, S(D(1, 31), null, 2)));   // same day: conflict
    }
}