using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using TimeTracker.Models;
using TimeTracker.Views;

namespace TimeTracker.Services;

public static class DialogService
{
    const string ReasonItemTemplateXaml =
        """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Grid ColumnSpacing="8">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <Ellipse Width="12" Height="12" Fill="{Binding Swatch}" Visibility="{Binding SwatchVisibility}" VerticalAlignment="Center"/>
            <TextBlock Grid.Column="1" Text="{Binding Label}" VerticalAlignment="Center"/>
            <Button Grid.Column="2" Command="{Binding DeleteCommand}"
                    Visibility="{Binding DeleteVisibility}"
                    Background="Transparent" BorderThickness="0" Padding="6,4"
                    MinWidth="32" MinHeight="32" AllowFocusOnInteraction="True"
                    ToolTipService.ToolTip="Remove from list"
                    AutomationProperties.Name="Remove from list">
              <FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE74D;" FontSize="12"/>
            </Button>
          </Grid>
        </DataTemplate>
        """;

    public static async Task<StopReasonChoice?> AskReasonAsync(
        XamlRoot xamlRoot,
        bool endingSession,
        DateTimeOffset stoppedAt,
        long stoppedSeconds,
        IReadOnlyList<string>? savedReasons = null,
        IReadOnlyList<Project>? projects = null,
        bool startNewSession = false,
        Action<string>? forgetReason = null)
    {
        var items = new ObservableCollection<ReasonListItem>();
        var box = new ComboBox
        {
            IsEditable = true,
            PlaceholderText = "Reason",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinWidth = 320,
            ItemTemplate = (DataTemplate)XamlReader.Load(ReasonItemTemplateXaml),
            ItemsSource = items
        };
        box.ItemContainerStyle = ComboItemStretchStyle();

        foreach (var project in projects ?? [])
            items.Add(ReasonListItem.FromProject(project));
        foreach (var reason in savedReasons ?? [])
        {
            ReasonListItem? item = null;
            item = ReasonListItem.FromText(reason, forgetReason is null ? null : () =>
            {
                forgetReason(reason);
                items.Remove(item!);
                if (ReferenceEquals(box.SelectedItem, item))
                    box.SelectedItem = null;
            });
            items.Add(item);
        }

        var hint = new TextBlock
        {
            Text = "A reason is required.",
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Margin = new Thickness(0, 8, 0, 0)
        };

        StopReasonChoice? Chosen()
        {
            var typed = box.Text?.Trim() ?? "";
            if (box.SelectedItem is ReasonListItem { Project: { } project }
                && (typed.Length == 0 || string.Equals(typed, project.Name, StringComparison.OrdinalIgnoreCase)))
                return StopReasonChoice.ForProject(project);
            return typed.Length == 0 ? null : StopReasonChoice.ForText(typed);
        }

        void UpdateHint()
            => hint.Visibility = Chosen() is null ? Visibility.Visible : Visibility.Collapsed;

        box.SelectionChanged += (_, _) => UpdateHint();
        box.Loaded += (_, _) =>
        {
            if (FindDescendant<TextBox>(box) is { } inner)
                inner.TextChanged += (_, _) => UpdateHint();
        };
        UpdateHint();

        var clock = stoppedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        var duration = TimeMath.FormatShort(stoppedSeconds);
        var times = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 20)
        };
        times.Children.Add(new TextBlock
        {
            Text = duration,
            FontSize = 48,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        times.Children.Add(new TextBlock
        {
            Text = "Stopped at " + clock,
            FontSize = 13,
            Opacity = 0.65,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });

        var content = new StackPanel();
        content.Children.Add(times);
        content.Children.Add(box);
        content.Children.Add(hint);

        var dialog = new ContentDialog
        {
            Title = "Why did you stop?",
            Content = content,
            PrimaryButtonText = endingSession ? "End session" : startNewSession ? "Start new session" : "Resume",
            CloseButtonText = "Stay paused",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (Chosen() is null)
                args.Cancel = true;
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? Chosen() : null;
    }

    sealed class ReasonListItem
    {
        public string Label { get; init; } = "";
        public Project? Project { get; init; }
        public Brush Swatch { get; init; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        public Visibility SwatchVisibility { get; init; } = Visibility.Collapsed;
        public ICommand? DeleteCommand { get; init; }
        public Visibility DeleteVisibility { get; init; } = Visibility.Collapsed;

        public override string ToString() => Label;

        public static ReasonListItem FromProject(Project project)
            => new()
            {
                Label = project.Name,
                Project = project,
                Swatch = ColorUtil.ToBrush(project.Color),
                SwatchVisibility = Visibility.Visible
            };

        public static ReasonListItem FromText(string reason, Action? onDelete = null)
            => new()
            {
                Label = reason,
                DeleteCommand = onDelete is null ? null : new ActionCommand(onDelete),
                DeleteVisibility = onDelete is null ? Visibility.Collapsed : Visibility.Visible
            };
    }

    sealed class ActionCommand(Action execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    static Style ComboItemStretchStyle()
    {
        var style = new Style(typeof(ComboBoxItem))
        {
            BasedOn = Application.Current.Resources["DefaultComboBoxItemStyle"] as Style
        };
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        return style;
    }

    static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            if (FindDescendant<T>(child) is { } nested)
                return nested;
        }

        return null;
    }

    public static async Task<ProjectChangeChoice?> AskProjectChangeAsync(XamlRoot xamlRoot, string projectName)
    {
        var dialog = new ContentDialog
        {
            Title = "Change project",
            Content = new TextBlock
            {
                Text = $"Bring this session's elapsed time to {projectName}, or start {projectName} from zero? Time already saved stays on the project that earned it when you start over.",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Bring elapsed time",
            SecondaryButtonText = "Start from zero",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => ProjectChangeChoice.MoveElapsed,
            ContentDialogResult.Secondary => ProjectChangeChoice.StartFromZero,
            _ => null
        };
    }

    public static async Task ShowErrorAsync(XamlRoot? xamlRoot, string message)
    {
        if (xamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            Title = "Focus",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync();
    }
}