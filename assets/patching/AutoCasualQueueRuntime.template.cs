namespace BnlCommunityFixes
{
    public sealed class AutoCasualQueueRuntime : UnityEngine.MonoBehaviour
    {
        private static AutoCasualQueueRuntime instance;
        private bool wasInQueueableActivity;
        private bool autoQueueSuppressed;
        private bool leaveRequestedForMatch;
        private MatchmakerStateType lastLoggedState = MatchmakerStateType.None;
        private int lastLoggedPlayersInQueue = -1;
        private string lastLoggedQueueMode;
        private bool lastLoggedInQueueableActivity;
        private float nextQueueAttemptTime;
        private float timeAssaultLaunchRecoveryUntil;
        private UnityEngine.GUIStyle overlayBoxStyle;
        private UnityEngine.GUIStyle overlayLabelStyle;
        private const float QueueRetrySeconds = 2f;
        private const float TimeAssaultLaunchRecoverySeconds = 8f;
        private const float TimeAssaultLaunchRetrySeconds = 0.5f;

        static AutoCasualQueueRuntime()
        {
            RuntimeFeatureState.ConfigureAutoCasualQueue(true, $(Format-BoolLiteral $AutoCasualQueueConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void EnsureInstance()
        {
            if (instance != null) return;
            UnityEngine.GameObject go = UnityEngine.GameObject.Find("BNL_AUTO_CASUAL_QUEUE");
            if (go == null) { go = new UnityEngine.GameObject("BNL_AUTO_CASUAL_QUEUE"); UnityEngine.Object.DontDestroyOnLoad(go); }
            instance = go.GetComponent<AutoCasualQueueRuntime>();
            if (instance == null) instance = go.AddComponent<AutoCasualQueueRuntime>();
        }

        public static void NotifyTimeAssaultMenuShown()
        {
            EnsureInstance();
            if (instance != null)
            {
                instance.TryEnterQueueFromTimeAssaultMenu();
            }
        }

        public static void WireTimeAssaultPlayButton(UiMenuTimeTrial menu)
        {
            EnsureInstance();
            if (instance != null)
            {
                instance.AttachTimeAssaultPlayButton(menu);
            }
        }

        private void Update()
        {
            if (!RuntimeFeatureState.AutoCasualQueueEnabled) { wasInQueueableActivity = false; autoQueueSuppressed = false; leaveRequestedForMatch = false; lastLoggedState = MatchmakerStateType.None; lastLoggedPlayersInQueue = -1; lastLoggedQueueMode = null; lastLoggedInQueueableActivity = false; nextQueueAttemptTime = 0f; timeAssaultLaunchRecoveryUntil = 0f; return; }
            try
            {
                CustomGameData customGameData = Singleton<CustomGameData>.Instance;
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                NetworkDispatcher dispatcher = Singleton<NetworkDispatcher>.Instance;
                if (matchmakerData == null || dispatcher == null) return;

                bool isInCustomGame = customGameData != null && customGameData.IsCustomGame;
                bool isInTimeAssault = IsInTimeAssault();
                bool isInQueueableActivity = isInCustomGame || isInTimeAssault;
                MatchmakerStateType currentState = matchmakerData.State != null ? matchmakerData.State.State : MatchmakerStateType.None;
                MatchmakerStateType previousState = lastLoggedState;
                string queueMode = GetQueueMode(matchmakerData);
                int playersInQueue = matchmakerData.PlayersInQueue;

                if (currentState != lastLoggedState || playersInQueue != lastLoggedPlayersInQueue || queueMode != lastLoggedQueueMode || isInQueueableActivity != lastLoggedInQueueableActivity)
                {
                    UnityEngine.Debug.Log(
                        "BNL auto casual queue: state=" + currentState +
                        " queueMode=" + queueMode +
                        " playersInQueue=" + playersInQueue +
                        " inCustom=" + isInCustomGame +
                        " inTimeAssault=" + isInTimeAssault +
                        " queueable=" + isInQueueableActivity);
                    lastLoggedState = currentState;
                    lastLoggedPlayersInQueue = playersInQueue;
                    lastLoggedQueueMode = queueMode;
                    lastLoggedInQueueableActivity = isInQueueableActivity;
                }

                if (currentState != MatchmakerStateType.Confirming)
                {
                    leaveRequestedForMatch = false;
                }

                if (isInQueueableActivity && !wasInQueueableActivity)
                {
                    autoQueueSuppressed = false;
                    nextQueueAttemptTime = 0f;
                }
                else if (!isInQueueableActivity)
                {
                    autoQueueSuppressed = false;
                }

                // The player clicked the normal Leave Queue button while remaining in the
                // custom game/time-assault activity. Do not treat that InQueue -> None
                // transition as a dropped request and immediately enqueue them again.
                // Entering a new side activity (or toggling this feature) clears the latch.
                if (isInQueueableActivity &&
                    previousState == MatchmakerStateType.InQueue &&
                    currentState == MatchmakerStateType.None)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: manual queue leave detected; automatic requeue suppressed until activity exit");
                    autoQueueSuppressed = true;
                    nextQueueAttemptTime = 0f;
                    timeAssaultLaunchRecoveryUntil = 0f;
                }

                wasInQueueableActivity = isInQueueableActivity;

                if (isInQueueableActivity && !autoQueueSuppressed && currentState == MatchmakerStateType.None && UnityEngine.Time.time >= nextQueueAttemptTime)
                {
                    UnityEngine.Debug.Log(
                        "BNL auto casual queue: attempting EnterQueue(" + CatalogueHelper.ModeFriendly.Key +
                        ") from side activity; state=" + currentState +
                        " queueMode=" + queueMode +
                        " playersInQueue=" + playersInQueue +
                        " t=" + UnityEngine.Time.time);
                    dispatcher.ServiceMatchmaker.EnterQueue(CatalogueHelper.ModeFriendly.Key);
                    nextQueueAttemptTime = UnityEngine.Time.time + QueueRetrySeconds;
                }

                if (!autoQueueSuppressed && timeAssaultLaunchRecoveryUntil > UnityEngine.Time.time && currentState == MatchmakerStateType.None && UnityEngine.Time.time >= nextQueueAttemptTime)
                {
                    UnityEngine.Debug.Log(
                        "BNL auto casual queue: launch recovery EnterQueue(" + CatalogueHelper.ModeFriendly.Key +
                        "); state=" + currentState +
                        " queueMode=" + queueMode +
                        " playersInQueue=" + playersInQueue +
                        " t=" + UnityEngine.Time.time);
                    dispatcher.ServiceMatchmaker.EnterQueue(CatalogueHelper.ModeFriendly.Key);
                    nextQueueAttemptTime = UnityEngine.Time.time + TimeAssaultLaunchRetrySeconds;
                }

                if ((!isInQueueableActivity && timeAssaultLaunchRecoveryUntil <= UnityEngine.Time.time) || currentState != MatchmakerStateType.None)
                {
                    nextQueueAttemptTime = 0f;
                }

                if (isInQueueableActivity && currentState == MatchmakerStateType.Confirming && !leaveRequestedForMatch)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: leaving side activity after match found");
                    ZoneData zoneData = Singleton<ZoneData>.Instance;
                    if (zoneData != null && (zoneData.IsCustomGame || zoneData.IsTimeTrial))
                    {
                        dispatcher.ServiceZone.ExitMatch();
                    }
                    else if (customGameData != null && customGameData.IsCustomGame)
                    {
                        customGameData.LeaveGame();
                    }
                    leaveRequestedForMatch = true;
                }
            }
            catch { }
        }

        private void OnGUI()
        {
            if (!ShouldShowQueueOverlay()) return;

            EnsureOverlayStyles();

            MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
            int playersInQueue = matchmakerData != null ? matchmakerData.PlayersInQueue : 0;
            string label = "Players in queue: " + playersInQueue;

            float width = 260f;
            float height = 34f;
            UnityEngine.Rect rect = new UnityEngine.Rect((UnityEngine.Screen.width - width) * 0.5f, 24f, width, height);
            UnityEngine.GUI.Box(rect, string.Empty, overlayBoxStyle);
            UnityEngine.GUI.Label(rect, label, overlayLabelStyle);
        }

        private bool ShouldShowQueueOverlay()
        {
            if (!RuntimeFeatureState.AutoCasualQueueEnabled) return false;

            try
            {
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                if (matchmakerData == null || matchmakerData.State == null) return false;
                return IsInQueueableActivity() && matchmakerData.State.State == MatchmakerStateType.InQueue;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureOverlayStyles()
        {
            if (overlayBoxStyle == null)
            {
                overlayBoxStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.box);
                overlayBoxStyle.alignment = UnityEngine.TextAnchor.MiddleCenter;
                overlayBoxStyle.padding = new UnityEngine.RectOffset(10, 10, 6, 6);
                overlayBoxStyle.normal.textColor = UnityEngine.Color.white;
            }

            if (overlayLabelStyle == null)
            {
                overlayLabelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label);
                overlayLabelStyle.alignment = UnityEngine.TextAnchor.MiddleCenter;
                overlayLabelStyle.fontSize = 16;
                overlayLabelStyle.fontStyle = UnityEngine.FontStyle.Bold;
                overlayLabelStyle.normal.textColor = UnityEngine.Color.white;
            }
        }

        private bool IsInQueueableActivity()
        {
            CustomGameData customGameData = Singleton<CustomGameData>.Instance;
            return (customGameData != null && customGameData.IsCustomGame) || IsInTimeAssault() || IsOnTimeAssaultMenu();
        }

        private bool IsInTimeAssault()
        {
            ZoneData zoneData = Singleton<ZoneData>.Instance;
            return zoneData != null && zoneData.IsTimeTrial;
        }

        private bool IsOnTimeAssaultMenu()
        {
            try
            {
                UnityEngine.Object menuObject = UnityEngine.Object.FindObjectOfType(typeof(UiMenuTimeTrial));
                UiMenuTimeTrial menu = menuObject as UiMenuTimeTrial;
                return menu != null && menu.gameObject != null && menu.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private string GetQueueMode(MatchmakerData matchmakerData)
        {
            if (matchmakerData == null || matchmakerData.State == null || matchmakerData.State.QueueGameMode == null)
            {
                return "null";
            }

            try
            {
                return matchmakerData.State.QueueGameMode.Value.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        private void TryEnterQueueFromTimeAssaultMenu()
        {
            if (!RuntimeFeatureState.AutoCasualQueueEnabled) return;

            try
            {
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                NetworkDispatcher dispatcher = Singleton<NetworkDispatcher>.Instance;
                if (matchmakerData == null || dispatcher == null || matchmakerData.State == null) return;
                if (matchmakerData.State.State != MatchmakerStateType.None) return;

                string queueMode = GetQueueMode(matchmakerData);
                UnityEngine.Debug.Log(
                    "BNL auto casual queue: UiMenuTimeTrial.OnShow requesting EnterQueue(" + CatalogueHelper.ModeFriendly.Key +
                    "); state=" + matchmakerData.State.State +
                    " queueMode=" + queueMode +
                    " playersInQueue=" + matchmakerData.PlayersInQueue);
                dispatcher.ServiceMatchmaker.EnterQueue(CatalogueHelper.ModeFriendly.Key);
                nextQueueAttemptTime = UnityEngine.Time.time + QueueRetrySeconds;
            }
            catch { }
        }

        private void AttachTimeAssaultPlayButton(UiMenuTimeTrial menu)
        {
            if (menu == null || menu.Play == null) return;
            menu.Play.onClick.RemoveListener(OnTimeAssaultPlayClicked);
            menu.Play.onClick.AddListener(OnTimeAssaultPlayClicked);
        }

        private void OnTimeAssaultPlayClicked()
        {
            timeAssaultLaunchRecoveryUntil = UnityEngine.Time.time + TimeAssaultLaunchRecoverySeconds;
            nextQueueAttemptTime = 0f;
            UnityEngine.Debug.Log(
                "BNL auto casual queue: Time Assault play clicked; enabling launch recovery window until t=" +
                timeAssaultLaunchRecoveryUntil);
        }
    }
}
