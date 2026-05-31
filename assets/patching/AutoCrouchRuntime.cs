namespace BnlCommunityFixes
{
    public static class AutoCrouchRuntime
    {
        private static bool configured;

        static AutoCrouchRuntime()
        {
            EnsureConfigured();
        }

        public static void EnsureConfigured()
        {
            if (configured) return;
            configured = true;
            RuntimeFeatureState.ConfigureAutoCrouchDisable(true, false);
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        // Returns true when auto-crouch should be suppressed (i.e. "ceiling check passes").
        // Only called from PlayerMovementGroundMove.Update to replace the IsPossibleToStay call
        // in the auto-crouch condition. Voluntary crouch/stand logic is unaffected.
        public static bool IsPossibleToStayForAutoCrouch(MovementController controller)
        {
            EnsureConfigured();
            if (RuntimeFeatureState.AutoCrouchDisabled) return true;
            return controller.IsPossibleToStay();
        }
    }
}
