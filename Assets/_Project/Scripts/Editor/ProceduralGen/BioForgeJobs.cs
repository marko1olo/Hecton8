using Unity.Burst;
using Unity.Collections;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeSdfBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BioForgeBranch> Branches;
        [WriteOnly] public NativeArray<float> Density;

        public int PointResolution;
        public int BranchCount;
        public int Mode;
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
            float density = Mode == 1 ? EvaluateRock(p, false) : Mode == 2 ? EvaluateRibbonBranches(p) : Mode == 3 ? EvaluateRock(p, true) : EvaluateBranches(p);
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
            float invK = math.rcp(kk);
            float limit = 80f * invK;
            float aa = math.clamp(a, -limit, limit);
            float bb = math.clamp(b, -limit, limit);
            float ea = math.exp(-kk * aa);
            float eb = math.exp(-kk * bb);
            return -math.log(math.max(1e-20f, ea + eb)) * invK;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

            Vertices[index] = new BioForgeMeshVertex
            {
                Position = p,
                Normal = normal,
                Uv = uv,
                Color = new float4(height01, 0f, 0f, 1f)
            };
        }

        private float ComputeCylindricalU(float3 p)
        {
            float cx = (BoundsMin.x + BoundsMax.x) * 0.5f;
            float cz = (BoundsMin.z + BoundsMax.z) * 0.5f;
            float angle = math.atan2(p.z - cz, p.x - cx);
            return angle * 0.15915494309189535f + 0.5f;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BioForgeEdgeCollapseDecimationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BioForgeMeshVertex> SourceVertices;
        [WriteOnly] public NativeArray<BioForgeMeshVertex> OutputVertices;

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
