using System;
#if UNITY_EDITOR
using System.IO;
using System.Threading;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    public unsafe sealed class BuoyancyDisplacementRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IOriginShiftListener
    {
        private const uint BuoyancyFaultEventHash = 0x42554654u; // BUFT
        private const uint BuoyancyFaultDumpHash = 0x42554450u; // BUDP
        private const uint BuoyancySimdFaultEventHash = 0x42534654u; // BSFT
        private const uint BuoyancySimdFaultDumpHash = 0x42534450u; // BSDP
        private static readonly ulong CounterFaultReadMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Counters);
        private static readonly ulong CompletionTelemetryMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Counters) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.TelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.TelemetryCursor) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepTelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepTelemetryCursor);
        private static readonly ulong ForceDrainMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.ForcePackets) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Counters) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.BodyBindings);
        private static readonly ulong MockSeedMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.States) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.DebugForces) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Tuning);
        private static readonly ulong JobMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.States) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.ForcePackets) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.FlowSamples) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Tuning) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.TelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.TelemetryCursor) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepTelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepTelemetryCursor) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepSdfDensity) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SleepSdfConfig) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.MaterialSettlingProfiles) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.DebugForces) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.Counters);
        private static readonly ulong SimdBenchmarkMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdLocalPositions) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdVelocities) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdDragCoefficients) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdOutputForces) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdTelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdTelemetryCursor) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning);
        private static readonly ulong SimdTelemetryMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdTelemetryRing) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdTelemetryCursor) |
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning);
#if UNITY_EDITOR
        private static readonly ulong MaterialVolumeCsvMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.MaterialVolumes);
        private static readonly ulong MaterialSettlingCsvMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.MaterialSettlingProfiles);
        private static readonly ulong SimdToleranceCsvMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdMathTolerances);
        private static readonly ulong SimdToleranceTuningMutationGuardMask =
            VaultMutationGuardBit(BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning);
        private static readonly byte[] s_csvImportScratch = new byte[BuoyancyDisplacementConstants.CsvScratchBytes];
        private static readonly BuoyancyMaterialVolumeDTO[] s_materialVolumeImportScratch =
            new BuoyancyMaterialVolumeDTO[BuoyancyDisplacementConstants.MaterialVolumeCapacity];
        private static readonly BuoyancyMaterialSettlingProfileDTO[] s_materialSettlingImportScratch =
            new BuoyancyMaterialSettlingProfileDTO[BuoyancyDisplacementConstants.MaterialSettlingProfileCapacity];
        private static readonly SimdMathToleranceDTO[] s_simdToleranceImportScratch =
            new SimdMathToleranceDTO[BuoyancyDisplacementConstants.SimdToleranceCapacity];
        private static int s_csvImportScratchBusy;
#endif

        [Header("Vault Capacity")]
        [SerializeField, Range(1, BuoyancyDisplacementConstants.StateCapacity)]
        [Tooltip("Maximum buoyant object records processed by the SHINOBU_201 SIMD/buoyancy solver.")]
        private int _stateCapacity = BuoyancyDisplacementConstants.StateCapacity;

        [SerializeField, Range(0, BuoyancyDisplacementConstants.FlowSampleCapacity)]
        [Tooltip("Maximum abyssal flow sample records read from the Vault.")]
        private int _flowSampleCapacity = BuoyancyDisplacementConstants.FlowSampleCapacity;

        [Header("Cold Boot")]
        [SerializeField]
        [Tooltip("Seeds 1000 deterministic synthetic buoyant objects when no inventory drop stream is present.")]
        private bool _seedEmergencyMockObjects = true;

#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Loads item_volume_specs.csv into the Vault-backed material volume table during cold startup.")]
        private bool _loadCsvOnEnable = true;

        [SerializeField]
        [Tooltip("Loads material_settling_profiles.csv into the Vault-backed sleep profile table during cold startup.")]
        private bool _loadMaterialSettlingProfilesOnEnable = true;

        [SerializeField]
        [Tooltip("Loads simd_math_tolerances.csv into the Vault-backed SIMD polynomial tolerance table during cold startup.")]
        private bool _loadSimdTolerancesOnEnable = true;

        [SerializeField]
        [Tooltip("Project-relative material volume CSV path.")]
        private string _csvRelativePath = BuoyancyDisplacementConstants.CsvRelativePath;

        [SerializeField]
        [Tooltip("Project-relative material settling CSV path.")]
        private string _materialSettlingProfilesCsvRelativePath = BuoyancyDisplacementConstants.MaterialSettlingProfilesCsvRelativePath;
#endif

        private IDataVault _dataVault;
        private VaultGenerationHandle<BuoyancyStateDTO> _statesHandle;
        private VaultGenerationHandle<BuoyancyForcePacketDTO> _forcePacketsHandle;
        private VaultGenerationHandle<BuoyancyFlowSampleDTO> _flowSamplesHandle;
        private VaultGenerationHandle<BuoyancyTuningDTO> _tuningHandle;
        private VaultGenerationHandle<BuoyancyTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<BuoyancyMaterialVolumeDTO> _materialVolumesHandle;
        private VaultGenerationHandle<BuoyancyMaterialSettlingProfileDTO> _materialSettlingProfilesHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<BuoyancyDebugForceDTO> _debugForcesHandle;
        private VaultGenerationHandle<BuoyancyCounterDTO> _countersHandle;
        private VaultGenerationHandle<BuoyancyBodyBindingDTO> _bodyBindingsHandle;
        private VaultGenerationHandle<sbyte> _sleepSdfDensityHandle;
        private VaultGenerationHandle<BuoyancySleepSdfConfigDTO> _sleepSdfConfigHandle;
        private VaultGenerationHandle<SleepStateTelemetryEntry> _sleepTelemetryRingHandle;
        private VaultGenerationHandle<int> _sleepTelemetryCursorHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdLocalPositionsHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdVelocitiesHandle;
        private VaultGenerationHandle<float> _simdDragCoefficientsHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdOutputForcesHandle;
        private VaultGenerationHandle<SimdTelemetryEntry> _simdTelemetryRingHandle;
        private VaultGenerationHandle<int> _simdTelemetryCursorHandle;
        private VaultGenerationHandle<SimdMathToleranceDTO> _simdMathTolerancesHandle;
        private VaultGenerationHandle<int> _simdVisibleIndexMaskHandle;
        private VaultGenerationHandle<int> _simdVisibleIndicesHandle;
        private VaultGenerationHandle<int> _simdVisibleCountHandle;
        private VaultGenerationHandle<SimdHydrodynamicTuningDTO> _simdHydrodynamicTuningHandle;
        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _activeStateCount;
        private IDataVault _jobGuardVault;
        private bool _jobBuffersPinned;
        private double3 _cachedSectorAup;
        private bool _jobScheduled;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _registeredOriginShiftListener;
        private bool _coldBuffersInitialized;
        private bool _coldBootCompleted;
        private bool _dumpedFault;
        private bool _coreBlackboxWarmed;
        private bool _forcePacketsReadyToDrain;

#if UNITY_EDITOR
        private static BuoyancyDisplacementRuntime _activeRuntimeInstance;

        public static bool TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null;
        }
#endif

