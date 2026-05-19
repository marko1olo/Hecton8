using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Visor;
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

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct TopographicalSonarTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int Sequence;
        [FieldOffset(8)] public int RequestedRayCount;
        [FieldOffset(12)] public int ActivePointCount;
        [FieldOffset(16)] public int HitCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float MaxDistanceMeters;
        [FieldOffset(32)] public float3 PingOriginCameraLocal;
        [FieldOffset(44)] public float3 SdfOriginRuntime;
        [FieldOffset(56)] public float SdfRangeMeters;
        [FieldOffset(60)] public float StepMeters;
        [FieldOffset(64)] public uint SdfVersion;
        [FieldOffset(68)] public uint Reserved0;
        [FieldOffset(72)] public double TimeSeconds;
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
    }

    public static class TopographicalSonarConstants
    {
        public const int MinRays = 2000;
        public const int MaxRays = 50000;
        public const int TelemetryFrames = 300;
        public const int CounterCount = 8;
        public const int ColorLutEntries = 256;
        public const int MockGridSide = 64;
        public const int MockVoxelCount = MockGridSide * MockGridSide * MockGridSide;
        public const float DefaultMaxDistanceMeters = 120f;
        public const float DefaultStepMeters = 0.85f;
        public const float MinimumStepMeters = 0.18f;
        public const uint UsedPublishedSdfFlag = 1u << 0;
        public const uint UsedMockSdfFlag = 1u << 1;
        public const uint GpuUploadFlag = 1u << 2;
        public const uint PingEventFlag = 1u << 3;
        public const uint CsvColorFlag = 1u << 4;
        public const uint FaultFlag = 1u << 31;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockSdfJob : IJobParallelFor
    {
        public NativeArray<byte> EncodedSdf;
        public NativeArray<byte> MaterialIds;
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
            float angle = math.atan2(local.z, local.x);
            float ridge = math.sin(angle * 7.0f + Seed * 0.00013f) * 4.5f +
                          math.sin((local.y + local.x) * 0.091f) * 2.0f;
            float caveRadius = math.lerp(42f, 74f, math.saturate(QualityWeight)) + ridge;
            float shell = radial - caveRadius;

            float2 pillarA = local.xz - new float2(18f, -22f);
            float2 pillarB = local.xz - new float2(-28f, 16f);
            float pillar0 = 6.0f - ApproxMagnitude(new float3(pillarA.x, 0f, pillarA.y));
            float pillar1 = 4.0f - ApproxMagnitude(new float3(pillarB.x, 0f, pillarB.y));
            float floorNoise = math.sin(local.x * 0.12f + local.z * 0.071f) * 2.75f;
            float floor = -(local.y + 18f + floorNoise);
            float ceiling = local.y - 38f + math.sin(local.x * 0.05f) * 3.0f;
            float signedDistance = math.max(math.max(shell, math.max(pillar0, pillar1)), math.max(floor, ceiling));
            signedDistance = math.clamp(signedDistance, -SdfRange, SdfRange);

            float encoded = math.saturate(signedDistance * math.rcp(SdfRange) * 0.5f + 0.5f) * 255f;
            EncodedSdf[index] = (byte)math.clamp((int)(encoded + 0.5f), 0, 255);

            byte material = 1;
            float oreMask = math.frac(math.sin(math.dot(local, new float3(12.9898f, 78.233f, 37.719f)) + Seed) * 43758.5453f);
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SonarRaymarchJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> EncodedSdf;
        [ReadOnly] public NativeArray<byte> MaterialIds;
        [ReadOnly] public NativeArray<uint> MaterialColorLut;
        public NativeArray<SonarPointDTO> Points;
        public NativeArray<byte> HitMask;

        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public int RayCount;
        public int MaxSteps;
        public double3 PingAup;
        public double3 CameraAup;
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
            float previousDistance = 0f;
            float3 previousPosition = PingRuntime;
            float previousSignedDistance = SampleSignedDistance(previousPosition, out _);
            bool hasPrevious = math.isfinite(previousSignedDistance);
            int maxSteps = math.min(MaxSteps, (int)math.ceil(MaxDistanceMeters * math.rcp(step)) + 1);
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
                    double3 hitAup = PingAup + new double3(direction.x, direction.y, direction.z) * resolvedDistance;
                    double3 cameraLocal = hitAup - CameraAup;
                    if (!IsFinite(cameraLocal))
                    {
                        WriteMiss(index);
                        return;
                    }

                    float distance01 = math.saturate(resolvedDistance * math.rcp(math.max(0.0001f, MaxDistanceMeters)));
                    uint packed = ResolvePackedColor(materialId, distance01, math.saturate(signedDistance * math.rcp(math.max(0.0001f, SdfRange))));
                    Points[index] = new SonarPointDTO
                    {
                        LocalPosition = new float3((float)cameraLocal.x, (float)cameraLocal.y, (float)cameraLocal.z),
                        ColorPacked = packed
                    };
                    HitMask[index] = 1;
                    return;
                }

                previousDistance = distance;
                previousPosition = samplePosition;
                previousSignedDistance = signedDistance;
                hasPrevious = true;
            }

            WriteMiss(index);
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
            return math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
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
            if ((uint)index < (uint)Points.Length)
                Points[index] = default;
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
            return new float3(math.cos(theta) * radius, z, math.sin(theta) * radius);
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SonarHitCountJob : IJob
    {
        [ReadOnly] public NativeArray<byte> HitMask;
        public NativeArray<int> Counters;
        public int RayCount;

        public void Execute()
        {
            int safeRayCount = math.min(math.max(0, RayCount), HitMask.IsCreated ? HitMask.Length : 0);
            int hits = 0;
            for (int i = 0; i < safeRayCount; i++)
                hits += HitMask[i] != 0 ? 1 : 0;

            if (!Counters.IsCreated || Counters.Length <= 0)
                return;

            Counters[0] = safeRayCount;
            if (Counters.Length > 1)
                Counters[1] = hits;
            if (Counters.Length > 2)
                Counters[2] = safeRayCount - hits;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DecaySonarPointsJob : IJobParallelFor
    {
        public NativeArray<SonarPointDTO> Points;
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
    public sealed class TopographicalSonarSynthesizer : MonoBehaviour, ILateFrameTickable, IRenderable, ISonarPingEventListener, IDisposable
    {
        private const string OwnerName = "SHINOBU_144";
        private const string RuntimeShaderName = "Hecton8/VFX/TopographicalSonarPoint";
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_SONAR_SYNTHESIZER.bin";

        private static readonly int SonarPointsId = Shader.PropertyToID("_SonarPoints");
        private static readonly int PointCloudLocalToWorldId = Shader.PropertyToID("_PointCloudLocalToWorld");
        private static readonly int PingSignalId = Shader.PropertyToID("_PingSignal");
        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistanceMeters");
        private static readonly int QualityId = Shader.PropertyToID("_GlobalQualityWeight");

        [Header("Dependencies")]
        [SerializeField] private Transform pingOrigin;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Mesh pointQuadMesh;
        [SerializeField] private Material pointCloudMaterial;

        [Header("Scan")]
        [SerializeField] private float maxDistanceMeters = TopographicalSonarConstants.DefaultMaxDistanceMeters;
        [SerializeField] private float stepMeters = TopographicalSonarConstants.DefaultStepMeters;
        [SerializeField] private float echoFadeSeconds = 5.5f;
        [SerializeField] private float pointSizePixels = 3.2f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.92f;
        [SerializeField, Range(-1f, 1f)] private float qualityOverride = -1f;
        [SerializeField] private bool scheduleCpuFadeJob;
        [SerializeField] private bool drawDebugRays;

        private VaultBufferHandle<SonarPointDTO> _pointsHandle;
        private VaultBufferHandle<byte> _hitMaskHandle;
        private VaultBufferHandle<int> _countersHandle;
        private VaultBufferHandle<byte> _mockSdfHandle;
        private VaultBufferHandle<byte> _mockMaterialIdsHandle;
        private VaultBufferHandle<TopographicalSonarTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<uint> _materialColorLutHandle;
        private GraphicsBuffer _pointBuffer;
        private GraphicsBuffer _argsBuffer;
        private Mesh _runtimeQuadMesh;
        private Material _runtimeMaterial;
        private Bounds _drawBounds;
        private JobHandle _scanHandle;
        private int _scanJobScheduled;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private int _registeredPingListener;
        private int _pendingPing;
        private int _activePointCount;
        private int _lastHitCount;
        private int _sequence;
        private int _telemetryWriteIndex;
        private float _pendingIntensity01 = 1f;
        private float _lastPingTimeSeconds;
        private uint _lastTelemetryFlags;
        private uint _mockSdfVersion;
        private double3 _lastPingAup;
        private double3 _lastCameraAup;
        private float3 _lastSdfOrigin;

        public static TopographicalSonarSynthesizer ActiveRuntime { get; private set; }
        public int ActivePointCount => _activePointCount;
        public int LastHitCount => _lastHitCount;
        public int Sequence => _sequence;
        public float LastQualityWeight => ResolveQualityWeight();
        public uint LastTelemetryFlags => _lastTelemetryFlags;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ActiveRuntime = this;
            AllocatePersistentState();
            EnsureGraphicsResources();
            InitializeMaterialColorLut();
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI) ? 1 : 0;
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

            if (_registeredLateFrame != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = 0;
            }

            if (_scanJobScheduled != 0)
            {
                _scanHandle.Complete();
                _scanJobScheduled = 0;
            }

            ReleaseGraphicsBuffer(ref _pointBuffer);
            ReleaseGraphicsBuffer(ref _argsBuffer);
            if (_runtimeQuadMesh != null)
            {
                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            _pointsHandle = default;
            _hitMaskHandle = default;
            _countersHandle = default;
            _mockSdfHandle = default;
            _mockMaterialIdsHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _materialColorLutHandle = default;
            _activePointCount = 0;
            _lastHitCount = 0;
            if (ReferenceEquals(ActiveRuntime, this))
                ActiveRuntime = null;
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
                if (!_scanHandle.IsCompleted)
                    return;

                _scanHandle.Complete();
                _scanJobScheduled = 0;
                CommitCompletedScan();
            }

            if (_pendingPing != 0)
            {
                _pendingPing = 0;
                ScheduleSonarScan();
                return;
            }

            if (scheduleCpuFadeJob && _activePointCount > 0)
                ScheduleAndCommitFade(Time.deltaTime);
        }

        public void Render(float deltaTime)
        {
            if (_activePointCount <= 0 || _pointBuffer == null || _argsBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            Mesh mesh = ResolveRenderMesh();
            if (material == null || mesh == null)
                return;

            float pingAge = math.max(0f, Time.time - _lastPingTimeSeconds);
            Transform cameraTransform = renderCamera != null ? renderCamera.transform : transform;
            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            material.SetBuffer(SonarPointsId, _pointBuffer);
            material.SetMatrix(PointCloudLocalToWorldId, Matrix4x4.Translate(cameraPosition));
            material.SetVector(PingSignalId, new Vector4(pingAge, math.max(0.001f, echoFadeSeconds), _pendingIntensity01, _activePointCount));
            material.SetFloat(PointSizeId, math.max(0.2f, pointSizePixels));
            material.SetFloat(OpacityId, math.saturate(opacity));
            material.SetFloat(MaxDistanceId, math.max(1f, maxDistanceMeters));
            material.SetFloat(QualityId, ResolveQualityWeight());

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = _drawBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            Graphics.RenderMeshIndirect(renderParams, mesh, _argsBuffer, 1, 0);
        }

        public void SetTuningFromEditor(float maxDistance, float step, float pointSize, float quality)
        {
            maxDistanceMeters = math.clamp(maxDistance, 4f, 400f);
            stepMeters = math.clamp(step, TopographicalSonarConstants.MinimumStepMeters, 8f);
            pointSizePixels = math.clamp(pointSize, 0.5f, 18f);
            qualityOverride = math.clamp(quality, -1f, 1f);
        }

        public bool TryApplyMaterialColorCsv(NativeArray<byte> csvBytes, out int appliedRows)
        {
            appliedRows = 0;
            if (!TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
                return false;

            appliedRows = ParseMaterialColorCsv(csvBytes, lut);
            return appliedRows > 0;
        }

        public bool TryDumpBlackBox()
        {
            return TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry) &&
                   DumpBlackBox(telemetry);
        }

        public static int ParseMaterialColorCsv(NativeArray<byte> csvBytes, NativeArray<uint> colorLut)
        {
            if (!csvBytes.IsCreated || !colorLut.IsCreated)
                return 0;

            int applied = 0;
            int index = 0;
            while (index < csvBytes.Length)
            {
                SkipSeparators(csvBytes, ref index);
                if (index >= csvBytes.Length)
                    break;

                byte current = csvBytes[index];
                if (current == (byte)'#')
                {
                    SkipLine(csvBytes, ref index);
                    continue;
                }

                if (!TryReadInt(csvBytes, ref index, out int materialId) ||
                    !TryReadInt(csvBytes, ref index, out int r) ||
                    !TryReadInt(csvBytes, ref index, out int g) ||
                    !TryReadInt(csvBytes, ref index, out int b))
                {
                    SkipLine(csvBytes, ref index);
                    continue;
                }

                int a = 255;
                int saved = index;
                if (!TryReadInt(csvBytes, ref index, out a))
                    index = saved;

                if ((uint)materialId < (uint)colorLut.Length)
                {
                    colorLut[materialId] = PackColor(r, g, b, a);
                    applied++;
                }

                SkipLine(csvBytes, ref index);
            }

            return applied;
        }

        private void AllocatePersistentState()
        {
            if (_pointsHandle.IsCreated && _pointBuffer != null && _argsBuffer != null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            _pointsHandle = vault.GetBufferHandle<SonarPointDTO>(
                TopographicalSonarBufferIds.Points,
                TopographicalSonarConstants.MaxRays,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _hitMaskHandle = vault.GetBufferHandle<byte>(
                TopographicalSonarBufferIds.HitMask,
                TopographicalSonarConstants.MaxRays,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.GetBufferHandle<int>(
                TopographicalSonarBufferIds.Counters,
                TopographicalSonarConstants.CounterCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _mockSdfHandle = vault.GetBufferHandle<byte>(
                TopographicalSonarBufferIds.MockSdf,
                TopographicalSonarConstants.MockVoxelCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _mockMaterialIdsHandle = vault.GetBufferHandle<byte>(
                TopographicalSonarBufferIds.MockMaterialIds,
                TopographicalSonarConstants.MockVoxelCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.GetBufferHandle<TopographicalSonarTelemetryEntry>(
                TopographicalSonarBufferIds.TelemetryRing,
                TopographicalSonarConstants.TelemetryFrames,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.GetBufferHandle<int>(
                TopographicalSonarBufferIds.TelemetryCursor,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _materialColorLutHandle = vault.GetBufferHandle<uint>(
                TopographicalSonarBufferIds.MaterialColorLut,
                TopographicalSonarConstants.ColorLutEntries,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
        }

        private void InitializeMaterialColorLut()
        {
            if (!TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out NativeArray<uint> lut))
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
            if (_pointBuffer == null)
                _pointBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<SonarPointDTO>(TopographicalSonarConstants.MaxRays);
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            ResolveRenderMesh();
            ResolveRenderMaterial();
            UpdateIndirectArgsBuffer(0u);
        }

        private void ScheduleSonarScan()
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
            _lastPingAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(pingRuntimeVector);
            _lastCameraAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(cameraRuntimeVector);

            NativeArray<byte> encodedSdf;
            NativeArray<byte> materialIds;
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
                    QualityWeight = ResolveQualityWeight(),
                    Seed = _mockSdfVersion * 2654435761u
                };
                dependency = mockJob.Schedule(TopographicalSonarConstants.MockVoxelCount, 128);
                encodedSdf = mockSdf;
                materialIds = mockMaterialIds;
                sdfVersion = _mockSdfVersion;
                flags |= TopographicalSonarConstants.UsedMockSdfFlag;
            }

            float quality = ResolveQualityWeight();
            int rayCount = ResolveRayCount(quality);
            float resolvedStep = ResolveStepMeters(quality);
            int maxSteps = math.min(1024, (int)math.ceil(math.max(1f, maxDistanceMeters) * math.rcp(math.max(TopographicalSonarConstants.MinimumStepMeters, resolvedStep))) + 2);

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
                PingAup = _lastPingAup,
                CameraAup = _lastCameraAup,
                PingRuntime = pingRuntime,
                MaxDistanceMeters = math.max(1f, maxDistanceMeters),
                StepMeters = resolvedStep,
                QualityWeight = quality,
                Intensity01 = math.max(0.05f, _pendingIntensity01),
                SequenceSeed = (uint)(_sequence + 1)
            };

            JobHandle rayHandle = raymarchJob.Schedule(rayCount, 128, dependency);
            SonarHitCountJob countJob = new SonarHitCountJob
            {
                HitMask = hitMask,
                Counters = counters,
                RayCount = rayCount
            };
            _scanHandle = countJob.Schedule(rayHandle);
            _scanJobScheduled = 1;
            _sequence++;
            _lastPingTimeSeconds = Time.time;
            _lastTelemetryFlags = flags;
            _lastSdfOrigin = volumeOrigin;
            _drawBounds = new Bounds(
                new Vector3(cameraRuntime.x, cameraRuntime.y, cameraRuntime.z),
                Vector3.one * math.max(16f, maxDistanceMeters * 2.25f));
        }

        private void CommitCompletedScan()
        {
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

            if (_activePointCount > 0 && _pointBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(_pointBuffer, points, _activePointCount);
                UpdateIndirectArgsBuffer((uint)_activePointCount);
                _lastTelemetryFlags |= TopographicalSonarConstants.GpuUploadFlag;
            }
            else
            {
                UpdateIndirectArgsBuffer(0u);
            }

            bool invalid = !IsFinite(_lastPingAup) || !IsFinite(_lastCameraAup) || !math.all(math.isfinite(_lastSdfOrigin));
            if (invalid)
                _lastTelemetryFlags |= TopographicalSonarConstants.FaultFlag;

            WriteTelemetry(_lastTelemetryFlags);
            if (invalid && TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry))
                DumpBlackBox(telemetry);
        }

        private void ScheduleAndCommitFade(float deltaTime)
        {
            if (!TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out NativeArray<SonarPointDTO> points))
                return;

            DecaySonarPointsJob fadeJob = new DecaySonarPointsJob
            {
                Points = points,
                ActivePointCount = _activePointCount,
                DeltaTime = math.max(0f, deltaTime),
                FadePerSecond = echoFadeSeconds > 0.001f ? math.rcp(echoFadeSeconds) : 1f
            };
            fadeJob.Schedule(_activePointCount, 128).Complete();
            if (_pointBuffer != null)
                GraphicsBufferUploadUtility.UploadNativeArray(_pointBuffer, points, _activePointCount);
        }

        private void WriteTelemetry(uint flags)
        {
            if (!TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _telemetryRingHandle, TopographicalSonarConstants.TelemetryFrames, out NativeArray<TopographicalSonarTelemetryEntry> telemetry))
                return;

            int index = _telemetryWriteIndex % TopographicalSonarConstants.TelemetryFrames;
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % TopographicalSonarConstants.TelemetryFrames;
            if (TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _telemetryCursorHandle, 1, out NativeArray<int> cursor))
                cursor[0] = _telemetryWriteIndex;

            float3 pingCameraLocal = new float3(
                (float)(_lastPingAup.x - _lastCameraAup.x),
                (float)(_lastPingAup.y - _lastCameraAup.y),
                (float)(_lastPingAup.z - _lastCameraAup.z));
            float quality = ResolveQualityWeight();
            telemetry[index] = new TopographicalSonarTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                Sequence = _sequence,
                RequestedRayCount = ResolveRayCount(quality),
                ActivePointCount = _activePointCount,
                HitCount = _lastHitCount,
                Flags = flags,
                GlobalQualityWeight = quality,
                MaxDistanceMeters = math.max(1f, maxDistanceMeters),
                PingOriginCameraLocal = pingCameraLocal,
                SdfOriginRuntime = _lastSdfOrigin,
                SdfRangeMeters = ResolveMockSdfRange(),
                StepMeters = ResolveStepMeters(quality),
                SdfVersion = _mockSdfVersion,
                TimeSeconds = Time.realtimeSinceStartupAsDouble
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
            IDataVault vault = GlobalRegistry.DataVault;
            bool resolvedPoints = TryResolveVaultBuffer(vault, ref _pointsHandle, TopographicalSonarConstants.MaxRays, out points);
            bool resolvedHitMask = TryResolveVaultBuffer(vault, ref _hitMaskHandle, TopographicalSonarConstants.MaxRays, out hitMask);
            bool resolvedCounters = TryResolveVaultBuffer(vault, ref _countersHandle, TopographicalSonarConstants.CounterCount, out counters);
            bool resolvedMockSdf = TryResolveVaultBuffer(vault, ref _mockSdfHandle, TopographicalSonarConstants.MockVoxelCount, out mockSdf);
            bool resolvedMockMaterials = TryResolveVaultBuffer(vault, ref _mockMaterialIdsHandle, TopographicalSonarConstants.MockVoxelCount, out mockMaterialIds);
            bool resolvedColorLut = TryResolveVaultBuffer(vault, ref _materialColorLutHandle, TopographicalSonarConstants.ColorLutEntries, out colorLut);
            return resolvedPoints && resolvedHitMask && resolvedCounters && resolvedMockSdf && resolvedMockMaterials && resolvedColorLut;
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultBufferHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !handle.IsCreated)
                return false;

            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool TryResolvePublishedSdf(
            float3 pingRuntime,
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> materialIds,
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
                    out NativeArray<byte> payload,
                    out NativeArray<byte> materialPayload,
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

        private Material ResolveRenderMaterial()
        {
            if (pointCloudMaterial != null)
                return pointCloudMaterial;

            if (_runtimeMaterial != null)
                return _runtimeMaterial;

            Shader shader = Shader.Find(RuntimeShaderName);
            if (shader == null)
                return null;

            _runtimeMaterial = new Material(shader)
            {
                name = "TopographicalSonarPointCloudRuntime",
                hideFlags = HideFlags.DontSave
            };
            return _runtimeMaterial;
        }

        private Mesh ResolveRenderMesh()
        {
            if (pointQuadMesh != null)
                return pointQuadMesh;
            if (_runtimeQuadMesh != null)
                return _runtimeQuadMesh;

            _runtimeQuadMesh = new Mesh
            {
                name = "TopographicalSonarPointQuad",
                hideFlags = HideFlags.DontSave
            };
            _runtimeQuadMesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, 1f, 0f)
            };
            _runtimeQuadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _runtimeQuadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _runtimeQuadMesh.RecalculateBounds();
            return _runtimeQuadMesh;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_argsBuffer == null)
                return;

            Mesh mesh = ResolveRenderMesh();
            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh != null ? mesh.GetIndexCount(0) : 0u,
                instanceCount = instanceCount,
                startIndex = mesh != null ? mesh.GetIndexStart(0) : 0u,
                baseVertexIndex = mesh != null ? (uint)math.max(0, mesh.GetBaseVertex(0)) : 0u,
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
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
                byte[] managedBytes = new byte[byteCount];
                fixed (byte* destination = managedBytes)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(destination, source, byteCount);
                }

                File.WriteAllBytes(BlackBoxDumpPath, managedBytes);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[" + OwnerName + "] Failed to dump topographical sonar blackbox: " + exception.Message, this);
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

        private static void SkipSeparators(NativeArray<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte value = bytes[index];
                if (value != (byte)' ' && value != (byte)'\t' && value != (byte)'\r' && value != (byte)'\n' && value != (byte)',')
                    return;
                index++;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, ref int index)
        {
            while (index < bytes.Length && bytes[index] != (byte)'\n')
                index++;
            if (index < bytes.Length)
                index++;
        }

        private static bool TryReadInt(NativeArray<byte> bytes, ref int index, out int value)
        {
            value = 0;
            SkipSeparators(bytes, ref index);
            if (index >= bytes.Length)
                return false;

            int sign = 1;
            if (bytes[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            int result = 0;
            int digits = 0;
            while (index < bytes.Length)
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

#if UNITY_EDITOR
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
            for (int i = 0; i < count; i++)
            {
                float k = i + 0.5f;
                float z = 1f - 2f * k / count;
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float theta = k * 2.39996323f;
                Vector3 direction = new Vector3(Mathf.Cos(theta) * radius, z, Mathf.Sin(theta) * radius);
                Gizmos.DrawLine(origin, origin + direction * Mathf.Min(maxDistanceMeters, 16f));
            }
        }
#endif
    }
}
