using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TimeTracker.Services;

namespace TimeTracker;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (!SingleInstance.Claim())
        {
            SingleInstance.ActivateExisting();
            return;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
