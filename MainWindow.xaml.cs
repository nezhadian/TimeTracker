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
        _viewModel = viewModel;
        InitializeComponent();
        WindowHelper.Configure(this, 1280, 820, 1100, 700);
        Root.DataContext = viewModel;
        if (Nav.MenuItems.Count > 0)
            Nav.SelectedItem = Nav.MenuItems[0];
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

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        _viewModel.ShowHistory = tag == "history";
        if (_viewModel.ShowHistory)
            HistoryTimelineScroll.ChangeView(0, 0, null, disableAnimation: true);
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

    void RemoveBreakReason_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string reason })
            return;
        if (_viewModel.RemoveSavedBreakReasonCommand.CanExecute(reason))
            _viewModel.RemoveSavedBreakReasonCommand.Execute(reason);
    }
}