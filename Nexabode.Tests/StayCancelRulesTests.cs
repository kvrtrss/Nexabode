using Nexabode.Services;
using Xunit;

public class StayCancelRulesTests
{
    [Fact]
    public void AStayWithOnlyVoidedCharges_AndNoDeposit_MayBeCancelled() =>
        Assert.Null(StayCancelRules.BlockedReason(hasActiveCharges: false, deposit: 0));

    [Fact]
    public void AStayWithActiveCharges_CannotBeCancelled_AndSaysWhatToDoFirst()
    {
        var reason = StayCancelRules.BlockedReason(hasActiveCharges: true, deposit: 0);
        Assert.NotNull(reason);
        Assert.Contains("Billing Charges", reason);
        Assert.Contains("voided", reason);
    }

    [Fact]
    public void AStayWithADeposit_CannotBeCancelled_SoNoMoneyRecordIsHidden()
    {
        var reason = StayCancelRules.BlockedReason(false, deposit: 1500m);
        Assert.NotNull(reason);
        Assert.Contains("\u20B11,500.00", reason);
        Assert.Contains("Edit this stay", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ACancellationNeedsAReason(string? reason) =>
        Assert.NotNull(StayCancelRules.ReasonError(reason));

    [Fact]
    public void TheWarning_ExplainsWhyItIsCancelledAndNotDeleted()
    {
        var text = StayCancelRules.FormNote("Joseph", "Room 101 / Bed 2, Nov 9, 2026 to Nov 9, 2026");
        Assert.Contains("Joseph", text);
        Assert.Contains("cannot be deleted", text);
        Assert.Contains("cancelled instead", text);
        Assert.Contains("cannot be undone", text);
    }

    [Fact]
    public void TheHistoryLine_ShowsTheDateAndTheReason()
    {
        Assert.Equal("Cancelled on Nov 9, 2026: Entered twice",
                     StayCancelRules.HistoryLine(new DateOnly(2026, 11, 9), " Entered twice "));
        Assert.Equal("Cancelled", StayCancelRules.HistoryLine(null, null));
        Assert.Equal("Cancelled on Nov 9, 2026", StayCancelRules.HistoryLine(new DateOnly(2026, 11, 9), ""));
    }
}
