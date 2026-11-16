using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeTracker.Views;

namespace TimeTracker.Services;

public static class DialogService
{
    public static async Task<string?> AskReasonAsync(XamlRoot xamlRoot, bool endingSession)
    {
        var box = new TextBox();
        var hint = new TextBlock
        {
            Text = "A reason is required.",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Margin = new Thickness(0, 8, 0, 0)
        };

        void UpdateHint()
        {
            var ready = !string.IsNullOrWhiteSpace(box.Text);
            hint.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        }

        box.TextChanged += (_, _) => UpdateHint();
        UpdateHint();

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = endingSession
                ? "This stop was longer than your threshold. Enter a reason to end the session."
                : "This stop was longer than your threshold. Enter a reason to resume.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        content.Children.Add(box);
        content.Children.Add(hint);

        var dialog = new ContentDialog
        {
            Title = "Why did you stop?",
            Content = content,
            PrimaryButtonText = endingSession ? "End session" : "Resume",
            CloseButtonText = "Stay paused",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
                args.Cancel = true;
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? box.Text.Trim() : null;
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