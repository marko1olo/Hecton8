using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Habitat.Deformation;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Graphics.Materials
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualAgingParamsDTO
    {
        [FieldOffset(0)] public float4 RustAndCorrosion;          // x=rust, y=corrosion, z=pitting, w=seed01
        [FieldOffset(16)] public float4 SaltAndBiomass;           // x=salt, y=biomass/algae, z=temperature boost, w=glass mask
        [FieldOffset(32)] public float4 StressAndMicroFractures;  // x=stress, y=microfracture, z=buckling, w=quality
        [FieldOffset(48)] public float4 DepthAndPressure;         // xyz=localized AUP offset, w=pressure01
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualAgingTuningDTO
    {
        [FieldOffset(0)] public float RustStressMultiplier;
        [FieldOffset(4)] public float CorrosionPressureMultiplier;
        [FieldOffset(8)] public float SaltDepthMultiplier;
        [FieldOffset(12)] public float BiomassTemperatureMultiplier;
        [FieldOffset(16)] public float GlassFractureThreshold;
        [FieldOffset(20)] public float TemperatureBoostMultiplier;
        [FieldOffset(24)] public float PressureScaleKPa;
        [FieldOffset(28)] public float DepthScaleMeters;
        [FieldOffset(32)] public float PittingStressMultiplier;
        [FieldOffset(36)] public float QualityNoiseOctaveScale;
        [FieldOffset(40)] public uint CsvGeneration;
        [FieldOffset(44)] public uint RuntimeFlags;
        [FieldOffset(48)] public uint GlassHashStride;
        [FieldOffset(52)] public uint ActiveCountOverride;
        [FieldOffset(56)] public float MockTemperatureC;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualAgingRuntimeDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int ActiveCount;
        [FieldOffset(12)] public int UploadedCount;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float LastUploadMicroseconds;
        [FieldOffset(24)] public float LastCpuEstimateMicroseconds;
        [FieldOffset(28)] public float MaxDepth01;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint LayoutHash;
        [FieldOffset(40)] public uint CsvGeneration;
        [FieldOffset(44)] public uint FaultFlags;
        [FieldOffset(48)] public float AverageStress01;
        [FieldOffset(52)] public float MeanMicrofracture01;
        [FieldOffset(56)] public float MeanTemperatureBoost01;
        [FieldOffset(60)] public uint Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualAgingTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public uint LayoutHash;
        [FieldOffset(16)] public int ActiveCount;
        [FieldOffset(20)] public int UploadedCount;
        [FieldOffset(24)] public uint UploadedBytes;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float MaxDepth01;
        [FieldOffset(36)] public float AverageStress01;
        [FieldOffset(40)] public uint ActiveGlassFractures;
        [FieldOffset(44)] public float MeanTemperatureBoost01;
        [FieldOffset(48)] public float CpuEstimateMicroseconds;
        [FieldOffset(52)] public float GpuUploadMicroseconds;
        [FieldOffset(56)] public uint CsvGeneration;
        [FieldOffset(60)] public uint Sequence;
    }

    public sealed unsafe class VisualPressureAgingRuntime
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsMaterials;
        private const uint SystemHash = 0x53323139u; // S219
        private const int Capacity = StructuralIntegrityConstants.MaxNodeCapacity;
        private const int TelemetryFrameCount = 300;
        private const int CsvScratchBytes = 4096;
        private const int JobBatchSize = 64;
        private const float UploadFaultMicroseconds = 100.0f;
        private const uint DumpMagic = 0x56414745u; // VAGE
        private const uint DumpVersion = 1u;
        private const string CsvRelativePath = "Data/Visuals/environmental_aging_rules.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_219.bin";

        private const uint FlagStructuralSource = 1u << 0;
        private const uint FlagMockSource = 1u << 1;
        private const uint FlagThermalSource = 1u << 2;
        private const uint FlagCsvLoaded = 1u << 3;
        private const uint FlagNoRollbackState = 1u << 4;
        private const uint FlagLayoutFault = 1u << 29;
        private const uint FlagNonFinite = 1u << 30;
        private const uint FlagUploadFault = 1u << 31;

        private static readonly int AgingParamsId = Shader.PropertyToID("_GlobalBaseAgingParams");
        private static readonly int AgingRuntimeId = Shader.PropertyToID("_GlobalBaseAgingRuntime");

        private static VisualPressureAgingRuntime s_active;
        private static bool s_hasPendingEditorTuning;
        private static VisualAgingTuningDTO s_pendingEditorTuning = DefaultTuning(1u);

        private IDataVault _vault;
        private VaultGenerationHandle<VisualAgingParamsDTO> _paramsHandle;
        private VaultGenerationHandle<VisualAgingRuntimeDTO> _runtimeHandle;
        private VaultGenerationHandle<VisualAgingTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<VisualAgingTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<float> _mockTemperatureHandle;

        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;

        private GraphicsBuffer _agingBufferA;
        private GraphicsBuffer _agingBufferB;
        private string _csvPath;
        private string _dumpPath;
        private long _csvLastWriteTicks;
        private int _lockedBufferMask;
        private int _activeCount;
        private int _uploadedCount;
        private int _readBufferIndex;
        private int _telemetryCursor;
        private uint _frame;
        private uint _csvGeneration = 1u;
        private uint _runtimeFlags = FlagMockSource | FlagNoRollbackState;
        private float _payloadBlend01;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _vaultInitialized;
        private bool _defaultsInitialized;
        private bool _simulationScheduled;
        private bool _hasGeneratedPayload;
        private bool _agingDirty = true;
        private bool _dumpedFault;
        private bool _shutdown;
        private bool _gizmoReadLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActive();
            s_active = null;
            s_hasPendingEditorTuning = false;
            s_pendingEditorTuning = DefaultTuning(1u);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            // COLD ALLOC: VisualPressureAgingRuntime[1] - dispatcher-owned procedural aging bridge - owner: SHINOBU_219
            VisualPressureAgingRuntime runtime = new VisualPressureAgingRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private static void ShutdownActive()
        {
            VisualPressureAgingRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

        public static bool ValidateLayout()
        {
            bool sizeValid = UnsafeUtility.SizeOf<VisualAgingParamsDTO>() == 64 &&
                UnsafeUtility.SizeOf<VisualAgingTuningDTO>() == 64 &&
                UnsafeUtility.SizeOf<VisualAgingRuntimeDTO>() == 64 &&
                UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>() == 64;
#if UNITY_EDITOR
            return sizeValid &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.RustAndCorrosion)) == 0 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.SaltAndBiomass)) == 16 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.StressAndMicroFractures)) == 32 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.DepthAndPressure)) == 48;
#else
            return sizeValid;
