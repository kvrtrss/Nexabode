using Nexabode.Services;
using Xunit;

public class StayRulesTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);
    private static StayRange S(DateOnly moveIn, DateOnly? moveOut, int id = 1) => new(id, moveIn, moveOut);

    [Fact]
    public void MoveOutDayIsStillOccupied_SoSameDayMoveInConflicts()
    {
        var overlap = StayRules.Overlap(S(D(1, 1), D(1, 10)), S(D(1, 10), null, 2));
        Assert.NotNull(overlap);
        Assert.Equal(D(1, 10), overlap!.Value.Start);
        Assert.Equal<DateOnly?>(D(1, 10), overlap.Value.End);
    }

    [Fact]
    public void MoveInTheDayAfterMoveOut_IsAllowed() =>
        Assert.Null(StayRules.Overlap(S(D(1, 1), D(1, 10)), S(D(1, 11), null, 2)));

    [Fact]
    public void EndedStay_DoesNotBlockALaterMoveIn() =>
        Assert.Null(StayRules.Overlap(S(D(1, 1), D(1, 31)), S(D(2, 1), null, 2)));

    [Fact]
    public void OngoingStay_BlocksAnyLaterStay()
    {
        var overlap = StayRules.Overlap(S(D(1, 1), null), S(D(12, 1), D(12, 5), 2));
        Assert.NotNull(overlap);
        Assert.Equal(D(12, 1), overlap!.Value.Start);
        Assert.Equal<DateOnly?>(D(12, 5), overlap.Value.End);
    }

    [Fact]
    public void TwoOngoingStays_OverlapWithNoEnd()
    {
        var overlap = StayRules.Overlap(S(D(1, 1), null), S(D(3, 1), null, 2));
        Assert.NotNull(overlap);
        Assert.Equal(D(3, 1), overlap!.Value.Start);
        Assert.Null(overlap.Value.End);
    }

    [Fact]
    public void HistoricalStay_FitsInAGapBetweenOtherStays()
    {
        var before = S(D(1, 1), D(1, 31));
        var after = S(D(3, 1), null, 2);
        var gap = S(D(2, 1), D(2, 28), 3);

        Assert.Null(StayRules.Overlap(gap, before));
        Assert.Null(StayRules.Overlap(gap, after));

        var spill = S(D(2, 1), D(3, 1), 4);   // runs into the later stay's first day
        Assert.True(StayRules.Overlaps(spill, after));
    }

    [Fact]
    public void Overlap_IsSymmetric()
    {
        var a = S(D(1, 1), D(1, 20));
        var b = S(D(1, 15), D(2, 10), 2);
        Assert.Equal(StayRules.Overlap(a, b), StayRules.Overlap(b, a));
    }

    [Fact]
    public void SingleDayStay_WorksAtItsBoundaries()
    {
        var oneDay = S(D(3, 5), D(3, 5));
        Assert.True(StayRules.IsValidRange(D(3, 5), D(3, 5)));
        Assert.True(StayRules.Overlaps(oneDay, S(D(3, 5), D(3, 5), 2)));
        Assert.False(StayRules.Overlaps(oneDay, S(D(3, 6), null, 2)));
        Assert.False(StayRules.Overlaps(oneDay, S(D(3, 1), D(3, 4), 2)));
    }

    [Fact]
    public void MoveOutBeforeMoveIn_IsInvalid()
    {
        Assert.False(StayRules.IsValidRange(D(3, 5), D(3, 4)));
        Assert.True(StayRules.IsValidRange(D(3, 5), null));
    }

    [Fact]
    public void IsActiveOn_CoversBothEndDays()
    {
        var stay = S(D(1, 10), D(1, 20));
        Assert.False(StayRules.IsActiveOn(stay, D(1, 9)));
        Assert.True(StayRules.IsActiveOn(stay, D(1, 10)));
        Assert.True(StayRules.IsActiveOn(stay, D(1, 20)));
        Assert.False(StayRules.IsActiveOn(stay, D(1, 21)));
        Assert.True(StayRules.IsActiveOn(S(D(1, 10), null), D(12, 31)));
    }

    [Fact]
    public void PhaseOn_ReservedStayingEnded()
    {
        var stay = S(D(1, 10), D(1, 20));
        Assert.Equal(StayPhase.Reserved, StayRules.PhaseOn(stay, D(1, 9)));
        Assert.Equal(StayPhase.Staying, StayRules.PhaseOn(stay, D(1, 20)));   // move-out day is still "staying"
        Assert.Equal(StayPhase.Ended, StayRules.PhaseOn(stay, D(1, 21)));
    }

    [Fact]
    public void FreeFrom_ReportsTheFirstConflictFreeDay()
    {
        var today = D(1, 5);
        Assert.Equal<DateOnly?>(today, StayRules.FreeFrom(new List<StayRange>(), today));
        Assert.Equal<DateOnly?>(today, StayRules.FreeFrom(new[] { S(D(1, 1), D(1, 4)) }, today));
        Assert.Equal<DateOnly?>(D(1, 11), StayRules.FreeFrom(new[] { S(D(1, 1), D(1, 10)) }, today));
        Assert.Null(StayRules.FreeFrom(new[] { S(D(1, 1), null) }, today));
    }
	
	    [Fact]
    public void DaysIn_CountsPartialAndFullMonths()
    {
        Assert.Equal(11, StayRules.DaysIn(S(D(1, 10), D(1, 20)), D(1, 1)));
        Assert.Equal(7, StayRules.DaysIn(S(D(1, 25), D(2, 5)), D(1, 1)));   // Jan 25-31
        Assert.Equal(5, StayRules.DaysIn(S(D(1, 25), D(2, 5)), D(2, 1)));   // Feb 1-5
    }

    [Fact]
    public void DaysIn_OngoingStayCoversEveryLaterMonth()
    {
        var ongoing = S(new DateOnly(2025, 12, 1), null);
        Assert.Equal(31, StayRules.DaysIn(ongoing, new DateOnly(2026, 1, 1)));
        Assert.Equal(28, StayRules.DaysIn(ongoing, new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void DaysIn_IsZeroOutsideTheStay()
    {
        var stay = S(D(3, 10), D(3, 20));
        Assert.Equal(0, StayRules.DaysIn(stay, D(2, 1)));
        Assert.Equal(0, StayRules.DaysIn(stay, D(4, 1)));
    }

    [Fact]
    public void Subtract_ReturnsTheDaysBeforeAndAfter()
    {
        var parts = StayRules.Subtract(S(D(1, 1), D(1, 31)), S(D(1, 10), D(1, 20), 2));
        Assert.Equal(2, parts.Count);
        Assert.Equal(new DateSpan(D(1, 1), D(1, 9)), parts[0]);
        Assert.Equal(new DateSpan(D(1, 21), D(1, 31)), parts[1]);
    }

    [Fact]
    public void Subtract_KeepsAnOngoingTail()
    {
        var parts = StayRules.Subtract(S(D(1, 1), null), S(D(1, 10), D(1, 20), 2));
        Assert.Equal(2, parts.Count);
        Assert.Equal(new DateSpan(D(1, 21), null), parts[1]);
    }

    [Fact]
    public void Subtract_NothingLeftWhenFullyCovered()
    {
        Assert.Empty(StayRules.Subtract(S(D(1, 10), D(1, 20)), S(D(1, 1), D(1, 31), 2)));
        Assert.Empty(StayRules.Subtract(S(D(1, 10), null), S(D(1, 1), null, 2)));
    }

    [Fact]
    public void Subtract_WholeStayWhenThereIsNoOverlap()
    {
        var parts = StayRules.Subtract(S(D(1, 1), D(1, 10)), S(D(2, 1), D(2, 10), 2));
        Assert.Single(parts);
        Assert.Equal(new DateSpan(D(1, 1), D(1, 10)), parts[0]);
    }
	
	    [Fact]
    public void IsWithin_ComparesDateSpans()
    {
        var jan15On = new DateSpan(D(1, 15), null);
        var jan15to31 = new DateSpan(D(1, 15), D(1, 31));

        Assert.True(StayRules.IsWithin(jan15to31, jan15On));
        Assert.False(StayRules.IsWithin(jan15On, jan15to31));
        Assert.True(StayRules.IsWithin(jan15On, jan15On));
        Assert.False(StayRules.IsWithin(new DateSpan(D(1, 10), D(1, 20)), jan15to31));
    }

    [Fact]
    public void EndingEitherStayOfAnExistingOverlap_IsNeverWorse()
    {
        var a = S(D(1, 1), null, 1);
        var b = S(D(1, 15), null, 2);
        var before = StayRules.Overlap(a, b);   // January 15 onward

        var aEndsFeb1 = StayRules.Overlap(S(D(1, 1), D(2, 1), 1), b)!.Value;
        var bEndsFeb1 = StayRules.Overlap(a, S(D(1, 15), D(2, 1), 2))!.Value;

        Assert.False(StayRules.IsNewOrWorse(aEndsFeb1, before));
        Assert.False(StayRules.IsNewOrWorse(bEndsFeb1, before));

        // Ending A before B arrives removes the overlap entirely.
        Assert.Null(StayRules.Overlap(S(D(1, 1), D(1, 14), 1), b));
    }

    [Fact]
    public void ExtendingIntoNewDays_IsWorse()
    {
        var b = S(D(1, 15), null, 2);
        var before = StayRules.Overlap(S(D(1, 1), D(1, 20), 1), b);   // January 15 to 20

        Assert.True(StayRules.IsNewOrWorse(StayRules.Overlap(S(D(1, 1), D(1, 25), 1), b)!.Value, before));
        Assert.True(StayRules.IsNewOrWorse(StayRules.Overlap(S(D(1, 1), null, 1), b)!.Value, before));
    }

    [Fact]
    public void AnEditThatKeepsTheSameOverlap_IsNotWorse()
    {
        var overlap = StayRules.Overlap(S(D(1, 1), D(1, 20), 1), S(D(1, 15), null, 2))!.Value;
        Assert.False(StayRules.IsNewOrWorse(overlap, overlap));
    }

    [Fact]
    public void ABrandNewConflict_IsAlwaysWorse() =>
        Assert.True(StayRules.IsNewOrWorse(new DateSpan(D(2, 1), D(2, 5)), null));
	
}