using Nexabode.Services;
using Xunit;

public class ConfirmWordingTests
{
    [Fact]
    public void OnlyTheFieldsThatChanged_AreListed()
    {
        var changes = ConfirmWording.Changes(
            ("Full name", "Joseph", "Joseph"),
            ("Contact number", null, "0917 123 4567"),
            ("Notes", "Quiet", ""));

        Assert.Equal(2, changes.Count);
        Assert.Equal("Contact number: (empty) to 0917 123 4567", changes[0]);
        Assert.Equal("Notes: Quiet to (empty)", changes[1]);
    }

    [Fact]
    public void SpacesAroundAValue_AreNotAChange()
    {
        var changes = ConfirmWording.Changes(("Full name", "Joseph", "  Joseph "), ("Notes", null, "   "));
        Assert.Empty(changes);
    }

    [Fact]
    public void AnEditMessage_NamesWhatIsBeingSaved_AndListsTheChanges()
    {
        var text = ConfirmWording.EditTenant("Joseph", new[] { "Contact number: (empty) to 0917" });
        Assert.StartsWith("Save these changes to Joseph's record?", text);
        Assert.Contains("- Contact number: (empty) to 0917", text);

        Assert.Contains("Room 101, Bed 1?", ConfirmWording.EditBed("Room 101", "Bed 1", new[] { "x" }));
        Assert.Contains("Save these changes to Room 101?", ConfirmWording.EditRoom("Room 101", new[] { "x" }));
    }

    [Fact]
    public void AnyRecordEdit_NamesWhatIsSaved_AndListsTheChanges()
    {
        var text = ConfirmWording.EditRecord("Joseph's monthly rent",
            ConfirmWording.Changes(("Amount", "\u20B11,500.00", "\u20B11,400.00"), ("Due date", "Nov 5, 2026", "Nov 5, 2026")));
        Assert.StartsWith("Save these changes to Joseph's monthly rent?", text);
        Assert.Contains("- Amount: \u20B11,500.00 to \u20B11,400.00", text);
        Assert.DoesNotContain("Due date", text);
    }

    [Fact]
    public void TheRoomStatusMessages_SayWhatChanges()
    {
        Assert.Contains("Deactivate Room 101?", ConfirmWording.RoomStatus("Room 101", activate: false));
        Assert.Contains("activate it again", ConfirmWording.RoomStatus("Room 101", activate: false));
        Assert.StartsWith("Activate Room 101?", ConfirmWording.RoomStatus("Room 101", activate: true));
    }

    [Fact]
    public void TheBedStatusMessages_NameTheRoomAndTheBed()
    {
        Assert.Contains("Deactivate Room 101, Bed 1?", ConfirmWording.BedStatus("Room 101", "Bed 1", activate: false));
        Assert.StartsWith("Activate Room 101, Bed 1?", ConfirmWording.BedStatus("Room 101", "Bed 1", activate: true));
        Assert.Contains("under maintenance", ConfirmWording.MaintenanceStart("Room 101", "Bed 1"));
        Assert.Contains("End the maintenance on Room 101, Bed 1?", ConfirmWording.MaintenanceEnd("Room 101", "Bed 1"));
    }

    [Fact]
    public void ApplyingCredit_ShowsTheCreditAndTheBalanceBeforeAndAfter()
    {
        var text = ConfirmWording.ApplyCredit("Joseph", 500m, 1000m, 1500m);
        Assert.StartsWith("Use \u20B1500.00 of Joseph's credit", text);
        Assert.Contains("Credit: \u20B11,000.00 to \u20B1500.00", text);
        Assert.Contains("Unpaid balance: \u20B11,500.00 to \u20B11,000.00", text);
        Assert.Contains("oldest unpaid charges first", text);
    }

    [Fact]
    public void RefundingAndUndoingARefund_MoveTheCreditInOppositeDirections()
    {
        Assert.Contains("Credit: \u20B11,000.00 to \u20B1700.00", ConfirmWording.RefundCredit("Joseph", 300m, 1000m));
        Assert.Contains("use Undo refund", ConfirmWording.RefundCredit("Joseph", 300m, 1000m));
        Assert.Contains("Credit: \u20B1700.00 to \u20B11,000.00", ConfirmWording.UndoRefund("Joseph", 300m, 700m));
    }

    [Fact]
    public void EditingCredit_ShowsTheChangeAndTheReason()
    {
        var text = ConfirmWording.EditCredit("Joseph", 1500m, 1000m, " Entered twice ");
        Assert.Contains("from \u20B11,500.00 to \u20B11,000.00", text);
        Assert.Contains("Reason: Entered twice", text);
        Assert.Contains("change history", text);
    }

    [Fact]
    public void TheOwnerMessage_NamesTheAccount()
    {
        var text = ConfirmWording.CreateOwner("Kurt");
        Assert.Contains("\"Kurt\"", text);
        Assert.Contains("manage accounts", text);
    }

    [Fact]
    public void ThePasswordMessages_NameTheAccount()
    {
        Assert.Contains("\"Kurt\"", ConfirmWording.ChangePassword("Kurt"));
        Assert.Contains("recovery code", ConfirmWording.RecoverPassword("Kurt"));
    }

    [Fact]
    public void TheUsernameMessage_ShowsTheOldAndTheNewName()
    {
        var text = ConfirmWording.ChangeUsername("Kurt", "KurtR");
        Assert.Contains("from \"Kurt\" to \"KurtR\"", text);
        Assert.Contains("next time you sign in", text);
        Assert.Contains("keep your old name", text);
    }

    [Fact]
    public void TheFirstSignInMessage_MentionsTheUsernameOnlyWhenItChanged()
    {
        Assert.DoesNotContain("username", ConfirmWording.FirstSignIn("admin", "admin"));
        var changed = ConfirmWording.FirstSignIn("admin", "Kurt");
        Assert.Contains("new username \"Kurt\"", changed);
        Assert.Contains("new password", changed);
    }

    [Theory]
    [InlineData("Owner", "Owner", "already Owner")]
    [InlineData("Staff", "Staff", "already Staff")]
    [InlineData("Owner", null, "Choose a role")]
    public void ChoosingTheSameRole_OrNone_IsRefused(string current, string? chosen, string expected) =>
        Assert.Contains(expected, ConfirmWording.RoleChoiceError("Russel", current, chosen));

    [Fact]
    public void ChoosingTheOtherRole_IsAccepted() =>
        Assert.Null(ConfirmWording.RoleChoiceError("Russel", "Owner", "Staff"));

    [Fact]
    public void TheRoleNote_SaysWhoTheAccountIsNow()
    {
        var note = ConfirmWording.RoleChoiceNote("Russel", "Owner");
        Assert.Contains("Russel is now Owner", note);
        Assert.Contains("restore backups", note);
    }
}
