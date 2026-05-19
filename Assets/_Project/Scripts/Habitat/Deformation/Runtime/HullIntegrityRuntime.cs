using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Habitat.Deformation.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

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
        private const int DeformationCapacityDumpFlag = 1 << 5;
        private const uint BreachSignalHash = 0x48384252u; // H8BR
        private const uint AcousticSignalHash = 0x48384143u; // H8AC
        private const uint DumpMagic = 0x48384E54u; // H8NT
        private const uint DumpVersion = 2u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_HULL_INTEGRITY.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_HULL_INTEGRITY.h8dump";
        private const string DeformationDumpRelativePath = "Docs/AgentLogs/Dump_DEFORMATION_SCULPTOR.bin";
        private const float DentCapUpgradeHysteresisSeconds = 2.5f;
        private const int HealthStateNominal = 0;
        private const int HealthStateWarning = 1;
        private const int HealthStateCritical = 2;
        private const float DefaultMetalPlasticity = 1f;
        private const float DefaultMaxDentDepth = 0.35f;
        private const float DefaultPressureBuckleThreshold01 = 0.82f;
        private const float DefaultVisualOverkillLimit = 1f;
        private const int BreachJetVertexCount = 6;
        private const BufferID DeformationStatesBufferId = (BufferID)70090;
        private const BufferID HullImpactScratchBufferId = (BufferID)70091;
        private const BufferID DeformationTelemetryBufferId = (BufferID)70092;
        private const BufferID DeformationTelemetryCursorBufferId = (BufferID)70093;
        private const BufferID BreachJetsBufferId = (BufferID)70094;
        private const BufferID BreachJetArgsBufferId = (BufferID)70095;
        private const BufferID HullMaterialStrengthBufferId = (BufferID)70096;
        private const BufferID HullMaterialStrengthCsvScratchBufferId = (BufferID)70097;
        private const BufferID ExternalPressure01BufferId = (BufferID)70098;
        private const BufferID PendingVisualImpactsBufferId = (BufferID)70099;

        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.Habitat.HullIntegrity.Tick");
        private static readonly ProfilerMarker _lateMarker = new ProfilerMarker("H8.Habitat.HullIntegrity.LateFrame");
        private static readonly int _HullDentDTOBufferId = Shader.PropertyToID("_HectonHullDentDTOBuffer");
        private static readonly int _HullDentDTOParamsId = Shader.PropertyToID("_HectonHullDentDTOParams");
        private static readonly int _DeformationStateBufferId = Shader.PropertyToID("_HectonDeformationStateBuffer");
        private static readonly int _DeformationStateParamsId = Shader.PropertyToID("_HectonDeformationStateParams");
        private static readonly int _BreachJetBufferId = Shader.PropertyToID("_HectonBreachJetBuffer");
        private static readonly int _BreachJetParamsId = Shader.PropertyToID("_HectonBreachJetParams");
        private static readonly int _UseLeakParticleBufferId = Shader.PropertyToID("_UseLeakParticleBuffer");
        private static readonly int _LeakPlumeParticleSizeId = Shader.PropertyToID("_LeakPlumeParticleSize");
        private static readonly int _SubmarineLocalToWorldId = Shader.PropertyToID("_SubmarineLocalToWorld");
        private static readonly int _CameraRightWSId = Shader.PropertyToID("_CameraRightWS");
        private static readonly int _CameraUpWSId = Shader.PropertyToID("_CameraUpWS");

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

        [Tooltip("Optional material using HECTON/VFX/LeakPlume for DrawProceduralIndirect breach jets.")]
        [SerializeField] private Material breachJetMaterial;

        [Tooltip("Optional camera basis override for procedural breach jet billboards. Falls back to cached player camera.")]
        [SerializeField] private Camera breachJetCameraOverride;

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

        [Tooltip("Project-relative material strength CSV. Format: material,plasticity,max_dent_depth,pressure_buckle_threshold,repair_relaxation")]
        [SerializeField] private string materialStrengthCsvPath = "hull_material_strengths.csv";

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
        private VaultBufferHandle<DeformationStateDTO> _deformationStatesHandle;
        private VaultBufferHandle<HullImpactDTO> _mockImpactsHandle;
        private VaultBufferHandle<HullImpactDTO> _pendingVisualImpactsHandle;
        private VaultBufferHandle<DeformationTelemetryEntry> _deformationTelemetryHandle;
        private VaultBufferHandle<int> _deformationTelemetryCursorHandle;
        private VaultBufferHandle<BreachJetDTO> _breachJetsHandle;
        private VaultBufferHandle<BreachJetIndirectArgsDTO> _breachJetArgsHandle;
        private VaultBufferHandle<HullMaterialStrengthDTO> _materialStrengthsHandle;
        private VaultBufferHandle<byte> _materialStrengthCsvScratchHandle;
        private VaultBufferHandle<float> _externalPressure01Handle;

        private GraphicsBuffer _dentBufferA;
        private GraphicsBuffer _dentBufferB;
        private GraphicsBuffer _deformationBufferA;
        private GraphicsBuffer _deformationBufferB;
        private GraphicsBuffer _breachJetBufferA;
        private GraphicsBuffer _breachJetBufferB;
        private GraphicsBuffer _breachJetArgsBufferA;
        private GraphicsBuffer _breachJetArgsBufferB;
        private int _gpuReadIndex;
        private int _deformationGpuReadIndex;
        private int _deformationGpuPendingIndex = -1;
        private int _breachGpuReadIndex;
        private int _cachedDentCap = HullIntegrityConstants.MinTrackedDentCapacity;
        private float _cachedGlobalQualityWeight = 1f;
        private int _cachedShaderDentLimit = HullIntegrityConstants.MinShaderDentCapacity;
        private int _cachedHealthState;
        private int _pendingDentCap;
        private int _pendingHealthState;
        private float _pendingQualitySeconds;
        private byte _cachedScalabilityProfileByte;
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
        private Camera _cachedBreachJetCamera;
        private float _maxPressureExperienced;
        private float3 _lastDentPosition;
        private float _lastDentDepth;
        private float _lastDeformationGpuUploadMicroseconds;
        private long _lastCsvTicks;
        private long _lastMaterialCsvTicks;
        private int _lastUploadedDentCount = -1;
        private Vector4 _lastDentParams = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private int _lastUploadedDeformationCount = -1;
        private Vector4 _deformationReadParams;
        private Vector4 _deformationPendingParams;

        /// <summary>Active runtime instance for editor-only visualization.</summary>
        public static HullIntegrityRuntime ActiveRuntime => s_activeRuntime;

        /// <summary>Root transform used to convert local dent DTOs into Scene View positions.</summary>
        public Transform DentRoot => ResolveDentRoot();

        /// <inheritdoc />
        public int ModuleStressCount => _activeModuleCount;

        private void Awake()
        {
            CacheColdScalabilityProfile();
            _cachedGlobalQualityWeight = ResolveGlobalQualityWeight();
            _cachedDentCap = ResolveDentCap(_cachedGlobalQualityWeight);
            _cachedShaderDentLimit = ResolveShaderDentLimit(_cachedGlobalQualityWeight, DefaultVisualOverkillLimit);
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
            ReleaseBuffer(ref _deformationBufferA);
            ReleaseBuffer(ref _deformationBufferB);
            ReleaseBuffer(ref _breachJetBufferA);
            ReleaseBuffer(ref _breachJetBufferB);
            ReleaseBuffer(ref _breachJetArgsBufferA);
            ReleaseBuffer(ref _breachJetArgsBufferB);
            _initialized = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _initialized == 0 || _dataVault == null)
                return;

            NativeArray<DeformationStateDTO> deformations = _deformationStatesHandle.Resolve(_dataVault);
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            if (!deformations.IsCreated || !counters.IsCreated || counters.Length <= HullIntegrityConstants.CounterActiveDeformationCount)
                return;

            int active = math.clamp(
                counters[HullIntegrityConstants.CounterActiveDeformationCount],
                0,
                math.min(deformations.Length, _cachedShaderDentLimit));
            if (active <= 0)
                return;

            Transform root = ResolveDentRoot();
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = root != null ? root.localToWorldMatrix : transform.localToWorldMatrix;

            for (int i = 0; i < active; i++)
            {
                DeformationStateDTO dent = deformations[i];
                if ((dent.Flags & DeformationStateFlags.Active) == 0u)
                    continue;

                bool breach = (dent.Flags & DeformationStateFlags.Breach) != 0u;
                Gizmos.color = breach ? Color.red : Color.yellow;
                Vector3 localPoint = new Vector3(dent.LocalPosition.x, dent.LocalPosition.y, dent.LocalPosition.z);
                Vector3 localNormal = new Vector3(dent.Normal.x, dent.Normal.y, dent.Normal.z);
                float radius = Mathf.Clamp(dent.Radius * 0.075f, 0.045f, 0.32f);
                Gizmos.DrawWireSphere(localPoint, radius);
                Gizmos.DrawLine(localPoint, localPoint + localNormal * Mathf.Clamp(dent.Depth * 2f, 0.08f, 0.65f));
            }

            Gizmos.matrix = previousMatrix;
        }
