using System.Windows;
using Nexabode.Data;

namespace Nexabode;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            DbInitializer.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database error:\n{ex.Message}", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}