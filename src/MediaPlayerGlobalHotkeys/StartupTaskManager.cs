using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

public static class StartupTaskManager
{
    private const string InstallArgument = "--install-startup-task";
    private const string UninstallArgument = "--uninstall-startup-task";
    private const string InstallCommandName = "InstallStartupTask";
    private const string UninstallCommandName = "UninstallStartupTask";
    private const string TaskName = "MediaPlayerGlobalHotkeys";
    private const string TaskAuthor = "MediaPlayerGlobalHotkeys";
    private const string TaskDescription = "Starts MediaPlayerGlobalHotkeys at user logon.";
    private const string SchtasksExecutable = "schtasks.exe";

    public static string GetTaskName()
    {
        return TaskName;
    }

    public static string ParseCommandName(string[] args)
    {
        string[] safeArgs = args ?? new string[0];
        foreach (string argument in safeArgs)
        {
            string commandName = ParseCommandText(argument);
            if (!string.IsNullOrEmpty(commandName))
            {
                return commandName;
            }
        }

        return null;
    }

    public static string ParseCommandText(string argument)
    {
        if (string.Equals(argument, InstallArgument, StringComparison.OrdinalIgnoreCase))
        {
            return InstallCommandName;
        }

        if (string.Equals(argument, UninstallArgument, StringComparison.OrdinalIgnoreCase))
        {
            return UninstallCommandName;
        }

        return null;
    }

    public static bool TryHandleCommand(string[] args, Action<string> logMessage)
    {
        Action<string> logger = logMessage ?? (_ => { });
        string commandName = ParseCommandName(args);
        if (string.IsNullOrEmpty(commandName))
        {
            return false;
        }

        try
        {
            if (string.Equals(commandName, InstallCommandName, StringComparison.Ordinal))
            {
                InstallForCurrentUser(logger);
                Program.ShowStartupMessage(
                    "Startup at logon is now enabled through Task Scheduler.",
                    MessageBoxIcon.Information);
            }
            else
            {
                bool removed = UninstallForCurrentUser(logger);
                Program.ShowStartupMessage(
                    removed
                        ? "Startup at logon has been removed from Task Scheduler."
                        : "No startup task was installed.",
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            logger("Failed to update startup task. " + ex);
            Program.ShowStartupMessage(
                "Failed to update startup at logon." + Environment.NewLine + Environment.NewLine + ex.Message,
                MessageBoxIcon.Error);
        }

        return true;
    }

    public static string BuildTaskXml(string exePath, string userId, string author, string startBoundaryText)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("Executable path is required.", "exePath");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", "userId");
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException("Author is required.", "author");
        }

        if (string.IsNullOrWhiteSpace(startBoundaryText))
        {
            throw new ArgumentException("Start boundary is required.", "startBoundaryText");
        }

        string workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
        builder.AppendLine("<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
        builder.AppendLine("  <RegistrationInfo>");
        builder.AppendFormat("    <Author>{0}</Author>{1}", EscapeXml(author), Environment.NewLine);
        builder.AppendFormat("    <Description>{0}</Description>{1}", EscapeXml(TaskDescription), Environment.NewLine);
        builder.AppendLine("  </RegistrationInfo>");
        builder.AppendLine("  <Triggers>");
        builder.AppendLine("    <LogonTrigger>");
        builder.AppendFormat("      <StartBoundary>{0}</StartBoundary>{1}", EscapeXml(startBoundaryText), Environment.NewLine);
        builder.AppendLine("      <Enabled>true</Enabled>");
        builder.AppendFormat("      <UserId>{0}</UserId>{1}", EscapeXml(userId), Environment.NewLine);
        builder.AppendLine("    </LogonTrigger>");
        builder.AppendLine("  </Triggers>");
        builder.AppendLine("  <Principals>");
        builder.AppendLine("    <Principal id=\"Author\">");
        builder.AppendFormat("      <UserId>{0}</UserId>{1}", EscapeXml(userId), Environment.NewLine);
        builder.AppendLine("      <LogonType>InteractiveToken</LogonType>");
        builder.AppendLine("      <RunLevel>LeastPrivilege</RunLevel>");
        builder.AppendLine("    </Principal>");
        builder.AppendLine("  </Principals>");
        builder.AppendLine("  <Settings>");
        builder.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
        builder.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
        builder.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
        builder.AppendLine("    <AllowHardTerminate>true</AllowHardTerminate>");
        builder.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
        builder.AppendLine("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>");
        builder.AppendLine("    <AllowStartOnDemand>true</AllowStartOnDemand>");
        builder.AppendLine("    <Enabled>true</Enabled>");
        builder.AppendLine("    <Hidden>false</Hidden>");
        builder.AppendLine("  </Settings>");
        builder.AppendLine("  <Actions Context=\"Author\">");
        builder.AppendLine("    <Exec>");
        builder.AppendFormat("      <Command>{0}</Command>{1}", EscapeXml(exePath), Environment.NewLine);
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            builder.AppendFormat("      <WorkingDirectory>{0}</WorkingDirectory>{1}", EscapeXml(workingDirectory), Environment.NewLine);
        }

        builder.AppendLine("    </Exec>");
        builder.AppendLine("  </Actions>");
        builder.AppendLine("</Task>");
        return builder.ToString();
    }

    private static void InstallForCurrentUser(Action<string> logMessage)
    {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        string userId = WindowsIdentity.GetCurrent().Name;
        string taskXml = BuildTaskXml(exePath, userId, TaskAuthor, GetCurrentStartBoundaryText());
        string tempFilePath = Path.Combine(
            Path.GetTempPath(),
            string.Format("{0}.{1}.xml", TaskName, Guid.NewGuid().ToString("N")));

        File.WriteAllText(tempFilePath, taskXml, Encoding.Unicode);

        try
        {
            RunSchtasks(
                string.Format("/create /f /tn {0} /xml {1}", QuoteArgument(TaskName), QuoteArgument(tempFilePath)),
                logMessage);
            logMessage("Installed startup task '" + TaskName + "'.");
        }
        finally
        {
            TryDeleteFile(tempFilePath);
        }
    }

    private static bool UninstallForCurrentUser(Action<string> logMessage)
    {
        if (!TaskExists())
        {
            logMessage("Startup task '" + TaskName + "' was not installed.");
            return false;
        }

        RunSchtasks(
            string.Format("/delete /f /tn {0}", QuoteArgument(TaskName)),
            logMessage);
        logMessage("Removed startup task '" + TaskName + "'.");
        return true;
    }

    private static bool TaskExists()
    {
        ProcessResult result = RunProcess(
            SchtasksExecutable,
            string.Format("/query /tn {0}", QuoteArgument(TaskName)));
        return result.ExitCode == 0;
    }

    private static void RunSchtasks(string arguments, Action<string> logMessage)
    {
        ProcessResult result = RunProcess(SchtasksExecutable, arguments);
        Action<string> logger = logMessage ?? (_ => { });
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            logger("schtasks: " + result.Output.Trim());
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "schtasks.exe exited with code " + result.ExitCode + "." +
                (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : " Output: " + result.Output.Trim()));
        }
    }

    private static ProcessResult RunProcess(string fileName, string arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = fileName;
        startInfo.Arguments = arguments;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            string combinedOutput = string.IsNullOrWhiteSpace(error)
                ? output
                : (output + Environment.NewLine + error).Trim();

            return new ProcessResult(process.ExitCode, combinedOutput);
        }
    }

    private static string GetCurrentStartBoundaryText()
    {
        return DateTime.Now.ToString("s");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
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

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }

        public int ExitCode { get; private set; }

        public string Output { get; private set; }
    }
}
