using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    internal static class WorldProceduralSeaweedMeshBuilder
    {
        private const float TwoPi = Mathf.PI * 2f;

        public static bool CanBuild(string rootToken)
        {
            return TryResolveSpec(rootToken, out _);
        }

        public static bool TryBuild(string rootToken, Vector3 scale, int lodLevel, out Mesh mesh)
        {
            mesh = null;
            VariantSpec spec;
            if (!TryResolveSpec(rootToken, out spec))
                return false;

            int lod = Mathf.Clamp(lodLevel, 0, 3);
            MeshBuffers buffers = new MeshBuffers(spec.EstimatedVertexCount);
            BuildHoldfast(buffers, spec, scale, lod);
            BuildStipe(buffers, spec, scale, lod);

            int activeBladeCount = Mathf.Max(1, spec.BladeCount - lod);
            for (int bladeIndex = 0; bladeIndex < activeBladeCount; bladeIndex++)
                BuildBlade(buffers, spec, scale, lod, bladeIndex, activeBladeCount);

            int activeBulbCount = Mathf.Max(0, spec.BulbCount - lod);
            for (int bulbIndex = 0; bulbIndex < activeBulbCount; bulbIndex++)
                BuildBulb(buffers, spec, scale, lod, bulbIndex, activeBulbCount);

            if (buffers.Indices.Count < 3)
                return false;

            mesh = CreateMesh(rootToken, lod, buffers);
            return mesh != null;
        }

        private static void BuildHoldfast(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod)
        {
            int rootCount = Mathf.Max(1, spec.RootCount - (lod * 2));
            int rootSegments = Mathf.Max(2, 4 - lod);
            float baseRadius = Mathf.Max(0.018f, scale.x * 0.14f);

            for (int i = 0; i < rootCount; i++)
            {
                float t = rootCount <= 1 ? 0f : i / (float)(rootCount - 1);
                float yaw = t * TwoPi + spec.RootYawOffset;
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 origin = new Vector3(0f, scale.y * 0.06f, 0f) + dir * (scale.x * 0.06f);
                float length = scale.x * Mathf.Lerp(0.36f, 0.62f, 0.5f + 0.5f * Mathf.Sin((i + 1) * 1.37f));
                AddRibbon(
                    buffers,
                    origin,
                    dir,
                    Vector3.up,
                    baseRadius * Mathf.Lerp(0.82f, 1.08f, t),
                    length,
                    0.14f,
                    rootSegments,
                    0.08f,
                    0.16f,
                    new Color32(spec.TintByte, 196, 46, 255));
            }
        }

        private static void BuildStipe(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod)
        {
            int radialSegments = Mathf.Max(3, spec.StipeSides - (lod * 2));
            int heightSegments = Mathf.Max(2, spec.StipeSegments - (lod * 3));
            float height = scale.y * spec.StipeHeightMultiplier;
            float bottomRadius = Mathf.Max(0.02f, scale.x * spec.BaseRadiusMultiplier);
            float topRadius = Mathf.Max(bottomRadius * 0.42f, scale.x * spec.TopRadiusMultiplier);
            float bend = spec.BendDegrees;

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = y / (float)heightSegments;
                float bendRadians = bend * Mathf.Deg2Rad * v * v;
                Vector3 center = new Vector3(
                    Mathf.Sin(bendRadians) * scale.x * spec.BendRadiusMultiplier,
                    v * height,
                    Mathf.Cos(bendRadians) * scale.z * spec.ForwardOffsetMultiplier - scale.z * spec.ForwardOffsetMultiplier);
                float radius = Mathf.Lerp(bottomRadius, topRadius, v);

                for (int side = 0; side <= radialSegments; side++)
                {
                    float u = side / (float)radialSegments;
                    float angle = u * TwoPi;
                    float rib = 1f + Mathf.Sin(angle * spec.RibCount) * spec.RibAmplitude;
                    float actualRadius = radius * rib;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 normal = radial.normalized;
                    Vector3 vertex = center + radial * actualRadius;
                    Vector4 tangent = new Vector4(-Mathf.Sin(angle), 0f, Mathf.Cos(angle), 1f);
                    buffers.AddVertex(vertex, normal, tangent, new Vector2(u, v), new Color32(spec.TintByte, (byte)Mathf.Lerp(92f, 188f, v), (byte)Mathf.Lerp(32f, 186f, v), 255));
                }
            }

            int rowSize = radialSegments + 1;
            for (int y = 0; y < heightSegments; y++)
            {
                int rowStart = y * rowSize;
                int nextRowStart = (y + 1) * rowSize;
                for (int side = 0; side < radialSegments; side++)
                    buffers.AddQuad(rowStart + side, nextRowStart + side, nextRowStart + side + 1, rowStart + side + 1);
            }
        }

        private static void BuildBlade(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bladeIndex, int bladeCount)
        {
            int bladeSegments = Mathf.Max(2, spec.BladeSegments - (lod * 3));
            float normalized = bladeCount <= 1 ? 0f : bladeIndex / (float)(bladeCount - 1);
            float angle = spec.BladeStartYaw + normalized * spec.BladeYawArc;
            Vector3 lateral = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 up = Vector3.up;
            Vector3 anchor = new Vector3(
                lateral.x * scale.x * spec.BladeAnchorRadius,
                scale.y * Mathf.Lerp(spec.BladeAnchorHeightMin, spec.BladeAnchorHeightMax, normalized),
                lateral.z * scale.z * spec.BladeAnchorRadius);
            float width = scale.x * Mathf.Lerp(spec.BladeWidthMin, spec.BladeWidthMax, normalized);
            float length = scale.y * Mathf.Lerp(spec.BladeLengthMin, spec.BladeLengthMax, 1f - normalized * spec.BladeLengthFalloff);
            float sideCurve = Mathf.Lerp(-spec.SideCurveDegrees, spec.SideCurveDegrees, normalized);
            float twist = Mathf.Lerp(spec.TwistDegreesMin, spec.TwistDegreesMax, normalized);
            float serration = lod == 0 ? spec.SerrationAmplitude : spec.SerrationAmplitude * 0.4f;
            AddRibbon(buffers, anchor, lateral, up, width, length, twist, bladeSegments, sideCurve, serration, new Color32(spec.TintByte, 208, (byte)Mathf.Lerp(40f, 210f, normalized), 255), forward);
        }

        private static void BuildBulb(MeshBuffers buffers, VariantSpec spec, Vector3 scale, int lod, int bulbIndex, int bulbCount)
        {
            float t = bulbCount <= 1 ? 0.5f : bulbIndex / (float)(bulbCount - 1);
            Vector3 center = new Vector3(
                Mathf.Sin((t + 0.2f) * 3.1f) * scale.x * 0.12f,
                scale.y * Mathf.Lerp(spec.BulbHeightMin, spec.BulbHeightMax, t),
                Mathf.Cos((t + 0.11f) * 2.7f) * scale.z * 0.08f);
            float radius = scale.x * Mathf.Lerp(spec.BulbRadiusMin, spec.BulbRadiusMax, 1f - t * 0.35f);
            int latSegments = Mathf.Max(2, 5 - lod);
            int lonSegments = Mathf.Max(4, 8 - (lod * 2));
            AddSphere(buffers, center, new Vector3(radius, radius * 1.2f, radius), latSegments, lonSegments, new Color32(spec.TintByte, 224, 118, 255));
        }

        private static void AddRibbon(MeshBuffers buffers, Vector3 anchor, Vector3 widthAxis, Vector3 upAxis, float width, float length, float twistDegrees, int segments, float sideCurveDegrees, float serration, Color32 color, Vector3? forwardHint = null)
        {
            Vector3 widthDir = widthAxis.sqrMagnitude > 0f ? widthAxis.normalized : Vector3.right;
            Vector3 upDir = upAxis.sqrMagnitude > 0f ? upAxis.normalized : Vector3.up;
            Vector3 forwardDir = forwardHint.HasValue && forwardHint.Value.sqrMagnitude > 0f
                ? forwardHint.Value.normalized
                : Vector3.Cross(widthDir, upDir).normalized;
            int startIndex = buffers.Vertices.Count;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float twist = Mathf.Lerp(0f, twistDegrees, t);
                Quaternion rotation = Quaternion.AngleAxis(twist, upDir) * Quaternion.AngleAxis(sideCurveDegrees * t, forwardDir);
                Vector3 rotatedWidth = rotation * widthDir;
                Vector3 center = anchor + upDir * (length * t) + forwardDir * (Mathf.Sin(t * Mathf.PI) * length * 0.08f);
                float edgeOffset = serration * Mathf.Sin(t * Mathf.PI * 7f);
                float halfWidth = width * Mathf.Lerp(0.92f, 0.22f, t);
                Vector3 left = center - rotatedWidth * (halfWidth + edgeOffset);
                Vector3 right = center + rotatedWidth * (halfWidth - edgeOffset);
                Vector3 normal = Vector3.Cross(rotatedWidth, upDir).normalized;
                Vector4 tangent = new Vector4(rotatedWidth.x, rotatedWidth.y, rotatedWidth.z, 1f);
                buffers.AddVertex(left, normal, tangent, new Vector2(0f, t), color);
                buffers.AddVertex(right, normal, tangent, new Vector2(1f, t), color);
            }

            for (int i = 0; i < segments; i++)
            {
                int row = startIndex + i * 2;
                buffers.AddQuad(row, row + 2, row + 3, row + 1);
            }
        }

        private static void AddSphere(MeshBuffers buffers, Vector3 center, Vector3 radii, int latSegments, int lonSegments, Color32 color)
        {
            int startIndex = buffers.Vertices.Count;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float v = lat / (float)latSegments;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float u = lon / (float)lonSegments;
                    float theta = u * TwoPi;
                    Vector3 normal = new Vector3(Mathf.Cos(theta) * cosPhi, sinPhi, Mathf.Sin(theta) * cosPhi).normalized;
                    Vector3 vertex = center + Vector3.Scale(normal, radii);
                    Vector3 tangentDir = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta)).normalized;
                    buffers.AddVertex(vertex, normal, new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, 1f), new Vector2(u, v), color);
                }
            }

            int rowSize = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                int rowStart = startIndex + lat * rowSize;
                int nextRowStart = rowStart + rowSize;
                for (int lon = 0; lon < lonSegments; lon++)
                    buffers.AddQuad(rowStart + lon, nextRowStart + lon, nextRowStart + lon + 1, rowStart + lon + 1);
            }
        }

        private static Mesh CreateMesh(string rootToken, int lod, MeshBuffers buffers)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            meshData.SetVertexBufferParams(buffers.Vertices.Count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            meshData.SetIndexBufferParams(buffers.Indices.Count, IndexFormat.UInt32);

            NativeArray<VertexData> vertexData = meshData.GetVertexData<VertexData>();
            for (int i = 0; i < buffers.Vertices.Count; i++)
            {
                vertexData[i] = new VertexData(buffers.Vertices[i], buffers.Normals[i], buffers.Tangents[i], buffers.Colors[i], buffers.UVs[i]);
            }

            NativeArray<uint> indexData = meshData.GetIndexData<uint>();
            for (int i = 0; i < buffers.Indices.Count; i++)
                indexData[i] = buffers.Indices[i];

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, buffers.Indices.Count, MeshTopology.Triangles)
            {
                bounds = buffers.Bounds,
                vertexCount = buffers.Vertices.Count
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

            Mesh mesh = new Mesh
            {
                name = rootToken + "_LOD" + lod
            };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            mesh.bounds = buffers.Bounds;
            return mesh;
        }

        private static bool TryResolveSpec(string rootToken, out VariantSpec spec)
        {
            switch (rootToken)
            {
                case "family_kelp_tall__stalk": spec = new VariantSpec(10, 14, 12, 4, 2, 0.94f, 0.18f, 0.08f, 4f, 0.05f, 0f, 0f, 0.26f, 0.32f, 0.34f, 0.82f, 0.18f, 0.92f, 0.18f, 0.78f, 0.14f, 8f, -4f, 10f, 22f, 6, 0.03f, 156, 1120); return true;
                case "family_kelp_tall__lean": spec = new VariantSpec(9, 12, 11, 4, 1, 0.88f, 0.18f, 0.08f, 5f, 0.08f, 18f, 0.18f, 0.3f, 0.34f, 0.38f, 0.86f, 0.22f, 0.94f, 0.14f, 0.84f, 0.16f, 14f, -10f, 16f, 28f, 5, 0.035f, 148, 980); return true;
                case "family_kelp_tall__ribbon": spec = new VariantSpec(8, 13, 14, 5, 2, 0.98f, 0.16f, 0.06f, 5f, 0.1f, 24f, 0.22f, 0.42f, 0.42f, 0.52f, 0.94f, 0.26f, 0.98f, 0.1f, 0.88f, 0.18f, 20f, -16f, 22f, 34f, 4, 0.04f, 164, 1040); return true;
                case "family_kelp_patch_dense__patch": spec = new VariantSpec(8, 11, 11, 5, 1, 0.84f, 0.2f, 0.09f, 4f, 0.06f, 8f, 0.12f, 0.18f, 0.26f, 0.42f, 0.72f, 0.22f, 0.88f, 0.22f, 0.72f, 0.16f, 18f, -24f, 20f, 48f, 6, 0.035f, 144, 1100); return true;
                case "family_kelp_patch_dense__patch_tall": spec = new VariantSpec(9, 12, 12, 6, 1, 0.92f, 0.18f, 0.08f, 4f, 0.05f, 12f, 0.14f, 0.22f, 0.28f, 0.46f, 0.82f, 0.18f, 0.92f, 0.2f, 0.76f, 0.18f, 22f, -18f, 24f, 56f, 7, 0.034f, 150, 1240); return true;
                case "family_kelp_patch_dense__ring": spec = new VariantSpec(8, 11, 11, 7, 0, 0.8f, 0.19f, 0.08f, 4f, 0.06f, 10f, 0.1f, 0.18f, 0.26f, 0.44f, 0.7f, 0.24f, 0.86f, 0.18f, 0.74f, 0.16f, 20f, 0f, 360f, 52f, 7, 0.036f, 146, 1320); return true;
                case "family_kelp_canopy__crown": spec = new VariantSpec(10, 15, 14, 6, 2, 1f, 0.2f, 0.08f, 5f, 0.08f, 12f, 0.1f, 0.44f, 0.58f, 0.56f, 1.02f, 0.34f, 1.08f, 0.16f, 0.84f, 0.18f, 24f, -32f, 64f, 42f, 7, 0.038f, 170, 1380); return true;
                case "family_kelp_canopy__frond": spec = new VariantSpec(9, 13, 12, 5, 1, 0.92f, 0.18f, 0.07f, 4f, 0.06f, 6f, 0.08f, 0.3f, 0.46f, 0.48f, 0.94f, 0.32f, 1f, 0.12f, 0.8f, 0.16f, 18f, -18f, 36f, 36f, 5, 0.03f, 162, 1120); return true;
                case "family_kelp_canopy__fan": spec = new VariantSpec(10, 14, 13, 7, 1, 0.96f, 0.18f, 0.07f, 5f, 0.08f, 10f, 0.09f, 0.38f, 0.56f, 0.54f, 0.96f, 0.34f, 1.06f, 0.14f, 0.82f, 0.16f, 28f, -54f, 108f, 46f, 6, 0.034f, 174, 1440); return true;
                default: spec = default; return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct VertexData
        {
            public VertexData(Vector3 position, Vector3 normal, Vector4 tangent, Color32 color, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                Tangent = tangent;
                Color = color;
                UV = uv;
            }

            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector4 Tangent;
            public readonly Color32 Color;
            public readonly Vector2 UV;
        }

        private readonly struct VariantSpec
        {
            public VariantSpec(int stipeSides, int stipeSegments, int bladeSegments, int bladeCount, int bulbCount, float stipeHeightMultiplier, float baseRadiusMultiplier, float topRadiusMultiplier, float ribCount, float ribAmplitude, float bendDegrees, float bendRadiusMultiplier, float bladeAnchorHeightMin, float bladeAnchorHeightMax, float bladeWidthMin, float bladeWidthMax, float bladeLengthMin, float bladeLengthMax, float bladeLengthFalloff, float bladeAnchorRadius, float forwardOffsetMultiplier, float twistDegreesMax, float bladeStartYaw, float bladeYawArc, float sideCurveDegrees, int rootCount, float rootYawOffset, byte tintByte, int estimatedVertexCount)
            {
                StipeSides = stipeSides;
                StipeSegments = stipeSegments;
                BladeSegments = bladeSegments;
                BladeCount = bladeCount;
                BulbCount = bulbCount;
                StipeHeightMultiplier = stipeHeightMultiplier;
                BaseRadiusMultiplier = baseRadiusMultiplier;
                TopRadiusMultiplier = topRadiusMultiplier;
                RibCount = ribCount;
                RibAmplitude = ribAmplitude;
                BendDegrees = bendDegrees;
                BendRadiusMultiplier = bendRadiusMultiplier;
                BladeAnchorHeightMin = bladeAnchorHeightMin;
                BladeAnchorHeightMax = bladeAnchorHeightMax;
                BladeWidthMin = bladeWidthMin;
                BladeWidthMax = bladeWidthMax;
                BladeLengthMin = bladeLengthMin;
                BladeLengthMax = bladeLengthMax;
                BladeLengthFalloff = bladeLengthFalloff;
                BladeAnchorRadius = bladeAnchorRadius;
                ForwardOffsetMultiplier = forwardOffsetMultiplier;
                TwistDegreesMin = 0f;
                TwistDegreesMax = twistDegreesMax;
                BladeStartYaw = bladeStartYaw;
                BladeYawArc = bladeYawArc;
                SideCurveDegrees = sideCurveDegrees;
                RootCount = rootCount;
                RootYawOffset = rootYawOffset;
                SerrationAmplitude = 0.015f;
                BulbHeightMin = 0.5f;
                BulbHeightMax = 0.82f;
                BulbRadiusMin = 0.18f;
                BulbRadiusMax = 0.28f;
                TintByte = tintByte;
                EstimatedVertexCount = estimatedVertexCount;
            }

            public int StipeSides { get; }
            public int StipeSegments { get; }
            public int BladeSegments { get; }
            public int BladeCount { get; }
            public int BulbCount { get; }
            public float StipeHeightMultiplier { get; }
            public float BaseRadiusMultiplier { get; }
            public float TopRadiusMultiplier { get; }
            public float RibCount { get; }
            public float RibAmplitude { get; }
            public float BendDegrees { get; }
            public float BendRadiusMultiplier { get; }
            public float BladeAnchorHeightMin { get; }
            public float BladeAnchorHeightMax { get; }
            public float BladeWidthMin { get; }
            public float BladeWidthMax { get; }
            public float BladeLengthMin { get; }
            public float BladeLengthMax { get; }
            public float BladeLengthFalloff { get; }
            public float BladeAnchorRadius { get; }
            public float ForwardOffsetMultiplier { get; }
            public float TwistDegreesMin { get; }
            public float TwistDegreesMax { get; }
            public float BladeStartYaw { get; }
            public float BladeYawArc { get; }
            public float SideCurveDegrees { get; }
            public int RootCount { get; }
            public float RootYawOffset { get; }
            public float SerrationAmplitude { get; }
            public float BulbHeightMin { get; }
            public float BulbHeightMax { get; }
            public float BulbRadiusMin { get; }
            public float BulbRadiusMax { get; }
            public byte TintByte { get; }
            public int EstimatedVertexCount { get; }
        }

        private sealed class MeshBuffers
        {
            public MeshBuffers(int capacity)
            {
                Vertices = new List<Vector3>(capacity);
                Normals = new List<Vector3>(capacity);
                Tangents = new List<Vector4>(capacity);
                Colors = new List<Color32>(capacity);
                UVs = new List<Vector2>(capacity);
                Indices = new List<uint>(capacity * 3);
                Bounds = new Bounds(Vector3.zero, Vector3.zero);
                _hasBounds = false;
            }

            public List<Vector3> Vertices { get; }
            public List<Vector3> Normals { get; }
            public List<Vector4> Tangents { get; }
            public List<Color32> Colors { get; }
            public List<Vector2> UVs { get; }
            public List<uint> Indices { get; }
            public Bounds Bounds { get; private set; }

            private bool _hasBounds;

            public void AddVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv, Color32 color)
            {
                Vertices.Add(position);
                Normals.Add(normal);
                Tangents.Add(tangent);
                UVs.Add(uv);
                Colors.Add(color);
                if (!_hasBounds)
                {
                    Bounds = new Bounds(position, Vector3.zero);
                    _hasBounds = true;
                }
                else
                {
                    Bounds bounds = Bounds;
                    bounds.Encapsulate(position);
                    Bounds = bounds;
                }
            }

            public void AddQuad(int a, int b, int c, int d)
            {
                Indices.Add((uint)a);
                Indices.Add((uint)b);
                Indices.Add((uint)c);
                Indices.Add((uint)a);
                Indices.Add((uint)c);
                Indices.Add((uint)d);
            }
        }
    }
}
