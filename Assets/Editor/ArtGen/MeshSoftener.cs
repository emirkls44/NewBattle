using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Keskin yuzlu (flat-shaded) mesh'lerin normallerini yumusatir.
    ///
    /// Aralarindaki aci esikten KUCUK olan komsu yuzler ortak normal paylasir;
    /// esikten buyuk olanlar keskin kalir. Boylece agac tepesi yuvarlak bir top
    /// gibi golgelenir ama bir evin 90 derecelik kosesi yine kose olarak okunur.
    ///
    /// Geometriye dokunmaz, sadece normalleri yeniden yazar. Hesap yuz
    /// geometrisinden yapildigi icin ayni mesh'e tekrar uygulamak ayni sonucu
    /// verir (idempotent); esigi degistirip yeniden calistirmak serbest.
    /// </summary>
    public static class MeshSoftener
    {
        /// <summary>Ayni noktadaki vertexleri eslestirirken kullanilan hassasiyet (metre).</summary>
        private const float PositionQuantum = 0.0001f;

        public static void SmoothNormals(Mesh mesh, float angleThreshold)
        {
            if (mesh == null || mesh.vertexCount == 0)
                return;

            Vector3[] vertices = mesh.vertices;
            List<int> triangles = new();

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                triangles.AddRange(mesh.GetTriangles(subMesh));

            int triangleCount = triangles.Count / 3;
            Vector3[] faceNormals = new Vector3[triangleCount];
            Vector3[] faceDirections = new Vector3[triangleCount];

            // Vertex -> onu kullanan ucgenler.
            List<int>[] vertexTriangles = new List<int>[vertices.Length];

            for (int t = 0; t < triangleCount; t++)
            {
                int a = triangles[t * 3];
                int b = triangles[t * 3 + 1];
                int c = triangles[t * 3 + 2];

                // Normalize edilmemis capraz carpim = alanla agirliklandirilmis normal.
                // Buyuk yuzler kucuk seritlerden daha cok soz sahibi olur.
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                faceNormals[t] = normal;
                faceDirections[t] = normal.sqrMagnitude > 1e-20f ? normal.normalized : Vector3.zero;

                (vertexTriangles[a] ??= new List<int>()).Add(t);
                (vertexTriangles[b] ??= new List<int>()).Add(t);
                (vertexTriangles[c] ??= new List<int>()).Add(t);
            }

            // Ayni konumdaki vertexler: flat-shaded mesh'te bir kosede birden
            // fazla vertex vardir, her yuz kendi kopyasini tasir.
            Dictionary<Vector3Int, List<int>> groups = new();

            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3Int key = Quantize(vertices[v]);
                if (!groups.TryGetValue(key, out List<int> list))
                    groups[key] = list = new List<int>();
                list.Add(v);
            }

            float cosThreshold = Mathf.Cos(Mathf.Clamp(angleThreshold, 0f, 180f) * Mathf.Deg2Rad);
            Vector3[] normals = new Vector3[vertices.Length];
            Vector3[] existing = mesh.normals;
            HashSet<int> visited = new();

            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 own = OwnNormal(v, vertexTriangles, faceDirections);

                if (own == Vector3.zero)
                {
                    normals[v] = existing.Length == vertices.Length ? existing[v] : Vector3.up;
                    continue;
                }

                visited.Clear();
                Vector3 sum = Vector3.zero;

                foreach (int neighbour in groups[Quantize(vertices[v])])
                {
                    if (vertexTriangles[neighbour] == null)
                        continue;

                    foreach (int t in vertexTriangles[neighbour])
                    {
                        if (!visited.Add(t))
                            continue;

                        if (Vector3.Dot(faceDirections[t], own) >= cosThreshold)
                            sum += faceNormals[t];
                    }
                }

                normals[v] = sum.sqrMagnitude > 1e-20f ? sum.normalized : own;
            }

            mesh.normals = normals;

            if (mesh.tangents != null && mesh.tangents.Length == vertices.Length)
                mesh.RecalculateTangents();
        }

        private static Vector3 OwnNormal(int vertex, List<int>[] vertexTriangles, Vector3[] faceDirections)
        {
            if (vertexTriangles[vertex] == null)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (int t in vertexTriangles[vertex])
                sum += faceDirections[t];

            return sum.sqrMagnitude > 1e-20f ? sum.normalized : Vector3.zero;
        }

        private static Vector3Int Quantize(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x / PositionQuantum),
                Mathf.RoundToInt(position.y / PositionQuantum),
                Mathf.RoundToInt(position.z / PositionQuantum));
        }
    }
}
