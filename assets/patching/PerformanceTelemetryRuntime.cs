namespace BnlCommunityFixes
{
    public static class PerformanceTelemetryRuntime
    {
        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            var go = new UnityEngine.GameObject("BNL Performance Telemetry");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PerformanceTelemetryCollector>();
        }
    }

    public sealed class PerformanceTelemetryCollector : UnityEngine.MonoBehaviour
    {
        private System.IO.BinaryWriter writer;
        private string label;
        private float warmupRemaining;
        private float matchElapsed;
        private float flushElapsed;
        private float metricsElapsed;
        private double managedMb;
        private int startGc0;
        private int startGc1;
        private int startGc2;
        private bool wasInMatch;
        private float stateCensusRemaining;
        private bool stateCensusWritten;

        private void Start()
        {
            label = Sanitize(PerformanceTelemetryGeneratedConfig.Label);
        }

        private void Update()
        {
            bool inMatch = UnityEngine.Application.loadedLevelName == "Zone";
            if (!inMatch)
            {
                if (wasInMatch) EndMatch();
                wasInMatch = false;
                return;
            }

            if (!wasInMatch)
            {
                wasInMatch = true;
                BeginMatch();
            }

            if (writer == null) return;
            float dt = UnityEngine.Time.unscaledDeltaTime;
            if (!stateCensusWritten)
            {
                stateCensusRemaining -= dt;
                if (stateCensusRemaining <= 0f)
                {
                    stateCensusWritten = true;
                    WriteStateCensus();
                }
            }
            if (warmupRemaining > 0f)
            {
                warmupRemaining -= dt;
                ResetGcCounts();
                return;
            }

            matchElapsed += dt;
            flushElapsed += dt;
            metricsElapsed += dt;
            if (metricsElapsed >= 1f)
            {
                metricsElapsed = 0f;
                managedMb = System.GC.GetTotalMemory(false) / 1048576.0;
            }

            writer.Write(System.DateTime.UtcNow.Ticks);
            writer.Write(matchElapsed);
            writer.Write(dt * 1000f);
            writer.Write((float)managedMb);
            writer.Write(System.GC.CollectionCount(0) - startGc0);
            writer.Write(System.GC.CollectionCount(1) - startGc1);
            writer.Write(System.GC.CollectionCount(2) - startGc2);

            if (flushElapsed >= PerformanceTelemetryGeneratedConfig.FlushIntervalSeconds)
            {
                writer.Flush();
                flushElapsed = 0f;
            }
        }

        private void BeginMatch()
        {
            EndMatch();
            warmupRemaining = PerformanceTelemetryGeneratedConfig.WarmupSeconds;
            matchElapsed = 0f;
            flushElapsed = 0f;
            metricsElapsed = 1f;
            managedMb = 0.0;
            stateCensusRemaining = 7f;
            stateCensusWritten = false;
            ResetGcCounts();
            try
            {
                var directory = System.IO.Path.Combine(UnityEngine.Application.dataPath, "bnl-performance-logs");
                System.IO.Directory.CreateDirectory(directory);
                var path = System.IO.Path.Combine(directory, "performance-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + label + ".bnlperf");
                writer = new System.IO.BinaryWriter(new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read, 65536));
                writer.Write(new byte[] { 66, 78, 76, 80, 82, 70, 48, 49 }); // BNLPRF01
                writer.Write(1);
                writer.Write(label);
                UnityEngine.Debug.Log("[BNL Perf] continuous match recording started: " + path);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[BNL Perf] Could not create telemetry log: " + ex);
                writer = null;
            }
        }

        private void EndMatch()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Close();
            writer = null;
            UnityEngine.Debug.Log("[BNL Perf] match recording stopped.");
        }

        private void ResetGcCounts()
        {
            startGc0 = System.GC.CollectionCount(0);
            startGc1 = System.GC.CollectionCount(1);
            startGc2 = System.GC.CollectionCount(2);
        }

        private static void WriteStateCensus()
        {
            try
            {
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                UnityEngine.MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    UnityEngine.MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null || !behaviour.enabled || !behaviour.gameObject.activeInHierarchy) continue;
                    string name = behaviour.GetType().FullName;
                    int count;
                    counts.TryGetValue(name, out count);
                    counts[name] = count + 1;
                }

                var entries = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(counts);
                entries.Sort(delegate(System.Collections.Generic.KeyValuePair<string, int> a, System.Collections.Generic.KeyValuePair<string, int> b)
                {
                    int byCount = b.Value.CompareTo(a.Value);
                    return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
                });

                UnityEngine.Debug.Log(
                    "[BNL StateCensus] BEGIN behaviours=" + behaviours.Length +
                    " transforms=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.Transform>().Length +
                    " renderers=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.Renderer>().Length +
                    " animators=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.Animator>().Length +
                    " particles=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.ParticleSystem>().Length +
                    " audio=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>().Length +
                    " cameras=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.Camera>().Length +
                    " lights=" + UnityEngine.Object.FindObjectsOfType<UnityEngine.Light>().Length +
                    " targetFps=" + UnityEngine.Application.targetFrameRate +
                    " vSync=" + UnityEngine.QualitySettings.vSyncCount +
                    " resolution=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);

                for (int i = 0; i < entries.Count; i++)
                {
                    UnityEngine.Debug.Log("[BNL StateCensus] TYPE " + entries[i].Value + " " + entries[i].Key);
                }
                UnityEngine.Debug.Log("[BNL StateCensus] END types=" + entries.Count);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL StateCensus] failed: " + ex.Message);
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "test";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(',', '_').Replace(' ', '-');
        }

        private void OnApplicationQuit()
        {
            EndMatch();
        }
    }
}
