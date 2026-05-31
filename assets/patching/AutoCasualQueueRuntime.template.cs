namespace BnlCommunityFixes
{
    public sealed class AutoCasualQueueRuntime : UnityEngine.MonoBehaviour
    {
        private static AutoCasualQueueRuntime instance;
        private bool wasInCustomGame;
        private bool leaveRequestedForMatch;
        private MatchmakerStateType lastLoggedState = MatchmakerStateType.None;

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

        private void Update()
        {
            if (!RuntimeFeatureState.AutoCasualQueueEnabled) { wasInCustomGame = false; leaveRequestedForMatch = false; lastLoggedState = MatchmakerStateType.None; return; }
            try
            {
                CustomGameData customGameData = Singleton<CustomGameData>.Instance;
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                NetworkDispatcher dispatcher = Singleton<NetworkDispatcher>.Instance;
                if (customGameData == null || matchmakerData == null || dispatcher == null) return;

                bool isInCustomGame = customGameData.IsCustomGame;
                MatchmakerStateType currentState = matchmakerData.State != null ? matchmakerData.State.State : MatchmakerStateType.None;

                if (currentState != lastLoggedState)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: matchmaker state=" + currentState + " inCustom=" + isInCustomGame);
                    lastLoggedState = currentState;
                }

                if (currentState != MatchmakerStateType.Confirming)
                {
                    leaveRequestedForMatch = false;
                }

                if (isInCustomGame && !wasInCustomGame)
                {
                    if (currentState == MatchmakerStateType.None)
                    {
                        UnityEngine.Debug.Log("BNL auto casual queue: entering casual queue from custom game");
                        dispatcher.ServiceMatchmaker.EnterQueue(CatalogueHelper.ModeFriendly.Key);
                    }
                }

                wasInCustomGame = isInCustomGame;

                if (isInCustomGame && currentState == MatchmakerStateType.Confirming && !leaveRequestedForMatch)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: leaving custom game after match found");
                    ZoneData zoneData = Singleton<ZoneData>.Instance;
                    if (zoneData != null && zoneData.IsCustomGame)
                    {
                        dispatcher.ServiceZone.ExitMatch();
                    }
                    else
                    {
                        customGameData.LeaveGame();
                    }
                    leaveRequestedForMatch = true;
                }
            }
            catch { }
        }
    }
}
