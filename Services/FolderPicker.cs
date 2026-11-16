using System.IO;
using System.Windows.Forms;

namespace TimeTracker.Services;

public static class FolderPicker
{
    public static string? Pick(string title, string? initialDirectory, IntPtr owner)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            AutoUpgradeEnabled = true
        };

        var start = ExistingDirectory(initialDirectory);
        if (start is not null)
        {
            dialog.InitialDirectory = start;
            dialog.SelectedPath = start;
        }

        var result = owner == IntPtr.Zero
            ? dialog.ShowDialog()
            : dialog.ShowDialog(new OwnerWindow(owner));

        return result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath)
            ? dialog.SelectedPath
            : null;
    }

    static string? ExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var current = path.Trim();
        while (current.Length > 0 && !Directory.Exists(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
                return null;
            current = parent;
        }

        return current.Length == 0 ? null : current;
    }

    sealed class OwnerWindow(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
