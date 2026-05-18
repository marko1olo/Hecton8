using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct LightProbeDTO
    {
        [FieldOffset(0)] public float R0;
        [FieldOffset(4)] public float R1;
        [FieldOffset(8)] public float R2;
        [FieldOffset(12)] public float R3;
        [FieldOffset(16)] public float R4;
        [FieldOffset(20)] public float R5;
        [FieldOffset(24)] public float R6;
        [FieldOffset(28)] public float R7;
        [FieldOffset(32)] public float R8;
        [FieldOffset(36)] public float G0;
        [FieldOffset(40)] public float G1;
        [FieldOffset(44)] public float G2;
        [FieldOffset(48)] public float G3;
        [FieldOffset(52)] public float G4;
        [FieldOffset(56)] public float G5;
        [FieldOffset(60)] public float G6;
        [FieldOffset(64)] public float G7;
        [FieldOffset(68)] public float G8;
        [FieldOffset(72)] public float B0;
        [FieldOffset(76)] public float B1;
        [FieldOffset(80)] public float B2;
        [FieldOffset(84)] public float B3;
        [FieldOffset(88)] public float B4;
        [FieldOffset(92)] public float B5;
        [FieldOffset(96)] public float B6;
        [FieldOffset(100)] public float B7;
        [FieldOffset(104)] public float B8;
        [FieldOffset(108)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct InteriorGITextureVoxelDTO
    {
        [FieldOffset(0)] public half R;
        [FieldOffset(2)] public half G;
        [FieldOffset(4)] public half B;
        [FieldOffset(6)] public half A;
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
        [FieldOffset(124)] public uint _pad1;
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

    [DisallowMultipleComponent]
    [AddComponentMenu("HECTON-8/Lighting/Interior GI Probe Volume Runtime")]
    public sealed unsafe class InteriorGIProbeVolumeRuntime : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IScalabilityChangedEventListener
    {
        public const int MaxResolution = 32;
        public const int MinResolution = 12;
        public const int MaxCellCount = MaxResolution * MaxResolution * MaxResolution;
        public const int MaxSourceCount = 128;
        public const int TelemetryCapacity = 300;
        public const int CsvBufferBytes = 32768;

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

        private const SystemID MemoryOwner = SystemID.GraphicsScalability;
        private const BufferID ProbeFrontBuffer = (BufferID)0x630800;
        private const BufferID ProbeBackBuffer = (BufferID)0x630801;
        private const BufferID ProbeSourcesBuffer = (BufferID)0x630802;
        private const BufferID ProbeOcclusionBuffer = (BufferID)0x630803;
        private const BufferID ProbeTuningBuffer = (BufferID)0x630804;
        private const BufferID ProbeTelemetryRingBuffer = (BufferID)0x630805;
        private const BufferID ProbeTelemetryScratchBuffer = (BufferID)0x630806;
        private const BufferID ProbeTextureUploadBuffer = (BufferID)0x630807;
        private const BufferID ProbeMockPowerBuffer = (BufferID)0x630808;
        private const BufferID ProbeFaultBuffer = (BufferID)0x630809;
        private const BufferID ProbeCsvBytesBuffer = (BufferID)0x63080A;

        private static readonly int InteriorGITextureId = Shader.PropertyToID("_H8InteriorGIProbeVolume");
        private static readonly int InteriorGIParamsId = Shader.PropertyToID("_H8InteriorGIProbeParams");
        private static readonly int InteriorGIOriginId = Shader.PropertyToID("_H8InteriorGIProbeOrigin");
        private static readonly int InteriorGIRootAupId = Shader.PropertyToID("_H8InteriorGIProbeRootAup");

        [Header("Grid")]
        [SerializeField, Min(1f)] private float cellSizeMeters = 3.5f;
        [SerializeField, Range(MinResolution, MaxResolution)] private int editorPreviewResolution = 24;
        [SerializeField] private bool forceEditorResolution;
        [SerializeField] private bool enableMockLighting = true;
        [SerializeField] private bool enableMockOcclusion = true;
        [SerializeField] private bool enableTextureUpload = true;
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

        private IDataVault _vault;
        private static GlobalDataVault _standaloneVault;
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
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredScalability;
        private bool _registeredOriginShift;
        private bool _nativeReady;
        private bool _mockSourcesSeeded;
        private bool _mockOcclusionSeeded;
        private bool _visualDirty;
        private bool _simulationJobActive;
        private bool _scheduledFinalBufferIsBack = true;
        private bool _nanDumpWritten;
        private bool _csvReloadRequested;
        private JobHandle _simulationHandle;
        private Texture3D _stagingTexture;
        private Texture3D _publishedTexture;
        private int _textureResolution;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;

        private VaultBufferHandle<LightProbeDTO> _probeFront;
        private VaultBufferHandle<LightProbeDTO> _probeBack;
        private VaultBufferHandle<InteriorGISourceDTO> _sources;
        private VaultBufferHandle<InteriorGIOcclusionCellDTO> _occlusion;
        private VaultBufferHandle<InteriorGITuningDTO> _tuning;
        private VaultBufferHandle<InteriorGITelemetryEntry> _telemetryRing;
        private VaultBufferHandle<InteriorGITelemetryEntry> _telemetryScratch;
        private VaultBufferHandle<InteriorGITextureVoxelDTO> _textureUpload;
        private VaultBufferHandle<MockPowerState> _mockPower;
        private VaultBufferHandle<int> _faults;
        private VaultBufferHandle<byte> _csvBytes;

        private int ActiveCellCount => _activeResolution * _activeResolution * _activeResolution;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        private void OnEnable()
        {
            _cachedTransform = transform;
            EnsureNativeState();
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
            ReleaseTextures();
        }

        public void Tick(float deltaTime)
        {
            EnsureNativeState();
            TryRegister();

            if (_simulationJobActive)
                return;

            float quality = ResolveQualityWeight();
            ResolveActiveResolution(quality);
            if (enableMockLighting)
                EnsureMockSources();
            if (enableMockOcclusion)
                EnsureMockOcclusionGrid();

            float cadence = ResolveCadenceSeconds(quality);
            _solverAccumulator += math.max(0f, deltaTime);
            _visualUploadAccumulator += math.max(0f, deltaTime);
            if (_solverAccumulator < cadence)
                return;

            float dt = math.min(_solverAccumulator, 0.5f);
            _solverAccumulator = 0f;
            InteriorGITuningDTO tuning = BuildTuning(quality, dt, cadence);
            ref InteriorGITuningDTO stored = ref _tuning.GetElementAsRef(EnsureVault(), 0);
            stored = tuning;
            ScheduleSimulation(tuning);
        }

        public void SlowTick()
        {
#if UNITY_EDITOR
            if (!_nativeReady)
                EnsureNativeState();

            if (!enableCsvOverridePolling)
                return;

            _csvPollTimer -= 0.1f;
            if (_csvPollTimer > 0f)
                return;

            _csvPollTimer = 2.0f;
            _csvReloadRequested = true;
            TryReloadCsvOverrides();
#endif
        }

        public void LateFrameTick()
        {
            if (!_simulationJobActive || !_simulationHandle.IsCompleted)
                return;

            long start = Stopwatch.GetTimestamp();
            _simulationHandle.Complete();
            long end = Stopwatch.GetTimestamp();
            _lastCompleteMs = (float)((end - start) * 1000.0 / Stopwatch.Frequency);
            _simulationHandle = default;
            _simulationJobActive = false;

            if (_scheduledFinalBufferIsBack)
                SwapFrontBack();
            else
                _visualDirty = true;
            _gridVersion++;
            CommitTelemetryScratch();
            UploadVisualTextureIfDirty();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            _rootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(_cachedTransform.position);
            _rootHash = HashAup(_rootAup);
            _visualDirty = true;
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedQualityTier = payload.CurrentQualityTier;
        }

        public bool TryGetProbeGridReadback(out NativeArray<LightProbeDTO> probes, out int resolution, out double3 rootAup, out float cellSize, out int version)
        {
            probes = default;
            resolution = _activeResolution;
            rootAup = _rootAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;
            if (_simulationJobActive || !_probeFront.IsCreated)
                return false;

            probes = ResolveArray(ref _probeFront);
            return probes.IsCreated;
        }

        public bool TryGetOcclusionReadback(out NativeArray<InteriorGIOcclusionCellDTO> occlusion, out int resolution)
        {
            occlusion = default;
            resolution = _activeResolution;
            if (_simulationJobActive || !_occlusion.IsCreated)
                return false;

            occlusion = ResolveArray(ref _occlusion);
            return occlusion.IsCreated;
        }

        public bool TryGetTelemetryReadback(out NativeArray<InteriorGITelemetryEntry> telemetry, out int cursor)
        {
            telemetry = default;
            cursor = _telemetryCursor;
            if (_simulationJobActive || !_telemetryRing.IsCreated)
                return false;

            telemetry = ResolveArray(ref _telemetryRing);
            return telemetry.IsCreated;
        }

        public bool TryGetTuningCopy(out InteriorGITuningDTO tuning)
        {
            tuning = default;
            if (!_tuning.IsCreated)
                return false;

            tuning = _tuning.GetElementAsRef(EnsureVault(), 0);
            return true;
        }

        public bool TryWriteOcclusionCell(int3 cell, float signedDistanceMeters, uint wallMask, float water01, float transferScale01, float floraGlow01, uint roomHash)
        {
            if (_simulationJobActive || !_occlusion.IsCreated)
                return false;

            if (!IsInside(cell, _activeResolution))
                return false;

            NativeArray<InteriorGIOcclusionCellDTO> occlusion = ResolveArray(ref _occlusion);
            int index = ToIndex(cell, _activeResolution);
            occlusion[index] = new InteriorGIOcclusionCellDTO
            {
                SignedDistanceMeters = math.isfinite(signedDistanceMeters) ? signedDistanceMeters : cellSizeMeters,
                Water01 = math.saturate(water01),
                TransferScale01 = math.saturate(transferScale01),
                WallMask = wallMask,
                FloraGlow01 = math.saturate(floraGlow01),
                EmergencyReflectance01 = 0.2f,
                RoomHash = roomHash,
                Flags = signedDistanceMeters <= 0f ? OcclusionFlagSolid : 0u
            };
            _visualDirty = true;
            return true;
        }

        public bool TryUpsertSource(uint sourceHash, double3 aup, float3 color, float intensity, float radiusMeters, uint flags, float3 direction)
        {
            if (sourceHash == 0u || _simulationJobActive || !_sources.IsCreated || !math.all(math.isfinite(aup)))
                return false;

            NativeArray<InteriorGISourceDTO> sources = ResolveArray(ref _sources);
            float safeIntensity = math.max(0f, math.isfinite(intensity) ? intensity : 0f);
            float safeRadius = math.max(0.25f, math.isfinite(radiusMeters) ? radiusMeters : 1f);
            float3 safeColor = math.select(new float3(1f, 0.2f, 0.1f), math.max(new float3(0f), color), math.all(math.isfinite(color)));
            float3 safeDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));

            for (int i = 0; i < _sourceCount; i++)
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

        public void RequestCsvReload()
        {
#if UNITY_EDITOR
            _csvReloadRequested = true;
            TryReloadCsvOverrides();
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
            if (_tuning.IsCreated && !_simulationJobActive)
            {
                ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
                tuning.EmergencyOverride01 = emergencyOverride01;
                tuning.RedOverride01 = emergencyOverride01;
            }
        }

        public void SetEditorPropagationSpeed(float value)
        {
            propagationSpeed = math.clamp(value, 0.05f, 4f);
            if (_tuning.IsCreated && !_simulationJobActive)
            {
                ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
                tuning.PropagationSpeed = propagationSpeed;
            }
        }

        public void SetEditorWallAbsorption(float value)
        {
            wallAbsorption = math.saturate(value);
            if (_tuning.IsCreated && !_simulationJobActive)
            {
                ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
                tuning.WallAbsorption = wallAbsorption;
            }
        }

        public void SetEditorEmergencyLightIntensity(float value)
        {
            emergencyLightIntensity = math.max(0f, value);
            if (_tuning.IsCreated && !_simulationJobActive)
            {
                ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
                tuning.EmergencyLightIntensity = emergencyLightIntensity;
            }
        }

        public void SetEditorWaterAbsorption(float value)
        {
            waterAbsorption = math.saturate(value);
            if (_tuning.IsCreated && !_simulationJobActive)
            {
                ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
                tuning.WaterAbsorption = waterAbsorption;
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

        private void EnsureNativeState()
        {
            if (_nativeReady)
                return;

            _vault = ResolveDataVault();
            if (_cachedTransform == null)
                _cachedTransform = transform;

            _rootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(_cachedTransform.position);
            _rootHash = HashAup(_rootAup);
            _activeResolution = ResolveResolutionFromQuality(ResolveQualityWeight());

            _probeFront = AcquireBuffer<LightProbeDTO>(ProbeFrontBuffer, MaxCellCount);
            _probeBack = AcquireBuffer<LightProbeDTO>(ProbeBackBuffer, MaxCellCount);
            _sources = AcquireBuffer<InteriorGISourceDTO>(ProbeSourcesBuffer, MaxSourceCount);
            _occlusion = AcquireBuffer<InteriorGIOcclusionCellDTO>(ProbeOcclusionBuffer, MaxCellCount);
            _tuning = AcquireBuffer<InteriorGITuningDTO>(ProbeTuningBuffer, 1);
            _telemetryRing = AcquireBuffer<InteriorGITelemetryEntry>(ProbeTelemetryRingBuffer, TelemetryCapacity);
            _telemetryScratch = AcquireBuffer<InteriorGITelemetryEntry>(ProbeTelemetryScratchBuffer, 1);
            _textureUpload = AcquireBuffer<InteriorGITextureVoxelDTO>(ProbeTextureUploadBuffer, MaxCellCount);
            _mockPower = AcquireBuffer<MockPowerState>(ProbeMockPowerBuffer, 1);
            _faults = AcquireBuffer<int>(ProbeFaultBuffer, MaxCellCount);
            _csvBytes = AcquireBuffer<byte>(ProbeCsvBytesBuffer, CsvBufferBytes);

            MemClearBuffer(ref _probeFront, MaxCellCount);
            MemClearBuffer(ref _probeBack, MaxCellCount);
            MemClearBuffer(ref _sources, MaxSourceCount);
            MemClearBuffer(ref _occlusion, MaxCellCount);
            MemClearBuffer(ref _telemetryRing, TelemetryCapacity);
            MemClearBuffer(ref _telemetryScratch, 1);
            MemClearBuffer(ref _textureUpload, MaxCellCount);
            MemClearBuffer(ref _mockPower, 1);
            MemClearBuffer(ref _faults, MaxCellCount);
            MemClearBuffer(ref _csvBytes, CsvBufferBytes);

            ref InteriorGITuningDTO tuning = ref _tuning.GetElementAsRef(EnsureVault(), 0);
            tuning = BuildTuning(ResolveQualityWeight(), ResolveCadenceSeconds(ResolveQualityWeight()), ResolveCadenceSeconds(ResolveQualityWeight()));
            if (enableTextureUpload)
                EnsureTextures(_activeResolution);

            _nativeReady = true;
            _mockSourcesSeeded = false;
            _mockOcclusionSeeded = false;
            _visualDirty = true;
        }

        private VaultBufferHandle<T> AcquireBuffer<T>(BufferID bufferId, int length) where T : struct
        {
            VaultBufferHandle<T> handle = EnsureVault().GetBufferHandle<T>(
                bufferId,
                length,
                MemoryOwner,
                NativeArrayOptions.UninitializedMemory);
            if (!handle.IsCreated)
                throw new InvalidOperationException("Interior GI DataVault buffer acquisition failed.");

            return handle;
        }

        private void MemClearBuffer<T>(ref VaultBufferHandle<T> handle, int length) where T : struct
        {
            void* ptr = handle.ResolvePointer(EnsureVault());
            if (ptr == null)
                return;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * length;
            UnsafeUtility.MemClear(ptr, bytes);
        }

        private IDataVault EnsureVault()
        {
            _vault ??= ResolveDataVault();
            if (_vault == null)
                throw new InvalidOperationException("Interior GI GlobalDataVault unavailable.");

            return _vault;
        }

        private static IDataVault ResolveDataVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null)
                return vault;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                return latest;

            _standaloneVault ??= GlobalDataVault.Create(64);
            return _standaloneVault;
        }

        private NativeArray<T> ResolveArray<T>(ref VaultBufferHandle<T> handle) where T : struct
        {
            return handle.Resolve(EnsureVault());
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

            if (!_registeredScalability)
            {
                _cachedQualityTier = GlobalRegistry.ScalabilityTier;
                ScalabilityEvents.Register(this);
                _registeredScalability = true;
            }

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

            if (_registeredScalability)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalability = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }
        }

        private bool ReleaseRuntimeState(bool blockingComplete)
        {
            if (_simulationJobActive)
            {
                if (!blockingComplete && !_simulationHandle.IsCompleted)
                    return false;

                _simulationHandle.Complete();
            }

            _simulationHandle = default;
            _simulationJobActive = false;
            _scheduledFinalBufferIsBack = true;
            _nativeReady = false;
            _probeFront = default;
            _probeBack = default;
            _sources = default;
            _occlusion = default;
            _tuning = default;
            _telemetryRing = default;
            _telemetryScratch = default;
            _textureUpload = default;
            _mockPower = default;
            _faults = default;
            _csvBytes = default;
            return true;
        }

        private void ReleaseTextures()
        {
            if (_stagingTexture != null)
            {
                Destroy(_stagingTexture);
                _stagingTexture = null;
            }

            if (_publishedTexture != null)
            {
                Destroy(_publishedTexture);
                _publishedTexture = null;
            }

            _textureResolution = 0;
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
            if (_mockSourcesSeeded || !_sources.IsCreated || _simulationJobActive)
                return;

            NativeArray<InteriorGISourceDTO> sources = ResolveArray(ref _sources);
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
            if (_mockOcclusionSeeded || !_occlusion.IsCreated || _simulationJobActive)
                return;

            NativeArray<InteriorGIOcclusionCellDTO> occlusion = ResolveArray(ref _occlusion);
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
            NativeArray<LightProbeDTO> front = ResolveArray(ref _probeFront);
            NativeArray<LightProbeDTO> back = ResolveArray(ref _probeBack);
            NativeArray<InteriorGISourceDTO> sources = ResolveArray(ref _sources);
            NativeArray<InteriorGIOcclusionCellDTO> occlusion = ResolveArray(ref _occlusion);
            NativeArray<InteriorGITextureVoxelDTO> upload = ResolveArray(ref _textureUpload);
            NativeArray<MockPowerState> power = ResolveArray(ref _mockPower);
            NativeArray<int> faults = ResolveArray(ref _faults);
            NativeArray<InteriorGITelemetryEntry> scratch = ResolveArray(ref _telemetryScratch);

            InteriorGIMockPowerJob powerJob = new InteriorGIMockPowerJob
            {
                FrameIndex = tuning.FrameIndex,
                EmergencyOverride01 = tuning.EmergencyOverride01,
                Power = power
            };
            JobHandle handle = powerJob.Schedule();

            int iterations = math.clamp(tuning.PropagationIterations, 1, 4);
            float iterationDt = tuning.SimulationDelta / math.max(1, iterations);
            NativeArray<LightProbeDTO> readProbes = front;
            NativeArray<LightProbeDTO> writeProbes = back;
            NativeArray<LightProbeDTO> finalProbes = back;
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
                    TextureUpload = upload,
                    Faults = faults,
                    Power = power,
                    Tuning = iterationTuning
                };
                handle = propagationJob.Schedule(tuning.ActiveProbeCount, 64, handle);
                finalProbes = writeProbes;
                finalBufferIsBack = (i & 1) == 0;
                NativeArray<LightProbeDTO> swap = readProbes;
                readProbes = writeProbes;
                writeProbes = swap;
            }

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

        private void SwapFrontBack()
        {
            VaultBufferHandle<LightProbeDTO> swap = _probeFront;
            _probeFront = _probeBack;
            _probeBack = swap;
            _visualDirty = true;
        }

        private void CommitTelemetryScratch()
        {
            if (!_telemetryScratch.IsCreated || !_telemetryRing.IsCreated)
                return;

            NativeArray<InteriorGITelemetryEntry> scratch = ResolveArray(ref _telemetryScratch);
            NativeArray<InteriorGITelemetryEntry> ring = ResolveArray(ref _telemetryRing);
            if (!scratch.IsCreated || !ring.IsCreated || ring.Length < TelemetryCapacity)
                return;

            InteriorGITelemetryEntry entry = scratch[0];
            entry.SolverCompleteMs = _lastCompleteMs;
            ring[_telemetryCursor % TelemetryCapacity] = entry;
            _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;

            if ((entry.Flags & TelemetryFlagNan) != 0u && !_nanDumpWritten)
            {
                _nanDumpWritten = true;
                DumpTelemetryRing();
            }
        }

        private void UploadVisualTextureIfDirty()
        {
            if (!enableTextureUpload || !_visualDirty || !_textureUpload.IsCreated)
                return;

            InteriorGITuningDTO tuning = _tuning.GetElementAsRef(EnsureVault(), 0);
            if (_visualUploadAccumulator < math.max(0.05f, tuning.UploadCadenceSeconds))
                return;

            _visualUploadAccumulator = 0f;
            EnsureTextures(_activeResolution);
            if (_stagingTexture == null || _publishedTexture == null)
                return;

            NativeArray<InteriorGITextureVoxelDTO> upload = ResolveArray(ref _textureUpload);
            NativeArray<InteriorGITextureVoxelDTO> active = upload.GetSubArray(0, ActiveCellCount);
            _stagingTexture.SetPixelData(active, 0);
            _stagingTexture.Apply(false, false);
            Graphics.CopyTexture(_stagingTexture, _publishedTexture);

            Shader.SetGlobalTexture(InteriorGITextureId, _publishedTexture);
            Shader.SetGlobalVector(InteriorGIParamsId, new Vector4(_activeResolution, math.max(1f, cellSizeMeters), tuning.GlobalQualityWeight, tuning.DirectionalWeight));
            float3 rootResidue = ToShaderRootResidue(_rootAup);
            Shader.SetGlobalVector(InteriorGIOriginId, new Vector4(rootResidue.x, rootResidue.y, rootResidue.z, 1f));
            Shader.SetGlobalVector(InteriorGIRootAupId, new Vector4(rootResidue.x, rootResidue.y, rootResidue.z, (float)_rootHash));
            _visualDirty = false;
        }

        private void EnsureTextures(int resolution)
        {
            if (_textureResolution == resolution && _stagingTexture != null && _publishedTexture != null)
                return;

            ReleaseTextures();
            _textureResolution = resolution;
            _stagingTexture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBAHalf, false)
            {
                name = "H8_InteriorGI_Staging",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            _publishedTexture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBAHalf, false)
            {
                name = "H8_InteriorGI_Published",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }

        private InteriorGITuningDTO BuildTuning(float quality, float dt, float cadence)
        {
            float safeQuality = math.saturate(math.isfinite(quality) ? quality : 0f);
            int resolution = ResolveResolutionFromQuality(safeQuality);
            float l1Gate = math.step(0.08f, safeQuality);
            float l2Gate = math.step(0.54f, safeQuality);
            float directional = Smooth01((safeQuality - 0.08f) * 1.35f) * l1Gate;
            float l2 = Smooth01((safeQuality - 0.54f) * 2.05f) * l2Gate;
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
                RootHash = _rootHash,
                RedOverride01 = math.saturate(emergencyOverride01),
                UploadCadenceSeconds = uploadCadence,
                AmbientRetain = math.lerp(0.78f, 0.93f, safeQuality),
                TransferDamping = math.lerp(0.55f, 1.15f, safeQuality),
                PropagationIterations = iterations,
                _pad1 = 0u
            };
        }

        private float ResolveQualityWeight()
        {
            if (forceQualityWeight >= 0f)
                return math.saturate(forceQualityWeight);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(weight))
                weight = _cachedQualityTier == HectonQualityTier.Mx350 || _cachedQualityTier == HectonQualityTier.Low ? 0.1f : 1f;

            return math.saturate(weight);
        }

        private void ResolveActiveResolution(float quality)
        {
            int desired = forceEditorResolution
                ? math.clamp(editorPreviewResolution, MinResolution, MaxResolution)
                : ResolveResolutionFromQuality(quality);

            if (desired == _activeResolution)
                return;

            _activeResolution = desired;
            MemClearBuffer(ref _probeFront, MaxCellCount);
            MemClearBuffer(ref _probeBack, MaxCellCount);
            MemClearBuffer(ref _textureUpload, MaxCellCount);
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
            float thermalGate = 1f - math.step(0.3f, q);
            float thermalCadence = math.lerp(0.20f, 0.25f, smooth);
            float normalCadence = math.lerp(0.25f, 0.12f, smooth);
            return math.lerp(normalCadence, thermalCadence, thermalGate);
        }

