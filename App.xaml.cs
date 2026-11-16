using System.Windows;
using System.Windows.Threading;
using TimeTracker.Services;
using TimeTracker.ViewModels;
using TimeTracker.Views;

namespace TimeTracker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        var store = new TimeStore();
        if (!store.TryLoadExistingFolder())
        {
            var setup = new SettingsWindow(store, firstRun: true, activeProjectId: null);
            if (setup.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var window = new MainWindow(new MainViewModel(store, new SessionClock(store), new AlarmPlayer()));
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Focus", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
