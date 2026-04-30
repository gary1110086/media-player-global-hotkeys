using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public sealed class MediaPlayerController : IHotkeyActionHandler
{
    private const string SessionManagerRuntimeName = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows, ContentType=WindowsRuntime";
    private const string TargetAppIdPrefix = "Microsoft.ZuneMusic";
    private static readonly TimeSpan RecentSeekStateWindow = TimeSpan.FromMilliseconds(1000);

    private readonly Action<string> logMessage;
    private readonly object seekStateSync = new object();

    private string lastSeekSessionKey;
    private long lastRequestedSeekTargetTicks;
    private DateTime lastSeekUtc;
    private bool hasRecentSeekState;

    public MediaPlayerController(Action<string> logMessage)
    {
        this.logMessage = logMessage ?? (_ => { });
        lastSeekUtc = DateTime.MinValue;
    }

    public static string GetTargetAppIdPrefix()
    {
        return TargetAppIdPrefix;
    }

    public static string GetSessionManagerTypeName()
    {
        Type sessionManagerType = ResolveSessionManagerType();
        return sessionManagerType == null ? string.Empty : sessionManagerType.FullName;
    }

    public void TogglePlayPause()
    {
        try
        {
            logMessage("TogglePlayPause requested.");
            ResetRecentSeekState("toggle play/pause requested.");
            object session = FindMatchingSession("TogglePlayPause");
            if (session == null)
            {
                return;
            }

            logMessage("TogglePlayPause matched session: " + GetSourceAppId(session));
            bool handled = InvokeAsyncBoolean(session, "TryTogglePlayPauseAsync");
            logMessage("TogglePlayPause result: " + handled);
            if (!handled)
            {
                logMessage("TogglePlayPause was rejected by the media session.");
            }
        }
        catch (Exception ex)
        {
            logMessage("TogglePlayPause failed: " + ex.Message);
        }
    }

    public void SeekBySeconds(int deltaSeconds)
    {
        if (deltaSeconds == 0)
        {
            return;
        }

        try
        {
            logMessage("SeekBySeconds requested: deltaSeconds=" + deltaSeconds);
            object session = FindMatchingSession("SeekBySeconds");
            if (session == null)
            {
                return;
            }

            logMessage("SeekBySeconds matched session: " + GetSourceAppId(session));
            object timeline = session.GetType().GetMethod("GetTimelineProperties").Invoke(session, null);
            if (timeline == null)
            {
                logMessage("SeekBySeconds skipped because timeline properties were unavailable.");
                return;
            }

            TimeSpan minimum = GetTimeSpanProperty(timeline, "MinSeekTime");
            TimeSpan maximum = GetTimeSpanProperty(timeline, "MaxSeekTime");

            if (maximum <= minimum)
            {
                minimum = GetTimeSpanProperty(timeline, "StartTime");
                maximum = GetTimeSpanProperty(timeline, "EndTime");
            }

            if (maximum <= minimum)
            {
                logMessage("SeekBySeconds skipped because the current media item is not seekable.");
                return;
            }

            TimeSpan position = GetTimeSpanProperty(timeline, "Position");
            string sessionKey = GetSessionKey(session);
            long deltaTicks = TimeSpan.FromSeconds(deltaSeconds).Ticks;
            string previousSessionKey;
            long lastRequestedTargetTicks;
            bool hasLastRequestedTarget;
            bool isSameSession;
            long elapsedSinceLastSeekTicks;

            ReadRecentSeekState(
                sessionKey,
                out previousSessionKey,
                out lastRequestedTargetTicks,
                out hasLastRequestedTarget,
                out isSameSession,
                out elapsedSinceLastSeekTicks);

            if (hasLastRequestedTarget && !isSameSession)
            {
                ResetRecentSeekState(string.Format(
                    "session changed from {0} to {1}.",
                    FormatValue(previousSessionKey),
                    FormatValue(sessionKey)));
            }

            if (hasLastRequestedTarget && isSameSession && elapsedSinceLastSeekTicks > RecentSeekStateWindow.Ticks)
            {
                ResetRecentSeekState("recent seek state expired.");
            }

            long baseTicks = MediaTimelineMath.ResolveSeekBaseTicks(
                position.Ticks,
                minimum.Ticks,
                maximum.Ticks,
                deltaTicks,
                lastRequestedTargetTicks,
                hasLastRequestedTarget,
                isSameSession,
                elapsedSinceLastSeekTicks,
                RecentSeekStateWindow.Ticks);

            long targetTicks = MediaTimelineMath.ClampSeekTargetTicks(
                baseTicks,
                minimum.Ticks,
                maximum.Ticks,
                deltaTicks);

            logMessage(string.Format(
                "SeekBySeconds decision: reported={0}, lastRequested={1}, base={2}, finalTarget={3}, min={4}, max={5}, sameSession={6}, elapsedMs={7}",
                FormatTicks(position.Ticks),
                FormatOptionalTicks(hasLastRequestedTarget, lastRequestedTargetTicks),
                FormatTicks(baseTicks),
                FormatTicks(targetTicks),
                minimum,
                maximum,
                isSameSession,
                FormatElapsedMilliseconds(hasLastRequestedTarget && isSameSession, elapsedSinceLastSeekTicks)));

            bool handled = InvokeAsyncBoolean(session, "TryChangePlaybackPositionAsync", targetTicks);
            logMessage("SeekBySeconds result: " + handled);
            if (!handled)
            {
                logMessage("SeekBySeconds was rejected by the media session.");
                ResetRecentSeekState("seek request was rejected.");
                return;
            }

            RememberRecentSeekState(sessionKey, targetTicks);
        }
        catch (Exception ex)
        {
            logMessage("SeekBySeconds failed: " + ex.Message);
        }
    }

    private object FindMatchingSession(string operationName)
    {
        object sessionManager = RequestSessionManager(operationName);
        if (sessionManager == null)
        {
            return null;
        }

        Type managerType = sessionManager.GetType();
        object currentSession = managerType.GetMethod("GetCurrentSession").Invoke(sessionManager, null);
        string currentAppId = GetSourceAppId(currentSession);
        logMessage(string.Format("{0} current session: {1}", operationName, FormatValue(currentAppId)));
        if (IsTargetSession(currentSession))
        {
            return currentSession;
        }

        IEnumerable sessions = managerType.GetMethod("GetSessions").Invoke(sessionManager, null) as IEnumerable;
        if (sessions == null)
        {
            logMessage(operationName + " saw no session list.");
            return null;
        }

        object[] sessionArray = sessions.Cast<object>().ToArray();
        logMessage(string.Format(
            "{0} visible sessions: {1}",
            operationName,
            DescribeSessions(sessionArray)));

        foreach (object session in sessionArray)
        {
            if (IsTargetSession(session))
            {
                return session;
            }
        }

        logMessage(operationName + " found no matching media player session.");
        return null;
    }

    private static bool IsTargetSession(object session)
    {
        if (session == null)
        {
            return false;
        }

        PropertyInfo property = session.GetType().GetProperty("SourceAppUserModelId");
        if (property == null)
        {
            return false;
        }

        string appId = property.GetValue(session, null) as string;
        return MediaPlayerTarget.IsTarget(appId);
    }

    private object RequestSessionManager(string operationName)
    {
        Type sessionManagerType = ResolveSessionManagerType();
        if (sessionManagerType == null)
        {
            logMessage(operationName + " could not resolve the Windows media session manager type.");
            return null;
        }

        logMessage(operationName + " resolved session manager type: " + sessionManagerType.FullName);
        object asyncOperation = sessionManagerType.GetMethod("RequestAsync").Invoke(null, null);
        object manager = AwaitAsyncOperation(asyncOperation, sessionManagerType);
        if (manager == null)
        {
            logMessage(operationName + " received a null session manager.");
        }

        return manager;
    }

    private static Type ResolveSessionManagerType()
    {
        return Type.GetType(SessionManagerRuntimeName);
    }

    private static TimeSpan GetTimeSpanProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName);
        if (property == null)
        {
            return TimeSpan.Zero;
        }

        object value = property.GetValue(instance, null);
        return value is TimeSpan ? (TimeSpan)value : TimeSpan.Zero;
    }

    private static bool InvokeAsyncBoolean(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName);
        if (method == null)
        {
            return false;
        }

        object asyncOperation = method.Invoke(target, arguments);
        object result = AwaitAsyncOperation(asyncOperation, typeof(bool));
        return result is bool && (bool)result;
    }

    private static object AwaitAsyncOperation(object asyncOperation, Type resultType)
    {
        if (asyncOperation == null)
        {
            return null;
        }

        MethodInfo asTaskMethod = typeof(System.WindowsRuntimeSystemExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(method =>
                method.Name == "AsTask" &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 1);

        MethodInfo closedMethod = asTaskMethod.MakeGenericMethod(resultType);
        object taskObject = closedMethod.Invoke(null, new[] { asyncOperation });
        Task task = taskObject as Task;

        if (task == null)
        {
            return null;
        }

        task.GetAwaiter().GetResult();
        return taskObject.GetType().GetProperty("Result").GetValue(taskObject, null);
    }

    private static string GetSourceAppId(object session)
    {
        if (session == null)
        {
            return null;
        }

        PropertyInfo property = session.GetType().GetProperty("SourceAppUserModelId");
        if (property == null)
        {
            return null;
        }

        return property.GetValue(session, null) as string;
    }

    private void ReadRecentSeekState(
        string sessionKey,
        out string previousSessionKey,
        out long lastRequestedTargetTicks,
        out bool hasLastRequestedTarget,
        out bool isSameSession,
        out long elapsedSinceLastSeekTicks)
    {
        previousSessionKey = null;
        lastRequestedTargetTicks = 0;
        hasLastRequestedTarget = false;
        isSameSession = false;
        elapsedSinceLastSeekTicks = long.MaxValue;

        lock (seekStateSync)
        {
            if (!hasRecentSeekState)
            {
                return;
            }

            previousSessionKey = lastSeekSessionKey;
            lastRequestedTargetTicks = lastRequestedSeekTargetTicks;
            hasLastRequestedTarget = true;
            isSameSession = string.Equals(lastSeekSessionKey, sessionKey, StringComparison.OrdinalIgnoreCase);
            if (!isSameSession)
            {
                return;
            }

            if (lastSeekUtc == DateTime.MinValue)
            {
                elapsedSinceLastSeekTicks = long.MaxValue;
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            elapsedSinceLastSeekTicks = nowUtc >= lastSeekUtc
                ? (nowUtc - lastSeekUtc).Ticks
                : long.MaxValue;
        }
    }

    private void RememberRecentSeekState(string sessionKey, long targetTicks)
    {
        lock (seekStateSync)
        {
            lastSeekSessionKey = sessionKey;
            lastRequestedSeekTargetTicks = targetTicks;
            lastSeekUtc = DateTime.UtcNow;
            hasRecentSeekState = true;
        }
    }

    private void ResetRecentSeekState(string reason)
    {
        bool hadState;

        lock (seekStateSync)
        {
            hadState = hasRecentSeekState;
            hasRecentSeekState = false;
            lastSeekSessionKey = null;
            lastRequestedSeekTargetTicks = 0;
            lastSeekUtc = DateTime.MinValue;
        }

        if (hadState)
        {
            logMessage("Recent seek state reset: " + reason);
        }
    }

    private static string GetSessionKey(object session)
    {
        return GetSourceAppId(session);
    }

    private static string DescribeSessions(object[] sessions)
    {
        if (sessions == null || sessions.Length == 0)
        {
            return "<none>";
        }

        return string.Join(", ", sessions
            .Select(GetSourceAppId)
            .Select(FormatValue)
            .ToArray());
    }

    private static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<null>" : value;
    }

    private static string FormatTicks(long ticks)
    {
        return string.Format("{0} ({1})", TimeSpan.FromTicks(ticks), ticks);
    }

    private static string FormatOptionalTicks(bool hasValue, long ticks)
    {
        return hasValue ? FormatTicks(ticks) : "<none>";
    }

    private static string FormatElapsedMilliseconds(bool hasValue, long elapsedTicks)
    {
        if (!hasValue || elapsedTicks == long.MaxValue)
        {
            return "<none>";
        }

        return TimeSpan.FromTicks(elapsedTicks).TotalMilliseconds.ToString("0");
    }
}