#if UNITY_EDITOR
        private void TryReloadCsvOverrides()
        {
            if (!_csvReloadRequested || _simulationJobActive || !_csvBytes.IsCreated || !_sources.IsCreated)
                return;

            _csvReloadRequested = false;
            string path = Path.Combine(Application.dataPath, "..", csvOverrideRelativePath);
            if (!File.Exists(path))
                return;

            try
            {
                NativeArray<byte> csv = ResolveArray(ref _csvBytes);
                int count = ReadFileIntoVaultBuffer(path, csv, CsvBufferBytes);
                NativeArray<InteriorGISourceDTO> sources = ResolveArray(ref _sources);
                int parsedCount = InteriorGICsvParser.Parse(csv, count, sources, MaxSourceCount, _rootAup, out int rowsRejected);
                if (parsedCount > 0)
                {
                    _sourceCount = parsedCount;
                    _mockSourcesSeeded = true;
                }

                if (rowsRejected > 0)
                    Debug.LogWarning("Interior GI CSV rejected rows: " + rowsRejected);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Interior GI CSV reload failed: " + ex.Message);
            }
        }

        private static int ReadFileIntoVaultBuffer(string path, NativeArray<byte> destination, int maxBytes)
        {
            if (!destination.IsCreated || maxBytes <= 0)
                return 0;

            int count = 0;
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            while (count < maxBytes)
            {
                int value = stream.ReadByte();
                if (value < 0)
                    break;

                destination[count] = (byte)value;
                count++;
            }

            return count;
        }
