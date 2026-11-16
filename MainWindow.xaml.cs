using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeTracker.Services;
using TimeTracker.ViewModels;

namespace TimeTracker;

public sealed partial class MainWindow : Window
{
    readonly MainViewModel _viewModel;
    readonly Microsoft.UI.Xaml.DispatcherTimer _timer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        WindowHelper.Configure(this, 1120, 820, 980, 700);
        _viewModel = viewModel;
        Root.DataContext = viewModel;
        _timer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Tick();

        var started = false;
        Activated += (_, _) =>
        {
            if (_viewModel.DialogXamlRoot is null)
                _viewModel.DialogXamlRoot = Root.XamlRoot;
            if (started)
                return;
            started = true;
            _viewModel.Start();
            _timer.Start();
        };

        Closed += (_, _) =>
        {
            _timer.Stop();
            _viewModel.Flush();
        };
    }

    void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.EndSessionCommand.IsRunning)
            return;
        if (_viewModel.EndSessionCommand.CanExecute(null))
            _ = _viewModel.EndSessionCommand.ExecuteAsync(null);
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.OpenSettingsCommand.CanExecute(null))
            _ = _viewModel.OpenSettingsCommand.ExecuteAsync(null);
    }
}