using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    internal static class HectonProceduralVegetationStripBuilder
    {
        public static Mesh Build(
            string meshName,
            int segmentCount,
            float height,
            float baseWidth,
            float tipWidth)
        {
            int clampedSegments = Mathf.Clamp(segmentCount, 4, 6);
            int rowCount = clampedSegments + 1;
            int vertexCount = rowCount * 2;
            int indexCount = clampedSegments * 6;

            // COLD ALLOC: Vector3[vertexCount] - runtime strip mesh vertex positions - owner: HectonProceduralVegetationStripBuilder
            Vector3[] vertices = new Vector3[vertexCount];
            // COLD ALLOC: Vector3[vertexCount] - runtime strip mesh normals - owner: HectonProceduralVegetationStripBuilder
            Vector3[] normals = new Vector3[vertexCount];
            // COLD ALLOC: Vector4[vertexCount] - runtime strip mesh tangents - owner: HectonProceduralVegetationStripBuilder
            Vector4[] tangents = new Vector4[vertexCount];
            // COLD ALLOC: Vector2[vertexCount] - runtime strip mesh uvs - owner: HectonProceduralVegetationStripBuilder
            Vector2[] uvs = new Vector2[vertexCount];
            // COLD ALLOC: Color32[vertexCount] - runtime strip mesh vertex colors - owner: HectonProceduralVegetationStripBuilder
            Color32[] colors = new Color32[vertexCount];
            // COLD ALLOC: int[indexCount] - runtime strip mesh triangle indices - owner: HectonProceduralVegetationStripBuilder
            int[] indices = new int[indexCount];

            float safeHeight = Mathf.Max(0.05f, height);
            float safeBaseWidth = Mathf.Max(0.005f, baseWidth);
            float safeTipWidth = Mathf.Clamp(tipWidth, 0.001f, safeBaseWidth);

            for (int row = 0; row < rowCount; row++)
            {
                float t = rowCount <= 1 ? 0f : row / (float)(rowCount - 1);
                float y = safeHeight * t;
                float width = Mathf.Lerp(safeBaseWidth, safeTipWidth, t * t);
                float halfWidth = width * 0.5f;
                float sweep = Mathf.Sin(t * Mathf.PI) * safeHeight * 0.045f;
                int leftIndex = row * 2;
                int rightIndex = leftIndex + 1;
                byte tipMask = (byte)Mathf.RoundToInt(t * 255f);

                vertices[leftIndex] = new Vector3(-halfWidth, y, sweep);
                vertices[rightIndex] = new Vector3(halfWidth, y, sweep);

                normals[leftIndex] = Vector3.forward;
                normals[rightIndex] = Vector3.forward;

                tangents[leftIndex] = new Vector4(1f, 0f, 0f, 1f);
                tangents[rightIndex] = new Vector4(1f, 0f, 0f, 1f);

                uvs[leftIndex] = new Vector2(0f, t);
                uvs[rightIndex] = new Vector2(1f, t);

                colors[leftIndex] = new Color32(tipMask, 255, 255, 255);
                colors[rightIndex] = new Color32(tipMask, 255, 255, 255);
            }

            int indexCursor = 0;
            for (int row = 0; row < clampedSegments; row++)
            {
                int rowStart = row * 2;
                int nextRowStart = rowStart + 2;

                indices[indexCursor++] = rowStart;
                indices[indexCursor++] = nextRowStart;
                indices[indexCursor++] = rowStart + 1;

                indices[indexCursor++] = rowStart + 1;
                indices[indexCursor++] = nextRowStart;
                indices[indexCursor++] = nextRowStart + 1;
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, true);
            mesh.bounds = new Bounds(
                new Vector3(0f, safeHeight * 0.5f, 0f),
                new Vector3(safeBaseWidth, safeHeight, safeHeight * 0.12f));
            return mesh;
        }
    }
}
