using Nexabode.Services;
using Xunit;

public class ReasonRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("j")]
    [InlineData("ab")]
    [InlineData("..,,")]
    public void TooLittle_IsNotAReason(string? reason)
    {
        Assert.False(ReasonRules.IsReal(reason));
        Assert.NotNull(ReasonRules.Error(reason));
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("n/a ok")]
    [InlineData("123")]
    [InlineData("  Entered twice by mistake  ")]
    public void ThreeLettersOrDigits_IsEnough(string reason)
    {
        Assert.True(ReasonRules.IsReal(reason));
        Assert.Null(ReasonRules.Error(reason));
    }

    [Fact]
    public void ACustomMessage_IsUsedWhenGiven() =>
        Assert.Equal("Say why.", ReasonRules.Error("", "Say why."));
}
