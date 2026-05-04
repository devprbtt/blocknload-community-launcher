using Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace BnlCommunityFixes
{
    public static class CrosshairRuntime
    {
        public static void ApplyVisibility(GuiCrosshairController controller, bool forceShowInAds)
        {
            if (!forceShowInAds || controller == null || controller.Content == null || controller.Content.activeSelf)
            {
                return;
            }

            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null)
            {
                return;
            }

            Unit player = registry.GetPlayer();
            if (player == null || player.IsDeath || player.CurrentGear == null)
            {
                return;
            }

            if (player.IsInAimingState() && !player.IsReloading && !player.IsSwitchingGear)
            {
                controller.Content.SetActive(true);
            }
        }

        public static void ApplyController(GuiCrosshairController controller, Color idleColor, Color fullDamageColor, Color belowMaxColor)
        {
            if (controller == null)
            {
                return;
            }

            controller.NoTarget = idleColor;
            controller.FullDamage = fullDamageColor;
            controller.BelowMaxDamage = belowMaxColor;
        }

        public static void ApplyBlank(GuiCrosshairBlank blank, float sizeMultiplier)
        {
            if (blank == null)
            {
                return;
            }

            blank.transform.localScale = Vector3.one * sizeMultiplier;
        }

        public static float ScaleAngle(float angle, float spreadMultiplier)
        {
            return angle * spreadMultiplier;
        }

        public static GameObject GetAppropriateCrosshairPrefab(GuiCrosshairController controller, ReticleInfo reticleInfo, int forcedTypeValue, bool useForcedType)
        {
            if (controller == null || reticleInfo == null)
            {
                return null;
            }

            ReticleType type = reticleInfo.Type;
            if (useForcedType && type != ReticleType.Melee)
            {
                type = (ReticleType)forcedTypeValue;
            }

            switch (type)
            {
                case ReticleType.Dot:
                    return controller.PrototypeDot.gameObject;
                case ReticleType.Crosshair:
                    return controller.PrototypeCrosshair.gameObject;
                case ReticleType.BrokenCircle:
                    return controller.PrototypeBrokenCircle.gameObject;
                case ReticleType.Hashed:
                    return controller.PrototypeHashed.gameObject;
                case ReticleType.HashedCrosshair:
                    return controller.PrototypeHashedCrosshair.gameObject;
                case ReticleType.Melee:
                    return controller.PrototypeMelee.gameObject;
                default:
                    return controller.PrototypeDot.gameObject;
            }
        }
    }

    public static class AdsSensitivityRuntime
    {
        public static float ApplyAdsScale(float currentScale, Unit unit, float adsSensitivityMultiplier)
        {
            if (unit != null && unit.GetAimingState() != null)
            {
                return currentScale * adsSensitivityMultiplier;
            }

            return currentScale;
        }
    }
}
