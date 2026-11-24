using Nexabode.Services;
using Xunit;

public class RentDueRulesTests
{
    private static readonly DateOnly UsualDue = new(2026, 11, 5);

    [Theory]
    [InlineData(2026, 11, 5, 5)]
    [InlineData(2026, 11, 28, 28)]
    [InlineData(2026, 11, 31, 30)]   // November has 30 days
    [InlineData(2026, 2, 31, 28)]    // and February 2026 has 28
    [InlineData(2026, 11, 0, 1)]     // never earlier than the 1st
    public void TheUsualDueDate_IsTheChosenDay_ButNeverOutsideTheMonth(int year, int month, int dueDay, int expectedDay) =>
        Assert.Equal(new DateOnly(year, month, expectedDay), RentDueRules.UsualDueDate(new DateOnly(year, month, 1), dueDay));

    [Fact]
    public void AStayThatStartedBeforeTheUsualDueDate_IsDueOnTheUsualDate() =>
        Assert.Equal(UsualDue, RentDueRules.DueDate(UsualDue, new DateOnly(2026, 10, 1)));

    [Fact]
    public void ALongStandingTenant_IsDueOnTheUsualDate() =>
        Assert.Equal(UsualDue, RentDueRules.DueDate(UsualDue, new DateOnly(2025, 3, 20)));

    [Fact]
    public void MovingInOnTheUsualDueDate_IsDueThatDay() =>
        Assert.Equal(UsualDue, RentDueRules.DueDate(UsualDue, UsualDue));

    [Fact]
    public void MovingInAfterTheUsualDueDate_IsDueOnTheMoveInDay_NotBeforeTheStay()
    {
        var moveIn = new DateOnly(2026, 11, 13);
        Assert.Equal(moveIn, RentDueRules.DueDate(UsualDue, moveIn));
    }
}
