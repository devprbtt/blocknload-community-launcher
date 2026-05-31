using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace BnlCommunityFixes
{
    public sealed class ShieldBuffBarController : MonoBehaviour
    {
        private static readonly bool FeatureEnabled = $(Format-BoolLiteral $EnemyShieldBuffBarEnabled);
        private const string TimedShieldEffectId = "effect_status_shield_block";
        private const float ShieldClockSizeMultiplier = $(Format-FloatLiteral $EnemyShieldClockSizeMultiplier);
        private const float ShieldClockOffsetX = $(Format-FloatLiteral $EnemyShieldClockOffsetX);
        private const float ShieldClockOffsetY = $(Format-FloatLiteral $EnemyShieldClockOffsetY);
        private const string ShieldTimerDisplayMode = "$($EnemyShieldTimerDisplayMode.Replace('\', '\\').Replace('"', '\"'))";
        private static readonly Color ShieldBarColor = new Color($(Format-FloatLiteral $EnemyShieldBuffBarColor.R), $(Format-FloatLiteral $EnemyShieldBuffBarColor.G), $(Format-FloatLiteral $EnemyShieldBuffBarColor.B), $(Format-FloatLiteral $EnemyShieldBuffBarColor.A));
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Sprite cachedClockSprite;

        private GuiHealthbar healthbar;
        private Image bar;
        private RectTransform barRect;
        private Image clock;
        private RectTransform clockRect;
        private Text timerText;
        private RectTransform timerTextRect;
        private float observedShieldMax;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
            EnsureVisuals();
        }

        private void LateUpdate()
        {
            if (!FeatureEnabled || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            EnsureVisuals();
            if (bar == null || clock == null || timerText == null)
            {
                return;
            }

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (unit == null || zoneData == null)
            {
                bar.enabled = false;
                clock.enabled = false;
                timerText.enabled = false;
                return;
            }

            bool isEnemy = unit.Team != TeamType.Neutral && unit.Team != zoneData.MyTeam;
            ConstEffectInfo strongestShieldEffect;
            float strongestShieldValue;
            bool hasStrongestShieldEffect = TryGetStrongestShieldEffect(unit, out strongestShieldEffect, out strongestShieldValue);
            float shieldValue = hasStrongestShieldEffect ? Mathf.Max(strongestShieldValue, 0f) : Mathf.Max(unit.Shield, 0f);
            float configuredShieldMax = 0f;
            if (unit.UnitCard != null && unit.UnitCard.Health != null && unit.UnitCard.Health.Health != null)
            {
                configuredShieldMax = Mathf.Max(unit.UnitCard.Health.Health.Shield, 0f);
            }

            if (shieldValue > observedShieldMax)
            {
                observedShieldMax = shieldValue;
            }

            float shieldMax = Mathf.Max(configuredShieldMax, observedShieldMax);
            float shieldFill = ResolveShieldFill(shieldValue, shieldMax);
            bool visible = isEnemy && shieldFill > Mathf.Epsilon && healthbar.Content != null && healthbar.Content.alpha > 0.01f;
            bar.enabled = visible;
            clock.enabled = false;
            timerText.enabled = false;
            if (!visible)
            {
                return;
            }

            PositionVisuals();
            bar.color = ShieldBarColor;
            bar.fillAmount = shieldFill;

            float timerFill;
            bool hasTimer = TryGetShieldTimerFraction(unit, strongestShieldEffect, out timerFill);
            if (hasTimer)
            {
                if (UseNumericTimer())
                {
                    timerText.color = ShieldBarColor;
                    timerText.text = GetRemainingTimeText(unit, strongestShieldEffect);
                    timerText.enabled = !string.IsNullOrEmpty(timerText.text);
                }
                else
                {
                    clock.color = ShieldBarColor;
                    clock.fillAmount = timerFill;
                    clock.enabled = true;
                }
            }
        }

        private void EnsureVisuals()
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            Image source = healthbar.HealthBar;

            if (bar == null)
            {
                GameObject barGo = new GameObject("ExperimentalShieldBuffBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barGo.transform.SetParent(healthbar.HealthBar.transform, false);
                bar = barGo.GetComponent<Image>();
                barRect = barGo.GetComponent<RectTransform>();
                bar.enabled = false;
                bar.sprite = source.sprite;
                bar.type = source.type;
                bar.fillMethod = source.fillMethod;
                bar.fillOrigin = source.fillOrigin;
                bar.fillClockwise = source.fillClockwise;
                bar.preserveAspect = source.preserveAspect;
                bar.material = source.material;
            }

            if (clock == null)
            {
                GameObject clockGo = new GameObject("ExperimentalShieldBuffClock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                clockGo.transform.SetParent(healthbar.HealthBar.transform, false);
                clock = clockGo.GetComponent<Image>();
                clockRect = clockGo.GetComponent<RectTransform>();
                clock.enabled = false;
                clock.sprite = ResolveClockSprite();
                if (clock.sprite == null)
                {
                    clock.sprite = source.sprite;
                }
                clock.type = Image.Type.Filled;
                clock.fillMethod = Image.FillMethod.Radial360;
                clock.fillOrigin = 2;
                clock.fillClockwise = false;
                clock.preserveAspect = true;
                clock.material = source.material;
            }

            if (timerText == null)
            {
                GameObject textGo = new GameObject("ExperimentalShieldBuffTimerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGo.transform.SetParent(healthbar.HealthBar.transform, false);
                timerText = textGo.GetComponent<Text>();
                timerTextRect = textGo.GetComponent<RectTransform>();
                timerText.enabled = false;
                timerText.alignment = TextAnchor.MiddleRight;
                timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                timerText.verticalOverflow = VerticalWrapMode.Overflow;
                timerText.font = healthbar.Title != null ? healthbar.Title.font : null;
                timerText.fontStyle = healthbar.Title != null ? healthbar.Title.fontStyle : FontStyle.Normal;
                timerText.material = healthbar.Title != null ? healthbar.Title.material : null;
            }
        }

        private static Sprite ResolveClockSprite()
        {
            if (cachedClockSprite != null)
            {
                return cachedClockSprite;
            }

            UnityEngine.Object[] guiBuffs = Resources.FindObjectsOfTypeAll(typeof(GuiBuff));
            for (int i = 0; i < guiBuffs.Length; i++)
            {
                GuiBuff guiBuff = guiBuffs[i] as GuiBuff;
                if (guiBuff != null && guiBuff.RoundCooldown != null)
                {
                    cachedClockSprite = guiBuff.RoundCooldown;
                    return cachedClockSprite;
                }
            }

            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            List<string> preferredNames = new List<string>(new string[] { "roundcooldown", "cooldown_round", "pie", "radial", "circle" });
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                {
                    continue;
                }

                string lower = sprite.name.ToLowerInvariant();
                for (int j = 0; j < preferredNames.Count; j++)
                {
                    if (lower.Contains(preferredNames[j]))
                    {
                        cachedClockSprite = sprite;
                        return cachedClockSprite;
                    }
                }
            }

            return cachedClockSprite;
        }

        private void PositionVisuals()
        {
            if (barRect == null || clockRect == null || timerTextRect == null || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            RectTransform src = healthbar.HealthBar.rectTransform;
            float sourceHeight = Mathf.Abs(src.rect.height);
            if (sourceHeight <= Mathf.Epsilon)
            {
                sourceHeight = Mathf.Abs(src.sizeDelta.y);
            }
            if (sourceHeight <= Mathf.Epsilon)
            {
                sourceHeight = 8f;
            }

            float shieldHeight = Mathf.Max(2f, sourceHeight * 0.35f);
            float yOffset = 2f;

            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, yOffset);
            barRect.sizeDelta = new Vector2(0f, shieldHeight);
            barRect.localScale = src.localScale;
            barRect.localRotation = src.localRotation;

            float clockSize = Mathf.Max(shieldHeight + 4f, sourceHeight * 0.8f) * Mathf.Max(0.25f, ShieldClockSizeMultiplier);
            float baseHealthBarCenterY = -sourceHeight * 0.5f;
            clockRect.anchorMin = new Vector2(0f, 1f);
            clockRect.anchorMax = new Vector2(0f, 1f);
            clockRect.pivot = new Vector2(1f, 0.5f);
            clockRect.anchoredPosition = new Vector2(-4f + ShieldClockOffsetX, baseHealthBarCenterY + ShieldClockOffsetY);
            clockRect.sizeDelta = new Vector2(clockSize, clockSize);
            clockRect.localScale = src.localScale;
            clockRect.localRotation = src.localRotation;

            timerText.fontSize = Mathf.Max(10, healthbar.Title != null ? Mathf.RoundToInt(healthbar.Title.fontSize * Mathf.Max(0.5f, ShieldClockSizeMultiplier)) : Mathf.RoundToInt(12f * Mathf.Max(0.5f, ShieldClockSizeMultiplier)));
            timerTextRect.anchorMin = new Vector2(0f, 1f);
            timerTextRect.anchorMax = new Vector2(0f, 1f);
            timerTextRect.pivot = new Vector2(1f, 0.5f);
            timerTextRect.anchoredPosition = new Vector2(-4f + ShieldClockOffsetX, baseHealthBarCenterY + ShieldClockOffsetY);
            timerTextRect.sizeDelta = new Vector2(48f * Mathf.Max(0.75f, ShieldClockSizeMultiplier), Mathf.Max(shieldHeight + 6f, sourceHeight));
            timerTextRect.localScale = src.localScale;
            timerTextRect.localRotation = src.localRotation;
        }

        private float ResolveShieldFill(float shieldValue, float shieldMax)
        {
            if (shieldValue <= 1.0001f)
            {
                return Mathf.Clamp01(shieldValue);
            }

            if (shieldMax > Mathf.Epsilon)
            {
                return Mathf.Clamp01(shieldValue / shieldMax);
            }

            return 0f;
        }

        private static bool TryGetShieldTimerFraction(Unit unit, ConstEffectInfo strongestShieldEffect, out float fillAmount)
        {
            fillAmount = 1f;
            ConstEffectInfo effectInfo = strongestShieldEffect;
            if ((effectInfo == null || !effectInfo.HasDuration) && !TryGetTimedShieldEffect(unit, out effectInfo))
            {
                return false;
            }

            if (effectInfo == null || !effectInfo.HasDuration || effectInfo.Card == null || effectInfo.Card.Duration == null || effectInfo.Card.Duration.Value <= Mathf.Epsilon || effectInfo.TimestampEnd == null || Singleton<IServerTime>.Instance == null)
            {
                return false;
            }

            float remaining = Mathf.Max(0f, Singleton<IServerTime>.Instance.TimeTill((long)effectInfo.TimestampEnd.Value));
            fillAmount = Mathf.Clamp01(remaining / effectInfo.Card.Duration.Value);
            return true;
        }

        private static string GetRemainingTimeText(Unit unit, ConstEffectInfo strongestShieldEffect)
        {
            ConstEffectInfo effectInfo = strongestShieldEffect;
            if ((effectInfo == null || !effectInfo.HasDuration) && !TryGetTimedShieldEffect(unit, out effectInfo))
            {
                return string.Empty;
            }

            return FormatRemainingTime(effectInfo);
        }

        private static string FormatRemainingTime(ConstEffectInfo effectInfo)
        {
            if (effectInfo == null || effectInfo.TimestampEnd == null || Singleton<IServerTime>.Instance == null)
            {
                return string.Empty;
            }

            float remaining = Mathf.Max(0f, Singleton<IServerTime>.Instance.TimeTill((long)effectInfo.TimestampEnd.Value));
            if (remaining <= Mathf.Epsilon)
            {
                return string.Empty;
            }

            return remaining.ToString("0.0") + "s";
        }

        private static bool UseNumericTimer()
        {
            return string.Equals(ShieldTimerDisplayMode, "text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ShieldTimerDisplayMode, "number", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ShieldTimerDisplayMode, "numeric", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetTimedShieldEffect(Unit unit, out ConstEffectInfo match)
        {
            match = null;
            float strongestValue;
            if (TryGetStrongestShieldEffect(unit, out match, out strongestValue) && match != null && match.HasDuration)
            {
                return true;
            }

            match = null;
            if (unit == null || unit.ActualConstEffects == null)
            {
                return false;
            }

            for (int i = 0; i < unit.ActualConstEffects.Count; i++)
            {
                ConstEffectInfo effectInfo = unit.ActualConstEffects[i];
                if (effectInfo == null || effectInfo.Card == null)
                {
                    continue;
                }

                CardEffect card = effectInfo.Card;
                ConstEffectBuff buffEffect = card.Effect as ConstEffectBuff;
                bool grantsShield = buffEffect != null && buffEffect.Buffs != null && buffEffect.Buffs.ContainsKey(BuffType.Shield);
                if (!grantsShield || !effectInfo.HasDuration)
                {
                    continue;
                }

                if (string.Equals(card.Id, TimedShieldEffectId, StringComparison.Ordinal))
                {
                    match = effectInfo;
                    return true;
                }

                if (match == null)
                {
                    match = effectInfo;
                }
            }

            return match != null;
        }

        private static bool TryGetStrongestShieldEffect(Unit unit, out ConstEffectInfo match, out float shieldValue)
        {
            match = null;
            shieldValue = 0f;
            if (unit == null || unit.ActualConstEffects == null)
            {
                return false;
            }

            for (int i = 0; i < unit.ActualConstEffects.Count; i++)
            {
                ConstEffectInfo effectInfo = unit.ActualConstEffects[i];
                if (effectInfo == null || effectInfo.Card == null)
                {
                    continue;
                }

                CardEffect card = effectInfo.Card;
                ConstEffectBuff buffEffect = card.Effect as ConstEffectBuff;
                if (buffEffect == null || buffEffect.Buffs == null || !buffEffect.Buffs.ContainsKey(BuffType.Shield))
                {
                    continue;
                }

                float candidateValue = Mathf.Max(buffEffect.Buffs[BuffType.Shield], 0f);
                bool takeCandidate = false;
                if (match == null || candidateValue > shieldValue + Mathf.Epsilon)
                {
                    takeCandidate = true;
                }
                else if (Mathf.Abs(candidateValue - shieldValue) <= Mathf.Epsilon)
                {
                    bool candidatePreferred = string.Equals(card.Id, TimedShieldEffectId, StringComparison.Ordinal);
                    bool currentPreferred = match.Card != null && string.Equals(match.Card.Id, TimedShieldEffectId, StringComparison.Ordinal);
                    if (candidatePreferred && !currentPreferred)
                    {
                        takeCandidate = true;
                    }
                    else if (effectInfo.HasDuration && !match.HasDuration)
                    {
                        takeCandidate = true;
                    }
                }

                if (takeCandidate)
                {
                    match = effectInfo;
                    shieldValue = candidateValue;
                }
            }

            return match != null;
        }
    }

    public static class ShieldBuffBarRuntime
    {
        public static void AttachShieldBuffBar(GuiHealthbar healthbar)
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            ShieldBuffBarController controller = healthbar.gameObject.GetComponent<ShieldBuffBarController>();
            if (controller == null)
            {
                controller = healthbar.gameObject.AddComponent<ShieldBuffBarController>();
            }
            controller.Init(healthbar);
        }
    }

    public sealed class FriendlyLowHealthController : MonoBehaviour
    {
        internal static readonly bool FeatureEnabled = $(Format-BoolLiteral $FriendlyLowHealthEnabled);
        internal static readonly float Threshold = $(Format-FloatLiteral $FriendlyLowHealthThreshold);
        internal static readonly bool IndicatorEnabled = $(Format-BoolLiteral ($FriendlyLowHealthEnabled -and $FriendlyLowHealthIndicatorEnabled));
        private static readonly Color AlertColor = new Color($(Format-FloatLiteral $FriendlyLowHealthColor.R), $(Format-FloatLiteral $FriendlyLowHealthColor.G), $(Format-FloatLiteral $FriendlyLowHealthColor.B), 1f);
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FollowFieldCtrl = typeof(GuiHealthbar).GetField("follow", BindingFlags.Instance | BindingFlags.NonPublic);

        private GuiHealthbar healthbar;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
        }

        private void OnDestroy()
        {
            if (IndicatorEnabled)
            {
                Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
                if (unit != null)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
            }
        }

        private void LateUpdate()
        {
            if (!FeatureEnabled || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.Health <= 0f || unit.IsDeath)
            {
                if (IndicatorEnabled && unit != null)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
                return;
            }

            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (zoneData == null)
            {
                return;
            }

            if (unit.Team == TeamType.Neutral || unit.Team != zoneData.MyTeam)
            {
                if (IndicatorEnabled)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
                return;
            }

            float maxHp = unit.MaxHealth;
            bool isLow = maxHp > 0f && (unit.Health / maxHp) <= Threshold;

            if (isLow)
            {
                // LateUpdate runs after Update() which sets the team color each frame.
                // We overwrite it here every frame so the alert color always wins while low.
                healthbar.HealthBar.color = AlertColor;
                if (healthbar.Title != null)
                {
                    healthbar.Title.color = AlertColor;
                }

                if (IndicatorEnabled)
                {
                    GuiFollow follow = ReferenceEquals(FollowFieldCtrl, null) ? null : FollowFieldCtrl.GetValue(healthbar) as GuiFollow;
                    bool isOffScreen = follow == null || !follow.IsInFrontOfCamera;
                    FriendlyLowHealthIndicatorService.UpdateIndicator(unit, isOffScreen, AlertColor);
                }
            }
            else
            {
                if (IndicatorEnabled)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
            }
        }
    }

    public static class FriendlyLowHealthIndicatorService
    {
        private const float IndicatorSize  = $(Format-FloatLiteral $FriendlyLowHealthIndicatorSize);
        private const float IndicatorAlpha = $(Format-FloatLiteral $FriendlyLowHealthIndicatorAlpha);

        private static readonly Dictionary<Unit, GuiWorldSpaceIndicator> indicators = new Dictionary<Unit, GuiWorldSpaceIndicator>();

        public static void UpdateIndicator(Unit unit, bool isOffScreen, Color color)
        {
            if (!isOffScreen)
            {
                RemoveIndicator(unit);
                return;
            }

            GuiWorldSpaceIndicatorFactory factory = Singleton<GuiWorldSpaceIndicatorFactory>.Instance;
            if (factory == null) return;

            GuiWorldSpaceIndicator existing;
            if (indicators.TryGetValue(unit, out existing))
            {
                if (existing == null)
                    indicators.Remove(unit);
                return;
            }

            GuiWorldSpaceIndicator indicator = factory.AddArrow(unit);
            if (indicator == null) return;

            indicator.SetColor(color);
            indicator.IconMinSize = IndicatorSize;
            indicator.IconMaxSize = IndicatorSize;

            // Override the fade-in tween's target alpha so it settles at our configured value.
            UiTweenAlpha tween = indicator.GetComponent<UiTweenAlpha>();
            if (tween != null)
                tween.to = IndicatorAlpha;

            indicators[unit] = indicator;
        }

        public static void RemoveIndicator(Unit unit)
        {
            GuiWorldSpaceIndicator existing;
            if (indicators.TryGetValue(unit, out existing))
            {
                indicators.Remove(unit);
                if (existing != null)
                    existing.Kill();
            }
        }
    }

    public static class FriendlyLowHealthRuntime
    {
        private static readonly FieldInfo FollowField = typeof(GuiHealthbar).GetField("follow", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UnitFieldStatic = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void AttachFriendlyLowHealth(GuiHealthbar healthbar)
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            FriendlyLowHealthController controller = healthbar.gameObject.GetComponent<FriendlyLowHealthController>();
            if (controller == null)
            {
                controller = healthbar.gameObject.AddComponent<FriendlyLowHealthController>();
            }
            controller.Init(healthbar);
        }

        public static bool IsFriendlyLowHealth(GuiHealthbar healthbar)
        {
            if (!FriendlyLowHealthController.FeatureEnabled || healthbar == null)
            {
                return false;
            }

            Unit unit = ReferenceEquals(UnitFieldStatic, null) ? null : UnitFieldStatic.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.IsDeath)
            {
                return false;
            }

            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (zoneData == null)
            {
                return false;
            }

            if (unit.Team == TeamType.Neutral || unit.Team != zoneData.MyTeam)
            {
                return false;
            }

            GuiFollow follow = ReferenceEquals(FollowField, null) ? null : FollowField.GetValue(healthbar) as GuiFollow;
            if (follow == null || !follow.IsInFrontOfCamera)
            {
                return false;
            }

            // During deathcam (CameraDeath singleton is active) show bar for all in-camera friendlies.
            CameraDeath deathCamInst = Singleton<CameraDeath>.Instance;
            if (deathCamInst != null && deathCamInst.Target != null)
            {
                return true;
            }

            // Alive: only show when below threshold.
            float maxHp = unit.MaxHealth;
            if (maxHp <= 0f)
            {
                return false;
            }

            return (unit.Health / maxHp) <= FriendlyLowHealthController.Threshold;
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class FontRuntime
    {
        private const string SelectedFontName = "$SelectedFontLiteral";
        private const string SelectedStyle = "$SelectedStyleLiteral";
        private const float SizeMultiplier = $SizeMultiplierLiteral;
        private const float LineSpacingMultiplier = $LineSpacingMultiplierLiteral;
        private static bool initialized;
        private static Font cachedFont;
        private static string cachedFontName;
        private static readonly HashSet<string> seenFontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, int> originalSizes = new Dictionary<int, int>();
        private static readonly Dictionary<int, float> originalSpacings = new Dictionary<int, float>();
        private static readonly Dictionary<int, FontStyle> originalStyles = new Dictionary<int, FontStyle>();
        private static string cachePath;

        public static void ApplyAllCanvases()
        {
            EnsureCacheInitialized();
            try
            {
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                HashSet<int> roots = new HashSet<int>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas == null)
                    {
                        continue;
                    }

                    GameObject root = canvas.transform.root.gameObject;
                    if (root != null && roots.Add(root.GetInstanceID()))
                    {
                        ApplyToHierarchy(root);
                    }
                }
            }
            catch
            {
            }
        }

        public static void ApplyToHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                ApplyToText(texts[i]);
            }
        }

        public static void ApplyToText(Text text)
        {
            if (text == null)
            {
                return;
            }
            EnsureCacheInitialized();
            RecordFont(text.font);
            CacheOriginals(text);

            if (!string.IsNullOrEmpty(SelectedFontName) && SelectedFontName != "__DEFAULT__")
            {
                Font target = ResolveFont();
                if (target != null && text.font != target)
                {
                    text.font = target;
                }
            }

            ApplyStyle(text);
        }

        private static void CacheOriginals(Text text)
        {
            int id = text.GetInstanceID();
            if (!originalSizes.ContainsKey(id))
            {
                originalSizes[id] = text.fontSize;
            }
            if (!originalSpacings.ContainsKey(id))
            {
                originalSpacings[id] = text.lineSpacing;
            }
            if (!originalStyles.ContainsKey(id))
            {
                originalStyles[id] = text.fontStyle;
            }
        }

        private static void ApplyStyle(Text text)
        {
            int id = text.GetInstanceID();
            int baseSize;
            float baseSpacing;
            FontStyle baseStyle;

            if (!originalSizes.TryGetValue(id, out baseSize))
            {
                baseSize = text.fontSize;
            }
            if (!originalSpacings.TryGetValue(id, out baseSpacing))
            {
                baseSpacing = text.lineSpacing;
            }
            if (!originalStyles.TryGetValue(id, out baseStyle))
            {
                baseStyle = text.fontStyle;
            }

            int targetSize = Math.Max(1, Mathf.RoundToInt((float)baseSize * SizeMultiplier));
            float targetSpacing = baseSpacing * LineSpacingMultiplier;

            if (text.fontSize != targetSize)
            {
                text.fontSize = targetSize;
            }
            if (Math.Abs(text.lineSpacing - targetSpacing) > 0.001f)
            {
                text.lineSpacing = targetSpacing;
            }

            FontStyle targetStyle = baseStyle;
            if (string.Equals(SelectedStyle, "Normal", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Normal;
            }
            else if (string.Equals(SelectedStyle, "Bold", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Bold;
            }
            else if (string.Equals(SelectedStyle, "Italic", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Italic;
            }
            else if (string.Equals(SelectedStyle, "BoldAndItalic", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.BoldAndItalic;
            }

            if (text.fontStyle != targetStyle)
            {
                text.fontStyle = targetStyle;
            }
        }

        private static void EnsureCacheInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string launcherDir = Path.Combine(root, "launcher");
                Directory.CreateDirectory(launcherDir);
                cachePath = Path.Combine(launcherDir, "available-fonts.txt");
                if (File.Exists(cachePath))
                {
                    string[] existing = File.ReadAllLines(cachePath);
                    for (int i = 0; i < existing.Length; i++)
                    {
                        string line = existing[i];
                        if (!string.IsNullOrEmpty(line))
                        {
                            seenFontNames.Add(line);
                        }
                    }
                }
            }
            catch
            {
            }

            if (!seenFontNames.Contains("__DEFAULT__"))
            {
                seenFontNames.Add("__DEFAULT__");
            }
            FlushFontCache();
        }

        private static void RecordFont(Font font)
        {
            if (font == null || string.IsNullOrEmpty(font.name))
            {
                return;
            }

            if (seenFontNames.Add(font.name))
            {
                FlushFontCache();
            }
        }

        private static void FlushFontCache()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                List<string> ordered = new List<string>(seenFontNames);
                ordered.Sort(StringComparer.OrdinalIgnoreCase);
                if (ordered.Remove("__DEFAULT__"))
                {
                    builder.AppendLine("__DEFAULT__");
                }
                for (int i = 0; i < ordered.Count; i++)
                {
                    builder.AppendLine(ordered[i]);
                }
                if (!string.IsNullOrEmpty(cachePath))
                {
                    File.WriteAllText(cachePath, builder.ToString());
                }
            }
            catch
            {
            }
        }

        private static Font ResolveFont()
        {
            if (cachedFont != null && cachedFontName == SelectedFontName)
            {
                return cachedFont;
            }

            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                Font font = fonts[i];
                if (font != null && string.Equals(font.name, SelectedFontName, StringComparison.OrdinalIgnoreCase))
                {
                    cachedFont = font;
                    cachedFontName = SelectedFontName;
                    return cachedFont;
                }
            }

            if (SelectedFontName.StartsWith("OS: ", StringComparison.OrdinalIgnoreCase))
            {
                string osFontName = SelectedFontName.Substring(4).Trim();
                if (!string.IsNullOrEmpty(osFontName))
                {
                    try
                    {
                        cachedFont = Font.CreateDynamicFontFromOSFont(osFontName, 16);
                        cachedFontName = SelectedFontName;
                        return cachedFont;
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class CrosshairRuntime
    {
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartScales = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Vector2> OriginalCrosshairPartSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartPositions = new Dictionary<int, Vector3>();

        static CrosshairRuntime()
        {
            RuntimeFeatureState.ConfigureCrosshair(
                $(Format-BoolLiteral $CrosshairConfig.enabled),
                new Color($CrosshairIdleR, $CrosshairIdleG, $CrosshairIdleB, 1f),
                new Color($CrosshairFullR, $CrosshairFullG, $CrosshairFullB, 1f),
                new Color($CrosshairBelowR, $CrosshairBelowG, $CrosshairBelowB, 1f),
                $CrosshairSizeMultiplierLiteral,
                $CrosshairSpreadMultiplierLiteral,
                $CrosshairForceShowInAdsLiteral,
                $(Format-BoolLiteral $CrosshairHideEntirely),
                "$CrosshairForceShapeLiteral");
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ApplyHardHide(GuiCrosshairController controller)
        {
            if (!RuntimeFeatureState.CrosshairHideEntirely || controller == null)
            {
                return false;
            }

            if (controller.Content != null)
            {
                controller.Content.SetActive(false);
            }

            if (controller.NotUse != null)
            {
                controller.NotUse.SetActive(false);
            }

            return true;
        }

        public static void ApplyVisibility(GuiCrosshairController controller)
        {
            if (!RuntimeFeatureState.CrosshairForceShowInAds || controller == null || controller.Content == null || controller.Content.activeSelf)
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

        public static void ApplyController(GuiCrosshairController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.NoTarget = RuntimeFeatureState.GetCrosshairIdleColor();
            controller.FullDamage = RuntimeFeatureState.GetCrosshairFullDamageColor();
            controller.BelowMaxDamage = RuntimeFeatureState.GetCrosshairBelowMaxColor();
        }

        public static void ApplyBlank(GuiCrosshairBlank blank)
        {
            if (blank == null)
            {
                return;
            }

            RectTransform root = blank.transform as RectTransform;
            RectTransform[] rects = blank.GetComponentsInChildren<RectTransform>(true);
            if (rects != null && rects.Length > 0)
            {
                blank.transform.localScale = Vector3.one;
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rect = rects[i];
                    if (rect == null || rect == root)
                    {
                        continue;
                    }

                    int id = rect.GetInstanceID();
                    Vector3 baseScale;
                    if (!OriginalCrosshairPartScales.TryGetValue(id, out baseScale))
                    {
                        baseScale = rect.localScale;
                        OriginalCrosshairPartScales[id] = baseScale;
                    }
                    rect.localScale = baseScale * RuntimeFeatureState.CrosshairSizeMultiplier;

                    Vector2 baseSize;
                    if (!OriginalCrosshairPartSizes.TryGetValue(id, out baseSize))
                    {
                        baseSize = rect.sizeDelta;
                        OriginalCrosshairPartSizes[id] = baseSize;
                    }
                    rect.sizeDelta = baseSize * RuntimeFeatureState.CrosshairSizeMultiplier;

                    Vector3 basePosition;
                    if (!OriginalCrosshairPartPositions.TryGetValue(id, out basePosition) || IsRuntimeCrosshairPart(blank, rect))
                    {
                        basePosition = rect.localPosition;
                        OriginalCrosshairPartPositions[id] = basePosition;
                    }
                    rect.localPosition = basePosition * RuntimeFeatureState.CrosshairSizeMultiplier;
                }
                return;
            }

            blank.transform.localScale = Vector3.one * RuntimeFeatureState.CrosshairSizeMultiplier;
        }

        public static float ScaleAngle(float angle)
        {
            return angle * RuntimeFeatureState.CrosshairSpreadMultiplier;
        }

        public static Vector3 ScaleSizeVector(Vector3 value)
        {
            return value * RuntimeFeatureState.CrosshairSizeMultiplier;
        }

        private static bool IsRuntimeCrosshairPart(GuiCrosshairBlank blank, RectTransform rect)
        {
            GuiCrosshair crosshair = blank as GuiCrosshair;
            if (crosshair == null || crosshair.Movable == null)
            {
                return false;
            }

            for (int i = 0; i < crosshair.Movable.Count; i++)
            {
                if (crosshair.Movable[i] == rect)
                {
                    return true;
                }
            }
            return false;
        }

        public static GameObject GetAppropriateCrosshairPrefab(GuiCrosshairController controller, ReticleInfo reticleInfo)
        {
            if (controller == null || reticleInfo == null)
            {
                return null;
            }

            ReticleType type = reticleInfo.Type;
            ReticleType? forcedType = GetForcedType();
            if (forcedType != null && type != ReticleType.Melee)
            {
                type = forcedType.Value;
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

        private static ReticleType? GetForcedType()
        {
            switch (RuntimeFeatureState.CrosshairForceShape)
            {
                case "Dot":
                    return ReticleType.Dot;
                case "Crosshair":
                    return ReticleType.Crosshair;
                case "BrokenCircle":
                    return ReticleType.BrokenCircle;
                case "Hashed":
                    return ReticleType.Hashed;
                case "HashedCrosshair":
                    return ReticleType.HashedCrosshair;
                case "Melee":
                    return ReticleType.Melee;
                default:
                    return null;
            }
        }
    }
}
"@

$HelperSource += @"
namespace BnlCommunityFixes
{
    public sealed class HealingNumberHoldController : MonoBehaviour
    {
        public float HoldUntil;
        private const float FadeDuration = 0.5f;
        private float fadeStart = -1f;

        public void Extend(float holdUntil)
        {
            HoldUntil = Mathf.Max(HoldUntil, holdUntil);
            gameObject.SetActive(true);
            fadeStart = -1f;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (Time.time <= HoldUntil)
            {
                fadeStart = -1f;
                SetAlpha(1f);
            }
            else
            {
                if (fadeStart < 0f)
                {
                    fadeStart = Time.time;
                }

                float t = Mathf.Clamp01((Time.time - fadeStart) / FadeDuration);
                SetAlpha(1f - t);
                if (t >= 1f)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }

        private void SetAlpha(float alpha)
        {
            CanvasGroup[] groups = gameObject.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].alpha = alpha;
            }

            Graphic[] graphics = gameObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color color = graphics[i].color;
                color.a = alpha;
                graphics[i].color = color;
            }
        }
    }

    public sealed class DamageNumberHoldController : MonoBehaviour
    {
        public float HoldUntil;
        private const float FadeDuration = 0.5f;
        private float fadeStart = -1f;

        public void Extend(float holdUntil)
        {
            HoldUntil = Mathf.Max(HoldUntil, holdUntil);
            gameObject.SetActive(true);
            fadeStart = -1f;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (Time.time <= HoldUntil)
            {
                fadeStart = -1f;
                SetAlpha(1f);
            }
            else
            {
                if (fadeStart < 0f) fadeStart = Time.time;
                float t = Mathf.Clamp01((Time.time - fadeStart) / FadeDuration);
                SetAlpha(1f - t);
                if (t >= 1f) UnityEngine.Object.Destroy(gameObject);
            }
        }

        private void SetAlpha(float alpha)
        {
            CanvasGroup[] groups = gameObject.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++) groups[i].alpha = alpha;
            Graphic[] graphics = gameObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color color = graphics[i].color;
                color.a = alpha;
                graphics[i].color = color;
            }
        }
    }

    public static class CombatNumberRuntime
    {
        private const float CollectTime = 0.15f;
        private const float HealContinueGrace = 2.5f;
        private const float StandardDisplayHold = 1.0f;
        private static readonly Dictionary<uint, ActiveDamageNumber> ActiveDamageNumbers = new Dictionary<uint, ActiveDamageNumber>();
        private static readonly Dictionary<uint, ActiveHealNumber> ActiveHealNumbers = new Dictionary<uint, ActiveHealNumber>();

        static CombatNumberRuntime()
        {
            RuntimeFeatureState.ConfigureCombat(
                $(Format-BoolLiteral $DamageHealingConfig.enabled),
                new Color($(Format-FloatLiteral $DamageColor.R), $(Format-FloatLiteral $DamageColor.G), $(Format-FloatLiteral $DamageColor.B), 1f),
                new Color($(Format-FloatLiteral $CritDamageColor.R), $(Format-FloatLiteral $CritDamageColor.G), $(Format-FloatLiteral $CritDamageColor.B), 1f),
                new Color($(Format-FloatLiteral $HealColor.R), $(Format-FloatLiteral $HealColor.G), $(Format-FloatLiteral $HealColor.B), 1f),
                $(Format-BoolLiteral $UseDamageColor),
                $(Format-BoolLiteral $UseCritDamageColor),
                $(Format-BoolLiteral $UseHealColor),
                $(Format-FloatLiteral $DamageSize),
                $(Format-FloatLiteral $HealSize),
                $(Format-FloatLiteral $SelfHealSize),
                $(Format-FloatLiteral $DamageHealingAlpha),
                $(Format-FloatLiteral $MinimumHeal),
                $(Format-BoolLiteral $ShowFriendlyHealing),
                $(Format-BoolLiteral $ShowSelfHealing),
                $(Format-BoolLiteral $CombineDamageUntilHidden),
                $(Format-BoolLiteral $CombineHealingUntilHidden),
                65f,
                65f);
            RuntimeFeatureState.SetSelfHealX($(Format-FloatLiteral $SelfHealX));
            RuntimeFeatureState.SetSelfHealY($(Format-FloatLiteral $SelfHealY));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void AttachHealing(GuiDamageNumberDetector detector)
        {
            if (detector == null) return;
            currentDetector = detector;
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
                    typeof(CombatNumberRuntime).GetMethod("OnHealthChangedReflected", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        private static void OnHealthChangedReflected(GlobalUnitHealthChangeArgs args)
        {
            try { OnHealthChanged(currentDetector, args); } catch { }
        }

        private static GuiDamageNumberDetector currentDetector;

        public static bool ShouldShowDamageNumber(DamageInfo args)
        {
            if (args == null || args.SourceUnitId == null)
            {
                return false;
            }

            Unit source = Singleton<UnitsRegistry>.Instance.Get(args.SourceUnitId.Value);
            if (source == null)
            {
                return false;
            }

            if (source.IsMyPlayer)
            {
                return true;
            }

            return source.UnitCard != null &&
                source.UnitCard.TreatHitsAsOwnerHits &&
                source.OwnerPlayerId == Singleton<PlayerData>.Instance.Id;
        }

        private static readonly System.Collections.Generic.HashSet<int> attachedNumbers = new System.Collections.Generic.HashSet<int>();
        private static readonly System.Reflection.FieldInfo healthbarMakerField = typeof(GuiHealthbar).GetField("maker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static void AttachToHealthbarUi(GuiDamageNumber number, Unit unit, float offsetY)
        {
            if (object.ReferenceEquals(number, null) || object.ReferenceEquals(unit, null)) return;
            int id = number.GetInstanceID();
            // Already processed - just update position if parented to healthbar
            if (attachedNumbers.Contains(id))
            {
                if (number.transform.parent != null)
                    number.transform.localPosition = new Vector3(0f, offsetY, 0f);
                return;
            }
            GuiHealthBarMaker maker = unit.GetComponentInChildren<GuiHealthBarMaker>();
            if (object.ReferenceEquals(maker, null))
            {
                number.GetOrAddComponent<GuiFollow>().WorldTarget = unit.transform;
                attachedNumbers.Add(id);
                return;
            }
            if (object.ReferenceEquals(healthbarMakerField, null))
            {
                number.GetOrAddComponent<GuiFollow>().WorldTarget = maker.transform;
                attachedNumbers.Add(id);
                return;
            }
            GuiHealthbar[] allBars = UnityEngine.Object.FindObjectsOfType<GuiHealthbar>();
            for (int i = 0; i < allBars.Length; i++)
            {
                if (object.ReferenceEquals(allBars[i], null)) continue;
                object hbMaker = healthbarMakerField.GetValue(allBars[i]);
                if (object.ReferenceEquals(hbMaker, maker))
                {
                    number.transform.SetParent(allBars[i].Content.transform, false);
                    number.transform.localPosition = new Vector3(0f, offsetY, 0f);
                    GuiFollow existing = number.GetComponent<GuiFollow>();
                    if (!object.ReferenceEquals(existing, null))
                    {
                        existing.enabled = false;
                        UnityEngine.Object.Destroy(existing);
                    }
                    attachedNumbers.Add(id);
                    return;
                }
            }
            // No matching healthbar found - track via maker transform
            number.GetOrAddComponent<GuiFollow>().WorldTarget = maker.transform;
            attachedNumbers.Add(id);
        }

        public static void ApplyDamageNumber(GuiDamageNumber number, bool crit)
        {
            if (object.ReferenceEquals(number, null)) return;
            AttachToHealthbarUi(number, number.Unit, RuntimeFeatureState.DamageNumberOffsetY);
            if (RuntimeFeatureState.DamageNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * RuntimeFeatureState.DamageNumberSizeMultiplier;
            bool useColor = crit ? RuntimeFeatureState.UseCritDamageNumberColor : RuntimeFeatureState.UseDamageNumberColor;
            Color color = crit ? RuntimeFeatureState.GetCritDamageNumberColor() : RuntimeFeatureState.GetDamageNumberColor();
            if (number.Damage != null && (useColor || RuntimeFeatureState.DamageNumberSizeMultiplier != 1f))
            {
                if (useColor) number.Damage.color = color;
                if (RuntimeFeatureState.DamageNumberSizeMultiplier != 1f) number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * RuntimeFeatureState.DamageNumberSizeMultiplier));
            }
            if (useColor) ApplyGraphics(number.gameObject, color);
        }

        public static float GetDamageCollectTime(float original)
        {
            return RuntimeFeatureState.CombineDamageUntilHidden ? 99999f : original;
        }

        public static GuiDamageNumber RefreshDamageNumber(GuiDamageNumberDetector detector, GuiDamageNumber oldNumber, Unit unit, float value, bool crit)
        {
            if (!RuntimeFeatureState.CombineDamageUntilHidden || detector == null || unit == null || oldNumber == null)
            {
                RefreshDamageHold(oldNumber);
                ApplyDamageNumber(oldNumber, crit);
                return oldNumber;
            }

            ActiveDamageNumber active;
            if (ActiveDamageNumbers.TryGetValue(unit.Id, out active) && active.Number != null)
            {
                if (active.Number == oldNumber)
                {
                    active.Value = value;
                    active.IsCrit = crit;
                    active.LastTime = Time.time;
                    RefreshDamageHold(active.Number);
                    ApplyDamageNumber(active.Number, active.IsCrit);
                    return active.Number;
                }
                else
                {
                    float combined = active.Value + value;
                    UnityEngine.Object.Destroy(active.Number.gameObject);
                    active.Value = combined;
                    active.IsCrit = crit;
                    active.LastTime = Time.time;
                    active.Number = oldNumber;
                    oldNumber.DamageValue = combined;
                    RefreshDamageHold(oldNumber);
                    ApplyDamageNumber(oldNumber, active.IsCrit);
                    return oldNumber;
                }
            }

            // First hit: adopt the game-created number directly.
            RefreshDamageHold(oldNumber);
            ApplyDamageNumber(oldNumber, crit);
            ActiveDamageNumbers[unit.Id] = new ActiveDamageNumber
            {
                Number = oldNumber,
                Value = value,
                IsCrit = crit,
                LastTime = Time.time
            };
            return oldNumber;
        }

        public static void OnHealthChanged(GuiDamageNumberDetector detector, GlobalUnitHealthChangeArgs args)
        {
            if (detector == null || args == null || args.unit == null) return;
            if (!Singleton<Settings>.Instance.ShowCombatNumbers) return;

            float amount = args.newHealth - args.oldHealth;
            if (amount < RuntimeFeatureState.MinimumHeal) return;
            if (args.unit.PlayerId == null) return;
            // Suppress the fake full-health "heal" fired on spawn: the unit's Health field
            // starts at 0, so the first server update produces oldHealth=0 → newHealth=maxHealth.
            // Any heal event where oldHealth was 0 and the unit is brand-new is a spawn artifact.
            if (args.oldHealth == 0f && Time.time - args.unit.CreationTime < 1f) return;
            if (args.unit.IsMyPlayer)
            {
                if (!RuntimeFeatureState.ShowSelfHealing) return;
            }
            else if (!RuntimeFeatureState.ShowFriendlyHealing || !args.unit.Team.IsMy())
            {
                return;
            }

            SpawnOrUpdateHeal(detector, args.unit, amount);
        }

        private static void SpawnOrUpdateHeal(GuiDamageNumberDetector detector, Unit unit, float amount)
        {
            ActiveHealNumber active;
            if (ActiveHealNumbers.TryGetValue(unit.Id, out active) && ShouldCombineHeal(active))
            {
                active.Value += amount;
                active.LastTime = Time.time;
                if (active.Number == null)
                {
                    active.Number = CreateHealNumber(detector, unit, active.Value);
                }
                else
                {
                    ApplyHealTextAndColor(active.Number, active.Value, detector.HealColor, false);
                    RefreshHealHold(active.Number);
                }
                return;
            }

            GuiDamageNumber number = CreateHealNumber(detector, unit, amount);
            ActiveHealNumbers[unit.Id] = new ActiveHealNumber
            {
                Number = number,
                Value = amount,
                LastTime = Time.time
            };
        }

        private static GuiDamageNumber CreateHealNumber(GuiDamageNumberDetector detector, Unit unit, float amount)
        {
            GuiDamageNumber number = GameObjectMaker.AddChild<GuiDamageNumber>(detector.transform, detector.Prefab.gameObject, false);
            number.IsMeCaster = true;
            number.Unit = unit;
            number.DamageValue = amount;

            if (unit.IsMyPlayer)
            {
                // Self-heal: stay on HUD canvas at a fixed screen position near health bar
                GuiFollow g = number.GetComponent<GuiFollow>();
                if (!object.ReferenceEquals(g, null))
                {
                    g.enabled = false;
                    UnityEngine.Object.Destroy(g);
                }
                number.transform.localPosition = new Vector3(RuntimeFeatureState.SelfHealX, RuntimeFeatureState.SelfHealY, 0f);
                float selfSize = RuntimeFeatureState.SelfHealNumberSizeMultiplier;
                if (selfSize != 1f) number.transform.localScale = number.transform.localScale * selfSize;
            }
            else
            {
                AttachToHealthbarUi(number, unit, RuntimeFeatureState.HealNumberOffsetY);
            }
            if (RuntimeFeatureState.HealNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * RuntimeFeatureState.HealNumberSizeMultiplier;
            ApplyHealTextAndColor(number, amount, detector.HealColor, true);
            RefreshHealHold(number);
            return number;
        }

        private static bool ShouldCombineHeal(ActiveHealNumber active)
        {
            if (active == null) return false;
            if (!RuntimeFeatureState.CombineHealingUntilHidden) return Time.time - active.LastTime <= CollectTime;
            return Time.time - active.LastTime <= HealContinueGrace;
        }

        private static void RefreshHealHold(GuiDamageNumber number)
        {
            if (number == null) return;
            HealingNumberHoldController hold = number.gameObject.GetComponent<HealingNumberHoldController>();
            if (hold == null)
            {
                UiTemporary[] temporaries = number.gameObject.GetComponentsInChildren<UiTemporary>(true);
                for (int i = 0; i < temporaries.Length; i++)
                {
                    UnityEngine.Object.Destroy(temporaries[i]);
                }
                hold = number.gameObject.AddComponent<HealingNumberHoldController>();
            }
            hold.Extend(Time.time + (RuntimeFeatureState.CombineHealingUntilHidden ? HealContinueGrace : StandardDisplayHold));
        }

        private static void RefreshDamageHold(GuiDamageNumber number)
        {
            if (number == null) return;
            DamageNumberHoldController hold = number.gameObject.GetComponent<DamageNumberHoldController>();
            if (hold == null)
            {
                UiTemporary[] temporaries = number.gameObject.GetComponentsInChildren<UiTemporary>(true);
                for (int i = 0; i < temporaries.Length; i++)
                    UnityEngine.Object.Destroy(temporaries[i]);
                Animation[] animations = number.gameObject.GetComponentsInChildren<Animation>(true);
                for (int i = 0; i < animations.Length; i++)
                    animations[i].Stop();
                Animator[] animators = number.gameObject.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                    animators[i].enabled = false;
                hold = number.gameObject.AddComponent<DamageNumberHoldController>();
            }
            hold.Extend(Time.time + (RuntimeFeatureState.CombineDamageUntilHidden ? HealContinueGrace : StandardDisplayHold));
        }

        private static void ApplyHealTextAndColor(GuiDamageNumber number, float amount, Color defaultHealColor, bool applySize)
        {
            if (number == null || number.Damage == null) return;
            number.Damage.text = "+" + Mathf.RoundToInt(amount).ToString();
            number.Damage.color = RuntimeFeatureState.UseHealNumberColor ? RuntimeFeatureState.GetHealNumberColor() : defaultHealColor;
            if (applySize && RuntimeFeatureState.HealNumberSizeMultiplier != 1f)
            {
                number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * RuntimeFeatureState.HealNumberSizeMultiplier));
            }
            ApplyGraphics(number.gameObject, number.Damage.color);
        }

        private static void ApplyGraphics(GameObject go, Color color)
        {
            Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].color = color;
            }
        }

        private static void RestartLifetime(GameObject go)
        {
            if (go == null) return;
            go.SetActive(true);

            Animation[] animations = go.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                animations[i].Stop();
                animations[i].Rewind();
                animations[i].Play();
            }

            Animator[] animators = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = true;
                animators[i].Play(0, -1, 0f);
                animators[i].Update(0f);
            }

            CanvasGroup[] groups = go.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].alpha = 1f;
            }
        }

        private sealed class ActiveHealNumber
        {
            public GuiDamageNumber Number;
            public float Value;
            public float LastTime;
        }

        private sealed class ActiveDamageNumber
        {
            public GuiDamageNumber Number;
            public float Value;
            public bool IsCrit;
            public float LastTime;
        }
    }
}

