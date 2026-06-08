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
        private static int registerLogCount;
        private static int activeStateLogCount;
        private static int activeListReadLogCount;
        private static bool activeListDirty = true;
        private static int lastFilteredCount = -1;

        private const int MinimapUpdateInterval = 6;
        private const int GravityTrapUpdateInterval = 6;
        private const int FanAnimatorUpdateInterval = 4;
        private const int WsiTeamOverlayUpdateInterval = 6;

        public static bool ShouldThrottleMinimapUpdate()
        {
            return (UnityEngine.Time.frameCount % MinimapUpdateInterval) != 0;
        }

        public static bool ShouldThrottleGravityTrapEffects()
        {
            return (UnityEngine.Time.frameCount % GravityTrapUpdateInterval) != 0;
        }

        public static bool ShouldThrottleFanAnimator()
        {
            return (UnityEngine.Time.frameCount % FanAnimatorUpdateInterval) != 0;
        }

        public static bool ShouldThrottleWsiTeamOverlay()
        {
            return (UnityEngine.Time.frameCount % WsiTeamOverlayUpdateInterval) != 0;
        }

        // Called at the top of GuiHealthbarPopulation.Update — skip the entire update if nothing changed.
        public static bool ShouldSkipUpdate()
        {
            if (activeListDirty)
            {
                return false;
            }

            // Re-evaluate health states to detect damage/heal transitions.
            // This is a lightweight scan — no allocations, just counting.
            int filteredCount = 0;
            for (int i = 0; i < ActiveHealthbarMakers.Count; i++)
            {
                GuiHealthBarMaker maker = ActiveHealthbarMakers[i];
                if (maker == null) continue;
                Unit unit = maker.Unit;
                if (unit == null) continue;
                if (unit.PlayerId != null) { filteredCount++; continue; }
                if (unit.IsHealth && unit.BombUnitData == null)
                {
                    float maxHp = unit.MaxHealth;
                    if (maxHp > 0f && unit.Health >= maxHp) continue;
                }
                filteredCount++;
            }

            if (filteredCount != lastFilteredCount)
            {
                lastFilteredCount = filteredCount;
                return false;
            }

            return true;
        }

        public static System.Collections.Generic.List<GuiHealthBarMaker> GetActiveHealthbarMakers(System.Collections.Generic.List<GuiHealthBarMaker> makers)
        {
            if (activeListReadLogCount < 20)
            {
                activeListReadLogCount++;
                Debug.Log("[BNL PerfOpt] GetActiveHealthbarMakers source=" + (makers != null ? makers.Count : -1) + " active=" + ActiveHealthbarMakers.Count);
            }

            activeListDirty = false;

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

            lastFilteredCount = FilteredHealthbarMakers.Count;
            return FilteredHealthbarMakers;
        }

        public static void RegisterHealthbarMaker(GuiHealthBarMaker maker)
        {
            if (maker == null)
            {
                return;
            }

            GuiHealthbarMakerController existing = maker.GetComponent<GuiHealthbarMakerController>();
            if (existing == null)
            {
                existing = maker.gameObject.AddComponent<GuiHealthbarMakerController>();
            }

            if (registerLogCount < 40)
            {
                registerLogCount++;
                Unit unit = maker.Unit;
                string unitId = unit != null && unit.UnitCard != null ? unit.UnitCard.Id : "<null>";
                string playerId = unit != null && unit.PlayerId != null ? unit.PlayerId.Value.ToString() : "device";
                Debug.Log("[BNL PerfOpt] RegisterHealthbarMaker unitId=" + unitId + " owner=" + playerId);
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
                        if (activeStateLogCount < 60)
                        {
                            activeStateLogCount++;
                            string unitId = unit != null && unit.UnitCard != null ? unit.UnitCard.Id : "<null>";
                            Debug.Log("[BNL PerfOpt] Active+ unitId=" + unitId + " activeCount=" + ActiveHealthbarMakers.Count);
                        }
                    }

                    isActive = true;
                    return;
                }

                if (ActiveHealthbarMakersSet.Remove(maker))
                {
                    ActiveHealthbarMakers.Remove(maker);
                    activeListDirty = true;
                    if (activeStateLogCount < 60)
                    {
                        activeStateLogCount++;
                        string unitId = unit != null && unit.UnitCard != null ? unit.UnitCard.Id : "<null>";
                        Debug.Log("[BNL PerfOpt] Active- unitId=" + unitId + " activeCount=" + ActiveHealthbarMakers.Count);
                    }
                }

                isActive = false;
            }
        }
    }
}
