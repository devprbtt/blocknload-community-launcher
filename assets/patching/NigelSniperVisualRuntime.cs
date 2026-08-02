namespace BnlCommunityFixes
{
    public static class NigelSniperVisualRuntime
    {
        private static UnityEngine.AssetBundle modelBundle;
        private static UnityEngine.GameObject replacementModelPrefab;

        public static void Apply(UnityEngine.GameObject gearObject)
        {
            if (gearObject == null)
                return;
            string objectName = gearObject.name;
            bool isBaseRifle =
                objectName == "SniperRifleOneBarrel" ||
                objectName == "SniperRifleOneBarrel(Clone)" ||
                objectName == "SniperRifleOneBarrel (Clone)";
            if (!isBaseRifle)
                return;
            NigelImportedRifleApplicator applicator =
                gearObject.GetComponent<NigelImportedRifleApplicator>();
            if (applicator == null)
                gearObject.AddComponent<NigelImportedRifleApplicator>();
        }

        public static UnityEngine.GameObject GetReplacementModelPrefab()
        {
            if (replacementModelPrefab != null)
                return replacementModelPrefab;
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "CommunityFixes/nigel-replacement-model-windows.bundle");
            modelBundle = UnityEngine.AssetBundle.CreateFromFile(path);
            if (modelBundle == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[BNL Community Fixes] Nigel replacement model bundle could not be loaded: " +
                    path);
                return null;
            }
            replacementModelPrefab =
                modelBundle.mainAsset as UnityEngine.GameObject;
            return replacementModelPrefab;
        }

        public static UnityEngine.Transform FindDeepChild(
            UnityEngine.Transform parent, string childName)
        {
            if (parent.name == childName)
                return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                UnityEngine.Transform found =
                    FindDeepChild(parent.GetChild(i), childName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }

    public sealed class NigelImportedRifleApplicator :
        UnityEngine.MonoBehaviour
    {
        private System.Collections.IEnumerator Start()
        {
            yield return null;

            UnityEngine.SkinnedMeshRenderer originalRenderer = null;
            UnityEngine.SkinnedMeshRenderer[] skinnedRenderers =
                gameObject.GetComponentsInChildren<
                    UnityEngine.SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                UnityEngine.SkinnedMeshRenderer renderer =
                    skinnedRenderers[i];
                if (renderer == null)
                    continue;
                string rendererName = renderer.name.ToLowerInvariant();
                if (rendererName.Contains("sniperrifleonebarrel"))
                {
                    originalRenderer = renderer;
                    break;
                }
            }
            if (originalRenderer == null && skinnedRenderers.Length > 0)
                originalRenderer = skinnedRenderers[0];

            UnityEngine.GameObject prefab =
                NigelSniperVisualRuntime.GetReplacementModelPrefab();
            if (prefab == null)
                yield break;
            UnityEngine.Transform rifleRoot =
                NigelSniperVisualRuntime.FindDeepChild(
                    gameObject.transform, "SniperRifle_Root");
            if (rifleRoot == null)
                rifleRoot = gameObject.transform;

            UnityEngine.GameObject model =
                UnityEngine.Object.Instantiate(prefab)
                    as UnityEngine.GameObject;
            model.name = "BNL_NigelImportedNoHammers";
            model.transform.parent = rifleRoot;
            model.transform.localPosition =
                new UnityEngine.Vector3(0f, 0.25f, -0.015f);
            model.transform.localRotation =
                UnityEngine.Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale =
                new UnityEngine.Vector3(0.5f, 0.5f, 0.5f);

            int weaponLayer = originalRenderer == null
                ? gameObject.layer
                : originalRenderer.gameObject.layer;
            SetLayerRecursive(model.transform, weaponLayer);
            if (originalRenderer != null)
                originalRenderer.enabled = false;

            UnityEngine.Debug.Log(
                "[BNL Community Fixes] Nigel imported no-hammers rifle installed.");
        }

        private static void SetLayerRecursive(
            UnityEngine.Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }
    }
}
