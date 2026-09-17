using System.IO;
using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Uretilen mesh / materyal / prefab'lari diske yazan yardimci katman.
    /// Tum uretecler idempotenttir: ayni menu komutunu tekrar calistirmak mevcut
    /// asset'i yerinde gunceller, GUID'i korur, sahnedeki referanslari bozmaz.
    /// </summary>
    public static class ArtGenIO
    {
        public const string RootFolder = "Assets/GameArt";
        public const string MeshFolder = RootFolder + "/Meshes";
        public const string MaterialFolder = RootFolder + "/Materials";
        public const string PrefabFolder = RootFolder + "/Prefabs";
        public const string AvatarFolder = RootFolder + "/Avatars";

        public const string ToonShaderName = "NewBattle/ToonVertexColor";

        public static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(AvatarFolder);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Mesh'i .asset olarak kaydeder. Asset zaten varsa icerigini degistirir ki
        /// mesh'e bagli prefab ve sahne referanslari kopmasin.
        /// </summary>
        public static Mesh SaveMesh(Mesh mesh, string assetName, string subFolder = null)
        {
            string folder = string.IsNullOrEmpty(subFolder) ? MeshFolder : MeshFolder + "/" + subFolder;
            EnsureFolder(folder);

            string path = $"{folder}/{assetName}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.colors = mesh.colors;
            existing.triangles = mesh.triangles;

            BoneWeight[] weights = mesh.boneWeights;
            if (weights != null && weights.Length == mesh.vertexCount)
                existing.boneWeights = weights;

            Matrix4x4[] bindposes = mesh.bindposes;
            if (bindposes != null && bindposes.Length > 0)
                existing.bindposes = bindposes;

            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);

            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Toon shader'i kullanan tekil materyal. Tum mesh'ler bunu paylasir.</summary>
        public static Material GetOrCreateToonMaterial(string materialName, bool transparent = false)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(ToonShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"ArtGen: '{ToonShaderName}' shader'i bulunamadi. " +
                    "Assets/Shaders/ToonVertexColor.shader dosyasinin derlendiginden emin ol."
                );
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new(shader) { name = materialName };
            material.enableInstancing = true;

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Prefab'i kaydeder; varsa uzerine yazar ve sahnedeki orneklerin baglantisini korur.</summary>
        public static GameObject SavePrefab(GameObject instance, string prefabName, string subFolder = null)
        {
            string folder = string.IsNullOrEmpty(subFolder) ? PrefabFolder : PrefabFolder + "/" + subFolder;
            EnsureFolder(folder);

            string path = $"{folder}/{prefabName}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        public static T SaveSubAsset<T>(T asset, Object parent, string assetName) where T : Object
        {
            asset.name = assetName;
            AssetDatabase.AddObjectToAsset(asset, parent);
            return asset;
        }

        /// <summary>Verilen mesh'i tasiyan, toon materyalli basit bir GameObject kurar.</summary>
        public static GameObject CreateMeshObject(string objectName, Mesh mesh, Material material, Transform parent = null)
        {
            GameObject go = new(objectName);
            if (parent != null)
                go.transform.SetParent(parent, false);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            return go;
        }
    }
}
