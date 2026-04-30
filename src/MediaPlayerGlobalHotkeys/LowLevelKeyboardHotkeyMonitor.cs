using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class LowLevelKeyboardHotkeyMonitor : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private static readonly TimeSpan HeldSeekInitialDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan HeldSeekRepeatInterval = TimeSpan.FromMilliseconds(160);

    private readonly IHotkeyActionHandler actionHandler;
    private readonly Action<string> logMessage;
    private readonly LowLevelHotkeyRecognizer recognizer;
    private readonly HeldSeekRepeatController heldSeekRepeatController;
    private readonly Dictionary<string, HotkeyBindingInfo> bindingsByName;
    private readonly HookProc hookProc;
    private readonly Timer heldSeekRepeatTimer;

    private IntPtr hookHandle;

    public LowLevelKeyboardHotkeyMonitor(
        IHotkeyActionHandler actionHandler,
        Action<string> logMessage,
        IEnumerable<HotkeyBindingInfo> bindings)
    {
        this.actionHandler = actionHandler;
        this.logMessage = logMessage ?? (_ => { });
        recognizer = new LowLevelHotkeyRecognizer(bindings);
        heldSeekRepeatController = new HeldSeekRepeatController(
            bindings,
            HeldSeekInitialDelay.Ticks,
            HeldSeekRepeatInterval.Ticks);
        bindingsByName = bindings.ToDictionary(binding => binding.Name, StringComparer.Ordinal);
        hookProc = HookCallback;
        heldSeekRepeatTimer = new Timer();
        heldSeekRepeatTimer.Interval = 25;
        heldSeekRepeatTimer.Tick += OnHeldSeekRepeatTimerTick;
    }

    public bool Install()
    {
        if (hookHandle != IntPtr.Zero)
        {
            return true;
        }

        IntPtr moduleHandle = GetModuleHandle(null);
        hookHandle = SetWindowsHookEx(WhKeyboardLl, hookProc, moduleHandle, 0);
        bool installed = hookHandle != IntPtr.Zero;
        logMessage(installed
            ? "Installed low-level keyboard hook."
            : "Failed to install low-level keyboard hook.");
        return installed;
    }

    public void Dispose()
    {
        heldSeekRepeatTimer.Stop();
        heldSeekRepeatTimer.Dispose();

        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(hookHandle);
        hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            KbdLlHookStruct keyboardData = (KbdLlHookStruct)Marshal.PtrToStructure(lParam, typeof(KbdLlHookStruct));
            int virtualKey = unchecked((int)keyboardData.vkCode);

            if (message == WmKeyDown || message == WmSysKeyDown)
            {
                bool controlPressed = IsKeyDown(VkControl);
                bool altPressed = IsKeyDown(VkMenu);
                string actionName = recognizer.ProcessKeyDown(virtualKey, controlPressed, altPressed);
                if (!string.IsNullOrEmpty(actionName))
                {
                    DispatchAction(actionName);
                    StartHeldSeekRepeatIfNeeded(actionName, virtualKey);
                }
            }
            else if (message == WmKeyUp || message == WmSysKeyUp)
            {
                recognizer.ProcessKeyUp(virtualKey);
                StopHeldSeekRepeatIfNeeded(virtualKey);
            }
        }

        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private void DispatchAction(string actionName)
    {
        HotkeyBindingInfo binding;
        if (!bindingsByName.TryGetValue(actionName, out binding))
        {
            logMessage("Low-level hook produced unknown action: " + actionName);
            return;
        }

        logMessage("Hotkey pressed: " + binding.Name);
        if (binding.Name == "TogglePlayPause")
        {
            actionHandler.TogglePlayPause();
        }
        else
        {
            actionHandler.SeekBySeconds(binding.SeekDeltaSeconds);
        }
    }

    private void StartHeldSeekRepeatIfNeeded(string actionName, int virtualKey)
    {
        if (heldSeekRepeatController.StartHold(actionName, virtualKey, DateTime.UtcNow.Ticks))
        {
            heldSeekRepeatTimer.Start();
        }
    }

    private void StopHeldSeekRepeatIfNeeded(int virtualKey)
    {
        heldSeekRepeatController.StopHold(virtualKey);
        if (!heldSeekRepeatController.IsActive)
        {
            heldSeekRepeatTimer.Stop();
        }
    }

    private void OnHeldSeekRepeatTimerTick(object sender, EventArgs e)
    {
        string actionName = heldSeekRepeatController.ConsumeDueAction(
            IsKeyDown(VkControl),
            IsKeyDown(VkMenu),
            DateTime.UtcNow.Ticks);

        if (string.IsNullOrEmpty(actionName))
        {
            if (!heldSeekRepeatController.IsActive)
            {
                heldSeekRepeatTimer.Stop();
            }

            return;
        }

        logMessage("Hotkey repeat: " + actionName);
        DispatchAction(actionName);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}

