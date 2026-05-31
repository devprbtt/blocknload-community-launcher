namespace BnlCommunityFixes
{
    public static class TeammateHpRuntime
    {
        static TeammateHpRuntime()
        {
            RuntimeFeatureState.ConfigureTeammateHp(true, $(Format-BoolLiteral $TeammateHpEnabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void UpdateTeammateHpText(GuiTeammate gui)
        {
            if (gui == null) return;
            if (!RuntimeFeatureState.TeammateHpEnabled) return;
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
    }
}
