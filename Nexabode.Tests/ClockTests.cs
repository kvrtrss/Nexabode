using Nexabode.Services;

public class ClockTests
{
    [Fact]
    public void AFrozenClock_StaysOnTheChosenDay_AndResetsToTheRealOne()
    {
        Clock.Freeze(new DateTime(2026, 3, 9, 15, 30, 0));
        try
        {
            Assert.Equal(new DateTime(2026, 3, 9), Clock.Today);
            Assert.Equal(new DateOnly(2026, 3, 9), StayQueries.Today);
        }
        finally { Clock.Reset(); }

        Assert.NotEqual(new DateTime(2026, 3, 9), Clock.Today);
    }
}
