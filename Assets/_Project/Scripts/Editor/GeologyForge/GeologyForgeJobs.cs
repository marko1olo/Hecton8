using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.GeologyForge
{
    internal static class GeologyTetraExtractionLut
    {
        private const int Edge01 = 0;
        private const int Edge02 = 1;
        private const int Edge03 = 2;
        private const int Edge12 = 3;
        private const int Edge13 = 4;
        private const int Edge23 = 5;
        private const int EdgeNone = 15;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CaseIndex(float d0, float d1, float d2, float d3)
        {
            int c0 = d0 < 0f ? 1 : 0;
            int c1 = d1 < 0f ? 2 : 0;
            int c2 = d2 < 0f ? 4 : 0;
            int c3 = d3 < 0f ? 8 : 0;
            return c0 | c1 | c2 | c3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int VertexCount(int caseIndex)
        {
            switch (caseIndex)
            {
                case 0:
                case 15:
                    return 0;
                case 3:
                case 5:
                case 6:
                case 9:
                case 10:
                case 12:
                    return 6;
                default:
                    return 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint EdgeSequence(int caseIndex)
        {
            switch (caseIndex)
            {
                case 1:
                    return Pack3(Edge01, Edge02, Edge03);
                case 14:
                    return Pack3(Edge03, Edge02, Edge01);
                case 2:
                    return Pack3(Edge01, Edge13, Edge12);
                case 13:
                    return Pack3(Edge12, Edge13, Edge01);
                case 4:
                    return Pack3(Edge02, Edge12, Edge23);
                case 11:
                    return Pack3(Edge23, Edge12, Edge02);
                case 8:
                    return Pack3(Edge23, Edge13, Edge03);
                case 7:
                    return Pack3(Edge03, Edge13, Edge23);
                case 3:
                    return Pack6(Edge02, Edge12, Edge03, Edge03, Edge12, Edge13);
                case 5:
                    return Pack6(Edge01, Edge12, Edge03, Edge03, Edge12, Edge23);
                case 6:
                    return Pack6(Edge01, Edge02, Edge13, Edge13, Edge02, Edge23);
                case 9:
                    return Pack6(Edge13, Edge02, Edge01, Edge23, Edge02, Edge13);
                case 10:
                    return Pack6(Edge03, Edge12, Edge01, Edge23, Edge12, Edge03);
                case 12:
                    return Pack6(Edge03, Edge12, Edge02, Edge13, Edge12, Edge03);
                default:
                    return Pack3(EdgeNone, EdgeNone, EdgeNone);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int EdgeAt(uint packedEdges, int slot)
        {
            return (int)((packedEdges >> (slot * 4)) & 15u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack3(int e0, int e1, int e2)
        {
            return Pack6(e0, e1, e2, EdgeNone, EdgeNone, EdgeNone);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack6(int e0, int e1, int e2, int e3, int e4, int e5)
        {
            return (uint)(e0 | (e1 << 4) | (e2 << 8) | (e3 << 12) | (e4 << 16) | (e5 << 20));
        }

        internal static void ValidateComplementWinding()
        {
            for (int caseIndex = 1; caseIndex < 15; caseIndex++)
            {
                int complement = 15 - caseIndex;
                if (caseIndex > complement)
                    continue;

                int count = VertexCount(caseIndex);
                if (count != VertexCount(complement))
                    throw new InvalidOperationException("Geology tetra LUT count/complement mismatch.");

                uint sequence = EdgeSequence(caseIndex);
                uint complementSequence = EdgeSequence(complement);
                for (int i = 0; i < count; i += 3)
                {
                    if (EdgeAt(sequence, i) != EdgeAt(complementSequence, i + 2) ||
                        EdgeAt(sequence, i + 1) != EdgeAt(complementSequence, i + 1) ||
                        EdgeAt(sequence, i + 2) != EdgeAt(complementSequence, i))
                    {
                        throw new InvalidOperationException("Geology tetra LUT complement winding mismatch.");
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockFractalNoiseJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<float> Density;

        public double3 SectorAup;
        public uint Seed;
        public int Points;
        public int Octaves;
        public float VoxelStep;
        public float RadiusMeters;
        public float HeightScale;
        public float Frequency;
        public float NoiseAmplitude;
        public float RidgedWeight;
        public float VoronoiWeight;
        public float IsoLevel;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int x = index % Points;
            int y = (index / Points) % Points;
            int z = index / (Points * Points);
            float center = (Points - 1) * 0.5f;
            float3 local = new float3(x - center, y - center, z - center) * VoxelStep;
            double3 aup = SectorAup + new double3(local.x, local.y, local.z);
            double3 relative = aup - SectorAup;
            float3 p = new float3((float)relative.x, (float)relative.y, (float)relative.z);

            float3 radii = new float3(
                math.max(0.05f, RadiusMeters),
                math.max(0.05f, RadiusMeters * math.max(0.15f, HeightScale)),
                math.max(0.05f, RadiusMeters * 0.82f));
            float3 q = p / radii;
            float qLenSq = math.lengthsq(q);
            float ellipsoid = (qLenSq * math.rsqrt(math.max(qLenSq, 1e-12f)) - 1f) * math.cmin(radii);

            float qualityCurve = math.smoothstep(0f, 1f, math.saturate(GlobalQualityWeight));
            float safeFrequency = math.max(0.001f, Frequency) * math.lerp(0.82f, 1f, qualityCurve);
            float amplitude = math.max(0f, NoiseAmplitude) * math.lerp(0.55f, 1f, qualityCurve);
            float fbm = 0f;
            float amp = 0.5f;
            float freq = safeFrequency;
            float3 seedOffset = new float3(
                (Seed & 1023u) * 0.013671875f,
                ((Seed >> 10) & 1023u) * 0.015625f,
                ((Seed >> 20) & 1023u) * 0.017578125f);

            float octaveSpan = math.lerp(1.5f, math.clamp(Octaves, 1, 8), qualityCurve);
            int octaveCount = math.clamp((int)math.ceil(octaveSpan), 1, 8);
            float ridgedWeight = math.saturate(RidgedWeight) * math.lerp(0.45f, 1f, qualityCurve);
            for (int i = 0; i < octaveCount; i++)
            {
                float n = noise.snoise(p * freq + seedOffset);
                float ridged = 1f - math.abs(n);
                float octaveWeight = math.saturate(octaveSpan - i);
                fbm += math.lerp(n, ridged * 2f - 1f, ridgedWeight) * amp * octaveWeight;
                freq *= 2.03f;
                amp *= 0.48f;
            }

            float voronoiFrequency = math.lerp(0.35f, 0.65f, qualityCurve);
            float voronoi = EvaluateVoronoi(p * safeFrequency * voronoiFrequency + seedOffset);
            float displacement = (fbm * amplitude) + ((voronoi - 0.5f) * amplitude * math.saturate(VoronoiWeight) * qualityCurve);
            float density = ellipsoid + displacement - IsoLevel;
            Density[index] = math.isfinite(density) ? density : 1f;
        }

        private float EvaluateVoronoi(float3 p)
        {
            int3 cell = (int3)math.floor(p);
            float3 f = p - math.floor(p);
            float minDistance = 8f;
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int3 candidate = cell + new int3(ox, oy, oz);
                        float3 jitter = new float3(
                            Hash01(candidate, 0xA341316Cu),
                            Hash01(candidate, 0xC8013EA4u),
                            Hash01(candidate, 0xAD90777Du));
                        float3 delta = new float3(ox, oy, oz) + jitter - f;
                        minDistance = math.min(minDistance, math.lengthsq(delta));
                    }
                }
            }

            return math.saturate(minDistance);
        }

        private float Hash01(int3 p, uint salt)
        {
            uint h = Seed ^ salt;
            h ^= (uint)p.x * 0x9E3779B9u;
            h ^= (uint)p.y * 0x85EBCA6Bu;
            h ^= (uint)p.z * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h * 2.3283064e-10f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct SdfCellVertexCountJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Density;
        [WriteOnly, NoAlias] public NativeArray<int> CellVertexCounts;

        public int Points;
        public int Cells;

        public void Execute(int cellIndex)
        {
            ResolveCell(cellIndex, out int x, out int y, out int z);
            float d0 = Sample(x, y, z);
            float d1 = Sample(x + 1, y, z);
            float d2 = Sample(x + 1, y + 1, z);
            float d3 = Sample(x, y + 1, z);
            float d4 = Sample(x, y, z + 1);
            float d5 = Sample(x + 1, y, z + 1);
            float d6 = Sample(x + 1, y + 1, z + 1);
            float d7 = Sample(x, y + 1, z + 1);
            int count = 0;
            count += TetraVertexCount(d0, d5, d1, d6);
            count += TetraVertexCount(d0, d1, d2, d6);
            count += TetraVertexCount(d0, d2, d3, d6);
            count += TetraVertexCount(d0, d3, d7, d6);
            count += TetraVertexCount(d0, d7, d4, d6);
            count += TetraVertexCount(d0, d4, d5, d6);
            CellVertexCounts[cellIndex] = count;
        }

        private static int TetraVertexCount(float d0, float d1, float d2, float d3)
        {
            int caseIndex = GeologyTetraExtractionLut.CaseIndex(d0, d1, d2, d3);
            return GeologyTetraExtractionLut.VertexCount(caseIndex);
        }

        private float Sample(int x, int y, int z)
        {
            return Density[x + (y * Points) + (z * Points * Points)];
        }

        private void ResolveCell(int index, out int x, out int y, out int z)
        {
            x = index % Cells;
            y = (index / Cells) % Cells;
            z = index / (Cells * Cells);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct SdfToMeshExtractionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Density;
        [ReadOnly, NoAlias] public NativeArray<int> CellVertexOffsets;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: CellVertexOffsets is an exclusive prefix sum; each cell owns one disjoint RawVertices write range.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: RawVertices is allocated by this bake lane and is not aliased with Density or CellVertexOffsets.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Later jobs consume RawVertices only through the returned JobHandle chain after extraction completes.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<GeologyRawVertex> RawVertices;

        public int Points;
        public int Cells;
        public float VoxelStep;
        public float3 BoundsMin;

        public void Execute(int cellIndex)
        {
            ResolveCell(cellIndex, out int x, out int y, out int z);
            int write = CellVertexOffsets[cellIndex];
            float3 p0 = Point(x, y, z);
            float3 p1 = Point(x + 1, y, z);
            float3 p2 = Point(x + 1, y + 1, z);
            float3 p3 = Point(x, y + 1, z);
            float3 p4 = Point(x, y, z + 1);
            float3 p5 = Point(x + 1, y, z + 1);
            float3 p6 = Point(x + 1, y + 1, z + 1);
            float3 p7 = Point(x, y + 1, z + 1);
            float d0 = Sample(x, y, z);
            float d1 = Sample(x + 1, y, z);
            float d2 = Sample(x + 1, y + 1, z);
            float d3 = Sample(x, y + 1, z);
            float d4 = Sample(x, y, z + 1);
            float d5 = Sample(x + 1, y, z + 1);
            float d6 = Sample(x + 1, y + 1, z + 1);
            float d7 = Sample(x, y + 1, z + 1);
            EmitTetra(p0, d0, p5, d5, p1, d1, p6, d6, ref write);
            EmitTetra(p0, d0, p1, d1, p2, d2, p6, d6, ref write);
            EmitTetra(p0, d0, p2, d2, p3, d3, p6, d6, ref write);
            EmitTetra(p0, d0, p3, d3, p7, d7, p6, d6, ref write);
            EmitTetra(p0, d0, p7, d7, p4, d4, p6, d6, ref write);
            EmitTetra(p0, d0, p4, d4, p5, d5, p6, d6, ref write);
        }

        private void EmitTetra(float3 p0, float d0, float3 p1, float d1, float3 p2, float d2, float3 p3, float d3, ref int write)
        {
            int caseIndex = GeologyTetraExtractionLut.CaseIndex(d0, d1, d2, d3);
            int count = GeologyTetraExtractionLut.VertexCount(caseIndex);
            if (count == 0)
                return;

            uint edgeSequence = GeologyTetraExtractionLut.EdgeSequence(caseIndex);
            for (int i = 0; i < count; i += 3)
            {
                float3 v0 = InterpolateEdge(p0, d0, p1, d1, p2, d2, p3, d3, GeologyTetraExtractionLut.EdgeAt(edgeSequence, i));
                float3 v1 = InterpolateEdge(p0, d0, p1, d1, p2, d2, p3, d3, GeologyTetraExtractionLut.EdgeAt(edgeSequence, i + 1));
                float3 v2 = InterpolateEdge(p0, d0, p1, d1, p2, d2, p3, d3, GeologyTetraExtractionLut.EdgeAt(edgeSequence, i + 2));
                EmitTriangle(v0, v1, v2, ref write);
            }
        }

        private void EmitTriangle(float3 a, float3 b, float3 c, ref int write)
        {
            WriteVertex(write++, a);
            WriteVertex(write++, b);
            WriteVertex(write++, c);
        }

        private void WriteVertex(int index, float3 position)
        {
            if ((uint)index >= (uint)RawVertices.Length)
                return;

            RawVertices[index] = new GeologyRawVertex
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                Normal = new float3(0f, 1f, 0f),
                Tangent = new float4(1f, 0f, 0f, 1f),
                Uv = float2.zero,
                AmbientOcclusion = 1f,
                Flags = 0u
            };
        }

        private static float3 Interpolate(float3 a, float3 b, float da, float db)
        {
            float denom = da - db;
            float t = math.abs(denom) > 1e-7f ? math.clamp(da * math.rcp(denom), 0.001f, 0.999f) : 0.5f;
            return a + (b - a) * t;
        }

        private static float3 InterpolateEdge(float3 p0, float d0, float3 p1, float d1, float3 p2, float d2, float3 p3, float d3, int edge)
        {
            switch (edge)
            {
                case 0:
                    return Interpolate(p0, p1, d0, d1);
                case 1:
                    return Interpolate(p0, p2, d0, d2);
                case 2:
                    return Interpolate(p0, p3, d0, d3);
                case 3:
                    return Interpolate(p1, p2, d1, d2);
                case 4:
                    return Interpolate(p1, p3, d1, d3);
                case 5:
                    return Interpolate(p2, p3, d2, d3);
                default:
                    return p0;
            }
        }

        private float Sample(int x, int y, int z)
        {
            return Density[x + (y * Points) + (z * Points * Points)];
        }

        private float3 Point(int x, int y, int z)
        {
            return BoundsMin + new float3(x, y, z) * VoxelStep;
        }

        private void ResolveCell(int index, out int x, out int y, out int z)
        {
            x = index % Cells;
            y = (index / Cells) % Cells;
            z = index / (Cells * Cells);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildNormalBucketJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<GeologyRawVertex> Vertices;
        [NoAlias] public NativeParallelMultiHashMap<ulong, int>.ParallelWriter Buckets;

        public float PositionBucketSize;

        public void Execute(int index)
        {
            GeologyRawVertex vertex = Vertices[index];
            if (!math.all(math.isfinite(vertex.Position)))
                return;

            float inv = math.rcp(math.max(PositionBucketSize, 1e-6f));
            int3 cell = QuantizedCell(vertex.Position, inv);
            Buckets.Add(HashCell(cell), index);
        }

        internal static int3 QuantizedCell(float3 position, float inverseBucketSize)
        {
            return (int3)math.round(position * inverseBucketSize);
        }

        internal static ulong HashCell(int3 cell)
        {
            unchecked
            {
                ulong x = (uint)cell.x * 0x9E3779B97F4A7C15UL;
                ulong y = (uint)cell.y * 0xC2B2AE3D27D4EB4FUL;
                ulong z = (uint)cell.z * 0x165667B19E3779F9UL;
                ulong h = x ^ ((y << 21) | (y >> 43)) ^ ((z << 42) | (z >> 22));
                h ^= h >> 33;
                h *= 0xff51afd7ed558ccdUL;
                h ^= h >> 33;
                return h;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CalculateSmoothNormalsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Density;
        [ReadOnly, NoAlias] public NativeParallelMultiHashMap<ulong, int> NormalBuckets;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Vertices is acquired from the single RawVertices buffer and stays allocated until this job completes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Each worker writes only its own vertex row; triangle-neighbor reads use immutable position lanes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Density and NormalBuckets are separate read-only containers, and the caller chains disposal after this job.
        [NoAlias, NativeDisableUnsafePtrRestriction] public GeologyRawVertex* Vertices;

        public int VertexCount;
        public int Points;
        public float VoxelStep;
        public float PositionBucketSize;
        public float PositionToleranceSq;
        public float3 BoundsMin;

        public void Execute(int index)
        {
            int triBase = (index / 3) * 3;
            if ((uint)(triBase + 2) >= (uint)VertexCount)
                return;

            ref GeologyRawVertex v = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + index);
            ref GeologyRawVertex t0 = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + triBase);
            ref GeologyRawVertex t1 = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + triBase + 1);
            ref GeologyRawVertex t2 = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + triBase + 2);
            float3 p0 = t0.Position;
            float3 p1 = t1.Position;
            float3 p2 = t2.Position;
            float3 face = NormalizeOrFallback(math.cross(p1 - p0, p2 - p0), new float3(0f, 1f, 0f));
            float3 weighted = AccumulateAngleWeightedNormal(v.Position, face);
            float3 sdfNormal = ResolveSdfNormal(v.Position, face);
            float3 smoothNormal = NormalizeOrFallback(weighted, face);
            if (math.dot(smoothNormal, sdfNormal) < 0f)
                smoothNormal = -smoothNormal;

            float3 normal = NormalizeOrFallback(smoothNormal * 1.25f + sdfNormal, sdfNormal);
            float3 helper = math.abs(normal.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 tangent = NormalizeOrFallback(math.cross(helper, normal), new float3(1f, 0f, 0f));
            v.Normal = normal;
            v.Tangent = new float4(tangent.x, tangent.y, tangent.z, 1f);
        }

        private float3 AccumulateAngleWeightedNormal(float3 position, float3 fallback)
        {
            if (VertexCount < 3)
                return fallback;

            float inv = math.rcp(math.max(PositionBucketSize, 1e-6f));
            int3 baseCell = BuildNormalBucketJob.QuantizedCell(position, inv);
            float toleranceSq = math.max(PositionToleranceSq, 1e-12f);
            float3 sum = float3.zero;
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        ulong key = BuildNormalBucketJob.HashCell(baseCell + new int3(x, y, z));
                        NativeParallelMultiHashMapIterator<ulong> iterator;
                        int candidate;
                        if (!NormalBuckets.TryGetFirstValue(key, out candidate, out iterator))
                            continue;

                        do
                        {
                            if ((uint)candidate >= (uint)VertexCount)
                                continue;

                            ref GeologyRawVertex other = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + candidate);
                            float3 delta = other.Position - position;
                            if (!math.all(math.isfinite(delta)) || math.lengthsq(delta) > toleranceSq)
                                continue;

                            int otherTriBase = (candidate / 3) * 3;
                            if ((uint)(otherTriBase + 2) >= (uint)VertexCount)
                                continue;

                            ref GeologyRawVertex a = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + otherTriBase);
                            ref GeologyRawVertex b = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + otherTriBase + 1);
                            ref GeologyRawVertex c = ref UnsafeUtility.AsRef<GeologyRawVertex>(Vertices + otherTriBase + 2);
                            float3 face = NormalizeOrFallback(math.cross(b.Position - a.Position, c.Position - a.Position), fallback);
                            float angleWeight = CornerAngle(candidate - otherTriBase, a.Position, b.Position, c.Position);
                            sum += face * angleWeight;
                        }
                        while (NormalBuckets.TryGetNextValue(out candidate, ref iterator));
                    }
                }
            }

            return NormalizeOrFallback(sum, fallback);
        }

        private static float CornerAngle(int corner, float3 a, float3 b, float3 c)
        {
            float3 origin = corner == 0 ? a : corner == 1 ? b : c;
            float3 p0 = corner == 0 ? b : corner == 1 ? a : a;
            float3 p1 = corner == 0 ? c : corner == 1 ? c : b;
            float3 v0 = p0 - origin;
            float3 v1 = p1 - origin;
            float invDenom = math.rsqrt(math.max(math.lengthsq(v0) * math.lengthsq(v1), 1e-12f));
            float cosAngle = math.clamp(math.dot(v0, v1) * invDenom, -1f, 1f);
            float angle = math.acos(cosAngle);
            return math.isfinite(angle) ? angle : 1f;
        }

        private float3 ResolveSdfNormal(float3 p, float3 fallback)
        {
            if (Points < 3 || Density.Length == 0 || VoxelStep <= 1e-6f)
                return fallback;

            float3 grid = (p - BoundsMin) * math.rcp(VoxelStep);
            int max = Points - 2;
            int x = math.clamp((int)math.round(grid.x), 1, max);
            int y = math.clamp((int)math.round(grid.y), 1, max);
            int z = math.clamp((int)math.round(grid.z), 1, max);
            float dx = Sample(x + 1, y, z) - Sample(x - 1, y, z);
            float dy = Sample(x, y + 1, z) - Sample(x, y - 1, z);
            float dz = Sample(x, y, z + 1) - Sample(x, y, z - 1);
            return NormalizeOrFallback(new float3(dx, dy, dz), fallback);
        }

        private float Sample(int x, int y, int z)
        {
            return Density[x + (y * Points) + (z * Points * Points)];
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lenSq > 1e-12f ? value * math.rsqrt(lenSq) : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateTriplanarUvsJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: The job maps Execute(index) to Vertices[index], so no two workers write the same row.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Vertices is the sole mutable UV stream in this phase and is not passed as another job field.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: The dependency chain starts after normal smoothing and ends before AO or LOD reads the UV result.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<GeologyRawVertex> Vertices;

        public float TextureScale;

        public void Execute(int index)
        {
            GeologyRawVertex v = Vertices[index];
            float3 normal = math.all(math.isfinite(v.Normal)) ? v.Normal : new float3(0f, 1f, 0f);
            float3 position = math.all(math.isfinite(v.Position)) ? v.Position : float3.zero;
            float3 n = math.abs(normal);
            float2 uv;
            if (n.y >= n.x && n.y >= n.z)
                uv = position.xz;
            else if (n.x >= n.z)
                uv = position.zy;
            else
                uv = position.xy;

            float scale = math.max(0.001f, TextureScale);
            v.Uv = uv * scale;
            Vertices[index] = v;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BakeVertexOcclusionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Density;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: The AO job mutates only Vertices[index]; Density is read-only SDF storage in a separate allocation.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: No cross-index writes occur, and hemisphere probes only read Density through clamped nearest samples.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: The caller schedules this phase after UV generation and before LOD decimation consumes the vertices.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<GeologyRawVertex> Vertices;

        public int Points;
        public int RayCount;
        public int StepsPerRay;
        public uint Seed;
        public float VoxelStep;
        public float MaxDistance;
        public float3 BoundsMin;

        public void Execute(int index)
        {
            GeologyRawVertex v = Vertices[index];
            int rays = math.clamp(RayCount, 1, GeologyForgeConstants.MaximumAoRays);
            int steps = math.clamp(StepsPerRay, 1, 16);
            float maxDistance = math.max(VoxelStep, MaxDistance);
            float stepDistance = maxDistance * math.rcp(steps);
            int occluded = 0;
            for (int ray = 0; ray < rays; ray++)
            {
                float3 dir = HemisphereDirection(v.Normal, (uint)index, (uint)ray);
                for (int step = 1; step <= steps; step++)
                {
                    float3 samplePos = v.Position + dir * (stepDistance * step);
                    if (SampleDensityNearest(samplePos) < -VoxelStep * 0.35f)
                    {
                        occluded++;
                        break;
                    }
                }
            }

            float ao = 1f - (occluded * math.rcp(rays));
            v.AmbientOcclusion = math.saturate(math.lerp(0.35f, 1f, ao));
            Vertices[index] = v;
        }

        private float SampleDensityNearest(float3 position)
        {
            if (Points <= 1 || Density.Length == 0 || VoxelStep <= 1e-6f || !math.all(math.isfinite(position)))
                return 1f;

            int3 p = (int3)math.round((position - BoundsMin) * math.rcp(VoxelStep));
            p = math.clamp(p, int3.zero, new int3(Points - 1));
            return Density[p.x + (p.y * Points) + (p.z * Points * Points)];
        }

        private float3 HemisphereDirection(float3 normal, uint vertexIndex, uint rayIndex)
        {
            float u = Hash01(vertexIndex, rayIndex, 0x9E3779B9u);
            float v = Hash01(vertexIndex, rayIndex, 0xBB67AE85u);
            float z = u;
            float radialSq = math.max(0f, 1f - z * z);
            float r = radialSq * math.rsqrt(math.max(radialSq, 1e-12f));
            float angle = v * 6.28318530718f;
            float3 local = new float3(math.cos(angle) * r, math.sin(angle) * r, z);
            float3 up = NormalizeOrFallback(normal, new float3(0f, 1f, 0f));
            float3 helper = math.abs(up.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 tangent = NormalizeOrFallback(math.cross(helper, up), new float3(1f, 0f, 0f));
            float3 bitangent = math.cross(up, tangent);
            return NormalizeOrFallback(tangent * local.x + bitangent * local.y + up * local.z, up);
        }

        private float Hash01(uint a, uint b, uint salt)
        {
            uint h = Seed ^ salt ^ (a * 0x85EBCA6Bu) ^ (b * 0xC2B2AE35u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h * 2.3283064e-10f;
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lenSq > 1e-12f ? value * math.rsqrt(lenSq) : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GeologyLodDecimationJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<GeologyRawVertex> SourceVertices;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Each triangle index writes exactly OutputVertices[dst..dst+2], where dst = triangleIndex * 3.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: OutputVertices is a distinct LOD allocation and never aliases SourceVertices in this bake phase.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: The caller sizes OutputVertices to OutputTriangleCount * 3 and chains upload after job completion.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<GeologyRawVertex> OutputVertices;

        public int SourceTriangleCount;
        public int OutputTriangleCount;
        public float CollapseCellSize;

        public void Execute(int triangleIndex)
        {
            int sourceTriangle = math.min(SourceTriangleCount - 1, (int)((long)triangleIndex * SourceTriangleCount / math.max(1, OutputTriangleCount)));
            int src = sourceTriangle * 3;
            int dst = triangleIndex * 3;
            GeologyRawVertex a = SourceVertices[src];
            GeologyRawVertex b = SourceVertices[src + 1];
            GeologyRawVertex c = SourceVertices[src + 2];
            Snap(ref a);
            Snap(ref b);
            Snap(ref c);
            float3 face = NormalizeOrFallback(math.cross(b.Position - a.Position, c.Position - a.Position), new float3(0f, 1f, 0f));
            a.Normal = NormalizeOrFallback(a.Normal + face, face);
            b.Normal = NormalizeOrFallback(b.Normal + face, face);
            c.Normal = NormalizeOrFallback(c.Normal + face, face);
            OutputVertices[dst] = a;
            OutputVertices[dst + 1] = b;
            OutputVertices[dst + 2] = c;
        }

        private void Snap(ref GeologyRawVertex v)
        {
            if (CollapseCellSize <= 1e-6f || !math.all(math.isfinite(v.Position)))
            {
                v.Position = math.all(math.isfinite(v.Position)) ? v.Position : float3.zero;
                return;
            }

            float inv = math.rcp(CollapseCellSize);
            v.Position = math.floor(v.Position * inv + 0.5f) * CollapseCellSize;
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lenSq > 1e-12f ? value * math.rsqrt(lenSq) : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GeologyPackVertexJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<GeologyRawVertex> SourceVertices;
        [WriteOnly, NoAlias] public NativeArray<GeologyVertex32> PackedVertices;

        public byte LodMask;

        public void Execute(int index)
        {
            GeologyRawVertex src = SourceVertices[index];
            float3 normal = NormalizeOrFallback(src.Normal, new float3(0f, 1f, 0f));
            byte red = (byte)math.clamp((int)math.round(math.saturate(src.AmbientOcclusion) * 255f), 0, 255);
            byte green = 0;
            byte blue = LodMask;
            uint color = red | ((uint)green << 8) | ((uint)blue << 16) | (255u << 24);
            PackedVertices[index] = new GeologyVertex32
            {
                Position = math.all(math.isfinite(src.Position)) ? src.Position : float3.zero,
                Normal = normal,
                ColorRgba = color,
                Uv0Packed = PackUnorm16(src.Uv)
            };
        }

        private static uint PackUnorm16(float2 uv)
        {
            uv = math.all(math.isfinite(uv)) ? uv : float2.zero;
            float2 wrapped = math.frac(uv);
            uint u = (uint)math.clamp((int)math.round(math.saturate(wrapped.x) * 65535f), 0, 65535);
            uint v = (uint)math.clamp((int)math.round(math.saturate(wrapped.y) * 65535f), 0, 65535);
            return u | (v << 16);
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lenSq > 1e-12f ? value * math.rsqrt(lenSq) : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GeologyIndexFillJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            Indices[index] = (uint)index;
        }
    }
}
