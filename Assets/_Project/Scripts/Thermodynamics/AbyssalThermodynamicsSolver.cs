using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Thermodynamics/Abyssal Thermodynamics Solver")]
    public sealed unsafe class AbyssalThermodynamicsSolver : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener
    {
        public const int MinResolution = 16;
        public const int MaxResolution = 32;
        public const int MaxCellCount = MaxResolution * MaxResolution * MaxResolution;
        public const int MaxSourceCount = 128;
        public const int MaxProfileCount = 32;
        public const int SampleCapacity = 512;
        public const int CsvScratchBytes = 8192;

        private const int DefaultBatchSize = 64;
        private const float ResolutionHysteresisSeconds = 3f;
        private const float DeterministicSimulationTickSeconds = 1f / 60f;
        private const int MaxSimulationCadenceFrames = 12;
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
        private VaultBufferHandle<ThermalCellDTO> _front;
        private VaultBufferHandle<ThermalCellDTO> _back;
        private VaultBufferHandle<ThermalCellDTO> _injection;
        private VaultBufferHandle<ThermalCellDTO> _shiftScratch;
        private VaultBufferHandle<HeatSourceDTO> _sources;
        private VaultBufferHandle<int> _sourceCount;
        private VaultBufferHandle<ThermalGridTuningDTO> _tuning;
        private VaultBufferHandle<double3> _sampleAups;
        private VaultBufferHandle<ThermalSampleResultDTO> _sampleResults;
        private VaultBufferHandle<ThermalTelemetryEntry> _telemetryRing;
        private VaultBufferHandle<byte> _profileBytes;
        private VaultBufferHandle<HeatSourceProfileDTO> _profiles;
        private VaultBufferHandle<int> _profileCount;
        private VaultBufferHandle<ThermalSolverConvergenceStateDTO> _solverConvergence;
        private VaultBufferHandle<ThermalResidualSlot64> _solverResidualSamples;
        private VaultBufferHandle<int> _solverDumpLatch;

        private JobHandle _pendingHandle;
        private JobHandle _sampleReadHandle;
        private bool _hasPendingJob;
        private bool _nativeReady;
        private bool _registeredUpdate;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredOrigin;
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
        private int _desiredResolution = MaxResolution;
        private float _resolutionSwitchTimer;
        private bool _resolutionInitialized;
        private int _cadenceFrameCursor;

        public static AbyssalThermodynamicsSolver ActiveRuntimeInstance { get; private set; }
        public bool IsInitialized => _nativeReady;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ThermalCellLayoutValidator.ValidateThermalCellLayout() ||
                !ThermalCellLayoutValidator.ValidateThermalSolverConvergenceLayout())
                throw new InvalidOperationException("ThermalCellDTO ABI mismatch.");

            EnsureNative();
            RegisterRuntime();
            ActiveRuntimeInstance = this;
        }

        private void OnDisable()
        {
            if (_hasPendingJob)
            {
                DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
                _hasPendingJob = false;
            }

            DispatcherJobFence.TryComplete(ref _sampleReadHandle, forceComplete: true);

            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredOrigin)
                HectonFloatingOrigin.UnregisterListener(this);

            _registeredUpdate = false;
            _registeredSlow = false;
            _registeredLate = false;
            _registeredOrigin = false;

            ReleaseVisualBuffers();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void Tick(float deltaTime)
        {
            if (!_nativeReady || _hasPendingJob)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            ThermalCellDTO* front = (ThermalCellDTO*)_front.ResolvePointer(vault);
            ThermalCellDTO* back = (ThermalCellDTO*)_back.ResolvePointer(vault);
            ThermalCellDTO* injection = (ThermalCellDTO*)_injection.ResolvePointer(vault);
            ThermalCellDTO* scratch = (ThermalCellDTO*)_shiftScratch.ResolvePointer(vault);
            HeatSourceDTO* sources = (HeatSourceDTO*)_sources.ResolvePointer(vault);
            int* sourceCount = (int*)_sourceCount.ResolvePointer(vault);
            ThermalTelemetryEntry* telemetry = (ThermalTelemetryEntry*)_telemetryRing.ResolvePointer(vault);
            ThermalGridTuningDTO* tuningPtr = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            ThermalSolverConvergenceStateDTO* solverState = (ThermalSolverConvergenceStateDTO*)_solverConvergence.ResolvePointer(vault);
            ThermalResidualSlot64* solverResiduals = (ThermalResidualSlot64*)_solverResidualSamples.ResolvePointer(vault);

            if (front == null ||
                back == null ||
                injection == null ||
                scratch == null ||
                sources == null ||
                sourceCount == null ||
                telemetry == null ||
                tuningPtr == null ||
                solverState == null ||
                solverResiduals == null)
                return;

            float quality = ResolveQualityWeight();
            uint nextFrame = _frame + 1u;
            CullExpiredTransientSources(sources, sourceCount, nextFrame);
            TryIngestThermalSourceSignals(sources, sourceCount, nextFrame);
            _hasRealSources = HasNonMockSources(sources, sourceCount);

            int cadenceFrames = ResolveSimulationCadenceFrames(quality);
            if (_frame != 0u)
            {
                _cadenceFrameCursor++;
                if (_cadenceFrameCursor < cadenceFrames)
                    return;
            }

            _cadenceFrameCursor = 0;
            float simulationTickDelta = DeterministicSimulationTickSeconds * cadenceFrames;
            ThermalGridTuningDTO tuning = BuildTuning(quality, simulationTickDelta);
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

            int jacobiPasses = math.max(1, tuning.JacobiIterations);
            ThermalGridTuningDTO passTuning = tuning;
            passTuning.JacobiIterations = 1;
            float targetTolerance = AbyssalThermalMath.ResolveSolverTargetTolerance(tuning.GlobalQualityWeight);
            float baseOmega = AbyssalThermalMath.ResolveSolverOmega(tuning.GlobalQualityWeight);
            int residualSampleMask = AbyssalThermalMath.ResolveResidualSampleMask(tuning.GlobalQualityWeight);
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

                HeatDiffusionSolverJob diffusionJob;
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

        public void SlowTick()
        {
            // Profile IO is boot/editor driven. Runtime cadence must not poll the filesystem.
        }

        public void LateFrameTick()
        {
            if (!_nativeReady || !_hasPendingJob)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return;

            _hasPendingJob = false;

            long completed = System.Diagnostics.Stopwatch.GetTimestamp();
            double ticks = completed - _scheduleTimestamp;
            _lastSolverMicroseconds = (float)(ticks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

            if (_pendingFrontBuffer == PendingFrontBufferBack)
                Swap(ref _front, ref _back);
            else if (_pendingFrontBuffer == PendingFrontBufferScratch)
                Swap(ref _front, ref _shiftScratch);

            _pendingFrontBuffer = PendingFrontBufferCurrent;
            _visualDirty = true;
            InspectLatestTelemetryAndDumpIfFaulted();
            UploadVisualBuffer();
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

            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (tuning != null)
            {
                tuning->LastShiftSequence = shiftData.Sequence;
            }
        }

        public bool TryUpsertSource(uint sourceId, double3 aup, float intensityCelsiusPerSecond, float radiusMeters, uint profileHash)
        {
            if (!_nativeReady || _hasPendingJob || sourceId == 0u || radiusMeters <= 0f)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            HeatSourceDTO* sources = (HeatSourceDTO*)_sources.ResolvePointer(vault);
            int* countPtr = (int*)_sourceCount.ResolvePointer(vault);
            if (sources == null || countPtr == null)
                return false;

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

            HeatSourceDTO* sources = (HeatSourceDTO*)_sources.ResolvePointer(vault);
            int* countPtr = (int*)_sourceCount.ResolvePointer(vault);
            if (sources == null || countPtr == null)
                return false;

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

            ThermalCellDTO* front = (ThermalCellDTO*)_front.ResolvePointer(vault);
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (front == null || tuning == null)
                return false;

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

            ThermalCellDTO* front = (ThermalCellDTO*)_front.ResolvePointer(vault);
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (front == null || tuning == null)
                return false;

            int3 safeResolution = math.clamp(
                AbyssalThermalMath.SafeResolution(tuning->GridResolution),
                new int3(1, 1, 1),
                new int3(MaxResolution, MaxResolution, MaxResolution));
            float safeCellSize = math.max(0.001f, math.isfinite(tuning->CellSizeMeters) ? tuning->CellSizeMeters : DefaultCellSizeMeters);
            int3 cell = AbyssalThermalMath.MapAupToWrappedCell(aup, tuning->GridOriginAup, safeCellSize, safeResolution);
            int index = AbyssalThermalMath.Index(cell.x, cell.y, cell.z, safeResolution);
            ThermalCellDTO value = front[index];
            double3 localDouble = aup - tuning->GridOriginAup;
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

            ThermalGridTuningDTO* ptr = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (ptr == null)
                return false;
            tuning = *ptr;
            return true;
        }

        public bool TryWriteTuning(ThermalGridTuningDTO tuning)
        {
            if (!_nativeReady || _hasPendingJob)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            ThermalGridTuningDTO* ptr = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (ptr == null)
                return false;

            tuning.CellSizeMeters = math.max(0.001f, math.isfinite(tuning.CellSizeMeters) ? tuning.CellSizeMeters : DefaultCellSizeMeters);
            tuning.AmbientTemperatureCelsius = math.isfinite(tuning.AmbientTemperatureCelsius) ? tuning.AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
            tuning.WaterThermalConductivity = math.max(0.0001f, math.isfinite(tuning.WaterThermalConductivity) ? tuning.WaterThermalConductivity : DefaultWaterConductivity);
            tuning.ConvectionSpeed = math.max(0f, math.isfinite(tuning.ConvectionSpeed) ? tuning.ConvectionSpeed : DefaultConvectionSpeed);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.JacobiIterations = AbyssalThermalMath.ResolveJacobiIterations(tuning.GlobalQualityWeight);
            tuning.GridResolution = math.clamp(
                AbyssalThermalMath.SafeResolution(tuning.GridResolution),
                new int3(MinResolution, MinResolution, MinResolution),
                new int3(MaxResolution, MaxResolution, MaxResolution));
            tuning.ActiveCellCount = math.clamp(tuning.GridResolution.x * tuning.GridResolution.y * tuning.GridResolution.z, 1, MaxCellCount);
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
            tuning.SimulationTickDeltaSeconds = SanitizeSimulationTickDelta(tuning.SimulationTickDeltaSeconds);
            *ptr = tuning;
            ApplyTuningToSerializedFields(tuning);
            return true;
        }

        public bool TryReadTelemetry(int offsetFromLatest, out ThermalTelemetryEntry entry)
        {
            entry = default;
            if (!_nativeReady)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            ThermalTelemetryEntry* ring = (ThermalTelemetryEntry*)_telemetryRing.ResolvePointer(vault);
            if (ring == null)
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
            _profileBytes = Acquire<byte>(BufferID.AbyssalThermalProfileBytes, CsvScratchBytes);
            _profiles = Acquire<HeatSourceProfileDTO>(BufferID.AbyssalThermalProfiles, MaxProfileCount);
            _profileCount = Acquire<int>(BufferID.AbyssalThermalProfileCount, 1);
            _solverConvergence = Acquire<ThermalSolverConvergenceStateDTO>(SolverConvergenceStateId, 1);
            _solverResidualSamples = Acquire<ThermalResidualSlot64>(SolverResidualSamplesId, AbyssalThermalMath.ResidualThreadSlotCount);
            _solverDumpLatch = Acquire<int>(SolverDumpLatchId, 1);

            ThermalGridTuningDTO tuning = BuildTuning(ResolveQualityWeight(), DeterministicSimulationTickSeconds);
            ThermalGridTuningDTO* tuningPtr = (ThermalGridTuningDTO*)_tuning.ResolvePointer(_vault);
            int* sourceCount = (int*)_sourceCount.ResolvePointer(_vault);
            int* profileCount = (int*)_profileCount.ResolvePointer(_vault);
            ThermalSolverConvergenceStateDTO* solverState = (ThermalSolverConvergenceStateDTO*)_solverConvergence.ResolvePointer(_vault);
            ThermalResidualSlot64* solverResiduals = (ThermalResidualSlot64*)_solverResidualSamples.ResolvePointer(_vault);
            int* dumpLatch = (int*)_solverDumpLatch.ResolvePointer(_vault);
            ThermalTelemetryEntry* telemetry = (ThermalTelemetryEntry*)_telemetryRing.ResolvePointer(_vault);
            HeatSourceProfileDTO* profiles = (HeatSourceProfileDTO*)_profiles.ResolvePointer(_vault);
            if (tuningPtr == null || sourceCount == null || profileCount == null || solverState == null || solverResiduals == null || dumpLatch == null || telemetry == null || profiles == null)
                throw new InvalidOperationException("Abyssal thermodynamics Vault pointer resolution failed.");

            *tuningPtr = tuning;
            *sourceCount = 0;
            *profileCount = 0;
            *solverState = default;
            *dumpLatch = 0;
            _hasRealSources = false;

            ThermalGridInitializeJob initJob;
            initJob.Front = (ThermalCellDTO*)_front.ResolvePointer(_vault);
            initJob.Back = (ThermalCellDTO*)_back.ResolvePointer(_vault);
            initJob.Injection = (ThermalCellDTO*)_injection.ResolvePointer(_vault);
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
            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            HectonFloatingOrigin.RegisterListener(this);
            _registeredOrigin = true;
        }

        private VaultBufferHandle<T> Acquire<T>(BufferID id, int count) where T : struct
        {
            IDataVault vault = _vault ?? EnsureVault();
            VaultBufferHandle<T> handle = vault.GetBufferHandle<T>(id, count, SystemID.Thermodynamics, NativeArrayOptions.UninitializedMemory);
            if (!handle.IsCreated)
                throw new InvalidOperationException("Abyssal thermodynamics Vault allocation failed.");
            return handle;
        }

        private IDataVault EnsureVault()
        {
            if (_vault != null)
                return _vault;

            _vault = GlobalRegistry.DataVault;
            if (_vault != null)
                return _vault;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
            {
                _vault = latest;
                return _vault;
            }

            throw new InvalidOperationException("Abyssal thermodynamics requires GlobalDataVault before boot.");
        }

        private ThermalGridTuningDTO BuildTuning(float quality, float simulationTickDeltaSeconds)
        {
            float safeQuality = math.saturate(math.isfinite(quality) ? quality : 1f);
            float safeSimulationTickDelta = SanitizeSimulationTickDelta(simulationTickDeltaSeconds);
            _activeResolution = ResolveStableResolution(safeQuality, safeSimulationTickDelta);
            _activeCellCount = _activeResolution * _activeResolution * _activeResolution;
            float safeCellSize = math.max(0.001f, math.isfinite(cellSizeMeters) ? cellSizeMeters : DefaultCellSizeMeters);
            double3 anchorAup = TryResolveAnchorAup(out double3 resolvedAnchorAup)
                ? resolvedAnchorAup
                : GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
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

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!MathGuard.IsFinite(in resolvedAup))
                return false;

            anchorAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(anchorAup));
        }

        private float ResolveQualityWeight()
        {
            if (useQualityOverride)
                return math.saturate(math.isfinite(qualityOverride) ? qualityOverride : 1f);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static int ResolveSimulationCadenceFrames(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float smooth = q * q * (3f - (2f * q));
            return math.clamp((int)math.round(math.lerp(MaxSimulationCadenceFrames, 1f, smooth)), 1, MaxSimulationCadenceFrames);
        }

        private static float SanitizeSimulationTickDelta(float value)
        {
            return math.clamp(math.isfinite(value) ? value : DeterministicSimulationTickSeconds, DeterministicSimulationTickSeconds, DeterministicSimulationTickSeconds * MaxSimulationCadenceFrames);
        }

        private int ResolveStableResolution(float quality, float deltaTime)
        {
            int resolved = AbyssalThermalMath.ResolveActiveResolution(quality, MinResolution, MaxResolution);
            if (!_resolutionInitialized)
            {
                _resolutionInitialized = true;
                _desiredResolution = resolved;
                _resolutionSwitchTimer = 0f;
                return resolved;
            }

            if (resolved == _activeResolution)
            {
                _desiredResolution = resolved;
                _resolutionSwitchTimer = 0f;
                return _activeResolution;
            }

            if (resolved != _desiredResolution)
            {
                _desiredResolution = resolved;
                _resolutionSwitchTimer = 0f;
                return _activeResolution;
            }

            _resolutionSwitchTimer += math.max(0f, deltaTime);
            if (_resolutionSwitchTimer < ResolutionHysteresisSeconds)
                return _activeResolution;

            _resolutionSwitchTimer = 0f;
            return _desiredResolution;
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
            IDataVault vault = _vault;
            if (vault == null)
                return;

            HeatSourceProfileDTO* profiles = (HeatSourceProfileDTO*)_profiles.ResolvePointer(vault);
            int* count = (int*)_profileCount.ResolvePointer(vault);
            if (profiles == null || count == null)
                return;

            profiles[0].NameHash = BlackSmokerHash;
            profiles[0].IntensityCelsiusPerSecond = DefaultMockVolcanoIntensity;
            profiles[0].RadiusMeters = DefaultMockVolcanoRadiusMeters;
            profiles[0].FalloffExponent = 1.55f;
            profiles[0].ConvectionGain = 1f;
            profiles[0].Flags = 0u;
            profiles[0]._pad0 = 0u;
            profiles[0]._pad1 = 0u;
            *count = 1;
        }

        private void TryLoadProfilesCold()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "heat_source_profiles.csv");
            if (!File.Exists(path))
                return;

            long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (writeTicks == _lastProfileWriteTicks)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            byte* scratch = (byte*)_profileBytes.ResolvePointer(vault);
            HeatSourceProfileDTO* profiles = (HeatSourceProfileDTO*)_profiles.ResolvePointer(vault);
            int* count = (int*)_profileCount.ResolvePointer(vault);
            if (scratch == null || profiles == null || count == null)
                return;

            int length;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                length = stream.Read(new Span<byte>(scratch, CsvScratchBytes));
            }

            int parsed = HeatSourceProfileCsvParser.Parse(new ReadOnlySpan<byte>(scratch, length), profiles, MaxProfileCount);
            if (parsed > 0)
            {
                *count = parsed;
                _lastProfileWriteTicks = writeTicks;
            }
        }

        private void InspectLatestTelemetryAndDumpIfFaulted()
        {
            if (!TryReadTelemetry(0, out ThermalTelemetryEntry entry))
                return;
            IDataVault vault = _vault;
            int* dumpLatch = vault != null && _solverDumpLatch.IsCreated
                ? (int*)_solverDumpLatch.ResolvePointer(vault)
                : null;
            if (dumpLatch == null)
                return;

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

            ThermalTelemetryEntry* ring = (ThermalTelemetryEntry*)_telemetryRing.ResolvePointer(vault);
            if (ring == null)
                return;

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
            Directory.CreateDirectory(directory);
            long bytes = UnsafeUtility.SizeOf<ThermalTelemetryEntry>() * AbyssalThermalMath.TelemetryCapacity;

            WriteDumpFile(Path.Combine(directory, "Dump_THERMO_SURGEON.bin"), ring, bytes);
            WriteDumpFile(Path.Combine(directory, "Dump_SHINOBU_203.bin"), ring, bytes);
        }

        private static void WriteDumpFile(string path, ThermalTelemetryEntry* ring, long bytes)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using UnmanagedMemoryStream unmanaged = new UnmanagedMemoryStream((byte*)ring, bytes);
            unmanaged.CopyTo(stream);
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
        }

        private void UploadVisualBuffer()
        {
            if (!_visualDirty)
                return;

            EnsureVisualBuffers();
            IDataVault vault = _vault;
            if (vault == null)
                return;

            NativeArray<ThermalCellDTO> front = _front.Resolve(vault);
            if (!front.IsCreated)
                return;

            GraphicsBuffer uploadBuffer = (_thermalCellsUploadParity & 1) == 0 ? _thermalCellsBufferA : _thermalCellsBufferB;
            int stride = UnsafeUtility.SizeOf<ThermalCellDTO>();
            NativeArray<ThermalCellDTO> writeWindow = uploadBuffer.LockBufferForWrite<ThermalCellDTO>(0, _activeCellCount);
            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(writeWindow),
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(front),
                (long)_activeCellCount * stride);
            uploadBuffer.UnlockBufferAfterWrite(_activeCellCount);

            Shader.SetGlobalBuffer(ThermalCellsBufferId, uploadBuffer);
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (tuning != null)
            {
                int3 safeResolution = math.clamp(
                    AbyssalThermalMath.SafeResolution(tuning->GridResolution),
                    new int3(1, 1, 1),
                    new int3(MaxResolution, MaxResolution, MaxResolution));
                float safeCellSize = math.max(0.001f, math.isfinite(tuning->CellSizeMeters) ? tuning->CellSizeMeters : DefaultCellSizeMeters);
                float safeAmbient = math.isfinite(tuning->AmbientTemperatureCelsius) ? tuning->AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
                float safeQuality = math.saturate(math.isfinite(tuning->GlobalQualityWeight) ? tuning->GlobalQualityWeight : ResolveQualityWeight());
                Shader.SetGlobalVector(ThermalGridMetaId, new Vector4(safeResolution.x, safeCellSize, safeAmbient, safeQuality));
                double3 origin = tuning->GridOriginAup;
                float safeConvection = math.max(0f, math.isfinite(tuning->ConvectionSpeed) ? tuning->ConvectionSpeed : DefaultConvectionSpeed);
                Shader.SetGlobalVector(ThermalGridOriginId, new Vector4((float)origin.x, (float)origin.y, (float)origin.z, safeConvection));
            }
            else
            {
                Shader.SetGlobalVector(ThermalGridMetaId, new Vector4(_activeResolution, math.max(0.001f, cellSizeMeters), ambientTemperatureCelsius, ResolveQualityWeight()));
            }

            _thermalCellsUploadParity ^= 1;
            _visualDirty = false;
        }

        private static void Swap<T>(ref VaultBufferHandle<T> a, ref VaultBufferHandle<T> b) where T : struct
        {
            VaultBufferHandle<T> tmp = a;
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

            ThermalCellDTO* front = (ThermalCellDTO*)_front.ResolvePointer(vault);
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (front == null || tuning == null)
                return;

            int3 safeResolution = math.clamp(
                AbyssalThermalMath.SafeResolution(tuning->GridResolution),
                new int3(1, 1, 1),
                new int3(MaxResolution, MaxResolution, MaxResolution));
            int res = safeResolution.x;
            int y = math.clamp((int)math.round((res - 1) * math.saturate(gizmoSliceY01)), 0, res - 1);
            float cell = math.max(0.001f, math.isfinite(tuning->CellSizeMeters) ? tuning->CellSizeMeters : DefaultCellSizeMeters);
            float ambient = math.isfinite(tuning->AmbientTemperatureCelsius) ? tuning->AmbientTemperatureCelsius : DefaultAmbientTemperatureCelsius;
            float maxStable = math.max(ambient + 1f, math.isfinite(tuning->MaxStableTemperatureCelsius) ? tuning->MaxStableTemperatureCelsius : DefaultMaxStableTemperatureCelsius);
            Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(tuning->GridOriginAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            Vector3 size = new Vector3(cell * 0.92f, 0.03f, cell * 0.92f);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int index = AbyssalThermalMath.Index(x, y, z, safeResolution);
                    float temp = math.isfinite(front[index].TemperatureCelsius) ? front[index].TemperatureCelsius : ambient;
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
