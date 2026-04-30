using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

public sealed class HotkeyAppContext : ApplicationContext
{
    private readonly LowLevelKeyboardHotkeyMonitor keyboardMonitor;

    public HotkeyAppContext()
        : this(new MediaPlayerController(SimpleLog.WriteLine), SimpleLog.WriteLine)
    {
    }

    public HotkeyAppContext(IHotkeyActionHandler actionHandler, Action<string> logMessage)
    {
        Action<string> resolvedLogger = logMessage ?? (_ => { });
        IHotkeyActionHandler resolvedHandler = actionHandler ?? new MediaPlayerController(resolvedLogger);
        HotkeyBindingInfo[] bindings = GetDefaultBindings();
        keyboardMonitor = new LowLevelKeyboardHotkeyMonitor(resolvedHandler, resolvedLogger, bindings);
        bool installed = keyboardMonitor.Install();
        HotkeyRegistrationReport report = installed
            ? new HotkeyRegistrationReport(bindings.Length, new string[0])
            : new HotkeyRegistrationReport(bindings.Length, bindings.Select(binding => binding.Name).ToArray());
        string startupSummary = HotkeyStartupStatus.BuildRegistrationSummary(
            report.TotalBindings,
            report.FailedBindingNames.ToArray());

        resolvedLogger(startupSummary);

        if (report.FailedBindingNames.Count > 0)
        {
            Program.ShowStartupMessage(startupSummary, MessageBoxIcon.Warning);
        }
    }

    public static HotkeyBindingInfo[] GetDefaultBindings()
    {
        return new[]
        {
            new HotkeyBindingInfo(1, "TogglePlayPause", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20, 0),
            new HotkeyBindingInfo(2, "SeekBackward", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x25, -5),
            new HotkeyBindingInfo(3, "SeekForward", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x27, 5)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            keyboardMonitor.Dispose();
        }

        base.Dispose(disposing);
    }
}

public interface IHotkeyActionHandler
{
    void TogglePlayPause();
    void SeekBySeconds(int deltaSeconds);
}

public sealed class HotkeyBindingInfo
{
    public HotkeyBindingInfo(int id, string name, HotkeyModifiers modifiers, int virtualKey, int seekDeltaSeconds)
    {
        Id = id;
        Name = name;
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        SeekDeltaSeconds = seekDeltaSeconds;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public HotkeyModifiers Modifiers { get; private set; }

    public int VirtualKey { get; private set; }

    public int SeekDeltaSeconds { get; private set; }
}

public static class HotkeyStartupStatus
{
    public static string BuildRegistrationSummary(int totalBindings, object[] failedBindingNames)
    {
        string[] failures = ConvertFailureNames(failedBindingNames);
        int registeredCount = totalBindings - failures.Length;

        if (registeredCount < 0)
        {
            registeredCount = 0;
        }

        string summary = string.Format("Registered {0} of {1} hotkeys.", registeredCount, totalBindings);
        if (failures.Length == 0)
        {
            return summary;
        }

        return string.Format("{0} Failed: {1}.", summary, string.Join(", ", failures));
    }

    private static string[] ConvertFailureNames(object[] failedBindingNames)
    {
        if (failedBindingNames == null || failedBindingNames.Length == 0)
        {
            return new string[0];
        }

        List<string> failures = new List<string>();
        foreach (object value in failedBindingNames)
        {
            if (value == null)
            {
                continue;
            }

            failures.Add(value.ToString());
        }

        return failures.ToArray();
    }
}

[System.Diagnostics.DebuggerDisplay("Total={TotalBindings}, Failed={FailedBindingNames.Count}")]
public sealed class HotkeyRegistrationReport
{
    public HotkeyRegistrationReport(int totalBindings, IList<string> failedBindingNames)
    {
        TotalBindings = totalBindings;
        FailedBindingNames = new List<string>(failedBindingNames ?? Enumerable.Empty<string>());
    }

    public int TotalBindings { get; private set; }

    public List<string> FailedBindingNames { get; private set; }
}

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}
