using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TimeTracker.Services;
using TimeTracker.ViewModels;

namespace TimeTracker.Views;

public partial class SettingsWindow : Window
{
    readonly SettingsViewModel _viewModel;

    public SettingsWindow(TimeStore store, bool firstRun, string? activeProjectId)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(store, firstRun, activeProjectId);
        DataContext = _viewModel;
        if (firstRun)
            Title = "Choose where to save";
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TrySave())
            DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var owner = new WindowInteropHelper(this).Handle;
        var picked = FolderPicker.Pick("Choose where Time Tracker saves history", _viewModel.DataFolder, owner);
        if (!string.IsNullOrWhiteSpace(picked))
            _viewModel.DataFolder = picked;
    }

    void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement source || source.DataContext is not ProjectEdit row)
            return;

        var menu = new ContextMenu { PlacementTarget = source };
        var panel = new WrapPanel { Width = 188 };
        foreach (var hex in ColorUtil.Palette)
        {
            var color = hex;
            var button = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(3),
                Background = Brushes.Transparent,
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
                menu.IsOpen = false;
            };
            panel.Children.Add(button);
        }

        menu.Items.Add(new MenuItem { Header = panel, StaysOpenOnClick = true });
        menu.IsOpen = true;
    }
}
