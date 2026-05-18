using System;
using System.IO;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed unsafe partial class ThermodynamicsHazardGridRuntime : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IScalabilityChangedEventListener
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

        private const SystemID MemoryOwner = SystemID.External;
        private const BufferID VaultConstantsBuffer = BufferID.ThermodynamicsHazardConstants;
        private const BufferID VaultTemperatureFrontMirror = BufferID.ThermodynamicsTemperatureFrontMirror;
        private const BufferID VaultRadiationFrontMirror = BufferID.ThermodynamicsRadiationFrontMirror;
        private const BufferID VaultTemperatureFrontBuffer = (BufferID)70019;
        private const BufferID VaultTemperatureBackBuffer = (BufferID)70020;
        private const BufferID VaultRadiationFrontBuffer = (BufferID)70021;
        private const BufferID VaultRadiationBackBuffer = (BufferID)70022;
        private const BufferID VaultTemperatureSourcesBuffer = (BufferID)70023;
        private const BufferID VaultRadiationSourcesBuffer = (BufferID)70024;
        private const BufferID VaultSourcesBuffer = (BufferID)70025;
        private const BufferID VaultSourceIdsBuffer = (BufferID)70026;
        private const BufferID VaultEntityAupsBuffer = (BufferID)70027;
        private const BufferID VaultEntityIdsBuffer = (BufferID)70028;
        private const BufferID VaultEntityDamageTimersBuffer = (BufferID)70029;
        private const BufferID VaultEntityDamageAccumulatorsBuffer = (BufferID)70030;
        private const BufferID VaultMockDamageSignalsBuffer = (BufferID)70031;
        private const BufferID VaultCombatDamageSignalsBuffer = (BufferID)70032;
        private const BufferID VaultUpdraftSignalsBuffer = (BufferID)70033;
        private const BufferID VaultSignalCountersBuffer = (BufferID)70034;
        private const BufferID VaultTelemetryRingBuffer = (BufferID)70035;
        private const BufferID VaultTelemetryScratchBuffer = (BufferID)70036;
        private const BufferID VaultCsvBytesBuffer = (BufferID)70037;
        private const BufferID VaultBinaryConstantBytesBuffer = (BufferID)70038;
        private const float DefaultCellSizeMeters = 10f;
        private const float TierSwitchHysteresisSeconds = 3f;
        private const float CsvPollSeconds = 1f;
        private const float DamageIntervalSeconds = 1f;
        private const float UpdraftThresholdCelsius = 120f;
        private const float DefaultRadiationDecayCoefficient = 0.9975f;
        private const int LowTierVisualUploadStride = 4;
        private const int HealthPressureLowTierFrames = 120;
        private const int CsvBufferBytes = 4096;
        private const int BinaryConstantsBytes = 16;
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint TelemetryFlagLowTier = 1u << 1;
        private const uint TelemetryFlagRebase = 1u << 2;
        private const uint TelemetryFlagHealthPressureLowTier = 1u << 3;
        private const uint MockPlayerEntityId = 1u;
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
        [Tooltip("Polls StreamingAssets/hazard_profiles.csv on a cold cadence and parses into unmanaged constants.")]
        private bool monitorCsvOverrides = true;

        [SerializeField]
        [Tooltip("Uploads the front temperature buffer into a global RFloat Texture3D for heat-haze shaders.")]
        private bool enableVisualTextureUpload = true;

        [SerializeField]
        [Tooltip("Forces the 16^3 toaster-grid path regardless of hardware tier.")]
        private bool forceLowResolution;

        private VaultBufferHandle<float> _temperatureFront;
        private VaultBufferHandle<float> _temperatureBack;
        private VaultBufferHandle<float> _radiationFront;
        private VaultBufferHandle<float> _radiationBack;
        private VaultBufferHandle<float> _temperatureSources;
        private VaultBufferHandle<float> _radiationSources;
        private VaultBufferHandle<HazardSourceDTO> _sources;
        private VaultBufferHandle<uint> _sourceIds;
        private VaultBufferHandle<double3> _entityAups;
        private VaultBufferHandle<uint> _entityIds;
        private VaultBufferHandle<float> _entityDamageTimers;
        private VaultBufferHandle<float> _entityDamageAccumulators;
        private VaultBufferHandle<MockDamageSignal> _mockDamageSignals;
        private VaultBufferHandle<ThermodynamicsCombatDamageSignal> _combatDamageSignals;
        private VaultBufferHandle<ThermalUpdraftSignal> _updraftSignals;
        private VaultBufferHandle<int> _signalCounters;
        private VaultBufferHandle<ThermodynamicsHazardTelemetryEntry> _telemetryRing;
        private VaultBufferHandle<ThermodynamicsHazardTelemetryEntry> _telemetryScratch;
        private VaultBufferHandle<ThermodynamicsHazardConstants> _constants;
        private VaultBufferHandle<float> _vaultTemperatureFrontMirror;
        private VaultBufferHandle<float> _vaultRadiationFrontMirror;
        private VaultBufferHandle<byte> _csvBytes;
        private VaultBufferHandle<byte> _binaryConstantBytes;

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
        private bool _registeredScalability;
        private bool _mockSeeded;
        private bool _visualDirty;
        private bool _vaultMirrorRequested;
        private uint _shiftSequence;
        private HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private static GlobalDataVault _standaloneVault;

        /// <summary>True after native buffers are allocated and the runtime is registered.</summary>
        public bool IsInitialized => _temperatureFront.IsCreated && _constants.IsCreated;

        private void Awake()
        {
            EnsureNativeState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
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
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregister();
            StopConfigWorker();
            ReleaseNativeState();
            ReleaseVisualTexture();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            EnsureNativeState();
            ApplyPendingConfigLoads();
            TryRegister();

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
            if (!monitorCsvOverrides)
                return;

            _csvPollTimer -= 0.1f;
            if (_csvPollTimer > 0f)
                return;

            _csvPollTimer = CsvPollSeconds;
            TryReloadCsvOverrides();
        }

        /// <inheritdoc />
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
        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedScalabilityTier = payload.CurrentQualityTier;
        }

        /// <summary>
        /// Registers or updates a mathematical hazard source.
        /// </summary>
        public bool TryUpsertSource(uint sourceId, double3 aup, float intensity, float radiusMeters, uint hazardTypeHash)
        {
            if (sourceId == 0u || _simulationJobActive || !_sources.IsCreated || !math.all(math.isfinite(aup)))
                return false;

            NativeArray<uint> sourceIds = ResolveArray(ref _sourceIds);
            NativeArray<HazardSourceDTO> sources = ResolveArray(ref _sources);
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
            if (sourceId == 0u || _simulationJobActive || !_sources.IsCreated)
                return false;

            NativeArray<uint> sourceIds = ResolveArray(ref _sourceIds);
            NativeArray<HazardSourceDTO> sources = ResolveArray(ref _sources);
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
        /// Registers or updates an entity hazard sample slot for throttled damage emission.
        /// </summary>
        public bool TryUpsertEntity(uint entityId, double3 aup)
        {
            if (entityId == 0u || _simulationJobActive || !_entityIds.IsCreated || !math.all(math.isfinite(aup)))
                return false;

            NativeArray<uint> entityIds = ResolveArray(ref _entityIds);
            NativeArray<double3> entityAups = ResolveArray(ref _entityAups);
            for (int i = 0; i < _entityCount; i++)
            {
                if (entityIds[i] != entityId)
                    continue;

                entityAups[i] = aup;
                return true;
            }

            if (_entityCount >= MaxEntityCount)
                return false;

            NativeArray<float> damageTimers = ResolveArray(ref _entityDamageTimers);
            NativeArray<float> damageAccumulators = ResolveArray(ref _entityDamageAccumulators);
            entityIds[_entityCount] = entityId;
            entityAups[_entityCount] = aup;
            damageTimers[_entityCount] = 0f;
            damageAccumulators[_entityCount] = 0f;
            _entityCount++;
            return true;
        }

        /// <summary>
        /// Samples heat and radiation with trilinear interpolation from the current front buffers.
        /// </summary>
        public bool TrySample(double3 aup, out ThermodynamicsHazardSample sample)
        {
            sample = default;
            if (!_temperatureFront.IsCreated || !_radiationFront.IsCreated || !math.all(math.isfinite(aup)))
                return false;

            ThermodynamicsHazardConstants constants = GetConstantsValue();
            float* temp = (float*)ResolvePointer(ref _temperatureFront);
            float* rad = (float*)ResolvePointer(ref _radiationFront);
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
            if (!_temperatureFront.IsCreated || !_radiationFront.IsCreated)
                return false;

            pointers = new ThermodynamicsHazardGridPointers
            {
                TemperatureFront = (float*)ResolvePointer(ref _temperatureFront),
                RadiationFront = (float*)ResolvePointer(ref _radiationFront),
                CellCount = ActiveCellCount,
                Resolution = _activeResolution
            };
            return true;
        }

        private ref HazardSourceDTO GetHazardSourceRef(int index)
        {
            EnsureNativeState();
            int safeIndex = math.clamp(index, 0, MaxSourceCount - 1);
            HazardSourceDTO* sources = (HazardSourceDTO*)ResolvePointer(ref _sources);
            return ref UnsafeUtility.AsRef<HazardSourceDTO>(sources + safeIndex);
        }

        /// <summary>
        /// Returns a mutable pointer to unmanaged tuning constants for editor facades.
        /// </summary>
        public ThermodynamicsHazardConstants* GetConstantsPointer()
        {
            EnsureNativeState();
            return _constants.IsCreated
                ? (ThermodynamicsHazardConstants*)ResolvePointer(ref _constants)
                : null;
        }

        /// <summary>
        /// Returns a mutable pointer that is guaranteed to resolve from GlobalDataVault unmanaged memory.
        /// </summary>
        public bool TryGetGlobalDataVaultConstantsPointer(out ThermodynamicsHazardConstants* constants)
        {
            EnsureNativeState();
            constants = null;

            if (!_constants.IsCreated)
                return false;

            constants = (ThermodynamicsHazardConstants*)ResolvePointer(ref _constants);
            return constants != null;
        }

        /// <summary>
        /// Copies front-grid metadata for editor gizmos without exposing managed collections.
        /// </summary>
        public bool TryGetGridReadback(out NativeArray<float> temperature, out NativeArray<float> radiation, out int resolution, out double3 originAup, out float cellSize, out int version)
        {
            temperature = _temperatureFront.IsCreated ? ResolveArray(ref _temperatureFront) : default;
            radiation = _radiationFront.IsCreated ? ResolveArray(ref _radiationFront) : default;
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;
            return _temperatureFront.IsCreated && _radiationFront.IsCreated;
        }

        /// <summary>
        /// Copies the live front grid into GlobalDataVault mirrors and returns Vault-backed views for editor visualization.
        /// </summary>
        public bool TryGetVaultGridReadback(out NativeArray<float> temperature, out NativeArray<float> radiation, out int resolution, out double3 originAup, out float cellSize, out int version)
        {
            temperature = default;
            radiation = default;
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _gridVersion;

            EnsureNativeState();
            _vaultMirrorRequested = true;
            if (!EnsureVaultGridMirrors())
                return false;

            MirrorFrontGridToVault();
            temperature = ResolveArray(ref _vaultTemperatureFrontMirror);
            radiation = ResolveArray(ref _vaultRadiationFrontMirror);
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = cellSizeMeters;
            version = _vaultMirrorVersion;
            return temperature.IsCreated && radiation.IsCreated;
        }

        private int ActiveCellCount => _activeResolution * _activeResolution * _activeResolution;

        private void EnsureNativeState()
        {
            if (_temperatureFront.IsCreated)
                return;

            _vault = ResolveDataVault();
            _gridOriginAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(Vector3.zero);
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
            _entityDamageTimers = AcquireBuffer<float>(VaultEntityDamageTimersBuffer, MaxEntityCount);
            _entityDamageAccumulators = AcquireBuffer<float>(VaultEntityDamageAccumulatorsBuffer, MaxEntityCount);
            _mockDamageSignals = AcquireBuffer<MockDamageSignal>(VaultMockDamageSignalsBuffer, MaxSignalsPerFrame);
            _combatDamageSignals = AcquireBuffer<ThermodynamicsCombatDamageSignal>(VaultCombatDamageSignalsBuffer, MaxSignalsPerFrame);
            _updraftSignals = AcquireBuffer<ThermalUpdraftSignal>(VaultUpdraftSignalsBuffer, MaxSignalsPerFrame);
            _signalCounters = AcquireBuffer<int>(VaultSignalCountersBuffer, 4);
            _csvBytes = AcquireBuffer<byte>(VaultCsvBytesBuffer, CsvBufferBytes);
            _binaryConstantBytes = AcquireBuffer<byte>(VaultBinaryConstantBytesBuffer, BinaryConstantsBytes);
            _telemetryRing = AcquireBuffer<ThermodynamicsHazardTelemetryEntry>(VaultTelemetryRingBuffer, TelemetryCapacity);
            _telemetryScratch = AcquireBuffer<ThermodynamicsHazardTelemetryEntry>(VaultTelemetryScratchBuffer, 1);
            _constants = AcquireBuffer<ThermodynamicsHazardConstants>(VaultConstantsBuffer, 1);

            ThermodynamicsHazardConstants loadedConstants = LoadConstantsOrEmergency();
            ref ThermodynamicsHazardConstants constants = ref _constants.GetElementAsRef(EnsureVault(), 0);
            constants = HasUsableConstants(constants) ? SanitizeConstants(constants) : loadedConstants;
            StartConfigWorkerIfNeeded();
            RequestBinaryConstantsLoad();
        }

        private VaultBufferHandle<T> AcquireBuffer<T>(BufferID bufferId, int length) where T : struct
        {
            IDataVault vault = EnsureVault();
            VaultBufferHandle<T> handle = vault.GetBufferHandle<T>(
                bufferId,
                length,
                MemoryOwner,
                NativeArrayOptions.ClearMemory);
            if (!handle.IsCreated)
                throw new InvalidOperationException("Thermodynamics vault buffer acquisition failed.");

            return handle;
        }

        private IDataVault EnsureVault()
        {
            _vault ??= ResolveDataVault();
            if (_vault == null)
                throw new InvalidOperationException("Thermodynamics GlobalDataVault unavailable.");

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

        private void* ResolvePointer<T>(ref VaultBufferHandle<T> handle) where T : struct
        {
            return handle.ResolvePointer(EnsureVault());
        }

        private ThermodynamicsHazardConstants GetConstantsValue()
        {
            return _constants.IsCreated
                ? SanitizeConstants(_constants.GetElementAsRef(EnsureVault(), 0))
                : GenerateEmergencyMockConstants();
        }

        private bool EnsureVaultGridMirrors()
        {
            if (!_vaultTemperatureFrontMirror.IsCreated || _vaultTemperatureFrontMirror.Length < MaxCellCount)
                _vaultTemperatureFrontMirror = AcquireBuffer<float>(VaultTemperatureFrontMirror, MaxCellCount);

            if (!_vaultRadiationFrontMirror.IsCreated || _vaultRadiationFrontMirror.Length < MaxCellCount)
                _vaultRadiationFrontMirror = AcquireBuffer<float>(VaultRadiationFrontMirror, MaxCellCount);

            return _vaultTemperatureFrontMirror.IsCreated && _vaultRadiationFrontMirror.IsCreated;
        }

        private void MirrorFrontGridToVault()
        {
            if (!_temperatureFront.IsCreated || !_radiationFront.IsCreated || !EnsureVaultGridMirrors())
            {
                return;
            }

            int count = ActiveCellCount;
            NativeArray<float> temperatureFront = ResolveArray(ref _temperatureFront);
            NativeArray<float> radiationFront = ResolveArray(ref _radiationFront);
            NativeArray<float> temperatureMirror = ResolveArray(ref _vaultTemperatureFrontMirror);
            NativeArray<float> radiationMirror = ResolveArray(ref _vaultRadiationFrontMirror);
            NativeArray<float>.Copy(temperatureFront, 0, temperatureMirror, 0, count);
            NativeArray<float>.Copy(radiationFront, 0, radiationMirror, 0, count);
            _vaultMirrorVersion = _gridVersion;
        }

        private void ReleaseNativeState()
        {
            if (_simulationJobActive)
                _simulationHandle.Complete();

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
            _entityDamageTimers = default;
            _entityDamageAccumulators = default;
            _mockDamageSignals = default;
            _combatDamageSignals = default;
            _updraftSignals = default;
            _signalCounters = default;
            _telemetryRing = default;
            _telemetryScratch = default;
            _constants = default;
            _vaultTemperatureFrontMirror = default;
            _vaultRadiationFrontMirror = default;
            _vaultMirrorVersion = -1;
            _csvBytes = default;
            _binaryConstantBytes = default;
            _simulationHandle = default;
            _simulationJobActive = false;
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
                RefreshScalabilityTierFromRegistry();
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

        private void ResolveResolutionWithHysteresis(float dt)
        {
            int desired = UsesLowResolution() ? LowResolution : HighResolution;
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

        private bool UsesLowResolution()
        {
            if (forceLowResolution || _healthPressureLowTierFrames > 0)
                return true;

            return IsLowTier(_cachedScalabilityTier);
        }

        private void RefreshScalabilityTierFromRegistry()
        {
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
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

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown;
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
            NativeArray<float> temperatureFront = ResolveArray(ref _temperatureFront);
            NativeArray<float> temperatureBack = ResolveArray(ref _temperatureBack);
            NativeArray<float> radiationFront = ResolveArray(ref _radiationFront);
            NativeArray<float> radiationBack = ResolveArray(ref _radiationBack);
            NativeArray<float> temperatureSources = ResolveArray(ref _temperatureSources);
            NativeArray<float> radiationSources = ResolveArray(ref _radiationSources);
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

            ref HazardSourceDTO heat = ref GetHazardSourceRef(0);
            ref HazardSourceDTO radiation = ref GetHazardSourceRef(1);
            _sourceCount = MockHazardGenerator.GenerateEmergencyMockSources(
                ref heat,
                ref radiation,
                _gridOriginAup,
                math.max(1f, cellSizeMeters),
                HeatHazardHash,
                RadiationHazardHash);
            NativeArray<uint> sourceIds = ResolveArray(ref _sourceIds);
            sourceIds[0] = MockHazardGenerator.MockHeatSourceId;
            sourceIds[1] = MockHazardGenerator.MockRadiationSourceId;
            _mockSeeded = true;

            double3 mockEntityAup = _gridOriginAup + new double3(cellSizeMeters * 2.5, cellSizeMeters * 2.0, cellSizeMeters * 2.5);
            TryUpsertEntity(MockPlayerEntityId, mockEntityAup);
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
            NativeArray<float> temperatureFront = ResolveArray(ref _temperatureFront);
            NativeArray<float> temperatureBack = ResolveArray(ref _temperatureBack);
            NativeArray<float> radiationFront = ResolveArray(ref _radiationFront);
            NativeArray<float> radiationBack = ResolveArray(ref _radiationBack);
            NativeArray<float> temperatureSourcesArray = ResolveArray(ref _temperatureSources);
            NativeArray<float> radiationSourcesArray = ResolveArray(ref _radiationSources);
            NativeArray<HazardSourceDTO> sources = ResolveArray(ref _sources);
            NativeArray<double3> entityAups = ResolveArray(ref _entityAups);
            NativeArray<uint> entityIds = ResolveArray(ref _entityIds);
            NativeArray<float> entityDamageTimers = ResolveArray(ref _entityDamageTimers);
            NativeArray<float> entityDamageAccumulators = ResolveArray(ref _entityDamageAccumulators);
            NativeArray<MockDamageSignal> mockDamageSignals = ResolveArray(ref _mockDamageSignals);
            NativeArray<ThermodynamicsCombatDamageSignal> combatDamageSignals = ResolveArray(ref _combatDamageSignals);
            NativeArray<ThermalUpdraftSignal> updraftSignals = ResolveArray(ref _updraftSignals);
            NativeArray<int> signalCounters = ResolveArray(ref _signalCounters);
            NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryScratch = ResolveArray(ref _telemetryScratch);
            ThermodynamicsHazardConstants constants = GetConstantsValue();
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
                handle = emissionJob.Schedule(math.max(1, _sourceCount), 1, handle);

                DiffusionJob diffusionJob = new DiffusionJob
                {
                    TemperatureFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureFront),
                    RadiationFront = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationFront),
                    TemperatureSources = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureSourcesArray),
                    RadiationSources = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationSourcesArray),
                    TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureBack),
                    RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(radiationBack),
                    UpdraftSignals = (ThermalUpdraftSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(updraftSignals),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(signalCounters),
                    GridOriginAup = _gridOriginAup,
                    CellSizeMeters = math.max(1f, cellSizeMeters),
                    Resolution = _activeResolution,
                    Frame = unchecked((uint)Time.frameCount),
                    Constants = constants,
                    ApplyRadiationDecay = applyDecay ? 1 : 0
                };
                handle = diffusionJob.Schedule(activeCellCount, 64, handle);

                EntityDamageSamplingJob damageJob = new EntityDamageSamplingJob
                {
                    TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureBack),
                    RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationBack),
                    EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafePtr(entityAups),
                    EntityIds = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(entityIds),
                    EntityDamageTimers = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(entityDamageTimers),
                    EntityDamageAccumulators = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(entityDamageAccumulators),
                    MockSignals = (MockDamageSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(mockDamageSignals),
                    CombatSignals = (ThermodynamicsCombatDamageSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(combatDamageSignals),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(signalCounters),
                    EntityCount = _entityCount,
                    Resolution = _activeResolution,
                    GridOriginAup = _gridOriginAup,
                    CellSizeMeters = math.max(1f, cellSizeMeters),
                    DeltaTime = dt,
                    Frame = unchecked((uint)Time.frameCount),
                    Constants = constants
                };
                handle = damageJob.Schedule(math.max(1, _entityCount), 16, handle);
            }

            ScanTelemetryJob scanJob = new ScanTelemetryJob
            {
                TemperatureBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(temperatureBack),
                RadiationBack = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(radiationBack),
                Telemetry = (ThermodynamicsHazardTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryScratch),
                Resolution = _activeResolution,
                CellCount = activeCellCount,
                GridOrigin = float3.zero,
                GridOriginHash = HashAupMillimeters(_gridOriginAup),
                Frame = unchecked((uint)Time.frameCount),
                GridVersion = unchecked((uint)_gridVersion),
                SourceCount = unchecked((uint)_sourceCount),
                ShiftSequence = _shiftSequence,
                LowTier = _activeResolution == LowResolution ? 1u : 0u,
                HealthPressureLowTier = _healthPressureLowTierFrames > 0 ? 1u : 0u
            };
            handle = scanJob.Schedule(handle);
            _simulationHandle = handle;
            _simulationJobActive = true;
            H8Memory.RegisterActiveJob(MemoryOwner, handle);
        }

        private void SwapFrontBack()
        {
            VaultBufferHandle<float> temp = _temperatureFront;
            _temperatureFront = _temperatureBack;
            _temperatureBack = temp;

            VaultBufferHandle<float> rad = _radiationFront;
            _radiationFront = _radiationBack;
            _radiationBack = rad;
        }

        private void PublishQueuedSignals()
        {
            NativeArray<int> signalCounters = ResolveArray(ref _signalCounters);
            NativeArray<ThermalUpdraftSignal> updraftSignals = ResolveArray(ref _updraftSignals);
            NativeArray<MockDamageSignal> mockDamageSignals = ResolveArray(ref _mockDamageSignals);
            NativeArray<ThermodynamicsCombatDamageSignal> combatDamageSignals = ResolveArray(ref _combatDamageSignals);
            int updraftCount = math.min(MaxSignalsPerFrame, math.max(0, signalCounters[0]));
            for (int i = 0; i < updraftCount; i++)
            {
                ThermalUpdraftSignal signal = updraftSignals[i];
                if (math.isfinite(signal.TemperatureCelsius))
                    SignalBus<ThermalUpdraftSignal>.Push(in signal);
            }

            int mockDamageCount = math.min(MaxSignalsPerFrame, math.max(0, signalCounters[1]));
            for (int i = 0; i < mockDamageCount; i++)
            {
                MockDamageSignal signal = mockDamageSignals[i];
                if (math.isfinite(signal.Damage) && signal.Damage > 0f)
                    SignalBus<MockDamageSignal>.Push(in signal);
            }

            int combatDamageCount = math.min(MaxSignalsPerFrame, math.max(0, signalCounters[2]));
            for (int i = 0; i < combatDamageCount; i++)
            {
                ThermodynamicsCombatDamageSignal staged = combatDamageSignals[i];
                if (math.isfinite(staged.Magnitude) && staged.Magnitude > 0f)
                {
                    CombatDamageSignal signal = new CombatDamageSignal
                    {
                        WorldPoint = staged.WorldPoint,
                        Direction = staged.Direction,
                        Magnitude = staged.Magnitude,
                        DamageType = staged.DamageType,
                        TargetHash = staged.TargetHash,
                        SourceHash = staged.SourceHash,
                        Frame = staged.Frame,
                        SourceId = staged.SourceId,
                        TargetId = staged.TargetId,
                        Channel = staged.Channel,
                        Flags = staged.Flags,
                        IntegrityDelta = staged.IntegrityDelta
                    };
                    SignalBus<CombatDamageSignal>.Push(in signal);
                }
            }
        }

        private void CommitTelemetryScratch()
        {
            if (!_telemetryRing.IsCreated || !_telemetryScratch.IsCreated)
                return;

            NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryRing = ResolveArray(ref _telemetryRing);
            NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryScratch = ResolveArray(ref _telemetryScratch);
            ThermodynamicsHazardTelemetryEntry entry = telemetryScratch[0];
            entry.DiffusionComputeTimeMs = _lastCompleteMs;
            telemetryRing[_telemetryWriteIndex % TelemetryCapacity] = entry;
            _telemetryWriteIndex++;
            if ((entry.Flags & TelemetryFlagNaN) != 0u)
                DumpBlackBox();
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

            NativeArray<float> uploadSlice = ResolveArray(ref _temperatureFront).GetSubArray(0, ActiveCellCount);
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

        private void TryReloadCsvOverrides()
        {
            if (!_csvBytes.IsCreated || !_constants.IsCreated)
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
                    constants.RadiationDecayCoefficient = math.pow(0.5f, math.rcp(math.max(1f, value)));
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
            WriteDump("Dump_THERMODYNAMICS.bin");
            WriteDump("Dump_THERMODYNAMICS.h8dump");
            WriteDump("Dump_SHINOBU_16.bin");
            WriteDump("Dump_SHINOBU_16.h8dump");
        }

        private void WriteDump(string fileName)
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                NativeArray<ThermodynamicsHazardTelemetryEntry> telemetryRing = ResolveArray(ref _telemetryRing);
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

        private static int IncrementCounter(int* counters, int counterIndex, int limit)
        {
            int index = System.Threading.Interlocked.Increment(ref counters[counterIndex]) - 1;
            return index < limit ? index : -1;
        }

        private static void AtomicAddFloat(float* address, float value)
        {
            if (!math.isfinite(value) || value == 0f)
                return;

            ref int target = ref UnsafeUtility.As<float, int>(ref UnsafeUtility.AsRef<float>(address));
            int oldBits;
            int newBits;
            do
            {
                oldBits = target;
                float oldValue = math.asfloat(oldBits);
                float newValue = math.isfinite(oldValue) ? oldValue + value : value;
                newBits = math.asint(newValue);
            }
            while (System.Threading.Interlocked.CompareExchange(ref target, newBits, oldBits) != oldBits);
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ResetCountersJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public int* Counters;
            public int CounterCount;

            public void Execute()
            {
                for (int i = 0; i < CounterCount; i++)
                    Counters[i] = 0;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearSourceGridJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction] public float* RadiationSources;

            public void Execute(int index)
            {
                TemperatureSources[index] = 0f;
                RadiationSources[index] = 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EmissionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public HazardSourceDTO* Sources;
            [NativeDisableUnsafePtrRestriction] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction] public float* RadiationSources;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int SourceCount;
            public uint HeatHash;
            public uint RadiationHash;
            public uint MixedHash;

            public void Execute(int sourceIndex)
            {
                if (sourceIndex >= SourceCount)
                    return;

                HazardSourceDTO source = Sources[sourceIndex];
                if (!math.all(math.isfinite(source.AUP)) || !math.isfinite(source.Intensity) || source.Intensity <= 0f)
                    return;

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
                                AtomicAddFloat(TemperatureSources + cellIndex, contribution);
                            if (source.HazardTypeHash == RadiationHash || source.HazardTypeHash == MixedHash)
                                AtomicAddFloat(RadiationSources + cellIndex, contribution);
                        }
                    }
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DiffusionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public float* TemperatureFront;
            [NativeDisableUnsafePtrRestriction] public float* RadiationFront;
            [NativeDisableUnsafePtrRestriction] public float* TemperatureSources;
            [NativeDisableUnsafePtrRestriction] public float* RadiationSources;
            [NativeDisableUnsafePtrRestriction] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction] public float* RadiationBack;
            [NativeDisableUnsafePtrRestriction] public ThermalUpdraftSignal* UpdraftSignals;
            [NativeDisableUnsafePtrRestriction] public int* Counters;
            public ThermodynamicsHazardConstants Constants;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int ApplyRadiationDecay;
            public uint Frame;

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

                if (nextHeat > UpdraftThresholdCelsius)
                {
                    int signalIndex = IncrementCounter(Counters, 0, MaxSignalsPerFrame);
                    if (signalIndex >= 0)
                    {
                        double3 aup = GridOriginAup + ((new double3(x, y, z) - new double3(Resolution * 0.5)) * CellSizeMeters);
                        UpdraftSignals[signalIndex] = new ThermalUpdraftSignal
                        {
                            Aup = aup,
                            TemperatureCelsius = nextHeat,
                            Intensity01 = math.saturate((nextHeat - UpdraftThresholdCelsius) * 0.01f),
                            CellIndex = unchecked((uint)index),
                            Frame = Frame,
                            Flags = 0
                        };
                    }
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EntityDamageSamplingJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction] public float* RadiationBack;
            [NativeDisableUnsafePtrRestriction] public double3* EntityAups;
            [NativeDisableUnsafePtrRestriction] public uint* EntityIds;
            [NativeDisableUnsafePtrRestriction] public float* EntityDamageTimers;
            [NativeDisableUnsafePtrRestriction] public float* EntityDamageAccumulators;
            [NativeDisableUnsafePtrRestriction] public MockDamageSignal* MockSignals;
            [NativeDisableUnsafePtrRestriction] public ThermodynamicsCombatDamageSignal* CombatSignals;
            [NativeDisableUnsafePtrRestriction] public int* Counters;
            public ThermodynamicsHazardConstants Constants;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public float DeltaTime;
            public int Resolution;
            public int EntityCount;
            public uint Frame;

            public void Execute(int index)
            {
                if (index >= EntityCount)
                    return;

                uint entityId = EntityIds[index];
                if (entityId == 0u)
                    return;

                float timer = math.max(0f, EntityDamageTimers[index] - DeltaTime);
                ThermodynamicsHazardSample sample = SampleTrilinear(
                    TemperatureBack,
                    RadiationBack,
                    Resolution,
                    CellSizeMeters,
                    GridOriginAup,
                    EntityAups[index],
                    Constants);
                float damage = math.max(0f, sample.HeatDamage * 0.05f) + math.max(0f, sample.RadiationDamage * 8f);
                EntityDamageAccumulators[index] += damage * DeltaTime;
                if (timer > 0f || EntityDamageAccumulators[index] <= 0f)
                {
                    EntityDamageTimers[index] = timer;
                    return;
                }

                uint lcg = entityId * 1664525u + Frame * 1013904223u;
                bool mockFire = sample.TemperatureCelsius > Constants.HeatDamageThresholdCelsius && ((lcg >> 30) & 1u) == 0u;
                float burstDamage = EntityDamageAccumulators[index];
                EntityDamageAccumulators[index] = 0f;
                EntityDamageTimers[index] = DamageIntervalSeconds;

                if (mockFire)
                {
                    int mockIndex = IncrementCounter(Counters, 1, MaxSignalsPerFrame);
                    if (mockIndex >= 0)
                    {
                        MockSignals[mockIndex] = new MockDamageSignal
                        {
                            Aup = EntityAups[index],
                            Normal = new float3(0f, 1f, 0f),
                            Damage = burstDamage,
                            EntityId = entityId,
                            Flags = 1
                        };
                    }
                }

                int combatIndex = IncrementCounter(Counters, 2, MaxSignalsPerFrame);
                if (combatIndex >= 0)
                {
                    CombatSignals[combatIndex] = new ThermodynamicsCombatDamageSignal
                    {
                        WorldPoint = (float3)(EntityAups[index] - GridOriginAup),
                        Direction = new float3(0f, 1f, 0f),
                        Magnitude = burstDamage,
                        DamageType = MixedHazardHash,
                        TargetHash = entityId,
                        SourceHash = MixedHazardHash,
                        Frame = Frame,
                        SourceId = 16,
                        TargetId = (ushort)math.min(entityId, ushort.MaxValue),
                        Channel = 3,
                        Flags = CombatDamageSignal.DirectRuntimeFlag,
                        IntegrityDelta = 0
                    };
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RebaseGridJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public float* TemperatureFront;
            [NativeDisableUnsafePtrRestriction] public float* RadiationFront;
            [NativeDisableUnsafePtrRestriction] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction] public float* RadiationBack;
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ScanTelemetryJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public float* TemperatureBack;
            [NativeDisableUnsafePtrRestriction] public float* RadiationBack;
            [NativeDisableUnsafePtrRestriction] public ThermodynamicsHazardTelemetryEntry* Telemetry;
            public float3 GridOrigin;
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
                }

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
