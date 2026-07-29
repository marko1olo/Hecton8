using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.ProceduralGen
{
    internal struct BioForgeBranch
    {
        public float3 Start;
        public float3 End;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float RadiusStart;
        public float RadiusEnd;
        public float MaxRadius;
    }

    internal struct BioForgeRawVertex
    {
        public float3 Position;
    }

    internal struct BioForgeMeshVertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 Uv;
        public float4 Color;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeSdfBuildJob : IJobParallelFor
    {
        public const int ModeFlagRock = 1 << 0;
        public const int ModeFlagRibbon = 1 << 1;
        public const int ModeFlagPorous = 1 << 2;

        [ReadOnly] public NativeArray<BioForgeBranch> Branches;
        [WriteOnly] public NativeArray<float> Density;

        public int PointResolution;
        public int BranchCount;
        public int ModeFlags;
        public uint Seed;
        public float3 BoundsMin;
        public float3 Step;
        public float SmoothMinK;
        public float RibbonThicknessScale;
        public float RibbonWidthScale;
        public float RockRadius;
        public float RockNoiseAmplitude;
        public float RockNoiseFrequency;
        public int RockPoreCount;
        public float RockPoreRadius;
        public float RockPoreSurfaceBias;

        public void Execute(int index)
        {
            int x = index % PointResolution;
            int y = (index / PointResolution) % PointResolution;
            int z = index / (PointResolution * PointResolution);

            float3 p = BoundsMin + new float3(x, y, z) * Step;
            bool rockMode = (ModeFlags & ModeFlagRock) != 0;
            bool ribbonMode = (ModeFlags & ModeFlagRibbon) != 0;
            bool porousMode = (ModeFlags & ModeFlagPorous) != 0;
            float density = rockMode ? EvaluateRock(p, porousMode) : ribbonMode ? EvaluateRibbonBranches(p) : EvaluateBranches(p);
            Density[index] = math.isfinite(density) ? density : 1f;
        }

        private float EvaluateBranches(float3 p)
        {
            if (BranchCount <= 0)
                return 1f;

            int max = math.min(BranchCount, Branches.Length);
            if (max <= 0)
                return 1f;

            float result = CapsuleConeSdf(p, Branches[0]);
            float cullMargin = ResolveCullMargin();
            for (int i = 1; i < max; i++)
            {
                BioForgeBranch branch = Branches[i];
                if (CanCullBranch(p, branch, result, cullMargin))
                    continue;

                float sdf = CapsuleConeSdf(p, branch);
                result = SmoothMinExp(result, sdf, SmoothMinK);
            }

            return result;
        }

        private float EvaluateRibbonBranches(float3 p)
        {
            if (BranchCount <= 0)
                return 1f;

            int max = math.min(BranchCount, Branches.Length);
            if (max <= 0)
                return 1f;

            float result = RibbonCapsuleSdf(p, Branches[0]);
            float cullMargin = ResolveCullMargin();
            for (int i = 1; i < max; i++)
            {
                BioForgeBranch branch = Branches[i];
                if (CanCullBranch(p, branch, result, cullMargin))
                    continue;

                float sdf = RibbonCapsuleSdf(p, branch);
                result = SmoothMinExp(result, sdf, SmoothMinK);
            }

            return result;
        }

        private float ResolveCullMargin()
        {
            float kk = math.max(0.01f, SmoothMinK);
            float voxelMargin = math.cmax(math.abs(Step)) * 2f;
            return math.max(voxelMargin, 8f * math.rcp(kk));
        }

        private static bool CanCullBranch(float3 p, BioForgeBranch branch, float current, float margin)
        {
            if (!math.isfinite(current))
                return false;

            float threshold = current + margin;
            if (threshold <= 0f)
                return false;

            float3 closest = math.clamp(p, branch.BoundsMin, branch.BoundsMax);
            float3 delta = p - closest;
            float distSq = math.lengthsq(delta);
            return distSq > threshold * threshold;
        }

        private float EvaluateRock(float3 p, bool porous)
        {
            float radius = math.max(0.01f, RockRadius);
            float pLenSq = math.lengthsq(p);
            float sphere = pLenSq * math.rsqrt(math.max(pLenSq, 1e-12f)) - radius;
            float seedOffset = (Seed & 1023u) * 0.013671875f;
            float3 offset = new float3(seedOffset, seedOffset * 1.37f, seedOffset * 1.91f);
            float simplex = noise.snoise(p * math.max(0.001f, RockNoiseFrequency) + offset);
            float result = sphere + simplex * math.max(0f, RockNoiseAmplitude);
            if (!porous)
                return result;

            int poreCount = math.clamp(RockPoreCount, 0, 32);
            float poreRadius = math.max(0.01f, RockPoreRadius);
            float surfaceBias = math.saturate(RockPoreSurfaceBias);
            for (int i = 0; i < poreCount; i++)
            {
                float3 direction = HashDirection((uint)i);
                float centerScale = math.lerp(0.18f, 0.92f, surfaceBias);
                float3 poreCenter = direction * radius * centerScale;
                float poreJitter = 0.72f + Hash01((uint)i, 0xB5297A4Du) * 0.62f;
                float poreNoise = noise.snoise((p + offset + poreCenter) * math.max(0.001f, RockNoiseFrequency * 1.67f));
                float localPoreRadius = poreRadius * poreJitter * (1f + poreNoise * 0.18f);
                float3 delta = p - poreCenter;
                float deltaLenSq = math.lengthsq(delta);
                float pore = deltaLenSq * math.rsqrt(math.max(deltaLenSq, 1e-12f)) - localPoreRadius;
                result = math.max(result, -pore);
            }

            return result;
        }

        private static float CapsuleConeSdf(float3 p, BioForgeBranch branch)
        {
            float3 ba = branch.End - branch.Start;
            float lenSq = math.max(1e-6f, math.dot(ba, ba));
            float h = math.saturate(math.dot(p - branch.Start, ba) * math.rcp(lenSq));
            float radius = math.lerp(branch.RadiusStart, branch.RadiusEnd, h);
            float3 delta = p - (branch.Start + ba * h);
            float deltaLenSq = math.lengthsq(delta);
            float deltaLen = deltaLenSq * math.rsqrt(math.max(deltaLenSq, 1e-12f));
            return deltaLen - math.max(0.001f, radius);
        }

        private float RibbonCapsuleSdf(float3 p, BioForgeBranch branch)
        {
            float3 ba = branch.End - branch.Start;
            float lenSq = math.max(1e-6f, math.dot(ba, ba));
            float h = math.saturate(math.dot(p - branch.Start, ba) * math.rcp(lenSq));
            float radius = math.lerp(branch.RadiusStart, branch.RadiusEnd, h);
            float3 axis = ba * math.rsqrt(lenSq);
            float3 helper = math.abs(axis.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 side = math.cross(axis, helper);
            float sideLenSq = math.lengthsq(side);
            side = sideLenSq > 1e-8f ? side * math.rsqrt(sideLenSq) : new float3(1f, 0f, 0f);
            float3 normal = math.cross(side, axis);
            float3 delta = p - (branch.Start + ba * h);
            float width = math.max(0.001f, radius * math.max(0.5f, RibbonWidthScale));
            float thickness = math.max(0.001f, radius * math.max(0.05f, RibbonThicknessScale));
            float sideDistance = math.dot(delta, side) * math.rcp(width);
            float normalDistance = math.dot(delta, normal) * math.rcp(thickness);
            float ellipse = math.length(new float2(sideDistance, normalDistance)) - 1f;
            return ellipse * math.min(width, thickness);
        }

        private static float SmoothMinExp(float a, float b, float k)
        {
            float kk = math.max(0.01f, k);
            float radius = 8f * math.rcp(kk);
            float h = math.saturate(0.5f + 0.5f * (b - a) * math.rcp(radius));
            return math.lerp(b, a, h) - (radius * h * (1f - h));
        }

        private float3 HashDirection(uint index)
        {
            float3 value = new float3(
                Hash01(index, 0x68E31DA4u) * 2f - 1f,
                Hash01(index, 0xB5297A4Du) * 2f - 1f,
                Hash01(index, 0x1B56C4E9u) * 2f - 1f);
            float lenSq = math.lengthsq(value);
            return lenSq > 1e-8f ? value * math.rsqrt(lenSq) : new float3(0f, 1f, 0f);
        }

        private float Hash01(uint index, uint salt)
        {
            uint h = Seed ^ salt ^ (index * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h * 2.3283064e-10f;
        }

    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeMarchingCubesJob : IJob
    {
        [ReadOnly] public NativeArray<float> Density;
        public NativeList<BioForgeRawVertex> RawVertices;
        public NativeArray<int> Overflow;

        public int Cells;
        public int Points;
        public float3 BoundsMin;
        public float3 Step;

        public void Execute()
        {
            for (int z = 0; z < Cells; z++)
            {
                for (int y = 0; y < Cells; y++)
                {
                    for (int x = 0; x < Cells; x++)
                    {
                        EmitCell(x, y, z);
                    }
                }
            }
        }

        private void EmitCell(int x, int y, int z)
        {
            float3 p0 = Point(x, y, z);
            float3 p1 = Point(x + 1, y, z);
            float3 p2 = Point(x + 1, y + 1, z);
            float3 p3 = Point(x, y + 1, z);
            float3 p4 = Point(x, y, z + 1);
            float3 p5 = Point(x + 1, y, z + 1);
            float3 p6 = Point(x + 1, y + 1, z + 1);
            float3 p7 = Point(x, y + 1, z + 1);

            float d0 = Density[Index(x, y, z)];
            float d1 = Density[Index(x + 1, y, z)];
            float d2 = Density[Index(x + 1, y + 1, z)];
            float d3 = Density[Index(x, y + 1, z)];
            float d4 = Density[Index(x, y, z + 1)];
            float d5 = Density[Index(x + 1, y, z + 1)];
            float d6 = Density[Index(x + 1, y + 1, z + 1)];
            float d7 = Density[Index(x, y + 1, z + 1)];

            EmitTetra(p0, d0, p5, d5, p1, d1, p6, d6);
            EmitTetra(p0, d0, p1, d1, p2, d2, p6, d6);
            EmitTetra(p0, d0, p2, d2, p3, d3, p6, d6);
            EmitTetra(p0, d0, p3, d3, p7, d7, p6, d6);
            EmitTetra(p0, d0, p7, d7, p4, d4, p6, d6);
            EmitTetra(p0, d0, p4, d4, p5, d5, p6, d6);
        }

        private void EmitTetra(float3 p0, float d0, float3 p1, float d1, float3 p2, float d2, float3 p3, float d3)
        {
            bool i0 = d0 < 0f;
            bool i1 = d1 < 0f;
            bool i2 = d2 < 0f;
            bool i3 = d3 < 0f;
            int count = (i0 ? 1 : 0) + (i1 ? 1 : 0) + (i2 ? 1 : 0) + (i3 ? 1 : 0);
            if (count == 0 || count == 4)
                return;

            if (count == 1)
            {
                if (i0) EmitOne(p0, d0, p1, d1, p2, d2, p3, d3, false);
                else if (i1) EmitOne(p1, d1, p0, d0, p3, d3, p2, d2, false);
                else if (i2) EmitOne(p2, d2, p0, d0, p1, d1, p3, d3, false);
                else EmitOne(p3, d3, p0, d0, p2, d2, p1, d1, false);
                return;
            }

            if (count == 3)
            {
                if (!i0) EmitOne(p0, d0, p1, d1, p3, d3, p2, d2, true);
                else if (!i1) EmitOne(p1, d1, p0, d0, p2, d2, p3, d3, true);
                else if (!i2) EmitOne(p2, d2, p0, d0, p3, d3, p1, d1, true);
                else EmitOne(p3, d3, p0, d0, p1, d1, p2, d2, true);
                return;
            }

            if (i0 && i1) EmitPair(p0, d0, p1, d1, p2, d2, p3, d3);
            else if (i0 && i2) EmitPair(p0, d0, p2, d2, p1, d1, p3, d3);
            else if (i0 && i3) EmitPair(p0, d0, p3, d3, p1, d1, p2, d2);
            else if (i1 && i2) EmitPair(p1, d1, p2, d2, p0, d0, p3, d3);
            else if (i1 && i3) EmitPair(p1, d1, p3, d3, p0, d0, p2, d2);
            else EmitPair(p2, d2, p3, d3, p0, d0, p1, d1);
        }

        private void EmitOne(float3 inside, float insideD, float3 a, float aD, float3 b, float bD, float3 c, float cD, bool invert)
        {
            float3 v0 = Interpolate(inside, a, insideD, aD);
            float3 v1 = Interpolate(inside, b, insideD, bD);
            float3 v2 = Interpolate(inside, c, insideD, cD);
            if (invert)
                EmitTriangle(v0, v2, v1);
            else
                EmitTriangle(v0, v1, v2);
        }

        private void EmitPair(float3 a, float aD, float3 b, float bD, float3 c, float cD, float3 d, float dD)
        {
            float3 v0 = Interpolate(a, c, aD, cD);
            float3 v1 = Interpolate(b, c, bD, cD);
            float3 v2 = Interpolate(a, d, aD, dD);
            float3 v3 = Interpolate(b, d, bD, dD);
            EmitTriangle(v0, v1, v2);
            EmitTriangle(v2, v1, v3);
        }

        private void EmitTriangle(float3 a, float3 b, float3 c)
        {
            if (RawVertices.Length + 3 > RawVertices.Capacity)
            {
                Overflow[0] = 1;
                return;
            }

            RawVertices.Add(new BioForgeRawVertex { Position = a });
            RawVertices.Add(new BioForgeRawVertex { Position = b });
            RawVertices.Add(new BioForgeRawVertex { Position = c });
        }

        private static float3 Interpolate(float3 a, float3 b, float da, float db)
        {
            float denom = da - db;
            float t = math.abs(denom) > 1e-6f ? math.clamp(da * math.rcp(denom), 0.001f, 0.999f) : 0.5f;
            return a + (b - a) * t;
        }

        private int Index(int x, int y, int z)
        {
            return x + y * Points + z * Points * Points;
        }

        private float3 Point(int x, int y, int z)
        {
            return BoundsMin + new float3(x, y, z) * Step;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeVertexBakeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BioForgeRawVertex> RawVertices;
        [ReadOnly] public NativeArray<float> Density;
        [WriteOnly] public NativeArray<BioForgeMeshVertex> Vertices;

        public float3 BoundsMin;
        public float3 BoundsMax;
        public float3 InvStep;
        public int PointResolution;

        public void Execute(int index)
        {
            int triBase = (index / 3) * 3;
            float3 p0 = RawVertices[triBase].Position;
            float3 p1 = RawVertices[triBase + 1].Position;
            float3 p2 = RawVertices[triBase + 2].Position;
            float3 normal = math.cross(p1 - p0, p2 - p0);
            float lenSq = math.lengthsq(normal);
            float3 faceNormal = lenSq > 1e-10f ? normal * math.rsqrt(lenSq) : new float3(0f, 1f, 0f);

            float3 p = RawVertices[index].Position;
            normal = ResolveSdfNormal(p, faceNormal);
            float height = math.max(0.001f, BoundsMax.y - BoundsMin.y);
            float height01 = math.saturate((p.y - BoundsMin.y) * math.rcp(height));
            float2 uv = new float2(ComputeCylindricalU(p), height01);
            float cavityAo = ResolveSdfAmbientOcclusion(p, normal);

            Vertices[index] = new BioForgeMeshVertex
            {
                Position = p,
                Normal = normal,
                Uv = uv,
                // Vertex colour channel contract, 3DMODEL_FLORA_CORAL.md section 2 and
                // 3DMODEL_GEOLOGY_ROCKS.md section 4: R = sway amplitude, G = bioluminescence mask
                // (0 is the contract's explicit value for non-emissive tissue, so it is a real
                // answer here and not a placeholder), B = baked ambient occlusion / cavity
                // darkness, A = family-specific mask.
                //
                // B used to be a hardcoded 0f, which is the MAXIMALLY occluded value: every vertex
                // this job emitted claimed to sit in a sealed crevice. It stayed invisible because
                // the only consumer read occlusion out of COLOR.a instead, so correcting that
                // reader would have turned this constant into a flat 28% darkening of the mesh.
                Color = new float4(height01, 0f, cavityAo, 1f)
            };
        }

        private float ComputeCylindricalU(float3 p)
        {
            float cx = (BoundsMin.x + BoundsMax.x) * 0.5f;
            float cz = (BoundsMin.z + BoundsMax.z) * 0.5f;
            float angle = global::Hecton8.Core.MathLodApproximation.ApproxAtan2Fast(p.z - cz, p.x - cx);
            return angle * 0.15915494309189535f + 0.5f;
        }

        /// <summary>
        /// Baked ambient occlusion / cavity darkness for vertex colour channel B, per
        /// 3DMODEL_FLORA_CORAL.md section 2 -- "Use low values in crevices, under plates, root
        /// clusters, and branch intersections".
        ///
        /// This is the standard signed-distance-field occlusion march, and it is a real measurement
        /// of this mesh's own geometry rather than a curvature heuristic. Step outward along the
        /// surface normal and compare the distance marched against the field's own distance to the
        /// nearest surface: on an exposed convex tip the field keeps pace with the march and nothing
        /// accumulates, while inside a crevice, under a plate, or at a branch intersection a
        /// neighbouring surface holds the field below the step distance and occlusion accumulates.
        /// The distinction matters because h8forge/vertexcolor.py's curvature_edge_wear is explicit
        /// that a curvature estimate is honest for wear and is NOT honest for occlusion; this reads
        /// the actual field the mesh was built from, which is the same quantity a bake integrates.
        ///
        /// The march is deliberately bounded to a few grid cells, for the same reason the Blender
        /// lane bounds its bake distance to 0.35 m: unbounded rays turn local cavity contrast into a
        /// global sky-occlusion term and bury exactly the crevice detail the contract asks for.
        ///
        /// Each sample is normalised by its own march distance, so the result is scale-invariant and
        /// needs no tuned gain constant. Sign convention is the one this file's marching cubes uses
        /// at the cube classification (<c>d &lt; 0f</c> is inside solid, isosurface at 0).
        /// </summary>
        private float ResolveSdfAmbientOcclusion(float3 p, float3 outwardNormal)
        {
            // No field to measure. 1 = fully unoccluded, never invented darkness: a darkening
            // default would bake fake shadow into every asset whose occlusion source was missing,
            // which is the failure h8forge/vertexcolor.py write_organic_channels calls out.
            if (PointResolution < 3 || Density.Length == 0)
                return 1f;

            float3 cellVector = math.rcp(math.max(math.abs(InvStep), new float3(1e-6f, 1e-6f, 1e-6f)));
            float cell = math.csum(cellVector) * (1f / 3f);
            if (!math.isfinite(cell) || cell <= 1e-6f)
                return 1f;

            const int MarchSteps = 5;
            float occlusion = 0f;
            float weight = 1f;
            float weightSum = 0f;
            for (int step = 1; step <= MarchSteps; step++)
            {
                float march = cell * step;
                float field = SampleDensityNearest(p + outwardNormal * march);
                // (march - field) is how much closer the nearest surface is than the free-space
                // distance this step assumed. Saturating bounds each term to 0..1 and also handles
                // a negative field, which happens when the march re-enters solid in a concavity.
                occlusion += math.saturate((march - field) * math.rcp(march)) * weight;
                weightSum += weight;
                weight *= 0.5f;
            }

            occlusion = weightSum > 1e-6f ? occlusion * math.rcp(weightSum) : 0f;
            return math.saturate(1f - occlusion);
        }

        private float SampleDensityNearest(float3 world)
        {
            float3 grid = (world - BoundsMin) * InvStep;
            int max = PointResolution - 1;
            int x = math.clamp((int)math.round(grid.x), 0, max);
            int y = math.clamp((int)math.round(grid.y), 0, max);
            int z = math.clamp((int)math.round(grid.z), 0, max);
            return Density[Index(x, y, z)];
        }

        private float3 ResolveSdfNormal(float3 p, float3 fallback)
        {
            if (PointResolution < 3 || Density.Length == 0)
                return fallback;

            float3 grid = (p - BoundsMin) * InvStep;
            int max = PointResolution - 2;
            int x = math.clamp((int)math.round(grid.x), 1, max);
            int y = math.clamp((int)math.round(grid.y), 1, max);
            int z = math.clamp((int)math.round(grid.z), 1, max);

            float dx = Density[Index(x + 1, y, z)] - Density[Index(x - 1, y, z)];
            float dy = Density[Index(x, y + 1, z)] - Density[Index(x, y - 1, z)];
            float dz = Density[Index(x, y, z + 1)] - Density[Index(x, y, z - 1)];
            float3 normal = new float3(dx, dy, dz);
            float lenSq = math.lengthsq(normal);
            return lenSq > 1e-10f ? normal * math.rsqrt(lenSq) : fallback;
        }

        private int Index(int x, int y, int z)
        {
            return x + y * PointResolution + z * PointResolution * PointResolution;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeEdgeCollapseDecimationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BioForgeMeshVertex> SourceVertices;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<BioForgeMeshVertex> OutputVertices;

        public int SourceTriangleCount;
        public int OutputTriangleCount;
        public float CollapseCellSize;

        public void Execute(int triangleIndex)
        {
            int sourceTriangle = math.min(SourceTriangleCount - 1, (int)((long)triangleIndex * SourceTriangleCount / math.max(1, OutputTriangleCount)));
            int src = sourceTriangle * 3;
            int dst = triangleIndex * 3;

            BioForgeMeshVertex a = SourceVertices[src];
            BioForgeMeshVertex b = SourceVertices[src + 1];
            BioForgeMeshVertex c = SourceVertices[src + 2];

            SnapTriangle(ref a, ref b, ref c);
            NormalizeNormals(ref a, ref b, ref c);

            OutputVertices[dst] = a;
            OutputVertices[dst + 1] = b;
            OutputVertices[dst + 2] = c;
        }

        private void SnapTriangle(ref BioForgeMeshVertex a, ref BioForgeMeshVertex b, ref BioForgeMeshVertex c)
        {
            if (CollapseCellSize <= 1e-5f)
                return;

            float invCell = math.rcp(CollapseCellSize);
            a.Position = SnapPosition(a.Position, invCell, CollapseCellSize);
            b.Position = SnapPosition(b.Position, invCell, CollapseCellSize);
            c.Position = SnapPosition(c.Position, invCell, CollapseCellSize);
        }

        private static float3 SnapPosition(float3 position, float invCell, float cellSize)
        {
            return math.floor(position * invCell + 0.5f) * cellSize;
        }

        private static void NormalizeNormals(ref BioForgeMeshVertex a, ref BioForgeMeshVertex b, ref BioForgeMeshVertex c)
        {
            float3 normal = math.cross(b.Position - a.Position, c.Position - a.Position);
            float lenSq = math.lengthsq(normal);
            float3 fallback = lenSq > 1e-10f ? normal * math.rsqrt(lenSq) : new float3(0f, 1f, 0f);
            a.Normal = NormalizeOrFallback(a.Normal, fallback);
            b.Normal = NormalizeOrFallback(b.Normal, fallback);
            c.Normal = NormalizeOrFallback(c.Normal, fallback);
        }

        private static float3 NormalizeOrFallback(float3 normal, float3 fallback)
        {
            float lenSq = math.lengthsq(normal);
            return math.all(math.isfinite(normal)) && lenSq > 1e-10f ? normal * math.rsqrt(lenSq) : fallback;
        }
    }
}
