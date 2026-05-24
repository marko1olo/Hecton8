#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Hecton8.World.VoxelTerrainSeamBinder;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.VoxelTerrainSeamBinder.Editor
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SeamSubMeshIndexRangeDTO
    {
        [FieldOffset(0)] public int SourceIndexStart;
        [FieldOffset(4)] public int IndexCount;
        [FieldOffset(8)] public int DestinationIndexStart;
        [FieldOffset(12)] public int BaseVertex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct SeamEdgeDTO
    {
        [FieldOffset(0)] public ulong Key;
        [FieldOffset(8)] public int VertexA;
        [FieldOffset(12)] public int VertexB;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct ExtractSeamVerticesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> PositionBytes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> NormalBytes;

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
        public NativeArray<SeamBindVertex32> Output;

        public int PositionOffset;
        public int PositionStride;
        public int NormalOffset;
        public int NormalStride;
        public int UvOffset;
        public int UvStride;
        public int ColorOffset;
        public int ColorStride;
        public int HasNormal;
        public int HasUv;
        public int HasColor;

        public void Execute(int index)
        {
            SeamBindVertex32 vertex = default;
            vertex.Position = ReadFloat3(PositionBytes, PositionOffset, PositionStride, index, float3.zero);
            vertex.Normal = HasNormal != 0
                ? math.normalizesafe(ReadFloat3(NormalBytes, NormalOffset, NormalStride, index, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f))
                : new float3(0f, 1f, 0f);
            float2 uv = HasUv != 0 ? ReadFloat2(UvBytes, UvOffset, UvStride, index, float2.zero) : float2.zero;
            vertex.PackedUv0 = VoxelTerrainSeamMath.PackUvUnorm16(uv);
            vertex.PackedColor = HasColor != 0 ? ReadUInt32(ColorBytes, ColorOffset, ColorStride, index, 0xFFFFFFFFu) : 0xFFFFFFFFu;
            if (!math.all(math.isfinite(vertex.Position)))
                vertex.Position = float3.zero;
            if (!math.all(math.isfinite(vertex.Normal)))
                vertex.Normal = new float3(0f, 1f, 0f);
            Output[index] = vertex;
        }

        private static float3 ReadFloat3(NativeArray<byte> bytes, int offset, int stride, int index, float3 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float3>(ptr, index, stride);
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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CopySeamIndex16RangesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ushort> Source;

        [ReadOnly]
        [NoAlias]
        public NativeArray<SeamSubMeshIndexRangeDTO> Ranges;

        // SAFETY: each range row owns a disjoint destination index window.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<int> Output;

        public void Execute(int rangeIndex)
        {
            SeamSubMeshIndexRangeDTO range = Ranges[rangeIndex];
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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CopySeamIndex32RangesJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<uint> Source;

        [ReadOnly]
        [NoAlias]
        public NativeArray<SeamSubMeshIndexRangeDTO> Ranges;

        // SAFETY: each range row owns a disjoint destination index window.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<int> Output;

        public void Execute(int rangeIndex)
        {
            SeamSubMeshIndexRangeDTO range = Ranges[rangeIndex];
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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ClearBoundaryMaskJob : IJobParallelFor
    {
        // SAFETY: IJobParallelFor writes only BoundaryMask[index].
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<byte> BoundaryMask;

        public void Execute(int index)
        {
            BoundaryMask[index] = 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BuildTriangleEdgeMapJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<int> Indices;

        // SAFETY: triIndex owns three disjoint Edge rows: tri*3..tri*3+2.
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        [NoAlias]
        public NativeArray<SeamEdgeDTO> Edges;

        [NoAlias]
        public NativeParallelMultiHashMap<ulong, int>.ParallelWriter EdgeMap;

        public int VertexCount;

        public void Execute(int triIndex)
        {
            int source = triIndex * 3;
            int i0 = Indices[source];
            int i1 = Indices[source + 1];
            int i2 = Indices[source + 2];
            WriteEdge(source, i0, i1);
            WriteEdge(source + 1, i1, i2);
            WriteEdge(source + 2, i2, i0);
        }

        private void WriteEdge(int edgeIndex, int a, int b)
        {
            SeamEdgeDTO edge = default;
            if (!IsValid(a) || !IsValid(b) || a == b)
            {
                edge.VertexA = -1;
                edge.VertexB = -1;
                edge.Flags = 1u;
                Edges[edgeIndex] = edge;
                return;
            }

            int lo = math.min(a, b);
            int hi = math.max(a, b);
            edge.Key = ((ulong)(uint)lo << 32) | (uint)hi;
            edge.VertexA = a;
            edge.VertexB = b;
            Edges[edgeIndex] = edge;
            EdgeMap.Add(edge.Key, edgeIndex);
        }

        private bool IsValid(int index)
        {
            return (uint)index < (uint)VertexCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct MarkBoundaryVerticesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<SeamEdgeDTO> Edges;

        [ReadOnly]
        [NoAlias]
        public NativeParallelMultiHashMap<ulong, int> EdgeMap;

        [NoAlias]
        public NativeArray<byte> BoundaryMask;

        public void Execute()
        {
            for (int edgeIndex = 0; edgeIndex < Edges.Length; edgeIndex++)
            {
                SeamEdgeDTO edge = Edges[edgeIndex];
                if (edge.VertexA < 0 || edge.VertexB < 0)
                    continue;

                int edgeCount = 0;
                NativeParallelMultiHashMapIterator<ulong> iterator;
                int value;
                if (EdgeMap.TryGetFirstValue(edge.Key, out value, out iterator))
                {
                    do
                    {
                        edgeCount++;
                    }
                    while (EdgeMap.TryGetNextValue(out value, ref iterator));
                }

                if (edgeCount == 1)
                {
                    BoundaryMask[edge.VertexA] = 1;
                    BoundaryMask[edge.VertexB] = 1;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ConstructBoundarySpatialHashJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<SeamBindVertex32> VoxelVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> BoundaryMask;

        [NoAlias]
        public NativeParallelMultiHashMap<long, SeamBoundaryVertex64>.ParallelWriter BoundaryHash;

        public double3 VoxelRootAup;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            if (BoundaryMask.IsCreated && BoundaryMask.Length > index && BoundaryMask[index] == 0)
                return;

            SeamBindVertex32 source = VoxelVertices[index];
            float3 local = math.all(math.isfinite(source.Position)) ? source.Position : float3.zero;
            float3 normal = math.normalizesafe(source.Normal, new float3(0f, 1f, 0f));
            double3 aup = VoxelRootAup + new double3(local.x, local.y, local.z);
            if (!math.all(math.isfinite(aup)))
                return;

            SeamBoundaryVertex64 boundary = default;
            boundary.Aup = aup;
            boundary.LocalPosition = local;
            boundary.Normal = normal;
            boundary.VertexIndex = index;
            boundary.BoundaryWeight = 1f;
            BoundaryHash.Add(VoxelTerrainSeamMath.HashCell(aup, CellSizeMeters), boundary);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct EvaluateSeamSnappingJob : IJobParallelFor
    {
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<SeamBindVertex32> TerrainVertices;

        [ReadOnly]
        [NoAlias]
        public NativeParallelMultiHashMap<long, SeamBoundaryVertex64> BoundaryHash;

        [WriteOnly]
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<SeamSnapResult64> SnapResults;

        public double3 TerrainRootAup;
        public double CellSizeMeters;
        public float SnapRadiusMeters;
        public float NormalBlendDistanceMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            SeamBindVertex32* ptr = (SeamBindVertex32*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TerrainVertices);
            ref SeamBindVertex32 vertex = ref UnsafeUtility.AsRef<SeamBindVertex32>(ptr + index);
            float3 terrainLocal = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 terrainNormal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
            double3 terrainAup = TerrainRootAup + new double3(terrainLocal.x, terrainLocal.y, terrainLocal.z);
            SeamSnapResult64 result = default;
            result.OriginalLocalPosition = terrainLocal;
            result.SnappedLocalPosition = terrainLocal;
            result.VoxelVertexIndex = -1;
            result.BlendedNormal = terrainNormal;
            result.DistanceMeters = float.MaxValue;

            if (!math.all(math.isfinite(terrainAup)))
            {
                SnapResults[index] = result;
                return;
            }

            float snapRadius = math.max(math.isfinite(SnapRadiusMeters) ? SnapRadiusMeters : 0f, 0.0001f);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0.5f);
            double snapRadiusSq = (double)snapRadius * snapRadius;
            double bestDistanceSq = snapRadiusSq;
            SeamBoundaryVertex64 best = default;
            bool found = false;
            VoxelTerrainSeamMath.ResolveCellIndices(terrainAup, CellSizeMeters, out long cx, out long cy, out long cz);
            for (long dz = -1; dz <= 1; dz++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    for (long dx = -1; dx <= 1; dx++)
                    {
                        long key = VoxelTerrainSeamMath.HashCellIndices(cx + dx, cy + dy, cz + dz);
                        NativeParallelMultiHashMapIterator<long> iterator;
                        SeamBoundaryVertex64 candidate;
                        if (!BoundaryHash.TryGetFirstValue(key, out candidate, out iterator))
                            continue;

                        do
                        {
                            double3 delta = candidate.Aup - terrainAup;
                            double distanceSq = math.dot(delta, delta);
                            if (math.isfinite(distanceSq) && distanceSq <= bestDistanceSq)
                            {
                                bestDistanceSq = distanceSq;
                                best = candidate;
                                found = true;
                            }
                        }
                        while (BoundaryHash.TryGetNextValue(out candidate, ref iterator));
                    }
                }
            }

            if (!found)
            {
                SnapResults[index] = result;
                return;
            }

            double3 snappedLocalD = best.Aup - TerrainRootAup;
            float3 snappedLocal = new float3((float)snappedLocalD.x, (float)snappedLocalD.y, (float)snappedLocalD.z);
            float distance = math.sqrt((float)math.max(bestDistanceSq, 0d));
            float blendRadius = math.max(math.isfinite(NormalBlendDistanceMeters) ? NormalBlendDistanceMeters : snapRadius, 0.0001f);
            float distance01 = math.saturate(distance / blendRadius);
            float smoothDistance01 = distance01 * distance01 * (3f - (2f * distance01));
            float blend01 = math.saturate(1f - math.lerp(distance01, smoothDistance01, quality));
            float3 voxelNormal = math.normalizesafe(best.Normal, terrainNormal);
            float3 averageNormal = math.normalizesafe(terrainNormal + voxelNormal, terrainNormal);
            float3 blendedNormal = math.normalizesafe(math.lerp(terrainNormal, averageNormal, blend01), terrainNormal);
            vertex.Position = math.all(math.isfinite(snappedLocal)) ? snappedLocal : terrainLocal;
            vertex.Normal = blendedNormal;
            result.SnappedLocalPosition = vertex.Position;
            result.VoxelVertexIndex = best.VertexIndex;
            result.BlendedNormal = blendedNormal;
            result.DistanceMeters = math.isfinite(distance) ? distance : float.MaxValue;
            SnapResults[index] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct BlendSeamNormalsJob : IJob
    {
        [NoAlias]
        public NativeArray<SeamBindVertex32> VoxelVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<SeamSnapResult64> SnapResults;

        public void Execute()
        {
            SeamBindVertex32* voxelPtr = (SeamBindVertex32*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(VoxelVertices);
            for (int i = 0; i < SnapResults.Length; i++)
            {
                SeamSnapResult64 snap = SnapResults[i];
                if ((uint)snap.VoxelVertexIndex >= (uint)VoxelVertices.Length)
                    continue;

                ref SeamBindVertex32 voxel = ref UnsafeUtility.AsRef<SeamBindVertex32>(voxelPtr + snap.VoxelVertexIndex);
                float3 n = math.normalizesafe(voxel.Normal + snap.BlendedNormal, snap.BlendedNormal);
                voxel.Normal = math.all(math.isfinite(n)) ? n : new float3(0f, 1f, 0f);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct BakeSeamTransitionColorsJob : IJobParallelFor
    {
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<SeamBindVertex32> Vertices;

        [ReadOnly]
        [NoAlias]
        public NativeParallelMultiHashMap<long, SeamBoundaryVertex64> BoundaryHash;

        public double3 RootAup;
        public double CellSizeMeters;
        public float TextureGradientFalloffMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            SeamBindVertex32* ptr = (SeamBindVertex32*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Vertices);
            ref SeamBindVertex32 vertex = ref UnsafeUtility.AsRef<SeamBindVertex32>(ptr + index);
            float3 local = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            double3 aup = RootAup + new double3(local.x, local.y, local.z);
            float falloff = math.max(math.isfinite(TextureGradientFalloffMeters) ? TextureGradientFalloffMeters : 0f, 0.0001f);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0.5f);
            double falloffSq = (double)falloff * falloff;
            double bestDistanceSq = falloffSq;
            bool found = false;

            VoxelTerrainSeamMath.ResolveCellIndices(aup, CellSizeMeters, out long cx, out long cy, out long cz);
            for (long dz = -1; dz <= 1; dz++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    for (long dx = -1; dx <= 1; dx++)
                    {
                        long key = VoxelTerrainSeamMath.HashCellIndices(cx + dx, cy + dy, cz + dz);
                        NativeParallelMultiHashMapIterator<long> iterator;
                        SeamBoundaryVertex64 candidate;
                        if (!BoundaryHash.TryGetFirstValue(key, out candidate, out iterator))
                            continue;

                        do
                        {
                            double3 delta = candidate.Aup - aup;
                            double distanceSq = math.dot(delta, delta);
                            if (math.isfinite(distanceSq) && distanceSq <= bestDistanceSq)
                            {
                                bestDistanceSq = distanceSq;
                                found = true;
                            }
                        }
                        while (BoundaryHash.TryGetNextValue(out candidate, ref iterator));
                    }
                }
            }

            float distance01 = found ? math.saturate(math.sqrt((float)math.max(bestDistanceSq, 0d)) / falloff) : 1f;
            float smoothDistance01 = distance01 * distance01 * (3f - (2f * distance01));
            float alpha01 = found ? math.saturate(1f - math.lerp(distance01, smoothDistance01, quality)) : 0f;
            byte alpha = (byte)math.clamp((int)math.round(alpha01 * 255f), 0, 255);
            vertex.PackedColor = VoxelTerrainSeamMath.ReplaceAlpha(vertex.PackedColor, alpha);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockSeamJob : IJobParallelFor
    {
        [WriteOnly]
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<SeamBindVertex32> TerrainVertices;

        [WriteOnly]
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<SeamBindVertex32> VoxelVertices;

        public int ResolutionX;
        public int ResolutionZ;
        public float CellSizeMeters;
        public float NoiseAmplitudeMeters;
        public float VoxelVerticalBiasMeters;
        public uint Seed;

        public void Execute(int index)
        {
            int xCount = math.max(ResolutionX, 2);
            int zCount = math.max(ResolutionZ, 2);
            int z = index / xCount;
            int x = index - (z * xCount);
            float cell = math.max(CellSizeMeters, 0.001f);
            float3 p = new float3(x * cell, 0f, z * cell);
            float n0 = Noise(index, Seed) * NoiseAmplitudeMeters;
            float n1 = Noise(index, Seed ^ 0x9E3779B9u) * NoiseAmplitudeMeters;

            SeamBindVertex32 terrain = default;
            terrain.Position = p + new float3(0f, n0, 0f);
            terrain.Normal = new float3(0f, 1f, 0f);
            terrain.PackedColor = VoxelTerrainSeamMath.PackColor(255, 255, 255, 0);
            terrain.PackedUv0 = VoxelTerrainSeamMath.PackUvUnorm16(new float2((float)x / (xCount - 1), (float)z / (zCount - 1)));
            TerrainVertices[index] = terrain;

            SeamBindVertex32 voxel = terrain;
            voxel.Position = p + new float3(cell * 0.12f, VoxelVerticalBiasMeters + n1, cell * -0.08f);
            voxel.Normal = math.normalizesafe(new float3(0.12f, 1f, -0.08f), new float3(0f, 1f, 0f));
            VoxelVertices[index] = voxel;
        }

        private static float Noise(int index, uint seed)
        {
            uint value = (uint)index ^ seed;
            value = VoxelTerrainSeamMath.Hash(value);
            return ((value & 0xFFFFu) / 65535f) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockPlaneIndicesJob : IJobParallelFor
    {
        [WriteOnly]
        [NoAlias]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> Output;

        public int ResolutionX;
        public int ResolutionZ;

        public void Execute(int quadIndex)
        {
            int xCount = math.max(ResolutionX, 2);
            int xQuads = xCount - 1;
            int x = quadIndex - ((quadIndex / xQuads) * xQuads);
            int z = quadIndex / xQuads;
            int i0 = z * xCount + x;
            int i1 = z * xCount + x + 1;
            int i2 = (z + 1) * xCount + x;
            int i3 = (z + 1) * xCount + x + 1;
            int write = quadIndex * 6;
            Output[write] = i0;
            Output[write + 1] = i2;
            Output[write + 2] = i1;
            Output[write + 3] = i1;
            Output[write + 4] = i2;
            Output[write + 5] = i3;
        }
    }
}
#endif
