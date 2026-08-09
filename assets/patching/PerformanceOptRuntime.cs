using UnityEngine;
using Protocol;

namespace BnlCommunityFixes
{
    public static class PerformanceOptRuntime
    {
        private static readonly System.Collections.Generic.List<GuiHealthBarMaker> ActiveHealthbarMakers = new System.Collections.Generic.List<GuiHealthBarMaker>();
        // Mirror set for O(1) Contains checks instead of O(n) list scans
        private static readonly System.Collections.Generic.HashSet<GuiHealthBarMaker> ActiveHealthbarMakersSet = new System.Collections.Generic.HashSet<GuiHealthBarMaker>();
        // Reusable output list for GetActiveHealthbarMakers — avoids allocation every frame
        private static readonly System.Collections.Generic.List<GuiHealthBarMaker> FilteredHealthbarMakers = new System.Collections.Generic.List<GuiHealthBarMaker>();
        private static bool activeListDirty = true;
        private static bool healthChangedThisFrame = false;
        private static bool healthEventSubscribed = false;

        private const int MinimapUpdateInterval = 6;
        private const int GravityTrapUpdateInterval = 6;
        // Team overlay WSI reconciliation is expensive because it rescans units and
        // runs O(n^2)-style list reconciliation inside GuiWorldSpaceIndicatorFactory.
        // Existing WSIs continue tracking targets on their own, so we can sync the
        // population much less often without making the overlay disappear.
        private const int WsiTeamOverlayUpdateInterval = 30;

        public static bool ShouldThrottleMinimapUpdate()
        {
            return (UnityEngine.Time.frameCount % MinimapUpdateInterval) != 0;
        }

        public static bool ShouldThrottleGravityTrapEffects()
        {
            return (UnityEngine.Time.frameCount % GravityTrapUpdateInterval) != 0;
        }

        private const int FanAnimatorUpdateInterval = 4;

        public static bool ShouldThrottleFanAnimator()
        {
            return (UnityEngine.Time.frameCount % FanAnimatorUpdateInterval) != 0;
        }

        public static bool ShouldThrottleWsiTeamOverlay()
        {
            return (UnityEngine.Time.frameCount % WsiTeamOverlayUpdateInterval) != 0;
        }

        private const int FrontPlayersUpdateInterval = 6;

        public static bool ShouldSkipFrontPlayersUpdate()
        {
            return (UnityEngine.Time.frameCount % FrontPlayersUpdateInterval) != 0;
        }

        private static void EnsureHealthEventSubscribed()
        {
            if (healthEventSubscribed) return;
            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger == null) return;
                var field = typeof(ZoneMessenger).GetField("OnGlobalUnitHealthChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (object.ReferenceEquals(field, null)) return;
                var eventSource = field.GetValue(messenger);
                if (object.ReferenceEquals(eventSource, null)) return;
                var subscribeMethod = eventSource.GetType().GetMethod("Subscribe");
                if (object.ReferenceEquals(subscribeMethod, null)) return;
                var handler = System.Delegate.CreateDelegate(
                    typeof(System.Action<>).MakeGenericType(typeof(GlobalUnitHealthChangeArgs)),
                    typeof(PerformanceOptRuntime).GetMethod("OnHealthChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
                if (object.ReferenceEquals(handler, null)) return;
                subscribeMethod.Invoke(eventSource, new object[] { handler, null });
                healthEventSubscribed = true;
            }
            catch { }
        }

        public static void OnHealthChanged(GlobalUnitHealthChangeArgs args)
        {
            healthChangedThisFrame = true;
        }

        public static bool ShouldSkipUpdate()
        {
            return !activeListDirty && !healthChangedThisFrame;
        }

        public static System.Collections.Generic.List<GuiHealthBarMaker> GetActiveHealthbarMakers(System.Collections.Generic.List<GuiHealthBarMaker> makers)
        {
            activeListDirty = false;
            healthChangedThisFrame = false;

            // Filter out full-HP devices here so UpdatePopulation never sees them,
            // but they stay in the active list so damage numbers have a valid anchor the moment HP drops.
            FilteredHealthbarMakers.Clear();
            for (int i = 0; i < ActiveHealthbarMakers.Count; i++)
            {
                GuiHealthBarMaker maker = ActiveHealthbarMakers[i];
                if (maker == null)
                {
                    continue;
                }

                Unit unit = maker.Unit;
                if (unit == null)
                {
                    continue;
                }

                // Always pass through player-owned units
                if (unit.PlayerId != null)
                {
                    FilteredHealthbarMakers.Add(maker);
                    continue;
                }

                // Skip devices at full HP — healthbar shows nothing useful.
                // Bombs are exempt: their healthbar shows a countdown timer regardless of HP.
                if (unit.IsHealth && unit.BombUnitData == null)
                {
                    float maxHp = unit.MaxHealth;
                    if (maxHp > 0f && unit.Health >= maxHp)
                    {
                        continue;
                    }
                }

                FilteredHealthbarMakers.Add(maker);
            }

            return FilteredHealthbarMakers;
        }

        public static void RegisterHealthbarMaker(GuiHealthBarMaker maker)
        {
            EnsureHealthEventSubscribed();
            if (maker == null)
            {
                return;
            }

            GuiHealthbarMakerController existing = maker.GetComponent<GuiHealthbarMakerController>();
            if (existing == null)
            {
                existing = maker.gameObject.AddComponent<GuiHealthbarMakerController>();
            }

            existing.Initialize(maker);
            existing.RefreshImmediate();
        }

        private static Unit GetLocalPlayer()
        {
            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null)
            {
                return null;
            }

            return registry.GetPlayer();
        }

