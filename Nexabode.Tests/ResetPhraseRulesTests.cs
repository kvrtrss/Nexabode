using Nexabode.Services;
using Xunit;

public class ResetPhraseRulesTests
{
    [Fact]
    public void Normalize_IgnoresCapitalsAndExtraSpaces() =>
        Assert.Equal("blue carabao jumps high", ResetPhraseRules.Normalize("  Blue   CARABAO jumps\thigh "));

    [Fact]
    public void Normalize_OfNothing_IsEmpty()
    {
        Assert.Equal("", ResetPhraseRules.Normalize(null));
        Assert.Equal("", ResetPhraseRules.Normalize("   "));
    }

    [Fact]
    public void AGoodPhrase_IsAccepted() =>
        Assert.Null(ResetPhraseRules.Error("My carabao sings at dawn", "kurt"));

    [Theory]
    [InlineData("")]
    [InlineData("password")]
    [InlineData("two words")]
    [InlineData("a b c d e f")]            // enough words, too short
    public void ATooShortPhrase_IsRefused(string phrase) =>
        Assert.NotNull(ResetPhraseRules.Error(phrase));

    [Fact]
    public void ARepeatedWord_IsRefused() =>
        Assert.NotNull(ResetPhraseRules.Error("again again again again again"));

    [Fact]
    public void APhraseWithTheUsername_IsRefused() =>
        Assert.NotNull(ResetPhraseRules.Error("kurt likes the blue carabao", "Kurt"));

    [Fact]
    public void AShortUsername_IsNotAnExcuseToRefuse() =>
        Assert.Null(ResetPhraseRules.Error("an old carabao sings at dawn", "an"));
}
