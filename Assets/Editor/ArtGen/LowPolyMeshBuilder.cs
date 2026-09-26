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

        /// <summary>
        /// Normalleri elle verilen ucgen. Yuvarlak yuzeyler (silindir govdesi,
        /// kure, pah) komsu ucgenlerle ayni normali paylasinca yumusak golgelenir.
        /// Sarim yonu AddTriangle ile ayni olmali: normaller disari bakar.
        /// </summary>
        public void AddSmoothTriangle(Vector3 a, Vector3 b, Vector3 c,
            Vector3 normalA, Vector3 normalB, Vector3 normalC, Color color)
        {
            Vector3 ta = _matrix.MultiplyPoint3x4(a);
            Vector3 tb = _matrix.MultiplyPoint3x4(b);
            Vector3 tc = _matrix.MultiplyPoint3x4(c);

            if (Vector3.Cross(tb - ta, tc - ta).sqrMagnitude < 1e-12f)
                return;

            // Olcekli bir matriste normal, noktayla ayni matrisle donusturulmez;
            // ters-devrigi gerekir, yoksa basik bir kurede normaller egilir.
            Matrix4x4 normalMatrix = _matrix.inverse.transpose;

            int baseIndex = _vertices.Count;
            PushVertex(ta, normalMatrix.MultiplyVector(normalA).normalized, color);
            PushVertex(tb, normalMatrix.MultiplyVector(normalB).normalized, color);
            PushVertex(tc, normalMatrix.MultiplyVector(normalC).normalized, color);

            _triangles.Add(baseIndex);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
        }

        public void AddSmoothQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 normalA, Vector3 normalB, Vector3 normalC, Vector3 normalD, Color color)
        {
            AddSmoothTriangle(a, b, c, normalA, normalB, normalC, color);
            AddSmoothTriangle(a, c, d, normalA, normalC, normalD, color);
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

            // Unity'de on yuz, disaridan bakinca saat yonunde dizilen yuzdur
            // (GlbImporter da glTF'i bu kurala cevirir). Onceki surum bu yuzleri
            // ters diziyordu: kutu disaridan bakinca icini gosteriyordu - ust yuz
            // kirpiliyor, yerine tabanin ic yuzu gorunuyordu.
            AddQuad(b0, b1, b2, b3, color); // alt
            AddQuad(t0, t3, t2, t1, color); // ust
            AddQuad(b0, t0, t1, b1, color); // -z
            AddQuad(b1, t1, t2, b2, color); // +x
            AddQuad(b2, t2, t3, b3, color); // +z
            AddQuad(b3, t3, t0, b0, color); // -x
        }

        /// <summary>
        /// Kenar ve koseleri yuvarlatilmis kutu: "oyuncak" hissinin temel sekli.
        ///
        /// Kup yuzeyindeki her nokta, kutunun radius kadar iceri cekilmis
        /// cekirdegine gore disari itilir. Duz yuzler tam duz kalir (normal = yuz
        /// normali), sadece pah seridi yumusak golgelenir; buyuk yuzlerde
        /// istenmeyen golge gecisi olusmaz.
        /// </summary>
        public void AddRoundedBox(Vector3 center, Vector3 size, float radius, Color color, int bevelSegments = 2)
        {
            Vector3 half = size * 0.5f;
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.999f);

            if (r <= 0.0001f)
            {
                AddBox(center, size, color);
                return;
            }

            Vector3 core = half - Vector3.one * r;
            float[] xs = BevelSamples(half.x, r, bevelSegments);
            float[] ys = BevelSamples(half.y, r, bevelSegments);
            float[] zs = BevelSamples(half.z, r, bevelSegments);

            // Her yuz: sabit eksen, iki gezen eksen. Sira, yuzun disa bakmasini saglar.
            AddRoundedFace(center, core, r, color, 0, 1, xs, ys, zs);  // +X
            AddRoundedFace(center, core, r, color, 0, -1, xs, ys, zs); // -X
            AddRoundedFace(center, core, r, color, 1, 1, xs, ys, zs);  // +Y
            AddRoundedFace(center, core, r, color, 1, -1, xs, ys, zs); // -Y
            AddRoundedFace(center, core, r, color, 2, 1, xs, ys, zs);  // +Z
            AddRoundedFace(center, core, r, color, 2, -1, xs, ys, zs); // -Z
        }

        /// <summary>
        /// Bir eksen boyunca ornek noktalari: pah bolgesinde acisal olarak esit
        /// aralikli, duz bolgede sadece iki uc. Kup-kure eslemesinde bir yuzun
        /// kenari pahin 45 derecesine denk gelir; iki komsu yuz birlikte 90'i tamamlar.
        /// </summary>
        private static float[] BevelSamples(float half, float radius, int segments)
        {
            segments = Mathf.Max(1, segments);
            float flat = half - radius;
            List<float> samples = new();

            for (int i = segments; i >= 1; i--)
            {
                float angle = 45f * i / segments * Mathf.Deg2Rad;
                samples.Add(-flat - radius * Mathf.Tan(angle));
            }

            samples.Add(-flat);

            if (flat > 0.0001f)
                samples.Add(flat);

            for (int i = 1; i <= segments; i++)
            {
                float angle = 45f * i / segments * Mathf.Deg2Rad;
                samples.Add(flat + radius * Mathf.Tan(angle));
            }

            return samples.ToArray();
        }

        private void AddRoundedFace(Vector3 center, Vector3 core, float radius, Color color,
            int axis, int sign, float[] xs, float[] ys, float[] zs)
        {
            float[][] samples = { xs, ys, zs };

            // Dortgen a -> b boyunca v'de, a -> c boyunca u+v'de ilerler; on yuz
            // normali Cross(v, u) yonundedir. Donguler (u = eksen+1, v = eksen+2)
            // icin Cross(v, u) = -eksen; arti yuzde eksenleri degistirip disari
            // baktiriyoruz.
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;
            if (sign > 0)
                (u, v) = (v, u);

            float[] us = samples[u];
            float[] vs = samples[v];
            float faceCoordinate = sign * (core[axis] + radius);

            Vector3[,] points = new Vector3[us.Length, vs.Length];
            Vector3[,] normals = new Vector3[us.Length, vs.Length];

            for (int i = 0; i < us.Length; i++)
            {
                for (int j = 0; j < vs.Length; j++)
                {
                    Vector3 onCube = Vector3.zero;
                    onCube[axis] = faceCoordinate;
                    onCube[u] = us[i];
                    onCube[v] = vs[j];

                    Vector3 inner = new(
                        Mathf.Clamp(onCube.x, -core.x, core.x),
                        Mathf.Clamp(onCube.y, -core.y, core.y),
                        Mathf.Clamp(onCube.z, -core.z, core.z));

                    Vector3 normal = (onCube - inner).normalized;
                    points[i, j] = center + inner + normal * radius;
                    normals[i, j] = normal;
                }
            }

            for (int i = 0; i < us.Length - 1; i++)
            {
                for (int j = 0; j < vs.Length - 1; j++)
                {
                    AddSmoothQuad(
                        points[i, j], points[i, j + 1], points[i + 1, j + 1], points[i + 1, j],
                        normals[i, j], normals[i, j + 1], normals[i + 1, j + 1], normals[i + 1, j],
                        color);
                }
            }
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

            // Sarim yonu AddTaperedBox'taki duzeltmeyle ayni: disaridan saat yonu.
            AddQuad(b0, b1, b2, b3, color);                // taban
            AddTriangle(b0, ridgeFront, b1, color);        // on alinlik
            AddTriangle(b2, ridgeBack, b3, color);         // arka alinlik
            AddQuad(b1, ridgeFront, ridgeBack, b2, color); // sag egim
            AddQuad(b3, ridgeBack, ridgeFront, b0, color); // sol egim
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

                // Disaridan saat yonu (bkz. AddTaperedBox). Aci arttikca nokta
                // ustten bakista saat yonunun TERSINE ilerler; bu yuzden sira
                // p0 -> p3 -> p2 -> p1.
                if (radiusTop <= 0.0001f)
                    AddTriangle(p0, topCenter, p1, color);
                else if (radiusBottom <= 0.0001f)
                    AddTriangle(baseCenter, p3, p2, color);
                else
                    AddQuad(p0, p3, p2, p1, color);

                if (capBottom && radiusBottom > 0.0001f)
                    AddTriangle(baseCenter, p0, p1, color);

                if (capTop && radiusTop > 0.0001f)
                    AddTriangle(topCenter, p2, p3, color);
            }
        }

        /// <summary>
        /// Govdesi yumusak golgelenen silindir / koni. Kapaklar duz kalir, boylece
        /// kenar hala okunur ama govde "yuvarlak" gorunur. Mermi, namlu, sise icin.
        /// </summary>
        public void AddSmoothCylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height,
            int sides, Color color, bool capBottom = true, bool capTop = true)
        {
            sides = Mathf.Max(3, sides);
            Vector3 topCenter = baseCenter + Vector3.up * height;

            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;

                Vector3 d0 = new(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 p0 = baseCenter + d0 * radiusBottom;
                Vector3 p1 = baseCenter + d1 * radiusBottom;
                Vector3 p2 = topCenter + d1 * radiusTop;
                Vector3 p3 = topCenter + d0 * radiusTop;

                // Egimli govdede normal biraz yukari (ya da asagi) bakar.
                float slope = radiusBottom - radiusTop;
                Vector3 n0 = (d0 * height + Vector3.up * slope).normalized;
                Vector3 n1 = (d1 * height + Vector3.up * slope).normalized;

                if (radiusTop <= 0.0001f)
                    AddSmoothTriangle(p0, topCenter, p1, n0, (n0 + n1).normalized, n1, color);
                else if (radiusBottom <= 0.0001f)
                    AddSmoothTriangle(baseCenter, p3, p2, (n0 + n1).normalized, n0, n1, color);
                else
                    AddSmoothQuad(p0, p3, p2, p1, n0, n0, n1, n1, color);

                if (capBottom && radiusBottom > 0.0001f)
                    AddTriangle(baseCenter, p0, p1, color);

                if (capTop && radiusTop > 0.0001f)
                    AddTriangle(topCenter, p2, p3, color);
            }
        }

        /// <summary>Yumusak golgelenen kure / elipsoit. squash ile basiklastirilabilir.</summary>
        public void AddSmoothSphere(Vector3 center, float radius, int segments, int rings, Color color,
            Vector3? squash = null)
        {
            segments = Mathf.Max(4, segments);
            rings = Mathf.Max(2, rings);

            Vector3 shape = squash ?? Vector3.one;
            Vector3[,] grid = new Vector3[rings + 1, segments + 1];
            Vector3[,] normals = new Vector3[rings + 1, segments + 1];

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float theta = Mathf.PI * 2f * (seg % segments) / segments;
                    Vector3 unit = new(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    grid[ring, seg] = center + Vector3.Scale(unit, shape) * radius;

                    // Elipsoidin normali: birim yon, sekil olcusune bolunur.
                    normals[ring, seg] = new Vector3(unit.x / shape.x, unit.y / shape.y, unit.z / shape.z).normalized;
                }
            }

            // AddSphere ile ayni sira: disaridan saat yonu.
            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    Vector3 a = grid[ring, seg];
                    Vector3 b = grid[ring, seg + 1];
                    Vector3 c = grid[ring + 1, seg + 1];
                    Vector3 d = grid[ring + 1, seg];

                    Vector3 na = normals[ring, seg];
                    Vector3 nb = normals[ring, seg + 1];
                    Vector3 nc = normals[ring + 1, seg + 1];
                    Vector3 nd = normals[ring + 1, seg];

                    if (ring == 0)
                        AddSmoothTriangle(a, c, d, na, nc, nd, color);
                    else if (ring == rings - 1)
                        AddSmoothTriangle(a, b, c, na, nb, nc, color);
                    else
                        AddSmoothQuad(a, b, c, d, na, nb, nc, nd, color);
                }
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
            WeldIdenticalVertices();

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

        /// <summary>
        /// Konumu, normali, rengi ve kemigi BIREBIR ayni olan vertexleri birlestirir.
        ///
        /// Her ucgen kendi vertexlerini urettigi icin bir dortgenin iki ucgeni ayni
        /// koseyi iki kez tasir; yumusak yuzeylerde komsu ucgenler de ayni vertexi
        /// tekrarlar. Birlestirmek goruntuyu degistirmez (ayni veri ayni sonucu
        /// verir) ama yuvarlak modellerde vertex sayisini yaklasik dortte birine indirir.
        /// Farkli normalli koseler - keskin kenarlar - ayri kalir.
        /// </summary>
        private void WeldIdenticalVertices()
        {
            Dictionary<(Vector3Int, Vector3Int, Color32, int), int> lookup = new();
            List<Vector3> vertices = new(_vertices.Count);
            List<Vector3> normals = new(_vertices.Count);
            List<Color> colors = new(_vertices.Count);
            List<BoneWeight> boneWeights = new(_boneWeights.Count);
            int[] remap = new int[_vertices.Count];

            for (int i = 0; i < _vertices.Count; i++)
            {
                int bone = _usesBones && i < _boneWeights.Count ? _boneWeights[i].boneIndex0 : 0;
                var key = (
                    Vector3Int.RoundToInt(_vertices[i] * 100000f),
                    Vector3Int.RoundToInt(_normals[i] * 10000f),
                    (Color32)_colors[i],
                    bone);

                if (!lookup.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    lookup[key] = index;
                    vertices.Add(_vertices[i]);
                    normals.Add(_normals[i]);
                    colors.Add(_colors[i]);

                    if (_usesBones && i < _boneWeights.Count)
                        boneWeights.Add(_boneWeights[i]);
                }

                remap[i] = index;
            }

            for (int i = 0; i < _triangles.Count; i++)
                _triangles[i] = remap[_triangles[i]];

            _vertices.Clear();
            _vertices.AddRange(vertices);
            _normals.Clear();
            _normals.AddRange(normals);
            _colors.Clear();
            _colors.AddRange(colors);
            _boneWeights.Clear();
            _boneWeights.AddRange(boneWeights);
        }

        #endregion
    }
}
