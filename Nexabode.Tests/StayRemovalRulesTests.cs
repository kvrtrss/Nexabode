using Nexabode.Services;
using Xunit;

public class StayRemovalRulesTests
{
    [Fact]
    public void AStayWithNoBillingAndNoRefund_MayBeRemoved() =>
        Assert.Null(StayRemovalRules.BlockedReason(hasBilling: false, depositRefunded: 0));

    [Fact]
    public void AStayWithBilling_CannotBeRemoved_AndTheMessageSaysWhatToDoInstead()
    {
        var reason = StayRemovalRules.BlockedReason(hasBilling: true, depositRefunded: 0);
        Assert.NotNull(reason);
        Assert.Contains("Billing Charges", reason);
        Assert.Contains("Edit this stay", reason);
    }

    [Fact]
    public void AStayWithARecordedDepositRefund_CannotBeRemoved()
    {
        var reason = StayRemovalRules.BlockedReason(hasBilling: false, depositRefunded: 500m);
        Assert.NotNull(reason);
        Assert.Contains("Undo refund", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ARemovalNeedsAReason(string? reason) =>
        Assert.NotNull(StayRemovalRules.ReasonError(reason));

    [Fact]
    public void AGivenReason_IsAccepted() =>
        Assert.Null(StayRemovalRules.ReasonError("Entered twice by mistake"));

    [Fact]
    public void TheWarning_SaysItCannotBeUndone_AndMentionsADepositOnlyWhenThereIsOne()
    {
        var without = StayRemovalRules.FormNote("Kikay", "Room 101 / Bed 1, Nov 9, 2026 to ongoing", 0);
        Assert.Contains("Kikay", without);
        Assert.Contains("cannot be undone", without);
        Assert.DoesNotContain("deposit", without, StringComparison.OrdinalIgnoreCase);

        var with = StayRemovalRules.FormNote("Kikay", "Room 101 / Bed 1", 1500m);
        Assert.Contains("\u20B11,500.00", with);
        Assert.Contains("settle the deposit separately", with);
    }

    [Fact]
    public void TheSameBedAndDates_AreADuplicate()
    {
        var day = new DateOnly(2026, 11, 9);
        Assert.True(StayRemovalRules.IsDuplicate("Room 101 / Bed 1", day, null, "Room 101 / Bed 1", day, null));
    }

    [Fact]
    public void ADifferentBedOrDate_IsNotADuplicate()
    {
        var day = new DateOnly(2026, 11, 9);
        Assert.False(StayRemovalRules.IsDuplicate("Room 101 / Bed 1", day, null, "Room 101 / Bed 2", day, null));
        Assert.False(StayRemovalRules.IsDuplicate("Room 101 / Bed 1", day, null, "Room 101 / Bed 1", day.AddDays(1), null));
        Assert.False(StayRemovalRules.IsDuplicate("Room 101 / Bed 1", day, null, "Room 101 / Bed 1", day, day.AddDays(30)));
    }

    [Theory]
    [InlineData(true, true, "probably a duplicate", "Remove this stay")]
    [InlineData(true, false, "probably a duplicate", "Ask an Owner")]
    [InlineData(false, true, "overlap", "Remove this stay if one was entered by mistake")]
    [InlineData(false, false, "overlap", "ask an Owner")]
    public void TheOverlapWarning_PointsEachPersonToWhatTheyCanDo(bool duplicate, bool owner, string a, string b)
    {
        var text = StayRemovalRules.OverlapWarning(duplicate, owner);
        Assert.Contains(a, text);
        Assert.Contains(b, text);
    }

    [Fact]
    public void ForAnOwner_AnExactDuplicateIsNotToldToJustEditTheDates() =>
        Assert.DoesNotContain("correct the dates", StayRemovalRules.OverlapWarning(hasExactDuplicate: true, isOwner: true));
}
