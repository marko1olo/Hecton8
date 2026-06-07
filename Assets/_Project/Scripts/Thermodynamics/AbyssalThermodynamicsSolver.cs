using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Thermodynamics/Abyssal Thermodynamics Solver")]
    public sealed unsafe partial class AbyssalThermodynamicsSolver : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        public const int MinResolution = 16;
        public const int MaxResolution = 32;
        public const int MaxCellCount = MaxResolution * MaxResolution * MaxResolution;
        public const int MaxSourceCount = 128;
        public const int MaxProfileCount = 32;
        public const int SampleCapacity = 512;
        public const int CsvScratchBytes = 8192;

        private const int DefaultBatchSize = 64;
        private const float DeterministicSimulationTickSeconds = 1f / 60f;
        private const float DefaultCellSizeMeters = 8f;
        private const float DefaultAmbientTemperatureCelsius = 2f;
        private const float DefaultWaterConductivity = 0.18f;
        private const float DefaultConvectionSpeed = 0.018f;
        private const float DefaultDissipationPerStep = 0.0025f;
        private const float DefaultMaxStableTemperatureCelsius = 1200f;
        private const float DefaultHullInsulationConductivity = 0.002f;
        private const float DefaultMockVolcanoIntensity = 180f;
        private const float DefaultMockVolcanoRadiusMeters = 42f;
        private const uint BlackSmokerHash = 0xA4D4E638u;
        private const uint TransientSourceTtlFrames = 6u;
        private const byte PendingFrontBufferCurrent = 0;
        private const byte PendingFrontBufferBack = 1;
        private const byte PendingFrontBufferScratch = 2;
        private static readonly BufferID SolverConvergenceStateId = (BufferID)70052;
        private static readonly BufferID SolverResidualSamplesId = (BufferID)70053;
        private static readonly BufferID SolverDumpLatchId = (BufferID)70054;
        private static readonly ulong HeatSourceProfileMutationGuardMask =
            ThermodynamicsMutationGuardBit(BufferID.AbyssalThermalProfiles) |
            ThermodynamicsMutationGuardBit(BufferID.AbyssalThermalProfileCount);

        private static readonly int ThermalCellsBufferId = Shader.PropertyToID("_H8AbyssalThermalCells");
        private static readonly int ThermalGridMetaId = Shader.PropertyToID("_H8AbyssalThermalGridMeta");
        private static readonly int ThermalGridOriginId = Shader.PropertyToID("_H8AbyssalThermalGridOrigin");

        [Header("Grid")]
        [SerializeField, Min(1f)] private float cellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Range(0f, 1f)] private float qualityOverride = -1f;
        [SerializeField] private bool useQualityOverride;

        [Header("Thermal")]
        [SerializeField] private float ambientTemperatureCelsius = DefaultAmbientTemperatureCelsius;
        [SerializeField, Min(0.0001f)] private float waterThermalConductivity = DefaultWaterConductivity;
        [SerializeField, Min(0f)] private float convectionSpeed = DefaultConvectionSpeed;
        [SerializeField, Range(0f, 0.05f)] private float dissipationPerStep = DefaultDissipationPerStep;

        [Header("Mock Producers")]
        [SerializeField] private bool enableMockVolcano = true;
        [SerializeField, Range(1, 16)] private int mockVolcanoCount = 4;
        [SerializeField, Min(1f)] private float mockVolcanoIntensity = DefaultMockVolcanoIntensity;
        [SerializeField, Min(1f)] private float mockVolcanoRadiusMeters = DefaultMockVolcanoRadiusMeters;

        [Header("Hull Insulation")]
        [SerializeField] private Vector3 submarineHalfExtentsMeters = new Vector3(8f, 4f, 22f);

        [Header("Debug")]
        [SerializeField] private bool drawThermalSlice = true;
        [SerializeField, Range(0f, 1f)] private float gizmoSliceY01 = 0.5f;

        private IDataVault _vault;
        private VaultGenerationHandle<ThermalCellDTO> _front;
        private VaultGenerationHandle<ThermalCellDTO> _back;
        private VaultGenerationHandle<ThermalCellDTO> _injection;
        private VaultGenerationHandle<ThermalCellDTO> _shiftScratch;
        private VaultGenerationHandle<HeatSourceDTO> _sources;
        private VaultGenerationHandle<int> _sourceCount;
        private VaultGenerationHandle<ThermalGridTuningDTO> _tuning;
        private VaultGenerationHandle<double3> _sampleAups;
        private VaultGenerationHandle<ThermalSampleResultDTO> _sampleResults;
        private VaultGenerationHandle<ThermalTelemetryEntry> _telemetryRing;
        private VaultGenerationHandle<HeatSourceProfileDTO> _profiles;
        private VaultGenerationHandle<int> _profileCount;
        private VaultGenerationHandle<ThermalSolverConvergenceStateDTO> _solverConvergence;
        private VaultGenerationHandle<ThermalResidualSlot64> _solverResidualSamples;
        private VaultGenerationHandle<int> _solverDumpLatch;

        private JobHandle _pendingHandle;
        private JobHandle _sampleReadHandle;
        private bool _hasPendingJob;
        private bool _nativeReady;
        private bool _registeredUpdate;
        private bool _registeredLate;
        private bool _registeredOrigin;
        private bool _registeredHotSwapListener;
        private bool _visualDirty;
        private bool _hasRealSources;
        private bool _gridOriginInitialized;
        private int3 _pendingShiftCells;
        private int _activeResolution = MaxResolution;
        private int _activeCellCount = MaxCellCount;
        private int _lastInitializedResolution;
        private uint _frame;
        private uint _lastShiftSequence;
        private byte _pendingFrontBuffer = PendingFrontBufferBack;
        private long _scheduleTimestamp;
        private long _lastProfileWriteTicks;
        private float _lastSolverMicroseconds;
        private double3 _gridOriginAup;
        private GraphicsBuffer _thermalCellsBufferA;
        private GraphicsBuffer _thermalCellsBufferB;
        private int _thermalCellsUploadParity;

        public static AbyssalThermodynamicsSolver ActiveRuntimeInstance { get; private set; }
        public bool IsInitialized => _nativeReady;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ThermalCellLayoutValidator.ValidateThermalCellLayout() ||
                !ThermalCellLayoutValidator.ValidateThermalSolverConvergenceLayout() ||
                !ReactorThermalLayoutValidator.ValidateBaseReactorLayout() ||
                !ReactorThermalLayoutValidator.ValidateReactorStateLayout() ||
                !ReactorThermalLayoutValidator.ValidateSupportLayouts())
                throw new InvalidOperationException("ThermalCellDTO ABI mismatch.");

            EnsureNative();
            RegisterRuntime();
            TryRegisterHotSwapListener();
            ActiveRuntimeInstance = this;
        }

        private void OnDisable()
        {
            CompleteThermalJobsForLifecycle();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();
            if (_registeredOrigin)
                HectonFloatingOrigin.UnregisterListener(this);

            _registeredOrigin = false;

            ReleaseVisualBuffers();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            CompleteThermalJobsForLifecycle();
            ReleaseVisualBuffers();
            ReleaseOwnedVaultHandles(_vault);
            ClearVaultHandles();
            _vault = null;
            _nativeReady = false;
            _lastInitializedResolution = 0;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
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

                TryUnregisterRuntimeLanes();
                TryRegisterRuntimeLanes();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (!RebindDataVaultForLifecycle(currentService as IDataVault))
                return;

            if (isActiveAndEnabled && _vault != null)
                EnsureNative();
        }

        public void Tick(float deltaTime)
        {
            if (!_nativeReady || _hasPendingJob)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryResolveArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryResolveArray(vault, in _back, MaxCellCount, out NativeArray<ThermalCellDTO> backArray) ||
                !TryResolveArray(vault, in _injection, MaxCellCount, out NativeArray<ThermalCellDTO> injectionArray) ||
                !TryResolveArray(vault, in _shiftScratch, MaxCellCount, out NativeArray<ThermalCellDTO> scratchArray) ||
                !TryResolveArray(vault, in _sources, MaxSourceCount, out NativeArray<HeatSourceDTO> sourceArray) ||
                !TryResolveArray(vault, in _sourceCount, 1, out NativeArray<int> sourceCountArray) ||
                !TryResolveArray(vault, in _telemetryRing, AbyssalThermalMath.TelemetryCapacity, out NativeArray<ThermalTelemetryEntry> telemetryArray) ||
                !TryResolveArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray) ||
                !TryResolveArray(vault, in _solverConvergence, 1, out NativeArray<ThermalSolverConvergenceStateDTO> solverStateArray) ||
                !TryResolveArray(vault, in _solverResidualSamples, AbyssalThermalMath.ResidualThreadSlotCount, out NativeArray<ThermalResidualSlot64> solverResidualArray))
                return;

            ThermalCellDTO* front = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(frontArray);
            ThermalCellDTO* back = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(backArray);
            ThermalCellDTO* injection = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(injectionArray);
            ThermalCellDTO* scratch = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchArray);
            HeatSourceDTO* sources = (HeatSourceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceArray);
            int* sourceCount = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceCountArray);
            ThermalTelemetryEntry* telemetry = (ThermalTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryArray);
            ThermalGridTuningDTO* tuningPtr = (ThermalGridTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            ThermalSolverConvergenceStateDTO* solverState = (ThermalSolverConvergenceStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(solverStateArray);
            ThermalResidualSlot64* solverResiduals = (ThermalResidualSlot64*)NativeArrayUnsafeUtility.GetUnsafePtr(solverResidualArray);

            uint nextFrame = _frame + 1u;
            CullExpiredTransientSources(sources, sourceCount, nextFrame);
            TryIngestThermalSourceSignals(sources, sourceCount, nextFrame);
            _hasRealSources = HasNonMockSources(sources, sourceCount);

            ThermalGridTuningDTO tuning = BuildTuning();
            *tuningPtr = tuning;
            _frame = tuning.Frame;
            CullExpiredTransientSources(sources, sourceCount, tuning.Frame);

            JobHandle dependency = _sampleReadHandle;
            _sampleReadHandle = default;
            uint telemetryFlags = 0u;
            if (_lastInitializedResolution != _activeResolution)
            {
                ThermalGridInitializeJob initJob;
                initJob.Front = front;
                initJob.Back = back;
                initJob.Injection = injection;
                initJob.AmbientTemperatureCelsius = tuning.AmbientTemperatureCelsius;
                initJob.WaterThermalConductivity = tuning.WaterThermalConductivity;
                dependency = initJob.Schedule(_activeCellCount, DefaultBatchSize, dependency);
                _lastInitializedResolution = _activeResolution;
                _pendingShiftCells = int3.zero;
                telemetryFlags |= AbyssalThermalMath.TelemetryFlagShift;
            }
            else if (math.any(_pendingShiftCells != int3.zero))
            {
                ShiftThermalGridJob shiftJob;
                shiftJob.Cells = front;
                shiftJob.Scratch = scratch;
                shiftJob.ShiftCells = _pendingShiftCells;
                shiftJob.Tuning = tuning;
                dependency = shiftJob.Schedule(dependency);
                telemetryFlags |= AbyssalThermalMath.TelemetryFlagShift;
                _pendingShiftCells = int3.zero;
            }

            ClearThermalInjectionJob clearJob;
            clearJob.Injection = injection;
            clearJob.WaterThermalConductivity = tuning.WaterThermalConductivity;
            dependency = clearJob.Schedule(_activeCellCount, DefaultBatchSize, dependency);

            if (enableMockVolcano && !_hasRealSources)
            {
                GenerateMockThermalSourcesJob mockJob;
                mockJob.Sources = sources;
                mockJob.SourceCount = sourceCount;
                mockJob.Tuning = tuning;
                mockJob.Frame = tuning.Frame;
                mockJob.ProfileHash = BlackSmokerHash;
                dependency = mockJob.Schedule(dependency);
                telemetryFlags |= AbyssalThermalMath.TelemetryFlagMockSources;
            }

            SubmarineHullInsulationJob hullJob;
            hullJob.Cells = front;
            hullJob.Tuning = tuning;
            dependency = hullJob.Schedule(_activeCellCount, DefaultBatchSize, dependency);

            dependency = ScheduleReactorThermalLink(vault, front, injection, in tuning, dependency);

            ThermalInjectionJob injectionJob;
            injectionJob.Injection = injection;
            injectionJob.Sources = sources;
            injectionJob.SourceCount = sourceCount;
            injectionJob.Tuning = tuning;
            injectionJob.DeltaTime = tuning.SimulationTickDeltaSeconds;
            injectionJob.Frame = tuning.Frame;
            injectionJob.SourceTtlFrames = TransientSourceTtlFrames;
            injectionJob.SourceCapacity = MaxSourceCount;
            dependency = injectionJob.Schedule(dependency);

            float qualityWeight = math.saturate(AbyssalThermalMath.FiniteOr(tuning.GlobalQualityWeight, AbyssalThermalMath.AuthoritativeQualityWeight));
            int jacobiPasses = math.max(AbyssalThermalMath.MinQualityJacobiIterations, tuning.JacobiIterations);
            ThermalGridTuningDTO passTuning = tuning;
            passTuning.JacobiIterations = 1;
            float targetTolerance = AbyssalThermalMath.ResolveSolverTargetTolerance(qualityWeight);
            float baseOmega = AbyssalThermalMath.ResolveSolverOmega(qualityWeight);
            int residualSampleMask = AbyssalThermalMath.ResolveResidualSampleMask(qualityWeight);
            dependency = new InitializeThermalSolverConvergenceJob
            {
                SolverState = solverState,
                ResidualSamples = solverResiduals,
                ResidualSlotCount = AbyssalThermalMath.ResidualThreadSlotCount,
                BaseOmega = baseOmega
            }.Schedule(AbyssalThermalMath.ResidualThreadSlotCount, DefaultBatchSize, dependency);

            ThermalCellDTO* readCells = front;
            ThermalCellDTO* writeCells = back;
            ThermalCellDTO* finalCells = back;
            bool writeBack = true;
            byte finalFrontBuffer = PendingFrontBufferBack;
            // The reduction job makes later ping-pong passes copy-forward once the grid reaches tolerance.
            for (int pass = 0; pass < jacobiPasses; pass++)
            {
                dependency = new ClearThermalSolverResidualSlotsJob
                {
                    ResidualSamples = solverResiduals,
                    ResidualSlotCount = AbyssalThermalMath.ResidualThreadSlotCount
                }.Schedule(AbyssalThermalMath.ResidualThreadSlotCount, DefaultBatchSize, dependency);

                HeatDiffusionSolverJob diffusionJob = default;
                diffusionJob.Front = readCells;
                diffusionJob.Back = writeCells;
                diffusionJob.Injection = injection;
                diffusionJob.SolverState = solverState;
                diffusionJob.ResidualSamples = solverResiduals;
                diffusionJob.Tuning = passTuning;
                diffusionJob.ResidualSampleMask = residualSampleMask;
                diffusionJob.ResidualSlotCount = AbyssalThermalMath.ResidualThreadSlotCount;
                diffusionJob.ApplyInjection = (byte)(pass == 0 ? 1 : 0);
                dependency = diffusionJob.Schedule(_activeCellCount, DefaultBatchSize, dependency);
                dependency = new ThermalSolverResidualReductionJob
                {
                    SolverState = solverState,
                    ResidualSamples = solverResiduals,
                    TargetTolerance = targetTolerance,
                    BaseOmega = baseOmega,
                    ResidualSlotCount = AbyssalThermalMath.ResidualThreadSlotCount,
                    FinalIteration = pass == jacobiPasses - 1 ? (byte)1 : (byte)0
                }.Schedule(dependency);
                finalCells = writeCells;
                finalFrontBuffer = writeBack ? PendingFrontBufferBack : PendingFrontBufferScratch;
                readCells = writeCells;
                writeBack = !writeBack;
                writeCells = writeBack ? back : scratch;
            }

            _pendingFrontBuffer = finalFrontBuffer;

            ThermalTelemetryRecorderJob telemetryJob;
            telemetryJob.Front = front;
            telemetryJob.Back = finalCells;
            telemetryJob.Injection = injection;
            telemetryJob.SourceCount = sourceCount;
            telemetryJob.Ring = telemetry;
            telemetryJob.SolverState = solverState;
            telemetryJob.Tuning = tuning;
            telemetryJob.SolverMicroseconds = _lastSolverMicroseconds;
            telemetryJob.Frame = tuning.Frame;
            telemetryJob.ExtraFlags = telemetryFlags;
            dependency = telemetryJob.Schedule(dependency);

            _scheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _pendingHandle = dependency;
            _hasPendingJob = true;
            H8Memory.RegisterActiveJob(SystemID.Thermodynamics, _pendingHandle);
        }

        public void LateFrameTick()
        {
            if (!_nativeReady || !_hasPendingJob)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return;

            _hasPendingJob = false;
            ReleaseReactorSharedLocks();

            long completed = System.Diagnostics.Stopwatch.GetTimestamp();
            double ticks = completed - _scheduleTimestamp;
            _lastSolverMicroseconds = (float)(ticks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

            if (_pendingFrontBuffer == PendingFrontBufferBack)
                Swap(ref _front, ref _back);
            else if (_pendingFrontBuffer == PendingFrontBufferScratch)
                Swap(ref _front, ref _shiftScratch);

            _pendingFrontBuffer = PendingFrontBufferCurrent;
            _visualDirty = true;
            InspectReactorTelemetryAndDumpIfFaulted();
            InspectLatestTelemetryAndDumpIfFaulted();
            UploadVisualBuffer();
            UploadReactorVisualScalar();
            H8Memory.RegisterActiveJob(SystemID.Thermodynamics, default);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_nativeReady)
                return;

            _lastShiftSequence = shiftData.Sequence;
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryResolveArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return;

            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            tuning->LastShiftSequence = shiftData.Sequence;
        }

        public bool TryUpsertSource(uint sourceId, double3 aup, float intensityCelsiusPerSecond, float radiusMeters, uint profileHash)
        {
            if (!_nativeReady || _hasPendingJob || sourceId == 0u || radiusMeters <= 0f)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryResolveArray(vault, in _sources, MaxSourceCount, out NativeArray<HeatSourceDTO> sourceArray) ||
                !TryResolveArray(vault, in _sourceCount, 1, out NativeArray<int> sourceCountArray))
                return false;

            HeatSourceDTO* sources = (HeatSourceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceArray);
            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceCountArray);
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            int target = -1;
            for (int i = 0; i < count; i++)
            {
                if (sources[i].SourceId == sourceId)
                {
                    target = i;
                    break;
                }
            }

            if (target < 0)
            {
                if (count >= MaxSourceCount)
                    return false;
                target = count++;
            }

            HeatSourceDTO source;
            source.Aup = aup;
            source.IntensityCelsiusPerSecond = intensityCelsiusPerSecond;
            source.RadiusMeters = radiusMeters;
            source.FalloffExponent = 1.5f;
            source.ProfileHash = profileHash;
            source.SourceId = sourceId;
            source.Flags = HeatSourceDTO.FlagPersistent;
            source.ConductivityOverride = waterThermalConductivity;
            source.ConvectionGain = 1f;
            source.Phase01 = 0f;
            source.LastTouchedFrame = _frame;
            sources[target] = source;
            *countPtr = count;
            RemoveMockSources(sources, countPtr);
            _hasRealSources = HasNonMockSources(sources, countPtr);
            return true;
        }

        public bool TryRemoveSource(uint sourceId)
        {
            if (!_nativeReady || _hasPendingJob || sourceId == 0u)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryResolveArray(vault, in _sources, MaxSourceCount, out NativeArray<HeatSourceDTO> sourceArray) ||
                !TryResolveArray(vault, in _sourceCount, 1, out NativeArray<int> sourceCountArray))
                return false;

            HeatSourceDTO* sources = (HeatSourceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceArray);
            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceCountArray);
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            for (int i = 0; i < count; i++)
            {
                if (sources[i].SourceId != sourceId)
                    continue;

                sources[i] = sources[count - 1];
                *countPtr = count - 1;
                _hasRealSources = HasNonMockSources(sources, countPtr);
                return true;
            }

            return false;
        }

        private static void CullExpiredTransientSources(HeatSourceDTO* sources, int* countPtr, uint frame)
        {
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                HeatSourceDTO source = sources[i];
                if (source.RadiusMeters <= 0f || source.IntensityCelsiusPerSecond <= 0f)
                    continue;

                bool persistent = (source.Flags & HeatSourceDTO.FlagPersistent) != 0u;
                if (!persistent && frame - source.LastTouchedFrame > TransientSourceTtlFrames)
                    continue;

                if (write != i)
                    sources[write] = source;

                write++;
            }

            *countPtr = write;
        }

        private static bool TryIngestThermalSourceSignals(HeatSourceDTO* sources, int* countPtr, uint frame)
        {
            ReadOnlySpan<ThermalSourceSignal> signals = SignalBus<ThermalSourceSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return false;

            RemoveMockSources(sources, countPtr);
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            bool wrote = false;
            for (int i = 0; i < signals.Length; i++)
            {
                ThermalSourceSignal signal = signals[i];
                if (signal.RadiusMeters <= 0f || signal.IntensityCelsiusPerSecond <= 0f)
                    continue;

                uint sourceId = signal.SourceId != 0u ? signal.SourceId : BuildThermalSourceId(in signal);
                int target = FindSourceIndex(sources, count, sourceId);
                if (target < 0)
                {
                    if (count >= MaxSourceCount)
                        continue;

                    target = count;
                    count++;
                }

                HeatSourceDTO source;
                source.Aup = signal.PositionAup.ToAbsoluteDouble3();
                source.IntensityCelsiusPerSecond = math.max(0f, signal.IntensityCelsiusPerSecond);
                source.RadiusMeters = math.max(0f, signal.RadiusMeters);
                source.FalloffExponent = 1.5f;
                source.ProfileHash = BlackSmokerHash;
                source.SourceId = sourceId;
                source.Flags = 0u;
                source.ConductivityOverride = 0f;
                source.ConvectionGain = 1f;
                source.Phase01 = 0f;
                source.LastTouchedFrame = frame;
                sources[target] = source;
                wrote = true;
            }

            *countPtr = count;
            return wrote;
        }

        private static void RemoveMockSources(HeatSourceDTO* sources, int* countPtr)
        {
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                HeatSourceDTO source = sources[i];
                if ((source.Flags & HeatSourceDTO.FlagMock) != 0u)
                    continue;

                if (write != i)
                    sources[write] = source;

                write++;
            }

            *countPtr = write;
        }

        private static bool HasNonMockSources(HeatSourceDTO* sources, int* countPtr)
        {
            int count = math.clamp(*countPtr, 0, MaxSourceCount);
            for (int i = 0; i < count; i++)
            {
                HeatSourceDTO source = sources[i];
                if ((source.Flags & HeatSourceDTO.FlagMock) == 0u &&
                    source.RadiusMeters > 0f &&
                    source.IntensityCelsiusPerSecond > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindSourceIndex(HeatSourceDTO* sources, int count, uint sourceId)
        {
            for (int i = 0; i < count; i++)
            {
                if (sources[i].SourceId == sourceId)
                    return i;
            }

            return -1;
        }

        private static uint BuildThermalSourceId(in ThermalSourceSignal signal)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            hash = FoldHash(hash, (uint)signal.PositionAup.GridX, fnvPrime);
            hash = FoldHash(hash, (uint)(signal.PositionAup.GridX >> 32), fnvPrime);
            hash = FoldHash(hash, (uint)signal.PositionAup.GridY, fnvPrime);
            hash = FoldHash(hash, (uint)(signal.PositionAup.GridY >> 32), fnvPrime);
            hash = FoldHash(hash, (uint)signal.PositionAup.GridZ, fnvPrime);
            hash = FoldHash(hash, (uint)(signal.PositionAup.GridZ >> 32), fnvPrime);
            hash = FoldHash(hash, math.asuint(signal.PositionAup.LocalX), fnvPrime);
            hash = FoldHash(hash, math.asuint(signal.PositionAup.LocalY), fnvPrime);
            hash = FoldHash(hash, math.asuint(signal.PositionAup.LocalZ), fnvPrime);
            hash = FoldHash(hash, math.asuint(signal.RadiusMeters), fnvPrime);
            return hash == 0u ? 1u : hash;
        }

        private static uint FoldHash(uint hash, uint value, uint prime)
        {
            hash ^= value;
            hash *= prime;
            return hash;
        }

        public bool TryScheduleSample(NativeArray<double3> sampleAups, NativeArray<ThermalSampleResultDTO> results, JobHandle dependency, out JobHandle handle)
        {
            handle = dependency;
            if (!_nativeReady || _hasPendingJob || !sampleAups.IsCreated || !results.IsCreated || results.Length < sampleAups.Length)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryResolveArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryResolveArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return false;

            ThermalCellDTO* front = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(frontArray);
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            SampleTemperatureJob job;
            job.Cells = front;
            job.SampleAups = (double3*)NativeArrayUnsafeUtility.GetUnsafePtr(sampleAups);
            job.Results = (ThermalSampleResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(results);
            job.Tuning = *tuning;
            handle = job.Schedule(sampleAups.Length, 64, dependency);
            _sampleReadHandle = JobHandle.CombineDependencies(_sampleReadHandle, handle);
            H8Memory.RegisterActiveJob(SystemID.Thermodynamics, handle);
            return true;
        }

        public bool TrySampleTemperature(double3 aup, out ThermalSampleResultDTO result)
        {
            result = default;
            if (!_nativeReady || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryReadArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return false;

            ThermalGridTuningDTO tuning = tuningArray[0];
            int3 safeResolution = math.clamp(
                AbyssalThermalMath.SafeResolution(tuning.GridResolution),
                new int3(1, 1, 1),
                new int3(MaxResolution, MaxResolution, MaxResolution));
            float safeCellSize = math.max(0.001f, math.isfinite(tuning.CellSizeMeters) ? tuning.CellSizeMeters : DefaultCellSizeMeters);
            int3 cell = AbyssalThermalMath.MapAupToWrappedCell(aup, tuning.GridOriginAup, safeCellSize, safeResolution);
            int index = AbyssalThermalMath.Index(cell.x, cell.y, cell.z, safeResolution);
            ThermalCellDTO value = frontArray[index];
            double3 localDouble = aup - tuning.GridOriginAup;
            result.TemperatureCelsius = math.isfinite(value.TemperatureCelsius) ? value.TemperatureCelsius : DefaultAmbientTemperatureCelsius;
            result.ConvectionVelocityY = math.isfinite(value.ConvectionVelocityY) ? value.ConvectionVelocityY : 0f;
            result.CellIndex = (uint)index;
            result.Flags = value.Flags;
            result.LocalGridPosition = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            result.Conductivity = math.max(0.0001f, math.isfinite(value.ThermalConductivity) ? value.ThermalConductivity : DefaultWaterConductivity);
            return true;
        }

        public bool TryReadTuning(out ThermalGridTuningDTO tuning)
        {
            tuning = default;
            if (!_nativeReady)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TryWriteTuning(ThermalGridTuningDTO tuning)
        {
            if (!_nativeReady || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!vault.TryAcquireWriteLock(in _tuning, SystemID.CoreDiagnostics, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return false;

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length < 1)
                    return false;

                float safeQuality = ResolveVisualQualityWeight();
                tuning.CellSizeMeters = math.max(0.001f, math.isfinite(tuning.CellSizeMeters) ? tuning.CellSizeMeters : DefaultCellSizeMeters);
                tuning.AmbientTemperatureCelsius = math.isfinite(tuning.AmbientTemperatureCelsius) ? tuning.AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
                tuning.WaterThermalConductivity = math.max(0.0001f, math.isfinite(tuning.WaterThermalConductivity) ? tuning.WaterThermalConductivity : DefaultWaterConductivity);
                tuning.ConvectionSpeed = math.max(0f, math.isfinite(tuning.ConvectionSpeed) ? tuning.ConvectionSpeed : DefaultConvectionSpeed);
                tuning.GlobalQualityWeight = safeQuality;
                tuning.JacobiIterations = AbyssalThermalMath.ResolveJacobiIterations(safeQuality);
                tuning.GridResolution = new int3(MaxResolution, MaxResolution, MaxResolution);
                tuning.ActiveCellCount = MaxCellCount;
                tuning.DissipationPerStep = math.saturate(math.isfinite(tuning.DissipationPerStep) ? tuning.DissipationPerStep : DefaultDissipationPerStep);
                tuning.MaxStableTemperatureCelsius = math.max(tuning.AmbientTemperatureCelsius + 1f, math.isfinite(tuning.MaxStableTemperatureCelsius) ? tuning.MaxStableTemperatureCelsius : DefaultMaxStableTemperatureCelsius);
                tuning.HullInsulationConductivity = math.max(0.0001f, math.isfinite(tuning.HullInsulationConductivity) ? tuning.HullInsulationConductivity : DefaultHullInsulationConductivity);
                tuning.MockVolcanoIntensity = math.max(1f, math.isfinite(tuning.MockVolcanoIntensity) ? tuning.MockVolcanoIntensity : DefaultMockVolcanoIntensity);
                tuning.MockVolcanoRadiusMeters = math.max(1f, math.isfinite(tuning.MockVolcanoRadiusMeters) ? tuning.MockVolcanoRadiusMeters : DefaultMockVolcanoRadiusMeters);
                tuning.MockVolcanoCount = math.clamp(tuning.MockVolcanoCount, 1, 16);
                tuning.ShiftThresholdMeters = math.max(tuning.CellSizeMeters, math.isfinite(tuning.ShiftThresholdMeters) ? tuning.ShiftThresholdMeters : tuning.CellSizeMeters * 2f);
                tuning.ThermalDamageThresholdCelsius = math.max(tuning.AmbientTemperatureCelsius, math.isfinite(tuning.ThermalDamageThresholdCelsius) ? tuning.ThermalDamageThresholdCelsius : 58f);
                tuning.SubmarineHalfExtentX = math.max(0f, math.isfinite(tuning.SubmarineHalfExtentX) ? tuning.SubmarineHalfExtentX : 0f);
                tuning.SubmarineHalfExtentY = math.max(0f, math.isfinite(tuning.SubmarineHalfExtentY) ? tuning.SubmarineHalfExtentY : 0f);
                tuning.SubmarineHalfExtentZ = math.max(0f, math.isfinite(tuning.SubmarineHalfExtentZ) ? tuning.SubmarineHalfExtentZ : 0f);
                tuning.SimulationTickDeltaSeconds = DeterministicSimulationTickSeconds;
                tuningArray[0] = tuning;
                ApplyTuningToSerializedFields(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuning, SystemID.CoreDiagnostics);
            }
        }

        public bool TryReadTelemetry(int offsetFromLatest, out ThermalTelemetryEntry entry)
        {
            entry = default;
            if (!_nativeReady || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryReadArray(vault, in _telemetryRing, AbyssalThermalMath.TelemetryCapacity, out NativeArray<ThermalTelemetryEntry> ring))
                return false;

            if (_frame == 0u)
                return false;

            uint latestFrame = _frame;
            uint offset = (uint)math.max(0, offsetFromLatest);
            if (offset > latestFrame)
                return false;

            uint frame = latestFrame - offset;
            int index = (int)(frame % AbyssalThermalMath.TelemetryCapacity);
            entry = ring[index];
            return entry.Frame == frame;
        }

        private void EnsureNative()
        {
            _vault = EnsureVault();
            _front = Acquire<ThermalCellDTO>(BufferID.AbyssalThermalCellFront, MaxCellCount);
            _back = Acquire<ThermalCellDTO>(BufferID.AbyssalThermalCellBack, MaxCellCount);
            _injection = Acquire<ThermalCellDTO>(BufferID.AbyssalThermalCellInjection, MaxCellCount);
            _shiftScratch = Acquire<ThermalCellDTO>(BufferID.AbyssalThermalShiftScratch, MaxCellCount);
            _sources = Acquire<HeatSourceDTO>(BufferID.AbyssalThermalHeatSources, MaxSourceCount);
            _sourceCount = Acquire<int>(BufferID.AbyssalThermalSourceCount, 1);
            _tuning = Acquire<ThermalGridTuningDTO>(BufferID.AbyssalThermalTuning, 1);
            _sampleAups = Acquire<double3>(BufferID.AbyssalThermalSampleAups, SampleCapacity);
            _sampleResults = Acquire<ThermalSampleResultDTO>(BufferID.AbyssalThermalSampleResults, SampleCapacity);
            _telemetryRing = Acquire<ThermalTelemetryEntry>(BufferID.AbyssalThermalTelemetryRing, AbyssalThermalMath.TelemetryCapacity);
            _profiles = Acquire<HeatSourceProfileDTO>(BufferID.AbyssalThermalProfiles, MaxProfileCount);
            _profileCount = Acquire<int>(BufferID.AbyssalThermalProfileCount, 1);
            _solverConvergence = Acquire<ThermalSolverConvergenceStateDTO>(SolverConvergenceStateId, 1);
            _solverResidualSamples = Acquire<ThermalResidualSlot64>(SolverResidualSamplesId, AbyssalThermalMath.ResidualThreadSlotCount);
            _solverDumpLatch = Acquire<int>(SolverDumpLatchId, 1);
            EnsureReactorThermalVaultBuffers();

            ThermalGridTuningDTO tuning = BuildTuning();
            if (!TryResolveArray(_vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray) ||
                !TryResolveArray(_vault, in _sourceCount, 1, out NativeArray<int> sourceCountArray) ||
                !TryResolveArray(_vault, in _profileCount, 1, out NativeArray<int> profileCountArray) ||
                !TryResolveArray(_vault, in _solverConvergence, 1, out NativeArray<ThermalSolverConvergenceStateDTO> solverStateArray) ||
                !TryResolveArray(_vault, in _solverResidualSamples, AbyssalThermalMath.ResidualThreadSlotCount, out NativeArray<ThermalResidualSlot64> solverResidualArray) ||
                !TryResolveArray(_vault, in _solverDumpLatch, 1, out NativeArray<int> dumpLatchArray) ||
                !TryResolveArray(_vault, in _telemetryRing, AbyssalThermalMath.TelemetryCapacity, out NativeArray<ThermalTelemetryEntry> telemetryArray) ||
                !TryResolveArray(_vault, in _profiles, MaxProfileCount, out NativeArray<HeatSourceProfileDTO> profileArray) ||
                !TryResolveArray(_vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryResolveArray(_vault, in _back, MaxCellCount, out NativeArray<ThermalCellDTO> backArray) ||
                !TryResolveArray(_vault, in _injection, MaxCellCount, out NativeArray<ThermalCellDTO> injectionArray))
                throw new InvalidOperationException("Abyssal thermodynamics Vault pointer resolution failed.");

            ThermalGridTuningDTO* tuningPtr = (ThermalGridTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            int* sourceCount = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(sourceCountArray);
            int* profileCount = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(profileCountArray);
            ThermalSolverConvergenceStateDTO* solverState = (ThermalSolverConvergenceStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(solverStateArray);
            int* dumpLatch = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(dumpLatchArray);
            ThermalTelemetryEntry* telemetry = (ThermalTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryArray);
            HeatSourceProfileDTO* profiles = (HeatSourceProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileArray);

            *tuningPtr = tuning;
            *sourceCount = 0;
            *profileCount = 0;
            *solverState = default;
            *dumpLatch = 0;
            _hasRealSources = false;

            ThermalGridInitializeJob initJob;
            initJob.Front = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(frontArray);
            initJob.Back = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(backArray);
            initJob.Injection = (ThermalCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(injectionArray);
            initJob.AmbientTemperatureCelsius = tuning.AmbientTemperatureCelsius;
            initJob.WaterThermalConductivity = tuning.WaterThermalConductivity;

            JobHandle initHandle = initJob.Schedule(MaxCellCount, DefaultBatchSize);
            DispatcherJobFence.TryComplete(ref initHandle, forceComplete: true); // COLD SYNC JOB: deterministic first frame, no OS zero-fill dependency.
            _lastInitializedResolution = _activeResolution;

            for (int i = 0; i < AbyssalThermalMath.TelemetryCapacity; i++)
                telemetry[i] = default;
            for (int i = 0; i < MaxProfileCount; i++)
                profiles[i] = default;
            SeedDefaultProfiles();
            TryLoadProfilesCold();

            EnsureVisualBuffers();
            _nativeReady = true;
            _visualDirty = true;
        }

        private void RegisterRuntime()
        {
            TryRegisterRuntimeLanes();
            HectonFloatingOrigin.RegisterListener(this);
            _registeredOrigin = true;
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLate = false;
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

        private void ClearVaultHandles()
        {
            _front = default;
            _back = default;
            _injection = default;
            _shiftScratch = default;
            _sources = default;
            _sourceCount = default;
            _tuning = default;
            _sampleAups = default;
            _sampleResults = default;
            _telemetryRing = default;
            _profiles = default;
            _profileCount = default;
            _solverConvergence = default;
            _solverResidualSamples = default;
            _solverDumpLatch = default;
            ClearReactorThermalVaultHandles();
        }

        private bool CompleteThermalJobsForLifecycle()
        {
            if (_hasPendingJob)
            {
                DispatcherJobFence.BeginLateFrameSwapWindow();
                try
                {
                    if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                        return false;
                }
                finally
                {
                    DispatcherJobFence.EndLateFrameSwapWindow();
                }
            }

            _hasPendingJob = false;
            ReleaseReactorSharedLocks();
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _sampleReadHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            H8Memory.RegisterActiveJob(SystemID.Thermodynamics, default);
            return true;
        }

        private bool RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return true;

            if (!CompleteThermalJobsForLifecycle())
                return false;

            ReleaseOwnedVaultHandles(_vault);
            ClearVaultHandles();
            _vault = nextVault;
            _nativeReady = false;
            _lastInitializedResolution = 0;
            _visualDirty = false;
            return true;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _front);
            ReleaseOwnedVaultHandle(vault, ref _back);
            ReleaseOwnedVaultHandle(vault, ref _injection);
            ReleaseOwnedVaultHandle(vault, ref _shiftScratch);
            ReleaseOwnedVaultHandle(vault, ref _sources);
            ReleaseOwnedVaultHandle(vault, ref _sourceCount);
            ReleaseOwnedVaultHandle(vault, ref _tuning);
            ReleaseOwnedVaultHandle(vault, ref _sampleAups);
            ReleaseOwnedVaultHandle(vault, ref _sampleResults);
            ReleaseOwnedVaultHandle(vault, ref _telemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _profiles);
            ReleaseOwnedVaultHandle(vault, ref _profileCount);
            ReleaseOwnedVaultHandle(vault, ref _solverConvergence);
            ReleaseOwnedVaultHandle(vault, ref _solverResidualSamples);
            ReleaseOwnedVaultHandle(vault, ref _solverDumpLatch);
            ReleaseReactorThermalVaultHandles(vault);
        }

        private static void ReleaseOwnedVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.Thermodynamics)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private VaultGenerationHandle<T> Acquire<T>(BufferID id, int count) where T : struct
        {
            IDataVault vault = _vault ?? EnsureVault();
            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(id, count, SystemID.Thermodynamics, NativeArrayOptions.UninitializedMemory);
            if (!TryResolveArray(vault, in handle, count, out _))
                throw new InvalidOperationException("Abyssal thermodynamics Vault allocation failed.");
            return handle;
        }

        private static bool TryResolveArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   requiredLength >= 0 &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static ulong ThermodynamicsMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private IDataVault EnsureVault()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_vault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

            if (_vault != null)
                return _vault;

            throw new InvalidOperationException("Abyssal thermodynamics requires GlobalDataVault before boot.");
        }

        private ThermalGridTuningDTO BuildTuning()
        {
            float safeQuality = ResolveVisualQualityWeight();
            const float safeSimulationTickDelta = DeterministicSimulationTickSeconds;
            _activeResolution = MaxResolution;
            _activeCellCount = MaxCellCount;
            float safeCellSize = math.max(0.001f, math.isfinite(cellSizeMeters) ? cellSizeMeters : DefaultCellSizeMeters);
            double3 anchorAup = TryResolveAnchorAup(out double3 resolvedAnchorAup)
                ? resolvedAnchorAup
                : RuntimeOriginRoute.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
            double halfExtent = (_activeResolution * safeCellSize) * 0.5;
            if (!_gridOriginInitialized)
            {
                _gridOriginAup = anchorAup - new double3(halfExtent, halfExtent, halfExtent);
                _gridOriginInitialized = true;
            }

            double3 localAnchor = anchorAup - _gridOriginAup;
            double3 center = new double3(halfExtent, halfExtent, halfExtent);
            double3 deltaFromCenter = localAnchor - center;
            float threshold = safeCellSize * math.max(2f, _activeResolution * 0.25f);
            if (math.any(math.abs(new float3((float)deltaFromCenter.x, (float)deltaFromCenter.y, (float)deltaFromCenter.z)) > threshold))
            {
                int3 shiftCells = new int3(
                    (int)math.clamp(math.round((float)(deltaFromCenter.x / safeCellSize)), -(_activeResolution - 1), _activeResolution - 1),
                    (int)math.clamp(math.round((float)(deltaFromCenter.y / safeCellSize)), -(_activeResolution - 1), _activeResolution - 1),
                    (int)math.clamp(math.round((float)(deltaFromCenter.z / safeCellSize)), -(_activeResolution - 1), _activeResolution - 1));
                _pendingShiftCells += shiftCells;
                _gridOriginAup += new double3(shiftCells.x * safeCellSize, shiftCells.y * safeCellSize, shiftCells.z * safeCellSize);
            }

            ThermalGridTuningDTO tuning;
            tuning.GridOriginAup = _gridOriginAup;
            tuning.CellSizeMeters = safeCellSize;
            tuning.AmbientTemperatureCelsius = math.isfinite(ambientTemperatureCelsius) ? ambientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
            tuning.WaterThermalConductivity = math.max(0.0001f, math.isfinite(waterThermalConductivity) ? waterThermalConductivity : DefaultWaterConductivity);
            tuning.ConvectionSpeed = math.max(0f, math.isfinite(convectionSpeed) ? convectionSpeed : DefaultConvectionSpeed);
            tuning.GlobalQualityWeight = safeQuality;
            tuning.JacobiIterations = AbyssalThermalMath.ResolveJacobiIterations(safeQuality);
            tuning.GridResolution = new int3(_activeResolution, _activeResolution, _activeResolution);
            tuning.ActiveCellCount = _activeCellCount;
            tuning.DissipationPerStep = math.saturate(math.isfinite(dissipationPerStep) ? dissipationPerStep : DefaultDissipationPerStep);
            tuning.MaxStableTemperatureCelsius = DefaultMaxStableTemperatureCelsius;
            tuning.HullInsulationConductivity = DefaultHullInsulationConductivity;
            tuning.MockVolcanoIntensity = math.max(1f, math.isfinite(mockVolcanoIntensity) ? mockVolcanoIntensity : DefaultMockVolcanoIntensity);
            tuning.MockVolcanoRadiusMeters = math.max(1f, math.isfinite(mockVolcanoRadiusMeters) ? mockVolcanoRadiusMeters : DefaultMockVolcanoRadiusMeters);
            tuning.MockVolcanoCount = math.clamp(mockVolcanoCount, 1, 16);
            tuning.ShiftThresholdMeters = safeCellSize * math.max(2f, _activeResolution * 0.25f);
            tuning.ThermalDamageThresholdCelsius = 58f;
            tuning.Frame = ++_frame;
            tuning.StateHash = ComputeStateHash(_activeResolution, tuning.JacobiIterations, safeQuality);
            tuning.Flags = 0u;
            tuning.LastShiftSequence = _lastShiftSequence;
            tuning.SubmarineHalfExtentX = math.max(0f, math.isfinite(submarineHalfExtentsMeters.x) ? submarineHalfExtentsMeters.x : 0f);
            tuning.SubmarineHalfExtentY = math.max(0f, math.isfinite(submarineHalfExtentsMeters.y) ? submarineHalfExtentsMeters.y : 0f);
            tuning.SubmarineHalfExtentZ = math.max(0f, math.isfinite(submarineHalfExtentsMeters.z) ? submarineHalfExtentsMeters.z : 0f);
            tuning.SimulationTickDeltaSeconds = safeSimulationTickDelta;
            return tuning;
        }

        private bool TryResolveAnchorAup(out double3 anchorAup)
        {
            anchorAup = default;
            Vector3 runtimePosition = transform.position;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            anchorAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(anchorAup));
        }

        private float ResolveVisualQualityWeight()
        {
            if (useQualityOverride)
                return MathLodApproximation.SaturateFinite(qualityOverride, AbyssalThermalMath.AuthoritativeQualityWeight);

            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AbyssalThermalMath.AuthoritativeQualityWeight);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return MathLodApproximation.SaturateFinite(weight, AbyssalThermalMath.AuthoritativeQualityWeight);
        }

        private static uint ComputeStateHash(int resolution, int iterations, float quality)
        {
            uint hash = AbyssalThermalMath.Fnv1A(0u, (uint)resolution);
            hash = AbyssalThermalMath.Fnv1A(hash, (uint)iterations);
            hash = AbyssalThermalMath.Fnv1A(hash, math.asuint(quality));
            return hash;
        }

        private void ApplyTuningToSerializedFields(ThermalGridTuningDTO tuning)
        {
            cellSizeMeters = tuning.CellSizeMeters;
            ambientTemperatureCelsius = tuning.AmbientTemperatureCelsius;
            waterThermalConductivity = tuning.WaterThermalConductivity;
            convectionSpeed = tuning.ConvectionSpeed;
            dissipationPerStep = tuning.DissipationPerStep;
            mockVolcanoIntensity = tuning.MockVolcanoIntensity;
            mockVolcanoRadiusMeters = tuning.MockVolcanoRadiusMeters;
            mockVolcanoCount = math.clamp(tuning.MockVolcanoCount, 1, 16);
        }

        private void SeedDefaultProfiles()
        {
            HeatSourceProfileDTO* profile = stackalloc HeatSourceProfileDTO[1];
            profile[0].NameHash = BlackSmokerHash;
            profile[0].IntensityCelsiusPerSecond = DefaultMockVolcanoIntensity;
            profile[0].RadiusMeters = DefaultMockVolcanoRadiusMeters;
            profile[0].FalloffExponent = 1.55f;
            profile[0].ConvectionGain = 1f;
            profile[0].Flags = 0u;
            profile[0]._pad0 = 0u;
            profile[0]._pad1 = 0u;
            CommitHeatSourceProfiles(profile, 1);
        }

        private void TryLoadProfilesCold()
        {
#if !UNITY_EDITOR
            return;
#else
            string path = Path.Combine(Application.dataPath, "_SourceData", "Thermodynamics", "heat_source_profiles.csv");
            if (!File.Exists(path))
                return;

            long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (writeTicks == _lastProfileWriteTicks)
                return;

            Span<byte> csvScratch = stackalloc byte[CsvScratchBytes];
            int length = ReadProfileCsvBytes(path, csvScratch);
            if (length <= 0)
                return;

            HeatSourceProfileDTO* profileScratch = stackalloc HeatSourceProfileDTO[MaxProfileCount];
            int parsed = HeatSourceProfileCsvParser.Parse(csvScratch.Slice(0, length), profileScratch, MaxProfileCount);
            if (parsed > 0 && CommitHeatSourceProfiles(profileScratch, parsed))
            {
                _lastProfileWriteTicks = writeTicks;
            }
#endif
        }

        private bool CommitHeatSourceProfiles(HeatSourceProfileDTO* sourceProfiles, int sourceCount)
        {
            IDataVault vault = _vault;
            if (vault == null || sourceProfiles == null || sourceCount <= 0)
                return false;

            int safeCount = math.clamp(sourceCount, 0, MaxProfileCount);
            if (safeCount <= 0)
                return false;

            if (!vault.TryAcquireMutationGuard(HeatSourceProfileMutationGuardMask))
                return false;

            try
            {
                if (!TryResolveArray(vault, in _profiles, MaxProfileCount, out NativeArray<HeatSourceProfileDTO> profileArray) ||
                    !TryResolveArray(vault, in _profileCount, 1, out NativeArray<int> profileCountArray))
                    return false;

                HeatSourceProfileDTO* profiles = (HeatSourceProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileArray);
                for (int i = 0; i < safeCount; i++)
                    profiles[i] = sourceProfiles[i];
                for (int i = safeCount; i < MaxProfileCount; i++)
                    profiles[i] = default;

                profileCountArray[0] = safeCount;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(HeatSourceProfileMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        private static int ReadProfileCsvBytes(string path, Span<byte> scratch)
        {
            if (scratch.Length <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length <= 0 || stream.Length > scratch.Length)
                    return 0;

                int expected = (int)stream.Length;
                int total = 0;
                while (total < expected)
                {
                    int read = stream.Read(scratch.Slice(total, expected - total));
                    if (read <= 0)
                        return 0;

                    total += read;
                }

                return total == expected ? total : 0;
            }
        }
#endif

        private void InspectLatestTelemetryAndDumpIfFaulted()
        {
            if (!TryReadTelemetry(0, out ThermalTelemetryEntry entry))
                return;
            IDataVault vault = _vault;
            if (!TryResolveArray(vault, in _solverDumpLatch, 1, out NativeArray<int> dumpLatchArray))
                return;

            int* dumpLatch = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(dumpLatchArray);
            const uint immediateDumpFaultMask = AbyssalThermalMath.TelemetryFlagNaN | AbyssalThermalMath.TelemetryFlagDivergent;
            bool immediateFault = (entry.Flags & immediateDumpFaultMask) != 0u;
            bool maxIterationFault = (entry.Flags & AbyssalThermalMath.TelemetryFlagMaxIterations) != 0u;
            if (!immediateFault && !maxIterationFault)
            {
                *dumpLatch = 0;
                return;
            }

            if (!immediateFault && !HasConsecutiveMaxIterationFaults(5))
                return;

            uint faultKey = entry.Flags & immediateDumpFaultMask;
            if (maxIterationFault)
                faultKey |= AbyssalThermalMath.TelemetryFlagMaxIterations;
            int faultKeyInt = (int)faultKey;
            if (*dumpLatch == faultKeyInt)
                return;

            DumpBlackBox();
            *dumpLatch = faultKeyInt;
        }

        private bool HasConsecutiveMaxIterationFaults(int requiredFrames)
        {
            int frames = math.max(1, requiredFrames);
            for (int i = 0; i < frames; i++)
            {
                if (!TryReadTelemetry(i, out ThermalTelemetryEntry entry) ||
                    (entry.Flags & AbyssalThermalMath.TelemetryFlagMaxIterations) == 0u)
                {
                    return false;
                }
            }

            return true;
        }

        private void DumpBlackBox()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryReadArray(vault, in _telemetryRing, AbyssalThermalMath.TelemetryCapacity, out NativeArray<ThermalTelemetryEntry> ringArray))
                return;

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
            int bytes = UnsafeUtility.SizeOf<ThermalTelemetryEntry>() * AbyssalThermalMath.TelemetryCapacity;
            ThermalTelemetryEntry* ring = (ThermalTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ringArray);
            ReadOnlySpan<byte> snapshot = new ReadOnlySpan<byte>(ring, bytes);

            WriteDumpFile(Path.Combine(directory, "Dump_THERMO_SURGEON.bin"), snapshot, bytes);
            WriteDumpFile(Path.Combine(directory, "Dump_SHINOBU_203.bin"), snapshot, bytes);
        }

        private static void WriteDumpFile(string path, ReadOnlySpan<byte> bytes, int byteCount)
        {
            NativeFaultDumpWriter.TryWriteAll(path, bytes, byteCount);
        }

        private void EnsureVisualBuffers()
        {
            int stride = UnsafeUtility.SizeOf<ThermalCellDTO>();
            if (IsUsableVisualBuffer(_thermalCellsBufferA, stride) && IsUsableVisualBuffer(_thermalCellsBufferB, stride))
                return;

            ReleaseVisualBuffers();
            _thermalCellsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxCellCount, stride);
            _thermalCellsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxCellCount, stride);
            _thermalCellsUploadParity = 0;
        }

        private static bool IsUsableVisualBuffer(GraphicsBuffer buffer, int stride)
        {
            return buffer != null && buffer.count == MaxCellCount && buffer.stride == stride;
        }

        private void ReleaseVisualBuffers()
        {
            _thermalCellsBufferA?.Release();
            _thermalCellsBufferB?.Release();
            _thermalCellsBufferA = null;
            _thermalCellsBufferB = null;
            ReleaseReactorThermalVisualBuffer();
        }

        private void UploadVisualBuffer()
        {
            if (!_visualDirty)
                return;

            EnsureVisualBuffers();
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryReadArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> front))
                return;

            GraphicsBuffer uploadBuffer = (_thermalCellsUploadParity & 1) == 0 ? _thermalCellsBufferA : _thermalCellsBufferB;
            int stride = UnsafeUtility.SizeOf<ThermalCellDTO>();
            NativeArray<ThermalCellDTO> writeWindow = default;
            bool mapped = false;
            try
            {
                writeWindow = uploadBuffer.LockBufferForWrite<ThermalCellDTO>(0, _activeCellCount);
                mapped = true;
                UnsafeUtility.MemCpy(
                    NativeArrayUnsafeUtility.GetUnsafePtr(writeWindow),
                    NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(front),
                    (long)_activeCellCount * stride);
            }
            finally
            {
                if (mapped)
                    uploadBuffer.UnlockBufferAfterWrite<ThermalCellDTO>(_activeCellCount);
            }

            Shader.SetGlobalBuffer(ThermalCellsBufferId, uploadBuffer);
            if (TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
            {
                ThermalGridTuningDTO tuning = tuningArray[0];
                int3 safeResolution = math.clamp(
                    AbyssalThermalMath.SafeResolution(tuning.GridResolution),
                    new int3(1, 1, 1),
                    new int3(MaxResolution, MaxResolution, MaxResolution));
                float safeCellSize = math.max(0.001f, math.isfinite(tuning.CellSizeMeters) ? tuning.CellSizeMeters : DefaultCellSizeMeters);
                float safeAmbient = math.isfinite(tuning.AmbientTemperatureCelsius) ? tuning.AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
                float safeQuality = ResolveVisualQualityWeight();
                Shader.SetGlobalVector(ThermalGridMetaId, new Vector4(safeResolution.x, safeCellSize, safeAmbient, safeQuality));
                double3 origin = tuning.GridOriginAup;
                float safeConvection = math.max(0f, math.isfinite(tuning.ConvectionSpeed) ? tuning.ConvectionSpeed : DefaultConvectionSpeed);
                Shader.SetGlobalVector(ThermalGridOriginId, new Vector4((float)origin.x, (float)origin.y, (float)origin.z, safeConvection));
            }
            else
            {
                Shader.SetGlobalVector(ThermalGridMetaId, new Vector4(_activeResolution, math.max(0.001f, cellSizeMeters), ambientTemperatureCelsius, ResolveVisualQualityWeight()));
            }

            _thermalCellsUploadParity ^= 1;
            _visualDirty = false;
        }

        private static void Swap<T>(ref VaultGenerationHandle<T> a, ref VaultGenerationHandle<T> b) where T : struct
        {
            VaultGenerationHandle<T> tmp = a;
            a = b;
            b = tmp;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawThermalSlice || !_nativeReady || _hasPendingJob)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryReadArray(vault, in _front, MaxCellCount, out NativeArray<ThermalCellDTO> frontArray) ||
                !TryReadArray(vault, in _tuning, 1, out NativeArray<ThermalGridTuningDTO> tuningArray))
                return;

            ThermalGridTuningDTO tuning = tuningArray[0];
            int3 safeResolution = math.clamp(
                AbyssalThermalMath.SafeResolution(tuning.GridResolution),
                new int3(1, 1, 1),
                new int3(MaxResolution, MaxResolution, MaxResolution));
            int res = safeResolution.x;
            int y = math.clamp((int)math.round((res - 1) * math.saturate(gizmoSliceY01)), 0, res - 1);
            float cell = math.max(0.001f, math.isfinite(tuning.CellSizeMeters) ? tuning.CellSizeMeters : DefaultCellSizeMeters);
            float ambient = math.isfinite(tuning.AmbientTemperatureCelsius) ? tuning.AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
            float maxStable = math.max(ambient + 1f, math.isfinite(tuning.MaxStableTemperatureCelsius) ? tuning.MaxStableTemperatureCelsius : DefaultMaxStableTemperatureCelsius);
            Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(tuning.GridOriginAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            Vector3 size = new Vector3(cell * 0.92f, 0.03f, cell * 0.92f);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int index = AbyssalThermalMath.Index(x, y, z, safeResolution);
                    float temp = math.isfinite(frontArray[index].TemperatureCelsius) ? frontArray[index].TemperatureCelsius : ambient;
                    float t = math.saturate((temp - ambient) / math.max(1f, maxStable - ambient));
                    Color coldToHot = t < 0.5f
                        ? Color.Lerp(Color.blue, Color.yellow, t * 2f)
                        : Color.Lerp(Color.yellow, Color.white, (t - 0.5f) * 2f);
                    coldToHot.a = 0.35f;
                    Gizmos.color = coldToHot;
                    Gizmos.DrawCube(origin + new Vector3((x + 0.5f) * cell, (y + 0.5f) * cell, (z + 0.5f) * cell), size);
                }
            }
        }
#endif
    }
}
