using System;
using System.IO;

public static class SimpleLog
{
    private const string LogFileName = "MediaPlayerGlobalHotkeys.log";

    public static string GetDefaultLogPath(string appBasePath)
    {
        return Path.Combine(appBasePath, "logs", LogFileName);
    }

    public static void WriteLine(string message)
    {
        try
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = GetDefaultLogPath(basePath);
            string logDirectory = Path.GetDirectoryName(logPath);

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            File.AppendAllText(
                logPath,
                string.Format("{0:u} {1}{2}", DateTime.UtcNow, message, Environment.NewLine));
        }
        catch
        {
        }
    }
}