#endif

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
                ResolveTuning(out HullIntegrityTuningDTO tuning);
                DrainQualitySignals(deltaTime, tuning);
                int damageCount = GatherDamageSignals(tuning);
                MockRepairLaserSignal repair = DrainRepairSignals();

                NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
                NativeArray<BaseIntegrityLedgerDTO> ledger = _ledgerHandle.Resolve(_dataVault);
                NativeArray<MockDepthSignal> depthSignal = _mockDepthHandle.Resolve(_dataVault);
                NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
                NativeArray<MockCombatDamageSignal> damageSignals = _damageSignalsHandle.Resolve(_dataVault);
                NativeArray<HullDentDTO> dents = _dentsHandle.Resolve(_dataVault);
                NativeArray<DeformationStateDTO> deformationStates = _deformationStatesHandle.Resolve(_dataVault);
                NativeArray<HullImpactDTO> pendingVisualImpacts = _pendingVisualImpactsHandle.Resolve(_dataVault);
                NativeArray<BreachJetDTO> breachJets = _breachJetsHandle.Resolve(_dataVault);
                NativeArray<BreachJetIndirectArgsDTO> breachJetArgs = _breachJetArgsHandle.Resolve(_dataVault);
                NativeArray<HullMaterialStrengthDTO> materialStrengths = _materialStrengthsHandle.Resolve(_dataVault);
                NativeArray<float> externalPressure01 = _externalPressure01Handle.Resolve(_dataVault);

                if (!modules.IsCreated || !ledger.IsCreated || !depthSignal.IsCreated || !counters.IsCreated || !damageSignals.IsCreated || !dents.IsCreated || !deformationStates.IsCreated || !pendingVisualImpacts.IsCreated || !breachJets.IsCreated || !breachJetArgs.IsCreated || !materialStrengths.IsCreated || !externalPressure01.IsCreated)
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

                handle = new AccumulateHullDamageJob
                {
                    Impacts = pendingVisualImpacts,
                    States = deformationStates,
                    Counters = counters,
                    MaterialStrengths = materialStrengths,
                    SubmarineAup = ResolveSubmarineAupDouble(),
                    HullExtents = new float3(submarineHullExtents.x, submarineHullExtents.y, submarineHullExtents.z),
                    Capacity = _cachedDentCap,
                    MaxActiveDents = _cachedShaderDentLimit,
                    MetalPlasticity = tuning.MetalPlasticity,
                    MaxDentDepth = tuning.MaxDentDepth,
                    GlobalQualityWeight = _cachedGlobalQualityWeight,
                    Frame = _frame
                }.Schedule(handle);

                handle = new ApplyPressureBucklingJob
                {
                    States = deformationStates,
                    Ledger = ledger,
                    ExternalPressure01 = externalPressure01,
                    Counters = counters,
                    Capacity = _cachedDentCap,
                    MaxActiveDents = _cachedShaderDentLimit,
                    HullExtents = new float3(submarineHullExtents.x, submarineHullExtents.y, submarineHullExtents.z),
                    PressureBuckleThreshold01 = tuning.PressureBuckleThreshold01,
                    MaxDentDepth = tuning.MaxDentDepth,
                    GlobalQualityWeight = _cachedGlobalQualityWeight,
                    Frame = _frame
                }.Schedule(handle);

                handle = new DecayDeformationJob
                {
                    States = deformationStates,
                    Counters = counters,
                    Capacity = _cachedDentCap,
                    DeltaTime = deltaTime,
                    RelaxDepthPerSecond = 0.0025f * math.lerp(0.25f, 1f, _cachedGlobalQualityWeight),
                    RepairDepthPerSecond = repair.DepthPerSecond,
                    RepairLocalPosition = repair.LocalPoint,
                    RepairRadius = repair.Radius,
                    RepairEnabled = (repair.Flags & 1u) != 0u ? 1 : 0
                }.Schedule(handle);

                handle = new BuildBreachJetsJob
                {
                    States = deformationStates,
                    Jets = breachJets,
                    Args = breachJetArgs,
                    Counters = counters,
                    Capacity = _cachedDentCap,
                    Frame = _frame,
                    MaxDentDepth = tuning.MaxDentDepth,
                    PressureBuckleThreshold01 = tuning.PressureBuckleThreshold01,
                    GlobalQualityWeight = _cachedGlobalQualityWeight,
                    VertexCountPerJet = (uint)BreachJetVertexCount
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
                NativeArray<DeformationStateDTO> deformationStates = _deformationStatesHandle.Resolve(_dataVault);
                NativeArray<BreachJetDTO> breachJets = _breachJetsHandle.Resolve(_dataVault);
                NativeArray<BreachJetIndirectArgsDTO> breachJetArgs = _breachJetArgsHandle.Resolve(_dataVault);
                if (!modules.IsCreated || !ledger.IsCreated || !counters.IsCreated || !dents.IsCreated || !deformationStates.IsCreated || !breachJets.IsCreated || !breachJetArgs.IsCreated)
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

                _lastDeformationGpuUploadMicroseconds = 0f;
                BindPendingDeformationReadBuffer();
                if (_forceGpuUpload != 0)
                {
                    long uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    UploadDentsToGpu(dents, counters);
                    UploadDeformationsToGpu(deformationStates, counters);
                    UploadBreachJetsToGpu(breachJets, breachJetArgs, counters);
                    long uploadTicks = System.Diagnostics.Stopwatch.GetTimestamp() - uploadStart;
                    _lastDeformationGpuUploadMicroseconds = uploadTicks > 0
                        ? (float)((double)uploadTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency)
                        : 0f;
                }

                RecordDeformationTelemetry(deformationStates, currentLedger, counters);
                RenderBreachJets(counters);
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
            CheckMaterialStrengthCsvCold();
#endif
            RefreshBreachJetCameraCold();
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
                _cachedScalabilityProfileByte);
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
            if (_jobScheduled)
                return false;

            return AppendDentAndDamage(signal);
        }

        /// <summary>
        /// Generates deterministic AUP-space visual impacts for isolated GPU deformation profiling.
        /// </summary>
        public bool GenerateMockHullImpacts(int requestedCount, float magnitudeScale)
        {
            if (_initialized == 0 && !TryInitialize())
                return false;
            if (_jobScheduled)
                return false;

            NativeArray<HullImpactDTO> impacts = _mockImpactsHandle.Resolve(_dataVault);
            if (!impacts.IsCreated)
                return false;

            int count = math.clamp(requestedCount, 0, math.min(impacts.Length, HullIntegrityConstants.MaxMockHullImpactCount));
            if (count <= 0)
                return false;

            float scale = math.isfinite(magnitudeScale) ? math.max(0.01f, magnitudeScale) : 1f;
            new GenerateMockHullImpactsJob
            {
                Impacts = impacts,
                SubmarineAup = ResolveSubmarineAupDouble(),
                HullExtents = new float3(submarineHullExtents.x, submarineHullExtents.y, submarineHullExtents.z),
                Frame = _frame,
                SectorHash = HullIntegrityConstants.DefaultSubmarineHash,
                ImpactCount = count,
                GlobalQualityWeight = _cachedGlobalQualityWeight,
                MinMagnitude = 100f * scale,
                MaxMagnitude = 900f * scale
            }.Schedule(count, 32).Complete(); // COLD/EDITOR SYNC JOB: deterministic stress injection, not per-frame gameplay.

            for (int i = 0; i < count; i++)
            {
                HullImpactDTO impact = impacts[i];
                if (!EnqueueVisualImpact(impact.ImpactAup, impact.Magnitude, impact.DamageTypeHash))
                    return false;
            }

            _forceGpuUpload = 1;
            return true;
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
        /// Reads a visual deformation DTO by packed active slot for editor diagnostics.
        /// </summary>
        public bool TryGetDeformation(int index, out DeformationStateDTO deformation)
        {
            deformation = default;
            if (_dataVault == null || (uint)index >= HullIntegrityConstants.MaxDentCapacity)
                return false;

            NativeArray<DeformationStateDTO> states = _deformationStatesHandle.Resolve(_dataVault);
            if (!states.IsCreated || index >= states.Length)
                return false;

            deformation = states[index];
            return (deformation.Flags & DeformationStateFlags.Active) != 0u &&
                deformation.Depth > 0f &&
                deformation.Radius > 0f;
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

            CacheColdScalabilityProfile();
            _cachedDentCap = ResolveDentCap(_cachedGlobalQualityWeight);
            _cachedShaderDentLimit = ResolveShaderDentLimit(_cachedGlobalQualityWeight, DefaultVisualOverkillLimit);
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
            _deformationStatesHandle = _dataVault.GetBufferHandle<DeformationStateDTO>(
                DeformationStatesBufferId,
                HullIntegrityConstants.MaxDentCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _mockImpactsHandle = _dataVault.GetBufferHandle<HullImpactDTO>(
                HullImpactScratchBufferId,
                HullIntegrityConstants.MaxMockHullImpactCount,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _pendingVisualImpactsHandle = _dataVault.GetBufferHandle<HullImpactDTO>(
                PendingVisualImpactsBufferId,
                HullIntegrityConstants.MaxMockHullImpactCount,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _deformationTelemetryHandle = _dataVault.GetBufferHandle<DeformationTelemetryEntry>(
                DeformationTelemetryBufferId,
                HullIntegrityConstants.TelemetryFrameCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _deformationTelemetryCursorHandle = _dataVault.GetBufferHandle<int>(
                DeformationTelemetryCursorBufferId,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _breachJetsHandle = _dataVault.GetBufferHandle<BreachJetDTO>(
                BreachJetsBufferId,
                HullIntegrityConstants.MaxBreachJets,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _breachJetArgsHandle = _dataVault.GetBufferHandle<BreachJetIndirectArgsDTO>(
                BreachJetArgsBufferId,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _materialStrengthsHandle = _dataVault.GetBufferHandle<HullMaterialStrengthDTO>(
                HullMaterialStrengthBufferId,
                32,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _materialStrengthCsvScratchHandle = _dataVault.GetBufferHandle<byte>(
                HullMaterialStrengthCsvScratchBufferId,
                16 * 1024,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _externalPressure01Handle = _dataVault.GetBufferHandle<float>(
                ExternalPressure01BufferId,
                1,
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
                !_damageSignalsHandle.IsCreated ||
                !_deformationStatesHandle.IsCreated ||
                !_mockImpactsHandle.IsCreated ||
                !_pendingVisualImpactsHandle.IsCreated ||
                !_deformationTelemetryHandle.IsCreated ||
                !_deformationTelemetryCursorHandle.IsCreated ||
                !_breachJetsHandle.IsCreated ||
                !_breachJetArgsHandle.IsCreated ||
                !_materialStrengthsHandle.IsCreated ||
                !_materialStrengthCsvScratchHandle.IsCreated ||
                !_externalPressure01Handle.IsCreated)
            {
                return false;
            }

            EnsureGpuBuffers();
            ClearBootBuffers();
            WriteDefaultTuning();
            GenerateEmergencyMockIntegrity();
            BuildEmergencyScratchProof();
            BindInitialShaderState();
            RefreshBreachJetCameraCold();
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

            EnqueueVisualImpact(BuildDoubleAupFromLocal(signal.LocalPoint), signal.Magnitude, signal.DamageType);

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

            int slot = counters[HullIntegrityConstants.CounterWriteCursor] % capacity;
            if (slot < 0)
                slot += capacity;
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

            counters[HullIntegrityConstants.CounterWriteCursor] = (slot + 1) % capacity;
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
                float3 finiteWorldPoint = CombatDamageSignalCodec.ToRuntimePointOrZero(in signal);
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
                EnqueueVisualImpact(HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint), magnitude, signal.DamageType);
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
            global::Hecton8.World.AbsoluteUniversePosition leakAup = BuildAupFromLocal(module.LocalCenter);
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
                QualityTier = _cachedScalabilityProfileByte
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

        private void RecordDeformationTelemetry(
            NativeArray<DeformationStateDTO> deformations,
            BaseIntegrityLedgerDTO ledger,
            NativeArray<int> counters)
        {
            NativeArray<DeformationTelemetryEntry> telemetry = _deformationTelemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursorArray = _deformationTelemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || !cursorArray.IsCreated || cursorArray.Length == 0)
                return;

            int cursor = cursorArray[0];
            int slot = math.abs(cursor) % HullIntegrityConstants.TelemetryFrameCapacity;
            int active = counters.Length > HullIntegrityConstants.CounterActiveDeformationCount
                ? math.clamp(counters[HullIntegrityConstants.CounterActiveDeformationCount], 0, _cachedDentCap)
                : 0;
            int discarded = counters.Length > HullIntegrityConstants.CounterDiscardedImpactCount
                ? math.max(0, counters[HullIntegrityConstants.CounterDiscardedImpactCount])
                : 0;
            int breachJets = counters.Length > HullIntegrityConstants.CounterBreachJetCount
                ? math.max(0, counters[HullIntegrityConstants.CounterBreachJetCount])
                : 0;
            float maxDepth = ResolveMaxDeformationDepth(deformations, active, out float3 lastDent);
            float safePressure = math.isfinite(ledger.DepthPressure) ? math.max(0f, ledger.DepthPressure) : 0f;
            bool finite = math.isfinite(maxDepth) &&
                math.isfinite(safePressure) &&
                math.all(math.isfinite(lastDent));
            uint faultFlags = counters.Length > HullIntegrityConstants.CounterFaultFlags
                ? (uint)counters[HullIntegrityConstants.CounterFaultFlags]
                : 0u;

            telemetry[slot] = new DeformationTelemetryEntry
            {
                Frame = _frame,
                ActiveDentCount = (uint)active,
                DiscardedImpactCount = (uint)discarded,
                BreachJetCount = (uint)breachJets,
                MaxCrushDepth = safePressure,
                MaxDentDepth = maxDepth,
                GpuUploadMicroseconds = _lastDeformationGpuUploadMicroseconds,
                GlobalQualityWeight = _cachedGlobalQualityWeight,
                LastDentLocalPosition = lastDent,
                Flags = finite ? 0u : 1u,
                StateHash = math.hash(new uint4(_frame, (uint)active, (uint)discarded, math.asuint(maxDepth))),
                FaultFlags = faultFlags
            };

            cursorArray[0] = (cursor + 1) % HullIntegrityConstants.TelemetryFrameCapacity;
            bool saturated = discarded > 0 && active >= _cachedShaderDentLimit;
            if (!finite || (saturated && (faultFlags & (uint)DeformationCapacityDumpFlag) == 0u))
            {
                DumpDeformationTelemetry();
                if (saturated && counters.IsCreated && counters.Length > HullIntegrityConstants.CounterFaultFlags)
                    counters[HullIntegrityConstants.CounterFaultFlags] = (int)(faultFlags | (uint)DeformationCapacityDumpFlag);
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
            new HullIntegrityMappedCopyJob
            {
                Source = sourcePtr,
                Destination = destinationPtr,
                Bytes = bytes
            }.Run();
            writeBuffer.UnlockBufferAfterWrite<HullDentDTO>(uploadCount);

            _gpuReadIndex = writeIndex;
            GraphicsBuffer readBuffer = _gpuReadIndex == 0 ? _dentBufferA : _dentBufferB;
            float maxDepth = ResolveMaxDentDepth(dents, _cachedDentCap);
            Vector4 dtoParams = new Vector4(activeCount, activeCount > 0 ? 1f : 0f, maxDepth, _cachedScalabilityProfileByte);

            if (_lastUploadedDentCount != activeCount || _lastDentParams != dtoParams)
            {
                Shader.SetGlobalBuffer(_HullDentDTOBufferId, readBuffer);
                Shader.SetGlobalVector(_HullDentDTOParamsId, dtoParams);
                _lastUploadedDentCount = activeCount;
                _lastDentParams = dtoParams;
            }

            _forceGpuUpload = 0;
        }

        private void UploadDeformationsToGpu(NativeArray<DeformationStateDTO> deformations, NativeArray<int> counters)
        {
            EnsureGpuBuffers();
            if (_deformationBufferA == null || _deformationBufferB == null || !deformations.IsCreated || !counters.IsCreated)
                return;

            int activeCount = counters.Length > HullIntegrityConstants.CounterActiveDeformationCount
                ? math.clamp(counters[HullIntegrityConstants.CounterActiveDeformationCount], 0, _cachedShaderDentLimit)
                : 0;
            int uploadCount = math.max(1, activeCount);
            int writeIndex = 1 - _deformationGpuReadIndex;
            GraphicsBuffer writeBuffer = writeIndex == 0 ? _deformationBufferA : _deformationBufferB;

            NativeArray<DeformationStateDTO> mapped = writeBuffer.LockBufferForWrite<DeformationStateDTO>(0, uploadCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(deformations);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long bytes = (long)UnsafeUtility.SizeOf<DeformationStateDTO>() * uploadCount;
            new HullIntegrityMappedCopyJob
            {
                Source = sourcePtr,
                Destination = destinationPtr,
                Bytes = bytes
            }.Run();
            writeBuffer.UnlockBufferAfterWrite<DeformationStateDTO>(uploadCount);

            float maxDepth = ResolveMaxDeformationDepth(deformations, activeCount, out _);
            _deformationPendingParams = new Vector4(activeCount, _cachedShaderDentLimit, maxDepth, _cachedGlobalQualityWeight);
            _deformationGpuPendingIndex = writeIndex;
            _lastUploadedDeformationCount = activeCount;
        }

        private void BindPendingDeformationReadBuffer()
        {
            if (_deformationGpuPendingIndex < 0 || _deformationBufferA == null || _deformationBufferB == null)
                return;

            _deformationGpuReadIndex = _deformationGpuPendingIndex;
            _deformationGpuPendingIndex = -1;
            _deformationReadParams = _deformationPendingParams;
            GraphicsBuffer readBuffer = _deformationGpuReadIndex == 0 ? _deformationBufferA : _deformationBufferB;
            Shader.SetGlobalBuffer(_DeformationStateBufferId, readBuffer);
            Shader.SetGlobalVector(_DeformationStateParamsId, _deformationReadParams);
        }

        private void UploadBreachJetsToGpu(
            NativeArray<BreachJetDTO> jets,
            NativeArray<BreachJetIndirectArgsDTO> args,
            NativeArray<int> counters)
        {
            EnsureGpuBuffers();
            if (_breachJetBufferA == null ||
                _breachJetBufferB == null ||
                _breachJetArgsBufferA == null ||
                _breachJetArgsBufferB == null ||
                !jets.IsCreated ||
                !args.IsCreated ||
                !counters.IsCreated)
            {
                return;
            }

            int jetCount = counters.Length > HullIntegrityConstants.CounterBreachJetCount
                ? math.clamp(counters[HullIntegrityConstants.CounterBreachJetCount], 0, HullIntegrityConstants.MaxBreachJets)
                : 0;
            int uploadCount = math.max(1, jetCount);
            int writeIndex = 1 - _breachGpuReadIndex;
            GraphicsBuffer jetWriteBuffer = writeIndex == 0 ? _breachJetBufferA : _breachJetBufferB;
            GraphicsBuffer argsWriteBuffer = writeIndex == 0 ? _breachJetArgsBufferA : _breachJetArgsBufferB;

            NativeArray<BreachJetDTO> mappedJets = jetWriteBuffer.LockBufferForWrite<BreachJetDTO>(0, uploadCount);
            void* sourceJets = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jets);
            void* destinationJets = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mappedJets);
            new HullIntegrityMappedCopyJob
            {
                Source = sourceJets,
                Destination = destinationJets,
                Bytes = (long)UnsafeUtility.SizeOf<BreachJetDTO>() * uploadCount
            }.Run();
            jetWriteBuffer.UnlockBufferAfterWrite<BreachJetDTO>(uploadCount);

            NativeArray<BreachJetIndirectArgsDTO> mappedArgs = argsWriteBuffer.LockBufferForWrite<BreachJetIndirectArgsDTO>(0, 1);
            void* sourceArgs = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(args);
            void* destinationArgs = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mappedArgs);
            new HullIntegrityMappedCopyJob
            {
                Source = sourceArgs,
                Destination = destinationArgs,
                Bytes = UnsafeUtility.SizeOf<BreachJetIndirectArgsDTO>()
            }.Run();
            argsWriteBuffer.UnlockBufferAfterWrite<BreachJetIndirectArgsDTO>(1);

            _breachGpuReadIndex = writeIndex;
            GraphicsBuffer jetReadBuffer = _breachGpuReadIndex == 0 ? _breachJetBufferA : _breachJetBufferB;
            Shader.SetGlobalBuffer(_BreachJetBufferId, jetReadBuffer);
            Shader.SetGlobalVector(_BreachJetParamsId, new Vector4(jetCount, HullIntegrityConstants.MaxBreachJets, _cachedGlobalQualityWeight, 0f));
        }

        private void RenderBreachJets(NativeArray<int> counters)
        {
            if (breachJetMaterial == null || !counters.IsCreated)
                return;

            int jetCount = counters.Length > HullIntegrityConstants.CounterBreachJetCount
                ? math.clamp(counters[HullIntegrityConstants.CounterBreachJetCount], 0, HullIntegrityConstants.MaxBreachJets)
                : 0;
            if (jetCount <= 0)
                return;

            GraphicsBuffer jetReadBuffer = _breachGpuReadIndex == 0 ? _breachJetBufferA : _breachJetBufferB;
            GraphicsBuffer argsReadBuffer = _breachGpuReadIndex == 0 ? _breachJetArgsBufferA : _breachJetArgsBufferB;
            if (jetReadBuffer == null || argsReadBuffer == null)
                return;

            Transform root = ResolveDentRoot();
            Matrix4x4 localToWorld = root != null ? root.localToWorldMatrix : transform.localToWorldMatrix;
            Camera camera = ResolveBreachJetCamera();
            Vector3 cameraRight = camera != null ? camera.transform.right : transform.right;
            Vector3 cameraUp = camera != null ? camera.transform.up : transform.up;
            Vector3 center = root != null ? root.position : transform.position;
            Vector3 extents = new Vector3(
                Mathf.Max(1f, submarineHullExtents.x + 4f),
                Mathf.Max(1f, submarineHullExtents.y + 4f),
                Mathf.Max(1f, submarineHullExtents.z + 4f));

            breachJetMaterial.SetBuffer(_BreachJetBufferId, jetReadBuffer);
            breachJetMaterial.SetVector(_BreachJetParamsId, new Vector4(jetCount, HullIntegrityConstants.MaxBreachJets, _cachedGlobalQualityWeight, 0f));
            breachJetMaterial.SetFloat(_UseLeakParticleBufferId, 1f);
            breachJetMaterial.SetFloat(_LeakPlumeParticleSizeId, 0.22f);
            breachJetMaterial.SetMatrix(_SubmarineLocalToWorldId, localToWorld);
            breachJetMaterial.SetVector(_CameraRightWSId, cameraRight);
            breachJetMaterial.SetVector(_CameraUpWSId, cameraUp);

            Graphics.DrawProceduralIndirect(
                breachJetMaterial,
                new Bounds(center, extents * 2f),
                MeshTopology.Triangles,
                argsReadBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer);
        }

        private Camera ResolveBreachJetCamera()
        {
            if (breachJetCameraOverride != null && breachJetCameraOverride.isActiveAndEnabled)
                return breachJetCameraOverride;

            if (_cachedBreachJetCamera != null && _cachedBreachJetCamera.isActiveAndEnabled)
                return _cachedBreachJetCamera;

            return null;
        }

        private void RefreshBreachJetCameraCold()
        {
            if (breachJetCameraOverride != null && breachJetCameraOverride.isActiveAndEnabled)
            {
                _cachedBreachJetCamera = breachJetCameraOverride;
                return;
            }

            if (_cachedBreachJetCamera != null && _cachedBreachJetCamera.isActiveAndEnabled)
                return;

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            Camera playerCamera = player != null ? player.PlayerCamera : null;
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
                _cachedBreachJetCamera = playerCamera;
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
            NativeArray<DeformationStateDTO> deformationStates = _deformationStatesHandle.Resolve(_dataVault);
            if (deformationStates.IsCreated)
            {
                handle = new ClearDeformationActiveFlagsJob
                {
                    States = deformationStates
                }.Schedule(deformationStates.Length, 64, handle);
            }

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
            handle = ScheduleMemClear(_mockImpactsHandle, handle);
            handle = ScheduleMemClear(_pendingVisualImpactsHandle, handle);
            handle = ScheduleMemClear(_deformationTelemetryHandle, handle);
            handle = ScheduleMemClear(_deformationTelemetryCursorHandle, handle);
            handle = ScheduleMemClear(_breachJetsHandle, handle);
            handle = ScheduleMemClear(_breachJetArgsHandle, handle);
            handle = ScheduleMemClear(_materialStrengthsHandle, handle);
            handle = ScheduleMemClear(_materialStrengthCsvScratchHandle, handle);
            handle = ScheduleMemClear(_externalPressure01Handle, handle);
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
                DentDepth = dentDepth,
                MetalPlasticity = DefaultMetalPlasticity,
                MaxDentDepth = DefaultMaxDentDepth,
                PressureBuckleThreshold01 = DefaultPressureBuckleThreshold01,
                VisualOverkillLimit = DefaultVisualOverkillLimit
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
                    DentDepth = dentDepth,
                    MetalPlasticity = DefaultMetalPlasticity,
                    MaxDentDepth = DefaultMaxDentDepth,
                    PressureBuckleThreshold01 = DefaultPressureBuckleThreshold01,
                    VisualOverkillLimit = DefaultVisualOverkillLimit
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
                DentDepth = math.isfinite(source.DentDepth) ? math.max(0.001f, source.DentDepth) : 0.18f,
                MetalPlasticity = math.isfinite(source.MetalPlasticity) ? math.max(0.0001f, source.MetalPlasticity) : DefaultMetalPlasticity,
                MaxDentDepth = math.isfinite(source.MaxDentDepth) ? math.max(0.001f, source.MaxDentDepth) : DefaultMaxDentDepth,
                PressureBuckleThreshold01 = math.isfinite(source.PressureBuckleThreshold01) ? math.saturate(source.PressureBuckleThreshold01) : DefaultPressureBuckleThreshold01,
                VisualOverkillLimit = math.isfinite(source.VisualOverkillLimit) ? math.saturate(source.VisualOverkillLimit) : DefaultVisualOverkillLimit
            };
        }

        private void DrainQualitySignals(float deltaTime, in HullIntegrityTuningDTO tuning)
        {
            DrainScalabilityProfileSignals();

            int healthState = ResolveHealthState();
            float qualityWeight = ResolveGlobalQualityWeight();
            if (healthState == HealthStateCritical)
            {
                qualityWeight = math.min(qualityWeight, 0.1f);
            }
            else if (healthState == HealthStateWarning)
            {
                qualityWeight = math.min(qualityWeight, 0.45f);
            }

            _cachedGlobalQualityWeight = math.saturate(qualityWeight);
            _cachedShaderDentLimit = ResolveShaderDentLimit(_cachedGlobalQualityWeight, tuning.VisualOverkillLimit);
            int desiredCap = ResolveDentCap(_cachedGlobalQualityWeight);
            ApplyDentQualityWithHysteresis(desiredCap, healthState, deltaTime);
        }

        private void DrainScalabilityProfileSignals()
        {
            ReadOnlySpan<ScalabilityChangedEvent> profileSignals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            if (profileSignals.Length == 0)
                return;

            _cachedScalabilityProfileByte = ScalabilityTierProfiles.Normalize(profileSignals[profileSignals.Length - 1].CurrentTier);
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

        private void ApplyDentQualityWithHysteresis(int desiredCap, int healthState, float deltaTime)
        {
            if (desiredCap == _cachedDentCap && healthState == _cachedHealthState)
            {
                ResetPendingDentQuality();
                return;
            }

            bool immediateDowngrade = desiredCap < _cachedDentCap || healthState > _cachedHealthState;
            if (immediateDowngrade)
            {
                ApplyDentQuality(desiredCap, healthState);
                return;
            }

            if (_pendingDentCap != desiredCap || _pendingHealthState != healthState)
            {
                _pendingDentCap = desiredCap;
                _pendingHealthState = healthState;
                _pendingQualitySeconds = 0f;
                return;
            }

            _pendingQualitySeconds += math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (_pendingQualitySeconds >= DentCapUpgradeHysteresisSeconds)
                ApplyDentQuality(desiredCap, healthState);
        }

        private void ApplyDentQuality(int dentCap, int healthState)
        {
            _cachedDentCap = dentCap;
            _cachedHealthState = healthState;
            _forceGpuUpload = 1;
            ResetPendingDentQuality();
        }

        private void ResetPendingDentQuality()
        {
            _pendingDentCap = _cachedDentCap;
            _pendingHealthState = _cachedHealthState;
            _pendingQualitySeconds = 0f;
        }

        private void CacheColdScalabilityProfile()
        {
            _cachedScalabilityProfileByte = ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
            _cachedGlobalQualityWeight = ResolveGlobalQualityWeight();
        }

        private static int ResolveDentCap(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float survivalGate = math.step(0.0001f, q);
            float curve = q * q * (3f - 2f * q) * survivalGate;
            int capacity = (int)math.round(math.lerp(
                HullIntegrityConstants.MinTrackedDentCapacity,
                HullIntegrityConstants.MaxTrackedDentCapacity,
                curve));
            return math.clamp(capacity, HullIntegrityConstants.MinTrackedDentCapacity, HullIntegrityConstants.MaxTrackedDentCapacity);
        }

        private static int ResolveShaderDentLimit(float globalQualityWeight, float visualOverkillLimit)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float limit = math.saturate(math.select(1f, visualOverkillLimit, math.isfinite(visualOverkillLimit)));
            float survivalGate = math.step(0.0001f, q);
            float curve = q * q * (3f - 2f * q) * survivalGate;
            int capacity = (int)math.floor(math.lerp(
                HullIntegrityConstants.MinShaderDentCapacity,
                HullIntegrityConstants.MaxShaderDentCapacity,
                curve * limit));
            return math.clamp(capacity, HullIntegrityConstants.MinShaderDentCapacity, HullIntegrityConstants.MaxShaderDentCapacity);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
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

        private global::Hecton8.World.AbsoluteUniversePosition BuildAupFromLocal(float3 local)
        {
            Transform root = ResolveDentRoot();
            Vector3 localPoint = new Vector3(local.x, local.y, local.z);
            Vector3 runtime = root != null ? root.localToWorldMatrix.MultiplyPoint3x4(localPoint) : localPoint;
            return global::Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(runtime);
        }

        private double3 BuildDoubleAupFromLocal(float3 local)
        {
            Transform root = ResolveDentRoot();
            Vector3 localPoint = new Vector3(local.x, local.y, local.z);
            Vector3 runtime = root != null ? root.localToWorldMatrix.MultiplyPoint3x4(localPoint) : localPoint;
            return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtime);
        }

        private double3 ResolveSubmarineAupDouble()
        {
            Transform root = submarineRoot != null ? submarineRoot : ResolveDentRoot();
            Vector3 runtime = root != null ? root.position : transform.position;
            return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtime);
        }

        private bool EnqueueVisualImpact(double3 impactAup, float magnitude, uint damageTypeHash)
        {
            if (_jobScheduled)
                return false;

            if (!math.all(math.isfinite(impactAup)) || !math.isfinite(magnitude))
                return false;

            NativeArray<HullImpactDTO> impacts = _pendingVisualImpactsHandle.Resolve(_dataVault);
            NativeArray<int> counters = _countersHandle.Resolve(_dataVault);
            if (!impacts.IsCreated || !counters.IsCreated || counters.Length <= HullIntegrityConstants.CounterPendingVisualImpactCount)
                return false;

            int pending = math.clamp(counters[HullIntegrityConstants.CounterPendingVisualImpactCount], 0, impacts.Length);
            if (pending >= impacts.Length)
            {
                if (counters.Length > HullIntegrityConstants.CounterDiscardedImpactCount)
                {
                    int discarded = math.max(0, counters[HullIntegrityConstants.CounterDiscardedImpactCount]);
                    counters[HullIntegrityConstants.CounterDiscardedImpactCount] = discarded < 0x3FFFFFFF ? discarded + 1 : discarded;
                }
                return false;
            }

            impacts[pending] = new HullImpactDTO
            {
                ImpactAup = impactAup,
                Magnitude = math.max(0f, magnitude),
                DamageTypeHash = damageTypeHash
            };
            counters[HullIntegrityConstants.CounterPendingVisualImpactCount] = pending + 1;
            return true;
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
                Flags = 0,
                QualityTier = _cachedScalabilityProfileByte,
                Channel = 0,
                DamageType = source.DamageType
            };
            SignalBus<HullDeformedSignal>.Push(in signal);
        }

        private void EnsureGpuBuffers()
        {
            if (_dentBufferA != null && _dentBufferA.IsValid() &&
                _dentBufferB != null && _dentBufferB.IsValid() &&
                _deformationBufferA != null && _deformationBufferA.IsValid() &&
                _deformationBufferB != null && _deformationBufferB.IsValid() &&
                _breachJetBufferA != null && _breachJetBufferA.IsValid() &&
                _breachJetBufferB != null && _breachJetBufferB.IsValid() &&
                _breachJetArgsBufferA != null && _breachJetArgsBufferA.IsValid() &&
                _breachJetArgsBufferB != null && _breachJetArgsBufferB.IsValid())
                return;

            ReleaseBuffer(ref _dentBufferA);
            ReleaseBuffer(ref _dentBufferB);
            ReleaseBuffer(ref _deformationBufferA);
            ReleaseBuffer(ref _deformationBufferB);
            ReleaseBuffer(ref _breachJetBufferA);
            ReleaseBuffer(ref _breachJetBufferB);
            ReleaseBuffer(ref _breachJetArgsBufferA);
            ReleaseBuffer(ref _breachJetArgsBufferB);
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

            int deformationStride = UnsafeUtility.SizeOf<DeformationStateDTO>();
            _deformationBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxDentCapacity,
                deformationStride); // COLD ALLOC: GraphicsBuffer[512] - SHINOBU_109 deformation state double buffer A
            _deformationBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxDentCapacity,
                deformationStride); // COLD ALLOC: GraphicsBuffer[512] - SHINOBU_109 deformation state double buffer B

            int breachStride = UnsafeUtility.SizeOf<BreachJetDTO>();
            _breachJetBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxBreachJets,
                breachStride); // COLD ALLOC: GraphicsBuffer[128] - SHINOBU_109 breach jet double buffer A
            _breachJetBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                HullIntegrityConstants.MaxBreachJets,
                breachStride); // COLD ALLOC: GraphicsBuffer[128] - SHINOBU_109 breach jet double buffer B

            _breachJetArgsBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BreachJetIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - SHINOBU_109 breach jet indirect args A
            _breachJetArgsBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BreachJetIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - SHINOBU_109 breach jet indirect args B
        }

        private void BindInitialShaderState()
        {
            EnsureGpuBuffers();
            GraphicsBuffer buffer = _dentBufferA != null ? _dentBufferA : _dentBufferB;
            if (buffer == null)
                return;

            Shader.SetGlobalBuffer(_HullDentDTOBufferId, buffer);
            Shader.SetGlobalVector(_HullDentDTOParamsId, Vector4.zero);
            Shader.SetGlobalBuffer(_DeformationStateBufferId, _deformationBufferA != null ? _deformationBufferA : _deformationBufferB);
            Shader.SetGlobalVector(_DeformationStateParamsId, Vector4.zero);
            Shader.SetGlobalBuffer(_BreachJetBufferId, _breachJetBufferA != null ? _breachJetBufferA : _breachJetBufferB);
            Shader.SetGlobalVector(_BreachJetParamsId, Vector4.zero);
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
                UnsafeUtility.SizeOf<HullImpactDTO>() == 32 &&
                UnsafeUtility.SizeOf<DeformationStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<BreachJetDTO>() == 64 &&
                UnsafeUtility.SizeOf<BreachJetIndirectArgsDTO>() == 16 &&
                UnsafeUtility.SizeOf<DeformationTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<HullMaterialStrengthDTO>() == 32 &&
                UnsafeUtility.SizeOf<BaseIntegrityLedgerDTO>() == 16 &&
                UnsafeUtility.SizeOf<BaseModuleStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<MockWFCBaseArray>() == 16 &&
                UnsafeUtility.SizeOf<MockCombatDamageSignal>() == 64 &&
                UnsafeUtility.SizeOf<MockDepthSignal>() == 16 &&
                UnsafeUtility.SizeOf<MockRepairLaserSignal>() == 32 &&
                UnsafeUtility.SizeOf<MockHullBreachSignal>() == 32 &&
                UnsafeUtility.SizeOf<HullIntegrityTuningDTO>() == 32 &&
                UnsafeUtility.SizeOf<HabitatModuleDeformationSample>() == 32 &&
                UnsafeUtility.SizeOf<HullIntegrityTelemetryEntry>() == 64 &&
                AssertOffset<HullImpactDTO>(nameof(HullImpactDTO.ImpactAup), 0) &&
                AssertOffset<HullImpactDTO>(nameof(HullImpactDTO.Magnitude), 24) &&
                AssertOffset<HullImpactDTO>(nameof(HullImpactDTO.DamageTypeHash), 28) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.LocalPosition), 0) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.Radius), 12) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.Normal), 16) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.Depth), 28) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.Age), 32) &&
                AssertOffset<DeformationStateDTO>(nameof(DeformationStateDTO.Flags), 52) &&
                AssertOffset<HullIntegrityTuningDTO>(nameof(HullIntegrityTuningDTO.MetalPlasticity), 16) &&
                AssertOffset<DeformationTelemetryEntry>(nameof(DeformationTelemetryEntry.LastDentLocalPosition), 32);
        }

        private static bool AssertOffset<T>(string fieldName, int expected) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field != null && UnsafeUtility.GetFieldOffset(field) == expected;
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

        private static float ResolveMaxDeformationDepth(NativeArray<DeformationStateDTO> deformations, int capacity, out float3 lastDent)
        {
            float maxDepth = 0f;
            lastDent = float3.zero;
            int count = math.clamp(capacity, 0, deformations.IsCreated ? deformations.Length : 0);
            for (int i = 0; i < count; i++)
            {
                DeformationStateDTO dent = deformations[i];
                if ((dent.Flags & DeformationStateFlags.Active) == 0u)
                    continue;

                float depth = math.isfinite(dent.Depth) ? math.max(0f, dent.Depth) : 0f;
                if (depth >= maxDepth)
                {
                    maxDepth = depth;
                    lastDent = math.all(math.isfinite(dent.LocalPosition)) ? dent.LocalPosition : float3.zero;
                }
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

        private void DumpDeformationTelemetry()
        {
            NativeArray<DeformationTelemetryEntry> telemetry = _deformationTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            WriteDeformationTelemetryDump(DeformationDumpRelativePath, telemetry);
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
                int stride = UnsafeUtility.SizeOf<HullIntegrityTelemetryEntry>();
                int remaining = telemetry.Length * stride;
                int offset = 0;
                while (remaining > 0)
                {
                    int bytes = math.min(16 * 1024, remaining);
                    stream.Write(new ReadOnlySpan<byte>(source + offset, bytes));
                    offset += bytes;
                    remaining -= bytes;
                }
            }
        }

        private void WriteDeformationTelemetryDump(string relativePath, NativeArray<DeformationTelemetryEntry> telemetry)
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
                WriteUInt32(stream, (uint)UnsafeUtility.SizeOf<DeformationTelemetryEntry>());

                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int stride = UnsafeUtility.SizeOf<DeformationTelemetryEntry>();
                int remaining = telemetry.Length * stride;
                int offset = 0;
                while (remaining > 0)
                {
                    int bytes = math.min(16 * 1024, remaining);
                    stream.Write(new ReadOnlySpan<byte>(source + offset, bytes));
                    offset += bytes;
                    remaining -= bytes;
                }
            }
        }

        private void WriteUInt32(FileStream stream, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            stream.Write(bytes);
        }

        private void CheckCsvOverrideCold()
        {
            if (string.IsNullOrEmpty(integrityProfileCsvPath))
                return;

            NativeArray<byte> scratch = _materialStrengthCsvScratchHandle.Resolve(_dataVault);
            if (!scratch.IsCreated || scratch.Length == 0)
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
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(new Span<byte>(ptr, scratch.Length));
            }

            ParseCsvProfiles(new ReadOnlySpan<byte>(ptr, math.clamp(bytesRead, 0, scratch.Length)));
        }

        private void CheckMaterialStrengthCsvCold()
        {
            if (string.IsNullOrEmpty(materialStrengthCsvPath))
                return;

            NativeArray<byte> scratch = _materialStrengthCsvScratchHandle.Resolve(_dataVault);
            NativeArray<HullMaterialStrengthDTO> strengths = _materialStrengthsHandle.Resolve(_dataVault);
            if (!scratch.IsCreated || !strengths.IsCreated || scratch.Length == 0)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, materialStrengthCsvPath);
            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _lastMaterialCsvTicks)
                return;

            _lastMaterialCsvTicks = ticks;
            int bytesRead;
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(new Span<byte>(ptr, scratch.Length));
            }

            ParseMaterialStrengthCsv(new ReadOnlySpan<byte>(ptr, math.clamp(bytesRead, 0, scratch.Length)), strengths);
        }

        private void ParseCsvProfiles(ReadOnlySpan<byte> csv)
        {
            NativeArray<BaseModuleStateDTO> modules = _modulesHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int index = 0;
            while (index < csv.Length)
            {
                uint keyHash = 2166136261u;
                while (index < csv.Length && csv[index] != (byte)',' && csv[index] != (byte)'\n' && csv[index] != (byte)'\r')
                {
                    byte value = csv[index++];
                    if (value >= (byte)'A' && value <= (byte)'Z')
                        value = (byte)(value + 32);
                    keyHash = (keyHash ^ value) * 16777619u;
                }

                if (index >= csv.Length || csv[index] != (byte)',')
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                index++;
                if (!TryParsePositiveFloat(csv, ref index, out float sip))
                {
                    SkipLine(csv, ref index);
                    continue;
                }

                ApplySipOverride(modules, keyHash, sip);
                SkipLine(csv, ref index);
            }
        }

        private static void ParseMaterialStrengthCsv(ReadOnlySpan<byte> csv, NativeArray<HullMaterialStrengthDTO> strengths)
        {
            if (!strengths.IsCreated)
                return;

            int cursor = 0;
            int count = 0;
            while (count < strengths.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = TrimAscii(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!TryParseMaterialStrengthRow(line, out HullMaterialStrengthDTO row))
                    continue;

                strengths[count++] = row;
            }

            for (int i = count; i < strengths.Length; i++)
                strengths[i] = default;
        }

        private static bool TryParseMaterialStrengthRow(ReadOnlySpan<byte> line, out HullMaterialStrengthDTO row)
        {
            row = default;
            int cursor = 0;
            if (!TryReadCsvToken(line, ref cursor, out ReadOnlySpan<byte> material) ||
                !TryReadCsvToken(line, ref cursor, out ReadOnlySpan<byte> plasticitySpan) ||
                !TryReadCsvToken(line, ref cursor, out ReadOnlySpan<byte> maxDepthSpan) ||
                !TryReadCsvToken(line, ref cursor, out ReadOnlySpan<byte> thresholdSpan))
            {
                return false;
            }

            TryReadCsvToken(line, ref cursor, out ReadOnlySpan<byte> repairSpan);
            uint materialHash = HashLowerAscii(material);
            if (materialHash == 0u ||
                !TryParsePositiveFloat(plasticitySpan, out float plasticity) ||
                !TryParsePositiveFloat(maxDepthSpan, out float maxDepth) ||
                !TryParsePositiveFloat(thresholdSpan, out float threshold))
            {
                return false;
            }

            float repair = TryParsePositiveFloat(repairSpan, out float parsedRepair) ? parsedRepair : 0.0025f;
            row = new HullMaterialStrengthDTO
            {
                MaterialHash = materialHash,
                Plasticity = math.max(0.0001f, plasticity),
                MaxDentDepth = math.max(0.001f, maxDepth),
                PressureBuckleThreshold01 = math.saturate(threshold),
                RepairRelaxation = math.max(0f, repair)
            };
            return true;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = ReadOnlySpan<byte>.Empty;
            if (cursor >= text.Length)
                return false;

            int start = cursor;
            while (cursor < text.Length && text[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < text.Length && text[cursor] == (byte)'\n')
                cursor++;
            if (end > start && text[end - 1] == (byte)'\r')
                end--;

            line = text.Slice(start, math.max(0, end - start));
            return true;
        }

        private static bool TryReadCsvToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            token = ReadOnlySpan<byte>.Empty;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            token = TrimAscii(line.Slice(start, math.max(0, end - start)));
            return token.Length > 0;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= (byte)' ')
                start++;
            while (end >= start && value[end] <= (byte)' ')
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash == 2166136261u ? 0u : hash;
        }

        private static bool TryParsePositiveFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            float scale = 1f;
            bool hasDigit = false;
            bool fraction = false;

            for (int i = 0; i < text.Length; i++)
            {
                byte c = text[i];
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

                    continue;
                }

                if (c == (byte)'.' && !fraction)
                {
                    fraction = true;
                    continue;
                }

                return false;
            }

            return hasDigit && math.isfinite(value);
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

        private static bool TryParsePositiveFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            float scale = 1f;
            bool hasDigit = false;
            bool fraction = false;

            while (index < bytes.Length)
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

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && bytes[index] != (byte)'\n')
                index++;
            if (index < bytes.Length)
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
