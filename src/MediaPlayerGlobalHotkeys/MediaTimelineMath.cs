public static class MediaTimelineMath
{
    public static long ResolveSeekBaseTicks(
        long reportedTicks,
        long minimumTicks,
        long maximumTicks,
        long deltaTicks,
        long lastRequestedTicks,
        bool hasLastRequestedTicks,
        bool isSameSession,
        long elapsedSinceLastSeekTicks,
        long staleWindowTicks)
    {
        long normalizedReportedTicks = ClampToRange(reportedTicks, minimumTicks, maximumTicks);
        if (!hasLastRequestedTicks || !isSameSession)
        {
            return normalizedReportedTicks;
        }

        if (elapsedSinceLastSeekTicks < 0 || elapsedSinceLastSeekTicks > staleWindowTicks)
        {
            return normalizedReportedTicks;
        }

        long normalizedLastRequestedTicks = ClampToRange(lastRequestedTicks, minimumTicks, maximumTicks);
        long staleGapTicks = System.Math.Abs(normalizedLastRequestedTicks - normalizedReportedTicks);
        long seekStepTicks = System.Math.Abs(deltaTicks);
        if (staleGapTicks >= seekStepTicks && staleGapTicks > 0)
        {
            return normalizedLastRequestedTicks;
        }

        return normalizedReportedTicks;
    }

    public static long ClampSeekTargetTicks(long currentTicks, long minimumTicks, long maximumTicks, long deltaTicks)
    {
        long targetTicks = currentTicks + deltaTicks;

        return ClampToRange(targetTicks, minimumTicks, maximumTicks);
    }

    private static long ClampToRange(long ticks, long minimumTicks, long maximumTicks)
    {
        if (ticks < minimumTicks)
        {
            return minimumTicks;
        }

        if (ticks > maximumTicks)
        {
            return maximumTicks;
        }

        return ticks;
    }
}
