namespace BnlCommunityFixes
{
    public static class NinjaTurtleSkinRuntime
    {
        private static UnityEngine.AssetBundle bundle;
        private static readonly string[] TextureNames =
        {
            "ninja_regular",
            "ninja_arms_regular",
            "unit_katana",
            "player_katana"
        };
        private static readonly UnityEngine.Texture2D[] Textures =
            new UnityEngine.Texture2D[4];
        private static readonly System.Collections.Generic.Dictionary<int, bool>
            PreparedFpsPrefabs =
                new System.Collections.Generic.Dictionary<int, bool>();

        public static void ApplyFpsPrefab(UnityEngine.GameObject prefab)
        {
            if (prefab == null)
                return;
            int instanceId = prefab.GetInstanceID();
            if (PreparedFpsPrefabs.ContainsKey(instanceId))
                return;
            PreparedFpsPrefabs[instanceId] = true;

            UnityEngine.Renderer[] renderers =
                prefab.GetComponentsInChildren<UnityEngine.Renderer>(true);
            int changed = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                UnityEngine.Material[] materials = renderers[i].sharedMaterials;
                bool rendererChanged = false;
                for (int j = 0; j < materials.Length; j++)
                {
                    UnityEngine.Material material = materials[j];
                    if (material == null || material.mainTexture == null)
                        continue;
                    int textureIndex = FindTextureIndex(material.mainTexture.name);
                    if (textureIndex < 0)
                        continue;
                    UnityEngine.Texture2D replacement = GetTexture(textureIndex);
                    if (replacement == null)
                        continue;
                    UnityEngine.Material clone =
                        new UnityEngine.Material(material);
                    clone.mainTexture = replacement;
                    materials[j] = clone;
                    rendererChanged = true;
                    changed++;
                }
                if (rendererChanged)
                    renderers[i].sharedMaterials = materials;
            }
            if (changed > 0)
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Ninja Turtle FPS prefab prepared with " +
                    changed + " replacement material(s) on " + prefab.name + ".");
        }

        public static void Apply(UnityEngine.GameObject root)
        {
            if (root == null || root.GetComponent<NinjaTurtleSkinApplied>() != null)
                return;

            UnityEngine.Renderer[] renderers =
                root.GetComponentsInChildren<UnityEngine.Renderer>(true);
            int changed = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                UnityEngine.Material[] sharedMaterials =
                    renderers[i].sharedMaterials;
                bool hasMatch = false;
                for (int j = 0; j < sharedMaterials.Length; j++)
                {
                    UnityEngine.Material material = sharedMaterials[j];
                    if (material == null || material.mainTexture == null)
                        continue;
                    if (FindTextureIndex(material.mainTexture.name) >= 0)
                    {
                        hasMatch = true;
                        break;
                    }
                }
                if (!hasMatch)
                    continue;

                UnityEngine.Material[] materials = renderers[i].materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    UnityEngine.Material material = materials[j];
                    if (material == null || material.mainTexture == null)
                        continue;
                    int textureIndex = FindTextureIndex(material.mainTexture.name);
                    if (textureIndex < 0)
                        continue;
                    UnityEngine.Texture2D replacement = GetTexture(textureIndex);
                    if (replacement == null)
                        continue;
                    material.mainTexture = replacement;
                    changed++;
                }
            }

            if (changed > 0)
            {
                root.AddComponent<NinjaTurtleSkinApplied>();
                UnityEngine.Debug.Log(
                    "[BNL Community Fixes] Ninja Turtle skin replaced " +
                    changed + " material texture(s) on " + root.name + ".");
            }
        }

        private static int FindTextureIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            string normalized = name.ToLowerInvariant();
            int instanceSuffix = normalized.IndexOf(" (instance)");
            if (instanceSuffix >= 0)
                normalized = normalized.Substring(0, instanceSuffix);
            // The default Ninja S1 body and FPS prefab use the historical
            // "american" material keys even though the matching exported
            // Turtle replacements are named "regular".
            if (normalized == "ninja_american")
                return 0;
            if (normalized == "ninja_arms_american")
                return 1;
            for (int i = 0; i < TextureNames.Length; i++)
            {
                if (normalized == TextureNames[i])
                    return i;
            }
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
                    "CommunityFixes/ninja-turtle-skin-windows.bundle");
                bundle = UnityEngine.AssetBundle.CreateFromFile(path);
                if (bundle == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[BNL Community Fixes] Ninja Turtle skin bundle could not be loaded: " +
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

    public sealed class NinjaTurtleSkinApplied : UnityEngine.MonoBehaviour
    {
    }
}
