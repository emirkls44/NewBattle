using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Visual
{
    /// <summary>
    /// Kodla uretilen cizgi film ahsap sandik (kose renkli; SoftToon / ToonLoot ile).
    /// Birim boyut: x ve z [-0.5, 0.5], y [-1, 0] — ust yuzey y = 0'da, boylece
    /// sandigin ust yuzu karakterin ayak hizasina konur ve olceklenir.
    ///
    /// Parcalar: koyu ic govde (kalas aralari bundan gorunur), yanlarda yatay
    /// kalaslar, ustte kalaslar, 12 kenar kirisi ve yan yuzlerde capraz destek.
    /// </summary>
    public static class WoodenCrateMesh
    {
        public struct Palette
        {
            public Color PlankLight;
            public Color PlankDark;
            public Color Frame;
            public Color Gap;
        }

        public static Palette DefaultPalette => new()
        {
            PlankLight = new Color(0.83f, 0.58f, 0.32f, 1f),
            PlankDark = new Color(0.74f, 0.49f, 0.26f, 1f),
            Frame = new Color(0.55f, 0.33f, 0.16f, 1f),
            Gap = new Color(0.29f, 0.17f, 0.08f, 1f)
        };

        private const float Frame = 0.1f;      // kenar kirisi kalinligi
        private const float Inset = 0.035f;    // ic govdenin icerde durusu
        private const float PlankGap = 0.018f; // kalaslar arasi bosluk
        private const int SideRows = 3;
        private const int TopRows = 4;

        public static Mesh Build(Palette palette)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            void Box(Vector3 min, Vector3 max, Color color) =>
                AddBox(vertices, normals, colors, triangles, Matrix4x4.identity, min, max, color);

            const float h = 0.5f;
            float outer = h + 0.004f;

            // Ic govde (koyu): kalas aralarindan gorunur.
            Box(new Vector3(-h + Inset, -1f + Inset, -h + Inset), new Vector3(h - Inset, -Inset, h - Inset), palette.Gap);

            // Yan yuzlerde yatay kalaslar.
            float rowSpan = (1f - 2f * Frame - (SideRows - 1) * PlankGap) / SideRows;
            for (int face = 0; face < 4; face++)
            {
                Quaternion turn = Quaternion.Euler(0f, face * 90f, 0f);
                Matrix4x4 m = Matrix4x4.Rotate(turn);
                for (int row = 0; row < SideRows; row++)
                {
                    float y0 = -1f + Frame + row * (rowSpan + PlankGap);
                    Color color = (row + face) % 2 == 0 ? palette.PlankLight : palette.PlankDark;
                    AddBox(vertices, normals, colors, triangles, m,
                        new Vector3(-h + Frame, y0, h - Inset), new Vector3(h - Frame, y0 + rowSpan, h), color);
                }

                // Capraz destek: sol alt koseden sag ust koseye.
                Vector3 from = new(-h + Frame, -1f + Frame, h);
                Vector3 to = new(h - Frame, -Frame, h);
                Vector3 mid = (from + to) * 0.5f;
                Vector3 dir = to - from;
                float length = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Matrix4x4 brace = m * Matrix4x4.TRS(mid, Quaternion.Euler(0f, 0f, angle), Vector3.one);
                AddBox(vertices, normals, colors, triangles, brace,
                    new Vector3(-length * 0.5f, -Frame * 0.42f, 0f), new Vector3(length * 0.5f, Frame * 0.42f, 0.006f),
                    palette.Frame);
            }

            // Ust kalaslar (x boyunca, z'de siralanir).
            float topSpan = (1f - 2f * Frame - (TopRows - 1) * PlankGap) / TopRows;
            for (int row = 0; row < TopRows; row++)
            {
                float z0 = -h + Frame + row * (topSpan + PlankGap);
                Color color = row % 2 == 0 ? palette.PlankLight : palette.PlankDark;
                Box(new Vector3(-h + Frame, -Inset, z0), new Vector3(h - Frame, 0f, z0 + topSpan), color);
            }

            // 12 kenar kirisi (en distaki parcalar).
            float f = Frame;
            // Dikey kenarlar
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Box(new Vector3(x < 0 ? -outer : outer - f, -1f, z < 0 ? -outer : outer - f),
                    new Vector3(x < 0 ? -outer + f : outer, 0.004f, z < 0 ? -outer + f : outer), palette.Frame);
            }

            // Yatay kenarlar (alt ve ust, x ve z boyunca)
            foreach (float y in new[] { -1f, -f + 0.004f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    Box(new Vector3(-outer + f, y, z < 0 ? -outer : outer - f),
                        new Vector3(outer - f, y + f, z < 0 ? -outer + f : outer), palette.Frame);
                }

                foreach (float x in new[] { -1f, 1f })
                {
                    Box(new Vector3(x < 0 ? -outer : outer - f, y, -outer + f),
                        new Vector3(x < 0 ? -outer + f : outer, y + f, outer - f), palette.Frame);
                }
            }

            var mesh = new Mesh { name = "AhsapSandik" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles,
            Matrix4x4 transform, Vector3 min, Vector3 max, Color color)
        {
            Vector3[] corners =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z), new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z), new(max.x, max.y, max.z), new(min.x, max.y, max.z)
            };

            // Her yuz: dort kose (disaridan bakinca saat yonunde) + normal.
            int[,] faces =
            {
                { 4, 5, 6, 7 }, // +z
                { 1, 0, 3, 2 }, // -z
                { 5, 1, 2, 6 }, // +x
                { 0, 4, 7, 3 }, // -x
                { 3, 7, 6, 2 }, // +y
                { 0, 1, 5, 4 }  // -y
            };
            Vector3[] faceNormals =
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down
            };

            for (int face = 0; face < 6; face++)
            {
                int start = vertices.Count;
                Vector3 normal = transform.MultiplyVector(faceNormals[face]).normalized;
                for (int k = 0; k < 4; k++)
                {
                    vertices.Add(transform.MultiplyPoint3x4(corners[faces[face, k]]));
                    normals.Add(normal);
                    colors.Add(color);
                }

                // Koseler disaridan bakinca saat yonunde: Unity'nin on yuz sirasi.
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }
    }
}
