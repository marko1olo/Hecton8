using System.Runtime.InteropServices;
using Hecton8.World.OfflineWreckageBaker;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct OfflineWreckageSubMeshIndexRangeDTO
    {
        [FieldOffset(0)] public int SourceIndexStart;
        [FieldOffset(4)] public int IndexCount;
        [FieldOffset(8)] public int DestinationIndexStart;
        [FieldOffset(12)] public int BaseVertex;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ExtractBaseVerticesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> PositionBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> NormalBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> TangentBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> UvBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> ColorBytes;

        // SAFETY: IJobParallelFor writes only Output[index]; scheduler owns unique indices.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Output;

        public int PositionOffset;
        public int PositionStride;
        public int NormalOffset;
        public int NormalStride;
        public int TangentOffset;
        public int TangentStride;
        public int UvOffset;
        public int UvStride;
        public int ColorOffset;
        public int ColorStride;
        public int HasNormal;
        public int HasTangent;
        public int HasUv;
        public int HasColor;

        public void Execute(int index)
        {
            OfflineWreckageBakeVertexDTO vertex = default;
            vertex.Position = ReadFloat3(PositionBytes, PositionOffset, PositionStride, index, float3.zero);
            vertex.Normal = HasNormal != 0
                ? math.normalizesafe(ReadFloat3(NormalBytes, NormalOffset, NormalStride, index, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f))
                : new float3(0f, 1f, 0f);
            vertex.Tangent = HasTangent != 0 ? ReadFloat4(TangentBytes, TangentOffset, TangentStride, index, new float4(1f, 0f, 0f, 1f)) : new float4(1f, 0f, 0f, 1f);
            vertex.Uv0 = HasUv != 0 ? ReadFloat2(UvBytes, UvOffset, UvStride, index, float2.zero) : float2.zero;
            vertex.PackedColor = HasColor != 0 ? ReadUInt32(ColorBytes, ColorOffset, ColorStride, index, 0xFFFFFFFFu) : 0xFFFFFFFFu;
            if (!math.all(math.isfinite(vertex.Position)))
                vertex.Position = float3.zero;
            if (!math.all(math.isfinite(vertex.Normal)))
                vertex.Normal = new float3(0f, 1f, 0f);
            if (!math.all(math.isfinite(vertex.Tangent)))
                vertex.Tangent = new float4(1f, 0f, 0f, 1f);
            if (!math.all(math.isfinite(vertex.Uv0)))
                vertex.Uv0 = float2.zero;
            vertex.Uv3AupLocal = vertex.Position;
            Output[index] = vertex;
        }

        private static float3 ReadFloat3(NativeArray<byte> bytes, int offset, int stride, int index, float3 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float3>(ptr, index, stride);
        }

        private static float4 ReadFloat4(NativeArray<byte> bytes, int offset, int stride, int index, float4 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float4>(ptr, index, stride);
        }

        private static float2 ReadFloat2(NativeArray<byte> bytes, int offset, int stride, int index, float2 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float2>(ptr, index, stride);
        }

        private static uint ReadUInt32(NativeArray<byte> bytes, int offset, int stride, int index, uint fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<uint>(ptr, index, stride);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockGridSurfaceIndicesJob : IJobParallelFor
    {
        [WriteOnly]
        [NoAlias]
        public NativeArray<int> Output;

        public int3 Resolution;

        public void Execute(int quadIndex)
        {
            int xCount = math.max(Resolution.x, 2);
            int yCount = math.max(Resolution.y, 2);
            int zCount = math.max(Resolution.z, 2);
            int xyQuads = (xCount - 1) * (yCount - 1);
            int xzQuads = (xCount - 1) * (zCount - 1);
            int yzQuads = (yCount - 1) * (zCount - 1);
            int write = quadIndex * 6;
            int local = quadIndex;
            if (local < xyQuads)
            {
                WriteXY(local, 0, false, xCount, yCount, write);
                return;
            }

            local -= xyQuads;
            if (local < xyQuads)
            {
                WriteXY(local, zCount - 1, true, xCount, yCount, write);
                return;
            }

            local -= xyQuads;
            if (local < xzQuads)
            {
                WriteXZ(local, 0, false, xCount, yCount, write);
                return;
            }

            local -= xzQuads;
            if (local < xzQuads)
            {
                WriteXZ(local, yCount - 1, true, xCount, yCount, write);
                return;
            }

            local -= xzQuads;
            if (local < yzQuads)
            {
                WriteYZ(local, 0, false, xCount, yCount, write);
                return;
            }

            local -= yzQuads;
            WriteYZ(local, xCount - 1, true, xCount, yCount, write);
        }

        private void WriteXY(int local, int z, bool positiveNormal, int xCount, int yCount, int write)
        {
            int xQuads = xCount - 1;
            int y = local / xQuads;
            int x = local - (y * xQuads);
            int i0 = Vertex(x, y, z, xCount, yCount);
            int i1 = Vertex(x + 1, y, z, xCount, yCount);
            int i2 = Vertex(x, y + 1, z, xCount, yCount);
            int i3 = Vertex(x + 1, y + 1, z, xCount, yCount);
            WriteQuad(i0, i1, i2, i3, positiveNormal, write);
        }

        private void WriteXZ(int local, int y, bool positiveNormal, int xCount, int yCount, int write)
        {
            int xQuads = xCount - 1;
            int z = local / xQuads;
            int x = local - (z * xQuads);
            int i0 = Vertex(x, y, z, xCount, yCount);
            int i1 = Vertex(x + 1, y, z, xCount, yCount);
            int i2 = Vertex(x, y, z + 1, xCount, yCount);
            int i3 = Vertex(x + 1, y, z + 1, xCount, yCount);
            WriteQuad(i0, i1, i2, i3, !positiveNormal, write);
        }

        private void WriteYZ(int local, int x, bool positiveNormal, int xCount, int yCount, int write)
        {
            int yQuads = yCount - 1;
            int z = local / yQuads;
            int y = local - (z * yQuads);
            int i0 = Vertex(x, y, z, xCount, yCount);
            int i1 = Vertex(x, y + 1, z, xCount, yCount);
            int i2 = Vertex(x, y, z + 1, xCount, yCount);
            int i3 = Vertex(x, y + 1, z + 1, xCount, yCount);
            WriteQuad(i0, i1, i2, i3, positiveNormal, write);
        }

        private void WriteQuad(int i0, int i1, int i2, int i3, bool positiveNormal, int write)
        {
            if (positiveNormal)
            {
                Output[write] = i0;
                Output[write + 1] = i1;
                Output[write + 2] = i2;
                Output[write + 3] = i1;
                Output[write + 4] = i3;
                Output[write + 5] = i2;
                return;
            }

            Output[write] = i0;
            Output[write + 1] = i2;
            Output[write + 2] = i1;
            Output[write + 3] = i1;
            Output[write + 4] = i2;
            Output[write + 5] = i3;
        }

        private static int Vertex(int x, int y, int z, int xCount, int yCount)
        {
            return ((z * yCount) + y) * xCount + x;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CopyIndex16RangesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ushort> Source;

        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageSubMeshIndexRangeDTO> Ranges;

        // SAFETY: each range row owns a disjoint destination index window built by BuildTriangleSubMeshRanges.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<int> Output;

        public void Execute(int rangeIndex)
        {
            OfflineWreckageSubMeshIndexRangeDTO range = Ranges[rangeIndex];
            int sourceStart = range.SourceIndexStart;
            int destinationStart = range.DestinationIndexStart;
            int count = range.IndexCount;
            int baseVertex = range.BaseVertex;
            for (int i = 0; i < count; i++)
            {
                long adjusted = (long)Source[sourceStart + i] + baseVertex;
                if (adjusted > int.MaxValue)
                    adjusted = int.MaxValue;
                if (adjusted < int.MinValue)
                    adjusted = int.MinValue;
                Output[destinationStart + i] = (int)adjusted;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CopyIndex32RangesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<uint> Source;

        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageSubMeshIndexRangeDTO> Ranges;

        // SAFETY: each range row owns a disjoint destination index window built by BuildTriangleSubMeshRanges.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<int> Output;

        public void Execute(int rangeIndex)
        {
            OfflineWreckageSubMeshIndexRangeDTO range = Ranges[rangeIndex];
            int sourceStart = range.SourceIndexStart;
            int destinationStart = range.DestinationIndexStart;
            int count = range.IndexCount;
            int baseVertex = range.BaseVertex;
            for (int i = 0; i < count; i++)
            {
                int raw = (int)math.min(Source[sourceStart + i], (uint)int.MaxValue);
                long adjusted = (long)raw + baseVertex;
                if (adjusted > int.MaxValue)
                    adjusted = int.MaxValue;
                if (adjusted < int.MinValue)
                    adjusted = int.MinValue;
                Output[destinationStart + i] = (int)adjusted;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CopyBaseVerticesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Source;

        // SAFETY: IJobParallelFor writes only Destination[index]; source and destination buffers are disjoint.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Destination;

        public void Execute(int index)
        {
            Destination[index] = Source[index];
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockStructuralDeformationJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor writes only Vertices[index] during mock generation.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        public int3 Resolution;
        public float CellSize;
        public float GlobalQualityWeight;
        public float ShearTorsion;
        public float BlastRadius;
        public float3 BlastEpicenter;

        public void Execute(int index)
        {
            int xCount = math.max(Resolution.x, 1);
            int yCount = math.max(Resolution.y, 1);
            int zCount = math.max(Resolution.z, 1);
            int layer = xCount * yCount;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / xCount;
            int x = rem - (y * xCount);
            float cellSize = math.max(math.isfinite(CellSize) ? math.abs(CellSize) : 0.0001f, 0.0001f);
            float3 center = (new float3(xCount - 1, yCount - 1, zCount - 1) * 0.5f) * cellSize;
            float3 p = (new float3(x, y, z) * cellSize) - center;
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float torsion = math.clamp(math.isfinite(ShearTorsion) ? ShearTorsion : 0f, -8f, 8f);
            float3 axis = math.normalizesafe(new float3(0.13f + q, 1f, 0.31f), new float3(0f, 1f, 0f));
            float projection = math.dot(p, axis);
            projection = math.isfinite(projection) ? projection : 0f;
            float twist = torsion * (0.35f + q) * projection * 0.025f;
            twist = math.clamp(math.isfinite(twist) ? twist : 0f, -6.2831855f, 6.2831855f);
            p = RotateAroundAxis(p, axis, twist);

            float3 epicenter = math.all(math.isfinite(BlastEpicenter)) ? BlastEpicenter : float3.zero;
            float3 blastDelta = p - epicenter;
            blastDelta = math.all(math.isfinite(blastDelta)) ? blastDelta : float3.zero;
            float radius = math.clamp(math.isfinite(BlastRadius) ? math.abs(BlastRadius) : 0.001f, 0.001f, 100000f);
            float distSq = math.max(math.dot(blastDelta, blastDelta), 0.000001f);
            float dist = math.sqrt(distSq);
            float falloff = math.saturate(1f - (dist / radius));
            p += blastDelta * math.rsqrt(distSq) * falloff * falloff * radius * math.lerp(0.08f, 0.32f, q);
            p.x += p.y * torsion * 0.08f;
            p.z -= p.x * torsion * 0.03f;

            OfflineWreckageBakeVertexDTO* ptr = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref OfflineWreckageBakeVertexDTO vertex = ref UnsafeUtility.AsRef<OfflineWreckageBakeVertexDTO>(ptr + index);
            vertex.Position = math.all(math.isfinite(p)) ? p : float3.zero;
            vertex.Normal = new float3(0f, 1f, 0f);
            vertex.Tangent = new float4(1f, 0f, 0f, 1f);
            vertex.Uv0 = new float2((float)x / math.max(xCount - 1, 1), (float)z / math.max(zCount - 1, 1));
            byte scorch = (byte)math.clamp((int)math.round(falloff * 255f), 0, 255);
            vertex.PackedColor = OfflineWreckageBakeMath.PackColor(255, 255, scorch, 255);
            vertex.Uv3AupLocal = vertex.Position;
        }

        private static float3 RotateAroundAxis(float3 p, float3 axis, float angle)
        {
            float s = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angle);
            float c = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(angle);
            return (p * c) + (math.cross(axis, p) * s) + (axis * math.dot(axis, p) * (1f - c));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyStructuralShearJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor mutates only Vertices[index]; no cross-index reads or writes.
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        public float3 ShearAxis;
        public float ShearTorsion;
        public float CollapseCompression;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            OfflineWreckageBakeVertexDTO* ptr = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref OfflineWreckageBakeVertexDTO vertex = ref UnsafeUtility.AsRef<OfflineWreckageBakeVertexDTO>(ptr + index);
            float3 original = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 p = original;
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float torsion = math.isfinite(ShearTorsion) ? math.clamp(ShearTorsion, -8f, 8f) : 0f;
            float collapse = math.isfinite(CollapseCompression) ? math.saturate(CollapseCompression) : 0f;
            float3 axisInput = math.all(math.isfinite(ShearAxis)) ? ShearAxis : new float3(0f, 1f, 0f);
            float3 axis = math.normalizesafe(axisInput, new float3(0f, 1f, 0f));
            float projection = math.dot(p, axis);
            projection = math.isfinite(projection) ? projection : 0f;
            float angle = projection * torsion * math.lerp(0.015f, 0.055f, q);
            angle = math.clamp(math.isfinite(angle) ? angle : 0f, -6.2831855f, 6.2831855f);
            p = RotateAroundAxis(p, axis, angle);
            float3 lateral = math.cross(axis, p);
            lateral = math.all(math.isfinite(lateral)) ? lateral : float3.zero;
            p += lateral * (torsion * math.lerp(0.02f, 0.11f, q));
            p.y *= math.lerp(1f, 1f - collapse, collapse);
            vertex.Position = math.all(math.isfinite(p)) ? p : original;
            vertex.Uv3AupLocal = vertex.Position;
        }

        private static float3 RotateAroundAxis(float3 p, float3 axis, float angle)
        {
            float s = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angle);
            float c = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(angle);
            return (p * c) + (math.cross(axis, p) * s) + (axis * math.dot(axis, p) * (1f - c));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyRadialBlastJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor mutates only Vertices[index]; tear output is separate.
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        // SAFETY: IJobParallelFor writes only TearWeights[index]; vertex buffer is a separate lane.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<float> TearWeights;

        public float3 EpicenterLocal;
        public float Radius;
        public float TearThreshold;
        public float DamageScale;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            OfflineWreckageBakeVertexDTO* ptr = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref OfflineWreckageBakeVertexDTO vertex = ref UnsafeUtility.AsRef<OfflineWreckageBakeVertexDTO>(ptr + index);
            float3 original = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 epicenter = math.all(math.isfinite(EpicenterLocal)) ? EpicenterLocal : float3.zero;
            float3 delta = original - epicenter;
            delta = math.all(math.isfinite(delta)) ? delta : float3.zero;
            float radius = math.clamp(math.isfinite(Radius) ? math.abs(Radius) : 0.001f, 0.001f, 100000f);
            float damage = math.clamp(math.isfinite(DamageScale) ? DamageScale : 0f, -16f, 16f);
            float distSq = math.max(math.dot(delta, delta), 0.000001f);
            float dist = math.sqrt(distSq);
            float falloff = math.saturate(1f - (dist / radius));
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float3 dir = delta * math.rsqrt(distSq);
            float impulse = falloff * falloff * damage * radius * math.lerp(0.06f, 0.26f, q);
            float3 bent = original + (dir * impulse);
            float ring = math.saturate(1f - math.abs((dist / radius) - 0.52f) * 2.4f);
            bent += math.cross(dir, new float3(0.17f, 1f, 0.31f)) * ring * damage * math.lerp(0.03f, 0.18f, q) * radius;
            float voronoiEdge = ResolveVoronoiEdgeWeight(original, epicenter, radius, q);
            bent += dir * voronoiEdge * falloff * damage * radius * math.lerp(0.015f, 0.075f, q);
            vertex.Position = math.all(math.isfinite(bent)) ? bent : original;
            vertex.Uv3AupLocal = vertex.Position;

            float tearStart = math.saturate(math.isfinite(TearThreshold) ? TearThreshold : 1f);
            float radialTear = math.saturate((falloff - tearStart) * math.rcp(math.max(1f - tearStart, 0.001f)));
            float seamBand = math.saturate(1f - math.abs((dist / radius) - math.lerp(0.42f, 0.68f, q)) * math.lerp(2.3f, 1.35f, q));
            float tear = math.max(radialTear, voronoiEdge * seamBand * math.saturate(math.abs(damage)));
            TearWeights[index] = math.isfinite(tear) ? tear : 0f;
        }

        public static float ResolveVoronoiEdgeWeight(float3 position, float3 epicenter, float radius, float q)
        {
            int seedCount = math.clamp((int)math.round(math.lerp(5f, 31f, q)), 5, 31);
            float nearest = float.MaxValue;
            float secondNearest = float.MaxValue;
            for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
            {
                float3 seed = BuildVoronoiSeed((uint)seedIndex, epicenter, radius, q);
                float d = math.lengthsq(position - seed);
                bool closer = d < nearest;
                secondNearest = math.select(math.min(secondNearest, d), nearest, closer);
                nearest = math.select(nearest, d, closer);
            }

            float delta = math.sqrt(math.max(secondNearest, 0f)) - math.sqrt(math.max(nearest, 0f));
            float band = radius * math.lerp(0.18f, 0.045f, q);
            return math.saturate(1f - delta * math.rcp(math.max(band, 0.0001f)));
        }

        public static float3 BuildVoronoiSeed(uint index, float3 epicenter, float radius, float q)
        {
            float3 raw = new float3(
                Hash01(index, 101u) * 2f - 1f,
                Hash01(index, 137u) * 2f - 1f,
                Hash01(index, 173u) * 2f - 1f);
            float3 dir = math.normalizesafe(raw, new float3(1f, 0f, 0f));
            float radial = math.lerp(0.16f, 0.96f, Hash01(index, 211u));
            return epicenter + dir * radius * radial * math.lerp(0.72f, 1.12f, q);
        }

        public static float Hash01(uint index, uint lane)
        {
            uint hash = 0xA341316Cu ^ (index * 0x9E3779B9u) ^ (lane * 0x85EBCA6Bu);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BuildTornTrianglesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> SourceVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> SourceIndices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<float> TearWeights;

        [WriteOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> OutputVertices;

        [WriteOnly]
        [NoAlias]
        public NativeArray<int> OutputIndices;

        [WriteOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeCounters64> Counters;

        public float TearThreshold;
        public float SplitDistance;
        public float GlobalQualityWeight;
        public float3 EpicenterLocal;
        public float DamageScale;

        public void Execute()
        {
            OfflineWreckageBakeVertexDTO* src = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceVertices);
            OfflineWreckageBakeVertexDTO* dst = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputVertices);
            int* srcIndices = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceIndices);
            int* dstIndices = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputIndices);
            float* tears = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(TearWeights);
            int sourceVertexCount = SourceVertices.Length;
            for (int i = 0; i < sourceVertexCount; i++)
                dst[i] = src[i];

            int writeVertex = sourceVertexCount;
            int writeIndex = 0;
            int tornVertices = 0;
            int degenerateTriangles = 0;
            int fractureHoleTriangles = 0;
            float split = math.clamp(math.isfinite(SplitDistance) ? math.abs(SplitDistance) : 0.001f, 0.001f, 100000f);
            float threshold = math.saturate(math.isfinite(TearThreshold) ? TearThreshold : 1f);
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float damage = math.saturate(math.isfinite(DamageScale) ? DamageScale : 0f);
            float3 epicenter = math.all(math.isfinite(EpicenterLocal)) ? EpicenterLocal : float3.zero;
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            float tearDetail01 = math.smoothstep(0.18f, 0.82f, q);
            float fractureHoleCutoff = ResolveFractureHoleCutoff(threshold, q, damage);
            for (int i = 0; i < sourceVertexCount; i++)
            {
                float3 p = dst[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
            }

            int triCount = SourceIndices.Length / 3;
            for (int tri = 0; tri < triCount; tri++)
            {
                int baseIndex = tri * 3;
                int i0 = srcIndices[baseIndex];
                int i1 = srcIndices[baseIndex + 1];
                int i2 = srcIndices[baseIndex + 2];
                if (!IsValidIndex(i0, sourceVertexCount) || !IsValidIndex(i1, sourceVertexCount) || !IsValidIndex(i2, sourceVertexCount))
                {
                    degenerateTriangles++;
                    continue;
                }

                float tear = (tears[i0] + tears[i1] + tears[i2]) * 0.33333334f;
                tear = math.saturate(math.isfinite(tear) ? tear : 0f);
                if (tear > fractureHoleCutoff)
                {
                    fractureHoleTriangles++;
                    continue;
                }

                float tearVisual01 = threshold < 0.9999f ? math.smoothstep(threshold, 1f, tear) * tearDetail01 : 0f;
                if (writeIndex + 3 > OutputIndices.Length)
                {
                    degenerateTriangles++;
                    continue;
                }

                if (tearVisual01 > 0.0001f && writeVertex + 2 < OutputVertices.Length)
                {
                    OfflineWreckageBakeVertexDTO v0 = src[i0];
                    OfflineWreckageBakeVertexDTO v1 = src[i1];
                    OfflineWreckageBakeVertexDTO v2 = src[i2];
                    v0.Position = math.all(math.isfinite(v0.Position)) ? v0.Position : float3.zero;
                    v1.Position = math.all(math.isfinite(v1.Position)) ? v1.Position : float3.zero;
                    v2.Position = math.all(math.isfinite(v2.Position)) ? v2.Position : float3.zero;
                    float3 normal = math.normalizesafe(math.cross(v1.Position - v0.Position, v2.Position - v0.Position), new float3(0f, 1f, 0f));
                    float offset = split * tear * tearVisual01;
                    float3 splitOffset = math.all(math.isfinite(normal)) && math.isfinite(offset) ? normal * offset : float3.zero;
                    v0.Position += splitOffset;
                    v1.Position += splitOffset;
                    v2.Position += splitOffset;
                    v0.Uv3AupLocal = v0.Position;
                    v1.Uv3AupLocal = v1.Position;
                    v2.Uv3AupLocal = v2.Position;
                    dst[writeVertex] = v0;
                    dst[writeVertex + 1] = v1;
                    dst[writeVertex + 2] = v2;
                    dstIndices[writeIndex++] = writeVertex;
                    dstIndices[writeIndex++] = writeVertex + 1;
                    dstIndices[writeIndex++] = writeVertex + 2;
                    writeVertex += 3;
                    tornVertices += 3;
                }
                else
                {
                    dstIndices[writeIndex++] = i0;
                    dstIndices[writeIndex++] = i1;
                    dstIndices[writeIndex++] = i2;
                }
            }

            if (math.all(math.isfinite(min)) && math.all(math.isfinite(max)) && math.all(max > min))
            {
                float3 boundsCenter = (min + max) * 0.5f;
                float3 boundsExtents = math.max((max - min) * 0.5f, new float3(0.05f));
                AppendSupportBeams(dst, dstIndices, ref writeVertex, ref writeIndex, boundsCenter, boundsExtents, epicenter, q, damage);
                AppendMergedDebris(dst, dstIndices, ref writeVertex, ref writeIndex, boundsCenter, boundsExtents, epicenter, q, damage);
            }

            if (Counters.Length > 0)
            {
                OfflineWreckageBakeCounters64 counters = default;
                counters.ActiveVertexCount = writeVertex;
                counters.ActiveIndexCount = writeIndex;
                counters.TornVertexCount = tornVertices;
                counters.DegenerateTriangleCount = degenerateTriangles;
                counters.FractureHoleTriangleCount = fractureHoleTriangles;
                if (fractureHoleTriangles > 0)
                    counters.WarningFlags |= OfflineWreckageBakeConstants.WarningFractureHolesGenerated;
                Counters[0] = counters;
            }
        }

        private static bool IsValidIndex(int index, int count)
        {
            return (uint)index < (uint)count;
        }

        private static float ResolveFractureHoleCutoff(float threshold, float q, float damage)
        {
            float deterministicCsg01 = math.saturate(damage * math.lerp(0.62f, 1.18f, q));
            float targetCutoff = math.saturate(threshold * math.lerp(0.95f, 0.58f, q));
            return math.clamp(math.lerp(1.01f, targetCutoff, deterministicCsg01), 0.08f, 1.01f);
        }

        private void AppendSupportBeams(
            OfflineWreckageBakeVertexDTO* vertices,
            int* indices,
            ref int writeVertex,
            ref int writeIndex,
            float3 boundsCenter,
            float3 boundsExtents,
            float3 epicenter,
            float q,
            float damage)
        {
            int supportCount = (int)math.round(math.lerp(1f, 7f, q) * math.saturate(damage));
            float radius = math.lerp(0.04f, 0.14f, q);
            for (int beam = 0; beam < supportCount; beam++)
            {
                if (writeVertex + 24 > OutputVertices.Length || writeIndex + 108 > OutputIndices.Length)
                    return;

                float hashA = Hash01((uint)beam, 11u);
                float hashB = Hash01((uint)beam, 17u);
                float hashC = Hash01((uint)beam, 23u);
                float3 center = boundsCenter + new float3(
                    (hashA - 0.5f) * boundsExtents.x * 1.45f,
                    (hashB - 0.5f) * boundsExtents.y * 0.85f,
                    (hashC - 0.5f) * boundsExtents.z * 1.45f);
                float3 toBlast = math.normalizesafe(center - epicenter, new float3(1f, 0f, 0f));
                center += toBlast * radius * math.lerp(0.8f, 3.0f, q);
                float3 size = new float3(radius, math.max(boundsExtents.y * math.lerp(0.25f, 0.85f, hashB), radius), radius);
                float noiseA = noise.snoise(new float3(hashA * 17f, beam + 0.13f, q * 31f));
                float noiseB = noise.snoise(new float3(hashC * 19f, beam + 0.37f, damage * 29f));
                float buckle = math.lerp(0.16f, 0.55f, q) * math.saturate(damage);
                float3 beamAxis = math.normalizesafe(
                    new float3(
                        ((hashA - 0.5f) * 0.45f) + noiseA * buckle,
                        1f,
                        ((hashC - 0.5f) * 0.45f) + noiseB * buckle),
                    new float3(0f, 1f, 0f));
                AppendIBeam(vertices, indices, ref writeVertex, ref writeIndex, center, size.y, radius, beamAxis, toBlast, OfflineWreckageBakeMath.PackColor(220, 32, 96, 210));
            }
        }

        private void AppendMergedDebris(
            OfflineWreckageBakeVertexDTO* vertices,
            int* indices,
            ref int writeVertex,
            ref int writeIndex,
            float3 boundsCenter,
            float3 boundsExtents,
            float3 epicenter,
            float q,
            float damage)
        {
            int shardCount = (int)math.round(math.lerp(4f, 48f, q) * math.saturate(damage));
            float radius = math.length(boundsExtents) * math.lerp(0.04f, 0.12f, q);
            for (int shard = 0; shard < shardCount; shard++)
            {
                if (writeVertex + 3 > OutputVertices.Length || writeIndex + 3 > OutputIndices.Length)
                    return;

                float3 radial = math.normalizesafe(new float3(
                    Hash01((uint)shard, 31u) - 0.5f,
                    Hash01((uint)shard, 37u) * 0.25f,
                    Hash01((uint)shard, 41u) - 0.5f), new float3(1f, 0f, 0f));
                float3 tangent = math.normalizesafe(math.cross(radial, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));
                float3 bitangent = math.normalizesafe(math.cross(radial, tangent), new float3(0f, 1f, 0f));
                float3 center = epicenter + radial * radius * math.lerp(1.5f, 7f, Hash01((uint)shard, 43u));
                center = math.clamp(center, boundsCenter - boundsExtents * 1.35f, boundsCenter + boundsExtents * 1.35f);
                float shardScale = radius * math.lerp(0.35f, 1.25f, Hash01((uint)shard, 47u));
                float floorY = ResolveProjectedDebrisFloorY(center, boundsCenter, boundsExtents, (uint)shard, q, damage);
                center.y = math.clamp(floorY + shardScale * 0.06f, boundsCenter.y - boundsExtents.y, boundsCenter.y + boundsExtents.y);
                int baseVertex = writeVertex;
                uint color = OfflineWreckageBakeMath.PackColor(235, 24, 180, 230);
                vertices[writeVertex++] = BuildVertex(center + tangent * shardScale, radial, color);
                vertices[writeVertex++] = BuildVertex(center - tangent * shardScale * 0.72f + bitangent * shardScale * 0.5f, radial, color);
                vertices[writeVertex++] = BuildVertex(center - tangent * shardScale * 0.33f - bitangent * shardScale, radial, color);
                indices[writeIndex++] = baseVertex;
                indices[writeIndex++] = baseVertex + 1;
                indices[writeIndex++] = baseVertex + 2;

                if ((shard & 7) == 0 && writeVertex + 8 <= OutputVertices.Length && writeIndex + 36 <= OutputIndices.Length)
                {
                    float3 rodAxis = math.normalizesafe(
                        tangent + bitangent * ((Hash01((uint)shard, 61u) - 0.5f) * 0.65f) + radial * 0.18f,
                        tangent);
                    float3 rodSide = math.normalizesafe(math.cross(radial, rodAxis), bitangent);
                    float3 rodUp = math.normalizesafe(math.cross(rodAxis, rodSide), radial);
                    float rodHalfLength = shardScale * math.lerp(0.85f, 2.1f, Hash01((uint)shard, 67u));
                    float rodRadius = math.max(shardScale * math.lerp(0.025f, 0.055f, q), 0.008f);
                    float3 rodCenter = center + radial * shardScale * 0.18f;
                    rodCenter.y = math.clamp(rodCenter.y + rodRadius * 1.35f, boundsCenter.y - boundsExtents.y, boundsCenter.y + boundsExtents.y);
                    AppendOrientedBox(
                        vertices,
                        indices,
                        ref writeVertex,
                        ref writeIndex,
                        rodCenter,
                        new float3(rodHalfLength, rodRadius, rodRadius),
                        rodAxis,
                        rodSide,
                        rodUp,
                        OfflineWreckageBakeMath.PackColor(165, 36, 132, 235));
                }
            }
        }

        private float ResolveProjectedDebrisFloorY(float3 point, float3 boundsCenter, float3 boundsExtents, uint shard, float q, float damage)
        {
            float tiltScale = math.lerp(0.015f, 0.12f, q) * math.saturate(damage);
            float tiltX = (Hash01(shard, 53u) - 0.5f) * tiltScale;
            float tiltZ = (Hash01(shard, 59u) - 0.5f) * tiltScale;
            float2 local = new float2(point.x - boundsCenter.x, point.z - boundsCenter.z);
            float baseY = boundsCenter.y - math.max(boundsExtents.y, 0.05f);
            return baseY + math.dot(local, new float2(tiltX, tiltZ));
        }

        private static void AppendBox(
            OfflineWreckageBakeVertexDTO* vertices,
            int* indices,
            ref int writeVertex,
            ref int writeIndex,
            float3 center,
            float3 size,
            uint color)
        {
            int baseVertex = writeVertex;
            float3 e = math.max(size, new float3(0.01f));
            float3 p0 = center + new float3(-e.x, -e.y, -e.z);
            float3 p1 = center + new float3(e.x, -e.y, -e.z);
            float3 p2 = center + new float3(e.x, e.y, -e.z);
            float3 p3 = center + new float3(-e.x, e.y, -e.z);
            float3 p4 = center + new float3(-e.x, -e.y, e.z);
            float3 p5 = center + new float3(e.x, -e.y, e.z);
            float3 p6 = center + new float3(e.x, e.y, e.z);
            float3 p7 = center + new float3(-e.x, e.y, e.z);
            vertices[writeVertex++] = BuildVertex(p0, math.normalizesafe(p0 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p1, math.normalizesafe(p1 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p2, math.normalizesafe(p2 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p3, math.normalizesafe(p3 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p4, math.normalizesafe(p4 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p5, math.normalizesafe(p5 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p6, math.normalizesafe(p6 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p7, math.normalizesafe(p7 - center, new float3(0f, 1f, 0f)), color);
            WriteBoxIndices(indices, ref writeIndex, baseVertex);
        }

        private static void AppendIBeam(
            OfflineWreckageBakeVertexDTO* vertices,
            int* indices,
            ref int writeVertex,
            ref int writeIndex,
            float3 center,
            float halfLength,
            float radius,
            float3 beamAxis,
            float3 blastAxis,
            uint color)
        {
            float3 axisY = math.normalizesafe(beamAxis, new float3(0f, 1f, 0f));
            float3 axisX = math.normalizesafe(math.cross(blastAxis, axisY), new float3(1f, 0f, 0f));
            float3 axisZ = math.normalizesafe(math.cross(axisX, axisY), new float3(0f, 0f, 1f));
            float flangeHalfY = math.max(radius * 0.22f, 0.0125f);
            float safeHalfLength = math.max(halfLength, flangeHalfY * 2f);
            float3 webHalf = new float3(radius * 0.42f, safeHalfLength, radius * 0.42f);
            float3 flangeHalf = new float3(radius * 1.85f, flangeHalfY, radius * 0.72f);
            float flangeOffset = math.max(0f, safeHalfLength - flangeHalfY);

            AppendOrientedBox(vertices, indices, ref writeVertex, ref writeIndex, center, webHalf, axisX, axisY, axisZ, color);
            AppendOrientedBox(vertices, indices, ref writeVertex, ref writeIndex, center + axisY * flangeOffset, flangeHalf, axisX, axisY, axisZ, color);
            AppendOrientedBox(vertices, indices, ref writeVertex, ref writeIndex, center - axisY * flangeOffset, flangeHalf, axisX, axisY, axisZ, color);
        }

        private static void AppendOrientedBox(
            OfflineWreckageBakeVertexDTO* vertices,
            int* indices,
            ref int writeVertex,
            ref int writeIndex,
            float3 center,
            float3 halfExtents,
            float3 axisX,
            float3 axisY,
            float3 axisZ,
            uint color)
        {
            int baseVertex = writeVertex;
            float3 x = axisX * math.max(halfExtents.x, 0.01f);
            float3 y = axisY * math.max(halfExtents.y, 0.01f);
            float3 z = axisZ * math.max(halfExtents.z, 0.01f);
            float3 p0 = center - x - y - z;
            float3 p1 = center + x - y - z;
            float3 p2 = center + x + y - z;
            float3 p3 = center - x + y - z;
            float3 p4 = center - x - y + z;
            float3 p5 = center + x - y + z;
            float3 p6 = center + x + y + z;
            float3 p7 = center - x + y + z;
            vertices[writeVertex++] = BuildVertex(p0, math.normalizesafe(p0 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p1, math.normalizesafe(p1 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p2, math.normalizesafe(p2 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p3, math.normalizesafe(p3 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p4, math.normalizesafe(p4 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p5, math.normalizesafe(p5 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p6, math.normalizesafe(p6 - center, new float3(0f, 1f, 0f)), color);
            vertices[writeVertex++] = BuildVertex(p7, math.normalizesafe(p7 - center, new float3(0f, 1f, 0f)), color);
            WriteBoxIndices(indices, ref writeIndex, baseVertex);
        }

        private static OfflineWreckageBakeVertexDTO BuildVertex(float3 position, float3 normal, uint color)
        {
            OfflineWreckageBakeVertexDTO vertex = default;
            vertex.Position = math.all(math.isfinite(position)) ? position : float3.zero;
            vertex.Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            float3 helper = math.abs(vertex.Normal.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 tangent = math.normalizesafe(math.cross(helper, vertex.Normal), new float3(1f, 0f, 0f));
            vertex.Tangent = new float4(tangent, 1f);
            vertex.Uv0 = float2.zero;
            vertex.PackedColor = color;
            vertex.Uv3AupLocal = vertex.Position;
            return vertex;
        }

        private static void WriteBoxIndices(int* indices, ref int writeIndex, int b)
        {
            indices[writeIndex++] = b; indices[writeIndex++] = b + 2; indices[writeIndex++] = b + 1;
            indices[writeIndex++] = b; indices[writeIndex++] = b + 3; indices[writeIndex++] = b + 2;
            indices[writeIndex++] = b + 4; indices[writeIndex++] = b + 5; indices[writeIndex++] = b + 6;
            indices[writeIndex++] = b + 4; indices[writeIndex++] = b + 6; indices[writeIndex++] = b + 7;
            indices[writeIndex++] = b; indices[writeIndex++] = b + 1; indices[writeIndex++] = b + 5;
            indices[writeIndex++] = b; indices[writeIndex++] = b + 5; indices[writeIndex++] = b + 4;
            indices[writeIndex++] = b + 1; indices[writeIndex++] = b + 2; indices[writeIndex++] = b + 6;
            indices[writeIndex++] = b + 1; indices[writeIndex++] = b + 6; indices[writeIndex++] = b + 5;
            indices[writeIndex++] = b + 2; indices[writeIndex++] = b + 3; indices[writeIndex++] = b + 7;
            indices[writeIndex++] = b + 2; indices[writeIndex++] = b + 7; indices[writeIndex++] = b + 6;
            indices[writeIndex++] = b + 3; indices[writeIndex++] = b; indices[writeIndex++] = b + 4;
            indices[writeIndex++] = b + 3; indices[writeIndex++] = b + 4; indices[writeIndex++] = b + 7;
        }

        private float Hash01(uint index, uint lane)
        {
            uint hash = (uint)SourceIndices.Length ^ (index * 0x9E3779B9u) ^ (lane * 0x85EBCA6Bu);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecalculateDeformedNormalsJob : IJob
    {
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> Indices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeCounters64> Counters;

        public void Execute()
        {
            OfflineWreckageBakeCounters64 counters = Counters.Length > 0 ? Counters[0] : default;
            int vertexCount = math.clamp(counters.ActiveVertexCount, 0, Vertices.Length);
            int indexCount = Counters.Length > 0
                ? math.clamp(counters.ActiveIndexCount, 0, Indices.Length)
                : Indices.Length;
            indexCount -= indexCount % 3;
            OfflineWreckageBakeVertexDTO* vertices = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            int* indices = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Indices);
            for (int i = 0; i < vertexCount; i++)
                vertices[i].Normal = float3.zero;

            int triCount = indexCount / 3;
            for (int tri = 0; tri < triCount; tri++)
            {
                int baseIndex = tri * 3;
                int i0 = indices[baseIndex];
                int i1 = indices[baseIndex + 1];
                int i2 = indices[baseIndex + 2];
                if (!IsValidIndex(i0, vertexCount) || !IsValidIndex(i1, vertexCount) || !IsValidIndex(i2, vertexCount) ||
                    i0 == i1 || i1 == i2 || i0 == i2)
                {
                    continue;
                }

                float3 p0 = vertices[i0].Position;
                float3 p1 = vertices[i1].Position;
                float3 p2 = vertices[i2].Position;
                float3 e0 = p1 - p0;
                float3 e1 = p2 - p0;
                float3 face = math.cross(e0, e1);
                float lenSq = math.dot(face, face);
                if (!math.isfinite(lenSq) || lenSq <= 0.0000001f)
                    continue;

                float3 n = face * math.rsqrt(lenSq);
                float a0 = Angle(p1 - p0, p2 - p0);
                float a1 = Angle(p2 - p1, p0 - p1);
                float a2 = Angle(p0 - p2, p1 - p2);
                vertices[i0].Normal += n * a0;
                vertices[i1].Normal += n * a1;
                vertices[i2].Normal += n * a2;
            }

            for (int i = 0; i < vertexCount; i++)
            {
                float3 n = math.normalizesafe(vertices[i].Normal, new float3(0f, 1f, 0f));
                vertices[i].Normal = n;
                float3 helper = math.abs(n.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                float3 tangent = math.normalizesafe(math.cross(helper, n), new float3(1f, 0f, 0f));
                vertices[i].Tangent = new float4(tangent, 1f);
            }
        }

        private static bool IsValidIndex(int index, int count)
        {
            return (uint)index < (uint)count;
        }

        private static float Angle(float3 a, float3 b)
        {
            float la = math.dot(a, a);
            float lb = math.dot(b, b);
            if (!math.isfinite(la) || !math.isfinite(lb) || la <= 0.0000001f || lb <= 0.0000001f)
                return 0f;

            float denom = math.max(la * lb, 0.0000001f);
            float d = math.dot(a, b) * math.rsqrt(denom);
            return math.isfinite(d) ? global::Hecton8.Core.MathLodApproximation.ApproxAcosFast(math.clamp(d, -1f, 1f)) : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BakeDamageColorsJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor mutates only Vertices[index]; color data is derived from immutable scalar inputs.
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeCounters64> Counters;

        public float3 EpicenterLocal;
        public float BlastRadius;
        public float ScorchIntensity;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int vertexCount = Counters.Length > 0 ? math.clamp(Counters[0].ActiveVertexCount, 0, Vertices.Length) : 0;
            if (index >= vertexCount)
                return;

            OfflineWreckageBakeVertexDTO* ptr = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref OfflineWreckageBakeVertexDTO vertex = ref UnsafeUtility.AsRef<OfflineWreckageBakeVertexDTO>(ptr + index);
            float3 position = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 normal = math.all(math.isfinite(vertex.Normal)) ? vertex.Normal : new float3(0f, 1f, 0f);
            float3 epicenter = math.all(math.isfinite(EpicenterLocal)) ? EpicenterLocal : float3.zero;
            float radius = math.clamp(math.isfinite(BlastRadius) ? math.abs(BlastRadius) : 0.001f, 0.001f, 100000f);
            float intensity = math.clamp(math.isfinite(ScorchIntensity) ? ScorchIntensity : 0f, 0f, 16f);
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float distSq = math.lengthsq(position - epicenter);
            distSq = math.max(math.isfinite(distSq) ? distSq : 0.000001f, 0.000001f);
            float dist = math.sqrt(distSq);
            float scorch01 = math.saturate(1f - (dist / radius));
            scorch01 = math.saturate(scorch01 * intensity * math.lerp(0.8f, 1.45f, q));
            float concavity01 = math.saturate(1f - math.abs(normal.y));
            byte edgeWear = (byte)math.clamp((int)math.round(math.saturate(scorch01 * 0.55f + concavity01 * 0.35f) * 255f), 0, 255);
            byte corrosion = (byte)math.clamp((int)math.round(math.saturate(scorch01 * 0.12f + concavity01 * 0.38f) * 255f), 0, 255);
            byte grime = (byte)math.clamp((int)math.round(math.saturate(scorch01 * 0.48f + concavity01 * 0.46f) * 255f), 0, 255);
            byte soot = (byte)math.clamp((int)math.round(math.saturate(scorch01 * math.lerp(0.72f, 1f, q)) * 255f), 0, 255);
            vertex.Position = position;
            vertex.Normal = normal;
            vertex.Uv3AupLocal = position;
            vertex.PackedColor = OfflineWreckageBakeMath.PackColor(edgeWear, corrosion, grime, soot);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BendFractureNormalsJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor mutates only Vertices[index]; counters are immutable after torn triangle construction.
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeCounters64> Counters;

        public float3 EpicenterLocal;
        public float BlastRadius;
        public float DamageScale;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int vertexCount = Counters.Length > 0 ? math.clamp(Counters[0].ActiveVertexCount, 0, Vertices.Length) : 0;
            if (index >= vertexCount)
                return;

            OfflineWreckageBakeVertexDTO* ptr = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref OfflineWreckageBakeVertexDTO vertex = ref UnsafeUtility.AsRef<OfflineWreckageBakeVertexDTO>(ptr + index);
            float3 position = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 normal = math.normalizesafe(
                math.all(math.isfinite(vertex.Normal)) ? vertex.Normal : new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f));
            float3 epicenter = math.all(math.isfinite(EpicenterLocal)) ? EpicenterLocal : float3.zero;
            float radius = math.clamp(math.isfinite(BlastRadius) ? math.abs(BlastRadius) : 0.001f, 0.001f, 100000f);
            float damage = math.saturate(math.isfinite(DamageScale) ? DamageScale : 0f);
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float3 delta = position - epicenter;
            delta = math.all(math.isfinite(delta)) ? delta : float3.zero;
            float distSq = math.max(math.lengthsq(delta), 0.000001f);
            float dist = math.sqrt(distSq);
            float3 radial = delta * math.rsqrt(distSq);
            float voronoiEdge = ApplyRadialBlastJob.ResolveVoronoiEdgeWeight(position, epicenter, radius, q);
            float seamRing = math.saturate(1f - math.abs((dist / radius) - math.lerp(0.42f, 0.68f, q)) * math.lerp(2.25f, 1.35f, q));
            float peel = math.saturate(voronoiEdge * seamRing * damage);
            float3 shear = math.normalizesafe(math.cross(radial, normal), new float3(0f, 1f, 0f));
            float3 bent = normal + radial * peel * math.lerp(0.35f, 1.1f, q) + shear * peel * math.lerp(0.05f, 0.22f, q);
            bent = math.normalizesafe(math.all(math.isfinite(bent)) ? bent : normal, normal);
            vertex.Position = position;
            vertex.Normal = bent;
            float3 helper = math.abs(bent.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 tangent = math.normalizesafe(math.cross(helper, bent), new float3(1f, 0f, 0f));
            vertex.Tangent = new float4(tangent, 1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateConvexHullsJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<OfflineWreckageBakeVertexDTO> Vertices;

        [NoAlias]
        public NativeArray<OfflineWreckageBakeCounters64> Counters;

        [NoAlias]
        [WriteOnly]
        public NativeArray<float3> HullPoints;

        public void Execute()
        {
            if (Counters.Length <= 0)
                return;

            int vertexCount = math.clamp(Counters[0].ActiveVertexCount, 0, Vertices.Length);
            if (vertexCount <= 0 || HullPoints.Length < OfflineWreckageBakeConstants.SupportHullPointCount)
            {
                OfflineWreckageBakeCounters64 counters = Counters[0];
                counters.HullVertexCount = 0;
                Counters[0] = counters;
                return;
            }

            OfflineWreckageBakeVertexDTO* vertices = (OfflineWreckageBakeVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Vertices);
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            for (int i = 0; i < vertexCount; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
            }

            OfflineWreckageBakeCounters64 output = Counters[0];
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
            {
                min = new float3(-0.5f);
                max = new float3(0.5f);
                output.WarningFlags |= OfflineWreckageBakeConstants.WarningNonFiniteFallback;
            }
            else
            {
                const float MinHullHalfExtent = 0.01f;
                float3 center = (min + max) * 0.5f;
                float3 halfExtent = (max - min) * 0.5f;
                float3 expandedHalfExtent = math.max(halfExtent, new float3(MinHullHalfExtent));
                if (math.any(expandedHalfExtent > halfExtent))
                    output.WarningFlags |= OfflineWreckageBakeConstants.WarningHullBoundsExpanded;

                min = center - expandedHalfExtent;
                max = center + expandedHalfExtent;
            }

            HullPoints[0] = new float3(min.x, min.y, min.z);
            HullPoints[1] = new float3(max.x, min.y, min.z);
            HullPoints[2] = new float3(max.x, min.y, max.z);
            HullPoints[3] = new float3(min.x, min.y, max.z);
            HullPoints[4] = new float3(min.x, max.y, min.z);
            HullPoints[5] = new float3(max.x, max.y, min.z);
            HullPoints[6] = new float3(max.x, max.y, max.z);
            HullPoints[7] = new float3(min.x, max.y, max.z);
            output.HullVertexCount = OfflineWreckageBakeConstants.SupportHullPointCount;
            Counters[0] = output;
        }
    }
}
