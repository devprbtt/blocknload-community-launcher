namespace BnlCommunityFixes
{
    public static class VanderBlueSkinRuntime
    {
        private static UnityEngine.AssetBundle bundle;
        private static UnityEngine.Texture2D bodyTexture;
        private static UnityEngine.Texture2D armsTexture;
        private static readonly System.Collections.Generic.Dictionary<int, bool>
            PreparedFpsPrefabs =
                new System.Collections.Generic.Dictionary<int, bool>();

        public static void ApplyFpsPrefab(UnityEngine.GameObject prefab)
        {
            if (prefab == null)
                return;
            int id = prefab.GetInstanceID();
            if (PreparedFpsPrefabs.ContainsKey(id))
                return;
            PreparedFpsPrefabs[id] = true;
            int changed = ApplyToRenderers(prefab, true);
            if (changed > 0)
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Vander Blue FPS prefab prepared with " +
                    changed + " replacement material(s) on " + prefab.name + ".");
        }

        public static void Apply(UnityEngine.GameObject root)
        {
            if (root == null || root.GetComponent<VanderBlueSkinApplied>() != null)
                return;
            int changed = ApplyToRenderers(root, false);
            if (changed > 0)
            {
                root.AddComponent<VanderBlueSkinApplied>();
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Vander Blue skin replaced " + changed +
                    " material texture(s) on " + root.name + ".");
            }
        }

        private static int ApplyToRenderers(
            UnityEngine.GameObject root, bool sharedPrefab)
        {
            UnityEngine.Renderer[] renderers =
                root.GetComponentsInChildren<UnityEngine.Renderer>(true);
            int changed = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                UnityEngine.Material[] shared = renderers[i].sharedMaterials;
                bool hasMatch = false;
                for (int j = 0; j < shared.Length; j++)
                {
                    if (GetReplacementIndex(shared[j]) >= 0)
                    {
                        hasMatch = true;
                        break;
                    }
                }
                if (!hasMatch)
                    continue;

                UnityEngine.Material[] materials = sharedPrefab
                    ? shared : renderers[i].materials;
                bool assignShared = false;
                for (int j = 0; j < materials.Length; j++)
                {
                    int index = GetReplacementIndex(materials[j]);
                    if (index < 0)
                        continue;
                    UnityEngine.Texture2D replacement = GetTexture(index);
                    if (replacement == null)
                        continue;
                    if (sharedPrefab)
                    {
                        materials[j] = new UnityEngine.Material(materials[j]);
                        assignShared = true;
                    }
                    materials[j].mainTexture = replacement;
                    changed++;
                }
                if (assignShared)
                    renderers[i].sharedMaterials = materials;
            }
            return changed;
        }

        private static int GetReplacementIndex(UnityEngine.Material material)
        {
            if (material == null || material.mainTexture == null)
                return -1;
            string name = material.mainTexture.name.ToLowerInvariant();
            int suffix = name.IndexOf(" (instance)");
            if (suffix >= 0)
                name = name.Substring(0, suffix);
            if (name == "magnus_blue")
                return 0;
            if (name == "magnus_arms_blue")
                return 1;
            return -1;
        }

        private static UnityEngine.Texture2D GetTexture(int index)
        {
            UnityEngine.Texture2D cached = index == 0 ? bodyTexture : armsTexture;
            if (cached != null)
                return cached;
            if (bundle == null)
            {
                string path = System.IO.Path.Combine(
                    UnityEngine.Application.dataPath,
                    "CommunityFixes/vander-blue-skin-windows.bundle");
                bundle = UnityEngine.AssetBundle.CreateFromFile(path);
                if (bundle == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[BNL Community Fixes] Vander Blue skin bundle could not be loaded: " +
                        path);
                    return null;
                }
            }
            string name = index == 0 ? "magnus_blue" : "magnus_arms_blue";
            cached = bundle.LoadAsset(name, typeof(UnityEngine.Texture2D))
                as UnityEngine.Texture2D;
            if (index == 0)
                bodyTexture = cached;
            else
                armsTexture = cached;
            return cached;
        }
    }

    public sealed class VanderBlueSkinApplied : UnityEngine.MonoBehaviour
    {
    }
}
