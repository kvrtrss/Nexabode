using Nexabode.Services;
using Xunit;

public class FloorRulesTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("01", "1")]
    [InlineData("  Floor   2 ", "2")]
    [InlineData("level 3", "3")]
    [InlineData("g", "Ground")]
    [InlineData("ground floor", "Ground")]
    [InlineData("Basement", "Basement")]
    [InlineData("mezzanine", "Mezzanine")]
    public void Normalize_GivesOneSpellingPerFloor(string input, string expected) =>
        Assert.Equal(expected, FloorRules.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Floor")]
    public void Normalize_BlankIsNull(string? input) => Assert.Null(FloorRules.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_RequiresAFloor(string? input) => Assert.NotNull(FloorRules.Validate(input));

    [Fact]
    public void Validate_RejectsVeryLongFloor() => Assert.NotNull(FloorRules.Validate(new string('a', 21)));

    [Theory]
    [InlineData("1")]
    [InlineData("Ground")]
    [InlineData("12")]
    public void Validate_AcceptsRealFloors(string input) => Assert.Null(FloorRules.Validate(input));

    [Theory]
    [InlineData("1", "Floor 1")]
    [InlineData("Floor 2", "Floor 2")]
    [InlineData("g", "Ground floor")]
    [InlineData("basement", "Basement")]
    [InlineData("mezzanine", "Mezzanine floor")]
    [InlineData("2A", "Floor 2A")]
    [InlineData(null, "No floor set")]
    public void Label_IsReadable(string? input, string expected) => Assert.Equal(expected, FloorRules.Label(input));

    [Fact]
    public void Order_IsNumericNotAlphabetical()
    {
        var floors = new[] { "10", null, "2", "Mezzanine", "1", "Ground", "Basement", "Floor 3" };
        var sorted = floors.OrderBy(f => f, Comparer<string?>.Create(FloorRules.Compare))
                           .Select(f => FloorRules.Normalize(f)).ToArray();
        Assert.Equal(new string?[] { "Basement", "Ground", "1", "2", "3", "10", "Mezzanine", null }, sorted);
    }
}