"@

if (Test-Path $RuntimeMenuSourcePath) {
    $RuntimeMenuSource = Get-Content -Raw -LiteralPath $RuntimeMenuSourcePath
    $RuntimeMenuSource = [regex]::Replace($RuntimeMenuSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $RuntimeMenuSource
}
if (Test-Path $MatchReplayRecorderSourcePath) {
    $MatchReplayRecorderSource = Get-Content -Raw -LiteralPath $MatchReplayRecorderSourcePath
    $MatchReplayRecorderSource = [regex]::Replace($MatchReplayRecorderSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $MatchReplayRecorderSource
}
if (Test-Path $ReplayPlayerSourcePath) {
    $ReplayPlayerSource = Get-Content -Raw -LiteralPath $ReplayPlayerSourcePath
    $ReplayPlayerSource = [regex]::Replace($ReplayPlayerSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $ReplayPlayerSource
}
if (Test-Path $AudioReplacerSourcePath) {
    $AudioReplacerSource = Get-Content -Raw -LiteralPath $AudioReplacerSourcePath
    $AudioReplacerSource = [regex]::Replace($AudioReplacerSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $AudioReplacerSource
}
if (Test-Path $MeshReplacerSourcePath) {
    $MeshReplacerSource = Get-Content -Raw -LiteralPath $MeshReplacerSourcePath
    $MeshReplacerSource = [regex]::Replace($MeshReplacerSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $MeshReplacerSource
}

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class AdsSensitivityRuntime
    {
        static AdsSensitivityRuntime()
        {
            RuntimeFeatureState.ConfigureFov($(Format-BoolLiteral ($EnableFovFeature -and [bool]$FovConfig.enabled)), $DefaultForcedFovLiteral, $AdsSensitivityMultiplierLiteral, $DefaultWeaponModelFovLiteral);
        }

        public static float ApplyAdsScale(float currentScale, Unit unit)
        {
            if (unit != null && unit.GetAimingState() != null)
            {
                return currentScale * RuntimeFeatureState.AdsSensitivityMultiplier;
            }

            return currentScale;
        }

        private static CameraFov cachedCameraFov;
        private static CameraArms cachedCameraArms;

        public static void ApplyCameraFov()
        {
            if (!RuntimeFeatureState.FovSupported) return;
            try
            {
                if (cachedCameraFov == null)
                    cachedCameraFov = UnityEngine.Object.FindObjectOfType<CameraFov>();
                if (cachedCameraFov == null) return;
                Camera camera = FindPrimaryCamera(cachedCameraFov);
                if (camera != null)
                    camera.fieldOfView = RuntimeFeatureState.ForcedFov;
            }
            catch { }
        }

        public static void ApplyWeaponModelFov()
        {
            if (!RuntimeFeatureState.FovSupported) return;
            try
            {
                if (cachedCameraArms == null)
                    cachedCameraArms = UnityEngine.Object.FindObjectOfType<CameraArms>();
                if (cachedCameraArms == null) return;
                Camera[] cameras = cachedCameraArms.GetComponentsInChildren<Camera>(true);
                if (cameras == null || cameras.Length == 0)
                {
                    Camera primary = cachedCameraArms.GetComponent<Camera>();
                    if (primary != null)
                    {
                        primary.fieldOfView = RuntimeFeatureState.WeaponModelFov;
                    }
                    return;
                }

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null)
                    {
                        continue;
                    }

                    if (camera != Camera.main || cameras.Length == 1)
                    {
                        camera.fieldOfView = RuntimeFeatureState.WeaponModelFov;
                    }
                }
            }
            catch
            {
            }
        }

        private static Camera FindPrimaryCamera(Component component)
        {
            if (component == null)
            {
                return Camera.main;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = component.GetType().GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!typeof(Camera).IsAssignableFrom(fields[i].FieldType))
                {
                    continue;
                }

                Camera fieldCamera = fields[i].GetValue(component) as Camera;
                if (fieldCamera != null)
                {
                    return fieldCamera;
                }
            }

            PropertyInfo[] properties = component.GetType().GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                if (!properties[i].CanRead || !typeof(Camera).IsAssignableFrom(properties[i].PropertyType) || properties[i].GetIndexParameters().Length != 0)
                {
                    continue;
                }

                Camera propertyCamera = null;
                try
                {
                    propertyCamera = properties[i].GetValue(component, null) as Camera;
                }
                catch
                {
                }

                if (propertyCamera != null)
                {
                    return propertyCamera;
                }
            }

            Camera[] children = component.GetComponentsInChildren<Camera>(true);
            if (children != null && children.Length > 0)
            {
                return children[0];
            }

            return Camera.main;
        }
    }
}

