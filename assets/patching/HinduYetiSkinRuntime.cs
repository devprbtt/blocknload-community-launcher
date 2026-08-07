namespace BnlCommunityFixes
{
    public static class HinduYetiSkinRuntime
    {
        private static UnityEngine.AssetBundle bundle;
        private static readonly string[] TextureNames =
        {
            "abe_first", "abe_arms_first", "abeparticle_ice",
            "abeparticle_snowball", "player_abe_snow_thrower",
            "player_abe_snowball"
        };
        private static readonly UnityEngine.Texture2D[] Textures =
            new UnityEngine.Texture2D[6];
        private static readonly System.Collections.Generic.Dictionary<int, bool>
            PreparedPrefabs = new System.Collections.Generic.Dictionary<int, bool>();

        public static void ApplyFpsPrefab(UnityEngine.GameObject prefab)
        {
            if (prefab == null || !ContainsAbeS1(prefab))
                return;
            int id = prefab.GetInstanceID();
            if (PreparedPrefabs.ContainsKey(id))
                return;
            PreparedPrefabs[id] = true;
            int changed = ApplyToRenderers(prefab, true);
            if (changed > 0)
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Hindu Yeti S1 prefab prepared with " +
                    changed + " replacement material(s) on " + prefab.name + ".");
        }

        public static void Apply(UnityEngine.GameObject root)
        {
            if (root == null || !ContainsAbeS1(root) ||
                root.GetComponent<HinduYetiSkinApplied>() != null)
                return;
            int changed = ApplyToRenderers(root, false);
            if (changed > 0)
            {
                root.AddComponent<HinduYetiSkinApplied>();
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Hindu Yeti S1 skin replaced " +
                    changed + " material texture(s) on " + root.name + ".");
            }
        }

        private static bool ContainsAbeS1(UnityEngine.GameObject root)
        {
            UnityEngine.Transform[] transforms =
                root.GetComponentsInChildren<UnityEngine.Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string name = transforms[i].name.ToLowerInvariant();
                if (name.Contains("abes1") || name.Contains("abeplayers1") ||
                    name.Contains("abe_player_s1"))
                    return true;
            }
            return false;
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

            if (name == "abe_first" || name == "abe_regular" ||
                name == "abe_american")
                return 0;
            if (name == "abe_arms_first" || name == "abe_arms_regular" ||
                name == "abe_arms_american")
                return 1;
            if (name == "abeparticle_ice")
                return 2;
            if (name == "abeparticle_snowball")
                return 3;
            if (name == "player_abe_snow_thrower" ||
                name == "unit_abe_snow_thrower" || name == "abe_snow_thrower")
                return 4;
            if (name == "player_abe_snowball" ||
                name == "unit_abe_snowball" || name == "abe_snowball")
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
                    "CommunityFixes/hindu-yeti-skin-windows.bundle");
                bundle = UnityEngine.AssetBundle.CreateFromFile(path);
                if (bundle == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[BNL Community Fixes] Hindu Yeti skin bundle could not be loaded: " +
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

    public sealed class HinduYetiSkinApplied : UnityEngine.MonoBehaviour
    {
    }
}
