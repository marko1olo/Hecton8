using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI
{
    /// <summary>
    /// GPU-visible point emitted by the SHINOBU topographical sonar path.
    /// Layout is fixed by the batch prompt: 12-byte local position + packed RGBA8.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SonarPointDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public uint ColorPacked;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct TopographicalSonarTelemetryEntry
    {
        [FieldOffset(0)] public double TimeSeconds;
        [FieldOffset(8)] public double PingAupX;
        [FieldOffset(16)] public double PingAupY;
        [FieldOffset(24)] public double PingAupZ;
        [FieldOffset(32)] public double CameraAupX;
        [FieldOffset(40)] public double CameraAupY;
        [FieldOffset(48)] public double CameraAupZ;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public int Sequence;
        [FieldOffset(64)] public int RequestedRayCount;
        [FieldOffset(68)] public int ActivePointCount;
        [FieldOffset(72)] public int HitCount;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public float GlobalQualityWeight;
        [FieldOffset(84)] public float MaxDistanceMeters;
        [FieldOffset(88)] public float3 PingOriginCameraLocal;
        [FieldOffset(100)] public float3 SdfOriginRuntime;
        [FieldOffset(112)] public float SdfRangeMeters;
        [FieldOffset(116)] public float StepMeters;
        [FieldOffset(120)] public uint SdfVersion;
        [FieldOffset(124)] public uint ComputeTimeMicroseconds;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SonarProceduralArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TopographicalSonarShaderGlobalsDTO
    {
        [FieldOffset(0)] public float4 CameraRuntimeAndPointSize;
        [FieldOffset(16)] public float4 PingSignal;
        [FieldOffset(32)] public float4 RenderParams0;
        [FieldOffset(48)] public float4 RenderParams1;
    }

    public static class TopographicalSonarBufferIds
    {
        public const BufferID Points = (BufferID)70840;
        public const BufferID HitMask = (BufferID)70841;
        public const BufferID Counters = (BufferID)70842;
        public const BufferID MockSdf = (BufferID)70843;
        public const BufferID MockMaterialIds = (BufferID)70844;
        public const BufferID TelemetryRing = (BufferID)70845;
        public const BufferID TelemetryCursor = (BufferID)70846;
        public const BufferID MaterialColorLut = (BufferID)70847;
        public const BufferID CsvScratch = (BufferID)70848;
        public const BufferID IndirectArgs = (BufferID)70849;
        public const BufferID ShaderGlobals = (BufferID)70850;
    }

    public static class TopographicalSonarConstants
    {
        public const int MinRays = 2000;
        public const int MaxRays = 50000;
        public const int TelemetryFrames = 300;
        public const int CounterCount = 8;
        public const int ColorLutEntries = 256;
        public const int CsvScratchBytes = 16 * 1024;
        public const int MockGridSide = 64;
        public const int MockVoxelCount = MockGridSide * MockGridSide * MockGridSide;
        public const float DefaultMaxDistanceMeters = 120f;
        public const float DefaultStepMeters = 0.85f;
        public const float MinimumStepMeters = 0.18f;
        public const float MinimumPingIntervalSeconds = 0.016666668f;
        public const float MaximumPingIntervalSeconds = 0.2f;
        public const double MaxTelemetryLocalMeters = 1000000d;
        public const uint UsedPublishedSdfFlag = 1u << 0;
        public const uint UsedMockSdfFlag = 1u << 1;
        public const uint GpuUploadFlag = 1u << 2;
        public const uint PingEventFlag = 1u << 3;
        public const uint CsvColorFlag = 1u << 4;
        public const uint FaultFlag = 1u << 31;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockSdfJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<byte> EncodedSdf;
        [NoAlias] public NativeArray<byte> MaterialIds;
        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float3 MockCenter;
        public float SdfRange;
        public float QualityWeight;
        public uint Seed;

        public void Execute(int index)
        {
            if (!EncodedSdf.IsCreated ||
                !MaterialIds.IsCreated ||
                index < 0 ||
                index >= EncodedSdf.Length ||
                index >= MaterialIds.Length ||
                GridDimensions.x <= 1 ||
                GridDimensions.y <= 1 ||
                GridDimensions.z <= 1 ||
                SdfRange <= 0.0001f)
            {
                return;
            }

            int x = index % GridDimensions.x;
            int yz = index / GridDimensions.x;
            int y = yz % GridDimensions.y;
            int z = yz / GridDimensions.y;
            float3 p = VolumeOrigin + new float3(x * CellSize.x, y * CellSize.y, z * CellSize.z);
            float3 local = p - MockCenter;
            float radial = ApproxMagnitude(local);
            float angle = MathLodApproximation.ApproxAtan2Fast(local.z, local.x);
            float ridge = MathLodApproximation.ApproxSinBhaskara(angle * 7.0f + Seed * 0.00013f) * 4.5f +
                          MathLodApproximation.ApproxSinBhaskara((local.y + local.x) * 0.091f) * 2.0f;
            float caveRadius = math.lerp(42f, 74f, math.saturate(QualityWeight)) + ridge;
            float shell = radial - caveRadius;

            float2 pillarA = local.xz - new float2(18f, -22f);
            float2 pillarB = local.xz - new float2(-28f, 16f);
            float pillar0 = 6.0f - ApproxMagnitude(new float3(pillarA.x, 0f, pillarA.y));
            float pillar1 = 4.0f - ApproxMagnitude(new float3(pillarB.x, 0f, pillarB.y));
            float floorNoise = MathLodApproximation.ApproxSinBhaskara(local.x * 0.12f + local.z * 0.071f) * 2.75f;
            float floor = -(local.y + 18f + floorNoise);
            float ceiling = local.y - 38f + MathLodApproximation.ApproxSinBhaskara(local.x * 0.05f) * 3.0f;
            float signedDistance = math.max(math.max(shell, math.max(pillar0, pillar1)), math.max(floor, ceiling));
            signedDistance = math.clamp(signedDistance, -SdfRange, SdfRange);

            float encoded = math.saturate(signedDistance * math.rcp(SdfRange) * 0.5f + 0.5f) * 255f;
            EncodedSdf[index] = (byte)math.clamp((int)(encoded + 0.5f), 0, 255);

            byte material = 1;
            float oreMask = math.frac(MathLodApproximation.ApproxSinBhaskara(math.dot(local, new float3(12.9898f, 78.233f, 37.719f)) + Seed) * 43758.5453f);
            if (oreMask > math.lerp(0.965f, 0.91f, math.saturate(QualityWeight)) && signedDistance > -1.5f)
                material = 2;
            if (local.y < -24f && signedDistance > -2.0f)
                material = 3;
            MaterialIds[index] = material;
        }

        private static float ApproxMagnitude(float3 value)
        {
            float3 axis = math.abs(value);
            float maxAxis = math.cmax(axis);
            float minAxis = math.cmin(axis);
            float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SonarRaymarchJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly MaterialIds;
        [ReadOnly, NoAlias] public NativeArray<uint> MaterialColorLut;
        [NoAlias] public NativeArray<SonarPointDTO> Points;
        [NoAlias] public NativeArray<byte> HitMask;

        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public int RayCount;
        public int MaxSteps;
        public float3 PingRuntime;
        public float MaxDistanceMeters;
        public float StepMeters;
        public float QualityWeight;
        public float Intensity01;
        public uint SequenceSeed;

        public void Execute(int index)
        {
            if (index < 0 || index >= RayCount || index >= Points.Length || index >= HitMask.Length || !HasValidPayload())
            {
                WriteMiss(index);
                return;
            }

            float3 direction = ResolveFibonacciDirection(index, RayCount, SequenceSeed);
            float step = math.max(TopographicalSonarConstants.MinimumStepMeters, StepMeters);
            int maxSteps = math.min(MaxSteps, (int)math.ceil(MaxDistanceMeters * math.rcp(step)) + 1);
            if (maxSteps <= 1)
            {
                ExecuteSingleLookup(index, direction, step);
                return;
            }

            float previousDistance = 0f;
            float previousSignedDistance = SampleSignedDistance(PingRuntime, out _);
            bool hasPrevious = math.isfinite(previousSignedDistance);
            for (int stepIndex = 1; stepIndex <= maxSteps; stepIndex++)
            {
                float distance = math.min(MaxDistanceMeters, stepIndex * step);
                float3 samplePosition = PingRuntime + direction * distance;
                float signedDistance = SampleSignedDistance(samplePosition, out byte materialId);
                if (!math.isfinite(signedDistance))
                    break;

                if (signedDistance >= 0f && (!hasPrevious || previousSignedDistance < 0f))
                {
                    float denom = math.max(0.0001f, signedDistance - previousSignedDistance);
                    float t = hasPrevious ? math.saturate(-previousSignedDistance * math.rcp(denom)) : 0f;
                    float resolvedDistance = math.lerp(previousDistance, distance, t);
                    if (!math.isfinite(resolvedDistance) || !math.all(math.isfinite(direction)))
                    {
                        WriteMiss(index);
                        return;
                    }

                    float distance01 = math.saturate(resolvedDistance * math.rcp(math.max(0.0001f, MaxDistanceMeters)));
                    uint packed = ResolvePackedColor(materialId, distance01, math.saturate(signedDistance * math.rcp(math.max(0.0001f, SdfRange))));
                    Points[index] = new SonarPointDTO
                    {
                        LocalPosition = direction * resolvedDistance,
                        ColorPacked = packed
                    };
                    HitMask[index] = 1;
                    return;
                }

                previousDistance = distance;
                previousSignedDistance = signedDistance;
                hasPrevious = true;
            }

            WriteMiss(index);
        }

        private void ExecuteSingleLookup(int index, float3 direction, float step)
        {
            float shell01 = ResolveSingleLookupDistance01(index, SequenceSeed);
            float minDistance = math.min(MaxDistanceMeters, math.max(step, MaxDistanceMeters * 0.08f));
            float distance = math.lerp(minDistance, MaxDistanceMeters, shell01);
            float signedDistance = SampleSignedDistance(PingRuntime + direction * distance, out byte materialId);
            if (!math.isfinite(signedDistance))
            {
                WriteMiss(index);
                return;
            }

            float acceptance = math.max(step, SdfRange * 0.25f);
            if (math.abs(signedDistance) > acceptance)
            {
                WriteMiss(index);
                return;
            }

            float resolvedDistance = math.clamp(distance - signedDistance, 0f, MaxDistanceMeters);
            float distance01 = math.saturate(resolvedDistance * math.rcp(math.max(0.0001f, MaxDistanceMeters)));
            uint packed = ResolvePackedColor(materialId, distance01, 1f - math.saturate(math.abs(signedDistance) * math.rcp(math.max(0.0001f, acceptance))));
            Points[index] = new SonarPointDTO
            {
                LocalPosition = direction * resolvedDistance,
                ColorPacked = packed
            };
            HitMask[index] = 1;
        }

        private bool HasValidPayload()
        {
            return EncodedSdf.IsCreated &&
                   MaterialIds.IsCreated &&
                   Points.IsCreated &&
                   HitMask.IsCreated &&
                   GridDimensions.x > 1 &&
                   GridDimensions.y > 1 &&
                   GridDimensions.z > 1 &&
                   EncodedSdf.Length >= GridDimensions.x * GridDimensions.y * GridDimensions.z &&
                   MaterialIds.Length >= EncodedSdf.Length &&
                   math.all(CellSize > new float3(0.0001f)) &&
                   math.isfinite(SdfRange) &&
                   SdfRange > 0.0001f &&
                   math.isfinite(MaxDistanceMeters) &&
                   MaxDistanceMeters > 0.0001f &&
                   math.isfinite(StepMeters) &&
                   StepMeters > 0.0001f;
        }

        private float SampleSignedDistance(float3 runtimePosition, out byte materialId)
        {
            materialId = 0;
            float3 grid = (runtimePosition - VolumeOrigin) * math.rcp(CellSize);
            if (grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                grid.x > GridDimensions.x - 1f ||
                grid.y > GridDimensions.y - 1f ||
                grid.z > GridDimensions.z - 1f)
            {
                return float.NaN;
            }

            grid = math.clamp(grid, float3.zero, new float3(GridDimensions.x - 1.001f, GridDimensions.y - 1.001f, GridDimensions.z - 1.001f));
            int x0 = (int)math.floor(grid.x);
            int y0 = (int)math.floor(grid.y);
            int z0 = (int)math.floor(grid.z);
            int x1 = math.min(x0 + 1, GridDimensions.x - 1);
            int y1 = math.min(y0 + 1, GridDimensions.y - 1);
            int z1 = math.min(z0 + 1, GridDimensions.z - 1);
            float tx = grid.x - x0;
            float ty = grid.y - y0;
            float tz = grid.z - z0;

            float c000 = DecodeAt(x0, y0, z0);
            float c100 = DecodeAt(x1, y0, z0);
            float c010 = DecodeAt(x0, y1, z0);
            float c110 = DecodeAt(x1, y1, z0);
            float c001 = DecodeAt(x0, y0, z1);
            float c101 = DecodeAt(x1, y0, z1);
            float c011 = DecodeAt(x0, y1, z1);
            float c111 = DecodeAt(x1, y1, z1);
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);

            int nx = math.clamp((int)math.round(grid.x), 0, GridDimensions.x - 1);
            int ny = math.clamp((int)math.round(grid.y), 0, GridDimensions.y - 1);
            int nz = math.clamp((int)math.round(grid.z), 0, GridDimensions.z - 1);
            int materialIndex = nx + GridDimensions.x * (ny + GridDimensions.y * nz);
            materialId = (uint)materialIndex < (uint)MaterialIds.Length ? MaterialIds[materialIndex] : (byte)0;
            float nearest = DecodeAt(nx, ny, nz);
            float trilinear = math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
            return math.lerp(nearest, trilinear, ResolveWorkCurve(QualityWeight));
        }

        private static float ResolveWorkCurve(float quality)
        {
            float t = math.saturate((math.saturate(quality) - 0.1f) * math.rcp(0.9f));
            return t * t * (3f - 2f * t);
        }

        private float DecodeAt(int x, int y, int z)
        {
            int index = x + GridDimensions.x * (y + GridDimensions.y * z);
            if ((uint)index >= (uint)EncodedSdf.Length)
                return -SdfRange;

            return (EncodedSdf[index] * math.rcp(255f) * 2f - 1f) * SdfRange;
        }

        private uint ResolvePackedColor(byte materialId, float distance01, float density01)
        {
            uint baseColor = 0u;
            if (MaterialColorLut.IsCreated && materialId < MaterialColorLut.Length)
                baseColor = MaterialColorLut[materialId];
            if (baseColor == 0u)
                baseColor = ResolveDefaultPackedColor(materialId);

            uint r = baseColor & 0xFFu;
            uint g = (baseColor >> 8) & 0xFFu;
            uint b = (baseColor >> 16) & 0xFFu;
            uint a = (baseColor >> 24) & 0xFFu;
            float distanceGlow = math.saturate(1f - distance01);
            float boost = math.lerp(0.62f, 1.35f, math.saturate(QualityWeight)) * (0.65f + distanceGlow * 0.35f);
            r = (uint)math.clamp((int)(r * boost), 0, 255);
            g = (uint)math.clamp((int)(g * boost), 0, 255);
            b = (uint)math.clamp((int)(b * boost), 0, 255);
            a = (uint)math.clamp((int)(a * Intensity01 * (0.35f + density01 * 0.35f + distanceGlow * 0.3f)), 0, 255);
            return r | (g << 8) | (b << 16) | (a << 24);
        }

        private static uint ResolveDefaultPackedColor(byte materialId)
        {
            switch (materialId)
            {
                case 2:
                    return Pack(255, 190, 68, 230);
                case 3:
                    return Pack(255, 72, 38, 210);
                case 1:
                    return Pack(78, 196, 255, 205);
                default:
                    return Pack(40, 255, 216, 185);
            }
        }

        private static uint Pack(int r, int g, int b, int a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        private void WriteMiss(int index)
        {
            if ((uint)index < (uint)HitMask.Length)
                HitMask[index] = 0;
        }

        private static float3 ResolveFibonacciDirection(int index, int count, uint seed)
        {
            float safeCount = math.max(1f, count);
            float k = index + 0.5f;
            float z = 1f - 2f * k * math.rcp(safeCount);
            float radius = math.sqrt(math.max(0f, 1f - z * z));
            float phase = (seed & 1023u) * 0.006135923f;
            float theta = k * 2.39996323f + phase;
            MathLodApproximation.ApproxSinCosBhaskara(theta, out float thetaSin, out float thetaCos);
            return new float3(thetaCos * radius, z, thetaSin * radius);
        }

        private static float ResolveSingleLookupDistance01(int index, uint seed)
        {
            uint hash = (uint)index * 747796405u + seed * 2891336453u + 0x9E3779B9u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SonarCompactHitsJob : IJob
    {
        [NoAlias] public NativeArray<SonarPointDTO> Points;
        [ReadOnly, NoAlias] public NativeArray<byte> HitMask;
        [NoAlias] public NativeArray<int> Counters;
        public int RayCount;

        public void Execute()
        {
            int safeRayCount = math.min(math.max(0, RayCount), HitMask.IsCreated ? HitMask.Length : 0);
            if (!Points.IsCreated)
                safeRayCount = 0;
            else
                safeRayCount = math.min(safeRayCount, Points.Length);

            int writeIndex = 0;
            for (int i = 0; i < safeRayCount; i++)
            {
                if (HitMask[i] == 0)
                    continue;

                Points[writeIndex] = Points[i];
                writeIndex++;
            }

            if (!Counters.IsCreated || Counters.Length <= 0)
                return;

            Counters[0] = writeIndex;
            if (Counters.Length > 1)
                Counters[1] = writeIndex;
            if (Counters.Length > 2)
                Counters[2] = safeRayCount - writeIndex;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DecaySonarPointsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SonarPointDTO> Points;
        public int ActivePointCount;
        public float DeltaTime;
        public float FadePerSecond;

        public void Execute(int index)
        {
            if (index < 0 || index >= ActivePointCount || index >= Points.Length)
                return;

            SonarPointDTO point = Points[index];
            uint color = point.ColorPacked;
            uint alpha = (color >> 24) & 0xFFu;
            if (alpha == 0u)
                return;

            int fade = (int)math.round(math.max(0f, DeltaTime) * math.max(0f, FadePerSecond) * 255f);
            uint resolvedAlpha = (uint)math.max(0, (int)alpha - fade);
            point.ColorPacked = (color & 0x00FFFFFFu) | (resolvedAlpha << 24);
            Points[index] = point;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Topographical Sonar Synthesizer")]
    public sealed class TopographicalSonarSynthesizer : MonoBehaviour, ILateFrameTickable, IRenderable, ISonarPingEventListener, IDisposable, IGlobalRegistryHotSwapListener
    {
        private const string OwnerName = "SHINOBU_144";
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_SONAR_SYNTHESIZER.bin";
        private const float TelemetryTimeoutMilliseconds = 20f;
        private const float SonarClockMaxSeconds = 16777215f;

        private static readonly int SonarPointsId = Shader.PropertyToID("_SonarPoints");
        private static readonly int SonarGlobalsId = Shader.PropertyToID("HectonTopographicalSonarGlobals");

        private struct JobBufferSet : IDisposable
        {
            public NativeArray<SonarPointDTO> Points;
            public NativeArray<byte> HitMask;
            public NativeArray<int> Counters;
            public NativeArray<byte> MockSdf;
            public NativeArray<byte> MockMaterialIds;
            public NativeArray<uint> MaterialColorLut;

            public bool EnsureCreated()
            {
                if (!Points.IsCreated)
                    Points = H8Memory.Allocate<SonarPointDTO>(
                        TopographicalSonarConstants.MaxRays,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                if (!HitMask.IsCreated)
                    HitMask = H8Memory.Allocate<byte>(
                        TopographicalSonarConstants.MaxRays,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                if (!Counters.IsCreated)
                    Counters = H8Memory.Allocate<int>(
                        TopographicalSonarConstants.CounterCount,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory);
                if (!MockSdf.IsCreated)
                    MockSdf = H8Memory.Allocate<byte>(
                        TopographicalSonarConstants.MockVoxelCount,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                if (!MockMaterialIds.IsCreated)
                    MockMaterialIds = H8Memory.Allocate<byte>(
                        TopographicalSonarConstants.MockVoxelCount,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                if (!MaterialColorLut.IsCreated)
                    MaterialColorLut = H8Memory.Allocate<uint>(
                        TopographicalSonarConstants.ColorLutEntries,
                        SystemID.UI,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory);

                return IsReady();
            }

            public bool IsReady()
            {
                return Points.IsCreated &&
                       Points.Length >= TopographicalSonarConstants.MaxRays &&
                       HitMask.IsCreated &&
                       HitMask.Length >= TopographicalSonarConstants.MaxRays &&
                       Counters.IsCreated &&
                       Counters.Length >= TopographicalSonarConstants.CounterCount &&
                       MockSdf.IsCreated &&
                       MockSdf.Length >= TopographicalSonarConstants.MockVoxelCount &&
                       MockMaterialIds.IsCreated &&
                       MockMaterialIds.Length >= TopographicalSonarConstants.MockVoxelCount &&
                       MaterialColorLut.IsCreated &&
                       MaterialColorLut.Length >= TopographicalSonarConstants.ColorLutEntries;
            }

            public void Dispose()
            {
                Release(ref Points);
                Release(ref HitMask);
                Release(ref Counters);
                Release(ref MockSdf);
                Release(ref MockMaterialIds);
                Release(ref MaterialColorLut);
            }

            private static void Release<T>(ref NativeArray<T> buffer) where T : struct
            {
                H8Memory.Release(ref buffer, SystemID.UI);
            }
        }

        [Header("Dependencies")]
        [SerializeField] private Transform pingOrigin;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Material pointCloudMaterial;

        [Header("Scan")]
        [SerializeField] private float maxDistanceMeters = TopographicalSonarConstants.DefaultMaxDistanceMeters;
        [SerializeField] private float stepMeters = TopographicalSonarConstants.DefaultStepMeters;
        [SerializeField] private float echoFadeSeconds = 5.5f;
        [SerializeField] private float depthFadeMeters = 0.12f;
        [SerializeField] private float pointSizePixels = 3.2f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.92f;
        [SerializeField, Range(-1f, 1f)] private float qualityOverride = -1f;
        [SerializeField] private bool scheduleCpuFadeJob;
        [SerializeField] private bool drawDebugRays;

        private IDataVault _dataVault;
        private VaultGenerationHandle<SonarPointDTO> _pointsHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<TopographicalSonarTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<uint> _materialColorLutHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<SonarProceduralArgsDTO> _indirectArgsHandle;
        private VaultGenerationHandle<TopographicalSonarShaderGlobalsDTO> _shaderGlobalsHandle;
        private JobBufferSet _jobBuffers;
        private GraphicsBuffer _pointBufferA;
        private GraphicsBuffer _pointBufferB;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _shaderGlobalsBufferA;
        private GraphicsBuffer _shaderGlobalsBufferB;
        private GraphicsBuffer _activeShaderGlobalsBuffer;
        private Bounds _drawBounds;
        private JobHandle _scanHandle;
        private JobHandle _fadeHandle;
        private int _scanJobScheduled;
        private int _fadeJobScheduled;
        private int _pointBufferReadSlot;
        private int _shaderGlobalsWriteIndex;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private int _registeredPingListener;
        private int _registeredHotSwapListener;
        private int _pendingPing;
        private int _activePointCount;
        private int _lastHitCount;
        private int _sequence;
        private int _telemetryWriteIndex;
        private float _pendingIntensity01 = 1f;
        private float _sonarClockSeconds;
        private float _lastPingTimeSeconds;
        private float _lastScheduledPingTimeSeconds = -1000f;
        private float _lastScanWallMilliseconds;
        private uint _lastTelemetryFlags;
        private uint _mockSdfVersion;
        private uint _lastSdfVersion;
        private long _scanStartTimestamp;
        private double3 _lastPingAup;
        private double3 _lastCameraAup;
        private float3 _lastSdfOrigin;
        private float _lastSdfRange;
        private bool _supportsSetConstantBufferCold;

        public static TopographicalSonarSynthesizer ActiveRuntime;

        public int GetActivePointCount() { return _activePointCount; }
        public int GetLastHitCount() { return _lastHitCount; }
        public int GetSequence() { return _sequence; }
        public float GetMaxDistanceMeters() { return maxDistanceMeters; }
        public float GetStepMeters() { return stepMeters; }
        public float GetEchoFadeSeconds() { return echoFadeSeconds; }
        public float GetPointSizePixels() { return pointSizePixels; }
        public float GetQualityOverride() { return qualityOverride; }
        public float GetLastScanWallMilliseconds() { return _lastScanWallMilliseconds; }
        public float GetLastQualityWeight() { return ResolveQualityWeight(); }
        public uint GetLastTelemetryFlags() { return _lastTelemetryFlags; }

        public static bool TryRunStaticSelfAudit(out uint failureMask)
        {
            failureMask = 0u;
            if (UnsafeUtility.SizeOf<SonarPointDTO>() != 16)
                failureMask |= 1u << 0;
            if ((int)Marshal.OffsetOf<SonarPointDTO>(nameof(SonarPointDTO.LocalPosition)) != 0)
                failureMask |= 1u << 1;
            if ((int)Marshal.OffsetOf<SonarPointDTO>(nameof(SonarPointDTO.ColorPacked)) != 12)
                failureMask |= 1u << 2;
            if (UnsafeUtility.SizeOf<TopographicalSonarTelemetryEntry>() != 128)
                failureMask |= 1u << 3;
            if (UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>() != 64)
                failureMask |= 1u << 4;
            if (UnsafeUtility.SizeOf<SonarProceduralArgsDTO>() != 16)
                failureMask |= 1u << 5;
            return failureMask == 0u;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ActiveRuntime = this;
            CacheGraphicsCapabilitiesCold();
            CacheDataVaultCold();
            AllocatePersistentState();
            EnsureGraphicsResources();
            InitializeMaterialColorLut();
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTickable();
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this) ? 1 : 0;
            SpectrumEvents.RegisterSonarPingListener(this);
            _registeredPingListener = 1;
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            TryUnregisterHotSwapListener();

            if (_registeredPingListener != 0)
            {
                SpectrumEvents.UnregisterSonarPingListener(this);
                _registeredPingListener = 0;
            }

            if (_registeredRenderable != 0)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = 0;
            }

            TryUnregisterLateFrameTickable();

            CompleteScheduledJobs();
            ReleaseJobBuffers();

            ReleaseGraphicsBuffer(ref _pointBufferA);
            ReleaseGraphicsBuffer(ref _pointBufferB);
            ReleaseGraphicsBuffer(ref _argsBuffer);
            ReleaseGraphicsBuffer(ref _shaderGlobalsBufferA);
            ReleaseGraphicsBuffer(ref _shaderGlobalsBufferB);
            _activeShaderGlobalsBuffer = null;
            _shaderGlobalsWriteIndex = 0;

            ReleaseVaultBuffers(_dataVault);
            _dataVault = null;
            _activePointCount = 0;
            _lastHitCount = 0;
            if (ReferenceEquals(ActiveRuntime, this))
                ActiveRuntime = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredLateFrame = 0;
                if (currentService == null || !isActiveAndEnabled)
                    return;

                TryRegisterLateFrameTickable();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteScheduledJobs();
            IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : _dataVault;
            IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
            BindDataVaultForLifecycle(nextVault, previousVault);
            _activePointCount = 0;
            _lastHitCount = 0;
            _lastTelemetryFlags = 0u;

            if (!isActiveAndEnabled || _dataVault == null)
                return;

            AllocatePersistentState();
            InitializeMaterialColorLut();
            UpdateIndirectArgsBuffer(0u);
        }

        public void OnSonarPingSent(float intensity)
        {
            _pendingIntensity01 = math.saturate(math.isfinite(intensity) ? intensity : 1f);
            _pendingPing = 1;
        }

        public void TriggerManualPing(float intensity01)
        {
            OnSonarPingSent(intensity01);
        }

        public void LateFrameTick()
        {
            if (_scanJobScheduled != 0)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _scanHandle))
                    return;

                _scanJobScheduled = 0;
                CommitCompletedScan();
            }

            if (_fadeJobScheduled != 0)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _fadeHandle))
                    return;

                _fadeJobScheduled = 0;
                CommitCompletedFade();
            }

            if (_pendingPing != 0)
            {
                float quality = ResolveQualityWeight();
                float now = ResolveSonarClockSeconds();
                if (now - _lastScheduledPingTimeSeconds < ResolveMinimumPingIntervalSeconds(quality))
                    return;

                _pendingPing = 0;
                ScheduleSonarScan(quality, now);
                return;
            }

        }

        public void Render(float deltaTime)
        {
            AdvanceSonarClock(deltaTime);

            if (scheduleCpuFadeJob)
                TryScheduleFadeJob(deltaTime);

            GraphicsBuffer readPointBuffer = ResolveReadPointBuffer();
            if (_activePointCount <= 0 || readPointBuffer == null || _argsBuffer == null || _activeShaderGlobalsBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            if (material == null || !_supportsSetConstantBufferCold)
                return;

            UploadShaderGlobals();
            Shader.SetGlobalBuffer(SonarPointsId, readPointBuffer);
            Shader.SetGlobalConstantBuffer(SonarGlobalsId, _activeShaderGlobalsBuffer, 0, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());
            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                _drawBounds,
                MeshTopology.Triangles,
                _argsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                0);
        }

        public void SetTuningFromEditor(float maxDistance, float step, float pointSize, float fadeSeconds, float quality)
        {
            maxDistanceMeters = math.clamp(maxDistance, 4f, 400f);
            stepMeters = math.clamp(step, TopographicalSonarConstants.MinimumStepMeters, 8f);
            pointSizePixels = math.clamp(pointSize, 0.5f, 18f);
            echoFadeSeconds = math.clamp(fadeSeconds, 0.1f, 60f);
            qualityOverride = math.clamp(quality, -1f, 1f);
        }

        public void SetTuningFromEditor(float maxDistance, float step, float pointSize, float quality)
        {
            SetTuningFromEditor(maxDistance, step, pointSize, echoFadeSeconds, quality);
        }

#if UNITY_EDITOR
        public bool TryApplyMaterialColorCsv(NativeArray<byte> csvBytes, out int appliedRows)
        {
            appliedRows = 0;
            if (!TryAcquireVaultWriteBuffer(_dataVault, in _materialColorLutHandle, TopographicalSonarBufferIds.MaterialColorLut, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
                return false;

            try
            {
                appliedRows = ParseMaterialColorCsv(csvBytes, csvBytes.IsCreated ? csvBytes.Length : 0, lut);
                if (appliedRows > 0)
                    CopyMaterialColorLutToJob(lut);
                return appliedRows > 0;
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _materialColorLutHandle);
            }
        }

        public bool TryDumpBlackBox()
        {
            return DumpBlackBox();
        }

        public static int ParseMaterialColorCsv(NativeArray<byte> csvBytes, NativeArray<uint> colorLut)
        {
            return ParseMaterialColorCsv(csvBytes, csvBytes.IsCreated ? csvBytes.Length : 0, colorLut);
        }

        public static int ParseMaterialColorCsv(NativeArray<byte> csvBytes, int byteCount, NativeArray<uint> colorLut)
        {
            if (!csvBytes.IsCreated)
                return 0;

            return ParseMaterialColorCsv(csvBytes.AsReadOnly(), byteCount, colorLut);
        }

        public static int ParseMaterialColorCsv(NativeArray<byte>.ReadOnly csvBytes, int byteCount, NativeArray<uint> colorLut)
        {
            if (!csvBytes.IsCreated || !colorLut.IsCreated)
                return 0;

            int applied = 0;
            int index = 0;
            int length = math.clamp(byteCount, 0, csvBytes.Length);
            while (index < length)
            {
                SkipSeparators(csvBytes, length, ref index);
                if (index >= length)
                    break;

                byte current = csvBytes[index];
                if (current == (byte)'#')
                {
                    SkipLine(csvBytes, length, ref index);
                    continue;
                }

                if (!TryReadMaterialKey(csvBytes, length, ref index, out int materialId) ||
                    !TryReadColor(csvBytes, length, ref index, out uint packed))
                {
                    SkipLine(csvBytes, length, ref index);
                    continue;
                }

                if ((uint)materialId < (uint)colorLut.Length)
                {
                    colorLut[materialId] = packed;
                    applied++;
                }

                SkipLine(csvBytes, length, ref index);
            }

            return applied;
        }
#endif

        private void AllocatePersistentState()
        {
            if (IsTopographicalHandle(in _pointsHandle, TopographicalSonarBufferIds.Points) &&
                JobBuffersReady() &&
                _pointBufferA != null &&
                _pointBufferB != null &&
                _argsBuffer != null)
                return;

            EnsureJobBuffers();
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _pointsHandle = vault.EnsureGenerationHandle<SonarPointDTO>(
                TopographicalSonarBufferIds.Points,
                TopographicalSonarConstants.MaxRays,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.EnsureGenerationHandle<int>(
                TopographicalSonarBufferIds.Counters,
                TopographicalSonarConstants.CounterCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<TopographicalSonarTelemetryEntry>(
                TopographicalSonarBufferIds.TelemetryRing,
                TopographicalSonarConstants.TelemetryFrames,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                TopographicalSonarBufferIds.TelemetryCursor,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _materialColorLutHandle = vault.EnsureGenerationHandle<uint>(
                TopographicalSonarBufferIds.MaterialColorLut,
                TopographicalSonarConstants.ColorLutEntries,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                TopographicalSonarBufferIds.CsvScratch,
                TopographicalSonarConstants.CsvScratchBytes,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _indirectArgsHandle = vault.EnsureGenerationHandle<SonarProceduralArgsDTO>(
                TopographicalSonarBufferIds.IndirectArgs,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _shaderGlobalsHandle = vault.EnsureGenerationHandle<TopographicalSonarShaderGlobalsDTO>(
                TopographicalSonarBufferIds.ShaderGlobals,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
        }

        private void InitializeMaterialColorLut()
        {
            if (!TryAcquireVaultWriteBuffer(_dataVault, in _materialColorLutHandle, TopographicalSonarBufferIds.MaterialColorLut, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
            {
                if (_jobBuffers.MaterialColorLut.IsCreated)
                    WriteDefaultMaterialColorLut(_jobBuffers.MaterialColorLut);
                return;
            }

            try
            {
                WriteDefaultMaterialColorLut(lut);
                CopyMaterialColorLutToJob(lut);
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _materialColorLutHandle);
            }
        }

        private static void WriteDefaultMaterialColorLut(NativeArray<uint> lut)
        {
            if (!lut.IsCreated || lut.Length < TopographicalSonarConstants.ColorLutEntries)
                return;

            for (int i = 0; i < lut.Length; i++)
                lut[i] = 0u;

            lut[0] = PackColor(40, 255, 216, 185);
            lut[1] = PackColor(78, 196, 255, 205);
            lut[2] = PackColor(255, 190, 68, 230);
            lut[3] = PackColor(255, 72, 38, 210);
        }

        private void EnsureGraphicsResources()
        {
            if (_pointBufferA == null)
                _pointBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<SonarPointDTO>(TopographicalSonarConstants.MaxRays);
            if (_pointBufferB == null)
                _pointBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<SonarPointDTO>(TopographicalSonarConstants.MaxRays);
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<SonarProceduralArgsDTO>());
            if (_supportsSetConstantBufferCold)
            {
                if (_shaderGlobalsBufferA == null)
                    _shaderGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());
                if (_shaderGlobalsBufferB == null)
                    _shaderGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());
                if (_activeShaderGlobalsBuffer == null)
                    _activeShaderGlobalsBuffer = _shaderGlobalsBufferA;
            }

            ResolveRenderMaterial();
            UpdateIndirectArgsBuffer(0u);
        }

        private void ScheduleSonarScan(float quality, float scheduleTimeSeconds)
        {
            if (_scanJobScheduled != 0 || !HotScanResourcesReady())
            {
                return;
            }

            if (!TryResolveNativeState(
                    out NativeArray<SonarPointDTO> points,
                    out NativeArray<byte> hitMask,
                    out NativeArray<int> counters,
                    out NativeArray<byte> mockSdf,
                    out NativeArray<byte> mockMaterialIds,
                    out NativeArray<uint> colorLut))
            {
                return;
            }

            Transform originTransform = pingOrigin != null ? pingOrigin : transform;
            Transform cameraTransform = renderCamera != null ? renderCamera.transform : originTransform;
            Vector3 pingRuntimeVector = originTransform.position;
            Vector3 cameraRuntimeVector = cameraTransform.position;
            float3 pingRuntime = new float3(pingRuntimeVector.x, pingRuntimeVector.y, pingRuntimeVector.z);
            float3 cameraRuntime = new float3(cameraRuntimeVector.x, cameraRuntimeVector.y, cameraRuntimeVector.z);
            if (!TryResolveRuntimeAup(pingRuntimeVector, out _lastPingAup) ||
                !TryResolveRuntimeAup(cameraRuntimeVector, out _lastCameraAup))
            {
                _lastTelemetryFlags |= TopographicalSonarConstants.FaultFlag;
                return;
            }
            quality = math.saturate(math.isfinite(quality) ? quality : 0f);

            NativeArray<byte>.ReadOnly encodedSdf;
            NativeArray<byte>.ReadOnly materialIds;
            int3 gridDimensions;
            float3 volumeOrigin;
            float3 cellSize;
            float sdfRange;
            uint sdfVersion;
            uint flags = TopographicalSonarConstants.PingEventFlag;
            JobHandle dependency = default;
            bool usingPublishedSdf = TryResolvePublishedSdfSnapshot(
                pingRuntime,
                mockSdf,
                mockMaterialIds,
                out encodedSdf,
                out materialIds,
                out gridDimensions,
                out volumeOrigin,
                out cellSize,
                out sdfRange,
                out sdfVersion);
            if (usingPublishedSdf)
            {
                flags |= TopographicalSonarConstants.UsedPublishedSdfFlag;
            }
            else
            {
                ResolveMockSdfDescriptor(pingRuntime, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange);
                _mockSdfVersion++;
                GenerateMockSdfJob mockJob = new GenerateMockSdfJob
                {
                    EncodedSdf = mockSdf,
                    MaterialIds = mockMaterialIds,
                    GridDimensions = gridDimensions,
                    VolumeOrigin = volumeOrigin,
                    CellSize = cellSize,
                    MockCenter = pingRuntime,
                    SdfRange = sdfRange,
                    QualityWeight = quality,
                    Seed = _mockSdfVersion * 2654435761u
                };
                dependency = mockJob.Schedule(TopographicalSonarConstants.MockVoxelCount, 128);
                encodedSdf = mockSdf.AsReadOnly();
                materialIds = mockMaterialIds.AsReadOnly();
                sdfVersion = _mockSdfVersion;
                flags |= TopographicalSonarConstants.UsedMockSdfFlag;
            }

            int rayCount = ResolveRayCount(quality);
            float resolvedStep = ResolveStepMeters(quality);
            int fullStepBudget = math.min(1024, (int)math.ceil(math.max(1f, maxDistanceMeters) * math.rcp(math.max(TopographicalSonarConstants.MinimumStepMeters, resolvedStep))) + 2);
            float workCurve = ResolveWorkCurve(quality);
            int maxSteps = math.clamp((int)math.lerp(1f, fullStepBudget, workCurve), 1, 1024);

            SonarRaymarchJob raymarchJob = new SonarRaymarchJob
            {
                EncodedSdf = encodedSdf,
                MaterialIds = materialIds,
                MaterialColorLut = colorLut,
                Points = points,
                HitMask = hitMask,
                GridDimensions = gridDimensions,
                VolumeOrigin = volumeOrigin,
                CellSize = cellSize,
                SdfRange = sdfRange,
                RayCount = rayCount,
                MaxSteps = maxSteps,
                PingRuntime = pingRuntime,
                MaxDistanceMeters = math.max(1f, maxDistanceMeters),
                StepMeters = resolvedStep,
                QualityWeight = quality,
                Intensity01 = math.max(0.05f, _pendingIntensity01),
                SequenceSeed = (uint)(_sequence + 1)
            };

            _scanStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            JobHandle rayHandle = raymarchJob.Schedule(rayCount, 128, dependency);
            SonarCompactHitsJob compactJob = new SonarCompactHitsJob
            {
                Points = points,
                HitMask = hitMask,
                Counters = counters,
                RayCount = rayCount
            };
            _scanHandle = compactJob.Schedule(rayHandle);
            _scanJobScheduled = 1;
            H8Memory.RegisterActiveJob(SystemID.UI, _scanHandle);
            JobHandle.ScheduleBatchedJobs();
            _sequence++;
            _lastPingTimeSeconds = scheduleTimeSeconds;
            _lastScheduledPingTimeSeconds = scheduleTimeSeconds;
            _lastTelemetryFlags = flags;
            _lastSdfOrigin = volumeOrigin;
            _lastSdfRange = sdfRange;
            _lastSdfVersion = sdfVersion;
            _drawBounds = new Bounds(
                new Vector3(cameraRuntime.x, cameraRuntime.y, cameraRuntime.z),
                Vector3.one * math.max(16f, maxDistanceMeters * 2.25f));
        }

        private void CommitCompletedScan()
        {
            long endTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastScanWallMilliseconds = _scanStartTimestamp > 0L
                ? (float)((endTimestamp - _scanStartTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency)
                : 0f;

            if (!TryResolveNativeState(
                    out NativeArray<SonarPointDTO> points,
                    out _,
                    out NativeArray<int> counters,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            _activePointCount = counters.IsCreated && counters.Length > 0
                ? math.clamp(counters[0], 0, TopographicalSonarConstants.MaxRays)
                : 0;
            _lastHitCount = counters.IsCreated && counters.Length > 1
                ? math.clamp(counters[1], 0, _activePointCount)
                : 0;

            GraphicsBuffer writePointBuffer = ResolveWritePointBuffer();
            if (_activePointCount > 0 && writePointBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(writePointBuffer, points, _activePointCount);
                FlipPointBuffers();
                UpdateIndirectArgsBuffer((uint)_activePointCount);
                _lastTelemetryFlags |= TopographicalSonarConstants.GpuUploadFlag;
            }
            else
            {
                UpdateIndirectArgsBuffer(0u);
            }

            MirrorCompletedScanToVault(points, counters);
            bool invalid = !IsFinite(_lastPingAup) ||
                           !IsFinite(_lastCameraAup) ||
                           !math.all(math.isfinite(_lastSdfOrigin)) ||
                           !math.isfinite(_lastScanWallMilliseconds) ||
                           _lastScanWallMilliseconds > TelemetryTimeoutMilliseconds;
            if (invalid)
                _lastTelemetryFlags |= TopographicalSonarConstants.FaultFlag;

            WriteTelemetry(_lastTelemetryFlags);
            if (invalid)
                DumpBlackBox();
        }

        private void TryScheduleFadeJob(float deltaTime)
        {
            if (_fadeJobScheduled != 0 || _scanJobScheduled != 0 || _activePointCount <= 0)
                return;

            if (!_jobBuffers.Points.IsCreated || _activePointCount > _jobBuffers.Points.Length)
                return;

            DecaySonarPointsJob fadeJob = new DecaySonarPointsJob
            {
                Points = _jobBuffers.Points,
                ActivePointCount = _activePointCount,
                DeltaTime = math.max(0f, deltaTime),
                FadePerSecond = echoFadeSeconds > 0.001f ? math.rcp(echoFadeSeconds) : 1f
            };
            _fadeHandle = fadeJob.Schedule(_activePointCount, 128);
            _fadeJobScheduled = 1;
            H8Memory.RegisterActiveJob(SystemID.UI, _fadeHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private void CommitCompletedFade()
        {
            if (!_jobBuffers.Points.IsCreated)
                return;

            GraphicsBuffer writePointBuffer = ResolveWritePointBuffer();
            if (writePointBuffer == null || _activePointCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(writePointBuffer, _jobBuffers.Points, _activePointCount);
            FlipPointBuffers();
            MirrorCompletedPointsToVault(_jobBuffers.Points, _activePointCount);
        }

        private void WriteTelemetry(uint flags)
        {
            int index = _telemetryWriteIndex % TopographicalSonarConstants.TelemetryFrames;
            int nextIndex = (_telemetryWriteIndex + 1) % TopographicalSonarConstants.TelemetryFrames;

            float3 pingCameraLocal = ResolveLocalAupDeltaFloat3(_lastPingAup, _lastCameraAup);
            float quality = ResolveQualityWeight();
            TopographicalSonarTelemetryEntry entry = default;
            entry.TimeSeconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            entry.PingAupX = _lastPingAup.x;
            entry.PingAupY = _lastPingAup.y;
            entry.PingAupZ = _lastPingAup.z;
            entry.CameraAupX = _lastCameraAup.x;
            entry.CameraAupY = _lastCameraAup.y;
            entry.CameraAupZ = _lastCameraAup.z;
            entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.Sequence = _sequence;
            entry.RequestedRayCount = ResolveRayCount(quality);
            entry.ActivePointCount = _activePointCount;
            entry.HitCount = _lastHitCount;
            entry.Flags = flags;
            entry.GlobalQualityWeight = quality;
            entry.MaxDistanceMeters = math.max(1f, maxDistanceMeters);
            entry.PingOriginCameraLocal = pingCameraLocal;
            entry.SdfOriginRuntime = _lastSdfOrigin;
            entry.SdfRangeMeters = _lastSdfRange;
            entry.StepMeters = ResolveStepMeters(quality);
            entry.SdfVersion = _lastSdfVersion;
            entry.ComputeTimeMicroseconds = (uint)math.max(0, (int)math.round(_lastScanWallMilliseconds * 1000f));

            if (!TryAcquireVaultWriteBuffer(_dataVault, in _telemetryRingHandle, TopographicalSonarBufferIds.TelemetryRing, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry))
                return;

            try
            {
                telemetry[index] = entry;
                _telemetryWriteIndex = nextIndex;
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _telemetryRingHandle);
            }

            if (!TryAcquireVaultWriteBuffer(_dataVault, in _telemetryCursorHandle, TopographicalSonarBufferIds.TelemetryCursor, 1, out NativeArray<int> cursor))
                return;

            try
            {
                cursor[0] = _telemetryWriteIndex;
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _telemetryCursorHandle);
            }
        }

        private bool TryResolveNativeState(
            out NativeArray<SonarPointDTO> points,
            out NativeArray<byte> hitMask,
            out NativeArray<int> counters,
            out NativeArray<byte> mockSdf,
            out NativeArray<byte> mockMaterialIds,
            out NativeArray<uint> colorLut)
        {
            points = _jobBuffers.Points;
            hitMask = _jobBuffers.HitMask;
            counters = _jobBuffers.Counters;
            mockSdf = _jobBuffers.MockSdf;
            mockMaterialIds = _jobBuffers.MockMaterialIds;
            colorLut = _jobBuffers.MaterialColorLut;
            return JobBuffersReady();
        }

        private bool EnsureJobBuffers()
        {
            return _jobBuffers.EnsureCreated();
        }

        private bool JobBuffersReady()
        {
            return _jobBuffers.IsReady();
        }

        private bool HotScanResourcesReady()
        {
            return _dataVault != null &&
                   _supportsSetConstantBufferCold &&
                   JobBuffersReady() &&
                   _pointBufferA != null &&
                   _pointBufferB != null &&
                   _argsBuffer != null &&
                   _shaderGlobalsBufferA != null &&
                   _shaderGlobalsBufferB != null;
        }

        private void ReleaseJobBuffers()
        {
            _jobBuffers.Dispose();
        }

        private void MirrorCompletedScanToVault(NativeArray<SonarPointDTO> points, NativeArray<int> counters)
        {
            MirrorCompletedPointsToVault(points, _activePointCount);
            if (!counters.IsCreated ||
                !TryAcquireVaultWriteBuffer(_dataVault, in _countersHandle, TopographicalSonarBufferIds.Counters, TopographicalSonarConstants.CounterCount, out NativeArray<int> vaultCounters))
            {
                return;
            }

            try
            {
                int count = math.min(counters.Length, vaultCounters.Length);
                for (int i = 0; i < count; i++)
                    vaultCounters[i] = counters[i];
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _countersHandle);
            }
        }

        private void MirrorCompletedPointsToVault(NativeArray<SonarPointDTO> points, int count)
        {
            if (!points.IsCreated ||
                count <= 0 ||
                !TryAcquireVaultWriteBuffer(_dataVault, in _pointsHandle, TopographicalSonarBufferIds.Points, TopographicalSonarConstants.MaxRays, out NativeArray<SonarPointDTO> vaultPoints))
            {
                return;
            }

            try
            {
                int clampedCount = math.min(math.min(count, points.Length), vaultPoints.Length);
                for (int i = 0; i < clampedCount; i++)
                    vaultPoints[i] = points[i];
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _pointsHandle);
            }
        }

        private void CopyMaterialColorLutToJob(NativeArray<uint> source)
        {
            if (!source.IsCreated || !_jobBuffers.MaterialColorLut.IsCreated)
                return;

            int count = math.min(source.Length, _jobBuffers.MaterialColorLut.Length);
            for (int i = 0; i < count; i++)
                _jobBuffers.MaterialColorLut[i] = source[i];
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBufferCold = SystemInfo.supportsSetConstantBuffer;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            BindDataVaultForLifecycle(GlobalRegistry.DataVault);
            return _dataVault;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault = null)
        {
            IDataVault releaseVault = previousVault ?? _dataVault;
            if (!ReferenceEquals(_dataVault, nextVault))
                ReleaseVaultBuffers(releaseVault);

            _dataVault = nextVault;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame != 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI) ? 1 : 0;
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (_registeredLateFrame == 0)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener != 0 || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this) ? 1 : 0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwapListener == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = 0;
        }

        private void CompleteScheduledJobs()
        {
            if (_scanJobScheduled != 0 &&
                DispatcherJobFence.TryComplete(ref _scanHandle, forceComplete: true))
            {
                _scanJobScheduled = 0;
            }

            if (_fadeJobScheduled != 0 &&
                DispatcherJobFence.TryComplete(ref _fadeHandle, forceComplete: true))
            {
                _fadeJobScheduled = 0;
            }
        }

        private void ReleaseVaultBuffers(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _pointsHandle, TopographicalSonarBufferIds.Points);
            ReleaseVaultBuffer(vault, ref _countersHandle, TopographicalSonarBufferIds.Counters);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle, TopographicalSonarBufferIds.TelemetryRing);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle, TopographicalSonarBufferIds.TelemetryCursor);
            ReleaseVaultBuffer(vault, ref _materialColorLutHandle, TopographicalSonarBufferIds.MaterialColorLut);
            ReleaseVaultBuffer(vault, ref _csvScratchHandle, TopographicalSonarBufferIds.CsvScratch);
            ReleaseVaultBuffer(vault, ref _indirectArgsHandle, TopographicalSonarBufferIds.IndirectArgs);
            ReleaseVaultBuffer(vault, ref _shaderGlobalsHandle, TopographicalSonarBufferIds.ShaderGlobals);
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsTopographicalHandle(in handle, expectedBufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer))
                return false;

            return !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryAcquireVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsTopographicalHandle(in handle, expectedBufferId))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.UI, out buffer))
                return false;

            if (!vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            vault.ReleaseWriteLock(in handle, SystemID.UI);
            buffer = default;
            return false;
        }

        private static void ReleaseVaultWriteBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : unmanaged
        {
            vault?.ReleaseWriteLock(in handle, SystemID.UI);
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : unmanaged
        {
            if (vault != null && IsTopographicalHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsTopographicalHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private static bool TryResolvePublishedSdfSnapshot(
            float3 pingRuntime,
            NativeArray<byte> sdfSnapshot,
            NativeArray<byte> materialSnapshot,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out NativeArray<byte>.ReadOnly materialIds,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange,
            out uint version)
        {
            encodedSdf = default;
            materialIds = default;
            gridDimensions = default;
            volumeOrigin = default;
            cellSize = default;
            sdfRange = 0f;
            version = 0u;
            if (!sdfSnapshot.IsCreated ||
                !materialSnapshot.IsCreated ||
                !math.all(math.isfinite(pingRuntime)))
            {
                return false;
            }

            Vector3 origin = new Vector3(pingRuntime.x, pingRuntime.y, pingRuntime.z);
            if (!HectonVoxelVolume.TryAcquireClosestPublishedSonarSdfPayloadReadLease(
                    origin,
                    out HectonVoxelVolume payloadVolume,
                    out NativeArray<byte>.ReadOnly payload,
                    out NativeArray<byte>.ReadOnly materialPayload,
                    out Vector3Int dimensions,
                    out Vector3 payloadOrigin,
                    out Vector3 payloadCellSize,
                    out float payloadRange,
                    out int payloadVersion,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease payloadLease))
            {
                return false;
            }

            try
            {
                long expectedLength64 = (long)dimensions.x * dimensions.y * dimensions.z;
                if (expectedLength64 <= 0L || expectedLength64 > int.MaxValue)
                    return false;

                int expectedLength = (int)expectedLength64;
                int3 resolvedDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
                float3 resolvedOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
                float3 resolvedCellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
                if (!payload.IsCreated ||
                    !materialPayload.IsCreated ||
                    payload.Length < expectedLength ||
                    materialPayload.Length < expectedLength ||
                    sdfSnapshot.Length < expectedLength ||
                    materialSnapshot.Length < expectedLength ||
                    !math.all(resolvedDimensions > 1) ||
                    !math.all(math.isfinite(resolvedOrigin)) ||
                    !math.all(math.isfinite(resolvedCellSize)) ||
                    math.any(math.abs(resolvedCellSize) <= new float3(0.0001f)) ||
                    !math.isfinite(payloadRange) ||
                    payloadRange <= 0.0001f)
                {
                    return false;
                }

                for (int i = 0; i < expectedLength; i++)
                {
                    sdfSnapshot[i] = payload[i];
                    materialSnapshot[i] = materialPayload[i];
                }

                encodedSdf = sdfSnapshot.AsReadOnly();
                materialIds = materialSnapshot.AsReadOnly();
                gridDimensions = resolvedDimensions;
                volumeOrigin = resolvedOrigin;
                cellSize = resolvedCellSize;
                sdfRange = payloadRange;
                version = (uint)math.max(0, payloadVersion);
                return true;
            }
            finally
            {
                if (payloadVolume != null)
                    payloadVolume.ReleasePublishedSonarSdfPayloadReadLease(in payloadLease);
            }
        }

        private void ResolveMockSdfDescriptor(float3 pingRuntime, out int3 dimensions, out float3 volumeOrigin, out float3 cellSize, out float sdfRange)
        {
            dimensions = new int3(
                TopographicalSonarConstants.MockGridSide,
                TopographicalSonarConstants.MockGridSide,
                TopographicalSonarConstants.MockGridSide);
            float extent = math.max(16f, maxDistanceMeters);
            float voxel = (extent * 2f) * math.rcp(TopographicalSonarConstants.MockGridSide - 1);
            cellSize = new float3(voxel);
            volumeOrigin = pingRuntime - new float3(extent);
            sdfRange = ResolveMockSdfRange();
        }

        private float ResolveMockSdfRange()
        {
            return math.max(0.25f, math.max(TopographicalSonarConstants.MinimumStepMeters, stepMeters) * 8f);
        }

        private float ResolveQualityWeight()
        {
            if (qualityOverride >= 0f)
                return math.saturate(qualityOverride);

            return math.saturate(HomeostasisBrain.GlobalQualityWeight);
        }

        private void AdvanceSonarClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _sonarClockSeconds = math.min(SonarClockMaxSeconds, _sonarClockSeconds + deltaTime);
        }

        private float ResolveSonarClockSeconds()
        {
            return _sonarClockSeconds;
        }

        private int ResolveRayCount(float quality)
        {
            return math.clamp(
                (int)math.lerp(TopographicalSonarConstants.MinRays, TopographicalSonarConstants.MaxRays, math.saturate(quality)),
                TopographicalSonarConstants.MinRays,
                TopographicalSonarConstants.MaxRays);
        }

        private float ResolveStepMeters(float quality)
        {
            float baseStep = math.max(TopographicalSonarConstants.MinimumStepMeters, stepMeters);
            float lodStep = math.lerp(baseStep * 1.85f, baseStep * 0.55f, math.saturate(quality));
            return math.clamp(lodStep, TopographicalSonarConstants.MinimumStepMeters, 8f);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static float ResolveWorkCurve(float quality)
        {
            return Smooth01(math.saturate((math.saturate(quality) - 0.1f) * math.rcp(0.9f)));
        }

        private static float ResolveMinimumPingIntervalSeconds(float quality)
        {
            return math.lerp(
                TopographicalSonarConstants.MaximumPingIntervalSeconds,
                TopographicalSonarConstants.MinimumPingIntervalSeconds,
                ResolveWorkCurve(quality));
        }

        private Material ResolveRenderMaterial()
        {
            return pointCloudMaterial;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_argsBuffer == null)
                return;

            SonarProceduralArgsDTO args = new SonarProceduralArgsDTO
            {
                VertexCountPerInstance = 6u,
                InstanceCount = instanceCount,
                StartVertex = 0u,
                StartInstance = 0u
            };
            if (TryAcquireVaultWriteBuffer(_dataVault, in _indirectArgsHandle, TopographicalSonarBufferIds.IndirectArgs, 1, out NativeArray<SonarProceduralArgsDTO> vaultArgs))
            {
                try
                {
                    vaultArgs[0] = args;
                }
                finally
                {
                    ReleaseVaultWriteBuffer(_dataVault, in _indirectArgsHandle);
                }
            }

            NativeArray<SonarProceduralArgsDTO> argsWrite =
                _argsBuffer.LockBufferForWrite<SonarProceduralArgsDTO>(0, 1);
            try
            {
                argsWrite[0] = args;
            }
            finally
            {
                _argsBuffer.UnlockBufferAfterWrite<SonarProceduralArgsDTO>(1);
            }
        }

        private GraphicsBuffer ResolveReadPointBuffer()
        {
            return _pointBufferReadSlot == 0 ? _pointBufferA : _pointBufferB;
        }

        private GraphicsBuffer ResolveWritePointBuffer()
        {
            return _pointBufferReadSlot == 0 ? _pointBufferB : _pointBufferA;
        }

        private void FlipPointBuffers()
        {
            _pointBufferReadSlot = _pointBufferReadSlot == 0 ? 1 : 0;
        }

        private void UploadShaderGlobals()
        {
            GraphicsBuffer shaderGlobalsWriteBuffer = (_shaderGlobalsWriteIndex & 1) == 0 ? _shaderGlobalsBufferA : _shaderGlobalsBufferB;
            if (shaderGlobalsWriteBuffer == null || !shaderGlobalsWriteBuffer.IsValid())
                return;

            Transform cameraTransform = renderCamera != null ? renderCamera.transform : transform;
            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            if (!TryResolveRuntimeAup(cameraPosition, out double3 cameraAup))
                return;

            float3 pingCameraLocal = ResolveLocalAupDeltaFloat3(_lastPingAup, cameraAup);
            float quality = ResolveQualityWeight();
            TopographicalSonarShaderGlobalsDTO globals = new TopographicalSonarShaderGlobalsDTO
            {
                CameraRuntimeAndPointSize = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, math.max(0.2f, pointSizePixels)),
                PingSignal = new float4(math.max(0f, ResolveSonarClockSeconds() - _lastPingTimeSeconds), math.max(0.001f, echoFadeSeconds), math.max(0.05f, _pendingIntensity01), _activePointCount),
                RenderParams0 = new float4(math.saturate(opacity), math.max(0.0001f, depthFadeMeters), math.max(1f, maxDistanceMeters), quality),
                RenderParams1 = new float4(pingCameraLocal.x, pingCameraLocal.y, pingCameraLocal.z, (float)_lastTelemetryFlags)
            };

            if (TryAcquireVaultWriteBuffer(_dataVault, in _shaderGlobalsHandle, TopographicalSonarBufferIds.ShaderGlobals, 1, out NativeArray<TopographicalSonarShaderGlobalsDTO> vaultGlobals))
            {
                try
                {
                    vaultGlobals[0] = globals;
                }
                finally
                {
                    ReleaseVaultWriteBuffer(_dataVault, in _shaderGlobalsHandle);
                }
            }

            NativeArray<TopographicalSonarShaderGlobalsDTO> mapped =
                shaderGlobalsWriteBuffer.LockBufferForWrite<TopographicalSonarShaderGlobalsDTO>(0, 1);
            try
            {
                mapped[0] = globals;
            }
            finally
            {
                shaderGlobalsWriteBuffer.UnlockBufferAfterWrite<TopographicalSonarShaderGlobalsDTO>(1);
            }
            _activeShaderGlobalsBuffer = shaderGlobalsWriteBuffer;
            _shaderGlobalsWriteIndex ^= 1;
        }

        private unsafe bool DumpBlackBox()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsTopographicalHandle(in _telemetryRingHandle, TopographicalSonarBufferIds.TelemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<TopographicalSonarTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return false;
            }

            int telemetryLength = math.min(telemetry.Length, TopographicalSonarConstants.TelemetryFrames);
            if (telemetryLength <= 0)
                return false;

            NativeArray<byte> dumpBytes = default;
            bool dumpRegistered = false;
            try
            {
                string directory = Path.GetDirectoryName(BlackBoxDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int stride = UnsafeUtility.SizeOf<TopographicalSonarTelemetryEntry>();
                if (stride <= 0 || telemetryLength > int.MaxValue / stride)
                    return false;

                int byteCount = telemetryLength * stride;
                dumpBytes = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(dumpBytes, nameof(TopographicalSonarSynthesizer), nameof(dumpBytes), NativeAllocationLifetime.Temp);
                dumpRegistered = true;

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(dumpBytes);
                int cursor = 0;
                for (int i = 0; i < telemetryLength; i++)
                {
                    TopographicalSonarTelemetryEntry entry = telemetry[i];
                    UnsafeUtility.MemCpy(destination + cursor, &entry, stride);
                    cursor += stride;
                }

                return Hecton8.SaveSystem.AsyncWriteManager.WriteAll(BlackBoxDumpPath, destination, cursor, out _);
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            catch (ObjectDisposedException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            catch (InvalidOperationException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            catch (ArgumentException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            catch (NotSupportedException)
            {
                Hecton8.Core.H8Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
            finally
            {
                if (dumpBytes.IsCreated)
                {
                    if (dumpRegistered)
                        NativeMemorySentinel.UnregisterNativeArray(dumpBytes);
                    dumpBytes.Dispose();
                }
            }
        }

        private static float3 ResolveLocalAupDeltaFloat3(double3 targetAup, double3 originAup)
        {
            double3 deltaAup = targetAup - originAup;
            double3 clamped = math.clamp(
                deltaAup,
                new double3(-TopographicalSonarConstants.MaxTelemetryLocalMeters),
                new double3(TopographicalSonarConstants.MaxTelemetryLocalMeters));
            return new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

#if UNITY_EDITOR
        private static void SkipSeparators(NativeArray<byte>.ReadOnly bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte value = bytes[index];
                if (value != (byte)' ' && value != (byte)'\t' && value != (byte)'\r' && value != (byte)'\n' && value != (byte)',')
                    return;
                index++;
            }
        }

        private static void SkipLine(NativeArray<byte>.ReadOnly bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n')
                index++;
            if (index < length)
                index++;
        }

        private static bool TryReadMaterialKey(NativeArray<byte>.ReadOnly bytes, int length, ref int index, out int materialId)
        {
            materialId = 0;
            SkipSeparators(bytes, length, ref index);
            if (index >= length)
                return false;

            byte c = bytes[index];
            if ((c >= (byte)'0' && c <= (byte)'9') || c == (byte)'-')
                return TryReadInt(bytes, length, ref index, out materialId);

            uint hash = 2166136261u;
            int consumed = 0;
            while (index < length)
            {
                c = bytes[index];
                if (c == (byte)',' || c == (byte)'\r' || c == (byte)'\n')
                    break;

                byte lower = c >= (byte)'A' && c <= (byte)'Z' ? (byte)(c + 32) : c;
                if (lower != (byte)' ' && lower != (byte)'\t')
                {
                    hash = (hash ^ lower) * 16777619u;
                    consumed++;
                }

                index++;
            }

            materialId = (int)(hash & 0xFFu);
            return consumed > 0;
        }

        private static bool TryReadColor(NativeArray<byte>.ReadOnly bytes, int length, ref int index, out uint packed)
        {
            packed = 0u;
            SkipSeparators(bytes, length, ref index);
            if (index >= length)
                return false;

            if (bytes[index] == (byte)'#')
            {
                index++;
                if (!TryReadHexByte(bytes, length, ref index, out int r) ||
                    !TryReadHexByte(bytes, length, ref index, out int g) ||
                    !TryReadHexByte(bytes, length, ref index, out int b))
                {
                    return false;
                }

                int a = 255;
                int saved = index;
                if (!TryReadHexByte(bytes, length, ref index, out a))
                    index = saved;

                packed = PackColor(r, g, b, a);
                return true;
            }

            if (!TryReadInt(bytes, length, ref index, out int red) ||
                !TryReadInt(bytes, length, ref index, out int green) ||
                !TryReadInt(bytes, length, ref index, out int blue))
            {
                return false;
            }

            int alpha = 255;
            int alphaSaved = index;
            if (!TryReadInt(bytes, length, ref index, out alpha))
                index = alphaSaved;

            packed = PackColor(red, green, blue, alpha);
            return true;
        }

        private static bool TryReadInt(NativeArray<byte>.ReadOnly bytes, int length, ref int index, out int value)
        {
            value = 0;
            SkipSeparators(bytes, length, ref index);
            if (index >= length)
                return false;

            int sign = 1;
            if (bytes[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            int result = 0;
            int digits = 0;
            while (index < length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                result = result * 10 + (c - (byte)'0');
                digits++;
                index++;
            }

            value = result * sign;
            return digits > 0;
        }

        private static bool TryReadHexByte(NativeArray<byte>.ReadOnly bytes, int length, ref int index, out int value)
        {
            value = 0;
            if (index + 1 >= length ||
                !TryReadHexNibble(bytes[index], out int hi) ||
                !TryReadHexNibble(bytes[index + 1], out int lo))
            {
                return false;
            }

            value = (hi << 4) | lo;
            index += 2;
            return true;
        }

        private static bool TryReadHexNibble(byte c, out int value)
        {
            if (c >= (byte)'0' && c <= (byte)'9')
            {
                value = c - (byte)'0';
                return true;
            }

            if (c >= (byte)'a' && c <= (byte)'f')
            {
                value = c - (byte)'a' + 10;
                return true;
            }

            if (c >= (byte)'A' && c <= (byte)'F')
            {
                value = c - (byte)'A' + 10;
                return true;
            }

            value = 0;
            return false;
        }
#endif

        private static uint PackColor(int r, int g, int b, int a)
        {
            uint rr = (uint)math.clamp(r, 0, 255);
            uint gg = (uint)math.clamp(g, 0, 255);
            uint bb = (uint)math.clamp(b, 0, 255);
            uint aa = (uint)math.clamp(a, 0, 255);
            return rr | (gg << 8) | (bb << 16) | (aa << 24);
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return IsFinite(positionAup);
        }

#if UNITY_EDITOR
        public bool TryApplyMaterialColorCsvFileForEditor(string path, out int appliedRows)
        {
            appliedRows = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryAcquireVaultWriteBuffer(_dataVault, in _csvScratchHandle, TopographicalSonarBufferIds.CsvScratch, TopographicalSonarConstants.CsvScratchBytes, out NativeArray<byte> scratch))
            {
                return false;
            }

            int byteCount = 0;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    Span<byte> readBuffer = stackalloc byte[4096];
                    while (byteCount < scratch.Length)
                    {
                        int requestedBytes = math.min(readBuffer.Length, scratch.Length - byteCount);
                        int read = stream.Read(readBuffer.Slice(0, requestedBytes));
                        if (read <= 0)
                            break;

                        for (int i = 0; i < read; i++)
                            scratch[byteCount + i] = readBuffer[i];
                        byteCount += read;
                    }
                }
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _csvScratchHandle);
            }

            if (!TryReadVaultBuffer(_dataVault, in _csvScratchHandle, TopographicalSonarBufferIds.CsvScratch, TopographicalSonarConstants.CsvScratchBytes, out NativeArray<byte>.ReadOnly scratchRead) ||
                !TryAcquireVaultWriteBuffer(_dataVault, in _materialColorLutHandle, TopographicalSonarBufferIds.MaterialColorLut, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
            {
                return false;
            }

            try
            {
                appliedRows = ParseMaterialColorCsv(scratchRead, byteCount, lut);
                if (appliedRows > 0)
                    CopyMaterialColorLutToJob(lut);
                if (appliedRows > 0)
                    _lastTelemetryFlags |= TopographicalSonarConstants.CsvColorFlag;
                return appliedRows > 0;
            }
            finally
            {
                ReleaseVaultWriteBuffer(_dataVault, in _materialColorLutHandle);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugRays)
                return;

            Transform originTransform = pingOrigin != null ? pingOrigin : transform;
            if (originTransform == null)
                return;

            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.35f);
            Vector3 origin = originTransform.position;
            int count = math.min(96, math.max(8, ResolveRayCount(ResolveQualityWeight()) / 256));
            NativeArray<SonarPointDTO>.ReadOnly points = default;
            bool hasPoints = Application.isPlaying &&
                             TryReadVaultBuffer(_dataVault, in _pointsHandle, TopographicalSonarBufferIds.Points, TopographicalSonarConstants.MaxRays, out points) &&
                             points.IsCreated;
            Transform cameraTransform = renderCamera != null ? renderCamera.transform : transform;
            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : origin;
            for (int i = 0; i < count; i++)
            {
                float k = i + 0.5f;
                float z = 1f - 2f * k / count;
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float theta = k * 2.39996323f;
                MathLodApproximation.ApproxSinCosBhaskara(theta, out float thetaSin, out float thetaCos);
                Vector3 direction = new Vector3(thetaCos * radius, z, thetaSin * radius);
                if (hasPoints && i < _activePointCount && i < points.Length && ((points[i].ColorPacked >> 24) & 0xFFu) != 0u)
                {
                    float3 local = points[i].LocalPosition;
                    Vector3 hit = origin + new Vector3(local.x, local.y, local.z);
                    Gizmos.color = new Color(1f, 0.08f, 0.03f, 0.8f);
                    Gizmos.DrawLine(origin, hit);
                    Gizmos.DrawSphere(hit, 0.18f);
                }
                else
                {
                    Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.35f);
                    Gizmos.DrawLine(origin, origin + direction * Mathf.Min(maxDistanceMeters, 16f));
                }
            }
        }
#endif
    }
}
