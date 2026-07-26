using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.VoxelSurfaceNets
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockVoxelDensitySphereJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<sbyte> Densities;

        public MockVoxelDensityArray Config;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int resolution = VoxelSurfaceNetsConstants.DensityResolution;
            int z = index / (resolution * resolution);
            int rem = index - (z * resolution * resolution);
            int y = rem / resolution;
            int x = rem - (y * resolution);

            float voxelSize = math.max(Config.VoxelSize, VoxelSurfaceNetsConstants.Epsilon);
            float3 local = new float3(x - 1, y - 1, z - 1) * voxelSize;
            float3 delta = local - Config.CenterLocal;
            float distSq = math.max(math.dot(delta, delta), VoxelSurfaceNetsConstants.Epsilon);
            float distance = distSq * math.rsqrt(distSq);
            uint hash = math.hash(new uint4((uint)x, (uint)y, (uint)z, Config.Seed));
            float quality = math.saturate(GlobalQualityWeight);
            float noiseScale = math.lerp(0.015f, 0.09f, Smooth01(quality));
            float signedNoise = (((hash & 2047u) * (1f / 2047f)) - 0.5f) * noiseScale;
            float shell = math.abs(distance - Config.Radius) - math.max(Config.ShellThickness, voxelSize);
            float sphere = distance - Config.Radius;
            float sdf = math.lerp(sphere, shell, (float)(Config.Flags & 1u)) + signedNoise;
            int packed = (int)math.round(math.clamp(sdf * math.rcp(voxelSize) * 127f, -127f, 127f));
            Densities[index] = (sbyte)packed;
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SurfaceNetExtractionJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<sbyte> Densities;

        [NoAlias]
        public NativeArray<VoxelVertexDTO> Vertices;

        [NoAlias]
        public NativeArray<uint> Indices;

        [NoAlias]
        public NativeArray<int> CellVertexMap;

        [NoAlias]
        public NativeArray<ChunkMeshingStateDTO> States;

        [ReadOnly]
        [NoAlias]
        public NativeArray<VoxelMeshingTuningDTO> Tuning;

        [ReadOnly]
        [NoAlias]
        public NativeArray<uint> SurfaceEdgeMasks;

        [NoAlias]
        public NativeArray<VoxelMeshingTelemetryEntry> TelemetryRing;

        [NoAlias]
        public NativeArray<int> TelemetryCursor;

        [NoAlias]
        public NativeArray<float3> RawDebugVertices;

        [NoAlias]
        public NativeArray<VoxelSurfaceIndirectArgsDTO> IndirectArgs;

        public int ChunkIndex;
        public uint Frame;

        public void Execute()
        {
            if (!Densities.IsCreated ||
                !Vertices.IsCreated ||
                !Indices.IsCreated ||
                !CellVertexMap.IsCreated ||
                !States.IsCreated ||
                States.Length <= 0)
            {
                return;
            }

            int stateIndex = math.clamp(ChunkIndex, 0, States.Length - 1);
            ChunkMeshingStateDTO state = States[stateIndex];
            VoxelMeshingTuningDTO tuning = ResolveTuning();
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float qualityCurve = Smooth01(quality);
            int stride = ResolveSamplingStride(quality);
            float voxelSize = math.max(tuning.VoxelSize > 0f ? tuning.VoxelSize : state.VoxelSize, VoxelSurfaceNetsConstants.Epsilon);
            float iso = tuning.IsoSurface;
            float decimationBias = math.saturate(tuning.DecimationAggression) * (1f - qualityCurve);
            float rawCaptureGate = math.saturate(tuning.DebugRawCapture01);

            state.Stage = (byte)VoxelMeshingStage.Extracting;
            state.Flags = (byte)(state.Flags & ~(VoxelMeshingFlags.CapacityClamped | VoxelMeshingFlags.NonFinite | VoxelMeshingFlags.SlowExtraction));
            state.VertexCount = 0;
            state.IndexCount = 0;
            state.RawDebugVertexCount = 0;

            int mapClearCount = math.min(CellVertexMap.Length, VoxelSurfaceNetsConstants.CellCount);
            for (int i = 0; i < mapClearCount; i++)
                CellVertexMap[i] = -1;

            int visitedCells = 0;
            int activeCells = 0;
            int vertexCount = 0;

            for (int z = 0; z < VoxelSurfaceNetsConstants.ChunkResolution; z += stride)
            {
                for (int y = 0; y < VoxelSurfaceNetsConstants.ChunkResolution; y += stride)
                {
                    for (int x = 0; x < VoxelSurfaceNetsConstants.ChunkResolution; x += stride)
                    {
                        visitedCells++;
                        float d000 = SampleCellDensity(x, y, z) - iso;
                        float d100 = SampleCellDensity(x + stride, y, z) - iso;
                        float d010 = SampleCellDensity(x, y + stride, z) - iso;
                        float d110 = SampleCellDensity(x + stride, y + stride, z) - iso;
                        float d001 = SampleCellDensity(x, y, z + stride) - iso;
                        float d101 = SampleCellDensity(x + stride, y, z + stride) - iso;
                        float d011 = SampleCellDensity(x, y + stride, z + stride) - iso;
                        float d111 = SampleCellDensity(x + stride, y + stride, z + stride) - iso;

                        int signMask = BuildSignMask(d000, d100, d010, d110, d001, d101, d011, d111);
                        uint edgeMask = ResolveEdgeMask(signMask);
                        if (edgeMask == 0u)
                            continue;

                        bool hasNegative =
                            d000 < 0f || d100 < 0f || d010 < 0f || d110 < 0f ||
                            d001 < 0f || d101 < 0f || d011 < 0f || d111 < 0f;
                        bool hasPositive =
                            d000 >= 0f || d100 >= 0f || d010 >= 0f || d110 >= 0f ||
                            d001 >= 0f || d101 >= 0f || d011 >= 0f || d111 >= 0f;

                        if (!hasNegative || !hasPositive)
                            continue;

                        if (vertexCount >= Vertices.Length || vertexCount >= VoxelSurfaceNetsConstants.MaxVertices)
                        {
                            state.Flags = (byte)(state.Flags | VoxelMeshingFlags.CapacityClamped);
                            continue;
                        }

                        float3 sum = 0f;
                        int hits = 0;
                        AccumulateEdge(ref sum, ref hits, d000, d100, new float3(x, y, z), new float3(x + stride, y, z), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d010, d110, new float3(x, y + stride, z), new float3(x + stride, y + stride, z), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d001, d101, new float3(x, y, z + stride), new float3(x + stride, y, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d011, d111, new float3(x, y + stride, z + stride), new float3(x + stride, y + stride, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d000, d010, new float3(x, y, z), new float3(x, y + stride, z), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d100, d110, new float3(x + stride, y, z), new float3(x + stride, y + stride, z), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d001, d011, new float3(x, y, z + stride), new float3(x, y + stride, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d101, d111, new float3(x + stride, y, z + stride), new float3(x + stride, y + stride, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d000, d001, new float3(x, y, z), new float3(x, y, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d100, d101, new float3(x + stride, y, z), new float3(x + stride, y, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d010, d011, new float3(x, y + stride, z), new float3(x, y + stride, z + stride), voxelSize);
                        AccumulateEdge(ref sum, ref hits, d110, d111, new float3(x + stride, y + stride, z), new float3(x + stride, y + stride, z + stride), voxelSize);

                        float3 cellCenter = new float3(x + (stride * 0.5f), y + (stride * 0.5f), z + (stride * 0.5f)) * voxelSize;
                        float3 vertexLocal = hits > 0 ? sum * math.rcp(hits) : cellCenter;
                        vertexLocal = math.lerp(vertexLocal, cellCenter, decimationBias);

                        if (!math.all(math.isfinite(vertexLocal)))
                        {
                            state.Flags = (byte)(state.Flags | VoxelMeshingFlags.NonFinite);
                            continue;
                        }

                        float3 normal = CalculateTetraNormal(vertexLocal, voxelSize, quality);
                        uint normalPacked = PackNormalRgb10A2(normal);
                        uint tangentPacked = PackNormalRgb10A2(ResolveTangent(normal));
                        uint colorPacked = PackColor(vertexLocal, quality, tuning.BiomeBlendScale, state.ChunkHash);
                        float uvScale = math.lerp(0.03125f, 0.125f, qualityCurve);

                        Vertices[vertexCount] = new VoxelVertexDTO
                        {
                            Position = vertexLocal,
                            NormalPacked = normalPacked,
                            TangentPacked = tangentPacked,
                            ColorPacked = colorPacked,
                            UV = new float2(vertexLocal.x, vertexLocal.z) * uvScale
                        };

                        int cellIndex = CellIndex(x, y, z);
                        if ((uint)cellIndex < (uint)CellVertexMap.Length)
                            CellVertexMap[cellIndex] = vertexCount;

                        vertexCount++;
                        activeCells++;
                    }
                }
            }

            int indexCount = 0;
            int rawDebugCount = 0;
            bool indexCapacityClamped = false;
            for (int z = 0; z < VoxelSurfaceNetsConstants.ChunkResolution; z += stride)
            {
                for (int y = 0; y < VoxelSurfaceNetsConstants.ChunkResolution; y += stride)
                {
                    for (int x = 0; x < VoxelSurfaceNetsConstants.ChunkResolution; x += stride)
                    {
                        int current = TryGetCellVertex(x, y, z);
                        if (current < 0)
                            continue;

                        if (x >= stride && y >= stride && TryResolveEdgeWinding(x, y, z, x, y, z + stride, iso, out bool reverseZ))
                        {
                            int a = TryGetCellVertex(x - stride, y, z);
                            int b = TryGetCellVertex(x - stride, y - stride, z);
                            int c = TryGetCellVertex(x, y - stride, z);
                            indexCapacityClamped |= !TryEmitQuad(current, a, b, c, reverseZ, ref indexCount, ref rawDebugCount, rawCaptureGate);
                        }

                        if (x >= stride && z >= stride && TryResolveEdgeWinding(x, y, z, x, y + stride, z, iso, out bool reverseY))
                        {
                            int a = TryGetCellVertex(x - stride, y, z);
                            int b = TryGetCellVertex(x - stride, y, z - stride);
                            int c = TryGetCellVertex(x, y, z - stride);
                            indexCapacityClamped |= !TryEmitQuad(current, c, b, a, reverseY, ref indexCount, ref rawDebugCount, rawCaptureGate);
                        }

                        if (y >= stride && z >= stride && TryResolveEdgeWinding(x, y, z, x + stride, y, z, iso, out bool reverseX))
                        {
                            int a = TryGetCellVertex(x, y - stride, z);
                            int b = TryGetCellVertex(x, y - stride, z - stride);
                            int c = TryGetCellVertex(x, y, z - stride);
                            indexCapacityClamped |= !TryEmitQuad(current, a, b, c, reverseX, ref indexCount, ref rawDebugCount, rawCaptureGate);
                        }
                    }
                }
            }

            if (indexCapacityClamped)
                state.Flags = (byte)(state.Flags | VoxelMeshingFlags.CapacityClamped);

            state.VertexCount = vertexCount;
            state.IndexCount = indexCount;
            state.RawDebugVertexCount = rawDebugCount;
            state.BoundsCenterLocal = new float3(
                VoxelSurfaceNetsConstants.ChunkResolution * voxelSize * 0.5f,
                VoxelSurfaceNetsConstants.ChunkResolution * voxelSize * 0.5f,
                VoxelSurfaceNetsConstants.ChunkResolution * voxelSize * 0.5f);
            state.VoxelSize = voxelSize;
            state.Stage = (byte)((state.Flags & (VoxelMeshingFlags.NonFinite | VoxelMeshingFlags.CapacityClamped)) == 0
                ? VoxelMeshingStage.ReadyForUpload
                : VoxelMeshingStage.Fault);
            States[stateIndex] = state;

            if (IndirectArgs.IsCreated && IndirectArgs.Length > 0)
            {
                IndirectArgs[0] = new VoxelSurfaceIndirectArgsDTO
                {
                    IndexCountPerInstance = (uint)math.max(indexCount, 0),
                    InstanceCount = indexCount > 0 ? 1u : 0u,
                    StartIndex = 0u,
                    BaseVertex = 0u,
                    StartInstance = 0u,
                    _pad0 = 0u,
                    _pad1 = 0u,
                    _pad2 = 0u
                };
            }

            WriteTelemetry(in state, in tuning, quality, stride, visitedCells, activeCells);
        }

        private VoxelMeshingTuningDTO ResolveTuning()
        {
            if (Tuning.IsCreated && Tuning.Length > 0)
                return SanitizeTuning(Tuning[0]);

            return VoxelSurfaceNetsDefaults.BuildDefaultTuning();
        }

        private float SampleCellDensity(int cellX, int cellY, int cellZ)
        {
            return SampleDensityGrid(cellX + 1, cellY + 1, cellZ + 1);
        }

        private uint ResolveEdgeMask(int signMask)
        {
            if (SurfaceEdgeMasks.IsCreated && (uint)signMask < (uint)SurfaceEdgeMasks.Length)
                return SurfaceEdgeMasks[signMask];

            uint edgeMask = 0u;
            AddInlineEdge(signMask, 0, 1, 0, ref edgeMask);
            AddInlineEdge(signMask, 2, 3, 1, ref edgeMask);
            AddInlineEdge(signMask, 4, 5, 2, ref edgeMask);
            AddInlineEdge(signMask, 6, 7, 3, ref edgeMask);
            AddInlineEdge(signMask, 0, 2, 4, ref edgeMask);
            AddInlineEdge(signMask, 1, 3, 5, ref edgeMask);
            AddInlineEdge(signMask, 4, 6, 6, ref edgeMask);
            AddInlineEdge(signMask, 5, 7, 7, ref edgeMask);
            AddInlineEdge(signMask, 0, 4, 8, ref edgeMask);
            AddInlineEdge(signMask, 1, 5, 9, ref edgeMask);
            AddInlineEdge(signMask, 2, 6, 10, ref edgeMask);
            AddInlineEdge(signMask, 3, 7, 11, ref edgeMask);
            return edgeMask;
        }

        private float SampleDensityGrid(int x, int y, int z)
        {
            int resolution = VoxelSurfaceNetsConstants.DensityResolution;
            int sx = math.clamp(x, 0, resolution - 1);
            int sy = math.clamp(y, 0, resolution - 1);
            int sz = math.clamp(z, 0, resolution - 1);
            int index = sx + (resolution * (sy + (resolution * sz)));
            if ((uint)index >= (uint)Densities.Length)
                return 1f;

            return Densities[index] * (1f / 127f);
        }

        private float SampleDensityLocal(float3 localPosition, float voxelSize, float quality)
        {
            float invVoxel = math.rcp(math.max(voxelSize, VoxelSurfaceNetsConstants.Epsilon));
            float3 grid = (localPosition * invVoxel) + 1f;
            float interpolationWeight = Smooth01(quality);
            float nearest = SampleDensityGrid((int)math.round(grid.x), (int)math.round(grid.y), (int)math.round(grid.z));
            if (interpolationWeight <= 0.0001f)
                return nearest;

            int3 baseGrid = (int3)math.floor(grid);
            float3 t = math.saturate(grid - baseGrid);
            float c000 = SampleDensityGrid(baseGrid.x, baseGrid.y, baseGrid.z);
            float c100 = SampleDensityGrid(baseGrid.x + 1, baseGrid.y, baseGrid.z);
            float c010 = SampleDensityGrid(baseGrid.x, baseGrid.y + 1, baseGrid.z);
            float c110 = SampleDensityGrid(baseGrid.x + 1, baseGrid.y + 1, baseGrid.z);
            float c001 = SampleDensityGrid(baseGrid.x, baseGrid.y, baseGrid.z + 1);
            float c101 = SampleDensityGrid(baseGrid.x + 1, baseGrid.y, baseGrid.z + 1);
            float c011 = SampleDensityGrid(baseGrid.x, baseGrid.y + 1, baseGrid.z + 1);
            float c111 = SampleDensityGrid(baseGrid.x + 1, baseGrid.y + 1, baseGrid.z + 1);
            float x00 = math.lerp(c000, c100, t.x);
            float x10 = math.lerp(c010, c110, t.x);
            float x01 = math.lerp(c001, c101, t.x);
            float x11 = math.lerp(c011, c111, t.x);
            float y0 = math.lerp(x00, x10, t.y);
            float y1 = math.lerp(x01, x11, t.y);
            float trilinear = math.lerp(y0, y1, t.z);
            return math.lerp(nearest, trilinear, interpolationWeight);
        }

        /// <summary>
        /// Calculates analytical surface normal vector via tetrahedral gradient sampling.
        /// Guards against zero-length gradients (\nabla D = 0) to prevent NaN/Inf normal vectors.
        /// </summary>
        private float3 CalculateTetraNormal(float3 vertexLocal, float voxelSize, float quality)
        {
            float eps = math.max(voxelSize * math.lerp(0.75f, 0.35f, Smooth01(quality)), VoxelSurfaceNetsConstants.Epsilon);
            const float tetra = 0.5773502691896258f;
            float3 k0 = new float3(tetra, -tetra, -tetra);
            float3 k1 = new float3(-tetra, -tetra, tetra);
            float3 k2 = new float3(-tetra, tetra, -tetra);
            float3 k3 = new float3(tetra, tetra, tetra);
            float3 gradient =
                (k0 * SampleDensityLocal(vertexLocal + (k0 * eps), voxelSize, quality)) +
                (k1 * SampleDensityLocal(vertexLocal + (k1 * eps), voxelSize, quality)) +
                (k2 * SampleDensityLocal(vertexLocal + (k2 * eps), voxelSize, quality)) +
                (k3 * SampleDensityLocal(vertexLocal + (k3 * eps), voxelSize, quality));
            float lenSq = math.dot(gradient, gradient);
            if (lenSq < 1e-8f || !math.all(math.isfinite(gradient)))
                return new float3(0f, 1f, 0f);

            return gradient * math.rsqrt(lenSq);
        }

        private static void AccumulateEdge(
            ref float3 sum,
            ref int hits,
            float a,
            float b,
            float3 pa,
            float3 pb,
            float voxelSize)
        {
            if ((a < 0f && b < 0f) || (a >= 0f && b >= 0f))
                return;

            float denom = math.abs(a - b);
            float t = math.clamp(math.abs(a) * math.rcp(math.max(denom, VoxelSurfaceNetsConstants.Epsilon)), VoxelSurfaceNetsConstants.Epsilon, 1f - VoxelSurfaceNetsConstants.Epsilon);
            sum += math.lerp(pa * voxelSize, pb * voxelSize, t);
            hits++;
        }

        private int TryGetCellVertex(int x, int y, int z)
        {
            int cellIndex = CellIndex(x, y, z);
            if ((uint)cellIndex >= (uint)CellVertexMap.Length)
                return -1;

            return CellVertexMap[cellIndex];
        }

        private bool TryResolveEdgeWinding(
            int ax,
            int ay,
            int az,
            int bx,
            int by,
            int bz,
            float iso,
            out bool reverse)
        {
            float a = SampleCellDensity(ax, ay, az) - iso;
            float b = SampleCellDensity(bx, by, bz) - iso;
            bool crosses = (a < 0f && b >= 0f) || (a >= 0f && b < 0f);
            reverse = a < b;
            return crosses;
        }

        private static int CellIndex(int x, int y, int z)
        {
            return x + (VoxelSurfaceNetsConstants.ChunkResolution * (y + (VoxelSurfaceNetsConstants.ChunkResolution * z)));
        }

        private static int BuildSignMask(
            float d000,
            float d100,
            float d010,
            float d110,
            float d001,
            float d101,
            float d011,
            float d111)
        {
            int mask = 0;
            mask |= d000 < 0f ? 1 << 0 : 0;
            mask |= d100 < 0f ? 1 << 1 : 0;
            mask |= d010 < 0f ? 1 << 2 : 0;
            mask |= d110 < 0f ? 1 << 3 : 0;
            mask |= d001 < 0f ? 1 << 4 : 0;
            mask |= d101 < 0f ? 1 << 5 : 0;
            mask |= d011 < 0f ? 1 << 6 : 0;
            mask |= d111 < 0f ? 1 << 7 : 0;
            return mask;
        }

        private static void AddInlineEdge(int mask, int a, int b, int edge, ref uint edgeMask)
        {
            bool sa = ((mask >> a) & 1) != 0;
            bool sb = ((mask >> b) & 1) != 0;
            if (sa != sb)
                edgeMask |= 1u << edge;
        }

        /// <summary>Squared-area gate. 0.5*|cross| is the triangle area, so this rejects only
        /// coincident or collinear vertices, never a legitimately small surface triangle.</summary>
        private const float DegenerateTriangleAreaSqEpsilon = 1e-10f;

        private bool IsEmittableTriangle(int i0, int i1, int i2)
        {
            if (i0 == i1 || i1 == i2 || i0 == i2)
                return false;

            if ((uint)i0 >= (uint)Vertices.Length ||
                (uint)i1 >= (uint)Vertices.Length ||
                (uint)i2 >= (uint)Vertices.Length)
            {
                return false;
            }

            float3 p0 = Vertices[i0].Position;
            float3 p1 = Vertices[i1].Position;
            float3 p2 = Vertices[i2].Position;
            float3 areaCross = math.cross(p1 - p0, p2 - p0);
            return math.lengthsq(areaCross) > DegenerateTriangleAreaSqEpsilon;
        }

        private bool TryEmitQuad(
            int a,
            int b,
            int c,
            int d,
            bool reverse,
            ref int indexCount,
            ref int rawDebugCount,
            float rawCaptureGate)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0)
                return true;

            if (indexCount + 6 > Indices.Length || indexCount + 6 > VoxelSurfaceNetsConstants.MaxIndices)
                return false;

            // Same winding as before, but each of the two triangles is emitted only when it has
            // real area. A Surface-Nets cell whose vertex lands on a shared grid corner can
            // collapse a quad into zero-area triangles, which produce garbage normals, flickering
            // slivers and physics-bake warnings.
            int t0b = reverse ? c : b;
            int t0c = reverse ? b : c;
            int t1b = reverse ? d : c;
            int t1c = reverse ? c : d;

            int firstIndex = indexCount;
            if (IsEmittableTriangle(a, t0b, t0c))
            {
                Indices[indexCount++] = (uint)a;
                Indices[indexCount++] = (uint)t0b;
                Indices[indexCount++] = (uint)t0c;
            }

            if (IsEmittableTriangle(a, t1b, t1c))
            {
                Indices[indexCount++] = (uint)a;
                Indices[indexCount++] = (uint)t1b;
                Indices[indexCount++] = (uint)t1c;
            }

            int emittedIndices = indexCount - firstIndex;
            if (emittedIndices <= 0)
                return true;

            if (rawCaptureGate < 0.5f || !RawDebugVertices.IsCreated || rawDebugCount + emittedIndices > RawDebugVertices.Length)
                return true;

            for (int i = 0; i < emittedIndices; i++)
                RawDebugVertices[rawDebugCount++] = Vertices[(int)Indices[firstIndex + i]].Position;

            return true;
        }

        private void WriteTelemetry(
            in ChunkMeshingStateDTO state,
            in VoxelMeshingTuningDTO tuning,
            float quality,
            int stride,
            int visitedCells,
            int activeCells)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int slot = (int)(Frame % (uint)math.min(TelemetryRing.Length, VoxelSurfaceNetsConstants.TelemetryFrames));
            float samplingRatio = math.rcp(math.max(stride, 1));
            float fullResolutionCellEstimate = VoxelSurfaceNetsConstants.CellCount;
            float decimationRatio = 1f - math.saturate(activeCells * math.rcp(math.max(fullResolutionCellEstimate, 1f)));
            float estimateMs = (visitedCells * 0.000012f) + (state.VertexCount * 0.000018f) + (state.IndexCount * 0.000003f);
            uint reason = 0u;
            byte flags = state.Flags;
            if (estimateMs > math.max(tuning.MaxExtractionMs, 2f))
            {
                flags = (byte)(flags | VoxelMeshingFlags.SlowExtraction);
                reason = VoxelSurfaceNetsConstants.FaultSlowExtraction;
            }

            TelemetryRing[slot] = new VoxelMeshingTelemetryEntry
            {
                Frame = Frame,
                ChunkHash = state.ChunkHash,
                VertexCount = state.VertexCount,
                IndexCount = state.IndexCount,
                ChunksMeshedThisFrame = state.VertexCount > 0 ? 1 : 0,
                ExtractionComputeTimeMs = estimateMs,
                GlobalQualityWeight = quality,
                DecimationRatio = decimationRatio,
                SamplingRatio = samplingRatio,
                Flags = flags,
                RawDebugVertexCount = state.RawDebugVertexCount,
                StateHash = math.hash(new uint4(state.ChunkHash, state.Version, (uint)state.VertexCount, (uint)state.IndexCount)),
                DumpReason = reason,
                _pad0 = 0UL,
                _pad1 = 0u
            };

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = slot;
        }

        private static int ResolveSamplingStride(float quality)
        {
            float qualityCurve = Smooth01(math.saturate((quality - 0.2f) * 1.25f));
            float sampleRatio = math.lerp(0.25f, 1f, qualityCurve);
            return math.clamp((int)math.round(math.rcp(math.max(sampleRatio, 0.25f))), 1, 4);
        }

        private static VoxelMeshingTuningDTO SanitizeTuning(in VoxelMeshingTuningDTO tuning)
        {
            VoxelMeshingTuningDTO sanitized = tuning;
            sanitized.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            sanitized.IsoSurface = math.clamp(tuning.IsoSurface, -1f, 1f);
            sanitized.DecimationAggression = math.saturate(tuning.DecimationAggression);
            sanitized.NormalSmoothingAngleDegrees = math.clamp(tuning.NormalSmoothingAngleDegrees, 0f, 89f);
            sanitized.VoxelSize = math.max(tuning.VoxelSize, VoxelSurfaceNetsConstants.Epsilon);
            sanitized.BiomeBlendScale = math.max(tuning.BiomeBlendScale, 0f);
            sanitized.MaxExtractionMs = math.max(tuning.MaxExtractionMs, 0.25f);
            sanitized.MaxChunksPerFrame = math.clamp(tuning.MaxChunksPerFrame, 1, 2);
            sanitized.ChunkResolution = VoxelSurfaceNetsConstants.ChunkResolution;
            return sanitized;
        }

        private static uint PackNormalRgb10A2(float3 normal)
        {
            float lenSq = math.max(math.dot(normal, normal), VoxelSurfaceNetsConstants.Epsilon);
            float3 n = normal * math.rsqrt(lenSq);
            uint x = (uint)math.clamp((int)math.round((n.x * 0.5f + 0.5f) * 1023f), 0, 1023);
            uint y = (uint)math.clamp((int)math.round((n.y * 0.5f + 0.5f) * 1023f), 0, 1023);
            uint z = (uint)math.clamp((int)math.round((n.z * 0.5f + 0.5f) * 1023f), 0, 1023);
            return x | (y << 10) | (z << 20) | (3u << 30);
        }

        private static float3 ResolveTangent(float3 normal)
        {
            float3 tangent = math.cross(normal, new float3(0f, 1f, 0f));
            float lenSq = math.dot(tangent, tangent);
            tangent = math.select(math.cross(normal, new float3(1f, 0f, 0f)), tangent, lenSq > 0.0004f);
            return tangent * math.rsqrt(math.max(math.dot(tangent, tangent), VoxelSurfaceNetsConstants.Epsilon));
        }

        private static uint PackColor(float3 positionLocal, float quality, float biomeBlendScale, uint chunkHash)
        {
            uint h = math.hash(new uint4((uint)math.abs((int)positionLocal.x), (uint)math.abs((int)positionLocal.y), (uint)math.abs((int)positionLocal.z), chunkHash));
            float hash01 = (h & 1023u) * (1f / 1023f);
            float blend = math.saturate((positionLocal.y * math.max(biomeBlendScale, 0.001f) * 0.01f) + (hash01 * 0.35f));
            uint r = (uint)math.clamp((int)math.round(hash01 * 255f), 0, 255);
            uint g = (uint)math.clamp((int)math.round(blend * 255f), 0, 255);
            uint b = (uint)math.clamp((int)math.round(math.saturate(quality) * 255f), 0, 255);
            return r | (g << 8) | (b << 16) | (255u << 24);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelSurfacePriorityJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<ChunkMeshingStateDTO> States;

        [NoAlias]
        public NativeArray<VoxelSurfacePriorityDTO> Priorities;

        [ReadOnly]
        public NativeArray<float4> FrustumPlanes;

        public double3 CameraAup;
        public float3 CameraForwardLocal;
        public int PlaneCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)States.Length || (uint)index >= (uint)Priorities.Length)
                return;

            ChunkMeshingStateDTO state = States[index];
            double3 chunkDelta = AupPrecisionMath.LocalDeltaDouble(state.ChunkOriginAup, CameraAup);
            float3 localToChunk = AupPrecisionMath.DowncastLocalDelta(chunkDelta, float3.zero) + state.BoundsCenterLocal;
            double3 centerDelta = chunkDelta + new double3(
                state.BoundsCenterLocal.x,
                state.BoundsCenterLocal.y,
                state.BoundsCenterLocal.z);
            double distanceSqDouble = math.lengthsq(centerDelta);
            float distanceSq = distanceSqDouble >= float.MaxValue ? float.MaxValue : (float)distanceSqDouble;
            float forwardDot = math.dot(math.normalizesafe(CameraForwardLocal, new float3(0f, 0f, 1f)), math.normalizesafe(localToChunk, new float3(0f, 0f, 1f)));
            float behindPenalty = math.lerp(250000f, 0f, math.saturate(forwardDot * 0.5f + 0.5f));
            float outsidePenalty = 0f;
            int planeLimit = math.min(PlaneCount, FrustumPlanes.IsCreated ? FrustumPlanes.Length : 0);
            for (int i = 0; i < planeLimit; i++)
            {
                float4 plane = FrustumPlanes[i];
                float signedDistance = math.dot(plane.xyz, localToChunk) + plane.w;
                outsidePenalty += math.select(0f, 1000000f, signedDistance < 0f);
            }

            float score = distanceSq + behindPenalty + outsidePenalty;
            state.Priority = (ushort)math.clamp((int)math.round(score * 0.0001f), 1, 65535);
            States[index] = state;
            Priorities[index] = new VoxelSurfacePriorityDTO
            {
                Score = score,
                ChunkIndex = index,
                ChunkHash = state.ChunkHash,
                Flags = state.Flags
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelSurfaceDirtySignalJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<VoxelSurfaceModifiedSignal> ModifiedSignals;

        [NoAlias]
        public NativeArray<ChunkMeshingStateDTO> States;

        public int SignalCount;

        public void Execute()
        {
            int signalLimit = math.min(SignalCount, ModifiedSignals.IsCreated ? ModifiedSignals.Length : 0);
            for (int signalIndex = 0; signalIndex < signalLimit; signalIndex++)
            {
                VoxelSurfaceModifiedSignal signal = ModifiedSignals[signalIndex];
                if (signal.Dirty == 0)
                    continue;

                // An exact ChunkHash match must win over claiming a free slot. Scanning for the
                // first "matching or empty" slot could claim an empty slot that precedes the
                // chunk's real state entry, leaving TWO live states for one chunk hash: the stale
                // one keeps its mesh while the new one re-meshes, which double-bakes colliders
                // (banned duplicate-collider class) and wastes a meshing slot.
                int targetIndex = -1;
                int freeIndex = -1;
                for (int stateIndex = 0; stateIndex < States.Length; stateIndex++)
                {
                    uint existingHash = States[stateIndex].ChunkHash;
                    if (existingHash == signal.ChunkHash)
                    {
                        targetIndex = stateIndex;
                        break;
                    }

                    if (freeIndex < 0 && existingHash == 0u)
                        freeIndex = stateIndex;
                }

                if (targetIndex < 0)
                    targetIndex = freeIndex;
                if (targetIndex < 0)
                    continue;

                {
                    ChunkMeshingStateDTO state = States[targetIndex];
                    state.ChunkOriginAup = signal.ChunkOriginAup;
                    state.ChunkHash = signal.ChunkHash;
                    state.Version = signal.Version;
                    state.Stage = (byte)VoxelMeshingStage.Dirty;
                    state.Flags = (byte)(state.Flags | VoxelMeshingFlags.Dirty | VoxelMeshingFlags.ModifiedByLaser);
                    state.Priority = signal.ForceHighPriority != 0 ? (ushort)1 : state.Priority;
                    States[targetIndex] = state;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelSurfaceAabbShiftJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<VoxelSurfaceAabbDTO> Aabbs;

        public double3 ShiftDeltaAup;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Aabbs.Length)
                return;

            VoxelSurfaceAabbDTO aabb = Aabbs[index];
            aabb.CenterAup -= ShiftDeltaAup;
            aabb.Version++;
            Aabbs[index] = aabb;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelSurfacePhysicsBakeRequestJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ChunkMeshingStateDTO> States;

        [NoAlias]
        public NativeArray<VoxelSurfacePhysicsBakeRequestDTO> Requests;

        public int MeshIdBase;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)States.Length || (uint)index >= (uint)Requests.Length)
                return;

            ChunkMeshingStateDTO state = States[index];
            if (state.Stage != (byte)VoxelMeshingStage.ReadyForUpload || state.IndexCount <= 0)
                return;

            Requests[index] = new VoxelSurfacePhysicsBakeRequestDTO
            {
                MeshId = MeshIdBase + index,
                ChunkIndex = index,
                ChunkHash = state.ChunkHash,
                Version = state.Version,
                Pending = 1,
                Completed = 0,
                _pad0 = 0,
                _pad1 = 0UL,
                _pad2 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelSurfaceHzbCullJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<VoxelSurfaceAabbDTO> Aabbs;

        [NoAlias]
        public NativeArray<VoxelSurfacePriorityDTO> Priorities;

        [ReadOnly]
        [NoAlias]
        public NativeArray<VoxelSurfaceHzbTileDTO> HzbTiles;

        public float4x4 CameraRelativeViewProjection;
        public double3 CameraAup;
        public int HzbWidth;
        public int HzbHeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Aabbs.Length || (uint)index >= (uint)Priorities.Length)
                return;

            VoxelSurfaceAabbDTO aabb = Aabbs[index];
            VoxelSurfacePriorityDTO priority = Priorities[index];
            if (!HzbTiles.IsCreated || HzbWidth <= 0 || HzbHeight <= 0)
            {
                aabb.VisibleFlags = 1;
                Aabbs[index] = aabb;
                return;
            }

            float3 cameraLocal = AupPrecisionMath.LocalDeltaFloat3(aabb.CenterAup, CameraAup, float3.zero);
            float3 extents = math.max(aabb.ExtentsLocal, new float3(VoxelSurfaceNetsConstants.Epsilon));
            float3 minNdc = new float3(float.MaxValue);
            float3 maxNdc = new float3(float.MinValue);
            int projectedCorners = 0;
            AccumulateProjectedCorner(cameraLocal + new float3(-extents.x, -extents.y, -extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(extents.x, -extents.y, -extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(-extents.x, extents.y, -extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(extents.x, extents.y, -extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(-extents.x, -extents.y, extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(extents.x, -extents.y, extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(-extents.x, extents.y, extents.z), ref minNdc, ref maxNdc, ref projectedCorners);
            AccumulateProjectedCorner(cameraLocal + new float3(extents.x, extents.y, extents.z), ref minNdc, ref maxNdc, ref projectedCorners);

            if (projectedCorners <= 0)
            {
                aabb.VisibleFlags = 1;
                Aabbs[index] = aabb;
                return;
            }

            bool outside = maxNdc.x < -1f || minNdc.x > 1f || maxNdc.y < -1f || minNdc.y > 1f || maxNdc.z < 0f || minNdc.z > 1f;
            if (outside)
            {
                aabb.VisibleFlags = 0;
                priority.Score += 1000000f;
                priority.Flags |= 1u << 24;
                Aabbs[index] = aabb;
                Priorities[index] = priority;
                return;
            }

            float2 minScreen = math.saturate((math.max(minNdc.xy, new float2(-1f)) * 0.5f) + 0.5f);
            float2 maxScreen = math.saturate((math.min(maxNdc.xy, new float2(1f)) * 0.5f) + 0.5f);
            int tx0 = math.clamp((int)math.floor(minScreen.x * HzbWidth), 0, HzbWidth - 1);
            int ty0 = math.clamp((int)math.floor(minScreen.y * HzbHeight), 0, HzbHeight - 1);
            int tx1 = math.clamp((int)math.floor(maxScreen.x * HzbWidth), 0, HzbWidth - 1);
            int ty1 = math.clamp((int)math.floor(maxScreen.y * HzbHeight), 0, HzbHeight - 1);
            int txc = (tx0 + tx1) >> 1;
            int tyc = (ty0 + ty1) >> 1;
            float conservativeDepth = math.saturate(minNdc.z);
            bool occluded =
                IsTileOccluded(tx0, ty0, conservativeDepth) &&
                IsTileOccluded(tx1, ty0, conservativeDepth) &&
                IsTileOccluded(tx0, ty1, conservativeDepth) &&
                IsTileOccluded(tx1, ty1, conservativeDepth) &&
                IsTileOccluded(txc, tyc, conservativeDepth);
            aabb.VisibleFlags = (byte)math.select(1, 0, occluded);
            if (occluded)
            {
                priority.Score += 2000000f;
                priority.Flags |= 1u << 25;
            }

            Aabbs[index] = aabb;
            Priorities[index] = priority;
        }

        private void AccumulateProjectedCorner(float3 cameraLocal, ref float3 minNdc, ref float3 maxNdc, ref int projectedCorners)
        {
            float4 clip = math.mul(CameraRelativeViewProjection, new float4(cameraLocal, 1f));
            if (clip.w <= VoxelSurfaceNetsConstants.Epsilon || !math.all(math.isfinite(clip)))
            {
                projectedCorners = -1024;
                return;
            }

            float3 ndc = clip.xyz * math.rcp(math.max(clip.w, VoxelSurfaceNetsConstants.Epsilon));
            if (!math.all(math.isfinite(ndc)))
            {
                projectedCorners = -1024;
                return;
            }

            minNdc = math.min(minNdc, ndc);
            maxNdc = math.max(maxNdc, ndc);
            projectedCorners++;
        }

        private bool IsTileOccluded(int tx, int ty, float conservativeDepth)
        {
            int tileIndex = tx + (ty * HzbWidth);
            if ((uint)tileIndex >= (uint)HzbTiles.Length)
                return false;

            float hzbDepth = HzbTiles[tileIndex].Depth01;
            return conservativeDepth > hzbDepth + 0.002f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct VoxelSurfaceGpuUploadCopyJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<VoxelVertexDTO> SourceVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<uint> SourceIndices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<VoxelSurfaceIndirectArgsDTO> SourceIndirectArgs;

        [NoAlias]
        public NativeArray<VoxelVertexDTO> DestinationVertices;

        [NoAlias]
        public NativeArray<uint> DestinationIndices;

        [NoAlias]
        public NativeArray<VoxelSurfaceIndirectArgsDTO> DestinationIndirectArgs;

        public int VertexCount;
        public int IndexCount;

        public void Execute()
        {
            int vertexLimit = SourceVertices.IsCreated && DestinationVertices.IsCreated
                ? math.min(math.max(VertexCount, 0), math.min(SourceVertices.Length, DestinationVertices.Length))
                : 0;
            if (vertexLimit > 0)
            {
                void* srcVertices = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceVertices);
                void* dstVertices = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DestinationVertices);
                UnsafeUtility.MemCpy(dstVertices, srcVertices, vertexLimit * UnsafeUtility.SizeOf<VoxelVertexDTO>());
            }

            int indexLimit = SourceIndices.IsCreated && DestinationIndices.IsCreated
                ? math.min(math.max(IndexCount, 0), math.min(SourceIndices.Length, DestinationIndices.Length))
                : 0;
            if (indexLimit > 0)
            {
                void* srcIndices = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceIndices);
                void* dstIndices = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DestinationIndices);
                UnsafeUtility.MemCpy(dstIndices, srcIndices, indexLimit * UnsafeUtility.SizeOf<uint>());
            }

            if (!DestinationIndirectArgs.IsCreated || DestinationIndirectArgs.Length <= 0)
                return;

            DestinationIndirectArgs[0] = SourceIndirectArgs.IsCreated && SourceIndirectArgs.Length > 0
                ? SourceIndirectArgs[0]
                : BuildFallbackArgs(indexLimit);
        }

        private static VoxelSurfaceIndirectArgsDTO BuildFallbackArgs(int indexCount)
        {
            return new VoxelSurfaceIndirectArgsDTO
            {
                IndexCountPerInstance = (uint)math.max(indexCount, 0),
                InstanceCount = indexCount > 0 ? 1u : 0u,
                StartIndex = 0u,
                BaseVertex = 0u,
                StartInstance = 0u,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    public static class VoxelSurfaceNetsDefaults
    {
        public static VoxelMeshingTuningDTO BuildDefaultTuning()
        {
            return new VoxelMeshingTuningDTO
            {
                GlobalQualityWeight = 1f,
                IsoSurface = 0f,
                DecimationAggression = 0f,
                NormalSmoothingAngleDegrees = 35f,
                VoxelSize = 1f,
                BiomeBlendScale = 1f,
                MaxExtractionMs = 2f,
                DebugRawCapture01 = 0f,
                MaxChunksPerFrame = 2,
                ChunkResolution = VoxelSurfaceNetsConstants.ChunkResolution,
                Version = 1u,
                Flags = 0u,
                ForceRemeshVersion = 0u,
                LastCsvHash = 0u,
                LastCsvWriteTicks = 0UL
            };
        }

        public static MockVoxelDensityArray BuildDefaultMockDensity()
        {
            return new MockVoxelDensityArray
            {
                Dimensions = new int3(
                    VoxelSurfaceNetsConstants.DensityResolution,
                    VoxelSurfaceNetsConstants.DensityResolution,
                    VoxelSurfaceNetsConstants.DensityResolution),
                VoxelSize = 1f,
                CenterLocal = new float3(16f, 16f, 16f),
                Radius = 12f,
                ShellThickness = 1.5f,
                Seed = 0x51B61u,
                Flags = 0u,
                _pad0 = 0u
            };
        }
    }
}
