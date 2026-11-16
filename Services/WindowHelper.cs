using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace TimeTracker.Services;

public static class WindowHelper
{
    public static void Configure(Window window, int width, int height, int? minWidth = null, int? minHeight = null)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow is null)
            return;

        appWindow.Resize(new SizeInt32(width, height));
        if (minWidth is not null || minHeight is not null)
        {
            // Enforce minimum size when the user resizes.
            appWindow.Changed += (_, args) =>
            {
                if (!args.DidSizeChange)
                    return;

                var size = appWindow.Size;
                var w = minWidth is null ? size.Width : Math.Max(size.Width, minWidth.Value);
                var h = minHeight is null ? size.Height : Math.Max(size.Height, minHeight.Value);
                if (w != size.Width || h != size.Height)
                    appWindow.Resize(new SizeInt32(w, h));
            };
        }
    }
}