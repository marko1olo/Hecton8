#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Shared editor-only mesh helpers for import-time LODs, shadow proxies, and baked mesh variants.
    /// </summary>
    internal static class HectonEditorMeshUtility
    {
        internal static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int triangles = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);

            return triangles;
        }

        internal static Mesh BuildDecimatedMesh(Mesh source, float triangleRatio, int maxTriangles, string meshName)
        {
            if (source == null)
                return null;

            int sourceTriangleCount = CountTriangles(source);
            if (sourceTriangleCount <= 0)
                return UnityEngine.Object.Instantiate(source);

            float clampedRatio = Mathf.Clamp01(triangleRatio);
            int targetTriangles = Mathf.Max(1, Mathf.RoundToInt(sourceTriangleCount * clampedRatio));
            if (maxTriangles > 0)
                targetTriangles = Mathf.Min(targetTriangles, maxTriangles);

            if (targetTriangles >= sourceTriangleCount)
            {
                Mesh fullCopy = UnityEngine.Object.Instantiate(source);
                fullCopy.name = meshName;
                return fullCopy;
            }

            List<Vector3> sourceVertices = new List<Vector3>(source.vertexCount);
            List<Vector3> sourceNormals = new List<Vector3>(source.vertexCount);
            List<Vector4> sourceTangents = new List<Vector4>(source.vertexCount);
            List<Vector2> sourceUv0 = new List<Vector2>(source.vertexCount);
            List<Vector2> sourceUv1 = new List<Vector2>(source.vertexCount);
            List<Color32> sourceColors = new List<Color32>(source.vertexCount);
            source.GetVertices(sourceVertices);
            source.GetNormals(sourceNormals);
            source.GetTangents(sourceTangents);
            source.GetUVs(0, sourceUv0);
            source.GetUVs(1, sourceUv1);
            source.GetColors(sourceColors);

            List<Vector3> vertices = new List<Vector3>(Mathf.Min(source.vertexCount, targetTriangles * 3));
            List<Vector3> normals = HasCount(sourceNormals, source.vertexCount) ? new List<Vector3>(vertices.Capacity) : null;
            List<Vector4> tangents = HasCount(sourceTangents, source.vertexCount) ? new List<Vector4>(vertices.Capacity) : null;
            List<Vector2> uv0 = HasCount(sourceUv0, source.vertexCount) ? new List<Vector2>(vertices.Capacity) : null;
            List<Vector2> uv1 = HasCount(sourceUv1, source.vertexCount) ? new List<Vector2>(vertices.Capacity) : null;
            List<Color32> colors = HasCount(sourceColors, source.vertexCount) ? new List<Color32>(vertices.Capacity) : null;
            int[] remap = new int[source.vertexCount];
            for (int i = 0; i < remap.Length; i++)
                remap[i] = -1;

            int maxSourceIndexCount = 0;
            for (int subMeshIndex = 0; subMeshIndex < source.subMeshCount; subMeshIndex++)
                maxSourceIndexCount = Mathf.Max(maxSourceIndexCount, (int)source.GetIndexCount(subMeshIndex));

            List<int> sourceIndices = new List<int>(maxSourceIndexCount);
            List<int>[] submeshIndices = new List<int>[source.subMeshCount];
            int writtenTriangles = 0;

            for (int subMeshIndex = 0; subMeshIndex < source.subMeshCount; subMeshIndex++)
            {
                sourceIndices.Clear();
                source.GetTriangles(sourceIndices, subMeshIndex);
                int sourceSubmeshTriangles = sourceIndices.Count / 3;
                if (sourceSubmeshTriangles <= 0)
                {
                    submeshIndices[subMeshIndex] = null;
                    continue;
                }

                int targetSubmeshTriangles = Mathf.Max(
                    1,
                    Mathf.RoundToInt(targetTriangles * (sourceSubmeshTriangles / (float)sourceTriangleCount)));
                targetSubmeshTriangles = Mathf.Min(targetSubmeshTriangles, sourceSubmeshTriangles);

                List<int> outputIndices = new List<int>(targetSubmeshTriangles * 3);
                for (int keptTriangle = 0; keptTriangle < targetSubmeshTriangles && writtenTriangles < targetTriangles; keptTriangle++)
                {
                    int sourceTriangle = Mathf.Min(
                        sourceSubmeshTriangles - 1,
                        Mathf.FloorToInt((keptTriangle + 0.5f) * sourceSubmeshTriangles / targetSubmeshTriangles));
                    int sourceIndexBase = sourceTriangle * 3;

                    outputIndices.Add(RemapIndex(sourceIndices[sourceIndexBase], sourceVertices, sourceNormals, sourceTangents, sourceUv0, sourceUv1, sourceColors, remap, vertices, normals, tangents, uv0, uv1, colors));
                    outputIndices.Add(RemapIndex(sourceIndices[sourceIndexBase + 1], sourceVertices, sourceNormals, sourceTangents, sourceUv0, sourceUv1, sourceColors, remap, vertices, normals, tangents, uv0, uv1, colors));
                    outputIndices.Add(RemapIndex(sourceIndices[sourceIndexBase + 2], sourceVertices, sourceNormals, sourceTangents, sourceUv0, sourceUv1, sourceColors, remap, vertices, normals, tangents, uv0, uv1, colors));
                    writtenTriangles++;
                }

                submeshIndices[subMeshIndex] = outputIndices;
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (normals != null)
                mesh.SetNormals(normals);
            if (tangents != null)
                mesh.SetTangents(tangents);
            if (uv0 != null)
                mesh.SetUVs(0, uv0);
            if (uv1 != null)
                mesh.SetUVs(1, uv1);
            if (colors != null)
                mesh.SetColors(colors);

            mesh.subMeshCount = submeshIndices.Length;
            for (int subMeshIndex = 0; subMeshIndex < submeshIndices.Length; subMeshIndex++)
            {
                List<int> indices = submeshIndices[subMeshIndex];
                if (indices == null)
                    mesh.SetTriangles(Array.Empty<int>(), subMeshIndex, false);
                else
                    mesh.SetTriangles(indices, subMeshIndex, false);
            }

            if (normals == null)
                mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Bounds CalculateLocalRendererBounds(GameObject root, out bool hasBounds)
        {
            hasBounds = false;
            Bounds combined = default;
            if (root == null)
                return combined;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                EncapsulateWorldBounds(root.transform, renderer.bounds, ref hasBounds, ref combined);
            }

            return combined;
        }

        internal static string SanitizeAssetToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if ((current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '_')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static int RemapIndex(
            int sourceIndex,
            List<Vector3> sourceVertices,
            List<Vector3> sourceNormals,
            List<Vector4> sourceTangents,
            List<Vector2> sourceUv0,
            List<Vector2> sourceUv1,
            List<Color32> sourceColors,
            int[] remap,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<Color32> colors)
        {
            int remappedIndex = remap[sourceIndex];
            if (remappedIndex >= 0)
                return remappedIndex;

            int newIndex = vertices.Count;
            remap[sourceIndex] = newIndex;
            vertices.Add(sourceVertices[sourceIndex]);
            if (normals != null)
                normals.Add(sourceNormals[sourceIndex]);
            if (tangents != null)
                tangents.Add(sourceTangents[sourceIndex]);
            if (uv0 != null)
                uv0.Add(sourceUv0[sourceIndex]);
            if (uv1 != null)
                uv1.Add(sourceUv1[sourceIndex]);
            if (colors != null)
                colors.Add(sourceColors[sourceIndex]);

            return newIndex;
        }

        private static bool HasCount<T>(List<T> list, int expectedLength)
        {
            return list != null && list.Count == expectedLength;
        }

        private static void EncapsulateWorldBounds(
            Transform root,
            Bounds worldBounds,
            ref bool hasBounds,
            ref Bounds combinedBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            EncapsulatePoint(root, new Vector3(min.x, min.y, min.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(min.x, min.y, max.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(min.x, max.y, min.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(min.x, max.y, max.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(max.x, min.y, min.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(max.x, min.y, max.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(max.x, max.y, min.z), ref hasBounds, ref combinedBounds);
            EncapsulatePoint(root, new Vector3(max.x, max.y, max.z), ref hasBounds, ref combinedBounds);
        }

        private static void EncapsulatePoint(
            Transform root,
            Vector3 worldPoint,
            ref bool hasBounds,
            ref Bounds combinedBounds)
        {
            Vector3 localPoint = root.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                combinedBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            combinedBounds.Encapsulate(localPoint);
        }
    }
}
#endif
