#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ana menu: karakterin bastigi zemin. Hepsi elle ayarlanabilir:
    ///   - Hierarchy > MenuKarakterSahnesi > Zemin: tasi / olcekle / dondur
    ///     (modeli istersen MeshFilter'dan baska bir sekille degistir)
    ///   - Assets/GameArt/Materials/MenuZemin: rengi (Tint), golgesi, parlakligi
    ///   - MenuCharacterStage > Zemin / Ayak Golgesi: ayaklari oturtma, gomulme,
    ///     kadrajdaki zemin payi, golge rengi ve boyu
    /// Zemin bir kez kurulur; sonra Kur ona dokunmaz.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string GroundMeshPath = "Assets/GameArt/Meshes/MenuZemin.asset";
        private const string GroundMaterialPath = "Assets/GameArt/Materials/MenuZemin.mat";

        private static bool EnsureMenuGround(MainMenuScreen screen)
        {
            bool changed = false;

            MenuCharacterStage stage = screen.GetComponentInChildren<MenuCharacterStage>(true);
            if (stage != null)
            {
                var serialized = new SerializedObject(stage);
                SerializedProperty groundProperty = serialized.FindProperty("ground");
                var turntable = serialized.FindProperty("turntable").objectReferenceValue as Transform;

                if (groundProperty != null && groundProperty.objectReferenceValue == null && turntable != null)
                {
                    Renderer ground = BuildMenuGround(turntable);
                    groundProperty.objectReferenceValue = ground;

                    // Zeminin ustu gorunsun: kamera biraz yukaridan baksin (Inspector'dan degisir).
                    SerializedProperty pitch = serialized.FindProperty("cameraPitch");
                    if (pitch != null && pitch.floatValue < 14f)
                        pitch.floatValue = 14f;

                    serialized.ApplyModifiedProperties();
                    changed = true;
                }
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

            var groundObject = new GameObject("Zemin") { layer = turntable.gameObject.layer };
            Undo.RegisterCreatedObjectUndo(groundObject, "Menu zemini");
            groundObject.transform.SetParent(stageRoot, false);
            groundObject.transform.position = turntable.position;
            groundObject.transform.localScale = new Vector3(height * 1.3f, height * 0.5f, height * 1.3f);

            groundObject.AddComponent<MeshFilter>().sharedMesh = LoadOrCreateGroundMesh();
            MeshRenderer renderer = groundObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadOrCreateGroundMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
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
        /// Yuvarlak, hafif kalin zemin: ust yuzey y = 0 (ayak hizasi), yaricap 0.5,
        /// kalinlik 0.1. Ust yuzeyin kenarinda yumusak pah; kenar koseleri biraz koyu.
        /// </summary>
        private static Mesh LoadOrCreateGroundMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
            if (existing != null)
                return existing;

            const int segments = 72;
            const float radius = 0.5f;
            const float bevelInner = 0.46f;
            const float bevelDrop = 0.012f;
            const float thickness = 0.1f;
            Color top = Color.white;
            Color rim = new(0.86f, 0.86f, 0.86f, 1f);
            Color side = new(0.7f, 0.7f, 0.7f, 1f);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            // Ust disk: merkez + ic halka.
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.up);
            colors.Add(top);
            for (int i = 0; i <= segments; i++)
            {
                Vector3 dir = Direction(i, segments);
                vertices.Add(dir * bevelInner);
                normals.Add(Vector3.up);
                colors.Add(top);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
            }

            // Pah: ic halkadan dis kenara hafif egim.
            int bevelStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 dir = Direction(i, segments);
                Vector3 normal = (Vector3.up + dir * 0.6f).normalized;
                vertices.Add(dir * bevelInner);
                normals.Add(normal);
                colors.Add(top);
                vertices.Add(dir * radius + Vector3.down * bevelDrop);
                normals.Add(normal);
                colors.Add(rim);
            }

            AddBand(triangles, bevelStart, segments);

            // Yan yuz.
            int sideStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 dir = Direction(i, segments);
                vertices.Add(dir * radius + Vector3.down * bevelDrop);
                normals.Add(dir);
                colors.Add(side);
                vertices.Add(dir * radius + Vector3.down * thickness);
                normals.Add(dir);
                colors.Add(side * 0.85f);
            }

            AddBand(triangles, sideStart, segments);

            var mesh = new Mesh { name = "MenuZemin" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, GroundMeshPath);
            return mesh;
        }

        private static Vector3 Direction(int index, int segments)
        {
            float angle = index / (float)segments * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>(ust, alt) kose ciftlerinden olusan halka seridini ucgenler (disaridan saat yonu).</summary>
        private static void AddBand(List<int> triangles, int start, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;     // ust
                int b = a + 1;             // alt
                int c = a + 2;             // sonraki ust
                int d = a + 3;             // sonraki alt
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
                triangles.Add(b);
            }
        }
    }
}
#endif