#endif
        }

        public static bool TryWriteEditorTuning(
            float rustStress,
            float corrosionPressure,
            float saltDepth,
            float biomassTemperature,
            float glassThreshold,
            float temperatureBoost,
            float qualityNoiseScale)
        {
            VisualAgingTuningDTO tuning = s_pendingEditorTuning;
            tuning.RustStressMultiplier = math.max(0.0f, SanitizeFloat(rustStress, tuning.RustStressMultiplier));
            tuning.CorrosionPressureMultiplier = math.max(0.0f, SanitizeFloat(corrosionPressure, tuning.CorrosionPressureMultiplier));
            tuning.SaltDepthMultiplier = math.max(0.0f, SanitizeFloat(saltDepth, tuning.SaltDepthMultiplier));
            tuning.BiomassTemperatureMultiplier = math.max(0.0f, SanitizeFloat(biomassTemperature, tuning.BiomassTemperatureMultiplier));
            tuning.GlassFractureThreshold = math.saturate(SanitizeFloat(glassThreshold, tuning.GlassFractureThreshold));
            tuning.TemperatureBoostMultiplier = math.max(0.0f, SanitizeFloat(temperatureBoost, tuning.TemperatureBoostMultiplier));
            tuning.QualityNoiseOctaveScale = math.saturate(SanitizeFloat(qualityNoiseScale, tuning.QualityNoiseOctaveScale));
            tuning.CsvGeneration = unchecked(tuning.CsvGeneration + 1u);
            s_pendingEditorTuning = tuning;
            s_hasPendingEditorTuning = true;

            VisualPressureAgingRuntime active = s_active;
            return active != null && active.ApplyPendingEditorTuningImmediate(true);
        }

        public static bool TryReadEditorTuning(
            out VisualAgingTuningDTO tuning,
            out int activeCount,
            out float uploadMicroseconds,
            out uint flags)
        {
            tuning = s_pendingEditorTuning;
            activeCount = 0;
            uploadMicroseconds = 0.0f;
            flags = 0u;

            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled)
                return false;

            IDataVault vault = active.ResolveVault(true);
            if (vault == null || !active.EnsureVaultState(vault))
                return false;

            bool runtimeLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            if (!runtimeLocked)
                return false;

            bool tuningLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            if (!tuningLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                return false;
            }

            try
            {
                vault.TryResolveHandle(in active._tuningHandle, out NativeArray<VisualAgingTuningDTO> tuningBuffer);
                if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                    tuning = tuningBuffer[0];

                vault.TryResolveHandle(in active._runtimeHandle, out NativeArray<VisualAgingRuntimeDTO> runtimeBuffer);
                if (runtimeBuffer.IsCreated && runtimeBuffer.Length > 0)
                {
                    VisualAgingRuntimeDTO runtime = runtimeBuffer[0];
                    activeCount = runtime.ActiveCount;
                    uploadMicroseconds = runtime.LastUploadMicroseconds;
                    flags = runtime.Flags | runtime.FaultFlags;
                }

                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            }
        }

#if UNITY_EDITOR
        public static bool TryReloadEditorCsv()
        {
            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            IDataVault vault = active.ResolveVault(true);
            return vault != null &&
                active.EnsureVaultState(vault) &&
                active.ReloadCsvFromDisk(vault, true);
        }
