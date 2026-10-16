using System.Windows;
using Nexabode.Services;
using Nexabode.Views;
using Wpf.Ui.Controls;

namespace Nexabode;

public partial class MainWindow : FluentWindow
{
    private readonly Type _startPage;

    public MainWindow() : this(typeof(DashboardPage)) { }

    public MainWindow(Type startPage)
    {
        InitializeComponent();
        _startPage = startPage;

        ThemeButton.Icon = new SymbolIcon(ThemeService.IsDark
            ? SymbolRegular.WeatherSunny24
            : SymbolRegular.WeatherMoon24);

        Loaded += (_, _) => RootNavigation.Navigate(_startPage);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        // Remember which page is open so we come back to it.
        var current = (RootNavigation.SelectedItem as NavigationViewItem)?.TargetPageType
                      ?? typeof(DashboardPage);

        ThemeService.Toggle();            // saves and applies the new theme

        var fresh = new MainWindow(current);   // a new window starts in the new theme -
        fresh.Show();
        Close();
    }
    public void NavigateTo(Type page) => RootNavigation.Navigate(page);
}