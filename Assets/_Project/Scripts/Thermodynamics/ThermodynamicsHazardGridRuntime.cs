using System;
using System.IO;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    /// <summary>
    /// Burst macro-grid for heat and radiation diffusion. Hazard truth is scalar field data, not trigger colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Thermodynamics/Thermodynamics Hazard Grid Runtime")]
    public sealed unsafe partial class ThermodynamicsHazardGridRuntime : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        public const int HighResolution = 32;
        public const int LowResolution = 16;
        public const int MaxCellCount = HighResolution * HighResolution * HighResolution;
        public const int MaxSourceCount = 128;
        public const int MaxEntityCount = 128;
        public const int MaxSignalsPerFrame = 64;
        public const int TelemetryCapacity = 300;
        public const uint HeatHazardHash = 0x48454154u;
        public const uint RadiationHazardHash = 0x52414453u;
        public const uint MixedHazardHash = 0x484D4958u;

        private const SystemID MemoryOwner = SystemID.Thermodynamics;
        private const BufferID VaultConstantsBuffer = BufferID.ThermodynamicsHazardConstants;
        private const BufferID VaultTemperatureFrontMirror = BufferID.ThermodynamicsTemperatureFrontMirror;
        private const BufferID VaultRadiationFrontMirror = BufferID.ThermodynamicsRadiationFrontMirror;
        private const BufferID VaultTemperatureFrontBuffer = BufferID.ThermodynamicsTemperatureFront;
        private const BufferID VaultTemperatureBackBuffer = BufferID.ThermodynamicsTemperatureBack;
        private const BufferID VaultRadiationFrontBuffer = BufferID.ThermodynamicsRadiationFront;
        private const BufferID VaultRadiationBackBuffer = BufferID.ThermodynamicsRadiationBack;
        private const BufferID VaultTemperatureSourcesBuffer = BufferID.ThermodynamicsTemperatureSources;
        private const BufferID VaultRadiationSourcesBuffer = BufferID.ThermodynamicsRadiationSources;
        private const BufferID VaultSourcesBuffer = BufferID.ThermodynamicsSources;
        private const BufferID VaultSourceIdsBuffer = BufferID.ThermodynamicsSourceIds;
        private const BufferID VaultEntityAupsBuffer = BufferID.ThermodynamicsEntityAups;
        private const BufferID VaultEntityIdsBuffer = BufferID.ThermodynamicsEntityIds;
        private const BufferID VaultUpdraftSignalsBuffer = BufferID.ThermodynamicsUpdraftSignals;
        private const BufferID VaultSignalCountersBuffer = BufferID.ThermodynamicsSignalCounters;
        private const BufferID VaultTelemetryRingBuffer = BufferID.ThermodynamicsTelemetryRing;
        private const BufferID VaultTelemetryScratchBuffer = BufferID.ThermodynamicsTelemetryScratch;
        private const BufferID VaultCsvBytesBuffer = BufferID.ThermodynamicsCsvBytes;
        private const BufferID VaultBinaryConstantBytesBuffer = BufferID.ThermodynamicsBinaryConstantBytes;
        private const float DefaultCellSizeMeters = 10f;
        private const float TierSwitchHysteresisSeconds = 3f;
        private const float CsvPollSeconds = 1f;
        private const float UpdraftThresholdCelsius = 120f;
        private const float DefaultRadiationDecayCoefficient = 0.9975f;
        private const int ConfigFileStreamBufferBytes = 4096;
        private const int LowTierVisualUploadStride = 4;
        private const int HealthPressureLowTierFrames = 120;
        private const int CsvBufferBytes = 4096;
        private const int BinaryConstantsBytes = 16;
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint TelemetryFlagLowTier = 1u << 1;
        private const uint TelemetryFlagRebase = 1u << 2;
        private const uint TelemetryFlagHealthPressureLowTier = 1u << 3;
        private const uint TelemetryFlagSignalDrop = 1u << 4;
        private static readonly int HeatTexturePropertyId = Shader.PropertyToID("_HectonThermoHazardHeatTex3D");
        private static readonly int GridMetaPropertyId = Shader.PropertyToID("_HectonThermoHazardGridMeta");

        internal static ThermodynamicsHazardGridRuntime ActiveRuntimeInstance { get; private set; }

        [Header("Runtime")]
        [SerializeField, Min(1f)]
        [Tooltip("Meters represented by one macro-grid cell.")]
        private float cellSizeMeters = DefaultCellSizeMeters;

        [SerializeField]
        [Tooltip("Seeds a heat source and radiation leak when no real hazard producers have registered.")]
        private bool enableMockHazards = true;

        [SerializeField]
        [Tooltip("Editor-only source-data CSV override. Player builds use baked binary/default constants.")]
        private bool monitorCsvOverrides = true;

        [SerializeField]
        [Tooltip("Uploads the front temperature buffer into a global RFloat Texture3D for heat-haze shaders.")]
        private bool enableVisualTextureUpload = true;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Continuous quality ceiling for designer/debug thermal load shedding; 1 keeps hardware quality unchanged.")]
        private float qualityCeiling = 1f;

        private VaultGenerationHandle<float> _temperatureFront;
        private VaultGenerationHandle<float> _temperatureBack;
        private VaultGenerationHandle<float> _radiationFront;
        private VaultGenerationHandle<float> _radiationBack;
        private VaultGenerationHandle<float> _temperatureSources;
        private VaultGenerationHandle<float> _radiationSources;
        private VaultGenerationHandle<HazardSourceDTO> _sources;
        private VaultGenerationHandle<uint> _sourceIds;
        private VaultGenerationHandle<double3> _entityAups;
        private VaultGenerationHandle<uint> _entityIds;
        private VaultGenerationHandle<ThermalUpdraftSignal> _updraftSignals;
        private VaultGenerationHandle<int> _signalCounters;
        private VaultGenerationHandle<ThermodynamicsHazardTelemetryEntry> _telemetryRing;
        private VaultGenerationHandle<ThermodynamicsHazardTelemetryEntry> _telemetryScratch;
        private VaultGenerationHandle<ThermodynamicsHazardConstants> _constants;
        private VaultGenerationHandle<float> _vaultTemperatureFrontMirror;
        private VaultGenerationHandle<float> _vaultRadiationFrontMirror;
        private VaultGenerationHandle<byte> _csvBytes;
        private VaultGenerationHandle<byte> _binaryConstantBytes;

        private JobHandle _simulationHandle;
        private IDataVault _vault;
        private double3 _gridOriginAup;
        private int3 _pendingRebaseCells;
        private int _sourceCount;
        private int _entityCount;
        private int _activeResolution = HighResolution;
        private int _desiredResolution = HighResolution;
        private int _telemetryWriteIndex;
        private int _gridVersion;
        private int _lastTextureVersion = -1;
        private int _vaultMirrorVersion = -1;
        private int _healthPressureLowTierFrames;
        private int _droppedSignalCount;
        private uint _simulationFrame;
        private float _tierSwitchTimer;
        private float _decayAccumulator;
        private float _csvPollTimer;
        private float _lastCompleteMs;
        private DateTime _csvLastWriteUtc;
        private Texture3D _temperatureTexture;
        private bool _simulationJobActive;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _pendingDataVaultRebind;
        private bool _mockSeeded;
        private bool _visualDirty;
        private bool _vaultMirrorRequested;
        private IDataVault _pendingDataVault;
        private uint _shiftSequence;

        /// <summary>True after native buffers are allocated and the runtime is registered.</summary>
        public bool IsInitialized => HasHandle(in _temperatureFront) && HasHandle(in _constants);

        public int DroppedSignalCount => _droppedSignalCount;

        private void Awake()
        {
            CacheRegistryServicesCold();
            EnsureNativeState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            _droppedSignalCount = 0;
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureNativeState();
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregister();
            TryUnregisterHotSwapListener();
            StopConfigWorker();
            ReleaseNativeState();
            ReleaseVisualTexture();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            ApplyPendingDataVaultRebindIfIdle();
            EnsureNativeState();
            if (!IsInitialized)
                return;

            ApplyPendingConfigLoads();

            float dt = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            ConsumeSystemHealthSignals();
            ResolveResolutionWithHysteresis(dt);
            if (_simulationJobActive)
                return;

            ApplyStableResolutionIfNeeded();
            EnsureMockSources();
            ScheduleSimulation(dt);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!monitorCsvOverrides)
                return;

            _csvPollTimer -= 0.1f;
            if (_csvPollTimer > 0f)
                return;

            _csvPollTimer = CsvPollSeconds;
            TryReloadCsvOverrides();
