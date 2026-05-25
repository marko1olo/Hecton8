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
        private VaultGenerationHandle<byte> _hitMaskHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<byte> _mockSdfHandle;
        private VaultGenerationHandle<byte> _mockMaterialIdsHandle;
        private VaultGenerationHandle<TopographicalSonarTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<uint> _materialColorLutHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<SonarProceduralArgsDTO> _indirectArgsHandle;
        private VaultGenerationHandle<TopographicalSonarShaderGlobalsDTO> _shaderGlobalsHandle;
        private GraphicsBuffer _pointBufferA;
        private GraphicsBuffer _pointBufferB;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _shaderGlobalsBuffer;
        private Bounds _drawBounds;
        private JobHandle _scanHandle;
        private JobHandle _fadeHandle;
        private int _scanJobScheduled;
        private int _fadeJobScheduled;
        private int _pointBufferReadSlot;
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

            ReleaseGraphicsBuffer(ref _pointBufferA);
            ReleaseGraphicsBuffer(ref _pointBufferB);
            ReleaseGraphicsBuffer(ref _argsBuffer);
            ReleaseGraphicsBuffer(ref _shaderGlobalsBuffer);

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
                if (currentService == null || !isActiveAndEnabled)
                    return;

                TryUnregisterLateFrameTickable();
                TryRegisterLateFrameTickable();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteScheduledJobs();
            ReleaseVaultBuffers(_dataVault);
            _dataVault = currentService as IDataVault;
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
            if (_activePointCount <= 0 || readPointBuffer == null || _argsBuffer == null || _shaderGlobalsBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            if (material == null || !SystemInfo.supportsSetConstantBuffer)
                return;

            UploadShaderGlobals();
            Shader.SetGlobalBuffer(SonarPointsId, readPointBuffer);
            Shader.SetGlobalConstantBuffer(SonarGlobalsId, _shaderGlobalsBuffer, 0, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());
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
            if (!TryResolveVaultBuffer(_dataVault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
                return false;

            appliedRows = ParseMaterialColorCsv(csvBytes, csvBytes.IsCreated ? csvBytes.Length : 0, lut);
            return appliedRows > 0;
        }

        public bool TryDumpBlackBox()
        {
            return TryResolveVaultBuffer(_dataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry) &&
                   DumpBlackBox(telemetry);
        }

        public static int ParseMaterialColorCsv(NativeArray<byte> csvBytes, NativeArray<uint> colorLut)
        {
            return ParseMaterialColorCsv(csvBytes, csvBytes.IsCreated ? csvBytes.Length : 0, colorLut);
        }

        public static int ParseMaterialColorCsv(NativeArray<byte> csvBytes, int byteCount, NativeArray<uint> colorLut)
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
            if (_pointsHandle.BufferID != 0u && _pointBufferA != null && _pointBufferB != null && _argsBuffer != null)
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            _pointsHandle = vault.EnsureGenerationHandle<SonarPointDTO>(
                TopographicalSonarBufferIds.Points,
                TopographicalSonarConstants.MaxRays,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _hitMaskHandle = vault.EnsureGenerationHandle<byte>(
                TopographicalSonarBufferIds.HitMask,
                TopographicalSonarConstants.MaxRays,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.EnsureGenerationHandle<int>(
                TopographicalSonarBufferIds.Counters,
                TopographicalSonarConstants.CounterCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _mockSdfHandle = vault.EnsureGenerationHandle<byte>(
                TopographicalSonarBufferIds.MockSdf,
                TopographicalSonarConstants.MockVoxelCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _mockMaterialIdsHandle = vault.EnsureGenerationHandle<byte>(
                TopographicalSonarBufferIds.MockMaterialIds,
                TopographicalSonarConstants.MockVoxelCount,
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
            if (!TryResolveVaultBuffer(_dataVault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
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
            if (_shaderGlobalsBuffer == null && SystemInfo.supportsSetConstantBuffer)
                _shaderGlobalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());

            ResolveRenderMaterial();
            UpdateIndirectArgsBuffer(0u);
        }

        private void ScheduleSonarScan(float quality, float scheduleTimeSeconds)
        {
            AllocatePersistentState();
            EnsureGraphicsResources();
            if (_scanJobScheduled != 0 ||
                !TryResolveNativeState(
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
            if (TryResolvePublishedSdf(pingRuntime, out encodedSdf, out materialIds, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange, out sdfVersion))
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

            bool invalid = !IsFinite(_lastPingAup) ||
                           !IsFinite(_lastCameraAup) ||
                           !math.all(math.isfinite(_lastSdfOrigin)) ||
                           !math.isfinite(_lastScanWallMilliseconds) ||
                           _lastScanWallMilliseconds > TelemetryTimeoutMilliseconds;
            if (invalid)
                _lastTelemetryFlags |= TopographicalSonarConstants.FaultFlag;

            WriteTelemetry(_lastTelemetryFlags);
            if (invalid && TryResolveVaultBuffer(_dataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry))
                DumpBlackBox(telemetry);
        }

        private void TryScheduleFadeJob(float deltaTime)
        {
            if (_fadeJobScheduled != 0 || _scanJobScheduled != 0 || _activePointCount <= 0)
                return;

            if (!TryResolveVaultBuffer(_dataVault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out NativeArray<SonarPointDTO> points))
                return;

            DecaySonarPointsJob fadeJob = new DecaySonarPointsJob
            {
                Points = points,
                ActivePointCount = _activePointCount,
                DeltaTime = math.max(0f, deltaTime),
                FadePerSecond = echoFadeSeconds > 0.001f ? math.rcp(echoFadeSeconds) : 1f
            };
            _fadeHandle = fadeJob.Schedule(_activePointCount, 128);
            _fadeJobScheduled = 1;
        }

        private void CommitCompletedFade()
        {
            if (!TryResolveVaultBuffer(_dataVault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out NativeArray<SonarPointDTO> points))
                return;

            GraphicsBuffer writePointBuffer = ResolveWritePointBuffer();
            if (writePointBuffer == null || _activePointCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(writePointBuffer, points, _activePointCount);
            FlipPointBuffers();
        }

        private void WriteTelemetry(uint flags)
        {
            if (!TryResolveVaultBuffer(_dataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry))
                return;

            int index = _telemetryWriteIndex % TopographicalSonarConstants.TelemetryFrames;
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % TopographicalSonarConstants.TelemetryFrames;
            if (TryResolveVaultBuffer(_dataVault, ref _telemetryCursorHandle, 1, out NativeArray<int> cursor))
                cursor[0] = _telemetryWriteIndex;

            float3 pingCameraLocal = new float3(
                (float)(_lastPingAup.x - _lastCameraAup.x),
                (float)(_lastPingAup.y - _lastCameraAup.y),
                (float)(_lastPingAup.z - _lastCameraAup.z));
            float quality = ResolveQualityWeight();
            telemetry[index] = new TopographicalSonarTelemetryEntry
            {
                TimeSeconds = Time.realtimeSinceStartupAsDouble,
                PingAupX = _lastPingAup.x,
                PingAupY = _lastPingAup.y,
                PingAupZ = _lastPingAup.z,
                CameraAupX = _lastCameraAup.x,
                CameraAupY = _lastCameraAup.y,
                CameraAupZ = _lastCameraAup.z,
                Frame = (uint)math.max(0, Hecton8.Core.SystemDispatcher.CurrentFrameIndex),
                Sequence = _sequence,
                RequestedRayCount = ResolveRayCount(quality),
                ActivePointCount = _activePointCount,
                HitCount = _lastHitCount,
                Flags = flags,
                GlobalQualityWeight = quality,
                MaxDistanceMeters = math.max(1f, maxDistanceMeters),
                PingOriginCameraLocal = pingCameraLocal,
                SdfOriginRuntime = _lastSdfOrigin,
                SdfRangeMeters = _lastSdfRange,
                StepMeters = ResolveStepMeters(quality),
                SdfVersion = _lastSdfVersion,
                ComputeTimeMicroseconds = (uint)math.max(0, (int)math.round(_lastScanWallMilliseconds * 1000f))
            };
        }

        private bool TryResolveNativeState(
            out NativeArray<SonarPointDTO> points,
            out NativeArray<byte> hitMask,
            out NativeArray<int> counters,
            out NativeArray<byte> mockSdf,
            out NativeArray<byte> mockMaterialIds,
            out NativeArray<uint> colorLut)
        {
            IDataVault vault = _dataVault;
            bool resolvedPoints = TryResolveVaultBuffer(vault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out points);
            bool resolvedHitMask = TryResolveVaultBuffer(vault, ref _hitMaskHandle, TopographicalSonarConstants.MaxRays, out hitMask);
            bool resolvedCounters = TryResolveVaultBuffer(vault, ref _countersHandle, TopographicalSonarConstants.CounterCount, out counters);
            bool resolvedMockSdf = TryResolveVaultBuffer(vault, ref _mockSdfHandle, TopographicalSonarConstants.MockVoxelCount, out mockSdf);
            bool resolvedMockMaterials = TryResolveVaultBuffer(vault, ref _mockMaterialIdsHandle, TopographicalSonarConstants.MockVoxelCount, out mockMaterialIds);
            bool resolvedColorLut = TryResolveVaultBuffer(vault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out colorLut);
            return resolvedPoints && resolvedHitMask && resolvedCounters && resolvedMockSdf && resolvedMockMaterials && resolvedColorLut;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
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
            ReleaseVaultBuffer(vault, ref _pointsHandle);
            ReleaseVaultBuffer(vault, ref _hitMaskHandle);
            ReleaseVaultBuffer(vault, ref _countersHandle);
            ReleaseVaultBuffer(vault, ref _mockSdfHandle);
            ReleaseVaultBuffer(vault, ref _mockMaterialIdsHandle);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle);
            ReleaseVaultBuffer(vault, ref _materialColorLutHandle);
            ReleaseVaultBuffer(vault, ref _csvScratchHandle);
            ReleaseVaultBuffer(vault, ref _indirectArgsHandle);
            ReleaseVaultBuffer(vault, ref _shaderGlobalsHandle);
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || handle.BufferID == 0u)
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer))
                return false;

            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryResolvePublishedSdf(
            float3 pingRuntime,
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

            Vector3 origin = new Vector3(pingRuntime.x, pingRuntime.y, pingRuntime.z);
            if (!HectonVoxelVolume.TryGetClosestPublishedSonarSdfPayload(
                    origin,
                    out NativeArray<byte>.ReadOnly payload,
                    out NativeArray<byte>.ReadOnly materialPayload,
                    out Vector3Int dimensions,
                    out Vector3 payloadOrigin,
                    out Vector3 payloadCellSize,
                    out float payloadRange,
                    out int payloadVersion))
            {
                return false;
            }

            int expectedLength = dimensions.x * dimensions.y * dimensions.z;
            if (!payload.IsCreated ||
                !materialPayload.IsCreated ||
                expectedLength <= 0 ||
                payload.Length < expectedLength ||
                materialPayload.Length < expectedLength ||
                payloadRange <= 0.0001f)
            {
                return false;
            }

            encodedSdf = payload;
            materialIds = materialPayload;
            gridDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
            volumeOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
            cellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
            sdfRange = payloadRange;
            version = (uint)math.max(0, payloadVersion);
            return true;
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
            if (TryResolveVaultBuffer(_dataVault, ref _indirectArgsHandle, 1, out NativeArray<SonarProceduralArgsDTO> vaultArgs))
                vaultArgs[0] = args;

            NativeArray<SonarProceduralArgsDTO> argsWrite =
                _argsBuffer.LockBufferForWrite<SonarProceduralArgsDTO>(0, 1);
            argsWrite[0] = args;
            _argsBuffer.UnlockBufferAfterWrite<SonarProceduralArgsDTO>(1);
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
            if (_shaderGlobalsBuffer == null)
                return;

            Transform cameraTransform = renderCamera != null ? renderCamera.transform : transform;
            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            if (!TryResolveRuntimeAup(cameraPosition, out double3 cameraAup))
                return;

            double3 pingCameraLocal = _lastPingAup - cameraAup;
            float quality = ResolveQualityWeight();
            TopographicalSonarShaderGlobalsDTO globals = new TopographicalSonarShaderGlobalsDTO
            {
                CameraRuntimeAndPointSize = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, math.max(0.2f, pointSizePixels)),
                PingSignal = new float4(math.max(0f, ResolveSonarClockSeconds() - _lastPingTimeSeconds), math.max(0.001f, echoFadeSeconds), math.max(0.05f, _pendingIntensity01), _activePointCount),
                RenderParams0 = new float4(math.saturate(opacity), math.max(0.0001f, depthFadeMeters), math.max(1f, maxDistanceMeters), quality),
                RenderParams1 = new float4((float)pingCameraLocal.x, (float)pingCameraLocal.y, (float)pingCameraLocal.z, (float)_lastTelemetryFlags)
            };

            if (TryResolveVaultBuffer(_dataVault, ref _shaderGlobalsHandle, 1, out NativeArray<TopographicalSonarShaderGlobalsDTO> vaultGlobals))
                vaultGlobals[0] = globals;

            NativeArray<TopographicalSonarShaderGlobalsDTO> mapped =
                _shaderGlobalsBuffer.LockBufferForWrite<TopographicalSonarShaderGlobalsDTO>(0, 1);
            mapped[0] = globals;
            _shaderGlobalsBuffer.UnlockBufferAfterWrite<TopographicalSonarShaderGlobalsDTO>(1);
        }

        private unsafe bool DumpBlackBox(NativeArray<TopographicalSonarTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            try
            {
                string directory = Path.GetDirectoryName(BlackBoxDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int byteCount = UnsafeUtility.SizeOf<TopographicalSonarTelemetryEntry>() * telemetry.Length;
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[TopographicalSonar] Failed to dump topographical sonar blackbox.", this);
                return false;
            }
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

#if UNITY_EDITOR
        private static void SkipSeparators(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte value = bytes[index];
                if (value != (byte)' ' && value != (byte)'\t' && value != (byte)'\r' && value != (byte)'\n' && value != (byte)',')
                    return;
                index++;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n')
                index++;
            if (index < length)
                index++;
        }

        private static bool TryReadMaterialKey(NativeArray<byte> bytes, int length, ref int index, out int materialId)
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

        private static bool TryReadColor(NativeArray<byte> bytes, int length, ref int index, out uint packed)
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

        private static bool TryReadInt(NativeArray<byte> bytes, int length, ref int index, out int value)
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

        private static bool TryReadHexByte(NativeArray<byte> bytes, int length, ref int index, out int value)
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

            if (!TryResolveVaultBuffer(_dataVault, ref _csvScratchHandle, TopographicalSonarConstants.CsvScratchBytes, out NativeArray<byte> scratch) ||
                !TryResolveVaultBuffer(_dataVault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
            {
                return false;
            }

            int byteCount = 0;
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

            appliedRows = ParseMaterialColorCsv(scratch, byteCount, lut);
            if (appliedRows > 0)
                _lastTelemetryFlags |= TopographicalSonarConstants.CsvColorFlag;
            return appliedRows > 0;
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
            NativeArray<SonarPointDTO> points = default;
            bool hasPoints = Application.isPlaying &&
                             TryResolveVaultBuffer(_dataVault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out points) &&
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
