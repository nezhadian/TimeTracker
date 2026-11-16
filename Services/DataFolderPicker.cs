using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TimeTracker.Services;

public static class DataFolderPicker
{
    public static async Task<string?> PickAsync(string title, string? initialDirectory, IntPtr owner)
    {
        var picker = new FolderPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };
        picker.FileTypeFilter.Add("*");

        if (owner != IntPtr.Zero)
            InitializeWithWindow.Initialize(picker, owner);

        _ = title;
        _ = ExistingDirectory(initialDirectory);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
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
}