public sealed class HeldSeekRepeatController
{
    private readonly Dictionary<string, HotkeyBindingInfo> seekBindingsByName;
    private readonly long initialDelayTicks;
    private readonly long repeatIntervalTicks;

    private string activeActionName;
    private int activeVirtualKey;
    private long nextRepeatTicks;

    public HeldSeekRepeatController(
        IEnumerable<HotkeyBindingInfo> bindings,
        long initialDelayTicks,
        long repeatIntervalTicks)
    {
        seekBindingsByName = (bindings ?? Enumerable.Empty<HotkeyBindingInfo>())
            .Where(binding => binding.SeekDeltaSeconds != 0)
            .ToDictionary(binding => binding.Name, StringComparer.Ordinal);
        this.initialDelayTicks = initialDelayTicks < 0 ? 0 : initialDelayTicks;
        this.repeatIntervalTicks = repeatIntervalTicks <= 0 ? TimeSpan.FromMilliseconds(160).Ticks : repeatIntervalTicks;
    }

    public bool IsActive
    {
        get { return activeActionName != null; }
    }

    public bool StartHold(string actionName, int virtualKey, long nowTicks)
    {
        if (string.IsNullOrEmpty(actionName) || !seekBindingsByName.ContainsKey(actionName))
        {
            return false;
        }

        activeActionName = actionName;
        activeVirtualKey = virtualKey;
        nextRepeatTicks = nowTicks + initialDelayTicks;
        return true;
    }

    public void StopHold(int virtualKey)
    {
        if (activeActionName == null || activeVirtualKey != virtualKey)
        {
            return;
        }

        Clear();
    }

    public string ConsumeDueAction(bool controlPressed, bool altPressed, long nowTicks)
    {
        if (activeActionName == null)
        {
            return null;
        }

        if (!controlPressed || !altPressed)
        {
            Clear();
            return null;
        }

        if (nowTicks < nextRepeatTicks)
        {
            return null;
        }

        string actionName = activeActionName;
        nextRepeatTicks = nowTicks + repeatIntervalTicks;
        return actionName;
    }

    private void Clear()
    {
        activeActionName = null;
        activeVirtualKey = 0;
        nextRepeatTicks = 0;
    }
}

public sealed class LowLevelHotkeyRecognizer
{
    private readonly Dictionary<int, string> actionByVirtualKey;
    private readonly HashSet<int> activeKeys = new HashSet<int>();

    public LowLevelHotkeyRecognizer()
        : this(new[]
        {
            new HotkeyBindingInfo(1, "TogglePlayPause", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20, 0),
            new HotkeyBindingInfo(2, "SeekBackward", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x25, -5),
            new HotkeyBindingInfo(3, "SeekForward", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x27, 5)
        })
    {
    }

    public LowLevelHotkeyRecognizer(IEnumerable<HotkeyBindingInfo> bindings)
    {
        actionByVirtualKey = bindings.ToDictionary(binding => binding.VirtualKey, binding => binding.Name);
    }

    public string ProcessKeyDown(int virtualKey, bool controlPressed, bool altPressed)
    {
        if (activeKeys.Contains(virtualKey))
        {
            return null;
        }

        activeKeys.Add(virtualKey);

        if (!controlPressed || !altPressed)
        {
            return null;
        }

        string actionName;
        return actionByVirtualKey.TryGetValue(virtualKey, out actionName)
            ? actionName
            : null;
    }

    public void ProcessKeyUp(int virtualKey)
    {
        activeKeys.Remove(virtualKey);
    }
}