namespace BnlCommunityFixes
{
    public static class TeamColorRuntime
    {
        static TeamColorRuntime()
        {
            RuntimeFeatureState.ConfigureTeamColors(
                $(Format-BoolLiteral $TeamColorConfig.enabled),
                new Color($(Format-FloatLiteral $RuntimeFriendlyTeamColor.R), $(Format-FloatLiteral $RuntimeFriendlyTeamColor.G), $(Format-FloatLiteral $RuntimeFriendlyTeamColor.B), 1f),
                new Color($(Format-FloatLiteral $RuntimeEnemyTeamColor.R), $(Format-FloatLiteral $RuntimeEnemyTeamColor.G), $(Format-FloatLiteral $RuntimeEnemyTeamColor.B), 1f));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static Color GetGuiFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetGuiEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetGuiBackgroundFriendlyColor() { return RuntimeFeatureState.GetGuiBackgroundFriendlyColor(); }
        public static Color GetGuiBackgroundEnemyColor() { return RuntimeFeatureState.GetGuiBackgroundEnemyColor(); }
        public static Color GetObjectCommonFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetObjectCommonEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetForceFieldFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetForceFieldEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetIceFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetIceEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
    }

    public static class BaseObjectiveBeamRuntime
    {
        static BaseObjectiveBeamRuntime()
        {
            RuntimeFeatureState.ConfigureBaseObjectiveBeam(
                $(Format-BoolLiteral $BaseObjectiveBeamConfig.enabled),
                $(Format-BoolLiteral ([bool]$BaseObjectiveBeamConfig.hide_beam)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ShouldHide()
        {
            return RuntimeFeatureState.HideBaseObjectiveBeam;
        }
    }

    public static class HideImpactVfxRuntime
    {
        private static bool initialized;

        static HideImpactVfxRuntime()
        {
            RuntimeFeatureState.ConfigureHideImpactVfx(
                $(Format-BoolLiteral $HideImpactVfxConfig.enabled),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_impact_vfx)),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_lava_water_plane)),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_falling_blocks)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL HideVfx] initialized - hideImpact=" + RuntimeFeatureState.HideImpactVfx + " hidePlane=" + RuntimeFeatureState.HideLavaWaterPlane + " hideFallingBlocks=" + RuntimeFeatureState.HideFallingBlocks);
            initialized = true;
        }

