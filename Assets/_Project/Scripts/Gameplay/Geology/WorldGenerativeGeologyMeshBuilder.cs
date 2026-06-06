using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Gameplay.Geology
{
    public static class WorldGenerativeGeologyMeshBuilder
    {
        private const float DegenerateTriangleEpsilon = 0.000001f;
        private const float ContactShadowLowerBand01 = 0.2f;

        public static Mesh DecimateWithSilhouetteProtection(Mesh sourceMesh, float targetReductionRatio)
        {
            if (sourceMesh == null)
                return null;

            Mesh result = CloneReadableMesh(sourceMesh, "_SilhouetteDecimated");
            Vector3[] vertices = result.vertices;
            int[] triangles = result.triangles;
            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
                return result;

            float reduction = Mathf.Clamp01(targetReductionRatio);
            if (reduction <= 0f)
                return result;

            int triangleCount = triangles.Length / 3;
            Dictionary<ulong, int> edgeUseCounts = BuildEdgeUseCounts(vertices, triangles);
            bool[] protectedTriangles = ResolveProtectedTriangleMask(vertices, triangles, edgeUseCounts);
            int protectedCount = 0;
            for (int i = 0; i < protectedTriangles.Length; i++)
            {
                if (protectedTriangles[i])
                    protectedCount++;
            }

            int targetTriangleCount = Mathf.Max(protectedCount, Mathf.CeilToInt(triangleCount * (1f - reduction)));
            if (targetTriangleCount >= triangleCount)
                return result;

            int mutableKeepBudget = Mathf.Max(0, targetTriangleCount - protectedCount);
            List<int> kept = new List<int>(targetTriangleCount * 3);
            int mutableSeen = 0;
            int mutableKept = 0;
            int mutableTotal = CountMutableTriangles(vertices, triangles, protectedTriangles);

            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int offset = triangleIndex * 3;
                if (!IsValidTriangle(vertices, triangles[offset], triangles[offset + 1], triangles[offset + 2]))
                    continue;

                if (protectedTriangles[triangleIndex])
                {
                    AppendTriangle(kept, triangles, triangleIndex);
                    continue;
                }

                bool keep = mutableKeepBudget > 0 &&
                            mutableKept < mutableKeepBudget &&
                            ShouldKeepMutableTriangle(mutableSeen, mutableKept, mutableTotal, mutableKeepBudget);
                mutableSeen++;
                if (!keep)
                    continue;

                mutableKept++;
                AppendTriangle(kept, triangles, triangleIndex);
            }

            if (kept.Count >= 3)
            {
                result.triangles = kept.ToArray();
                result.RecalculateBounds();
                result.RecalculateNormals();
            }

            return result;
        }

        public static void BakeContactShadowsToVertexColor(Mesh targetMesh, Transform proxyGroundPlane)
        {
            if (targetMesh == null)
                return;

            Vector3[] vertices = targetMesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return;

            Vector3[] normals = targetMesh.normals;
            if (normals == null || normals.Length != vertices.Length)
            {
                targetMesh.RecalculateNormals();
                normals = targetMesh.normals;
            }

            Color[] colors = targetMesh.colors;
            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = Color.white;
            }

            targetMesh.RecalculateBounds();
            Bounds bounds = targetMesh.bounds;
            float height = Mathf.Max(0.0001f, bounds.size.y);
            float lowerBandTop = bounds.min.y + height * ContactShadowLowerBand01;
            Vector3 planeNormal = proxyGroundPlane != null ? proxyGroundPlane.up.normalized : Vector3.up;
            Vector3 planePoint = proxyGroundPlane != null ? proxyGroundPlane.position : Vector3.zero;

            for (int i = 0; i < vertices.Length; i++)
            {
                float lowerBand01 = Mathf.InverseLerp(lowerBandTop, bounds.min.y, vertices[i].y);
                float planeDistance = Mathf.Abs(Vector3.Dot(vertices[i] - planePoint, planeNormal));
                float contact01 = Mathf.Clamp01(1f - planeDistance / Mathf.Max(0.0001f, height * 0.25f));
                float downwardNormal01 = normals != null && normals.Length == vertices.Length
                    ? Mathf.Clamp01(Vector3.Dot(-normals[i].normalized, planeNormal) * 0.5f + 0.5f)
                    : 0.5f;
                float occlusion01 = Mathf.Clamp01(lowerBand01 * Mathf.Lerp(0.35f, 1f, contact01) * downwardNormal01);
                colors[i].b = Mathf.Lerp(1f, 0.22f, occlusion01);
            }

            DiffuseVertexColors(colors, targetMesh.triangles, 3);
            targetMesh.colors = colors;
        }

        public static float[,,] ConstructiveSolidGeometryUnion(float[,,] sdfGridA, float[,,] sdfGridB)
        {
            if (sdfGridA == null)
                return CloneSdfGrid(sdfGridB);
            if (sdfGridB == null)
                return CloneSdfGrid(sdfGridA);

            int sizeX = Mathf.Min(sdfGridA.GetLength(0), sdfGridB.GetLength(0));
            int sizeY = Mathf.Min(sdfGridA.GetLength(1), sdfGridB.GetLength(1));
            int sizeZ = Mathf.Min(sdfGridA.GetLength(2), sdfGridB.GetLength(2));
            float[,,] mergedGrid = new float[sizeX, sizeY, sizeZ];

            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                        mergedGrid[x, y, z] = Mathf.Min(sdfGridA[x, y, z], sdfGridB[x, y, z]);
                }
            }

            return mergedGrid;
        }

        private static float[,,] CloneSdfGrid(float[,,] source)
        {
            if (source == null)
                return null;

            int sizeX = source.GetLength(0);
            int sizeY = source.GetLength(1);
            int sizeZ = source.GetLength(2);
            float[,,] clone = new float[sizeX, sizeY, sizeZ];
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                        clone[x, y, z] = source[x, y, z];
                }
            }

            return clone;
        }

        private static Mesh CloneReadableMesh(Mesh source, string suffix)
        {
            Mesh clone = new Mesh();
            clone.name = string.IsNullOrEmpty(source.name) ? "GeologyMesh" + suffix : source.name + suffix;
            clone.vertices = source.vertices;
            clone.normals = source.normals;
            clone.tangents = source.tangents;
            clone.uv = source.uv;
            clone.colors = source.colors;
            clone.triangles = source.triangles;
            clone.RecalculateBounds();
            if (clone.normals == null || clone.normals.Length != clone.vertexCount)
                clone.RecalculateNormals();
            return clone;
        }

        private static Dictionary<ulong, int> BuildEdgeUseCounts(Vector3[] vertices, int[] triangles)
        {
            Dictionary<ulong, int> counts = new Dictionary<ulong, int>(triangles.Length);
            int triangleCount = triangles.Length / 3;
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int offset = triangleIndex * 3;
                if (!IsValidTriangle(vertices, triangles[offset], triangles[offset + 1], triangles[offset + 2]))
                    continue;

                AddEdge(counts, triangles[offset], triangles[offset + 1]);
                AddEdge(counts, triangles[offset + 1], triangles[offset + 2]);
                AddEdge(counts, triangles[offset + 2], triangles[offset]);
            }

            return counts;
        }

        private static bool[] ResolveProtectedTriangleMask(Vector3[] vertices, int[] triangles, Dictionary<ulong, int> edgeUseCounts)
        {
            int triangleCount = triangles.Length / 3;
            bool[] protectedTriangles = new bool[triangleCount];
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int offset = triangleIndex * 3;
                int a = triangles[offset];
                int b = triangles[offset + 1];
                int c = triangles[offset + 2];
                if (!IsValidTriangle(vertices, a, b, c))
                {
                    protectedTriangles[triangleIndex] = false;
                    continue;
                }

                protectedTriangles[triangleIndex] =
                    IsBoundaryEdge(edgeUseCounts, a, b) ||
                    IsBoundaryEdge(edgeUseCounts, b, c) ||
                    IsBoundaryEdge(edgeUseCounts, c, a);
            }

            return protectedTriangles;
        }

        private static bool IsValidTriangle(Vector3[] vertices, int a, int b, int c)
        {
            if ((uint)a >= (uint)vertices.Length || (uint)b >= (uint)vertices.Length || (uint)c >= (uint)vertices.Length)
                return false;

            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            return normal.sqrMagnitude > DegenerateTriangleEpsilon;
        }

        private static int CountMutableTriangles(Vector3[] vertices, int[] triangles, bool[] protectedTriangles)
        {
            int count = 0;
            int triangleCount = triangles.Length / 3;
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                if (protectedTriangles[triangleIndex])
                    continue;

                int offset = triangleIndex * 3;
                if (IsValidTriangle(vertices, triangles[offset], triangles[offset + 1], triangles[offset + 2]))
                    count++;
            }

            return count;
        }

        private static bool ShouldKeepMutableTriangle(int mutableSeen, int mutableKept, int mutableTotal, int mutableKeepBudget)
        {
            int remaining = mutableTotal - mutableSeen;
            int needed = mutableKeepBudget - mutableKept;
            if (needed >= remaining)
                return true;

            float expectedKept = (mutableSeen + 1) * (mutableKeepBudget / Mathf.Max(1f, mutableTotal));
            return mutableKept < Mathf.CeilToInt(expectedKept);
        }

        private static void DiffuseVertexColors(Color[] colors, int[] triangles, int iterations)
        {
            if (colors == null || triangles == null || colors.Length == 0 || triangles.Length < 3 || iterations <= 0)
                return;

            List<int>[] adjacency = new List<int>[colors.Length];
            int triangleCount = triangles.Length / 3;
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int offset = triangleIndex * 3;
                AddAdjacent(adjacency, triangles[offset], triangles[offset + 1]);
                AddAdjacent(adjacency, triangles[offset + 1], triangles[offset + 2]);
                AddAdjacent(adjacency, triangles[offset + 2], triangles[offset]);
            }

            Color[] source = colors;
            Color[] scratch = new Color[colors.Length];
            for (int pass = 0; pass < iterations; pass++)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    List<int> neighbors = adjacency[i];
                    if (neighbors == null || neighbors.Count == 0)
                    {
                        scratch[i] = source[i];
                        continue;
                    }

                    float blue = source[i].b;
                    for (int n = 0; n < neighbors.Count; n++)
                        blue += source[neighbors[n]].b;
                    Color color = source[i];
                    color.b = blue / (neighbors.Count + 1);
                    scratch[i] = color;
                }

                Color[] swap = source;
                source = scratch;
                scratch = swap;
            }

            if (!ReferenceEquals(source, colors))
            {
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = source[i];
            }
        }

        private static void AppendTriangle(List<int> kept, int[] triangles, int triangleIndex)
        {
            int offset = triangleIndex * 3;
            kept.Add(triangles[offset]);
            kept.Add(triangles[offset + 1]);
            kept.Add(triangles[offset + 2]);
        }

        private static void AddAdjacent(List<int>[] adjacency, int a, int b)
        {
            if ((uint)a >= (uint)adjacency.Length || (uint)b >= (uint)adjacency.Length || a == b)
                return;

            if (adjacency[a] == null)
                adjacency[a] = new List<int>(6);
            if (adjacency[b] == null)
                adjacency[b] = new List<int>(6);
            if (!adjacency[a].Contains(b))
                adjacency[a].Add(b);
            if (!adjacency[b].Contains(a))
                adjacency[b].Add(a);
        }

        private static void AddEdge(Dictionary<ulong, int> counts, int a, int b)
        {
            ulong key = EdgeKey(a, b);
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static bool IsBoundaryEdge(Dictionary<ulong, int> counts, int a, int b)
        {
            return !counts.TryGetValue(EdgeKey(a, b), out int count) || count <= 1;
        }

        private static ulong EdgeKey(int a, int b)
        {
            uint min = (uint)Mathf.Min(a, b);
            uint max = (uint)Mathf.Max(a, b);
            return ((ulong)min << 32) | max;
        }
    }
}
