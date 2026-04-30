using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class DesktopSessionBridge
{
    private const string SkipArgument = "--skip-desktop-relaunch";
    private const string InteractiveDesktopName = "Default";
    private const string InteractiveDesktopPath = @"winsta0\default";
    private const int UoiName = 2;

    public static bool ShouldRelaunchOnDefaultDesktop(string desktopName)
    {
        return !string.IsNullOrWhiteSpace(desktopName)
            && !string.Equals(desktopName, InteractiveDesktopName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryRelaunchOnInteractiveDesktop(string[] args, Action<string> logMessage)
    {
        Action<string> logger = logMessage ?? (_ => { });
        string currentDesktop = GetCurrentDesktopName();
        logger("Current desktop: " + currentDesktop);

        string[] safeArgs = args ?? new string[0];
        if (safeArgs.Any(argument => string.Equals(argument, SkipArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!ShouldRelaunchOnDefaultDesktop(currentDesktop))
        {
            return false;
        }

        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        string[] relaunchedArgs = safeArgs.Concat(new[] { SkipArgument }).ToArray();
        string commandLine = BuildCommandLine(exePath, relaunchedArgs);

        StartupInfo startupInfo = new StartupInfo();
        startupInfo.cb = Marshal.SizeOf(typeof(StartupInfo));
        startupInfo.lpDesktop = InteractiveDesktopPath;

        ProcessInformation processInformation;
        bool started = CreateProcess(
            exePath,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            0,
            IntPtr.Zero,
            AppDomain.CurrentDomain.BaseDirectory,
            ref startupInfo,
            out processInformation);

        if (!started)
        {
            logger("Failed to relaunch on interactive desktop. Win32Error=" + Marshal.GetLastWin32Error());
            return false;
        }

        CloseHandle(processInformation.hProcess);
        CloseHandle(processInformation.hThread);
        logger("Relaunched helper on interactive desktop.");
        return true;
    }

    public static string GetCurrentDesktopName()
    {
        IntPtr desktopHandle = GetThreadDesktop(GetCurrentThreadId());
        if (desktopHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        int needed = 0;
        StringBuilder builder = new StringBuilder(256);
        bool ok = GetUserObjectInformation(desktopHandle, UoiName, builder, builder.Capacity, ref needed);
        return ok ? builder.ToString() : string.Empty;
    }

    private static string BuildCommandLine(string exePath, string[] args)
    {
        string[] commandParts = new[] { QuoteArgument(exePath) }
            .Concat(args.Select(QuoteArgument))
            .ToArray();

        return string.Join(" ", commandParts);
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Contains(" ") && !value.Contains("\""))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref StartupInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(int threadId);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(
        IntPtr hObj,
        int nIndex,
        StringBuilder pvInfo,
        int nLength,
        ref int lpnLengthNeeded);
}
