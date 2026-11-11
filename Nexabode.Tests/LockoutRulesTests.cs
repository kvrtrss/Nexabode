using Nexabode.Services;
using Xunit;

public class LockoutRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
    private static readonly LockState Fresh = LockoutRules.AfterSuccess();

    [Fact]
    public void FirstTwoFailures_DoNotLock()
    {
        var s = LockoutRules.AfterFailure(Fresh, Now);
        Assert.Equal(2, LockoutRules.AttemptsLeft(s));
        Assert.False(LockoutRules.IsLocked(s, Now, out _));

        s = LockoutRules.AfterFailure(s, Now);
        Assert.Equal(1, LockoutRules.AttemptsLeft(s));
        Assert.False(LockoutRules.IsLocked(s, Now, out _));
    }

    [Fact]
    public void ThirdFailure_Locks_ForFiveMinutes()
    {
        var s = Fresh;
        for (var i = 0; i < LockoutRules.MaxFailedAttempts; i++) s = LockoutRules.AfterFailure(s, Now);

        Assert.True(LockoutRules.IsLocked(s, Now, out var remaining));
        Assert.Equal(TimeSpan.FromMinutes(5), remaining);
    }

    [Fact]
    public void Lock_EndsOnTime_AndGivesAFreshRoundOfAttempts()
    {
        var s = Fresh;
        for (var i = 0; i < 3; i++) s = LockoutRules.AfterFailure(s, Now);

        var later = Now.AddMinutes(5).AddSeconds(1);
        s = LockoutRules.Refresh(s, later);

        Assert.False(LockoutRules.IsLocked(s, later, out _));
        Assert.Equal(LockoutRules.MaxFailedAttempts, LockoutRules.AttemptsLeft(s));
    }

    [Fact]
    public void RepeatedLocks_GetLonger_UpToTheLongestLock()
    {
        var expected = new[] { 5, 15, 30, 60, 60, 60 };
        var s = Fresh;
        var clock = Now;

        foreach (var minutes in expected)
        {
            for (var i = 0; i < 3; i++) s = LockoutRules.AfterFailure(s, clock);
            Assert.True(LockoutRules.IsLocked(s, clock, out var remaining));
            Assert.Equal(TimeSpan.FromMinutes(minutes), remaining);

            clock = clock.AddMinutes(minutes).AddSeconds(1);
            s = LockoutRules.Refresh(s, clock);
        }
    }

    [Fact]
    public void SuccessfulSignIn_StartsOver()
    {
        var s = Fresh;
        for (var i = 0; i < 3; i++) s = LockoutRules.AfterFailure(s, Now);
        s = LockoutRules.Refresh(s, Now.AddMinutes(6));
        Assert.Equal(1, s.LockLevel);

        s = LockoutRules.AfterSuccess();
        Assert.Equal(0, s.LockLevel);
        Assert.Equal(LockoutRules.MaxFailedAttempts, LockoutRules.AttemptsLeft(s));

        for (var i = 0; i < 3; i++) s = LockoutRules.AfterFailure(s, Now);
        LockoutRules.IsLocked(s, Now, out var remaining);
        Assert.Equal(TimeSpan.FromMinutes(5), remaining);
    }

    [Fact]
    public void ClockMovedBack_CannotStretchTheLock()
    {
        var s = new LockState(0, 1, Now.AddDays(30));
        var refreshed = LockoutRules.Refresh(s, Now);

        Assert.True(LockoutRules.IsLocked(refreshed, Now, out var remaining));
        Assert.Equal(LockoutRules.LongestLock, remaining);
        Assert.True(LockoutRules.IsLocked(s, Now, out var direct));
        Assert.Equal(LockoutRules.LongestLock, direct);
    }

    [Fact]
    public void NotLocked_WhenNothingIsSaved()
    {
        Assert.False(LockoutRules.IsLocked(Fresh, Now, out var remaining));
        Assert.Equal(TimeSpan.Zero, remaining);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(272, "04:32")]
    [InlineData(300, "05:00")]
    [InlineData(3600, "60:00")]
    public void Countdown_IsMinutesAndSeconds(int seconds, string expected) =>
        Assert.Equal(expected, LockoutRules.Countdown(TimeSpan.FromSeconds(seconds)));
}
