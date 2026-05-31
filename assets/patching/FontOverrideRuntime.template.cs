namespace BnlCommunityFixes
{
    public static class FontOverrideRuntime
    {
        static FontOverrideRuntime()
        {
            RuntimeFeatureState.ConfigureFontOverride(true, $(Format-BoolLiteral $FontOverrideEnabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            FontOverrideBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL FontOverride] initialized");
        }

        public static void EnsureInit() { }

        public static void PatchChatMessage(UiChatMessage msg)
        {
            FontOverrideBootstrapper.PatchChatMessage(msg);
        }
    }
}
