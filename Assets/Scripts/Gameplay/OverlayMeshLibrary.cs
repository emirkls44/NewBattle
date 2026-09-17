using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Zemin gostergeleri icin paylasilan mesh ve materyal havuzu.
    ///
    /// Halka ve ayak izi mesh'leri calisma aninda bir kez uretilip butun oyuncular
    /// arasinda paylasilir - 16 oyuncu icin 16 ayri mesh tahsis etmenin anlami yok.
    ///
    /// Halkanin UV.x kanali aciyi 0..1 arasinda tasir (saat 12'den saat yonunde).
    /// ToonOverlay shader'i bunu kullanarak tek bir float ile radyal dolum yapar;
    /// drop kutusunun "3 saniye bekle" halkasi da ayni mesh'i kullanacak.
    /// </summary>
    public static class OverlayMeshLibrary
    {
        private const string OverlayShaderName = "NewBattle/ToonOverlay";

        private static Mesh _ringMesh;
        private static Mesh _discMesh;
        private static Mesh _footprintMesh;
        private static Material _overlayMaterial;

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int FillId = Shader.PropertyToID("_Fill");

        /// <summary>Ic yaricapi 0.78 olan ince halka. Oyuncu cemberi ve dolum halkasi.</summary>
        public static Mesh Ring => _ringMesh ??= BuildRing(0.80f, 1f, 48);

        /// <summary>Dolu daire. Yumusak gecisli golge / vurgu lekesi icin.</summary>
        public static Mesh Disc => _discMesh ??= BuildRing(0f, 1f, 32);

        public static Mesh Footprint => _footprintMesh ??= BuildFootprint();

        public static Material OverlayMaterial
        {
            get
            {
                if (_overlayMaterial != null)
                    return _overlayMaterial;

                Shader shader = Shader.Find(OverlayShaderName);

                if (shader == null)
                {
                    Debug.LogError(
                        $"OverlayMeshLibrary: '{OverlayShaderName}' bulunamadi. " +
                        "Assets/Shaders/ToonOverlay.shader derlenmis mi?");
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                _overlayMaterial = new Material(shader)
                {
                    name = "OverlayShared",
                    enableInstancing = true
                };

                return _overlayMaterial;
            }
        }

        /// <summary>
        /// XZ duzleminde yatan halka. innerRadius 0 verilirse dolu daire olur.
        /// </summary>
        private static Mesh BuildRing(float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(8, segments);

            bool solid = innerRadius <= 0.0001f;
            int ringVertexCount = solid ? segments + 2 : (segments + 1) * 2;

            Vector3[] vertices = new Vector3[ringVertexCount];
            Vector2[] uvs = new Vector2[ringVertexCount];
            Color[] colors = new Color[ringVertexCount];
            int[] triangles = new int[segments * (solid ? 3 : 6)];

            if (solid)
            {
                vertices[0] = Vector3.zero;
                uvs[0] = new Vector2(0f, 0f);
                colors[0] = Color.white;

                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float angle = -t * Mathf.PI * 2f + Mathf.PI * 0.5f;

                    vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * outerRadius;
                    uvs[i + 1] = new Vector2(t, 1f);
                    colors[i + 1] = Color.white;
                }

                for (int i = 0; i < segments; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }
            else
            {
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;

                    // Saat 12'den baslayip saat yonunde ilerle: dolum gostergesi
                    // boyle daha dogal okunuyor.
                    float angle = -t * Mathf.PI * 2f + Mathf.PI * 0.5f;
                    Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                    vertices[i * 2] = direction * innerRadius;
                    vertices[i * 2 + 1] = direction * outerRadius;

                    uvs[i * 2] = new Vector2(t, 0f);
                    uvs[i * 2 + 1] = new Vector2(t, 1f);

                    colors[i * 2] = Color.white;
                    colors[i * 2 + 1] = Color.white;
                }

                for (int i = 0; i < segments; i++)
                {
                    int v = i * 2;
                    int t = i * 6;

                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 3;

                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 3;
                    triangles[t + 5] = v + 2;
                }
            }

            Mesh mesh = new() { name = solid ? "OverlayDisc" : "OverlayRing" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Basit ayak izi: one dogru hafif sivrilen kucuk dortgen.</summary>
        private static Mesh BuildFootprint()
        {
            const float halfWidth = 0.055f;
            const float halfLength = 0.10f;

            Vector3[] vertices =
            {
                new(-halfWidth, 0f, -halfLength),
                new(halfWidth, 0f, -halfLength),
                new(halfWidth * 0.72f, 0f, halfLength),
                new(-halfWidth * 0.72f, 0f, halfLength)
            };

            Mesh mesh = new() { name = "OverlayFootprint" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 0f)
            });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 3, 2, 0, 2, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Overlay mesh'i cizen hazir bir GameObject kurar.</summary>
        public static MeshRenderer CreateOverlayObject(string objectName, Mesh mesh, Transform parent,
            Vector3 localPosition, float scale)
        {
            GameObject overlay = new(objectName);
            overlay.transform.SetParent(parent, false);
            overlay.transform.localPosition = localPosition;
            overlay.transform.localScale = Vector3.one * scale;

            // PlayerVisibility govde renderer'larini tararken bunu atlasin.
            overlay.AddComponent<OverlayVisual>();

            MeshFilter filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OverlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return renderer;
        }
    }
}
