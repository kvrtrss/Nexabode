using System.Windows;
using Nexabode.Data;
using Nexabode.Services;
using Nexabode.Views;

namespace Nexabode;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only one copy of Nexabode may run at a time, so two windows can't write to the same database.
        _singleInstance = new Mutex(true, @"Local\Nexabode.SingleInstance", out var isFirstCopy);
        if (!isFirstCopy)
        {
            // Ask the copy that is already running to come to the front, then quit quietly.
            try { EventWaitHandle.OpenExisting(@"Local\Nexabode.Show").Set(); } catch { }
            Shutdown();
            return;
        }

        ListenForSecondCopies();

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Something went wrong:\n\n" + args.Exception.Message +
                            "\n\nNexabode will keep running. If this keeps happening, restart it.",
                            "Nexabode", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        List<string> warnings;
        try
        {
            warnings = DbInitializer.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database error:\n{ex.Message}", "Nexabode",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ThemeService.Initialize();

        if (warnings.Count > 0)
            MessageBox.Show(
                "Nexabode could not turn on every data safety rule, because your existing data already contains duplicates:\n\n" +
                string.Join("\n\n", warnings.Select(w => "- " + w)) +
                "\n\nThe app works normally. Please report this on the project's GitHub page so it can be fixed.",
                "Nexabode", MessageBoxButton.OK, MessageBoxImage.Warning);

        new LoginWindow().Show();
    }

    private void ListenForSecondCopies()
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Nexabode.Show");
        _showSignal = signal;

        var listener = new Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.BeginInvoke(new Action(BringWindowToFront));
        })
        { IsBackground = true, Name = "Nexabode single-instance listener" };
        listener.Start();
    }

    private void BringWindowToFront()
    {
        var window = this.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
        if (window == null) return;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
        window.Topmost = true;    // a quick flip makes Windows really bring it to the front
        window.Topmost = false;
        window.Focus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }
}