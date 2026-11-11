using Nexabode.Services;
using Xunit;

public class GenderRulesTests
{
    [Theory]
    [InlineData("Male", "Male")]
    [InlineData("female", "Female")]
    [InlineData("  OTHER ", "Other")]
    public void AKnownChoice_IsStoredInItsProperForm(string input, string expected) =>
        Assert.Equal(expected, GenderRules.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not set")]
    [InlineData("not set")]
    [InlineData("Robot")]
    public void NotSet_BlankOrUnknown_IsStoredAsNothing(string? input) =>
        Assert.Null(GenderRules.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not set")]
    [InlineData("Male")]
    [InlineData("female")]
    [InlineData("Other")]
    public void TheseValues_AreAccepted(string? input) =>
        Assert.Null(GenderRules.Error(input));

    [Fact]
    public void AnythingElse_IsRefused() =>
        Assert.NotNull(GenderRules.Error("Robot"));

    [Theory]
    [InlineData(null, "Not set")]
    [InlineData("", "Not set")]
    [InlineData("Female", "Female")]
    [InlineData("male", "Male")]
    public void TheDisplay_ShowsNotSetWhenThereIsNothing(string? stored, string expected) =>
        Assert.Equal(expected, GenderRules.Display(stored));

    [Fact]
    public void TheDropDown_StartsWithNotSet_AndOffersTheThreeChoices() =>
        Assert.Equal(new[] { "Not set", "Male", "Female", "Other" }, GenderRules.Choices);
}