        public static void EnsureInit() { }

        public static bool ShouldHideVfx()
        {
            return RuntimeFeatureState.HideImpactVfx;
        }

        public static bool ShouldHidePlane()
        {
            return RuntimeFeatureState.HideLavaWaterPlane;
        }

        public static bool ShouldHideFallingBlocks()
        {
            return RuntimeFeatureState.HideFallingBlocks;
        }

        public static void DestroyFallingBlock(UnityEngine.GameObject go)
        {
            if (!RuntimeFeatureState.HideFallingBlocks) return;
            if (go == null) return;
            UnityEngine.Object.Destroy(go);
        }

        public static void HidePlane(MapPlane plane)
        {
            if (!RuntimeFeatureState.HideLavaWaterPlane) return;
            if (plane == null) return;
            Renderer[] renderers = plane.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
            UnityEngine.Debug.Log("[BNL HideVfx] HidePlane: disabled " + renderers.Length + " renderer(s)");
        }
    }

    public static class UnitGuiScaleRuntime
    {
        static UnitGuiScaleRuntime()
        {
            RuntimeFeatureState.ConfigureUnitGuiScale(
                $(Format-BoolLiteral $UnitGuiScaleConfig.enabled),
                $(Format-FloatLiteral ([double]$UnitGuiScaleConfig.scale_multiplier)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL UnitGuiScale] initialized - scale=" + RuntimeFeatureState.UnitGuiScaleMultiplier);
        }

        public static void EnsureInit() { }

        public static float GetScaleMultiplier()
        {
            return RuntimeFeatureState.UnitGuiScaleMultiplier;
        }
    }

    public static class WsiScaleRuntime
    {
        static WsiScaleRuntime()
        {
            RuntimeFeatureState.ConfigureWsiScale(
                $(Format-BoolLiteral $WsiConfig.scale_enabled),
                $(Format-FloatLiteral ([double]$WsiConfig.scale_multiplier)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL WsiScale] initialized - scale=" + RuntimeFeatureState.WsiScaleMultiplier);
        }

        public static void EnsureInit() { }

        public static void ApplyScale(GuiWorldSpaceIndicator indicator)
        {
            if (!RuntimeFeatureState.WsiScaleSupported) return;
            float m = RuntimeFeatureState.WsiScaleMultiplier;
            indicator.IconMinSize = indicator.IconMinSize * m;
            indicator.IconMaxSize = indicator.IconMaxSize * m;
        }
    }

    public static class MapRenderOverrideRuntime
    {
        static MapRenderOverrideRuntime()
        {
            RuntimeFeatureState.ConfigureMapRenderOverride(
                $(Format-BoolLiteral $MapRenderConfig.enabled),
                "$MapRenderPresetLiteral");
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL MapRender] initialized - preset=" + RuntimeFeatureState.MapRenderOverride);
        }

        public static void EnsureInit() { }

        public static string GetRenderOverride(string original)
        {
            if (!RuntimeFeatureState.MapRenderOverrideSupported) return original;
            string ov = RuntimeFeatureState.MapRenderOverride;
            if (ov == null || ov == "Default") return original;
            return ov;
        }
    }

    public static class DpsOverlayRuntime
    {
        private static float sessionStart;
        private static float sessionTotal;
        private static float lastHitTime;
        private static float currentDps;
        private static bool sessionActive;
        private const float SessionResetSeconds = 2f;

        static DpsOverlayRuntime()
        {
            RuntimeFeatureState.ConfigureDpsOverlay(true, false);
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            SubscribePhaseUpdate();
            UnityEngine.Debug.Log("[BNL DpsOverlay] initialized");
        }

        public static void EnsureInit() { }

        public static void TryRecordDamage(DamageInfo args)
        {
            if (!RuntimeFeatureState.DpsOverlayEnabled) return;
            if (args == null) return;
            Unit player = Singleton<UnitsRegistry>.Instance.GetPlayer();
            if (player == null || args.SourceUnitId == null || args.SourceUnitId.Value != player.Id) return;
            RecordDamage(args.Damage);
        }

        public static void RecordDamage(float amount)
        {
            if (amount <= 0f) return;

            float now = UnityEngine.Time.time;

            if (!sessionActive || (now - lastHitTime) >= SessionResetSeconds)
            {
                sessionStart = now;
                sessionTotal = amount;
                lastHitTime = now;
                sessionActive = true;
                return;
            }

            sessionTotal += amount;
            lastHitTime = now;

            float elapsed = now - sessionStart;
            if (elapsed > 0f)
                currentDps = sessionTotal / elapsed;
        }

        public static void ResetSession()
        {
            sessionActive = false;
            sessionTotal = 0f;
            currentDps = 0f;
        }

        public static string GetDisplayText()
        {
            return "DPS: " + UnityEngine.Mathf.RoundToInt(currentDps).ToString();
        }


        private static void SubscribePhaseUpdate()
        {
            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger == null) return;
                var field = typeof(ZoneMessenger).GetField("OnGlobalPhaseUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (object.ReferenceEquals(field, null)) return;
                var eventSource = field.GetValue(messenger);
                if (object.ReferenceEquals(eventSource, null)) return;
                var subscribeMethod = eventSource.GetType().GetMethod("Subscribe");
                if (object.ReferenceEquals(subscribeMethod, null)) return;
                var handler = System.Delegate.CreateDelegate(
                    typeof(System.Action<>).MakeGenericType(typeof(GlobalPhaseUpdateEventArgs)),
                    typeof(DpsOverlayRuntime).GetMethod("OnPhaseUpdate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        public static void OnPhaseUpdate(GlobalPhaseUpdateEventArgs args)
        {
            try
            {
                if (args == null) return;
                bool wasNotAssault = args.oldPhase == null || args.oldPhase.PhaseType != Protocol.ZonePhaseType.Assault;
                bool isAssault = args.newPhase != null && args.newPhase.PhaseType == Protocol.ZonePhaseType.Assault;
                if (wasNotAssault && isAssault)
                {
                    ResetSession();
                }
            }
            catch { }
        }
    }

    public static class AimHealthbarRuntime
    {
        static AimHealthbarRuntime()
        {
            RuntimeFeatureState.ConfigureAimHealthbar($(Format-BoolLiteral $AimHealthbarConfig.enabled), $(Format-BoolLiteral $AimHealthbarConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ShouldShow(Unit healthbarUnit)
        {
            if (!RuntimeFeatureState.AimHealthbarEnabled || healthbarUnit == null)
            {
                return false;
            }

            Crosshair crosshair = Singleton<Crosshair>.Instance;
            if (crosshair == null)
            {
                return false;
            }

            return crosshair.RaycastUnitInfo.Unit == healthbarUnit;
        }
    }

    public static class DeathCamRuntime
    {
        static DeathCamRuntime()
        {
            RuntimeFeatureState.ConfigureDeathCamHealthbar($(Format-BoolLiteral $DeathCamHealthbarConfig.enabled), $(Format-BoolLiteral $DeathCamHealthbarConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool IsDeathCamFriendly(Unit healthbarUnit)
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return false;
            if (healthbarUnit == null) return false;
            if (healthbarUnit.IsMyPlayer) return false;
            // Only show for actual player units (has PlayerId), not devices/minions
            if (!healthbarUnit.PlayerId.HasValue) return false;
            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null) return false;
            Unit player = registry.GetPlayer();
            if (player == null || !player.IsDeath) return false;
            return healthbarUnit.Team == player.Team;
        }

        public static void AttachDeathCamController(GuiHealthbar healthbar)
        {
            if (healthbar == null) return;
            DeathCamHealthbarController ctrl = healthbar.gameObject.GetComponent<DeathCamHealthbarController>();
            if (ctrl == null)
                ctrl = healthbar.gameObject.AddComponent<DeathCamHealthbarController>();
            ctrl.Init(healthbar);
        }

        public static void UpdateDeathCamHpText(UnityEngine.UI.Text nicknameText)
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return;
            if (nicknameText == null) return;
            try
            {
                CameraDeath deathCam = Singleton<CameraDeath>.Instance;
                if (deathCam == null) return;
                Transform targetTransform = deathCam.Target;
                if (targetTransform == null) return;
                Unit targetUnit = targetTransform.GetComponent<Unit>();
                if (targetUnit == null) return;
                float health = targetUnit.Health;
                float maxHealth = targetUnit.MaxHealth;
                float pct = (maxHealth > 0f) ? Mathf.Clamp01(health / maxHealth) : 0f;
                int filled = Mathf.RoundToInt(pct * 10f);
                string bar = "";
                for (int i = 0; i < 10; i++)
                    bar += (i < filled) ? "\u2588" : "\u2591";
                string playerName = targetUnit.name;
                if (targetUnit.PlayerId.HasValue)
                {
                    FriendInfo fi = PlayerData.Instance.FindFriend(targetUnit.PlayerId.Value);
                    if (fi != null && !string.IsNullOrEmpty(fi.Nickname))
                    {
                        playerName = fi.Nickname;
                    }
                }
                string hpInfo = string.Format(" {0} {1:F0}/{2:F0}", bar, health, maxHealth);
                nicknameText.text = playerName + hpInfo;
            }
            catch { }
        }
    }

    public sealed class DeathCamHealthbarController : MonoBehaviour
    {
        private static readonly float LowThreshold = $(Format-FloatLiteral $FriendlyLowHealthThreshold);
        private static readonly Color AlertColor = new Color($(Format-FloatLiteral $FriendlyLowHealthColor.R), $(Format-FloatLiteral $FriendlyLowHealthColor.G), $(Format-FloatLiteral $FriendlyLowHealthColor.B), 1f);
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);

        private GuiHealthbar healthbar;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
        }

        private void LateUpdate()
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return;
            if (healthbar == null || healthbar.HealthBar == null) return;

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.IsDeath) return;

            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            Unit myPlayer = registry != null ? registry.GetPlayer() : null;
            if (myPlayer == null || !myPlayer.IsDeath) return;
            if (unit.Team != myPlayer.Team) return;

            float maxHp = unit.MaxHealth;
            bool isLow = maxHp > 0f && (unit.Health / maxHp) <= LowThreshold;
            if (isLow)
            {
                healthbar.HealthBar.color = AlertColor;
                if (healthbar.Title != null)
                    healthbar.Title.color = AlertColor;
            }
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class HealAlertRuntime
    {
        private static readonly bool UseDamageIndicatorColor = $(Format-BoolLiteral $HealAlertUseDamageColor);
        private static readonly bool UseHealIndicatorColor   = $(Format-BoolLiteral $HealAlertUseHealColor);
        private static readonly Color DamageIndicatorBaseColor = new Color($(Format-FloatLiteral $HealAlertDamageColor.R), $(Format-FloatLiteral $HealAlertDamageColor.G), $(Format-FloatLiteral $HealAlertDamageColor.B), $(Format-FloatLiteral $HealAlertDamageColor.A));
        private static readonly Color HealIndicatorBaseColor   = new Color($(Format-FloatLiteral $HealAlertHealColor.R),   $(Format-FloatLiteral $HealAlertHealColor.G),   $(Format-FloatLiteral $HealAlertHealColor.B),   $(Format-FloatLiteral $HealAlertHealColor.A));

        static HealAlertRuntime()
        {
            RuntimeFeatureState.ConfigureHealAlert(
                $(Format-BoolLiteral $HealAlertConfig.enabled),
                $(Format-FloatLiteral $HealAlertDamageSize),
                $(Format-FloatLiteral $HealAlertHealSize),
                $(Format-FloatLiteral $HealAlertMinimumHeal),
                $(Format-BoolLiteral $HealAlertShowDir));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void AttachHealBridge(GuiHitAlertMaker maker)
        {
            if (maker == null) return;
            currentBridgeMaker = maker;
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
                    maker,
                    typeof(HealAlertRuntime).GetMethod("OnHealthChangedReflected", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        private static void OnHealthChangedReflected(GlobalUnitHealthChangeArgs args)
        {
            try { OnHealthChanged(currentBridgeMaker, args); } catch { }
        }

        private static GuiHitAlertMaker currentBridgeMaker;

        public static void ApplyDamageIndicator(Component component)
        {
            GameObject go = component == null ? null : component.gameObject;
            if (go == null) return;
            if (RuntimeFeatureState.HealAlertDamageSizeMultiplier != 1f)
                go.transform.localScale = go.transform.localScale * RuntimeFeatureState.HealAlertDamageSizeMultiplier;
            if (UseDamageIndicatorColor) ApplyGraphics(go, DamageIndicatorBaseColor);
        }

        public static void OnHealthChanged(GuiHitAlertMaker maker, GlobalUnitHealthChangeArgs args)
        {
            if (maker == null || args == null || args.unit == null) return;
            if (!args.unit.IsMyPlayer) return;
            if (maker.Content == null || !maker.Content.activeSelf) return;

            float healAmount = args.newHealth - args.oldHealth;
            if (healAmount < RuntimeFeatureState.HealAlertMinimumHeal) return;

            if (RuntimeFeatureState.HealAlertShowDirectionOnHeal)
            {
                GuiHitAlert dir = GameObjectMaker.AddChild<GuiHitAlert>(maker.transform, maker.DirectionHitPrefab.gameObject, false);
                dir.DestroySelf = true;
                dir.AttackerPosition = Vector3.zero;
                ApplyHealIndicator(dir);
            }

            GuiHitAlertOnScreenEdge edge = GameObjectMaker.AddChild<GuiHitAlertOnScreenEdge>(maker.transform, maker.OnScreenEdgePrefab.gameObject, false);
            edge.DestroySelf = true;
            edge.AttackerPosition = Vector3.zero;
            ApplyHealIndicator(edge);
        }

        private static void ApplyHealIndicator(Component component)
        {
            GameObject go = component == null ? null : component.gameObject;
            if (go == null) return;
            if (RuntimeFeatureState.HealAlertHealSizeMultiplier != 1f)
                go.transform.localScale = go.transform.localScale * RuntimeFeatureState.HealAlertHealSizeMultiplier;
            if (UseHealIndicatorColor) ApplyGraphics(go, HealIndicatorBaseColor);
            ApplyImmediateFade(go);
        }

        private static void ApplyGraphics(GameObject go, Color color)
        {
            Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) graphics[i].color = color;
        }

        private static void ApplyImmediateFade(GameObject go)
        {
            UiTemporary[] temporaries = go.GetComponentsInChildren<UiTemporary>(true);
            for (int i = 0; i < temporaries.Length; i++)
            {
                temporaries[i].ShowDuration = 0f;
                temporaries[i].FadeDuration = 0.1f;
            }
        }
    }
}
"@

if ($LocalBuildPreviewConfig.enabled) {
    $HelperSource += @"

namespace BnlCommunityFixes
{
    public static class LocalBuildPredictionRuntime
    {
        private static readonly float InstantCrateChainWindowSeconds = 5.0f;
        private static PredictionManager manager;
        private static float instantCrateChainUntil;

        private const float PredictionTimeoutSeconds = 3f;

        static LocalBuildPredictionRuntime()
        {
            RuntimeFeatureState.ConfigureLocalBuildPreview($(Format-BoolLiteral $LocalBuildPreviewConfig.enabled), true, PredictionTimeoutSeconds);
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        private static bool Enabled
        {
            get { return RuntimeFeatureState.LocalBuildPreviewEnabled; }
        }

        private static PredictionManager Manager
        {
            get
            {
                if (!Enabled) return null;
                if (manager == null)
                {
                    UnityEngine.GameObject go = UnityEngine.GameObject.Find("BNL_LOCAL_BUILD_PREDICTION");
                    if (go == null) { go = new UnityEngine.GameObject("BNL_LOCAL_BUILD_PREDICTION"); UnityEngine.Object.DontDestroyOnLoad(go); }
                    manager = go.GetComponent<PredictionManager>();
                    if (manager == null) manager = go.AddComponent<PredictionManager>();
                }
                return manager;
            }
        }

        private static bool IsBlockBackedDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            return blockCard != null && blockCard.BlockId != 0;
        }

        private static bool IsInstantPlacementDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            if (blockCard == null || blockCard.BlockId == 0) return false;
            if (deviceCard.BuildTime.GetValueOrDefault(0f) > 0f) return false;
            // Bounce/speed pads have server-side placement validation that clients can't replicate -
            // skip instant placement for them to avoid ghost blocks the server rejects.
            if (blockCard.Special is BlockSpecialBounce) return false;
            if (blockCard.Special is BlockSpecialFastMovement) return false;
            return true;
        }

        private static bool IsCratePlacementDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            return blockCard != null && blockCard.BlockId == 58;
        }

        private static void ActivateInstantCrateChainWindow()
        {
            instantCrateChainUntil = UnityEngine.Mathf.Max(instantCrateChainUntil, UnityEngine.Time.time + InstantCrateChainWindowSeconds);
        }

        private static bool IsInstantCrateChainWindowActive()
        {
            return Enabled && UnityEngine.Time.time < instantCrateChainUntil;
        }

        public static bool ShouldBypassBuildValidate(ToolLogicBuild tool)
        {
            return tool != null && tool.Unit != null && tool.Unit.IsMyPlayer && tool.Unit.CurrentDevice != null &&
                   (IsInstantPlacementDeviceKey(tool.Unit.CurrentDevice.DeviceKey) ||
                    (IsCratePlacementDeviceKey(tool.Unit.CurrentDevice.DeviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static bool ShouldZeroBuildTime(Unit unit, Key deviceKey)
        {
            return unit != null && unit.IsMyPlayer &&
                   (IsInstantPlacementDeviceKey(deviceKey) ||
                    (IsCratePlacementDeviceKey(deviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static bool ShouldZeroBuildTiming()
        {
            Unit player = Singleton<UnitsRegistry>.Instance != null ? Singleton<UnitsRegistry>.Instance.GetPlayer() : null;
            return player != null && player.IsMyPlayer && player.CurrentDevice != null &&
                   (IsInstantPlacementDeviceKey(player.CurrentDevice.DeviceKey) ||
                    (IsCratePlacementDeviceKey(player.CurrentDevice.DeviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static float GetBuildPrecastTime(ToolTiming timing)
        {
            return ShouldZeroBuildTiming() ? 0f : ToolTimingHelper.GetPrecastTime(timing);
        }

        public static float GetBuildTotalCastTime(ToolTiming timing)
        {
            return ShouldZeroBuildTiming() ? 0f : ToolTimingHelper.GetTotalCastTime(timing);
        }

        public static bool ShouldSkipBuildCompletionWait()
        {
            return false;
        }

        // Maps RPC ID → block position so OnStartBuildResult can roll back the right prediction.
        private static readonly System.Collections.Generic.Dictionary<ushort, Vector3s> pendingRpcBlockPos
            = new System.Collections.Generic.Dictionary<ushort, Vector3s>();

        public static void TryInstantAcceptStartBuild(BuildInfo info, ServiceZone.Rpc_StartBuild rpc)
        {
            if (info == null || rpc == null) return;
            if (IsInstantPlacementDeviceKey(info.DeviceKey) ||
                (IsCratePlacementDeviceKey(info.DeviceKey) && IsInstantCrateChainWindowActive()))
            {
                if (IsCratePlacementDeviceKey(info.DeviceKey)) ActivateInstantCrateChainWindow();
                // Record which block position this RPC corresponds to before optimistically accepting.
                pendingRpcBlockPos[rpc._Id] = info.BuildInsidePosition;
                rpc._Success(true);
            }
        }

        public static void OnStartBuildResult(ServiceZone.Rpc_StartBuild rpc, bool accepted)
        {
            Vector3s blockPos;
            if (!pendingRpcBlockPos.TryGetValue(rpc._Id, out blockPos)) return;
            pendingRpcBlockPos.Remove(rpc._Id);
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (accepted)
                // Server accepted the build — resolve immediately so the prediction doesn't
                // time out and roll back a block the server confirmed.
                predictionManager.ResolveBlock(blockPos);
            else
                predictionManager.RollbackBlock(blockPos);
        }

        public static void TryInstantAcceptSwitchGear(Unit unit, ServiceZone.Rpc_SwitchGear rpc)
        {
            if (!Enabled || unit == null || rpc == null) return;
            if (unit.IsMyPlayer) rpc._Success(true);
        }

        public static void OnLocalPlace(BuildGhostController controller)
        {
            if (!Enabled || controller == null) return;
            try
            {
                BuildHelper.BuildData buildData = controller.TryPlaceDevice();
                if (buildData == null || buildData.Result != BuildHelper.BuildResultType.Success || buildData.Ri == null) return;
                Unit unit = controller.GetComponent<Unit>();
                if (unit == null || unit.CurrentDevice == null) return;
                PredictionManager predictionManager = Manager;
                if (predictionManager == null) return;
                RaycastInfo ri = buildData.Ri.Value;
                CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(unit.CurrentDevice.DeviceKey);
                if (deviceCard == null) return;
                Card objectCard = Singleton<Catalogue>.Instance.GetCard<Card>(deviceCard.DeviceKey);
                if (objectCard == null) return;
                CardBlock blockCard = objectCard as CardBlock;
                // Don't create a local preview for bounce/speed pads — orientation is server-determined
                // and optimistic placement renders them wrong.
                if (blockCard != null && (blockCard.Special is BlockSpecialBounce || blockCard.Special is BlockSpecialFastMovement)) return;
                BuildGhostObject preview = BuildGhostObject.Create(unit.CurrentDevice.DeviceKey, false, unit.Team);
                PredictionEntry entry = new PredictionEntry
                {
                    DeviceKey = unit.CurrentDevice.DeviceKey,
                    SpawnCardKey = deviceCard.DeviceKey,
                    BlockPos = ri.BlockPosBuildIn,
                    WorldPos = preview.transform.position,
                    PreviousBlock = Singleton<ZoneManager>.Instance.Map.Blocks[ri.BlockPosBuildIn],
                    IsUnit = objectCard.Category == CardCategory.Unit,
                    ExpireTime = UnityEngine.Time.time + UnityEngine.Mathf.Max(0.25f, PredictionTimeoutSeconds)
                };
                if (blockCard != null && blockCard.BlockId != 0)
                {
                    if (blockCard.BlockId == 58) ActivateInstantCrateChainWindow();
                    Block newBlock = new Block((ushort)blockCard.BlockId);
                    newBlock.Team = unit.Team;
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    updates[ri.BlockPosBuildIn] = newBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(updates);
                    entry.WorldPos = ri.BlockPosBuildIn.ToVector3();
                    entry.IsRealLocalBlock = true;
                    UnityEngine.Object.Destroy(preview.gameObject);
                    preview = null;
                }
                else
                {
                    preview.SetValid(true);
                    preview.SetBlockPosition(ri.BlockPosBuildIn, ri.BlockPosBuildOn, ri.Direction);
                    preview.SetVisible(true);
                    preview.SetValue(1f);
                    entry.PreviewObject = preview.gameObject;
                    entry.WorldPos = preview.transform.position;
                }
                predictionManager.AddPrediction(entry);
            }
            catch (System.Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        public static void OnBlockUpdates(System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates)
        {
            if (!Enabled || updates == null) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            foreach (System.Collections.Generic.KeyValuePair<Vector3s, BlockUpdate> pair in updates)
                predictionManager.ResolveBlock(pair.Key);
        }

        public static void OnDeviceBuilt(uint builderPlayerId, Key deviceKey, UnityEngine.Vector3 position)
        {
            if (!Enabled) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (builderPlayerId != Singleton<PlayerData>.Instance.Id) return;
            predictionManager.ResolveDevice(deviceKey, position);
        }

        public static void OnUnitCreate(UnitInit data)
        {
            if (!Enabled || data == null || data.OwnerId == null) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (data.OwnerId.Value != Singleton<PlayerData>.Instance.Id) return;
            predictionManager.ResolveUnit(data.Key, data.Transform.GetPosition());
        }
    }

    public sealed class PredictionEntry
    {
        public Key DeviceKey;
        public Key SpawnCardKey;
        public Vector3s BlockPos;
        public UnityEngine.Vector3 WorldPos;
        public Block PreviousBlock;
        public bool IsUnit;
        public bool IsRealLocalBlock;
        public UnityEngine.GameObject PreviewObject;
        public float ExpireTime;
    }

    public sealed class PredictionManager : UnityEngine.MonoBehaviour
    {
        private readonly System.Collections.Generic.List<PredictionEntry> entries = new System.Collections.Generic.List<PredictionEntry>();
        // Rollbacks deferred because ZoneManager wasn't ready at removal time.
        private readonly System.Collections.Generic.List<PredictionEntry> pendingRollbacks = new System.Collections.Generic.List<PredictionEntry>();

        public void AddPrediction(PredictionEntry entry)
        {
            if (entry == null) return;
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                PredictionEntry current = this.entries[i];
                bool sameBlock = !current.IsUnit && !entry.IsUnit && current.BlockPos.Equals(entry.BlockPos);
                bool sameUnitSpot = current.IsUnit == entry.IsUnit && current.DeviceKey.Equals(entry.DeviceKey) && UnityEngine.Vector3.Distance(current.WorldPos, entry.WorldPos) <= 0.75f;
                if (sameBlock || sameUnitSpot)
                {
                    // If we're overwriting a prediction for the same block/unit,
                    // pass along the original world state (PreviousBlock) so
                    // we don't accidentally "resolve" to a predicted crate.
                    if (entry.PreviousBlock == null || entry.PreviousBlock.Id == 0)
                    {
                        entry.PreviousBlock = current.PreviousBlock;
                    }
                    this.RemoveAt(i, false);
                }
            }
            this.entries.Add(entry);
        }

        public void ResolveBlock(Vector3s blockPos)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (!this.entries[i].IsUnit && this.entries[i].BlockPos.Equals(blockPos))
                    this.RemoveAt(i, false);
        }

        // Server explicitly rejected the build - roll back the local block.
        public void RollbackBlock(Vector3s blockPos)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (!this.entries[i].IsUnit && this.entries[i].BlockPos.Equals(blockPos))
                    this.RemoveAt(i, true);
        }

        public void ResolveDevice(Key deviceKey, UnityEngine.Vector3 position)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (this.entries[i].DeviceKey.Equals(deviceKey) && UnityEngine.Vector3.Distance(this.entries[i].WorldPos, position) <= 1.5f)
                    this.RemoveAt(i, false);
        }

        public void ResolveUnit(Key spawnCardKey, UnityEngine.Vector3 position)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (this.entries[i].IsUnit && this.entries[i].SpawnCardKey.Equals(spawnCardKey) && UnityEngine.Vector3.Distance(this.entries[i].WorldPos, position) <= 1.5f)
                    this.RemoveAt(i, false);
        }

        private void Update()
        {
            // Retry any rollbacks that were deferred because ZoneManager wasn't ready.
            if (this.pendingRollbacks.Count > 0 &&
                Singleton<ZoneManager>.Instance != null && Singleton<ZoneManager>.Instance.MapCreated)
            {
                for (int i = this.pendingRollbacks.Count - 1; i >= 0; i--)
                {
                    PredictionEntry rb = this.pendingRollbacks[i];
                    this.pendingRollbacks.RemoveAt(i);
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> rbUpdates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    rbUpdates[rb.BlockPos] = rb.PreviousBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(rbUpdates);
                }
            }

            float now = UnityEngine.Time.time;
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                PredictionEntry entry = this.entries[i];
                if (entry == null) { this.RemoveAt(i, false); continue; }
                if (!entry.IsRealLocalBlock && entry.PreviewObject == null) { this.RemoveAt(i, false); continue; }
                if (now >= entry.ExpireTime) this.RemoveAt(i, true);
            }
        }

        private void RemoveAt(int index, bool rollbackRealLocalBlock)
        {
            PredictionEntry entry = this.entries[index];
            this.entries.RemoveAt(index);
            if (rollbackRealLocalBlock && entry != null && entry.IsRealLocalBlock)
            {
                if (Singleton<ZoneManager>.Instance != null && Singleton<ZoneManager>.Instance.MapCreated)
                {
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    updates[entry.BlockPos] = entry.PreviousBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(updates);
                }
                else
                {
                    // ZoneManager not ready - defer rollback to next Update tick.
                    this.pendingRollbacks.Add(entry);
                }
            }
            if (entry != null && entry.PreviewObject != null)
                UnityEngine.Object.Destroy(entry.PreviewObject);
        }
    }
}