#endif
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_simulationJobActive)
                return;

            long start = Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;
            long end = Stopwatch.GetTimestamp();
            _lastCompleteMs = (float)((end - start) * 1000.0 / Stopwatch.Frequency);
            _simulationJobActive = false;
            H8Memory.RegisterActiveJob(MemoryOwner, default);
            if (_pendingDataVaultRebind)
            {
                ApplyPendingDataVaultRebindIfIdle();
                return;
            }

            SwapFrontBack();
            _gridVersion++;
            _visualDirty = true;
            PublishQueuedSignals();
            CommitTelemetryScratch();
            if (_vaultMirrorRequested)
                MirrorFrontGridToVault();
            UploadVisualTextureIfDirty();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float safeCellSize = math.max(1f, cellSizeMeters);
            float3 shift = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            if (!math.all(math.isfinite(shift)))
                return;

            _pendingRebaseCells += (int3)math.round(shift / safeCellSize);
            _shiftSequence = shiftData.Sequence;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                QueueDataVaultRebind(currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredTick = false;
                _registeredSlowTick = false;
                _registeredLateFrame = false;
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }
        }

        /// <summary>
        /// Registers or updates a mathematical hazard source.
        /// </summary>
        public bool TryUpsertSource(uint sourceId, double3 aup, float intensity, float radiusMeters, uint hazardTypeHash)
        {
            if (sourceId == 0u || _simulationJobActive || !HasHandle(in _sources) || !math.all(math.isfinite(aup)))
                return false;

            if (!TryOpenArray(in _sourceIds, MaxSourceCount, out NativeArray<uint> sourceIds) ||
                !TryOpenArray(in _sources, MaxSourceCount, out NativeArray<HazardSourceDTO> sources))
            {
                return false;
            }

            float safeIntensity = math.max(0f, math.isfinite(intensity) ? intensity : 0f);
            float safeRadius = math.max(0.5f, math.isfinite(radiusMeters) ? radiusMeters : 0f);
            for (int i = 0; i < _sourceCount; i++)
            {
                if (sourceIds[i] != sourceId)
                    continue;

                sources[i] = new HazardSourceDTO
                {
                    AUP = aup,
                    Intensity = safeIntensity,
                    Radius = safeRadius,
                    HazardTypeHash = hazardTypeHash,
                    _pad0 = 0u
                };
                return true;
            }

            if (_sourceCount >= MaxSourceCount)
                return false;

            sourceIds[_sourceCount] = sourceId;
            sources[_sourceCount] = new HazardSourceDTO
            {
                AUP = aup,
                Intensity = safeIntensity,
                Radius = safeRadius,
                HazardTypeHash = hazardTypeHash,
                _pad0 = 0u
            };
            _sourceCount++;
            return true;
        }

        /// <summary>
        /// Removes a mathematical hazard source.
        /// </summary>
        public bool TryRemoveSource(uint sourceId)
        {
            if (sourceId == 0u || _simulationJobActive || !HasHandle(in _sources))
                return false;

            if (!TryOpenArray(in _sourceIds, MaxSourceCount, out NativeArray<uint> sourceIds) ||
                !TryOpenArray(in _sources, MaxSourceCount, out NativeArray<HazardSourceDTO> sources))
            {
                return false;
            }

            for (int i = 0; i < _sourceCount; i++)
            {
                if (sourceIds[i] != sourceId)
                    continue;

                int last = _sourceCount - 1;
                sourceIds[i] = sourceIds[last];
                sources[i] = sources[last];
                sourceIds[last] = 0u;
                sources[last] = default;
                _sourceCount = last;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers or updates an entity sample slot for external owners. This runtime never emits damage.
        /// </summary>
        public bool TryUpsertEntity(uint entityId, double3 aup)
        {
            if (entityId == 0u || _simulationJobActive || !HasHandle(in _entityIds) || !math.all(math.isfinite(aup)))
                return false;

            if (!TryOpenArray(in _entityIds, MaxEntityCount, out NativeArray<uint> entityIds) ||
                !TryOpenArray(in _entityAups, MaxEntityCount, out NativeArray<double3> entityAups))
            {
                return false;
            }

            for (int i = 0; i < _entityCount; i++)
            {
                if (entityIds[i] != entityId)
                    continue;

                entityAups[i] = aup;
                return true;
            }

            if (_entityCount >= MaxEntityCount)
                return false;

            entityIds[_entityCount] = entityId;
            entityAups[_entityCount] = aup;
            _entityCount++;
            return true;
        }

        /// <summary>
        /// Samples heat and radiation with trilinear interpolation from the current front buffers.
        /// </summary>
        public bool TrySample(double3 aup, out ThermodynamicsHazardSample sample)
        {
            sample = default;
            if (!HasHandle(in _temperatureFront) || !HasHandle(in _radiationFront) || !math.all(math.isfinite(aup)))
                return false;

            if (!TryOpenReadArray(in _temperatureFront, ActiveCellCount, out NativeArray<float> temperatureFront) ||
                !TryOpenReadArray(in _radiationFront, ActiveCellCount, out NativeArray<float> radiationFront))
            {
                return false;
            }

            ThermodynamicsHazardConstants constants = GetConstantsValue();
            float* temp = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureFront);
            float* rad = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationFront);
            sample = SampleTrilinear(
                temp,
                rad,
                _activeResolution,
                cellSizeMeters,
                _gridOriginAup,
                aup,
                constants);
            return true;
        }

        /// <summary>
        /// Exposes raw front-buffer pointers for Burst read consumers. Back buffers stay owner-only.
        /// </summary>
        public bool TryGetUnsafeGridPointers(out ThermodynamicsHazardGridPointers pointers)
        {
            pointers = default;
            if (!HasHandle(in _temperatureFront) || !HasHandle(in _radiationFront))
                return false;

            if (!TryOpenReadArray(in _temperatureFront, ActiveCellCount, out NativeArray<float> temperatureFront) ||
                !TryOpenReadArray(in _radiationFront, ActiveCellCount, out NativeArray<float> radiationFront))
            {
                return false;
            }

            pointers = new ThermodynamicsHazardGridPointers
            {
                TemperatureFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureFront),
                RadiationFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationFront),
                CellCount = ActiveCellCount,
                Resolution = _activeResolution
            };
            return true;
        }

        /// <summary>Reads a copy of the live unmanaged thermodynamics constants.</summary>
        public bool TryReadConstants(out ThermodynamicsHazardConstants constants)
        {
            constants = default;
            if (!TryOpenReadArray(in _constants, 1, out NativeArray<ThermodynamicsHazardConstants> constantsArray))
                return false;

            constants = SanitizeConstants(constantsArray[0]);
            return true;
        }

        /// <summary>Commits editor-authored thermodynamics constants through an explicit diagnostics writer fence.</summary>
        public bool TryWriteConstants(in ThermodynamicsHazardConstants constants)
        {
            EnsureNativeState();
            ThermodynamicsHazardConstants sanitized = SanitizeConstants(constants);
            return TryWriteConstantsWithOwner(in sanitized, SystemID.CoreDiagnostics);
        }

        /// <summary>
        /// Copies front-grid metadata for editor gizmos without exposing managed collections.
        /// </summary>
        public bool TryGetGridReadback(
            out NativeArray<float>.ReadOnly temperature,
            out NativeArray<float>.ReadOnly radiation,
            out int resolution,
            out double3 originAup,
            out float cellSize,
            out int version)
        {
            temperature = default;
            radiation = default;
            bool hasTemperature = TryOpenReadArray(in _temperatureFront, ActiveCellCount, out NativeArray<float> mutableTemperature);
            bool hasRadiation = TryOpenReadArray(in _radiationFront, ActiveCellCount, out NativeArray<float> mutableRadiation);
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;
            if (hasTemperature)
                temperature = mutableTemperature.AsReadOnly();
            if (hasRadiation)
                radiation = mutableRadiation.AsReadOnly();
            return hasTemperature && hasRadiation;
        }

        /// <summary>
        /// Copies the live front grid into GlobalDataVault mirrors for editor visualization.
        /// </summary>
        public bool PrepareVaultGridReadback()
        {
            EnsureNativeState();
            _vaultMirrorRequested = true;
            if (!EnsureVaultGridMirrors())
                return false;

            MirrorFrontGridToVault();
            return _vaultMirrorVersion == _gridVersion;
        }

        /// <summary>
        /// Reads already prepared Vault-backed mirror views for editor visualization.
        /// </summary>
        public bool TryGetVaultGridReadback(
            out NativeArray<float>.ReadOnly temperature,
            out NativeArray<float>.ReadOnly radiation,
            out int resolution,
            out double3 originAup,
            out float cellSize,
            out int version)
        {
            temperature = default;
            radiation = default;
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;

            if (!TryOpenReadArray(in _vaultTemperatureFrontMirror, ActiveCellCount, out NativeArray<float> mutableTemperature) ||
                !TryOpenReadArray(in _vaultRadiationFrontMirror, ActiveCellCount, out NativeArray<float> mutableRadiation))
            {
                temperature = default;
                radiation = default;
                return false;
            }

            temperature = mutableTemperature.AsReadOnly();
            radiation = mutableRadiation.AsReadOnly();
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _vaultMirrorVersion;
            return temperature.Length > 0 && radiation.Length > 0;
        }

        private int ActiveCellCount => _activeResolution * _activeResolution * _activeResolution;

        private void EnsureNativeState()
        {
            if (HasHandle(in _temperatureFront))
                return;

            if (_vault == null)
                return;

            _gridOriginAup = TryResolveCurrentRuntimeOrigin(out double3 originAup) ? originAup : double3.zero;
            _temperatureFront = AcquireBuffer<float>(VaultTemperatureFrontBuffer, MaxCellCount);
            _temperatureBack = AcquireBuffer<float>(VaultTemperatureBackBuffer, MaxCellCount);
            _radiationFront = AcquireBuffer<float>(VaultRadiationFrontBuffer, MaxCellCount);
            _radiationBack = AcquireBuffer<float>(VaultRadiationBackBuffer, MaxCellCount);
            _temperatureSources = AcquireBuffer<float>(VaultTemperatureSourcesBuffer, MaxCellCount);
            _radiationSources = AcquireBuffer<float>(VaultRadiationSourcesBuffer, MaxCellCount);
            _sources = AcquireBuffer<HazardSourceDTO>(VaultSourcesBuffer, MaxSourceCount);
            _sourceIds = AcquireBuffer<uint>(VaultSourceIdsBuffer, MaxSourceCount);
            _entityAups = AcquireBuffer<double3>(VaultEntityAupsBuffer, MaxEntityCount);
            _entityIds = AcquireBuffer<uint>(VaultEntityIdsBuffer, MaxEntityCount);
            _updraftSignals = AcquireBuffer<ThermalUpdraftSignal>(VaultUpdraftSignalsBuffer, MaxSignalsPerFrame);
            _signalCounters = AcquireBuffer<int>(VaultSignalCountersBuffer, 4);
#if UNITY_EDITOR
            _csvBytes = AcquireBuffer<byte>(VaultCsvBytesBuffer, CsvBufferBytes);
#endif
            _binaryConstantBytes = AcquireBuffer<byte>(VaultBinaryConstantBytesBuffer, BinaryConstantsBytes);
            _telemetryRing = AcquireBuffer<ThermodynamicsHazardTelemetryEntry>(VaultTelemetryRingBuffer, TelemetryCapacity);
            _telemetryScratch = AcquireBuffer<ThermodynamicsHazardTelemetryEntry>(VaultTelemetryScratchBuffer, 1);
            _constants = AcquireBuffer<ThermodynamicsHazardConstants>(VaultConstantsBuffer, 1);

            ThermodynamicsHazardConstants loadedConstants = LoadConstantsOrEmergency();
            if (TryOpenArray(in _constants, 1, out NativeArray<ThermodynamicsHazardConstants> constantsArray))
            {
                ThermodynamicsHazardConstants existing = constantsArray[0];
                constantsArray[0] = HasUsableConstants(existing) ? SanitizeConstants(existing) : loadedConstants;
            }

            StartConfigWorkerIfNeeded();
            RequestBinaryConstantsLoad();
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(BufferID bufferId, int length) where T : struct
        {
            IDataVault vault = EnsureVault();
            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                MemoryOwner,
                NativeArrayOptions.ClearMemory);
            if (!TryOpenArray(in handle, length, out _))
                throw new InvalidOperationException("Thermodynamics vault buffer acquisition failed.");

            return handle;
        }

        private static bool TryResolveCurrentRuntimeOrigin(out double3 originAup)
        {
            originAup = default;
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!runtimeOriginAup.IsFinite())
                return false;

            originAup = runtimeOriginAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAup));
        }

        private IDataVault EnsureVault()
        {
            if (_vault == null)
                throw new InvalidOperationException("Thermodynamics GlobalDataVault unavailable.");

            return _vault;
        }

        private void CacheRegistryServicesCold()
        {
            if (_vault == null)
                _vault = GlobalRegistry.DataVault;
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryOpenArray<T>(in VaultGenerationHandle<T> handle, int requiredLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!HasHandle(in handle) || requiredLength < 0)
                return false;

            IDataVault vault = EnsureVault();
            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryOpenReadArray<T>(in VaultGenerationHandle<T> handle, int requiredLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!HasHandle(in handle) || requiredLength < 0)
                return false;

            IDataVault vault = EnsureVault();
            return vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private ThermodynamicsHazardConstants GetConstantsValue()
        {
            return TryReadConstants(out ThermodynamicsHazardConstants constants)
                ? constants
                : GenerateEmergencyMockConstants();
        }

        private bool TryWriteConstantsWithOwner(in ThermodynamicsHazardConstants constants, SystemID writerSystem)
        {
            IDataVault vault = EnsureVault();
            if (!HasHandle(in _constants) ||
                !vault.TryAcquireWriteLock(in _constants, writerSystem, out NativeArray<ThermodynamicsHazardConstants> constantsArray))
            {
                return false;
            }

            try
            {
                if (!constantsArray.IsCreated || constantsArray.Length < 1)
                    return false;

                constantsArray[0] = SanitizeConstants(constants);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _constants, writerSystem);
            }
        }

        private bool EnsureVaultGridMirrors()
        {
            if (!TryOpenArray(in _vaultTemperatureFrontMirror, MaxCellCount, out _))
                _vaultTemperatureFrontMirror = AcquireBuffer<float>(VaultTemperatureFrontMirror, MaxCellCount);

            if (!TryOpenArray(in _vaultRadiationFrontMirror, MaxCellCount, out _))
                _vaultRadiationFrontMirror = AcquireBuffer<float>(VaultRadiationFrontMirror, MaxCellCount);

            return HasHandle(in _vaultTemperatureFrontMirror) && HasHandle(in _vaultRadiationFrontMirror);
        }

        private void MirrorFrontGridToVault()
        {
            if (!HasHandle(in _temperatureFront) || !HasHandle(in _radiationFront) || !EnsureVaultGridMirrors())
            {
                return;
            }

            int count = ActiveCellCount;
            if (!TryOpenReadArray(in _temperatureFront, count, out NativeArray<float> temperatureFront) ||
                !TryOpenReadArray(in _radiationFront, count, out NativeArray<float> radiationFront) ||
                !TryOpenArray(in _vaultTemperatureFrontMirror, count, out NativeArray<float> temperatureMirror) ||
                !TryOpenArray(in _vaultRadiationFrontMirror, count, out NativeArray<float> radiationMirror))
            {
                return;
            }

            NativeArray<float>.Copy(temperatureFront, 0, temperatureMirror, 0, count);
            NativeArray<float>.Copy(radiationFront, 0, radiationMirror, 0, count);
            _vaultMirrorVersion = _gridVersion;
        }

        private void ReleaseNativeState()
        {
            if (_simulationJobActive)
            {
                // [BLOCKING_SYNC_POINT] Teardown cannot release Vault handles while the simulation writer is active.
                DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
                H8Memory.RegisterActiveJob(MemoryOwner, default);
            }

            _temperatureFront = default;
            _temperatureBack = default;
            _radiationFront = default;
            _radiationBack = default;
            _temperatureSources = default;
            _radiationSources = default;
            _sources = default;
            _sourceIds = default;
            _entityAups = default;
            _entityIds = default;
            _updraftSignals = default;
            _signalCounters = default;
            _telemetryRing = default;
            _telemetryScratch = default;
            _constants = default;
            _vaultTemperatureFrontMirror = default;
            _vaultRadiationFrontMirror = default;
            _vaultMirrorVersion = -1;
            _simulationFrame = 0u;
            _csvBytes = default;
            _binaryConstantBytes = default;
            _simulationHandle = default;
            _simulationJobActive = false;
            _pendingDataVaultRebind = false;
            _pendingDataVault = null;
        }

        private void QueueDataVaultRebind(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault) && !_pendingDataVaultRebind)
                return;

            if (_simulationJobActive)
            {
                _pendingDataVault = vault;
                _pendingDataVaultRebind = true;
                return;
            }

            ApplyDataVaultRebind(vault);
        }

        private void ApplyPendingDataVaultRebindIfIdle()
        {
            if (!_pendingDataVaultRebind || _simulationJobActive)
                return;

            IDataVault vault = _pendingDataVault;
            _pendingDataVaultRebind = false;
            _pendingDataVault = null;
            ApplyDataVaultRebind(vault);
        }

        private void ApplyDataVaultRebind(IDataVault vault)
        {
            ReleaseNativeState();
            _vault = vault;
            if (_vault == null || !isActiveAndEnabled)
                return;

            EnsureNativeState();
            _visualDirty = true;
            _vaultMirrorRequested = true;
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

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
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
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.UnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ResolveResolutionWithHysteresis(float dt)
        {
            float quality = ResolveContinuousQualityWeight();
            float curvedQuality = quality * quality * (3f - (2f * quality));
            int desired = math.clamp((int)math.round(math.lerp(LowResolution, HighResolution, curvedQuality)), LowResolution, HighResolution);
            if (desired == _desiredResolution)
            {
                _tierSwitchTimer = 0f;
                return;
            }

            _tierSwitchTimer += dt;
            if (_tierSwitchTimer < TierSwitchHysteresisSeconds)
                return;

            _desiredResolution = desired;
            _tierSwitchTimer = 0f;
        }

        private float ResolveContinuousQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.isfinite(weight) ? math.saturate(weight) : 1f;
            quality = math.min(quality, math.saturate(qualityCeiling));

            float pressure01 = math.saturate(_healthPressureLowTierFrames * math.rcp(math.max(1f, HealthPressureLowTierFrames)));
            float pressureCurve = pressure01 * pressure01 * (3f - (2f * pressure01));
            return math.saturate(quality * math.lerp(1f, 0.1f, pressureCurve));
        }

        private void ConsumeSystemHealthSignals()
        {
            if (_healthPressureLowTierFrames > 0)
                _healthPressureLowTierFrames--;

            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SystemHealthIndexSignal signal = signals[i];
                if (signal.State >= SystemHealthIndexSignal.StateCritical ||
                    (signal.Flags & SystemHealthIndexSignal.FlagAdrenaline) != 0)
                {
                    _healthPressureLowTierFrames = HealthPressureLowTierFrames;
                }
            }
        }

        private void ApplyStableResolutionIfNeeded()
        {
            if (_activeResolution == _desiredResolution || _simulationJobActive)
                return;

            _activeResolution = _desiredResolution;
            ClearAllGridData();
            _gridVersion++;
            _visualDirty = true;
        }

        private void ClearAllGridData()
        {
            int length = MaxCellCount;
            if (!TryOpenArray(in _temperatureFront, length, out NativeArray<float> temperatureFront) ||
                !TryOpenArray(in _temperatureBack, length, out NativeArray<float> temperatureBack) ||
                !TryOpenArray(in _radiationFront, length, out NativeArray<float> radiationFront) ||
                !TryOpenArray(in _radiationBack, length, out NativeArray<float> radiationBack) ||
                !TryOpenArray(in _temperatureSources, length, out NativeArray<float> temperatureSources) ||
                !TryOpenArray(in _radiationSources, length, out NativeArray<float> radiationSources))
            {
                return;
            }

            for (int i = 0; i < length; i++)
            {
                temperatureFront[i] = 0f;
                temperatureBack[i] = 0f;
                radiationFront[i] = 0f;
                radiationBack[i] = 0f;
                temperatureSources[i] = 0f;
                radiationSources[i] = 0f;
            }
        }

        private void EnsureMockSources()
        {
            if (!enableMockHazards || _mockSeeded || _sourceCount > 0 || _simulationJobActive)
                return;

            if (!TryOpenArray(in _sources, MaxSourceCount, out NativeArray<HazardSourceDTO> sources) ||
                !TryOpenArray(in _sourceIds, MaxSourceCount, out NativeArray<uint> sourceIds))
            {
                return;
            }

            ref HazardSourceDTO heat = ref UnsafeUtility.ArrayElementAsRef<HazardSourceDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(sources),
                0);
            ref HazardSourceDTO radiation = ref UnsafeUtility.ArrayElementAsRef<HazardSourceDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(sources),
                1);
            _sourceCount = MockHazardGenerator.GenerateEmergencyMockSources(
                ref heat,
                ref radiation,
                _gridOriginAup,
                math.max(1f, cellSizeMeters),
                HeatHazardHash,
                RadiationHazardHash);
            sourceIds[0] = MockHazardGenerator.MockHeatSourceId;
            sourceIds[1] = MockHazardGenerator.MockRadiationSourceId;
            _mockSeeded = true;
        }

        private void ScheduleSimulation(float dt)
        {
            bool applyDecay = false;
            _decayAccumulator += dt;
            if (_decayAccumulator >= 1f)
            {
                _decayAccumulator -= math.floor(_decayAccumulator);
                applyDecay = true;
            }

            int activeCellCount = ActiveCellCount;
            if (!TryOpenArray(in _temperatureFront, activeCellCount, out NativeArray<float> temperatureFront) ||
                !TryOpenArray(in _temperatureBack, activeCellCount, out NativeArray<float> temperatureBack) ||
                !TryOpenArray(in _radiationFront, activeCellCount, out NativeArray<float> radiationFront) ||
                !TryOpenArray(in _radiationBack, activeCellCount, out NativeArray<float> radiationBack) ||
                !TryOpenArray(in _temperatureSources, activeCellCount, out NativeArray<float> temperatureSourcesArray) ||
                !TryOpenArray(in _radiationSources, activeCellCount, out NativeArray<float> radiationSourcesArray) ||
                !TryOpenArray(in _sources, MaxSourceCount, out NativeArray<HazardSourceDTO> sources) ||
                !TryOpenArray(in _updraftSignals, MaxSignalsPerFrame, out NativeArray<ThermalUpdraftSignal> updraftSignals) ||
                !TryOpenArray(in _signalCounters, 4, out NativeArray<int> signalCounters) ||
                !TryOpenArray(in _telemetryScratch, 1, out NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryScratch))
            {
                return;
            }

            ThermodynamicsHazardConstants constants = GetConstantsValue();
            uint simulationFrame = ++_simulationFrame;
            ResetCountersJob resetJob = new ResetCountersJob
            {
                Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(signalCounters),
                CounterCount = signalCounters.Length
            };
            JobHandle handle = resetJob.Schedule();

            ClearSourceGridJob clearSources = new ClearSourceGridJob
            {
                TemperatureSources = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureSourcesArray),
                RadiationSources = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(radiationSourcesArray)
            };
            handle = clearSources.Schedule(activeCellCount, 64, handle);

            if (math.any(_pendingRebaseCells != int3.zero))
            {
                RebaseGridJob rebaseJob = new RebaseGridJob
                {
                    TemperatureFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureFront),
                    RadiationFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationFront),
                    TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureBack),
                    RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(radiationBack),
                    Resolution = _activeResolution,
                    ShiftCells = _pendingRebaseCells,
                    AmbientCelsius = constants.BaseWaterTempCelsius
                };
                _pendingRebaseCells = int3.zero;
                handle = rebaseJob.Schedule(activeCellCount, 64, handle);
            }
            else
            {
                EmissionJob emissionJob = new EmissionJob
                {
                    Sources = (HazardSourceDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sources),
                    SourceCount = _sourceCount,
                    TemperatureSources = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureSourcesArray),
                    RadiationSources = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(radiationSourcesArray),
                    GridOriginAup = _gridOriginAup,
                    CellSizeMeters = math.max(1f, cellSizeMeters),
                    Resolution = _activeResolution,
                    HeatHash = HeatHazardHash,
                    RadiationHash = RadiationHazardHash,
                    MixedHash = MixedHazardHash
                };
                handle = emissionJob.Schedule(handle);

                DiffusionJob diffusionJob = new DiffusionJob
                {
                    TemperatureFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureFront),
                    RadiationFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationFront),
                    TemperatureSources = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureSourcesArray),
                    RadiationSources = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationSourcesArray),
                    TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureBack),
                    RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(radiationBack),
                    Resolution = _activeResolution,
                    Constants = constants,
                    ApplyRadiationDecay = applyDecay ? 1 : 0
                };
                handle = diffusionJob.Schedule(activeCellCount, 64, handle);
            }

            ScanTelemetryJob scanJob = new ScanTelemetryJob
            {
                TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureBack),
                RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationBack),
                Telemetry = (ThermodynamicsHazardTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryScratch),
                UpdraftSignals = (ThermalUpdraftSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(updraftSignals),
                Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(signalCounters),
                Resolution = _activeResolution,
                CellCount = activeCellCount,
                GridOrigin = float3.zero,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = math.max(1f, cellSizeMeters),
                GridOriginHash = HashAupMillimeters(_gridOriginAup),
                Frame = simulationFrame,
                GridVersion = unchecked((uint)_gridVersion),
                SourceCount = unchecked((uint)_sourceCount),
                ShiftSequence = _shiftSequence,
                LowTier = _activeResolution < HighResolution ? 1u : 0u,
                HealthPressureLowTier = _healthPressureLowTierFrames > 0 ? 1u : 0u
            };
            handle = scanJob.Schedule(handle);
            _simulationHandle = handle;
            _simulationJobActive = true;
            H8Memory.RegisterActiveJob(MemoryOwner, handle);
        }

        private void SwapFrontBack()
        {
            VaultGenerationHandle<float> temp = _temperatureFront;
            _temperatureFront = _temperatureBack;
            _temperatureBack = temp;

            VaultGenerationHandle<float> rad = _radiationFront;
            _radiationFront = _radiationBack;
            _radiationBack = rad;
        }

        private void PublishQueuedSignals()
        {
            if (!TryOpenReadArray(in _signalCounters, 1, out NativeArray<int> signalCounters) ||
                !TryOpenReadArray(in _updraftSignals, MaxSignalsPerFrame, out NativeArray<ThermalUpdraftSignal> updraftSignals))
            {
                return;
            }

            int updraftCount = math.min(MaxSignalsPerFrame, math.max(0, signalCounters[0]));
            for (int i = 0; i < updraftCount; i++)
            {
                ThermalUpdraftSignal signal = updraftSignals[i];
                if (math.isfinite(signal.TemperatureCelsius) &&
                    !SignalBus<ThermalUpdraftSignal>.TryPush(in signal))
                {
                    IncrementDroppedSignalCount();
                }
            }
        }

        private void CommitTelemetryScratch()
        {
            if (!HasHandle(in _telemetryRing) || !HasHandle(in _telemetryScratch))
                return;

            if (!TryOpenArray(in _telemetryRing, TelemetryCapacity, out NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryRing) ||
                !TryOpenReadArray(in _telemetryScratch, 1, out NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryScratch))
            {
                return;
            }

            ThermodynamicsHazardTelemetryEntry entry = telemetryScratch[0];
            entry.DiffusionComputeTimeMs = _lastCompleteMs;
            if (_droppedSignalCount > 0)
                entry.Flags |= TelemetryFlagSignalDrop;
            telemetryRing[_telemetryWriteIndex % TelemetryCapacity] = entry;
            _telemetryWriteIndex++;
            if ((entry.Flags & TelemetryFlagNaN) != 0u)
                DumpBlackBox();
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;
        }

        private void UploadVisualTextureIfDirty()
        {
            if (!enableVisualTextureUpload || !_visualDirty || _lastTextureVersion == _gridVersion)
                return;

            bool textureRequiresRebuild = _temperatureTexture == null || _temperatureTexture.width != _activeResolution;
            if (!textureRequiresRebuild && _activeResolution == LowResolution && (_gridVersion & (LowTierVisualUploadStride - 1)) != 0)
                return;

            if (textureRequiresRebuild)
            {
                ReleaseVisualTexture();
                _temperatureTexture = new Texture3D(_activeResolution, _activeResolution, _activeResolution, TextureFormat.RFloat, true)
                {
                    name = "TX_Runtime_ThermodynamicsHeatGrid",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                }; // COLD ALLOC: Texture3D[1] - visual heat distortion scalar field - owner: ThermodynamicsHazardGridRuntime
            }

            if (!TryOpenReadArray(in _temperatureFront, ActiveCellCount, out NativeArray<float> temperatureFront))
                return;

            NativeArray<float> uploadSlice = temperatureFront.GetSubArray(0, ActiveCellCount);
            _temperatureTexture.SetPixelData(uploadSlice, 0);
            _temperatureTexture.Apply(false, false);
            Shader.SetGlobalTexture(HeatTexturePropertyId, _temperatureTexture);
            Shader.SetGlobalVector(GridMetaPropertyId, new Vector4(_activeResolution, cellSizeMeters, _gridVersion, 1f));
            _lastTextureVersion = _gridVersion;
            _visualDirty = false;
        }

        private void ReleaseVisualTexture()
        {
            if (_temperatureTexture == null)
                return;

            Destroy(_temperatureTexture);
            _temperatureTexture = null;
        }

        private ThermodynamicsHazardConstants LoadConstantsOrEmergency()
        {
            return GenerateEmergencyMockConstants();
        }

        private static ThermodynamicsHazardConstants GenerateEmergencyMockConstants()
        {
            return new ThermodynamicsHazardConstants
            {
                BaseWaterTempCelsius = 2f,
                HeatDiffusionRate = 0.58f,
                RadiationDiffusionRate = 0.22f,
                RadiationDecayCoefficient = DefaultRadiationDecayCoefficient,
                RockShieldingFactor = 0.05f,
                VerticalHeatBias = 1.25f,
                HeatDamageThresholdCelsius = 100f,
                RadiationDamageThreshold = 0.35f
            };
        }

