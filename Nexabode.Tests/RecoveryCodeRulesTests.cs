using Nexabode.Services;
using Xunit;

public class RecoveryCodeRulesTests
{
    [Fact]
    public void GeneratedCode_HasTheRightLength_AndOnlyAllowedCharacters()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = RecoveryCodeRules.Generate();
            Assert.Equal(RecoveryCodeRules.Length, code.Length);
            Assert.True(RecoveryCodeRules.LooksValid(code));
        }
    }

    [Fact]
    public void GeneratedCodes_DoNotRepeat()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => RecoveryCodeRules.Generate()).ToHashSet();
        Assert.Equal(500, codes.Count);
    }

    [Fact]
    public void Alphabet_LeavesOutConfusingCharacters()
    {
        foreach (var c in "01IO") Assert.DoesNotContain(c, RecoveryCodeRules.Alphabet);
        Assert.Equal(32, RecoveryCodeRules.Alphabet.Length);
        Assert.Equal(32, RecoveryCodeRules.Alphabet.Distinct().Count());
    }

    [Fact]
    public void Format_GroupsByFour()
    {
        Assert.Equal("ABCD-EFGH-JKLM-NPQR", RecoveryCodeRules.Format("ABCDEFGHJKLMNPQR"));
    }

    [Theory]
    [InlineData("abcd-efgh-jklm-npqr")]
    [InlineData("  ABCD EFGH JKLM NPQR  ")]
    [InlineData("abcdefghjklmnpqr")]
    [InlineData("ABCD-EFGH-JKLM-NPQR\n")]
    public void Normalize_IgnoresCaseSpacesAndDashes(string typed) =>
        Assert.Equal("ABCDEFGHJKLMNPQR", RecoveryCodeRules.Normalize(typed));

    [Fact]
    public void Normalize_HandlesNull() => Assert.Equal("", RecoveryCodeRules.Normalize(null));

    [Fact]
    public void FormatThenNormalize_GivesTheOriginalBack()
    {
        var code = RecoveryCodeRules.Generate();
        Assert.Equal(code, RecoveryCodeRules.Normalize(RecoveryCodeRules.Format(code)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHJKLMNPQ")]
    [InlineData("ABCDEFGHJKLMNPQRS")]
    [InlineData("ABCDEFGHJKLMNPQ0")]
    public void LooksValid_RejectsWrongLengthOrCharacters(string value) =>
        Assert.False(RecoveryCodeRules.LooksValid(value));

    [Fact]
    public void EmergencyKey_IsLonger_AndGroupsByFour()
    {
        var key = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
        Assert.Equal(20, key.Length);
        Assert.True(RecoveryCodeRules.LooksValid(key, RecoveryCodeRules.EmergencyKeyLength));
        Assert.False(RecoveryCodeRules.LooksValid(key));   // not a 16-character recovery code
        Assert.Equal(4, RecoveryCodeRules.Format(key).Count(c => c == '-'));
        Assert.Equal(key, RecoveryCodeRules.Normalize(RecoveryCodeRules.Format(key)));
    }
}
