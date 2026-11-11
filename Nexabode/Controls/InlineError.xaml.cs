using System.Windows.Controls;

namespace Nexabode.Controls;

// Dialogs set ErrorText.Text and ErrorText.Visibility, exactly as they did with a plain TextBlock.
public partial class InlineError : UserControl
{
    public InlineError() => InitializeComponent();

    public string Text
    {
        get => MessageText.Text;
        set => MessageText.Text = value ?? string.Empty;
    }
}
