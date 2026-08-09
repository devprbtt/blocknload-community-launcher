namespace BnlCommunityFixes
{
    public static class FpsCounterRuntime
    {
        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            var go = new UnityEngine.GameObject("BNL FPS Counter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FpsCounterDisplay>();
        }
    }

    public sealed class FpsCounterDisplay : UnityEngine.MonoBehaviour
    {
        private UnityEngine.GUIStyle style;
        private float smoothedFrameTime;
        private float refreshRemaining;
        private string text = "FPS: --";

        private void Awake()
        {
            smoothedFrameTime = 1f / 60f;
        }

        private void Update()
        {
            if (UnityEngine.Application.loadedLevelName != "Zone") return;
            float dt = UnityEngine.Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            smoothedFrameTime += (dt - smoothedFrameTime) * UnityEngine.Mathf.Clamp01(dt * 5f);
            refreshRemaining -= dt;
            if (refreshRemaining > 0f) return;
            refreshRemaining = FpsCounterGeneratedConfig.RefreshInterval;
            int fps = UnityEngine.Mathf.RoundToInt(1f / smoothedFrameTime);
            text = "FPS: " + fps;
        }

        private void OnGUI()
        {
            if (UnityEngine.Application.loadedLevelName != "Zone" || UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
            if (style == null)
            {
                style = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label);
                style.alignment = UnityEngine.TextAnchor.UpperRight;
                style.fontSize = 18;
                style.fontStyle = UnityEngine.FontStyle.Bold;
                style.normal.textColor = UnityEngine.Color.white;
            }
            var rect = new UnityEngine.Rect(UnityEngine.Screen.width - 180f, 8f, 164f, 28f);
            UnityEngine.GUI.Label(new UnityEngine.Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, ShadowStyle());
            UnityEngine.GUI.Label(rect, text, style);
        }

        private UnityEngine.GUIStyle ShadowStyle()
        {
            // Created only once, together with the main style.
            if (shadow == null)
            {
                shadow = new UnityEngine.GUIStyle(style);
                shadow.normal.textColor = new UnityEngine.Color(0f, 0f, 0f, 0.85f);
            }
            return shadow;
        }

        private UnityEngine.GUIStyle shadow;
    }
}
