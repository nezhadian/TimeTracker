using System.Windows;

namespace TimeTracker.Views;

public enum ProjectChangeChoice
{
    MoveElapsed,
    StartFromZero
}

public partial class ProjectChangeDialog : Window
{
    public ProjectChangeDialog(string projectName)
    {
        InitializeComponent();
        Prompt.Text = $"Bring this session's elapsed time to {projectName}, or start {projectName} from zero? Time already saved stays on the project that earned it when you start over.";
    }

    public ProjectChangeChoice? Choice { get; private set; }

    void Move_Click(object sender, RoutedEventArgs e)
    {
        Choice = ProjectChangeChoice.MoveElapsed;
        DialogResult = true;
    }

    void Zero_Click(object sender, RoutedEventArgs e)
    {
        Choice = ProjectChangeChoice.StartFromZero;
        DialogResult = true;
    }
}
