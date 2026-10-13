#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu: karakterin bastigi zemin. Hepsi elle ayarlanabilir:
    ///   - Hierarchy > MenuKarakterSahnesi > KarakterZemini: tasi / olcekle / dondur
    ///     (modeli istersen MeshFilter'dan baska bir sekille degistir)
    ///   - Assets/GameArt/Materials/MenuZemin: rengi (Tint), golgesi, parlakligi
    ///   - MenuCharacterStage > Zemin / Ayak Golgesi: ayaklari oturtma, gomulme,
    ///     kadrajdaki zemin payi, golge rengi ve boyu
    /// Zemin bir kez kurulur; sonra Kur ona dokunmaz.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string GroundMeshPath = "Assets/GameArt/Meshes/MenuZeminPanel.asset";
        private const int GroundVersion = 2;          // 2 = duz dikdortgen panel
        // "Zemin" adli baska UI parcalari da var; Hierarchy aramasinda karismasin.
        private const string GroundObjectName = "KarakterZemini";
        private const float PanelWidth = 1.5f;        // karakter boyuna oran
        private const float PanelDepth = 0.85f;
        private const float DefaultPitch = 18f;
        private const float DefaultFov = 30f;
        private const string GroundMaterialPath = "Assets/GameArt/Materials/MenuZemin.mat";

        private static bool EnsureMenuGround(MainMenuScreen screen)
        {
            bool changed = false;

            MenuCharacterStage stage = screen.GetComponentInChildren<MenuCharacterStage>(true);
            if (stage != null)
            {
                var serialized = new SerializedObject(stage);
                SerializedProperty groundProperty = serialized.FindProperty("ground");
                SerializedProperty version = serialized.FindProperty("groundVersion");
                var turntable = serialized.FindProperty("turntable").objectReferenceValue as Transform;

                if (groundProperty != null && version != null && turntable != null && version.intValue < GroundVersion)
                {
                    var existing = groundProperty.objectReferenceValue as Renderer;
                    if (existing == null)
                        groundProperty.objectReferenceValue = BuildMenuGround(turntable);
                    else
                        ConvertToPanel(existing, turntable); // eski yuvarlak zemin: materyal (renk) korunur

                    // Perspektif: hafif yukaridan, dogal derinlik (Inspector'dan degisir).
                    serialized.FindProperty("cameraPitch").floatValue = DefaultPitch;
                    serialized.FindProperty("cameraFov").floatValue = DefaultFov;
                    serialized.FindProperty("frameWholeGround").boolValue = true;
                    version.intValue = GroundVersion;
                    serialized.ApplyModifiedProperties();
                    changed = true;
                }
            }

            // Eski ad "Zemin" idi (UI'daki ayni adli parcalarla karisiyordu).
            if (stage != null && new SerializedObject(stage).FindProperty("ground").objectReferenceValue is Renderer named &&
                named.name == "Zemin")
            {
                Undo.RecordObject(named.gameObject, "Zemin adi");
                named.gameObject.name = GroundObjectName;
                changed = true;
            }

            // Eski duz golge resmi: golgeyi artik zemin veriyor.
            Transform oldShadow = screen.transform.Find("OrtaAlan/KarakterGolgesi");
            if (oldShadow != null && oldShadow.gameObject.activeSelf)
            {
                Undo.RecordObject(oldShadow.gameObject, "Menu golgesi");
                oldShadow.gameObject.SetActive(false);
                changed = true;
            }

            return changed;
        }

        private static Renderer BuildMenuGround(Transform turntable)
        {
            Transform stageRoot = turntable.parent != null ? turntable.parent : turntable;

            // Karakter boyu (edit modunda): zemin buna gore boyutlanir.
            float height = 1.7f;
            Renderer[] renderers = turntable.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.y > 0.1f)
                    height = bounds.size.y;
            }

            var groundObject = new GameObject(GroundObjectName) { layer = turntable.gameObject.layer };
            Undo.RegisterCreatedObjectUndo(groundObject, "Menu zemini");
            groundObject.transform.SetParent(stageRoot, false);
            groundObject.transform.position = turntable.position;
            groundObject.transform.localScale = PanelScale(height);

            groundObject.AddComponent<MeshFilter>().sharedMesh = LoadOrCreateGroundMesh();
            MeshRenderer renderer = groundObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadOrCreateGroundMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        private static Vector3 PanelScale(float characterHeight) =>
            new(characterHeight * PanelWidth, 1f, characterHeight * PanelDepth);

        private static float CharacterHeight(Transform turntable)
        {
            Renderer[] renderers = turntable.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return 1.7f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y > 0.1f ? bounds.size.y : 1.7f;
        }

        private static void ConvertToPanel(Renderer ground, Transform turntable)
        {
            Undo.RecordObject(ground.transform, "Zemin paneli");
            if (ground.TryGetComponent(out MeshFilter filter))
            {
                Undo.RecordObject(filter, "Zemin paneli");
                filter.sharedMesh = LoadOrCreateGroundMesh();
            }

            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = PanelScale(CharacterHeight(turntable));
        }

        private static Material LoadOrCreateGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("NewBattle/SoftToon");
            material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "MenuZemin"
            };

            // Menunun sari-turuncusuyla uyumlu, biraz koyu bir sahne zemini.
            SetIfPresent(material, "_BaseColor", Hex("#E39A3B"));
            SetIfPresent(material, "_VertexColorStrength", 1f); // kenar halkasi biraz koyu
            SetIfPresent(material, "_VertexColorSRGB", 1f);
            SetIfPresent(material, "_Ambient", 0.8f);
            SetIfPresent(material, "_RimStrength", 0.08f);

            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }

        private static void SetIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        /// <summary>
        /// Duz dikdortgen panel: x ve z [-0.5, 0.5], ust yuzey y = 0 (ayak hizasi),
        /// kalinlik 0.04. Ust yuzeyin kenarinda ince, biraz koyu bir cerceve seridi;
        /// yanlar daha koyu (panel kalinligi okunsun). Olcek: genislik x, derinlik z.
        /// </summary>
        private static Mesh LoadOrCreateGroundMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
            if (existing != null)
                return existing;

            const float half = 0.5f;
            const float border = 0.035f;
            const float thickness = 0.04f;
            Color top = Color.white;
            Color edge = new(0.84f, 0.84f, 0.84f, 1f);
            Color side = new(0.68f, 0.68f, 0.68f, 1f);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
            {
                // a-b-c-d: normalin tarafindan bakinca saat yonunde.
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int k = 0; k < 4; k++)
                {
                    normals.Add(normal);
                    colors.Add(color);
                }

                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            float inner = half - border;
            Vector3 up = Vector3.up;

            // Ust yuzey: ic alan + dort kenar seridi.
            Quad(new Vector3(-inner, 0, -inner), new Vector3(-inner, 0, inner), new Vector3(inner, 0, inner), new Vector3(inner, 0, -inner), up, top);
            Quad(new Vector3(-half, 0, -half), new Vector3(-half, 0, half), new Vector3(-inner, 0, half), new Vector3(-inner, 0, -half), up, edge);
            Quad(new Vector3(inner, 0, -half), new Vector3(inner, 0, half), new Vector3(half, 0, half), new Vector3(half, 0, -half), up, edge);
            Quad(new Vector3(-inner, 0, inner), new Vector3(-inner, 0, half), new Vector3(inner, 0, half), new Vector3(inner, 0, inner), up, edge);
            Quad(new Vector3(-inner, 0, -half), new Vector3(-inner, 0, -inner), new Vector3(inner, 0, -inner), new Vector3(inner, 0, -half), up, edge);

            // Yanlar (disaridan bakinca saat yonunde).
            float y0 = -thickness;
            Quad(new Vector3(half, y0, half), new Vector3(half, 0, half), new Vector3(-half, 0, half), new Vector3(-half, y0, half), Vector3.forward, side);
            Quad(new Vector3(-half, y0, -half), new Vector3(-half, 0, -half), new Vector3(half, 0, -half), new Vector3(half, y0, -half), Vector3.back, side);
            Quad(new Vector3(half, y0, -half), new Vector3(half, 0, -half), new Vector3(half, 0, half), new Vector3(half, y0, half), Vector3.right, side);
            Quad(new Vector3(-half, y0, half), new Vector3(-half, 0, half), new Vector3(-half, 0, -half), new Vector3(-half, y0, -half), Vector3.left, side);

            var mesh = new Mesh { name = "MenuZeminPanel" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, GroundMeshPath);
            return mesh;
        }
    }
}
#endif
