using Nexabode.Services;
using Xunit;

public class FmtCountTests
{
    [Theory]
    [InlineData(0, "0 beds")]
    [InlineData(1, "1 bed")]
    [InlineData(2, "2 beds")]
    [InlineData(12, "12 beds")]
    public void Count_UsesTheRightWord(int number, string expected) =>
        Assert.Equal(expected, Fmt.Count(number, "bed"));

    [Theory]
    [InlineData(1, "1 person")]
    [InlineData(3, "3 people")]
    public void Count_AcceptsAnIrregularPlural(int number, string expected) =>
        Assert.Equal(expected, Fmt.Count(number, "person", "people"));
}
