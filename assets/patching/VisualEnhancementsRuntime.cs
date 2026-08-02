namespace BnlCommunityFixes
{
    public static class VisualEnhancementsRuntime
    {
        private static bool attempted;

        public static void EnsureCameraEffect(CameraFov cameraFov)
        {
            if (attempted || cameraFov == null) return;
            attempted = true;
            UnityEngine.Camera camera = cameraFov.GetComponent<UnityEngine.Camera>();
            if (camera == null) return;
            CommunityVisualEnhancements effect =
                camera.gameObject.GetComponent<CommunityVisualEnhancements>();
            if (effect == null)
                effect = camera.gameObject.AddComponent<CommunityVisualEnhancements>();
            effect.enabled = true;
        }
    }

    public sealed class CommunityVisualEnhancements : UnityEngine.MonoBehaviour
    {
        private UnityEngine.AssetBundle bundle;
        private UnityEngine.Material material;

        private void OnEnable()
        {
            string path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "CommunityFixes/visual-enhancements-windows.bundle");
            bundle = UnityEngine.AssetBundle.CreateFromFile(path);
            if (bundle == null)
            {
                UnityEngine.Debug.LogWarning("[BNL Community Fixes] Visual enhancement bundle could not be loaded: " + path);
                enabled = false;
                return;
            }
            UnityEngine.Shader shader = bundle.mainAsset as UnityEngine.Shader;
            if (shader == null)
            {
                enabled = false;
                return;
            }
            material = new UnityEngine.Material(shader);
            material.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            UnityEngine.Debug.Log("[BNL Community Fixes] Visual enhancements loaded: " + shader.name);
        }

        private void OnDisable()
        {
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            material = null;
            if (bundle != null) bundle.Unload(false);
            bundle = null;
        }

        private void OnRenderImage(UnityEngine.RenderTexture source, UnityEngine.RenderTexture destination)
        {
            if (material == null)
            {
                UnityEngine.Graphics.Blit(source, destination);
                return;
            }
            material.SetFloat("_Sharpening", VisualEnhancementsGeneratedConfig.Sharpening);
            material.SetFloat("_Saturation", VisualEnhancementsGeneratedConfig.Saturation);
            material.SetFloat("_Contrast", VisualEnhancementsGeneratedConfig.Contrast);
            material.SetFloat("_Brightness", VisualEnhancementsGeneratedConfig.Brightness);
            material.SetFloat("_Temperature", VisualEnhancementsGeneratedConfig.Temperature);
            UnityEngine.Graphics.Blit(source, destination, material);
        }
    }
}
