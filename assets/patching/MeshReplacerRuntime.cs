using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BnlCommunityFixes
{
    public sealed class MeshReplacerManager : MonoBehaviour
    {
        private const string CustomMeshFolder = "CustomMeshes";

        private static MeshReplacerManager instance;
        private static readonly Dictionary<string, Mesh> customMeshes = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> replacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<TransformFix>> transformFixMap = new Dictionary<string, List<TransformFix>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<GameObject> pendingObjects = new List<GameObject>();
        private static readonly HashSet<int> pendingObjectIds = new HashSet<int>();
        private static bool bootstrapApplied;
        private static bool loadComplete;
        private static bool isProcessingQueue;

        private struct TransformFix
        {
            public string path;
            public bool hasPosition;
            public Vector3 position;
            public bool hasRotation;
            public Quaternion rotation;
            public bool hasScale;
            public Vector3 scale;
        }

        public static void EnsureInstance()
        {
            if (instance != null) return;
            GameObject go = new GameObject("BNL_MeshReplacerManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MeshReplacerManager>();
        }

        public static bool BeginBootstrap()
        {
            if (bootstrapApplied)
            {
                return false;
            }

            bootstrapApplied = true;
            replacementMap.Clear();
            transformFixMap.Clear();
            pendingObjects.Clear();
            pendingObjectIds.Clear();
            EnsureInstance();
            return true;
        }

        public static void RegisterReplacement(string meshName, string fileName)
        {
            replacementMap[meshName] = fileName;
        }

        // path: child path relative to go (e.g. "SniperRifleOneBarrel_Unit/SniperRifle_Root")
        // px/py/pz: local position components, or float.NaN to leave unchanged
        // rx/ry/rz/rw: local rotation quaternion components, or float.NaN to leave unchanged
        // sx/sy/sz: local scale components, or float.NaN to leave unchanged
        public static void RegisterTransformFix(string meshName, string path,
            float px, float py, float pz,
            float rx, float ry, float rz, float rw,
            float sx, float sy, float sz)
        {
            List<TransformFix> list;
            if (!transformFixMap.TryGetValue(meshName, out list))
            {
                list = new List<TransformFix>();
                transformFixMap[meshName] = list;
            }
            var fix = new TransformFix { path = path };
            if (!float.IsNaN(px)) { fix.hasPosition = true; fix.position = new Vector3(px, py, pz); }
            if (!float.IsNaN(rx)) { fix.hasRotation = true; fix.rotation = new Quaternion(rx, ry, rz, rw); }
            if (!float.IsNaN(sx)) { fix.hasScale = true; fix.scale = new Vector3(sx, sy, sz); }
            list.Add(fix);
        }

        public static bool IsLoadComplete
        {
            get { return loadComplete; }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            StartCoroutine(LoadCustomMeshes());
        }

        private IEnumerator LoadCustomMeshes()
        {
            string dataPath = Application.dataPath;
            string customPath = Path.Combine(dataPath, CustomMeshFolder);

            if (!Directory.Exists(customPath))
            {
                Debug.Log("[MeshReplacer] CustomMeshes folder not found: " + customPath);
                loadComplete = true;
                yield break;
            }

            string[] files = Directory.GetFiles(customPath, "*.obj", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Debug.Log("[MeshReplacer] CustomMeshes folder is empty.");
                loadComplete = true;
                yield break;
            }

            Debug.Log("[MeshReplacer] Loading " + files.Length + " custom mesh(es) from " + customPath + "...");

            foreach (string filePath in files)
            {
                // relative path from CustomMeshes root, e.g. "snipers/SniperRifle_mesh_noHammers.obj"
                string relativePath = filePath.Substring(customPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fileName = Path.GetFileName(filePath);
                try
                {
                    string[] lines = File.ReadAllLines(filePath);
                    Mesh mesh = ParseObj(lines);
                    if (mesh != null)
                    {
                        // index by relative path (with and without extension) and bare filename (with and without extension)
                        customMeshes[relativePath] = mesh;
                        customMeshes[Path.GetFileNameWithoutExtension(relativePath)] = mesh;
                        customMeshes[fileName] = mesh;
                        customMeshes[Path.GetFileNameWithoutExtension(fileName)] = mesh;
                        Debug.Log("[MeshReplacer] Loaded mesh: " + relativePath + " (" + mesh.vertexCount + " verts)");
                    }
                    else
                    {
                        Debug.LogWarning("[MeshReplacer] Failed to parse mesh: " + relativePath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MeshReplacer] Error loading " + relativePath + ": " + ex.Message);
                }
                yield return null; // spread across frames
            }

            loadComplete = true;
            Debug.Log("[MeshReplacer] Mesh loading complete. " + customMeshes.Count / 2 + " mesh(es) loaded.");
            QueueExistingSceneRoots();
            StartQueueProcessing();
        }

        // Called from IL-injected hooks in GearModel.Awake and UnitView.UpdateUnit.
        // Scans only the provided GameObject's children — fast, no scene-wide search.
        public static void OnGameObjectInstantiated(GameObject go)
        {
            if (go == null || replacementMap.Count == 0) return;
            QueueGameObject(go);
            if (loadComplete)
            {
                StartQueueProcessing();
            }
        }

        private static void ApplyReplacements(GameObject go)
        {
            if (go == null || replacementMap.Count == 0) return;

            SkinnedMeshRenderer[] skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in skinned)
            {
                if (smr.sharedMesh == null) continue;
                string meshName = smr.sharedMesh.name;
                string fileName;
                if (!replacementMap.TryGetValue(meshName, out fileName)) continue;
                Mesh customMesh;
                if (!customMeshes.TryGetValue(fileName, out customMesh) &&
                    !customMeshes.TryGetValue(Path.GetFileNameWithoutExtension(fileName), out customMesh)) continue;
                Mesh skinnedMesh = GetOrBuildSkinnedMesh(smr.sharedMesh, customMesh, meshName);
                smr.sharedMesh = skinnedMesh;
                Debug.Log("[MeshReplacer] Replaced SMR \"" + meshName + "\" on " + go.name);
            }

            MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                string meshName = mf.sharedMesh.name;
                string fileName;
                if (!replacementMap.TryGetValue(meshName, out fileName)) continue;
                Mesh customMesh;
                if (!customMeshes.TryGetValue(fileName, out customMesh) &&
                    !customMeshes.TryGetValue(Path.GetFileNameWithoutExtension(fileName), out customMesh)) continue;
                mf.sharedMesh = customMesh;
                Debug.Log("[MeshReplacer] Replaced MF \"" + meshName + "\" on " + go.name);
            }

            // Apply transform fixes keyed by the GameObject name
            List<TransformFix> fixes;
            if (transformFixMap.TryGetValue(go.name, out fixes))
            {
                foreach (TransformFix fix in fixes)
                {
                    Transform t = string.IsNullOrEmpty(fix.path) ? go.transform : go.transform.Find(fix.path);
                    if (t == null)
                    {
                        Debug.LogWarning("[MeshReplacer] TransformFix: path not found \"" + fix.path + "\" on " + go.name);
                        continue;
                    }
                    if (fix.hasPosition) t.localPosition = fix.position;
                    if (fix.hasRotation) t.localRotation = fix.rotation;
                    if (fix.hasScale)    t.localScale    = fix.scale;
                    Debug.Log("[MeshReplacer] Applied transform fix on \"" + fix.path + "\" of " + go.name);
                }
            }
        }

        // Cache of already-built skinned replacement meshes keyed by original mesh instance ID
        private static readonly Dictionary<int, Mesh> skinnedMeshCache = new Dictionary<int, Mesh>();

        private static Mesh GetOrBuildSkinnedMesh(Mesh originalMesh, Mesh customMesh, string meshName)
        {
            int key = originalMesh.GetInstanceID();
            Mesh cached;
            if (skinnedMeshCache.TryGetValue(key, out cached))
                return cached;

            Mesh built = TransferSkinning(originalMesh, customMesh);
            built.name = meshName + "_custom";
            skinnedMeshCache[key] = built;
            return built;
        }

        // Copies bone weights from originalMesh onto replacementMesh using nearest-vertex lookup.
        // This allows the replacement to follow the skeleton even when vertex counts differ.
        private static Mesh TransferSkinning(Mesh originalMesh, Mesh replacementMesh)
        {
            Vector3[] origVerts = originalMesh.vertices;
            BoneWeight[] origWeights = originalMesh.boneWeights;
            Matrix4x4[] origBindposes = originalMesh.bindposes;

            if (origWeights == null || origWeights.Length == 0 || origBindposes == null || origBindposes.Length == 0)
            {
                Debug.LogWarning("[MeshReplacer] Original mesh has no skinning data, swap will T-pose.");
                return replacementMesh;
            }

            Vector3[] newVerts = replacementMesh.vertices;
            BoneWeight[] newWeights = new BoneWeight[newVerts.Length];

            if (newVerts.Length == origWeights.Length)
            {
                Array.Copy(origWeights, newWeights, origWeights.Length);
            }
            else
            {
                for (int i = 0; i < newVerts.Length; i++)
                {
                    newWeights[i] = FindNearestBoneWeight(newVerts[i], origVerts, origWeights);
                }
            }

            // Build the final mesh with original geometry + transferred skinning
            var mesh = new Mesh();
            mesh.name = replacementMesh.name;
            mesh.vertices = newVerts;
            mesh.normals = replacementMesh.normals;
            mesh.uv = replacementMesh.uv;
            mesh.triangles = replacementMesh.triangles;
            mesh.boneWeights = newWeights;
            mesh.bindposes = origBindposes;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static BoneWeight FindNearestBoneWeight(Vector3 point, Vector3[] origVerts, BoneWeight[] origWeights)
        {
            int nearest = 0;
            float nearestSqDist = float.MaxValue;
            for (int i = 0; i < origVerts.Length; i++)
            {
                float sqDist = (origVerts[i] - point).sqrMagnitude;
                if (sqDist < nearestSqDist)
                {
                    nearestSqDist = sqDist;
                    nearest = i;
                }
            }
            return origWeights[nearest];
        }

        private static Mesh ParseObj(string[] lines)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            // face corners: position index, uv index, normal index (all 1-based from OBJ)
            var facePositionIndices = new List<int>();
            var faceUvIndices = new List<int>();
            var faceNormalIndices = new List<int>();

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line.StartsWith("vn "))
                {
                    string[] p = line.Substring(3).Trim().Split(' ');
                    if (p.Length >= 3)
                        normals.Add(new Vector3(ParseFloat(p[0]), ParseFloat(p[1]), ParseFloat(p[2])));
                }
                else if (line.StartsWith("vt "))
                {
                    string[] p = line.Substring(3).Trim().Split(' ');
                    if (p.Length >= 2)
                        uvs.Add(new Vector2(ParseFloat(p[0]), ParseFloat(p[1])));
                }
                else if (line.StartsWith("v "))
                {
                    string[] p = line.Substring(2).Trim().Split(' ');
                    if (p.Length >= 3)
                        positions.Add(new Vector3(ParseFloat(p[0]), ParseFloat(p[1]), ParseFloat(p[2])));
                }
                else if (line.StartsWith("f "))
                {
                    string[] corners = line.Substring(2).Trim().Split(' ');
                    // Triangulate fan (handles tris and quads)
                    for (int i = 1; i < corners.Length - 1; i++)
                    {
                        AddFaceCorner(corners[0], facePositionIndices, faceUvIndices, faceNormalIndices);
                        AddFaceCorner(corners[i], facePositionIndices, faceUvIndices, faceNormalIndices);
                        AddFaceCorner(corners[i + 1], facePositionIndices, faceUvIndices, faceNormalIndices);
                    }
                }
            }

            if (positions.Count == 0 || facePositionIndices.Count == 0)
                return null;

            // Build unified vertex list (Unity needs one index per attribute combination)
            var vertexMap = new Dictionary<long, int>();
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var triangles = new List<int>();

            bool hasNormals = normals.Count > 0;
            bool hasUvs = uvs.Count > 0;

            for (int i = 0; i < facePositionIndices.Count; i++)
            {
                int pi = facePositionIndices[i];
                int ni = faceNormalIndices[i];
                int ti = faceUvIndices[i];

                long key = ((long)pi << 42) | ((long)(ni & 0x1FFFFF) << 21) | (long)(ti & 0x1FFFFF);
                int idx;
                if (!vertexMap.TryGetValue(key, out idx))
                {
                    idx = outPositions.Count;
                    vertexMap[key] = idx;
                    outPositions.Add(pi >= 0 && pi < positions.Count ? positions[pi] : Vector3.zero);
                    outNormals.Add(hasNormals && ni >= 0 && ni < normals.Count ? normals[ni] : Vector3.up);
                    outUvs.Add(hasUvs && ti >= 0 && ti < uvs.Count ? uvs[ti] : Vector2.zero);
                }
                triangles.Add(idx);
            }

            var mesh = new Mesh();
            mesh.vertices = outPositions.ToArray();
            if (hasNormals) mesh.normals = outNormals.ToArray();
            if (hasUvs) mesh.uv = outUvs.ToArray();
            mesh.triangles = triangles.ToArray();
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFaceCorner(string token, List<int> pi, List<int> ti, List<int> ni)
        {
            // token formats: "v", "v/vt", "v/vt/vn", "v//vn"
            string[] parts = token.Split('/');
            int p = parts.Length > 0 ? ParseIndex(parts[0]) : -1;
            int t = parts.Length > 1 ? ParseIndex(parts[1]) : -1;
            int n = parts.Length > 2 ? ParseIndex(parts[2]) : -1;
            pi.Add(p);
            ti.Add(t);
            ni.Add(n);
        }

        private static int ParseIndex(string s)
        {
            int v;
            if (string.IsNullOrEmpty(s) || !int.TryParse(s, out v)) return -1;
            return v - 1; // OBJ is 1-based
        }

        private static float ParseFloat(string s)
        {
            float v;
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v);
            return v;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnLevelWasLoaded(int level)
        {
            if (!loadComplete || replacementMap.Count == 0) return;
            QueueExistingSceneRoots();
            StartQueueProcessing();
        }

        private static void QueueExistingSceneRoots()
        {
            UnityEngine.Object[] sceneObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            if (sceneObjects == null) return;

            foreach (UnityEngine.Object obj in sceneObjects)
            {
                GameObject go = obj as GameObject;
                if (go == null) continue;
                if (go.transform.parent != null) continue;
                if (instance != null && go == instance.gameObject) continue;
                if ((go.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
                QueueGameObject(go);
            }
        }

        private static void QueueGameObject(GameObject go)
        {
            if (go == null) return;

            GameObject root = go.transform.root != null ? go.transform.root.gameObject : go;
            int id = root.GetInstanceID();
            if (pendingObjectIds.Add(id))
            {
                pendingObjects.Add(root);
            }
        }

        private static void StartQueueProcessing()
        {
            if (instance == null || isProcessingQueue) return;
            instance.StartCoroutine(instance.ProcessQueuedObjects());
        }

        private IEnumerator ProcessQueuedObjects()
        {
            isProcessingQueue = true;
            while (pendingObjects.Count > 0)
            {
                GameObject go = pendingObjects[0];
                pendingObjects.RemoveAt(0);

                if (go != null)
                {
                    pendingObjectIds.Remove(go.GetInstanceID());
                    ApplyReplacements(go);
                }

                yield return null;
            }

            isProcessingQueue = false;
        }
    }
}
