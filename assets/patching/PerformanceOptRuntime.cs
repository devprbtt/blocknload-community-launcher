using UnityEngine;
using Protocol;

namespace BnlCommunityFixes
{
    public static class PerformanceOptRuntime
    {
        private static readonly System.Collections.Generic.List<GuiHealthBarMaker> ActiveHealthbarMakers = new System.Collections.Generic.List<GuiHealthBarMaker>();
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
            private GuiHealthBarMaker maker;
            private Unit unit;
            private bool isActive;
            private int frameOffset;

            public void Initialize(GuiHealthBarMaker source)
            {
                maker = source;
                unit = source != null ? source.Unit : null;
                frameOffset = Mathf.Abs(GetInstanceID()) % 12;
            }

            public void RefreshImmediate()
            {
                UpdateActiveState();
            }

            private void Update()
            {
                if ((Time.frameCount + frameOffset) % 12 != 0)
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

                Unit player = GetLocalPlayer();
                float maxDistance = PerformanceOptGeneratedConfig.DeviceHealthbarCullDistance;
                bool shouldBeActive = ShouldKeepHealthbar(unit, player, maxDistance * maxDistance);
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
                    if (!isActive && !ActiveHealthbarMakers.Contains(maker))
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

                if (ActiveHealthbarMakers.Contains(maker))
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