#if UNITY_EDITOR
        private void TryReloadCsvOverrides()
        {
            if (!HasHandle(in _csvBytes) || !HasHandle(in _constants))
                return;

            StartConfigWorkerIfNeeded();
            RequestCsvOverrideLoad();
        }

        private static void ParseCsvConstants(NativeArray<byte> bytes, int length, ref ThermodynamicsHazardConstants constants)
        {
            int cursor = 0;
            while (cursor < length)
            {
                uint keyHash = 2166136261u;
                while (cursor < length)
                {
                    byte c = bytes[cursor++];
                    if (c == (byte)',' || c == (byte)'=' || c == (byte)';')
                        break;
                    if (c == (byte)'\r' || c == (byte)'\n')
                        goto NextLine;
                    keyHash = (keyHash ^ ToLowerAscii(c)) * 16777619u;
                }

                float value = ParseFloat(bytes, ref cursor, length);
                ApplyCsvValue(keyHash, value, ref constants);

            NextLine:
                while (cursor < length && bytes[cursor] != (byte)'\n')
                    cursor++;
                if (cursor < length)
                    cursor++;
            }
        }
#endif

        private static void ApplyCsvValue(uint keyHash, float value, ref ThermodynamicsHazardConstants constants)
        {
            if (!math.isfinite(value))
                return;

            switch (keyHash)
            {
                case 0xD22C7CCFu:
                    constants.BaseWaterTempCelsius = value;
                    break;
                case 0x072E5B40u:
                    constants.HeatDiffusionRate = value;
                    break;
                case 0x35572599u:
                    constants.RadiationDiffusionRate = value;
                    break;
                case 0x164961E4u:
                    constants.RockShieldingFactor = value;
                    break;
                case 0x44010D7Bu:
                    constants.RadiationDecayCoefficient = value;
                    break;
                case 0xCA7D3E13u:
                    constants.RadiationDecayCoefficient = MathLodApproximation.ApproxExpNegPade33Reduced(new float4(0.69314718056f * math.rcp(math.max(1f, value)))).x;
                    break;
            }
        }

        private static ThermodynamicsHazardConstants SanitizeConstants(ThermodynamicsHazardConstants constants)
        {
            constants.BaseWaterTempCelsius = math.isfinite(constants.BaseWaterTempCelsius) ? math.clamp(constants.BaseWaterTempCelsius, -8f, 40f) : 2f;
            constants.HeatDiffusionRate = math.isfinite(constants.HeatDiffusionRate) ? math.saturate(constants.HeatDiffusionRate) : 0.58f;
            constants.RadiationDiffusionRate = math.isfinite(constants.RadiationDiffusionRate) ? math.saturate(constants.RadiationDiffusionRate) : 0.22f;
            constants.RadiationDecayCoefficient = math.isfinite(constants.RadiationDecayCoefficient) ? math.clamp(constants.RadiationDecayCoefficient, 0.9f, 1f) : DefaultRadiationDecayCoefficient;
            constants.RockShieldingFactor = math.isfinite(constants.RockShieldingFactor) ? math.clamp(constants.RockShieldingFactor, 0f, 1f) : 0.05f;
            constants.VerticalHeatBias = math.isfinite(constants.VerticalHeatBias) ? math.clamp(constants.VerticalHeatBias, 0.5f, 2f) : 1.25f;
            constants.HeatDamageThresholdCelsius = math.isfinite(constants.HeatDamageThresholdCelsius) ? math.max(1f, constants.HeatDamageThresholdCelsius) : 100f;
            constants.RadiationDamageThreshold = math.isfinite(constants.RadiationDamageThreshold) ? math.saturate(constants.RadiationDamageThreshold) : 0.35f;
            return constants;
        }

        private static bool HasUsableConstants(ThermodynamicsHazardConstants constants)
        {
            return math.isfinite(constants.BaseWaterTempCelsius) &&
                   math.isfinite(constants.HeatDiffusionRate) &&
                   math.isfinite(constants.RadiationDiffusionRate) &&
                   math.isfinite(constants.RadiationDecayCoefficient) &&
                   constants.HeatDiffusionRate > 0f &&
                   constants.RadiationDecayCoefficient >= 0.9f;
        }

        private void DumpBlackBox()
        {
            WriteDump("Dump_THERMO_SURGEON.bin");
            WriteDump("Dump_THERMODYNAMICS.bin");
            WriteDump("Dump_THERMODYNAMICS.h8dump");
            WriteDump("Dump_SHINOBU_16.bin");
            WriteDump("Dump_SHINOBU_16.h8dump");
        }

        private void WriteDump(string fileName)
        {
            if (!HasHandle(in _telemetryRing))
                return;

            try
            {
                if (!TryOpenReadArray(in _telemetryRing, TelemetryCapacity, out NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryRing))
                    return;

                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", fileName));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(0x484543544F4E3800ul);
                writer.Write(TelemetryCapacity);
                writer.Write(UnsafeUtility.SizeOf<ThermodynamicsHazardTelemetryEntry>());
                writer.Write(_telemetryWriteIndex);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    ThermodynamicsHazardTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.MaxGridTemperature);
                    writer.Write(entry.MaxRadiationLevel);
                    writer.Write(entry.DiffusionComputeTimeMs);
                    writer.Write(entry.GridOrigin.x);
                    writer.Write(entry.GridOrigin.y);
                    writer.Write(entry.GridOrigin.z);
                    writer.Write(entry.Frame);
                    writer.Write(entry.GridVersion);
                    writer.Write(entry.SourceCount);
                    writer.Write(entry.Flags);
                    writer.Write(entry.ShiftSequence);
                    writer.Write(entry.NaNCellIndex);
                    writer.Write(entry.ActiveResolution);
                    writer.Write(entry.GridOriginHash);
                    writer.Write(entry._pad0);
                    writer.Write(entry._pad1);
                }
            }
            catch (Exception)
            {
            }
        }

        private static byte ToLowerAscii(byte c)
        {
            return c >= (byte)'A' && c <= (byte)'Z' ? (byte)(c + 32) : c;
        }

        private static float ParseFloat(NativeArray<byte> bytes, ref int cursor, int length)
        {
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            float sign = 1f;
            if (cursor < length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float value = 0f;
            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = value * 10f + (c - (byte)'0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                float scale = 0.1f;
                while (cursor < length)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    value += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    cursor++;
                }
            }

            return value * sign;
        }

        private static uint HashAupMillimeters(double3 aup)
        {
            ulong hash = 1469598103934665603UL;
            hash = MixHash(hash, QuantizeMillimeters(aup.x));
            hash = MixHash(hash, QuantizeMillimeters(aup.y));
            hash = MixHash(hash, QuantizeMillimeters(aup.z));
            return unchecked((uint)(hash ^ (hash >> 32)));
        }

        private static long QuantizeMillimeters(double value)
        {
            return math.isfinite(value) ? (long)math.round(value * 1000.0) : 0L;
        }

        private static ulong MixHash(ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                return hash * 1099511628211UL;
            }
        }

        private static float ReadFloatLe(NativeArray<byte> bytes, int offset)
        {
            int raw = bytes[offset] |
                      (bytes[offset + 1] << 8) |
                      (bytes[offset + 2] << 16) |
                      (bytes[offset + 3] << 24);
            return math.asfloat(raw);
        }

        private static ThermodynamicsHazardSample SampleTrilinear(
            float* temperatureGrid,
            float* radiationGrid,
            int resolution,
            float cellSize,
            double3 originAup,
            double3 sampleAup,
            ThermodynamicsHazardConstants constants)
        {
            ThermodynamicsHazardSample sample = default;
            float safeCellSize = math.max(1f, cellSize);
            double3 offset = sampleAup - originAup;
            float3 grid = (float3)(offset / safeCellSize) + new float3(resolution * 0.5f);
            sample.LocalGridPosition = grid;

            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                grid.x > resolution - 1 || grid.y > resolution - 1 || grid.z > resolution - 1)
            {
                return sample;
            }

            int x0 = math.clamp((int)math.floor(grid.x), 0, resolution - 1);
            int y0 = math.clamp((int)math.floor(grid.y), 0, resolution - 1);
            int z0 = math.clamp((int)math.floor(grid.z), 0, resolution - 1);
            int x1 = math.min(resolution - 1, x0 + 1);
            int y1 = math.min(resolution - 1, y0 + 1);
            int z1 = math.min(resolution - 1, z0 + 1);
            float tx = math.saturate(grid.x - x0);
            float ty = math.saturate(grid.y - y0);
            float tz = math.saturate(grid.z - z0);

            float temperature = Trilinear(temperatureGrid, resolution, x0, y0, z0, x1, y1, z1, tx, ty, tz);
            float radiation = Trilinear(radiationGrid, resolution, x0, y0, z0, x1, y1, z1, tx, ty, tz);
            sample.TemperatureCelsius = math.isfinite(temperature) ? temperature : constants.BaseWaterTempCelsius;
            sample.Radiation = math.isfinite(radiation) ? math.max(0f, radiation) : 0f;
            sample.HeatDamage = math.max(0f, sample.TemperatureCelsius - constants.HeatDamageThresholdCelsius);
            sample.RadiationDamage = math.max(0f, sample.Radiation - constants.RadiationDamageThreshold);
            sample.Flags = sample.HeatDamage > 0f || sample.RadiationDamage > 0f ? 1u : 0u;
            return sample;
        }

        private static float Trilinear(
            float* grid,
            int resolution,
            int x0,
            int y0,
            int z0,
            int x1,
            int y1,
            int z1,
            float tx,
            float ty,
            float tz)
        {
            float c000 = grid[Flatten(x0, y0, z0, resolution)];
            float c100 = grid[Flatten(x1, y0, z0, resolution)];
            float c010 = grid[Flatten(x0, y1, z0, resolution)];
            float c110 = grid[Flatten(x1, y1, z0, resolution)];
            float c001 = grid[Flatten(x0, y0, z1, resolution)];
            float c101 = grid[Flatten(x1, y0, z1, resolution)];
            float c011 = grid[Flatten(x0, y1, z1, resolution)];
            float c111 = grid[Flatten(x1, y1, z1, resolution)];
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float c0 = math.lerp(c00, c10, ty);
            float c1 = math.lerp(c01, c11, ty);
            return math.lerp(c0, c1, tz);
        }

        private static int Flatten(int x, int y, int z, int resolution)
        {
            return x + (y * resolution) + (z * resolution * resolution);
        }

        private static void AddFinite(float* address, float value)
        {
            if (!math.isfinite(value) || value == 0f)
                return;

            float oldValue = *address;
            *address = math.isfinite(oldValue) ? oldValue + value : value;
        }

        private static float Shield(int x, int y, int z, int nx, int ny, int nz, float rockShieldingFactor)
        {
            return MockWorldSampler.SampleSdfBetweenCells(x, y, z, nx, ny, nz) < 0f
                ? math.clamp(rockShieldingFactor, 0f, 1f)
                : 1f;
        }

        private static class MockWorldSampler
        {
            public static float SampleSdfBetweenCells(int x, int y, int z, int nx, int ny, int nz)
            {
                int barrier = 15;
                bool crossesBarrier = (x < barrier && nx >= barrier) || (x >= barrier && nx < barrier);
                bool hasGap = (y & 7) == 0 || (z & 7) == 0;
                return crossesBarrier && !hasGap ? -1f : 1f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ResetCountersJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
            public int CounterCount;

            public void Execute()
            {
                for (int i = 0; i < CounterCount; i++)
                    Counters[i] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearSourceGridJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationSources;

            public void Execute(int index)
            {
                TemperatureSources[index] = 0f;
                RadiationSources[index] = 0f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct EmissionJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public HazardSourceDTO* Sources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationSources;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int SourceCount;
            public uint HeatHash;
            public uint RadiationHash;
            public uint MixedHash;

            public void Execute()
            {
                int count = math.max(0, SourceCount);
                for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
                {
                    HazardSourceDTO source = Sources[sourceIndex];
                    if (!math.all(math.isfinite(source.AUP)) || !math.isfinite(source.Intensity) || source.Intensity <= 0f)
                        continue;

                    float radius = math.max(CellSizeMeters, source.Radius);
                    double3 offset = source.AUP - GridOriginAup;
                    float3 grid = (float3)(offset / math.max(1f, CellSizeMeters)) + new float3(Resolution * 0.5f);
                    int3 center = (int3)math.round(grid);
                    int radiusCells = math.max(1, (int)math.ceil(radius / math.max(1f, CellSizeMeters)));
                    int minX = math.max(0, center.x - radiusCells);
                    int maxX = math.min(Resolution - 1, center.x + radiusCells);
                    int minY = math.max(0, center.y - radiusCells);
                    int maxY = math.min(Resolution - 1, center.y + radiusCells);
                    int minZ = math.max(0, center.z - radiusCells);
                    int maxZ = math.min(Resolution - 1, center.z + radiusCells);
                    float radiusSq = radius * radius;

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                float3 cellCenter = (new float3(x, y, z) - new float3(Resolution * 0.5f)) * CellSizeMeters;
                                float distanceSq = math.lengthsq(cellCenter - (float3)offset);
                                float falloff = radiusSq * math.rcp(math.max(1f, distanceSq));
                                float contribution = source.Intensity * math.saturate(falloff);
                                int cellIndex = Flatten(x, y, z, Resolution);
                                if (source.HazardTypeHash == HeatHash || source.HazardTypeHash == MixedHash)
                                    AddFinite(TemperatureSources + cellIndex, contribution);
                                if (source.HazardTypeHash == RadiationHash || source.HazardTypeHash == MixedHash)
                                    AddFinite(RadiationSources + cellIndex, contribution);
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct DiffusionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureFront;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationFront;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationSources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationBack;
            public ThermodynamicsHazardConstants Constants;
            public int Resolution;
            public int ApplyRadiationDecay;

            public void Execute(int index)
            {
                int plane = Resolution * Resolution;
                int z = index / plane;
                int rem = index - z * plane;
                int y = rem / Resolution;
                int x = rem - y * Resolution;
                float selfHeat = TemperatureFront[index];
                float selfRad = RadiationFront[index];

                int left = Flatten(math.max(0, x - 1), y, z, Resolution);
                int right = Flatten(math.min(Resolution - 1, x + 1), y, z, Resolution);
                int down = Flatten(x, math.max(0, y - 1), z, Resolution);
                int up = Flatten(x, math.min(Resolution - 1, y + 1), z, Resolution);
                int back = Flatten(x, y, math.max(0, z - 1), Resolution);
                int forward = Flatten(x, y, math.min(Resolution - 1, z + 1), Resolution);

                float shieldLeft = Shield(x, y, z, math.max(0, x - 1), y, z, Constants.RockShieldingFactor);
                float shieldRight = Shield(x, y, z, math.min(Resolution - 1, x + 1), y, z, Constants.RockShieldingFactor);
                float shieldDown = Shield(x, y, z, x, math.max(0, y - 1), z, Constants.RockShieldingFactor);
                float shieldUp = Shield(x, y, z, x, math.min(Resolution - 1, y + 1), z, Constants.RockShieldingFactor);
                float shieldBack = Shield(x, y, z, x, y, math.max(0, z - 1), Constants.RockShieldingFactor);
                float shieldForward = Shield(x, y, z, x, y, math.min(Resolution - 1, z + 1), Constants.RockShieldingFactor);

                float upwardBias = math.max(0.5f, Constants.VerticalHeatBias);
                float inverseUpwardBias = math.rcp(math.max(0.0001f, upwardBias));
                float downHeatDelta = TemperatureFront[down] - selfHeat;
                float upHeatDelta = TemperatureFront[up] - selfHeat;
                float downCoefficient = math.select(inverseUpwardBias, upwardBias, downHeatDelta > 0f);
                float upCoefficient = math.select(upwardBias, inverseUpwardBias, upHeatDelta > 0f);
                float heatNeighborDelta =
                    (TemperatureFront[left] - selfHeat) * shieldLeft +
                    (TemperatureFront[right] - selfHeat) * shieldRight +
                    (TemperatureFront[back] - selfHeat) * shieldBack +
                    (TemperatureFront[forward] - selfHeat) * shieldForward +
                    downHeatDelta * shieldDown * downCoefficient +
                    upHeatDelta * shieldUp * upCoefficient;

                float radNeighborDelta =
                    (RadiationFront[left] - selfRad) * shieldLeft +
                    (RadiationFront[right] - selfRad) * shieldRight +
                    (RadiationFront[back] - selfRad) * shieldBack +
                    (RadiationFront[forward] - selfRad) * shieldForward +
                    (RadiationFront[down] - selfRad) * shieldDown +
                    (RadiationFront[up] - selfRad) * shieldUp;

                float nextHeat = selfHeat + math.saturate(Constants.HeatDiffusionRate) * heatNeighborDelta;
                nextHeat = math.max(nextHeat, Constants.BaseWaterTempCelsius);
                nextHeat = math.max(nextHeat, TemperatureSources[index]);

                float nextRad = math.max(0f, selfRad + math.saturate(Constants.RadiationDiffusionRate) * radNeighborDelta);
                nextRad = math.max(nextRad, RadiationSources[index]);
                if (ApplyRadiationDecay != 0)
                    nextRad *= math.clamp(Constants.RadiationDecayCoefficient, 0.9f, 1f);

                if (!math.isfinite(nextHeat))
                    nextHeat = Constants.BaseWaterTempCelsius;
                if (!math.isfinite(nextRad))
                    nextRad = 0f;

                TemperatureBack[index] = nextHeat;
                RadiationBack[index] = nextRad;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct RebaseGridJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureFront;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationFront;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationBack;
            public int3 ShiftCells;
            public int Resolution;
            public float AmbientCelsius;

            public void Execute(int index)
            {
                int plane = Resolution * Resolution;
                int z = index / plane;
                int rem = index - z * plane;
                int y = rem / Resolution;
                int x = rem - y * Resolution;
                int sx = x + ShiftCells.x;
                int sy = y + ShiftCells.y;
                int sz = z + ShiftCells.z;
                if ((uint)sx >= (uint)Resolution || (uint)sy >= (uint)Resolution || (uint)sz >= (uint)Resolution)
                {
                    TemperatureBack[index] = AmbientCelsius;
                    RadiationBack[index] = 0f;
                    return;
                }

                int sourceIndex = Flatten(sx, sy, sz, Resolution);
                TemperatureBack[index] = TemperatureFront[sourceIndex];
                RadiationBack[index] = RadiationFront[sourceIndex];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ScanTelemetryJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction, NoAlias] public float* RadiationBack;
            [NativeDisableUnsafePtrRestriction, NoAlias] public ThermodynamicsHazardTelemetryEntry* Telemetry;
            [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalUpdraftSignal* UpdraftSignals;
            [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
            public float3 GridOrigin;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public uint GridOriginHash;
            public int Resolution;
            public int CellCount;
            public uint Frame;
            public uint GridVersion;
            public uint SourceCount;
            public uint ShiftSequence;
            public uint LowTier;
            public uint HealthPressureLowTier;

            public void Execute()
            {
                float maxTemp = -1000f;
                float maxRad = 0f;
                uint flags = LowTier != 0u ? TelemetryFlagLowTier : 0u;
                if (HealthPressureLowTier != 0u)
                    flags |= TelemetryFlagHealthPressureLowTier;

                uint nanIndex = 0u;
                int updraftCount = 0;
                int plane = Resolution * Resolution;
                for (int i = 0; i < CellCount; i++)
                {
                    float temp = TemperatureBack[i];
                    float rad = RadiationBack[i];
                    bool finite = math.isfinite(temp) && math.isfinite(rad);
                    if (!finite)
                    {
                        flags |= TelemetryFlagNaN;
                        nanIndex = unchecked((uint)i);
                        continue;
                    }

                    maxTemp = math.max(maxTemp, temp);
                    maxRad = math.max(maxRad, rad);
                    if (temp > UpdraftThresholdCelsius && updraftCount < MaxSignalsPerFrame)
                    {
                        int z = i / plane;
                        int rem = i - z * plane;
                        int y = rem / Resolution;
                        int x = rem - y * Resolution;
                        double3 aup = GridOriginAup + ((new double3(x, y, z) - new double3(Resolution * 0.5)) * CellSizeMeters);
                        UpdraftSignals[updraftCount] = new ThermalUpdraftSignal
                        {
                            Aup = aup,
                            TemperatureCelsius = temp,
                            Intensity01 = math.saturate((temp - UpdraftThresholdCelsius) * 0.01f),
                            CellIndex = unchecked((uint)i),
                            Frame = Frame,
                            Flags = 0
                        };
                        updraftCount++;
                    }
                }

                Counters[0] = updraftCount;

                Telemetry[0] = new ThermodynamicsHazardTelemetryEntry
                {
                    MaxGridTemperature = maxTemp,
                    MaxRadiationLevel = maxRad,
                    DiffusionComputeTimeMs = 0f,
                    GridOrigin = GridOrigin,
                    Frame = Frame,
                    GridVersion = GridVersion,
                    SourceCount = SourceCount,
                    Flags = flags | (ShiftSequence != 0u ? TelemetryFlagRebase : 0u),
                    ShiftSequence = ShiftSequence,
                    NaNCellIndex = nanIndex,
                    ActiveResolution = unchecked((uint)Resolution),
                    GridOriginHash = GridOriginHash,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
            }
        }
    }
}
