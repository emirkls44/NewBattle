using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewBattle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Oyunu "yumusak" gorunume gecirir (keskin hatlar yerine Apple / oyuncak hissi):
    ///
    ///   1. Materyaller: gercekci URP/Lit ve eski bantli toon shader yerine
    ///      NewBattle/SoftToon. Doku ve renkler korunur.
    ///   2. Harita modelleri: keskin yuzlu agac ve kayalarin normalleri yumusatilir
    ///      (geometri ayni kalir, golgelenme yuvarlaklasir).
    ///   3. Golge: mobil render ayarinda yumusak golge acilir (dusuk kalite).
    ///   4. Renk: sahneye hafif bir post-processing (tonemapping, cok az bloom,
    ///      biraz canli renk, hafif vinyet).
    ///   5. Arayuz: spritesiz duz dikdortgen paneller yuvarlak koseli olur.
    ///
    /// Geri almak: git'teki "safe commit" noktasina donmek her seyi geri alir.
    /// Materyal ve sahne degisiklikleri ayrica Ctrl+Z ile de geri alinabilir.
    /// </summary>
    public static class SoftStyleSetup
    {
        private const string SoftToonShaderName = "NewBattle/SoftToon";
        private const string ProfilePath = "Assets/Settings/SoftStyleProfile.asset";
        private const string RoundedSpritePath = "Assets/GameArt/UI/RoundedPanel.png";

        /// <summary>Yumusatilacak model asset'lerinin kok klasoru (ice aktarilan harita modelleri).</summary>
        private const string SmoothableMeshFolder = "Assets/GameArt/Imported/";
        private const float SmoothingAngle = 55f;

        private static readonly string[] PrefabFolders =
        {
            "Assets/Prefabs", "Assets/GameArt/Prefabs", "Assets/Arts/Characters", "Assets/GameArt/Imported/Props"
        };

        private static readonly HashSet<string> ConvertibleShaders = new()
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "NewBattle/ToonVertexColor"
        };

        /// <summary>
        /// Ice aktarilan orman paketinin renkleri: neredeyse siyah kaya, neon yesil
        /// yaprak. Yumusak stilde kaya bir "delik" gibi, yaprak da fosforlu duruyor.
        /// Sadece renk hala paketin orijinal rengiyse degistirilir; elle
        /// ayarladigin bir renge dokunulmaz.
        /// </summary>
        private static readonly (string path, Color original, string replacement)[] PaletteFixes =
        {
            ("Assets/GameArt/Imported/low_poly_forest/Mat_Rock.mat", new Color(0.126219f, 0.126219f, 0.126219f), "#B3ACA2"),
            ("Assets/GameArt/Imported/low_poly_forest/Mat_leaves.mat", new Color(0.142f, 0.8f, 0.142f), "#74C24F"),
            ("Assets/GameArt/Imported/low_poly_forest/Mat_wood.mat", new Color(0.269737f, 0.213288f, 0.167423f), "#8E6242")
        };

        [MenuItem("Tools/NewBattle/Gorsel Yenileme/3 - Yumusak Stili Uygula", false, 42)]
        public static void Run()
        {
            List<string> log = new();

            if (!RunSilently(log))
            {
                Debug.LogError("Yumusak stil yarida kaldi:\n" + string.Join("\n", log));
                return;
            }

            Debug.Log("Yumusak stil uygulandi:\n" + string.Join("\n", log));
        }

        [MenuItem("Tools/NewBattle/Gorsel Yenileme/Hepsini Uygula (1-2-3)", false, 60)]
        public static void RunAll()
        {
            List<string> log = new();
            bool ok = AnimationSetup.RunSilently(log) &&
                      LootVisualSetup.RunSilently(log) &&
                      RunSilently(log);

            if (ok)
                Debug.Log("Gorsel yenileme tamam:\n" + string.Join("\n", log));
            else
                Debug.LogError("Gorsel yenileme yarida kaldi:\n" + string.Join("\n", log));
        }

        public static bool RunSilently(List<string> log)
        {
            Shader softToon = Shader.Find(SoftToonShaderName);

            if (softToon == null || ShaderUtil.ShaderHasError(softToon))
            {
                log.Add($"HATA: '{SoftToonShaderName}' derlenemedi. Console'daki shader hatasina bak; " +
                        "hicbir materyale dokunulmadi.");
                return false;
            }

            List<Renderer> renderers = CollectRenderers();

            ConvertMaterials(renderers, softToon, log);
            SmoothMeshes(renderers, log);
            EnableSoftShadows(log);
            SetupPostProcessing(log);
            RoundUiPanels(log);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            log.Add("SAHNEYI KAYDETMEYI UNUTMA (Ctrl+S).");
            return true;
        }

        private static List<Renderer> CollectRenderers()
        {
            List<Renderer> renderers = Object
                .FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToList();

            string[] folders = PrefabFolders.Where(AssetDatabase.IsValidFolder).ToArray();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null)
                    renderers.AddRange(prefab.GetComponentsInChildren<Renderer>(true));
            }

            // Zemin gostergeleri (halkalar, ayak izleri) kendi unlit shader'ini kullanir.
            return renderers.Where(r => r != null && r.GetComponent<OverlayVisual>() == null).ToList();
        }

        #region Materyaller

        private static void ConvertMaterials(List<Renderer> renderers, Shader softToon, List<string> log)
        {
            HashSet<Material> materials = new();

            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                    continue;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                        materials.Add(material);
                }
            }

            int converted = 0;
            List<string> skipped = new();

            foreach (Material material in materials.OrderBy(m => m.name))
            {
                string path = AssetDatabase.GetAssetPath(material);

                // Model dosyasinin icine gomulu materyaller salt okunur; sadece
                // .mat olarak duran materyaller degistirilebilir.
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".mat") || !path.StartsWith("Assets/"))
                    continue;

                if (material.shader == softToon || !ConvertibleShaders.Contains(material.shader.name))
                    continue;

                if (IsTransparentOrCutout(material))
                {
                    skipped.Add(material.name);
                    continue;
                }

                Convert(material, softToon);
                converted++;
            }

            int recolored = FixPalette();

            log.Add($"Materyal: {converted} tanesi SoftToon'a gecti" +
                    (recolored > 0 ? $", {recolored} orman rengi yumusatildi" : "") +
                    (skipped.Count > 0 ? $". Saydam oldugu icin atlanan: {string.Join(", ", skipped)}" : "."));
        }

        private static bool IsTransparentOrCutout(Material material)
        {
            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
                return true;

            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                return true;

            return material.renderQueue >= (int)RenderQueue.AlphaTest;
        }

        private static void Convert(Material material, Shader softToon)
        {
            bool usesVertexColor = material.shader.name == "NewBattle/ToonVertexColor";
            Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            Vector2 scale = material.HasProperty("_BaseMap") ? material.GetTextureScale("_BaseMap") : Vector2.one;
            Vector2 offset = material.HasProperty("_BaseMap") ? material.GetTextureOffset("_BaseMap") : Vector2.zero;

            Undo.RecordObject(material, "Yumusak Stil");
            material.shader = softToon;

            baseColor.a = 1f;
            material.SetColor("_BaseColor", baseColor);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetFloat("_VertexColorStrength", usesVertexColor ? 1f : 0f);

            // Opak baslangic durumu: eski shader'dan kalma saydamlik ayari kalmasin.
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.renderQueue = -1;
            material.shaderKeywords = System.Array.Empty<string>();
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
        }

        private static int FixPalette()
        {
            int changed = 0;

            foreach ((string path, Color original, string replacement) in PaletteFixes)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_BaseColor"))
                    continue;

                Color current = material.GetColor("_BaseColor");
                bool untouched = Mathf.Abs(current.r - original.r) < 0.01f &&
                                 Mathf.Abs(current.g - original.g) < 0.01f &&
                                 Mathf.Abs(current.b - original.b) < 0.01f;

                if (!untouched || !ColorUtility.TryParseHtmlString(replacement, out Color target))
                    continue;

                Undo.RecordObject(material, "Yumusak Stil");
                material.SetColor("_BaseColor", target);
                EditorUtility.SetDirty(material);
                changed++;
            }

            return changed;
        }

        #endregion

        #region Modeller

        private static void SmoothMeshes(List<Renderer> renderers, List<string> log)
        {
            HashSet<Mesh> meshes = new();

            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;

                if (mesh == null)
                    continue;

                // Sadece ayri .asset olarak duran ice aktarilmis modeller
                // duzenlenebilir; FBX icindeki mesh'ler salt okunur ve Meshy
                // modelleri zaten yumusak.
                string path = AssetDatabase.GetAssetPath(mesh);
                if (path.StartsWith(SmoothableMeshFolder) && path.EndsWith(".asset"))
                    meshes.Add(mesh);
            }

            foreach (Mesh mesh in meshes)
            {
                MeshSoftener.SmoothNormals(mesh, SmoothingAngle);
                EditorUtility.SetDirty(mesh);
            }

            log.Add($"Harita modelleri: {meshes.Count} mesh'in normalleri yumusatildi " +
                    $"({SmoothingAngle} dereceden keskin kenarlar korunur).");
        }

        #endregion

        #region Golge ve renk

        private static void EnableSoftShadows(List<string> log)
        {
            HashSet<RenderPipelineAsset> assets = new();

            if (GraphicsSettings.defaultRenderPipeline != null)
                assets.Add(GraphicsSettings.defaultRenderPipeline);

            for (int i = 0; i < QualitySettings.count; i++)
            {
                RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(i);
                if (asset != null)
                    assets.Add(asset);
            }

            foreach (RenderPipelineAsset asset in assets.OfType<UniversalRenderPipelineAsset>())
            {
                SerializedObject serialized = new(asset);
                SerializedProperty supported = serialized.FindProperty("m_SoftShadowsSupported");
                SerializedProperty quality = serialized.FindProperty("m_SoftShadowQuality");

                if (supported == null)
                    continue;

                bool wasOn = supported.boolValue;
                supported.boolValue = true;

                // Mobilde en ucuz yumusak golge (1 = Low). Kenar yine yumusar,
                // piksel basina orneklem sayisi dusuk kalir.
                if (!wasOn && quality != null)
                    quality.intValue = 1;

                serialized.ApplyModifiedProperties();
                log.Add(wasOn
                    ? $"Golge: {asset.name} zaten yumusak golge destekliyor."
                    : $"Golge: {asset.name} icin yumusak golge acildi (dusuk kalite).");
            }
        }

        private static void SetupPostProcessing(List<string> log)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.55f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.05f);
            color.contrast.Override(8f);
            color.saturation.Override(10f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);

            Volume volume = Object
                .FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.isGlobal);

            if (volume == null)
            {
                GameObject holder = new("SoftStylePostFX");
                Undo.RegisterCreatedObjectUndo(holder, "Yumusak Stil");
                volume = holder.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                log.Add($"Renk: sahneye global Volume eklendi ({ProfilePath}).");
            }
            else if (volume.sharedProfile == null)
            {
                Undo.RecordObject(volume, "Yumusak Stil");
                volume.sharedProfile = profile;
                log.Add($"Renk: mevcut Volume'a {ProfilePath} atandi.");
            }
            else
            {
                log.Add($"Renk: sahnede zaten '{volume.name}' Volume'u var, ona dokunulmadi. " +
                        $"Istersen profilini {ProfilePath} yap.");
            }

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!camera.CompareTag("MainCamera"))
                    continue;

                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data == null || data.renderPostProcessing)
                    continue;

                Undo.RecordObject(data, "Yumusak Stil");
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                log.Add($"Renk: '{camera.name}' kamerasinda post-processing acildi.");
            }
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;

            component = profile.Add<T>(true);

            // Profil bir asset; bilesenleri de onun alt asset'i olarak kaydedilmeli,
            // yoksa Unity kapaninca kaybolurlar.
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        #endregion

        #region Arayuz

        private const int SpriteSize = 64;
        private const int CornerRadius = 20;
        private const int SliceBorder = 22;

        private static void RoundUiPanels(List<string> log)
        {
            Sprite rounded = EnsureRoundedSprite();

            if (rounded == null)
            {
                log.Add($"UYARI: {RoundedSpritePath} olusturulamadi; paneller oldugu gibi kaldi.");
                return;
            }

            int changed = 0;

            foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!IsPlainPanel(image))
                    continue;

                Undo.RecordObject(image, "Yumusak Stil");
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                EditorUtility.SetDirty(image);

                // Icinde baska ogeler olan paneller ve dugmeler hafifce "kalksin".
                if (image.transform.childCount > 0 && image.GetComponent<Shadow>() == null)
                {
                    Shadow shadow = Undo.AddComponent<Shadow>(image.gameObject);
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
                    shadow.effectDistance = new Vector2(0f, -3f);
                }

                changed++;
            }

            log.Add($"Arayuz: {changed} panel yuvarlak koseli oldu.");
        }

        /// <summary>
        /// Sadece "duz dikdortgen" paneller: sprite'i yok, tam ekran degil,
        /// gorunmez tiklama alani degil, maske degil. Ikonlara, haritaya ve
        /// dolum cubuklarinin Filled tipine dokunulmaz.
        /// </summary>
        private static bool IsPlainPanel(Image image)
        {
            if (image == null || image.sprite != null || image.type != Image.Type.Simple)
                return false;

            if (image.color.a < 0.02f || image.GetComponent<Mask>() != null)
                return false;

            string lowerName = image.name.ToLowerInvariant();
            if (lowerName.Contains("map") || lowerName.Contains("mask") || lowerName.Contains("overlay"))
                return false;

            RectTransform rect = image.rectTransform;
            bool stretchedFull = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                                 rect.offsetMin.sqrMagnitude < 1f && rect.offsetMax.sqrMagnitude < 1f &&
                                 (rect.parent == null || rect.parent.GetComponent<Canvas>() != null);

            return !stretchedFull;
        }

        private static Sprite EnsureRoundedSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (existing != null)
                return existing;

            ArtGenIO.EnsureFolder(Path.GetDirectoryName(RoundedSpritePath).Replace('\\', '/'));

            Texture2D texture = new(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            float half = SpriteSize * 0.5f;

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    // Yuvarlak dikdortgen mesafe alani; 1 piksellik yumusak kenar.
                    Vector2 p = new(Mathf.Abs(x + 0.5f - half), Mathf.Abs(y + 0.5f - half));
                    Vector2 q = p - new Vector2(half - CornerRadius, half - CornerRadius);
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - CornerRadius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RoundedSpritePath);

            if (AssetImporter.GetAtPath(RoundedSpritePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        }

        #endregion
    }
}
