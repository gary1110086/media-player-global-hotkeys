using System;
using System.Linq;
using System.Windows.Forms;

public static class Program
{
    public static string GetAlreadyRunningMessage()
    {
        return "MediaPlayerGlobalHotkeys is already running.";
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (StartupTaskManager.TryHandleCommand(args, SimpleLog.WriteLine))
        {
            return;
        }

        if (DesktopSessionBridge.TryRelaunchOnInteractiveDesktop(args, SimpleLog.WriteLine))
        {
            return;
        }

        using (SingleInstanceGate instanceGate = SingleInstanceGate.TryAcquireDefault())
        {
            if (instanceGate == null)
            {
                ShowStartupMessage(GetAlreadyRunningMessage(), MessageBoxIcon.Information);
                return;
            }

            Application.Run(new HotkeyAppContext());
        }
    }

    public static void ShowStartupMessage(string message, MessageBoxIcon icon)
    {
        try
        {
            MessageBox.Show(
                message,
                "Media Player Global Hotkeys",
                MessageBoxButtons.OK,
                icon);
        }
        catch
        {
            SimpleLog.WriteLine("Failed to show startup message: " + message);
        }
    }
}
