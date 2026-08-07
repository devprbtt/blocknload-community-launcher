namespace BnlCommunityFixes
{
    public static class DarklordSweetScienceSkinRuntime
    {
        private static UnityEngine.AssetBundle bundle;
        private static readonly string[] TextureNames =
        {
            "sweet_science_demon", "sweet_science_arms_demon",
            "sweet_science_demon_blitz", "sweet_science_arms_demon_blitz",
            "sweet_science_demon_graviton",
            "sweet_science_arms_demon_graviton"
        };
        private static readonly UnityEngine.Texture2D[] Textures =
            new UnityEngine.Texture2D[6];
        private static readonly System.Collections.Generic.Dictionary<int, bool>
            PreparedPrefabs = new System.Collections.Generic.Dictionary<int, bool>();

        public static void ApplyFpsPrefab(UnityEngine.GameObject prefab)
        {
            if (prefab == null || !IsDevilSweetScience(prefab))
                return;
            int id = prefab.GetInstanceID();
            if (PreparedPrefabs.ContainsKey(id))
                return;
            PreparedPrefabs[id] = true;
            int changed = ApplyToRenderers(prefab, true);
            if (changed > 0)
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Darklord SS Devil prefab prepared with " +
                    changed + " replacement material(s) on " + prefab.name + ".");
        }

        public static void Apply(UnityEngine.GameObject root)
        {
            if (root == null || !IsDevilSweetScience(root) ||
                root.GetComponent<DarklordSweetScienceSkinApplied>() != null)
                return;
            int changed = ApplyToRenderers(root, false);
            if (changed > 0)
            {
                root.AddComponent<DarklordSweetScienceSkinApplied>();
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Darklord SS Devil skin replaced " +
                    changed + " material texture(s) on " + root.name + ".");
            }
        }

        private static bool IsDevilSweetScience(UnityEngine.GameObject root)
        {
            string name = root.name.ToLowerInvariant()
                .Replace("_", "").Replace(" ", "");
            return name.Contains("sweetsciences6") ||
                name.Contains("sweetscienceplayers6") ||
                name.Contains("playersweetsciences6");
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

            if (name == "sweet_science_demon" || name == "sweet_science" ||
                name == "sweet_science_regular" || name == "sweet_science_american")
                return 0;
            if (name == "sweet_science_arms_demon" ||
                name == "sweet_science_arms" ||
                name == "sweet_science_arms_regular" ||
                name == "sweet_science_arms_american")
                return 1;
            if (name == "sweet_science_demon_blitz" ||
                name == "sweet_science_blitz" ||
                name == "sweet_science_regular_blitz")
                return 2;
            if (name == "sweet_science_arms_demon_blitz" ||
                name == "sweet_science_arms_blitz" ||
                name == "sweet_science_arms_regular_blitz")
                return 3;
            if (name == "sweet_science_demon_graviton" ||
                name == "sweet_science_graviton" ||
                name == "sweet_science_regular_graviton")
                return 4;
            if (name == "sweet_science_arms_demon_graviton" ||
                name == "sweet_science_arms_graviton" ||
                name == "sweet_science_arms_regular_graviton")
                return 5;
            return -1;
        }

        private static UnityEngine.Texture2D GetTexture(int index)
        {
            if (Textures[index] != null)
                return Textures[index];
            if (bundle == null)
            {
                string path = System.IO.Path.Combine(
                    UnityEngine.Application.dataPath,
                    "CommunityFixes/darklord-sweet-science-skin-windows.bundle");
                bundle = UnityEngine.AssetBundle.CreateFromFile(path);
                if (bundle == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[BNL Community Fixes] Darklord SS skin bundle could not be loaded: " +
                        path);
                    return null;
                }
            }
            Textures[index] = bundle.LoadAsset(
                TextureNames[index], typeof(UnityEngine.Texture2D))
                as UnityEngine.Texture2D;
            return Textures[index];
        }
    }

    public sealed class DarklordSweetScienceSkinApplied
        : UnityEngine.MonoBehaviour
    {
    }
}
