#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.GeographySanity
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMockSpatialAnomaliesJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* HeightSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* SdfSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialEntityDTO* Entities;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyRuleDTO* Rules;
        [NoAlias, NativeDisableUnsafePtrRestriction] public NavigationRequestDTO* NavigationRequests;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* EntityResults;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* NavigationResults;

        public GeographySectorDTO Sector;
        public int HeightSampleCount;
        public int SdfSampleCount;
        public int EntityCount;
        public int RuleCount;
        public int NavigationRequestCount;

        public void Execute(int index)
        {
            if (index < HeightSampleCount)
                WriteHeight(index);

            if (index < SdfSampleCount)
                WriteSdf(index);

            if (index < EntityCount)
                WriteEntity(index);

            if (index < RuleCount)
                WriteRule(index);

            if (index < NavigationRequestCount)
                WriteNavigationRequest(index);
        }

        private void WriteHeight(int index)
        {
            int width = math.max(2, Sector.HeightResolution);
            int x = index % width;
            int z = index / width;
            float fx = x * math.rcp(width - 1);
            float fz = z * math.rcp(width - 1);
            float ridge = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((fx * 13.0f) + (Sector.WorldSeed & 255u) * 0.01f) * 18f;
            float shelf = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara((fz * 7.0f) + Sector.SectorX * 0.37f) * 11f;
            float bowl = -math.abs((fx - 0.5f) * (fz - 0.5f)) * 80f;
            HeightSamples[index] = -180f + ridge + shelf + bowl;
        }

        private void WriteSdf(int index)
        {
            int rx = math.max(2, Sector.SdfResolutionX);
            int ry = math.max(2, Sector.SdfResolutionY);
            int rz = math.max(2, Sector.SdfResolutionZ);
            int plane = rx * ry;
            int z = index / plane;
            int rem = index - (z * plane);
            int y = rem / rx;
            int x = rem - (y * rx);
            float3 local = new float3(
                x * Sector.SectorSizeMeters * math.rcp(rx - 1),
                Sector.SdfMinYLocalMeters + (y * Sector.SdfSizeYMeters * math.rcp(ry - 1)),
                z * Sector.SectorSizeMeters * math.rcp(rz - 1));

            float height = MockHeightAt(local.xz);
            float floorSdf = local.y - height;
            float3 sphereCenter = new float3(Sector.SectorSizeMeters * 0.5f, -110f, Sector.SectorSizeMeters * 0.5f);
            float sphereRadius = Sector.SectorSizeMeters * 0.18f;
            float3 dp = local - sphereCenter;
            float sphereSdf = Distance(dp) - sphereRadius;
            SdfSamples[index] = math.min(floorSdf, sphereSdf);
        }

        private void WriteEntity(int index)
        {
            uint hash = Mix((uint)index ^ Sector.WorldSeed ^ 0x247u);
            float u = ((hash & 1023u) + 0.5f) * (1f / 1024f);
            float v = (((hash >> 10) & 1023u) + 0.5f) * (1f / 1024f);
            float x = math.lerp(24f, Sector.SectorSizeMeters - 24f, u);
            float z = math.lerp(24f, Sector.SectorSizeMeters - 24f, v);
            float height = MockHeightAt(new float2(x, z));
            float y = height + 0.25f;
            uint typeHash = 0x1000u + (uint)(index & 7);
            uint materialHash = 0xC0FFEE01u;

            int mode = index & 7;
            if (mode == 1)
                y = height + 8.0f + ((index & 15) * 0.15f);
            else if (mode == 2)
                y = height - 6.0f;
            else if (mode == 3)
            {
                x = Sector.SectorSizeMeters * 0.5f;
                z = Sector.SectorSizeMeters * 0.5f;
                y = -110f;
            }
            else if (mode == 4)
            {
                y = -3200f;
                materialHash = 0x474C4153u; // GLAS
            }

            double3 aup = Sector.SectorOriginAup + new double3(x, y, z);
            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + index);
            entity.TargetAUP = aup;
            entity.RadiusMeters = 0.5f + ((index & 3) * 0.25f);
            entity.RequiredClearance = 0.15f;
            entity.MaxFloatingDistance = math.max(0.05f, Sector.MaxFloatingDistance);
            entity.RecoverableEpsilon = 4.0f;
            entity.EntityHash = hash;
            entity.ObjectTypeHash = typeHash;
            entity.HullMaterialHash = materialHash;
            entity.RuleFlags = GeographySanityConstants.RuleCheckFloating |
                               GeographySanityConstants.RuleCheckBuried |
                               GeographySanityConstants.RuleCheckCrushDepth;
            entity.SourceFlags = 1u;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(EntityResults + index);
            result = default;
            result.TargetAUP = aup;
            result.EntityHash = entity.EntityHash;
            result.ObjectTypeHash = entity.ObjectTypeHash;
            result.HullMaterialHash = entity.HullMaterialHash;
            result.SectorX = Sector.SectorX;
            result.SectorZ = Sector.SectorZ;
        }

        private void WriteRule(int index)
        {
            int entityIndex = math.min(index, math.max(0, EntityCount - 1));
            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + entityIndex);
            ref SpatialAnomalyRuleDTO rule = ref UnsafeUtility.AsRef<SpatialAnomalyRuleDTO>(Rules + index);
            rule.TargetAUP = entity.TargetAUP;
            rule.RequiredClearance = entity.RequiredClearance;
            rule.RuleFlags = entity.RuleFlags;
        }

        private void WriteNavigationRequest(int index)
        {
            float z = math.lerp(80f, Sector.SectorSizeMeters - 80f, (index + 1) * math.rcp(math.max(2, NavigationRequestCount + 1)));
            float y = -110f;
            ref NavigationRequestDTO request = ref UnsafeUtility.AsRef<NavigationRequestDTO>(NavigationRequests + index);
            request.StartAUP = Sector.SectorOriginAup + new double3(24f, y, z);
            request.EndAUP = Sector.SectorOriginAup + new double3(Sector.SectorSizeMeters - 24f, y, z);
            request.VehicleRadiusMeters = 8.0f;
            request.RequiredClearance = 2.0f;
            request.RequestHash = Mix((uint)index ^ Sector.WorldSeed ^ 0xBADC0DEu);
            request.RuleFlags = GeographySanityConstants.RuleCheckConnectivity;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(NavigationResults + index);
            result = default;
            result.TargetAUP = request.StartAUP;
            result.EntityHash = request.RequestHash;
            result.RequestHash = request.RequestHash;
            result.SectorX = Sector.SectorX;
            result.SectorZ = Sector.SectorZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float MockHeightAt(float2 xz)
        {
            float fx = math.saturate(xz.x * math.rcp(math.max(1f, Sector.SectorSizeMeters)));
            float fz = math.saturate(xz.y * math.rcp(math.max(1f, Sector.SectorSizeMeters)));
            float ridge = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((fx * 13.0f) + (Sector.WorldSeed & 255u) * 0.01f) * 18f;
            float shelf = Hecton8.Core.MathLodApproximation.ApproxCosBhaskara((fz * 7.0f) + Sector.SectorX * 0.37f) * 11f;
            float bowl = -math.abs((fx - 0.5f) * (fz - 0.5f)) * 80f;
            return -180f + ridge + shelf + bowl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Distance(float3 value)
        {
            float lenSq = math.lengthsq(value);
            return lenSq > 1e-12f ? lenSq * math.rsqrt(lenSq) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }
}
#endif
