namespace BnlCommunityFixes
{
    public static class MotionBlurRuntime
    {
        private static bool reported;
        private static bool cameraEffectAttempted;

        public static void EnsureCameraEffect(CameraFov cameraFov)
        {
            if (cameraEffectAttempted || cameraFov == null) return;
            cameraEffectAttempted = true;

            UnityEngine.Camera camera = cameraFov.GetComponent<UnityEngine.Camera>();
            if (camera == null)
            {
                UnityEngine.Debug.LogWarning("[BNL Community Fixes] Motion blur: CameraFov has no Camera.");
                return;
            }

            CommunityDirectionalMotionBlur effect =
                camera.gameObject.GetComponent<CommunityDirectionalMotionBlur>();
            if (effect == null)
                effect = camera.gameObject.AddComponent<CommunityDirectionalMotionBlur>();
            effect.enabled = true;
        }

        public static void EnableAssignedEffect(AssignCameraEffects assigner)
        {
            if (assigner == null || assigner.Assigned == null) return;

            int enabledCameraEffects = 0;
            for (int i = 0; i < assigner.Assigned.Count; i++)
            {
                AssignCameraEffects.SourceDest pair = assigner.Assigned[i];
                UnityEngine.MonoBehaviour assigned = pair == null ? null : pair.Dest;
                if (assigned == null) continue;

                System.Type assignedType = assigned.GetType();
                if (assignedType.Name == "CameraMotionBlur")
                {
                    SetField(assigned, assignedType, "filterType", "Reconstruction");
                    SetField(assigned, assignedType, "movementScale", 1.25f);
                    SetField(assigned, assignedType, "rotationScale", 2.0f);
                    SetField(assigned, assignedType, "velocityScale", 1.0f);
                    SetField(assigned, assignedType, "maxVelocity", 12.0f);
                    SetField(assigned, assignedType, "minVelocity", 0.05f);
                    assigned.enabled = true;
                    enabledCameraEffects++;
                }

                PerObjectMotionBlurEffect effect = pair == null ? null : pair.Dest as PerObjectMotionBlurEffect;
                if (effect == null) continue;

                effect.mode = PerObjectMotionBlurEffect.Output.Full;
                effect.enabled = true;
            }

            if (!reported)
            {
                reported = true;
                UnityEngine.Debug.Log("[BNL Community Fixes] Motion blur prototype enabled "
                    + enabledCameraEffects + " serialized CameraMotionBlur effect(s).");
            }
        }

        public static void EnableUnitBlur(UnitMotionBlur unitMotionBlur)
        {
            if (unitMotionBlur == null) return;

            ObjectBlur blur = unitMotionBlur.gameObject.GetComponentInChildren<ObjectBlur>();
            if (blur != null && !blur.enabled)
                blur.enabled = true;
        }

        private static void SetField(object target, System.Type targetType, string name, object value)
        {
            System.Reflection.FieldInfo field = targetType.GetField(name);
            if (field == null) return;

            if (field.FieldType.IsEnum && value is string)
            {
                field.SetValue(target, System.Enum.Parse(field.FieldType, (string)value));
                return;
            }

            field.SetValue(target, value);
        }
    }

    public sealed class CommunityDirectionalMotionBlur : UnityEngine.MonoBehaviour
    {
        private UnityEngine.AssetBundle bundle;
        private UnityEngine.Material material;
        private UnityEngine.Vector3 previousEuler;
        private UnityEngine.Vector2 smoothedBlur;
        private bool hasPreviousRotation;

        private void OnEnable()
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "CommunityFixes/motion-blur-windows.bundle");
            bundle = UnityEngine.AssetBundle.CreateFromFile(path);
            if (bundle == null)
            {
                UnityEngine.Debug.LogWarning("[BNL Community Fixes] Motion blur bundle could not be loaded: " + path);
                enabled = false;
                return;
            }

            UnityEngine.Shader shader = bundle.mainAsset as UnityEngine.Shader;
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[BNL Community Fixes] Motion blur shader is missing from its bundle.");
                enabled = false;
                return;
            }

            material = new UnityEngine.Material(shader);
            material.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            UnityEngine.Debug.Log("[BNL Community Fixes] Directional motion blur loaded: " + shader.name);
        }

        private void OnDisable()
        {
            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }
            if (bundle != null)
            {
                bundle.Unload(false);
                bundle = null;
            }
            hasPreviousRotation = false;
            smoothedBlur = UnityEngine.Vector2.zero;
        }

        private void OnRenderImage(UnityEngine.RenderTexture source, UnityEngine.RenderTexture destination)
        {
            if (material == null)
            {
                UnityEngine.Graphics.Blit(source, destination);
                return;
            }

            UnityEngine.Vector3 currentEuler = transform.eulerAngles;
            UnityEngine.Vector2 targetBlur = UnityEngine.Vector2.zero;
            if (hasPreviousRotation)
            {
                float yaw = UnityEngine.Mathf.DeltaAngle(previousEuler.y, currentEuler.y);
                float pitch = UnityEngine.Mathf.DeltaAngle(previousEuler.x, currentEuler.x);
                float maxBlur = 0.028f * UnityEngine.Mathf.Clamp(
                    MotionBlurGeneratedConfig.Strength, 0.1f, 2f);
                targetBlur.x = UnityEngine.Mathf.Clamp(
                    -yaw * 0.0024f * MotionBlurGeneratedConfig.Strength, -maxBlur, maxBlur);
                targetBlur.y = UnityEngine.Mathf.Clamp(
                    pitch * 0.0024f * MotionBlurGeneratedConfig.Strength, -maxBlur, maxBlur);
            }
            previousEuler = currentEuler;
            hasPreviousRotation = true;

            float response = UnityEngine.Mathf.Clamp01(UnityEngine.Time.deltaTime * 40f);
            smoothedBlur = UnityEngine.Vector2.Lerp(smoothedBlur, targetBlur, response);
            if (smoothedBlur.sqrMagnitude < 0.00000025f)
            {
                UnityEngine.Graphics.Blit(source, destination);
                return;
            }

            material.SetVector("_BlurVector",
                new UnityEngine.Vector4(smoothedBlur.x, smoothedBlur.y, 0f, 0f));
            material.SetFloat("_CenterFocus",
                UnityEngine.Mathf.Clamp01(MotionBlurGeneratedConfig.CenterFocus));
            UnityEngine.Graphics.Blit(source, destination, material,
                MotionBlurGeneratedConfig.QualityPass);
        }
    }
}
