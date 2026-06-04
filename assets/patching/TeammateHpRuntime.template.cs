namespace BnlCommunityFixes
{
    public static class TeammateHpRuntime
    {
        private static readonly HashSet<int> HiddenBackgroundGuids = new HashSet<int>();

        static TeammateHpRuntime()
        {
            RuntimeFeatureState.ConfigureTeammateHp(true, $(Format-BoolLiteral $TeammateHpEnabledOrBackgroundHide));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void UpdateTeammateHpText(GuiTeammate gui)
        {
            if (gui == null) return;
            if (!RuntimeFeatureState.TeammateHpEnabled) return;
            if ($(Format-BoolLiteral $TeammateHideNameBackground))
                TryHideNameBackground(gui);
            if (!$(Format-BoolLiteral $TeammateHpEnabled)) return;
            try
            {
                Unit unit = Singleton<UnitsRegistry>.Instance.GetByPlayerId(gui.PlayerId);
                if (unit == null || unit.IsDeath) return;
                float health = unit.Health;
                float maxHealth = unit.MaxHealth;
                if (maxHealth <= 0f) return;
                int pct = Mathf.RoundToInt((health / maxHealth) * 100f);
                string hpText = pct + "%";
                if (gui.PlayerName != null)
                    gui.PlayerName.text = Singleton<ZonePlayersCache>.Instance.GetPlayerName(gui.PlayerId) + " " + hpText;
                if (gui.RespawnTime != null)
                    gui.RespawnTime.text = hpText;
            }
            catch { }
        }

        private static void TryHideNameBackground(GuiTeammate gui)
        {
            try
            {
                int guiId = gui.GetInstanceID();
                if (HiddenBackgroundGuids.Contains(guiId))
                    return;

                if (gui.PlayerName == null)
                    return;

                RectTransform nameRect = gui.PlayerName.rectTransform;
                if (nameRect == null)
                    return;

                List<Image> candidates = new List<Image>();
                CollectAncestorSiblingImages(nameRect, gui.transform, candidates);
                if (candidates.Count == 0)
                    candidates.AddRange(gui.GetComponentsInChildren<Image>(true));

                Vector3 targetCenter = GetWorldCenter(nameRect);
                Image best = null;
                float bestScore = float.MaxValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    Image image = candidates[i];
                    if (image == null || !image.enabled)
                        continue;

                    if (ReferenceEquals(image.gameObject, gui.PlayerName.gameObject))
                        continue;
                    if (ReferenceEquals(image, gui.Portrait) || ReferenceEquals(image, gui.DeadPortrait) ||
                        ReferenceEquals(image, gui.Health) || ReferenceEquals(image, gui.Shield))
                        continue;

                    Color color = image.color;
                    if (color.a < 0.15f)
                        continue;
                    if (color.r > 0.25f || color.g > 0.25f || color.b > 0.25f)
                        continue;

                    RectTransform rect = image.rectTransform;
                    if (rect == null)
                        continue;

                    float width = Math.Abs(rect.rect.width * rect.lossyScale.x);
                    float height = Math.Abs(rect.rect.height * rect.lossyScale.y);
                    if (width < 24f || height < 8f)
                        continue;
                    if (width < height * 1.8f)
                        continue;

                    Vector3 center = GetWorldCenter(rect);
                    float dy = Math.Abs(center.y - targetCenter.y);
                    if (dy > Math.Max(18f, height))
                        continue;

                    float dx = Math.Abs(center.x - targetCenter.x);
                    if (dx > Math.Max(80f, width * 0.75f))
                        continue;

                    float score = dx + dy;
                    if (RectContainsPoint(rect, targetCenter))
                        score -= 30f;
                    if (rect.parent == nameRect.parent)
                        score -= 20f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = image;
                    }
                }

                if (best != null)
                {
                    best.enabled = false;
                    HiddenBackgroundGuids.Add(guiId);
                }
            }
            catch { }
        }

        private static Vector3 GetWorldCenter(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private static bool RectContainsPoint(RectTransform rect, Vector3 worldPoint)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return worldPoint.x >= corners[0].x && worldPoint.x <= corners[2].x &&
                   worldPoint.y >= corners[0].y && worldPoint.y <= corners[2].y;
        }

        private static void CollectAncestorSiblingImages(RectTransform start, Transform stopAt, List<Image> results)
        {
            if (start == null || results == null)
                return;

            Transform current = start.parent;
            while (current != null)
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (child == null)
                        continue;

                    Image image = child.GetComponent<Image>();
                    if (image != null && !results.Contains(image))
                        results.Add(image);
                }

                if (ReferenceEquals(current, stopAt))
                    break;
                current = current.parent;
            }
        }
    }
}
