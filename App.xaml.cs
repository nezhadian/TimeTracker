using Microsoft.UI.Xaml;
using TimeTracker.Services;
using TimeTracker.ViewModels;
using TimeTracker.Views;

namespace TimeTracker;

public partial class App : Application
{
    Window? _window;
    public static MainWindow? Main { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UnhandledException += OnUnhandled;

        var store = new TimeStore();
        if (!store.TryLoadExistingFolder())
        {
            var setup = new SettingsWindow(store, firstRun: true, activeProjectId: null);
            if (!await setup.ShowModalAsync())
            {
                Exit();
                return;
            }
        }

        var viewModel = new MainViewModel(store, new SessionClock(store), new AlarmPlayer());
        _window = new MainWindow(viewModel);
        Main = (MainWindow)_window;
        _window.Activate();
    }

    async void OnUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        var root = Main?.Content as FrameworkElement;
        await DialogService.ShowErrorAsync(root?.XamlRoot, e.Exception.Message);
    }
}