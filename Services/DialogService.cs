using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TimeTracker.Views;

namespace TimeTracker.Services;

public static class DialogService
{
    public static async Task<string?> AskReasonAsync(
        XamlRoot xamlRoot,
        bool endingSession,
        DateTimeOffset stoppedAt,
        long stoppedSeconds,
        IReadOnlyList<string>? savedReasons = null,
        bool startNewSession = false)
    {
        var box = new ComboBox
        {
            IsEditable = true,
            PlaceholderText = "Reason",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 320
        };
        foreach (var reason in savedReasons ?? [])
            box.Items.Add(reason);

        var hint = new TextBlock
        {
            Text = "A reason is required.",
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Margin = new Thickness(0, 8, 0, 0)
        };

        string Chosen()
        {
            var typed = box.Text?.Trim() ?? "";
            if (typed.Length > 0)
                return typed;
            return (box.SelectedItem as string)?.Trim() ?? "";
        }

        void UpdateHint()
            => hint.Visibility = Chosen().Length > 0 ? Visibility.Collapsed : Visibility.Visible;

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
            if (Chosen().Length == 0)
                args.Cancel = true;
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? Chosen() : null;
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