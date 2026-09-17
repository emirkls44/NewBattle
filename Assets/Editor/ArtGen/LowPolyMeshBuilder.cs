using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Prosedurel low-poly mesh uretimi icin temel yapi tasi.
    ///
    /// Iki tasarim karari onemli:
    /// 1) Her ucgen kendi 3 vertex'ini uretir (vertex paylasimi yok). Bu sayede normaller
    ///    her zaman yuz normalidir ve mesh otomatik olarak flat-shaded gorunur - gorsellerdeki
    ///    faceted stilin kaynagi budur. Low-poly mesh'lerde vertex sayisi zaten dusuk oldugu
    ///    icin bu israfin maliyeti yok denecek kadar azdir.
    /// 2) Renk vertex color kanalinda tasinir. Boylece butun harita tek materyal ile cizilir
    ///    (bkz. NewBattle/ToonVertexColor shader) ve mobilde draw call sayisi dibe iner.
    /// </summary>
    public class LowPolyMeshBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colors = new();
        private readonly List<BoneWeight> _boneWeights = new();
        private readonly List<int> _triangles = new();
        private readonly Stack<Matrix4x4> _matrixStack = new();

        private Matrix4x4 _matrix = Matrix4x4.identity;
        private int _activeBone;
        private bool _usesBones;

        public int VertexCount => _vertices.Count;
        public int TriangleCount => _triangles.Count / 3;

        #region Transform yigini

        public void Push() => _matrixStack.Push(_matrix);

        public void Pop() => _matrix = _matrixStack.Pop();

        public void Reset()
        {
            _matrix = Matrix4x4.identity;
            _matrixStack.Clear();
        }

        public void Translate(Vector3 offset) => _matrix *= Matrix4x4.Translate(offset);

        public void Translate(float x, float y, float z) => Translate(new Vector3(x, y, z));

        public void Rotate(Quaternion rotation) => _matrix *= Matrix4x4.Rotate(rotation);

        public void RotateEuler(float x, float y, float z) => Rotate(Quaternion.Euler(x, y, z));

        public void Scale(Vector3 scale) => _matrix *= Matrix4x4.Scale(scale);

        public void Scale(float uniform) => Scale(Vector3.one * uniform);

        /// <summary>Skinned mesh uretirken bundan sonra eklenen tum vertexlerin bagli olacagi kemik.</summary>
        public void SetBone(int boneIndex)
        {
            _activeBone = boneIndex;
            _usesBones = true;
        }

        #endregion

        #region Ilkel ekleme

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 ta = _matrix.MultiplyPoint3x4(a);
            Vector3 tb = _matrix.MultiplyPoint3x4(b);
            Vector3 tc = _matrix.MultiplyPoint3x4(c);

            Vector3 normal = Vector3.Cross(tb - ta, tc - ta);
            if (normal.sqrMagnitude < 1e-12f)
                return; // Dejenere ucgen: normali hesaplanamaz, atla.

            normal.Normalize();

            int baseIndex = _vertices.Count;
            PushVertex(ta, normal, color);
            PushVertex(tb, normal, color);
            PushVertex(tc, normal, color);

            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            AddTriangle(a, b, c, color);
            AddTriangle(a, c, d, color);
        }

        private void PushVertex(Vector3 position, Vector3 normal, Color color)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _colors.Add(color);

            if (_usesBones)
            {
                _boneWeights.Add(new BoneWeight
                {
                    boneIndex0 = _activeBone,
                    weight0 = 1f
                });
            }
        }

        #endregion

        #region Kutu ailesi

        public void AddBox(Vector3 center, Vector3 size, Color color)
        {
            AddTaperedBox(center, size, new Vector2(size.x, size.z), color);
        }

        /// <summary>
        /// Ust yuzu alt yuzden farkli olcude olabilen kutu. Catilar, agac govdeleri,
        /// konik uzuvlar ve kaya bloklari icin temel sekil.
        /// </summary>
        public void AddTaperedBox(Vector3 center, Vector3 size, Vector2 topSize, Color color)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;
            float tx = topSize.x * 0.5f;
            float tz = topSize.y * 0.5f;

            Vector3 b0 = center + new Vector3(-hx, -hy, -hz);
            Vector3 b1 = center + new Vector3(hx, -hy, -hz);
            Vector3 b2 = center + new Vector3(hx, -hy, hz);
            Vector3 b3 = center + new Vector3(-hx, -hy, hz);

            Vector3 t0 = center + new Vector3(-tx, hy, -tz);
            Vector3 t1 = center + new Vector3(tx, hy, -tz);
            Vector3 t2 = center + new Vector3(tx, hy, tz);
            Vector3 t3 = center + new Vector3(-tx, hy, tz);

            AddQuad(b0, b3, b2, b1, color); // alt
            AddQuad(t0, t1, t2, t3, color); // ust
            AddQuad(b0, b1, t1, t0, color); // -z
            AddQuad(b1, b2, t2, t1, color); // +x
            AddQuad(b2, b3, t3, t2, color); // +z
            AddQuad(b3, b0, t0, t3, color); // -x
        }

        /// <summary>Ust yuzu sivri olan ucgen prizma: klasik besik cati.</summary>
        public void AddPitchedRoof(Vector3 center, Vector3 size, Color color)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            Vector3 b0 = center + new Vector3(-hx, -hy, -hz);
            Vector3 b1 = center + new Vector3(hx, -hy, -hz);
            Vector3 b2 = center + new Vector3(hx, -hy, hz);
            Vector3 b3 = center + new Vector3(-hx, -hy, hz);

            Vector3 ridgeFront = center + new Vector3(0f, hy, -hz);
            Vector3 ridgeBack = center + new Vector3(0f, hy, hz);

            AddQuad(b0, b3, b2, b1, color);                // taban
            AddTriangle(b0, b1, ridgeFront, color);        // on alinlik
            AddTriangle(b2, b3, ridgeBack, color);         // arka alinlik
            AddQuad(b1, b2, ridgeBack, ridgeFront, color); // sag egim
            AddQuad(b3, b0, ridgeFront, ridgeBack, color); // sol egim
        }

        #endregion

        #region Silindir / koni / kure

        public void AddCylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height,
            int sides, Color color, bool capBottom = true, bool capTop = true)
        {
            sides = Mathf.Max(3, sides);
            Vector3 topCenter = baseCenter + Vector3.up * height;

            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;

                Vector3 db0 = new(Mathf.Cos(a0) * radiusBottom, 0f, Mathf.Sin(a0) * radiusBottom);
                Vector3 db1 = new(Mathf.Cos(a1) * radiusBottom, 0f, Mathf.Sin(a1) * radiusBottom);
                Vector3 dt0 = new(Mathf.Cos(a0) * radiusTop, 0f, Mathf.Sin(a0) * radiusTop);
                Vector3 dt1 = new(Mathf.Cos(a1) * radiusTop, 0f, Mathf.Sin(a1) * radiusTop);

                Vector3 p0 = baseCenter + db0;
                Vector3 p1 = baseCenter + db1;
                Vector3 p2 = topCenter + dt1;
                Vector3 p3 = topCenter + dt0;

                if (radiusTop <= 0.0001f)
                    AddTriangle(p0, p1, topCenter, color);
                else if (radiusBottom <= 0.0001f)
                    AddTriangle(baseCenter, p2, p3, color);
                else
                    AddQuad(p0, p1, p2, p3, color);

                if (capBottom && radiusBottom > 0.0001f)
                    AddTriangle(baseCenter, p1, p0, color);

                if (capTop && radiusTop > 0.0001f)
                    AddTriangle(topCenter, p3, p2, color);
            }
        }

        public void AddCone(Vector3 baseCenter, float radius, float height, int sides, Color color)
        {
            AddCylinder(baseCenter, radius, 0f, height, sides, color);
        }

        /// <summary>
        /// Faceted kure. Dusuk segment sayisi ile low-poly agac tepesi / karakter kafasi verir.
        /// noise &gt; 0 verilirse her halka rastgele bozulur: kaya ve cali formu icin.
        /// </summary>
        public void AddSphere(Vector3 center, float radius, int segments, int rings, Color color,
            float noise = 0f, int seed = 0, Vector3? squash = null)
        {
            segments = Mathf.Max(4, segments);
            rings = Mathf.Max(2, rings);

            Vector3 shape = squash ?? Vector3.one;
            System.Random random = new(seed);

            // Once tum vertex konumlarini uret, sonra ucgenle. Boylece komsu ucgenler
            // ayni bozulmus vertex'i kullanir ve mesh'te delik olusmaz.
            Vector3[,] grid = new Vector3[rings + 1, segments + 1];

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                float ringNoise = noise > 0f ? 1f + ((float)random.NextDouble() - 0.5f) * noise : 1f;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float theta = Mathf.PI * 2f * seg / segments;
                    float vertexNoise = noise > 0f ? 1f + ((float)random.NextDouble() - 0.5f) * noise * 0.6f : 1f;
                    float r = radius * ringNoise * vertexNoise;

                    Vector3 point = new(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * r * shape.x,
                        Mathf.Cos(phi) * r * shape.y,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * r * shape.z
                    );

                    // Son sutun ilk sutunla ayni olmali, yoksa dikis yerinde catlak olusur.
                    grid[ring, seg] = seg == segments ? grid[ring, 0] : center + point;
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    Vector3 a = grid[ring, seg];
                    Vector3 b = grid[ring, seg + 1];
                    Vector3 c = grid[ring + 1, seg + 1];
                    Vector3 d = grid[ring + 1, seg];

                    if (ring == 0)
                        AddTriangle(a, c, d, color);
                    else if (ring == rings - 1)
                        AddTriangle(a, b, c, color);
                    else
                        AddQuad(a, b, c, d, color);
                }
            }
        }

        /// <summary>Kapsul: karakter govdesi ve uzuvlari icin. Ust/alt yarim kure + silindir.</summary>
        public void AddCapsule(Vector3 baseCenter, float radius, float height, int sides, Color color)
        {
            float cylinderHeight = Mathf.Max(0f, height - radius * 2f);
            AddCylinder(baseCenter + Vector3.up * radius, radius, radius, cylinderHeight, sides, color, false, false);
            AddSphere(baseCenter + Vector3.up * radius, radius, sides, Mathf.Max(3, sides / 2), color,
                0f, 0, new Vector3(1f, 1f, 1f));
            AddSphere(baseCenter + Vector3.up * (radius + cylinderHeight), radius, sides, Mathf.Max(3, sides / 2), color);
        }

        #endregion

        #region Zemin / yuzey

        /// <summary>
        /// Yatay bir izgara yuzey uretir. heightFunction null ise duz zemin olur.
        /// Her hucre iki ucgene bolunur ve flat shading sayesinde faceted arazi gorunumu cikar.
        /// </summary>
        public void AddGrid(Vector3 origin, float width, float depth, int columns, int rows,
            System.Func<float, float, float> heightFunction, System.Func<float, float, Color> colorFunction)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            float cellWidth = width / columns;
            float cellDepth = depth / rows;

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float x0 = origin.x + x * cellWidth;
                    float x1 = x0 + cellWidth;
                    float z0 = origin.z + z * cellDepth;
                    float z1 = z0 + cellDepth;

                    float y00 = origin.y + (heightFunction?.Invoke(x0, z0) ?? 0f);
                    float y10 = origin.y + (heightFunction?.Invoke(x1, z0) ?? 0f);
                    float y11 = origin.y + (heightFunction?.Invoke(x1, z1) ?? 0f);
                    float y01 = origin.y + (heightFunction?.Invoke(x0, z1) ?? 0f);

                    Vector3 p00 = new(x0, y00, z0);
                    Vector3 p10 = new(x1, y10, z0);
                    Vector3 p11 = new(x1, y11, z1);
                    Vector3 p01 = new(x0, y01, z1);

                    float centerX = (x0 + x1) * 0.5f;
                    float centerZ = (z0 + z1) * 0.5f;
                    Color color = colorFunction?.Invoke(centerX, centerZ) ?? Color.white;

                    // Kosegen yonunu degistirerek izgara deseninin gozle secilmesini zorlastiriyoruz.
                    if ((x + z) % 2 == 0)
                    {
                        AddTriangle(p00, p11, p10, color);
                        AddTriangle(p00, p01, p11, color);
                    }
                    else
                    {
                        AddTriangle(p00, p01, p10, color);
                        AddTriangle(p10, p01, p11, color);
                    }
                }
            }
        }

        #endregion

        #region Cikti

        public Mesh Build(string meshName, bool optimize = true)
        {
            Mesh mesh = new()
            {
                name = meshName,
                indexFormat = _vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);

            if (_usesBones && _boneWeights.Count == _vertices.Count)
                mesh.boneWeights = _boneWeights.ToArray();

            mesh.RecalculateBounds();

            if (optimize)
                mesh.Optimize();

            mesh.UploadMeshData(false);
            return mesh;
        }

        #endregion
    }
}
