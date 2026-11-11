using Nexabode.Services;
using Xunit;

public class AuditWordingTests
{
    private static readonly DateTime T = new(2026, 11, 9, 7, 14, 0);

    private static AuditItem Item(int id, string type, string field, string? old, string? @new,
                                  string reason = "Edit", DateTime? when = null, string by = "Kurt",
                                  int? tenant = 1, bool exception = false) =>
        new(id, when ?? T, by, type, field, old, @new, reason, exception, tenant);

    [Theory]
    [InlineData("2026-11-05", "Nov 5, 2026")]
    [InlineData("not a date", "not a date")]
    [InlineData(null, "(empty)")]
    public void Dates_AreShownTheWayThePeopleReadThem(string? raw, string expected) =>
        Assert.Equal(expected, AuditWording.FriendlyDate(raw));

    [Theory]
    [InlineData("1100.00", "\u20B11,100.00")]
    [InlineData("50", "\u20B150.00")]
    [InlineData("Voided: n", "Voided: n")]
    public void Money_IsShownWithThePesoSign(string raw, string expected) =>
        Assert.Equal(expected, AuditWording.FriendlyMoney(raw));

    [Fact]
    public void ARentAmountChange_ReadsAsASentence()
    {
        var text = AuditWording.Describe(Item(1, "Charge", "Rent 2026-11: amount", "50.00", "1100.00"));
        Assert.Equal("November 2026 rent amount changed from \u20B150.00 to \u20B11,100.00", text);
    }

    [Fact]
    public void ARentDueDateChange_UsesFriendlyDates()
    {
        var text = AuditWording.Describe(Item(1, "Charge", "Rent 2026-11: due date", "2026-11-05", "2026-11-09"));
        Assert.Equal("November 2026 rent due date changed from Nov 5, 2026 to Nov 9, 2026", text);
    }

    [Fact]
    public void ARestoredRent_IsSaidPlainly() =>
        Assert.Equal("November 2026 rent was restored (it had been voided)",
                     AuditWording.Describe(Item(1, "Charge", "Rent 2026-11: restored", "Voided: n", "1100.00")));

    [Fact]
    public void AManualVoid_ShowsTheAmount() =>
        Assert.Equal("Rent (1 of 30 days) was voided (\u20B150.00)",
                     AuditWording.Describe(Item(1, "Charge", "Rent (1 of 30 days): voided", "50.00", null)));

    [Fact]
    public void StayChanges_ReadAsSentences()
    {
        Assert.Equal("Move-out date changed from Nov 9, 2026 to none (still staying)",
                     AuditWording.Describe(Item(1, "Stay", "MoveOutDate", "2026-11-09", "(none - ongoing)")));
        Assert.Equal("Move-out date changed from none (still staying) to Nov 9, 2026",
                     AuditWording.Describe(Item(2, "Stay", "MoveOutDate", "(none - ongoing)", "2026-11-09")));
        Assert.Equal("Bed changed from Room 101 / Bed 2 to Room 101 / Bed 1",
                     AuditWording.Describe(Item(3, "Stay", "Bed", "Room 101 / Bed 2", "Room 101 / Bed 1")));
        Assert.StartsWith("Stay deleted", AuditWording.Describe(Item(4, "Stay", "Removed", "Room 101 / Bed 1", null)));
        Assert.StartsWith("Stay removed after billing", AuditWording.Describe(Item(5, "Stay", "Cancelled", "Room 101 / Bed 1", null)));
    }

    [Fact]
    public void AStayRecord_IsShownInPlainWords()
    {
        Assert.Equal("Room 101 / Bed 2, Nov 9, 2026 to Nov 9, 2026, rent \u20B11,500.00",
                     AuditWording.FriendlyStay("Room 101 / Bed 2, 2026-11-09 to 2026-11-09, rent 1500.00, deposit 0.00"));
        Assert.Equal("Room 101 / Bed 1, from Nov 9, 2026 (still staying), rent \u20B11,500.00, deposit \u20B13,000.00",
                     AuditWording.FriendlyStay("Room 101 / Bed 1, 2026-11-09 to (none - ongoing), rent 1500.00, deposit 3000.00"));
    }

    [Fact]
    public void AStayRecord_WithACommaInTheRoomName_StillReads() =>
        Assert.StartsWith("Annex, Wing B / Bed 1, Nov 9, 2026",
                          AuditWording.FriendlyStay("Annex, Wing B / Bed 1, 2026-11-09 to 2026-11-30, rent 900.00, deposit 0.00"));

    [Fact]
    public void ARecordThatIsNotAStayLine_IsShownAsItWas() =>
        Assert.Equal("Something else", AuditWording.FriendlyStay("Something else"));

    [Fact]
    public void ACancelledStay_ReadsInWordsNotInRawDates() =>
        Assert.Equal("Stay removed after billing, kept as cancelled: Room 101 / Bed 2, Nov 9, 2026 to Nov 9, 2026, rent \u20B11,500.00",
                     AuditWording.Describe(Item(1, "Stay", "Cancelled",
                         "Room 101 / Bed 2, 2026-11-09 to 2026-11-09, rent 1500.00, deposit 0.00", null)));

