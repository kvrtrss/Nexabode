using Nexabode.Services;
using Xunit;

public class RentRestoreRulesTests
{
    [Fact]
    public void AVoidedCharge_WithNoPaymentsOrAdjustments_AndDaysInTheMonth_MayBeRestored() =>
        Assert.Null(RentRestoreRules.BlockedReason(isVoided: true, hasPayments: false, hasAdjustments: false,
                                                   stayHasDaysInMonth: true));

    [Fact]
    public void ACharge_ThatIsNotVoided_HasNothingToRestore()
    {
        var reason = RentRestoreRules.BlockedReason(false, false, false, true);
        Assert.NotNull(reason);
        Assert.Contains("not voided", reason);
    }

    [Fact]
    public void AChargeWithPayments_CannotBeRestored_AndPointsToWhereToLook()
    {
        var reason = RentRestoreRules.BlockedReason(true, hasPayments: true, false, true);
        Assert.NotNull(reason);
        Assert.Contains("Payments", reason);
    }

    [Fact]
    public void AChargeWithAdjustments_CannotBeRestoredAutomatically()
    {
        var reason = RentRestoreRules.BlockedReason(true, false, hasAdjustments: true, true);
        Assert.NotNull(reason);
        Assert.Contains("Billing Charges", reason);
    }

    [Fact]
    public void AStayWithNoDaysInTheMonth_HasNoRentToRestore()
    {
        var reason = RentRestoreRules.BlockedReason(true, false, false, stayHasDaysInMonth: false);
        Assert.NotNull(reason);
        Assert.Contains("no days", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ARestoreNeedsAReason(string? reason) =>
        Assert.NotNull(RentRestoreRules.ReasonError(reason));

    [Fact]
    public void AGivenReason_IsAccepted() =>
        Assert.Null(RentRestoreRules.ReasonError("Voided by mistake"));

    [Fact]
    public void TheConfirmation_ShowsWhatWillChange()
    {
        var text = RentRestoreRules.ConfirmNote("Kikay", "November 2026", 50m, "Rent (1 of 30 days)",
                                                1100m, "Rent (22 of 30 days)", new DateOnly(2026, 11, 9));
        Assert.Contains("Kikay", text);
        Assert.Contains("November 2026", text);
        Assert.Contains("Rent (1 of 30 days), \u20B150.00", text);
        Assert.Contains("Rent (22 of 30 days): \u20B11,100.00, due Nov 9, 2026", text);
        Assert.Contains("change history", text);
    }

    [Fact]
    public void TheConfirmation_CopesWithAMissingOldDescription()
    {
        var text = RentRestoreRules.ConfirmNote("Kikay", "November 2026", 50m, null,
                                                1100m, "Rent (22 of 30 days)", new DateOnly(2026, 11, 9));
        Assert.Contains("Monthly rent, \u20B150.00", text);
    }

    [Fact]
    public void TheVoidedMonthNote_NamesTheMonth_AndSaysWhereToRestoreIt()
    {
        var note = RentRestoreRules.VoidedMonthNote("November 2026");
        Assert.Contains("November 2026", note);
        Assert.Contains("Restore charge", note);
        Assert.Contains("Show voided", note);
    }
}
