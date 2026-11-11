using Nexabode.Services;
using Xunit;

public class ClashWarningsTests
{
    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "Joseph" }, "Joseph")]
    [InlineData(new[] { "Joseph", "Kikay" }, "Joseph and Kikay")]
    [InlineData(new[] { "Joseph", "Kikay", "Ana" }, "Joseph, Kikay and Ana")]
    public void Names_AreJoinedTheWayYouWouldSayThem(string[] names, string expected) =>
        Assert.Equal(expected, ClashWarnings.JoinNames(names));

    [Fact]
    public void TheTenantWarning_NamesTheBedAndTheOtherTenant()
    {
        var text = ClashWarnings.TenantBedWarning("Room 101 / Bed 1", new[] { "Joseph" }, isOwner: true);
        Assert.Contains("Room 101 / Bed 1 is also booked for Joseph", text);
        Assert.Contains("Edit this stay", text);
        Assert.Contains("Remove this stay", text);
    }

    [Fact]
    public void ForStaff_TheTenantWarning_SendsRemovalToAnOwner()
    {
        var text = ClashWarnings.TenantBedWarning("Room 101 / Bed 1", new[] { "Joseph" }, isOwner: false);
        Assert.Contains("ask an Owner", text);
        Assert.DoesNotContain("Remove this stay", text);
    }

    [Theory]
    [InlineData(1, "Check: 1 bed has a clash")]
    [InlineData(3, "Check: 3 beds have a clash")]
    public void TheRoomTitle_CountsTheBeds(int beds, string expected) =>
        Assert.Equal(expected, ClashWarnings.RoomTitle(beds));

    [Fact]
    public void ABedLine_NamesTheBedAndEveryTenantOnIt() =>
        Assert.Equal("Bed 1: Joseph and Kikay are booked on some of the same days.",
                     ClashWarnings.BedLine("Bed 1", new[] { "Joseph", "Kikay" }));

    [Fact]
    public void ABedLine_WithASingleName_StaysGrammatical() =>
        Assert.Equal("Bed 1: Kikay is booked on some of the same days.",
                     ClashWarnings.BedLine("Bed 1", new[] { "Kikay" }));

    [Fact]
    public void TheRoomAdvice_PointsToTheButtonsBelowAndDiffersForStaff()
    {
        var owner = ClashWarnings.RoomAdvice(isOwner: true);
        Assert.Contains("Edit this stay", owner);
        Assert.Contains("Remove this stay", owner);

        var staff = ClashWarnings.RoomAdvice(isOwner: false);
        Assert.Contains("ask an Owner", staff);
        Assert.DoesNotContain("Remove this stay", staff);
    }

    [Fact]
    public void TheBedChipTip_SaysWhatTheMarkMeans()
    {
        var tip = ClashWarnings.BedChipTip("Bed 1", "Occupied");
        Assert.Contains("Bed 1: Occupied", tip);
        Assert.Contains("overlap", tip);
    }
}