        private static bool ShouldKeepHealthbar(Unit unit, Unit player, float maxDistanceSq)
        {
            if (unit == null)
            {
                return false;
            }

            if (player == null)
            {
                return true;
            }

            // Always show player unit healthbars
            if (unit == player || unit.PlayerId != null)
            {
                return true;
            }

            CardUnit card = unit.UnitCard;
            if (card == null)
            {
                return true;
            }

            // Always show objective/base/supply healthbars regardless of distance or HP
            if (IsAlwaysRelevant(card))
            {
                return true;
            }

            return (unit.transform.position - player.transform.position).sqrMagnitude <= maxDistanceSq;
        }

        private static bool IsAlwaysRelevant(CardUnit card)
        {
            if (card.Labels != null)
            {
                if (card.Labels.Contains(UnitLabel.Base) ||
                    card.Labels.Contains(UnitLabel.ShieldGenerator) ||
                    card.Labels.Contains(UnitLabel.Objective) ||
                    card.Labels.Contains(UnitLabel.SupplyResource) ||
                    card.Labels.Contains(UnitLabel.SupplyBlockbuster))
                {
                    return true;
                }
            }

            if (card.MinimapType != null)
            {
                UnitMinimapType minimapType = card.MinimapType.Value;
                if (minimapType == UnitMinimapType.Player ||
                    minimapType == UnitMinimapType.Base ||
                    minimapType == UnitMinimapType.ShieldGenerator ||
                    minimapType == UnitMinimapType.SupplyResource ||
                    minimapType == UnitMinimapType.SupplyBlockbuster)
                {
                    return true;
                }
            }

            return card.IsDropPoint;
        }

        public class GuiHealthbarMakerController : MonoBehaviour
        {
            // Frames between checks when culled (distant/inactive) — longer gap = less overhead for the majority of devices
            private const int CulledCheckInterval = 30;
            // Frames between checks when active (nearby) — short gap keeps healthbars responsive
            private const int ActiveCheckInterval = 4;
            // How often to re-resolve the player reference (player unit rarely changes)
            private const int PlayerRefreshInterval = 120;

            private GuiHealthBarMaker maker;
            private Unit unit;
            private Unit cachedPlayer;
            private bool isActive;
            private int frameOffset;
            private int playerRefreshOffset;

            public void Initialize(GuiHealthBarMaker source)
            {
                maker = source;
                unit = source != null ? source.Unit : null;
                // Spread each controller's check across different frames using instance ID
                int id = Mathf.Abs(GetInstanceID());
                frameOffset = id % CulledCheckInterval;
                playerRefreshOffset = id % PlayerRefreshInterval;
            }

            public void RefreshImmediate()
            {
                RefreshPlayer();
                UpdateActiveState();
            }

            private void Update()
            {
                int frame = Time.frameCount;

                // Refresh player reference on a slow cadence — it almost never changes
                if ((frame + playerRefreshOffset) % PlayerRefreshInterval == 0)
                {
                    RefreshPlayer();
                }

                // Adaptive check interval: active devices check more often (stay responsive),
                // culled devices check rarely (most of the map when the player is in one area)
                int interval = isActive ? ActiveCheckInterval : CulledCheckInterval;
                if ((frame + frameOffset) % interval != 0)
                {
                    return;
                }

                UpdateActiveState();
            }

            private void OnDisable()
            {
                SetActive(false);
            }

            private void OnDestroy()
            {
                SetActive(false);
            }

            private void RefreshPlayer()
            {
                cachedPlayer = GetLocalPlayer();
            }

            private void UpdateActiveState()
            {
                if (maker == null)
                {
                    SetActive(false);
                    return;
                }

                if (unit == null)
                {
                    unit = maker.Unit;
                }

                float maxDistance = PerformanceOptGeneratedConfig.DeviceHealthbarCullDistance;
                bool shouldBeActive = ShouldKeepHealthbar(unit, cachedPlayer, maxDistance * maxDistance);
                SetActive(shouldBeActive);
            }

            private void SetActive(bool value)
            {
                if (maker == null)
                {
                    return;
                }

                if (value)
                {
                    if (!isActive && ActiveHealthbarMakersSet.Add(maker))
                    {
                        ActiveHealthbarMakers.Add(maker);
                        activeListDirty = true;
                    }

                    isActive = true;
                    return;
                }

                if (ActiveHealthbarMakersSet.Remove(maker))
                {
                    ActiveHealthbarMakers.Remove(maker);
                    activeListDirty = true;
                }

                isActive = false;
            }
        }
    }
}
