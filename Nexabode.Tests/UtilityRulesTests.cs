using Nexabode.Services;
using Xunit;

public class UtilityRulesTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);
    private static StayRange S(DateOnly moveIn, DateOnly? moveOut) => new(1, moveIn, moveOut);

    [Fact]
    public void NoPeriod_MeansTheWholeMonth()
    {
        var feb = UtilityRules.EffectivePeriod("2026-02", null, null);
        Assert.Equal(new BillPeriod(D(2, 1), D(2, 28)), feb!.Value);

        var leap = UtilityRules.EffectivePeriod("2028-02", null, null);
        Assert.Equal(new BillPeriod(new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)), leap!.Value);
    }

    [Fact]
    public void ExplicitPeriod_CanSpanMonths()
    {
        var period = UtilityRules.EffectivePeriod("2026-10", D(9, 15), D(10, 14));
        Assert.Equal(new BillPeriod(D(9, 15), D(10, 14)), period!.Value);
    }

    [Fact]
    public void BackwardsPeriodOrBadMonth_IsRejected()
    {
        Assert.Null(UtilityRules.EffectivePeriod("2026-10", D(10, 14), D(9, 15)));
        Assert.Null(UtilityRules.EffectivePeriod("not-a-month", null, null));
    }

    [Fact]
    public void Stay_ThatEndsOnTheFirstDayOfThePeriod_StillShares()
    {
        var period = new BillPeriod(D(9, 15), D(10, 14));
        Assert.True(UtilityRules.Overlaps(S(D(8, 1), D(9, 15)), period));    // last day is the period's first day
        Assert.False(UtilityRules.Overlaps(S(D(8, 1), D(9, 14)), period));   // left the day before
    }

    [Fact]
    public void Stay_ThatStartsOnTheLastDayOfThePeriod_Shares()
    {
        var period = new BillPeriod(D(9, 15), D(10, 14));
        Assert.True(UtilityRules.Overlaps(S(D(10, 14), null), period));
        Assert.False(UtilityRules.Overlaps(S(D(10, 15), null), period));
    }

    [Fact]
    public void ShortStay_InsideALongPeriod_Shares()
    {
        var period = new BillPeriod(D(9, 15), D(10, 14));
        Assert.True(UtilityRules.Overlaps(S(D(10, 1), D(10, 3)), period));
    }

    [Fact]
    public void Periods_ThatTouchOnOneDay_Overlap()
    {
        var first = new BillPeriod(D(9, 15), D(10, 14));
        Assert.True(UtilityRules.PeriodsOverlap(first, new BillPeriod(D(10, 14), D(11, 13))));
        Assert.False(UtilityRules.PeriodsOverlap(first, new BillPeriod(D(10, 15), D(11, 14))));
    }
}