#endif

        private void DumpTelemetryRing()
        {
            if (!_telemetryRing.IsCreated)
                return;

            NativeArray<InteriorGITelemetryEntry> ring = ResolveArray(ref _telemetryRing);
            WriteTelemetryDump(Path.Combine(Application.dataPath, "..", "Docs/AgentLogs/Dump_LUMEN_SURGEON.bin"), ring);
            WriteTelemetryDump(Path.Combine(Application.dataPath, "..", "Docs/AgentLogs/Dump_SHINOBU_63.bin"), ring);
        }

        private void WriteTelemetryDump(string path, NativeArray<InteriorGITelemetryEntry> ring)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(0x63474953u);
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryCursor);
                writer.Write(_activeResolution);
                writer.Write(_rootAup.x);
                writer.Write(_rootAup.y);
                writer.Write(_rootAup.z);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    InteriorGITelemetryEntry e = ring[i];
                    writer.Write(e.FrameIndex);
                    writer.Write(e.ActiveProbeCount);
                    writer.Write(e.SourceCount);
                    writer.Write(e.SourceSampleLimit);
                    writer.Write(e.GlobalQualityWeight);
                    writer.Write(e.SolverCompleteMs);
                    writer.Write(e.MaxL0);
                    writer.Write(e.AverageL0);
                    writer.Write(e.NaNCount);
                    writer.Write(e.Flags);
                    writer.Write(e.GridHash);
                    writer.Write(e.RootHash);
                    writer.Write(e.WaterAbsorption);
                    writer.Write(e.DirectionalWeight);
                    writer.Write(e.BouncesEstimated);
                    writer.Write(e._pad0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Interior GI black box dump failed: " + ex.Message);
            }
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
            if (!drawProbeGizmos || !TryGetProbeGridReadback(out NativeArray<LightProbeDTO> probes, out int resolution, out double3 root, out float cell, out _))
                return;

            int count = resolution * resolution * resolution;
            int stride = math.max(1, count / math.max(1, maxEditorGizmoProbes));
            for (int i = 0; i < count; i += stride)
            {
                LightProbeDTO probe = probes[i];
                float luma = math.saturate(InteriorGIProbeMath.LuminanceL0(in probe) * 0.25f);
                if (luma <= 0.01f)
                    continue;

                int3 coord = InteriorGIProbeMath.IndexToCoord(i, resolution);
                Vector3 pos = new Vector3(
                    (float)(root.x + (coord.x + 0.5f) * cell - _rootAup.x),
                    (float)(root.y + (coord.y + 0.5f) * cell - _rootAup.y),
                    (float)(root.z + (coord.z + 0.5f) * cell - _rootAup.z)) + (_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
                Gizmos.color = new Color(math.saturate(probe.R0), math.saturate(probe.G0), math.saturate(probe.B0), math.saturate(luma));
                Gizmos.DrawCube(pos, Vector3.one * math.max(0.05f, cell * 0.08f));
            }
        }
