using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Lighting
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CustomLightProbeDTO
    {
        [FieldOffset(0)] public ulong SpatialHash64;
        [FieldOffset(8)] public uint PackedGridCoord;
        [FieldOffset(12)] public uint Flags;

        [FieldOffset(16)] public float4 Lane0;
        [FieldOffset(32)] public float4 Lane1;
        [FieldOffset(48)] public float4 Lane2;
        [FieldOffset(64)] public float4 Lane3;
        [FieldOffset(80)] public float4 Lane4;
        [FieldOffset(96)] public float4 Lane5;
        [FieldOffset(112)] public float4 Lane6;

        [FieldOffset(16)] public float R0;
        [FieldOffset(20)] public float R1;
        [FieldOffset(24)] public float R2;
        [FieldOffset(28)] public float R3;
        [FieldOffset(32)] public float R4;
        [FieldOffset(36)] public float R5;
        [FieldOffset(40)] public float R6;
        [FieldOffset(44)] public float R7;
        [FieldOffset(48)] public float R8;
        [FieldOffset(52)] public float G0;
        [FieldOffset(56)] public float G1;
        [FieldOffset(60)] public float G2;
        [FieldOffset(64)] public float G3;
        [FieldOffset(68)] public float G4;
        [FieldOffset(72)] public float G5;
        [FieldOffset(76)] public float G6;
        [FieldOffset(80)] public float G7;
        [FieldOffset(84)] public float G8;
        [FieldOffset(88)] public float B0;
        [FieldOffset(92)] public float B1;
        [FieldOffset(96)] public float B2;
        [FieldOffset(100)] public float B3;
        [FieldOffset(104)] public float B4;
        [FieldOffset(108)] public float B5;
        [FieldOffset(112)] public float B6;
        [FieldOffset(116)] public float B7;
        [FieldOffset(120)] public float B8;
        [FieldOffset(124)] public float Spare0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct InteriorGISourceDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Color;
        [FieldOffset(36)] public float Intensity;
        [FieldOffset(40)] public float RadiusMeters;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public float ConeCos;
        [FieldOffset(64)] public uint SourceHash;
        [FieldOffset(68)] public float Phase01;
        [FieldOffset(72)] public float WaterAbsorptionScalar;
        [FieldOffset(76)] public float FloraPulse01;
        [FieldOffset(80)] public float PowerScale01;
        [FieldOffset(84)] public uint RoomHash;
        [FieldOffset(88)] public uint _pad0;
        [FieldOffset(92)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InteriorGIOcclusionCellDTO
    {
        [FieldOffset(0)] public float SignedDistanceMeters;
        [FieldOffset(4)] public float Water01;
        [FieldOffset(8)] public float TransferScale01;
        [FieldOffset(12)] public uint WallMask;
        [FieldOffset(16)] public float FloraGlow01;
        [FieldOffset(20)] public float EmergencyReflectance01;
        [FieldOffset(24)] public uint RoomHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct InteriorGITuningDTO
    {
        [FieldOffset(0)] public double3 RootAup;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float PropagationSpeed;
        [FieldOffset(36)] public float WallAbsorption;
        [FieldOffset(40)] public float EmergencyLightIntensity;
        [FieldOffset(44)] public float WaterAbsorption;
        [FieldOffset(48)] public float FlashlightIntensity;
        [FieldOffset(52)] public float FloraGlowScale;
        [FieldOffset(56)] public float SimulationDelta;
        [FieldOffset(60)] public float DirectionalWeight;
        [FieldOffset(64)] public float L2Weight;
        [FieldOffset(68)] public float EmergencyOverride01;
        [FieldOffset(72)] public float GridDecimation01;
        [FieldOffset(76)] public int Resolution;
        [FieldOffset(80)] public int ActiveProbeCount;
        [FieldOffset(84)] public int SourceCount;
        [FieldOffset(88)] public int SourceSampleLimit;
        [FieldOffset(92)] public int FrameIndex;
        [FieldOffset(96)] public uint Flags;
        [FieldOffset(100)] public uint RootHash;
        [FieldOffset(104)] public float RedOverride01;
        [FieldOffset(108)] public float UploadCadenceSeconds;
        [FieldOffset(112)] public float AmbientRetain;
        [FieldOffset(116)] public float TransferDamping;
        [FieldOffset(120)] public int PropagationIterations;
        [FieldOffset(124)] public uint PackedBiomeTint;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockPowerState
    {
        [FieldOffset(0)] public float MainPower01;
        [FieldOffset(4)] public float Emergency01;
        [FieldOffset(8)] public float DoorOpen01;
        [FieldOffset(12)] public float OutagePhase01;
        [FieldOffset(16)] public int FrameIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint SourceMask;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InteriorGITelemetryEntry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public int ActiveProbeCount;
        [FieldOffset(8)] public int SourceCount;
        [FieldOffset(12)] public int SourceSampleLimit;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float SolverCompleteMs;
        [FieldOffset(24)] public float MaxL0;
        [FieldOffset(28)] public float AverageL0;
        [FieldOffset(32)] public int NaNCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint GridHash;
        [FieldOffset(44)] public uint RootHash;
        [FieldOffset(48)] public float WaterAbsorption;
        [FieldOffset(52)] public float DirectionalWeight;
        [FieldOffset(56)] public float BouncesEstimated;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CustomDynamicProbeLightDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Color;
        [FieldOffset(36)] public float Intensity;
        [FieldOffset(40)] public float RadiusMeters;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AmbientLightingProfileDTO
    {
        [FieldOffset(0)] public ulong ProfileHash64;
        [FieldOffset(8)] public uint ProfileId;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float3 L0Color;
        [FieldOffset(28)] public float DirectionalWeight;
        [FieldOffset(32)] public float3 BiomeTint;
        [FieldOffset(44)] public float L2Weight;
        [FieldOffset(48)] public float3 WaterAbsorption;
        [FieldOffset(60)] public uint _pad0;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("HECTON-8/Lighting/Interior GI Probe Volume Runtime")]
    public sealed unsafe class InteriorGIProbeVolumeRuntime : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        public const int MaxResolution = 32;
        public const int MinResolution = 12;
        public const int MaxCellCount = MaxResolution * MaxResolution * MaxResolution;
        public const int MaxSourceCount = 128;
        public const int MaxAmbientProfileCount = 64;
        public const int TelemetryCapacity = 300;
        public const int CsvBufferBytes = 32768;
        public const int CustomLightProbeDtoSizeBytes = 128;
        public const int InteriorGISourceDtoSizeBytes = 96;
        public const int InteriorGIOcclusionCellDtoSizeBytes = 32;
        public const int InteriorGITuningDtoSizeBytes = 128;
        public const int MockPowerStateSizeBytes = 32;
        public const int InteriorGITelemetryEntrySizeBytes = 64;
        public const int CustomDynamicProbeLightDtoSizeBytes = 64;
        public const int AmbientLightingProfileDtoSizeBytes = 64;
        private const int TelemetryDumpHeaderBytes = 40;

        public const uint SourceFlagPowered = 1u << 0;
        public const uint SourceFlagEmergency = 1u << 1;
        public const uint SourceFlagFlashlight = 1u << 2;
        public const uint SourceFlagFlora = 1u << 3;
        public const uint SourceFlagAlwaysOn = 1u << 4;

        public const uint WallNegX = 1u << 0;
        public const uint WallPosX = 1u << 1;
        public const uint WallNegY = 1u << 2;
        public const uint WallPosY = 1u << 3;
        public const uint WallNegZ = 1u << 4;
        public const uint WallPosZ = 1u << 5;
        public const uint OcclusionFlagSolid = 1u << 6;
        public const uint TelemetryFlagNan = 1u << 0;
        public const uint TelemetryFlagEmergency = 1u << 1;
        public const uint TelemetryFlagMock = 1u << 2;

        public static bool ValidateStructLayouts(out uint failureMask)
        {
            failureMask = 0u;
            ValidateLayoutSize<CustomLightProbeDTO>(CustomLightProbeDtoSizeBytes, 1u << 0, ref failureMask);
            ValidateLayoutSize<InteriorGISourceDTO>(InteriorGISourceDtoSizeBytes, 1u << 1, ref failureMask);
            ValidateLayoutSize<InteriorGIOcclusionCellDTO>(InteriorGIOcclusionCellDtoSizeBytes, 1u << 2, ref failureMask);
            ValidateLayoutSize<InteriorGITuningDTO>(InteriorGITuningDtoSizeBytes, 1u << 3, ref failureMask);
            ValidateLayoutSize<MockPowerState>(MockPowerStateSizeBytes, 1u << 4, ref failureMask);
            ValidateLayoutSize<InteriorGITelemetryEntry>(InteriorGITelemetryEntrySizeBytes, 1u << 5, ref failureMask);
            ValidateLayoutSize<CustomDynamicProbeLightDTO>(CustomDynamicProbeLightDtoSizeBytes, 1u << 6, ref failureMask);
            ValidateLayoutSize<AmbientLightingProfileDTO>(AmbientLightingProfileDtoSizeBytes, 1u << 7, ref failureMask);
            return failureMask == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateLayoutSize<T>(int expectedBytes, uint bit, ref uint failureMask) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes || (actualBytes & 7) != 0)
                failureMask |= bit;
        }

        private const SystemID MemoryOwner = SystemID.GraphicsScalability;
        private const BufferID ProbeFrontBuffer = (BufferID)0x630800;
        private const BufferID ProbeBackBuffer = (BufferID)0x630801;
        private const BufferID ProbeSourcesBuffer = (BufferID)0x630802;
        private const BufferID ProbeOcclusionBuffer = (BufferID)0x630803;
        private const BufferID ProbeTuningBuffer = (BufferID)0x630804;
        private const BufferID ProbeTelemetryRingBuffer = (BufferID)0x630805;
        private const BufferID ProbeTelemetryScratchBuffer = (BufferID)0x630806;
        private const BufferID ProbeMockPowerBuffer = (BufferID)0x630808;
        private const BufferID ProbeFaultBuffer = (BufferID)0x630809;
        private const BufferID ProbeCsvBytesBuffer = (BufferID)0x63080A;
        private const BufferID ProbeAmbientProfileBuffer = (BufferID)0x63080B;
        private const BufferID ProbeAmbientProfileCountBuffer = (BufferID)0x63080C;

        private static readonly int InteriorGIProbeBufferId = Shader.PropertyToID("_H8CustomLightProbeGrid");
        private static readonly int InteriorGIParamsId = Shader.PropertyToID("_H8InteriorGIProbeParams");
        private static readonly int InteriorGIOriginId = Shader.PropertyToID("_H8InteriorGIProbeOrigin");
        private static readonly int InteriorGIRootAupId = Shader.PropertyToID("_H8InteriorGIProbeRootAup");
        private static readonly int InteriorGIGpuStateId = Shader.PropertyToID("_H8CustomLightProbeGridState");

        [Header("Grid")]
        [SerializeField, Min(1f)] private float cellSizeMeters = 3.5f;
        [SerializeField, Range(MinResolution, MaxResolution)] private int editorPreviewResolution = 24;
        [SerializeField] private bool forceEditorResolution;
        [SerializeField] private bool enableMockLighting = true;
        [SerializeField] private bool enableMockOcclusion = true;
        [SerializeField] private bool enableGpuUpload = true;
        [SerializeField] private bool enableCsvOverridePolling;

        [Header("Propagation")]
        [SerializeField, Range(0.05f, 4f)] private float propagationSpeed = 0.9f;
        [SerializeField, Range(0f, 1f)] private float wallAbsorption = 1f;
        [SerializeField, Range(0f, 8f)] private float emergencyLightIntensity = 2.4f;
        [SerializeField, Range(0f, 1f)] private float waterAbsorption = 0.8f;
        [SerializeField, Range(0f, 8f)] private float flashlightIntensity = 2.2f;
        [SerializeField, Range(0f, 6f)] private float floraGlowScale = 1.25f;
        [SerializeField, Range(0f, 1f)] private float emergencyOverride01;
        [SerializeField, Range(-1f, 1f)] private float forceQualityWeight = -1f;

        [Header("Diagnostics")]
        [SerializeField] private bool drawProbeGizmos;
        [SerializeField, Range(32, 4096)] private int maxEditorGizmoProbes = 512;
        [SerializeField] private string csvOverrideRelativePath = "Docs/lighting_fixtures.csv";
        [SerializeField] private string ambientProfileCsvRelativePath = "Docs/Data/Profiles/ambient_lighting_profiles.csv";

        private IDataVault _vault;
        private Transform _cachedTransform;
        private double3 _rootAup;
        private int _activeResolution = 16;
        private int _sourceCount;
        private int _gridVersion;
        private int _telemetryCursor;
        private float _solverAccumulator;
        private float _csvPollTimer;
        private float _visualUploadAccumulator;
        private float _lastCompleteMs;
        private uint _rootHash;
        private uint _biomeHash;
        private float3 _biomeTint = new float3(0.08f, 0.64f, 0.82f);
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;
        private bool _registeredHotSwapListener;
        private bool _nativeReady;
        private bool _mockSourcesSeeded;
        private bool _mockOcclusionSeeded;
        private bool _visualDirty;
        private bool _simulationJobActive;
        private bool _scheduledFinalBufferIsBack = true;
        private bool _scheduledBootClear;
        private bool _gridClearRequested;
        private bool _scheduledGridClear;
        private bool _nanDumpWritten;
        private bool _csvReloadRequested;
        private JobHandle _simulationHandle;
        private JobHandle _gpuUploadHandle;
        private GraphicsBuffer _probeBufferA;
        private GraphicsBuffer _probeBufferB;
        private int _gpuProbeCapacity;
        private int _gpuProbeWriteIndex;
        private int _gpuProbePublishedCount;
        private int _gpuUploadPendingBufferIndex = -1;
        private int _gpuUploadPendingCount;
        private int _gpuUploadPendingFrame = -1;
        private bool _gpuUploadPending;
        private Vector4 _gpuUploadPendingParams;
        private Vector4 _gpuUploadPendingOrigin;
        private Vector4 _gpuUploadPendingRootAup;
        private Vector4 _gpuUploadPendingState;
        private VaultGenerationHandle<CustomLightProbeDTO> _probeFront;
        private VaultGenerationHandle<CustomLightProbeDTO> _probeBack;
        private VaultGenerationHandle<InteriorGISourceDTO> _sources;
        private VaultGenerationHandle<InteriorGIOcclusionCellDTO> _occlusion;
        private VaultGenerationHandle<InteriorGITuningDTO> _tuning;
        private VaultGenerationHandle<InteriorGITelemetryEntry> _telemetryRing;
        private VaultGenerationHandle<InteriorGITelemetryEntry> _telemetryScratch;
        private VaultGenerationHandle<MockPowerState> _mockPower;
        private VaultGenerationHandle<int> _faults;
        private VaultGenerationHandle<byte> _csvBytes;
        private VaultGenerationHandle<AmbientLightingProfileDTO> _ambientProfiles;
        private VaultGenerationHandle<int> _ambientProfileCount;

        private int ActiveCellCount => _activeResolution * _activeResolution * _activeResolution;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            CacheDependencies();
            TryRegisterHotSwapListener();
            EnsureNativeState();
            if (_nativeReady && enableGpuUpload)
                EnsureGpuBuffersCold(MaxCellCount);
#if UNITY_EDITOR
            if (_nativeReady && enableCsvOverridePolling)
            {
                _csvReloadRequested = true;
                TryReloadCsvOverrides();
            }
#endif
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ReleaseRuntimeState(blockingComplete: false);
        }

        private void OnDestroy()
        {
            ReleaseRuntimeState(blockingComplete: true);
            ReleaseGpuBuffers();
        }

        public void Tick(float deltaTime)
        {
            EnsureNativeState(allowAllocation: false);
            if (!_nativeReady)
                return;

            if (_simulationJobActive)
                return;

            float quality = ResolveQualityWeight();
            ResolveActiveResolution(quality);
            UpdateBiomeTintFromSignals();
            float cadence = ResolveCadenceSeconds(quality);
            float safeDelta = math.max(0f, deltaTime);
            _visualUploadAccumulator += safeDelta;
            if (_gridClearRequested)
            {
                InteriorGITuningDTO clearTuning = BuildTuning(quality, 0f, cadence);
                if (!TryWriteTuning(clearTuning))
                    return;
                _visualUploadAccumulator = math.max(_visualUploadAccumulator, math.max(0.05f, cadence));
                ScheduleGridClear();
                return;
            }

            if (enableMockLighting)
                EnsureMockSources();
            if (enableMockOcclusion)
                EnsureMockOcclusionGrid();

            _solverAccumulator += safeDelta;
            if (_solverAccumulator < cadence)
                return;

            float dt = math.min(_solverAccumulator, 0.5f);
            _solverAccumulator = 0f;
            InteriorGITuningDTO tuning = BuildTuning(quality, dt, cadence);
            if (!TryWriteTuning(tuning))
                return;
            ScheduleSimulation(tuning);
        }

        public void SlowTick()
        {
            _gpuUploadPending &= !enableGpuUpload || HasGpuBuffersReady(MaxCellCount);

#if UNITY_EDITOR
            if (!_nativeReady || _scheduledBootClear || _simulationJobActive)
                return;

            if (!enableCsvOverridePolling)
                return;

            _csvReloadRequested = false;
#endif
        }

        public void LateFrameTick()
        {
            TryPublishCompletedGpuUpload();

            if (!_simulationJobActive)
            {
                TryStartGpuUploadIfDirty();
                return;
            }

            long start = Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;
            long end = Stopwatch.GetTimestamp();
            _lastCompleteMs = (float)((end - start) * 1000.0 / Stopwatch.Frequency);
            _simulationJobActive = false;

            if (_scheduledGridClear)
            {
                _scheduledGridClear = false;
                _visualDirty = true;
                _gridVersion++;
                TryStartGpuUploadIfDirty();
                return;
            }

            if (_scheduledBootClear)
            {
                _scheduledBootClear = false;
                _visualDirty = true;
                _gridVersion++;
                TryStartGpuUploadIfDirty();
                return;
            }

            if (_scheduledFinalBufferIsBack)
                SwapFrontBack();
            else
                _visualDirty = true;
            _gridVersion++;
            CommitTelemetryScratch();
            TryStartGpuUploadIfDirty();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            if (_cachedTransform == null)
                _cachedTransform = transform;

            Vector3 runtimePosition = _cachedTransform.position;
            if (!MathGuard.IsFinite(runtimePosition))
                return;

            double3 shiftedRootAup = shiftData.NewTotalOffsetDouble + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(shiftedRootAup)))
                return;

            _rootAup = shiftedRootAup;
            _rootHash = HashAup(_rootAup);

            _visualDirty = true;
        }

        public bool TryGetProbeGridReadback(out NativeArray<CustomLightProbeDTO>.ReadOnly probes, out int resolution, out double3 rootAup, out float cellSize, out int version)
        {
            probes = default;
            resolution = _activeResolution;
            rootAup = _rootAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;
            if (_scheduledBootClear ||
                _simulationJobActive ||
                !TryReadOnlyArray(in _probeFront, ProbeFrontBuffer, MaxCellCount, out probes))
            {
                return false;
            }

            return probes.Length > 0;
        }

        public bool TryGetOcclusionReadback(out NativeArray<InteriorGIOcclusionCellDTO>.ReadOnly occlusion, out int resolution)
        {
            occlusion = default;
            resolution = _activeResolution;
            if (_scheduledBootClear ||
                _simulationJobActive ||
                !TryReadOnlyArray(in _occlusion, ProbeOcclusionBuffer, MaxCellCount, out occlusion))
            {
                return false;
            }

            return occlusion.Length > 0;
        }

        public bool TryGetTelemetryReadback(out NativeArray<InteriorGITelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = _telemetryCursor;
            if (_simulationJobActive ||
                !TryReadOnlyArray(in _telemetryRing, ProbeTelemetryRingBuffer, TelemetryCapacity, out telemetry))
            {
                return false;
            }

            return telemetry.Length > 0;
        }

        public bool TryGetTuningCopy(out InteriorGITuningDTO tuning)
        {
            tuning = default;
            if (!TryReadTuning(out tuning))
                return false;

            return true;
        }

        public bool TryWriteOcclusionCell(int3 cell, float signedDistanceMeters, uint wallMask, float water01, float transferScale01, float floraGlow01, uint roomHash)
        {
            if (_simulationJobActive || !IsInside(cell, _activeResolution))
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !HasInteriorGIHandle(in _occlusion, ProbeOcclusionBuffer))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _occlusion, MemoryOwner, out NativeArray<InteriorGIOcclusionCellDTO> occlusion))
                return false;

            int index = ToIndex(cell, _activeResolution);
            float safeSignedDistance = math.isfinite(signedDistanceMeters) ? signedDistanceMeters : cellSizeMeters;
            InteriorGIOcclusionCellDTO requestedCell = new InteriorGIOcclusionCellDTO
            {
                SignedDistanceMeters = safeSignedDistance,
                Water01 = math.saturate(water01),
                TransferScale01 = math.saturate(transferScale01),
                WallMask = wallMask,
                FloraGlow01 = math.saturate(floraGlow01),
                EmergencyReflectance01 = 0.2f,
                RoomHash = roomHash,
                Flags = safeSignedDistance <= 0f ? OcclusionFlagSolid : 0u
            };

            try
            {
                if (!occlusion.IsCreated || occlusion.Length <= index)
                    return false;

                occlusion[index] = requestedCell;
                _visualDirty = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _occlusion, MemoryOwner);
            }
        }

        public bool TryUpsertSource(uint sourceHash, double3 aup, float3 color, float intensity, float radiusMeters, uint flags, float3 direction)
        {
            if (sourceHash == 0u || _simulationJobActive || !math.all(math.isfinite(aup)))
                return false;

            float safeIntensity = math.max(0f, math.isfinite(intensity) ? intensity : 0f);
            float safeRadius = math.max(0.25f, math.isfinite(radiusMeters) ? radiusMeters : 1f);
            float3 safeColor = math.select(new float3(1f, 0.2f, 0.1f), math.max(new float3(0f), color), math.all(math.isfinite(color)));
            float3 safeDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !HasInteriorGIHandle(in _sources, ProbeSourcesBuffer))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _sources, MemoryOwner, out NativeArray<InteriorGISourceDTO> sources))
                return false;

            try
            {
                if (!sources.IsCreated || sources.Length < MaxSourceCount)
                    return false;

                int sourceCount = math.min(_sourceCount, MaxSourceCount);
                for (int i = 0; i < sourceCount; i++)
                {
                    if (sources[i].SourceHash != sourceHash)
                        continue;

                    sources[i] = BuildSource(sourceHash, aup, safeColor, safeIntensity, safeRadius, flags, safeDirection, i);
                    return true;
                }

                if (_sourceCount >= MaxSourceCount)
                    return false;

                sources[_sourceCount] = BuildSource(sourceHash, aup, safeColor, safeIntensity, safeRadius, flags, safeDirection, _sourceCount);
                _sourceCount++;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _sources, MemoryOwner);
            }
        }

        public void RequestCsvReload()
        {
#if UNITY_EDITOR
            _csvReloadRequested = true;
            TryReloadCsvOverrides();
#endif
        }

        public void RequestAmbientProfileCsvReload()
        {
#if UNITY_EDITOR
            TryReloadAmbientProfileCsv();
#endif
        }

        public void DumpBlackBoxNow()
        {
            DumpTelemetryRing();
        }

        public void SetEditorForceQuality(float value)
        {
            forceQualityWeight = math.clamp(value, -1f, 1f);
        }

        public void SetEditorEmergencyOverride(float value)
        {
            emergencyOverride01 = math.saturate(value);
            if (TryReadTuning(out InteriorGITuningDTO tuning) && !_simulationJobActive)
            {
                tuning.EmergencyOverride01 = emergencyOverride01;
                tuning.RedOverride01 = emergencyOverride01;
                TryWriteTuning(tuning);
            }
        }

        public void SetEditorPropagationSpeed(float value)
        {
            propagationSpeed = math.clamp(value, 0.05f, 4f);
            if (TryReadTuning(out InteriorGITuningDTO tuning) && !_simulationJobActive)
            {
                tuning.PropagationSpeed = propagationSpeed;
                TryWriteTuning(tuning);
            }
        }

        public void SetEditorWallAbsorption(float value)
        {
            wallAbsorption = math.saturate(value);
            if (TryReadTuning(out InteriorGITuningDTO tuning) && !_simulationJobActive)
            {
                tuning.WallAbsorption = wallAbsorption;
                TryWriteTuning(tuning);
            }
        }

        public void SetEditorEmergencyLightIntensity(float value)
        {
            emergencyLightIntensity = math.max(0f, value);
            if (TryReadTuning(out InteriorGITuningDTO tuning) && !_simulationJobActive)
            {
                tuning.EmergencyLightIntensity = emergencyLightIntensity;
                TryWriteTuning(tuning);
            }
        }

        public void SetEditorWaterAbsorption(float value)
        {
            waterAbsorption = math.saturate(value);
            if (TryReadTuning(out InteriorGITuningDTO tuning) && !_simulationJobActive)
            {
                tuning.WaterAbsorption = waterAbsorption;
                TryWriteTuning(tuning);
            }
        }

        public bool ShouldDrawProbeGizmos()
        {
            return drawProbeGizmos;
        }

        public int GetMaxEditorGizmoProbes()
        {
            return math.max(32, maxEditorGizmoProbes);
        }

        private void EnsureNativeState(bool allowAllocation = true)
        {
            if (_nativeReady)
                return;

            if (_vault == null)
                return;

            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (!TryResolveAbsoluteAupFromRuntimeOrigin(_cachedTransform.position, out _rootAup))
                _rootAup = double3.zero;

            _rootHash = HashAup(_rootAup);
            _activeResolution = ResolveResolutionFromQuality(ResolveQualityWeight());

            _probeFront = AcquireBuffer<CustomLightProbeDTO>(ProbeFrontBuffer, MaxCellCount, allowAllocation);
            _probeBack = AcquireBuffer<CustomLightProbeDTO>(ProbeBackBuffer, MaxCellCount, allowAllocation);
            _sources = AcquireBuffer<InteriorGISourceDTO>(ProbeSourcesBuffer, MaxSourceCount, allowAllocation);
            _occlusion = AcquireBuffer<InteriorGIOcclusionCellDTO>(ProbeOcclusionBuffer, MaxCellCount, allowAllocation);
            _tuning = AcquireBuffer<InteriorGITuningDTO>(ProbeTuningBuffer, 1, allowAllocation);
            _telemetryRing = AcquireBuffer<InteriorGITelemetryEntry>(ProbeTelemetryRingBuffer, TelemetryCapacity, allowAllocation);
            _telemetryScratch = AcquireBuffer<InteriorGITelemetryEntry>(ProbeTelemetryScratchBuffer, 1, allowAllocation);
            _mockPower = AcquireBuffer<MockPowerState>(ProbeMockPowerBuffer, 1, allowAllocation);
            _faults = AcquireBuffer<int>(ProbeFaultBuffer, MaxCellCount, allowAllocation);
            _csvBytes = AcquireBuffer<byte>(ProbeCsvBytesBuffer, CsvBufferBytes, allowAllocation);
            _ambientProfiles = AcquireBuffer<AmbientLightingProfileDTO>(ProbeAmbientProfileBuffer, MaxAmbientProfileCount, allowAllocation);
            _ambientProfileCount = AcquireBuffer<int>(ProbeAmbientProfileCountBuffer, 1, allowAllocation);

            if (!HasRequiredNativeBuffers())
            {
                _nativeReady = false;
                return;
            }

            float bootQuality = ResolveQualityWeight();
            float bootCadence = ResolveCadenceSeconds(bootQuality);
            InteriorGITuningDTO tuning = BuildTuning(bootQuality, bootCadence, bootCadence);
            if (!TryWriteTuning(tuning))
            {
                _nativeReady = false;
                return;
            }

            _nativeReady = true;
            _mockSourcesSeeded = false;
            _mockOcclusionSeeded = false;
            _visualDirty = true;
            _visualUploadAccumulator = math.max(_visualUploadAccumulator, math.max(0.05f, tuning.UploadCadenceSeconds));
            ScheduleBootClearJob(tuning);
        }

        private bool HasRequiredNativeBuffers()
        {
            return ResolveArray(ref _probeFront, ProbeFrontBuffer, MaxCellCount).IsCreated &&
                   ResolveArray(ref _probeBack, ProbeBackBuffer, MaxCellCount).IsCreated &&
                   ResolveArray(ref _sources, ProbeSourcesBuffer, MaxSourceCount).IsCreated &&
                   ResolveArray(ref _occlusion, ProbeOcclusionBuffer, MaxCellCount).IsCreated &&
                   ResolveArray(ref _tuning, ProbeTuningBuffer, 1).IsCreated &&
                   ResolveArray(ref _telemetryRing, ProbeTelemetryRingBuffer, TelemetryCapacity).IsCreated &&
                   ResolveArray(ref _telemetryScratch, ProbeTelemetryScratchBuffer, 1).IsCreated &&
                   ResolveArray(ref _mockPower, ProbeMockPowerBuffer, 1).IsCreated &&
                   ResolveArray(ref _faults, ProbeFaultBuffer, MaxCellCount).IsCreated &&
                   ResolveArray(ref _csvBytes, ProbeCsvBytesBuffer, CsvBufferBytes).IsCreated &&
                   ResolveArray(ref _ambientProfiles, ProbeAmbientProfileBuffer, MaxAmbientProfileCount).IsCreated &&
                   ResolveArray(ref _ambientProfileCount, ProbeAmbientProfileCountBuffer, 1).IsCreated;
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(BufferID bufferId, int length, bool allowAllocation) where T : struct
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return default;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                IsInteriorGIHandle(in existingHandle, bufferId) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= length)
            {
                return existingHandle;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                MemoryOwner,
                NativeArrayOptions.UninitializedMemory);
            if (!IsInteriorGIHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < length)
            {
                return default;
            }

            return handle;
        }

        private void ScheduleBootClearJob(InteriorGITuningDTO tuning)
        {
            if (_simulationJobActive)
                return;

            InteriorGIClearStateJob clearJob = new InteriorGIClearStateJob
            {
                ProbeFront = ResolveProbeFront(),
                ProbeBack = ResolveProbeBack(),
                Sources = ResolveSources(),
                Occlusion = ResolveOcclusion(),
                TelemetryRing = ResolveTelemetryRing(),
                TelemetryScratch = ResolveTelemetryScratch(),
                Power = ResolveMockPower(),
                Faults = ResolveFaults(),
                CsvBytes = ResolveCsvBytes(),
                AmbientProfiles = ResolveAmbientProfiles(),
                AmbientProfileCount = ResolveAmbientProfileCount()
            };
            JobHandle handle = clearJob.Schedule();
            if (enableMockLighting)
            {
                GenerateMockProbeGridJob mockJob = new GenerateMockProbeGridJob
                {
                    Front = ResolveProbeFront(),
                    Back = ResolveProbeBack(),
                    Tuning = tuning
                };
                int count = math.clamp(tuning.ActiveProbeCount, 0, MaxCellCount);
                handle = mockJob.Schedule(count, 64, handle);
            }

            H8Memory.RegisterActiveJob(MemoryOwner, handle);
            _simulationHandle = handle;
            _simulationJobActive = true;
            _scheduledFinalBufferIsBack = false;
            _scheduledBootClear = true;
        }

        private void CacheDependencies()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
        }

        private NativeArray<T> ResolveArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return default;

            if (IsInteriorGIHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return buffer;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsInteriorGIHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return default;
            }

            return buffer;
        }

        private bool TryReadOnlyArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsInteriorGIHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            return true;
        }

        private NativeArray<CustomLightProbeDTO> ResolveProbeFront()
        {
            return ResolveArray(ref _probeFront, ProbeFrontBuffer, MaxCellCount);
        }

        private NativeArray<CustomLightProbeDTO> ResolveProbeBack()
        {
            return ResolveArray(ref _probeBack, ProbeBackBuffer, MaxCellCount);
        }

        private NativeArray<InteriorGISourceDTO> ResolveSources()
        {
            return ResolveArray(ref _sources, ProbeSourcesBuffer, MaxSourceCount);
        }

        private NativeArray<InteriorGIOcclusionCellDTO> ResolveOcclusion()
        {
            return ResolveArray(ref _occlusion, ProbeOcclusionBuffer, MaxCellCount);
        }

        private NativeArray<InteriorGITelemetryEntry> ResolveTelemetryRing()
        {
            return ResolveArray(ref _telemetryRing, ProbeTelemetryRingBuffer, TelemetryCapacity);
        }

        private NativeArray<InteriorGITelemetryEntry> ResolveTelemetryScratch()
        {
            return ResolveArray(ref _telemetryScratch, ProbeTelemetryScratchBuffer, 1);
        }

        private NativeArray<MockPowerState> ResolveMockPower()
        {
            return ResolveArray(ref _mockPower, ProbeMockPowerBuffer, 1);
        }

        private NativeArray<int> ResolveFaults()
        {
            return ResolveArray(ref _faults, ProbeFaultBuffer, MaxCellCount);
        }

        private NativeArray<byte> ResolveCsvBytes()
        {
            return ResolveArray(ref _csvBytes, ProbeCsvBytesBuffer, CsvBufferBytes);
        }

        private NativeArray<AmbientLightingProfileDTO> ResolveAmbientProfiles()
        {
            return ResolveArray(ref _ambientProfiles, ProbeAmbientProfileBuffer, MaxAmbientProfileCount);
        }

        private NativeArray<int> ResolveAmbientProfileCount()
        {
            return ResolveArray(ref _ambientProfileCount, ProbeAmbientProfileCountBuffer, 1);
        }

        private bool TryReadTuning(out InteriorGITuningDTO tuning)
        {
            tuning = default;
            if (!TryReadOnlyArray(in _tuning, ProbeTuningBuffer, 1, out NativeArray<InteriorGITuningDTO>.ReadOnly rows))
                return false;

            tuning = rows[0];
            return true;
        }

        private bool TryWriteTuning(in InteriorGITuningDTO tuning)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !HasInteriorGIHandle(in _tuning, ProbeTuningBuffer))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _tuning, MemoryOwner, out NativeArray<InteriorGITuningDTO> rows))
                return false;

            try
            {
                if (!rows.IsCreated || rows.Length < 1)
                    return false;

                rows[0] = tuning;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuning, MemoryOwner);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasInteriorGIHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return IsInteriorGIHandle(in handle, bufferId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInteriorGIHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)MemoryOwner &&
                   handle.Generation != 0u;
        }

        public void GenerateMockProbeGrid()
        {
            if (_simulationJobActive ||
                !HasInteriorGIHandle(in _probeFront, ProbeFrontBuffer) ||
                !HasInteriorGIHandle(in _probeBack, ProbeBackBuffer) ||
                !TryReadTuning(out InteriorGITuningDTO tuning))
            {
                return;
            }

            GenerateMockProbeGrid(tuning);
        }

        private void GenerateMockProbeGrid(InteriorGITuningDTO tuning)
        {
            NativeArray<CustomLightProbeDTO> front = ResolveProbeFront();
            NativeArray<CustomLightProbeDTO> back = ResolveProbeBack();
            if (!front.IsCreated || !back.IsCreated)
                return;

            GenerateMockProbeGridJob job = new GenerateMockProbeGridJob
            {
                Front = front,
                Back = back,
                Tuning = tuning
            };
            int count = math.clamp(tuning.ActiveProbeCount, 0, MaxCellCount);
            JobHandle handle = job.Schedule(count, 64);
            H8Memory.RegisterActiveJob(MemoryOwner, handle);
            // COLD/EDITOR SYNC FACADE: UI Toolkit mock-grid button drains isolated proof data outside frame cadence.
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            _visualDirty = true;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            TryUnregisterHotSwapListener();
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    if (!ReferenceEquals(_vault, currentService))
                        RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);

                    if (_vault != null && isActiveAndEnabled)
                        EnsureNativeState();
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherTicks();
                    if (currentService != null)
                        TryRegister();
                    break;
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault vault, IDataVault releaseVaultOverride = null)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            ReleaseRuntimeState(blockingComplete: true, releaseVaultOverride: _vault ?? releaseVaultOverride);
            _vault = vault;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private bool ReleaseRuntimeState(bool blockingComplete, IDataVault releaseVaultOverride = null)
        {
            if (_simulationJobActive)
            {
                if (!blockingComplete && !_simulationHandle.IsCompleted)
                    return false;

                DispatcherJobFence.TryComplete(ref _simulationHandle, blockingComplete);
            }

            _simulationJobActive = false;
            _scheduledFinalBufferIsBack = true;
            _scheduledBootClear = false;
            _scheduledGridClear = false;
            _gridClearRequested = false;
            _nativeReady = false;
            CompletePendingGpuUpload();
            ReleaseInteriorGIVaultHandles(releaseVaultOverride ?? _vault);
            _probeFront = default;
            _probeBack = default;
            _sources = default;
            _occlusion = default;
            _tuning = default;
            _telemetryRing = default;
            _telemetryScratch = default;
            _mockPower = default;
            _faults = default;
            _csvBytes = default;
            _ambientProfiles = default;
            _ambientProfileCount = default;
            return true;
        }

        private void ReleaseInteriorGIVaultHandles(IDataVault vault)
        {
            ReleaseInteriorGIVaultHandle(vault, ref _probeFront, ProbeFrontBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _probeBack, ProbeBackBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _sources, ProbeSourcesBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _occlusion, ProbeOcclusionBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _tuning, ProbeTuningBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _telemetryRing, ProbeTelemetryRingBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _telemetryScratch, ProbeTelemetryScratchBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _mockPower, ProbeMockPowerBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _faults, ProbeFaultBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _csvBytes, ProbeCsvBytesBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _ambientProfiles, ProbeAmbientProfileBuffer);
            ReleaseInteriorGIVaultHandle(vault, ref _ambientProfileCount, ProbeAmbientProfileCountBuffer);
        }

        private static void ReleaseInteriorGIVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsInteriorGIHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ReleaseGpuBuffers()
        {
            CompletePendingGpuUpload();

            if (_probeBufferA != null)
            {
                _probeBufferA.Release();
                _probeBufferA = null;
            }

            if (_probeBufferB != null)
            {
                _probeBufferB.Release();
                _probeBufferB = null;
            }

            _gpuProbeCapacity = 0;
            _gpuProbeWriteIndex = 0;
            _gpuProbePublishedCount = 0;
            _gpuUploadPendingBufferIndex = -1;
            _gpuUploadPendingCount = 0;
            _gpuUploadPendingFrame = -1;
            _gpuUploadPending = false;
            _gpuUploadHandle = default;
        }

        private InteriorGISourceDTO BuildSource(uint sourceHash, double3 aup, float3 color, float intensity, float radiusMeters, uint flags, float3 direction, int ordinal)
        {
            return new InteriorGISourceDTO
            {
                AUP = aup,
                Color = color,
                Intensity = intensity,
                RadiusMeters = radiusMeters,
                Flags = flags,
                Direction = direction,
                ConeCos = 0.65f,
                SourceHash = sourceHash,
                Phase01 = Frac01((sourceHash * 0.61803398875f) + ordinal * 0.137f),
                WaterAbsorptionScalar = 1f,
                FloraPulse01 = (flags & SourceFlagFlora) != 0u ? 1f : 0f,
                PowerScale01 = 1f,
                RoomHash = HashAup(aup),
                _pad0 = 0u,
                _pad1 = 0u
            };
        }

        private void EnsureMockSources()
        {
            if (_mockSourcesSeeded || !HasInteriorGIHandle(in _sources, ProbeSourcesBuffer) || _simulationJobActive)
                return;

            NativeArray<InteriorGISourceDTO> sources = ResolveSources();
            _sourceCount = 0;
            double s = math.max(1.0, cellSizeMeters);
            double3 c = _rootAup + new double3(_activeResolution * s * 0.5, _activeResolution * s * 0.42, _activeResolution * s * 0.5);
            AddMockSource(sources, 0x630801u, c + new double3(-s * 5.0, 0.0, -s * 3.0), new float3(1.25f, 0.86f, 0.48f), 2.4f, (float)(s * 7.5), SourceFlagPowered, new float3(0f, 0f, 1f));
            AddMockSource(sources, 0x630802u, c + new double3(s * 4.0, s * 0.5, s * 2.0), new float3(0.28f, 0.95f, 1.4f), 1.8f, (float)(s * 6.0), SourceFlagPowered, new float3(-1f, 0f, 0f));
            AddMockSource(sources, 0x630803u, c + new double3(0.0, -s * 1.0, s * 5.0), new float3(1.8f, 0.06f, 0.02f), 1.6f, (float)(s * 8.0), SourceFlagEmergency | SourceFlagAlwaysOn, new float3(0f, 0f, -1f));
            AddMockSource(sources, 0x630804u, c + new double3(s * 2.0, s * 0.2, -s * 4.0), new float3(0.22f, 0.9f, 0.54f), 1.0f, (float)(s * 5.0), SourceFlagFlora | SourceFlagAlwaysOn, new float3(0f, 1f, 0f));
            AddMockSource(sources, 0x630805u, c + new double3(-s * 1.0, s * 1.0, 0.0), new float3(1.1f, 1.05f, 0.92f), 1.5f, (float)(s * 10.0), SourceFlagFlashlight | SourceFlagPowered, new float3(1f, -0.1f, 0.1f));
            _mockSourcesSeeded = true;
        }

        private void AddMockSource(NativeArray<InteriorGISourceDTO> sources, uint hash, double3 aup, float3 color, float intensity, float radiusMeters, uint flags, float3 direction)
        {
            if (_sourceCount >= MaxSourceCount)
                return;

            sources[_sourceCount] = BuildSource(hash, aup, color, intensity, radiusMeters, flags, direction, _sourceCount);
            _sourceCount++;
        }

        private void EnsureMockOcclusionGrid()
        {
            if (_mockOcclusionSeeded || !HasInteriorGIHandle(in _occlusion, ProbeOcclusionBuffer) || _simulationJobActive)
                return;

            NativeArray<InteriorGIOcclusionCellDTO> occlusion = ResolveOcclusion();
            int res = _activeResolution;
            float cell = math.max(1f, cellSizeMeters);
            for (int z = 0; z < res; z++)
            {
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int index = ToIndex(new int3(x, y, z), res);
                        uint wallMask = 0u;
                        if (x == 0)
                            wallMask |= WallNegX;
                        if (x == res - 1)
                            wallMask |= WallPosX;
                        if (y == 0)
                            wallMask |= WallNegY;
                        if (y == res - 1)
                            wallMask |= WallPosY;
                        if (z == 0)
                            wallMask |= WallNegZ;
                        if (z == res - 1)
                            wallMask |= WallPosZ;

                        if ((x == res / 2 || z == res / 3) && y > 1 && y < res - 2)
                        {
                            if (math.abs(z - res / 2) > 2 || x == res / 2)
                                wallMask |= WallPosX | WallNegX;
                        }

                        float signedDistance = wallMask == 0u ? cell : cell * 0.35f;
                        float water = y < res / 5 ? 0.65f : 0f;
                        float flora = (z > res * 2 / 3 && y < res / 2) ? 0.5f : 0f;
                        occlusion[index] = new InteriorGIOcclusionCellDTO
                        {
                            SignedDistanceMeters = signedDistance,
                            Water01 = water,
                            TransferScale01 = wallMask == 0u ? 1f : 0.08f,
                            WallMask = wallMask,
                            FloraGlow01 = flora,
                            EmergencyReflectance01 = wallMask == 0u ? 0.05f : 0.35f,
                            RoomHash = HashCell(x, y, z),
                            Flags = 0u
                        };
                    }
                }
            }

            _mockOcclusionSeeded = true;
        }

        private void ScheduleSimulation(InteriorGITuningDTO tuning)
        {
            NativeArray<CustomLightProbeDTO> front = ResolveProbeFront();
            NativeArray<CustomLightProbeDTO> back = ResolveProbeBack();
            NativeArray<InteriorGISourceDTO> sources = ResolveSources();
            NativeArray<InteriorGIOcclusionCellDTO> occlusion = ResolveOcclusion();
            NativeArray<MockPowerState> power = ResolveMockPower();
            NativeArray<int> faults = ResolveFaults();
            NativeArray<InteriorGITelemetryEntry> scratch = ResolveTelemetryScratch();

            InteriorGIMockPowerJob powerJob = new InteriorGIMockPowerJob
            {
                FrameIndex = tuning.FrameIndex,
                EmergencyOverride01 = tuning.EmergencyOverride01,
                Power = power
            };
            JobHandle handle = powerJob.Schedule();

            int iterations = math.clamp(tuning.PropagationIterations, 1, 4);
            float iterationDt = tuning.SimulationDelta / math.max(1, iterations);
            NativeArray<CustomLightProbeDTO> readProbes = front;
            NativeArray<CustomLightProbeDTO> writeProbes = back;
            NativeArray<CustomLightProbeDTO> finalProbes = back;
            bool finalBufferIsBack = true;
            for (int i = 0; i < iterations; i++)
            {
                InteriorGITuningDTO iterationTuning = tuning;
                iterationTuning.SimulationDelta = iterationDt;
                InteriorGIPropagationJob propagationJob = new InteriorGIPropagationJob
                {
                    Front = readProbes,
                    Back = writeProbes,
                    Sources = sources,
                    Occlusion = occlusion,
                    Faults = faults,
                    Power = power,
                    Tuning = iterationTuning
                };
                handle = propagationJob.Schedule(tuning.ActiveProbeCount, 64, handle);
                finalProbes = writeProbes;
                finalBufferIsBack = (i & 1) == 0;
                NativeArray<CustomLightProbeDTO> swap = readProbes;
                readProbes = writeProbes;
                writeProbes = swap;
            }

            UpdateProbeOcclusionJob occlusionJob = new UpdateProbeOcclusionJob
            {
                Probes = finalProbes,
                Occlusion = occlusion,
                Tuning = tuning,
                OcclusionStrength = math.saturate(tuning.WallAbsorption)
            };
            handle = occlusionJob.Schedule(tuning.ActiveProbeCount, 64, handle);

            InteriorGITelemetryScanJob scanJob = new InteriorGITelemetryScanJob
            {
                Probes = finalProbes,
                Faults = faults,
                Scratch = scratch,
                Tuning = tuning
            };
            handle = scanJob.Schedule(handle);

            H8Memory.RegisterActiveJob(MemoryOwner, handle);
            _simulationHandle = handle;
            _simulationJobActive = true;
            _scheduledFinalBufferIsBack = finalBufferIsBack;
        }

        private void ScheduleGridClear()
        {
            if (_simulationJobActive ||
                !HasInteriorGIHandle(in _probeFront, ProbeFrontBuffer) ||
                !HasInteriorGIHandle(in _probeBack, ProbeBackBuffer))
            {
                return;
            }

            InteriorGIProbeGridClearJob clearJob = new InteriorGIProbeGridClearJob
            {
                ProbeFront = ResolveProbeFront(),
                ProbeBack = ResolveProbeBack(),
                Faults = ResolveFaults(),
                TelemetryScratch = ResolveTelemetryScratch()
            };
            JobHandle handle = clearJob.Schedule();
            H8Memory.RegisterActiveJob(MemoryOwner, handle);
            _simulationHandle = handle;
            _simulationJobActive = true;
            _scheduledFinalBufferIsBack = false;
            _scheduledGridClear = true;
            _gridClearRequested = false;
        }

        private void SwapFrontBack()
        {
            VaultGenerationHandle<CustomLightProbeDTO> swap = _probeFront;
            _probeFront = _probeBack;
            _probeBack = swap;
            _visualDirty = true;
        }

        private void CommitTelemetryScratch()
        {
            if (!TryReadOnlyArray(in _telemetryScratch, ProbeTelemetryScratchBuffer, 1, out NativeArray<InteriorGITelemetryEntry>.ReadOnly scratch))
            {
                return;
            }

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !HasInteriorGIHandle(in _telemetryRing, ProbeTelemetryRingBuffer))
            {
                return;
            }

            if (!vault.TryAcquireWriteLock(in _telemetryRing, MemoryOwner, out NativeArray<InteriorGITelemetryEntry> ring))
                return;

            InteriorGITelemetryEntry entry = scratch[0];
            entry.SolverCompleteMs = _lastCompleteMs;
            bool dumpNan = false;

            try
            {
                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return;

                ring[_telemetryCursor % TelemetryCapacity] = entry;
                _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
                dumpNan = (entry.Flags & TelemetryFlagNan) != 0u && !_nanDumpWritten;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryRing, MemoryOwner);
            }

            if (dumpNan)
            {
                _nanDumpWritten = true;
                DumpTelemetryRing();
            }
        }

        private void TryPublishCompletedGpuUpload()
        {
            if (!_gpuUploadPending)
                return;

            if (!_gpuUploadHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _gpuUploadHandle))
                return;

            GraphicsBuffer completed = _gpuUploadPendingBufferIndex == 0 ? _probeBufferA : _probeBufferB;
            if (completed != null && _gpuUploadPendingCount > 0)
            {
                completed.UnlockBufferAfterWrite<CustomLightProbeDTO>(_gpuUploadPendingCount);
                if (SystemDispatcher.CurrentFrameIndex > _gpuUploadPendingFrame)
                {
                    Shader.SetGlobalBuffer(InteriorGIProbeBufferId, completed);
                    Shader.SetGlobalVector(InteriorGIParamsId, _gpuUploadPendingParams);
                    Shader.SetGlobalVector(InteriorGIOriginId, _gpuUploadPendingOrigin);
                    Shader.SetGlobalVector(InteriorGIRootAupId, _gpuUploadPendingRootAup);
                    Shader.SetGlobalVector(InteriorGIGpuStateId, _gpuUploadPendingState);
                    _gpuProbePublishedCount = _gpuUploadPendingCount;
                }
                else
                {
                    _visualDirty = true;
                }
            }

            _gpuUploadPending = false;
            _gpuUploadPendingBufferIndex = -1;
            _gpuUploadPendingCount = 0;
            _gpuUploadPendingFrame = -1;
        }

        private void CompletePendingGpuUpload()
        {
            if (!_gpuUploadPending)
                return;

            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _gpuUploadHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            GraphicsBuffer pending = _gpuUploadPendingBufferIndex == 0 ? _probeBufferA : _probeBufferB;
            if (pending != null && _gpuUploadPendingCount > 0)
                pending.UnlockBufferAfterWrite<CustomLightProbeDTO>(_gpuUploadPendingCount);

            _gpuUploadPending = false;
            _gpuUploadPendingBufferIndex = -1;
            _gpuUploadPendingCount = 0;
            _gpuUploadPendingFrame = -1;
            _visualDirty = true;
        }

        private void TryStartGpuUploadIfDirty()
        {
            if (!enableGpuUpload ||
                !_visualDirty ||
                _gpuUploadPending ||
                !HasInteriorGIHandle(in _probeFront, ProbeFrontBuffer) ||
                !TryReadTuning(out InteriorGITuningDTO tuning))
            {
                return;
            }

            if (_visualUploadAccumulator < math.max(0.05f, tuning.UploadCadenceSeconds))
                return;

            _visualUploadAccumulator = 0f;
            int activeCount = math.clamp(tuning.ActiveProbeCount, 0, MaxCellCount);
            if (activeCount <= 0)
                return;

            if (!HasGpuBuffersReady(MaxCellCount))
                return;

            GraphicsBuffer target = _gpuProbeWriteIndex == 0 ? _probeBufferA : _probeBufferB;
            if (target == null || target.count < activeCount)
                return;

            NativeArray<CustomLightProbeDTO> source = ResolveProbeFront();
            if (!source.IsCreated || source.Length < activeCount)
                return;

            NativeArray<CustomLightProbeDTO> mapped = target.LockBufferForWrite<CustomLightProbeDTO>(0, activeCount);
            CustomLightProbeGpuUploadJob uploadJob = new CustomLightProbeGpuUploadJob
            {
                Source = source,
                Destination = mapped,
                Count = activeCount
            };
            JobHandle uploadHandle = uploadJob.Schedule();
            H8Memory.RegisterActiveJob(MemoryOwner, uploadHandle);
            Vector3 runtimeRoot = _cachedTransform != null ? _cachedTransform.position : transform.position;
            float3 rootResidue = ToShaderRootResidue(_rootAup);
            _gpuUploadHandle = uploadHandle;
            _gpuUploadPendingBufferIndex = _gpuProbeWriteIndex;
            _gpuUploadPendingCount = activeCount;
            _gpuUploadPendingFrame = SystemDispatcher.CurrentFrameIndex;
            _gpuUploadPendingParams = new Vector4(_activeResolution, math.max(1f, cellSizeMeters), tuning.GlobalQualityWeight, tuning.DirectionalWeight);
            _gpuUploadPendingOrigin = new Vector4(runtimeRoot.x, runtimeRoot.y, runtimeRoot.z, 1f);
            _gpuUploadPendingRootAup = new Vector4(rootResidue.x, rootResidue.y, rootResidue.z, (float)_rootHash);
            _gpuUploadPendingState = new Vector4(activeCount, _gridVersion, activeCount, _gpuProbeWriteIndex);
            _gpuUploadPending = true;
            _gpuProbeWriteIndex ^= 1;
            _visualDirty = false;
        }

        private bool HasGpuBuffersReady(int requiredCount)
        {
            int safeCount = math.clamp(requiredCount, 1, MaxCellCount);
            return _gpuProbeCapacity >= safeCount &&
                   _probeBufferA != null &&
                   _probeBufferB != null;
        }

        private void EnsureGpuBuffersCold(int requiredCount)
        {
            int safeCount = math.clamp(requiredCount, 1, MaxCellCount);
            if (HasGpuBuffersReady(safeCount))
                return;

            ReleaseGpuBuffers();
            int stride = UnsafeUtility.SizeOf<CustomLightProbeDTO>();
            _probeBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, safeCount, stride);
            _probeBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, safeCount, stride);
            _gpuProbeCapacity = safeCount;
        }

        private InteriorGITuningDTO BuildTuning(float quality, float dt, float cadence)
        {
            float safeQuality = math.saturate(math.isfinite(quality) ? quality : 0f);
            int resolution = ResolveResolutionFromQuality(safeQuality);
            float directional = Smooth01((safeQuality - 0.08f) * 1.35f);
            float l2 = Smooth01((safeQuality - 0.54f) * 2.05f);
            int sampleLimit = math.clamp((int)math.round(math.lerp(4f, MaxSourceCount, safeQuality * safeQuality)), 1, MaxSourceCount);
            float uploadCadence = math.lerp(0.45f, 0.10f, safeQuality);
            int iterations = math.clamp(1 + (int)math.floor(Smooth01(safeQuality) * 3.999f), 1, 4);
            return new InteriorGITuningDTO
            {
                RootAup = _rootAup,
                CellSizeMeters = math.max(1f, cellSizeMeters),
                GlobalQualityWeight = safeQuality,
                PropagationSpeed = math.max(0.01f, propagationSpeed),
                WallAbsorption = math.saturate(wallAbsorption),
                EmergencyLightIntensity = math.max(0f, emergencyLightIntensity),
                WaterAbsorption = math.saturate(waterAbsorption),
                FlashlightIntensity = math.max(0f, flashlightIntensity),
                FloraGlowScale = math.max(0f, floraGlowScale),
                SimulationDelta = math.max(0.001f, dt),
                DirectionalWeight = directional,
                L2Weight = l2,
                EmergencyOverride01 = math.saturate(emergencyOverride01),
                GridDecimation01 = 1f - ((float)resolution / MaxResolution),
                Resolution = resolution,
                ActiveProbeCount = resolution * resolution * resolution,
                SourceCount = math.clamp(_sourceCount, 0, MaxSourceCount),
                SourceSampleLimit = sampleLimit,
                FrameIndex = _gridVersion + 1,
                Flags = (uint)math.round(safeQuality * 65535f),
                RootHash = _rootHash ^ _biomeHash,
                RedOverride01 = math.saturate(emergencyOverride01),
                UploadCadenceSeconds = uploadCadence,
                AmbientRetain = math.lerp(0.78f, 0.93f, safeQuality),
                TransferDamping = math.lerp(0.55f, 1.15f, safeQuality),
                PropagationIterations = iterations,
                PackedBiomeTint = InteriorGIProbeMath.PackRgb10(_biomeTint)
            };
        }

        private void UpdateBiomeTintFromSignals()
        {
            ReadOnlySpan<BiomeGradientSignal> signals = SignalBus<BiomeGradientSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            BiomeGradientSignal signal = signals[signals.Length - 1];
            float blend = math.saturate(math.isfinite(signal.BlendFactor01) ? signal.BlendFactor01 : 0f);
            float3 tintA = ResolveProfileTint(signal.BiomeAHash, signal.BiomeA);
            float3 tintB = ResolveProfileTint(signal.BiomeBHash, signal.BiomeB);
            float3 tint = math.lerp(tintA, tintB, blend);
            if (!math.all(math.isfinite(tint)))
                return;

            _biomeTint = math.max(new float3(0f), tint);
            uint rotatedBiomeB = (signal.BiomeBHash << 11) | (signal.BiomeBHash >> 21);
            _biomeHash = signal.BiomeAHash ^ rotatedBiomeB ^ math.asuint(blend);
        }

        private float3 ResolveProfileTint(uint biomeHash, byte biomeId)
        {
            if (HasInteriorGIHandle(in _ambientProfiles, ProbeAmbientProfileBuffer) &&
                HasInteriorGIHandle(in _ambientProfileCount, ProbeAmbientProfileCountBuffer) &&
                !_simulationJobActive)
            {
                NativeArray<AmbientLightingProfileDTO> profiles = ResolveAmbientProfiles();
                NativeArray<int> profileCount = ResolveAmbientProfileCount();
                if (profiles.IsCreated && profileCount.IsCreated && profileCount.Length > 0)
                {
                    int count = math.clamp(profileCount[0], 0, profiles.Length);
                    ulong hash64 = biomeHash;
                    for (int i = 0; i < count; i++)
                    {
                        AmbientLightingProfileDTO profile = profiles[i];
                        if (profile.ProfileId == biomeHash || profile.ProfileHash64 == hash64)
                            return math.max(new float3(0f), profile.BiomeTint);
                    }
                }
            }

            uint hash = biomeHash != 0u ? biomeHash : (uint)(biomeId + 1) * 747796405u;
            uint r = InteriorGIProbeMath.Hash32(hash ^ 0x9E3779B9u);
            uint g = InteriorGIProbeMath.Hash32(hash ^ 0x85EBCA6Bu);
            uint b = InteriorGIProbeMath.Hash32(hash ^ 0xC2B2AE35u);
            return new float3(
                0.04f + ((r & 255u) * (1f / 255f)) * 0.18f,
                0.18f + ((g & 255u) * (1f / 255f)) * 0.62f,
                0.24f + ((b & 255u) * (1f / 255f)) * 0.66f);
        }

        private float ResolveQualityWeight()
        {
            if (forceQualityWeight >= 0f)
                return math.saturate(forceQualityWeight);

            float weight = MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)
                ? config.GlobalQualityWeight
                : HomeostasisBrain.GlobalQualityWeight;

            return MathLodApproximation.SaturateFinite(weight, 1f);
        }

        private void ResolveActiveResolution(float quality)
        {
            int desired = forceEditorResolution
                ? math.clamp(editorPreviewResolution, MinResolution, MaxResolution)
                : ResolveResolutionFromQuality(quality);

            if (desired == _activeResolution)
                return;

            _activeResolution = desired;
            _gridClearRequested = true;
            _mockOcclusionSeeded = false;
            _visualDirty = true;
        }

        private int ResolveResolutionFromQuality(float quality)
        {
            if (forceEditorResolution)
                return math.clamp(editorPreviewResolution, MinResolution, MaxResolution);

            float smoothed = Smooth01(math.saturate(quality));
            int raw = (int)math.round(math.lerp(MinResolution, MaxResolution, smoothed));
            int aligned = math.clamp((raw + 1) & ~1, MinResolution, MaxResolution);
            return aligned;
        }

        private static float ResolveCadenceSeconds(float quality)
        {
            float q = math.saturate(quality);
            float smooth = Smooth01(q);
            float thermalGate = 1f - Smooth01((q - 0.05f) * 2.2222223f);
            float thermalCadence = math.lerp(0.20f, 0.25f, smooth);
            float normalCadence = math.lerp(0.25f, 0.12f, smooth);
            return math.lerp(normalCadence, thermalCadence, thermalGate);
        }

