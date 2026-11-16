using System.Runtime.InteropServices;
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

    public static void BringToFront(Window window)
    {
        window.Activate();
        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
            return;

        if (IsIconic(hwnd))
            ShowWindow(hwnd, SwRestore);

        var foreground = GetForegroundWindow();
        var thisThread = GetCurrentThreadId();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        if (foregroundThread != thisThread)
            AttachThreadInput(foregroundThread, thisThread, true);

        ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);

        if (foregroundThread != thisThread)
            AttachThreadInput(foregroundThread, thisThread, false);
    }

    const int SwRestore = 9;

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}