    [Fact]
    public void AReceiptPrinting_MentionsADuplicate()
    {
        Assert.Equal("Receipt RCT-2026-00002 printed",
                     AuditWording.Describe(Item(1, "Payment", "Receipt printed", null, "RCT-2026-00002", "Original printed")));
        Assert.Equal("Receipt RCT-2026-00002 printed (duplicate copy)",
                     AuditWording.Describe(Item(2, "Payment", "Receipt printed", null, "RCT-2026-00002", "Duplicate copy, print no. 2")));
    }

    [Fact]
    public void AccountChanges_ReadAsSentences()
    {
        Assert.Equal("Password changed", AuditWording.Describe(Item(1, "Account", "Password", null, null)));
        Assert.Equal("Role changed from Owner to Staff", AuditWording.Describe(Item(2, "Account", "Role", "Owner", "Staff")));
    }

    [Theory]
    [InlineData("Stay", "Stay")]
    [InlineData("Charge", "Billing")]
    [InlineData("UtilityBill", "Billing")]
    [InlineData("Payment", "Payment")]
    [InlineData("Account", "Account")]
    [InlineData("Security", "Security")]
    [InlineData("Something else", "Other")]
    public void EveryRecordKind_HasAPlainCategory(string type, string expected) =>
        Assert.Equal(expected, AuditWording.CategoryOf(type));

    [Theory]
    [InlineData(".", "No reason given")]
    [InlineData("j", "No reason given")]
    [InlineData("", "No reason given")]
    [InlineData(" Edit ", "Edit")]
    [InlineData("Entered twice", "Entered twice")]
    public void AJunkReason_IsReplacedByAnHonestWord(string? reason, string expected) =>
        Assert.Equal(expected, AuditWording.ReasonText(reason));

    [Fact]
    public void RecordsFromOneAction_BecomeOneEvent_WithTheStayAsTheHeadline()
    {
        // Newest first, as stored. One stay edit that also corrected the rent, all in the same second.
        var items = new[]
        {
            Item(4, "Charge", "Rent 2026-11: due date", "2026-11-05", "2026-11-09"),
            Item(3, "Charge", "Rent 2026-11: amount", "50.00", "1100.00"),
            Item(2, "Stay", "MoveOutDate", "2026-11-09", "(none - ongoing)"),
        };

        var events = AuditWording.Group(items);

        var e = Assert.Single(events);
        Assert.Equal("Stay", e.Category);
        Assert.Equal("Move-out date changed from Nov 9, 2026 to none (still staying)", e.Title);
        Assert.Equal(2, e.Details.Count);
        Assert.Contains("November 2026 rent amount changed from \u20B150.00 to \u20B11,100.00", e.Details);
        Assert.Equal("Edit", e.Reason);
    }

    [Fact]
    public void ARestoreEvent_IsHeadlinedByTheRestore()
    {
        var items = new[]
        {
            Item(12, "Charge", "Rent 2026-11: amount", "50.00", "1100.00", "Wrong void"),
            Item(11, "Charge", "Rent 2026-11: restored", "Voided: n", "1100.00", "Wrong void"),
        };
        var e = Assert.Single(AuditWording.Group(items));
        Assert.Equal("November 2026 rent was restored (it had been voided)", e.Title);
        Assert.Single(e.Details);
    }

    [Fact]
    public void ADifferentPerson_ADifferentReason_OrALaterTime_StartsANewEvent()
    {
        var items = new[]
        {
            Item(5, "Stay", "Bed", "A", "B", "second", T.AddMinutes(5)),
            Item(4, "Stay", "Bed", "A", "B", "first", T),
            Item(3, "Stay", "Bed", "A", "B", "first", T, by: "Russel"),
            Item(2, "Stay", "Bed", "A", "B", "first", T, tenant: 2),
            Item(1, "Stay", "Bed", "A", "B", "first", T.AddSeconds(-30)),
        };
        Assert.Equal(5, AuditWording.Group(items).Count);
    }

    [Fact]
    public void AnEvent_RemembersThatAnOverlapWasAccepted()
    {
        var items = new[] { Item(1, "Stay", "Bed", "A", "B", exception: true) };
        Assert.True(Assert.Single(AuditWording.Group(items)).IsException);
    }

    [Theory]
    [InlineData("2026-11-09 08:00", "2026-11-09 18:00", "Today")]
    [InlineData("2026-11-08 08:00", "2026-11-09 18:00", "Yesterday")]
    [InlineData("2026-11-01 08:00", "2026-11-09 18:00", "Nov 1, 2026")]
    public void TheDayHeaders_SayTodayAndYesterday(string when, string now, string expected) =>
        Assert.Equal(expected, AuditWording.DayLabel(DateTime.Parse(when), DateTime.Parse(now)));

    [Fact]
    public void TheSearch_LooksAtTheSentence_TheReasonThePersonAndTheTenant()
    {
        var e = Assert.Single(AuditWording.Group(new[] { Item(1, "Stay", "Bed", "A", "B", "Wrong bed") }));
        Assert.True(AuditWording.Matches(e, "Joseph", ""));
        Assert.True(AuditWording.Matches(e, "Joseph", "bed changed"));
        Assert.True(AuditWording.Matches(e, "Joseph", "wrong"));
        Assert.True(AuditWording.Matches(e, "Joseph", "kurt"));
        Assert.True(AuditWording.Matches(e, "Joseph", "joseph"));
        Assert.False(AuditWording.Matches(e, "Joseph", "payment"));
    }
}
