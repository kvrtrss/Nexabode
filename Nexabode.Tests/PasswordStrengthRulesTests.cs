using Nexabode.Services;
using Xunit;

public class PasswordStrengthRulesTests
{
    [Fact]
    public void Empty_HasNoLevel() =>
        Assert.Equal(PasswordStrength.None, PasswordStrengthRules.Evaluate("").Level);

    [Theory]
    [InlineData("short")]            // under 8
    [InlineData("password123")]      // a very common password
    [InlineData("Admin123")]         // common, any capitals
    [InlineData("aaaaaaaaaa")]       // one character repeated
    [InlineData("12345678")]         // a sequence
    [InlineData("hgfedcba")]         // a sequence, backwards
    public void WeakPasswords(string password) =>
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthRules.Evaluate(password).Level);

    [Fact]
    public void ContainingTheUsername_IsWeak() =>
        Assert.Equal(PasswordStrength.Weak, PasswordStrengthRules.Evaluate("johnsmith99!", "JohnSmith").Level);

    [Fact]
    public void ShortUsernames_AreNotMatched() =>
        Assert.NotEqual(PasswordStrength.Weak, PasswordStrengthRules.Evaluate("Sunflower9", "su").Level);

    [Theory]
    [InlineData("sunflower", PasswordStrength.Fair)]          // 9, one kind of character
    [InlineData("Sunflower9", PasswordStrength.Good)]         // 10, three kinds
    [InlineData("bluekettle12", PasswordStrength.Good)]       // 12, two kinds
    [InlineData("Sunflower9!x", PasswordStrength.Strong)]     // 12, four kinds
    [InlineData("blue kettle river", PasswordStrength.Strong)] // a long passphrase
    public void Levels(string password, PasswordStrength expected) =>
        Assert.Equal(expected, PasswordStrengthRules.Evaluate(password).Level);

    [Fact]
    public void EveryLevelHasAHint()
    {
        foreach (var p in new[] { "short", "sunflower", "Sunflower9", "blue kettle river" })
            Assert.False(string.IsNullOrWhiteSpace(PasswordStrengthRules.Evaluate(p).Hint));
    }

    [Fact]
    public void Labels() => Assert.Equal("Strong", PasswordStrengthRules.Label(PasswordStrength.Strong));
}