#if UNITY_EDITOR
        /// <summary>Opens mutable editor views for buoyancy tuning, counters, telemetry, and cursor rows.</summary>
        /// <param name="tuning">Opened tuning row.</param>
        /// <param name="counters">Opened counter row.</param>
        /// <param name="telemetry">Opened buoyancy telemetry ring.</param>
        /// <param name="cursor">Opened buoyancy telemetry cursor.</param>
        /// <remarks>Editor-only cold surface; gameplay phases do not call this accessor.</remarks>
        public bool TryOpenEditorViews(
            out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
            out NativeArray<BuoyancyCounterDTO>.ReadOnly counters,
            out NativeArray<BuoyancyTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor)
        {
            NativeArray<SleepStateTelemetryEntry>.ReadOnly unused;
            return TryOpenEditorViews(out tuning, out counters, out telemetry, out unused, out cursor);
        }

        /// <summary>Opens mutable editor views for buoyancy and sleep telemetry rows.</summary>
        /// <param name="tuning">Opened tuning row.</param>
        /// <param name="counters">Opened counter row.</param>
        /// <param name="telemetry">Opened buoyancy telemetry ring.</param>
        /// <param name="sleepTelemetry">Opened sleep telemetry ring.</param>
        /// <param name="cursor">Opened telemetry cursor.</param>
        /// <remarks>Editor-only cold surface; returned buffers are not a read-only gameplay API.</remarks>
        public bool TryOpenEditorViews(
            out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
            out NativeArray<BuoyancyCounterDTO>.ReadOnly counters,
            out NativeArray<BuoyancyTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<SleepStateTelemetryEntry>.ReadOnly sleepTelemetry,
            out NativeArray<int>.ReadOnly cursor)
        {
            tuning = default;
            counters = default;
            telemetry = default;
            sleepTelemetry = default;
            cursor = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<BuoyancyTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<BuoyancyCounterDTO> counterBuffer = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<BuoyancyTelemetryEntry> telemetryBuffer = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            NativeArray<SleepStateTelemetryEntry> sleepTelemetryBuffer = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            NativeArray<int> cursorBuffer = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0 ||
                !counterBuffer.IsCreated || counterBuffer.Length <= 0 ||
                !telemetryBuffer.IsCreated || telemetryBuffer.Length <= 0 ||
                !sleepTelemetryBuffer.IsCreated || sleepTelemetryBuffer.Length <= 0 ||
                !cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer.AsReadOnly();
            counters = counterBuffer.AsReadOnly();
            telemetry = telemetryBuffer.AsReadOnly();
            sleepTelemetry = sleepTelemetryBuffer.AsReadOnly();
            cursor = cursorBuffer.AsReadOnly();
            return true;
        }

        /// <summary>Opens read-only editor views for SIMD benchmark telemetry and tolerance rows.</summary>
        /// <param name="telemetry">Opened SIMD telemetry ring.</param>
        /// <param name="cursor">Opened SIMD telemetry cursor.</param>
        /// <param name="tolerances">Opened SIMD tolerance rows.</param>
        /// <remarks>Editor-only cold surface used by Burst Vectorization X-Ray tooling.</remarks>
        public bool TryOpenSimdEditorViews(
            out NativeArray<SimdTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<SimdMathToleranceDTO>.ReadOnly tolerances)
        {
            telemetry = default;
            cursor = default;
            tolerances = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<SimdTelemetryEntry> mutableTelemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
            NativeArray<int> mutableCursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
            NativeArray<SimdMathToleranceDTO> mutableTolerances = ResolveVaultBuffer(vault, in _simdMathTolerancesHandle);
            if (!mutableTelemetry.IsCreated ||
                mutableTelemetry.Length <= 0 ||
                !mutableCursor.IsCreated ||
                mutableCursor.Length <= 0 ||
                !mutableTolerances.IsCreated ||
                mutableTolerances.Length <= 0)
            {
                return false;
            }

            telemetry = mutableTelemetry.AsReadOnly();
            cursor = mutableCursor.AsReadOnly();
            tolerances = mutableTolerances.AsReadOnly();
            return true;
        }

        /// <summary>Opens the editor-only SIMD hydrodynamic tuning row.</summary>
        /// <param name="tuning">Opened SIMD hydrodynamic tuning row.</param>
        /// <remarks>Editor-only cold surface; not used by runtime gameplay phases.</remarks>
        public bool TryOpenSimdTuningEditorView(out NativeArray<SimdHydrodynamicTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<SimdHydrodynamicTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            tuning = tuningBuffer.AsReadOnly();
            return true;
        }

        /// <summary>Opens mutable editor views for SHINOBU_249 sleep telemetry and SDF tuning.</summary>
        /// <param name="tuning">Opened buoyancy tuning row.</param>
        /// <param name="telemetry">Opened sleep telemetry ring.</param>
        /// <param name="cursor">Opened sleep telemetry cursor.</param>
        /// <param name="sdfConfig">Opened sleep SDF config row.</param>
        /// <remarks>Editor-only cold surface used by the Physics Sleep State X-Ray window.</remarks>
        public bool TryOpenSleepTelemetryEditorViews(
            out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
            out NativeArray<SleepStateTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<BuoyancySleepSdfConfigDTO>.ReadOnly sdfConfig)
        {
            tuning = default;
            telemetry = default;
            cursor = default;
            sdfConfig = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<BuoyancyTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<SleepStateTelemetryEntry> telemetryBuffer = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            NativeArray<int> cursorBuffer = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            NativeArray<BuoyancySleepSdfConfigDTO> sdfConfigBuffer = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0 ||
                !telemetryBuffer.IsCreated || telemetryBuffer.Length <= 0 ||
                !cursorBuffer.IsCreated || cursorBuffer.Length <= 0 ||
                !sdfConfigBuffer.IsCreated || sdfConfigBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer.AsReadOnly();
            telemetry = telemetryBuffer.AsReadOnly();
            cursor = cursorBuffer.AsReadOnly();
            sdfConfig = sdfConfigBuffer.AsReadOnly();
            return true;
        }

        public bool TryApplyEditorTuning(in BuoyancyTuningDTO tuning)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<BuoyancyTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            BuoyancyTuningDTO value = tuning;
            value.GlobalQualityWeight = math.saturate(math.select(1f, value.GlobalQualityWeight, math.isfinite(value.GlobalQualityWeight)));
            value.WaterDensityKgPerM3 = math.clamp(math.select(BuoyancyDisplacementConstants.DefaultWaterDensityKgPerM3, value.WaterDensityKgPerM3, math.isfinite(value.WaterDensityKgPerM3)), 1f, 2000f);
            value.GravityMetersPerSecondSq = math.max(0f, math.select(BuoyancyDisplacementConstants.DefaultGravityMetersPerSecondSq, value.GravityMetersPerSecondSq, math.isfinite(value.GravityMetersPerSecondSq)));
            value.LinearDragCoefficient = math.max(0f, math.select(0f, value.LinearDragCoefficient, math.isfinite(value.LinearDragCoefficient)));
            value.QuadraticDragCoefficient = math.max(0f, math.select(0f, value.QuadraticDragCoefficient, math.isfinite(value.QuadraticDragCoefficient)));
            value.SurfaceDampening = math.saturate(math.select(0f, value.SurfaceDampening, math.isfinite(value.SurfaceDampening)));
            value.FlowForceCoefficient = math.max(0f, math.select(0f, value.FlowForceCoefficient, math.isfinite(value.FlowForceCoefficient)));
            value.ActiveStateCount = math.clamp(value.ActiveStateCount, 0, BuoyancyDisplacementConstants.StateCapacity);
            tuningBuffer[0] = value;
            return true;
        }

        public bool TryApplySleepTelemetryEditorTuning(float sleepSpeedSq, int restFrames, float currentStirThreshold)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<BuoyancyTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<BuoyancySleepSdfConfigDTO> configBuffer = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0 ||
                !configBuffer.IsCreated || configBuffer.Length <= 0)
            {
                return false;
            }

            BuoyancyTuningDTO tuningValue = tuningBuffer[0];
            tuningValue.SleepSpeedSq = math.max(0.000001f, math.select(0.000001f, sleepSpeedSq, math.isfinite(sleepSpeedSq)));
            tuningBuffer[0] = tuningValue;

            BuoyancySleepSdfConfigDTO configValue = configBuffer[0];
            int safeRestFrames = math.clamp(restFrames, 1, 255);
            uint packedRestFrames = (uint)safeRestFrames << BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideShift;
            configValue.Flags = (configValue.Flags & ~BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideMask) | packedRestFrames | BuoyancyDisplacementConstants.FlagActive;
            float stir = math.max(0.0001f, math.select(0.0001f, currentStirThreshold, math.isfinite(currentStirThreshold)));
            configValue.AmbientStirThresholdSq = stir * stir;
            configBuffer[0] = configValue;
            return true;
        }

        public bool TryApplySimdScalarFallbackEditorTuning(float scalarFallbackWeight01)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<SimdHydrodynamicTuningDTO> tuningBuffer = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            SimdHydrodynamicTuningDTO value = tuningBuffer[0];
            value.ScalarFallbackWeight01 = math.saturate(math.select(0f, scalarFallbackWeight01, math.isfinite(scalarFallbackWeight01)));
            value.Flags = SimdVectorizationConstants.FlagActive;
            tuningBuffer[0] = value;
            return true;
        }
#endif

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;
#endif

            RefreshColdDependencies();
            EnsureColdBooted();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;
#endif

            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            EnsureColdBooted();
            WarmCoreBlackboxRoute();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingSolverForTeardown();
            _forcePacketsReadyToDrain = false;
            _coreBlackboxWarmed = false;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;
