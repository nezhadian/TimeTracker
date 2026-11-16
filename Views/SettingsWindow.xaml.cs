using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TimeTracker.Services;
using TimeTracker.ViewModels;
using WinRT.Interop;

namespace TimeTracker.Views;

public sealed partial class SettingsWindow : Window
{
    readonly SettingsViewModel _viewModel;
    readonly bool _projectsOnly;
    readonly bool _firstRun;
    bool _saved;
    TaskCompletionSource<bool>? _modalTask;
    Flyout? _swatchFlyout;

    public SettingsWindow(TimeStore store, bool firstRun, string? activeProjectId, bool projectsOnly = false)
    {
        InitializeComponent();
        WindowHelper.Configure(this, 640, 520, 480, 320);
        _projectsOnly = projectsOnly;
        _firstRun = firstRun;
        _viewModel = new SettingsViewModel(store, firstRun, activeProjectId);
        Root.DataContext = _viewModel;
        if (projectsOnly)
        {
            Title = "Projects";
            GeneralSettings.Visibility = Visibility.Collapsed;
            ProjectSettings.Margin = new Thickness(0);
        }
        else
        {
            ProjectSettings.Visibility = Visibility.Collapsed;
            if (firstRun)
                Title = "Choose where to save";
        }

        Closed += (_, _) => _modalTask?.TrySetResult(_saved);
    }

    public Task<bool> ShowModalAsync()
    {
        _modalTask = new TaskCompletionSource<bool>();
        Activate();
        return _modalTask.Task;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_projectsOnly ? _viewModel.TrySaveProjects() : _viewModel.TrySave())
        {
            _saved = true;
            Close();
        }
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_firstRun)
        {
            _saved = false;
            Close();
            Application.Current.Exit();
            return;
        }

        _saved = false;
        Close();
    }

    async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var owner = WindowNative.GetWindowHandle(this);
        var picked = await DataFolderPicker.PickAsync("Choose where Time Tracker saves history", _viewModel.DataFolder, owner);
        if (!string.IsNullOrWhiteSpace(picked))
            _viewModel.DataFolder = picked;
    }

    void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement source || source.DataContext is not ProjectEdit row)
            return;

        var wrap = new ItemsWrapGrid { Orientation = Orientation.Horizontal, MaximumRowsOrColumns = 4, Width = 188 };
        foreach (var hex in ColorUtil.Palette)
        {
            var color = hex;
            var button = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(3),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Fill = ColorUtil.ToBrush(color)
                }
            };
            button.Click += (_, _) =>
            {
                row.Color = color;
                _swatchFlyout?.Hide();
            };
            wrap.Children.Add(button);
        }

        _swatchFlyout = new Flyout { Content = wrap };
        _swatchFlyout.ShowAt(source);
    }
}