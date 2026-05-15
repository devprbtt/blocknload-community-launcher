using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BnlCommunityFixes
{
    public sealed class ReplayPlayerRuntime : MonoBehaviour
    {
        private const string ObjectName = "BNL_ReplayPlayerRuntime";
        private const long LargeReplayJsonFallbackBytes = 512L * 1024L * 1024L;

        private static ReplayPlayerRuntime instance;

        private readonly List<ReplayTrack> tracks = new List<ReplayTrack>();
        private readonly List<UnitMarker> markers = new List<UnitMarker>();
        private ulong replayServerTime;
        private readonly List<GameObject> mapBlocks = new List<GameObject>();
        private readonly Dictionary<string, UnitMetadata> unitMetadata = new Dictionary<string, UnitMetadata>();
        private readonly Dictionary<string, List<UnitGearEvent>> unitGearEvents = new Dictionary<string, List<UnitGearEvent>>();
        private readonly Dictionary<string, List<UnitToolEvent>> unitToolEvents = new Dictionary<string, List<UnitToolEvent>>();
        private readonly Dictionary<string, List<UnitCastEvent>> unitCastEvents = new Dictionary<string, List<UnitCastEvent>>();
        private readonly List<ReloadReplayEvent> reloadEvents = new List<ReloadReplayEvent>();
        private readonly List<AbilityReplayEvent> abilityEvents = new List<AbilityReplayEvent>();
        private readonly List<BuildReplayEvent> buildEvents = new List<BuildReplayEvent>();
        private readonly List<KillReplayEvent> killEvents = new List<KillReplayEvent>();
        private readonly List<DamageReplayEvent> damageEvents = new List<DamageReplayEvent>();
        private readonly List<ZoneStatsReplayEvent> zoneStatsEvents = new List<ZoneStatsReplayEvent>();
        private readonly Dictionary<string, float> deathHoldUntilByUnitId = new Dictionary<string, float>();
        private readonly List<ProjectileReplayObject> projectileObjects = new List<ProjectileReplayObject>();
        private readonly List<ImpactReplayEvent> impactEvents = new List<ImpactReplayEvent>();
        private readonly List<BarrierReplayEvent> barrierEvents = new List<BarrierReplayEvent>();
        private readonly List<ChannelReplayEvent> channelEvents = new List<ChannelReplayEvent>();
        private readonly List<DashChargeReplayEvent> dashChargeEvents = new List<DashChargeReplayEvent>();
        private readonly List<RecallReplayEvent> recallEvents = new List<RecallReplayEvent>();
        private readonly List<PortalTeleportReplayEvent> portalTeleportEvents = new List<PortalTeleportReplayEvent>();
        private readonly List<PickupTakenReplayEvent> pickupTakenEvents = new List<PickupTakenReplayEvent>();
        private readonly Dictionary<string, BuildReplayEvent> buildPlacementByUnitId = new Dictionary<string, BuildReplayEvent>();
        private readonly Dictionary<uint, string> projectilePrefabCache = new Dictionary<uint, string>();
        private readonly Dictionary<uint, string> abilityPrefabCache = new Dictionary<uint, string>();
        private readonly Dictionary<uint, GearInfo> gearInfoCache = new Dictionary<uint, GearInfo>();
        private readonly Dictionary<string, uint> deviceKeyHashByName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> unitDropTimes = new Dictionary<string, float>();
        private readonly Dictionary<uint, UnitCardInfo> unitCardCache = new Dictionary<uint, UnitCardInfo>();
        private readonly List<BlockTimelineEvent> blockTimeline = new List<BlockTimelineEvent>();
        private readonly Dictionary<string, Protocol.BlockUpdate> initialBlockByCell = new Dictionary<string, Protocol.BlockUpdate>();
        private readonly List<PlayerReplayInfo> replayPlayers = new List<PlayerReplayInfo>();
        private readonly Dictionary<string, List<UnitStateEvent>> unitStateEvents = new Dictionary<string, List<UnitStateEvent>>();
        private int nextBlockTimelineIndex;
        private float blockTimelineAppliedThrough = float.MinValue;
        private string replayPath = "";
        private string analysisDirectory = "";
        private string status = "Launch replay mode from the launcher";
        private bool loaded;
        private bool playing;
        private bool mapVisible = true;
        private bool spectatorCameraActive;
        private bool realZoneLoadPending;
        private bool realZoneInitApplied;
        private int displayMode;
        private bool realUnitSpawnWarningShown;
        private bool realUnitMoveWarningShown;
        private byte[] pendingInitZonePayload;
        private float replayTime;
        private float realZoneLoadedAt = -1f;
        private float statsResyncAt = -1f;
        private float startTime;
        private float endTime;
        private float speed = 1f;
        private float statusVisibleUntil;
        private float lastGlobalEventTime = float.MinValue;
        private ZoneStatsReplayEvent currentPhaseEvent;
        private Camera spectatorCamera;
        private float cameraYaw;
        private float cameraPitch = 35f;
        private int followPlayerIndex = -1;
        private bool showDiagnosticPlayerHud;
        private bool showDebugWorldLabels;
        private bool replayModeLaunchChecked;

        public static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                instance = existing.GetComponent<ReplayPlayerRuntime>();
                if (instance != null)
                {
                    return;
                }
            }

            GameObject host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ReplayPlayerRuntime>();
            Debug.Log("[BNL Replay] Runtime initialized");
        }

        private void Update()
        {
            TryStartLauncherReplayMode();

            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (!loaded)
                {
                    ShowStatus("Replay loading now starts from the launcher Replay Mode button");
                }
                else
                {
                    playing = !playing;
                    ShowStatus(playing ? "Replay playback resumed" : "Replay playback paused");
                }
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                ResetPlayback();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                ToggleSpectatorCamera();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                ToggleMap();
            }

            if (Input.GetKeyDown(KeyCode.F11) || Input.GetKeyDown(KeyCode.F6))
            {
                ShowStatus("Replay mode must be launched from the launcher");
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                CycleDisplayMode();
            }

            HandlePlayerFollowKeys();

            if (realZoneLoadPending)
            {
                TryApplyPendingRealZoneInit();
            }

            if (statsResyncAt > 0f && Time.realtimeSinceStartup >= statsResyncAt)
            {
                statsResyncAt = -1f;
                ApplyReplayPlayerStatsSnapshot();
            }

            if (spectatorCameraActive)
            {
                UpdateSpectatorCamera();
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                SkipReplaySeconds(-5f);
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                SkipReplaySeconds(5f);
            }

            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                SkipReplaySeconds(-30f);
            }

            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                SkipReplaySeconds(30f);
            }

            if (Input.GetKeyDown(KeyCode.Minus))
            {
                speed = Mathf.Max(0.25f, speed * 0.5f);
                ShowStatus("Replay speed " + speed.ToString("0.##", CultureInfo.InvariantCulture) + "x");
            }

            if (Input.GetKeyDown(KeyCode.Equals))
            {
                speed = Mathf.Min(8f, speed * 2f);
                ShowStatus("Replay speed " + speed.ToString("0.##", CultureInfo.InvariantCulture) + "x");
            }

            if (!loaded || !playing)
            {
                UpdateFollowCamera();
                UpdateLabels();
                return;
            }

            if (replayTime == startTime && Time.deltaTime > 0f && speed > 0f)
            {
                Debug.Log("[BNL Replay] First tick advancing from startTime=" + startTime + " deltaTime=" + Time.deltaTime + " speed=" + speed);
            }

            replayTime += Time.deltaTime * speed;
            replayServerTime = (ulong)(replayTime * 1000.0);
            if (replayTime >= endTime)
            {
                replayTime = endTime;
                playing = false;
                ShowStatus("Replay reached end");
            }

            UpdateMarkerPositions();
            UpdateFollowCamera();
            UpdateLabels();
        }

        private void OnGUI()
        {
            if (!loaded && Time.realtimeSinceStartup > statusVisibleUntil)
            {
                return;
            }

            GUI.Box(new Rect(12f, 12f, 420f, loaded ? 128f : 48f), "");
            GUI.Label(new Rect(22f, 20f, 400f, 22f), "BNL Replay Prototype");
                GUI.Label(new Rect(22f, 42f, 400f, 22f), status);
            if (loaded)
            {
                float relative = replayTime - startTime;
                float duration = Mathf.Max(0f, endTime - startTime);
                GUI.Label(new Rect(22f, 64f, 400f, 22f), "F7 play/pause  F8 reset  F9 cam  F10 map  F12 filter  PgUp/Dn ±30s  [/] ±5s");
                GUI.Label(new Rect(22f, 82f, 400f, 22f), "Time " + relative.ToString("0.0", CultureInfo.InvariantCulture) + " / " + duration.ToString("0.0", CultureInfo.InvariantCulture) + "s, mode " + DisplayModeName() + ", units " + markers.Count + ", map blocks " + mapBlocks.Count);
                if (GUI.Button(new Rect(22f, 108f, 62f, 24f), "-30s"))
                {
                    SkipReplaySeconds(-30f);
                }
                if (GUI.Button(new Rect(90f, 108f, 56f, 24f), "-5s"))
                {
                    SkipReplaySeconds(-5f);
                }
                if (GUI.Button(new Rect(152f, 108f, 68f, 24f), playing ? "Pause" : "Play"))
                {
                    playing = !playing;
                    ShowStatus(playing ? "Replay playback resumed" : "Replay playback paused");
                }
                if (GUI.Button(new Rect(226f, 108f, 56f, 24f), "+5s"))
                {
                    SkipReplaySeconds(5f);
                }
                if (GUI.Button(new Rect(288f, 108f, 62f, 24f), "+30s"))
                {
                    SkipReplaySeconds(30f);
                }

                if (showDiagnosticPlayerHud)
                {
                    DrawPlayerHud();
                }
            }
        }

        private void DrawPlayerHud()
        {
            if (replayPlayers.Count == 0)
            {
                return;
            }

            float x = Screen.width - 320f;
            float y = 12f;
            GUI.Box(new Rect(x, y, 308f, 32f + replayPlayers.Count * 30f), "");
            GUI.Label(new Rect(x + 10f, y + 8f, 285f, 22f), "Players  (1-9 follow, 0 free camera)");
            for (int i = 0; i < replayPlayers.Count; i++)
            {
                PlayerReplayInfo player = replayPlayers[i];
                UnitStateEvent state = GetUnitStateAt(player.UnitId, replayTime);
                float rowY = y + 34f + i * 30f;
                bool follow = i == followPlayerIndex;
                string prefix = i < 9 ? (i + 1).ToString(CultureInfo.InvariantCulture) : "-";
                string hp = state != null && state.HasHealth ? state.Health.ToString("0", CultureInfo.InvariantCulture) : "?";
                string res = state != null && state.HasResource ? state.Resource.ToString("0", CultureInfo.InvariantCulture) : "?";

                GUI.color = TeamGuiColor(player.Team);
                GUI.Label(new Rect(x + 10f, rowY, 22f, 22f), prefix);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 34f, rowY, 138f, 22f), (follow ? "> " : "") + player.Nickname);
                GUI.Label(new Rect(x + 176f, rowY, 62f, 22f), hp + " hp");
                GUI.Label(new Rect(x + 240f, rowY, 60f, 22f), res + " bricks");
            }

            GUI.color = Color.white;
        }

        private static Color TeamGuiColor(string team)
        {
            if (string.Equals(team, "Team1", StringComparison.Ordinal))
            {
                return new Color(0.35f, 0.75f, 1f, 1f);
            }
            if (string.Equals(team, "Team2", StringComparison.Ordinal))
            {
                return new Color(1f, 0.35f, 0.25f, 1f);
            }
            return Color.white;
        }

        private UnitStateEvent GetUnitStateAt(string unitId, float time)
        {
            List<UnitStateEvent> list;
            if (string.IsNullOrEmpty(unitId) || !unitStateEvents.TryGetValue(unitId, out list))
            {
                return null;
            }

            UnitStateEvent merged = null;
            for (int i = 0; i < list.Count; i++)
            {
                UnitStateEvent item = list[i];
                if (item.Time > time)
                {
                    break;
                }

                if (merged == null)
                {
                    merged = new UnitStateEvent();
                }

                merged.Time = item.Time;
                if (item.HasHealth)
                {
                    merged.Health = item.Health;
                    merged.HasHealth = true;
                }
                if (item.HasShield)
                {
                    merged.Shield = item.Shield;
                    merged.HasShield = true;
                }
                if (item.HasResource)
                {
                    merged.Resource = item.Resource;
                    merged.HasResource = true;
                }
                if (item.HasDevices)
                {
                    merged.Devices = item.Devices;
                    merged.HasDevices = true;
                }
                if (item.HasEffects)
                {
                    merged.Effects = item.Effects;
                    merged.HasEffects = true;
                }
                if (item.HasBuffs)
                {
                    merged.Buffs = item.Buffs;
                    merged.HasBuffs = true;
                }
            }

            return merged;
        }

        private void HandlePlayerFollowKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                followPlayerIndex = -1;
                ShowStatus("Replay camera follow disabled");
                return;
            }

            for (int i = 0; i < replayPlayers.Count && i < 9; i++)
            {
                KeyCode alpha = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad))
                {
                    followPlayerIndex = i;
                    TrySpectateReplayPlayer(replayPlayers[i]);
                    ShowStatus("Following " + replayPlayers[i].Nickname + " with game spectator camera");
                    return;
                }
            }
        }

        private void TrySpectateReplayPlayer(PlayerReplayInfo player)
        {
            UnitMarker marker = FindMarkerByUnitId(player.UnitId);
            if (marker == null || marker.RealUnit == null)
            {
                return;
            }

            try
            {
                if (spectatorCameraActive && spectatorCamera != null)
                {
                    spectatorCameraActive = false;
                    spectatorCamera.gameObject.SetActive(false);
                }

                CameraSpectatorView spectatorView = Singleton<CameraSpectatorView>.Instance;
                if (spectatorView != null)
                {
                    spectatorView.Locked = false;
                    spectatorView.SpectateUnit(marker.RealUnit);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] CameraSpectatorView.SpectateUnit failed: " + ex.Message);
            }
        }

        private void UpdateFollowCamera()
        {
            // Native CameraSpectatorView handles follow/orbit/zoom when a replay player is selected.
        }

        private UnitMarker FindMarkerByUnitId(string unitId)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (string.Equals(markers[i].UnitId, unitId, StringComparison.Ordinal))
                {
                    return markers[i];
                }
            }

            return null;
        }

        private void SkipReplaySeconds(float seconds)
        {
            if (!loaded)
            {
                return;
            }

            replayTime = Mathf.Clamp(replayTime + seconds, startTime, endTime);
            replayServerTime = (ulong)(replayTime * 1000.0);
            playing = true;
            UpdateMarkerPositions();
            ShowStatus((seconds < 0f ? "Skipped back " + Mathf.Abs(seconds).ToString("0", CultureInfo.InvariantCulture) + "s" : "Skipped ahead " + seconds.ToString("0", CultureInfo.InvariantCulture) + "s") + " and resumed playback");
        }

        private void LoadReplay()
        {
            LoadReplay(true, true, "Loaded ");
        }

        private void LoadReplay(bool renderMapSurface, bool autoPlay, string statusPrefix)
        {
            string path = FindReplayPath();
            LoadReplayFromPath(path, renderMapSurface, autoPlay, statusPrefix);
        }

        private void LoadReplayFromPath(string path, bool renderMapSurface, bool autoPlay, string statusPrefix)
        {
            ClearMarkers();
            ClearMap();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                loaded = false;
                playing = false;
                ShowStatus("No replay.normalized.json found in latest analysis output");
                return;
            }

            try
            {
                FileInfo replayInfo = new FileInfo(path);
                bool useCsvTracks = replayInfo.Exists && replayInfo.Length > LargeReplayJsonFallbackBytes;
                List<ReplayTrack> parsedTracks;
                if (useCsvTracks)
                {
                    parsedTracks = ParseTracksFromCsv(Path.GetDirectoryName(path) ?? "");
                    Debug.Log("[BNL Replay] Using CSV movement tracks for large replay: " + replayInfo.Length + " bytes");
                }
                else
                {
                    string json = File.ReadAllText(path);
                    parsedTracks = ParseTracks(json);
                }
                if (parsedTracks.Count == 0)
                {
                    loaded = false;
                    playing = false;
                    ShowStatus("Replay has no movement tracks");
                    return;
                }

                tracks.Clear();
                tracks.AddRange(parsedTracks);
                replayPath = path;
                analysisDirectory = Path.GetDirectoryName(path) ?? "";
                LoadUnitMetadata();
                LoadUnitEvents();
                LoadPlayerHudData();
                LoadUnitStateEvents();
                LoadZoneStatsEvents();
                LoadKillEvents();
                LoadDamageEvents();
                LoadAbilityEvents();
                LoadBuildEvents();
                LoadBarrierEvents();
                LoadChannelEvents();
                LoadDashChargeEvents();
                LoadRecallEvents();
                LoadPortalTeleportEvents();
                LoadPickupTakenEvents();
                LoadProjectileEvents();
                LoadBlockTimeline();
                CalculateTimeRange();
                CreateMarkers();
                if (renderMapSurface)
                {
                    LoadMapSurface();
                }

                replayTime = startTime;
                replayServerTime = (ulong)(startTime * 1000.0);
                loaded = true;
                playing = autoPlay;
                Debug.Log("[BNL Replay] LoadReplay done: tracks=" + tracks.Count + " startTime=" + startTime + " endTime=" + endTime + " playing=" + playing);
                UpdateMarkerPositions();
                ShowStatus(statusPrefix + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                loaded = false;
                playing = false;
                ShowStatus("Replay load failed: " + ex.Message);
            }
        }

        private string FindReplayPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local))
            {
                string requested = FindRequestedReplayPath(local);
                if (!string.IsNullOrEmpty(requested))
                {
                    return requested;
                }

                string root = CombinePath(local, "BNL-CommunityFixes", "data", "replay-analysis");
                string newest = FindNewestAnalysisReplay(root, true);
                if (!string.IsNullOrEmpty(newest))
                {
                    return newest;
                }

                newest = FindNewestAnalysisReplay(root, false);
                if (!string.IsNullOrEmpty(newest))
                {
                    return newest;
                }
            }

            string dataDir = Application.dataPath;
            string replayRoot = Path.Combine(dataDir, "bnl-match-replays");
            string direct = Path.Combine(replayRoot, "replay.normalized.json");
            if (File.Exists(direct))
            {
                return direct;
            }

            string nested = Path.Combine(Path.Combine(replayRoot, "latest"), "replay.normalized.json");
            return nested;
        }

        private static string FindRequestedReplayPath(string localApplicationData)
        {
            string textRequestPath = CombinePath(localApplicationData, "BNL-CommunityFixes", "data", "replay-launch-request.path");
            if (File.Exists(textRequestPath))
            {
                try
                {
                    string textPath = File.ReadAllText(textRequestPath).Trim();
                    if (!string.IsNullOrEmpty(textPath) && File.Exists(textPath))
                    {
                        Debug.Log("[BNL Replay] Using replay selection path file: " + textPath);
                        return textPath;
                    }

                    Debug.Log("[BNL Replay] Ignored replay selection path file because target was missing: " + textPath);
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] Failed reading replay selection path file: " + ex.Message);
                }
            }

            string requestPath = CombinePath(localApplicationData, "BNL-CommunityFixes", "data", "replay-launch-request.json");
            if (!File.Exists(requestPath))
            {
                return "";
            }

            try
            {
                string json = File.ReadAllText(requestPath);
                string normalizedPath = ExtractJsonString(json, "NormalizedPath");
                if (string.IsNullOrEmpty(normalizedPath))
                {
                    normalizedPath = ExtractJsonString(json, "normalizedPath");
                }

                if (!string.IsNullOrEmpty(normalizedPath) && File.Exists(normalizedPath))
                {
                    Debug.Log("[BNL Replay] Using replay selection json file: " + normalizedPath);
                    return normalizedPath;
                }

                Debug.Log("[BNL Replay] Ignored replay selection json because target was missing: " + normalizedPath);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Failed reading replay selection json: " + ex.Message);
            }

            return "";
        }

        private void TryStartLauncherReplayMode()
        {
            if (replayModeLaunchChecked || loaded || realZoneLoadPending)
            {
                return;
            }

            string requestPath = GetReplayLaunchRequestJsonPath();
            if (string.IsNullOrEmpty(requestPath) || !File.Exists(requestPath))
            {
                replayModeLaunchChecked = true;
                return;
            }

            bool launchReplayMode = false;
            try
            {
                string json = File.ReadAllText(requestPath);
                launchReplayMode = ExtractJsonBool(json, "LaunchReplayMode") || ExtractJsonBool(json, "launchReplayMode");
            }
            catch (Exception ex)
            {
                replayModeLaunchChecked = true;
                Debug.Log("[BNL Replay] Failed reading replay mode launch request: " + ex.Message);
                return;
            }

            if (!launchReplayMode)
            {
                replayModeLaunchChecked = true;
                return;
            }

            if (Application.loadedLevelName == "LoaderScene")
            {
                return;
            }

            if (Application.loadedLevelName == "Zone")
            {
                replayModeLaunchChecked = true;
                ShowStatus("Replay mode launch ignored inside an active match");
                return;
            }

            replayModeLaunchChecked = true;
            Debug.Log("[BNL Replay] Starting one-shot launcher replay mode from " + requestPath);
            StartRealZoneSpectatorExperiment(true);
        }

        private static string GetReplayLaunchRequestJsonPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                return "";
            }

            return CombinePath(local, "BNL-CommunityFixes", "data", "replay-launch-request.json");
        }

        private static string GetReplayLaunchRequestTextPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                return "";
            }

            return CombinePath(local, "BNL-CommunityFixes", "data", "replay-launch-request.path");
        }

        private static bool ExtractJsonBool(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            string pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*(true|false)";
            Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success && string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void ConsumeReplayLaunchRequest()
        {
            DeleteFileIfExists(GetReplayLaunchRequestJsonPath());
            DeleteFileIfExists(GetReplayLaunchRequestTextPath());
        }

        private static void DeleteFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Failed deleting replay launch request " + path + ": " + ex.Message);
            }
        }

        private static string ExtractJsonString(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            {
                return "";
            }

            string pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"([^\"]*)\"";
            Match match = Regex.Match(json, pattern, RegexOptions.CultureInvariant);
            return match.Success ? UnescapeJsonString(match.Groups[1].Value) : "";
        }

        private static string FindNewestAnalysisReplay(string root, bool requireMapBlocks)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return "";
            }

            string bestPath = "";
            DateTime bestWrite = DateTime.MinValue;
            DirectoryInfo rootInfo = new DirectoryInfo(root);
            DirectoryInfo[] dirs = rootInfo.GetDirectories();
            for (int i = 0; i < dirs.Length; i++)
            {
                DirectoryInfo dir = dirs[i];
                string replay = Path.Combine(dir.FullName, "replay.normalized.json");
                if (!File.Exists(replay))
                {
                    continue;
                }

                if (requireMapBlocks && !File.Exists(Path.Combine(dir.FullName, "map_blocks.csv")))
                {
                    continue;
                }

                DateTime write = File.GetLastWriteTimeUtc(replay);
                if (write > bestWrite)
                {
                    bestWrite = write;
                    bestPath = replay;
                }
            }

            return bestPath;
        }

        private static string CombinePath(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return "";
            }

            string path = parts[0] ?? "";
            for (int i = 1; i < parts.Length; i++)
            {
                path = Path.Combine(path, parts[i] ?? "");
            }

            return path;
        }

        private void LoadMapSurface()
        {
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "map_blocks.csv");
            if (!File.Exists(path))
            {
                ShowStatus("Loaded replay; map_blocks.csv not found for map render");
                return;
            }

            try
            {
                Dictionary<string, MapBlock> surface = new Dictionary<string, MapBlock>();
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length < 5)
                        {
                            continue;
                        }

                        int x;
                        int y;
                        int z;
                        int id;
                        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out z) ||
                            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                        {
                            continue;
                        }

                        string key = x.ToString(CultureInfo.InvariantCulture) + ":" + z.ToString(CultureInfo.InvariantCulture);
                        MapBlock existing;
                        if (!surface.TryGetValue(key, out existing) || y > existing.Y)
                        {
                            MapBlock block = new MapBlock();
                            block.X = x;
                            block.Y = y;
                            block.Z = z;
                            block.Id = id;
                            surface[key] = block;
                        }
                    }
                }

                foreach (MapBlock block in surface.Values)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "BNL_ReplayMapBlock";
                    cube.transform.position = new Vector3(block.X + 0.5f, block.Y + 0.5f, block.Z + 0.5f);
                    cube.transform.localScale = Vector3.one;
                    DontDestroyOnLoad(cube);

                    Collider collider = cube.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Destroy(collider);
                    }

                    Renderer renderer = cube.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = new Material(Shader.Find("Diffuse"));
                        renderer.material.color = ColorForBlock(block.Id);
                    }

                    cube.SetActive(mapVisible);
                    mapBlocks.Add(cube);
                }

                ShowStatus("Loaded replay and rendered map surface blocks: " + mapBlocks.Count);
            }
            catch (Exception ex)
            {
                ShowStatus("Map render failed: " + ex.Message);
            }
        }

        private static Color ColorForBlock(int id)
        {
            switch (id)
            {
                case 3:
                    return new Color(0.48f, 0.50f, 0.52f, 1f);
                case 4:
                    return new Color(1f, 0.25f, 0.05f, 1f);
                case 10:
                    return new Color(0.64f, 0.66f, 0.70f, 1f);
                case 13:
                    return new Color(0.33f, 0.25f, 0.17f, 1f);
                case 15:
                    return new Color(0.08f, 0.55f, 0.18f, 1f);
                default:
                    float r = 0.30f + (float)((id * 37) % 70) / 100f;
                    float g = 0.30f + (float)((id * 53) % 60) / 100f;
                    float b = 0.30f + (float)((id * 71) % 60) / 100f;
                    return new Color(Mathf.Min(r, 0.85f), Mathf.Min(g, 0.80f), Mathf.Min(b, 0.80f), 1f);
            }
        }

        private void ToggleMap()
        {
            mapVisible = !mapVisible;
            for (int i = 0; i < mapBlocks.Count; i++)
            {
                if (mapBlocks[i] != null)
                {
                    mapBlocks[i].SetActive(mapVisible);
                }
            }

            ShowStatus(mapVisible ? "Replay map shown" : "Replay map hidden");
        }

        private void ToggleSpectatorCamera()
        {
            if (spectatorCamera == null)
            {
                GameObject cameraObject = new GameObject("BNL_ReplaySpectatorCamera");
                DontDestroyOnLoad(cameraObject);
                spectatorCamera = cameraObject.AddComponent<Camera>();
                spectatorCamera.depth = 100f;
                spectatorCamera.fieldOfView = 70f;
                Vector3 center = EstimateReplayCenter();
                cameraObject.transform.position = center + new Vector3(0f, 28f, -38f);
                cameraObject.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            }

            spectatorCameraActive = !spectatorCameraActive;
            spectatorCamera.gameObject.SetActive(spectatorCameraActive);
            ShowStatus(spectatorCameraActive ? "Spectator camera enabled" : "Spectator camera disabled");
        }

        private void StartRealZoneSpectatorExperiment(bool consumeLaunchRequest)
        {
            try
            {
                if (realZoneLoadPending)
                {
                    ShowStatus("Replay Zone load is already pending");
                    return;
                }

                if (Application.loadedLevelName == "LoaderScene")
                {
                    ShowStatus("Replay Zone load ignored while the game loader is active");
                    return;
                }

                if (Application.loadedLevelName == "Zone")
                {
                    ShowStatus("F11 replay-zone test must start from the main menu, not inside a live match");
                    return;
                }

                string normalizedPath = FindReplayPath();
                if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
                {
                    ShowStatus("Real zone load failed: replay.normalized.json not found");
                    return;
                }

                if (!string.Equals(replayPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[BNL Replay] Replay selection changed from " + (string.IsNullOrEmpty(replayPath) ? "(none)" : replayPath) + " to " + normalizedPath);
                }

                ClearMarkers();
                ClearMap();
                replayPath = normalizedPath;
                analysisDirectory = Path.GetDirectoryName(normalizedPath) ?? "";
                string selectedMapName = ExtractReplayMapName(normalizedPath);
                string selectedMapHash = ExtractReplayMapHash(normalizedPath);
                string displayCaptureName = Path.GetFileName(normalizedPath);
                pendingInitZonePayload = ReadInitZonePayloadFromAnalysis(analysisDirectory, selectedMapHash);
                if (pendingInitZonePayload == null || pendingInitZonePayload.Length == 0)
                {
                    string capturePath = FindCapturePathFromNormalized(normalizedPath);
                    if (string.IsNullOrEmpty(capturePath) || !File.Exists(capturePath))
                    {
                        ShowStatus("Real zone load failed: source capture not found");
                        return;
                    }

                    pendingInitZonePayload = ReadInitZonePayload(capturePath, selectedMapHash);
                    if (pendingInitZonePayload == null || pendingInitZonePayload.Length == 0)
                    {
                        ShowStatus("Real zone load failed: InitZone payload not found");
                        return;
                    }

                    displayCaptureName = Path.GetFileName(capturePath);
                }

                Protocol.SceneZone scene = new Protocol.SceneZone();
                scene.GameMode = new Key("game_mode_custom");
                scene.MatchKey = new Key("match_shield_rush_v2_custom");
                scene.MyTeam = Protocol.TeamType.Team1;
                scene.IsSpectator = true;
                scene.IsMapEditor = false;
                scene.Restart = false;

                realZoneInitApplied = false;
                realZoneLoadPending = true;
                realZoneLoadedAt = -1f;
                if (consumeLaunchRequest)
                {
                    ConsumeReplayLaunchRequest();
                }

                LoadReplayZoneScene(scene);
                ShowStatus("Loading selected replay " + displayCaptureName + " map=" + (string.IsNullOrEmpty(selectedMapName) ? "unknown" : selectedMapName));
            }
            catch (Exception ex)
            {
                realZoneLoadPending = false;
                ShowStatus("Real zone load failed: " + ex.Message);
            }
        }

        private static void LoadReplayZoneScene(Protocol.SceneZone scene)
        {
            List<ILoaderData> loaders = new List<ILoaderData>();
            loaders.Add(new CommonAssetsLoader());
            loaders.Add(new LoadLevelLoader("Zone"));
            loaders.Add(new SetTargetFramerate(-1));
            InvokeSceneManager("Load", new object[] { "Zone", loaders, scene });
        }

        private static object InvokeSceneManager(string methodName, object[] args)
        {
            SceneManager sceneManager = Singleton<SceneManager>.Instance;
            System.Reflection.MethodInfo method = typeof(SceneManager).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (object.ReferenceEquals(method, null))
            {
                throw new MissingMethodException("SceneManager", methodName);
            }

            return method.Invoke(sceneManager, args);
        }

        private void TryApplyPendingRealZoneInit()
        {
            if (realZoneInitApplied || pendingInitZonePayload == null || pendingInitZonePayload.Length == 0)
            {
                return;
            }

            try
            {
                if (Application.loadedLevelName != "Zone")
                {
                    realZoneLoadedAt = -1f;
                    return;
                }

                if (realZoneLoadedAt < 0f)
                {
                    realZoneLoadedAt = Time.realtimeSinceStartup;
                    ShowStatus("Replay Zone scene loaded; waiting for game services");
                    return;
                }

                if (Time.realtimeSinceStartup - realZoneLoadedAt < 2f)
                {
                    return;
                }

                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener == null)
                {
                    return;
                }

                ZoneManager manager = Singleton<ZoneManager>.Instance;
                if (manager != null)
                {
                    manager.RemoveMap();
                }

                Protocol.ZoneInitData data = new Protocol.ZoneInitData();
                using (MemoryStream stream = new MemoryStream(pendingInitZonePayload))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    data.Read(reader);
                }

                Debug.Log("[BNL Replay] Applying InitZone mapKey=" + (data.MapKey.HasValue ? data.MapKey.Value.ToString() : "none") + " mapDataBytes=" + (data.MapData == null ? 0 : data.MapData.Length) + " colorDataBytes=" + (data.ColorData == null ? 0 : data.ColorData.Length));

                listener.InitZone(data);
                realZoneInitApplied = true;
                realZoneLoadPending = false;
                if (!spectatorCameraActive)
                {
                    ToggleSpectatorCamera();
                }

                LoadReplayFromPath(replayPath, false, true, "Loaded replay units on real map: ");
                // Re-push player stats 1s after InitZone so ZoneData and the HUD are fully ready.
                statsResyncAt = Time.realtimeSinceStartup + 1f;
                // Force Hud into Spectator screen so TAB scoreboard works.
                // Hud.Loading() sets Screen.Spectator only if ZoneData.IsSpectator is true when
                // SceneManager.Loader.IsDone — but in synthetic replay init the loader may have
                // already completed in Game screen. We force it via reflection.
                TryForceHudSpectatorScreen();
            }
            catch (Exception ex)
            {
                realZoneLoadPending = false;
                ShowStatus("Replay InitZone apply failed: " + ex.Message);
            }
        }

        private static string FindCapturePathFromNormalized(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
            {
                return "";
            }

            string json = ReadFilePrefix(normalizedPath, 1024 * 1024);
            Match match = Regex.Match(json, "\"source\"\\s*:\\s*\\{.*?\"path\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return "";
            }

            string path = UnescapeJsonString(match.Groups[1].Value);
            if (File.Exists(path))
            {
                return path;
            }

            // The launcher compresses completed captures to .jsonl.gz — check for that.
            string gzPath = path + ".gz";
            if (File.Exists(gzPath))
            {
                return gzPath;
            }

            return path;
        }

        private static string ReadFilePrefix(string path, int maxChars)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || maxChars <= 0)
            {
                return "";
            }

            using (StreamReader reader = new StreamReader(path))
            {
                char[] buffer = new char[maxChars];
                int read = reader.Read(buffer, 0, buffer.Length);
                return new string(buffer, 0, read);
            }
        }

        private static string ExtractReplayMapName(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
            {
                return "";
            }

            try
            {
                string json = ReadFilePrefix(normalizedPath, 1024 * 1024);
                return ExtractJsonString(json, "mapName");
            }
            catch
            {
                return "";
            }
        }

        private static string ExtractReplayMapHash(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
            {
                return "";
            }

            try
            {
                string json = ReadFilePrefix(normalizedPath, 1024 * 1024);
                return ExtractJsonString(json, "mapKeyHash");
            }
            catch
            {
                return "";
            }
        }

        private static StreamReader OpenCaptureReader(string capturePath)
        {
            var fileStream = new FileStream(capturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (capturePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                return new StreamReader(new GZipStream(fileStream, CompressionMode.Decompress));
            }

            return new StreamReader(fileStream);
        }

        private static byte[] ReadInitZonePayload(string capturePath, string expectedMapHash)
        {
            byte[] largestPayload = null;
            int largestLength = 0;
            string expectedDecimalHash = MapHashHexToDecimalString(expectedMapHash);

            using (StreamReader reader = OpenCaptureReader(capturePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf("\"event\":\"Recv_InitZone\"", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    Match match = Regex.Match(line, "\"payloadBase64\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);
                    if (!match.Success)
                    {
                        continue;
                    }

                    byte[] payload = Convert.FromBase64String(match.Groups[1].Value);
                    if (payload.Length > largestLength)
                    {
                        largestPayload = payload;
                        largestLength = payload.Length;
                    }

                    if (!string.IsNullOrEmpty(expectedDecimalHash) && InitZonePayloadMatchesMap(payload, expectedDecimalHash))
                    {
                        Debug.Log("[BNL Replay] Selected InitZone payload by map hash " + expectedMapHash + " bytes=" + payload.Length);
                        return payload;
                    }
                }
            }

            if (largestPayload != null)
            {
                Debug.Log("[BNL Replay] Selected largest InitZone payload bytes=" + largestPayload.Length);
            }

            return largestPayload;
        }

        private static byte[] ReadInitZonePayloadFromAnalysis(string directory, string expectedMapHash)
        {
            try
            {
                if (string.IsNullOrEmpty(directory))
                {
                    return null;
                }

                string path = Path.Combine(directory, "init_zone_payload.bin");
                if (!File.Exists(path))
                {
                    return null;
                }

                byte[] payload = File.ReadAllBytes(path);
                string expectedDecimalHash = MapHashHexToDecimalString(expectedMapHash);
                if (!string.IsNullOrEmpty(expectedDecimalHash) && !InitZonePayloadMatchesMap(payload, expectedDecimalHash))
                {
                    Debug.Log("[BNL Replay] Ignored init_zone_payload.bin because map hash did not match " + expectedMapHash);
                    return null;
                }

                Debug.Log("[BNL Replay] Loaded InitZone payload from analysis bytes=" + payload.Length);
                return payload;
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ReadInitZonePayloadFromAnalysis failed: " + ex.Message);
                return null;
            }
        }

        private static string MapHashHexToDecimalString(string mapHash)
        {
            if (string.IsNullOrEmpty(mapHash))
            {
                return "";
            }

            uint value;
            if (!uint.TryParse(mapHash, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                return "";
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool InitZonePayloadMatchesMap(byte[] payload, string expectedDecimalHash)
        {
            if (payload == null || payload.Length == 0 || string.IsNullOrEmpty(expectedDecimalHash))
            {
                return false;
            }

            try
            {
                Protocol.ZoneInitData data = new Protocol.ZoneInitData();
                using (MemoryStream stream = new MemoryStream(payload))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    data.Read(reader);
                }

                return data.MapKey.HasValue && string.Equals(data.MapKey.Value.ToString(), expectedDecimalHash, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string UnescapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\/", "/")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }

        private Vector3 EstimateReplayCenter()
        {
            if (markers.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < markers.Count; i++)
                {
                    if (markers[i].Track.Points.Count == 0)
                    {
                        continue;
                    }

                    sum += markers[i].Track.Points[0].Position;
                    count++;
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            return new Vector3(80f, 20f, 55f);
        }

        private void UpdateSpectatorCamera()
        {
            if (spectatorCamera == null)
            {
                return;
            }

            if (Input.GetMouseButton(1))
            {
                cameraYaw += Input.GetAxis("Mouse X") * 4f;
                cameraPitch -= Input.GetAxis("Mouse Y") * 4f;
                cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
                spectatorCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            }

            float moveSpeed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 32f : 12f;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += spectatorCamera.transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= spectatorCamera.transform.forward;
            if (Input.GetKey(KeyCode.D)) move += spectatorCamera.transform.right;
            if (Input.GetKey(KeyCode.A)) move -= spectatorCamera.transform.right;
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) move -= Vector3.up;
            if (move.sqrMagnitude > 0.001f)
            {
                spectatorCamera.transform.position += move.normalized * moveSpeed * Time.deltaTime;
            }
        }

        private static List<ReplayTrack> ParseTracks(string json)
        {
            List<ReplayTrack> result = new List<ReplayTrack>();
            int tracksName = json.IndexOf("\"tracks\"", StringComparison.Ordinal);
            if (tracksName < 0)
            {
                return result;
            }

            int tracksArrayStart = json.IndexOf('[', tracksName);
            if (tracksArrayStart < 0)
            {
                return result;
            }

            int tracksArrayEnd = FindMatching(json, tracksArrayStart, '[', ']');
            if (tracksArrayEnd <= tracksArrayStart)
            {
                return result;
            }

            int cursor = tracksArrayStart + 1;
            while (cursor < tracksArrayEnd)
            {
                int objectStart = json.IndexOf('{', cursor);
                if (objectStart < 0 || objectStart >= tracksArrayEnd)
                {
                    break;
                }

                int objectEnd = FindMatching(json, objectStart, '{', '}');
                if (objectEnd <= objectStart || objectEnd > tracksArrayEnd)
                {
                    break;
                }

                string block = json.Substring(objectStart, objectEnd - objectStart + 1);
                ReplayTrack track = ParseTrackBlock(block);
                if (track != null && track.Points.Count > 0)
                {
                    result.Add(track);
                }

                cursor = objectEnd + 1;
            }

            return result;
        }

        private static List<ReplayTrack> ParseTracksFromCsv(string directory)
        {
            List<ReplayTrack> result = new List<ReplayTrack>();
            if (string.IsNullOrEmpty(directory))
            {
                return result;
            }

            string path = Path.Combine(directory, "unit_moves.csv");
            if (!File.Exists(path))
            {
                return result;
            }

            Dictionary<string, ReplayTrack> byUnit = new Dictionary<string, ReplayTrack>();
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return result;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        float time; float x; float y; float z;
                        if (string.IsNullOrEmpty(unitId) ||
                            !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !TryParseFloat(GetCsvField(fields, columns, "x"), out x) ||
                            !TryParseFloat(GetCsvField(fields, columns, "y"), out y) ||
                            !TryParseFloat(GetCsvField(fields, columns, "z"), out z))
                        {
                            continue;
                        }

                        ReplayTrack track;
                        if (!byUnit.TryGetValue(unitId, out track))
                        {
                            track = new ReplayTrack();
                            track.UnitId = unitId;
                            byUnit[unitId] = track;
                            result.Add(track);
                        }

                        ReplayPoint point = new ReplayPoint(time, new Vector3(x, y, z));
                        float rx; float ry; float rz;
                        if (TryParseFloat(GetCsvField(fields, columns, "rot_x"), out rx) &&
                            TryParseFloat(GetCsvField(fields, columns, "rot_y"), out ry) &&
                            TryParseFloat(GetCsvField(fields, columns, "rot_z"), out rz))
                        {
                            point.Rotation = new Vector3(rx, ry, rz);
                            point.HasRotation = true;
                        }
                        track.Points.Add(point);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ParseTracksFromCsv failed: " + ex.Message);
                result.Clear();
            }

            return result;
        }

        private static ReplayTrack ParseTrackBlock(string block)
        {
            Match unitMatch = Regex.Match(block, "\"unitId\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
            if (!unitMatch.Success)
            {
                return null;
            }

            ReplayTrack track = new ReplayTrack();
            track.UnitId = unitMatch.Groups[1].Value;

            int pointsName = block.IndexOf("\"points\"", StringComparison.Ordinal);
            if (pointsName < 0)
            {
                return track;
            }

            int pointsStart = block.IndexOf('[', pointsName);
            if (pointsStart < 0)
            {
                return track;
            }

            int pointsEnd = FindMatching(block, pointsStart, '[', ']');
            if (pointsEnd <= pointsStart)
            {
                return track;
            }

            int cursor = pointsStart + 1;
            while (cursor < pointsEnd)
            {
                int pointStart = block.IndexOf('{', cursor);
                if (pointStart < 0 || pointStart >= pointsEnd)
                {
                    break;
                }

                int pointEnd = FindMatching(block, pointStart, '{', '}');
                if (pointEnd <= pointStart || pointEnd > pointsEnd)
                {
                    break;
                }

                string pointBlock = block.Substring(pointStart, pointEnd - pointStart + 1);
                ReplayPoint point;
                if (TryParseReplayPoint(pointBlock, out point))
                {
                    track.Points.Add(point);
                }

                cursor = pointEnd + 1;
            }

            return track;
        }

        private static bool TryParseReplayPoint(string block, out ReplayPoint point)
        {
            point = null;
            float t;
            Vector3 position;
            if (!TryExtractJsonFloat(block, "t", out t) || !TryExtractJsonVector(block, "position", out position))
            {
                return false;
            }

            point = new ReplayPoint(t, position);

            Vector3 rotation;
            if (TryExtractJsonVector(block, "rotation", out rotation))
            {
                point.Rotation = rotation;
                point.HasRotation = true;
            }

            Vector3 velocity;
            if (TryExtractJsonVector(block, "localVelocity", out velocity))
            {
                point.LocalVelocity = velocity;
                point.HasLocalVelocity = true;
            }

            point.IsCrouch = TryExtractNullableBool(block, "crouch");
            point.IsJump = TryExtractNullableBool(block, "jump");
            point.IsSprint = TryExtractNullableBool(block, "sprint");
            point.IsWallClimb = TryExtractNullableBool(block, "wallClimb");
            point.IsDash = TryExtractNullableBool(block, "dash");
            point.IsGroundSlam = TryExtractNullableBool(block, "groundSlam");
            point.NoInterpolation = TryExtractNullableBool(block, "noInterpolation");
            return true;
        }

        private static bool TryExtractJsonFloat(string block, string propertyName, out float value)
        {
            value = 0f;
            Match match = Regex.Match(block, "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*([-+0-9.Ee]+)", RegexOptions.CultureInvariant);
            return match.Success && TryParseFloat(match.Groups[1].Value, out value);
        }

        private static bool TryExtractJsonVector(string block, string propertyName, out Vector3 value)
        {
            value = Vector3.zero;
            int property = block.IndexOf("\"" + propertyName + "\"", StringComparison.Ordinal);
            if (property < 0)
            {
                return false;
            }

            int objectStart = block.IndexOf('{', property);
            if (objectStart < 0)
            {
                return false;
            }

            int objectEnd = FindMatching(block, objectStart, '{', '}');
            if (objectEnd <= objectStart)
            {
                return false;
            }

            string vectorBlock = block.Substring(objectStart, objectEnd - objectStart + 1);
            float x;
            float y;
            float z;
            if (!TryExtractJsonFloat(vectorBlock, "x", out x) ||
                !TryExtractJsonFloat(vectorBlock, "y", out y) ||
                !TryExtractJsonFloat(vectorBlock, "z", out z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }

        private static bool? TryExtractNullableBool(string block, string propertyName)
        {
            Match match = Regex.Match(block, "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*(true|false|null)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success || string.Equals(match.Groups[1].Value, "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindMatching(string text, int start, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                {
                    depth++;
                }
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseByte(string value, out byte result)
        {
            return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static float? ParseFloatOrNull(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            float result;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : (float?)null;
        }

        private static ulong? ParseUlongOrNull(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            ulong result;
            return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : (ulong?)null;
        }

        private static uint ParseUint(string value)
        {
            uint result;
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            return result;
        }

        private void CalculateTimeRange()
        {
            startTime = float.MaxValue;
            endTime = float.MinValue;
            for (int i = 0; i < tracks.Count; i++)
            {
                ReplayTrack track = tracks[i];
                if (track.Points.Count == 0)
                {
                    continue;
                }

                startTime = Mathf.Min(startTime, track.Points[0].Time);
                endTime = Mathf.Max(endTime, track.Points[track.Points.Count - 1].Time);
            }

            if (startTime == float.MaxValue)
            {
                startTime = 0f;
                endTime = 0f;
            }
        }

        private void CreateMarkers()
        {
            // Build set of unit IDs that have movement tracks
            Dictionary<string, ReplayTrack> trackById = new Dictionary<string, ReplayTrack>();
            for (int i = 0; i < tracks.Count; i++)
                trackById[tracks[i].UnitId] = tracks[i];

            int colorIndex = 0;

            // First pass: tracked units (heroes + moving devices)
            for (int i = 0; i < tracks.Count; i++)
            {
                ReplayTrack track = tracks[i];
                UnitMetadata metadata;
                unitMetadata.TryGetValue(track.UnitId, out metadata);
                if (!ShouldShowUnit(metadata))
                    continue;

                UnitMarker unitMarker = new UnitMarker();
                unitMarker.UnitId = track.UnitId;
                unitMarker.Track = track;
                unitMarker.Metadata = metadata;
                unitMarker.ColorIndex = colorIndex;
                unitMarker.SpawnTime = metadata != null ? metadata.SpawnTime : float.MinValue;
                float dropTime;
                unitMarker.DropTime = unitDropTimes.TryGetValue(track.UnitId, out dropTime) ? dropTime : float.MaxValue;
                float blockDropTime;
                if (TryFindBlockRemovalDropTime(unitMarker, out blockDropTime))
                {
                    unitMarker.DropTime = Mathf.Min(unitMarker.DropTime, blockDropTime);
                }
                float deathHoldUntil;
                if (deathHoldUntilByUnitId.TryGetValue(track.UnitId, out deathHoldUntil))
                {
                    unitMarker.DropTime = Mathf.Max(unitMarker.DropTime, deathHoldUntil);
                }
                markers.Add(unitMarker);
                colorIndex++;
            }

            // Second pass: static units from unit_creates.csv that have no track (devices, objectives, etc.)
            foreach (KeyValuePair<string, UnitMetadata> kv in unitMetadata)
            {
                string unitId = kv.Key;
                UnitMetadata metadata = kv.Value;
                if (trackById.ContainsKey(unitId)) continue; // already handled above
                if (!metadata.HasSpawnPosition) continue;
                if (!IsDeviceOrObjectiveUnit(metadata)) continue;

                UnitMarker unitMarker = new UnitMarker();
                unitMarker.Track = null;
                unitMarker.Metadata = metadata;
                unitMarker.UnitId = unitId;
                unitMarker.ColorIndex = colorIndex;
                unitMarker.IsStaticUnit = true;
                unitMarker.SpawnTime = metadata.SpawnTime;
                float dropTime;
                unitMarker.DropTime = unitDropTimes.TryGetValue(unitId, out dropTime) ? dropTime : float.MaxValue;
                float blockDropTime;
                if (TryFindBlockRemovalDropTime(unitMarker, out blockDropTime))
                {
                    unitMarker.DropTime = Mathf.Min(unitMarker.DropTime, blockDropTime);
                }
                float deathHoldUntil;
                if (deathHoldUntilByUnitId.TryGetValue(unitId, out deathHoldUntil))
                {
                    unitMarker.DropTime = Mathf.Max(unitMarker.DropTime, deathHoldUntil);
                }
                markers.Add(unitMarker);
                colorIndex++;
            }
        }

        private bool TryFindBlockRemovalDropTime(UnitMarker marker, out float dropTime)
        {
            dropTime = float.MaxValue;
            if (marker == null || marker.Metadata == null || blockTimeline.Count == 0)
            {
                return false;
            }

            string keyName = marker.Metadata.KeyName ?? "";
            if (keyName.IndexOf("device", StringComparison.OrdinalIgnoreCase) < 0 &&
                keyName.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) < 0 &&
                keyName.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            short x;
            short y;
            short z;
            BuildReplayEvent placement;
            if (!string.IsNullOrEmpty(marker.UnitId) &&
                buildPlacementByUnitId.TryGetValue(marker.UnitId, out placement) &&
                placement.HasPosition)
            {
                x = ClampInt16(Mathf.FloorToInt(placement.Position.x));
                y = ClampInt16(Mathf.FloorToInt(placement.Position.y));
                z = ClampInt16(Mathf.FloorToInt(placement.Position.z));
            }
            else if (marker.Metadata.HasSpawnPosition)
            {
                x = ClampInt16(Mathf.FloorToInt(marker.Metadata.SpawnPosition.x));
                y = ClampInt16(Mathf.FloorToInt(marker.Metadata.SpawnPosition.y));
                z = ClampInt16(Mathf.FloorToInt(marker.Metadata.SpawnPosition.z));
            }
            else
            {
                return false;
            }

            float after = marker.SpawnTime + 0.01f;
            for (int i = 0; i < blockTimeline.Count; i++)
            {
                BlockTimelineEvent item = blockTimeline[i];
                if (item.Time <= after || item.X != x || item.Y != y || item.Z != z)
                {
                    continue;
                }

                if (item.Id == 0)
                {
                    dropTime = item.Time;
                    return true;
                }
            }

            float bestDistance = float.MaxValue;
            float bestTime = float.MaxValue;
            for (int i = 0; i < blockTimeline.Count; i++)
            {
                BlockTimelineEvent item = blockTimeline[i];
                if (item.Time <= after || item.Id != 0)
                {
                    continue;
                }

                float dx = item.X - x;
                float dy = item.Y - y;
                float dz = item.Z - z;
                float distance = dx * dx + dy * dy + dz * dz;
                if (distance <= 3.0f && (item.Time < bestTime || distance < bestDistance))
                {
                    bestDistance = distance;
                    bestTime = item.Time;
                }
            }

            if (bestTime < float.MaxValue)
            {
                dropTime = bestTime;
                return true;
            }

            return false;
        }

        private void EnsureReplayObjectSpawned(UnitMarker marker)
        {
            if (marker.GameObject != null)
            {
                return;
            }

            if (marker.IsStaticUnit)
            {
                SpawnStaticReplayObject(marker);
                return;
            }

            SpawnTrackedReplayObject(marker);
        }

        private void SpawnTrackedReplayObject(UnitMarker marker)
        {
            ReplayTrack track = marker.Track;
            UnitMetadata metadata = marker.Metadata;
            if (track == null)
            {
                return;
            }

            Unit realUnit = TryCreateReplayUnit(track, metadata);
            GameObject go = null;
            if (realUnit != null)
            {
                go = realUnit.gameObject;
            }
            else if (Application.loadedLevelName == "Zone" && metadata != null)
            {
                // Try to spawn the visual prefab for non-hero tracked units (projectiles, moving devices)
                go = SpawnNonHeroUnit(track.UnitId, metadata);
            }
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "BNL_ReplayUnit_" + track.UnitId;
                go.transform.localScale = Vector3.one * MarkerScale(metadata);
                DontDestroyOnLoad(go);
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Diffuse"));
                    renderer.material.color = ColorForUnit(metadata, marker.ColorIndex);
                }
            }

            TextMesh text = showDebugWorldLabels ? AttachReplayLabel(go, "BNL_ReplayUnitLabel_" + track.UnitId, LabelForUnit(track.UnitId, metadata), realUnit == null ? 1.1f : 2.2f, 0.28f) : null;
            marker.GameObject = go;
            marker.RealUnit = realUnit;
            marker.Label = text;
            marker.LastEventTime = float.MinValue;
        }

        private void SpawnStaticReplayObject(UnitMarker marker)
        {
            UnitMetadata metadata = marker.Metadata;
            if (metadata == null)
            {
                return;
            }

            string unitId = string.IsNullOrEmpty(marker.UnitId) ? metadata.UnitId : marker.UnitId;
            GameObject go = null;
            if (Application.loadedLevelName == "Zone")
            {
                Unit realUnit = TryCreateObjectiveReplayUnit(unitId, metadata);
                if (realUnit != null)
                {
                    go = realUnit.gameObject;
                    marker.RealUnit = realUnit;
                }
                else
                {
                    go = SpawnNonHeroUnit(unitId, metadata);
                }
            }

            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "BNL_ReplayStaticUnit_" + unitId;
                go.transform.localScale = Vector3.one * 0.55f;
                DontDestroyOnLoad(go);

                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Diffuse"));
                    renderer.material.color = ColorForStaticUnit(metadata);
                }
            }

            BuildReplayEvent placement;
            if (buildPlacementByUnitId.TryGetValue(unitId, out placement) && placement.HasPosition)
            {
                go.transform.position = GetPlacementPosition(placement);
                Quaternion rotation;
                if (TryGetPlacementRotation(placement, out rotation))
                {
                    go.transform.rotation = rotation;
                }
            }
            else
            {
                go.transform.position = metadata.SpawnPosition;
            }
            marker.GameObject = go;
            marker.Label = showDebugWorldLabels ? AttachReplayLabel(go, "BNL_ReplayStaticUnitLabel_" + unitId, LabelForUnit(unitId, metadata), 1.2f, 0.18f) : null;
        }

        private static TextMesh AttachReplayLabel(GameObject parent, string name, string textValue, float yOffset, float characterSize)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.parent = parent.transform;
            labelObject.transform.localPosition = new Vector3(0f, yOffset, 0f);
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = textValue;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            return text;
        }

        private Unit TryCreateReplayUnit(ReplayTrack track, UnitMetadata metadata)
        {
            if (!IsPlayerUnit(metadata) || Application.loadedLevelName != "Zone")
            {
                return null;
            }

            try
            {
                Vector3 spawnPos = metadata.HasSpawnPosition ? metadata.SpawnPosition : Sample(track, replayTime);
                Unit unit = SpawnHeroUnitDirect(track.UnitId, metadata, spawnPos);
                if (unit != null)
                {
                    RegisterReplayUnitWithGameSystems(unit, metadata, spawnPos);
                    Debug.Log("[BNL Replay] Spawned hero unit " + track.UnitId + " " + metadata.KeyName);
                    return unit;
                }
            }
            catch (Exception ex)
            {
                if (!realUnitSpawnWarningShown)
                {
                    realUnitSpawnWarningShown = true;
                    Debug.Log("[BNL Replay] Hero unit spawn failed; falling back to markers: " + ex.Message);
                }
            }

            return null;
        }

        private Unit SpawnHeroUnitDirect(string unitIdStr, UnitMetadata metadata, Vector3 position)
        {
            uint unitId;
            if (!uint.TryParse(unitIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId))
            {
                return null;
            }

            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null)
            {
                return null;
            }

            Unit existing = registry.Get(unitId);
            if (existing != null)
            {
                try { registry.DropUnit(unitId); } catch { }
            }

            // Load the base hero unit prefab — used for all non-local player heroes
            GameObject prefab = Resources.Load("Prefabs/UnitType/Unit") as GameObject;
            if (prefab == null)
            {
                Debug.Log("[BNL Replay] Prefabs/UnitType/Unit not found");
                return null;
            }

            GameObject unitGo = UnityEngine.Object.Instantiate(prefab) as GameObject;
            if (unitGo != null)
            {
                unitGo.transform.position = position;
                unitGo.transform.rotation = Quaternion.identity;
            }
            if (unitGo == null)
            {
                return null;
            }

            unitGo.name = "BNL_ReplayHero_" + unitIdStr;
            DontDestroyOnLoad(unitGo);

            Unit unit = unitGo.GetComponent<Unit>();
            if (unit == null)
            {
                UnityEngine.Object.Destroy(unitGo);
                return null;
            }

            unit.Id = unitId;
            unit.Team = TeamFromString(metadata.Team);
            unit.Controlled = false;
            uint playerId;
            if (uint.TryParse(metadata.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
            {
                unit.PlayerId = playerId;
            }

            uint keyHash;
            if (TryParseHexUInt(metadata.KeyHash, out keyHash))
            {
                unit.Key = KeyFromHash(keyHash);
            }

            // Resolve gear info from couchdb (avoids Catalogue.GetCard<CardGear> which fails in spectator)
            List<Key> gearKeys = ParseGearKeys(metadata.GearKeyHashes);
            string[] gearHashes = string.IsNullOrEmpty(metadata.GearKeyHashes) ? new string[0] : metadata.GearKeyHashes.Split('|');

            // Attach the skin model — mirrors UnitsRegistry.CreateUnitModel
            uint skinHash;
            Protocol.CardSkin skinCard = null;
            if (TryParseHexUInt(metadata.SkinKeyHash, out skinHash))
            {
                unit.SkinKey = KeyFromHash(skinHash);
                skinCard = FindSkinCard(skinHash);
            }
            if (object.ReferenceEquals(skinCard, null) && keyHash != 0)
            {
                skinCard = FindDefaultSkinCardInCatalogue(keyHash);
                if (object.ReferenceEquals(skinCard, null))
                    skinCard = FindDefaultSkinCardInCouchdb(keyHash, metadata.KeyName);
            }
            if (!object.ReferenceEquals(skinCard, null))
            {
                TryAttachSkinModel(unit, skinCard, unitGo);
            }
            else
            {
                Debug.Log("[BNL Replay] No skin card found for " + metadata.KeyName);
            }

            UnitMotor motor = unitGo.GetComponent<UnitMotor>();
            if (motor != null)
            {
                motor.AutoUpdateLerpBuffer = false;
                motor.enabled = false;
                motor.RotationYOnly = true;
                // Prime the interpolator's Last with the actual spawn position.
                // Awake() captures transform.position before we reposition the GO,
                // so Last.Position defaults to the prefab origin (sky or 0,0,0).
                // Without this, UnitMotor.Update returns Last until Move() builds 2 entries.
                try
                {
                    FieldInfo interpField = typeof(UnitMotor).GetField("interpolator",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (!object.ReferenceEquals(interpField, null))
                    {
                        MoveUpdateInterpolator interp = interpField.GetValue(motor) as MoveUpdateInterpolator;
                        if (!object.ReferenceEquals(interp, null))
                        {
                            MoveUpdate primed = interp.Last;
                            primed.Position = position;
                            primed.Rotation = Quaternion.identity;
                            interp.Last = primed;
                        }
                    }
                }
                catch { }
            }

            Rigidbody body = unitGo.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            RegisterUnitInRegistry(unit);

            // Broadcast OnUnitCreate so UnitAnimation.OnUnitCreate sets its unit/motor references
            try
            {
                unitGo.BroadcastMessage("SendMessage", new object[] { "OnUnitCreate", unit });
            }
            catch { }
            BroadcastOnUnitCreate(unitGo, unit);

            // Equip the first gear slot so UnitAnimation switches from lobby → movement animations.
            // We bypass HandleSwitchGear (which calls Catalogue.GetCard<CardGear>) by using couchdb directly.
            if (gearHashes.Length > 0)
            {
                uint firstHash;
                if (TryParseHexUInt(gearHashes[0], out firstHash))
                {
                    TrySwitchGearDirect(unitGo, 0, firstHash);
                }
            }

            return unit;
        }

        private static void RegisterUnitInRegistry(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            try
            {
                UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
                if (registry == null)
                {
                    return;
                }

                FieldInfo field = typeof(UnitsRegistry).GetField("units", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (object.ReferenceEquals(field, null))
                {
                    return;
                }

                Dictionary<uint, Unit> units = field.GetValue(registry) as Dictionary<uint, Unit>;
                if (object.ReferenceEquals(units, null))
                {
                    units = new Dictionary<uint, Unit>();
                    field.SetValue(registry, units);
                }

                units[unit.Id] = unit;
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] RegisterUnitInRegistry failed: " + ex.Message);
            }
        }

        private Unit TryCreateObjectiveReplayUnit(string unitIdStr, UnitMetadata metadata)
        {
            if (!IsObjectiveUiUnit(metadata))
            {
                return null;
            }

            try
            {
                uint unitId;
                if (!uint.TryParse(unitIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId))
                {
                    return null;
                }

                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
                if (listener == null || registry == null)
                {
                    return null;
                }

                Unit existing = registry.Get(unitId);
                if (existing != null)
                {
                    try { registry.DropUnit(unitId); } catch { }
                }

                Vector3 spawnPos = metadata.HasSpawnPosition ? metadata.SpawnPosition : Vector3.zero;
                Protocol.UnitInit init = BuildUnitInit(metadata, spawnPos, false);
                listener.UnitCreate(unitId, init);

                Unit unit = registry.Get(unitId);
                if (unit == null)
                {
                    return null;
                }

                unit.transform.position = spawnPos + new Vector3(0.5f, 0.5f, 0.5f);
                Debug.Log("[BNL Replay] Spawned objective unit through game systems " + unitIdStr + " " + metadata.KeyName);
                return unit;
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TryCreateObjectiveReplayUnit failed for " + metadata.KeyName + ": " + ex.Message);
                return null;
            }
        }

        private void RegisterReplayUnitWithGameSystems(Unit unit, UnitMetadata metadata, Vector3 position)
        {
            if (unit == null || metadata == null)
            {
                return;
            }

            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger != null)
                {
                    messenger.OnUnitsCreate(unit);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ZoneMessenger.OnUnitsCreate failed: " + ex.Message);
            }

            uint playerId;
            if (uint.TryParse(metadata.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
            {
                RegisterReplayPlayerNamesWithGameSystems();
                try
                {
                    ZonePlayersCache playersCache = Singleton<ZonePlayersCache>.Instance;
                    if (playersCache != null)
                    {
                        playersCache.OnPlayerUnitCreate(playerId, unit.Id, BuildUnitInit(metadata, position, true));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] ZonePlayersCache.OnPlayerUnitCreate failed: " + ex.Message);
                }
            }
        }

        private void RegisterReplayPlayerNamesWithGameSystems()
        {
            if (replayPlayers.Count == 0)
            {
                return;
            }

            try
            {
                ZonePlayersCache playersCache = Singleton<ZonePlayersCache>.Instance;
                if (playersCache == null)
                {
                    return;
                }

                Dictionary<uint, Protocol.ZonePlayerInfo> infoByPlayer = new Dictionary<uint, Protocol.ZonePlayerInfo>();
                for (int i = 0; i < replayPlayers.Count; i++)
                {
                    PlayerReplayInfo player = replayPlayers[i];
                    uint playerId;
                    if (!uint.TryParse(player.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
                    {
                        continue;
                    }

                    Protocol.ZonePlayerInfo info = new Protocol.ZonePlayerInfo();
                    info.Nickname = string.IsNullOrEmpty(player.Nickname) ? ("Player " + player.PlayerId) : player.Nickname;
                    ulong steamId;
                    if (ulong.TryParse(player.SteamId, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
                    {
                        info.SteamId = steamId;
                    }
                    infoByPlayer[playerId] = info;
                }

                if (infoByPlayer.Count > 0)
                {
                    playersCache.OnZoneDataUpdate(infoByPlayer);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ZonePlayersCache.OnZoneDataUpdate failed: " + ex.Message);
            }
        }

        private Protocol.UnitInit BuildUnitInit(UnitMetadata metadata, Vector3 position, bool includePlayerId)
        {
            Protocol.UnitInit init = new Protocol.UnitInit();
            uint keyHash;
            if (TryParseHexUInt(metadata.KeyHash, out keyHash))
            {
                init.Key = KeyFromHash(keyHash);
            }
            init.Team = TeamFromString(metadata.Team);
            init.Controlled = false;
            init.Transform = BuildZoneTransform(position);

            uint ownerId;
            if (uint.TryParse(metadata.OwnerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out ownerId))
            {
                init.OwnerId = ownerId;
            }

            uint playerId;
            if (includePlayerId && uint.TryParse(metadata.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
            {
                init.PlayerId = playerId;
            }

            uint skinHash;
            if (TryParseHexUInt(metadata.SkinKeyHash, out skinHash))
            {
                init.SkinKey = KeyFromHash(skinHash);
            }

            List<Key> gearKeys = ParseGearKeys(metadata.GearKeyHashes);
            if (gearKeys.Count > 0)
            {
                init.Gears = gearKeys;
            }

            return init;
        }

        private static Protocol.ZoneTransform BuildZoneTransform(Vector3 position)
        {
            Protocol.ZoneTransform transform = new Protocol.ZoneTransform();
            transform.Position = position;
            transform.Rotation = Vector3s.zero;
            transform.LocalVelocity = Vector3s.zero;
            transform.NoInterpolation = true;
            return transform;
        }

        private static void TryAttachSkinModel(Unit unit, Protocol.CardSkin skinCard, GameObject unitGo)
        {
            try
            {
                if (!string.IsNullOrEmpty(skinCard.Bundle))
                    CommonAssets.LoadBundle(skinCard.Bundle);

                AssetCache assetCache = Singleton<AssetCache>.Instance;
                if (object.ReferenceEquals(assetCache, null)) { Debug.Log("[BNL Replay] TryAttachSkinModel: no AssetCache"); return; }
                if (string.IsNullOrEmpty(skinCard.Prefab)) { Debug.Log("[BNL Replay] TryAttachSkinModel: no prefab on skin card"); return; }

                GameObject skinPrefab = assetCache.LoadPrefab(skinCard.Prefab);
                if (object.ReferenceEquals(skinPrefab, null)) { Debug.Log("[BNL Replay] TryAttachSkinModel: prefab not loaded: " + skinCard.Prefab); return; }

                // Offset matches UnitsRegistry.CreateUnitModel for non-local players (y = -0.08)
                Vector3 offset = new Vector3(0f, -0.08f, 0f);
                GameObject skinGo = UnityEngine.Object.Instantiate(skinPrefab, unitGo.transform.TransformPoint(offset), unitGo.transform.rotation) as GameObject;
                if (object.ReferenceEquals(skinGo, null)) { return; }
                skinGo.transform.parent = unitGo.transform;

                // Copy collider/rigidbody to unit root, as the game does
                if (skinGo.GetComponent<CapsuleCollider>() != null)
                {
                    CapsuleCollider src = skinGo.GetComponent<CapsuleCollider>();
                    CapsuleCollider dst = unitGo.AddComponent<CapsuleCollider>();
                    dst.center = src.center; dst.radius = src.radius;
                    dst.height = src.height; dst.direction = src.direction; dst.isTrigger = src.isTrigger;
                    UnityEngine.Object.DestroyImmediate(src);
                }
                else if (skinGo.GetComponent<Rigidbody>() != null)
                {
                    Rigidbody src = skinGo.GetComponent<Rigidbody>();
                    Rigidbody dst = unitGo.AddComponent<Rigidbody>();
                    dst.isKinematic = src.isKinematic; dst.useGravity = src.useGravity;
                    dst.mass = src.mass; dst.interpolation = src.interpolation;
                    UnityEngine.Object.DestroyImmediate(src);
                }

                Debug.Log("[BNL Replay] Attached skin model " + skinCard.Prefab + " to unit " + unit.Id);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TryAttachSkinModel failed: " + ex.Message);
            }
        }

        private static void BroadcastOnUnitCreate(GameObject unitGo, Unit unit)
        {
            try
            {
                MessagesHandlerBehaviour[] handlers = unitGo.GetComponentsInChildren<MessagesHandlerBehaviour>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].OnUnitCreate(unit); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] BroadcastOnUnitCreate failed: " + ex.Message);
            }
        }

        private static void TryApplyHeroSkinByHash(Unit unit, uint skinHash)
        {
            Protocol.CardSkin skinCard = FindSkinCard(skinHash);
            if (object.ReferenceEquals(skinCard, null))
            {
                Debug.Log("[BNL Replay] Skin card not found for hash " + skinHash.ToString("X8"));
                return;
            }
            TryApplyHeroSkinCard(unit, skinCard);
        }

        private static void TryApplyDefaultHeroSkin(Unit unit, uint heroKeyHash, string heroKeyName)
        {
            try
            {
                // Strategy 1: find a matching CardSkin in the live catalogue via All property
                Protocol.CardSkin skinCard = FindDefaultSkinCardInCatalogue(heroKeyHash);
                if (!object.ReferenceEquals(skinCard, null))
                {
                    Debug.Log("[BNL Replay] Found default skin in catalogue: " + skinCard.Id + " prefab=" + skinCard.Prefab + " bundle=" + skinCard.Bundle);
                    TryApplyHeroSkinCard(unit, skinCard);
                    return;
                }

                // Strategy 2: parse couchdb JSON resource (hero_key field links skins to heroes)
                skinCard = FindDefaultSkinCardInCouchdb(heroKeyHash, heroKeyName);
                if (!object.ReferenceEquals(skinCard, null))
                {
                    Debug.Log("[BNL Replay] Found default skin in couchdb: " + (skinCard.Id ?? "?") + " prefab=" + skinCard.Prefab + " bundle=" + skinCard.Bundle);
                    TryApplyHeroSkinCard(unit, skinCard);
                    return;
                }

                Debug.Log("[BNL Replay] No default skin found for hero " + heroKeyHash.ToString("X8") + " " + heroKeyName);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TryApplyDefaultHeroSkin failed: " + ex.Message);
            }
        }

        private static void TryApplyHeroSkinCard(Unit unit, Protocol.CardSkin skinCard)
        {
            try
            {
                if (!string.IsNullOrEmpty(skinCard.Bundle))
                {
                    CommonAssets.LoadBundle(skinCard.Bundle);
                }

                AssetCache assetCache = Singleton<AssetCache>.Instance;
                if (object.ReferenceEquals(assetCache, null))
                {
                    Debug.Log("[BNL Replay] TryApplyHeroSkinCard: AssetCache not available");
                    return;
                }

                if (string.IsNullOrEmpty(skinCard.Prefab))
                {
                    Debug.Log("[BNL Replay] TryApplyHeroSkinCard: skin card has no prefab");
                    return;
                }

                GameObject skinPrefab = assetCache.LoadPrefab(skinCard.Prefab);
                if (object.ReferenceEquals(skinPrefab, null))
                {
                    Debug.Log("[BNL Replay] TryApplyHeroSkinCard: prefab not loaded: " + skinCard.Prefab);
                    return;
                }

                GameObject skinGo = UnityEngine.Object.Instantiate(skinPrefab) as GameObject;
                if (object.ReferenceEquals(skinGo, null)) return;

                skinGo.transform.parent = unit.transform;
                skinGo.transform.localPosition = Vector3.zero;
                skinGo.transform.localRotation = Quaternion.identity;
                skinGo.transform.localScale = Vector3.one;
                Debug.Log("[BNL Replay] Applied default skin " + skinCard.Prefab + " to unit " + unit.Id);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TryApplyHeroSkinCard failed: " + ex.Message);
            }
        }

        private static Protocol.CardSkin FindDefaultSkinCardInCatalogue(uint heroKeyHash)
        {
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (object.ReferenceEquals(catalogue, null)) return null;
                Key heroKey = KeyFromHash(heroKeyHash);
                System.Collections.Generic.IEnumerable<Protocol.Card> all = catalogue.All;
                if (object.ReferenceEquals(all, null)) return null;
                foreach (Protocol.Card card in all)
                {
                    Protocol.CardSkin skin = card as Protocol.CardSkin;
                    if (object.ReferenceEquals(skin, null)) continue;
                    if (skin.HeroKey == heroKey && !string.IsNullOrEmpty(skin.Prefab))
                        return skin;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] FindDefaultSkinCardInCatalogue: " + ex.Message);
            }
            return null;
        }

        private static Protocol.CardSkin FindDefaultSkinCardInCouchdb(uint heroKeyHash, string heroKeyName)
        {
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (object.ReferenceEquals(textAsset, null)) return null;

                string json = textAsset.text;
                // CardSkin docs have "hero_key":"<decimal>" linking them to their hero
                string heroDecimal = heroKeyHash.ToString(System.Globalization.CultureInfo.InvariantCulture);

                System.Text.RegularExpressions.MatchCollection heroMatches =
                    System.Text.RegularExpressions.Regex.Matches(
                        json,
                        "\"hero_key\"\\s*:\\s*\"?" + System.Text.RegularExpressions.Regex.Escape(heroDecimal) + "\"?",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                Protocol.CardSkin best = null;
                for (int i = 0; i < heroMatches.Count; i++)
                {
                    System.Text.RegularExpressions.Match heroMatch = heroMatches[i];
                    int docStart = json.LastIndexOf('{', heroMatch.Index);
                    if (docStart < 0) continue;
                    int docEnd = json.IndexOf("\"_id\"", heroMatch.Index + 1, StringComparison.Ordinal);
                    if (docEnd < 0) docEnd = json.Length;
                    string docSlice = json.Substring(docStart, docEnd - docStart);

                    System.Text.RegularExpressions.Match prefabMatch = System.Text.RegularExpressions.Regex.Match(
                        docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!prefabMatch.Success) continue;

                    System.Text.RegularExpressions.Match bundleMatch = System.Text.RegularExpressions.Regex.Match(
                        docSlice, "\"bundle\"\\s*:\\s*\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    System.Text.RegularExpressions.Match idMatch = System.Text.RegularExpressions.Regex.Match(
                        docSlice, "\"_id\"\\s*:\\s*\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                    Protocol.CardSkin skin = new Protocol.CardSkin();
                    skin.Prefab = prefabMatch.Groups[1].Value;
                    if (bundleMatch.Success) skin.Bundle = bundleMatch.Groups[1].Value;
                    if (idMatch.Success) skin.Id = idMatch.Groups[1].Value;

                    // Prefer "default" scope skins
                    System.Text.RegularExpressions.Match scopeMatch = System.Text.RegularExpressions.Regex.Match(
                        docSlice, "\"scope\"\\s*:\\s*\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                    bool isDefault = !scopeMatch.Success ||
                        string.Equals(scopeMatch.Groups[1].Value, "default", StringComparison.OrdinalIgnoreCase);

                    if (isDefault) return skin;
                    if (object.ReferenceEquals(best, null)) best = skin;
                }

                return best;
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] FindDefaultSkinCardInCouchdb: " + ex.Message);
            }
            return null;
        }

        private void TrySwitchGearDirect(GameObject unitGo, int gearIndex, uint gearKeyHash)
        {
            try
            {
                GearInfo info = FindOrCacheGearInfo(gearKeyHash);
                if (info == null || string.IsNullOrEmpty(info.GearTag))
                {
                    Debug.Log("[BNL Replay] TrySwitchGearDirect: no gear info for hash " + gearKeyHash.ToString("X8"));
                    return;
                }

                SwitchGearEventArgs args = new SwitchGearEventArgs();
                args.IsPlayer = false;
                args.NewGearIndex = gearIndex;
                args.DropTime = 0f;
                args.PickupTime = 0f;
                args.GearTag = info.GearTag;
                args.AnimationTag = info.AnimationTag ?? string.Empty;

                Debug.Log("[BNL Replay] SwitchGear gearIndex=" + gearIndex + " gearTag=" + info.GearTag);

                MessagesHandlerBehaviour[] handlers = unitGo.GetComponentsInChildren<MessagesHandlerBehaviour>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].OnUnitSwitchGear(args); } catch (Exception hEx) { Debug.Log("[BNL Replay] SwitchGear handler " + handlers[i].GetType().Name + " threw: " + hEx.Message); }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TrySwitchGearDirect failed: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private GearInfo FindOrCacheGearInfo(uint gearKeyHash)
        {
            GearInfo cached;
            if (gearInfoCache.TryGetValue(gearKeyHash, out cached))
                return cached;

            GearInfo info = FindGearInfoFromCatalogue(gearKeyHash);
            if (info == null)
                info = FindGearInfoFromCouchdb(gearKeyHash);

            gearInfoCache[gearKeyHash] = info;
            return info;
        }

        private static GearInfo FindGearInfoFromCatalogue(uint gearKeyHash)
        {
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (object.ReferenceEquals(catalogue, null)) return null;
                Key gearKey = KeyFromHash(gearKeyHash);
                System.Collections.Generic.IEnumerable<Protocol.Card> all = catalogue.All;
                if (object.ReferenceEquals(all, null)) return null;
                foreach (Protocol.Card card in all)
                {
                    Protocol.CardGear gear = card as Protocol.CardGear;
                    if (object.ReferenceEquals(gear, null)) continue;
                    if (gear.Key == gearKey && !string.IsNullOrEmpty(gear.Prefab))
                        return new GearInfo(gear.Prefab, gear.AnimationTag ?? string.Empty);
                }
            }
            catch { }
            return null;
        }

        private static GearInfo FindGearInfoFromCouchdb(uint gearKeyHash)
        {
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (object.ReferenceEquals(textAsset, null)) return null;

                string json = textAsset.text;
                string keyDecimal = gearKeyHash.ToString(System.Globalization.CultureInfo.InvariantCulture);

                System.Text.RegularExpressions.Match docMatch = System.Text.RegularExpressions.Regex.Match(
                    json, "\"_id\"\\s*:\\s*\"" + keyDecimal + "\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (!docMatch.Success) return null;

                int docEnd = json.IndexOf("\"_id\"", docMatch.Index + 1, StringComparison.Ordinal);
                if (docEnd < 0) docEnd = json.Length;
                string docSlice = json.Substring(docMatch.Index, docEnd - docMatch.Index);

                System.Text.RegularExpressions.Match prefabMatch = System.Text.RegularExpressions.Regex.Match(
                    docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!prefabMatch.Success) return null;

                System.Text.RegularExpressions.Match animTagMatch = System.Text.RegularExpressions.Regex.Match(
                    docSlice, "\"animation_tag\"\\s*:\\s*\"([^\"]+)\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                string gearTag = prefabMatch.Groups[1].Value;
                string animTag = animTagMatch.Success ? animTagMatch.Groups[1].Value : string.Empty;
                return new GearInfo(gearTag, animTag);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] FindGearInfoFromCouchdb: " + ex.Message);
            }
            return null;
        }

        private UnitCardInfo FindOrCacheUnitCard(uint keyHash)
        {
            UnitCardInfo cached;
            if (unitCardCache.TryGetValue(keyHash, out cached)) return cached;
            UnitCardInfo info = FindUnitCardFromCatalogue(keyHash) ?? FindUnitCardFromCouchdb(keyHash);
            unitCardCache[keyHash] = info;
            return info;
        }

        private static UnitCardInfo FindUnitCardFromCatalogue(uint keyHash)
        {
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (object.ReferenceEquals(catalogue, null)) return null;
                Key key = KeyFromHash(keyHash);
                foreach (Protocol.Card card in catalogue.All)
                {
                    Protocol.CardUnit unit = card as Protocol.CardUnit;
                    if (object.ReferenceEquals(unit, null)) continue;
                    if (unit.Key == key)
                        return new UnitCardInfo(unit.Prefab, unit.Data != null ? unit.Data.Type.ToString() : "Common", unit.IsDropPoint);
                }
            }
            catch { }
            return null;
        }

        private static UnitCardInfo FindUnitCardFromCouchdb(uint keyHash)
        {
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (object.ReferenceEquals(textAsset, null)) return null;
                string json = textAsset.text;
                string keyDecimal = keyHash.ToString(System.Globalization.CultureInfo.InvariantCulture);

                System.Text.RegularExpressions.Match docMatch = System.Text.RegularExpressions.Regex.Match(
                    json, "\"_id\"\\s*:\\s*\"" + keyDecimal + "\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (!docMatch.Success) return null;

                int docEnd = json.IndexOf("\"_id\"", docMatch.Index + 1, StringComparison.Ordinal);
                if (docEnd < 0) docEnd = json.Length;
                string docSlice = json.Substring(docMatch.Index, docEnd - docMatch.Index);

                System.Text.RegularExpressions.Match prefabMatch = System.Text.RegularExpressions.Regex.Match(
                    docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // unit type is nested: "data": { "type": "Common" }
                System.Text.RegularExpressions.Match typeMatch = System.Text.RegularExpressions.Regex.Match(
                    docSlice, "\"data\"\\s*:\\s*\\{[^}]*\"type\"\\s*:\\s*\"([^\"]+)\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!typeMatch.Success)
                    typeMatch = System.Text.RegularExpressions.Regex.Match(
                        docSlice, "\"type\"\\s*:\\s*\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                bool isDropPoint = docSlice.IndexOf("\"is_drop_point\"\\s*:\\s*true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   System.Text.RegularExpressions.Regex.IsMatch(docSlice, "\"is_drop_point\"\\s*:\\s*true", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                string prefab = prefabMatch.Success ? prefabMatch.Groups[1].Value : null;
                string unitType = typeMatch.Success ? typeMatch.Groups[1].Value : "Common";
                return new UnitCardInfo(prefab, unitType, isDropPoint);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] FindUnitCardFromCouchdb: " + ex.Message);
            }
            return null;
        }

        private GameObject SpawnNonHeroUnit(string unitIdStr, UnitMetadata metadata)
        {
            try
            {
                uint keyHash;
                if (!TryParseHexUInt(metadata.KeyHash, out keyHash)) return null;

                UnitCardInfo cardInfo = FindOrCacheUnitCard(keyHash);
                if (cardInfo == null || cardInfo.IsDropPoint) return null;
                string prefabPath = !string.IsNullOrEmpty(cardInfo.Prefab) ? cardInfo.Prefab : FindSpecialUnitPrefab(metadata.KeyName);
                if (string.IsNullOrEmpty(prefabPath)) return null;

                // Spawn only the visual model prefab — no Unit container, no OnUnitCreate broadcast.
                // This avoids any game-logic components interfering with the hero or other systems.
                AssetCache assetCache = Singleton<AssetCache>.Instance;
                if (object.ReferenceEquals(assetCache, null)) return null;

                GameObject modelPrefab = null;
                try { modelPrefab = LoadPrefabWithFallbacks(assetCache, prefabPath); } catch { }
                if (object.ReferenceEquals(modelPrefab, null))
                {
                    Debug.Log("[BNL Replay] Model prefab not found for " + metadata.KeyName + ": " + prefabPath);
                    return null;
                }

                Vector3 pos = metadata.HasSpawnPosition ? metadata.SpawnPosition : Vector3.zero;
                GameObject go = UnityEngine.Object.Instantiate(modelPrefab, pos, Quaternion.identity) as GameObject;
                if (object.ReferenceEquals(go, null)) return null;

                go.name = "BNL_ReplayUnit_" + unitIdStr;
                DontDestroyOnLoad(go);

                // Disable all physics so the model doesn't interact with anything
                Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < rbs.Length; i++) rbs[i].isKinematic = true;
                Collider[] cols = go.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
                DisableReplayLogicComponents(go);

                Debug.Log("[BNL Replay] Spawned unit visual " + metadata.KeyName + " prefab=" + prefabPath);
                return go;
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] SpawnNonHeroUnit failed for " + metadata.KeyName + ": " + ex.Message);
                return null;
            }
        }

        private static void DisableReplayLogicComponents(GameObject go)
        {
            if (object.ReferenceEquals(go, null))
            {
                return;
            }

            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (object.ReferenceEquals(behaviour, null))
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "UnitMotor" ||
                    typeName == "DeviceFallingMovement" ||
                    typeName == "ProjectileMovement" ||
                    typeName == "UnitControl")
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static string FindSpecialUnitPrefab(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return "";
            }

            if (keyName.IndexOf("smoketrap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                keyName.IndexOf("smoke_trap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "assets/prefabs/unitseffects/ninjasmoketrap.prefab";
            }

            return "";
        }

        private static GameObject LoadPrefabWithFallbacks(AssetCache assetCache, string prefabPath)
        {
            if (assetCache == null || string.IsNullOrEmpty(prefabPath))
            {
                return null;
            }

            string[] candidates = new string[]
            {
                prefabPath,
                NormalizeAssetPrefabPath(prefabPath),
                ToUnityPrefabPath(prefabPath)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i]))
                {
                    continue;
                }

                try
                {
                    GameObject prefab = assetCache.LoadPrefab(candidates[i]);
                    if (prefab != null)
                    {
                        return prefab;
                    }
                }
                catch { }
            }

            return null;
        }

        private static string NormalizeAssetPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            if (path.StartsWith("assets/prefabs/", StringComparison.OrdinalIgnoreCase))
            {
                string trimmed = path.Substring("assets/prefabs/".Length);
                if (trimmed.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - ".prefab".Length);
                }
                return "Prefabs/" + trimmed;
            }

            return "";
        }

        private static string ToUnityPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(0, path.Length - ".prefab".Length);
            }

            return "";
        }

        private int FindGearIndexByHash(string unitId, uint gearKeyHash)
        {
            UnitMetadata meta;
            if (!unitMetadata.TryGetValue(unitId, out meta) || string.IsNullOrEmpty(meta.GearKeyHashes))
                return 0;
            string[] parts = meta.GearKeyHashes.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                uint h;
                if (TryParseHexUInt(parts[i], out h) && h == gearKeyHash)
                    return i;
            }
            return 0;
        }

        private static Protocol.CardSkin FindSkinCard(uint skinHash)
        {
            // Try the live catalogue first (iterate keys to avoid IDictionary hash mismatch)
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (!object.ReferenceEquals(catalogue, null))
                {
                    Key skinKey = KeyFromHash(skinHash);
                    System.Collections.Generic.IEnumerable<Protocol.Card> all = catalogue.All;
                    if (!object.ReferenceEquals(all, null))
                    {
                        foreach (Protocol.Card card in all)
                        {
                            Protocol.CardSkin skin = card as Protocol.CardSkin;
                            if (!object.ReferenceEquals(skin, null) && skin.Key == skinKey && !string.IsNullOrEmpty(skin.Prefab))
                                return skin;
                        }
                    }
                }
            }
            catch { }

            // Fall back to parsing the offline couchdb resource
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (!object.ReferenceEquals(textAsset, null))
                {
                    string json = textAsset.text;
                    string keyDecimal = skinHash.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string pattern = "\"_id\"\\s*:\\s*\"" + keyDecimal + "\"";
                    System.Text.RegularExpressions.Match docMatch = System.Text.RegularExpressions.Regex.Match(
                        json, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                    if (docMatch.Success)
                    {
                        int docEnd = json.IndexOf("\"_id\"", docMatch.Index + 1, StringComparison.Ordinal);
                        if (docEnd < 0) docEnd = json.Length;
                        string docSlice = json.Substring(docMatch.Index, docEnd - docMatch.Index);

                        System.Text.RegularExpressions.Match prefabMatch = System.Text.RegularExpressions.Regex.Match(
                            docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"",
                            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (!prefabMatch.Success) return null;

                        System.Text.RegularExpressions.Match bundleMatch = System.Text.RegularExpressions.Regex.Match(
                            docSlice, "\"bundle\"\\s*:\\s*\"([^\"]+)\"",
                            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        Protocol.CardSkin skin = new Protocol.CardSkin();
                        skin.Prefab = prefabMatch.Groups[1].Value;
                        if (bundleMatch.Success) skin.Bundle = bundleMatch.Groups[1].Value;
                        skin.Id = keyDecimal;
                        return skin;
                    }
                }
            }
            catch { }

            return null;
        }

        private void CycleDisplayMode()
        {
            displayMode = (displayMode + 1) % 3;
            if (!loaded)
            {
                ShowStatus("Replay display mode: " + DisplayModeName());
                return;
            }

            DestroyMarkerObjects();
            CreateMarkers();
            UpdateMarkerPositions();
            ShowStatus("Replay display mode: " + DisplayModeName());
        }

        private string DisplayModeName()
        {
            if (displayMode == 0)
            {
                return "players-real";
            }

            if (displayMode == 1)
            {
                return "players+combat-markers";
            }

            return "all-markers";
        }

        private bool ShouldShowUnit(UnitMetadata metadata)
        {
            if (displayMode == 2)
            {
                return true;
            }

            if (IsPlayerUnit(metadata))
            {
                return true;
            }

            // Always show important units (projectiles, turrets, objectives) regardless of display mode
            if (IsImportantReplayUnit(metadata))
            {
                return true;
            }

            if (displayMode == 1)
            {
                return IsDeviceOrObjectiveUnit(metadata);
            }

            return false;
        }

        private static bool IsPlayerUnit(UnitMetadata metadata)
        {
            return metadata != null && !string.IsNullOrEmpty(metadata.PlayerId);
        }

        private static bool IsImportantReplayUnit(UnitMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.KeyName))
            {
                return false;
            }

            string key = metadata.KeyName;
            return key.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("bomb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("mine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsObjectiveUiUnit(UnitMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.KeyName))
            {
                return false;
            }

            string key = metadata.KeyName;
            return key.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("objective", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("cube", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadUnitMetadata()
        {
            unitMetadata.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "unit_creates.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        if (string.IsNullOrEmpty(unitId) || unitMetadata.ContainsKey(unitId))
                        {
                            continue;
                        }

                        UnitMetadata metadata = new UnitMetadata();
                        metadata.UnitId = unitId;
                        metadata.KeyName = GetCsvField(fields, columns, "key_name");
                        metadata.Team = GetCsvField(fields, columns, "team");
                        metadata.PlayerId = GetCsvField(fields, columns, "player_id");
                        metadata.OwnerId = GetCsvField(fields, columns, "owner_id");
                        metadata.KeyHash = GetCsvField(fields, columns, "key_hash");
                        metadata.SkinKeyHash = GetCsvField(fields, columns, "skin_key_hash");
                        metadata.SkinName = GetCsvField(fields, columns, "skin_name");
                        metadata.GearKeyHashes = GetCsvField(fields, columns, "gear_key_hashes");
                        metadata.GearNames = GetCsvField(fields, columns, "gear_names");
                        float spawnTime;
                        if (TryParseFloat(GetCsvField(fields, columns, "time"), out spawnTime))
                            metadata.SpawnTime = spawnTime;
                        float x;
                        float y;
                        float z;
                        if (TryParseFloat(GetCsvField(fields, columns, "x"), out x) &&
                            TryParseFloat(GetCsvField(fields, columns, "y"), out y) &&
                            TryParseFloat(GetCsvField(fields, columns, "z"), out z))
                        {
                            metadata.SpawnPosition = new Vector3(x, y, z);
                            metadata.HasSpawnPosition = true;
                        }

                        unitMetadata[unitId] = metadata;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Unit metadata load failed: " + ex.Message);
            }
        }

        private void LoadUnitEvents()
        {
            unitGearEvents.Clear();
            unitToolEvents.Clear();
            unitCastEvents.Clear();
            unitDropTimes.Clear();
            if (string.IsNullOrEmpty(replayPath)) return;
            FileInfo replayInfo = null;
            try { replayInfo = new FileInfo(replayPath); } catch { }
            if (replayInfo != null && replayInfo.Exists && replayInfo.Length > LargeReplayJsonFallbackBytes)
            {
                LoadUnitEventsFromCsv();
                Debug.Log("[BNL Replay] Skipping normalized JSON event parse for large replay (" + replayInfo.Length + " bytes); using CSV event files");
                return;
            }

            string json;
            try { json = File.ReadAllText(replayPath); } catch { return; }

            // Parse unitUpdates for gear switches — find the array occurrence (not the count field)
            try
            {
                int updatesIdx = -1;
                int searchFrom = 0;
                while (true)
                {
                    int candidate = json.IndexOf("\"unitUpdates\"", searchFrom, StringComparison.Ordinal);
                    if (candidate < 0) break;
                    // Accept only if the next non-whitespace char after the colon is '['
                    int colon = json.IndexOf(':', candidate + 13);
                    if (colon < 0) break;
                    int bracket = colon + 1;
                    while (bracket < json.Length && (json[bracket] == ' ' || json[bracket] == '\t' || json[bracket] == '\r' || json[bracket] == '\n')) bracket++;
                    if (bracket < json.Length && json[bracket] == '[') { updatesIdx = candidate; break; }
                    searchFrom = candidate + 1;
                }
                if (updatesIdx >= 0)
                {
                    int arrStart = json.IndexOf('[', updatesIdx);
                    int arrEnd = FindMatching(json, arrStart, '[', ']');
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        string arrText = json.Substring(arrStart, arrEnd - arrStart + 1);
                        int cur = 1;
                        while (cur < arrText.Length)
                        {
                            int os = arrText.IndexOf('{', cur);
                            if (os < 0) break;
                            int oe = FindMatching(arrText, os, '{', '}');
                            if (oe <= os) break;
                            string obj = arrText.Substring(os, oe - os + 1);
                            cur = oe + 1;

                            string gearHash = ExtractJsonString(obj, "currentGearKeyHash");
                            if (string.IsNullOrEmpty(gearHash)) continue;

                            float t; string unitId;
                            if (!TryExtractJsonFloat(obj, "t", out t)) continue;
                            Match um = Regex.Match(obj, "\"unitId\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
                            if (!um.Success) continue;
                            unitId = um.Groups[1].Value;

                            uint gearKeyHash;
                            if (!TryParseHexUInt(gearHash, out gearKeyHash)) continue;

                            List<UnitGearEvent> list;
                            if (!unitGearEvents.TryGetValue(unitId, out list))
                            {
                                list = new List<UnitGearEvent>();
                                unitGearEvents[unitId] = list;
                            }
                            list.Add(new UnitGearEvent(t, gearKeyHash));
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadUnitEvents gear: " + ex.Message); }

            // Parse zoneEvents for ToolFire (type 6) and ToolFireLoop (type 7) — find array occurrence
            try
            {
                int eventsIdx = -1;
                int searchFrom2 = 0;
                while (true)
                {
                    int candidate = json.IndexOf("\"zoneEvents\"", searchFrom2, StringComparison.Ordinal);
                    if (candidate < 0) break;
                    int colon = json.IndexOf(':', candidate + 12);
                    if (colon < 0) break;
                    int bracket = colon + 1;
                    while (bracket < json.Length && (json[bracket] == ' ' || json[bracket] == '\t' || json[bracket] == '\r' || json[bracket] == '\n')) bracket++;
                    if (bracket < json.Length && json[bracket] == '[') { eventsIdx = candidate; break; }
                    searchFrom2 = candidate + 1;
                }
                if (eventsIdx >= 0)
                {
                    int arrStart = json.IndexOf('[', eventsIdx);
                    int arrEnd = FindMatching(json, arrStart, '[', ']');
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        string arrText = json.Substring(arrStart, arrEnd - arrStart + 1);
                        int cur = 1;
                        while (cur < arrText.Length)
                        {
                            int os = arrText.IndexOf('{', cur);
                            if (os < 0) break;
                            int oe = FindMatching(arrText, os, '{', '}');
                            if (oe <= os) break;
                            string obj = arrText.Substring(os, oe - os + 1);
                            cur = oe + 1;

                            float evtType;
                            if (!TryExtractJsonFloat(obj, "eventType", out evtType)) continue;
                            int evtTypeInt = (int)evtType;
                            if (evtTypeInt != 6 && evtTypeInt != 7 && evtTypeInt != 8) continue;

                            float t; float toolIndexF;
                            if (!TryExtractJsonFloat(obj, "t", out t)) continue;
                            if (!TryExtractJsonFloat(obj, "toolIndex", out toolIndexF)) continue;
                            Match um = Regex.Match(obj, "\"unitId\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
                            if (!um.Success) continue;
                            string unitId = um.Groups[1].Value;

                            byte toolIndex = (byte)Mathf.Clamp((int)toolIndexF, 0, 255);
                            bool isLoop = evtTypeInt == 7;
                            bool isHold = evtTypeInt == 8;
                            bool active = true;
                            if (isLoop)
                            {
                                bool? activeParsed = TryExtractNullableBool(obj, "active");
                                active = !activeParsed.HasValue || activeParsed.Value;
                            }

                            List<UnitToolEvent> list;
                            if (!unitToolEvents.TryGetValue(unitId, out list))
                            {
                                list = new List<UnitToolEvent>();
                                unitToolEvents[unitId] = list;
                            }
                            list.Add(new UnitToolEvent(t, toolIndex, isLoop, isHold, active));
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadUnitEvents tool: " + ex.Message); }

            // Parse unitDrops — find the array occurrence (not the count field)
            try
            {
                int dropsIdx = -1;
                int searchFrom3 = 0;
                while (true)
                {
                    int candidate = json.IndexOf("\"unitDrops\"", searchFrom3, StringComparison.Ordinal);
                    if (candidate < 0) break;
                    int colon = json.IndexOf(':', candidate + 11);
                    if (colon < 0) break;
                    int bracket = colon + 1;
                    while (bracket < json.Length && (json[bracket] == ' ' || json[bracket] == '\t' || json[bracket] == '\r' || json[bracket] == '\n')) bracket++;
                    if (bracket < json.Length && json[bracket] == '[') { dropsIdx = candidate; break; }
                    searchFrom3 = candidate + 1;
                }
                if (dropsIdx >= 0)
                {
                    int arrStart = json.IndexOf('[', dropsIdx);
                    int arrEnd = FindMatching(json, arrStart, '[', ']');
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        string arrText = json.Substring(arrStart, arrEnd - arrStart + 1);
                        int cur = 1;
                        while (cur < arrText.Length)
                        {
                            int os = arrText.IndexOf('{', cur);
                            if (os < 0) break;
                            int oe = FindMatching(arrText, os, '{', '}');
                            if (oe <= os) break;
                            string obj = arrText.Substring(os, oe - os + 1);
                            cur = oe + 1;

                            float t;
                            if (!TryExtractJsonFloat(obj, "t", out t)) continue;
                            Match um = Regex.Match(obj, "\"unitId\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
                            if (!um.Success) continue;
                            unitDropTimes[um.Groups[1].Value] = t;
                        }
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + unitDropTimes.Count + " unit drop times");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadUnitEvents drops: " + ex.Message); }

            LoadCastEventsFromCsv();
        }

        private void LoadUnitEventsFromCsv()
        {
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            try
            {
                string dropsPath = Path.Combine(analysisDirectory, "unit_drops.csv");
                if (File.Exists(dropsPath))
                {
                    using (StreamReader reader = new StreamReader(dropsPath))
                    {
                        string header = reader.ReadLine();
                        Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            List<string> fields = SplitCsvLine(line);
                            float time;
                            string unitId = GetCsvField(fields, columns, "unit_id");
                            if (!string.IsNullOrEmpty(unitId) && TryParseFloat(GetCsvField(fields, columns, "time"), out time))
                            {
                                unitDropTimes[unitId] = time;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadUnitEventsFromCsv drops failed: " + ex.Message);
            }

            try
            {
                string updatesPath = Path.Combine(analysisDirectory, "unit_updates.csv");
                if (File.Exists(updatesPath))
                {
                    using (StreamReader reader = new StreamReader(updatesPath))
                    {
                        string header = reader.ReadLine();
                        Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            List<string> fields = SplitCsvLine(line);
                            string unitId = GetCsvField(fields, columns, "unit_id");
                            string gearHash = GetCsvField(fields, columns, "current_gear_hash");
                            float time;
                            uint gearKeyHash;
                            if (string.IsNullOrEmpty(unitId) ||
                                string.IsNullOrEmpty(gearHash) ||
                                !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                                !TryParseHexUInt(gearHash, out gearKeyHash))
                            {
                                continue;
                            }

                            List<UnitGearEvent> list;
                            if (!unitGearEvents.TryGetValue(unitId, out list))
                            {
                                list = new List<UnitGearEvent>();
                                unitGearEvents[unitId] = list;
                            }
                            list.Add(new UnitGearEvent(time, gearKeyHash));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadUnitEventsFromCsv gear failed: " + ex.Message);
            }

            LoadZoneEventsFromCsv();
            LoadCastEventsFromCsv();

            Debug.Log("[BNL Replay] Loaded CSV unit events: drops=" + unitDropTimes.Count + " gearUnits=" + unitGearEvents.Count + " toolUnits=" + unitToolEvents.Count + " castUnits=" + unitCastEvents.Count);
        }

        private void LoadZoneEventsFromCsv()
        {
            string path = Path.Combine(analysisDirectory, "zone_events.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        float time; int type; int toolIndexInt;
                        if (string.IsNullOrEmpty(unitId) ||
                            !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !int.TryParse(GetCsvField(fields, columns, "type"), NumberStyles.Integer, CultureInfo.InvariantCulture, out type) ||
                            !int.TryParse(GetCsvField(fields, columns, "tool_index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out toolIndexInt))
                        {
                            continue;
                        }
                        if (type != 6 && type != 7 && type != 8)
                        {
                            continue;
                        }

                        bool active = true;
                        string activeValue = GetCsvField(fields, columns, "active");
                        if (!string.IsNullOrEmpty(activeValue))
                        {
                            bool.TryParse(activeValue, out active);
                        }

                        List<UnitToolEvent> list;
                        if (!unitToolEvents.TryGetValue(unitId, out list))
                        {
                            list = new List<UnitToolEvent>();
                            unitToolEvents[unitId] = list;
                        }
                        list.Add(new UnitToolEvent(time, (byte)Mathf.Clamp(toolIndexInt, 0, 255), type == 7, type == 8, active));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadZoneEventsFromCsv failed: " + ex.Message);
            }
        }

        private void LoadCastEventsFromCsv()
        {
            string path = Path.Combine(analysisDirectory, "casts.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        float time; int toolIndexInt; float shotX; float shotY; float shotZ;
                        if (string.IsNullOrEmpty(unitId) ||
                            !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !int.TryParse(GetCsvField(fields, columns, "tool_index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out toolIndexInt) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_x"), out shotX) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_y"), out shotY) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_z"), out shotZ))
                        {
                            continue;
                        }

                        UnitCastEvent evt = new UnitCastEvent();
                        evt.Time = time;
                        evt.ToolIndex = (byte)Mathf.Clamp(toolIndexInt, 0, 255);
                        evt.ShotOrigin = new Vector3(shotX, shotY, shotZ);
                        evt.Shots = ParseShotDataList(GetCsvField(fields, columns, "shots"));
                        float speed;
                        if (TryParseFloat(GetCsvField(fields, columns, "projectile_speed"), out speed))
                        {
                            evt.HasProjectileSpeed = true;
                            evt.ProjectileSpeed = speed;
                        }

                        List<UnitCastEvent> list;
                        if (!unitCastEvents.TryGetValue(unitId, out list))
                        {
                            list = new List<UnitCastEvent>();
                            unitCastEvents[unitId] = list;
                        }
                        list.Add(evt);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadCastEventsFromCsv failed: " + ex.Message);
            }
        }

        private void LoadBlockTimeline()
        {
            blockTimeline.Clear();
            initialBlockByCell.Clear();
            nextBlockTimelineIndex = 0;
            blockTimelineAppliedThrough = float.MinValue;
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "map_state_timeline.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        int x;
                        int y;
                        int z;
                        int id;
                        int damage;
                        int vdata;
                        int ldata;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !TryParseInt(GetCsvField(fields, columns, "x"), out x) ||
                            !TryParseInt(GetCsvField(fields, columns, "y"), out y) ||
                            !TryParseInt(GetCsvField(fields, columns, "z"), out z))
                        {
                            continue;
                        }

                        TryParseInt(GetCsvField(fields, columns, "id"), out id);
                        TryParseInt(GetCsvField(fields, columns, "damage"), out damage);
                        TryParseInt(GetCsvField(fields, columns, "vdata"), out vdata);
                        TryParseInt(GetCsvField(fields, columns, "ldata"), out ldata);

                        BlockTimelineEvent item = new BlockTimelineEvent();
                        item.Time = time;
                        item.X = ClampInt16(x);
                        item.Y = ClampInt16(y);
                        item.Z = ClampInt16(z);
                        item.Id = (ushort)Mathf.Clamp(id, 0, 65535);
                        item.Damage = (byte)Mathf.Clamp(damage, 0, 255);
                        item.Vdata = (ushort)Mathf.Clamp(vdata, 0, 65535);
                        item.Ldata = (byte)Mathf.Clamp(ldata, 0, 255);
                        blockTimeline.Add(item);
                    }
                }

                LoadInitialBlockStatesForTimelineCells();
                Debug.Log("[BNL Replay] Loaded " + blockTimeline.Count + " map block timeline updates");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadBlockTimeline failed: " + ex.Message);
            }
        }

        private void LoadDamageEvents()
        {
            damageEvents.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "damage.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time; uint targetId;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "target_unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out targetId))
                        {
                            continue;
                        }

                        DamageReplayEvent evt = new DamageReplayEvent();
                        evt.Time = time;
                        evt.TargetUnitId = targetId;
                        uint sourceId;
                        if (uint.TryParse(GetCsvField(fields, columns, "source_unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceId))
                        {
                            evt.HasSourceUnitId = true;
                            evt.SourceUnitId = sourceId;
                        }
                        TryParseFloat(GetCsvField(fields, columns, "damage"), out evt.Damage);
                        TryParseFloat(GetCsvField(fields, columns, "initial_damage"), out evt.InitialDamage);
                        bool crit;
                        if (bool.TryParse(GetCsvField(fields, columns, "crit"), out crit))
                        {
                            evt.Crit = crit;
                        }
                        uint impactHash;
                        if (TryParseHexUInt(GetCsvField(fields, columns, "impact_key_hash"), out impactHash))
                        {
                            evt.HasImpact = true;
                            evt.Impact = KeyFromHash(impactHash);
                        }
                        damageEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + damageEvents.Count + " damage events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadDamageEvents failed: " + ex.Message);
            }
        }

        private void LoadAbilityEvents()
        {
            abilityEvents.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "ability_casts.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint unitId;
                        uint abilityHash;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId) ||
                            !TryParseHexUInt(GetCsvField(fields, columns, "ability_key_hash"), out abilityHash))
                        {
                            continue;
                        }

                        AbilityReplayEvent evt = new AbilityReplayEvent();
                        evt.Time = time;
                        evt.UnitId = unitId;
                        evt.AbilityKeyHash = abilityHash;
                        float x; float y; float z;
                        if (TryParseFloat(GetCsvField(fields, columns, "shot_x"), out x) &&
                            TryParseFloat(GetCsvField(fields, columns, "shot_y"), out y) &&
                            TryParseFloat(GetCsvField(fields, columns, "shot_z"), out z))
                        {
                            evt.HasShotPosition = true;
                            evt.ShotPosition = new Vector3(x, y, z);
                        }
                        evt.Shots = ParseShotDataList(GetCsvField(fields, columns, "shots"));
                        abilityEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + abilityEvents.Count + " ability cast events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadAbilityEvents failed: " + ex.Message);
            }
        }

        private void LoadInitialBlockStatesForTimelineCells()
        {
            Dictionary<string, bool> needed = new Dictionary<string, bool>();
            for (int i = 0; i < blockTimeline.Count; i++)
            {
                BlockTimelineEvent item = blockTimeline[i];
                needed[BlockCellKey(item.X, item.Y, item.Z)] = true;
            }

            string path = Path.Combine(analysisDirectory, "map_blocks.csv");
            if (!File.Exists(path) || needed.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        int x; int y; int z; int id; int damage; int vdata; int ldata;
                        if (!TryParseInt(GetCsvField(fields, columns, "x"), out x) ||
                            !TryParseInt(GetCsvField(fields, columns, "y"), out y) ||
                            !TryParseInt(GetCsvField(fields, columns, "z"), out z))
                        {
                            continue;
                        }

                        string key = BlockCellKey(ClampInt16(x), ClampInt16(y), ClampInt16(z));
                        if (!needed.ContainsKey(key))
                        {
                            continue;
                        }

                        TryParseInt(GetCsvField(fields, columns, "id"), out id);
                        TryParseInt(GetCsvField(fields, columns, "damage"), out damage);
                        TryParseInt(GetCsvField(fields, columns, "vdata"), out vdata);
                        TryParseInt(GetCsvField(fields, columns, "ldata"), out ldata);
                        Protocol.BlockUpdate update = new Protocol.BlockUpdate();
                        update.Id = (ushort)Mathf.Clamp(id, 0, 65535);
                        update.Damage = (byte)Mathf.Clamp(damage, 0, 255);
                        update.Vdata = (ushort)Mathf.Clamp(vdata, 0, 65535);
                        update.Ldata = (byte)Mathf.Clamp(ldata, 0, 255);
                        initialBlockByCell[key] = update;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadInitialBlockStatesForTimelineCells failed: " + ex.Message);
            }
        }

        private void LoadPlayerHudData()
        {
            replayPlayers.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "player_units.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        if (string.IsNullOrEmpty(unitId))
                        {
                            continue;
                        }

                        PlayerReplayInfo info = new PlayerReplayInfo();
                        info.PlayerId = GetCsvField(fields, columns, "player_id");
                        info.Nickname = GetCsvField(fields, columns, "nickname");
                        info.SteamId = GetCsvField(fields, columns, "steam_id");
                        if (string.IsNullOrEmpty(info.Nickname))
                        {
                            info.Nickname = info.PlayerId;
                        }

                        info.Team = GetCsvField(fields, columns, "team");
                        info.UnitId = unitId;
                        info.UnitName = GetCsvField(fields, columns, "unit_name");
                        replayPlayers.Add(info);
                    }
                }

                replayPlayers.Sort(CompareReplayPlayers);
                ApplyReplayPlayerStatsSnapshot();
                Debug.Log("[BNL Replay] Loaded " + replayPlayers.Count + " replay player HUD rows");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadPlayerHudData failed: " + ex.Message);
            }
        }

        private static int CompareReplayPlayers(PlayerReplayInfo a, PlayerReplayInfo b)
        {
            int team = string.Compare(a.Team, b.Team, StringComparison.Ordinal);
            if (team != 0) return team;
            return string.Compare(a.Nickname, b.Nickname, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadUnitStateEvents()
        {
            unitStateEvents.Clear();
            deviceKeyHashByName.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            LoadDeviceKeyHashCache();

            string path = Path.Combine(analysisDirectory, "unit_updates.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string unitId = GetCsvField(fields, columns, "unit_id");
                        if (string.IsNullOrEmpty(unitId))
                        {
                            continue;
                        }

                        float t;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out t))
                        {
                            continue;
                        }

                        UnitStateEvent evt = new UnitStateEvent();
                        evt.Time = t;
                        float value;
                        if (TryParseFloat(GetCsvField(fields, columns, "health"), out value))
                        {
                            evt.Health = value;
                            evt.HasHealth = true;
                        }
                        if (TryParseFloat(GetCsvField(fields, columns, "shield"), out value))
                        {
                            evt.Shield = value;
                            evt.HasShield = true;
                        }
                        if (TryParseFloat(GetCsvField(fields, columns, "resource"), out value))
                        {
                            evt.Resource = value;
                            evt.HasResource = true;
                        }
                        string devices = GetCsvField(fields, columns, "devices");
                        if (!string.IsNullOrEmpty(devices))
                        {
                            evt.Devices = ParseDeviceState(devices);
                            evt.HasDevices = evt.Devices.Count > 0;
                        }
                        string effects = GetCsvField(fields, columns, "effects");
                        if (!string.IsNullOrEmpty(effects))
                        {
                            evt.Effects = ParseEffectState(effects);
                            evt.HasEffects = evt.Effects.Count > 0;
                        }
                        string buffs = GetCsvField(fields, columns, "buffs");
                        if (!string.IsNullOrEmpty(buffs))
                        {
                            evt.Buffs = ParseBuffState(buffs);
                            evt.HasBuffs = evt.Buffs.Count > 0;
                        }

                        if (!evt.HasHealth && !evt.HasShield && !evt.HasResource && !evt.HasDevices && !evt.HasEffects && !evt.HasBuffs)
                        {
                            continue;
                        }

                        List<UnitStateEvent> list;
                        if (!unitStateEvents.TryGetValue(unitId, out list))
                        {
                            list = new List<UnitStateEvent>();
                            unitStateEvents[unitId] = list;
                        }
                        list.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded unit state timelines for " + unitStateEvents.Count + " units");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadUnitStateEvents failed: " + ex.Message);
            }
        }

        private void LoadDeviceKeyHashCache()
        {
            LoadDeviceKeyHashCacheFromCsv("build_placements.csv", "device_name", "device_key_hash");
            LoadDeviceKeyHashCacheFromCsv("build_starts.csv", "device_name", "device_key_hash");
            LoadDeviceKeyHashCacheFromCsv("devices_built.csv", "device_name", "device_key_hash");
            LoadDeviceKeyHashCacheFromCsv("map_state_timeline.csv", "device_name", "device_key_hash");
            Debug.Log("[BNL Replay] Loaded " + deviceKeyHashByName.Count + " device name/hash mappings");
        }

        private void LoadDeviceKeyHashCacheFromCsv(string fileName, string nameColumn, string hashColumn)
        {
            string path = Path.Combine(analysisDirectory, fileName);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string name = GetCsvField(fields, columns, nameColumn);
                        uint hash;
                        if (!string.IsNullOrEmpty(name) &&
                            TryParseHexUInt(GetCsvField(fields, columns, hashColumn), out hash) &&
                            !deviceKeyHashByName.ContainsKey(name))
                        {
                            deviceKeyHashByName[name] = hash;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadDeviceKeyHashCacheFromCsv failed for " + fileName + ": " + ex.Message);
            }
        }

        private void LoadZoneStatsEvents()
        {
            zoneStatsEvents.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "zone_updates.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header))
                    {
                        return;
                    }

                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time))
                        {
                            continue;
                        }

                        MatchTeamStatsData team1;
                        MatchTeamStatsData team2;
                        bool hasTeam1 = TryParseTeamStats(GetCsvField(fields, columns, "team1_stats"), out team1);
                        bool hasTeam2 = TryParseTeamStats(GetCsvField(fields, columns, "team2_stats"), out team2);
                        ZoneStatsReplayEvent evt = new ZoneStatsReplayEvent();
                        evt.Time = time;
                        evt.Phase = GetCsvField(fields, columns, "phase");
                        long phaseStart;
                        long phaseEnd;
                        if (long.TryParse(GetCsvField(fields, columns, "phase_start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out phaseStart))
                        {
                            evt.HasPhaseStart = true;
                            evt.PhaseStart = phaseStart;
                        }
                        if (long.TryParse(GetCsvField(fields, columns, "phase_end"), NumberStyles.Integer, CultureInfo.InvariantCulture, out phaseEnd))
                        {
                            evt.HasPhaseEnd = true;
                            evt.PhaseEnd = phaseEnd;
                        }
                        if (!hasTeam1 && !hasTeam2 && string.IsNullOrEmpty(evt.Phase) && !evt.HasPhaseStart && !evt.HasPhaseEnd)
                        {
                            continue;
                        }
                        evt.HasTeam1 = hasTeam1;
                        evt.Team1 = team1;
                        evt.HasTeam2 = hasTeam2;
                        evt.Team2 = team2;
                        zoneStatsEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + zoneStatsEvents.Count + " zone statistics updates");
                LoadRespawnEventsIntoZoneUpdates();
                LoadMatchPlayerStatsIntoZoneUpdates();
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadZoneStatsEvents failed: " + ex.Message);
            }
        }

        private void LoadRespawnEventsIntoZoneUpdates()
        {
            string path = Path.Combine(analysisDirectory, "respawns.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint playerId;
                        ulong respawnTime;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "player_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId) ||
                            !ulong.TryParse(GetCsvField(fields, columns, "respawn_time"), NumberStyles.Integer, CultureInfo.InvariantCulture, out respawnTime))
                        {
                            continue;
                        }

                        ZoneStatsReplayEvent evt = FindOrCreateZoneStatsEvent(time);
                        if (evt.RespawnInfo == null)
                        {
                            evt.RespawnInfo = new Dictionary<uint, ulong>();
                        }
                        evt.RespawnInfo[playerId] = respawnTime;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadRespawnEvents failed: " + ex.Message);
            }
        }

        private void LoadMatchPlayerStatsIntoZoneUpdates()
        {
            string path = Path.Combine(analysisDirectory, "match_player_stats.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint playerId;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "player_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
                        {
                            continue;
                        }

                        PlayerStatsReplayData stats = new PlayerStatsReplayData();
                        stats.Team = GetCsvField(fields, columns, "team");
                        int parsed;
                        if (int.TryParse(GetCsvField(fields, columns, "kills"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        {
                            stats.Kills = parsed;
                        }
                        if (int.TryParse(GetCsvField(fields, columns, "deaths"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        {
                            stats.Deaths = parsed;
                        }
                        if (int.TryParse(GetCsvField(fields, columns, "assists"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                        {
                            stats.Assists = parsed;
                        }

                        ZoneStatsReplayEvent evt = FindOrCreateZoneStatsEvent(time);
                        if (evt.PlayerStats == null)
                        {
                            evt.PlayerStats = new Dictionary<uint, PlayerStatsReplayData>();
                        }
                        evt.PlayerStats[playerId] = stats;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadMatchPlayerStats failed: " + ex.Message);
            }
        }

        private ZoneStatsReplayEvent FindOrCreateZoneStatsEvent(float time)
        {
            for (int i = 0; i < zoneStatsEvents.Count; i++)
            {
                if (Math.Abs(zoneStatsEvents[i].Time - time) < 0.001f)
                {
                    return zoneStatsEvents[i];
                }
            }

            ZoneStatsReplayEvent evt = new ZoneStatsReplayEvent();
            evt.Time = time;
            zoneStatsEvents.Add(evt);
            zoneStatsEvents.Sort(CompareZoneStatsEvents);
            return evt;
        }

        private static int CompareZoneStatsEvents(ZoneStatsReplayEvent a, ZoneStatsReplayEvent b)
        {
            return a.Time.CompareTo(b.Time);
        }

        private void LoadBuildEvents()
        {
            reloadEvents.Clear();
            buildEvents.Clear();
            buildPlacementByUnitId.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            LoadReloadEventsFromCsv();
            LoadBuildEventsFromCsv();
        }

        private void LoadKillEvents()
        {
            killEvents.Clear();
            deathHoldUntilByUnitId.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "kills.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint deadUnitId;
                        uint damageSourceHash;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "dead_unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out deadUnitId) ||
                            !TryParseHexUInt(GetCsvField(fields, columns, "damage_source_key_hash"), out damageSourceHash))
                        {
                            continue;
                        }

                        KillReplayEvent evt = new KillReplayEvent();
                        evt.Time = time;
                        evt.DeadUnitId = deadUnitId;
                        evt.DamageSource = KeyFromHash(damageSourceHash);
                        evt.Assistants = ParseUIntList(GetCsvField(fields, columns, "assistants"));
                        evt.Crit = string.Equals(GetCsvField(fields, columns, "crit"), "true", StringComparison.OrdinalIgnoreCase);

                        uint deadPlayerId;
                        if (uint.TryParse(GetCsvField(fields, columns, "dead_player_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out deadPlayerId))
                        {
                            evt.HasDeadPlayerId = true;
                            evt.DeadPlayerId = deadPlayerId;
                        }

                        uint killerPlayerId;
                        if (uint.TryParse(GetCsvField(fields, columns, "killer_player_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out killerPlayerId))
                        {
                            evt.HasKillerPlayerId = true;
                            evt.KillerPlayerId = killerPlayerId;
                        }

                        float sourceX; float sourceY; float sourceZ;
                        if (TryParseFloat(GetCsvField(fields, columns, "source_x"), out sourceX) &&
                            TryParseFloat(GetCsvField(fields, columns, "source_y"), out sourceY) &&
                            TryParseFloat(GetCsvField(fields, columns, "source_z"), out sourceZ))
                        {
                            evt.HasSourcePosition = true;
                            evt.SourcePosition = new Vector3(sourceX, sourceY, sourceZ);
                        }

                        killEvents.Add(evt);
                        deathHoldUntilByUnitId[deadUnitId.ToString(CultureInfo.InvariantCulture)] = time + 3.0f;
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + killEvents.Count + " kill events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadKillEvents failed: " + ex.Message);
            }
        }

        private void LoadProjectileEvents()
        {
            projectileObjects.Clear();
            impactEvents.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string createsPath = Path.Combine(analysisDirectory, "projectile_creates.csv");
            if (!File.Exists(createsPath))
            {
                LoadImpactEvents();
                return;
            }

            Dictionary<string, ProjectileReplayObject> byId = new Dictionary<string, ProjectileReplayObject>();
            try
            {
                using (StreamReader reader = new StreamReader(createsPath))
                {
                    string header = reader.ReadLine();
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        string id = GetCsvField(fields, columns, "projectile_id");
                        float time; float x; float y; float z;
                        uint keyHash = 0;
                        if (string.IsNullOrEmpty(id) ||
                            !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !TryParseFloat(GetCsvField(fields, columns, "x"), out x) ||
                            !TryParseFloat(GetCsvField(fields, columns, "y"), out y) ||
                            !TryParseFloat(GetCsvField(fields, columns, "z"), out z))
                        {
                            continue;
                        }

                        TryParseHexUInt(GetCsvField(fields, columns, "projectile_key_hash"), out keyHash);
                        ProjectileReplayObject projectile = new ProjectileReplayObject();
                        projectile.ProjectileId = id;
                        projectile.KeyHash = keyHash;
                        projectile.SpawnTime = time;
                        projectile.DropTime = time + 1.5f;
                        float speed;
                        if (TryParseFloat(GetCsvField(fields, columns, "speed"), out speed))
                        {
                            projectile.Speed = speed;
                            projectile.HasSpeed = true;
                        }
                        projectile.Points.Add(new ReplayPoint(time, new Vector3(x, y, z)));
                        projectileObjects.Add(projectile);
                        byId[id] = projectile;
                    }
                }

                LoadProjectileMoves(byId);
                LoadProjectileCastTargets(byId);
                LoadProjectileDrops(byId);
                LoadImpactEvents();
                Debug.Log("[BNL Replay] Loaded " + projectileObjects.Count + " projectile replay objects");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadProjectileEvents failed: " + ex.Message);
            }
        }

        private void LoadProjectileMoves(Dictionary<string, ProjectileReplayObject> byId)
        {
            string path = Path.Combine(analysisDirectory, "projectile_moves.csv");
            if (!File.Exists(path))
            {
                return;
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    List<string> fields = SplitCsvLine(line);
                    ProjectileReplayObject projectile;
                    float time; float x; float y; float z;
                    if (!byId.TryGetValue(GetCsvField(fields, columns, "projectile_id"), out projectile) ||
                        !TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                        !TryParseFloat(GetCsvField(fields, columns, "x"), out x) ||
                        !TryParseFloat(GetCsvField(fields, columns, "y"), out y) ||
                        !TryParseFloat(GetCsvField(fields, columns, "z"), out z))
                    {
                        continue;
                    }

                    ReplayPoint point = new ReplayPoint(time, new Vector3(x, y, z));
                    float rx; float ry; float rz;
                    if (TryParseFloat(GetCsvField(fields, columns, "rot_x"), out rx) &&
                        TryParseFloat(GetCsvField(fields, columns, "rot_y"), out ry) &&
                        TryParseFloat(GetCsvField(fields, columns, "rot_z"), out rz))
                    {
                        point.Rotation = new Vector3(rx, ry, rz);
                        point.HasRotation = true;
                    }
                    projectile.Points.Add(point);
                    if (time + 0.5f > projectile.DropTime)
                    {
                        projectile.DropTime = time + 0.5f;
                    }
                }
            }
        }

        private void LoadProjectileCastTargets(Dictionary<string, ProjectileReplayObject> byId)
        {
            string path = Path.Combine(analysisDirectory, "casts.csv");
            if (!File.Exists(path))
            {
                return;
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    List<string> fields = SplitCsvLine(line);
                    float time;
                    if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time))
                    {
                        continue;
                    }

                    string shots = GetCsvField(fields, columns, "shots");
                    if (string.IsNullOrEmpty(shots))
                    {
                        continue;
                    }

                    string[] shotItems = shots.Split('|');
                    for (int i = 0; i < shotItems.Length; i++)
                    {
                        string shot = shotItems[i];
                        int hash = shot.IndexOf('#');
                        if (hash <= 0 || hash >= shot.Length - 1)
                        {
                            continue;
                        }

                        string projectileId = shot.Substring(hash + 1);
                        ProjectileReplayObject projectile;
                        if (!byId.TryGetValue(projectileId, out projectile))
                        {
                            continue;
                        }
                        if (projectile.Points.Count > 1)
                        {
                            continue;
                        }

                        Vector3 target;
                        if (!TryParseShotPosition(shot.Substring(0, hash), out target))
                        {
                            continue;
                        }

                        Vector3 origin = projectile.Points[0].Position;
                        float speed = projectile.HasSpeed && projectile.Speed > 0.01f ? projectile.Speed : 40f;
                        float travelTime = Mathf.Clamp(Vector3.Distance(origin, target) / speed, 0.08f, 6f);
                        ReplayPoint targetPoint = new ReplayPoint(Mathf.Max(projectile.SpawnTime + 0.05f, time + travelTime), target);
                        projectile.Points.Add(targetPoint);
                        projectile.DropTime = Mathf.Max(projectile.DropTime, targetPoint.Time + 0.08f);
                    }
                }
            }
        }

        private static bool TryParseShotPosition(string value, out Vector3 position)
        {
            position = Vector3.zero;
            string[] parts = value.Split('/');
            if (parts.Length < 3)
            {
                return false;
            }

            float x; float y; float z;
            if (!TryParseFloat(parts[0], out x) ||
                !TryParseFloat(parts[1], out y) ||
                !TryParseFloat(parts[2], out z))
            {
                return false;
            }

            position = new Vector3(x, y, z);
            return true;
        }

        private static List<Protocol.ShotData> ParseShotDataList(string value)
        {
            List<Protocol.ShotData> shots = new List<Protocol.ShotData>();
            if (string.IsNullOrEmpty(value))
            {
                return shots;
            }

            string[] items = value.Split('|');
            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                string positionText = item;
                string idText = "";
                int hash = item.IndexOf('#');
                if (hash >= 0)
                {
                    positionText = item.Substring(0, hash);
                    if (hash < item.Length - 1)
                    {
                        idText = item.Substring(hash + 1);
                    }
                }

                Vector3 target;
                if (!TryParseShotPosition(positionText, out target))
                {
                    continue;
                }

                Protocol.ShotData shot = new Protocol.ShotData();
                shot.TargetPos = target;
                ulong shotId;
                if (!string.IsNullOrEmpty(idText) && ulong.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out shotId))
                {
                    shot.ShotId = shotId;
                }
                shots.Add(shot);
            }

            return shots;
        }

        private void LoadProjectileDrops(Dictionary<string, ProjectileReplayObject> byId)
        {
            string path = Path.Combine(analysisDirectory, "projectile_drops.csv");
            if (!File.Exists(path))
            {
                return;
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                Dictionary<string, int> columns = BuildCsvColumnIndex(header ?? "");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    List<string> fields = SplitCsvLine(line);
                    ProjectileReplayObject projectile;
                    float time;
                    if (byId.TryGetValue(GetCsvField(fields, columns, "projectile_id"), out projectile) &&
                        TryParseFloat(GetCsvField(fields, columns, "time"), out time))
                    {
                        projectile.DropTime = Mathf.Max(time + 0.05f, projectile.SpawnTime + 0.05f);
                    }
                }
            }
        }

        private void LoadReloadEventsFromCsv()
        {
            string path = Path.Combine(analysisDirectory, "reloads.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint unitId;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !uint.TryParse(GetCsvField(fields, columns, "unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId))
                        {
                            continue;
                        }

                        ReloadReplayEvent evt = new ReloadReplayEvent();
                        evt.Time = time;
                        evt.UnitId = unitId;
                        evt.IsStart = string.Equals(GetCsvField(fields, columns, "phase"), "Start", StringComparison.OrdinalIgnoreCase);
                        reloadEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + reloadEvents.Count + " reload events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadReloadEvents failed: " + ex.Message);
            }
        }

        private void LoadBuildEventsFromCsv()
        {
            string path = Path.Combine(analysisDirectory, "build_placements.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float deviceTime;
                        if (!TryParseFloat(GetCsvField(fields, columns, "device_time"), out deviceTime))
                        {
                            continue;
                        }

                        uint deviceHash;
                        if (!TryParseHexUInt(GetCsvField(fields, columns, "device_key_hash"), out deviceHash))
                        {
                            continue;
                        }

                        BuildReplayEvent evt = new BuildReplayEvent();
                        float startTime;
                        uint builderUnitId = 0u;
                        bool hasBuildStart = TryParseFloat(GetCsvField(fields, columns, "build_start_time"), out startTime) &&
                            uint.TryParse(GetCsvField(fields, columns, "builder_unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out builderUnitId);
                        evt.Time = hasBuildStart ? startTime : deviceTime;
                        evt.DeviceTime = deviceTime;
                        evt.BuilderUnitId = hasBuildStart ? builderUnitId : 0u;
                        evt.BuiltUnitId = GetCsvField(fields, columns, "built_unit_id");
                        evt.DeviceKey = KeyFromHash(deviceHash);
                        evt.DeviceName = GetCsvField(fields, columns, "device_name");
                        evt.Direction = GetCsvField(fields, columns, "direction");
                        evt.ShowGhost = !string.Equals(GetCsvField(fields, columns, "show_ghost"), "False", StringComparison.OrdinalIgnoreCase);
                        int insideX; int insideY; int insideZ; int outsideX; int outsideY; int outsideZ;
                        if (TryParseInt(GetCsvField(fields, columns, "inside_x"), out insideX) &&
                            TryParseInt(GetCsvField(fields, columns, "inside_y"), out insideY) &&
                            TryParseInt(GetCsvField(fields, columns, "inside_z"), out insideZ) &&
                            TryParseInt(GetCsvField(fields, columns, "outside_x"), out outsideX) &&
                            TryParseInt(GetCsvField(fields, columns, "outside_y"), out outsideY) &&
                            TryParseInt(GetCsvField(fields, columns, "outside_z"), out outsideZ))
                        {
                            evt.Inside = new Vector3s(ClampInt16(insideX), ClampInt16(insideY), ClampInt16(insideZ));
                            evt.Outside = new Vector3s(ClampInt16(outsideX), ClampInt16(outsideY), ClampInt16(outsideZ));
                            evt.HasSurface = true;
                        }
                        float x; float y; float z;
                        if (TryParseFloat(GetCsvField(fields, columns, "x"), out x) &&
                            TryParseFloat(GetCsvField(fields, columns, "y"), out y) &&
                            TryParseFloat(GetCsvField(fields, columns, "z"), out z))
                        {
                            evt.Position = new Vector3(x, y, z);
                            evt.HasPosition = true;
                        }
                        if (!string.IsNullOrEmpty(evt.BuiltUnitId))
                        {
                            buildPlacementByUnitId[evt.BuiltUnitId] = evt;
                        }
                        if (!hasBuildStart || !evt.HasSurface)
                        {
                            continue;
                        }
                        buildEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + buildEvents.Count + " build ghost/device events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadBuildEvents failed: " + ex.Message);
            }
        }

        private void LoadBarrierEvents()
        {
            barrierEvents.Clear();
            if (string.IsNullOrEmpty(analysisDirectory))
            {
                return;
            }

            string path = Path.Combine(analysisDirectory, "barrier_updates.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time))
                        {
                            continue;
                        }

                        BarrierReplayEvent evt = new BarrierReplayEvent();
                        evt.Time = time;
                        evt.Labels = ParseBarrierLabels(GetCsvField(fields, columns, "labels"));
                        barrierEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + barrierEvents.Count + " barrier updates");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadBarrierEvents failed: " + ex.Message);
            }
        }

        private static List<Protocol.BarrierLabel> ParseBarrierLabels(string value)
        {
            List<Protocol.BarrierLabel> labels = new List<Protocol.BarrierLabel>();
            if (string.IsNullOrEmpty(value))
            {
                return labels;
            }

            string[] parts = value.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                try
                {
                    labels.Add((Protocol.BarrierLabel)Enum.Parse(typeof(Protocol.BarrierLabel), parts[i], true));
                }
                catch { }
            }
            return labels;
        }

        private void LoadChannelEvents()
        {
            channelEvents.Clear();
            string path = Path.Combine(analysisDirectory, "channels.csv");
            if (!File.Exists(path)) return;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time)) continue;
                        ChannelReplayEvent evt = new ChannelReplayEvent();
                        evt.Time = time;
                        evt.Phase = GetCsvField(fields, columns, "phase");
                        evt.UnitId = ParseUint(GetCsvField(fields, columns, "unit_id"));
                        byte toolIndex;
                        if (TryParseByte(GetCsvField(fields, columns, "tool_index"), out toolIndex)) evt.ToolIndex = toolIndex;
                        evt.HitX = ParseFloatOrNull(GetCsvField(fields, columns, "hit_x"));
                        evt.HitY = ParseFloatOrNull(GetCsvField(fields, columns, "hit_y"));
                        evt.HitZ = ParseFloatOrNull(GetCsvField(fields, columns, "hit_z"));
                        channelEvents.Add(evt);
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + channelEvents.Count + " channel events");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadChannelEvents failed: " + ex.Message); }
        }

        private void LoadDashChargeEvents()
        {
            dashChargeEvents.Clear();
            string path = Path.Combine(analysisDirectory, "dash_charges.csv");
            if (!File.Exists(path)) return;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time)) continue;
                        DashChargeReplayEvent evt = new DashChargeReplayEvent();
                        evt.Time = time;
                        evt.Phase = GetCsvField(fields, columns, "phase");
                        evt.UnitId = ParseUint(GetCsvField(fields, columns, "unit_id"));
                        byte toolIndex;
                        if (TryParseByte(GetCsvField(fields, columns, "tool_index"), out toolIndex)) evt.ToolIndex = toolIndex;
                        dashChargeEvents.Add(evt);
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + dashChargeEvents.Count + " dash charge events");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadDashChargeEvents failed: " + ex.Message); }
        }

        private void LoadRecallEvents()
        {
            recallEvents.Clear();
            string path = Path.Combine(analysisDirectory, "recalls.csv");
            if (!File.Exists(path)) return;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time)) continue;
                        RecallReplayEvent evt = new RecallReplayEvent();
                        evt.Time = time;
                        evt.Phase = GetCsvField(fields, columns, "phase");
                        evt.UnitId = ParseUint(GetCsvField(fields, columns, "unit_id"));
                        evt.Duration = ParseFloatOrNull(GetCsvField(fields, columns, "duration"));
                        evt.EndTime = ParseUlongOrNull(GetCsvField(fields, columns, "end_time"));
                        recallEvents.Add(evt);
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + recallEvents.Count + " recall events");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadRecallEvents failed: " + ex.Message); }
        }

        private void LoadPortalTeleportEvents()
        {
            portalTeleportEvents.Clear();
            string path = Path.Combine(analysisDirectory, "portal_teleports.csv");
            if (!File.Exists(path)) return;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time)) continue;
                        PortalTeleportReplayEvent evt = new PortalTeleportReplayEvent();
                        evt.Time = time;
                        evt.UnitId = ParseUint(GetCsvField(fields, columns, "unit_id"));
                        evt.PortalFromId = ParseUint(GetCsvField(fields, columns, "portal_from_id"));
                        evt.PortalToId = ParseUint(GetCsvField(fields, columns, "portal_to_id"));
                        portalTeleportEvents.Add(evt);
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + portalTeleportEvents.Count + " portal teleport events");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadPortalTeleportEvents failed: " + ex.Message); }
        }

        private void LoadPickupTakenEvents()
        {
            pickupTakenEvents.Clear();
            string path = Path.Combine(analysisDirectory, "pickup_taken.csv");
            if (!File.Exists(path)) return;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time)) continue;
                        PickupTakenReplayEvent evt = new PickupTakenReplayEvent();
                        evt.Time = time;
                        evt.PlayerId = ParseUint(GetCsvField(fields, columns, "player_id"));
                        uint pickupHash;
                        TryParseHexUInt(GetCsvField(fields, columns, "pickup_key_hash"), out pickupHash);
                        evt.PickupKeyHash = pickupHash;
                        pickupTakenEvents.Add(evt);
                    }
                }
                Debug.Log("[BNL Replay] Loaded " + pickupTakenEvents.Count + " pickup taken events");
            }
            catch (Exception ex) { Debug.Log("[BNL Replay] LoadPickupTakenEvents failed: " + ex.Message); }
        }

        private void LoadImpactEvents()
        {
            string path = Path.Combine(analysisDirectory, "impacts.csv");
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string header = reader.ReadLine();
                    if (string.IsNullOrEmpty(header)) return;
                    Dictionary<string, int> columns = BuildCsvColumnIndex(header);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        List<string> fields = SplitCsvLine(line);
                        float time;
                        uint impactHash;
                        if (!TryParseFloat(GetCsvField(fields, columns, "time"), out time) ||
                            !TryParseHexUInt(GetCsvField(fields, columns, "impact_key_hash"), out impactHash))
                        {
                            continue;
                        }

                        float insideX; float insideY; float insideZ; float shotX; float shotY; float shotZ;
                        if (!TryParseFloat(GetCsvField(fields, columns, "inside_x"), out insideX) ||
                            !TryParseFloat(GetCsvField(fields, columns, "inside_y"), out insideY) ||
                            !TryParseFloat(GetCsvField(fields, columns, "inside_z"), out insideZ) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_x"), out shotX) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_y"), out shotY) ||
                            !TryParseFloat(GetCsvField(fields, columns, "shot_z"), out shotZ))
                        {
                            continue;
                        }

                        ImpactReplayEvent evt = new ImpactReplayEvent();
                        evt.Time = time;
                        evt.ImpactKey = KeyFromHash(impactHash);
                        evt.InsidePoint = new Vector3(insideX, insideY, insideZ);
                        evt.ShotPos = new Vector3(shotX, shotY, shotZ);

                        uint casterUnitId;
                        if (uint.TryParse(GetCsvField(fields, columns, "caster_unit_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out casterUnitId))
                        {
                            evt.HasCasterUnitId = true;
                            evt.CasterUnitId = casterUnitId;
                        }

                        uint casterPlayerId;
                        if (uint.TryParse(GetCsvField(fields, columns, "caster_player_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out casterPlayerId))
                        {
                            evt.HasCasterPlayerId = true;
                            evt.CasterPlayerId = casterPlayerId;
                        }

                        uint sourceHash;
                        if (TryParseHexUInt(GetCsvField(fields, columns, "source_key_hash"), out sourceHash))
                        {
                            evt.HasSourceKey = true;
                            evt.SourceKey = KeyFromHash(sourceHash);
                        }

                        evt.HitUnits = ParseUIntList(GetCsvField(fields, columns, "hit_units"));
                        evt.Crit = string.Equals(GetCsvField(fields, columns, "crit"), "true", StringComparison.OrdinalIgnoreCase);

                        float normalX; float normalY; float normalZ;
                        if (TryParseFloat(GetCsvField(fields, columns, "normal_x"), out normalX) &&
                            TryParseFloat(GetCsvField(fields, columns, "normal_y"), out normalY) &&
                            TryParseFloat(GetCsvField(fields, columns, "normal_z"), out normalZ))
                        {
                            evt.Normal = new Vector3s(ClampInt16(Mathf.RoundToInt(normalX)), ClampInt16(Mathf.RoundToInt(normalY)), ClampInt16(Mathf.RoundToInt(normalZ)));
                        }

                        impactEvents.Add(evt);
                    }
                }

                Debug.Log("[BNL Replay] Loaded " + impactEvents.Count + " impact events");
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] LoadImpactEvents failed: " + ex.Message);
            }
        }

        private static bool TryParseInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static short ClampInt16(int value)
        {
            if (value < short.MinValue) return short.MinValue;
            if (value > short.MaxValue) return short.MaxValue;
            return (short)value;
        }

        private static bool TryParseTeamStats(string value, out MatchTeamStatsData stats)
        {
            stats = new MatchTeamStatsData();
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            Match warfare = Regex.Match(value, "W:(-?\\d+)", RegexOptions.CultureInvariant);
            Match construction = Regex.Match(value, "C:(-?\\d+)", RegexOptions.CultureInvariant);
            Match tactics = Regex.Match(value, "T:(-?\\d+)", RegexOptions.CultureInvariant);
            Match healing = Regex.Match(value, "H:(-?\\d+)", RegexOptions.CultureInvariant);
            bool any = false;
            int parsed;
            if (warfare.Success && int.TryParse(warfare.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                stats.Warfare = parsed;
                any = true;
            }
            if (construction.Success && int.TryParse(construction.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                stats.Construction = parsed;
                any = true;
            }
            if (tactics.Success && int.TryParse(tactics.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                stats.Tactics = parsed;
                any = true;
            }
            if (healing.Success && int.TryParse(healing.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                stats.Healing = parsed;
                any = true;
            }

            return any;
        }

        private Dictionary<int, Protocol.DeviceData> ParseDeviceState(string value)
        {
            Dictionary<int, Protocol.DeviceData> devices = new Dictionary<int, Protocol.DeviceData>();
            if (string.IsNullOrEmpty(value))
            {
                return devices;
            }

            string[] entries = value.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                int colon = entry.IndexOf(':');
                if (colon <= 0 || colon >= entry.Length - 1)
                {
                    continue;
                }

                int slot;
                if (!int.TryParse(entry.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
                {
                    continue;
                }

                string[] parts = entry.Substring(colon + 1).Split('/');
                if (parts.Length == 0)
                {
                    continue;
                }

                uint keyHash;
                if (!TryResolveDeviceKeyHash(parts[0], out keyHash))
                {
                    continue;
                }

                Protocol.DeviceData data = new Protocol.DeviceData();
                data.DeviceKey = KeyFromHash(keyHash);
                float totalCost;
                float costInc;
                if (parts.Length > 1 && TryParseFloat(parts[1], out totalCost))
                {
                    data.TotalCost = totalCost;
                }
                if (parts.Length > 2 && TryParseFloat(parts[2], out costInc))
                {
                    data.CostInc = costInc;
                }
                devices[slot] = data;
            }

            return devices;
        }

        private Dictionary<Key, ulong?> ParseEffectState(string value)
        {
            Dictionary<Key, ulong?> effects = new Dictionary<Key, ulong?>();
            if (string.IsNullOrEmpty(value))
            {
                return effects;
            }

            string[] entries = value.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                string[] parts = entry.Split(':');
                if (parts.Length == 0)
                {
                    continue;
                }

                string keyToken = parts[0];
                int timestampIndex = 1;
                uint keyHash;
                if (!TryParseHexUInt(keyToken, out keyHash))
                {
                    if (!TryResolveDeviceKeyHash(keyToken, out keyHash))
                    {
                        continue;
                    }
                }
                else if (parts.Length > 2)
                {
                    timestampIndex = 2;
                }

                ulong timestamp;
                ulong? timestampEnd = null;
                if (parts.Length > timestampIndex &&
                    ulong.TryParse(parts[timestampIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                {
                    timestampEnd = timestamp;
                }

                effects[KeyFromHash(keyHash)] = timestampEnd;
            }

            return effects;
        }

        private static Dictionary<Protocol.BuffType, float> ParseBuffState(string value)
        {
            Dictionary<Protocol.BuffType, float> buffs = new Dictionary<Protocol.BuffType, float>();
            if (string.IsNullOrEmpty(value))
            {
                return buffs;
            }

            string[] entries = value.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                int colon = entry.IndexOf(':');
                if (colon <= 0 || colon >= entry.Length - 1)
                {
                    continue;
                }

                Protocol.BuffType buffType;
                float amount;
                if (TryParseBuffType(entry.Substring(0, colon), out buffType) &&
                    TryParseFloat(entry.Substring(colon + 1), out amount))
                {
                    buffs[buffType] = amount;
                }
            }

            return buffs;
        }

        private static bool TryParseBuffType(string value, out Protocol.BuffType buffType)
        {
            buffType = default(Protocol.BuffType);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                buffType = (Protocol.BuffType)Enum.Parse(typeof(Protocol.BuffType), value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryResolveDeviceKeyHash(string token, out uint keyHash)
        {
            keyHash = 0;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            if (TryParseHexUInt(token, out keyHash))
            {
                return true;
            }
            if (deviceKeyHashByName.TryGetValue(token, out keyHash))
            {
                return true;
            }

            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (textAsset == null)
                {
                    return false;
                }

                string json = textAsset.text;
                string escaped = Regex.Escape(token);
                Match idMatch = Regex.Match(json, "\"_id\"\\s*:\\s*\"(\\d+)\"[\\s\\S]{0,2000}?\"name\"\\s*:\\s*\"" + escaped + "\"", RegexOptions.CultureInvariant);
                if (!idMatch.Success)
                {
                    idMatch = Regex.Match(json, "\"name\"\\s*:\\s*\"" + escaped + "\"[\\s\\S]{0,2000}?\"_id\"\\s*:\\s*\"(\\d+)\"", RegexOptions.CultureInvariant);
                }
                if (!idMatch.Success)
                {
                    idMatch = Regex.Match(json, "\"_id\"\\s*:\\s*\"(\\d+)\"[\\s\\S]{0,2000}?\"key\"\\s*:\\s*\"" + escaped + "\"", RegexOptions.CultureInvariant);
                }
                uint decimalValue;
                if (idMatch.Success && uint.TryParse(idMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimalValue))
                {
                    keyHash = decimalValue;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static Dictionary<string, int> BuildCsvColumnIndex(string header)
        {
            Dictionary<string, int> columns = new Dictionary<string, int>();
            List<string> names = SplitCsvLine(header);
            for (int i = 0; i < names.Count; i++)
            {
                if (!columns.ContainsKey(names[i]))
                {
                    columns[names[i]] = i;
                }
            }

            return columns;
        }

        private static string GetCsvField(List<string> fields, Dictionary<string, int> columns, string name)
        {
            int index;
            if (!columns.TryGetValue(name, out index) || index < 0 || index >= fields.Count)
            {
                return "";
            }

            return fields[index];
        }

        private static List<string> SplitCsvLine(string line)
        {
            List<string> result = new List<string>();
            if (line == null)
            {
                return result;
            }

            System.Text.StringBuilder value = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            value.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        value.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(value.ToString());
                    value.Length = 0;
                }
                else
                {
                    value.Append(c);
                }
            }

            result.Add(value.ToString());
            return result;
        }

        private static bool TryParseHexUInt(string value, out uint result)
        {
            result = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        private static Key KeyFromHash(uint hash)
        {
            return hash;
        }

        private static Protocol.TeamType TeamFromString(string value)
        {
            if (string.Equals(value, "Team1", StringComparison.Ordinal))
            {
                return Protocol.TeamType.Team1;
            }

            if (string.Equals(value, "Team2", StringComparison.Ordinal))
            {
                return Protocol.TeamType.Team2;
            }

            return Protocol.TeamType.Neutral;
        }

        private static List<Key> ParseGearKeys(string value)
        {
            List<Key> keys = new List<Key>();
            if (string.IsNullOrEmpty(value))
            {
                return keys;
            }

            string[] parts = value.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                uint hash;
                if (TryParseHexUInt(parts[i], out hash))
                {
                    keys.Add(KeyFromHash(hash));
                }
            }

            return keys;
        }

        private static List<uint> ParseUIntList(string value)
        {
            List<uint> result = new List<uint>();
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }

            string[] parts = value.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                uint parsed;
                if (uint.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    result.Add(parsed);
                }
            }

            return result;
        }

        private static string LabelForUnit(string unitId, UnitMetadata metadata)
        {
            if (metadata == null)
            {
                return unitId;
            }

            string name = ShortUnitName(metadata.KeyName);
            if (string.IsNullOrEmpty(name))
            {
                name = unitId;
            }

            if (!string.IsNullOrEmpty(metadata.PlayerId))
            {
                return name + "\nP" + metadata.PlayerId + " #" + unitId;
            }

            return name + "\n#" + unitId;
        }

        private static string ShortUnitName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return "";
            }

            string value = keyName;
            string[] prefixes = new string[] { "unit_player_", "unit_device_generic_", "unit_device_", "unit_projectile_", "unit_special_", "unit_dummy_", "unit_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (value.StartsWith(prefixes[i], StringComparison.Ordinal))
                {
                    value = value.Substring(prefixes[i].Length);
                    break;
                }
            }

            return value.Replace('_', ' ');
        }

        private static float MarkerScale(UnitMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.KeyName))
            {
                return 0.85f;
            }

            if (metadata.KeyName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1.15f;
            }

            if (metadata.KeyName.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0.45f;
            }

            return 0.75f;
        }

        private static Color ColorForUnit(UnitMetadata metadata, int index)
        {
            if (metadata != null)
            {
                if (string.Equals(metadata.Team, "Team1", StringComparison.Ordinal))
                {
                    return new Color(0.1f, 0.55f, 1f, 1f);
                }

                if (string.Equals(metadata.Team, "Team2", StringComparison.Ordinal))
                {
                    return new Color(1f, 0.24f, 0.18f, 1f);
                }

                if (string.Equals(metadata.Team, "Neutral", StringComparison.Ordinal))
                {
                    return new Color(0.85f, 0.85f, 0.85f, 1f);
                }
            }

            return ColorForIndex(index);
        }

        private static bool IsDeviceOrObjectiveUnit(UnitMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.KeyName)) return false;
            string k = metadata.KeyName;
            return k.IndexOf("device", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("objective", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("hero_loot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("boosted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("drop_point", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   k.IndexOf("blockbuster", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPickupUnit(UnitMetadata metadata)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.KeyName))
            {
                return false;
            }

            string k = metadata.KeyName;
            return k.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                k.IndexOf("hero_loot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                k.IndexOf("boosted", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color ColorForStaticUnit(UnitMetadata metadata)
        {
            if (metadata == null) return new Color(0.8f, 0.8f, 0.2f, 1f);
            string k = metadata.KeyName ?? "";
            if (k.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                k.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (string.Equals(metadata.Team, "Team1", StringComparison.Ordinal)) return new Color(0.1f, 0.4f, 1f, 1f);
                if (string.Equals(metadata.Team, "Team2", StringComparison.Ordinal)) return new Color(1f, 0.2f, 0.1f, 1f);
                return new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            if (k.IndexOf("device", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (string.Equals(metadata.Team, "Team1", StringComparison.Ordinal)) return new Color(0.3f, 0.8f, 1f, 1f);
                if (string.Equals(metadata.Team, "Team2", StringComparison.Ordinal)) return new Color(1f, 0.5f, 0.3f, 1f);
            }
            if (k.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                k.IndexOf("drop_point", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.9f, 0.9f, 0.2f, 1f);
            return new Color(0.7f, 0.7f, 0.3f, 1f);
        }

        private static Color ColorForIndex(int index)
        {
            Color[] colors = new Color[]
            {
                new Color(0.1f, 0.75f, 1f, 1f),
                new Color(1f, 0.25f, 0.2f, 1f),
                new Color(0.2f, 1f, 0.35f, 1f),
                new Color(1f, 0.85f, 0.15f, 1f),
                new Color(0.9f, 0.25f, 1f, 1f),
                new Color(1f, 0.55f, 0.15f, 1f)
            };
            return colors[index % colors.Length];
        }

        private static bool TryGetPlacementRotation(BuildReplayEvent placement, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            Vector3 surfaceNormal;
            if (!TryGetPlacementSurfaceNormal(placement, out surfaceNormal))
            {
                return false;
            }

            if (ShouldAlignDeviceUpToSurface(placement.DeviceName))
            {
                rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            }
            else
            {
                rotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);
            }
            return true;
        }

        private static Vector3 GetPlacementPosition(BuildReplayEvent placement)
        {
            Vector3 position = placement.Position;
            if (!placement.HasPosition)
            {
                return position;
            }

            Vector3 surfaceNormal;
            if (ShouldAlignDeviceUpToSurface(placement.DeviceName) &&
                TryGetPlacementSurfaceNormal(placement, out surfaceNormal))
            {
                return position - surfaceNormal * 0.45f;
            }

            return position;
        }

        private static bool TryGetPlacementSurfaceNormal(BuildReplayEvent placement, out Vector3 surfaceNormal)
        {
            surfaceNormal = Vector3.zero;
            if (placement.HasSurface)
            {
                Vector3 normal = new Vector3(
                    placement.Outside.x - placement.Inside.x,
                    placement.Outside.y - placement.Inside.y,
                    placement.Outside.z - placement.Inside.z);
            if (normal.sqrMagnitude > 0.01f)
            {
                    surfaceNormal = normal.normalized;
                return true;
            }
            }

            if (string.Equals(placement.Direction, "Back", StringComparison.OrdinalIgnoreCase))
            {
                surfaceNormal = Vector3.back;
                return true;
            }
            if (string.Equals(placement.Direction, "Left", StringComparison.OrdinalIgnoreCase))
            {
                surfaceNormal = Vector3.left;
                return true;
            }
            if (string.Equals(placement.Direction, "Right", StringComparison.OrdinalIgnoreCase))
            {
                surfaceNormal = Vector3.right;
                return true;
            }
            if (string.Equals(placement.Direction, "Front", StringComparison.OrdinalIgnoreCase))
            {
                surfaceNormal = Vector3.forward;
                return true;
            }

            return false;
        }

        private static bool ShouldAlignDeviceUpToSurface(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return false;
            }

            return deviceName.IndexOf("mine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceName.IndexOf("tiki", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceName.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceName.IndexOf("caltrop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceName.IndexOf("bomb", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateMarkerPositions()
        {
            ApplyBlockTimeline();
            ApplyGlobalReplayEvents();
            ApplyCurrentPhaseClock();
            UpdateProjectileObjects();

            for (int i = 0; i < markers.Count; i++)
            {
                UnitMarker marker = markers[i];

                // Spawn/destroy based on replay create/drop times. This keeps game assets from
                // existing before their recorded creation time.
                bool shouldBeVisible = replayTime >= marker.SpawnTime && replayTime < marker.DropTime;
                if (!shouldBeVisible)
                {
                    if (marker.GameObject != null || marker.RealUnit != null)
                    {
                        DestroyReplayObject(marker);
                    }

                    continue;
                }

                EnsureReplayObjectSpawned(marker);
                if (marker.GameObject == null)
                {
                    continue;
                }

                if (marker.IsStaticUnit)
                {
                    if (marker.RealUnit != null)
                    {
                        ApplyReplayUnitState(marker);
                    }
                    // Static units stay at their spawn position — nothing else to update
                    continue;
                }

                if (marker.RealUnit != null)
                {
                    ApplyUnitEvents(marker);
                    ApplyReplayUnitState(marker);
                    if (IsUnitInDeathHold(marker))
                    {
                        continue;
                    }
                    ReplaySample sample = SampleReplay(marker.Track, replayTime);
                    MoveRealReplayUnit(marker, sample);
                }
                else if (marker.GameObject != null)
                {
                    ReplaySample sample = SampleReplay(marker.Track, replayTime);
                    marker.GameObject.transform.position = sample.Position;
                    if (sample.HasRotation)
                    {
                        marker.GameObject.transform.rotation = RotationToQuaternion(sample.Rotation);
                    }
                }
            }
        }

        private bool IsUnitInDeathHold(UnitMarker marker)
        {
            if (marker == null || string.IsNullOrEmpty(marker.UnitId))
            {
                return false;
            }

            float holdUntil;
            return deathHoldUntilByUnitId.TryGetValue(marker.UnitId, out holdUntil) &&
                replayTime < holdUntil &&
                replayTime >= holdUntil - 3.0f;
        }

        private void UpdateProjectileObjects()
        {
            for (int i = 0; i < projectileObjects.Count; i++)
            {
                ProjectileReplayObject projectile = projectileObjects[i];
                bool visible = replayTime >= projectile.SpawnTime && replayTime < projectile.DropTime;
                if (!visible)
                {
                    if (projectile.GameObject != null)
                    {
                        Destroy(projectile.GameObject);
                        projectile.GameObject = null;
                    }
                    continue;
                }

                if (projectile.GameObject == null)
                {
                    projectile.GameObject = SpawnProjectileVisual(projectile);
                }

                if (projectile.GameObject == null)
                {
                    continue;
                }

                ReplaySample sample = SampleProjectile(projectile, replayTime);
                projectile.GameObject.transform.position = sample.Position;
                if (sample.HasRotation)
                {
                    projectile.GameObject.transform.rotation = RotationToQuaternion(sample.Rotation);
                }
            }
        }

        private GameObject SpawnProjectileVisual(ProjectileReplayObject projectile)
        {
            GameObject go = null;
            string prefabPath = FindProjectilePrefab(projectile.KeyHash);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                try
                {
                    AssetCache assetCache = Singleton<AssetCache>.Instance;
                    GameObject prefab = assetCache == null ? null : assetCache.LoadPrefab(prefabPath);
                    if (prefab != null)
                    {
                        go = UnityEngine.Object.Instantiate(prefab) as GameObject;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] Projectile prefab spawn failed " + prefabPath + ": " + ex.Message);
                }
            }

            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.18f;
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Diffuse"));
                    renderer.material.color = new Color(1f, 0.6f, 0.15f, 1f);
                }
            }

            go.name = "BNL_ReplayProjectile_" + projectile.ProjectileId;
            DontDestroyOnLoad(go);
            go.SetActive(true);

            // Broadcast OnProjectileCreate so MessagesHandlerBehaviour scripts (auras, trails, orb
            // effects) initialise. We need a real Projectile component so scripts can read Speed/Owner.
            Projectile projComponent = go.GetComponent<Projectile>();
            if (projComponent == null)
            {
                projComponent = go.AddComponent<Projectile>();
            }
            projComponent.StartSpeed = projectile.Speed;
            try { go.BroadcastMessage("OnProjectileCreate", projComponent, SendMessageOptions.DontRequireReceiver); } catch { }

            ParticleSystem[] particles = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                try
                {
                    particles[i].gameObject.SetActive(true);
                    particles[i].Play(true);
                }
                catch { }
            }

            // Activate TrailRenderers which start disabled on many projectile prefabs
            TrailRenderer[] trails = go.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                try { trails[i].gameObject.SetActive(true); trails[i].enabled = true; } catch { }
            }
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
            Rigidbody[] bodies = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }
            return go;
        }

        private static void DisableReplayProjectileAudio(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            AudioSource[] audioSources = go.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].enabled = false;
            }

            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                string name = behaviour.GetType().Name;
                if (name.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void ApplyBlockTimeline()
        {
            if (blockTimeline.Count == 0 || Application.loadedLevelName != "Zone")
            {
                return;
            }

            ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
            if (listener == null)
            {
                return;
            }

            if (replayTime < blockTimelineAppliedThrough)
            {
                ResetChangedBlocksToInitialState(listener);
                nextBlockTimelineIndex = 0;
                blockTimelineAppliedThrough = float.MinValue;
                Debug.Log("[BNL Replay] Replay time moved backwards; reset changed blocks and re-applying block timeline.");
            }

            Dictionary<Vector3s, Protocol.BlockUpdate> updates = null;
            int applied = 0;
            while (nextBlockTimelineIndex < blockTimeline.Count && blockTimeline[nextBlockTimelineIndex].Time <= replayTime)
            {
                BlockTimelineEvent item = blockTimeline[nextBlockTimelineIndex++];
                if (updates == null)
                {
                    updates = new Dictionary<Vector3s, Protocol.BlockUpdate>();
                }

                Protocol.BlockUpdate update = new Protocol.BlockUpdate();
                update.Id = item.Id;
                update.Damage = item.Damage;
                update.Vdata = item.Vdata;
                update.Ldata = item.Ldata;
                updates[new Vector3s(item.X, item.Y, item.Z)] = update;
                applied++;

                if (updates.Count >= 256)
                {
                    listener.BlockUpdates(updates);
                    updates.Clear();
                }
            }

            if (updates != null && updates.Count > 0)
            {
                listener.BlockUpdates(updates);
            }

            if (applied > 0)
            {
                blockTimelineAppliedThrough = replayTime;
            }
        }

        private void ResetChangedBlocksToInitialState(ZoneServiceListener listener)
        {
            if (listener == null || blockTimeline.Count == 0)
            {
                return;
            }

            Dictionary<Vector3s, Protocol.BlockUpdate> updates = new Dictionary<Vector3s, Protocol.BlockUpdate>();
            Dictionary<string, bool> added = new Dictionary<string, bool>();
            for (int i = 0; i < blockTimeline.Count; i++)
            {
                BlockTimelineEvent item = blockTimeline[i];
                string key = BlockCellKey(item.X, item.Y, item.Z);
                if (added.ContainsKey(key))
                {
                    continue;
                }
                added[key] = true;

                Protocol.BlockUpdate update;
                if (!initialBlockByCell.TryGetValue(key, out update))
                {
                    update = new Protocol.BlockUpdate();
                    update.Id = 0;
                    update.Damage = 0;
                    update.Vdata = 0;
                    update.Ldata = 0;
                }

                updates[new Vector3s(item.X, item.Y, item.Z)] = update;
                if (updates.Count >= 256)
                {
                    listener.BlockUpdates(updates);
                    updates.Clear();
                }
            }

            if (updates.Count > 0)
            {
                listener.BlockUpdates(updates);
            }
        }

        private static string BlockCellKey(short x, short y, short z)
        {
            return x.ToString(CultureInfo.InvariantCulture) + ":" +
                y.ToString(CultureInfo.InvariantCulture) + ":" +
                z.ToString(CultureInfo.InvariantCulture);
        }

        private void ApplyGlobalReplayEvents()
        {
            float prevTime = lastGlobalEventTime;
            float curTime = replayTime;
            if (curTime < prevTime)
            {
                prevTime = float.MinValue;
            }

            ZoneServiceListener listener = null;
            try { listener = Singleton<ZoneServiceListener>.Instance; } catch { }
            if (listener == null)
            {
                lastGlobalEventTime = curTime;
                return;
            }

            for (int i = 0; i < zoneStatsEvents.Count; i++)
            {
                ZoneStatsReplayEvent evt = zoneStatsEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyZoneStatsEvent(listener, evt);
                    if (!string.IsNullOrEmpty(evt.Phase))
                    {
                        currentPhaseEvent = evt;
                    }
                }
            }

            for (int i = 0; i < reloadEvents.Count; i++)
            {
                ReloadReplayEvent evt = reloadEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    try
                    {
                        if (evt.IsStart)
                        {
                            listener.DoStartReload(evt.UnitId);
                        }
                        else
                        {
                            listener.DoEndReload(evt.UnitId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("[BNL Replay] Replay reload event failed: " + ex.Message);
                    }
                }
            }

            for (int i = 0; i < killEvents.Count; i++)
            {
                KillReplayEvent evt = killEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyKillEvent(listener, evt);
                }
            }

            for (int i = 0; i < buildEvents.Count; i++)
            {
                BuildReplayEvent evt = buildEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyBuildStartEvent(listener, evt);
                }
                if (evt.DeviceTime > prevTime && evt.DeviceTime <= curTime)
                {
                    ApplyDeviceBuiltEvent(listener, evt);
                }
            }

            for (int i = 0; i < barrierEvents.Count; i++)
            {
                BarrierReplayEvent evt = barrierEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyBarrierEvent(evt);
                }
            }

            for (int i = 0; i < channelEvents.Count; i++)
            {
                ChannelReplayEvent evt = channelEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyChannelEvent(evt);
                }
            }

            for (int i = 0; i < dashChargeEvents.Count; i++)
            {
                DashChargeReplayEvent evt = dashChargeEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyDashChargeEvent(evt);
                }
            }

            for (int i = 0; i < recallEvents.Count; i++)
            {
                RecallReplayEvent evt = recallEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyRecallEvent(evt);
                }
            }

            for (int i = 0; i < portalTeleportEvents.Count; i++)
            {
                PortalTeleportReplayEvent evt = portalTeleportEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyPortalTeleportEvent(evt);
                }
            }

            for (int i = 0; i < impactEvents.Count; i++)
            {
                ImpactReplayEvent evt = impactEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyImpactEvent(listener, evt);
                }
            }

            for (int i = 0; i < damageEvents.Count; i++)
            {
                DamageReplayEvent evt = damageEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyDamageEvent(listener, evt);
                }
            }

            for (int i = 0; i < abilityEvents.Count; i++)
            {
                AbilityReplayEvent evt = abilityEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyAbilityEvent(evt);
                }
            }

            for (int i = 0; i < pickupTakenEvents.Count; i++)
            {
                PickupTakenReplayEvent evt = pickupTakenEvents[i];
                if (evt.Time > prevTime && evt.Time <= curTime)
                {
                    ApplyPickupTakenEvent(listener, evt);
                }
            }

            lastGlobalEventTime = curTime;
        }

        private void ApplyImpactEvent(ZoneServiceListener listener, ImpactReplayEvent evt)
        {
            try
            {
                Protocol.ImpactData data = new Protocol.ImpactData();
                data.InsidePoint = evt.InsidePoint;
                data.ShotPos = evt.ShotPos;
                data.Normal = evt.Normal;
                data.Impact = evt.ImpactKey;
                if (evt.HasSourceKey)
                {
                    data.SourceKey = evt.SourceKey;
                }
                if (evt.HasCasterUnitId)
                {
                    data.CasterUnitId = evt.CasterUnitId;
                }
                if (evt.HasCasterPlayerId)
                {
                    data.CasterPlayerId = evt.CasterPlayerId;
                }
                data.HitUnits = evt.HitUnits ?? new List<uint>();
                data.Crit = evt.Crit;
                listener.Impact(data);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay impact failed: " + ex.Message);
            }
        }

        private void ApplyDamageEvent(ZoneServiceListener listener, DamageReplayEvent evt)
        {
            try
            {
                Protocol.DamageInfo info = new Protocol.DamageInfo();
                info.TargetUnitId = evt.TargetUnitId;
                if (evt.HasSourceUnitId)
                {
                    info.SourceUnitId = evt.SourceUnitId;
                }
                if (evt.HasImpact)
                {
                    info.Impact = evt.Impact;
                }
                info.Damage = evt.Damage;
                info.InitialDamage = evt.InitialDamage;
                info.Crit = evt.Crit;

                if (listener != null)
                {
                    listener.Damage(info);
                }

                ApplyDamageToReplayUnit(evt);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay damage failed: " + ex.Message);
            }
        }

        private void ApplyDamageToReplayUnit(DamageReplayEvent evt)
        {
            UnitMarker marker = FindMarkerByUnitId(evt.TargetUnitId.ToString(CultureInfo.InvariantCulture));
            if (marker == null || marker.RealUnit == null || evt.Damage <= 0f)
            {
                return;
            }

            try
            {
                Unit unit = marker.RealUnit;
                float oldHealth = unit.Health;
                float newHealth = Mathf.Max(0f, oldHealth - evt.Damage);
                unit.Health = newHealth;

                ZoneMessenger messenger = null;
                try { messenger = Singleton<ZoneMessenger>.Instance; } catch { }
                if (messenger != null)
                {
                    messenger.OnUnitHealthChange(unit, oldHealth, newHealth);
                }

                if (newHealth <= 0f)
                {
                    TriggerReplayUnitDropAnimation(evt.TargetUnitId);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ApplyDamageToReplayUnit failed: " + ex.Message);
            }
        }

        private void ApplyAbilityEvent(AbilityReplayEvent evt)
        {
            try
            {
                Unit unit = null;
                UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
                if (registry != null)
                {
                    unit = registry.Get(evt.UnitId);
                }

                Protocol.AbilityCastData data = new Protocol.AbilityCastData();
                data.AbilityKey = KeyFromHash(evt.AbilityKeyHash);
                if (evt.HasShotPosition)
                {
                    data.ShotPos = evt.ShotPosition;
                }
                data.Shots = evt.Shots ?? new List<Protocol.ShotData>();

                if (unit != null)
                {
                    try { UnitEventHelper.HandleAbilityCast(unit, data); } catch { }
                }

                SpawnAbilityPrefab(evt, unit);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay ability failed: " + ex.Message);
            }
        }

        private void SpawnAbilityPrefab(AbilityReplayEvent evt, Unit unit)
        {
            string prefabPath = FindAbilityPrefab(evt.AbilityKeyHash);
            if (string.IsNullOrEmpty(prefabPath) && IsNinjaVanishAbility(evt.AbilityKeyHash))
            {
                prefabPath = "assets/prefabs/effects/instant/ninjavanish.prefab";
            }
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            try
            {
                AssetCache assetCache = Singleton<AssetCache>.Instance;
                GameObject prefab = LoadPrefabWithFallbacks(assetCache, prefabPath);
                if (prefab == null)
                {
                    Debug.Log("[BNL Replay] Ability prefab not found " + prefabPath);
                    return;
                }

                Vector3 pos = evt.HasShotPosition ? evt.ShotPosition : (unit != null ? unit.transform.position : Vector3.zero);
                GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity) as GameObject;
                if (go == null)
                {
                    return;
                }

                go.name = "BNL_ReplayAbility_" + evt.AbilityKeyHash.ToString("X8", CultureInfo.InvariantCulture);
                DontDestroyOnLoad(go);
                ParticleSystem[] particles = go.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    try { particles[i].gameObject.SetActive(true); particles[i].Play(true); } catch { }
                }
                Debug.Log("[BNL Replay] Spawned ability prefab " + prefabPath);
                Destroy(go, 8f);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] SpawnAbilityPrefab failed: " + ex.Message);
            }
        }

        private void ApplyKillEvent(ZoneServiceListener listener, KillReplayEvent evt)
        {
            try
            {
                Protocol.KillInfo info = new Protocol.KillInfo();
                info.DeadUnitId = evt.DeadUnitId;
                if (evt.HasDeadPlayerId)
                {
                    info.Dead = evt.DeadPlayerId;
                }
                if (evt.HasKillerPlayerId)
                {
                    info.Killer = evt.KillerPlayerId;
                }
                info.Assistants = evt.Assistants ?? new List<uint>();
                info.DamageSource = evt.DamageSource;
                if (evt.HasSourcePosition)
                {
                    info.SourcePosition = evt.SourcePosition;
                }
                info.Crit = evt.Crit;
                listener.Kill(info);
                TriggerReplayUnitDropAnimation(evt.DeadUnitId);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay kill failed: " + ex.Message);
            }
        }

        private void TriggerReplayUnitDropAnimation(uint unitId)
        {
            try
            {
                UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
                Unit unit = registry == null ? null : registry.Get(unitId);
                if (unit == null)
                {
                    return;
                }

                MessagesHandlerBehaviour[] handlers = unit.GetComponentsInChildren<MessagesHandlerBehaviour>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].OnUnitDrop(); } catch { }
                }

                GearSoundHandler[] soundHandlers = unit.GetComponentsInChildren<GearSoundHandler>(true);
                for (int i = 0; i < soundHandlers.Length; i++)
                {
                    try { soundHandlers[i].UnitDie(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TriggerReplayUnitDropAnimation failed: " + ex.Message);
            }
        }

        private void ApplyBarrierEvent(BarrierReplayEvent evt)
        {
            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener != null)
                {
                    listener.UpdateBarriers(evt.Labels ?? new List<Protocol.BarrierLabel>());
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay barrier update failed: " + ex.Message);
            }
        }

        private void ApplyChannelEvent(ChannelReplayEvent evt)
        {
            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener == null) return;
                if (evt.Phase == "Start")
                {
                    Vector3 hitPos = evt.HitX.HasValue && evt.HitY.HasValue && evt.HitZ.HasValue
                        ? new Vector3(evt.HitX.Value, evt.HitY.Value, evt.HitZ.Value)
                        : Vector3.zero;
                    InvokeListenerMethod(listener, "DoStartChannel", new object[] { evt.UnitId, evt.ToolIndex ?? (byte)0, hitPos, Vector3s.zero, (uint)0 });
                }
                else if (evt.Phase == "End")
                {
                    InvokeListenerMethod(listener, "DoEndChannel", new object[] { evt.UnitId, evt.ToolIndex ?? (byte)0 });
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay channel event failed: " + ex.Message);
            }
        }

        private void ApplyDashChargeEvent(DashChargeReplayEvent evt)
        {
            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener == null) return;
                if (evt.Phase == "Start")
                {
                    InvokeListenerMethod(listener, "DoDashStartCharge", new object[] { evt.UnitId, evt.ToolIndex ?? (byte)0 });
                }
                else if (evt.Phase == "End")
                {
                    InvokeListenerMethod(listener, "DoDashEndCharge", new object[] { evt.UnitId, evt.ToolIndex ?? (byte)0 });
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay dash charge event failed: " + ex.Message);
            }
        }

        private void ApplyRecallEvent(RecallReplayEvent evt)
        {
            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener == null) return;
                if (evt.Phase == "Start")
                {
                    InvokeListenerMethod(listener, "DoStartRecall", new object[] { evt.UnitId, evt.Duration ?? 0f, evt.EndTime ?? 0UL });
                }
                else if (evt.Phase == "Cancel")
                {
                    InvokeListenerMethod(listener, "DoCancelRecall", new object[] { evt.UnitId });
                }
                else if (evt.Phase == "Recall")
                {
                    InvokeListenerMethod(listener, "DoRecall", new object[] { evt.UnitId });
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay recall event failed: " + ex.Message);
            }
        }

        private void ApplyPortalTeleportEvent(PortalTeleportReplayEvent evt)
        {
            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener != null)
                {
                    InvokeListenerMethod(listener, "PortalTeleport", new object[] { evt.UnitId, evt.PortalFromId, evt.PortalToId });
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay portal teleport failed: " + ex.Message);
            }
        }

        private void ApplyPickupTakenEvent(ZoneServiceListener listener, PickupTakenReplayEvent evt)
        {
            try
            {
                if (listener != null && evt.PickupKeyHash != 0)
                {
                    listener.PickupTaken(evt.PlayerId, KeyFromHash(evt.PickupKeyHash));
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay pickup taken failed: " + ex.Message);
            }
        }

        private static void InvokeListenerMethod(ZoneServiceListener listener, string methodName, object[] args)
        {
            System.Reflection.MethodInfo method = typeof(ZoneServiceListener).GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (!object.ReferenceEquals(method, null))
            {
                method.Invoke(listener, args);
            }
        }

        private void ApplyCurrentPhaseClock()
        {
            if (currentPhaseEvent == null || string.IsNullOrEmpty(currentPhaseEvent.Phase))
            {
                currentPhaseEvent = GetPhaseEventAt(replayTime);
                if (currentPhaseEvent == null)
                {
                    return;
                }
            }

            ApplyPhaseDirect(currentPhaseEvent);
        }

        private ZoneStatsReplayEvent GetPhaseEventAt(float time)
        {
            ZoneStatsReplayEvent best = null;
            for (int i = 0; i < zoneStatsEvents.Count; i++)
            {
                ZoneStatsReplayEvent evt = zoneStatsEvents[i];
                if (evt.Time > time)
                {
                    break;
                }
                if (!string.IsNullOrEmpty(evt.Phase))
                {
                    best = evt;
                }
            }

            return best;
        }

        private void ApplyPhaseDirect(ZoneStatsReplayEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.Phase))
            {
                return;
            }

            try
            {
                ZoneData zoneData = Singleton<ZoneData>.Instance;
                if (zoneData == null)
                {
                    return;
                }

                Protocol.ZonePhase phase = BuildLocalPhase(evt);
                if (phase != null)
                {
                    zoneData.Phase = phase;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay direct phase sync failed: " + ex.Message);
            }
        }

        private Protocol.ZonePhase BuildLocalPhase(ZoneStatsReplayEvent evt)
        {
            Protocol.ZonePhase phase = new Protocol.ZonePhase();
            try { phase.PhaseType = (Protocol.ZonePhaseType)Enum.Parse(typeof(Protocol.ZonePhaseType), evt.Phase, true); } catch { return null; }

            long now = GetCurrentServerTimeMillis();
            long replayElapsedMillis = (long)Mathf.Max(0f, (replayTime - evt.Time) * 1000f);
            long localStart = now - replayElapsedMillis;
            if (evt.HasPhaseStart)
            {
                phase.StartTime = localStart;
            }
            if (evt.HasPhaseStart && evt.HasPhaseEnd)
            {
                phase.EndTime = localStart + Math.Max(0L, evt.PhaseEnd - evt.PhaseStart);
            }
            return phase;
        }

        private void ApplyZoneStatsEvent(ZoneServiceListener listener, ZoneStatsReplayEvent evt)
        {
            try
            {
                Protocol.ZoneUpdate update = new Protocol.ZoneUpdate();
                Protocol.MatchStats stats = new Protocol.MatchStats();
                stats.PlayerStats = BuildReplayPlayerStats(evt.PlayerStats);
                if (evt.HasTeam1)
                {
                    stats.Team1Stats = BuildMatchTeamStats(evt.Team1);
                }
                if (evt.HasTeam2)
                {
                    stats.Team2Stats = BuildMatchTeamStats(evt.Team2);
                }
                update.Statistics = stats;
                if (!string.IsNullOrEmpty(evt.Phase))
                {
                    Protocol.ZonePhase phase = BuildLocalPhase(evt);
                    update.Phase = phase;
                }
                if (evt.RespawnInfo != null && evt.RespawnInfo.Count > 0)
                {
                    update.RespawnInfo = BuildLocalRespawnInfo(evt.RespawnInfo);
                }
                listener.UpdateZone(update);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay zone statistics update failed: " + ex.Message);
            }
        }

        private Dictionary<uint, ulong> BuildLocalRespawnInfo(Dictionary<uint, ulong> replayRespawns)
        {
            Dictionary<uint, ulong> local = new Dictionary<uint, ulong>();
            if (replayRespawns == null)
            {
                return local;
            }

            foreach (KeyValuePair<uint, ulong> pair in replayRespawns)
            {
                local[pair.Key] = ConvertReplayServerMillisToLocal(pair.Value);
            }

            return local;
        }

        private ulong ConvertReplayServerMillisToLocal(ulong replayServerMillisAbsolute)
        {
            ZoneStatsReplayEvent phase = currentPhaseEvent ?? GetPhaseEventAt(replayTime);
            if (phase == null || !phase.HasPhaseStart)
            {
                return replayServerMillisAbsolute;
            }

            long replayNow = phase.PhaseStart + (long)Mathf.Max(0f, (replayTime - phase.Time) * 1000f);
            long delta = (long)replayServerMillisAbsolute - replayNow;
            long local = GetCurrentServerTimeMillis() + delta;
            return local < 0L ? 0UL : (ulong)local;
        }

        private static void TryForceHudSpectatorScreen()
        {
            try
            {
                // Hud.Screen.Spectator = 1 (enum value from decompiled assembly)
                // Hud.SetScreen is public — call directly via Singleton<Hud>.
                System.Type hudType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    hudType = asm.GetType("Hud");
                    if (!object.ReferenceEquals(hudType, null)) break;
                }
                if (object.ReferenceEquals(hudType, null)) return;

                var singletonType = typeof(Singleton<>).MakeGenericType(hudType);
                var instanceProp = singletonType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (object.ReferenceEquals(instanceProp, null)) return;

                object hud = instanceProp.GetValue(null, null);
                if (object.ReferenceEquals(hud, null)) return;

                var screenType = hudType.GetNestedType("Screen");
                if (object.ReferenceEquals(screenType, null)) return;
                object spectatorValue = System.Enum.ToObject(screenType, 2); // Spectator = 2 (Invisible=0, Game=1, Spectator=2)

                var setScreen = hudType.GetMethod("SetScreen",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (!object.ReferenceEquals(setScreen, null))
                {
                    setScreen.Invoke(hud, new object[] { spectatorValue });
                    Debug.Log("[BNL Replay] Forced Hud.SetScreen(Spectator) for TAB scoreboard");
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] TryForceHudSpectatorScreen failed: " + ex.Message);
            }
        }

        private void ApplyReplayPlayerStatsSnapshot()
        {
            if (replayPlayers.Count == 0)
            {
                return;
            }

            try
            {
                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                if (listener == null)
                {
                    return;
                }

                Protocol.ZoneUpdate update = new Protocol.ZoneUpdate();
                Protocol.MatchStats stats = new Protocol.MatchStats();
                stats.PlayerStats = BuildReplayPlayerStats();
                update.Statistics = stats;
                listener.UpdateZone(update);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Initial replay player stats update failed: " + ex.Message);
            }
        }

        private Dictionary<uint, Protocol.MatchPlayerStats> BuildReplayPlayerStats(Dictionary<uint, PlayerStatsReplayData> replayStats)
        {
            Dictionary<uint, Protocol.MatchPlayerStats> playerStats = new Dictionary<uint, Protocol.MatchPlayerStats>();
            for (int i = 0; i < replayPlayers.Count; i++)
            {
                PlayerReplayInfo player = replayPlayers[i];
                uint playerId;
                if (!uint.TryParse(player.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId))
                {
                    continue;
                }

                Protocol.MatchPlayerStats stats = new Protocol.MatchPlayerStats();
                stats.Team = TeamFromString(player.Team);
                PlayerStatsReplayData decoded;
                if (replayStats != null && replayStats.TryGetValue(playerId, out decoded))
                {
                    if (!string.IsNullOrEmpty(decoded.Team))
                    {
                        stats.Team = TeamFromString(decoded.Team);
                    }
                    stats.Kills = decoded.Kills;
                    stats.Deaths = decoded.Deaths;
                    stats.Assists = decoded.Assists;
                }
                playerStats[playerId] = stats;
            }

            return playerStats;
        }

        private Dictionary<uint, Protocol.MatchPlayerStats> BuildReplayPlayerStats()
        {
            return BuildReplayPlayerStats(null);
        }

        private static long GetCurrentServerTimeMillis()
        {
            try
            {
                IServerTime serverTime = Singleton<IServerTime>.Instance;
                if (!object.ReferenceEquals(serverTime, null))
                {
                    return serverTime.TimeMillis;
                }
            }
            catch { }

            return (long)(Time.realtimeSinceStartup * 1000f);
        }

        private static Protocol.MatchTeamStats BuildMatchTeamStats(MatchTeamStatsData data)
        {
            Protocol.MatchTeamStats stats = new Protocol.MatchTeamStats();
            stats.Warfare = data.Warfare;
            stats.Construction = data.Construction;
            stats.Tactics = data.Tactics;
            stats.Healing = data.Healing;
            return stats;
        }

        private void ApplyBuildStartEvent(ZoneServiceListener listener, BuildReplayEvent evt)
        {
            try
            {
                Protocol.BuildInfo info = new Protocol.BuildInfo();
                info.ToolIndex = 0;
                info.DeviceKey = evt.DeviceKey;
                info.BuildInsidePosition = evt.Inside;
                info.BuildOutsidePosition = evt.Outside;
                info.ShowGhost = evt.ShowGhost;
                try
                {
                    info.Direction = (Protocol.Direction2D)Enum.Parse(typeof(Protocol.Direction2D), evt.Direction, true);
                }
                catch
                {
                    info.Direction = Protocol.Direction2D.Front;
                }
                listener.DoStartBuild(evt.BuilderUnitId, info);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay build ghost event failed: " + ex.Message);
            }
        }

        private void ApplyDeviceBuiltEvent(ZoneServiceListener listener, BuildReplayEvent evt)
        {
            try
            {
                UnitMetadata builder;
                uint builderPlayerId;
                if (!unitMetadata.TryGetValue(evt.BuilderUnitId.ToString(CultureInfo.InvariantCulture), out builder) ||
                    !uint.TryParse(builder.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out builderPlayerId))
                {
                    return;
                }

                Vector3 position = evt.HasPosition ? evt.Position : new Vector3(evt.Outside.x, evt.Outside.y, evt.Outside.z);
                listener.DeviceBuilt(builderPlayerId, evt.DeviceKey, position);
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] Replay device built event failed: " + ex.Message);
            }
        }

        private void DestroyReplayObject(UnitMarker marker)
        {
            if (marker.RealUnit != null)
            {
                Destroy(marker.RealUnit.gameObject);
            }
            else if (marker.GameObject != null)
            {
                Destroy(marker.GameObject);
            }

            marker.GameObject = null;
            marker.RealUnit = null;
            marker.Label = null;
            marker.LastEventTime = float.MinValue;
        }

        private void ApplyUnitEvents(UnitMarker marker)
        {
            string unitId = marker.Track.UnitId;
            float prevTime = marker.LastEventTime;
            float curTime = replayTime;

            // When scrubbing backwards, reset and replay from start
            if (curTime < prevTime)
            {
                prevTime = float.MinValue;
            }

            // Apply gear switch events in chronological order, bypassing Catalogue via couchdb lookup
            List<UnitGearEvent> gearEvts;
            if (unitGearEvents.TryGetValue(unitId, out gearEvts))
            {
                for (int i = 0; i < gearEvts.Count; i++)
                {
                    UnitGearEvent evt = gearEvts[i];
                    if (evt.Time > prevTime && evt.Time <= curTime)
                    {
                        int gearIndex = FindGearIndexByHash(marker.Track.UnitId, evt.GearKeyHash);
                        TrySwitchGearDirect(marker.RealUnit.gameObject, gearIndex, evt.GearKeyHash);
                    }
                }
            }

            // Apply tool events in chronological order
            List<UnitToolEvent> toolEvts;
            if (unitToolEvents.TryGetValue(unitId, out toolEvts))
            {
                for (int i = 0; i < toolEvts.Count; i++)
                {
                    UnitToolEvent evt = toolEvts[i];
                    if (evt.Time > prevTime && evt.Time <= curTime)
                    {
                        try
                        {
                            if (evt.IsLoop)
                            {
                                UnitEventHelper.HandleToolFireLoop(marker.RealUnit, evt.ToolIndex, evt.Active);
                            }
                            else if (evt.IsHold)
                            {
                                UnitEventHelper.HandleToolHold(marker.RealUnit, evt.ToolIndex, evt.Active);
                            }
                            else
                            {
                                UnitEventHelper.HandleToolFire(marker.RealUnit, evt.ToolIndex);
                            }
                        }
                        catch { }

                        if (evt.IsLoop)
                        {
                            ReplayToolFireLoopSound(marker.RealUnit, evt.ToolIndex, evt.Active);
                        }
                        else if (evt.IsHold)
                        {
                            ReplayToolHoldSound(marker.RealUnit, evt.ToolIndex, evt.Active);
                        }
                        else
                        {
                            ReplayToolFireSound(marker.RealUnit, evt.ToolIndex);
                        }
                    }
                }
            }

            List<UnitCastEvent> castEvts;
            if (unitCastEvents.TryGetValue(unitId, out castEvts))
            {
                for (int i = 0; i < castEvts.Count; i++)
                {
                    UnitCastEvent evt = castEvts[i];
                    if (evt.Time > prevTime && evt.Time <= curTime)
                    {
                        ReplayToolCast(marker.RealUnit, evt);
                    }
                }
            }

            marker.LastEventTime = curTime;
        }

        private void ReplayToolFireSound(Unit unit, byte toolIndex)
        {
            if (unit == null)
            {
                return;
            }

            try
            {
                ToolFireEventArgs args = new ToolFireEventArgs();
                args.ToolIndex = toolIndex;
                GearSoundHandler[] handlers = unit.GetComponentsInChildren<GearSoundHandler>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].ToolFire(args); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ReplayToolFireSound failed: " + ex.Message);
            }
        }

        private void ReplayToolFireLoopSound(Unit unit, byte toolIndex, bool active)
        {
            if (unit == null)
            {
                return;
            }

            try
            {
                ToolFireLoopEventArgs args = new ToolFireLoopEventArgs();
                args.ToolIndex = toolIndex;
                args.Active = active;
                GearSoundHandler[] handlers = unit.GetComponentsInChildren<GearSoundHandler>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].ToolFireLoop(args); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ReplayToolFireLoopSound failed: " + ex.Message);
            }
        }

        private void ReplayToolHoldSound(Unit unit, byte toolIndex, bool active)
        {
            if (unit == null)
            {
                return;
            }

            try
            {
                ToolHoldEventArgs args = new ToolHoldEventArgs();
                args.ToolIndex = toolIndex;
                args.Active = active;
                GearSoundHandler[] handlers = unit.GetComponentsInChildren<GearSoundHandler>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].ToolHold(args); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ReplayToolHoldSound failed: " + ex.Message);
            }
        }

        private void ReplayToolCast(Unit unit, UnitCastEvent evt)
        {
            if (unit == null || evt == null)
            {
                return;
            }

            try
            {
                Protocol.CastData data = new Protocol.CastData();
                data.ToolIndex = evt.ToolIndex;
                data.ShotPos = evt.ShotOrigin;
                data.Shots = evt.Shots ?? new List<Protocol.ShotData>();
                if (evt.HasProjectileSpeed)
                {
                    data.UnitProjectileSpeed = evt.ProjectileSpeed;
                }

                try { UnitEventHelper.HandleToolCast(unit, data); } catch { }

                ToolCastEventArgs args = new ToolCastEventArgs();
                args.ToolIndex = evt.ToolIndex;
                args.ShotOrigin = evt.ShotOrigin;
                args.Shots = data.Shots;
                GearSoundHandler[] handlers = unit.GetComponentsInChildren<GearSoundHandler>(true);
                for (int i = 0; i < handlers.Length; i++)
                {
                    try { handlers[i].ToolCast(args); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[BNL Replay] ReplayToolCast failed: " + ex.Message);
            }
        }

        private void ApplyReplayUnitState(UnitMarker marker)
        {
            if (marker == null || marker.RealUnit == null || string.IsNullOrEmpty(marker.UnitId))
            {
                return;
            }

            UnitStateEvent state = GetUnitStateAt(marker.UnitId, replayTime);
            if (state == null)
            {
                return;
            }

            ZoneMessenger messenger = null;
            try { messenger = Singleton<ZoneMessenger>.Instance; } catch { }

            if (state.HasHealth && Math.Abs(marker.RealUnit.Health - state.Health) > 0.01f)
            {
                float oldHealth = marker.RealUnit.Health;
                marker.RealUnit.Health = state.Health;
                try
                {
                    if (messenger != null)
                    {
                        messenger.OnUnitHealthChange(marker.RealUnit, oldHealth, state.Health);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] OnUnitHealthChange failed: " + ex.Message);
                }
            }

            if (state.HasShield)
            {
                marker.RealUnit.Shield = state.Shield;
            }

            if (state.HasResource && Math.Abs(marker.RealUnit.Resource - state.Resource) > 0.01f)
            {
                float oldResource = marker.RealUnit.Resource;
                marker.RealUnit.Resource = state.Resource;
                try
                {
                    if (messenger != null)
                    {
                        messenger.OnUnitResourcesChange(marker.RealUnit, oldResource, state.Resource);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] OnUnitResourcesChange failed: " + ex.Message);
                }
            }

            if (state.HasDevices)
            {
                if (marker.RealUnit.Devices == null)
                {
                    marker.RealUnit.Devices = new Dictionary<int, Protocol.DeviceData>();
                }

                foreach (KeyValuePair<int, Protocol.DeviceData> item in state.Devices)
                {
                    marker.RealUnit.Devices[item.Key] = item.Value;
                }
            }

            if (state.HasEffects || state.HasBuffs)
            {
                try
                {
                    Protocol.UnitUpdate update = new Protocol.UnitUpdate();
                    if (state.HasEffects)
                    {
                        update.Effects = state.Effects;
                    }
                    if (state.HasBuffs)
                    {
                        update.Buffs = state.Buffs;
                    }
                    marker.RealUnit.UpdateData(update);
                }
                catch (Exception ex)
                {
                    Debug.Log("[BNL Replay] Unit.UpdateData effects/buffs failed: " + ex.Message);
                }
            }
        }

        private void ApplyRecordedUnitMoves(UnitMarker marker)
        {
            if (marker == null || marker.Track == null || marker.RealUnit == null || marker.Track.Points.Count == 0)
            {
                return;
            }

            if (replayTime < marker.LastMoveDispatchReplayTime)
            {
                marker.NextMoveIndex = 0;
            }

            int lastDueIndex = -1;
            while (marker.NextMoveIndex < marker.Track.Points.Count &&
                marker.Track.Points[marker.NextMoveIndex].Time <= replayTime)
            {
                lastDueIndex = marker.NextMoveIndex;
                marker.NextMoveIndex++;
            }

            if (lastDueIndex >= 0)
            {
                ReplayPoint point = marker.Track.Points[lastDueIndex];
                bool forceSnap = lastDueIndex == 0 || point.Time <= marker.SpawnTime + 0.05f || replayTime - marker.LastMoveDispatchReplayTime > 1.0f;
                DispatchUnitMove(marker, point, forceSnap);
            }

            marker.LastMoveDispatchReplayTime = replayTime;
        }

        private void MoveRealReplayUnit(UnitMarker marker, ReplaySample sample)
        {
            try
            {
                Unit unit = marker.RealUnit;
                unit.LocalVelocity = sample.HasLocalVelocity ? sample.LocalVelocity : Vector3.zero;
                unit.IsCrouch = sample.IsCrouch.HasValue && sample.IsCrouch.Value;
                unit.IsJump = sample.IsJump.HasValue && sample.IsJump.Value;
                unit.IsSprint = sample.IsSprint.HasValue && sample.IsSprint.Value;
                unit.IsWallClimb = sample.IsWallClimb.HasValue && sample.IsWallClimb.Value;
                unit.IsDash = sample.IsDash.HasValue && sample.IsDash.Value;
                unit.IsGroundSlam = sample.IsGroundSlam.HasValue && sample.IsGroundSlam.Value;
                unit.IsCommonMovementActive = true;
                unit.LocalVelocity = sample.HasLocalVelocity ? sample.LocalVelocity * 0.1f : Vector3.zero;
                ApplyReplayAnimationState(unit, sample);

                UnitMotor motor = unit.GetComponent<UnitMotor>();
                if (motor != null && motor.enabled)
                {
                    motor.enabled = false;
                }

                Rigidbody body = unit.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }

                bool snap = marker.LastMoveDispatchReplayTime == float.MinValue ||
                    replayTime < marker.LastMoveDispatchReplayTime ||
                    Vector3.Distance(unit.transform.position, sample.Position) > 4f;

                if (snap)
                {
                    unit.transform.position = sample.Position;
                    if (sample.HasRotation)
                    {
                        unit.transform.rotation = RotationToYawQuaternion(sample.Rotation);
                    }
                }
                else
                {
                    float smoothing = Mathf.Clamp01(Time.deltaTime * Mathf.Max(18f, 18f * speed));
                    unit.transform.position = Vector3.Lerp(unit.transform.position, sample.Position, smoothing);
                    if (sample.HasRotation)
                    {
                        unit.transform.rotation = Quaternion.Slerp(unit.transform.rotation, RotationToYawQuaternion(sample.Rotation), smoothing);
                    }
                }

                marker.LastMoveDispatchReplayTime = replayTime;
            }
            catch (Exception ex)
            {
                if (!realUnitMoveWarningShown)
                {
                    realUnitMoveWarningShown = true;
                    Debug.Log("[BNL Replay] Smoothed replay movement failed: " + ex.Message);
                }
            }
        }

        private static void ApplyReplayAnimationState(Unit unit, ReplaySample sample)
        {
            if (unit == null)
            {
                return;
            }

            try
            {
                UnitAnimation[] animations = unit.GetComponentsInChildren<UnitAnimation>(true);
                for (int i = 0; i < animations.Length; i++)
                {
                    UnitAnimation animation = animations[i];
                    if (animation == null)
                    {
                        continue;
                    }

                    animation.IsCrouch = sample.IsCrouch.HasValue && sample.IsCrouch.Value;
                    animation.IsJump = sample.IsJump.HasValue && sample.IsJump.Value;
                    animation.IsSprint = sample.IsSprint.HasValue && sample.IsSprint.Value;
                    animation.IsWallClimb = sample.IsWallClimb.HasValue && sample.IsWallClimb.Value;
                    animation.IsDash = sample.IsDash.HasValue && sample.IsDash.Value;
                    animation.IsGroundSlam = sample.IsGroundSlam.HasValue && sample.IsGroundSlam.Value;
                    animation.LocalVelocity = sample.HasLocalVelocity ? sample.LocalVelocity * 0.1f : Vector3.zero;
                }
            }
            catch { }
        }

        private void DispatchUnitMove(UnitMarker marker, ReplayPoint point, bool forceSnap)
        {
            try
            {
                Unit unit = marker.RealUnit;
                Protocol.ZoneTransform transform = BuildZoneTransform(point);
                transform.NoInterpolation = forceSnap || (point.NoInterpolation.HasValue && point.NoInterpolation.Value);

                ZoneServiceListener listener = Singleton<ZoneServiceListener>.Instance;
                uint unitId;
                if (listener != null && uint.TryParse(marker.UnitId, NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId))
                {
                    listener.UnitMove(unitId, (ulong)Mathf.Max(0f, point.Time * 1000f), transform);
                    return;
                }

                unit.LocalVelocity = point.HasLocalVelocity ? point.LocalVelocity : Vector3.zero;
                unit.IsCrouch = point.IsCrouch.HasValue && point.IsCrouch.Value;
                unit.IsJump = point.IsJump.HasValue && point.IsJump.Value;
                unit.IsSprint = point.IsSprint.HasValue && point.IsSprint.Value;
                unit.IsWallClimb = point.IsWallClimb.HasValue && point.IsWallClimb.Value;
                unit.IsDash = point.IsDash.HasValue && point.IsDash.Value;
                unit.IsGroundSlam = point.IsGroundSlam.HasValue && point.IsGroundSlam.Value;
                unit.transform.position = point.Position;
                if (point.HasRotation)
                {
                    unit.transform.rotation = RotationToQuaternion(point.Rotation);
                }
                return;
            }
            catch (Exception ex)
            {
                if (!realUnitMoveWarningShown)
                {
                    realUnitMoveWarningShown = true;
                    Debug.Log("[BNL Replay] Replay UnitMove dispatch failed; using transform only: " + ex.Message);
                }
            }

            marker.RealUnit.transform.position = point.Position;
            if (point.HasRotation)
            {
                marker.RealUnit.transform.rotation = RotationToQuaternion(point.Rotation);
            }
        }

        private static Protocol.ZoneTransform BuildZoneTransform(ReplayPoint point)
        {
            Protocol.ZoneTransform transform = new Protocol.ZoneTransform();
            transform.Position = point.Position;
            transform.Rotation = point.HasRotation ? RotationToVector3s(point.Rotation) : Vector3s.zero;
            transform.LocalVelocity = point.HasLocalVelocity ? VelocityToVector3s(point.LocalVelocity) : Vector3s.zero;
            transform.IsCrouch = point.IsCrouch.HasValue && point.IsCrouch.Value;
            transform.IsJump = point.IsJump.HasValue && point.IsJump.Value;
            transform.IsSprint = point.IsSprint.HasValue && point.IsSprint.Value;
            transform.IsWallClimb = point.IsWallClimb.HasValue && point.IsWallClimb.Value;
            transform.IsDash = point.IsDash.HasValue && point.IsDash.Value;
            transform.IsGroundSlam = point.IsGroundSlam.HasValue && point.IsGroundSlam.Value;
            return transform;
        }

        private static Vector3s RotationToVector3s(Vector3 rotation)
        {
            // sample.Rotation values are already in PackToShort format (raw ×10 shorts stored as floats).
            // Just clamp and cast — do NOT multiply by 10 again.
            Vector3s v = new Vector3s();
            v.x = (short)Mathf.Clamp(Mathf.RoundToInt(rotation.x), short.MinValue, short.MaxValue);
            v.y = (short)Mathf.Clamp(Mathf.RoundToInt(rotation.y), short.MinValue, short.MaxValue);
            v.z = (short)Mathf.Clamp(Mathf.RoundToInt(rotation.z), short.MinValue, short.MaxValue);
            return v;
        }

        private static Vector3s VelocityToVector3s(Vector3 velocity)
        {
            // sample.LocalVelocity is also in PackToShort format (raw ×10 shorts stored as floats).
            Vector3s v = new Vector3s();
            v.x = (short)Mathf.Clamp(Mathf.RoundToInt(velocity.x), short.MinValue, short.MaxValue);
            v.y = (short)Mathf.Clamp(Mathf.RoundToInt(velocity.y), short.MinValue, short.MaxValue);
            v.z = (short)Mathf.Clamp(Mathf.RoundToInt(velocity.z), short.MinValue, short.MaxValue);
            return v;
        }

        private void UpdateLabels()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                TextMesh label = markers[i].Label;
                if (label != null)
                {
                    label.transform.rotation = camera.transform.rotation;
                }
            }
        }

        private static Vector3 Sample(ReplayTrack track, float time)
        {
            return SampleReplay(track, time).Position;
        }

        private static ReplaySample SampleReplay(ReplayTrack track, float time)
        {
            if (track.Points.Count == 0)
            {
                return new ReplaySample(Vector3.zero);
            }

            if (time <= track.Points[0].Time)
            {
                return ReplaySample.FromPoint(track.Points[0]);
            }

            int last = track.Points.Count - 1;
            if (time >= track.Points[last].Time)
            {
                return ReplaySample.FromPoint(track.Points[last]);
            }

            for (int i = 0; i < last; i++)
            {
                ReplayPoint a = track.Points[i];
                ReplayPoint b = track.Points[i + 1];
                if (time < a.Time || time > b.Time)
                {
                    continue;
                }

                float span = Mathf.Max(0.001f, b.Time - a.Time);
                float t = Mathf.Clamp01((time - a.Time) / span);
                ReplaySample sample = new ReplaySample(Vector3.Lerp(a.Position, b.Position, t));
                if (a.HasRotation && b.HasRotation)
                {
                    sample.Rotation = Vector3.Lerp(a.Rotation, b.Rotation, t);
                    sample.HasRotation = true;
                }
                else if (a.HasRotation)
                {
                    sample.Rotation = a.Rotation;
                    sample.HasRotation = true;
                }
                else if (b.HasRotation)
                {
                    sample.Rotation = b.Rotation;
                    sample.HasRotation = true;
                }

                if (a.HasLocalVelocity && b.HasLocalVelocity)
                {
                    sample.LocalVelocity = Vector3.Lerp(a.LocalVelocity, b.LocalVelocity, t);
                    sample.HasLocalVelocity = true;
                }
                else if (a.HasLocalVelocity)
                {
                    sample.LocalVelocity = a.LocalVelocity;
                    sample.HasLocalVelocity = true;
                }

                ReplayPoint flags = t < 0.5f ? a : b;
                sample.IsCrouch = flags.IsCrouch;
                sample.IsJump = flags.IsJump;
                sample.IsSprint = flags.IsSprint;
                sample.IsWallClimb = flags.IsWallClimb;
                sample.IsDash = flags.IsDash;
                sample.IsGroundSlam = flags.IsGroundSlam;
                sample.NoInterpolation = null;

                return sample;
            }

            return ReplaySample.FromPoint(track.Points[last]);
        }

        private static ReplaySample SampleProjectile(ProjectileReplayObject projectile, float time)
        {
            if (projectile.Points.Count == 0)
            {
                return new ReplaySample(Vector3.zero);
            }
            if (time <= projectile.Points[0].Time)
            {
                return ReplaySample.FromPoint(projectile.Points[0]);
            }
            int last = projectile.Points.Count - 1;
            if (time >= projectile.Points[last].Time)
            {
                return ReplaySample.FromPoint(projectile.Points[last]);
            }
            for (int i = 0; i < last; i++)
            {
                ReplayPoint a = projectile.Points[i];
                ReplayPoint b = projectile.Points[i + 1];
                if (time < a.Time || time > b.Time) continue;
                float span = Mathf.Max(0.001f, b.Time - a.Time);
                float t = Mathf.Clamp01((time - a.Time) / span);
                ReplaySample sample = new ReplaySample(Vector3.Lerp(a.Position, b.Position, t));
                if (a.HasRotation && b.HasRotation)
                {
                    sample.Rotation = Vector3.Lerp(a.Rotation, b.Rotation, t);
                    sample.HasRotation = true;
                }
                else if (a.HasRotation || b.HasRotation)
                {
                    sample.Rotation = a.HasRotation ? a.Rotation : b.Rotation;
                    sample.HasRotation = true;
                }
                return sample;
            }
            return ReplaySample.FromPoint(projectile.Points[last]);
        }

        private string FindProjectilePrefab(uint keyHash)
        {
            if (keyHash == 0) return "";
            string cached;
            if (projectilePrefabCache.TryGetValue(keyHash, out cached)) return cached;

            string prefab = "";
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (catalogue != null)
                {
                    Key key = KeyFromHash(keyHash);
                    foreach (Protocol.Card card in catalogue.All)
                    {
                        Protocol.CardProjectile projectile = card as Protocol.CardProjectile;
                        if (projectile != null && projectile.Key == key && !string.IsNullOrEmpty(projectile.Prefab))
                        {
                            prefab = projectile.Prefab;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(prefab))
            {
                prefab = FindProjectilePrefabFromCouchdb(keyHash);
            }

            projectilePrefabCache[keyHash] = prefab ?? "";
            return projectilePrefabCache[keyHash];
        }

        private string FindAbilityPrefab(uint keyHash)
        {
            if (keyHash == 0) return "";
            string cached;
            if (abilityPrefabCache.TryGetValue(keyHash, out cached)) return cached;

            string prefab = "";
            try
            {
                Catalogue catalogue = Singleton<Catalogue>.Instance;
                if (catalogue != null)
                {
                    Key key = KeyFromHash(keyHash);
                    foreach (Protocol.Card card in catalogue.All)
                    {
                        Protocol.CardAbility ability = card as Protocol.CardAbility;
                        if (ability != null && ability.Key == key && !string.IsNullOrEmpty(ability.Prefab))
                        {
                            prefab = ability.Prefab;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(prefab))
            {
                prefab = FindCardPrefabFromCouchdb(keyHash);
            }

            abilityPrefabCache[keyHash] = prefab ?? "";
            return abilityPrefabCache[keyHash];
        }

        private static bool IsNinjaVanishAbility(uint keyHash)
        {
            return keyHash == 0x268AA3E6u || keyHash == 0x1EE5AFDEu;
        }

        private static string FindProjectilePrefabFromCouchdb(uint keyHash)
        {
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (textAsset == null) return "";
                string json = textAsset.text;
                string keyDecimal = keyHash.ToString(CultureInfo.InvariantCulture);
                Match docMatch = Regex.Match(json, "\"_id\"\\s*:\\s*\"" + keyDecimal + "\"", RegexOptions.CultureInvariant);
                if (!docMatch.Success) return "";
                int docEnd = json.IndexOf("\"_id\"", docMatch.Index + 1, StringComparison.Ordinal);
                if (docEnd < 0) docEnd = json.Length;
                string docSlice = json.Substring(docMatch.Index, docEnd - docMatch.Index);
                Match prefabMatch = Regex.Match(docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                return prefabMatch.Success ? prefabMatch.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        private static string FindCardPrefabFromCouchdb(uint keyHash)
        {
            try
            {
                UnityEngine.Object textObj = Resources.Load("couchdb");
                TextAsset textAsset = textObj as TextAsset;
                if (textAsset == null) return "";
                string json = textAsset.text;
                string keyDecimal = keyHash.ToString(CultureInfo.InvariantCulture);
                Match docMatch = Regex.Match(json, "\"_id\"\\s*:\\s*\"" + keyDecimal + "\"", RegexOptions.CultureInvariant);
                if (!docMatch.Success) return "";
                int docEnd = json.IndexOf("\"_id\"", docMatch.Index + 1, StringComparison.Ordinal);
                if (docEnd < 0) docEnd = json.Length;
                string docSlice = json.Substring(docMatch.Index, docEnd - docMatch.Index);
                Match prefabMatch = Regex.Match(docSlice, "\"prefab\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                return prefabMatch.Success ? prefabMatch.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        private static Quaternion RotationToQuaternion(Vector3 rotation)
        {
            return Quaternion.Euler(rotation.x / 10f, rotation.y / 10f, rotation.z / 10f);
        }

        private static Quaternion RotationToYawQuaternion(Vector3 rotation)
        {
            return Quaternion.Euler(0f, rotation.y / 10f, 0f);
        }

        private void ResetPlayback()
        {
            if (!loaded)
            {
                return;
            }

            replayTime = startTime;
            playing = false;
            UpdateMarkerPositions();
            ShowStatus("Replay reset");
        }

        private void ClearMarkers()
        {
            DestroyMarkerObjects();
            tracks.Clear();
            unitMetadata.Clear();
            unitGearEvents.Clear();
            unitToolEvents.Clear();
            reloadEvents.Clear();
            buildEvents.Clear();
            damageEvents.Clear();
            zoneStatsEvents.Clear();
            projectileObjects.Clear();
            projectilePrefabCache.Clear();
            abilityEvents.Clear();
            abilityPrefabCache.Clear();
            barrierEvents.Clear();
            channelEvents.Clear();
            dashChargeEvents.Clear();
            recallEvents.Clear();
            portalTeleportEvents.Clear();
            pickupTakenEvents.Clear();
            buildPlacementByUnitId.Clear();
            gearInfoCache.Clear();
            unitDropTimes.Clear();
            unitCardCache.Clear();
            blockTimeline.Clear();
            initialBlockByCell.Clear();
            replayPlayers.Clear();
            unitStateEvents.Clear();
            nextBlockTimelineIndex = 0;
            blockTimelineAppliedThrough = float.MinValue;
            lastGlobalEventTime = float.MinValue;
            currentPhaseEvent = null;
            followPlayerIndex = -1;
        }

        private void DestroyMarkerObjects()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                DestroyReplayObject(markers[i]);
            }

            markers.Clear();
            for (int i = 0; i < projectileObjects.Count; i++)
            {
                if (projectileObjects[i].GameObject != null)
                {
                    Destroy(projectileObjects[i].GameObject);
                    projectileObjects[i].GameObject = null;
                }
            }
            realUnitSpawnWarningShown = false;
            realUnitMoveWarningShown = false;
        }

        private void ClearMap()
        {
            for (int i = 0; i < mapBlocks.Count; i++)
            {
                if (mapBlocks[i] != null)
                {
                    Destroy(mapBlocks[i]);
                }
            }

            mapBlocks.Clear();
        }

        private void ShowStatus(string message)
        {
            status = message;
            statusVisibleUntil = Time.realtimeSinceStartup + 6f;
            Debug.Log("[BNL Replay] " + message + (string.IsNullOrEmpty(replayPath) ? "" : " (" + replayPath + ")"));
        }

        private sealed class ReplayTrack
        {
            public string UnitId;
            public readonly List<ReplayPoint> Points = new List<ReplayPoint>();
        }

        private sealed class ReplayPoint
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public Vector3 Rotation;
            public bool HasRotation;
            public Vector3 LocalVelocity;
            public bool HasLocalVelocity;
            public bool? IsCrouch;
            public bool? IsJump;
            public bool? IsSprint;
            public bool? IsWallClimb;
            public bool? IsDash;
            public bool? IsGroundSlam;
            public bool? NoInterpolation;

            public ReplayPoint(float time, Vector3 position)
            {
                Time = time;
                Position = position;
            }
        }

        private sealed class ReplaySample
        {
            public Vector3 Position;
            public Vector3 Rotation;
            public bool HasRotation;
            public Vector3 LocalVelocity;
            public bool HasLocalVelocity;
            public bool? IsCrouch;
            public bool? IsJump;
            public bool? IsSprint;
            public bool? IsWallClimb;
            public bool? IsDash;
            public bool? IsGroundSlam;
            public bool? NoInterpolation;

            public ReplaySample(Vector3 position)
            {
                Position = position;
            }

            public static ReplaySample FromPoint(ReplayPoint point)
            {
                ReplaySample sample = new ReplaySample(point.Position);
                sample.Rotation = point.Rotation;
                sample.HasRotation = point.HasRotation;
                sample.LocalVelocity = point.LocalVelocity;
                sample.HasLocalVelocity = point.HasLocalVelocity;
                sample.IsCrouch = point.IsCrouch;
                sample.IsJump = point.IsJump;
                sample.IsSprint = point.IsSprint;
                sample.IsWallClimb = point.IsWallClimb;
                sample.IsDash = point.IsDash;
                sample.IsGroundSlam = point.IsGroundSlam;
                sample.NoInterpolation = point.NoInterpolation;
                return sample;
            }
        }

        private sealed class UnitMarker
        {
            public string UnitId;
            public ReplayTrack Track;
            public UnitMetadata Metadata;
            public int ColorIndex;
            public GameObject GameObject;
            public TextMesh Label;
            public Unit RealUnit;
            public float LastEventTime = float.MinValue;
            public float LastMoveDispatchReplayTime = float.MinValue;
            public int NextMoveIndex;
            public float SpawnTime = float.MinValue;
            public float DropTime = float.MaxValue;
            public bool IsStaticUnit; // no track — position is fixed at spawn position
        }

        private sealed class UnitMetadata
        {
            public string UnitId;
            public string KeyHash;
            public string KeyName;
            public string Team;
            public string PlayerId;
            public string OwnerId;
            public string SkinKeyHash;
            public string SkinName;
            public string GearKeyHashes;
            public string GearNames;
            public Vector3 SpawnPosition;
            public bool HasSpawnPosition;
            public float SpawnTime = float.MinValue;
        }

        private sealed class MapBlock
        {
            public int X;
            public int Y;
            public int Z;
            public int Id;
        }

        private sealed class GearInfo
        {
            public readonly string GearTag;
            public readonly string AnimationTag;
            public GearInfo(string gearTag, string animationTag) { GearTag = gearTag; AnimationTag = animationTag; }
        }

        private sealed class UnitCardInfo
        {
            public readonly string Prefab;
            public readonly string UnitType;
            public readonly bool IsDropPoint;
            public UnitCardInfo(string prefab, string unitType, bool isDropPoint) { Prefab = prefab; UnitType = unitType; IsDropPoint = isDropPoint; }
        }

        private sealed class UnitGearEvent
        {
            public readonly float Time;
            public readonly uint GearKeyHash;
            public UnitGearEvent(float time, uint gearKeyHash) { Time = time; GearKeyHash = gearKeyHash; }
        }

        private sealed class UnitToolEvent
        {
            public readonly float Time;
            public readonly byte ToolIndex;
            public readonly bool IsLoop;
            public readonly bool IsHold;
            public readonly bool Active;
            public UnitToolEvent(float time, byte toolIndex, bool isLoop, bool isHold, bool active) { Time = time; ToolIndex = toolIndex; IsLoop = isLoop; IsHold = isHold; Active = active; }
        }

        private sealed class UnitCastEvent
        {
            public float Time;
            public byte ToolIndex;
            public Vector3 ShotOrigin;
            public List<Protocol.ShotData> Shots;
            public bool HasProjectileSpeed;
            public float ProjectileSpeed;
        }

        private sealed class PlayerReplayInfo
        {
            public string PlayerId;
            public string Nickname;
            public string SteamId;
            public string Team;
            public string UnitId;
            public string UnitName;
        }

        private sealed class UnitStateEvent
        {
            public float Time;
            public float Health;
            public bool HasHealth;
            public float Shield;
            public bool HasShield;
            public float Resource;
            public bool HasResource;
            public Dictionary<int, Protocol.DeviceData> Devices;
            public bool HasDevices;
            public Dictionary<Key, ulong?> Effects;
            public bool HasEffects;
            public Dictionary<Protocol.BuffType, float> Buffs;
            public bool HasBuffs;
        }

        private struct MatchTeamStatsData
        {
            public int Warfare;
            public int Construction;
            public int Tactics;
            public int Healing;
        }

        private sealed class ZoneStatsReplayEvent
        {
            public float Time;
            public string Phase;
            public long PhaseStart;
            public long PhaseEnd;
            public bool HasPhaseStart;
            public bool HasPhaseEnd;
            public bool HasTeam1;
            public bool HasTeam2;
            public MatchTeamStatsData Team1;
            public MatchTeamStatsData Team2;
            public Dictionary<uint, ulong> RespawnInfo;
            public Dictionary<uint, PlayerStatsReplayData> PlayerStats;
        }

        private sealed class PlayerStatsReplayData
        {
            public string Team;
            public int Kills;
            public int Deaths;
            public int Assists;
        }

        private sealed class ReloadReplayEvent
        {
            public float Time;
            public uint UnitId;
            public bool IsStart;
        }

        private sealed class KillReplayEvent
        {
            public float Time;
            public uint DeadUnitId;
            public bool HasDeadPlayerId;
            public uint DeadPlayerId;
            public bool HasKillerPlayerId;
            public uint KillerPlayerId;
            public List<uint> Assistants;
            public Key DamageSource;
            public bool HasSourcePosition;
            public Vector3 SourcePosition;
            public bool Crit;
        }

        private sealed class DamageReplayEvent
        {
            public float Time;
            public uint TargetUnitId;
            public bool HasSourceUnitId;
            public uint SourceUnitId;
            public float Damage;
            public float InitialDamage;
            public bool Crit;
            public bool HasImpact;
            public Key Impact;
        }

        private sealed class AbilityReplayEvent
        {
            public float Time;
            public uint UnitId;
            public uint AbilityKeyHash;
            public bool HasShotPosition;
            public Vector3 ShotPosition;
            public List<Protocol.ShotData> Shots;
        }

        private sealed class BuildReplayEvent
        {
            public float Time;
            public float DeviceTime;
            public uint BuilderUnitId;
            public string BuiltUnitId;
            public Key DeviceKey;
            public string DeviceName;
            public Vector3s Inside;
            public Vector3s Outside;
            public bool HasSurface;
            public string Direction;
            public bool ShowGhost;
            public Vector3 Position;
            public bool HasPosition;
        }

        private sealed class ImpactReplayEvent
        {
            public float Time;
            public Key ImpactKey;
            public bool HasSourceKey;
            public Key SourceKey;
            public bool HasCasterUnitId;
            public uint CasterUnitId;
            public bool HasCasterPlayerId;
            public uint CasterPlayerId;
            public Vector3 InsidePoint;
            public Vector3 ShotPos;
            public Vector3s Normal;
            public List<uint> HitUnits;
            public bool Crit;
        }

        private sealed class BarrierReplayEvent
        {
            public float Time;
            public List<Protocol.BarrierLabel> Labels;
        }

        private sealed class ChannelReplayEvent
        {
            public float Time;
            public string Phase;
            public uint UnitId;
            public byte? ToolIndex;
            public float? HitX;
            public float? HitY;
            public float? HitZ;
        }

        private sealed class DashChargeReplayEvent
        {
            public float Time;
            public string Phase;
            public uint UnitId;
            public byte? ToolIndex;
        }

        private sealed class RecallReplayEvent
        {
            public float Time;
            public string Phase;
            public uint UnitId;
            public float? Duration;
            public ulong? EndTime;
        }

        private sealed class PortalTeleportReplayEvent
        {
            public float Time;
            public uint UnitId;
            public uint PortalFromId;
            public uint PortalToId;
        }

        private sealed class PickupTakenReplayEvent
        {
            public float Time;
            public uint PlayerId;
            public uint PickupKeyHash;
        }

        private sealed class ProjectileReplayObject
        {
            public string ProjectileId;
            public uint KeyHash;
            public float SpawnTime;
            public float DropTime;
            public float Speed;
            public bool HasSpeed;
            public readonly List<ReplayPoint> Points = new List<ReplayPoint>();
            public GameObject GameObject;
        }

        private sealed class BlockTimelineEvent
        {
            public float Time;
            public short X;
            public short Y;
            public short Z;
            public ushort Id;
            public byte Damage;
            public ushort Vdata;
            public byte Ldata;
        }
    }
}
