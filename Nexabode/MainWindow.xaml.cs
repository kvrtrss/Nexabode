using Nexabode.Views;
using Wpf.Ui.Controls;

namespace Nexabode;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }
}