using System;

public static class MediaPlayerTarget
{
    private static readonly string[] BuiltInMediaPlayerPrefixes =
    {
        "Microsoft.ZuneMusic",
        "Microsoft.ZuneVideo"
    };

    public static bool IsTarget(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        foreach (string prefix in BuiltInMediaPlayerPrefixes)
        {
            if (appId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
