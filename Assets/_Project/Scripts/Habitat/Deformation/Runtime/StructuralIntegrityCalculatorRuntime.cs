using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Habitat.Deformation
{
    [DisallowMultipleComponent]
    public sealed unsafe class StructuralIntegrityCalculatorRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IColdTickable
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_115.bin";
        private const string SurgeonDumpRelativePath = "Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin";
        private const string DefaultCsvRelativePath = "Docs/Data/hull_materials.csv";
        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.Habitat.StructuralIntegrity.Tick");
        private static readonly ProfilerMarker _lateMarker = new ProfilerMarker("H8.Habitat.StructuralIntegrity.LateFrame");
        private static readonly int _stateBufferId = Shader.PropertyToID("_HectonStructuralIntegrityStateBuffer");
        private static readonly int _stateParamsId = Shader.PropertyToID("_HectonStructuralIntegrityParams");
        private const int SolverLockStates = 1 << 0;
        private const int SolverLockNodeAups = 1 << 1;
        private const int SolverLockOffsets = 1 << 2;
        private const int SolverLockDestinations = 1 << 3;
        private const int SolverLockEdgeFlags = 1 << 4;
        private const int SolverLockTelemetry = 1 << 5;
        private const int SolverLockTelemetryCursor = 1 << 6;
        private const int SolverLockTuning = 1 << 7;
        private const int SolverLockSdf = 1 << 8;
        private const ulong StructuralMutationGuardMask = 1UL << 45;

        private static StructuralIntegrityCalculatorRuntime s_activeRuntime;

        [Header("Structural Solver")]
        [SerializeField, Range(1, StructuralIntegrityConstants.MaxNodeCapacity)] private int mockNodeCount = 128;
        [SerializeField] private bool generateMockGraphOnEnable = true;
        [SerializeField] private bool uploadStateBufferToShaders = true;
        [SerializeField] private string materialStrengthCsvRelativePath = DefaultCsvRelativePath;

        [Header("Pressure")]
        [SerializeField] private Vector3 seaLevelAup = Vector3.zero;
        [SerializeField, Min(0f)] private float basePressureKPa = 101.325f;
        [SerializeField, Min(0f)] private float pressureGradientKPaPerMeter = 10.05f;
        [SerializeField, Min(0f)] private float pressureToStressScale = 1f;

        [Header("Strength")]
        [SerializeField, Min(0.01f)] private float materialStrengthFactor = 1f;
        [SerializeField, Range(0f, 0.99f)] private float bucklingStart01 = 0.72f;
        [SerializeField, Min(0f)] private float bucklingVisualIntensity = 1f;
        [SerializeField, Min(0f)] private float supportDamping = 0.45f;
        [SerializeField, Min(0.01f)] private float collapseStress01 = 1f;

        [Header("SDF Anchor")]
        [SerializeField] private Vector3 sdfOriginAup = Vector3.zero;
        [SerializeField, Min(0.01f)] private float sdfMetersPerVoxel = 1f;
        [SerializeField, Min(0.01f)] private float sdfRangeMeters = 8f;

        private IDataVault _dataVault;
        private VaultBufferHandle<IntegrityStateDTO> _statesHandle;
        private VaultBufferHandle<double3> _nodeAupsHandle;
        private VaultBufferHandle<int> _csrOffsetsHandle;
        private VaultBufferHandle<int> _csrDestinationsHandle;
        private VaultBufferHandle<byte> _edgeFlagsHandle;
        private VaultBufferHandle<StructuralTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<StructuralTuningDTO> _tuningHandle;
        private VaultBufferHandle<StructuralMaterialStrengthEntry> _materialsHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;

        private JobHandle _scheduledHandle;
        private GraphicsBuffer _stateBufferA;
        private GraphicsBuffer _stateBufferB;
        private int _gpuReadIndex;
        private int _initialized;
        private int _registeredUpdate;
        private int _registeredLate;
        private int _registeredCold;
        private int _jobScheduled;
        private int _solverLockMask;
        private int _activeNodeCount;
        private int _activeEdgeCount;
        private uint _frame;
        private uint _lastDumpedFrame;
        private long _lastCsvWriteTicks;
        private int _materialTableInitialized;
        private uint _glassHash;
        private uint _titaniumHash;
        private uint _plasteelHash;

        public static StructuralIntegrityCalculatorRuntime ActiveRuntime => s_activeRuntime;
        public int ActiveNodeCount => _activeNodeCount;

        private void OnEnable()
        {
            s_activeRuntime = this;
            if (TryInitialize())
                TryRegisterTickables();
        }

        private void OnDisable()
        {
            CompleteScheduled(true);
            TryUnregisterTickables();
            ReleaseGpuBuffers();
            if (s_activeRuntime == this)
                s_activeRuntime = null;
            _initialized = 0;
        }

        private void OnDrawGizmos()
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return;

            NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<double3> aups = _nodeAupsHandle.Resolve(_dataVault);
            NativeArray<StructuralTuningDTO> tuningArray = _tuningHandle.Resolve(_dataVault);
            if (!states.IsCreated || !aups.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return;

            StructuralTuningDTO tuning = tuningArray[0];
            int count = math.min(_activeNodeCount, math.min(states.Length, aups.Length));
            count = math.min(count, 512);
            for (int i = 0; i < count; i++)
            {
                IntegrityStateDTO state = states[i];
                if (state.NodeHash == 0u)
                    continue;

                float stress = math.saturate(math.isfinite(state.CurrentStress) ? state.CurrentStress : 1f);
                Color color = Color.Lerp(Color.green, Color.yellow, math.saturate(stress / 0.8f));
                if (stress >= 0.95f)
                {
                    float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 4f, 1f);
                    color = Color.Lerp(Color.red, Color.white, pulse * 0.35f);
                }

                double3 relative = aups[i] - tuning.SeaLevelAup;
                Vector3 position = new Vector3((float)relative.x, (float)relative.y, (float)relative.z);
                float size = math.lerp(0.18f, 0.85f, stress);
                Gizmos.color = color;
                Gizmos.DrawWireCube(position, Vector3.one * size);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return;

            using (_tickMarker.Auto())
            {
                _frame++;
                float quality = ResolveGlobalQualityWeight();
                int framesBetweenUpdates = (int)math.lerp(1f, 30f, 1.0f - quality);
                framesBetweenUpdates = math.clamp(framesBetweenUpdates, 1, 30);
                if ((_frame % (uint)framesBetweenUpdates) != 0u)
                    return;

                ScheduleSolver(quality, framesBetweenUpdates);
            }
        }

        public void LateFrameTick()
        {
            if (_initialized == 0)
                return;

            using (_lateMarker.Auto())
            {
                if (_jobScheduled != 0)
                {
                    try
                    {
                        CompleteScheduled(false);
                        AfterSolverComplete();
                    }
                    finally
                    {
                        UnlockSolverBuffers();
                    }
                }
            }
        }

        public void ColdTick()
        {
            if (_initialized == 0)
                return;

            if (_jobScheduled != 0)
                return;

            TryLoadMaterialStrengthCsv();
        }

        public bool TryGetState(int index, out IntegrityStateDTO state, out double3 aup)
        {
            state = default;
            aup = default;
            if (_initialized == 0 || _jobScheduled != 0 || (uint)index >= (uint)_activeNodeCount)
                return false;

            NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<double3> aups = _nodeAupsHandle.Resolve(_dataVault);
            if (!states.IsCreated || !aups.IsCreated || index >= states.Length || index >= aups.Length)
                return false;

            state = states[index];
            aup = aups[index];
            return state.NodeHash != 0u;
        }

        public bool TryGetTuning(out StructuralTuningDTO tuning)
        {
            tuning = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<StructuralTuningDTO> tuningArray = _tuningHandle.Resolve(_dataVault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TryGetTelemetrySample(int framesBack, out StructuralTelemetryEntry entry)
        {
            entry = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<StructuralTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length == 0 || cursor.Length == 0)
                return false;

            int capacity = math.min(telemetry.Length, StructuralIntegrityConstants.TelemetryFrameCapacity);
            int clampedBack = math.clamp(framesBack, 0, capacity - 1);
            int slot = cursor[0] - 1 - clampedBack;
            while (slot < 0)
                slot += capacity;
            slot %= capacity;

            entry = telemetry[slot];
            return entry.Sequence != 0u || entry.Frame != 0u || entry.ActiveNodeCount != 0;
        }

        public void SetTuning(in StructuralTuningDTO tuning)
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return;

            if (!TryAcquireStructuralMutationGuard())
                return;

            bool tuningLocked = false;
            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityTuning, SystemID.HullIntegrity))
            {
                ReleaseStructuralMutationGuard();
                return;
            }

            tuningLocked = true;
            try
            {
                NativeArray<StructuralTuningDTO> tuningArray = _tuningHandle.Resolve(_dataVault);
                if (!tuningArray.IsCreated || tuningArray.Length == 0)
                    return;

                StructuralTuningDTO sanitized = SanitizeTuning(tuning, ResolveGlobalQualityWeight());
                tuningArray[0] = sanitized;
                basePressureKPa = sanitized.BasePressureKPa;
                pressureGradientKPaPerMeter = sanitized.PressureGradientKPaPerMeter;
                pressureToStressScale = sanitized.PressureToStressScale;
                materialStrengthFactor = sanitized.MaterialStrengthFactor;
                bucklingStart01 = sanitized.BucklingStart01;
                bucklingVisualIntensity = sanitized.BucklingVisualIntensity;
                supportDamping = sanitized.SupportDamping;
                collapseStress01 = sanitized.CollapseStress01;
                sdfMetersPerVoxel = sanitized.SdfMetersPerVoxel;
                sdfRangeMeters = sanitized.SdfRangeMeters;
            }
            finally
            {
                if (tuningLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityTuning, SystemID.HullIntegrity);
                ReleaseStructuralMutationGuard();
            }
        }

        public bool RegenerateMockGraph()
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            return GenerateEmergencyMockStressData();
        }

        private bool TryInitialize()
        {
            _dataVault = _dataVault ?? GlobalRegistry.DataVault;
            if (_dataVault == null)
                return false;

            if (!StructuralIntegrityLayout.Validate())
                return false;

            _glassHash = HashLowerAsciiLiteral("glass");
            _titaniumHash = HashLowerAsciiLiteral("titanium");
            _plasteelHash = HashLowerAsciiLiteral("plasteel");

            _statesHandle = _dataVault.GetBufferHandle<IntegrityStateDTO>(
                BufferID.StructuralIntegrityStates,
                StructuralIntegrityConstants.MaxNodeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _nodeAupsHandle = _dataVault.GetBufferHandle<double3>(
                BufferID.StructuralIntegrityNodeAups,
                StructuralIntegrityConstants.MaxNodeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csrOffsetsHandle = _dataVault.GetBufferHandle<int>(
                BufferID.StructuralIntegrityCsrOffsets,
                StructuralIntegrityConstants.MaxNodeCapacity + 1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csrDestinationsHandle = _dataVault.GetBufferHandle<int>(
                BufferID.StructuralIntegrityCsrDestinations,
                StructuralIntegrityConstants.MaxEdgeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _edgeFlagsHandle = _dataVault.GetBufferHandle<byte>(
                BufferID.StructuralIntegrityEdgeFlags,
                StructuralIntegrityConstants.MaxEdgeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _dataVault.GetBufferHandle<StructuralTelemetryEntry>(
                BufferID.StructuralIntegrityTelemetryRing,
                StructuralIntegrityConstants.TelemetryFrameCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _dataVault.GetBufferHandle<int>(
                BufferID.StructuralIntegrityTelemetryCursor,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _dataVault.GetBufferHandle<StructuralTuningDTO>(
                BufferID.StructuralIntegrityTuning,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _materialsHandle = _dataVault.GetBufferHandle<StructuralMaterialStrengthEntry>(
                BufferID.StructuralIntegrityMaterialStrengths,
                StructuralIntegrityConstants.MaterialStrengthCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _dataVault.GetBufferHandle<byte>(
                BufferID.StructuralIntegrityCsvScratch,
                StructuralIntegrityConstants.CsvScratchBytes,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);

            if (!_statesHandle.IsCreated ||
                !_nodeAupsHandle.IsCreated ||
                !_csrOffsetsHandle.IsCreated ||
                !_csrDestinationsHandle.IsCreated ||
                !_edgeFlagsHandle.IsCreated ||
                !_telemetryHandle.IsCreated ||
                !_telemetryCursorHandle.IsCreated ||
                !_tuningHandle.IsCreated ||
                !_materialsHandle.IsCreated ||
                !_csvScratchHandle.IsCreated)
            {
                return false;
            }

            SignalBus<BaseIntegrityEventPayload>.Configure(64, 256, 32, StructuralIntegrityConstants.SignalLaneHash);
            SignalBus<BaseIntegrityEventPayload>.EnsureInitialized();
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            SignalBus<BaseModuleCompromisedSignal>.EnsureInitialized();

            if (!ClearBootBuffers())
                return false;
            if (!WriteDefaultMaterials())
                return false;
            TryLoadMaterialStrengthCsv();
            _activeNodeCount = math.clamp(mockNodeCount, 1, StructuralIntegrityConstants.MaxNodeCapacity);
            if (!WriteDefaultTuning())
                return false;
            if (generateMockGraphOnEnable)
            {
                if (!GenerateEmergencyMockStressData())
                    return false;
            }

            EnsureGpuBuffers();
            _initialized = 1;
            return true;
        }

        private bool ClearBootBuffers()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            if (!TryLockSolverBuffers(false))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            bool cleared = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
                NativeArray<double3> aups = _nodeAupsHandle.Resolve(_dataVault);
                NativeArray<int> offsets = _csrOffsetsHandle.Resolve(_dataVault);
                NativeArray<int> destinations = _csrDestinationsHandle.Resolve(_dataVault);
                NativeArray<byte> flags = _edgeFlagsHandle.Resolve(_dataVault);
                NativeArray<StructuralTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
                NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);
                // COLD SYNC JOB: boot-time explicit memclear of Vault buffers acquired with UninitializedMemory.
                new StructuralIntegrityClearJob
                {
                    States = states,
                    NodeAups = aups,
                    CsrOffsets = offsets,
                    CsrDestinations = destinations,
                    EdgeFlags = flags,
                    Telemetry = telemetry,
                    TelemetryCursor = telemetryCursor
                }.Schedule().Complete();
                cleared = true;
            }
            finally
            {
                UnlockSolverBuffers();
                ReleaseStructuralMutationGuard();
            }

            return cleared;
        }

        private bool WriteDefaultTuning()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool tuningLocked = false;
            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityTuning, SystemID.HullIntegrity))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            tuningLocked = true;
            bool written = false;
            try
            {
                NativeArray<StructuralTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                WriteDefaultTuning(tuning);
                written = true;
            }
            finally
            {
                if (tuningLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityTuning, SystemID.HullIntegrity);
                ReleaseStructuralMutationGuard();
            }

            return written;
        }

        private void WriteDefaultTuning(NativeArray<StructuralTuningDTO> tuning)
        {
            tuning[0] = SanitizeTuning(new StructuralTuningDTO
            {
                SeaLevelAup = new double3(seaLevelAup.x, seaLevelAup.y, seaLevelAup.z),
                SdfOriginAup = new double3(sdfOriginAup.x, sdfOriginAup.y, sdfOriginAup.z),
                BasePressureKPa = basePressureKPa,
                PressureGradientKPaPerMeter = pressureGradientKPaPerMeter,
                PressureToStressScale = pressureToStressScale,
                MaterialStrengthFactor = materialStrengthFactor,
                BucklingStart01 = bucklingStart01,
                BucklingVisualIntensity = bucklingVisualIntensity,
                SupportDamping = supportDamping,
                CollapseStress01 = collapseStress01,
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                SdfMetersPerVoxel = sdfMetersPerVoxel,
                SdfRangeMeters = sdfRangeMeters,
                ActiveNodeCount = _activeNodeCount
            }, ResolveGlobalQualityWeight());
        }

        private bool GenerateEmergencyMockStressData()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            if (!TryLockSolverBuffers(false))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            bool materialsLocked = false;
            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity))
            {
                UnlockSolverBuffers();
                ReleaseStructuralMutationGuard();
                return false;
            }

            materialsLocked = true;
            bool generated = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
                NativeArray<double3> aups = _nodeAupsHandle.Resolve(_dataVault);
                NativeArray<int> offsets = _csrOffsetsHandle.Resolve(_dataVault);
                NativeArray<int> destinations = _csrDestinationsHandle.Resolve(_dataVault);
                NativeArray<byte> flags = _edgeFlagsHandle.Resolve(_dataVault);
                NativeArray<StructuralMaterialStrengthEntry> materials = _materialsHandle.Resolve(_dataVault);
                NativeArray<StructuralTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
                if (!states.IsCreated || !aups.IsCreated || !offsets.IsCreated || !destinations.IsCreated || !flags.IsCreated || !materials.IsCreated ||
                    !tuning.IsCreated || tuning.Length == 0)
                {
                    return false;
                }

                _activeNodeCount = math.clamp(mockNodeCount, 1, math.min(StructuralIntegrityConstants.MaxNodeCapacity, states.Length));
                // COLD SYNC JOB: deterministic mock topology generation for isolated profiling and CI fallback data.
                new GenerateMockStructuralStressJob
                {
                    States = states,
                    NodeAups = aups,
                    CsrOffsets = offsets,
                    CsrDestinations = destinations,
                    EdgeFlags = flags,
                    Materials = materials,
                    NodeCount = _activeNodeCount,
                    BaseHash = StructuralIntegrityConstants.DefaultBaseHash,
                    SeaLevelAup = new double3(seaLevelAup.x, seaLevelAup.y, seaLevelAup.z),
                    GlassHash = _glassHash,
                    TitaniumHash = _titaniumHash,
                    PlasteelHash = _plasteelHash
                }.Schedule().Complete();

                _activeEdgeCount = offsets.IsCreated && _activeNodeCount < offsets.Length ? offsets[_activeNodeCount] : 0;
                WriteDefaultTuning(tuning);
                generated = true;
            }
            finally
            {
                if (materialsLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity);
                UnlockSolverBuffers();
                ReleaseStructuralMutationGuard();
            }

            return generated;
        }

        private void ScheduleSolver(float quality, int framesBetweenUpdates)
        {
            NativeArray<byte> sdf = default;
            bool includeSdfLock = _dataVault.TryGetBuffer(BufferID.VoxelSdfTexture3D, out sdf) && sdf.IsCreated;
            if (!TryLockSolverBuffers(includeSdfLock))
                return;

            NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<double3> aups = _nodeAupsHandle.Resolve(_dataVault);
            NativeArray<int> offsets = _csrOffsetsHandle.Resolve(_dataVault);
            NativeArray<int> destinations = _csrDestinationsHandle.Resolve(_dataVault);
            NativeArray<byte> edgeFlags = _edgeFlagsHandle.Resolve(_dataVault);
            NativeArray<StructuralTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);
            NativeArray<StructuralTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
            if (!states.IsCreated || !aups.IsCreated || !offsets.IsCreated || !destinations.IsCreated || !edgeFlags.IsCreated ||
                !telemetry.IsCreated || !telemetryCursor.IsCreated || !tuning.IsCreated || telemetryCursor.Length == 0 || tuning.Length == 0)
            {
                UnlockSolverBuffers();
                return;
            }

            StructuralTuningDTO current = SanitizeTuning(tuning[0], quality);
            current.ActiveNodeCount = _activeNodeCount;
            tuning[0] = current;

            if (includeSdfLock)
                _dataVault.TryGetBuffer(BufferID.VoxelSdfTexture3D, out sdf);
            else
                sdf = default;
            int sdfDimension = ResolveSdfDimension(sdf);
            int sdfFallback = sdfDimension <= 1 ? 1 : 0;
            int safeCount = math.clamp(_activeNodeCount, 0, math.min(states.Length, aups.Length));
            int batchSize = ResolveBatchSize(quality);
            float estimatedMicroseconds = EstimateMicroseconds(safeCount, _activeEdgeCount, framesBetweenUpdates);

            JobHandle handle = new StructuralDepthPressureJob
            {
                States = states,
                NodeAups = aups,
                Tuning = tuning,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize);

            handle = new StructuralSdfAnchorJob
            {
                States = states,
                NodeAups = aups,
                VoxelSdfTexture3D = sdf,
                Tuning = tuning,
                ActiveNodeCount = safeCount,
                SdfDimension = sdfDimension
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralGraphStressJob
            {
                States = states,
                CsrOffsets = offsets,
                CsrDestinations = destinations,
                EdgeFlags = edgeFlags,
                Tuning = tuning,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralCollapseSignalJob
            {
                States = states,
                NodeAups = aups,
                Tuning = tuning,
                IntegrityEvents = SignalBus<BaseIntegrityEventPayload>.ParallelWriter,
                FluidEvents = SignalBus<FluidIncursionSignal>.ParallelWriter,
                CompromisedEvents = SignalBus<BaseModuleCompromisedSignal>.ParallelWriter,
                ActiveNodeCount = safeCount,
                Frame = _frame
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralEdgeSeverJob
            {
                States = states,
                CsrOffsets = offsets,
                CsrDestinations = destinations,
                EdgeFlags = edgeFlags,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralTelemetryJob
            {
                States = states,
                CsrOffsets = offsets,
                Tuning = tuning,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                ActiveNodeCount = safeCount,
                Frame = _frame,
                FramesBetweenUpdates = framesBetweenUpdates,
                EstimatedMicroseconds = estimatedMicroseconds,
                BaseHash = StructuralIntegrityConstants.DefaultBaseHash,
                SdfFallback = sdfFallback
            }.Schedule(handle);

            _scheduledHandle = handle;
            _jobScheduled = 1;
        }

        private void CompleteScheduled(bool releaseLocks)
        {
            if (_jobScheduled == 0)
                return;

            try
            {
                _scheduledHandle.Complete();
            }
            finally
            {
                _scheduledHandle = default;
                _jobScheduled = 0;
                if (releaseLocks)
                    UnlockSolverBuffers();
            }
        }

        private bool TryLockSolverBuffers(bool includeSdf)
        {
            if (_solverLockMask != 0 || _dataVault == null)
                return false;

            int mask = 0;
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityStates, SolverLockStates, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityNodeAups, SolverLockNodeAups, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityCsrOffsets, SolverLockOffsets, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityCsrDestinations, SolverLockDestinations, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityEdgeFlags, SolverLockEdgeFlags, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityTelemetryRing, SolverLockTelemetry, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityTelemetryCursor, SolverLockTelemetryCursor, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryLockSolverBuffer(BufferID.StructuralIntegrityTuning, SolverLockTuning, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (includeSdf && !TryLockSolverBuffer(BufferID.VoxelSdfTexture3D, SolverLockSdf, ref mask)) { UnlockSolverBuffers(mask); return false; }

            _solverLockMask = mask;
            return true;
        }

        private bool TryLockSolverBuffer(BufferID id, int bit, ref int mask)
        {
            if (!_dataVault.TryLockBuffer(id, SystemID.HullIntegrity))
                return false;

            mask |= bit;
            return true;
        }

        private bool TryAcquireStructuralMutationGuard()
        {
            return _dataVault != null && _dataVault.TryAcquireMutationGuard(StructuralMutationGuardMask);
        }

        private void ReleaseStructuralMutationGuard()
        {
            if (_dataVault != null)
                _dataVault.ReleaseMutationGuard(StructuralMutationGuardMask);
        }

        private void UnlockSolverBuffers()
        {
            int mask = _solverLockMask;
            _solverLockMask = 0;
            UnlockSolverBuffers(mask);
        }

        private void UnlockSolverBuffers(int mask)
        {
            if (_dataVault == null || mask == 0)
                return;

            if ((mask & SolverLockSdf) != 0) _dataVault.TryUnlockBuffer(BufferID.VoxelSdfTexture3D, SystemID.HullIntegrity);
            if ((mask & SolverLockTuning) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityTuning, SystemID.HullIntegrity);
            if ((mask & SolverLockTelemetryCursor) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityTelemetryCursor, SystemID.HullIntegrity);
            if ((mask & SolverLockTelemetry) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityTelemetryRing, SystemID.HullIntegrity);
            if ((mask & SolverLockEdgeFlags) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityEdgeFlags, SystemID.HullIntegrity);
            if ((mask & SolverLockDestinations) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityCsrDestinations, SystemID.HullIntegrity);
            if ((mask & SolverLockOffsets) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityCsrOffsets, SystemID.HullIntegrity);
            if ((mask & SolverLockNodeAups) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityNodeAups, SystemID.HullIntegrity);
            if ((mask & SolverLockStates) != 0) _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityStates, SystemID.HullIntegrity);
        }

        private void AfterSolverComplete()
        {
            NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<StructuralTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);
            if (states.IsCreated && uploadStateBufferToShaders)
                UploadStatesToGpu(states);

            if (!telemetry.IsCreated || !telemetryCursor.IsCreated || telemetryCursor.Length == 0)
                return;

            int previousCursor = telemetryCursor[0] - 1;
            if (previousCursor < 0)
                previousCursor += StructuralIntegrityConstants.TelemetryFrameCapacity;
            int slot = previousCursor % StructuralIntegrityConstants.TelemetryFrameCapacity;
            if ((uint)slot >= (uint)telemetry.Length)
                return;

            StructuralTelemetryEntry entry = telemetry[slot];
            if ((entry.FaultFlags & (StructuralIntegrityConstants.TelemetryFlagNonFinite | StructuralIntegrityConstants.TelemetryFlagMassCollapse)) != 0u &&
                entry.Frame != _lastDumpedFrame)
            {
                DumpTelemetry(DumpRelativePath, in entry);
                DumpTelemetry(SurgeonDumpRelativePath, in entry);
                _lastDumpedFrame = entry.Frame;
            }
        }

        private void UploadStatesToGpu(NativeArray<IntegrityStateDTO> states)
        {
            EnsureGpuBuffers();
            if (_stateBufferA == null || _stateBufferB == null || !states.IsCreated)
                return;

            int uploadCount = math.max(1, math.clamp(_activeNodeCount, 0, states.Length));
            int writeIndex = 1 - _gpuReadIndex;
            GraphicsBuffer writeBuffer = writeIndex == 0 ? _stateBufferA : _stateBufferB;
            NativeArray<IntegrityStateDTO> mapped = writeBuffer.LockBufferForWrite<IntegrityStateDTO>(0, uploadCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<IntegrityStateDTO>() * uploadCount);
            writeBuffer.UnlockBufferAfterWrite<IntegrityStateDTO>(uploadCount);

            _gpuReadIndex = writeIndex;
            GraphicsBuffer readBuffer = _gpuReadIndex == 0 ? _stateBufferA : _stateBufferB;
            Shader.SetGlobalBuffer(_stateBufferId, readBuffer);
            Shader.SetGlobalVector(_stateParamsId, new Vector4(uploadCount, _activeEdgeCount, ResolveGlobalQualityWeight(), _frame));
        }

        private void EnsureGpuBuffers()
        {
            if (!uploadStateBufferToShaders)
                return;

            int stride = UnsafeUtility.SizeOf<IntegrityStateDTO>();
            if (_stateBufferA == null || _stateBufferA.count != StructuralIntegrityConstants.MaxNodeCapacity || _stateBufferA.stride != stride)
            {
                ReleaseGpuBuffers();
                _stateBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, StructuralIntegrityConstants.MaxNodeCapacity, stride); // COLD ALLOC: GraphicsBuffer[4096] - structural state upload A - owner: SHINOBU_115
                _stateBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, StructuralIntegrityConstants.MaxNodeCapacity, stride); // COLD ALLOC: GraphicsBuffer[4096] - structural state upload B - owner: SHINOBU_115
                _gpuReadIndex = 0;
            }
        }

        private void ReleaseGpuBuffers()
        {
            if (_stateBufferA != null)
            {
                _stateBufferA.Release();
                _stateBufferA = null;
            }

            if (_stateBufferB != null)
            {
                _stateBufferB.Release();
                _stateBufferB = null;
            }
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

        private bool WriteDefaultMaterials()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool materialsLocked = false;
            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            materialsLocked = true;
            bool written = false;
            try
            {
                NativeArray<StructuralMaterialStrengthEntry> materials = _materialsHandle.Resolve(_dataVault);
                if (!materials.IsCreated)
                    return false;

                WriteDefaultMaterials(materials);
                written = true;
            }
            finally
            {
                if (materialsLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity);
                ReleaseStructuralMutationGuard();
            }

            return written;
        }

        private void WriteDefaultMaterials(NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i] = default;

            UpsertMaterial(materials, _glassHash, 420f, 0.55f, 1.15f);
            UpsertMaterial(materials, _titaniumHash, 1220f, 0.72f, 1f);
            UpsertMaterial(materials, _plasteelHash, 2100f, 0.82f, 0.85f);
            _materialTableInitialized = 1;
        }

        private bool TryLoadMaterialStrengthCsv()
        {
            string path = ResolveProjectPath(string.IsNullOrEmpty(materialStrengthCsvRelativePath) ? DefaultCsvRelativePath : materialStrengthCsvRelativePath);
            if (!File.Exists(path))
            {
                if (_materialTableInitialized == 0)
                    return WriteDefaultMaterials();
                return false;
            }

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (_materialTableInitialized != 0 && ticks == _lastCsvWriteTicks)
                return true;

            if (!TryAcquireStructuralMutationGuard())
                return false;

            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityCsvScratch, SystemID.HullIntegrity))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            bool materialsLocked = false;
            bool loaded = false;
            try
            {
                if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity))
                    return false;

                materialsLocked = true;
                NativeArray<byte> scratch = _csvScratchHandle.Resolve(_dataVault);
                NativeArray<StructuralMaterialStrengthEntry> materials = _materialsHandle.Resolve(_dataVault);
                if (!scratch.IsCreated || !materials.IsCreated)
                    return false;

                int bytesRead;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    StructuralIntegrityConstants.CsvScratchBytes,
                    FileOptions.SequentialScan))
                {
                    bytesRead = stream.Read(new Span<byte>(scratchPtr, scratch.Length));
                }

                WriteDefaultMaterials(materials);
                ParseMaterialCsv(new ReadOnlySpan<byte>(scratchPtr, bytesRead), materials);
                _lastCsvWriteTicks = ticks;
                _materialTableInitialized = 1;
                loaded = true;
            }
            finally
            {
                if (materialsLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity);
                _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityCsvScratch, SystemID.HullIntegrity);
                ReleaseStructuralMutationGuard();
            }

            if (loaded)
                return ApplyMaterialsToStatesCold();

            return loaded;
        }

        private void ParseMaterialCsv(ReadOnlySpan<byte> bytes, NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                ParseMaterialCsvLine(line, materials);
            }
        }

        private void ParseMaterialCsvLine(ReadOnlySpan<byte> line, NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            int comma0 = IndexOf(line, (byte)',');
            if (comma0 <= 0)
                return;
            int comma1 = IndexOf(line.Slice(comma0 + 1), (byte)',');
            if (comma1 < 0)
                return;
            comma1 += comma0 + 1;
            int comma2 = IndexOf(line.Slice(comma1 + 1), (byte)',');
            if (comma2 >= 0)
                comma2 += comma1 + 1;

            uint materialHash = HashLowerAscii(TrimAscii(line.Slice(0, comma0)));
            if (materialHash == 0u)
                return;

            if (!TryParseAsciiFloat(TrimAscii(line.Slice(comma0 + 1, comma1 - comma0 - 1)), out float baseStrength))
                return;

            float buckling = 0.72f;
            float pressureScale = 1f;
            if (comma2 >= 0)
            {
                TryParseAsciiFloat(TrimAscii(line.Slice(comma1 + 1, comma2 - comma1 - 1)), out buckling);
                TryParseAsciiFloat(TrimAscii(line.Slice(comma2 + 1)), out pressureScale);
            }
            else
            {
                TryParseAsciiFloat(TrimAscii(line.Slice(comma1 + 1)), out buckling);
            }

            UpsertMaterial(materials, materialHash, baseStrength, buckling, pressureScale);
        }

        private bool ApplyMaterialsToStatesCold()
        {
            if (_activeNodeCount <= 0)
                return true;

            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool statesLocked = false;
            if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityStates, SystemID.HullIntegrity))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            statesLocked = true;
            bool materialsLocked = false;
            bool applied = false;
            try
            {
                if (!_dataVault.TryLockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity))
                    return false;

                materialsLocked = true;
                NativeArray<IntegrityStateDTO> states = _statesHandle.Resolve(_dataVault);
                NativeArray<StructuralMaterialStrengthEntry> materials = _materialsHandle.Resolve(_dataVault);
                if (!states.IsCreated || !materials.IsCreated)
                    return false;

                // COLD SYNC JOB: material CSV reload is skipped while the solver fence is alive.
                new StructuralMaterialStrengthApplyJob
                {
                    States = states,
                    Materials = materials,
                    ActiveNodeCount = _activeNodeCount,
                    GlassHash = _glassHash,
                    TitaniumHash = _titaniumHash,
                    PlasteelHash = _plasteelHash
                }.Schedule(_activeNodeCount, 64).Complete();
                applied = true;
            }
            finally
            {
                if (materialsLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityMaterialStrengths, SystemID.HullIntegrity);
                if (statesLocked)
                    _dataVault.TryUnlockBuffer(BufferID.StructuralIntegrityStates, SystemID.HullIntegrity);
                ReleaseStructuralMutationGuard();
            }

            return applied;
        }

        private static void UpsertMaterial(NativeArray<StructuralMaterialStrengthEntry> materials, uint hash, float baseStrength, float bucklingStart, float pressureScale)
        {
            if (!materials.IsCreated || materials.Length == 0 || hash == 0u)
                return;

            StructuralMaterialStrengthEntry entry = new StructuralMaterialStrengthEntry
            {
                MaterialHash = hash,
                BaseStrength = math.max(1f, math.isfinite(baseStrength) ? baseStrength : 1f),
                BucklingStart01 = math.saturate(math.isfinite(bucklingStart) ? bucklingStart : 0.72f),
                PressureScale = math.max(0.01f, math.isfinite(pressureScale) ? pressureScale : 1f)
            };

            int start = (int)(hash % (uint)materials.Length);
            int firstEmpty = -1;
            for (int probe = 0; probe < materials.Length; probe++)
            {
                int index = WrapMaterialIndex(start + probe, materials.Length);
                StructuralMaterialStrengthEntry current = materials[index];
                if (current.MaterialHash == hash)
                {
                    materials[index] = entry;
                    return;
                }

                if (firstEmpty < 0 && current.MaterialHash == 0u)
                    firstEmpty = index;
            }

            if (firstEmpty >= 0)
                materials[firstEmpty] = entry;
        }

        private static int WrapMaterialIndex(int value, int length)
        {
            return (length & (length - 1)) == 0 ? (value & (length - 1)) : (value % length);
        }

        private void DumpTelemetry(string relativePath, in StructuralTelemetryEntry faultEntry)
        {
            NativeArray<StructuralTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            string path = ResolveProjectPath(relativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0;
            StructuralTelemetryDumpHeader header = new StructuralTelemetryDumpHeader
            {
                Magic = StructuralIntegrityConstants.DumpMagic,
                Version = StructuralIntegrityConstants.DumpVersion,
                Frame = faultEntry.Frame,
                EntrySize = (uint)UnsafeUtility.SizeOf<StructuralTelemetryEntry>(),
                EntryCount = telemetry.Length,
                Cursor = cursorValue,
                FaultFlags = faultEntry.FaultFlags,
                StateHash = faultEntry.StateHash
            };

            using (FileStream stream = File.Create(path))
            {
                stream.Write(new ReadOnlySpan<byte>((byte*)&header, UnsafeUtility.SizeOf<StructuralTelemetryDumpHeader>()));
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int stride = UnsafeUtility.SizeOf<StructuralTelemetryEntry>();
                for (int i = 0; i < telemetry.Length; i++)
                    stream.Write(new ReadOnlySpan<byte>(source + i * stride, stride));
            }
        }

        private static StructuralTuningDTO SanitizeTuning(in StructuralTuningDTO source, float quality)
        {
            StructuralTuningDTO tuning = source;
            tuning.BasePressureKPa = SanitizeNonNegative(tuning.BasePressureKPa, 101.325f);
            tuning.PressureGradientKPaPerMeter = SanitizeNonNegative(tuning.PressureGradientKPaPerMeter, 10.05f);
            tuning.PressureToStressScale = SanitizeNonNegative(tuning.PressureToStressScale, 1f);
            tuning.MaterialStrengthFactor = math.max(0.01f, SanitizeNonNegative(tuning.MaterialStrengthFactor, 1f));
            tuning.BucklingStart01 = math.saturate(math.isfinite(tuning.BucklingStart01) ? tuning.BucklingStart01 : 0.72f);
            tuning.BucklingVisualIntensity = SanitizeNonNegative(tuning.BucklingVisualIntensity, 1f);
            tuning.SupportDamping = SanitizeNonNegative(tuning.SupportDamping, 0.45f);
            tuning.CollapseStress01 = math.max(0.01f, SanitizeNonNegative(tuning.CollapseStress01, 1f));
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            tuning.SdfMetersPerVoxel = math.max(0.01f, SanitizeNonNegative(tuning.SdfMetersPerVoxel, 1f));
            tuning.SdfRangeMeters = math.max(0.01f, SanitizeNonNegative(tuning.SdfRangeMeters, 8f));
            tuning.ActiveNodeCount = math.clamp(tuning.ActiveNodeCount, 0, StructuralIntegrityConstants.MaxNodeCapacity);
            if (!math.all(math.isfinite(tuning.SeaLevelAup)))
                tuning.SeaLevelAup = double3.zero;
            if (!math.all(math.isfinite(tuning.SdfOriginAup)))
                tuning.SdfOriginAup = double3.zero;
            return tuning;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private static int ResolveBatchSize(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.clamp((int)math.lerp(128f, 32f, q), 32, 128);
        }

        private static float EstimateMicroseconds(int nodeCount, int edgeCount, int framesBetweenUpdates)
        {
            float raw = nodeCount * 0.018f + edgeCount * 0.006f + 7f;
            return math.max(1f, raw / math.max(1, framesBetweenUpdates));
        }

        private static int ResolveSdfDimension(NativeArray<byte> sdf)
        {
            if (!sdf.IsCreated || sdf.Length <= 0)
                return 0;

            int dim = (int)math.round(math.pow(sdf.Length, 1f / 3f));
            return dim > 1 && dim * dim * dim <= sdf.Length ? dim : 0;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(root, normalized);
        }

        private static int IndexOf(ReadOnlySpan<byte> span, byte value)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == value)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && span[start] <= 32)
                start++;
            while (end >= start && span[end] <= 32)
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> span, out float result)
        {
            result = 0f;
            span = TrimAscii(span);
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                negative = span[index] == (byte)'-';
                index++;
            }

            double value = 0d;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                value = value * 10d + (span[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    value += (span[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            result = (float)(negative ? -value : value);
            return math.isfinite(result);
        }

        private static uint HashLowerAsciiLiteral(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = (byte)value[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            bool any = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c <= 32)
                    continue;
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
                any = true;
            }

            return any ? hash : 0u;
        }

        private void OnValidate()
        {
            mockNodeCount = Mathf.Clamp(mockNodeCount, 1, StructuralIntegrityConstants.MaxNodeCapacity);
            basePressureKPa = Mathf.Max(0f, basePressureKPa);
            pressureGradientKPaPerMeter = Mathf.Max(0f, pressureGradientKPaPerMeter);
            pressureToStressScale = Mathf.Max(0f, pressureToStressScale);
            materialStrengthFactor = Mathf.Max(0.01f, materialStrengthFactor);
            bucklingStart01 = Mathf.Clamp01(bucklingStart01);
            bucklingVisualIntensity = Mathf.Max(0f, bucklingVisualIntensity);
            supportDamping = Mathf.Max(0f, supportDamping);
            collapseStress01 = Mathf.Max(0.01f, collapseStress01);
            sdfMetersPerVoxel = Mathf.Max(0.01f, sdfMetersPerVoxel);
            sdfRangeMeters = Mathf.Max(0.01f, sdfRangeMeters);
        }
    }
}
