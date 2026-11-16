using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace TimeTracker.Services;

public static class SessionNotifier
{
    public const string Aumid = "SimpleTools.Focus";

    public static void Register()
    {
        try
        {
            _ = SetCurrentProcessExplicitAppUserModelID(Aumid);
        }
        catch
        {
        }

        try
        {
            AppNotificationManager.Default.Register();
        }
        catch
        {
        }
    }

    public static void ShowSessionComplete(string? projectName)
    {
        var title = "Session complete";
        var body = string.IsNullOrWhiteSpace(projectName)
            ? "Focus time is done."
            : "Focus on " + projectName + " is done.";

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .MuteAudio()
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            return;
        }
        catch
        {
        }

        try
        {
            var xml =
                "<toast duration=\"short\">" +
                "<audio silent=\"true\"/>" +
                "<visual><binding template=\"ToastGeneric\">" +
                "<text>" + SecurityElement.Escape(title) + "</text>" +
                "<text>" + SecurityElement.Escape(body) + "</text>" +
                "</binding></visual></toast>";
            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch
        {
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