#endif
            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingSolverForTeardown();
            ReleaseVaultHandles(_dataVault);
            _coreBlackboxWarmed = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying || !math.isfinite(fixedDeltaTime) || fixedDeltaTime <= 0f || _jobScheduled || _forcePacketsReadyToDrain)
                return;

            float safeFixedDeltaTime = math.clamp(fixedDeltaTime, 0.0001f, 0.2f);
            if (!TryPrepareRuntimeVault(out IDataVault vault))
                return;

            if (!TryLockJobBuffers(vault))
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveRuntimeBuffers(
                        vault,
                        out NativeArray<BuoyancyStateDTO> states,
                        out NativeArray<BuoyancyForcePacketDTO> forcePackets,
                        out NativeArray<BuoyancyFlowSampleDTO> flowSamples,
                        out NativeArray<BuoyancyTuningDTO> tuning,
                        out NativeArray<BuoyancyTelemetryEntry> telemetry,
                        out NativeArray<int> telemetryCursor,
                        out NativeArray<SleepStateTelemetryEntry> sleepTelemetry,
                        out NativeArray<int> sleepTelemetryCursor,
                        out NativeArray<sbyte> sleepSdfDensity,
                        out NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig,
                        out NativeArray<BuoyancyMaterialSettlingProfileDTO> materialSettlingProfiles,
                        out NativeArray<BuoyancyDebugForceDTO> debugForces,
                        out NativeArray<BuoyancyCounterDTO> counters))
                {
                    return;
                }

                BuoyancyTuningDTO tuningDto = tuning[0];
                float quality = ResolveGlobalQualityWeight(ref tuningDto);
                tuningDto.SectorAUP = ResolveCachedSectorAUP();
                tuningDto.ResolvedQualityWeight = quality;
                tuningDto.SimulationTickDelta = safeFixedDeltaTime;
                tuningDto.FrameIndex = _simulationFrame;
                tuning[0] = tuningDto;

                int authoredActiveCount = math.select(_stateCapacity, tuningDto.ActiveStateCount, tuningDto.ActiveStateCount > 0);
                _activeStateCount = math.clamp(authoredActiveCount, 0, states.Length);
                if (_activeStateCount <= 0)
                {
                    UnlockJobBuffers();
                    WriteCompletedSimdUtilizationTelemetry(0f);
                    return;
                }

                if (!PhysicsApplySystem.TryPrepareBuoyancyForcePackets(forcePackets, counters))
                    return;

                NativeArray<WakeRequestSignal>.ReadOnly wakeRequests = SignalBus<WakeRequestSignal>.GetFrameSnapshotArray();
                int wakeRequestCount = wakeRequests.IsCreated ? wakeRequests.Length : 0;
                JobHandle sleepPrepassHandle = default;
                if (wakeRequestCount > 0)
                {
                    ProcessBuoyancyWakeTriggersJob wakeJob = new ProcessBuoyancyWakeTriggersJob
                    {
                        States = states,
                        WakeRequests = wakeRequests,
                        StateCount = _activeStateCount,
                        WakeRequestCount = wakeRequestCount
                    };
                    sleepPrepassHandle = wakeJob.Schedule(_activeStateCount, 64);
                }

                int currentPollCadence = ResolveAmbientCurrentPollCadence(quality);
                if ((_simulationFrame % (uint)currentPollCadence) == 0u)
                {
                    BuoyancySleepSdfConfigDTO sdfConfig = sleepSdfConfig.Length > 0
                        ? sleepSdfConfig[0]
                        : BuoyancySleepSdfConfigDTO.Default();
                    PollAmbientCurrentsJob currentWakeJob = new PollAmbientCurrentsJob
                    {
                        States = states,
                        FlowSamples = flowSamples,
                        StateCount = _activeStateCount,
                        FlowSampleCount = flowSamples.Length,
                        StirThresholdSq = sdfConfig.AmbientStirThresholdSq,
                        SimulationFrame = _simulationFrame
                    };
                    sleepPrepassHandle = currentWakeJob.Schedule(_activeStateCount, 64, sleepPrepassHandle);
                }

                int stride = ResolveEvaluationStride(quality);
                int evaluationOffset = math.select((int)(_simulationFrame % (uint)stride), 0, stride == 1);
                int scheduledEvaluationCount = ResolveScheduledEvaluationCount(_activeStateCount, stride, evaluationOffset);
                _scheduleTimestamp = Stopwatch.GetTimestamp();
                if (scheduledEvaluationCount <= 0)
                {
                    ReduceBuoyancyTelemetryJob emptyReduceJob = new ReduceBuoyancyTelemetryJob
                    {
                        DebugForces = debugForces,
                        Counters = counters,
                        TelemetryRing = telemetry,
                        TelemetryCursor = telemetryCursor,
                        SleepTelemetryRing = sleepTelemetry,
                        SleepTelemetryCursor = sleepTelemetryCursor,
                        ActiveStateCount = _activeStateCount,
                        WakeRequestCount = wakeRequestCount,
                        SimulationFrame = _simulationFrame,
                        GlobalQualityWeight = quality,
                        SleepEnergyThreshold = tuningDto.SleepSpeedSq,
                        ComputeMicros = 0f
                    };
                    _pendingHandle = emptyReduceJob.Schedule(sleepPrepassHandle);
                    _jobScheduled = true;
                    scheduled = true;
                    return;
                }

                BuoyancySleepSdfConfigDTO sleepConfig = sleepSdfConfig.Length > 0
                    ? sleepSdfConfig[0]
                    : BuoyancySleepSdfConfigDTO.Default();
                EvaluateBuoyancyJob evaluateJob = new EvaluateBuoyancyJob
                {
                    States = states,
                    StateCount = states.Length,
                    FlowSamples = flowSamples,
                    FlowSampleCount = flowSamples.Length,
                    SleepSdfDensity = sleepSdfDensity,
                    MaterialSettlingProfiles = materialSettlingProfiles,
                    MaterialSettlingProfileCount = materialSettlingProfiles.Length,
                    SleepSdfConfig = sleepConfig,
                    Tuning = tuningDto,
                    DebugForces = debugForces,
                    DebugForceCount = debugForces.Length,
                    ForcePackets = forcePackets,
                    ForcePacketCount = forcePackets.Length,
                    ForcePacketWriteEnabled = 1,
                    ActiveStateCount = _activeStateCount,
                    EvaluationStride = stride,
                    EvaluationOffset = evaluationOffset,
                    SimulationFrame = _simulationFrame,
                    SimulationTickDelta = safeFixedDeltaTime,
                    GlobalQualityWeight = quality
                };

                JobHandle evaluateHandle = evaluateJob.Schedule(scheduledEvaluationCount, 64, sleepPrepassHandle);
                CompactBuoyancyForcePacketsJob compactForcePacketsJob = new CompactBuoyancyForcePacketsJob
                {
                    ForcePackets = forcePackets,
                    Counters = counters,
                    CandidateCount = scheduledEvaluationCount
                };
                JobHandle compactHandle = compactForcePacketsJob.Schedule(evaluateHandle);
                ReduceBuoyancyTelemetryJob reduceJob = new ReduceBuoyancyTelemetryJob
                {
                    DebugForces = debugForces,
                    Counters = counters,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    SleepTelemetryRing = sleepTelemetry,
                    SleepTelemetryCursor = sleepTelemetryCursor,
                    ActiveStateCount = _activeStateCount,
                    WakeRequestCount = wakeRequestCount,
                    SimulationFrame = _simulationFrame,
                    GlobalQualityWeight = quality,
                    SleepEnergyThreshold = tuningDto.SleepSpeedSq,
                    ComputeMicros = 0f
                };
                _pendingHandle = reduceJob.Schedule(compactHandle);
                _jobScheduled = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    UnlockJobBuffers();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying)
                return;

            TryFinalizePendingSolverNoWait();
            if (!_forcePacketsReadyToDrain)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !HasHandle(in _forcePacketsHandle) ||
                !HasHandle(in _countersHandle) ||
                !HasHandle(in _bodyBindingsHandle))
            {
                _forcePacketsReadyToDrain = false;
                return;
            }

            if (!TryAcquireBuoyancyMutationGuard(vault, ForceDrainMutationGuardMask))
                return;

            try
            {
                NativeArray<BuoyancyForcePacketDTO> forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
                NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                NativeArray<BuoyancyBodyBindingDTO> bodyBindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
                PhysicsApplySystem.DrainBuoyancyForcePackets(
                    forcePackets,
                    counters,
                    bodyBindings,
                    BuoyancyDisplacementConstants.ForceQueueSoftCapacity,
                    out _,
                    out _);
                _forcePacketsReadyToDrain = false;
            }
            finally
            {
                vault.ReleaseMutationGuard(ForceDrainMutationGuardMask);
            }
        }

        public void LateFrameTick()
        {
            if (!_jobScheduled)
                return;

            TryFinalizePendingSolverNoWait();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FloatingOriginRuntime)
            {
                RefreshCachedSectorAUP();
                RefreshOriginShiftListenerRegistration();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingSolverForTeardown();
            IDataVault previousVault = (previousService as IDataVault) ?? _dataVault;
            IDataVault currentVault = currentService as IDataVault;
            if (!ReferenceEquals(previousVault, currentVault))
                ReleaseVaultHandles(previousVault);
            _dataVault = currentVault;
            _coreBlackboxWarmed = false;
            if (!HandlesReady() &&
                currentVault != null &&
                !currentVault.IsCompactionFenceActive &&
                !currentVault.IsAllocationLocked)
            {
                EnsureColdBooted();
            }
            if (currentVault != null)
                WarmCoreBlackboxRoute();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _cachedSectorAup = math.select(double3.zero, shiftData.NewTotalOffsetDouble, math.isfinite(shiftData.NewTotalOffsetDouble));
        }

        public bool GenerateMockBuoyantObjects()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (!TryAcquireBuoyancyMutationGuard(vault, MockSeedMutationGuardMask))
                return false;

            try
            {
                NativeArray<BuoyancyStateDTO> states = ResolveVaultBuffer(vault, in _statesHandle);
                NativeArray<BuoyancyDebugForceDTO> debugForces = ResolveVaultBuffer(vault, in _debugForcesHandle);
                NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
                if (!states.IsCreated || !debugForces.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                    return false;

                BuoyancyTuningDTO tuningDto = tuning[0];
                int authoredMockCount = math.select(
                    BuoyancyDisplacementConstants.MockObjectCount,
                    tuningDto.MockStateCount,
                    tuningDto.MockStateCount > 0);
                int mockCount = math.clamp(authoredMockCount, 1, math.min(states.Length, BuoyancyDisplacementConstants.MockObjectCount));
                GenerateMockBuoyantObjectsJob job = new GenerateMockBuoyantObjectsJob
                {
                    States = states,
                    DebugForces = debugForces,
                    StateCount = states.Length,
                    DebugForceCount = debugForces.Length,
                    ActiveMockCount = mockCount,
                    SurfaceAUP = tuningDto.OceanSurfaceAUP,
                    SimulationFrame = _simulationFrame
                };
                JobHandle handle = job.Schedule(states.Length, 64);
                // COLD/EDITOR BLOCKING SYNC: emergency mock seeding is a boot/tuner path, not a frame-loop solver fence.
                if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                    return false;

                tuningDto.ActiveStateCount = math.max(tuningDto.ActiveStateCount, mockCount);
                tuningDto.MockStateCount = mockCount;
                tuning[0] = tuningDto;
                _activeStateCount = mockCount;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(MockSeedMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        // EDITOR/MANUAL BLOCKING SYNC: this benchmark is invoked by the X-Ray window only.
        // It intentionally completes jobs for measured microsecond samples and is never called from FixedTick.
        public bool GenerateMockSimdBenchmark()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (!TryAcquireBuoyancyMutationGuard(vault, SimdBenchmarkMutationGuardMask))
                return false;

            bool succeeded = false;
            bool shouldDumpFault = false;
            float faultScalar = 0f;
            uint faultHash = 0u;
            try
            {
            NativeArray<SimdFloat3Padded> positions = ResolveVaultBuffer(vault, in _simdLocalPositionsHandle);
            NativeArray<SimdFloat3Padded> velocities = ResolveVaultBuffer(vault, in _simdVelocitiesHandle);
            NativeArray<float> dragCoefficients = ResolveVaultBuffer(vault, in _simdDragCoefficientsHandle);
            NativeArray<SimdFloat3Padded> outputForces = ResolveVaultBuffer(vault, in _simdOutputForcesHandle);
            NativeArray<SimdTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
            NativeArray<SimdHydrodynamicTuningDTO> benchmarkTuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
            if (!positions.IsCreated ||
                !velocities.IsCreated ||
                !dragCoefficients.IsCreated ||
                !outputForces.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                !benchmarkTuning.IsCreated ||
                benchmarkTuning.Length <= 0)
            {
                return false;
            }

            int count = math.min(
                BuoyancyDisplacementConstants.SimdBenchmarkCapacity,
                math.min(positions.Length, math.min(velocities.Length, math.min(dragCoefficients.Length, outputForces.Length))));
            if (count <= 0)
                return false;
            int laneCount = (count + SimdVectorizationConstants.HydrodynamicsLaneWidth - 1) /
                            SimdVectorizationConstants.HydrodynamicsLaneWidth;

            SimdHydrodynamicTuningDTO tuningValue = PrepareBenchmarkSimdTuning(benchmarkTuning, _simulationFrame);
            float scalarMicros = 0f;
            float scalarProbeWeight = math.saturate(math.select(
                0f,
                tuningValue.ScalarFallbackWeight01,
                math.isfinite(tuningValue.ScalarFallbackWeight01)));

            GenerateMockSimdBenchmarkJob generateJob = new GenerateMockSimdBenchmarkJob
            {
                LocalPositions = positions,
                Velocities = velocities,
                DragCoefficients = dragCoefficients,
                Count = count,
                Seed = 0x2015A11Du,
                FrameIndex = _simulationFrame
            };
            JobHandle handle = generateJob.Schedule(count, 128);
            int scalarProbeCount = math.clamp((int)math.round(count * scalarProbeWeight), 0, count);
            if (scalarProbeCount > 0)
            {
                if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                    return false;

                long scalarStart = Stopwatch.GetTimestamp();
                ScalarHydrodynamicsReferenceJob scalarJob = new ScalarHydrodynamicsReferenceJob
                {
                    LocalPositions = positions,
                    Velocities = velocities,
                    DragCoefficients = dragCoefficients,
                    OutputForces = outputForces,
                    Tuning = tuningValue,
                    Count = scalarProbeCount
                };
                JobHandle scalarHandle = scalarJob.Schedule();
                if (!DispatcherJobFence.TryComplete(ref scalarHandle, forceComplete: true))
                    return false;

                float scalarScale = count * math.rcp(math.max(1, scalarProbeCount));
                float rawScalarMicros = ResolveElapsedMicros(scalarStart) * scalarScale;
                scalarMicros = rawScalarMicros;

                generateJob.FrameIndex = _simulationFrame;
                handle = generateJob.Schedule(count, 128);
            }

            long start = Stopwatch.GetTimestamp();
            VectorizedHydrodynamicsLane4Job hydroJob = new VectorizedHydrodynamicsLane4Job
            {
                LocalPositions = positions,
                Velocities = velocities,
                DragCoefficients = dragCoefficients,
                OutputForces = outputForces,
                Tuning = tuningValue,
                Count = count
            };
            handle = hydroJob.Schedule(laneCount, 64, handle);
            if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                return false;

            float vectorMicros = ResolveElapsedMicros(start);
            float effectiveMaxSpeed = math.max(0f, math.select(0f, tuningValue.MaxSpeed, math.isfinite(tuningValue.MaxSpeed)));
            float effectiveMaxSpeedSq = effectiveMaxSpeed * effectiveMaxSpeed;
            RecordSimdTelemetryJob telemetryJob = new RecordSimdTelemetryJob
            {
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                FrameIndex = _simulationFrame,
                KernelHash = SimdVectorizationConstants.HydrodynamicsKernelHash,
                EntityCount = count,
                VectorMicros = vectorMicros,
                ScalarMicros = scalarMicros,
                GlobalQualityWeight = tuningValue.GlobalQualityWeight,
                StateHash = (uint)count ^ _simulationFrame ^ SimdVectorizationConstants.HydrodynamicsKernelHash,
                MaxApproximationError = tuningValue.MaxApproximationError,
                MaxSpeedSq = math.select(0f, effectiveMaxSpeedSq, math.isfinite(effectiveMaxSpeedSq))
            };
            JobHandle telemetryHandle = telemetryJob.Schedule();
            if (!DispatcherJobFence.TryComplete(ref telemetryHandle, forceComplete: true))
                return false;

            if (ResolveSimdThroughputDrop(vectorMicros, scalarMicros) > 0.5f ||
                !math.isfinite(vectorMicros) ||
                !math.isfinite(scalarMicros))
            {
                shouldDumpFault = TryComposeSimdFaultPayload(telemetry, cursor, out faultScalar, out faultHash);
            }
            succeeded = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(SimdBenchmarkMutationGuardMask);
            }

            if (shouldDumpFault)
                PushSimdFaultEvent(faultScalar, faultHash);

            return succeeded;
        }
#endif

#if UNITY_EDITOR
        public bool TryLoadMaterialVolumesCsv()
        {
            // COLD TUNING PATH: editor import stages outside DataVault, then commits one target buffer.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                string path = ResolveProjectPath(_csvRelativePath);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                int bytesRead = ReadFileIntoColdScratch(path, s_csvImportScratch);
                if (bytesRead <= 0)
                    return false;

                ReadOnlySpan<byte> span = s_csvImportScratch.AsSpan(0, math.min(bytesRead, s_csvImportScratch.Length));
                Span<BuoyancyMaterialVolumeDTO> materialVolumeScratch = s_materialVolumeImportScratch.AsSpan();
                if (!BuoyancyMaterialVolumeCsvParser.TryApply(span, materialVolumeScratch, out _))
                    return false;

                return TryCommitMaterialVolumeCsv(vault, materialVolumeScratch);
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        public bool TryLoadMaterialSettlingProfilesCsv()
        {
            // COLD TUNING PATH: editor import stages outside DataVault, then commits one target buffer.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                string path = ResolveProjectPath(_materialSettlingProfilesCsvRelativePath);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                int bytesRead = ReadFileIntoColdScratch(path, s_csvImportScratch);
                if (bytesRead <= 0)
                    return false;

                ReadOnlySpan<byte> span = s_csvImportScratch.AsSpan(0, math.min(bytesRead, s_csvImportScratch.Length));
                Span<BuoyancyMaterialSettlingProfileDTO> materialSettlingScratch = s_materialSettlingImportScratch.AsSpan();
                if (!BuoyancyMaterialSettlingProfileCsvParser.TryApply(span, materialSettlingScratch, out _))
                    return false;

                return TryCommitMaterialSettlingCsv(vault, materialSettlingScratch);
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        public bool TryLoadSimdMathTolerancesCsv()
        {
            // COLD TUNING PATH: editor import stages outside DataVault, then commits tolerance rows and tuning separately.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                string path = ResolveProjectPath(BuoyancyDisplacementConstants.SimdToleranceCsvRelativePath);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                int bytesRead = ReadFileIntoColdScratch(path, s_csvImportScratch);
                if (bytesRead <= 0)
                    return false;

                ReadOnlySpan<byte> span = s_csvImportScratch.AsSpan(0, math.min(bytesRead, s_csvImportScratch.Length));
                Span<SimdMathToleranceDTO> simdToleranceScratch = s_simdToleranceImportScratch.AsSpan();
                if (!SimdToleranceCsvParser.TryApply(span, simdToleranceScratch, out int toleranceRows))
                    return false;

                if (!TryCommitSimdMathTolerances(vault, simdToleranceScratch))
                    return false;

                return TryCommitSimdToleranceTuning(vault, simdToleranceScratch, toleranceRows);
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        private bool TryCommitMaterialVolumeCsv(IDataVault vault, ReadOnlySpan<BuoyancyMaterialVolumeDTO> staged)
        {
            if (vault == null || staged.Length <= 0 || !TryAcquireBuoyancyMutationGuard(vault, MaterialVolumeCsvMutationGuardMask))
                return false;

            try
            {
                NativeArray<BuoyancyMaterialVolumeDTO> table = ResolveVaultBuffer(vault, in _materialVolumesHandle);
                if (!table.IsCreated || table.Length != staged.Length)
                    return false;

                fixed (BuoyancyMaterialVolumeDTO* source = staged)
                {
                    void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(table);
                    long byteCount = (long)staged.Length * UnsafeUtility.SizeOf<BuoyancyMaterialVolumeDTO>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(target, byteCount, source, byteCount))
                        return false;
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(MaterialVolumeCsvMutationGuardMask);
            }
        }

        private bool TryCommitMaterialSettlingCsv(IDataVault vault, ReadOnlySpan<BuoyancyMaterialSettlingProfileDTO> staged)
        {
            if (vault == null || staged.Length <= 0 || !TryAcquireBuoyancyMutationGuard(vault, MaterialSettlingCsvMutationGuardMask))
                return false;

            try
            {
                NativeArray<BuoyancyMaterialSettlingProfileDTO> table = ResolveVaultBuffer(vault, in _materialSettlingProfilesHandle);
                if (!table.IsCreated || table.Length != staged.Length)
                    return false;

                fixed (BuoyancyMaterialSettlingProfileDTO* source = staged)
                {
                    void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(table);
                    long byteCount = (long)staged.Length * UnsafeUtility.SizeOf<BuoyancyMaterialSettlingProfileDTO>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(target, byteCount, source, byteCount))
                        return false;
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(MaterialSettlingCsvMutationGuardMask);
            }
        }

        private bool TryCommitSimdMathTolerances(IDataVault vault, ReadOnlySpan<SimdMathToleranceDTO> staged)
        {
            if (vault == null || staged.Length <= 0 || !TryAcquireBuoyancyMutationGuard(vault, SimdToleranceCsvMutationGuardMask))
                return false;

            try
            {
                NativeArray<SimdMathToleranceDTO> tolerances = ResolveVaultBuffer(vault, in _simdMathTolerancesHandle);
                if (!tolerances.IsCreated || tolerances.Length != staged.Length)
                    return false;

                fixed (SimdMathToleranceDTO* source = staged)
                {
                    void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tolerances);
                    long byteCount = (long)staged.Length * UnsafeUtility.SizeOf<SimdMathToleranceDTO>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(target, byteCount, source, byteCount))
                        return false;
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(SimdToleranceCsvMutationGuardMask);
            }
        }

        private bool TryCommitSimdToleranceTuning(IDataVault vault, ReadOnlySpan<SimdMathToleranceDTO> staged, int toleranceRows)
        {
            if (vault == null || staged.Length <= 0 || toleranceRows <= 0 || !TryAcquireBuoyancyMutationGuard(vault, SimdToleranceTuningMutationGuardMask))
                return false;

            try
            {
                NativeArray<SimdHydrodynamicTuningDTO> tuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return false;

                ApplySimdToleranceTuning(staged, toleranceRows, tuning);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(SimdToleranceTuningMutationGuardMask);
            }
        }
#endif

        private void RefreshColdDependencies()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private bool EnsureColdBooted()
        {
            if (_coldBootCompleted)
                return true;

            RefreshColdDependencies();
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (!OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            SeedDefaultTuningIfNeeded();
            InitializeColdBuffersIfNeeded();
#if UNITY_EDITOR
            if (_loadCsvOnEnable)
                TryLoadMaterialVolumesCsv();
            if (_loadMaterialSettlingProfilesOnEnable)
                TryLoadMaterialSettlingProfilesCsv();
            if (_loadSimdTolerancesOnEnable)
                TryLoadSimdMathTolerancesCsv();
#endif
            if (_seedEmergencyMockObjects && ShouldSeedEmergencyMock())
                GenerateMockBuoyantObjects();

            _coldBootCompleted = true;
            return true;
        }

        private bool ShouldSeedEmergencyMock()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _tuningHandle))
                return false;

            NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return false;

            BuoyancyTuningDTO tuningDto = tuning[0];
            return tuningDto.ActiveStateCount <= 0;
        }

        private bool OpenOrAcquireVaultBuffersForOwnerRoute()
        {
            if (_dataVault == null)
                RefreshColdDependencies();
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int stateCapacity = math.clamp(_stateCapacity, 1, BuoyancyDisplacementConstants.StateCapacity);
            int flowCapacity = math.clamp(_flowSampleCapacity, 0, BuoyancyDisplacementConstants.FlowSampleCapacity);
            return OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _statesHandle, BuoyancyDisplacementBufferIds.States, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _forcePacketsHandle, BuoyancyDisplacementBufferIds.ForcePackets, BuoyancyDisplacementConstants.ForceQueueSoftCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _flowSamplesHandle, BuoyancyDisplacementBufferIds.FlowSamples, math.max(1, flowCapacity), NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _tuningHandle, BuoyancyDisplacementBufferIds.Tuning, BuoyancyDisplacementConstants.TuningCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _telemetryRingHandle, BuoyancyDisplacementBufferIds.TelemetryRing, BuoyancyDisplacementConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _telemetryCursorHandle, BuoyancyDisplacementBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _sleepTelemetryRingHandle, BuoyancyDisplacementBufferIds.SleepTelemetryRing, BuoyancyDisplacementConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _sleepTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SleepTelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _sleepSdfDensityHandle, BuoyancyDisplacementBufferIds.SleepSdfDensity, BuoyancyDisplacementConstants.SleepSdfCellCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _sleepSdfConfigHandle, BuoyancyDisplacementBufferIds.SleepSdfConfig, 1, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _materialVolumesHandle, BuoyancyDisplacementBufferIds.MaterialVolumes, BuoyancyDisplacementConstants.MaterialVolumeCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _materialSettlingProfilesHandle, BuoyancyDisplacementBufferIds.MaterialSettlingProfiles, BuoyancyDisplacementConstants.MaterialSettlingProfileCapacity, NativeArrayOptions.UninitializedMemory) &&
#if UNITY_EDITOR
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _csvScratchHandle, BuoyancyDisplacementBufferIds.CsvScratch, BuoyancyDisplacementConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory) &&
#endif
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _debugForcesHandle, BuoyancyDisplacementBufferIds.DebugForces, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _countersHandle, BuoyancyDisplacementBufferIds.Counters, BuoyancyDisplacementConstants.CounterCapacity, NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _bodyBindingsHandle, BuoyancyDisplacementBufferIds.BodyBindings, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdLocalPositionsHandle, BuoyancyDisplacementBufferIds.SimdLocalPositions, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdVelocitiesHandle, BuoyancyDisplacementBufferIds.SimdVelocities, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdDragCoefficientsHandle, BuoyancyDisplacementBufferIds.SimdDragCoefficients, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdOutputForcesHandle, BuoyancyDisplacementBufferIds.SimdOutputForces, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdTelemetryRingHandle, BuoyancyDisplacementBufferIds.SimdTelemetryRing, BuoyancyDisplacementConstants.SimdTelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SimdTelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdMathTolerancesHandle, BuoyancyDisplacementBufferIds.SimdMathTolerances, BuoyancyDisplacementConstants.SimdToleranceCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdVisibleIndexMaskHandle, BuoyancyDisplacementBufferIds.SimdVisibleIndexMask, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdVisibleIndicesHandle, BuoyancyDisplacementBufferIds.SimdVisibleIndices, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdVisibleCountHandle, BuoyancyDisplacementBufferIds.SimdVisibleCount, 1, NativeArrayOptions.ClearMemory) &&
                   OpenOrAcquireVaultDescriptorForOwnerRoute(vault, ref _simdHydrodynamicTuningHandle, BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning, BuoyancyDisplacementConstants.SimdHydrodynamicTuningCapacity, NativeArrayOptions.ClearMemory) &&
                   HandlesReady(vault);
        }

        private bool HandlesReady()
        {
            IDataVault vault = _dataVault;
            return vault != null && HandlesReady(vault);
        }

        private bool HandlesReady(IDataVault vault)
        {
            return vault != null &&
                   HasHandle(in _statesHandle) &&
                   HasHandle(in _forcePacketsHandle) &&
                   HasHandle(in _flowSamplesHandle) &&
                   HasHandle(in _tuningHandle) &&
                   HasHandle(in _telemetryRingHandle) &&
                   HasHandle(in _telemetryCursorHandle) &&
                   HasHandle(in _sleepTelemetryRingHandle) &&
                   HasHandle(in _sleepTelemetryCursorHandle) &&
                   HasHandle(in _sleepSdfDensityHandle) &&
                   HasHandle(in _sleepSdfConfigHandle) &&
                   HasHandle(in _materialVolumesHandle) &&
                   HasHandle(in _materialSettlingProfilesHandle) &&
#if UNITY_EDITOR
                   HasHandle(in _csvScratchHandle) &&
#endif
                   HasHandle(in _debugForcesHandle) &&
                   HasHandle(in _countersHandle) &&
                   HasHandle(in _bodyBindingsHandle) &&
                   HasHandle(in _simdLocalPositionsHandle) &&
                   HasHandle(in _simdVelocitiesHandle) &&
                   HasHandle(in _simdDragCoefficientsHandle) &&
                   HasHandle(in _simdOutputForcesHandle) &&
                   HasHandle(in _simdTelemetryRingHandle) &&
                   HasHandle(in _simdTelemetryCursorHandle) &&
                   HasHandle(in _simdMathTolerancesHandle) &&
                   HasHandle(in _simdVisibleIndexMaskHandle) &&
                   HasHandle(in _simdVisibleIndicesHandle) &&
                   HasHandle(in _simdVisibleCountHandle) &&
                   HasHandle(in _simdHydrodynamicTuningHandle) &&
                   BuoyancyDisplacementLayout.Validate() &&
                   SimdVectorizationLayout.Validate();
        }

        private bool TryPrepareRuntimeVault(out IDataVault vault)
        {
            vault = _dataVault;
            if (!_coldBootCompleted || vault == null)
                return false;

            return HandlesReady(vault);
        }

        private static bool OpenOrAcquireVaultDescriptorForOwnerRoute<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (HasHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (TryAdoptExistingVaultDescriptor(vault, bufferId, requiredLength, ref handle))
                return true;

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Physics, options);
            return HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private static bool TryAdoptExistingVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                HasHandle(in existingHandle) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existingHandle;
                return true;
            }

            return false;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return vault != null &&
                   HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static NativeArray<T> ResolvePhysicsVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return vault != null &&
                   HasPhysicsHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool HasPhysicsHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.Physics &&
                   handle.Generation != 0u;
        }

        private void SeedDefaultTuningIfNeeded()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            BuoyancyTuningDTO value = tuning[0];
            if (!math.isfinite(value.WaterDensityKgPerM3) ||
                !math.isfinite(value.GravityMetersPerSecondSq) ||
                !math.isfinite(value.LinearDragCoefficient) ||
                !math.isfinite(value.QuadraticDragCoefficient) ||
                !math.isfinite(value.SurfaceDampening) ||
                !math.isfinite(value.GlobalQualityWeight) ||
                !math.isfinite(value.ResolvedQualityWeight) ||
                !math.isfinite(value.SimulationTickDelta) ||
                value.WaterDensityKgPerM3 < 900f ||
                value.WaterDensityKgPerM3 > 1160f ||
                value.GravityMetersPerSecondSq <= BuoyancyDisplacementConstants.Epsilon ||
                value.GravityMetersPerSecondSq > 40f ||
                value.LinearDragCoefficient < 0f ||
                value.QuadraticDragCoefficient < 0f ||
                value.SurfaceDampening < 0f ||
                value.SurfaceDampening > 1f ||
                value.GlobalQualityWeight < 0f ||
                value.GlobalQualityWeight > 1f ||
                value.ResolvedQualityWeight < 0f ||
                value.ResolvedQualityWeight > 1f ||
                value.SimulationTickDelta <= 0f ||
                value.SimulationTickDelta > 0.2f ||
                value.ActiveStateCount < 0 ||
                value.ActiveStateCount > BuoyancyDisplacementConstants.StateCapacity ||
                value.MockStateCount < 0 ||
                value.MockStateCount > BuoyancyDisplacementConstants.MockObjectCount ||
                value.MinFluidDensityKgPerM3 <= 0f ||
                value.MaxFluidDensityKgPerM3 <= value.MinFluidDensityKgPerM3)
            {
                tuning[0] = BuoyancyTuningDTO.Default();
            }
        }

        private void InitializeColdBuffersIfNeeded()
        {
            if (_coldBuffersInitialized)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<BuoyancyFlowSampleDTO> flowSamples = ResolveVaultBuffer(vault, in _flowSamplesHandle);
            NativeArray<BuoyancyTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<SleepStateTelemetryEntry> sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            NativeArray<int> sleepTelemetryCursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            NativeArray<sbyte> sleepSdfDensity = ResolveVaultBuffer(vault, in _sleepSdfDensityHandle);
            NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            NativeArray<BuoyancyMaterialVolumeDTO> materials = ResolveVaultBuffer(vault, in _materialVolumesHandle);
            NativeArray<BuoyancyMaterialSettlingProfileDTO> settlingProfiles = ResolveVaultBuffer(vault, in _materialSettlingProfilesHandle);
            NativeArray<BuoyancyDebugForceDTO> debug = ResolveVaultBuffer(vault, in _debugForcesHandle);
            NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<BuoyancyBodyBindingDTO> bindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
            if (!flowSamples.IsCreated ||
                !telemetry.IsCreated ||
                !telemetryCursor.IsCreated ||
                !sleepTelemetry.IsCreated ||
                !sleepTelemetryCursor.IsCreated ||
                !sleepSdfDensity.IsCreated ||
                !sleepSdfConfig.IsCreated ||
                !materials.IsCreated ||
                !settlingProfiles.IsCreated ||
                !debug.IsCreated ||
                !counters.IsCreated ||
                !bindings.IsCreated)
            {
                return;
            }

            InitializeBuoyancyColdBuffersJob job = new InitializeBuoyancyColdBuffersJob
            {
                FlowSamples = flowSamples,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                SleepTelemetryRing = sleepTelemetry,
                SleepTelemetryCursor = sleepTelemetryCursor,
                SleepSdfDensity = sleepSdfDensity,
                SleepSdfConfig = sleepSdfConfig,
                MaterialVolumes = materials,
                MaterialSettlingProfiles = settlingProfiles,
                DebugForces = debug,
                Counters = counters,
                BodyBindings = bindings
            };
            // COLD BOOT DIRECT CLEAR: clears Vault-owned buffers once before steady-state scheduling.
            job.Execute();

            _coldBuffersInitialized = true;
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<BuoyancyStateDTO> states,
            out NativeArray<BuoyancyForcePacketDTO> forcePackets,
            out NativeArray<BuoyancyFlowSampleDTO> flowSamples,
            out NativeArray<BuoyancyTuningDTO> tuning,
            out NativeArray<BuoyancyTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<SleepStateTelemetryEntry> sleepTelemetry,
            out NativeArray<int> sleepTelemetryCursor,
            out NativeArray<sbyte> sleepSdfDensity,
            out NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig,
            out NativeArray<BuoyancyMaterialSettlingProfileDTO> materialSettlingProfiles,
            out NativeArray<BuoyancyDebugForceDTO> debugForces,
            out NativeArray<BuoyancyCounterDTO> counters)
        {
            states = default;
            forcePackets = default;
            flowSamples = default;
            tuning = default;
            telemetry = default;
            telemetryCursor = default;
            sleepTelemetry = default;
            sleepTelemetryCursor = default;
            sleepSdfDensity = default;
            sleepSdfConfig = default;
            materialSettlingProfiles = default;
            debugForces = default;
            counters = default;
            if (vault == null)
                return false;

            states = ResolvePhysicsVaultBuffer(vault, in _statesHandle, BuoyancyDisplacementBufferIds.States);
            forcePackets = ResolvePhysicsVaultBuffer(vault, in _forcePacketsHandle, BuoyancyDisplacementBufferIds.ForcePackets);
            flowSamples = ResolvePhysicsVaultBuffer(vault, in _flowSamplesHandle, BuoyancyDisplacementBufferIds.FlowSamples);
            tuning = ResolvePhysicsVaultBuffer(vault, in _tuningHandle, BuoyancyDisplacementBufferIds.Tuning);
            telemetry = ResolvePhysicsVaultBuffer(vault, in _telemetryRingHandle, BuoyancyDisplacementBufferIds.TelemetryRing);
            telemetryCursor = ResolvePhysicsVaultBuffer(vault, in _telemetryCursorHandle, BuoyancyDisplacementBufferIds.TelemetryCursor);
            sleepTelemetry = ResolvePhysicsVaultBuffer(vault, in _sleepTelemetryRingHandle, BuoyancyDisplacementBufferIds.SleepTelemetryRing);
            sleepTelemetryCursor = ResolvePhysicsVaultBuffer(vault, in _sleepTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SleepTelemetryCursor);
            sleepSdfDensity = ResolvePhysicsVaultBuffer(vault, in _sleepSdfDensityHandle, BuoyancyDisplacementBufferIds.SleepSdfDensity);
            sleepSdfConfig = ResolvePhysicsVaultBuffer(vault, in _sleepSdfConfigHandle, BuoyancyDisplacementBufferIds.SleepSdfConfig);
            materialSettlingProfiles = ResolvePhysicsVaultBuffer(vault, in _materialSettlingProfilesHandle, BuoyancyDisplacementBufferIds.MaterialSettlingProfiles);
            debugForces = ResolvePhysicsVaultBuffer(vault, in _debugForcesHandle, BuoyancyDisplacementBufferIds.DebugForces);
            counters = ResolvePhysicsVaultBuffer(vault, in _countersHandle, BuoyancyDisplacementBufferIds.Counters);
            return states.IsCreated &&
                   forcePackets.IsCreated &&
                   flowSamples.IsCreated &&
                   tuning.IsCreated &&
                   telemetry.IsCreated &&
                   telemetryCursor.IsCreated &&
                   sleepTelemetry.IsCreated &&
                   sleepTelemetryCursor.IsCreated &&
                   sleepSdfDensity.IsCreated &&
                   sleepSdfConfig.IsCreated &&
                   materialSettlingProfiles.IsCreated &&
                   debugForces.IsCreated &&
                   counters.IsCreated &&
                   tuning.Length >= 1 &&
                   telemetry.Length >= BuoyancyDisplacementConstants.TelemetryCapacity &&
                   telemetryCursor.Length >= 1 &&
                   sleepTelemetry.Length >= BuoyancyDisplacementConstants.TelemetryCapacity &&
                   sleepTelemetryCursor.Length >= 1 &&
                   sleepSdfConfig.Length >= 1 &&
                   counters.Length >= 1;
        }

        private bool TryFinalizePendingSolverNoWait()
        {
            if (!_jobScheduled)
                return true;

            if (!_pendingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool CompletePendingSolverForTeardown()
        {
            if (!_jobScheduled)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _jobScheduled = false;
            bool shouldDumpFault = false;
            float faultScalar = 0f;
            uint faultEntityHash = 0u;
            UnlockJobBuffers();
            float micros = ResolveElapsedMicros(_scheduleTimestamp);
            WriteCompletedComputeMicros(micros);
            WriteCompletedSimdUtilizationTelemetry(micros);
            shouldDumpFault = !_dumpedFault && TryReadLatestCounterFault(out faultScalar, out faultEntityHash);

            if (shouldDumpFault)
            {
                PushBuoyancyFaultEvent(faultScalar, faultEntityHash);
                _dumpedFault = true;
            }

            _simulationFrame++;
            _forcePacketsReadyToDrain = true;
            return true;
        }

        private void WriteCompletedComputeMicros(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _countersHandle))
                return;

            if (!TryAcquireBuoyancyMutationGuard(vault, CompletionTelemetryMutationGuardMask))
                return;

            try
            {
                float safeMicros = math.max(0f, math.select(0f, micros, math.isfinite(micros)));
                NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                if (!counters.IsCreated || counters.Length <= 0)
                    return;

                BuoyancyCounterDTO counter = counters[0];
                counter.ComputeMicros = safeMicros;
                counters[0] = counter;

                NativeArray<BuoyancyTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
                NativeArray<int> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
                if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                    return;

                int currentCursor = math.clamp(cursor[0], 0, telemetry.Length - 1);
                int slot = (currentCursor + telemetry.Length - 1) % telemetry.Length;
                BuoyancyTelemetryEntry entry = telemetry[slot];
                entry.ComputeMicros = safeMicros;
                telemetry[slot] = entry;

                NativeArray<SleepStateTelemetryEntry> sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
                NativeArray<int> sleepCursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
                if (!sleepTelemetry.IsCreated || sleepTelemetry.Length <= 0 || !sleepCursor.IsCreated || sleepCursor.Length <= 0)
                    return;

                int sleepCurrentCursor = math.clamp(sleepCursor[0], 0, sleepTelemetry.Length - 1);
                int sleepSlot = (sleepCurrentCursor + sleepTelemetry.Length - 1) % sleepTelemetry.Length;
                SleepStateTelemetryEntry sleepEntry = sleepTelemetry[sleepSlot];
                sleepEntry.ComputeMicros = safeMicros;
                sleepTelemetry[sleepSlot] = sleepEntry;
            }
            finally
            {
                vault.ReleaseMutationGuard(CompletionTelemetryMutationGuardMask);
            }
        }

        private void WriteCompletedSimdUtilizationTelemetry(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !HasPhysicsHandle(in _simdTelemetryRingHandle, BuoyancyDisplacementBufferIds.SimdTelemetryRing) ||
                !HasPhysicsHandle(in _simdTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SimdTelemetryCursor))
            {
                return;
            }

            if (!TryAcquireBuoyancyMutationGuard(vault, SimdTelemetryMutationGuardMask))
                return;

            bool shouldDumpFault = false;
            float faultScalar = 0f;
            uint faultHash = 0u;
            try
            {
                NativeArray<SimdTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
                NativeArray<int> cursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
                if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                    return;

                NativeArray<SimdHydrodynamicTuningDTO> tuning = HasPhysicsHandle(in _simdHydrodynamicTuningHandle, BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning)
                    ? ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle)
                    : default;
                SimdHydrodynamicTuningDTO tuningValue = default;
                if (tuning.IsCreated && tuning.Length > 0)
                    tuningValue = tuning[0];

                float safeMicros = math.max(0f, math.select(0f, micros, math.isfinite(micros)));
                float vectorMs = math.max(0.0001f, safeMicros * 0.001f);
                float throughput = math.max(0, _activeStateCount) * math.rcp(vectorMs);
                float quality = ResolveGlobalQualityWeightFromHomeostasis();
                float maxError = math.max(0f, math.select(0.01f, tuningValue.MaxApproximationError, math.isfinite(tuningValue.MaxApproximationError)));
                float maxSpeed = math.max(0f, math.select(0f, tuningValue.MaxSpeed, math.isfinite(tuningValue.MaxSpeed)));
                float maxSpeedSq = maxSpeed * maxSpeed;
                int currentCursor = math.max(0, cursor[0]);
                int previousSlot = (currentCursor + telemetry.Length - 1) % telemetry.Length;
                SimdTelemetryEntry previousEntry = telemetry[previousSlot];
                bool previousSameKernel = previousEntry.KernelHash == SimdVectorizationConstants.HydrodynamicsKernelHash;
                float previousScalarMicros = math.max(
                    0f,
                    math.select(0f, previousEntry.ScalarMicros, previousSameKernel & math.isfinite(previousEntry.ScalarMicros)));
                float throughputDrop = ResolveSimdThroughputDrop(safeMicros, previousScalarMicros);
                bool nonFinite = !math.isfinite(micros) |
                                 !math.isfinite(throughput) |
                                 !math.isfinite(maxError) |
                                 !math.isfinite(maxSpeedSq) |
                                 !math.isfinite(throughputDrop);

                int slot = currentCursor % telemetry.Length;
                SimdTelemetryEntry entry = default;
                entry.FrameIndex = _simulationFrame;
                entry.KernelHash = SimdVectorizationConstants.HydrodynamicsKernelHash;
                entry.EntityCount = math.max(0, _activeStateCount);
                entry.VectorMicros = safeMicros;
                entry.ScalarMicros = previousScalarMicros;
                entry.EntitiesPerMillisecond = math.select(0f, throughput, math.isfinite(throughput));
                entry.ThroughputDrop01 = throughputDrop;
                entry.GlobalQualityWeight = quality;
                entry.Flags = math.select(0u, SimdVectorizationConstants.FlagNonFinite, nonFinite);
                entry.LastStateHash = (_simulationFrame * 747796405u) ^
                                      (uint)math.max(0, _activeStateCount) ^
                                      SimdVectorizationConstants.HydrodynamicsKernelHash;
                entry.MaxError = maxError;
                entry.MaxSpeedSq = math.select(0f, maxSpeedSq, math.isfinite(maxSpeedSq));
                telemetry[slot] = entry;
                int nextCursor = slot + 1;
                cursor[0] = math.select(nextCursor, 0, nextCursor >= telemetry.Length);

                if ((entry.Flags & SimdVectorizationConstants.FlagNonFinite) != 0u || throughputDrop > 0.5f)
                {
                    shouldDumpFault = true;
                    faultScalar = math.max(entry.VectorMicros, entry.ScalarMicros);
                    faultHash = entry.LastStateHash;
                }
            }
            finally
            {
                vault.ReleaseMutationGuard(SimdTelemetryMutationGuardMask);
            }

            if (shouldDumpFault)
                PushSimdFaultEvent(faultScalar, faultHash);
        }

        private bool TryReadLatestCounterFault(out float faultScalar, out uint entityHash)
        {
            faultScalar = 0f;
            entityHash = 0u;

            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _countersHandle))
                return false;

            if (!TryAcquireBuoyancyMutationGuard(vault, CounterFaultReadMutationGuardMask))
                return false;

            try
            {
                NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                if (!counters.IsCreated || counters.Length <= 0)
                    return false;

                BuoyancyCounterDTO counter = counters[0];
                bool hasFault = (counter.Flags & BuoyancyDisplacementConstants.FlagNonFinite) != 0u ||
                                counter.NonFiniteCount > 0;
                if (!hasFault)
                    return false;

                faultScalar = ComposeBuoyancyFaultScalar(counter);
                entityHash = counter.LastEntityHashID;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(CounterFaultReadMutationGuardMask);
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobBuffersPinned || !TryAcquireBuoyancyMutationGuard(vault, JobMutationGuardMask))
                return false;

            _jobGuardVault = vault;
            _jobBuffersPinned = true;
            return true;
        }

        private static bool TryAcquireBuoyancyMutationGuard(IDataVault vault, ulong mask)
        {
            return vault != null &&
                   mask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(mask);
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobBuffersPinned)
            {
                _jobGuardVault = null;
                return;
            }

            IDataVault vault = _jobGuardVault;
            _jobGuardVault = null;
            _jobBuffersPinned = false;
            if (vault != null)
                vault.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredHotSwap)
            {
                GlobalRegistry.RegisterHotSwapListener(this);
                _registeredHotSwap = true;
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterOriginShiftListener()
        {
            if (!Application.isPlaying)
                return;

            RefreshCachedSectorAUP();
            RefreshOriginShiftListenerRegistration();
        }

        private void RefreshOriginShiftListenerRegistration()
        {
            if (!Application.isPlaying)
                return;

            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregister()
        {
            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _registeredPostFixed = false;
            }

            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixed = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void TryUnregisterOriginShiftListener()
        {
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _statesHandle);
                ReleaseVaultHandle(vault, ref _forcePacketsHandle);
                ReleaseVaultHandle(vault, ref _flowSamplesHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _telemetryRingHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _sleepTelemetryRingHandle);
                ReleaseVaultHandle(vault, ref _sleepTelemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _sleepSdfDensityHandle);
                ReleaseVaultHandle(vault, ref _sleepSdfConfigHandle);
                ReleaseVaultHandle(vault, ref _materialVolumesHandle);
                ReleaseVaultHandle(vault, ref _materialSettlingProfilesHandle);
#if UNITY_EDITOR
                ReleaseVaultHandle(vault, ref _csvScratchHandle);
#endif
                ReleaseVaultHandle(vault, ref _debugForcesHandle);
                ReleaseVaultHandle(vault, ref _countersHandle);
                ReleaseVaultHandle(vault, ref _bodyBindingsHandle);
                ReleaseVaultHandle(vault, ref _simdLocalPositionsHandle);
                ReleaseVaultHandle(vault, ref _simdVelocitiesHandle);
                ReleaseVaultHandle(vault, ref _simdDragCoefficientsHandle);
                ReleaseVaultHandle(vault, ref _simdOutputForcesHandle);
                ReleaseVaultHandle(vault, ref _simdTelemetryRingHandle);
                ReleaseVaultHandle(vault, ref _simdTelemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _simdMathTolerancesHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleIndexMaskHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleIndicesHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleCountHandle);
                ReleaseVaultHandle(vault, ref _simdHydrodynamicTuningHandle);
            }

            ClearHandles();
            if (ReferenceEquals(vault, _dataVault))
                _dataVault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && HasHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearHandles()
        {
            _statesHandle = default;
            _forcePacketsHandle = default;
            _flowSamplesHandle = default;
            _tuningHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _sleepTelemetryRingHandle = default;
            _sleepTelemetryCursorHandle = default;
            _sleepSdfDensityHandle = default;
            _sleepSdfConfigHandle = default;
            _materialVolumesHandle = default;
            _materialSettlingProfilesHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
            _debugForcesHandle = default;
            _countersHandle = default;
            _bodyBindingsHandle = default;
            _simdLocalPositionsHandle = default;
            _simdVelocitiesHandle = default;
            _simdDragCoefficientsHandle = default;
            _simdOutputForcesHandle = default;
            _simdTelemetryRingHandle = default;
            _simdTelemetryCursorHandle = default;
            _simdMathTolerancesHandle = default;
            _simdVisibleIndexMaskHandle = default;
            _simdVisibleIndicesHandle = default;
            _simdVisibleCountHandle = default;
            _simdHydrodynamicTuningHandle = default;
            _jobGuardVault = null;
            _jobBuffersPinned = false;
            _coldBuffersInitialized = false;
            _coldBootCompleted = false;
            _forcePacketsReadyToDrain = false;
        }

        private static int ResolveEvaluationStride(float quality)
        {
            return 1;
        }

        private static int ResolveAmbientCurrentPollCadence(float quality)
        {
            float safeQuality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return math.clamp((int)math.round(math.lerp(12f, 4f, safeQuality)), 4, 12);
        }

        private static int ResolveScheduledEvaluationCount(int activeCount, int stride, int offset)
        {
            int safeActive = math.max(0, activeCount);
            int safeStride = math.max(1, stride);
            int safeOffset = math.clamp(offset, 0, safeStride - 1);
            int numerator = math.max(0, safeActive - safeOffset);
            return (numerator + safeStride - 1) / safeStride;
        }

        private static float ResolveGlobalQualityWeight(ref BuoyancyTuningDTO tuning)
        {
            float homeostasis = ResolveGlobalQualityWeightFromHomeostasis();
            float tuningQuality = math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight));
            return math.saturate(math.min(homeostasis, math.saturate(tuningQuality)));
        }

        private static float ResolveGlobalQualityWeightFromHomeostasis()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float homeostasis = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, homeostasis, math.isfinite(homeostasis)));
        }

        private static SimdHydrodynamicTuningDTO PrepareBenchmarkSimdTuning(
            NativeArray<SimdHydrodynamicTuningDTO> tuning,
            uint frameIndex)
        {
            SimdHydrodynamicTuningDTO value = tuning[0];
            float scalarWeight = math.saturate(math.select(0f, value.ScalarFallbackWeight01, math.isfinite(value.ScalarFallbackWeight01)));
            value.DeltaTime = math.select(1f / 60f, value.DeltaTime, math.isfinite(value.DeltaTime) & value.DeltaTime > 0f);
            value.GlobalQualityWeight = ResolveGlobalQualityWeightFromHomeostasis();
            value.BaseLinearDrag = math.select(0.02f, value.BaseLinearDrag, math.isfinite(value.BaseLinearDrag) & value.BaseLinearDrag >= 0f);
            value.BuoyancyAccelerationY = math.select(0.15f, value.BuoyancyAccelerationY, math.isfinite(value.BuoyancyAccelerationY));
            value.BaseFlowVelocity = math.select(new float3(0.04f, 0f, -0.03f), value.BaseFlowVelocity, math.isfinite(value.BaseFlowVelocity));
            value.TurbulenceAmplitude = math.select(0.35f, value.TurbulenceAmplitude, math.isfinite(value.TurbulenceAmplitude) & value.TurbulenceAmplitude >= 0f);
            value.MaxSpeed = math.select(12f, value.MaxSpeed, math.isfinite(value.MaxSpeed) & value.MaxSpeed > 0f);
            value.FrameIndex = frameIndex;
            value.Flags = SimdVectorizationConstants.FlagActive;
            value.ScalarFallbackWeight01 = scalarWeight;
            bool hasApproximationWeight = math.isfinite(value.ApproximationQualityWeight) &
                                          value.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            value.ApproximationQualityWeight = math.saturate(math.select(value.GlobalQualityWeight, value.ApproximationQualityWeight, hasApproximationWeight));
            value.MaxApproximationError = math.select(0.01f, value.MaxApproximationError, math.isfinite(value.MaxApproximationError) & value.MaxApproximationError >= 0f);
            value.SinPolynomialDegree = math.select(7, math.clamp(value.SinPolynomialDegree, 3, 7), value.SinPolynomialDegree > 0);
            tuning[0] = value;
            return value;
        }

        private static void ApplySimdToleranceTuning(
            ReadOnlySpan<SimdMathToleranceDTO> tolerances,
            int toleranceRows,
            NativeArray<SimdHydrodynamicTuningDTO> tuning)
        {
            if (tolerances.Length <= 0 || toleranceRows <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return;

            SimdHydrodynamicTuningDTO value = tuning[0];
            int degree = math.select(7, math.clamp(value.SinPolynomialDegree, 3, 7), value.SinPolynomialDegree > 0);
            float maxError = math.select(0.01f, value.MaxApproximationError, math.isfinite(value.MaxApproximationError) & value.MaxApproximationError >= 0f);
            int rows = math.min(toleranceRows, tolerances.Length);
            for (int i = 0; i < rows; i++)
            {
                SimdMathToleranceDTO row = tolerances[i];
                bool appliesToSine = row.FormulaHash == SimdVectorizationConstants.SinPolynomialFormulaHash ||
                                     row.FormulaHash == SimdVectorizationConstants.HydrodynamicTurbulenceFormulaHash;
                bool rowErrorFinite = math.isfinite(row.MaxError);
                float rowMaxError = math.max(0f, math.select(0f, row.MaxError, rowErrorFinite));
                bool applyRow = ((row.Flags & SimdVectorizationConstants.FlagActive) != 0u) &
                                appliesToSine &
                                rowErrorFinite;
                degree = math.select(degree, math.clamp(row.PolynomialDegree, 3, 7), applyRow);
                maxError = math.select(maxError, rowMaxError, applyRow);
            }

            value.SinPolynomialDegree = degree;
            value.MaxApproximationError = maxError;
            bool hasApproximationWeight = math.isfinite(value.ApproximationQualityWeight) &
                                          value.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            value.ApproximationQualityWeight = math.saturate(math.select(value.GlobalQualityWeight, value.ApproximationQualityWeight, hasApproximationWeight));
            value.Flags = SimdVectorizationConstants.FlagActive;
            tuning[0] = value;
        }

        private double3 ResolveCachedSectorAUP()
        {
            return math.select(double3.zero, _cachedSectorAup, math.isfinite(_cachedSectorAup));
        }

        private void RefreshCachedSectorAUP()
        {
            _cachedSectorAup = ResolveSectorAUPFromOrigin();
        }

        private static double3 ResolveSectorAUPFromOrigin()
        {
            double3 sectorAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.select(double3.zero, sectorAup, math.isfinite(sectorAup));
        }

        private static float ResolveElapsedMicros(long scheduleTimestamp)
        {
            if (scheduleTimestamp <= 0L)
                return 0f;

            long elapsed = Stopwatch.GetTimestamp() - scheduleTimestamp;
            if (elapsed <= 0L)
                return 0f;

            long frequency = Stopwatch.Frequency;
            if (frequency <= 0L)
                return 0f;

            double seconds = elapsed / (double)frequency;
            double micros = Math.Min(seconds * 1000000.0, float.MaxValue);
            float value = (float)micros;
            return math.max(0f, math.select(0f, value, math.isfinite(value)));
        }

#if UNITY_EDITOR
        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static int ReadFileIntoColdScratch(string path, byte[] scratch)
        {
            // COLD IO ONLY: managed editor scratch receives bytes before any DataVault writer guard is acquired.
            if (scratch == null || scratch.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int limit = (int)math.min(stream.Length, scratch.Length);
                    if (limit <= 0)
                        return 0;

                    Span<byte> destination = scratch.AsSpan(0, limit);
                    return stream.Read(destination);
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
#endif

        private static float ComposeBuoyancyFaultScalar(BuoyancyCounterDTO counter)
        {
            float totalForce = math.max(math.abs(counter.TotalBuoyantForce), math.abs(counter.TotalDragForce));
            float nonFiniteWeight = math.max(0, counter.NonFiniteCount);
            float flagWeight = (counter.Flags & BuoyancyDisplacementConstants.FlagNonFinite) != 0u ? 1f : 0f;
            float scalar = math.max(totalForce, nonFiniteWeight + flagWeight);
            return math.select(0f, scalar, math.isfinite(scalar));
        }

        private void PushBuoyancyFaultEvent(float faultScalar, uint entityHash)
        {
            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            GlobalTelemetryBus.PushEvent(BuoyancyFaultEventHash, faultScalar, entityHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(BuoyancyFaultDumpHash);
        }

        private static float ResolveSimdThroughputDrop(float vectorMicros, float scalarMicros)
        {
            float safeVectorMicros = math.max(0.0001f, math.select(0.0001f, vectorMicros, math.isfinite(vectorMicros)));
            float safeScalarMicros = math.max(0f, math.select(0f, scalarMicros, math.isfinite(scalarMicros)));
            float drop = math.saturate(1f - (safeScalarMicros * math.rcp(safeVectorMicros)));
            return math.select(0f, drop, (safeScalarMicros > 0.0001f) & math.isfinite(drop));
        }

        private void TryDumpSimdTelemetry(NativeArray<SimdTelemetryEntry> telemetry, NativeArray<int> cursor)
        {
            if (!TryComposeSimdFaultPayload(telemetry, cursor, out float scalar, out uint stateHash))
                return;

            PushSimdFaultEvent(scalar, stateHash);
        }

        private static bool TryComposeSimdFaultPayload(
            NativeArray<SimdTelemetryEntry> telemetry,
            NativeArray<int> cursor,
            out float scalar,
            out uint stateHash)
        {
            scalar = 0f;
            stateHash = 0u;

            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? math.max(0, cursor[0]) : 0;
            int latestIndex = cursorValue > 0 ? (cursorValue - 1) % telemetry.Length : 0;
            SimdTelemetryEntry latest = telemetry[latestIndex];
            scalar = math.max(latest.VectorMicros, latest.ScalarMicros);
            stateHash = latest.LastStateHash;
            return math.isfinite(scalar);
        }

        private void PushSimdFaultEvent(float scalar, uint stateHash)
        {
            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            GlobalTelemetryBus.PushEvent(BuoyancySimdFaultEventHash, scalar, stateHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(BuoyancySimdFaultDumpHash);
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

#if UNITY_EDITOR
        private const string SimdLocalPositionsName = "SHINOBU SIMD local-pos";
        private const string SimdVelocitiesName = "SHINOBU SIMD velocity";
        private const string SimdOutputForcesName = "SHINOBU SIMD force-out";
        private const string SimdDragCoefficientsName = "SHINOBU SIMD drag";

        private readonly char[] _simdGizmoLabelBuffer = new char[160];
        private readonly UnityEngine.GUIContent _simdLocalPositionsLabel =
            new UnityEngine.GUIContent(SimdLocalPositionsName);
        private readonly UnityEngine.GUIContent _simdVelocitiesLabel =
            new UnityEngine.GUIContent(SimdVelocitiesName);
        private readonly UnityEngine.GUIContent _simdOutputForcesLabel =
            new UnityEngine.GUIContent(SimdOutputForcesName);
        private readonly UnityEngine.GUIContent _simdDragCoefficientsLabel =
            new UnityEngine.GUIContent(SimdDragCoefficientsName);
        private int _simdLocalPositionsLabelHash = int.MinValue;
        private int _simdVelocitiesLabelHash = int.MinValue;
        private int _simdOutputForcesLabelHash = int.MinValue;
        private int _simdDragCoefficientsLabelHash = int.MinValue;

        private static readonly UnityEngine.GUIContent SimdAlignmentFaultLabel =
            new UnityEngine.GUIContent("SHINOBU SIMD ALIGNMENT FAULT - ARM64 NEON unsafe");

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _dataVault == null)
                return;

            DrawSimdAlignmentGizmos();
            DrawSleepStateGizmos();
            if (!HasHandle(in _debugForcesHandle))
                return;

            NativeArray<BuoyancyDebugForceDTO> debugForces = ResolveVaultBuffer(_dataVault, in _debugForcesHandle);
            if (!debugForces.IsCreated)
                return;

            int count = math.min(math.max(0, _activeStateCount), debugForces.Length);
            double3 committedOffset = ResolveCachedSectorAUP();
            for (int i = 0; i < count; i++)
            {
                BuoyancyDebugForceDTO debug = debugForces[i];
                if ((debug.Flags & BuoyancyDisplacementConstants.FlagActive) == 0u || debug.EntityHashID == 0u)
                    continue;

                Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(debug.CurrentAUP, committedOffset);
                DrawVector(origin, debug.BuoyantForce, Color.blue, 0.0025f);
                DrawVector(origin, debug.GravityForce, Color.red, 0.0025f);
                DrawVector(origin, debug.DragForce, Color.green, 0.01f);
            }
        }

        private void DrawSleepStateGizmos()
        {
            if (!HasHandle(in _statesHandle))
                return;

            NativeArray<BuoyancyStateDTO> states = ResolveVaultBuffer(_dataVault, in _statesHandle);
            if (!states.IsCreated)
                return;

            int count = math.min(math.max(0, _activeStateCount), states.Length);
            double3 committedOffset = ResolveCachedSectorAUP();
            for (int i = 0; i < count; i++)
            {
                BuoyancyStateDTO state = states[i];
                if ((state.Flags & BuoyancyDisplacementConstants.FlagActive) == 0u || state.EntityHashID == 0u)
                    continue;

                bool sleeping = (state.Flags & BuoyancyDisplacementConstants.FlagSleeping) != 0u;
                Gizmos.color = sleeping ? new Color(0.02f, 0.08f, 0.32f, 0.92f) : new Color(0.05f, 0.9f, 0.25f, 0.85f);
                Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(state.CurrentAUP, committedOffset);
                float size = math.clamp(EstimateGizmoSize(state.VolumeCubicMeters), 0.12f, 1.25f);
                Gizmos.DrawWireCube(origin, new Vector3(size, size, size));
            }
        }

        private static float EstimateGizmoSize(float volume)
        {
            float safeVolume = math.max(BuoyancyDisplacementConstants.Epsilon, math.select(BuoyancyDisplacementConstants.Epsilon, volume, math.isfinite(volume)));
            float size = safeVolume * math.rsqrt(math.max(safeVolume, BuoyancyDisplacementConstants.Epsilon));
            return math.max(0.12f, size);
        }

        private static void DrawVector(Vector3 origin, float3 vector, Color color, float scale)
        {
            if (!math.all(math.isfinite(vector)))
                return;

            Gizmos.color = color;
            Vector3 delta = new Vector3(vector.x, vector.y, vector.z) * scale;
            Gizmos.DrawLine(origin, origin + delta);
        }

        private void DrawSimdAlignmentGizmos()
        {
            if (!HasHandle(in _simdLocalPositionsHandle) ||
                !HasHandle(in _simdVelocitiesHandle) ||
                !HasHandle(in _simdOutputForcesHandle) ||
                !HasHandle(in _simdDragCoefficientsHandle))
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 1.25f;
            bool localOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdLocalPositionsHandle),
                origin + Vector3.right * -0.75f,
                0.16f,
                _simdLocalPositionsLabel,
                SimdLocalPositionsName,
                ref _simdLocalPositionsLabelHash);
            bool velocityOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdVelocitiesHandle),
                origin + Vector3.right * -0.25f,
                0.16f,
                _simdVelocitiesLabel,
                SimdVelocitiesName,
                ref _simdVelocitiesLabelHash);
            bool forceOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdOutputForcesHandle),
                origin + Vector3.right * 0.25f,
                0.16f,
                _simdOutputForcesLabel,
                SimdOutputForcesName,
                ref _simdOutputForcesLabelHash);
            bool dragOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdDragCoefficientsHandle),
                origin + Vector3.right * 0.75f,
                0.16f,
                _simdDragCoefficientsLabel,
                SimdDragCoefficientsName,
                ref _simdDragCoefficientsLabelHash);

            if (!(localOk & velocityOk & forceOk & dragOk))
                DrawSimdAlignmentFault(origin);
        }

        private unsafe bool DrawSimdLaneBar<T>(
            NativeArray<T> array,
            Vector3 origin,
            float scale,
            UnityEngine.GUIContent label,
            string labelName,
            ref int labelHash) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return true;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            bool pointerAligned = (((ulong)ptr) & 15UL) == 0UL;
            int stride = UnsafeUtility.SizeOf<T>();
            bool strideVectorSafe = stride == 4 || (stride & 15) == 0;
            bool ok = pointerAligned && strideVectorSafe;
            UpdateSimdLaneLabel(label, labelName, array.Length, stride, pointerAligned, strideVectorSafe, ok, ref labelHash);
            Gizmos.color = ok ? new Color(0.05f, 0.85f, 0.9f, 0.85f) : new Color(1f, 0.05f, 0.02f, 1f);
            float height = math.saturate(array.Length * (1f / SimdVectorizationConstants.BenchmarkEntityCount)) * 1.5f + 0.1f;
            Gizmos.DrawWireCube(origin + Vector3.up * (height * 0.5f), new Vector3(scale, height, scale));
            UnityEditor.Handles.color = ok ? Color.cyan : Color.red;
            UnityEditor.Handles.Label(origin + Vector3.up * (height + 0.08f), label);
            return ok;
        }

        private void UpdateSimdLaneLabel(
            UnityEngine.GUIContent label,
            string labelName,
            int capacity,
            int stride,
            bool pointerAligned,
            bool strideVectorSafe,
            bool ok,
            ref int labelHash)
        {
            int hash = capacity ^
                       (stride << 9) ^
                       (pointerAligned ? 0x10000 : 0x20000) ^
                       (strideVectorSafe ? 0x40000 : 0x80000) ^
                       (ok ? 0x100000 : 0x200000);
            if (hash == labelHash)
                return;

            int write = 0;
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, labelName);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | stride ");
            AppendEditorInt(_simdGizmoLabelBuffer, ref write, stride);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | cap ");
            AppendEditorInt(_simdGizmoLabelBuffer, ref write, capacity);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | ptr16 ");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, pointerAligned ? "OK" : "FAIL");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | lane ");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, strideVectorSafe ? "OK" : "FAIL");
            label.text = new string(_simdGizmoLabelBuffer, 0, write);
            labelHash = hash;
        }

        private static void AppendEditorLiteral(char[] buffer, ref int offset, string value)
        {
            for (int i = 0; i < value.Length && offset < buffer.Length; i++)
                buffer[offset++] = value[i];
        }

        private static void AppendEditorInt(char[] buffer, ref int offset, int value)
        {
            if (offset >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[offset++] = '0';
                return;
            }

            int remaining = math.abs(value);
            if (value < 0 && offset < buffer.Length)
                buffer[offset++] = '-';

            int start = offset;
            while (remaining > 0 && offset < buffer.Length)
            {
                buffer[offset++] = (char)('0' + remaining % 10);
                remaining /= 10;
            }

            int end = offset - 1;
            while (start < end)
            {
                char swap = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = swap;
                start++;
                end--;
            }
        }

        private static void DrawSimdAlignmentFault(Vector3 origin)
        {
            float phase = math.frac((float)UnityEditor.EditorApplication.timeSinceStartup * 4f);
            float flash = math.step(0.5f, phase);
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f + 0.55f * flash);
            Gizmos.DrawWireCube(origin + Vector3.up * 0.9f, new Vector3(2.6f, 2.2f, 2.6f));
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(origin + Vector3.up * 2.15f, SimdAlignmentFaultLabel);
        }
#endif
    }
}
