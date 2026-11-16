using System.Windows;
using System.Windows.Controls;

namespace TimeTracker.Views;

public partial class ReasonDialog : Window
{
    public ReasonDialog(bool endingSession = false)
    {
        InitializeComponent();
        if (endingSession)
        {
            Prompt.Text = "This stop was longer than your threshold. Enter a reason to end the session.";
            OkButton.Content = "End session";
        }

        Loaded += (_, _) => ReasonBox.Focus();
    }

    public string ReasonText => ReasonBox.Text.Trim();

    void ReasonBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var ready = !string.IsNullOrWhiteSpace(ReasonBox.Text);
        OkButton.IsEnabled = ready;
        Hint.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
            return;
        DialogResult = true;
    }
}
