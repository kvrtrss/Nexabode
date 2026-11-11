using Nexabode.Services;
using Xunit;

public class RecoverySheetTextTests
{
    [Theory]
    [InlineData(RecoverySheetKind.RecoveryCode, "Forgot password?")]
    [InlineData(RecoverySheetKind.EmergencyKey, "Locked out of every account?")]
    public void TheSteps_NameTheRightButtonOnTheSignInScreen(RecoverySheetKind kind, string button) =>
        Assert.Contains(RecoverySheetText.Steps(kind), step => step.Contains(button));

    [Theory]
    [InlineData(RecoverySheetKind.RecoveryCode)]
    [InlineData(RecoverySheetKind.EmergencyKey)]
    public void EverySheetHasAHeadingAPurposeStepsAndCautions(RecoverySheetKind kind)
    {
        Assert.False(string.IsNullOrWhiteSpace(RecoverySheetText.Heading(kind)));
        Assert.False(string.IsNullOrWhiteSpace(RecoverySheetText.SecretCaption(kind)));
        Assert.False(string.IsNullOrWhiteSpace(RecoverySheetText.Purpose(kind)));
        Assert.True(RecoverySheetText.Steps(kind).Count >= 3);
        Assert.True(RecoverySheetText.Cautions(kind).Count >= 3);
    }

    [Fact]
    public void TheEmergencyKeySheet_MentionsTheResetPhrase_ButTheRecoveryCodeSheetDoesNot()
    {
        Assert.Contains(RecoverySheetText.Steps(RecoverySheetKind.EmergencyKey),
                        step => step.Contains("reset phrase", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(RecoverySheetText.Steps(RecoverySheetKind.RecoveryCode),
                              step => step.Contains("reset phrase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheAccountLine_IsOnlyForRecoveryCodes()
    {
        Assert.Equal("Account: kurt", RecoverySheetText.AccountLine(RecoverySheetKind.RecoveryCode, " kurt "));
        Assert.Null(RecoverySheetText.AccountLine(RecoverySheetKind.RecoveryCode, ""));
        Assert.Null(RecoverySheetText.AccountLine(RecoverySheetKind.EmergencyKey, "kurt"));
    }
}
