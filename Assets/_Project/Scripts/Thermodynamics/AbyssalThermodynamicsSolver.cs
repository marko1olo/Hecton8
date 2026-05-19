using System;
using System.IO;
using Hecton8.Core;
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
        private const byte PendingFrontBufferCurrent = 0;
        private const byte PendingFrontBufferBack = 1;
        private const byte PendingFrontBufferScratch = 2;

        private static readonly int ThermalCellsBufferId = Shader.PropertyToID("_H8AbyssalThermalCells");
        private static readonly int ThermalGridMetaId = Shader.PropertyToID("_H8AbyssalThermalGridMeta");
        private static readonly int ThermalGridOriginId = Shader.PropertyToID("_H8AbyssalThermalGridOrigin");

        private static GlobalDataVault _standaloneVault;

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

        private JobHandle _pendingHandle;
        private bool _hasPendingJob;
        private bool _nativeReady;
        private bool _registeredUpdate;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredOrigin;
        private bool _visualDirty;
        private bool _hasRealSources;
        private bool _blackBoxDumpedForCurrentFault;
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
        private GraphicsBuffer _thermalCellsBuffer;

        public static AbyssalThermodynamicsSolver ActiveRuntimeInstance { get; private set; }
        public bool IsInitialized => _nativeReady;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ThermalCellLayoutValidator.ValidateThermalCellLayout())
                throw new InvalidOperationException("ThermalCellDTO ABI mismatch.");

            EnsureNative();
            RegisterRuntime();
            ActiveRuntimeInstance = this;
        }

        private void OnDisable()
        {
            if (_hasPendingJob)
            {
                _pendingHandle.Complete();
                _hasPendingJob = false;
            }

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

            _thermalCellsBuffer?.Release();
            _thermalCellsBuffer = null;

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

            if (front == null || back == null || injection == null || scratch == null || sources == null || sourceCount == null || telemetry == null || tuningPtr == null)
                return;

            ThermalGridTuningDTO tuning = BuildTuning();
            *tuningPtr = tuning;
            _frame = tuning.Frame;

            JobHandle dependency = default;
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
            injectionJob.DeltaTime = math.max(0f, deltaTime);
            dependency = injectionJob.Schedule(dependency);

            int jacobiPasses = math.max(1, tuning.JacobiIterations);
            ThermalGridTuningDTO passTuning = tuning;
            passTuning.JacobiIterations = 1;
            ThermalCellDTO* readCells = front;
            ThermalCellDTO* writeCells = back;
            ThermalCellDTO* finalCells = back;
            bool writeBack = true;
            byte finalFrontBuffer = PendingFrontBufferBack;
            for (int pass = 0; pass < jacobiPasses; pass++)
            {
                HeatDiffusionSolverJob diffusionJob;
                diffusionJob.Front = readCells;
                diffusionJob.Back = writeCells;
                diffusionJob.Injection = injection;
                diffusionJob.Tuning = passTuning;
                diffusionJob.ApplyInjection = (byte)(pass == 0 ? 1 : 0);
                dependency = diffusionJob.Schedule(_activeCellCount, DefaultBatchSize, dependency);
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
            if (!_nativeReady || _hasPendingJob)
                return;

            TryLoadProfilesCold();
        }

        public void LateFrameTick()
        {
            if (!_nativeReady || !_hasPendingJob || !_pendingHandle.IsCompleted)
                return;

            _pendingHandle.Complete();
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
            source.Flags = 0u;
            source.ConductivityOverride = waterThermalConductivity;
            source.ConvectionGain = 1f;
            source.Phase01 = 0f;
            source._pad0 = 0u;
            sources[target] = source;
            *countPtr = count;
            _hasRealSources = count > 0;
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
                _hasRealSources = *countPtr > 0;
                return true;
            }

            return false;
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

            int3 cell = AbyssalThermalMath.MapAupToWrappedCell(aup, tuning->GridOriginAup, tuning->CellSizeMeters, tuning->GridResolution);
            int index = AbyssalThermalMath.Index(cell.x, cell.y, cell.z, tuning->GridResolution);
            ThermalCellDTO value = front[index];
            double3 localDouble = aup - tuning->GridOriginAup;
            result.TemperatureCelsius = value.TemperatureCelsius;
            result.ConvectionVelocityY = value.ConvectionVelocityY;
            result.CellIndex = (uint)index;
            result.Flags = value.Flags;
            result.LocalGridPosition = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            result.Conductivity = value.ThermalConductivity;
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

            tuning.CellSizeMeters = math.max(0.001f, tuning.CellSizeMeters);
            tuning.WaterThermalConductivity = math.max(0.0001f, tuning.WaterThermalConductivity);
            tuning.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            tuning.JacobiIterations = AbyssalThermalMath.ResolveJacobiIterations(tuning.GlobalQualityWeight);
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

            ThermalGridTuningDTO tuning = BuildTuning();
            ThermalGridTuningDTO* tuningPtr = (ThermalGridTuningDTO*)_tuning.ResolvePointer(_vault);
            int* sourceCount = (int*)_sourceCount.ResolvePointer(_vault);
            int* profileCount = (int*)_profileCount.ResolvePointer(_vault);
            if (tuningPtr == null || sourceCount == null || profileCount == null)
                throw new InvalidOperationException("Abyssal thermodynamics Vault pointer resolution failed.");

            *tuningPtr = tuning;
            *sourceCount = 0;
            *profileCount = 0;
            _hasRealSources = false;

            ThermalGridInitializeJob initJob;
            initJob.Front = (ThermalCellDTO*)_front.ResolvePointer(_vault);
            initJob.Back = (ThermalCellDTO*)_back.ResolvePointer(_vault);
            initJob.Injection = (ThermalCellDTO*)_injection.ResolvePointer(_vault);
            initJob.AmbientTemperatureCelsius = tuning.AmbientTemperatureCelsius;
            initJob.WaterThermalConductivity = tuning.WaterThermalConductivity;

            JobHandle initHandle = initJob.Schedule(MaxCellCount, DefaultBatchSize);
            initHandle.Complete(); // COLD SYNC JOB: deterministic first frame, no OS zero-fill dependency.
            _lastInitializedResolution = _activeResolution;

            UnsafeUtility.MemClear(_telemetryRing.ResolvePointer(_vault), UnsafeUtility.SizeOf<ThermalTelemetryEntry>() * AbyssalThermalMath.TelemetryCapacity);
            UnsafeUtility.MemClear(_profiles.ResolvePointer(_vault), UnsafeUtility.SizeOf<HeatSourceProfileDTO>() * MaxProfileCount);
            SeedDefaultProfiles();
            TryLoadProfilesCold();

            EnsureVisualBuffer();
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

            _standaloneVault ??= GlobalDataVault.Create(64);
            _vault = _standaloneVault;
            return _vault;
        }

        private ThermalGridTuningDTO BuildTuning()
        {
            float quality = ResolveQualityWeight();
            _activeResolution = AbyssalThermalMath.ResolveActiveResolution(quality, MinResolution, MaxResolution);
            _activeCellCount = _activeResolution * _activeResolution * _activeResolution;
            float safeCellSize = math.max(0.001f, cellSizeMeters);
            double3 anchorAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(transform.position);
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
            tuning.AmbientTemperatureCelsius = ambientTemperatureCelsius;
            tuning.WaterThermalConductivity = math.max(0.0001f, waterThermalConductivity);
            tuning.ConvectionSpeed = math.max(0f, convectionSpeed);
            tuning.GlobalQualityWeight = quality;
            tuning.JacobiIterations = AbyssalThermalMath.ResolveJacobiIterations(quality);
            tuning.GridResolution = new int3(_activeResolution, _activeResolution, _activeResolution);
            tuning.ActiveCellCount = _activeCellCount;
            tuning.DissipationPerStep = math.saturate(dissipationPerStep);
            tuning.MaxStableTemperatureCelsius = DefaultMaxStableTemperatureCelsius;
            tuning.HullInsulationConductivity = DefaultHullInsulationConductivity;
            tuning.MockVolcanoIntensity = math.max(1f, mockVolcanoIntensity);
            tuning.MockVolcanoRadiusMeters = math.max(1f, mockVolcanoRadiusMeters);
            tuning.MockVolcanoCount = math.clamp(mockVolcanoCount, 1, 16);
            tuning.ShiftThresholdMeters = safeCellSize * math.max(2f, _activeResolution * 0.25f);
            tuning.ThermalDamageThresholdCelsius = 58f;
            tuning.Frame = ++_frame;
            tuning.StateHash = ComputeStateHash(_activeResolution, tuning.JacobiIterations, quality);
            tuning.Flags = 0u;
            tuning.LastShiftSequence = _lastShiftSequence;
            tuning.SubmarineHalfExtentX = math.max(0f, submarineHalfExtentsMeters.x);
            tuning.SubmarineHalfExtentY = math.max(0f, submarineHalfExtentsMeters.y);
            tuning.SubmarineHalfExtentZ = math.max(0f, submarineHalfExtentsMeters.z);
            tuning._pad0 = 0f;
            return tuning;
        }

        private float ResolveQualityWeight()
        {
            if (useQualityOverride)
                return math.saturate(qualityOverride);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
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

            if ((entry.Flags & AbyssalThermalMath.TelemetryFlagNaN) == 0u)
            {
                _blackBoxDumpedForCurrentFault = false;
                return;
            }

            if (_blackBoxDumpedForCurrentFault)
                return;

            DumpBlackBox();
            _blackBoxDumpedForCurrentFault = true;
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
            string path = Path.Combine(directory, "Dump_THERMO_SURGEON.bin");
            long bytes = UnsafeUtility.SizeOf<ThermalTelemetryEntry>() * AbyssalThermalMath.TelemetryCapacity;

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (UnmanagedMemoryStream unmanaged = new UnmanagedMemoryStream((byte*)ring, bytes))
            {
                unmanaged.CopyTo(stream);
            }
        }

        private void EnsureVisualBuffer()
        {
            int stride = UnsafeUtility.SizeOf<ThermalCellDTO>();
            if (_thermalCellsBuffer != null && _thermalCellsBuffer.count == MaxCellCount && _thermalCellsBuffer.stride == stride)
                return;

            _thermalCellsBuffer?.Release();
            _thermalCellsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxCellCount, stride);
        }

        private void UploadVisualBuffer()
        {
            if (!_visualDirty)
                return;

            EnsureVisualBuffer();
            IDataVault vault = _vault;
            if (vault == null)
                return;

            NativeArray<ThermalCellDTO> front = _front.Resolve(vault);
            if (!front.IsCreated)
                return;

            _thermalCellsBuffer.SetData(front, 0, 0, _activeCellCount);
            Shader.SetGlobalBuffer(ThermalCellsBufferId, _thermalCellsBuffer);
            Shader.SetGlobalVector(ThermalGridMetaId, new Vector4(_activeResolution, cellSizeMeters, ambientTemperatureCelsius, ResolveQualityWeight()));
            ThermalGridTuningDTO* tuning = (ThermalGridTuningDTO*)_tuning.ResolvePointer(vault);
            if (tuning != null)
            {
                double3 origin = tuning->GridOriginAup;
                Shader.SetGlobalVector(ThermalGridOriginId, new Vector4((float)origin.x, (float)origin.y, (float)origin.z, tuning->ConvectionSpeed));
            }

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

            int res = tuning->GridResolution.x;
            int y = math.clamp((int)math.round((res - 1) * math.saturate(gizmoSliceY01)), 0, res - 1);
            float cell = tuning->CellSizeMeters;
            Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(tuning->GridOriginAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            Vector3 size = new Vector3(cell * 0.92f, 0.03f, cell * 0.92f);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int index = AbyssalThermalMath.Index(x, y, z, tuning->GridResolution);
                    float temp = front[index].TemperatureCelsius;
                    float t = math.saturate((temp - tuning->AmbientTemperatureCelsius) / math.max(1f, tuning->MaxStableTemperatureCelsius - tuning->AmbientTemperatureCelsius));
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
