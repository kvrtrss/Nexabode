using Nexabode.Services;
using Xunit;

public class ReceiptNumberRulesTests
{
    [Fact]
    public void Format_IsYearThenFiveDigits() =>
        Assert.Equal("RCT-2026-00042", ReceiptNumberRules.Format(2026, 42));

    [Fact]
    public void Format_GrowsPastFiveDigits() =>
        Assert.Equal("RCT-2026-123456", ReceiptNumberRules.Format(2026, 123456));

    [Fact]
    public void TryGetSequence_ReadsTheRunningNumber()
    {
        Assert.True(ReceiptNumberRules.TryGetSequence("RCT-2026-00042", 2026, out var seq));
        Assert.Equal(42, seq);
    }

    [Theory]
    [InlineData("RCT-2025-00042")]   // another year
    [InlineData("INV-2026-00042")]   // another prefix
    [InlineData("RCT-2026-abc")]     // not a number
    [InlineData("RCT-2026-")]        // nothing after the year
    [InlineData("")]
    [InlineData(null)]
    public void TryGetSequence_RejectsOtherText(string? number) =>
        Assert.False(ReceiptNumberRules.TryGetSequence(number, 2026, out _));

    [Fact]
    public void NextNumber_IsTheWholeNumber()
    {
        var existing = new string?[] { "RCT-2026-00001", "RCT-2026-00003" };
        Assert.Equal("RCT-2026-00004", ReceiptNumberRules.NextNumber(existing, 2026));
        Assert.Equal("RCT-2027-00001", ReceiptNumberRules.NextNumber(existing, 2027));
    }

    [Fact]
    public void NextSequence_StartsAtOne() =>
        Assert.Equal(1, ReceiptNumberRules.NextSequence(Array.Empty<string?>(), 2026));

    [Fact]
    public void NextSequence_ContinuesFromTheHighest_NotTheCount()
    {
        // 00002 is missing, but the next number must still be after 00005, so no number is ever reused.
        var existing = new string?[] { "RCT-2026-00001", "RCT-2026-00005", "RCT-2026-00003", null };
        Assert.Equal(6, ReceiptNumberRules.NextSequence(existing, 2026));
    }

    [Fact]
    public void NextSequence_EachYearStartsAgain()
    {
        var existing = new string?[] { "RCT-2025-00120", "RCT-2025-00121" };
        Assert.Equal(1, ReceiptNumberRules.NextSequence(existing, 2026));
        Assert.Equal(122, ReceiptNumberRules.NextSequence(existing, 2025));
    }
}
