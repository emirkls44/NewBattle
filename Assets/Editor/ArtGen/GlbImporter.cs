using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Kendi GLB (binary glTF) okuyucumuz.
    ///
    /// Neden hazir paket yerine bu: glTFast kurulumu ek bir bagimlilik ve surum
    /// riski getiriyor. Elimizdeki dosya ise en sade glTF turunden - doku yok,
    /// animasyon yok, deri (skin) yok, uzanti yok. Bu kadarini okumak birkac yuz
    /// satir ve karsiliginda projede hicbir dis paket tasimiyoruz.
    ///
    /// DESTEKLENEN: mesh (POSITION/NORMAL/TEXCOORD_0), indeksler, dugum hiyerarsisi,
    /// TRS ve matris donusumleri, pbrMetallicRoughness materyalleri.
    /// DESTEKLENMEYEN: dokular, animasyon, skinning, draco sikistirma.
    /// Bunlardan biri dosyada varsa uyari basip o kismi atlariz.
    ///
    /// KOORDINAT DONUSUMU: glTF sag elli (RH), Unity sol elli (LH). Z eksenini
    /// ters ceviriyoruz; bu ayni zamanda ucgen sarim yonunu da tersine cevirdigi
    /// icin indeksleri de ters siraliyoruz. Bu yapilmazsa model ici disina donuk
    /// gorunur (yuzler kameradan kacar).
    /// </summary>
    public static class GlbImporter
    {
        private const uint JsonChunk = 0x4E4F534A;
        private const uint BinaryChunk = 0x004E4942;

        [MenuItem("Tools/NewBattle/GLB Modelini Ice Aktar", false, 4)]
        public static void ImportViaDialog()
        {
            string startFolder = Directory.Exists(Application.dataPath + "/Map")
                ? Application.dataPath + "/Map"
                : Application.dataPath;

            string path = EditorUtility.OpenFilePanel("GLB dosyasi sec", startFolder, "glb");

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("GLB", "Okunuyor...", 0.1f);
                GameObject instance = Import(path);

                if (instance != null)
                {
                    Selection.activeGameObject = instance;
                    EditorGUIUtility.PingObject(instance);
                    Debug.Log(
                        $"GLB ice aktarildi: {instance.name}\n" +
                        "Sirada: Tools > NewBattle > Harita Kur (GLB/FBX)");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"GLB okunamadi: {exception.Message}\n{exception}");
                EditorUtility.DisplayDialog("GLB okunamadi", exception.Message, "Tamam");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region Ana akis

        public static GameObject Import(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            ParseContainer(data, out JObject gltf, out byte[] binary);

            string modelName = Path.GetFileNameWithoutExtension(filePath);
            string outputFolder = $"{ArtGenIO.RootFolder}/Imported/{modelName}";
            ArtGenIO.EnsureFolder(outputFolder);
            ArtGenIO.EnsureFolder(outputFolder + "/Meshes");

            WarnAboutUnsupported(gltf);

            Context context = new()
            {
                Gltf = gltf,
                Binary = binary,
                OutputFolder = outputFolder,
                ModelName = modelName
            };

            EditorUtility.DisplayProgressBar("GLB", "Materyaller...", 0.25f);
            context.Materials = BuildMaterials(context);

            EditorUtility.DisplayProgressBar("GLB", "Mesh'ler...", 0.45f);
            context.Meshes = BuildMeshes(context);

            EditorUtility.DisplayProgressBar("GLB", "Hiyerarsi...", 0.8f);
            GameObject root = BuildHierarchy(context);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return root;
        }

        private class Context
        {
            public JObject Gltf;
            public byte[] Binary;
            public string OutputFolder;
            public string ModelName;
            public Material[] Materials;
            public Mesh[] Meshes;
            public int[] MeshMaterial;
        }

        /// <summary>GLB kabugunu ac: 12 bayt baslik, ardindan JSON ve BIN parcalari.</summary>
        private static void ParseContainer(byte[] data, out JObject gltf, out byte[] binary)
        {
            if (data.Length < 12)
                throw new Exception("Dosya cok kucuk, gecerli bir GLB degil.");

            uint magic = BitConverter.ToUInt32(data, 0);
            if (magic != 0x46546C67) // "glTF"
                throw new Exception("Bu bir GLB dosyasi degil (imza tutmuyor).");

            uint totalLength = BitConverter.ToUInt32(data, 8);
            int offset = 12;

            gltf = null;
            binary = Array.Empty<byte>();

            while (offset + 8 <= data.Length && offset < totalLength)
            {
                uint chunkLength = BitConverter.ToUInt32(data, offset);
                uint chunkType = BitConverter.ToUInt32(data, offset + 4);
                offset += 8;

                if (offset + chunkLength > data.Length)
                    break;

                if (chunkType == JsonChunk)
                {
                    string json = System.Text.Encoding.UTF8.GetString(data, offset, (int)chunkLength);
                    gltf = JObject.Parse(json);
                }
                else if (chunkType == BinaryChunk)
                {
                    binary = new byte[chunkLength];
                    Array.Copy(data, offset, binary, 0, (int)chunkLength);
                }

                offset += (int)chunkLength;
            }

            if (gltf == null)
                throw new Exception("GLB icinde JSON parcasi bulunamadi.");
        }

        private static void WarnAboutUnsupported(JObject gltf)
        {
            if (gltf["animations"] is JArray animations && animations.Count > 0)
                Debug.LogWarning($"GLB: {animations.Count} animasyon atlandi (desteklenmiyor).");

            if (gltf["skins"] is JArray skins && skins.Count > 0)
                Debug.LogWarning($"GLB: {skins.Count} skin atlandi (desteklenmiyor).");

            if (gltf["extensionsRequired"] is JArray required && required.Count > 0)
                Debug.LogWarning($"GLB: zorunlu uzantilar desteklenmiyor: {string.Join(", ", required)}");

            if (gltf["images"] is JArray images && images.Count > 0)
                Debug.LogWarning($"GLB: {images.Count} doku atlandi; materyaller duz renk olarak kurulur.");
        }

        #endregion

        #region Materyal

        private static Material[] BuildMaterials(Context context)
        {
            JArray materials = context.Gltf["materials"] as JArray;
            string folder = context.OutputFolder;

            if (materials == null || materials.Count == 0)
                return new[] { CreateMaterial("Default", Color.white, 0f, 0.5f, false, folder) };

            Material[] result = new Material[materials.Count];

            for (int i = 0; i < materials.Count; i++)
            {
                JObject material = (JObject)materials[i];
                string name = material["name"]?.ToString() ?? $"Material_{i}";

                Color baseColor = Color.white;
                float metallic = 0f;
                float roughness = 0.75f;

                if (material["pbrMetallicRoughness"] is JObject pbr)
                {
                    if (pbr["baseColorFactor"] is JArray factor && factor.Count >= 4)
                    {
                        baseColor = new Color(
                            factor[0].Value<float>(), factor[1].Value<float>(),
                            factor[2].Value<float>(), factor[3].Value<float>());
                    }

                    if (pbr["metallicFactor"] != null)
                        metallic = pbr["metallicFactor"].Value<float>();

                    if (pbr["roughnessFactor"] != null)
                        roughness = pbr["roughnessFactor"].Value<float>();
                }

                bool doubleSided = material["doubleSided"]?.Value<bool>() ?? false;
                result[i] = CreateMaterial(name, baseColor, metallic, roughness, doubleSided, folder);
            }

            return result;
        }

        private static Material CreateMaterial(string name, Color baseColor, float metallic,
            float roughness, bool doubleSided, string folder)
        {
            string safeName = MakeSafeName(name);
            string path = $"{folder}/Mat_{safeName}.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = existing != null ? existing : new Material(shader);
            material.shader = shader;
            material.name = $"Mat_{safeName}";

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(roughness));

            // Cift yuzlu materyaller low-poly bitki/yaprak icin sik kullanilir;
            // tek yuze zorlarsak yapraklar bir yonden bakinca kaybolur.
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", doubleSided ? 0f : 2f);

            material.enableInstancing = true;

            if (existing == null)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(existing);

            return material;
        }

        #endregion

        #region Mesh

        private static Mesh[] BuildMeshes(Context context)
        {
            JArray meshes = (JArray)context.Gltf["meshes"];
            Mesh[] result = new Mesh[meshes.Count];
            context.MeshMaterial = new int[meshes.Count];

            for (int i = 0; i < meshes.Count; i++)
            {
                JObject mesh = (JObject)meshes[i];
                JArray primitives = (JArray)mesh["primitives"];

                if (primitives == null || primitives.Count == 0)
                    continue;

                // Dosyada mesh basina tek primitive var; birden fazlasi gelirse
                // hepsini tek mesh'te birlestiriyoruz (materyal ilki kazanir).
                List<Vector3> vertices = new();
                List<Vector3> normals = new();
                List<Vector2> uvs = new();
                List<int> triangles = new();
                int materialIndex = 0;

                for (int p = 0; p < primitives.Count; p++)
                {
                    JObject primitive = (JObject)primitives[p];

                    // mode 4 = TRIANGLES. Digerleri (line, point) bizim icin anlamsiz.
                    int mode = primitive["mode"]?.Value<int>() ?? 4;
                    if (mode != 4)
                        continue;

                    if (p == 0 && primitive["material"] != null)
                        materialIndex = primitive["material"].Value<int>();

                    JObject attributes = (JObject)primitive["attributes"];
                    if (attributes?["POSITION"] == null)
                        continue;

                    int baseVertex = vertices.Count;

                    Vector3[] positions = ReadVector3(context, attributes["POSITION"].Value<int>(), true);
                    vertices.AddRange(positions);

                    if (attributes["NORMAL"] != null)
                        normals.AddRange(ReadVector3(context, attributes["NORMAL"].Value<int>(), true));
                    else
                        normals.AddRange(new Vector3[positions.Length]);

                    if (attributes["TEXCOORD_0"] != null)
                        uvs.AddRange(ReadVector2(context, attributes["TEXCOORD_0"].Value<int>()));
                    else
                        uvs.AddRange(new Vector2[positions.Length]);

                    if (primitive["indices"] != null)
                    {
                        int[] indices = ReadIndices(context, primitive["indices"].Value<int>());

                        // Z ters cevrildigi icin sarim yonu de ters cevrilmeli.
                        for (int t = 0; t + 2 < indices.Length; t += 3)
                        {
                            triangles.Add(baseVertex + indices[t]);
                            triangles.Add(baseVertex + indices[t + 2]);
                            triangles.Add(baseVertex + indices[t + 1]);
                        }
                    }
                    else
                    {
                        for (int t = 0; t + 2 < positions.Length; t += 3)
                        {
                            triangles.Add(baseVertex + t);
                            triangles.Add(baseVertex + t + 2);
                            triangles.Add(baseVertex + t + 1);
                        }
                    }
                }

                if (vertices.Count == 0)
                    continue;

                string meshName = mesh["name"]?.ToString() ?? $"Mesh_{i}";
                Mesh unityMesh = new()
                {
                    name = MakeSafeName(meshName),
                    indexFormat = vertices.Count > 65000
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16
                };

                unityMesh.SetVertices(vertices);
                unityMesh.SetNormals(normals);
                unityMesh.SetUVs(0, uvs);
                unityMesh.SetTriangles(triangles, 0);
                unityMesh.RecalculateBounds();

                result[i] = SaveMeshAsset(unityMesh, $"{context.OutputFolder}/Meshes/{unityMesh.name}_{i}.asset");
                context.MeshMaterial[i] = materialIndex;
            }

            return result;
        }

        #endregion

        #region Accessor okuma

        private static Vector3[] ReadVector3(Context context, int accessorIndex, bool flipZ)
        {
            float[] raw = ReadFloats(context, accessorIndex, 3, out int count);
            Vector3[] result = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = new Vector3(
                    raw[i * 3],
                    raw[i * 3 + 1],
                    flipZ ? -raw[i * 3 + 2] : raw[i * 3 + 2]);
            }

            return result;
        }

        private static Vector2[] ReadVector2(Context context, int accessorIndex)
        {
            float[] raw = ReadFloats(context, accessorIndex, 2, out int count);
            Vector2[] result = new Vector2[count];

            // glTF UV baslangici sol UST, Unity'de sol ALT: V eksenini cevir.
            for (int i = 0; i < count; i++)
                result[i] = new Vector2(raw[i * 2], 1f - raw[i * 2 + 1]);

            return result;
        }

        private static float[] ReadFloats(Context context, int accessorIndex, int components, out int count)
        {
            JObject accessor = (JObject)((JArray)context.Gltf["accessors"])[accessorIndex];
            count = accessor["count"].Value<int>();
            int componentType = accessor["componentType"].Value<int>();
            bool normalized = accessor["normalized"]?.Value<bool>() ?? false;

            GetBufferSlice(context, accessor, components, componentType,
                out byte[] buffer, out int start, out int stride, out int elementSize);

            float[] result = new float[count * components];

            for (int i = 0; i < count; i++)
            {
                int offset = start + i * stride;

                for (int c = 0; c < components; c++)
                {
                    int at = offset + c * elementSize;
                    result[i * components + c] = ReadComponentAsFloat(buffer, at, componentType, normalized);
                }
            }

            return result;
        }

        private static int[] ReadIndices(Context context, int accessorIndex)
        {
            JObject accessor = (JObject)((JArray)context.Gltf["accessors"])[accessorIndex];
            int count = accessor["count"].Value<int>();
            int componentType = accessor["componentType"].Value<int>();

            GetBufferSlice(context, accessor, 1, componentType,
                out byte[] buffer, out int start, out int stride, out int elementSize);

            int[] result = new int[count];

            for (int i = 0; i < count; i++)
            {
                int at = start + i * stride;

                result[i] = componentType switch
                {
                    5121 => buffer[at],
                    5123 => BitConverter.ToUInt16(buffer, at),
                    5125 => (int)BitConverter.ToUInt32(buffer, at),
                    5122 => BitConverter.ToInt16(buffer, at),
                    _ => throw new Exception($"Desteklenmeyen indeks tipi: {componentType}")
                };
            }

            return result;
        }

        private static void GetBufferSlice(Context context, JObject accessor, int components,
            int componentType, out byte[] buffer, out int start, out int stride, out int elementSize)
        {
            elementSize = ComponentSize(componentType);

            if (accessor["bufferView"] == null)
                throw new Exception("Sparse accessor desteklenmiyor.");

            JObject view = (JObject)((JArray)context.Gltf["bufferViews"])[accessor["bufferView"].Value<int>()];

            int viewOffset = view["byteOffset"]?.Value<int>() ?? 0;
            int accessorOffset = accessor["byteOffset"]?.Value<int>() ?? 0;
            int byteStride = view["byteStride"]?.Value<int>() ?? 0;

            buffer = context.Binary;
            start = viewOffset + accessorOffset;

            // byteStride 0 ise veriler sikisik dizilmistir.
            stride = byteStride > 0 ? byteStride : elementSize * components;
        }

        private static int ComponentSize(int componentType)
        {
            return componentType switch
            {
                5120 or 5121 => 1,
                5122 or 5123 => 2,
                5125 or 5126 => 4,
                _ => throw new Exception($"Bilinmeyen componentType: {componentType}")
            };
        }

        private static float ReadComponentAsFloat(byte[] buffer, int at, int componentType, bool normalized)
        {
            switch (componentType)
            {
                case 5126: return BitConverter.ToSingle(buffer, at);
                case 5125: return BitConverter.ToUInt32(buffer, at);
                case 5123:
                {
                    ushort value = BitConverter.ToUInt16(buffer, at);
                    return normalized ? value / 65535f : value;
                }
                case 5122:
                {
                    short value = BitConverter.ToInt16(buffer, at);
                    return normalized ? Mathf.Max(value / 32767f, -1f) : value;
                }
                case 5121:
                {
                    byte value = buffer[at];
                    return normalized ? value / 255f : value;
                }
                case 5120:
                {
                    sbyte value = (sbyte)buffer[at];
                    return normalized ? Mathf.Max(value / 127f, -1f) : value;
                }
                default:
                    throw new Exception($"Bilinmeyen componentType: {componentType}");
            }
        }

        #endregion

        #region Hiyerarsi

        private static GameObject BuildHierarchy(Context context)
        {
            JArray nodes = (JArray)context.Gltf["nodes"];
            JObject scene = (JObject)((JArray)context.Gltf["scenes"])[
                context.Gltf["scene"]?.Value<int>() ?? 0];

            GameObject root = new(context.ModelName);

            foreach (JToken rootIndex in (JArray)scene["nodes"])
                CreateNode(context, nodes, rootIndex.Value<int>(), root.transform);

            return root;
        }

        private static void CreateNode(Context context, JArray nodes, int index, Transform parent)
        {
            JObject node = (JObject)nodes[index];
            string name = node["name"]?.ToString() ?? $"Node_{index}";

            GameObject nodeObject = new(MakeSafeName(name));
            nodeObject.transform.SetParent(parent, false);

            ApplyTransform(node, nodeObject.transform);

            if (node["mesh"] != null)
            {
                int meshIndex = node["mesh"].Value<int>();

                if (meshIndex >= 0 && meshIndex < context.Meshes.Length && context.Meshes[meshIndex] != null)
                {
                    MeshFilter filter = nodeObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = context.Meshes[meshIndex];

                    MeshRenderer renderer = nodeObject.AddComponent<MeshRenderer>();
                    int materialIndex = Mathf.Clamp(context.MeshMaterial[meshIndex], 0,
                        Mathf.Max(0, context.Materials.Length - 1));
                    renderer.sharedMaterial = context.Materials[materialIndex];
                }
            }

            if (node["children"] is JArray children)
            {
                foreach (JToken child in children)
                    CreateNode(context, nodes, child.Value<int>(), nodeObject.transform);
            }
        }

        /// <summary>
        /// Dugum donusumunu Unity'ye cevirir. Dugum ya TRS ya da 16 elemanli
        /// matris tasir; matris varsa once ayristiriyoruz.
        /// </summary>
        private static void ApplyTransform(JObject node, Transform target)
        {
            Vector3 translation = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one;

            if (node["matrix"] is JArray matrixArray && matrixArray.Count == 16)
            {
                float[] m = new float[16];
                for (int i = 0; i < 16; i++)
                    m[i] = matrixArray[i].Value<float>();

                // glTF matrisi sutun oncelikli saklanir.
                Matrix4x4 matrix = new();
                matrix.SetColumn(0, new Vector4(m[0], m[1], m[2], m[3]));
                matrix.SetColumn(1, new Vector4(m[4], m[5], m[6], m[7]));
                matrix.SetColumn(2, new Vector4(m[8], m[9], m[10], m[11]));
                matrix.SetColumn(3, new Vector4(m[12], m[13], m[14], m[15]));

                translation = matrix.GetColumn(3);
                scale = matrix.lossyScale;
                rotation = matrix.rotation;
            }
            else
            {
                if (node["translation"] is JArray t && t.Count >= 3)
                    translation = new Vector3(t[0].Value<float>(), t[1].Value<float>(), t[2].Value<float>());

                if (node["rotation"] is JArray r && r.Count >= 4)
                {
                    rotation = new Quaternion(
                        r[0].Value<float>(), r[1].Value<float>(),
                        r[2].Value<float>(), r[3].Value<float>());
                }

                if (node["scale"] is JArray s && s.Count >= 3)
                    scale = new Vector3(s[0].Value<float>(), s[1].Value<float>(), s[2].Value<float>());
            }

            // RH -> LH: konumda Z ters, donuste X ve Y bilesenleri ters.
            target.localPosition = new Vector3(translation.x, translation.y, -translation.z);
            target.localRotation = new Quaternion(-rotation.x, -rotation.y, rotation.z, rotation.w);
            target.localScale = scale;
        }

        #endregion

        /// <summary>
        /// Mesh'i verilen yola yazar. Asset zaten varsa icerigini degistirir ki
        /// yeniden ice aktarim sahnedeki ve prefab'lardaki referanslari koparmasin.
        /// </summary>
        private static Mesh SaveMeshAsset(Mesh mesh, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);

            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        private static string MakeSafeName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Replace('.', '_').Trim();
        }
    }
}
