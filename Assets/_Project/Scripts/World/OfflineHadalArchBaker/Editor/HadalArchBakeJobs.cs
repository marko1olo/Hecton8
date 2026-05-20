using Hecton8.World.OfflineHadalArchBaker;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockSdfVolumeJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<float> Densities;

        public HadalArchBakeConfigDTO Config;

        public void Execute(int index)
        {
            float3 p = ResolveLocalPosition(index);
            float floor = SdBox(p - new float3(0f, -18f, 0f), new float3(72f, 10f, 72f));
            float arch = SdVerticalTorus(p, math.lerp(18f, 28f, math.saturate(Config.GlobalQualityWeight)), 5.5f);
            arch = math.max(arch, -p.y - 3f);
            float result = math.min(floor, arch);

            float cave0 = math.length(p - new float3(-13f, -5f, 0f)) - 9f;
            float cave1 = math.length(p - new float3(14f, -4f, -7f)) - 8f;
            float cave2 = math.length(p - new float3(0f, 6f, 8f)) - 5f;
            result = math.max(result, -cave0);
            result = math.max(result, -cave1);
            result = math.max(result, -cave2);

            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            ref float density = ref UnsafeUtility.AsRef<float>(ptr + index);
            density = math.isfinite(result) ? result : 1f;
        }

        private float3 ResolveLocalPosition(int index)
        {
            int3 res = SanitizeResolution(Config.Resolution);
            int layer = res.x * res.y;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            float3 center = (new float3(res.x - 1, res.y - 1, res.z - 1) * 0.5f) * math.max(Config.VoxelSize, 0.001f);
            return (new float3(x, y, z) * math.max(Config.VoxelSize, 0.001f)) - center;
        }

        private static int3 SanitizeResolution(int3 value)
        {
            return math.max(value, new int3(2));
        }

        private static float SdVerticalTorus(float3 p, float majorRadius, float minorRadius)
        {
            float2 q = new float2(math.length(p.xy) - math.max(majorRadius, 0.001f), p.z);
            return math.length(q) - math.max(minorRadius, 0.001f);
        }

        private static float SdBox(float3 p, float3 extents)
        {
            float3 q = math.abs(p) - math.max(extents, new float3(0.001f));
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateSdfBooleanGraphJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<float> Densities;

        [ReadOnly]
        [NoAlias]
        public NativeArray<SdfShapeDTO> Shapes;

        public HadalArchBakeConfigDTO Config;

        public void Execute(int index)
        {
            int shapeCount = math.clamp(Config.ShapeCount, 0, Shapes.IsCreated ? Shapes.Length : 0);
            float3 p = ResolveLocalPosition(index);
            float result = 1000000f;

            SdfShapeDTO* shapePtr = shapeCount > 0 ? (SdfShapeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Shapes) : null;
            for (int i = 0; i < shapeCount; i++)
            {
                ref SdfShapeDTO shape = ref UnsafeUtility.AsRef<SdfShapeDTO>(shapePtr + i);
                float sdf = EvaluateShape(p, in shape);
                if (!math.isfinite(sdf))
                    sdf = 1000000f;

                uint op = shape.Operation;
                if (op == (uint)SdfBooleanOperation.Subtract)
                    result = math.max(result, -sdf);
                else if (op == (uint)SdfBooleanOperation.Intersect)
                    result = math.max(result, sdf);
                else if (op == (uint)SdfBooleanOperation.SmoothUnion)
                    result = SmoothUnion(result, sdf, math.max(shape.BlendRadius, 0.001f));
                else
                    result = math.min(result, sdf);
            }

            if (shapeCount == 0)
            {
                float floor = SdBox(p - new float3(0f, -18f, 0f), new float3(72f, 10f, 72f));
                float arch = SdTorus(new float3(p.x, p.z, p.y), new float2(24f, 5f));
                arch = math.max(arch, -p.y - 3f);
                result = math.min(floor, arch);
            }

            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            ref float density = ref UnsafeUtility.AsRef<float>(ptr + index);
            density = math.isfinite(result) ? result : 1f;
        }

        private float3 ResolveLocalPosition(int index)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int layer = res.x * res.y;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            float voxel = math.max(Config.VoxelSize, 0.001f);
            float3 local = (new float3(x, y, z) * voxel) - ((new float3(res.x - 1, res.y - 1, res.z - 1) * 0.5f) * voxel);
            double3 sampleAup = Config.CenterAup + new double3(local.x, local.y, local.z);
            return HadalArchBakeMath.LocalizeAup(sampleAup, Config.CenterAup);
        }

        private static float EvaluateShape(float3 p, in SdfShapeDTO shape)
        {
            float3 local = p - shape.Position;
            if (shape.ShapeType == (uint)SdfShapeType.Box)
                return SdBox(local, shape.Extents);

            if (shape.ShapeType == (uint)SdfShapeType.Torus)
                return SdTorus(local, new float2(math.max(shape.Extents.x, 0.001f), math.max(shape.Extents.y, 0.001f)));

            if (shape.ShapeType == (uint)SdfShapeType.Cylinder)
                return SdCappedCylinderY(local, math.max(shape.Extents.x, 0.001f), math.max(shape.Extents.y, 0.001f));

            return math.length(local) - math.max(shape.Extents.x, 0.001f);
        }

        private static float SdBox(float3 p, float3 extents)
        {
            float3 q = math.abs(p) - math.max(extents, new float3(0.001f));
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
        }

        private static float SdTorus(float3 p, float2 radii)
        {
            float2 q = new float2(math.length(p.xz) - radii.x, p.y);
            return math.length(q) - radii.y;
        }

        private static float SdCappedCylinderY(float3 p, float radius, float halfHeight)
        {
            float2 d = new float2(math.length(p.xz) - radius, math.abs(p.y) - halfHeight);
            return math.min(math.max(d.x, d.y), 0f) + math.length(math.max(d, 0f));
        }

        private static float SmoothUnion(float a, float b, float k)
        {
            float h = math.saturate(0.5f + 0.5f * (b - a) * math.rcp(math.max(k, 0.001f)));
            return math.lerp(b, a, h) - (k * h * (1f - h));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplySdfNoiseDisplacementJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<float> Densities;

        public HadalArchBakeConfigDTO Config;

        public void Execute(int index)
        {
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            ref float density = ref UnsafeUtility.AsRef<float>(ptr + index);
            float d = density;
            float3 local = ResolveLocalPosition(index);
            float3 seedJitter = Config.NoiseSeedJitter;
            float frequency = math.max(Config.NoiseFrequency, 0.0001f);
            float quality = math.saturate(Config.GlobalQualityWeight);
            float n0 = noise.snoise((local + seedJitter) * frequency);
            float n1 = noise.snoise((local * (frequency * 2.03f)) + (seedJitter * 1.37f));
            float ridged = 1f - math.abs(n0 * 0.7f + n1 * 0.3f);
            float signedRidged = (ridged * 2f) - 1f;
            float envelope = math.saturate(1f - (math.abs(d) * math.rcp(math.max(Config.SurfaceBand, Config.VoxelSize * 2f))));
            float displacement = signedRidged * math.max(Config.NoiseAmplitude, 0f) * math.lerp(0.35f, 1f, quality) * envelope;
            float result = d + displacement;
            density = math.isfinite(result) ? result : d;
        }

        private float3 ResolveLocalPosition(int index)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int layer = res.x * res.y;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            float voxel = math.max(Config.VoxelSize, 0.001f);
            float3 local = (new float3(x, y, z) * voxel) - ((new float3(res.x - 1, res.y - 1, res.z - 1) * 0.5f) * voxel);
            double3 sampleAup = Config.CenterAup + new double3(local.x, local.y, local.z);
            return HadalArchBakeMath.LocalizeAup(sampleAup, Config.CenterAup);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SealSdfBoundaryShellJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<float> Densities;

        public HadalArchBakeConfigDTO Config;

        public void Execute(int index)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int3 cell = Unflatten(index, res);
            bool boundary =
                cell.x == 0 || cell.y == 0 || cell.z == 0 ||
                cell.x == res.x - 1 || cell.y == res.y - 1 || cell.z == res.z - 1;
            if (!boundary)
                return;

            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            ref float density = ref UnsafeUtility.AsRef<float>(ptr + index);
            float shellDistance = math.max(Config.VoxelSize * 3f, 0.01f);
            float current = math.isfinite(density) ? density : shellDistance;
            density = math.max(math.abs(current), shellDistance);
        }

        private static int3 Unflatten(int index, int3 res)
        {
            int layer = res.x * res.y;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            return new int3(x, y, z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BakeCavityOcclusionJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<float> Densities;

        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<byte> CavityVisibility;

        public HadalArchBakeConfigDTO Config;

        public void Execute(int index)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int3 cell = Unflatten(index, res);
            float centerDensity = Sample(cell);
            if (math.abs(centerDensity) > math.max(Config.VoxelSize * 2f, 0.001f))
            {
                CavityVisibility[index] = 255;
                return;
            }

            int rayCount = math.clamp(Config.CavityRayCount, 1, 12);
            int steps = math.clamp((int)math.round(math.lerp(2f, 6f, math.saturate(Config.GlobalQualityWeight))), 2, 6);
            float stepCells = math.max(Config.CavityRayDistance, Config.VoxelSize) * math.rcp(math.max(Config.VoxelSize, 0.001f)) * math.rcp(steps);
            int occluded = 0;
            for (int ray = 0; ray < rayCount; ray++)
            {
                float3 dir = ResolveDirection(ray);
                bool hit = false;
                for (int step = 1; step <= steps; step++)
                {
                    float3 sample = new float3(cell.x, cell.y, cell.z) + (dir * (stepCells * step));
                    if (SampleRounded(sample) < 0f)
                    {
                        hit = true;
                        break;
                    }
                }

                occluded += hit ? 1 : 0;
            }

            float visibility = 1f - (occluded * math.rcp(rayCount));
            CavityVisibility[index] = (byte)math.clamp((int)math.round(math.saturate(visibility) * 255f), 0, 255);
        }

        private static int3 Unflatten(int index, int3 res)
        {
            int layer = res.x * res.y;
            int z = index / math.max(layer, 1);
            int rem = index - (z * layer);
            int y = rem / res.x;
            int x = rem - (y * res.x);
            return new int3(x, y, z);
        }

        private float SampleRounded(float3 cell)
        {
            return Sample(new int3((int)math.round(cell.x), (int)math.round(cell.y), (int)math.round(cell.z)));
        }

        private float Sample(int3 cell)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int x = math.clamp(cell.x, 0, res.x - 1);
            int y = math.clamp(cell.y, 0, res.y - 1);
            int z = math.clamp(cell.z, 0, res.z - 1);
            int idx = x + (res.x * (y + (res.y * z)));
            return (uint)idx < (uint)Densities.Length ? Densities[idx] : 1f;
        }

        private static float3 ResolveDirection(int index)
        {
            switch (index % 12)
            {
                case 0: return math.normalize(new float3(1f, 0f, 0f));
                case 1: return math.normalize(new float3(-1f, 0f, 0f));
                case 2: return math.normalize(new float3(0f, 1f, 0f));
                case 3: return math.normalize(new float3(0f, -1f, 0f));
                case 4: return math.normalize(new float3(0f, 0f, 1f));
                case 5: return math.normalize(new float3(0f, 0f, -1f));
                case 6: return math.normalize(new float3(1f, 1f, 0f));
                case 7: return math.normalize(new float3(-1f, 1f, 0f));
                case 8: return math.normalize(new float3(1f, 0f, 1f));
                case 9: return math.normalize(new float3(-1f, 0f, 1f));
                case 10: return math.normalize(new float3(0f, 1f, 1f));
                default: return math.normalize(new float3(0f, -1f, 1f));
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ExtractArchMeshJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<float> Densities;

        [ReadOnly]
        [NoAlias]
        public NativeArray<byte> CavityVisibility;

        [NoAlias]
        public NativeList<HadalArchVertexDTO> Vertices;

        [NoAlias]
        public NativeList<int> Indices;

        public HadalArchBakeConfigDTO Config;

        public void Execute()
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            for (int z = 0; z < res.z - 1; z++)
            {
                for (int y = 0; y < res.y - 1; y++)
                {
                    for (int x = 0; x < res.x - 1; x++)
                    {
                        PolygonizeCell(new int3(x, y, z));
                    }
                }
            }
        }

        private void PolygonizeCell(int3 cell)
        {
            float4 d0 = default;
            float4 d1 = default;
            float3 p0 = default;
            float3 p1 = default;
            float3 p2 = default;
            float3 p3 = default;
            for (int tetra = 0; tetra < 6; tetra++)
            {
                int c0 = ResolveTetraCorner(tetra, 0);
                int c1 = ResolveTetraCorner(tetra, 1);
                int c2 = ResolveTetraCorner(tetra, 2);
                int c3 = ResolveTetraCorner(tetra, 3);
                int3 g0 = cell + ResolveCubeCorner(c0);
                int3 g1 = cell + ResolveCubeCorner(c1);
                int3 g2 = cell + ResolveCubeCorner(c2);
                int3 g3 = cell + ResolveCubeCorner(c3);
                d0 = new float4(Sample(g0), Sample(g1), Sample(g2), Sample(g3));
                p0 = GridToLocal(g0);
                p1 = GridToLocal(g1);
                p2 = GridToLocal(g2);
                p3 = GridToLocal(g3);
                PolygonizeTetra(g0, g1, g2, g3, p0, p1, p2, p3, d0);
            }
        }

        private void PolygonizeTetra(
            int3 g0,
            int3 g1,
            int3 g2,
            int3 g3,
            float3 p0,
            float3 p1,
            float3 p2,
            float3 p3,
            float4 density)
        {
            int inside0 = density.x < 0f ? 1 : 0;
            int inside1 = density.y < 0f ? 1 : 0;
            int inside2 = density.z < 0f ? 1 : 0;
            int inside3 = density.w < 0f ? 1 : 0;
            int insideCount = inside0 + inside1 + inside2 + inside3;
            if (insideCount == 0 || insideCount == 4)
                return;

            int4 inside = new int4(-1);
            int4 outside = new int4(-1);
            int inCount = 0;
            int outCount = 0;
            AppendClassified(0, inside0 != 0, ref inside, ref inCount, ref outside, ref outCount);
            AppendClassified(1, inside1 != 0, ref inside, ref inCount, ref outside, ref outCount);
            AppendClassified(2, inside2 != 0, ref inside, ref inCount, ref outside, ref outCount);
            AppendClassified(3, inside3 != 0, ref inside, ref inCount, ref outside, ref outCount);

            if (insideCount == 1)
            {
                EmitTriangle(
                    Interpolate(inside.x, outside.x, g0, g1, g2, g3, p0, p1, p2, p3, density),
                    Interpolate(inside.x, outside.y, g0, g1, g2, g3, p0, p1, p2, p3, density),
                    Interpolate(inside.x, outside.z, g0, g1, g2, g3, p0, p1, p2, p3, density));
                return;
            }

            if (insideCount == 3)
            {
                EmitTriangle(
                    Interpolate(outside.x, inside.z, g0, g1, g2, g3, p0, p1, p2, p3, density),
                    Interpolate(outside.x, inside.y, g0, g1, g2, g3, p0, p1, p2, p3, density),
                    Interpolate(outside.x, inside.x, g0, g1, g2, g3, p0, p1, p2, p3, density));
                return;
            }

            HadalArchVertexDTO a = Interpolate(inside.x, outside.x, g0, g1, g2, g3, p0, p1, p2, p3, density);
            HadalArchVertexDTO b = Interpolate(inside.x, outside.y, g0, g1, g2, g3, p0, p1, p2, p3, density);
            HadalArchVertexDTO c = Interpolate(inside.y, outside.x, g0, g1, g2, g3, p0, p1, p2, p3, density);
            HadalArchVertexDTO d = Interpolate(inside.y, outside.y, g0, g1, g2, g3, p0, p1, p2, p3, density);
            EmitTriangle(a, b, c);
            EmitTriangle(c, b, d);
        }

        private void EmitTriangle(HadalArchVertexDTO a, HadalArchVertexDTO b, HadalArchVertexDTO c)
        {
            if (Vertices.Length + 3 > Vertices.Capacity || Indices.Length + 3 > Indices.Capacity)
                return;

            float3 face = math.cross(b.Position - a.Position, c.Position - a.Position);
            float faceAreaSq = math.dot(face, face);
            if (!math.isfinite(faceAreaSq) || faceAreaSq <= 0.00000001f)
                return;

            float3 avgNormal = math.normalizesafe(a.Normal + b.Normal + c.Normal, new float3(0f, 1f, 0f));
            bool flip = math.dot(face, avgNormal) < 0f;
            int baseIndex = Vertices.Length;
            Vertices.Add(a);
            Vertices.Add(flip ? c : b);
            Vertices.Add(flip ? b : c);
            Indices.Add(baseIndex);
            Indices.Add(baseIndex + 1);
            Indices.Add(baseIndex + 2);
        }

        private HadalArchVertexDTO Interpolate(
            int a,
            int b,
            int3 g0,
            int3 g1,
            int3 g2,
            int3 g3,
            float3 p0,
            float3 p1,
            float3 p2,
            float3 p3,
            float4 density)
        {
            float da = SelectDensity(a, density);
            float db = SelectDensity(b, density);
            float denom = math.max(math.abs(da - db), 0.000001f);
            float t = math.clamp(math.abs(da) * math.rcp(denom), 0.001f, 0.999f);
            float3 p = math.lerp(SelectPosition(a, p0, p1, p2, p3), SelectPosition(b, p0, p1, p2, p3), t);
            int3 grid = SelectGrid(math.select(a, b, t > 0.5f), g0, g1, g2, g3);
            float3 normal = CalculateNormal(p);
            float3 tangent = ResolveTangent(normal);
            byte cavity = SampleCavity(grid);
            byte material = (byte)math.clamp((int)math.round(math.saturate(Config.GlobalQualityWeight) * 255f), 0, 255);
            return new HadalArchVertexDTO
            {
                Position = math.all(math.isfinite(p)) ? p : float3.zero,
                Normal = normal,
                Tangent = new float4(tangent, 1f),
                Uv0 = new float2(p.x, p.z) * 0.05f,
                PackedColor = HadalArchBakeMath.PackColor(cavity, material, 0, 255),
                Uv3AupLocal = p
            };
        }

        private static void AppendClassified(
            int value,
            bool isInside,
            ref int4 inside,
            ref int insideCount,
            ref int4 outside,
            ref int outsideCount)
        {
            if (isInside)
            {
                inside[insideCount] = value;
                insideCount++;
            }
            else
            {
                outside[outsideCount] = value;
                outsideCount++;
            }
        }

        private float3 CalculateNormal(float3 local)
        {
            float voxel = math.max(Config.VoxelSize, 0.001f);
            float eps = voxel;
            float dx = SampleLocal(local + new float3(eps, 0f, 0f)) - SampleLocal(local - new float3(eps, 0f, 0f));
            float dy = SampleLocal(local + new float3(0f, eps, 0f)) - SampleLocal(local - new float3(0f, eps, 0f));
            float dz = SampleLocal(local + new float3(0f, 0f, eps)) - SampleLocal(local - new float3(0f, 0f, eps));
            return math.normalizesafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        private float SampleLocal(float3 local)
        {
            float voxel = math.max(Config.VoxelSize, 0.001f);
            float3 center = (new float3(Config.Resolution.x - 1, Config.Resolution.y - 1, Config.Resolution.z - 1) * 0.5f) * voxel;
            int3 grid = (int3)math.round((local + center) * math.rcp(voxel));
            return Sample(grid);
        }

        private float Sample(int3 grid)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int x = math.clamp(grid.x, 0, res.x - 1);
            int y = math.clamp(grid.y, 0, res.y - 1);
            int z = math.clamp(grid.z, 0, res.z - 1);
            int index = x + (res.x * (y + (res.y * z)));
            return (uint)index < (uint)Densities.Length ? Densities[index] : 1f;
        }

        private byte SampleCavity(int3 grid)
        {
            int3 res = math.max(Config.Resolution, new int3(2));
            int x = math.clamp(grid.x, 0, res.x - 1);
            int y = math.clamp(grid.y, 0, res.y - 1);
            int z = math.clamp(grid.z, 0, res.z - 1);
            int index = x + (res.x * (y + (res.y * z)));
            return (uint)index < (uint)CavityVisibility.Length ? CavityVisibility[index] : (byte)255;
        }

        private float3 GridToLocal(int3 grid)
        {
            float voxel = math.max(Config.VoxelSize, 0.001f);
            return (new float3(grid.x, grid.y, grid.z) * voxel) - ((new float3(Config.Resolution.x - 1, Config.Resolution.y - 1, Config.Resolution.z - 1) * 0.5f) * voxel);
        }

        private static int3 ResolveCubeCorner(int corner)
        {
            switch (corner)
            {
                case 1: return new int3(1, 0, 0);
                case 2: return new int3(1, 1, 0);
                case 3: return new int3(0, 1, 0);
                case 4: return new int3(0, 0, 1);
                case 5: return new int3(1, 0, 1);
                case 6: return new int3(1, 1, 1);
                case 7: return new int3(0, 1, 1);
                default: return int3.zero;
            }
        }

        private static int ResolveTetraCorner(int tetra, int local)
        {
            switch ((tetra * 4) + local)
            {
                case 0: return 0;
                case 1: return 5;
                case 2: return 1;
                case 3: return 6;
                case 4: return 0;
                case 5: return 1;
                case 6: return 2;
                case 7: return 6;
                case 8: return 0;
                case 9: return 2;
                case 10: return 3;
                case 11: return 6;
                case 12: return 0;
                case 13: return 3;
                case 14: return 7;
                case 15: return 6;
                case 16: return 0;
                case 17: return 7;
                case 18: return 4;
                case 19: return 6;
                case 20: return 0;
                case 21: return 4;
                case 22: return 5;
                default: return 6;
            }
        }

        private static float SelectDensity(int index, float4 density)
        {
            return index == 0 ? density.x : index == 1 ? density.y : index == 2 ? density.z : density.w;
        }

        private static float3 SelectPosition(int index, float3 p0, float3 p1, float3 p2, float3 p3)
        {
            return index == 0 ? p0 : index == 1 ? p1 : index == 2 ? p2 : p3;
        }

        private static int3 SelectGrid(int index, int3 g0, int3 g1, int3 g2, int3 g3)
        {
            return index == 0 ? g0 : index == 1 ? g1 : index == 2 ? g2 : g3;
        }

        private static float3 ResolveTangent(float3 normal)
        {
            float3 helper = math.abs(normal.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            return math.normalizesafe(math.cross(helper, normal), new float3(1f, 0f, 0f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DeterministicLodDecimationJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<HadalArchVertexDTO> SourceVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> SourceIndices;

        [NoAlias]
        public NativeList<HadalArchVertexDTO> OutputVertices;

        [NoAlias]
        public NativeList<int> OutputIndices;

        public float KeepRatio;
        public float CollapseWeight;
        public uint Seed;

        public void Execute()
        {
            int triangleCount = SourceIndices.Length / 3;
            float keepRatio = math.saturate(KeepRatio);
            for (int tri = 0; tri < triangleCount; tri++)
            {
                if (!ShouldKeep(tri, keepRatio))
                    continue;

                int i0 = SourceIndices[tri * 3];
                int i1 = SourceIndices[tri * 3 + 1];
                int i2 = SourceIndices[tri * 3 + 2];
                if ((uint)i0 >= (uint)SourceVertices.Length || (uint)i1 >= (uint)SourceVertices.Length || (uint)i2 >= (uint)SourceVertices.Length)
                    continue;

                if (OutputVertices.Length + 3 > OutputVertices.Capacity || OutputIndices.Length + 3 > OutputIndices.Capacity)
                    return;

                HadalArchVertexDTO v0 = SourceVertices[i0];
                HadalArchVertexDTO v1 = SourceVertices[i1];
                HadalArchVertexDTO v2 = SourceVertices[i2];
                float3 centroid = (v0.Position + v1.Position + v2.Position) * 0.33333334f;
                float collapse = math.saturate(CollapseWeight);
                v0.Position = math.lerp(v0.Position, centroid, collapse);
                v1.Position = math.lerp(v1.Position, centroid, collapse);
                v2.Position = math.lerp(v2.Position, centroid, collapse);
                v0.Uv3AupLocal = v0.Position;
                v1.Uv3AupLocal = v1.Position;
                v2.Uv3AupLocal = v2.Position;

                int baseIndex = OutputVertices.Length;
                OutputVertices.Add(v0);
                OutputVertices.Add(v1);
                OutputVertices.Add(v2);
                OutputIndices.Add(baseIndex);
                OutputIndices.Add(baseIndex + 1);
                OutputIndices.Add(baseIndex + 2);
            }
        }

        private bool ShouldKeep(int triangleIndex, float keepRatio)
        {
            if (keepRatio >= 0.999f)
                return true;

            uint h = HadalArchBakeMath.Mix((uint)triangleIndex ^ Seed);
            float threshold = (h & 65535u) * (1f / 65535f);
            return threshold <= keepRatio;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WeldArchMeshJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<HadalArchVertexDTO> SourceVertices;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> SourceIndices;

        [NoAlias]
        public NativeList<HadalArchVertexDTO> OutputVertices;

        [NoAlias]
        public NativeList<int> OutputIndices;

        public NativeParallelHashMap<ulong, int> VertexLookup;
        public HadalArchBakeConfigDTO Config;

        public void Execute()
        {
            float weldScale = math.rcp(math.max(Config.VoxelSize * 0.015f, 0.0001f));
            int indexCount = SourceIndices.Length - (SourceIndices.Length % 3);
            for (int i = 0; i < indexCount; i += 3)
            {
                if (OutputIndices.Length + 3 > OutputIndices.Capacity)
                    return;

                if (!TryResolveVertexIndex(SourceIndices[i], weldScale, out int i0))
                    continue;

                if (!TryResolveVertexIndex(SourceIndices[i + 1], weldScale, out int i1))
                    continue;

                if (!TryResolveVertexIndex(SourceIndices[i + 2], weldScale, out int i2))
                    continue;

                OutputIndices.Add(i0);
                OutputIndices.Add(i1);
                OutputIndices.Add(i2);
            }
        }

        private bool TryResolveVertexIndex(int sourceIndex, float weldScale, out int weldedIndex)
        {
            weldedIndex = -1;
            if ((uint)sourceIndex >= (uint)SourceVertices.Length)
                return false;

            HadalArchVertexDTO vertex = SourceVertices[sourceIndex];
            if (!HadalArchBakeMath.IsFinite(vertex.Position))
                return false;

            ulong key = QuantizePosition(vertex.Position, weldScale);
            if (VertexLookup.TryGetValue(key, out weldedIndex))
                return true;

            if (OutputVertices.Length >= OutputVertices.Capacity)
                return false;

            weldedIndex = OutputVertices.Length;
            OutputVertices.Add(vertex);
            VertexLookup.TryAdd(key, weldedIndex);
            return true;
        }

        private static ulong QuantizePosition(float3 position, float scale)
        {
            int3 q = (int3)math.round(position * scale);
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (uint)q.x);
            hash = Mix(hash, (uint)q.y);
            hash = Mix(hash, (uint)q.z);
            return hash == 0UL ? 1UL : hash;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct HadalSdfPreviewRaymarchJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<SdfShapeDTO> Shapes;

        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<float3> HitPositions;

        [NativeDisableParallelForRestriction]
        [NoAlias]
        public NativeArray<byte> HitFlags;

        public int2 Grid;
        public int ShapeCount;
        public float3 BoundsExtents;
        public int Steps;

        public void Execute(int index)
        {
            int2 grid = math.max(Grid, new int2(1));
            int x = index % grid.x;
            int y = index / grid.x;
            float u = grid.x <= 1 ? 0.5f : x * math.rcp(grid.x - 1f);
            float v = grid.y <= 1 ? 0.5f : y * math.rcp(grid.y - 1f);
            float3 ext = math.max(BoundsExtents, new float3(1f));
            float3 origin = new float3(math.lerp(-ext.x, ext.x, u), math.lerp(-ext.y, ext.y, v), -ext.z);
            float3 dir = new float3(0f, 0f, 1f);
            int stepCount = math.clamp(Steps, 4, 128);
            float stepSize = (ext.z * 2f) * math.rcp(stepCount);
            float previous = Evaluate(origin);
            for (int step = 1; step <= stepCount; step++)
            {
                float3 p = origin + (dir * (stepSize * step));
                float current = Evaluate(p);
                if ((previous < 0f && current >= 0f) || (previous >= 0f && current < 0f))
                {
                    float denom = math.max(math.abs(previous - current), 0.000001f);
                    float t = math.saturate(math.abs(previous) * math.rcp(denom));
                    HitPositions[index] = math.lerp(p - (dir * stepSize), p, t);
                    HitFlags[index] = 1;
                    return;
                }

                previous = current;
            }

            HitPositions[index] = float3.zero;
            HitFlags[index] = 0;
        }

        private float Evaluate(float3 p)
        {
            int count = math.clamp(ShapeCount, 0, Shapes.IsCreated ? Shapes.Length : 0);
            SdfShapeDTO* shapePtr = count > 0 ? (SdfShapeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Shapes) : null;
            float result = 1000000f;
            for (int i = 0; i < count; i++)
            {
                ref SdfShapeDTO shape = ref UnsafeUtility.AsRef<SdfShapeDTO>(shapePtr + i);
                float sdf = EvaluateShape(p, in shape);
                if (shape.Operation == (uint)SdfBooleanOperation.Subtract)
                    result = math.max(result, -sdf);
                else if (shape.Operation == (uint)SdfBooleanOperation.Intersect)
                    result = math.max(result, sdf);
                else if (shape.Operation == (uint)SdfBooleanOperation.SmoothUnion)
                    result = SmoothUnion(result, sdf, math.max(shape.BlendRadius, 0.001f));
                else
                    result = math.min(result, sdf);
            }

            return result;
        }

        private static float EvaluateShape(float3 p, in SdfShapeDTO shape)
        {
            float3 local = p - shape.Position;
            if (shape.ShapeType == (uint)SdfShapeType.Box)
                return SdBox(local, shape.Extents);

            if (shape.ShapeType == (uint)SdfShapeType.Torus)
                return SdTorus(local, new float2(math.max(shape.Extents.x, 0.001f), math.max(shape.Extents.y, 0.001f)));

            if (shape.ShapeType == (uint)SdfShapeType.Cylinder)
                return SdCappedCylinderY(local, math.max(shape.Extents.x, 0.001f), math.max(shape.Extents.y, 0.001f));

            return math.length(local) - math.max(shape.Extents.x, 0.001f);
        }

        private static float SdBox(float3 p, float3 extents)
        {
            float3 q = math.abs(p) - math.max(extents, new float3(0.001f));
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
        }

        private static float SdTorus(float3 p, float2 radii)
        {
            float2 q = new float2(math.length(p.xz) - radii.x, p.y);
            return math.length(q) - radii.y;
        }

        private static float SdCappedCylinderY(float3 p, float radius, float halfHeight)
        {
            float2 d = new float2(math.length(p.xz) - radius, math.abs(p.y) - halfHeight);
            return math.min(math.max(d.x, d.y), 0f) + math.length(math.max(d, 0f));
        }

        private static float SmoothUnion(float a, float b, float k)
        {
            float h = math.saturate(0.5f + 0.5f * (b - a) * math.rcp(math.max(k, 0.001f)));
            return math.lerp(b, a, h) - (k * h * (1f - h));
        }
    }
}
