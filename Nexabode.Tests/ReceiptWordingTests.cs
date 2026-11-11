using Nexabode.Services;
using Xunit;

public class ReceiptWordingTests
{
    [Theory]
    [InlineData("Rent", "2026-10", false, "Rent, October 2026")]
    [InlineData("Electricity", "2026-01", false, "Electricity, January 2026")]
    [InlineData("Rent", "2026-10", true, "Rent, October 2026 (adjustment)")]
    [InlineData("Other", "", false, "Other")]
    [InlineData("Water", "Oct", false, "Water, Oct")]   // not a stored month: shown as it is
    public void TheChargeLabel_NamesTheItemAndTheMonth(string item, string month, bool adjustment, string expected) =>
        Assert.Equal(expected, ReceiptWording.ChargeLabel(item, month, adjustment));

    [Theory]
    [InlineData(1000, 800, 200)]
    [InlineData(800, 800, 0)]
    [InlineData(500, 800, 0)]   // never a negative change
    public void TheChange_IsWhatWasHandedOverBeyondWhatWasApplied(double received, double applied, double expected) =>
        Assert.Equal((decimal)expected, ReceiptWording.Change((decimal)received, (decimal)applied));

    [Fact]
    public void AVoidedPayment_CannotBePrinted()
    {
        Assert.False(ReceiptWording.CanPrint(isVoided: true));
        Assert.True(ReceiptWording.CanPrint(isVoided: false));
    }

    [Theory]
    [InlineData(null, "Boarding House")]
    [InlineData("", "Boarding House")]
    [InlineData("   ", "Boarding House")]
    [InlineData("  Casa Verde ", "Casa Verde")]
    public void TheHouseName_FallsBackToADefault(string? name, string expected) =>
        Assert.Equal(expected, ReceiptWording.HouseName(name));

    [Fact]
    public void TheBalanceLine_SaysWhenNothingIsLeft()
    {
        var day = new DateOnly(2026, 11, 8);
        Assert.Equal("No balance remaining as of Nov 8, 2026", ReceiptWording.BalanceText(0, day));
        Assert.Equal("Balance remaining as of Nov 8, 2026: \u20B11,500.00", ReceiptWording.BalanceText(1500m, day));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void TheFirstPrinting_IsNotLabelledADuplicate(int printNumber) =>
        Assert.Null(ReceiptWording.DuplicateLabel(printNumber));

    [Fact]
    public void ALaterPrinting_IsLabelledADuplicate() =>
        Assert.Equal("DUPLICATE COPY (print no. 2)", ReceiptWording.DuplicateLabel(2));

    [Theory]
    [InlineData(null, "Not stated")]
    [InlineData("  ", "Not stated")]
    [InlineData(" GCash ", "GCash")]
    public void TheMethod_IsShownOrMarkedNotStated(string? method, string expected) =>
        Assert.Equal(expected, ReceiptWording.MethodText(method));

    [Fact]
    public void TheRecordedMessage_MentionsChangeOnlyWhenThereIsSome()
    {
        var exact = ReceiptWording.PaymentRecordedMessage("RCT-2026-00001", "Kikay", 750m, 750m);
        Assert.Equal("Payment recorded (receipt no. RCT-2026-00001).", exact);

        var withChange = ReceiptWording.PaymentRecordedMessage("RCT-2026-00002", "Kikay", 2500m, 750m);
        Assert.Contains("receipt no. RCT-2026-00002", withChange);
        Assert.Contains("Change to return to Kikay: \u20B11,750.00", withChange);
        Assert.Contains("received \u20B12,500.00, applied \u20B1750.00", withChange);
    }

    [Fact]
    public void ThePlace_NamesTheRoomAndTheBed() =>
        Assert.Equal("Room 3, Bed A", ReceiptWording.PlaceText(" Room 3 ", "Bed A "));
}