#endif

        public static bool TryAcquireAgingBufferRead(out NativeArray<VisualAgingParamsDTO> aging, out int activeCount)
        {
            aging = default;
            activeCount = 0;
            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled)
                return false;

            IDataVault vault = active.ResolveVault(true);
            if (vault == null || !active.EnsureVaultState(vault))
                return false;

            if (!vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId))
                return false;

            active._gizmoReadLocked = true;
            vault.TryResolveHandle(in active._paramsHandle, out aging);
            if (active._hasGeneratedPayload && aging.IsCreated && aging.Length > 0)
            {
                activeCount = math.min(active._activeCount, aging.Length);
                return true;
            }

            ReleaseAgingBufferRead();
            aging = default;
            activeCount = 0;
            return false;
        }

        public static void ReleaseAgingBufferRead()
        {
            VisualPressureAgingRuntime active = s_active;
            if (active == null || !active._gizmoReadLocked)
                return;

            IDataVault vault = active.ResolveVault(true);
            if (vault != null)
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            active._gizmoReadLocked = false;
        }

        private VisualPressureAgingRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));
            _preSimulationPhase = new PreSimulationPhaseSystem(this); // COLD ALLOC: phase adapter - owner: SHINOBU_219
            _simulationPhase = new SimulationPhaseSystem(this);       // COLD ALLOC: phase adapter - owner: SHINOBU_219
            _postSimulationPhase = new PostSimulationPhaseSystem(this);// COLD ALLOC: phase adapter - owner: SHINOBU_219
            _visualSyncPhase = new VisualSyncPhaseSystem(this);       // COLD ALLOC: phase adapter - owner: SHINOBU_219
        }

        private void Initialize()
        {
            _shutdown = false;
            _vault = GlobalRegistry.DataVault;
            EnsureGraphicsBuffers();
            RegisterDispatcherPhases();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            ReleaseAgingBufferRead();
            UnlockJobBuffers();
            UnregisterDispatcherPhases();
            ReleaseGraphicsBuffers();
            ReleaseVaultHandles(_vault);
            _vault = null;
            _vaultInitialized = false;
            _simulationScheduled = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }
            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }
            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }
            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }
        }

        private IDataVault ResolveVault(bool allowRegistryLookup = false)
        {
            IDataVault vault = _vault;
            if (vault != null)
                return vault;

            if (!allowRegistryLookup)
                return null;

            vault = GlobalRegistry.DataVault;
            _vault = vault;
            return vault;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return;

            ApplyPendingEditorTuningImmediate();
        }

        private JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (_simulationScheduled)
                return dependsOn;

            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return dependsOn;

            if (!TryLockJobBuffers(vault))
                return dependsOn;

            _frame = context.Frame;
            if (!TryResolveJobBuffers(
                    vault,
                    out NativeArray<VisualAgingParamsDTO> output,
                    out NativeArray<VisualAgingRuntimeDTO> runtime,
                    out NativeArray<VisualAgingTuningDTO> tuning,
                    out NativeArray<VisualAgingTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor))
            {
                UnlockJobBuffers();
                return dependsOn;
            }

            bool keepLocksForScheduledJob = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = default;
                NativeArray<double3> nodeAups = default;
                bool hasStructural = TryResolveStructuralInputs(vault, out states, out nodeAups);

                NativeArray<StructuralTuningDTO> structuralTuning = default;
                if (hasStructural)
                    TryResolveStructuralTuning(vault, out structuralTuning);

                NativeArray<float> temperatures = default;
                _runtimeFlags &= ~FlagThermalSource;
                if (!TryResolveThermalInput(vault, out temperatures))
                    vault.TryResolveHandle(in _mockTemperatureHandle, out temperatures);

                VisualAgingTuningDTO localTuning = tuning[0];
                float quality = ResolveGlobalQualityWeight();
                int count = ResolveActiveCount(hasStructural, states, structuralTuning, localTuning, output.Length, quality);
                _activeCount = count;

                double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                JobHandle handle;
                if (hasStructural)
                {
                    _runtimeFlags = (_runtimeFlags & ~FlagMockSource) | FlagStructuralSource | FlagNoRollbackState;
                    handle = new ProcessAgingParametersJob
                    {
                        States = states,
                        NodeAups = nodeAups,
                        StructuralTuning = structuralTuning,
                        Temperatures = temperatures,
                        Output = output,
                        Tuning = localTuning,
                        OriginAup = originAup,
                        Frame = context.Frame,
                        ActiveCount = count,
                        GlobalQualityWeight = quality
                    }.Schedule(count, JobBatchSize, dependsOn);
                }
                else
                {
                    _runtimeFlags = (_runtimeFlags & ~FlagStructuralSource) | FlagMockSource | FlagNoRollbackState;
                    handle = new GenerateMockAgingDataJob
                    {
                        Output = output,
                        Temperatures = temperatures,
                        Tuning = localTuning,
                        OriginAup = originAup,
                        Frame = context.Frame,
                        ActiveCount = count,
                        GlobalQualityWeight = quality
                    }.Schedule(count, JobBatchSize, dependsOn);
                }

                handle = new RecordVisualAgingTelemetryJob
                {
                    Output = output,
                    Runtime = runtime,
                    Telemetry = telemetry,
                    TelemetryCursor = telemetryCursor,
                    Frame = context.Frame,
                    ActiveCount = count,
                    UploadedCount = _uploadedCount,
                    UploadedBytes = (uint)(_uploadedCount * UnsafeUtility.SizeOf<VisualAgingParamsDTO>()),
                    GpuUploadMicroseconds = runtime[0].LastUploadMicroseconds,
                    GlobalQualityWeight = quality,
                    RuntimeFlags = _runtimeFlags,
                    LayoutHash = ResolveLayoutHash(),
                    CsvGeneration = localTuning.CsvGeneration
                }.Schedule(handle);

                _simulationScheduled = true;
                _agingDirty = true;
                keepLocksForScheduledJob = true;
                H8Memory.RegisterActiveJob(OwnerSystemId, handle);
                return handle;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    UnlockJobBuffers();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
            {
                UnlockJobBuffers();
                _simulationScheduled = false;
                _hasGeneratedPayload = _activeCount > 0;
            }
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault) || !EnsureGraphicsBuffers())
                return;

            bool paramsLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            if (!paramsLocked)
                return;

            bool runtimeLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            if (!runtimeLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return;
            }

            bool telemetryLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTelemetryRing, OwnerSystemId);
            if (!telemetryLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return;
            }

            bool cursorLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTelemetryCursor, OwnerSystemId);
            if (!cursorLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryRing, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return;
            }

            try
            {
                vault.TryResolveHandle(in _paramsHandle, out NativeArray<VisualAgingParamsDTO> aging);
                vault.TryResolveHandle(in _runtimeHandle, out NativeArray<VisualAgingRuntimeDTO> runtime);
                vault.TryResolveHandle(in _telemetryHandle, out NativeArray<VisualAgingTelemetryEntry> telemetry);
                vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor);
                if (!aging.IsCreated || !runtime.IsCreated || !telemetry.IsCreated || !telemetryCursor.IsCreated ||
                    runtime.Length == 0 || telemetryCursor.Length == 0)
                    return;

                int bufferCapacity = math.min(aging.Length, _agingBufferA != null ? _agingBufferA.count : 0);
                int uploadCount = _hasGeneratedPayload ? math.clamp(_activeCount, 1, bufferCapacity) : 0;
                float quality = ResolveGlobalQualityWeight();
                long start = Stopwatch.GetTimestamp();
                GraphicsBuffer readBuffer = SelectAgingBuffer(_readBufferIndex);
                if (_hasGeneratedPayload && uploadCount > 0 && (_agingDirty || uploadCount != _uploadedCount))
                {
                    int writeIndex = _readBufferIndex ^ 1;
                    GraphicsBuffer writeBuffer = SelectAgingBuffer(writeIndex);
                    UploadNativeArray(writeBuffer, aging, uploadCount);
                    _readBufferIndex = writeIndex;
                    _uploadedCount = uploadCount;
                    _agingDirty = false;
                    readBuffer = writeBuffer;
                }
                else if (!_hasGeneratedPayload)
                {
                    _uploadedCount = 0;
                }

                float targetPayloadBlend = uploadCount > 0 && readBuffer != null ? 1.0f : 0.0f;
                _payloadBlend01 = math.saturate(math.lerp(_payloadBlend01, targetPayloadBlend, math.lerp(0.25f, 0.85f, quality)));
                Shader.SetGlobalBuffer(AgingParamsId, readBuffer);
                Shader.SetGlobalVector(AgingRuntimeId, new Vector4(uploadCount, _payloadBlend01, quality, (float)_runtimeFlags));
                float uploadUs = ElapsedMicroseconds(start);

                VisualAgingRuntimeDTO current = runtime[0];
                _telemetryCursor = telemetryCursor[0];
                current.Frame = _frame;
                current.UploadedCount = uploadCount;
                current.LastUploadMicroseconds = uploadUs;
                current.GlobalQualityWeight = quality;
                current.Flags = _runtimeFlags;
                if (uploadUs > UploadFaultMicroseconds)
                    current.FaultFlags |= FlagUploadFault;
                if (!ValidateLayout())
                    current.FaultFlags |= FlagLayoutFault;
                runtime[0] = current;

                if ((current.FaultFlags & (FlagUploadFault | FlagLayoutFault | FlagNonFinite)) != 0u && !_dumpedFault)
                {
                    DumpTelemetry(telemetry);
                    _dumpedFault = true;
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryCursor, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryRing, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            }
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            _vaultInitialized =
                TryResolveOrAcquire(vault, ref _paramsHandle, BufferID.VisualPressureAgingParams, Capacity, NativeArrayOptions.UninitializedMemory, out NativeArray<VisualAgingParamsDTO> output) &&
                TryResolveOrAcquire(vault, ref _runtimeHandle, BufferID.VisualPressureAgingRuntime, 1, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingRuntimeDTO> runtime) &&
                TryResolveOrAcquire(vault, ref _telemetryHandle, BufferID.VisualPressureAgingTelemetryRing, TelemetryFrameCount, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingTelemetryEntry> telemetry) &&
                TryResolveOrAcquire(vault, ref _telemetryCursorHandle, BufferID.VisualPressureAgingTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out NativeArray<int> telemetryCursor) &&
                TryResolveOrAcquire(vault, ref _tuningHandle, BufferID.VisualPressureAgingTuning, 1, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingTuningDTO> tuning) &&
                TryResolveOrAcquire(vault, ref _csvScratchHandle, BufferID.VisualPressureAgingCsvScratch, CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out NativeArray<byte> csvScratch) &&
                TryResolveOrAcquire(vault, ref _mockTemperatureHandle, BufferID.VisualPressureAgingMockTemperature, 1, NativeArrayOptions.ClearMemory, out NativeArray<float> mockTemperature) &&
                output.IsCreated &&
                runtime.IsCreated &&
                telemetry.IsCreated &&
                telemetryCursor.IsCreated &&
                tuning.IsCreated &&
                csvScratch.IsCreated &&
                mockTemperature.IsCreated;

            if (!_vaultInitialized)
                return false;

            if (!_defaultsInitialized || !ValidateLayout())
            {
                if (!WriteDefaults(vault))
                    return false;
            }

            return true;
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsHandleValid(in handle) &&
                vault.TryGetBufferGeneration(bufferId, out uint generation) &&
                handle.Generation == generation &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                IsHandleValid(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsHandleValid(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private bool WriteDefaults(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool paramsLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            if (!paramsLocked)
                return false;

            bool runtimeLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            if (!runtimeLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return false;
            }

            bool tuningLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            if (!tuningLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return false;
            }

            bool mockTemperatureLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
            if (!mockTemperatureLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                return false;
            }

            try
            {
                vault.TryResolveHandle(in _paramsHandle, out NativeArray<VisualAgingParamsDTO> output);
                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                vault.TryResolveHandle(in _mockTemperatureHandle, out NativeArray<float> mockTemperature);
                vault.TryResolveHandle(in _runtimeHandle, out NativeArray<VisualAgingRuntimeDTO> runtime);
                if (!output.IsCreated || output.Length == 0 ||
                    !tuning.IsCreated || tuning.Length == 0 ||
                    !mockTemperature.IsCreated || mockTemperature.Length == 0 ||
                    !runtime.IsCreated || runtime.Length == 0)
                {
                    return false;
                }

                output[0] = default;
                tuning[0] = s_pendingEditorTuning = DefaultTuning(_csvGeneration);
                mockTemperature[0] = 42.0f;
                runtime[0] = new VisualAgingRuntimeDTO
                {
                    Flags = FlagMockSource | FlagNoRollbackState,
                    LayoutHash = ResolveLayoutHash(),
                    GlobalQualityWeight = ResolveGlobalQualityWeight(),
                    CsvGeneration = _csvGeneration
                };

                _runtimeFlags = FlagMockSource | FlagNoRollbackState;
                _activeCount = 0;
                _uploadedCount = 0;
                _payloadBlend01 = 0.0f;
                _hasGeneratedPayload = false;
                _agingDirty = true;
                _defaultsInitialized = true;
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            }
        }

        private bool TryResolveJobBuffers(
            IDataVault vault,
            out NativeArray<VisualAgingParamsDTO> output,
            out NativeArray<VisualAgingRuntimeDTO> runtime,
            out NativeArray<VisualAgingTuningDTO> tuning,
            out NativeArray<VisualAgingTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            output = default;
            runtime = default;
            tuning = default;
            telemetry = default;
            telemetryCursor = default;
            return vault != null &&
                vault.TryResolveHandle(in _paramsHandle, out output) &&
                vault.TryResolveHandle(in _runtimeHandle, out runtime) &&
                vault.TryResolveHandle(in _tuningHandle, out tuning) &&
                vault.TryResolveHandle(in _telemetryHandle, out telemetry) &&
                vault.TryResolveHandle(in _telemetryCursorHandle, out telemetryCursor) &&
                output.IsCreated &&
                runtime.IsCreated &&
                tuning.IsCreated &&
                telemetry.IsCreated &&
                telemetryCursor.IsCreated &&
                output.Length > 0 &&
                runtime.Length > 0 &&
                tuning.Length > 0 &&
                telemetry.Length > 0 &&
                telemetryCursor.Length > 0;
        }

        private bool TryResolveStructuralInputs(
            IDataVault vault,
            out NativeArray<IntegrityStateDTO> states,
            out NativeArray<double3> nodeAups)
        {
            states = default;
            nodeAups = default;
            if (!vault.TryGetGenerationHandle(BufferID.StructuralIntegrityStates, out VaultGenerationHandle<IntegrityStateDTO> statesHandle) ||
                !vault.TryGetGenerationHandle(BufferID.StructuralIntegrityNodeAups, out VaultGenerationHandle<double3> nodeAupsHandle) ||
                !TryLockStructuralInputs(vault))
            {
                return false;
            }

            bool resolved = vault.TryResolveHandle(in statesHandle, out states) &&
                vault.TryResolveHandle(in nodeAupsHandle, out nodeAups) &&
                states.IsCreated &&
                nodeAups.IsCreated &&
                states.Length > 0 &&
                nodeAups.Length > 0;
            if (!resolved)
            {
                UnlockOptional(vault, BufferID.StructuralIntegrityNodeAups, 1 << 9);
                UnlockOptional(vault, BufferID.StructuralIntegrityStates, 1 << 8);
            }

            return resolved;
        }

        private bool TryResolveStructuralTuning(IDataVault vault, out NativeArray<StructuralTuningDTO> structuralTuning)
        {
            structuralTuning = default;
            if (!vault.TryGetGenerationHandle(BufferID.StructuralIntegrityTuning, out VaultGenerationHandle<StructuralTuningDTO> structuralTuningHandle) ||
                !TryLockOptional(vault, BufferID.StructuralIntegrityTuning, 1 << 10))
            {
                return false;
            }

            bool resolved = vault.TryResolveHandle(in structuralTuningHandle, out structuralTuning) &&
                structuralTuning.IsCreated &&
                structuralTuning.Length > 0;
            if (!resolved)
                UnlockOptional(vault, BufferID.StructuralIntegrityTuning, 1 << 10);

            return resolved;
        }

        private bool ApplyPendingEditorTuningImmediate(bool allowRegistryLookup = false)
        {
            if (!s_hasPendingEditorTuning)
                return true;

            IDataVault vault = ResolveVault(allowRegistryLookup);
            if (vault == null || !IsHandleValid(in _tuningHandle))
                return false;

            if (!vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId))
                return false;

            try
            {
                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                tuning[0] = s_pendingEditorTuning;
                _csvGeneration = math.max(_csvGeneration, s_pendingEditorTuning.CsvGeneration);
                _agingDirty = true;
                s_hasPendingEditorTuning = false;
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }
        }

        private bool TryResolveThermalInput(IDataVault vault, out NativeArray<float> temperatures)
        {
            temperatures = default;
            if (!vault.TryGetGenerationHandle(BufferID.ThermodynamicsTemperatureFrontMirror, out VaultGenerationHandle<float> frontMirrorHandle))
            {
                return false;
            }

            if (!TryLockOptional(vault, BufferID.ThermodynamicsTemperatureFrontMirror, 1 << 7))
                return false;

            bool resolved = vault.TryResolveHandle(in frontMirrorHandle, out temperatures) &&
                temperatures.IsCreated &&
                temperatures.Length > 0;
            if (!resolved)
            {
                UnlockOptional(vault, BufferID.ThermodynamicsTemperatureFrontMirror, 1 << 7);
                return false;
            }

            _runtimeFlags |= FlagThermalSource;
            return true;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            UnlockJobBuffers();
            if (!TryLock(vault, BufferID.VisualPressureAgingParams, 1 << 0)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingRuntime, 1 << 1)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTelemetryRing, 1 << 2)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTelemetryCursor, 1 << 3)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTuning, 1 << 4)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingMockTemperature, 1 << 5)) return false;
            return true;
        }

        private bool TryLockStructuralInputs(IDataVault vault)
        {
            bool statesLocked = TryLockOptional(vault, BufferID.StructuralIntegrityStates, 1 << 8);
            bool aupsLocked = TryLockOptional(vault, BufferID.StructuralIntegrityNodeAups, 1 << 9);
            if (statesLocked && aupsLocked)
                return true;

            if (statesLocked)
            {
                vault.TryUnlockBuffer(BufferID.StructuralIntegrityStates, OwnerSystemId);
                _lockedBufferMask &= ~(1 << 8);
            }
            if (aupsLocked)
            {
                vault.TryUnlockBuffer(BufferID.StructuralIntegrityNodeAups, OwnerSystemId);
                _lockedBufferMask &= ~(1 << 9);
            }
            return false;
        }

        private bool TryLock(IDataVault vault, BufferID bufferId, int bit)
        {
            if (!vault.TryLockBuffer(bufferId, OwnerSystemId))
            {
                UnlockJobBuffers();
                return false;
            }

            _lockedBufferMask |= bit;
            return true;
        }

        private bool TryLockOptional(IDataVault vault, BufferID bufferId, int bit)
        {
            if (!vault.TryLockBuffer(bufferId, OwnerSystemId))
                return false;

            _lockedBufferMask |= bit;
            return true;
        }

        private void UnlockOptional(IDataVault vault, BufferID bufferId, int bit)
        {
            if ((_lockedBufferMask & bit) == 0)
                return;

            vault.TryUnlockBuffer(bufferId, OwnerSystemId);
            _lockedBufferMask &= ~bit;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null || _lockedBufferMask == 0)
            {
                _lockedBufferMask = 0;
                return;
            }

            if ((_lockedBufferMask & (1 << 10)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityTuning, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 9)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityNodeAups, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 8)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityStates, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 7)) != 0) vault.TryUnlockBuffer(BufferID.ThermodynamicsTemperatureFrontMirror, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 5)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 4)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 3)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryCursor, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 2)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryRing, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 1)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 0)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            _lockedBufferMask = 0;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultGenerationHandle(vault, ref _mockTemperatureHandle);
            ReleaseVaultGenerationHandle(vault, ref _csvScratchHandle);
            ReleaseVaultGenerationHandle(vault, ref _tuningHandle);
            ReleaseVaultGenerationHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultGenerationHandle(vault, ref _telemetryHandle);
            ReleaseVaultGenerationHandle(vault, ref _runtimeHandle);
            ReleaseVaultGenerationHandle(vault, ref _paramsHandle);
            _defaultsInitialized = false;
            _hasGeneratedPayload = false;
            _activeCount = 0;
            _uploadedCount = 0;
            _payloadBlend01 = 0.0f;
            _agingDirty = true;
        }

        private static void ReleaseVaultGenerationHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsHandleValid(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u &&
                handle.SystemID == (uint)OwnerSystemId &&
                handle.Generation != 0u;
        }

        private bool EnsureGraphicsBuffers()
        {
            int stride = UnsafeUtility.SizeOf<VisualAgingParamsDTO>();
            bool changedA = EnsureBuffer(ref _agingBufferA, Capacity, stride);
            bool changedB = EnsureBuffer(ref _agingBufferB, Capacity, stride);
            if (changedA || changedB)
            {
                _readBufferIndex = 0;
                _uploadedCount = 0;
                _agingDirty = true;
            }

            return _agingBufferA != null && _agingBufferB != null;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);
            // COLD ALLOC: GraphicsBuffer[VisualAgingParamsDTO] - double-buffered LockBufferForWrite upload lane - owner: SHINOBU_219
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
            return true;
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseBuffer(ref _agingBufferA);
            ReleaseBuffer(ref _agingBufferB);
            _readBufferIndex = 0;
            _uploadedCount = 0;
            _payloadBlend01 = 0.0f;
            _agingDirty = true;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private GraphicsBuffer SelectAgingBuffer(int index)
        {
            return (index & 1) == 0 ? _agingBufferA : _agingBufferB;
        }

        private static void UploadNativeArray(GraphicsBuffer destination, NativeArray<VisualAgingParamsDTO> source, int count)
        {
            if (destination == null || !source.IsCreated || count <= 0)
                return;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<VisualAgingParamsDTO>())
                return;

            NativeArray<VisualAgingParamsDTO> mapped = destination.LockBufferForWrite<VisualAgingParamsDTO>(0, safeCount);
            void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            void* src = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(source);
            UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<VisualAgingParamsDTO>());
            destination.UnlockBufferAfterWrite<VisualAgingParamsDTO>(safeCount);
        }

        private bool ReloadCsvFromDisk(IDataVault vault, bool force)
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return false;

            long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (!force && ticks == _csvLastWriteTicks)
                return false;

            bool tuningLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            if (!tuningLocked)
                return false;

            bool scratchLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingCsvScratch, OwnerSystemId);
            if (!scratchLocked)
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                return false;
            }

            try
            {
                vault.TryResolveHandle(in _csvScratchHandle, out NativeArray<byte> scratch);
                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                if (!scratch.IsCreated || !tuning.IsCreated || tuning.Length == 0)
                    return false;

                int bytesRead = ReadFileIntoScratch(_csvPath, scratch);
                if (bytesRead <= 0)
                    return false;

                VisualAgingTuningDTO dto = tuning[0];
                if (!ParseAgingRulesCsv(scratch, bytesRead, ref dto))
                    return false;

                dto.CsvGeneration = unchecked(dto.CsvGeneration + 1u);
                dto.RuntimeFlags |= FlagCsvLoaded;
                tuning[0] = dto;
                s_pendingEditorTuning = dto;
                _csvGeneration = dto.CsvGeneration;
                _csvLastWriteTicks = ticks;
                _runtimeFlags |= FlagCsvLoaded;
                _agingDirty = true;
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingCsvScratch, OwnerSystemId);
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }
        }

        private static int ReadFileIntoScratch(string path, NativeArray<byte> scratch)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = stream.Length;
                if (length <= 0)
                    return 0;

                int readLength = (int)math.min(length, scratch.Length);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                Span<byte> span = new Span<byte>(ptr, readLength);
                return stream.Read(span);
            }
        }

        private static bool ParseAgingRulesCsv(NativeArray<byte> bytes, int byteCount, ref VisualAgingTuningDTO tuning)
        {
            bool parsed = false;
            int index = 0;
            while (index < byteCount)
            {
                SkipWhitespaceAndLineEnds(bytes, byteCount, ref index);
                if (index >= byteCount)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                int keyStart = index;
                while (index < byteCount && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                if (index >= byteCount || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                int keyEnd = TrimTokenEnd(bytes, keyStart, index);
                index++;
                float value = ParseFloat(bytes, byteCount, ref index, out bool ok);
                if (!ok)
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                uint key = HashToken(bytes, keyStart, keyEnd);
                parsed |= ApplyCsvValue(key, value, ref tuning);
                SkipLine(bytes, byteCount, ref index);
            }

            return parsed;
        }

        private static bool ApplyCsvValue(uint key, float value, ref VisualAgingTuningDTO tuning)
        {
            switch (key)
            {
                case 0x436ED2B4u: tuning.RustStressMultiplier = math.max(0.0f, value); return true;       // rust_stress
                case 0x67C02865u: tuning.CorrosionPressureMultiplier = math.max(0.0f, value); return true;// corrosion_pressure
                case 0xA173BDF3u: tuning.SaltDepthMultiplier = math.max(0.0f, value); return true;        // salt_depth
                case 0xBFF5E684u: tuning.BiomassTemperatureMultiplier = math.max(0.0f, value); return true;// biomass_temperature
                case 0xEDD0017Fu: tuning.GlassFractureThreshold = math.saturate(value); return true;      // glass_threshold
                case 0x1BC3EDBDu: tuning.TemperatureBoostMultiplier = math.max(0.0f, value); return true; // temperature_boost
                case 0x1ACB3DD7u: tuning.QualityNoiseOctaveScale = math.saturate(value); return true;     // quality_noise
                default: return false;
            }
        }

        private void DumpTelemetry(NativeArray<VisualAgingTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(_dumpPath))
                return;

            string directory = Path.GetDirectoryName(_dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[24];
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), DumpMagic);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), DumpVersion);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), (uint)telemetry.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), (uint)UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>());
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), _frame);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20, 4), ResolveLayoutHash());
                stream.Write(header);

                int cursorStart = WrapTelemetryIndex(_telemetryCursor, telemetry.Length);
                for (int offset = 0; offset < telemetry.Length; offset++)
                {
                    int index = WrapTelemetryIndex(cursorStart + offset, telemetry.Length);
                    VisualAgingTelemetryEntry entry = telemetry[index];
                    ReadOnlySpan<byte> entryBytes = new ReadOnlySpan<byte>(&entry, UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>());
                    stream.Write(entryBytes);
                }
            }
        }

        private static int WrapTelemetryIndex(int cursor, int length)
        {
            if (length <= 0)
                return 0;

            int wrapped = cursor % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }

        private static int ResolveActiveCount(
            bool hasStructural,
            NativeArray<IntegrityStateDTO> states,
            NativeArray<StructuralTuningDTO> structuralTuning,
            VisualAgingTuningDTO tuning,
            int capacity,
            float quality)
        {
            int requested = hasStructural ? math.min(states.Length, capacity) : math.min(512, capacity);
            if (structuralTuning.IsCreated && structuralTuning.Length > 0 && structuralTuning[0].ActiveNodeCount > 0)
                requested = math.min(requested, structuralTuning[0].ActiveNodeCount);
            if (tuning.ActiveCountOverride > 0u)
                requested = math.min(requested, (int)tuning.ActiveCountOverride);

            float q = math.saturate(quality);
            float budget = math.lerp(0.25f, 1.0f, q * q);
            return math.clamp((int)math.ceil(requested * budget), 1, capacity);
        }

        private static VisualAgingTuningDTO DefaultTuning(uint generation)
        {
            return new VisualAgingTuningDTO
            {
                RustStressMultiplier = 0.78f,
                CorrosionPressureMultiplier = 0.62f,
                SaltDepthMultiplier = 0.58f,
                BiomassTemperatureMultiplier = 0.42f,
                GlassFractureThreshold = 0.68f,
                TemperatureBoostMultiplier = 0.018f,
                PressureScaleKPa = 5500.0f,
                DepthScaleMeters = 1400.0f,
                PittingStressMultiplier = 0.74f,
                QualityNoiseOctaveScale = 1.0f,
                CsvGeneration = generation,
                RuntimeFlags = FlagNoRollbackState,
                GlassHashStride = 5u,
                ActiveCountOverride = 0u,
                MockTemperatureC = 42.0f
            };
        }

        private static uint ResolveLayoutHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingParamsDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingTuningDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingRuntimeDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>()) * 16777619u;
            return hash;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 0.0f);
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float ElapsedMicroseconds(long start)
        {
            long now = Stopwatch.GetTimestamp();
            return (now - start) * 1000000.0f / Stopwatch.Frequency;
        }