#endif
    }

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
        public static void AddScaled(ref LightProbeDTO dst, in LightProbeDTO src, float scale, float l1Weight, float l2Weight)
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
        public static void AddDirectional(ref LightProbeDTO dst, float3 color, float gain, float3 direction, float l1Weight, float l2Weight)
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
        public static void SanitizeAndClamp(ref LightProbeDTO probe, float maxL0)
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
        public static InteriorGITextureVoxelDTO PackTexture(in LightProbeDTO probe)
        {
            float l1Sq = probe.R1 * probe.R1 + probe.G1 * probe.G1 + probe.B1 * probe.B1 + probe.R2 * probe.R2 + probe.G2 * probe.G2 + probe.B2 * probe.B2 + probe.R3 * probe.R3 + probe.G3 * probe.G3 + probe.B3 * probe.B3;
            float l1 = l1Sq > 0.000001f ? math.sqrt(math.max(0f, l1Sq)) : 0f;
            return new InteriorGITextureVoxelDTO
            {
                R = (half)math.clamp(probe.R0, 0f, 32f),
                G = (half)math.clamp(probe.G0, 0f, 32f),
                B = (half)math.clamp(probe.B0, 0f, 32f),
                A = (half)math.clamp(l1, 0f, 32f)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LuminanceL0(in LightProbeDTO probe)
        {
            return probe.R0 * 0.2126f + probe.G0 * 0.7152f + probe.B0 * 0.0722f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashProbe(in LightProbeDTO probe, uint hash)
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
        [ReadOnly, NoAlias] public NativeArray<LightProbeDTO> Front;
        [WriteOnly, NoAlias] public NativeArray<LightProbeDTO> Back;
        [ReadOnly, NoAlias] public NativeArray<InteriorGISourceDTO> Sources;
        [ReadOnly, NoAlias] public NativeArray<InteriorGIOcclusionCellDTO> Occlusion;
        [WriteOnly, NoAlias] public NativeArray<InteriorGITextureVoxelDTO> TextureUpload;
        [WriteOnly, NoAlias] public NativeArray<int> Faults;
        [ReadOnly, NoAlias] public NativeArray<MockPowerState> Power;
        public InteriorGITuningDTO Tuning;

        public void Execute(int index)
        {
            int resolution = Tuning.Resolution;
            int3 coord = InteriorGIProbeMath.IndexToCoord(index, resolution);
            InteriorGIOcclusionCellDTO currentCell = Occlusion[index];
            LightProbeDTO current = Front[index];
            LightProbeDTO result = default;
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

            Back[index] = result;
            TextureUpload[index] = InteriorGIProbeMath.PackTexture(in result);
            Faults[index] = fault;
        }

        private void AccumulateNeighbor(ref LightProbeDTO result, int currentIndex, int3 coord, int3 delta, uint wallBit, uint oppositeWallBit, float transferBase)
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

            LightProbeDTO neighbor = Front[neighborIndex];
            InteriorGIProbeMath.AddScaled(ref result, in neighbor, transfer, Tuning.DirectionalWeight, Tuning.L2Weight);
        }

        private void InjectSources(ref LightProbeDTO result, int3 coord, InteriorGIOcclusionCellDTO currentCell)
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
                    ? Tuning.FloraGlowScale * (0.7f + 0.3f * math.sin((Tuning.FrameIndex + source.Phase01 * 32f) * 0.17f))
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

        private void InjectOcclusionGlow(ref LightProbeDTO result, InteriorGIOcclusionCellDTO currentCell)
        {
            if (currentCell.FloraGlow01 > 0.0001f)
            {
                float pulse = 0.75f + 0.25f * math.sin(Tuning.FrameIndex * 0.13f + currentCell.RoomHash * 0.0001f);
                InteriorGIProbeMath.AddDirectional(ref result, new float3(0.08f, 0.75f, 0.42f), currentCell.FloraGlow01 * Tuning.FloraGlowScale * pulse * Tuning.SimulationDelta, new float3(0f, 1f, 0f), Tuning.DirectionalWeight, Tuning.L2Weight);
            }

            float emergencyReflect = math.saturate(currentCell.EmergencyReflectance01 * Tuning.EmergencyOverride01);
            if (emergencyReflect > 0.0001f)
                InteriorGIProbeMath.AddDirectional(ref result, new float3(1.8f, 0.03f, 0.01f), emergencyReflect * Tuning.EmergencyLightIntensity * Tuning.SimulationDelta, new float3(0f, 0f, 1f), Tuning.DirectionalWeight, Tuning.L2Weight);
        }

        private static bool IsProbeFinite(in LightProbeDTO p)
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
        [ReadOnly, NoAlias] public NativeArray<LightProbeDTO> Probes;
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
                LightProbeDTO probe = Probes[i];
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
