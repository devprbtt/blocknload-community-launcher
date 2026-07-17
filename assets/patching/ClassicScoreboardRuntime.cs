namespace BnlCommunityFixes
{
    public static class ClassicScoreboardRuntime
    {
        private const string ConfigFileName = "experimental-classic-scoreboard-config.json";
        private const string PanelTextureFileName = "classic_scoreboard_panel_bg.png";
        private const string RowTextureFileName = "classic_scoreboard_row_bg.png";
        private const string TeamRingTextureFileName = "classic_scoreboard_team_ring.png";
        private const string BarBackgroundTextureFileName = "classic_scoreboard_bar_bg.png";
        private const string BarFillTextureFileName = "classic_scoreboard_bar_fill.png";

        private static bool configLoaded;
        private static bool enabled;
        private static float nextConfigRefreshTime;
        private static bool texturesLoaded;
        private static UnityEngine.Sprite panelSprite;
        private static UnityEngine.Sprite rowSprite;
        private static UnityEngine.Sprite teamRingSprite;
        private static UnityEngine.Sprite barBackgroundSprite;
        private static UnityEngine.Sprite barFillSprite;
        private static bool loggedMissingTextures;

        public static void ApplyToGuiScores(GuiScores gui)
        {
            if (gui == null || gui.Content == null)
            {
                return;
            }

            if (!IsEnabled())
            {
                return;
            }

            EnsureTexturesLoaded();

            try
            {
                ApplyContainer(gui);
                ApplyRows(gui);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL] ClassicScoreboard: failed to apply scoreboard skin: " + ex.Message);
            }
        }

        private static bool IsEnabled()
        {
            if (!configLoaded || UnityEngine.Time.realtimeSinceStartup >= nextConfigRefreshTime)
            {
                configLoaded = true;
                nextConfigRefreshTime = UnityEngine.Time.realtimeSinceStartup + 2f;
                enabled = ReadConfigEnabled();
            }

            return enabled;
        }

        private static bool ReadConfigEnabled()
        {
            try
            {
                string path = System.IO.Path.Combine(GetPatchingFolder(), ConfigFileName);
                if (!System.IO.File.Exists(path))
                {
                    return false;
                }

                string text = System.IO.File.ReadAllText(path);
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                string needle = "\"enabled\"";
                int index = text.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                int colon = text.IndexOf(':', index + needle.Length);
                if (colon < 0)
                {
                    return false;
                }

                string remainder = text.Substring(colon + 1).TrimStart();
                return remainder.StartsWith("true", System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyContainer(GuiScores gui)
        {
            UnityEngine.RectTransform contentRect = gui.Content.GetComponent<UnityEngine.RectTransform>();
            if (contentRect == null)
            {
                return;
            }

            UnityEngine.UI.Image background = EnsureImage(gui.Content.transform, "BNL_ClassicScoreboardBackground", panelSprite);
            if (background != null)
            {
                background.color = new UnityEngine.Color(1f, 1f, 1f, 0.9f);
                SetFullStretch(background.rectTransform, -22f, -18f, 22f, 18f);
                background.transform.SetAsFirstSibling();
            }

            if (gui.IgnoreTeammates != null)
            {
                gui.IgnoreTeammates.gameObject.SetActive(false);
            }

            if (gui.IgnoreOpponents != null)
            {
                gui.IgnoreOpponents.gameObject.SetActive(false);
            }

            StyleHeaderTexts(gui.Content.transform);
        }

        private static void StyleHeaderTexts(UnityEngine.Transform root)
        {
            UnityEngine.UI.Text[] texts = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                UnityEngine.UI.Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                string normalized = text.text.Trim().ToUpperInvariant();
                if (normalized == "K" || normalized == "KILLS")
                {
                    ApplyHeaderStyle(text, "K");
                }
                else if (normalized == "D" || normalized == "DEATHS")
                {
                    ApplyHeaderStyle(text, "D");
                }
                else if (normalized == "A" || normalized == "ASSISTS")
                {
                    ApplyHeaderStyle(text, "A");
                }
                else if (normalized == "TEAM 1" || normalized == "TEAM 2" || normalized == "MY TEAM" || normalized == "ENEMY TEAM")
                {
                    text.color = new UnityEngine.Color(0.96f, 0.84f, 0.38f, 1f);
                    text.fontStyle = UnityEngine.FontStyle.Bold;
                }
            }
        }

        private static void ApplyHeaderStyle(UnityEngine.UI.Text text, string replacement)
        {
            text.text = replacement;
            text.color = new UnityEngine.Color(0.96f, 0.84f, 0.38f, 1f);
            text.fontStyle = UnityEngine.FontStyle.Bold;
            text.alignment = UnityEngine.TextAnchor.MiddleCenter;
        }

        private static void ApplyRows(GuiScores gui)
        {
            GuiPlayerScore[] rows = gui.Content.GetComponentsInChildren<GuiPlayerScore>(true);
            for (int i = 0; i < rows.Length; i++)
            {
                ApplyRow(rows[i]);
            }
        }

        private static void ApplyRow(GuiPlayerScore row)
        {
            if (row == null)
            {
                return;
            }

            UnityEngine.UI.Image rowBackground = EnsureImage(row.transform, "BNL_ClassicRowBackground", rowSprite);
            if (rowBackground != null)
            {
                rowBackground.color = new UnityEngine.Color(1f, 1f, 1f, 0.95f);
                SetFullStretch(rowBackground.rectTransform, -2f, -1f, 2f, 1f);
                rowBackground.transform.SetAsFirstSibling();
            }

            bool isMe = row.IsMeBackground != null && row.IsMeBackground.gameObject.activeSelf;
            if (row.IsMeBackground != null)
            {
                row.IsMeBackground.sprite = barFillSprite != null ? barFillSprite : row.IsMeBackground.sprite;
                row.IsMeBackground.type = UnityEngine.UI.Image.Type.Sliced;
                row.IsMeBackground.color = isMe
                    ? new UnityEngine.Color(0.93f, 0.76f, 0.21f, 0.6f)
                    : new UnityEngine.Color(1f, 1f, 1f, 0f);
            }

            if (row.State != null)
            {
                row.State.sprite = barBackgroundSprite != null ? barBackgroundSprite : row.State.sprite;
                row.State.type = UnityEngine.UI.Image.Type.Sliced;
            }

            if (row.TeamColor != null)
            {
                row.TeamColor.sprite = teamRingSprite != null ? teamRingSprite : row.TeamColor.sprite;
                row.TeamColor.type = UnityEngine.UI.Image.Type.Simple;
            }

            StyleText(row.Nickname, new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f), UnityEngine.TextAnchor.MiddleLeft, 14);
            StyleText(row.Kills, new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f), UnityEngine.TextAnchor.MiddleCenter, 14);
            StyleText(row.Deaths, new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f), UnityEngine.TextAnchor.MiddleCenter, 14);
            StyleText(row.Assists, new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f), UnityEngine.TextAnchor.MiddleCenter, 14);
            StyleText(row.RespawnTime, new UnityEngine.Color(1f, 0.4f, 0.35f, 1f), UnityEngine.TextAnchor.MiddleCenter, 13);

            if (row.SquadNumContent != null)
            {
                row.SquadNumContent.SetActive(false);
            }

            if (row.LookingForFriends != null)
            {
                row.LookingForFriends.SetActive(false);
            }

            if (row.Invite != null)
            {
                row.Invite.gameObject.SetActive(false);
            }

            if (row.SteamProfile != null)
            {
                row.SteamProfile.gameObject.SetActive(false);
            }

            if (row.Ignore != null)
            {
                row.Ignore.gameObject.SetActive(false);
            }

            HideChildrenByPartialName(row.transform, "ping");
            HideChildrenByPartialName(row.transform, "latency");
        }

        private static void StyleText(
            UnityEngine.UI.Text text,
            UnityEngine.Color color,
            UnityEngine.TextAnchor alignment,
            int fontSize)
        {
            if (text == null)
            {
                return;
            }

            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.fontSize = fontSize;
            text.supportRichText = false;
        }

        private static void SetFullStretch(UnityEngine.RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new UnityEngine.Vector2(0f, 0f);
            rect.anchorMax = new UnityEngine.Vector2(1f, 1f);
            rect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
            rect.offsetMin = new UnityEngine.Vector2(left, bottom);
            rect.offsetMax = new UnityEngine.Vector2(right, top);
        }

        private static UnityEngine.UI.Image EnsureImage(UnityEngine.Transform parent, string name, UnityEngine.Sprite sprite)
        {
            if (parent == null)
            {
                return null;
            }

            UnityEngine.Transform existing = parent.Find(name);
            UnityEngine.GameObject go = existing != null ? existing.gameObject : new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }

            return image;
        }

        private static void HideChildrenByPartialName(UnityEngine.Transform parent, string partialName)
        {
            if (parent == null || string.IsNullOrEmpty(partialName))
            {
                return;
            }

            string needle = partialName.ToLowerInvariant();
            UnityEngine.Transform[] children = parent.GetComponentsInChildren<UnityEngine.Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                UnityEngine.Transform child = children[i];
                if (child == null || child == parent)
                {
                    continue;
                }

                string name = child.name;
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(needle))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void EnsureTexturesLoaded()
        {
            if (texturesLoaded)
            {
                return;
            }

            texturesLoaded = true;
            panelSprite = LoadSprite(PanelTextureFileName, 8f);
            rowSprite = LoadSprite(RowTextureFileName, 8f);
            teamRingSprite = LoadSprite(TeamRingTextureFileName, 1f);
            barBackgroundSprite = LoadSprite(BarBackgroundTextureFileName, 4f);
            barFillSprite = LoadSprite(BarFillTextureFileName, 4f);

            if ((panelSprite == null || rowSprite == null) && !loggedMissingTextures)
            {
                loggedMissingTextures = true;
                UnityEngine.Debug.LogWarning("[BNL] ClassicScoreboard: missing one or more legacy textures in CustomTextures.");
            }
        }

        private static UnityEngine.Sprite LoadSprite(string fileName, float border)
        {
            try
            {
                string path = System.IO.Path.Combine(GetCustomTexturesFolder(), fileName);
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }

                byte[] bytes = System.IO.File.ReadAllBytes(path);
                UnityEngine.Texture2D texture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    return null;
                }

                texture.name = fileName;
                UnityEngine.Vector4 borders = new UnityEngine.Vector4(border, border, border, border);
                UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(
                    texture,
                    new UnityEngine.Rect(0f, 0f, texture.width, texture.height),
                    new UnityEngine.Vector2(0.5f, 0.5f),
                    100f,
                    0u,
                    UnityEngine.SpriteMeshType.FullRect,
                    borders);
                sprite.name = fileName;
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        private static string GetCustomTexturesFolder()
        {
            return System.IO.Path.Combine(UnityEngine.Application.dataPath, "CustomTextures");
        }

        private static string GetPatchingFolder()
        {
            return System.IO.Path.Combine(
                System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "BNL-CommunityFixes"),
                System.IO.Path.Combine("app", "patching"));
        }
    }
}
