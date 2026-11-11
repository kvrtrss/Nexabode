using System.Windows;

namespace Nexabode.Views;

// Shown after a payment is saved. It repeats what was recorded (and any change to return) and lets the
// person print the receipt right away. A second printing is marked as a duplicate copy on the paper.
public partial class PaymentRecordedDialog : Window
{
    private readonly int _paymentId;

    private PaymentRecordedDialog(int paymentId, string message)
    {
        InitializeComponent();
        _paymentId = paymentId;
        MessageText.Text = message;
    }

    public static void Show(Window? owner, int paymentId, string message)
    {
        var dialog = new PaymentRecordedDialog(paymentId, message);
        if (owner != null) dialog.Owner = owner;
        dialog.ShowDialog();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptPrinter.Print(this, _paymentId))
        {
            PrintButton.Content = "Print again";
            PrintButton.ToolTip = "A second printing is marked as a duplicate copy";
        }
    }
}
