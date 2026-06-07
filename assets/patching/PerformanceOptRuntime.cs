using UnityEngine;
using Protocol;

namespace BnlCommunityFixes
{
    public static class PerformanceOptRuntime
    {
        private static readonly System.Collections.Generic.List<GuiHealthBarMaker> ActiveHealthbarMakers = new System.Collections.Generic.List<GuiHealthBarMaker>();
        // Mirror set for O(1) Contains checks instead of O(n) list scans
        private static readonly System.Collections.Generic.HashSet<GuiHealthBarMaker> ActiveHealthbarMakersSet = new System.Collections.Generic.HashSet<GuiHealthBarMaker>();
        private static int registerLogCount;
        private static int activeStateLogCount;
        private static int activeListReadLogCount;

        public static System.Collections.Generic.List<GuiHealthBarMaker> GetActiveHealthbarMakers(System.Collections.Generic.List<GuiHealthBarMaker> makers)
        {
            if (activeListReadLogCount < 20)
            {
                activeListReadLogCount++;
                Debug.Log("[BNL PerfOpt] GetActiveHealthbarMakers source=" + (makers != null ? makers.Count : -1) + " active=" + ActiveHealthbarMakers.Count);
            }

            return ActiveHealthbarMakers;
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

            if (unit == player || unit.PlayerId != null)
            {
                return true;
            }

            CardUnit card = unit.UnitCard;
            if (card == null)
            {
                return true;
            }

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
