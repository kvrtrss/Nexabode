using Nexabode.Services;
using Xunit;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("Maria.Santos")]
    [InlineData("landlord_01")]
    [InlineData("a-b")]
    public void GoodUsernames_AreAccepted(string name) => Assert.Null(UsernameRules.Validate(name));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("   ")]
    public void TooShort_IsRejected(string name) => Assert.NotNull(UsernameRules.Validate(name));

    [Fact]
    public void TooLong_IsRejected() => Assert.NotNull(UsernameRules.Validate(new string('a', 33)));

    [Fact]
    public void ExactlyTheMaximum_IsAccepted() => Assert.Null(UsernameRules.Validate(new string('a', 32)));

    [Theory]
    [InlineData("two words")]
    [InlineData("name!")]
    [InlineData("café")]
    [InlineData("a@b.com")]
    public void SpacesAndSymbols_AreRejected(string name) => Assert.NotNull(UsernameRules.Validate(name));

    [Fact]
    public void SurroundingSpaces_AreIgnored() => Assert.Null(UsernameRules.Validate("  admin  "));

    [Fact]
    public void Initial_IsTheUppercaseFirstLetter()
    {
        Assert.Equal("M", UsernameRules.Initial("maria"));
        Assert.Equal("L", UsernameRules.Initial("  landlord"));
        Assert.Equal("A", UsernameRules.Initial(""));
    }
}