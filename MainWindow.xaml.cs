using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TimeTracker.ViewModels;

namespace TimeTracker;

public partial class MainWindow : Window
{
    readonly DispatcherTimer _timer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => viewModel.Tick();
        Loaded += (_, _) =>
        {
            viewModel.Start();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            viewModel.Flush();
        };
    }

    void OpenMore(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
