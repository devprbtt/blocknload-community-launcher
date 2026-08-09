namespace BnlCommunityFixes
{
    public static class WsiPerformanceRuntime
    {
        private static float nextUpdate;
        private static bool initialized;

        public static bool ShouldSkipReconciliation()
        {
            if (UnityEngine.Application.loadedLevelName != "Zone") return false;
            float now = UnityEngine.Time.unscaledTime;
            if (!initialized)
            {
                initialized = true;
                nextUpdate = now;
            }
            if (now < nextUpdate) return true;
            nextUpdate = now + WsiPerformanceGeneratedConfig.UpdateInterval;
            return false;
        }
    }
}
