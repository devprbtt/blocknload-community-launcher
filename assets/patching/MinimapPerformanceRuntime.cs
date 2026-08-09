namespace BnlCommunityFixes
{
    public static class MinimapPerformanceRuntime
    {
        private static float nextCamera;
        private static float nextLayout;
        private static float nextPopulation;
        private static bool cameraInitialized;
        private static bool layoutInitialized;
        private static bool populationInitialized;

        public static bool ShouldSkipCameraUpdate()
        {
            return ShouldSkip(ref nextCamera, ref cameraInitialized, 0f);
        }

        public static bool ShouldSkipLayoutUpdate()
        {
            return ShouldSkip(ref nextLayout, ref layoutInitialized, MinimapPerformanceGeneratedConfig.UpdateInterval / 3f);
        }

        public static bool ShouldSkipPopulationUpdate()
        {
            return ShouldSkip(ref nextPopulation, ref populationInitialized, MinimapPerformanceGeneratedConfig.UpdateInterval * 2f / 3f);
        }

        private static bool ShouldSkip(ref float nextUpdate, ref bool initialized, float initialOffset)
        {
            if (UnityEngine.Application.loadedLevelName != "Zone") return false;
            float now = UnityEngine.Time.unscaledTime;
            if (!initialized)
            {
                initialized = true;
                nextUpdate = now + initialOffset;
            }
            if (now < nextUpdate) return true;
            nextUpdate = now + MinimapPerformanceGeneratedConfig.UpdateInterval;
            return false;
        }
    }
}