#if UNITY_EDITOR
        private void TryReloadCsvOverrides()
        {
            if (!_csvReloadRequested ||
                _simulationJobActive ||
                !HasInteriorGIHandle(in _csvBytes, ProbeCsvBytesBuffer) ||
                !HasInteriorGIHandle(in _sources, ProbeSourcesBuffer))
            {
                return;
            }

            _csvReloadRequested = false;
            string path = Path.Combine(Application.dataPath, "..", csvOverrideRelativePath);
            if (!File.Exists(path))
                return;

            try
            {
                NativeArray<byte> csv = ResolveCsvBytes();
                int count = ReadFileIntoVaultBuffer(path, csv, CsvBufferBytes);
                NativeArray<InteriorGISourceDTO> sources = ResolveSources();
                int parsedCount = InteriorGICsvParser.Parse(csv, count, sources, MaxSourceCount, _rootAup, out int rowsRejected);
                if (parsedCount > 0)
                {
                    _sourceCount = parsedCount;
                    _mockSourcesSeeded = true;
                }

                if (rowsRejected > 0)
                    Hecton8.Core.H8Debug.LogWarning("Interior GI CSV rejected rows: " + rowsRejected);

                TryReloadAmbientProfileCsv();
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning("Interior GI CSV reload failed: " + ex.Message);
            }
        }

        private void TryReloadAmbientProfileCsv()
        {
            if (_simulationJobActive ||
                !HasInteriorGIHandle(in _csvBytes, ProbeCsvBytesBuffer) ||
                !HasInteriorGIHandle(in _ambientProfiles, ProbeAmbientProfileBuffer) ||
                !HasInteriorGIHandle(in _ambientProfileCount, ProbeAmbientProfileCountBuffer))
            {
                return;
            }

            string path = Path.Combine(Application.dataPath, "..", ambientProfileCsvRelativePath);
            if (!File.Exists(path))
                return;

            try
            {
                NativeArray<byte> csv = ResolveCsvBytes();
                int count = ReadFileIntoVaultBuffer(path, csv, CsvBufferBytes);
                NativeArray<AmbientLightingProfileDTO> profiles = ResolveAmbientProfiles();
                int parsedCount = AmbientLightingProfileCsvParser.Parse(csv, count, profiles, MaxAmbientProfileCount, out int rowsRejected);
                NativeArray<int> profileCount = ResolveAmbientProfileCount();
                if (profileCount.IsCreated && profileCount.Length > 0)
                    profileCount[0] = parsedCount;

                if (rowsRejected > 0)
                    Hecton8.Core.H8Debug.LogWarning("Ambient lighting profile CSV rejected rows: " + rowsRejected);
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning("Ambient lighting profile CSV reload failed: " + ex.Message);
            }
        }

        private static int ReadFileIntoVaultBuffer(string path, NativeArray<byte> destination, int maxBytes)
        {
            if (!destination.IsCreated || maxBytes <= 0)
                return 0;

            int count = 0;
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> readBuffer = stackalloc byte[4096];
            while (count < maxBytes)
            {
                int requestedBytes = math.min(readBuffer.Length, maxBytes - count);
                int read = stream.Read(readBuffer.Slice(0, requestedBytes));
                if (read <= 0)
                    break;

                for (int i = 0; i < read; i++)
                    destination[count + i] = readBuffer[i];
                count += read;
            }

            return count;
        }
