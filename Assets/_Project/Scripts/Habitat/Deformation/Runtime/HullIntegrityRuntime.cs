using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Habitat.Deformation.Contracts;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Habitat.Deformation
{
    /// <summary>
    /// Burst-backed structural integrity ledger and GPU hull-dent bridge for bases and submarines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed unsafe class HullIntegrityRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IColdTickable, IHabitatModuleDeformationReadModel
    {
        private const float DefaultWaterDensity = 1025f;
        private const float DefaultGravity = 9.80665f;
        private const float DefaultDamageToSipScale = 0.18f;
        private const float DefaultSubmarineSip = 120000f;
        private const int TelemetryNanFlag = 1;
        private const uint BreachSignalHash = 0x48384252u; // H8BR
        private const uint AcousticSignalHash = 0x48384143u; // H8AC
        private const uint DumpMagic = 0x48384E54u; // H8NT
        private const uint DumpVersion = 2u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_HULL_INTEGRITY.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_HULL_INTEGRITY.h8dump";
        private const float DentCapUpgradeHysteresisSeconds = 2.5f;
        private const int HealthStateNominal = 0;
        private const int HealthStateWarning = 1;
        private const int HealthStateCritical = 2;

        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.Habitat.HullIntegrity.Tick");
        private static readonly ProfilerMarker _lateMarker = new ProfilerMarker("H8.Habitat.HullIntegrity.LateFrame");
        private static readonly int _HullDentDTOBufferId = Shader.PropertyToID("_HectonHullDentDTOBuffer");
        private static readonly int _HullDentDTOParamsId = Shader.PropertyToID("_HectonHullDentDTOParams");

        private static HullIntegrityRuntime s_activeRuntime;

        [Header("Structural Roots")]
        [Tooltip("Local origin for base dent positions. Dents are stored in this space, never global AUP.")]
        [SerializeField] private Transform baseRoot;

        [Tooltip("Local origin for submarine crush dents. Falls back to base root when missing.")]
        [SerializeField] private Transform submarineRoot;

        [Header("Integrity Defaults")]
        [Tooltip("Fallback module count used when WFC integrity data is absent.")]
        [SerializeField, Range(1, HullIntegrityConstants.MaxMockModuleCapacity)] private int mockModuleCount = 128;

        [Tooltip("Default depth meters used by the blind mock depth job.")]
        [SerializeField, Range(0f, 12000f)] private float mockDepthMeters = 850f;

        [Tooltip("Deterministic triangle-wave depth jitter for temporal-blind pressure proof.")]
        [SerializeField, Range(0f, 2000f)] private float mockDepthJitterMeters = 80f;

        [Tooltip("Fallback SIP multiplier before CSV or tuner overrides land in the Vault.")]
        [SerializeField, Range(0.1f, 10f)] private float baseSipMultiplier = 1f;

        [Tooltip("Pressure gradient scalar applied after density * gravity * depth.")]
        [SerializeField, Range(0.000001f, 0.1f)] private float crushDepthGradient = 0.00008f;

        [Header("Dent Defaults")]
        [Tooltip("Fallback dent radius in local meters.")]
        [SerializeField, Range(0.05f, 8f)] private float dentRadius = 1.25f;

        [Tooltip("Fallback dent depth in local meters.")]
        [SerializeField, Range(0.001f, 2f)] private float dentDepth = 0.18f;

        [Tooltip("SIP damage scalar applied by impact jobs.")]
        [SerializeField, Range(0f, 8f)] private float damageToSipScale = DefaultDamageToSipScale;

        [Header("Submarine Crush")]
        [Tooltip("Whether the blind submarine crush proof can generate DTO dents.")]
        [SerializeField] private bool enableSubmarineMockCrush = true;

        [Tooltip("Fallback submarine SIP used before the real vehicle solver connects.")]
        [SerializeField, Range(1000f, 500000f)] private float submarineSip = DefaultSubmarineSip;

        [Tooltip("Local half extents used by the blind submarine AABB crush dent generator.")]
        [SerializeField] private Vector3 submarineHullExtents = new Vector3(3.4f, 2.2f, 8.5f);

        [Header("CSV Overrides")]
        [Tooltip("Project-relative integrity profile CSV. Format: key,sip")]
        [SerializeField] private string integrityProfileCsvPath = "integrity_profiles.csv";

        private IDataVault _dataVault;
        private VaultBufferHandle<HullDentDTO> _dentsHandle;
        private VaultBufferHandle<HullDentDTO> _dentUploadScratchHandle;
        private VaultBufferHandle<BaseModuleStateDTO> _modulesHandle;
        private VaultBufferHandle<BaseIntegrityLedgerDTO> _ledgerHandle;
        private VaultBufferHandle<HullIntegrityTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<MockDepthSignal> _mockDepthHandle;
        private VaultBufferHandle<int> _countersHandle;
        private VaultBufferHandle<HullIntegrityTuningDTO> _tuningHandle;
        private VaultBufferHandle<MockCombatDamageSignal> _damageSignalsHandle;

        private GraphicsBuffer _dentBufferA;
        private GraphicsBuffer _dentBufferB;
        private int _gpuReadIndex;
        private int _cachedDentCap = HullIntegrityConstants.LowTierDentCapacity;
        private int _cachedQualityTier;
        private int _cachedHealthState;
        private int _pendingQualityTier = -1;
        private int _pendingDentCap;
        private int _pendingHealthState;
        private float _pendingQualitySeconds;
        private byte _cachedProfileTier;
        private int _activeModuleCount;
        private int _registeredUpdate;
        private int _registeredLate;
        private int _registeredCold;
        private int _initialized;
        private int _mockGenerated;
        private int _forceGpuUpload;
        private uint _frame;
        private JobHandle _scheduledHandle;
        private bool _jobScheduled;
        private MockRepairLaserSignal _pendingRepair;
        private float _maxPressureExperienced;
        private float3 _lastDentPosition;
        private float _lastDentDepth;
        private long _lastCsvTicks;
        private int _lastUploadedDentCount = -1;
        private Vector4 _lastDentParams = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);

        // COLD ALLOC: byte[16384] - bounded CSV profile read buffer, used only by ColdTick - owner: HullIntegrityRuntime
        private readonly byte[] _csvBytes = new byte[16 * 1024];

        /// <summary>Active runtime instance for editor-only visualization.</summary>
        public static HullIntegrityRuntime ActiveRuntime => s_activeRuntime;

        /// <summary>Root transform used to convert local dent DTOs into Scene View positions.</summary>
        public Transform DentRoot => ResolveDentRoot();

        /// <inheritdoc />
        public int ModuleStressCount => _activeModuleCount;

        private void Awake()
        {
            CacheColdQualityTier();
            _cachedQualityTier = _cachedProfileTier;
            _cachedDentCap = ResolveDentCap(_cachedQualityTier);
            _pendingDentCap = _cachedDentCap;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _dataVault = GlobalRegistry.DataVault;
            TryInitialize();
            TryRegisterTickables();
            s_activeRuntime = this;
        }

        private void OnDisable()
        {
            if (_jobScheduled)
            {
                _scheduledHandle.Complete();
                _jobScheduled = false;
            }

            TryUnregisterTickables();
            if (s_activeRuntime == this)
                s_activeRuntime = null;

            ReleaseBuffer(ref _dentBufferA);
            ReleaseBuffer(ref _dentBufferB);
            _initialized = 0;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                if (_jobScheduled)
                    return;

                if (_initialized == 0)
                    return;

                _frame++;
                DrainQualitySignals(deltaTime);
                ResolveTuning(out HullIntegrityTuningDTO tuning);
                int damageCount = GatherDamageSignals(tuning);
                MockRepairLaserSignal repair = DrainRepairSignals();

                NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
                NativeArray<BaseIntegrityLedgerDTO> ledger = _ledgerHandle.Resolve(_dataVault);
                NativeArray<MockDepthSignal> depthSignal = _mockDepthHandle.Resolve(_dataVault);
                NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
                NativeArray<MockCombatDamageSignal> damageSignals = _damageSignalsHandle.Resolve(_dataVault);
                NativeArray<HullDentDTO> dents = _dentsHandle.Resolve(_dataVault);

                if (!modules.IsCreated || !ledger.IsCreated || !depthSignal.IsCreated || !counters.IsCreated || !damageSignals.IsCreated || !dents.IsCreated)
                    return;

                if (counters.Length > HullIntegrityConstants.CounterPendingDamageCount)
                    counters[HullIntegrityConstants.CounterPendingDamageCount] = damageCount;

                JobHandle handle = new HullIntegrityDamageJob
                {
                    Modules = modules,
                    DamageSignals = damageSignals,
                    Counters = counters,
                    ModuleCount = _activeModuleCount,
                    DamageCount = damageCount,
                    BaseHash = HullIntegrityConstants.DefaultBaseHash,
                    DamageToSipScale = damageToSipScale
                }.Schedule();

                handle = new HullIntegrityMockDepthJob
                {
                    DepthSignal = depthSignal,
                    BaseHash = HullIntegrityConstants.DefaultBaseHash,
                    Frame = _frame,
                    BaseDepthMeters = mockDepthMeters,
                    DepthJitterMeters = mockDepthJitterMeters
                }.Schedule(handle);

                handle = new HullIntegritySipAggregationJob
                {
                    Modules = modules,
                    Ledger = ledger,
                    Counters = counters,
                    ModuleCount = _activeModuleCount,
                    BaseHash = HullIntegrityConstants.DefaultBaseHash,
                    BaseSipMultiplier = tuning.BaseSipMultiplier
                }.Schedule(handle);

                handle = new HullIntegrityHydrostaticPressureJob
                {
                    Modules = modules,
                    Ledger = ledger,
                    DepthSignal = depthSignal,
                    Counters = counters,
                    ModuleCount = _activeModuleCount,
                    Frame = _frame,
                    BaseHash = HullIntegrityConstants.DefaultBaseHash,
                    WaterDensity = DefaultWaterDensity,
                    Gravity = DefaultGravity,
                    CrushDepthGradient = tuning.CrushDepthGradient
                }.Schedule(handle);

                handle = new HullIntegrityRepairDentJob
                {
                    Dents = dents,
                    Counters = counters,
                    Repair = repair,
                    Capacity = _cachedDentCap,
                    DeltaTime = deltaTime
                }.Schedule(handle);

                handle = new HullIntegritySubmarineCrushDentJob
                {
                    Dents = dents,
                    Ledger = ledger,
                    Counters = counters,
                    Capacity = _cachedDentCap,
                    Frame = _frame,
                    SubmarineSIP = submarineSip,
                    HullExtents = new float3(submarineHullExtents.x, submarineHullExtents.y, submarineHullExtents.z),
                    DentRadius = tuning.DentRadius,
                    DentDepth = tuning.DentDepth,
                    Enabled = enableSubmarineMockCrush ? 1 : 0
                }.Schedule(handle);

                _scheduledHandle = handle;
                _jobScheduled = true;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            using (_lateMarker.Auto())
            {
                if (!_jobScheduled)
                    return;

                _scheduledHandle.Complete();
                _jobScheduled = false;

                NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
                NativeArray<BaseIntegrityLedgerDTO> ledger = _ledgerHandle.Resolve(_dataVault);
                NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
                NativeArray<HullDentDTO> dents = _dentsHandle.Resolve(_dataVault);
                if (!modules.IsCreated || !ledger.IsCreated || !counters.IsCreated || !dents.IsCreated)
                    return;

                BaseIntegrityLedgerDTO currentLedger = ledger[0];
                float safePressure = math.isfinite(currentLedger.DepthPressure) ? math.max(0f, currentLedger.DepthPressure) : 0f;
                _maxPressureExperienced = math.isfinite(_maxPressureExperienced)
                    ? math.max(_maxPressureExperienced, safePressure)
                    : safePressure;
                PublishBreachAndPressureSignals(modules, currentLedger, counters);
                RecordTelemetry(modules, currentLedger, counters);

                if (counters.Length > HullIntegrityConstants.CounterDentDirty && counters[HullIntegrityConstants.CounterDentDirty] != 0)
                {
                    _forceGpuUpload = 1;
                    counters[HullIntegrityConstants.CounterDentDirty] = 0;
                }

                if (_forceGpuUpload != 0)
                    UploadDentsToGpu(dents, counters);
            }
        }

        /// <inheritdoc />
        public void ColdTick()
        {
            if (_initialized == 0)
            {
                TryInitialize();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CheckCsvOverrideCold();
#endif
        }

        /// <inheritdoc />
        public bool TryGetModuleStress(int stressIndex, out HabitatModuleDeformationSample sample)
        {
            sample = default;
            if ((uint)stressIndex >= (uint)_activeModuleCount || _dataVault == null)
                return false;

            NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
            if (!modules.IsCreated || stressIndex >= modules.Length)
                return false;

            BaseModuleStateDTO module = modules[stressIndex];
            sample = new HabitatModuleDeformationSample(
                module.NodeId,
                module.ModuleHash,
                module.LocalCenter,
                module.Stress01,
                module.PeakStress01,
                (byte)_cachedQualityTier);
            return true;
        }

        /// <summary>
        /// Injects a blind mock impact into the local dent and SIP paths.
        /// </summary>
        /// <param name="signal">Mock damage payload in local space.</param>
        /// <returns>True when the signal was written into vault buffers.</returns>
        public bool InjectMockDamage(in MockCombatDamageSignal signal)
        {
            if (_initialized == 0 && !TryInitialize())
                return false;

            return AppendDentAndDamage(signal);
        }

        /// <summary>
        /// Queues a blind mock repair laser payload for the next Burst repair job.
        /// </summary>
        /// <param name="repair">Repair payload in local space.</param>
        public void QueueMockRepair(in MockRepairLaserSignal repair)
        {
            _pendingRepair = repair;
        }

        /// <summary>
        /// Reads a dent DTO by raw ring slot for editor visualization.
        /// </summary>
        /// <param name="index">Raw dent slot.</param>
        /// <param name="dent">Dent payload.</param>
        /// <returns>True when a live dent exists at the slot.</returns>
        public bool TryGetDent(int index, out HullDentDTO dent)
        {
            dent = default;
            if (_dataVault == null || (uint)index >= HullIntegrityConstants.MaxDentCapacity)
                return false;

            NativeArray<HullDentDTO> dents = _dentsHandle.Resolve(_dataVault);
            if (!dents.IsCreated || index >= dents.Length)
                return false;

            dent = dents[index];
            return dent.Depth > 0f && dent.Radius > 0f;
        }

        /// <summary>
        /// Writes tuner values into the unmanaged vault block.
        /// </summary>
        /// <param name="tuning">New tuning values.</param>
        public void SetTuning(in HullIntegrityTuningDTO tuning)
        {
            if (_initialized == 0 && !TryInitialize())
                return;

            NativeArray<HullIntegrityTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            tuningBuffer[0] = SanitizeTuning(tuning);
        }

        /// <summary>
        /// Reads current tuner values from unmanaged vault memory.
        /// </summary>
        /// <param name="tuning">Current tuning values.</param>
        /// <returns>True when the vault tuning block exists.</returns>
        public bool TryGetTuning(out HullIntegrityTuningDTO tuning)
        {
            tuning = default;
            if (_dataVault == null)
                return false;

            NativeArray<HullIntegrityTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return false;

            tuning = tuningBuffer[0];
            return true;
        }

        private bool TryInitialize()
        {
            _dataVault = _dataVault ?? GlobalRegistry.DataVault;
            if (_dataVault == null)
                return false;

            if (!ValidateLayouts())
                return false;

            CacheColdQualityTier();
            _cachedQualityTier = _cachedProfileTier;
            _cachedDentCap = ResolveDentCap(_cachedQualityTier);
            _cachedHealthState = HealthStateNominal;
            ResetPendingDentQuality();

            _dentsHandle = _dataVault.GetBufferHandle<HullDentDTO>(
                BufferID.HullIntegrityDents,
                HullIntegrityConstants.MaxDentCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _dentUploadScratchHandle = _dataVault.GetBufferHandle<HullDentDTO>(
                BufferID.HullIntegrityDentUploadScratch,
                HullIntegrityConstants.MaxDentCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _modulesHandle = _dataVault.GetBufferHandle<BaseModuleStateDTO>(
                BufferID.HullIntegrityBaseModules,
                HullIntegrityConstants.MaxMockModuleCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _ledgerHandle = _dataVault.GetBufferHandle<BaseIntegrityLedgerDTO>(
                BufferID.HullIntegrityLedger,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _dataVault.GetBufferHandle<HullIntegrityTelemetryEntry>(
                BufferID.HullIntegrityTelemetryRing,
                HullIntegrityConstants.TelemetryFrameCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _dataVault.GetBufferHandle<int>(
                BufferID.HullIntegrityTelemetryCursor,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _mockDepthHandle = _dataVault.GetBufferHandle<MockDepthSignal>(
                BufferID.HullIntegrityMockDepth,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _countersHandle = _dataVault.GetBufferHandle<int>(
                BufferID.HullIntegrityCounters,
                HullIntegrityConstants.CounterCount,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _dataVault.GetBufferHandle<HullIntegrityTuningDTO>(
                BufferID.HullIntegrityTuning,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _damageSignalsHandle = _dataVault.GetBufferHandle<MockCombatDamageSignal>(
                BufferID.HullIntegrityDamageSignals,
                HullIntegrityConstants.MaxDamageSignals,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);

            if (!_dentsHandle.IsCreated ||
                !_dentUploadScratchHandle.IsCreated ||
                !_modulesHandle.IsCreated ||
                !_ledgerHandle.IsCreated ||
                !_telemetryHandle.IsCreated ||
                !_telemetryCursorHandle.IsCreated ||
                !_mockDepthHandle.IsCreated ||
                !_countersHandle.IsCreated ||
                !_tuningHandle.IsCreated ||
                !_damageSignalsHandle.IsCreated)
            {
                return false;
            }

            EnsureGpuBuffers();
            ClearBootBuffers();
            WriteDefaultTuning();
            GenerateEmergencyMockIntegrity();
            BuildEmergencyScratchProof();
            BindInitialShaderState();
            _initialized = 1;
            _forceGpuUpload = 1;
            return true;
        }

        private void TryRegisterTickables()
        {
            if (_registeredUpdate == 0 && GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (_registeredLate == 0 && GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLate = 1;
            if (_registeredCold == 0 && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredCold = 1;
        }

        private void TryUnregisterTickables()
        {
            if (_registeredUpdate != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = 0;
            }

            if (_registeredLate != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = 0;
            }

            if (_registeredCold != 0)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredCold = 0;
            }
        }

        private bool AppendDentAndDamage(in MockCombatDamageSignal signal)
        {
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            NativeArray<MockCombatDamageSignal> damageSignals = _damageSignalsHandle.Resolve(_dataVault);
            if (!counters.IsCreated || !damageSignals.IsCreated || !AppendDentOnly(signal, true))
                return false;

            int damageCount = math.clamp(counters[HullIntegrityConstants.CounterPendingDamageCount], 0, damageSignals.Length);
            if (damageCount < damageSignals.Length)
            {
                damageSignals[damageCount] = signal;
                counters[HullIntegrityConstants.CounterPendingDamageCount] = damageCount + 1;
            }

            return true;
        }

        private bool AppendDentOnly(in MockCombatDamageSignal signal, bool publishSignal)
        {
            NativeArray<HullDentDTO> dents = _dentsHandle.Resolve(_dataVault);
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            if (!dents.IsCreated || !counters.IsCreated)
                return false;

            int capacity = _cachedDentCap;
            if (capacity <= 0)
                return false;

            int slot = counters[HullIntegrityConstants.CounterWriteCursor] & (capacity - 1);
            float fallbackRadius = math.isfinite(dentRadius) ? dentRadius : 1.25f;
            float fallbackDepth = math.isfinite(dentDepth) ? dentDepth : 0.18f;
            float radius = math.max(0.05f, math.isfinite(signal.Radius) && signal.Radius > 0f ? signal.Radius : fallbackRadius);
            float depth = math.max(0.001f, math.isfinite(signal.Depth) && signal.Depth > 0f ? signal.Depth : fallbackDepth);
            float3 normal = math.normalizesafe(signal.LocalNormal, new float3(0f, 1f, 0f));
            float3 point = math.all(math.isfinite(signal.LocalPoint)) ? signal.LocalPoint : float3.zero;

            dents[slot] = new HullDentDTO
            {
                Position = point,
                Radius = radius,
                Normal = normal,
                Depth = depth
            };

            counters[HullIntegrityConstants.CounterWriteCursor] = (slot + 1) & (capacity - 1);
            counters[HullIntegrityConstants.CounterActiveDentCount] = math.min(capacity, counters[HullIntegrityConstants.CounterActiveDentCount] + 1);
            counters[HullIntegrityConstants.CounterDentDirty] = 1;
            _forceGpuUpload = 1;
            _lastDentPosition = point;
            _lastDentDepth = depth;

            if (publishSignal)
                PublishHullDeformedSignal(point, radius, depth, signal);
            return true;
        }

        private int GatherDamageSignals(in HullIntegrityTuningDTO tuning)
        {
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            NativeArray<MockCombatDamageSignal> damageSignals = _damageSignalsHandle.Resolve(_dataVault);
            if (!counters.IsCreated || !damageSignals.IsCreated)
                return 0;

            int count = 0;
            ReadOnlySpan<CombatDamageSignal> combatSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            Matrix4x4 baseWorldToLocal = ResolveWorldToLocal(ResolveDentRoot());
            counters[HullIntegrityConstants.CounterPendingDamageCount] = 0;

            for (int i = 0; i < combatSignals.Length && count < damageSignals.Length; i++)
            {
                CombatDamageSignal signal = combatSignals[i];
                float3 finiteWorldPoint = math.all(math.isfinite(signal.WorldPoint)) ? signal.WorldPoint : float3.zero;
                Vector3 worldPoint = new Vector3(finiteWorldPoint.x, finiteWorldPoint.y, finiteWorldPoint.z);
                Vector3 local = baseWorldToLocal.MultiplyPoint3x4(worldPoint);
                float magnitude = math.isfinite(signal.Magnitude) ? math.max(0f, signal.Magnitude) : 0f;
                float3 direction = math.all(math.isfinite(signal.Direction)) ? signal.Direction : new float3(0f, -1f, 0f);
                MockCombatDamageSignal mock = new MockCombatDamageSignal
                {
                    LocalPoint = new float3(local.x, local.y, local.z),
                    LocalNormal = math.normalizesafe(-direction, new float3(0f, 1f, 0f)),
                    Magnitude = magnitude,
                    Radius = math.max(0.05f, tuning.DentRadius),
                    TargetHash = signal.TargetHash == 0u ? HullIntegrityConstants.DefaultBaseHash : signal.TargetHash,
                    SourceHash = signal.SourceHash,
                    Frame = _frame,
                    DamageType = signal.DamageType,
                    Depth = math.max(0.001f, tuning.DentDepth)
                };

                damageSignals[count] = mock;
                count++;
                AppendDentOnly(mock, true);
            }

            counters[HullIntegrityConstants.CounterPendingDamageCount] = count;
            return count;
        }

        private MockRepairLaserSignal DrainRepairSignals()
        {
            MockRepairLaserSignal repair = _pendingRepair;
            _pendingRepair = default;
            if ((repair.Flags & 1u) != 0u)
                return repair;

            ReadOnlySpan<HullRepairedSignal> repairedSignals = SignalBus<HullRepairedSignal>.GetFrameSnapshot();
            if (repairedSignals.Length <= 0)
                return default;

            HullRepairedSignal signal = repairedSignals[repairedSignals.Length - 1];
            float radius = math.isfinite(dentRadius) ? math.max(0.25f, dentRadius * 1.5f) : 1.875f;
            float repairDepth = math.isfinite(dentDepth) ? math.max(0.01f, dentDepth * 4f) : 0.72f;
            return new MockRepairLaserSignal
            {
                LocalPoint = new float3(
                    math.isfinite(signal.HitAup.LocalX) ? signal.HitAup.LocalX : 0f,
                    math.isfinite(signal.HitAup.LocalY) ? signal.HitAup.LocalY : 0f,
                    math.isfinite(signal.HitAup.LocalZ) ? signal.HitAup.LocalZ : 0f),
                Radius = radius,
                TargetHash = HullIntegrityConstants.DefaultBaseHash,
                DepthPerSecond = repairDepth,
                Frame = _frame,
                Flags = 1u
            };
        }

        private void PublishBreachAndPressureSignals(
            NativeArray<BaseModuleStateDTO> modules,
            BaseIntegrityLedgerDTO ledger,
            NativeArray<int> counters)
        {
            float totalSip = math.isfinite(ledger.TotalSIP) ? math.max(ledger.TotalSIP, 0.0001f) : 0.0001f;
            float depthPressure = math.isfinite(ledger.DepthPressure) ? math.max(0f, ledger.DepthPressure) : 0f;
            float ratio = depthPressure / totalSip;

            if (ratio >= 0.8f && ratio <= 0.99f)
            {
                float imminent = math.saturate((ratio - 0.8f) / 0.19f);
                AcousticPingSignal acoustic = new AcousticPingSignal
                {
                    PositionAup = BuildAupFromLocal(float3.zero),
                    RadiusMeters = 32f,
                    Intensity01 = imminent,
                    SourceId = AcousticSignalHash,
                    Channel = AcousticPingSignal.ChannelMetalStress,
                    Flags = AcousticPingSignal.FlagActiveSonar
                };
                SignalBus<AcousticPingSignal>.Push(in acoustic);
            }

            if (counters.Length <= HullIntegrityConstants.CounterBreachPending ||
                counters[HullIntegrityConstants.CounterBreachPending] == 0)
            {
                return;
            }

            int moduleIndex = counters[HullIntegrityConstants.CounterBreachedModuleIndex];
            if ((uint)moduleIndex >= (uint)modules.Length)
                return;

            BaseModuleStateDTO module = modules[moduleIndex];
            AbsoluteUniversePosition leakAup = BuildAupFromLocal(module.LocalCenter);
            FluidIncursionSignal flood = new FluidIncursionSignal
            {
                LeakAup = leakAup,
                CompartmentId = module.NodeId,
                FloodLevel01 = 1f,
                FlowRate01 = math.saturate(ratio),
                Flags = 1
            };
            BaseModuleCompromisedSignal compromised = new BaseModuleCompromisedSignal
            {
                ModuleCenter = module.LocalCenter,
                Stress01 = module.Stress01,
                PeakStress01 = module.PeakStress01,
                DepthMeters = module.DepthMeters,
                NodeId = module.NodeId,
                ModuleHash = module.ModuleHash,
                Frame = _frame,
                Sequence = (uint)counters[HullIntegrityConstants.CounterBreachedCount],
                SourceId = (ushort)(BreachSignalHash & 0xFFFFu),
                Flags = BaseModuleCompromisedSignal.MaxDeformationFlag,
                StressIndex = (byte)math.min(255, moduleIndex),
                QualityTier = (byte)_cachedQualityTier
            };

            SignalBus<FluidIncursionSignal>.Push(in flood);
            SignalBus<BaseModuleCompromisedSignal>.Push(in compromised);
            counters[HullIntegrityConstants.CounterBreachPending] = 0;
        }

        private void RecordTelemetry(
            NativeArray<BaseModuleStateDTO> modules,
            BaseIntegrityLedgerDTO ledger,
            NativeArray<int> counters)
        {
            NativeArray<HullIntegrityTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursorArray = _telemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || !cursorArray.IsCreated || cursorArray.Length == 0)
                return;

            int cursor = cursorArray[0];
            int slot = math.abs(cursor) % HullIntegrityConstants.TelemetryFrameCapacity;
            float safeTotalSip = math.isfinite(ledger.TotalSIP) ? math.max(0f, ledger.TotalSIP) : 0f;
            float safeDepthPressure = math.isfinite(ledger.DepthPressure) ? math.max(0f, ledger.DepthPressure) : 0f;
            float pressureRatio = safeDepthPressure / math.max(safeTotalSip, 0.0001f);
            bool finite = math.isfinite(ledger.TotalSIP) &&
                math.isfinite(ledger.DepthPressure) &&
                math.all(math.isfinite(_lastDentPosition)) &&
                math.isfinite(_lastDentDepth);
            uint flags = finite ? 0u : 1u;
            int activeDents = counters.Length > HullIntegrityConstants.CounterActiveDentCount
                ? counters[HullIntegrityConstants.CounterActiveDentCount]
                : 0;
            int weakestIndex = counters.Length > HullIntegrityConstants.CounterWeakestModuleIndex
                ? counters[HullIntegrityConstants.CounterWeakestModuleIndex]
                : -1;
            uint weakestNode = (uint)((uint)weakestIndex < (uint)modules.Length ? modules[weakestIndex].NodeId : 0u);

            telemetry[slot] = new HullIntegrityTelemetryEntry
            {
                Frame = _frame,
                BaseHash = ledger.BaseHash,
                AverageBaseSIP = _activeModuleCount > 0 ? safeTotalSip / _activeModuleCount : 0f,
                ActiveDentCount = activeDents,
                MaxPressureExperienced = _maxPressureExperienced,
                TotalSIP = safeTotalSip,
                DepthPressure = safeDepthPressure,
                PressureRatio = pressureRatio,
                LastDentLocalPosition = math.all(math.isfinite(_lastDentPosition)) ? _lastDentPosition : float3.zero,
                Flags = flags,
                WeakestNodeId = weakestNode,
                LastDentDepth = math.isfinite(_lastDentDepth) ? math.max(0f, _lastDentDepth) : 0f,
                DentCount = (uint)math.max(0, activeDents),
                StateHash = math.hash(new uint4(_frame, ledger.BaseHash, (uint)activeDents, (uint)counters[HullIntegrityConstants.CounterBreachedCount]))
            };

            cursorArray[0] = (cursor + 1) % HullIntegrityConstants.TelemetryFrameCapacity;
            if (!finite)
            {
                DumpTelemetry();
                if (counters.IsCreated && counters.Length > HullIntegrityConstants.CounterFaultFlags)
                    counters[HullIntegrityConstants.CounterFaultFlags] |= TelemetryNanFlag;
            }
        }

        private void UploadDentsToGpu(NativeArray<HullDentDTO> dents, NativeArray<int> counters)
        {
            EnsureGpuBuffers();
            if (_dentBufferA == null || _dentBufferB == null || !dents.IsCreated || !counters.IsCreated)
                return;

            int activeCount = counters.Length > HullIntegrityConstants.CounterActiveDentCount
                ? math.clamp(counters[HullIntegrityConstants.CounterActiveDentCount], 0, _cachedDentCap)
                : 0;
            int uploadCount = math.max(1, activeCount);
            int writeIndex = 1 - _gpuReadIndex;
            GraphicsBuffer writeBuffer = writeIndex == 0 ? _dentBufferA : _dentBufferB;
            NativeArray<HullDentDTO> mapped = writeBuffer.LockBufferForWrite<HullDentDTO>(0, uploadCount);

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dents);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long bytes = (long)UnsafeUtility.SizeOf<HullDentDTO>() * uploadCount;
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, bytes);
            writeBuffer.UnlockBufferAfterWrite<HullDentDTO>(uploadCount);

            _gpuReadIndex = writeIndex;
            GraphicsBuffer readBuffer = _gpuReadIndex == 0 ? _dentBufferA : _dentBufferB;
            float maxDepth = ResolveMaxDentDepth(dents, _cachedDentCap);
            Vector4 dtoParams = new Vector4(activeCount, activeCount > 0 ? 1f : 0f, maxDepth, _cachedQualityTier);

            if (_lastUploadedDentCount != activeCount || _lastDentParams != dtoParams)
            {
                Shader.SetGlobalBuffer(_HullDentDTOBufferId, readBuffer);
                Shader.SetGlobalVector(_HullDentDTOParamsId, dtoParams);
                _lastUploadedDentCount = activeCount;
                _lastDentParams = dtoParams;
            }

            _forceGpuUpload = 0;
        }

        private void GenerateEmergencyMockIntegrity()
        {
            if (_mockGenerated != 0)
                return;

            NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
            NativeArray<BaseIntegrityLedgerDTO> ledger = _ledgerHandle.Resolve(_dataVault);
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            if (!modules.IsCreated || !ledger.IsCreated || !counters.IsCreated)
                return;

            _activeModuleCount = math.clamp(mockModuleCount, 1, math.min(HullIntegrityConstants.MaxMockModuleCapacity, modules.Length));
            HullIntegrityEmergencyMockJob job = new HullIntegrityEmergencyMockJob
            {
                Modules = modules,
                Ledger = ledger,
                Counters = counters,
                ModuleCount = _activeModuleCount,
                BaseHash = HullIntegrityConstants.DefaultBaseHash,
                SipMultiplier = baseSipMultiplier
            };
            // COLD SYNC JOB: boot-only emergency mock SIP generation; runtime jobs stay deferred to LateFrameTick.
            job.Schedule().Complete();
            _mockGenerated = 1;
        }

        private void ClearBootBuffers()
        {
            JobHandle handle = default;
            handle = ScheduleMemClear(_dentsHandle, handle);
            handle = ScheduleMemClear(_dentUploadScratchHandle, handle);
            handle = ScheduleMemClear(_modulesHandle, handle);
            handle = ScheduleMemClear(_ledgerHandle, handle);
            handle = ScheduleMemClear(_telemetryHandle, handle);
            handle = ScheduleMemClear(_telemetryCursorHandle, handle);
            handle = ScheduleMemClear(_mockDepthHandle, handle);
            handle = ScheduleMemClear(_countersHandle, handle);
            handle = ScheduleMemClear(_tuningHandle, handle);
            handle = ScheduleMemClear(_damageSignalsHandle, handle);
            // COLD SYNC JOB: boot-only MemClear for uninitialized vault buffers before gameplay reads them.
            handle.Complete();
        }

        private JobHandle ScheduleMemClear<T>(VaultBufferHandle<T> handle, JobHandle dependency) where T : struct
        {
            if (!handle.IsCreated || handle.ptr == null)
                return dependency;

            return new HullIntegrityMemClearJob
            {
                Ptr = handle.ptr,
                Bytes = (long)handle.Length * UnsafeUtility.SizeOf<T>()
            }.Schedule(dependency);
        }

        private void BuildEmergencyScratchProof()
        {
            if (!HectonArenaAllocator.IsCreated)
                HectonArenaAllocator.Initialize();

            if (!HectonArenaAllocator.TryAllocateNativeArray(
                    math.max(1, _activeModuleCount),
                    NativeArrayOptions.UninitializedMemory,
                    HullIntegrityConstants.AgentHash,
                    out NativeArray<int> scratch))
            {
                return;
            }

            new HullIntegrityArenaBfsProofJob
            {
                Queue = scratch,
                NodeCount = _activeModuleCount
            }.Schedule().Complete();
        }

        private void WriteDefaultTuning()
        {
            NativeArray<HullIntegrityTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
            if (!tuning.IsCreated || tuning.Length == 0)
                return;

            tuning[0] = SanitizeTuning(new HullIntegrityTuningDTO
            {
                BaseSipMultiplier = baseSipMultiplier,
                CrushDepthGradient = crushDepthGradient,
                DentRadius = dentRadius,
                DentDepth = dentDepth
            });
        }

        private void ResolveTuning(out HullIntegrityTuningDTO tuning)
        {
            if (!TryGetTuning(out tuning))
            {
                tuning = SanitizeTuning(new HullIntegrityTuningDTO
                {
                    BaseSipMultiplier = baseSipMultiplier,
                    CrushDepthGradient = crushDepthGradient,
                    DentRadius = dentRadius,
                    DentDepth = dentDepth
                });
            }
        }

        private static HullIntegrityTuningDTO SanitizeTuning(in HullIntegrityTuningDTO source)
        {
            return new HullIntegrityTuningDTO
            {
                BaseSipMultiplier = math.isfinite(source.BaseSipMultiplier) ? math.max(0.01f, source.BaseSipMultiplier) : 1f,
                CrushDepthGradient = math.isfinite(source.CrushDepthGradient) ? math.max(0.000001f, source.CrushDepthGradient) : 0.00008f,
                DentRadius = math.isfinite(source.DentRadius) ? math.max(0.05f, source.DentRadius) : 1.25f,
                DentDepth = math.isfinite(source.DentDepth) ? math.max(0.001f, source.DentDepth) : 0.18f
            };
        }

        private void DrainQualitySignals(float deltaTime)
        {
            DrainScalabilityTierSignals();

            int healthState = ResolveHealthState();
            int desiredTier = _cachedProfileTier;
            int desiredCap = ResolveDentCap(desiredTier);
            if (healthState == HealthStateCritical)
            {
                desiredTier = ScalabilityTierProfiles.LowMx350;
                desiredCap = HullIntegrityConstants.LowTierDentCapacity;
            }
            else if (desiredTier == ScalabilityTierProfiles.LowMx350)
            {
                desiredCap = HullIntegrityConstants.LowTierDentCapacity;
            }
            else if (healthState == HealthStateWarning)
            {
                desiredCap = HullIntegrityConstants.MediumTierDentCapacity;
            }

            ApplyDentQualityWithHysteresis(desiredTier, desiredCap, healthState, deltaTime);
        }

        private void DrainScalabilityTierSignals()
        {
            ReadOnlySpan<ScalabilityChangedEvent> tierSignals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            if (tierSignals.Length == 0)
                return;

            _cachedProfileTier = ScalabilityTierProfiles.Normalize(tierSignals[tierSignals.Length - 1].CurrentTier);
        }

        private static int ResolveHealthState()
        {
            ReadOnlySpan<SystemHealthIndexSignal> health = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (health.Length == 0)
                return HealthStateNominal;

            byte state = health[health.Length - 1].State;
            if (state == SystemHealthIndexSignal.StateCritical)
                return HealthStateCritical;

            return state == SystemHealthIndexSignal.StateWarning ? HealthStateWarning : HealthStateNominal;
        }

        private void ApplyDentQualityWithHysteresis(int desiredTier, int desiredCap, int healthState, float deltaTime)
        {
            if (desiredTier == _cachedQualityTier && desiredCap == _cachedDentCap && healthState == _cachedHealthState)
            {
                ResetPendingDentQuality();
                return;
            }

            bool immediateDowngrade = desiredCap < _cachedDentCap || healthState > _cachedHealthState;
            if (immediateDowngrade)
            {
                ApplyDentQuality(desiredTier, desiredCap, healthState);
                return;
            }

            if (_pendingQualityTier != desiredTier || _pendingDentCap != desiredCap || _pendingHealthState != healthState)
            {
                _pendingQualityTier = desiredTier;
                _pendingDentCap = desiredCap;
                _pendingHealthState = healthState;
                _pendingQualitySeconds = 0f;
                return;
            }

            _pendingQualitySeconds += math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_pendingQualitySeconds >= DentCapUpgradeHysteresisSeconds)
                ApplyDentQuality(desiredTier, desiredCap, healthState);
        }

        private void ApplyDentQuality(int qualityTier, int dentCap, int healthState)
        {
            _cachedQualityTier = qualityTier;
            _cachedDentCap = dentCap;
            _cachedHealthState = healthState;
            _forceGpuUpload = 1;
            ResetPendingDentQuality();
        }

        private void ResetPendingDentQuality()
        {
            _pendingQualityTier = -1;
            _pendingDentCap = _cachedDentCap;
            _pendingHealthState = _cachedHealthState;
            _pendingQualitySeconds = 0f;
        }

        private void CacheColdQualityTier()
        {
            _cachedProfileTier = ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
        }

        private static int ResolveDentCap(int qualityTier)
        {
            if (qualityTier <= ScalabilityTierProfiles.LowMx350)
                return HullIntegrityConstants.LowTierDentCapacity;
            return HullIntegrityConstants.UltraTierDentCapacity;
        }

        private Transform ResolveDentRoot()
        {
            if (baseRoot != null)
                return baseRoot;

            return submarineRoot != null ? submarineRoot : transform;
        }

        private static Matrix4x4 ResolveWorldToLocal(Transform root)
        {
            return root != null ? root.worldToLocalMatrix : Matrix4x4.identity;
        }

        private AbsoluteUniversePosition BuildAupFromLocal(float3 local)
        {
            Transform root = ResolveDentRoot();
            Vector3 localPoint = new Vector3(local.x, local.y, local.z);
            Vector3 runtime = root != null ? root.localToWorldMatrix.MultiplyPoint3x4(localPoint) : localPoint;
            return AbsoluteUniversePosition.FromRuntimePosition(runtime);
        }

        private void PublishHullDeformedSignal(
            float3 point,
            float radius,
            float depth,
            in MockCombatDamageSignal source)
        {
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            int activeDents = counters.IsCreated && counters.Length > HullIntegrityConstants.CounterActiveDentCount
                ? counters[HullIntegrityConstants.CounterActiveDentCount]
                : 0;
            HullDeformedSignal signal = new HullDeformedSignal
            {
                LocalPoint = point,
                Radius = radius,
                Depth = depth,
                Intensity01 = math.saturate(depth / math.max(radius, 0.0001f)),
                TargetHash = source.TargetHash,
                SourceHash = source.SourceHash,
                Frame = _frame,
                TargetId = 0,
                SourceId = (ushort)(source.SourceHash & 0xFFFFu),
                ActiveDentCount = (byte)math.min(255, activeDents),
                Flags = _cachedQualityTier == 0 ? HullDeformedSignal.LowTierVisualOnlyFlag : (byte)0,
                QualityTier = (byte)_cachedQualityTier,
                Channel = 0,
                DamageType = source.DamageType
            };
            SignalBus<HullDeformedSignal>.Push(in signal);
        }

        private void EnsureGpuBuffers()
        {
            if (_dentBufferA != null && _dentBufferA.IsValid() && _dentBufferB != null && _dentBufferB.IsValid())
                return;

            ReleaseBuffer(ref _dentBufferA);
            ReleaseBuffer(ref _dentBufferB);
            int stride = UnsafeUtility.SizeOf<HullDentDTO>();
            _dentBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxDentCapacity,
                stride); // COLD ALLOC: GraphicsBuffer[512] - hull dent DTO double buffer A - owner: HullIntegrityRuntime
            _dentBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxDentCapacity,
                stride); // COLD ALLOC: GraphicsBuffer[512] - hull dent DTO double buffer B - owner: HullIntegrityRuntime
        }

        private void BindInitialShaderState()
        {
            EnsureGpuBuffers();
            GraphicsBuffer buffer = _dentBufferA != null ? _dentBufferA : _dentBufferB;
            if (buffer == null)
                return;

            Shader.SetGlobalBuffer(_HullDentDTOBufferId, buffer);
            Shader.SetGlobalVector(_HullDentDTOParamsId, Vector4.zero);
        }

        private void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<HullDentDTO>() == 32 &&
                UnsafeUtility.SizeOf<BaseIntegrityLedgerDTO>() == 16 &&
                UnsafeUtility.SizeOf<BaseModuleStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<MockWFCBaseArray>() == 16 &&
                UnsafeUtility.SizeOf<MockCombatDamageSignal>() == 64 &&
                UnsafeUtility.SizeOf<MockDepthSignal>() == 16 &&
                UnsafeUtility.SizeOf<MockRepairLaserSignal>() == 32 &&
                UnsafeUtility.SizeOf<MockHullBreachSignal>() == 32 &&
                UnsafeUtility.SizeOf<HullIntegrityTuningDTO>() == 16 &&
                UnsafeUtility.SizeOf<HabitatModuleDeformationSample>() == 32 &&
                UnsafeUtility.SizeOf<HullIntegrityTelemetryEntry>() == 64;
        }

        private static float ResolveMaxDentDepth(NativeArray<HullDentDTO> dents, int capacity)
        {
            float maxDepth = 0f;
            int count = math.clamp(capacity, 0, dents.IsCreated ? dents.Length : 0);
            for (int i = 0; i < count; i++)
            {
                float depth = math.isfinite(dents[i].Depth) ? math.max(0f, dents[i].Depth) : 0f;
                maxDepth = math.max(maxDepth, depth);
            }

            return math.saturate(maxDepth);
        }

        private void DumpTelemetry()
        {
            NativeArray<HullIntegrityTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            WriteTelemetryDump(DumpRelativePath, telemetry);
            WriteTelemetryDump(DumpH8RelativePath, telemetry);
        }

        private void WriteTelemetryDump(string relativePath, NativeArray<HullIntegrityTelemetryEntry> telemetry)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, relativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                WriteUInt32(stream, DumpMagic);
                WriteUInt32(stream, DumpVersion);
                WriteUInt32(stream, (uint)telemetry.Length);
                WriteUInt32(stream, (uint)UnsafeUtility.SizeOf<HullIntegrityTelemetryEntry>());

                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int remaining = telemetry.Length * UnsafeUtility.SizeOf<HullIntegrityTelemetryEntry>();
                int offset = 0;
                fixed (byte* chunk = _csvBytes)
                {
                    while (remaining > 0)
                    {
                        int bytes = math.min(_csvBytes.Length, remaining);
                        UnsafeUtility.MemCpy(chunk, source + offset, bytes);
                        stream.Write(_csvBytes, 0, bytes);
                        offset += bytes;
                        remaining -= bytes;
                    }
                }
            }
        }

        private void WriteUInt32(FileStream stream, uint value)
        {
            _csvBytes[0] = (byte)value;
            _csvBytes[1] = (byte)(value >> 8);
            _csvBytes[2] = (byte)(value >> 16);
            _csvBytes[3] = (byte)(value >> 24);
            stream.Write(_csvBytes, 0, 4);
        }

        private void CheckCsvOverrideCold()
        {
            if (string.IsNullOrEmpty(integrityProfileCsvPath))
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, integrityProfileCsvPath);
            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _lastCsvTicks)
                return;

            _lastCsvTicks = ticks;
            int bytesRead;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(_csvBytes, 0, _csvBytes.Length);
            }

            ParseCsvProfiles(_csvBytes, bytesRead);
        }

        private void ParseCsvProfiles(byte[] bytes, int length)
        {
            NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int index = 0;
            while (index < length)
            {
                uint keyHash = 2166136261u;
                while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                {
                    byte value = bytes[index++];
                    if (value >= (byte)'A' && value <= (byte)'Z')
                        value = (byte)(value + 32);
                    keyHash = (keyHash ^ value) * 16777619u;
                }

                if (index >= length || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                index++;
                if (!TryParsePositiveFloat(bytes, length, ref index, out float sip))
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                ApplySipOverride(modules, keyHash, sip);
                SkipLine(bytes, length, ref index);
            }
        }

        private void ApplySipOverride(NativeArray<BaseModuleStateDTO> modules, uint keyHash, float sip)
        {
            if (!math.isfinite(sip) || sip <= 0f)
                return;

            for (int i = 0; i < _activeModuleCount && i < modules.Length; i++)
            {
                BaseModuleStateDTO module = modules[i];
                if (!ProfileMatchesModuleKind(keyHash, module.ModuleKind))
                    continue;

                module.BaseSIP = sip;
                float currentSip = math.isfinite(module.CurrentSIP) ? math.max(0f, module.CurrentSIP) : 0f;
                module.CurrentSIP = math.max(currentSip, sip);
                modules[i] = module;
            }
        }

        private static bool ProfileMatchesModuleKind(uint keyHash, byte moduleKind)
        {
            const uint GlassHash = 0xF203A92Bu;
            const uint GlassDomeHash = 0x71E78AE7u;
            const uint TitaniumHash = 0x1B9F8256u;
            const uint TitaniumCorridorHash = 0x72101A8Du;
            const uint ReinforcementHash = 0xD0AC36AAu;
            const uint BulkheadHash = 0x40465B1Bu;

            if (moduleKind == 0)
                return keyHash == GlassHash || keyHash == GlassDomeHash;
            if (moduleKind == 1)
                return keyHash == TitaniumHash || keyHash == TitaniumCorridorHash;
            if (moduleKind == 3)
                return keyHash == ReinforcementHash || keyHash == BulkheadHash;
            return false;
        }

        private static bool TryParsePositiveFloat(byte[] bytes, int length, ref int index, out float value)
        {
            value = 0f;
            float scale = 1f;
            bool hasDigit = false;
            bool fraction = false;

            while (index < length)
            {
                byte c = bytes[index];
                if (c >= (byte)'0' && c <= (byte)'9')
                {
                    hasDigit = true;
                    float digit = c - (byte)'0';
                    if (fraction)
                    {
                        scale *= 0.1f;
                        value += digit * scale;
                    }
                    else
                    {
                        value = value * 10f + digit;
                    }

                    index++;
                    continue;
                }

                if (c == (byte)'.' && !fraction)
                {
                    fraction = true;
                    index++;
                    continue;
                }

                break;
            }

            return hasDigit && math.isfinite(value);
        }

        private static void SkipLine(byte[] bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n')
                index++;
            if (index < length)
                index++;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            mockModuleCount = Mathf.Clamp(mockModuleCount, 1, HullIntegrityConstants.MaxMockModuleCapacity);
            mockDepthMeters = Mathf.Max(0f, mockDepthMeters);
            mockDepthJitterMeters = Mathf.Max(0f, mockDepthJitterMeters);
            baseSipMultiplier = Mathf.Max(0.01f, baseSipMultiplier);
            crushDepthGradient = Mathf.Max(0.000001f, crushDepthGradient);
            dentRadius = Mathf.Max(0.05f, dentRadius);
            dentDepth = Mathf.Max(0.001f, dentDepth);
            submarineSip = Mathf.Max(1f, submarineSip);
        }
#endif
    }
}