#if UNITY_EDITOR
        private static int Offset<T>(string fieldName)
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
#endif

        private static uint HashToken(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }

        private static float ParseFloat(NativeArray<byte> bytes, int byteCount, ref int index, out bool ok)
        {
            SkipSpaces(bytes, byteCount, ref index);
            ok = false;
            float sign = 1.0f;
            if (index < byteCount && bytes[index] == (byte)'-')
            {
                sign = -1.0f;
                index++;
            }

            float value = 0.0f;
            int digits = 0;
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = value * 10.0f + (c - (byte)'0');
                index++;
                digits++;
            }

            if (index < byteCount && bytes[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < byteCount)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    value += (c - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    digits++;
                }
            }

            ok = digits > 0;
            return sign * value;
        }

        private static int TrimTokenEnd(NativeArray<byte> bytes, int start, int end)
        {
            int result = end;
            while (result > start)
            {
                byte c = bytes[result - 1];
                if (c != (byte)' ' && c != (byte)'\t')
                    break;
                result--;
            }
            return result;
        }

        private static void SkipWhitespaceAndLineEnds(NativeArray<byte> bytes, int byteCount, ref int index)
        {
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                    return;
                index++;
            }
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int byteCount, ref int index)
        {
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    return;
                index++;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int byteCount, ref int index)
        {
            while (index < byteCount)
            {
                byte c = bytes[index++];
                if (c == (byte)'\n')
                    break;
            }
        }

        private sealed class PreSimulationPhaseSystem : PhaseSystemBase
        {
            public PreSimulationPhaseSystem(VisualPressureAgingRuntime owner) : base(owner, DispatcherPhase.PreSimulation, 0x56323150u) { }
            public override void PreSimulationTick(in DispatcherTimingDTO timing) { Owner.PreSimulationTick(in timing); }
        }

        private sealed class SimulationPhaseSystem : PhaseSystemBase
        {
            public SimulationPhaseSystem(VisualPressureAgingRuntime owner) : base(owner, DispatcherPhase.Simulation, 0x56323153u) { }
            public override JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return Owner.ScheduleSimulation(in timing, in context, dependsOn);
            }
        }

        private sealed class PostSimulationPhaseSystem : PhaseSystemBase
        {
            public PostSimulationPhaseSystem(VisualPressureAgingRuntime owner) : base(owner, DispatcherPhase.PostSimulation, 0x5632314Fu) { }
            public override void PostSimulationTick(in DispatcherTimingDTO timing) { Owner.PostSimulationTick(in timing); }
        }

        private sealed class VisualSyncPhaseSystem : PhaseSystemBase
        {
            public VisualSyncPhaseSystem(VisualPressureAgingRuntime owner) : base(owner, DispatcherPhase.VisualSync, 0x56323156u) { }
            public override void VisualSyncTick(in DispatcherTimingDTO timing) { Owner.VisualSyncTick(in timing); }
        }

        private abstract class PhaseSystemBase : IDispatcherSystem
        {
            protected readonly VisualPressureAgingRuntime Owner;
            private readonly DispatcherPhase _phase;
            private readonly uint _hash;

            protected PhaseSystemBase(VisualPressureAgingRuntime owner, DispatcherPhase phase, uint hash)
            {
                Owner = owner;
                _phase = phase;
                _hash = hash;
            }

            public uint GetSystemIdHash() { return _hash; }
            public DispatcherPhase GetDispatcherPhase() { return _phase; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public virtual void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public virtual JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public virtual void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public virtual void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProcessAgingParametersJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly, NoAlias] public NativeArray<StructuralTuningDTO> StructuralTuning;
        [ReadOnly, NoAlias] public NativeArray<float> Temperatures;
        [NoAlias] public NativeArray<VisualAgingParamsDTO> Output;
        public VisualAgingTuningDTO Tuning;
        public double3 OriginAup;
        public uint Frame;
        public int ActiveCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Output.Length || (uint)index >= (uint)States.Length || (uint)index >= (uint)NodeAups.Length)
                return;

            IntegrityStateDTO state = States[index];
            double3 nodeAup = NodeAups[index];
            double3 seaLevel = StructuralTuning.IsCreated && StructuralTuning.Length > 0 ? StructuralTuning[0].SeaLevelAup : new double3(0.0);
            float pressureScale = math.max(1.0f, Tuning.PressureScaleKPa);
            float depthScale = math.max(1.0f, Tuning.DepthScaleMeters);
            float3 local = Localize(nodeAup, OriginAup);
            float depthMeters = math.max(0.0f, (float)(seaLevel.y - nodeAup.y));
            float pressure01 = math.saturate(FiniteOr(state.AppliedPressure, 0.0f) / pressureScale);
            float depth01 = math.saturate(depthMeters / depthScale);
            float stress01 = math.saturate(FiniteOr(state.CurrentStress, 0.0f));
            float buckling01 = math.saturate(FiniteOr(state.BucklingScalar, 0.0f));
            float baseWeakness01 = 1.0f - math.saturate(FiniteOr(state.BaseStrength, 1.0f));
            float temperatureC = ResolveTemperature(index);
            float temperatureBoost = math.saturate(math.max(0.0f, temperatureC - 4.0f) * Tuning.TemperatureBoostMultiplier);
            float q = math.saturate(GlobalQualityWeight);
            uint hash = Mix(state.NodeHash ^ ((uint)index * 747796405u) ^ Frame);
            float seed01 = (hash & 65535u) * (1.0f / 65535.0f);
            float glassMask = ((hash % math.max(1u, Tuning.GlassHashStride)) == 0u) ? 1.0f : 0.0f;
            float rust = math.saturate((stress01 * Tuning.RustStressMultiplier + depth01 * 0.35f + baseWeakness01 * 0.22f) * (1.0f + temperatureBoost));
            float corrosion = math.saturate((pressure01 * Tuning.CorrosionPressureMultiplier + buckling01 * 0.45f) * (0.35f + q));
            float pitting = math.saturate((stress01 + buckling01 + corrosion) * Tuning.PittingStressMultiplier * (0.55f + seed01 * 0.45f));
            float salt = math.saturate(depth01 * Tuning.SaltDepthMultiplier + pressure01 * 0.18f);
            float biomass = math.saturate(temperatureBoost * Tuning.BiomassTemperatureMultiplier + (1.0f - stress01) * depth01 * 0.18f);
            float fracture = glassMask * math.saturate((stress01 + buckling01 * 0.55f - Tuning.GlassFractureThreshold) * math.rcp(math.max(0.01f, 1.0f - Tuning.GlassFractureThreshold)));

            Output[index] = new VisualAgingParamsDTO
            {
                RustAndCorrosion = new float4(rust, corrosion, pitting, seed01),
                SaltAndBiomass = new float4(salt, biomass, temperatureBoost, glassMask),
                StressAndMicroFractures = new float4(stress01, fracture, buckling01, q),
                DepthAndPressure = new float4(local.x, local.y, local.z, pressure01)
            };
        }

        private float ResolveTemperature(int index)
        {
            if (!Temperatures.IsCreated || Temperatures.Length == 0)
                return Tuning.MockTemperatureC;

            float temperature = Temperatures[index % Temperatures.Length];
            return math.isfinite(temperature) ? temperature : Tuning.MockTemperatureC;
        }

        private static float3 Localize(double3 aup, double3 origin)
        {
            double3 delta = aup - origin;
            delta = math.clamp(delta, new double3(-8192.0), new double3(8192.0));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockAgingDataJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VisualAgingParamsDTO> Output;
        [ReadOnly, NoAlias] public NativeArray<float> Temperatures;
        public VisualAgingTuningDTO Tuning;
        public double3 OriginAup;
        public uint Frame;
        public int ActiveCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Output.Length)
                return;

            uint hash = Mix(((uint)index + 1u) * 2891336453u ^ Frame ^ 0x53323139u);
            float t = (hash & 1023u) * (1.0f / 1023.0f);
            float lane = ((hash >> 10) & 1023u) * (1.0f / 1023.0f);
            float depth01 = math.saturate(0.18f + lane * 0.82f);
            float stress01 = math.saturate(0.25f + t * 0.75f);
            float buckling = math.saturate(stress01 * 0.65f + Triangle(Frame * 0.0031f + t) * 0.18f);
            float temperatureC = ResolveTemperature();
            float temperatureBoost = math.saturate(math.max(0.0f, temperatureC - 4.0f) * Tuning.TemperatureBoostMultiplier);
            float pressure01 = math.saturate(depth01 * 0.75f + stress01 * 0.25f);
            float q = math.saturate(GlobalQualityWeight);
            float seed01 = ((hash >> 20) & 1023u) * (1.0f / 1023.0f);
            float glassMask = ((hash % math.max(1u, Tuning.GlassHashStride)) == 0u) ? 1.0f : 0.0f;
            double3 mockAup = OriginAup + new double3((index & 31) * 5.0, -depth01 * Tuning.DepthScaleMeters, (index >> 5) * 5.0);
            float3 local = Localize(mockAup, OriginAup);
            float fracture = glassMask * math.saturate((stress01 + buckling * 0.55f - Tuning.GlassFractureThreshold) * math.rcp(math.max(0.01f, 1.0f - Tuning.GlassFractureThreshold)));

            Output[index] = new VisualAgingParamsDTO
            {
                RustAndCorrosion = new float4(
                    math.saturate((stress01 * Tuning.RustStressMultiplier + depth01 * 0.32f) * (1.0f + temperatureBoost)),
                    math.saturate(pressure01 * Tuning.CorrosionPressureMultiplier),
                    math.saturate((stress01 + buckling) * Tuning.PittingStressMultiplier),
                    seed01),
                SaltAndBiomass = new float4(
                    math.saturate(depth01 * Tuning.SaltDepthMultiplier),
                    math.saturate(temperatureBoost * Tuning.BiomassTemperatureMultiplier + depth01 * 0.12f),
                    temperatureBoost,
                    glassMask),
                StressAndMicroFractures = new float4(stress01, fracture, buckling, q),
                DepthAndPressure = new float4(local.x, local.y, local.z, pressure01)
            };
        }

        private static float3 Localize(double3 aup, double3 origin)
        {
            double3 delta = aup - origin;
            delta = math.clamp(delta, new double3(-8192.0), new double3(8192.0));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static float Triangle(float phase)
        {
            return 1.0f - math.abs(math.frac(phase) * 2.0f - 1.0f);
        }

        private float ResolveTemperature()
        {
            if (!Temperatures.IsCreated || Temperatures.Length == 0)
                return Tuning.MockTemperatureC;

            float temperature = Temperatures[0];
            return math.isfinite(temperature) ? temperature : Tuning.MockTemperatureC;
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct RecordVisualAgingTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<VisualAgingParamsDTO> Output;
        [NoAlias] public NativeArray<VisualAgingRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<VisualAgingTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint Frame;
        public int ActiveCount;
        public int UploadedCount;
        public uint UploadedBytes;
        public float GpuUploadMicroseconds;
        public float GlobalQualityWeight;
        public uint RuntimeFlags;
        public uint LayoutHash;
        public uint CsvGeneration;

        public void Execute()
        {
            if (!Output.IsCreated || !Runtime.IsCreated || Runtime.Length == 0 || !Telemetry.IsCreated || Telemetry.Length == 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0)
            {
                return;
            }

            int sampleCount = math.clamp(ActiveCount, 0, Output.Length);
            int sampleBudget = math.max(16, (int)math.round(math.lerp(32.0f, 256.0f, math.saturate(GlobalQualityWeight))));
            int stride = sampleCount > sampleBudget ? math.max(1, sampleCount / sampleBudget) : 1;
            float stressMean = 0.0f;
            float fractureMean = 0.0f;
            float tempMean = 0.0f;
            float maxDepth = 0.0f;
            uint activeGlassFractures = 0u;
            uint hash = 2166136261u;
            uint faults = 0u;
            int sampled = 0;
            for (int i = 0; i < sampleCount && sampled < sampleBudget; i += stride)
            {
                VisualAgingParamsDTO dto = Output[i];
                if (!math.all(math.isfinite(dto.RustAndCorrosion)) ||
                    !math.all(math.isfinite(dto.SaltAndBiomass)) ||
                    !math.all(math.isfinite(dto.StressAndMicroFractures)) ||
                    !math.all(math.isfinite(dto.DepthAndPressure)))
                {
                    faults |= 1u << 30;
                    continue;
                }

                stressMean += dto.StressAndMicroFractures.x;
                fractureMean += dto.StressAndMicroFractures.y;
                tempMean += dto.SaltAndBiomass.z;
                maxDepth = math.max(maxDepth, math.saturate(math.abs(dto.DepthAndPressure.y) * 0.0007142857f));
                if (dto.SaltAndBiomass.w > 0.5f && dto.StressAndMicroFractures.y > 0.01f)
                    activeGlassFractures++;
                hash = (hash ^ math.asuint(dto.RustAndCorrosion.x)) * 16777619u;
                hash = (hash ^ math.asuint(dto.DepthAndPressure.w)) * 16777619u;
                sampled++;
            }

            float inv = sampled > 0 ? math.rcp(sampled) : 0.0f;
            int cursor = WrapTelemetryIndex(TelemetryCursor[0], Telemetry.Length);
            uint sequence = Runtime[0].Sequence + 1u;
            float cpuEstimateUs = ActiveCount * math.lerp(0.010f, 0.026f, math.saturate(GlobalQualityWeight));
            VisualAgingTelemetryEntry entry = new VisualAgingTelemetryEntry
            {
                Frame = Frame,
                Flags = RuntimeFlags | faults,
                StateHash = hash,
                LayoutHash = LayoutHash,
                ActiveCount = ActiveCount,
                UploadedCount = UploadedCount,
                UploadedBytes = UploadedBytes,
                GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                MaxDepth01 = maxDepth,
                AverageStress01 = stressMean * inv,
                ActiveGlassFractures = activeGlassFractures,
                MeanTemperatureBoost01 = tempMean * inv,
                CpuEstimateMicroseconds = cpuEstimateUs,
                GpuUploadMicroseconds = GpuUploadMicroseconds,
                CsvGeneration = CsvGeneration,
                Sequence = sequence
            };

            Telemetry[cursor] = entry;
            TelemetryCursor[0] = (cursor + 1) % Telemetry.Length;
            Runtime[0] = new VisualAgingRuntimeDTO
            {
                Frame = Frame,
                Flags = RuntimeFlags,
                ActiveCount = ActiveCount,
                UploadedCount = UploadedCount,
                GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                LastUploadMicroseconds = GpuUploadMicroseconds,
                LastCpuEstimateMicroseconds = cpuEstimateUs,
                MaxDepth01 = maxDepth,
                StateHash = hash,
                LayoutHash = LayoutHash,
                CsvGeneration = CsvGeneration,
                FaultFlags = faults,
                AverageStress01 = stressMean * inv,
                MeanMicrofracture01 = fractureMean * inv,
                MeanTemperatureBoost01 = tempMean * inv,
                Sequence = sequence
            };
        }

        private static int WrapTelemetryIndex(int cursor, int length)
        {
            int wrapped = cursor % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }
    }
}
