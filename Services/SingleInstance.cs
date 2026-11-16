using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace TimeTracker.Services;

public static class SingleInstance
{
    const string MutexName = @"Local\SimpleTools.Focus.SingleInstance";
    const string EventName = @"Local\SimpleTools.Focus.Activate";
    const int SwRestore = 9;

    static Mutex? mutex;
    static EventWaitHandle? activate;
    static Window? bound;

    public static bool Claim()
    {
        mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            mutex.Dispose();
            mutex = null;
            return false;
        }

        activate = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        _ = Task.Run(Listen);
        return true;
    }

    public static void Bind(Window window)
    {
        bound = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(bound, window))
                bound = null;
        };
    }

    public static void ActivateExisting()
    {
        var current = Environment.ProcessId;
        var name = Process.GetCurrentProcess().ProcessName;
        foreach (var process in Process.GetProcessesByName(name))
        {
            if (process.Id == current)
                continue;

            AllowSetForegroundWindow(process.Id);
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
                hwnd = FindVisibleWindow(process.Id);
            if (hwnd != IntPtr.Zero)
                RestoreAndFocus(hwnd);
        }

        try
        {
            using var ev = EventWaitHandle.OpenExisting(EventName);
            ev.Set();
        }
        catch
        {
        }
    }

    static void Listen()
    {
        while (activate is not null)
        {
            if (!activate.WaitOne(TimeSpan.FromMilliseconds(500)))
                continue;

            var window = bound;
            window?.DispatcherQueue.TryEnqueue(() =>
            {
                if (bound is { } current)
                    WindowHelper.BringToFront(current);
            });
        }
    }

    static void RestoreAndFocus(IntPtr hwnd)
    {
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);
    }

    static IntPtr FindVisibleWindow(int processId)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != processId || !IsWindowVisible(hwnd) || GetWindow(hwnd, GwOwner) != IntPtr.Zero)
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    const int GwOwner = 4;

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);
}