#endif

        private void DumpTelemetryRing()
        {
            if (!HasInteriorGIHandle(in _telemetryRing, ProbeTelemetryRingBuffer))
                return;

            NativeArray<InteriorGITelemetryEntry> ring = ResolveTelemetryRing();
            WriteTelemetryDump("Docs/AgentLogs/Dump_13KRA.bin", ring);
        }

        private void WriteTelemetryDump(string path, NativeArray<InteriorGITelemetryEntry> ring)
        {
            if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                return;

            try
            {
                int entrySize = UnsafeUtility.SizeOf<InteriorGITelemetryEntry>();
                int byteCount = TelemetryDumpHeaderBytes + TelemetryCapacity * entrySize;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(InteriorGIProbeVolumeRuntime),
                    "InteriorGITelemetryDumpPayload");
                try
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    int offset = 0;
                    if (!TryWriteUInt32LittleEndian(destination, byteCount, ref offset, 0x63474953u) ||
                        !TryWriteInt32LittleEndian(destination, byteCount, ref offset, TelemetryCapacity) ||
                        !TryWriteInt32LittleEndian(destination, byteCount, ref offset, _telemetryCursor) ||
                        !TryWriteInt32LittleEndian(destination, byteCount, ref offset, _activeResolution) ||
                        !TryWriteDouble64LittleEndian(destination, byteCount, ref offset, _rootAup.x) ||
                        !TryWriteDouble64LittleEndian(destination, byteCount, ref offset, _rootAup.y) ||
                        !TryWriteDouble64LittleEndian(destination, byteCount, ref offset, _rootAup.z))
                    {
                        return;
                    }

                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                    UnsafeUtility.MemCpy(destination + offset, source, TelemetryCapacity * entrySize);
                    offset += TelemetryCapacity * entrySize;
                    if (offset == byteCount)
                        NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(InteriorGIProbeVolumeRuntime),
                        "InteriorGITelemetryDumpPayload");
                }
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning("Interior GI black box dump failed: " + ex.Message);
            }
        }

        private static bool TryWriteUInt64LittleEndian(byte* destination, int capacity, ref int offset, ulong value)
        {
            if (offset > capacity - 8)
                return false;

            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            destination[offset + 4] = (byte)(value >> 32);
            destination[offset + 5] = (byte)(value >> 40);
            destination[offset + 6] = (byte)(value >> 48);
            destination[offset + 7] = (byte)(value >> 56);
            offset += 8;
            return true;
        }

        private static bool TryWriteUInt32LittleEndian(byte* destination, int capacity, ref int offset, uint value)
        {
            if (offset > capacity - 4)
                return false;

            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            offset += 4;
            return true;
        }

        private static bool TryWriteInt32LittleEndian(byte* destination, int capacity, ref int offset, int value)
        {
            return TryWriteUInt32LittleEndian(destination, capacity, ref offset, unchecked((uint)value));
        }

        private static bool TryWriteDouble64LittleEndian(byte* destination, int capacity, ref int offset, double value)
        {
            ulong bits = math.asulong(value);
            return TryWriteUInt64LittleEndian(destination, capacity, ref offset, bits);
        }

        private static uint HashAup(double3 aup)
        {
            uint hash = 2166136261u;
            hash = HashLong(hash, QuantizeAupForHash(aup.x));
            hash = HashLong(hash, QuantizeAupForHash(aup.y));
            hash = HashLong(hash, QuantizeAupForHash(aup.z));
            return hash;
        }

        private static long QuantizeAupForHash(double value)
        {
            if (!math.isfinite(value))
                return 0L;

            const double minSafeInteger = -4503599627370496.0;
            const double maxSafeInteger = 4503599627370496.0;
            double scaled = math.floor(value * 0.03125);
            return (long)math.clamp(scaled, minSafeInteger, maxSafeInteger);
        }

        private static uint HashLong(uint hash, long value)
        {
            ulong bits = (ulong)value;
            hash = (hash ^ (uint)bits) * 16777619u;
            hash = (hash ^ (uint)(bits >> 32)) * 16777619u;
            return hash;
        }

        private static bool TryResolveAbsoluteAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = originAup.ToAbsoluteDouble3() + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(absoluteAup));
        }

        private static uint HashCell(int x, int y, int z)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)y) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            return hash;
        }

        private static float3 ToShaderRootResidue(double3 aup)
        {
            return new float3(
                (float)(aup.x - math.floor(aup.x / 1000.0) * 1000.0),
                (float)(aup.y - math.floor(aup.y / 1000.0) * 1000.0),
                (float)(aup.z - math.floor(aup.z / 1000.0) * 1000.0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToIndex(int3 cell, int resolution)
        {
            return cell.x + (cell.y * resolution) + (cell.z * resolution * resolution);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInside(int3 cell, int resolution)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.z >= 0 && cell.x < resolution && cell.y < resolution && cell.z < resolution;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float x)
        {
            float t = math.saturate(x);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Frac01(float x)
        {
            return x - math.floor(x);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cellSizeMeters = math.max(1f, cellSizeMeters);
            editorPreviewResolution = math.clamp(editorPreviewResolution, MinResolution, MaxResolution);
            propagationSpeed = math.clamp(propagationSpeed, 0.05f, 4f);
            wallAbsorption = math.saturate(wallAbsorption);
            waterAbsorption = math.saturate(waterAbsorption);
            emergencyOverride01 = math.saturate(emergencyOverride01);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawProbeGizmos || !TryGetProbeGridReadback(out NativeArray<CustomLightProbeDTO>.ReadOnly probes, out int resolution, out double3 root, out float cell, out _))
                return;

            int count = resolution * resolution * resolution;
            int stride = math.max(1, count / math.max(1, maxEditorGizmoProbes));
            for (int i = 0; i < count; i += stride)
            {
                CustomLightProbeDTO probe = probes[i];
                float3 forward = _cachedTransform != null ? (float3)_cachedTransform.forward : new float3(0f, 0f, 1f);
                float3 forwardColor = InteriorGIProbeMath.EvaluateDirection(in probe, forward);
                float luma = math.saturate(math.dot(forwardColor, new float3(0.2126f, 0.7152f, 0.0722f)) * 0.25f);
                if (luma <= 0.01f)
                    continue;

                int3 coord = InteriorGIProbeMath.IndexToCoord(i, resolution);
                Vector3 pos = new Vector3(
                    (float)(root.x + (coord.x + 0.5f) * cell - _rootAup.x),
                    (float)(root.y + (coord.y + 0.5f) * cell - _rootAup.y),
                    (float)(root.z + (coord.z + 0.5f) * cell - _rootAup.z)) + (_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
                Gizmos.color = new Color(math.saturate(forwardColor.x), math.saturate(forwardColor.y), math.saturate(forwardColor.z), math.saturate(luma));
                Gizmos.DrawSphere(pos, math.max(0.05f, cell * 0.08f));
            }
        }
#endif
    }

#if UNITY_EDITOR
    public static class InteriorGICsvParser
    {
        public static int Parse(NativeArray<byte> bytes, int byteCount, NativeArray<InteriorGISourceDTO> sources, int maxSources, double3 rootAup, out int rowsRejected)
        {
            int sourceCount = 0;
            rowsRejected = 0;
            int offset = 0;
            while (offset < byteCount && sourceCount < maxSources)
            {
                int lineStart = offset;
                while (offset < byteCount && bytes[offset] != 10 && bytes[offset] != 13)
                    offset++;

                int lineEnd = offset;
                while (offset < byteCount && (bytes[offset] == 10 || bytes[offset] == 13))
                    offset++;

                if (lineEnd <= lineStart)
                    continue;

                if (bytes[lineStart] == (byte)'#')
                    continue;

                if (TryParseSourceLine(bytes, lineStart, lineEnd, sourceCount, rootAup, out InteriorGISourceDTO source))
                {
                    sources[sourceCount] = source;
                    sourceCount++;
                }
                else
                {
                    rowsRejected++;
                }
            }

            return sourceCount;
        }

        private static bool TryParseSourceLine(NativeArray<byte> bytes, int start, int end, int ordinal, double3 rootAup, out InteriorGISourceDTO source)
        {
            source = default;
            CsvCursor cursor = new CsvCursor
            {
                Start = start,
                End = end,
                Position = start
            };

            uint hash = 0x6308C500u + (uint)ordinal;
            TryReadOptionalKeyHash(bytes, ref cursor, ref hash);
            if (!ReadFloat(bytes, ref cursor, out float x) ||
                !ReadFloat(bytes, ref cursor, out float y) ||
                !ReadFloat(bytes, ref cursor, out float z) ||
                !ReadFloat(bytes, ref cursor, out float r) ||
                !ReadFloat(bytes, ref cursor, out float g) ||
                !ReadFloat(bytes, ref cursor, out float b) ||
                !ReadFloat(bytes, ref cursor, out float intensity) ||
                !ReadFloat(bytes, ref cursor, out float radius) ||
                !ReadUInt(bytes, ref cursor, out uint flags))
            {
                return false;
            }

            double3 aup = rootAup + new double3(x, y, z);
            source = new InteriorGISourceDTO
            {
                AUP = aup,
                Color = new float3(math.max(0f, r), math.max(0f, g), math.max(0f, b)),
                Intensity = math.max(0f, intensity),
                RadiusMeters = math.max(0.25f, radius),
                Flags = flags,
                Direction = new float3(0f, 0f, 1f),
                ConeCos = 0.65f,
                SourceHash = hash,
                Phase01 = (ordinal & 15) * 0.0625f,
                WaterAbsorptionScalar = 1f,
                FloraPulse01 = (flags & InteriorGIProbeVolumeRuntime.SourceFlagFlora) != 0u ? 1f : 0f,
                PowerScale01 = 1f,
                RoomHash = hash * 16777619u,
                _pad0 = 0u,
                _pad1 = 0u
            };
            return true;
        }

        private static void TryReadOptionalKeyHash(NativeArray<byte> bytes, ref CsvCursor cursor, ref uint hash)
        {
            SkipSpaces(bytes, ref cursor);
            if (cursor.Position >= cursor.End || IsNumericStart(bytes[cursor.Position]))
                return;

            uint h = 2166136261u;
            bool hasToken = false;
            while (cursor.Position < cursor.End)
            {
                byte c = bytes[cursor.Position];
                if (c == (byte)',' || c == (byte)' ' || c == (byte)'\t')
                    break;

                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                h = (h ^ c) * 16777619u;
                hasToken = true;
                cursor.Position++;
            }

            if (hasToken)
                hash = h;

            ConsumeDelimiter(bytes, ref cursor);
        }

        private static bool ReadFloat(NativeArray<byte> bytes, ref CsvCursor cursor, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, ref cursor);
            bool negative = false;
            if (cursor.Position < cursor.End && bytes[cursor.Position] == (byte)'-')
            {
                negative = true;
                cursor.Position++;
            }

            float whole = 0f;
            bool hasDigit = false;
            while (cursor.Position < cursor.End)
            {
                byte c = bytes[cursor.Position];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                whole = (whole * 10f) + (c - (byte)'0');
                hasDigit = true;
                cursor.Position++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor.Position < cursor.End && bytes[cursor.Position] == (byte)'.')
            {
                cursor.Position++;
                while (cursor.Position < cursor.End)
                {
                    byte c = bytes[cursor.Position];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction = (fraction * 10f) + (c - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    cursor.Position++;
                }
            }

            if (!hasDigit)
                return false;

            value = whole + fraction / divisor;
            if (negative)
                value = -value;

            ConsumeDelimiter(bytes, ref cursor);
            return math.isfinite(value);
        }

        private static bool IsNumericStart(byte c)
        {
            return c == (byte)'-' || c == (byte)'.' || (c >= (byte)'0' && c <= (byte)'9');
        }

        private static bool ReadUInt(NativeArray<byte> bytes, ref CsvCursor cursor, out uint value)
        {
            value = 0u;
            SkipSpaces(bytes, ref cursor);
            bool hasDigit = false;
            while (cursor.Position < cursor.End)
            {
                byte c = bytes[cursor.Position];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                value = (value * 10u) + (uint)(c - (byte)'0');
                hasDigit = true;
                cursor.Position++;
            }

            ConsumeDelimiter(bytes, ref cursor);
            return hasDigit;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, ref CsvCursor cursor)
        {
            while (cursor.Position < cursor.End)
            {
                byte c = bytes[cursor.Position];
                if (c != (byte)' ' && c != (byte)'\t')
                    return;
                cursor.Position++;
            }
        }

        private static void ConsumeDelimiter(NativeArray<byte> bytes, ref CsvCursor cursor)
        {
            while (cursor.Position < cursor.End)
            {
                byte c = bytes[cursor.Position];
                if (c == (byte)',')
                {
                    cursor.Position++;
                    return;
                }

                if (c != (byte)' ' && c != (byte)'\t')
                    return;

                cursor.Position++;
            }
        }

        private struct CsvCursor
        {
            public int Start;
            public int End;
            public int Position;
        }
    }
#endif

    public static class CustomLightProbeLayoutAudit
    {
        public const int ExpectedSize = 128;
        public const int SpatialHashOffset = 0;
        public const int PackedCoordOffset = 8;
        public const int FlagsOffset = 12;
        public const int Lane0Offset = 16;
        public const int Lane6Offset = 112;
        public const int LastCoefficientOffset = 120;
        public const int SpareOffset = 124;

        public static bool Validate(out int actualSize, out int lane0Offset, out int lane6Offset, out int lastCoefficientOffset)
        {
            actualSize = UnsafeUtility.SizeOf<CustomLightProbeDTO>();
            lane0Offset = Marshal.OffsetOf(typeof(CustomLightProbeDTO), nameof(CustomLightProbeDTO.Lane0)).ToInt32();
            lane6Offset = Marshal.OffsetOf(typeof(CustomLightProbeDTO), nameof(CustomLightProbeDTO.Lane6)).ToInt32();
            lastCoefficientOffset = Marshal.OffsetOf(typeof(CustomLightProbeDTO), nameof(CustomLightProbeDTO.B8)).ToInt32();
            int spareOffset = Marshal.OffsetOf(typeof(CustomLightProbeDTO), nameof(CustomLightProbeDTO.Spare0)).ToInt32();
            return actualSize == ExpectedSize &&
                   lane0Offset == Lane0Offset &&
                   lane6Offset == Lane6Offset &&
                   lastCoefficientOffset == LastCoefficientOffset &&
                   spareOffset == SpareOffset;
        }
    }

#if UNITY_EDITOR
    public static class AmbientLightingProfileCsvParser
    {
        public static unsafe int Parse(NativeArray<byte> csv, int byteCount, NativeArray<AmbientLightingProfileDTO> profiles, int maxProfiles, out int rowsRejected)
        {
            rowsRejected = 0;
            if (!csv.IsCreated || !profiles.IsCreated || byteCount <= 0 || maxProfiles <= 0)
                return 0;

            int safeBytes = math.min(byteCount, csv.Length);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csv);
            return Parse(new ReadOnlySpan<byte>(ptr, safeBytes), profiles, maxProfiles, out rowsRejected);
        }

        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<AmbientLightingProfileDTO> profiles, int maxProfiles, out int rowsRejected)
        {
            rowsRejected = 0;
            if (!profiles.IsCreated || csv.Length <= 0 || maxProfiles <= 0)
                return 0;

            int safeBytes = csv.Length;
            int safeProfiles = math.min(maxProfiles, profiles.Length);
            int count = 0;
            int position = 0;
            while (position < safeBytes && count < safeProfiles)
            {
                int lineStart = position;
                while (position < safeBytes && csv[position] != (byte)'\n' && csv[position] != (byte)'\r')
                    position++;

                int lineEnd = position;
                while (position < safeBytes && (csv[position] == (byte)'\n' || csv[position] == (byte)'\r'))
                    position++;

                if (lineEnd <= lineStart)
                    continue;

                int first = SkipWhitespace(csv, lineStart, lineEnd);
                if (first >= lineEnd || csv[first] == (byte)'#')
                    continue;

                CsvCursor cursor = new CsvCursor { Start = first, End = lineEnd, Position = first };
                if (!ReadToken(csv, ref cursor, out int nameStart, out int nameEnd))
                {
                    rowsRejected++;
                    continue;
                }

                if (LooksLikeHeader(csv, nameStart, nameEnd))
                    continue;

                float r = 0.05f;
                float g = 0.08f;
                float b = 0.12f;
                float directional = 0.2f;
                float l2 = 0.1f;
                float tintR = 1f;
                float tintG = 1f;
                float tintB = 1f;
                float water = 0.8f;
                if (!ReadFloatOrDefault(csv, ref cursor, ref r) ||
                    !ReadFloatOrDefault(csv, ref cursor, ref g) ||
                    !ReadFloatOrDefault(csv, ref cursor, ref b))
                {
                    rowsRejected++;
                    continue;
                }

                ReadFloatOrDefault(csv, ref cursor, ref directional);
                ReadFloatOrDefault(csv, ref cursor, ref l2);
                ReadFloatOrDefault(csv, ref cursor, ref tintR);
                ReadFloatOrDefault(csv, ref cursor, ref tintG);
                ReadFloatOrDefault(csv, ref cursor, ref tintB);
                ReadFloatOrDefault(csv, ref cursor, ref water);

                ulong hash = HashName(csv, nameStart, nameEnd);
                profiles[count] = new AmbientLightingProfileDTO
                {
                    ProfileHash64 = hash,
                    ProfileId = (uint)hash,
                    Flags = 0u,
                    L0Color = math.max(new float3(0f), new float3(SafeFloat(r), SafeFloat(g), SafeFloat(b))),
                    DirectionalWeight = math.saturate(SafeFloat(directional)),
                    BiomeTint = math.max(new float3(0f), new float3(SafeFloat(tintR), SafeFloat(tintG), SafeFloat(tintB))),
                    L2Weight = math.saturate(SafeFloat(l2)),
                    WaterAbsorption = new float3(math.saturate(SafeFloat(water))),
                    _pad0 = 0u
                };
                count++;
            }

            return count;
        }

        private static bool ReadFloatOrDefault(ReadOnlySpan<byte> csv, ref CsvCursor cursor, ref float value)
        {
            if (!ReadToken(csv, ref cursor, out int start, out int end))
                return true;

            if (start >= end)
                return true;

            return TryParseFloat(csv, start, end, out value);
        }

        private static bool ReadToken(ReadOnlySpan<byte> csv, ref CsvCursor cursor, out int start, out int end)
        {
            start = cursor.Position;
            end = cursor.Position;
            if (cursor.Position >= cursor.End)
                return false;

            start = SkipWhitespace(csv, cursor.Position, cursor.End);
            int p = start;
            while (p < cursor.End && csv[p] != (byte)',')
                p++;

            end = TrimRight(csv, start, p);
            cursor.Position = p < cursor.End ? p + 1 : cursor.End;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> csv, int start, int end, out float value)
        {
            value = 0f;
            int p = SkipWhitespace(csv, start, end);
            int e = TrimRight(csv, p, end);
            if (p >= e)
                return false;

            float sign = 1f;
            if (csv[p] == (byte)'-')
            {
                sign = -1f;
                p++;
            }
            else if (csv[p] == (byte)'+')
            {
                p++;
            }

            float integer = 0f;
            bool sawDigit = false;
            while (p < e && csv[p] >= (byte)'0' && csv[p] <= (byte)'9')
            {
                sawDigit = true;
                integer = integer * 10f + (csv[p] - (byte)'0');
                p++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (p < e && csv[p] == (byte)'.')
            {
                p++;
                while (p < e && csv[p] >= (byte)'0' && csv[p] <= (byte)'9')
                {
                    sawDigit = true;
                    scale *= 0.1f;
                    fraction += (csv[p] - (byte)'0') * scale;
                    p++;
                }
            }

            if (!sawDigit)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }

        private static ulong HashName(ReadOnlySpan<byte> csv, int start, int end)
        {
            ulong hash = 1469598103934665603UL;
            for (int i = start; i < end; i++)
            {
                byte b = csv[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);

                hash = (hash ^ b) * 1099511628211UL;
            }

            return hash;
        }

        private static bool LooksLikeHeader(ReadOnlySpan<byte> csv, int start, int end)
        {
            int len = end - start;
            return len == 4 &&
                   ToLower(csv[start]) == (byte)'n' &&
                   ToLower(csv[start + 1]) == (byte)'a' &&
                   ToLower(csv[start + 2]) == (byte)'m' &&
                   ToLower(csv[start + 3]) == (byte)'e';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static int SkipWhitespace(ReadOnlySpan<byte> csv, int start, int end)
        {
            int p = start;
            while (p < end && (csv[p] == (byte)' ' || csv[p] == (byte)'\t'))
                p++;
            return p;
        }

        private static int TrimRight(ReadOnlySpan<byte> csv, int start, int end)
        {
            int p = end;
            while (p > start && (csv[p - 1] == (byte)' ' || csv[p - 1] == (byte)'\t'))
                p--;
            return p;
        }

        private static float SafeFloat(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        private struct CsvCursor
        {
            public int Start;
            public int End;
            public int Position;
        }
    }
#endif

    public static class InteriorGIProbeMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexToCoord(int index, int resolution)
        {
            int z = index / (resolution * resolution);
            int rem = index - z * resolution * resolution;
            int y = rem / resolution;
            int x = rem - y * resolution;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackCoord(int3 coord)
        {
            return ((uint)(coord.x & 1023)) | ((uint)(coord.y & 1023) << 10) | ((uint)(coord.z & 1023) << 20);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashCell64(int3 coord, uint rootHash)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ rootHash) * 1099511628211UL;
            hash = (hash ^ (uint)coord.x) * 1099511628211UL;
            hash = (hash ^ (uint)coord.y) * 1099511628211UL;
            hash = (hash ^ (uint)coord.z) * 1099511628211UL;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackRgb10(float3 color)
        {
            float3 c = math.saturate(math.select(new float3(0f), color, math.all(math.isfinite(color))));
            uint r = (uint)math.round(c.x * 1023f);
            uint g = (uint)math.round(c.y * 1023f);
            uint b = (uint)math.round(c.z * 1023f);
            return (r & 1023u) | ((g & 1023u) << 10) | ((b & 1023u) << 20);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 UnpackRgb10(uint packed)
        {
            return new float3(
                (packed & 1023u) * (1f / 1023f),
                ((packed >> 10) & 1023u) * (1f / 1023f),
                ((packed >> 20) & 1023u) * (1f / 1023f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyRgb(ref CustomLightProbeDTO probe, float3 tint)
        {
            float3 c = math.max(new float3(0f), math.select(new float3(1f), tint, math.all(math.isfinite(tint))));
            probe.R0 *= c.x;
            probe.R1 *= c.x;
            probe.R2 *= c.x;
            probe.R3 *= c.x;
            probe.R4 *= c.x;
            probe.R5 *= c.x;
            probe.R6 *= c.x;
            probe.R7 *= c.x;
            probe.R8 *= c.x;
            probe.G0 *= c.y;
            probe.G1 *= c.y;
            probe.G2 *= c.y;
            probe.G3 *= c.y;
            probe.G4 *= c.y;
            probe.G5 *= c.y;
            probe.G6 *= c.y;
            probe.G7 *= c.y;
            probe.G8 *= c.y;
            probe.B0 *= c.z;
            probe.B1 *= c.z;
            probe.B2 *= c.z;
            probe.B3 *= c.z;
            probe.B4 *= c.z;
            probe.B5 *= c.z;
            probe.B6 *= c.z;
            probe.B7 *= c.z;
            probe.B8 *= c.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteProbeMetadata(ref CustomLightProbeDTO probe, int3 coord, uint rootHash, uint flags)
        {
            probe.SpatialHash64 = HashCell64(coord, rootHash);
            probe.PackedGridCoord = PackCoord(coord);
            probe.Flags = flags;
            probe.Spare0 = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddScaled(ref CustomLightProbeDTO dst, in CustomLightProbeDTO src, float scale, float l1Weight, float l2Weight)
        {
            if (scale <= 0.000001f || !math.isfinite(scale))
                return;

            dst.R0 += src.R0 * scale;
            dst.G0 += src.G0 * scale;
            dst.B0 += src.B0 * scale;
            float s1 = scale * l1Weight;
            if (s1 > 0.000001f)
            {
                dst.R1 += src.R1 * s1;
                dst.R2 += src.R2 * s1;
                dst.R3 += src.R3 * s1;
                dst.G1 += src.G1 * s1;
                dst.G2 += src.G2 * s1;
                dst.G3 += src.G3 * s1;
                dst.B1 += src.B1 * s1;
                dst.B2 += src.B2 * s1;
                dst.B3 += src.B3 * s1;
            }

            float s2 = scale * l2Weight;
            if (s2 > 0.000001f)
            {
                dst.R4 += src.R4 * s2;
                dst.R5 += src.R5 * s2;
                dst.R6 += src.R6 * s2;
                dst.R7 += src.R7 * s2;
                dst.R8 += src.R8 * s2;
                dst.G4 += src.G4 * s2;
                dst.G5 += src.G5 * s2;
                dst.G6 += src.G6 * s2;
                dst.G7 += src.G7 * s2;
                dst.G8 += src.G8 * s2;
                dst.B4 += src.B4 * s2;
                dst.B5 += src.B5 * s2;
                dst.B6 += src.B6 * s2;
                dst.B7 += src.B7 * s2;
                dst.B8 += src.B8 * s2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDirectional(ref CustomLightProbeDTO dst, float3 color, float gain, float3 direction, float l1Weight, float l2Weight)
        {
            float safeGain = math.max(0f, math.isfinite(gain) ? gain : 0f);
            if (safeGain <= 0.000001f)
                return;

            float3 safeColor = math.select(new float3(0f), math.max(new float3(0f), color), math.all(math.isfinite(color)));
            float3 c = safeColor * safeGain;
            dst.R0 += c.x;
            dst.G0 += c.y;
            dst.B0 += c.z;

            float l1 = l1Weight * 0.55f;
            float l2 = l2Weight * 0.22f;
            if (l1 <= 0.000001f && l2 <= 0.000001f)
                return;

            float3 d = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            if (l1 > 0.000001f)
            {
                dst.R1 += c.x * d.y * l1;
                dst.R2 += c.x * d.z * l1;
                dst.R3 += c.x * d.x * l1;
                dst.G1 += c.y * d.y * l1;
                dst.G2 += c.y * d.z * l1;
                dst.G3 += c.y * d.x * l1;
                dst.B1 += c.z * d.y * l1;
                dst.B2 += c.z * d.z * l1;
                dst.B3 += c.z * d.x * l1;
            }

            if (l2 > 0.000001f)
            {
                float xy = d.x * d.y;
                float yz = d.y * d.z;
                float zz = (3f * d.z * d.z) - 1f;
                float xz = d.x * d.z;
                float xxmyy = (d.x * d.x) - (d.y * d.y);
                dst.R4 += c.x * xy * l2;
                dst.R5 += c.x * yz * l2;
                dst.R6 += c.x * zz * l2;
                dst.R7 += c.x * xz * l2;
                dst.R8 += c.x * xxmyy * l2;
                dst.G4 += c.y * xy * l2;
                dst.G5 += c.y * yz * l2;
                dst.G6 += c.y * zz * l2;
                dst.G7 += c.y * xz * l2;
                dst.G8 += c.y * xxmyy * l2;
                dst.B4 += c.z * xy * l2;
                dst.B5 += c.z * yz * l2;
                dst.B6 += c.z * zz * l2;
                dst.B7 += c.z * xz * l2;
                dst.B8 += c.z * xxmyy * l2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SanitizeAndClamp(ref CustomLightProbeDTO probe, float maxL0)
        {
            probe.R0 = ClampFinite(probe.R0, maxL0);
            probe.G0 = ClampFinite(probe.G0, maxL0);
            probe.B0 = ClampFinite(probe.B0, maxL0);
            probe.R1 = ClampFinite(probe.R1, maxL0);
            probe.R2 = ClampFinite(probe.R2, maxL0);
            probe.R3 = ClampFinite(probe.R3, maxL0);
            probe.R4 = ClampFinite(probe.R4, maxL0);
            probe.R5 = ClampFinite(probe.R5, maxL0);
            probe.R6 = ClampFinite(probe.R6, maxL0);
            probe.R7 = ClampFinite(probe.R7, maxL0);
            probe.R8 = ClampFinite(probe.R8, maxL0);
            probe.G1 = ClampFinite(probe.G1, maxL0);
            probe.G2 = ClampFinite(probe.G2, maxL0);
            probe.G3 = ClampFinite(probe.G3, maxL0);
            probe.G4 = ClampFinite(probe.G4, maxL0);
            probe.G5 = ClampFinite(probe.G5, maxL0);
            probe.G6 = ClampFinite(probe.G6, maxL0);
            probe.G7 = ClampFinite(probe.G7, maxL0);
            probe.G8 = ClampFinite(probe.G8, maxL0);
            probe.B1 = ClampFinite(probe.B1, maxL0);
            probe.B2 = ClampFinite(probe.B2, maxL0);
            probe.B3 = ClampFinite(probe.B3, maxL0);
            probe.B4 = ClampFinite(probe.B4, maxL0);
            probe.B5 = ClampFinite(probe.B5, maxL0);
            probe.B6 = ClampFinite(probe.B6, maxL0);
            probe.B7 = ClampFinite(probe.B7, maxL0);
            probe.B8 = ClampFinite(probe.B8, maxL0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LuminanceL0(in CustomLightProbeDTO probe)
        {
            return probe.R0 * 0.2126f + probe.G0 * 0.7152f + probe.B0 * 0.0722f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EvaluateDirection(in CustomLightProbeDTO probe, float3 direction)
        {
            float3 d = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float xy = d.x * d.y;
            float yz = d.y * d.z;
            float zz = (3f * d.z * d.z) - 1f;
            float xz = d.x * d.z;
            float xxmyy = (d.x * d.x) - (d.y * d.y);
            float r = probe.R0 + probe.R1 * d.y + probe.R2 * d.z + probe.R3 * d.x + probe.R4 * xy + probe.R5 * yz + probe.R6 * zz + probe.R7 * xz + probe.R8 * xxmyy;
            float g = probe.G0 + probe.G1 * d.y + probe.G2 * d.z + probe.G3 * d.x + probe.G4 * xy + probe.G5 * yz + probe.G6 * zz + probe.G7 * xz + probe.G8 * xxmyy;
            float b = probe.B0 + probe.B1 * d.y + probe.B2 * d.z + probe.B3 * d.x + probe.B4 * xy + probe.B5 * yz + probe.B6 * zz + probe.B7 * xz + probe.B8 * xxmyy;
            return math.max(new float3(0f), new float3(ClampFinite(r, 32f), ClampFinite(g, 32f), ClampFinite(b, 32f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashProbe(in CustomLightProbeDTO probe, uint hash)
        {
            hash = (hash ^ math.asuint(probe.R0)) * 16777619u;
            hash = (hash ^ math.asuint(probe.G0)) * 16777619u;
            hash = (hash ^ math.asuint(probe.B0)) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampFinite(float value, float maxAbs)
        {
            return math.isfinite(value) ? math.clamp(value, -maxAbs, maxAbs) : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorGIClearStateJob : IJob
    {
        [NoAlias] public NativeArray<CustomLightProbeDTO> ProbeFront;
        [NoAlias] public NativeArray<CustomLightProbeDTO> ProbeBack;
        [NoAlias] public NativeArray<InteriorGISourceDTO> Sources;
        [NoAlias] public NativeArray<InteriorGIOcclusionCellDTO> Occlusion;
        [NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryScratch;
        [NoAlias] public NativeArray<MockPowerState> Power;
        [NoAlias] public NativeArray<int> Faults;
        [NoAlias] public NativeArray<byte> CsvBytes;
        [NoAlias] public NativeArray<AmbientLightingProfileDTO> AmbientProfiles;
        [NoAlias] public NativeArray<int> AmbientProfileCount;

        public void Execute()
        {
            for (int i = 0; i < ProbeFront.Length; i++)
            {
                ProbeFront[i] = default;
                ProbeBack[i] = default;
            }

            for (int i = 0; i < Sources.Length; i++)
                Sources[i] = default;

            for (int i = 0; i < Occlusion.Length; i++)
                Occlusion[i] = default;

            for (int i = 0; i < TelemetryRing.Length; i++)
                TelemetryRing[i] = default;

            for (int i = 0; i < TelemetryScratch.Length; i++)
                TelemetryScratch[i] = default;

            for (int i = 0; i < Power.Length; i++)
                Power[i] = default;

            for (int i = 0; i < Faults.Length; i++)
                Faults[i] = 0;

            for (int i = 0; i < CsvBytes.Length; i++)
                CsvBytes[i] = 0;

            for (int i = 0; i < AmbientProfiles.Length; i++)
                AmbientProfiles[i] = default;

            for (int i = 0; i < AmbientProfileCount.Length; i++)
                AmbientProfileCount[i] = 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorGIProbeGridClearJob : IJob
    {
        [NoAlias] public NativeArray<CustomLightProbeDTO> ProbeFront;
        [NoAlias] public NativeArray<CustomLightProbeDTO> ProbeBack;
        [NoAlias] public NativeArray<int> Faults;
        [NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryScratch;

        public void Execute()
        {
            int probeCount = math.min(ProbeFront.Length, ProbeBack.Length);
            for (int i = 0; i < probeCount; i++)
            {
                ProbeFront[i] = default;
                ProbeBack[i] = default;
            }

            for (int i = 0; i < Faults.Length; i++)
                Faults[i] = 0;

            for (int i = 0; i < TelemetryScratch.Length; i++)
                TelemetryScratch[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CustomLightProbeGpuUploadJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Source;
        [WriteOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Destination;
        public int Count;

        public void Execute()
        {
            int safeCount = math.min(Count, math.min(Source.Length, Destination.Length));
            if (safeCount <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Destination);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)safeCount * UnsafeUtility.SizeOf<CustomLightProbeDTO>());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockProbeGridJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Front;
        [WriteOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Back;
        public InteriorGITuningDTO Tuning;

        public void Execute(int index)
        {
            int3 coord = InteriorGIProbeMath.IndexToCoord(index, Tuning.Resolution);
            float denom = math.max(1f, Tuning.Resolution - 1f);
            float3 uvw = new float3(coord.x, coord.y, coord.z) / denom;
            float depth01 = math.saturate(1f - uvw.y);
            float sideGlow = math.saturate(1f - math.abs(uvw.x - 0.5f) * 2f);
            float causticLie = 0.85f + 0.15f * MathLodApproximation.ApproxSinBhaskara((coord.x * 12.9898f) + (coord.z * 78.233f) + (Tuning.FrameIndex * 0.071f));
            float3 color = math.lerp(new float3(0.02f, 0.035f, 0.07f), new float3(0.08f, 0.64f, 0.82f), math.saturate(1f - depth01));
            color += new float3(0.02f, 0.18f, 0.13f) * sideGlow * causticLie * math.saturate(Tuning.GlobalQualityWeight);

            CustomLightProbeDTO probe = default;
            InteriorGIProbeMath.AddDirectional(
                ref probe,
                color,
                1f,
                new float3(0f, -0.65f, 0.35f),
                Tuning.DirectionalWeight,
                Tuning.L2Weight);
            InteriorGIProbeMath.WriteProbeMetadata(ref probe, coord, Tuning.RootHash, InteriorGIProbeVolumeRuntime.TelemetryFlagMock);
            Front[index] = probe;
            Back[index] = probe;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateProbeLightingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Probes;
        [ReadOnly, NoAlias] public NativeArray<double3> EntityAup;
        [WriteOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Output;
        public CustomLightProbeDTO GlobalFallback;
        public InteriorGITuningDTO Tuning;
        public int EntityCount;

        public void Execute(int index)
        {
            if (index >= EntityCount)
                return;

            float quality = math.saturate(Tuning.GlobalQualityWeight);
            float fallbackWeight = 1f - Smooth01((quality - 0.04f) * 8.333334f);
            if (fallbackWeight >= 0.999f)
            {
                Output[index] = GlobalFallback;
                return;
            }

            double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(EntityAup[index], Tuning.RootAup);
            float3 local = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
            if (!math.all(math.isfinite(local)))
            {
                Output[index] = GlobalFallback;
                return;
            }

            float invCell = 1f / math.max(0.0001f, Tuning.CellSizeMeters);
            float3 grid = local * invCell - new float3(0.5f);
            float3 clamped = math.clamp(grid, new float3(0f), new float3(math.max(0, Tuning.Resolution - 1)));
            int3 baseCoord = (int3)math.floor(clamped);
            int3 nextCoord = math.min(baseCoord + 1, new int3(Tuning.Resolution - 1));
            float3 frac = math.saturate(clamped - baseCoord);
            float triWeight = Smooth01((quality - 0.22f) * 4.0f);
            frac *= triWeight;

            CustomLightProbeDTO sample = default;
            Accumulate(ref sample, baseCoord, new float3(1f - frac.x, 1f - frac.y, 1f - frac.z));
            Accumulate(ref sample, new int3(nextCoord.x, baseCoord.y, baseCoord.z), new float3(frac.x, 1f - frac.y, 1f - frac.z));
            Accumulate(ref sample, new int3(baseCoord.x, nextCoord.y, baseCoord.z), new float3(1f - frac.x, frac.y, 1f - frac.z));
            Accumulate(ref sample, new int3(nextCoord.x, nextCoord.y, baseCoord.z), new float3(frac.x, frac.y, 1f - frac.z));
            Accumulate(ref sample, new int3(baseCoord.x, baseCoord.y, nextCoord.z), new float3(1f - frac.x, 1f - frac.y, frac.z));
            Accumulate(ref sample, new int3(nextCoord.x, baseCoord.y, nextCoord.z), new float3(frac.x, 1f - frac.y, frac.z));
            Accumulate(ref sample, new int3(baseCoord.x, nextCoord.y, nextCoord.z), new float3(1f - frac.x, frac.y, frac.z));
            Accumulate(ref sample, nextCoord, frac);

            Blend(ref sample, in GlobalFallback, fallbackWeight);
            InteriorGIProbeMath.WriteProbeMetadata(ref sample, baseCoord, Tuning.RootHash, 0u);
            Output[index] = sample;
        }

        private void Accumulate(ref CustomLightProbeDTO sample, int3 coord, float3 weight3)
        {
            float weight = weight3.x * weight3.y * weight3.z;
            if (weight <= 0.000001f)
                return;

            int probeIndex = coord.x + coord.y * Tuning.Resolution + coord.z * Tuning.Resolution * Tuning.Resolution;
            if ((uint)probeIndex >= (uint)math.min(Probes.Length, Tuning.ActiveProbeCount))
                return;

            CustomLightProbeDTO probe = Probes[probeIndex];
            InteriorGIProbeMath.AddScaled(ref sample, in probe, weight, Tuning.DirectionalWeight, Tuning.L2Weight);
        }

        private static void Blend(ref CustomLightProbeDTO dst, in CustomLightProbeDTO fallback, float weight)
        {
            float w = math.saturate(weight);
            if (w <= 0.000001f)
                return;

            float keep = 1f - w;
            dst.R0 = dst.R0 * keep + fallback.R0 * w;
            dst.G0 = dst.G0 * keep + fallback.G0 * w;
            dst.B0 = dst.B0 * keep + fallback.B0 * w;
            dst.R1 *= keep;
            dst.R2 *= keep;
            dst.R3 *= keep;
            dst.R4 *= keep;
            dst.R5 *= keep;
            dst.R6 *= keep;
            dst.R7 *= keep;
            dst.R8 *= keep;
            dst.G1 *= keep;
            dst.G2 *= keep;
            dst.G3 *= keep;
            dst.G4 *= keep;
            dst.G5 *= keep;
            dst.G6 *= keep;
            dst.G7 *= keep;
            dst.G8 *= keep;
            dst.B1 *= keep;
            dst.B2 *= keep;
            dst.B3 *= keep;
            dst.B4 *= keep;
            dst.B5 *= keep;
            dst.B6 *= keep;
            dst.B7 *= keep;
            dst.B8 *= keep;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct UpdateProbeOcclusionJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CustomLightProbeDTO> Probes;
        [ReadOnly, NoAlias] public NativeArray<InteriorGIOcclusionCellDTO> Occlusion;
        public InteriorGITuningDTO Tuning;
        public float OcclusionStrength;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)math.min(Probes.Length, Tuning.ActiveProbeCount))
                return;

            InteriorGIOcclusionCellDTO cell = index < Occlusion.Length ? Occlusion[index] : default;
            float sdf = index < Occlusion.Length ? cell.SignedDistanceMeters : Tuning.CellSizeMeters;
            float safeSdf = math.isfinite(sdf) ? sdf : Tuning.CellSizeMeters;
            float inside = 1f - math.step(0f, safeSdf);
            float proximity = 1f - math.saturate(safeSdf / math.max(0.0001f, Tuning.CellSizeMeters));
            float darken = 1f - math.saturate((inside + proximity * 0.65f) * math.saturate(OcclusionStrength));
            CustomLightProbeDTO probe = Probes[index];
            ScaleProbe(ref probe, darken);
            InteriorGIProbeMath.MultiplyRgb(ref probe, InteriorGIProbeMath.UnpackRgb10(Tuning.PackedBiomeTint));
            probe.Flags |= cell.Flags;
            probe.Flags |= inside > 0.5f ? InteriorGIProbeVolumeRuntime.OcclusionFlagSolid : 0u;
            Probes[index] = probe;
        }

        private static void ScaleProbe(ref CustomLightProbeDTO probe, float scale)
        {
            float s = math.saturate(scale);
            probe.R0 *= s;
            probe.R1 *= s;
            probe.R2 *= s;
            probe.R3 *= s;
            probe.R4 *= s;
            probe.R5 *= s;
            probe.R6 *= s;
            probe.R7 *= s;
            probe.R8 *= s;
            probe.G0 *= s;
            probe.G1 *= s;
            probe.G2 *= s;
            probe.G3 *= s;
            probe.G4 *= s;
            probe.G5 *= s;
            probe.G6 *= s;
            probe.G7 *= s;
            probe.G8 *= s;
            probe.B0 *= s;
            probe.B1 *= s;
            probe.B2 *= s;
            probe.B3 *= s;
            probe.B4 *= s;
            probe.B5 *= s;
            probe.B6 *= s;
            probe.B7 *= s;
            probe.B8 *= s;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InjectDynamicLightJob : IJob
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CustomLightProbeDTO> Probes;
        [ReadOnly, NoAlias] public NativeArray<CustomDynamicProbeLightDTO> Lights;
        public InteriorGITuningDTO Tuning;
        public int LightCount;

        public void Execute()
        {
            int count = math.min(LightCount, Lights.Length);
            float bounceWeight = Smooth01(Tuning.GlobalQualityWeight);
            for (int i = 0; i < count; i++)
            {
                CustomDynamicProbeLightDTO light = Lights[i];
                float3 lightLocal = new float3(
                    (float)(light.AUP.x - Tuning.RootAup.x),
                    (float)(light.AUP.y - Tuning.RootAup.y),
                    (float)(light.AUP.z - Tuning.RootAup.z));
                if (!math.all(math.isfinite(lightLocal)))
                    continue;

                float invCell = 1f / math.max(0.0001f, Tuning.CellSizeMeters);
                float3 grid = lightLocal * invCell - new float3(0.5f);
                int3 baseCoord = (int3)math.floor(math.clamp(grid, new float3(0f), new float3(math.max(0, Tuning.Resolution - 1))));
                int3 nextCoord = math.min(baseCoord + 1, new int3(Tuning.Resolution - 1));
                float3 frac = math.saturate(grid - baseCoord);
                Inject(baseCoord, new float3(1f - frac.x, 1f - frac.y, 1f - frac.z), light, bounceWeight);
                Inject(new int3(nextCoord.x, baseCoord.y, baseCoord.z), new float3(frac.x, 1f - frac.y, 1f - frac.z), light, bounceWeight);
                Inject(new int3(baseCoord.x, nextCoord.y, baseCoord.z), new float3(1f - frac.x, frac.y, 1f - frac.z), light, bounceWeight);
                Inject(new int3(nextCoord.x, nextCoord.y, baseCoord.z), new float3(frac.x, frac.y, 1f - frac.z), light, bounceWeight);
                Inject(new int3(baseCoord.x, baseCoord.y, nextCoord.z), new float3(1f - frac.x, 1f - frac.y, frac.z), light, bounceWeight);
                Inject(new int3(nextCoord.x, baseCoord.y, nextCoord.z), new float3(frac.x, 1f - frac.y, frac.z), light, bounceWeight);
                Inject(new int3(baseCoord.x, nextCoord.y, nextCoord.z), new float3(1f - frac.x, frac.y, frac.z), light, bounceWeight);
                Inject(nextCoord, frac, light, bounceWeight);
            }
        }

        private void Inject(int3 coord, float3 weights, CustomDynamicProbeLightDTO light, float bounceWeight)
        {
            float w = weights.x * weights.y * weights.z * bounceWeight;
            if (w <= 0.000001f)
                return;

            int probeIndex = coord.x + coord.y * Tuning.Resolution + coord.z * Tuning.Resolution * Tuning.Resolution;
            if ((uint)probeIndex >= (uint)math.min(Probes.Length, Tuning.ActiveProbeCount))
                return;

            CustomLightProbeDTO probe = Probes[probeIndex];
            float gain = math.max(0f, light.Intensity) * w * math.saturate(Tuning.SimulationDelta) / math.max(0.0001f, light.RadiusMeters);
            float3 direction = math.normalizesafe(light.Direction, new float3(0f, 1f, 0f));
            InteriorGIProbeMath.AddDirectional(ref probe, light.Color, gain, direction, Tuning.DirectionalWeight, Tuning.L2Weight);
            InteriorGIProbeMath.SanitizeAndClamp(ref probe, 32f);
            Probes[probeIndex] = probe;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorGIMockPowerJob : IJob
    {
        public int FrameIndex;
        public float EmergencyOverride01;
        [NoAlias] public NativeArray<MockPowerState> Power;

        public void Execute()
        {
            float phase = (FrameIndex & 255) * (1f / 255f);
            float triangle = 1f - math.abs((phase * 2f) - 1f);
            float outage = math.saturate((triangle - 0.72f) * 3.58f);
            float emergency = math.max(EmergencyOverride01, outage);
            Power[0] = new MockPowerState
            {
                MainPower01 = 1f - outage,
                Emergency01 = emergency,
                DoorOpen01 = 0.25f + 0.75f * (1f - outage),
                OutagePhase01 = phase,
                FrameIndex = FrameIndex,
                Flags = outage > 0.02f ? InteriorGIProbeVolumeRuntime.TelemetryFlagEmergency : 0u,
                SourceMask = 0xFFFFFFFFu,
                _pad0 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorGIPropagationJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Front;
        [WriteOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Back;
        [ReadOnly, NoAlias] public NativeArray<InteriorGISourceDTO> Sources;
        [ReadOnly, NoAlias] public NativeArray<InteriorGIOcclusionCellDTO> Occlusion;
        [WriteOnly, NoAlias] public NativeArray<int> Faults;
        [ReadOnly, NoAlias] public NativeArray<MockPowerState> Power;
        public InteriorGITuningDTO Tuning;

        public void Execute(int index)
        {
            int resolution = Tuning.Resolution;
            int3 coord = InteriorGIProbeMath.IndexToCoord(index, resolution);
            InteriorGIOcclusionCellDTO currentCell = Occlusion[index];
            CustomLightProbeDTO current = Front[index];
            CustomLightProbeDTO result = default;
            if ((currentCell.Flags & InteriorGIProbeVolumeRuntime.OcclusionFlagSolid) == 0u && currentCell.SignedDistanceMeters > 0f)
            {
                InteriorGIProbeMath.AddScaled(ref result, in current, Tuning.AmbientRetain, Tuning.DirectionalWeight, Tuning.L2Weight);
                float transferBase = Tuning.SimulationDelta * Tuning.PropagationSpeed * Tuning.TransferDamping * 0.16666667f;
                AccumulateNeighbor(ref result, index, coord, new int3(-1, 0, 0), InteriorGIProbeVolumeRuntime.WallNegX, InteriorGIProbeVolumeRuntime.WallPosX, transferBase);
                AccumulateNeighbor(ref result, index, coord, new int3(1, 0, 0), InteriorGIProbeVolumeRuntime.WallPosX, InteriorGIProbeVolumeRuntime.WallNegX, transferBase);
                AccumulateNeighbor(ref result, index, coord, new int3(0, -1, 0), InteriorGIProbeVolumeRuntime.WallNegY, InteriorGIProbeVolumeRuntime.WallPosY, transferBase);
                AccumulateNeighbor(ref result, index, coord, new int3(0, 1, 0), InteriorGIProbeVolumeRuntime.WallPosY, InteriorGIProbeVolumeRuntime.WallNegY, transferBase);
                AccumulateNeighbor(ref result, index, coord, new int3(0, 0, -1), InteriorGIProbeVolumeRuntime.WallNegZ, InteriorGIProbeVolumeRuntime.WallPosZ, transferBase);
                AccumulateNeighbor(ref result, index, coord, new int3(0, 0, 1), InteriorGIProbeVolumeRuntime.WallPosZ, InteriorGIProbeVolumeRuntime.WallNegZ, transferBase);
                InjectSources(ref result, coord, currentCell);
                InjectOcclusionGlow(ref result, currentCell);
            }

            InteriorGIProbeMath.SanitizeAndClamp(ref result, 32f);
            int fault = IsProbeFinite(in result) ? 0 : 1;
            if (fault != 0)
                result = default;

            InteriorGIProbeMath.WriteProbeMetadata(ref result, coord, Tuning.RootHash, currentCell.Flags);
            Back[index] = result;
            Faults[index] = fault;
        }

        private void AccumulateNeighbor(ref CustomLightProbeDTO result, int currentIndex, int3 coord, int3 delta, uint wallBit, uint oppositeWallBit, float transferBase)
        {
            int3 neighborCoord = coord + delta;
            if (neighborCoord.x < 0 || neighborCoord.y < 0 || neighborCoord.z < 0 ||
                neighborCoord.x >= Tuning.Resolution || neighborCoord.y >= Tuning.Resolution || neighborCoord.z >= Tuning.Resolution)
            {
                return;
            }

            InteriorGIOcclusionCellDTO currentCell = Occlusion[currentIndex];
            if ((currentCell.WallMask & wallBit) != 0u || currentCell.SignedDistanceMeters <= 0f)
                return;

            int neighborIndex = neighborCoord.x + neighborCoord.y * Tuning.Resolution + neighborCoord.z * Tuning.Resolution * Tuning.Resolution;
            InteriorGIOcclusionCellDTO neighborCell = Occlusion[neighborIndex];
            if ((neighborCell.WallMask & oppositeWallBit) != 0u ||
                (neighborCell.Flags & InteriorGIProbeVolumeRuntime.OcclusionFlagSolid) != 0u ||
                neighborCell.SignedDistanceMeters <= 0f)
            {
                return;
            }

            float sdf = math.saturate(math.min(currentCell.SignedDistanceMeters, neighborCell.SignedDistanceMeters) / math.max(0.01f, Tuning.CellSizeMeters));
            float water = 1f - math.saturate(math.max(currentCell.Water01, neighborCell.Water01)) * Tuning.WaterAbsorption;
            float wallProximity = 1f - sdf;
            float wallLoss = 1f - (math.saturate(Tuning.WallAbsorption) * wallProximity * 0.75f);
            float transfer = transferBase * sdf * water * wallLoss * math.saturate(currentCell.TransferScale01) * math.saturate(neighborCell.TransferScale01);
            if (transfer <= 0.00001f)
                return;

            CustomLightProbeDTO neighbor = Front[neighborIndex];
            InteriorGIProbeMath.AddScaled(ref result, in neighbor, transfer, Tuning.DirectionalWeight, Tuning.L2Weight);
        }

        private void InjectSources(ref CustomLightProbeDTO result, int3 coord, InteriorGIOcclusionCellDTO currentCell)
        {
            MockPowerState power = Power[0];
            float3 cellLocal = new float3(coord.x + 0.5f, coord.y + 0.5f, coord.z + 0.5f) * Tuning.CellSizeMeters;
            int sourceCount = math.min(Tuning.SourceCount, math.min(Tuning.SourceSampleLimit, Sources.Length));
            for (int i = 0; i < sourceCount; i++)
            {
                InteriorGISourceDTO source = Sources[i];
                float3 sourceLocal = new float3(
                    (float)(source.AUP.x - Tuning.RootAup.x),
                    (float)(source.AUP.y - Tuning.RootAup.y),
                    (float)(source.AUP.z - Tuning.RootAup.z));
                if (!math.all(math.isfinite(sourceLocal)))
                    continue;

                float3 toCell = cellLocal - sourceLocal;
                float radius = math.max(0.25f, source.RadiusMeters);
                float distanceSq = math.lengthsq(toCell);
                if (distanceSq > radius * radius)
                    continue;

                float distance = math.sqrt(math.max(0f, distanceSq));
                float falloff = 1f - math.saturate(distance / radius);
                falloff *= falloff;
                float powered = ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagPowered) != 0u) ? power.MainPower01 : 1f;
                if ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagAlwaysOn) != 0u)
                    powered = math.max(powered, 0.35f);

                float emergency = ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagEmergency) != 0u) ? math.max(power.Emergency01, Tuning.EmergencyOverride01) : 1f;
                float flora = ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagFlora) != 0u)
                    ? Tuning.FloraGlowScale * (0.7f + 0.3f * MathLodApproximation.ApproxSinBhaskara((Tuning.FrameIndex + source.Phase01 * 32f) * 0.17f))
                    : 1f;
                float flashlight = ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagFlashlight) != 0u) ? Tuning.FlashlightIntensity : 1f;
                float water = 1f - math.saturate(currentCell.Water01) * Tuning.WaterAbsorption * math.max(0f, source.WaterAbsorptionScalar);
                float gain = source.Intensity * falloff * Tuning.SimulationDelta * powered * emergency * flora * flashlight * water;
                if (gain <= 0.00001f)
                    continue;

                float redOverride = math.max(Tuning.RedOverride01, ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagEmergency) != 0u) ? power.Emergency01 : 0f);
                float3 color = math.lerp(source.Color, new float3(2.2f, 0.02f, 0.01f), math.saturate(redOverride));
                float3 direction = ((source.Flags & InteriorGIProbeVolumeRuntime.SourceFlagFlashlight) != 0u)
                    ? source.Direction
                    : math.normalizesafe(toCell, new float3(0f, 1f, 0f));
                InteriorGIProbeMath.AddDirectional(ref result, color, gain, direction, Tuning.DirectionalWeight, Tuning.L2Weight);
            }
        }

        private void InjectOcclusionGlow(ref CustomLightProbeDTO result, InteriorGIOcclusionCellDTO currentCell)
        {
            if (currentCell.FloraGlow01 > 0.0001f)
            {
                float pulse = 0.75f + 0.25f * MathLodApproximation.ApproxSinBhaskara(Tuning.FrameIndex * 0.13f + currentCell.RoomHash * 0.0001f);
                InteriorGIProbeMath.AddDirectional(ref result, new float3(0.08f, 0.75f, 0.42f), currentCell.FloraGlow01 * Tuning.FloraGlowScale * pulse * Tuning.SimulationDelta, new float3(0f, 1f, 0f), Tuning.DirectionalWeight, Tuning.L2Weight);
            }

            float emergencyReflect = math.saturate(currentCell.EmergencyReflectance01 * Tuning.EmergencyOverride01);
            if (emergencyReflect > 0.0001f)
                InteriorGIProbeMath.AddDirectional(ref result, new float3(1.8f, 0.03f, 0.01f), emergencyReflect * Tuning.EmergencyLightIntensity * Tuning.SimulationDelta, new float3(0f, 0f, 1f), Tuning.DirectionalWeight, Tuning.L2Weight);
        }

        private static bool IsProbeFinite(in CustomLightProbeDTO p)
        {
            return math.isfinite(p.R0) && math.isfinite(p.G0) && math.isfinite(p.B0) &&
                   math.isfinite(p.R1) && math.isfinite(p.R2) && math.isfinite(p.R3) &&
                   math.isfinite(p.R4) && math.isfinite(p.R5) && math.isfinite(p.R6) &&
                   math.isfinite(p.R7) && math.isfinite(p.R8) && math.isfinite(p.G1) &&
                   math.isfinite(p.G2) && math.isfinite(p.G3) && math.isfinite(p.G4) &&
                   math.isfinite(p.G5) && math.isfinite(p.G6) && math.isfinite(p.G7) &&
                   math.isfinite(p.G8) && math.isfinite(p.B1) && math.isfinite(p.B2) &&
                   math.isfinite(p.B3) && math.isfinite(p.B4) && math.isfinite(p.B5) &&
                   math.isfinite(p.B6) && math.isfinite(p.B7) && math.isfinite(p.B8);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteriorGITelemetryScanJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CustomLightProbeDTO> Probes;
        [ReadOnly, NoAlias] public NativeArray<int> Faults;
        [NoAlias] public NativeArray<InteriorGITelemetryEntry> Scratch;
        public InteriorGITuningDTO Tuning;

        public void Execute()
        {
            float sum = 0f;
            float max = 0f;
            int nanCount = 0;
            uint hash = 2166136261u;
            for (int i = 0; i < Tuning.ActiveProbeCount; i++)
            {
                CustomLightProbeDTO probe = Probes[i];
                float luma = InteriorGIProbeMath.LuminanceL0(in probe);
                sum += luma;
                max = math.max(max, luma);
                nanCount += Faults[i] != 0 ? 1 : 0;
                if ((i & 31) == 0)
                    hash = InteriorGIProbeMath.HashProbe(in probe, hash);
            }

            uint flags = nanCount > 0 ? InteriorGIProbeVolumeRuntime.TelemetryFlagNan : 0u;
            if (Tuning.EmergencyOverride01 > 0.001f)
                flags |= InteriorGIProbeVolumeRuntime.TelemetryFlagEmergency;
            flags |= InteriorGIProbeVolumeRuntime.TelemetryFlagMock;
            Scratch[0] = new InteriorGITelemetryEntry
            {
                FrameIndex = Tuning.FrameIndex,
                ActiveProbeCount = Tuning.ActiveProbeCount,
                SourceCount = Tuning.SourceCount,
                SourceSampleLimit = Tuning.SourceSampleLimit,
                GlobalQualityWeight = Tuning.GlobalQualityWeight,
                SolverCompleteMs = 0f,
                MaxL0 = max,
                AverageL0 = Tuning.ActiveProbeCount > 0 ? sum / Tuning.ActiveProbeCount : 0f,
                NaNCount = nanCount,
                Flags = flags,
                GridHash = hash,
                RootHash = Tuning.RootHash,
                WaterAbsorption = Tuning.WaterAbsorption,
                DirectionalWeight = Tuning.DirectionalWeight,
                BouncesEstimated = Tuning.ActiveProbeCount * 6f * math.max(0.25f, Tuning.PropagationSpeed),
                _pad0 = 0u
            };
        }
    }
}
