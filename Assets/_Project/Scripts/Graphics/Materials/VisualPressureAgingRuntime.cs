using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InstanceDegradationDTO
    {
        [FieldOffset(0)] public uint InstanceID;
        [FieldOffset(4)] public float RustAmount;
        [FieldOffset(8)] public float ScorchAmount;
        [FieldOffset(12)] public float BioFouling;
        [FieldOffset(16)] public float StructuralStress;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public uint _pad1;
        [FieldOffset(28)] public uint _pad2;
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
        [FieldOffset(60)] public float ScorchIntensityMultiplier;
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DegradationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int InstancesEvaluated;
        [FieldOffset(12)] public int UploadedCount;
        [FieldOffset(16)] public float AverageBaseStress01;
        [FieldOffset(20)] public float MaxScorch01;
        [FieldOffset(24)] public float GpuUploadMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint LayoutHash;
        [FieldOffset(40)] public uint UploadedBytes;
        [FieldOffset(44)] public uint CsvGeneration;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    public sealed unsafe class VisualPressureAgingRuntime : IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsMaterials;
        private const uint SystemHash = 0x53323139u; // S219
        private const int Capacity = StructuralIntegrityConstants.MaxNodeCapacity;
        private const int TelemetryFrameCount = 300;
        private const int TelemetryDumpHeaderBytes = 32;
        private const int TelemetryDumpSnapshotBytes = TelemetryDumpHeaderBytes + TelemetryFrameCount * 64 * 2;
        private const int CsvScratchBytes = TelemetryDumpSnapshotBytes;
        private const int JobBatchSize = 64;
        private const float UploadFaultMicroseconds = 100.0f;
        private const uint DumpMagic = 0x56414745u; // VAGE
        private const uint DumpVersion = 2u;
        private const string CsvRelativePath = "Data/Visuals/environmental_aging_rules.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_219.bin";
        private const string DegradationDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_219_Degradation.bin";

        private const uint FlagStructuralSource = 1u << 0;
        private const uint FlagMockSource = 1u << 1;
        private const uint FlagThermalSource = 1u << 2;
        private const uint FlagCsvLoaded = 1u << 3;
        private const uint FlagNoRollbackState = 1u << 4;
        private const uint FlagExternalGenerationRefresh = 1u << 5;
        private const uint FlagJobFencePending = 1u << 6;
        private const uint FlagLayoutFault = 1u << 29;
        private const uint FlagNonFinite = 1u << 30;
        private const uint FlagUploadFault = 1u << 31;

        private static readonly int AgingParamsId = Shader.PropertyToID("_GlobalBaseAgingParams");
        private static readonly int AgingRuntimeId = Shader.PropertyToID("_GlobalBaseAgingRuntime");
        private static readonly int DegradationBufferId = Shader.PropertyToID("_GlobalUberNoirDegradation");
        private static readonly int DegradationRuntimeId = Shader.PropertyToID("_GlobalUberNoirDegradationRuntime");

        private static VisualPressureAgingRuntime s_active;
        private static bool s_hasPendingEditorTuning;
        private static VisualAgingTuningDTO s_pendingEditorTuning = DefaultTuning(1u);

        private IDataVault _vault;
        private VaultGenerationHandle<VisualAgingParamsDTO> _paramsHandle;
        private VaultGenerationHandle<VisualAgingRuntimeDTO> _runtimeHandle;
        private VaultGenerationHandle<VisualAgingTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<InstanceDegradationDTO> _degradationHandle;
        private VaultGenerationHandle<DegradationTelemetryEntry> _degradationTelemetryHandle;
        private VaultGenerationHandle<int> _degradationTelemetryCursorHandle;
        private VaultGenerationHandle<VisualAgingTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<float> _mockTemperatureHandle;
        private VaultGenerationHandle<IntegrityStateDTO> _structuralStatesHandle;
        private VaultGenerationHandle<double3> _structuralNodeAupsHandle;
        private VaultGenerationHandle<StructuralTuningDTO> _structuralTuningHandle;
        private VaultGenerationHandle<float> _thermalFrontMirrorHandle;

        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;

        private GraphicsBuffer _agingBufferA;
        private GraphicsBuffer _agingBufferB;
        private GraphicsBuffer _degradationBufferA;
        private GraphicsBuffer _degradationBufferB;
        private string _csvPath;
        private string _degradationCsvPath;
        private string _dumpPath;
        private string _degradationDumpPath;
        private FileStream _dumpStream;
        private FileStream _degradationDumpStream;
#if UNITY_EDITOR
        private byte[] _csvManagedScratch;
#endif
        private long _csvLastWriteTicks;
        private long _degradationCsvLastWriteTicks;
        private int _lockedBufferMask;
        private int _activeCount;
        private int _uploadedCount;
        private int _readBufferIndex;
        private int _degradationUploadedCount;
        private int _degradationReadBufferIndex;
        private int _telemetryCursor;
        private int _degradationTelemetryCursor;
        private uint _frame;
        private uint _csvGeneration = 1u;
        private uint _runtimeFlags = FlagMockSource | FlagNoRollbackState;
        private uint _publishedFlags = FlagMockSource | FlagNoRollbackState;
        private float _payloadBlend01;
        private float _publishedUploadMicroseconds;
        private float _cachedGlobalQualityWeight;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredHotSwap;
        private JobHandle _scheduledSimulationHandle;
        private bool _vaultInitialized;
        private bool _defaultsInitialized;
        private bool _simulationScheduled;
        private bool _hasGeneratedPayload;
        private bool _agingDirty = true;
        private bool _degradationDirty = true;
        private bool _dumpedFault;
        private bool _dumpedDegradationFault;
        private bool _dumpWriteFailureLogged;
        private bool _degradationDumpWriteFailureLogged;
        private bool _shutdown;
        private bool _gizmoReadLocked;
        private bool _degradationGizmoReadLocked;

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
                UnsafeUtility.SizeOf<InstanceDegradationDTO>() == 32 &&
                UnsafeUtility.SizeOf<VisualAgingTuningDTO>() == 64 &&
                UnsafeUtility.SizeOf<VisualAgingRuntimeDTO>() == 64 &&
                UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<DegradationTelemetryEntry>() == 64;
#if UNITY_EDITOR
            return sizeValid &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.RustAndCorrosion)) == 0 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.SaltAndBiomass)) == 16 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.StressAndMicroFractures)) == 32 &&
                Offset<VisualAgingParamsDTO>(nameof(VisualAgingParamsDTO.DepthAndPressure)) == 48 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO.InstanceID)) == 0 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO.RustAmount)) == 4 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO.ScorchAmount)) == 8 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO.BioFouling)) == 12 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO.StructuralStress)) == 16 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO._pad0)) == 20 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO._pad1)) == 24 &&
                Offset<InstanceDegradationDTO>(nameof(InstanceDegradationDTO._pad2)) == 28;
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
            float qualityNoiseScale,
            float scorchIntensity = 1.0f)
        {
            VisualAgingTuningDTO tuning = s_pendingEditorTuning;
            tuning.RustStressMultiplier = math.max(0.0f, SanitizeFloat(rustStress, tuning.RustStressMultiplier));
            tuning.CorrosionPressureMultiplier = math.max(0.0f, SanitizeFloat(corrosionPressure, tuning.CorrosionPressureMultiplier));
            tuning.SaltDepthMultiplier = math.max(0.0f, SanitizeFloat(saltDepth, tuning.SaltDepthMultiplier));
            tuning.BiomassTemperatureMultiplier = math.max(0.0f, SanitizeFloat(biomassTemperature, tuning.BiomassTemperatureMultiplier));
            tuning.GlassFractureThreshold = math.saturate(SanitizeFloat(glassThreshold, tuning.GlassFractureThreshold));
            tuning.TemperatureBoostMultiplier = math.max(0.0f, SanitizeFloat(temperatureBoost, tuning.TemperatureBoostMultiplier));
            tuning.QualityNoiseOctaveScale = math.saturate(SanitizeFloat(qualityNoiseScale, tuning.QualityNoiseOctaveScale));
            tuning.ScorchIntensityMultiplier = math.max(0.0f, SanitizeFloat(scorchIntensity, tuning.ScorchIntensityMultiplier));
            tuning.CsvGeneration = unchecked(tuning.CsvGeneration + 1u);
            s_pendingEditorTuning = tuning;
            s_hasPendingEditorTuning = true;

            VisualPressureAgingRuntime active = s_active;
            return active != null && active.ApplyPendingEditorTuningImmediate();
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

            if (active._simulationScheduled || !active._vaultInitialized)
                return false;

            activeCount = active._hasGeneratedPayload ? active._activeCount : 0;
            uploadMicroseconds = active._publishedUploadMicroseconds;
            flags = active._publishedFlags;
            return true;
        }

#if UNITY_EDITOR
        public static bool TryReloadEditorCsv()
        {
            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            IDataVault vault = active.ResolveVault();
            if (vault == null)
                return false;

            bool ready = active.HasCurrentCsvReloadState(vault) || active.TryInitializeVaultState(vault);
            if (!ready)
                return false;

            active.RefreshExternalInputHandles(vault);
            bool loadedAging = active.ReloadCsvFromDisk(vault, active._csvPath, ref active._csvLastWriteTicks, true);
            bool loadedDegradation = false;
            if (!string.Equals(active._csvPath, active._degradationCsvPath, StringComparison.OrdinalIgnoreCase))
                loadedDegradation = active.ReloadCsvFromDisk(vault, active._degradationCsvPath, ref active._degradationCsvLastWriteTicks, true);
            else
                active._degradationCsvLastWriteTicks = active._csvLastWriteTicks;

            return loadedAging | loadedDegradation;
        }
#endif

        public static bool TryOpenAgingBufferSnapshotLease(out NativeArray<VisualAgingParamsDTO>.ReadOnly aging, out int activeCount)
        {
            aging = default;
            activeCount = 0;
#if !UNITY_EDITOR
            return false;
#else
            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled || active._gizmoReadLocked)
                return false;

            IDataVault vault = active.ResolveVault();
            if (vault == null || !active.HasCurrentOwnedCoreState(vault))
                return false;

            if (!vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId))
                return false;

            active._gizmoReadLocked = true;
            bool agingReady = IsCurrentOwnedBuffer(
                vault,
                in active._paramsHandle,
                BufferID.VisualPressureAgingParams,
                Capacity,
                out NativeArray<VisualAgingParamsDTO> mutableAging);
            if (active._hasGeneratedPayload && agingReady && mutableAging.IsCreated && mutableAging.Length > 0)
            {
                aging = mutableAging.AsReadOnly();
                activeCount = math.min(active._activeCount, mutableAging.Length);
                return true;
            }

            CloseAgingBufferSnapshotLease();
            aging = default;
            activeCount = 0;
            return false;
#endif
        }

        public static void CloseAgingBufferSnapshotLease()
        {
#if UNITY_EDITOR
            VisualPressureAgingRuntime active = s_active;
            if (active == null || !active._gizmoReadLocked)
                return;

            IDataVault vault = active.ResolveVault();
            if (vault != null)
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            active._gizmoReadLocked = false;
#endif
        }

        public static bool TryOpenDegradationBufferSnapshotLease(out NativeArray<InstanceDegradationDTO>.ReadOnly degradation, out int activeCount)
        {
            degradation = default;
            activeCount = 0;
#if !UNITY_EDITOR
            return false;
#else
            VisualPressureAgingRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled || active._degradationGizmoReadLocked)
                return false;

            IDataVault vault = active.ResolveVault();
            if (vault == null || !active.HasCurrentOwnedCoreState(vault))
                return false;

            if (!vault.TryLockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId))
                return false;

            active._degradationGizmoReadLocked = true;
            bool degradationReady = IsCurrentOwnedBuffer(
                vault,
                in active._degradationHandle,
                BufferID.UberNoirInstanceDegradation,
                Capacity,
                out NativeArray<InstanceDegradationDTO> mutableDegradation);
            if (active._hasGeneratedPayload && degradationReady && mutableDegradation.IsCreated && mutableDegradation.Length > 0)
            {
                degradation = mutableDegradation.AsReadOnly();
                activeCount = math.min(active._activeCount, mutableDegradation.Length);
                return true;
            }

            CloseDegradationBufferSnapshotLease();
            degradation = default;
            activeCount = 0;
            return false;
#endif
        }

        public static void CloseDegradationBufferSnapshotLease()
        {
#if UNITY_EDITOR
            VisualPressureAgingRuntime active = s_active;
            if (active == null || !active._degradationGizmoReadLocked)
                return;

            IDataVault vault = active.ResolveVault();
            if (vault != null)
                vault.TryUnlockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
            active._degradationGizmoReadLocked = false;
#endif
        }

        private VisualPressureAgingRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _degradationCsvPath = _csvPath;
            _dumpPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));
            _degradationDumpPath = Path.GetFullPath(Path.Combine(projectRoot, DegradationDumpRelativePath));
            _preSimulationPhase = new PreSimulationPhaseSystem(this); // COLD ALLOC: phase adapter - owner: SHINOBU_219
            _simulationPhase = new SimulationPhaseSystem(this);       // COLD ALLOC: phase adapter - owner: SHINOBU_219
            _postSimulationPhase = new PostSimulationPhaseSystem(this);// COLD ALLOC: phase adapter - owner: SHINOBU_219
            _visualSyncPhase = new VisualSyncPhaseSystem(this);       // COLD ALLOC: phase adapter - owner: SHINOBU_219
        }

        private void Initialize()
        {
            _shutdown = false;
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            EnsureGraphicsBuffers();
            EnsureDumpStreams();
            if (_vault != null)
            {
                TryInitializeVaultState(_vault);
                RefreshExternalInputHandles(_vault);
            }
            TryRegisterHotSwapListener();
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
            CloseAgingBufferSnapshotLease();
            CloseDegradationBufferSnapshotLease();
            TryUnregisterHotSwapListener();
            CompleteSimulationForLifecycle();
            UnlockJobBuffers();
            UnregisterDispatcherPhases();
            ReleaseGraphicsBuffers();
            ReleaseDumpStreams();
            ReleaseVaultHandles(_vault);
            _vault = null;
            _vaultInitialized = false;
            _simulationScheduled = false;
            _scheduledSimulationHandle = default;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService as IDataVault;
            if (ReferenceEquals(_vault, nextVault))
                return;

            CloseAgingBufferSnapshotLease();
            CloseDegradationBufferSnapshotLease();
            CompleteSimulationForLifecycle();
            UnlockJobBuffers();
            RebindDataVaultForLifecycle(nextVault, previousService as IDataVault);
            if (_vault == null)
                return;

            TryInitializeVaultState(_vault);
            RefreshExternalInputHandles(_vault);
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault releaseVaultOverride = null)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            ReleaseVaultHandles(_vault ?? releaseVaultOverride);
            _vault = nextVault;
            _vaultInitialized = false;
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

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private IDataVault ResolveVault()
        {
            return _vault;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null)
                return;

            if (!HasCurrentOwnedCoreState(vault))
                return;

            MarkExternalGenerationRefresh(vault);
            RefreshGlobalQualitySnapshot(vault);
            ApplyPendingEditorTuningImmediate();
        }

        private JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledSimulationHandle))
                    return JobHandle.CombineDependencies(dependsOn, _scheduledSimulationHandle);

                UnlockJobBuffers();
                _runtimeFlags &= ~FlagJobFencePending;
                _simulationScheduled = false;
                _hasGeneratedPayload = _activeCount > 0;
            }

            IDataVault vault = ResolveVault();
            if (vault == null || !HasCurrentOwnedCoreState(vault))
                return dependsOn;

            MarkExternalGenerationRefresh(vault);
            float quality = RefreshGlobalQualitySnapshot(vault);
            if (_hasGeneratedPayload && ShouldSkipSimulationFrame(context.Frame, quality))
                return dependsOn;

            UnlockJobBuffers();
            NativeArray<float> temperatures = default;
            _runtimeFlags &= ~FlagThermalSource;
            bool hasThermalInput = AcquireThermalInputForSchedule(vault, out temperatures);

            NativeArray<IntegrityStateDTO> states = default;
            NativeArray<double3> nodeAups = default;
            bool hasStructural = AcquireStructuralInputsForSchedule(vault, out states, out nodeAups);

            NativeArray<StructuralTuningDTO> structuralTuning = default;
            if (hasStructural)
                AcquireStructuralTuningForSchedule(vault, out structuralTuning);

            if (!TryLockJobBuffers(vault, !hasThermalInput, out bool mockTemperatureLocked))
                return dependsOn;

            _frame = context.Frame;
            if (!BindLockedJobBuffersForSchedule(
                    vault,
                    out NativeArray<VisualAgingParamsDTO> output,
                    out NativeArray<InstanceDegradationDTO> degradation,
                    out NativeArray<VisualAgingRuntimeDTO> runtime,
                    out NativeArray<VisualAgingTuningDTO> tuning,
                    out NativeArray<VisualAgingTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    out NativeArray<DegradationTelemetryEntry> degradationTelemetry,
                    out NativeArray<int> degradationTelemetryCursor))
            {
                UnlockJobBuffers();
                return dependsOn;
            }

            bool keepLocksForScheduledJob = false;
            try
            {
                if (!hasThermalInput && mockTemperatureLocked &&
                    !IsCurrentOwnedBuffer(vault, in _mockTemperatureHandle, BufferID.VisualPressureAgingMockTemperature, 1, out temperatures))
                    temperatures = default;

                VisualAgingTuningDTO localTuning = tuning[0];
                int count = ResolveActiveCount(hasStructural, states, structuralTuning, localTuning, output.Length, quality);
                _activeCount = count;

                double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                JobHandle handle;
                if (hasStructural)
                {
                    _runtimeFlags = (_runtimeFlags & ~FlagMockSource) | FlagStructuralSource | FlagNoRollbackState;
                    handle = new CompileDegradationParametersJob
                    {
                        States = states,
                        NodeAups = nodeAups,
                        StructuralTuning = structuralTuning,
                        Temperatures = temperatures,
                        Output = output,
                        Degradation = degradation,
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
                    handle = new GenerateMockDegradationDataJob
                    {
                        Output = output,
                        Degradation = degradation,
                        Temperatures = temperatures,
                        Tuning = localTuning,
                        OriginAup = originAup,
                        Frame = context.Frame,
                        ActiveCount = count,
                        GlobalQualityWeight = quality
                    }.Schedule(count, JobBatchSize, dependsOn);
                }

                int lastUploadedCount = math.min(_uploadedCount, _degradationUploadedCount);
                uint lastUploadedBytes = (uint)(lastUploadedCount * (UnsafeUtility.SizeOf<VisualAgingParamsDTO>() + UnsafeUtility.SizeOf<InstanceDegradationDTO>()));
                handle = new RecordVisualAgingTelemetryJob
                {
                    Output = output,
                    Degradation = degradation,
                    Runtime = runtime,
                    Telemetry = telemetry,
                    TelemetryCursor = telemetryCursor,
                    DegradationTelemetry = degradationTelemetry,
                    DegradationTelemetryCursor = degradationTelemetryCursor,
                    Frame = context.Frame,
                    ActiveCount = count,
                    UploadedCount = lastUploadedCount,
                    UploadedBytes = lastUploadedBytes,
                    GpuUploadMicroseconds = _publishedUploadMicroseconds,
                    GlobalQualityWeight = quality,
                    RuntimeFlags = _runtimeFlags,
                    LayoutHash = ResolveLayoutHash(),
                    CsvGeneration = localTuning.CsvGeneration
                }.Schedule(handle);

                _simulationScheduled = true;
                _scheduledSimulationHandle = handle;
                _agingDirty = true;
                _degradationDirty = true;
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
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledSimulationHandle))
                {
                    _runtimeFlags |= FlagJobFencePending;
                    return;
                }

                _runtimeFlags &= ~FlagJobFencePending;
                UnlockJobBuffers();
                _simulationScheduled = false;
                _hasGeneratedPayload = _activeCount > 0;
            }
        }

        private void CompleteSimulationForLifecycle()
        {
            if (!_simulationScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _scheduledSimulationHandle, forceComplete: true);
            _runtimeFlags &= ~FlagJobFencePending;
            UnlockJobBuffers();
            _simulationScheduled = false;
            _hasGeneratedPayload = _activeCount > 0;
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null || !HasCurrentOwnedCoreState(vault) || !AreGraphicsBuffersReady())
                return;

            float quality = ResolveGlobalQualityWeight();
            int graphicsCapacity = math.min(
                _agingBufferA != null ? _agingBufferA.count : 0,
                _degradationBufferA != null ? _degradationBufferA.count : 0);
            int uploadCount = _hasGeneratedPayload && graphicsCapacity > 0
                ? math.clamp(_activeCount, 1, graphicsCapacity)
                : 0;
            GraphicsBuffer readBuffer = SelectAgingBuffer(_readBufferIndex);
            GraphicsBuffer degradationReadBuffer = SelectDegradationBuffer(_degradationReadBufferIndex);
            bool needsUpload = _hasGeneratedPayload &&
                uploadCount > 0 &&
                (_agingDirty || _degradationDirty || uploadCount != _uploadedCount || uploadCount != _degradationUploadedCount);

            long start = Stopwatch.GetTimestamp();
            if (needsUpload)
            {
                int writeIndex = _readBufferIndex ^ 1;
                int degradationWriteIndex = _degradationReadBufferIndex ^ 1;
                GraphicsBuffer writeBuffer = SelectAgingBuffer(writeIndex);
                GraphicsBuffer degradationWriteBuffer = SelectDegradationBuffer(degradationWriteIndex);
                if (!TryUploadAgingSnapshot(vault, writeBuffer, uploadCount, out int agingUploaded))
                    return;
                if (!TryUploadDegradationSnapshot(vault, degradationWriteBuffer, agingUploaded, out int degradationUploaded))
                    return;

                uploadCount = math.min(agingUploaded, degradationUploaded);
                if (uploadCount <= 0)
                    return;
                _readBufferIndex = writeIndex;
                _degradationReadBufferIndex = degradationWriteIndex;
                _uploadedCount = uploadCount;
                _degradationUploadedCount = uploadCount;
                _agingDirty = false;
                _degradationDirty = false;
                readBuffer = writeBuffer;
                degradationReadBuffer = degradationWriteBuffer;
            }
            else if (!_hasGeneratedPayload)
            {
                _uploadedCount = 0;
                _degradationUploadedCount = 0;
            }

            float rawUploadUs = ElapsedMicroseconds(start);
            bool uploadTimingFinite = math.isfinite(rawUploadUs);
            float uploadUs = uploadTimingFinite ? math.max(0.0f, rawUploadUs) : 0.0f;
            bool layoutValid = ValidateLayout();
            float targetPayloadBlend = uploadCount > 0 && readBuffer != null && degradationReadBuffer != null ? 1.0f : 0.0f;
            float payloadBlend01 = math.saturate(math.lerp(_payloadBlend01, targetPayloadBlend, math.lerp(0.25f, 0.85f, quality)));
            uint currentFaultFlags = 0u;
            if (!uploadTimingFinite)
                currentFaultFlags |= FlagNonFinite;
            if (uploadUs > UploadFaultMicroseconds)
                currentFaultFlags |= FlagUploadFault;
            if (!layoutValid)
                currentFaultFlags |= FlagLayoutFault;

            _payloadBlend01 = payloadBlend01;
            _publishedUploadMicroseconds = uploadUs;
            _publishedFlags = _runtimeFlags | currentFaultFlags;
            Shader.SetGlobalBuffer(AgingParamsId, readBuffer);
            Shader.SetGlobalBuffer(DegradationBufferId, degradationReadBuffer);
            Vector4 runtimeVector = default;
            runtimeVector.x = uploadCount;
            runtimeVector.y = _payloadBlend01;
            runtimeVector.z = quality;
            runtimeVector.w = (float)_runtimeFlags;
            Shader.SetGlobalVector(AgingRuntimeId, runtimeVector);
            Shader.SetGlobalVector(DegradationRuntimeId, runtimeVector);

            if ((currentFaultFlags & (FlagUploadFault | FlagLayoutFault | FlagNonFinite)) != 0u &&
                (!_dumpedFault || !_dumpedDegradationFault))
            {
                Span<byte> telemetryDumpScratch = stackalloc byte[TelemetryDumpSnapshotBytes];
                int telemetryDumpBytes = CopyTelemetryDumpSnapshot(vault, telemetryDumpScratch);
                if (telemetryDumpBytes > 0)
                {
                    if (!_dumpedFault &&
                        TryWriteTelemetryDumpSnapshot(telemetryDumpScratch, telemetryDumpBytes, _dumpStream, ref _dumpWriteFailureLogged))
                        _dumpedFault = true;

                    if (!_dumpedDegradationFault &&
                        TryWriteTelemetryDumpSnapshot(telemetryDumpScratch, telemetryDumpBytes, _degradationDumpStream, ref _degradationDumpWriteFailureLogged))
                        _dumpedDegradationFault = true;
                }
            }
        }

        private bool TryInitializeVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            _vaultInitialized =
                EnsureVaultBufferForInit(vault, ref _paramsHandle, BufferID.VisualPressureAgingParams, Capacity, NativeArrayOptions.UninitializedMemory, out NativeArray<VisualAgingParamsDTO> output) &&
                EnsureVaultBufferForInit(vault, ref _degradationHandle, BufferID.UberNoirInstanceDegradation, Capacity, NativeArrayOptions.UninitializedMemory, out NativeArray<InstanceDegradationDTO> degradation) &&
                EnsureVaultBufferForInit(vault, ref _runtimeHandle, BufferID.VisualPressureAgingRuntime, 1, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingRuntimeDTO> runtime) &&
                EnsureVaultBufferForInit(vault, ref _telemetryHandle, BufferID.VisualPressureAgingTelemetryRing, TelemetryFrameCount, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingTelemetryEntry> telemetry) &&
                EnsureVaultBufferForInit(vault, ref _telemetryCursorHandle, BufferID.VisualPressureAgingTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out NativeArray<int> telemetryCursor) &&
                EnsureVaultBufferForInit(vault, ref _degradationTelemetryHandle, BufferID.UberNoirDegradationTelemetryRing, TelemetryFrameCount, NativeArrayOptions.ClearMemory, out NativeArray<DegradationTelemetryEntry> degradationTelemetry) &&
                EnsureVaultBufferForInit(vault, ref _degradationTelemetryCursorHandle, BufferID.UberNoirDegradationTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out NativeArray<int> degradationTelemetryCursor) &&
                EnsureVaultBufferForInit(vault, ref _tuningHandle, BufferID.VisualPressureAgingTuning, 1, NativeArrayOptions.ClearMemory, out NativeArray<VisualAgingTuningDTO> tuning) &&
                EnsureVaultBufferForInit(vault, ref _csvScratchHandle, BufferID.VisualPressureAgingCsvScratch, CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out NativeArray<byte> csvScratch) &&
                EnsureVaultBufferForInit(vault, ref _mockTemperatureHandle, BufferID.VisualPressureAgingMockTemperature, 1, NativeArrayOptions.ClearMemory, out NativeArray<float> mockTemperature) &&
                output.IsCreated &&
                degradation.IsCreated &&
                runtime.IsCreated &&
                telemetry.IsCreated &&
                telemetryCursor.IsCreated &&
                degradationTelemetry.IsCreated &&
                degradationTelemetryCursor.IsCreated &&
                tuning.IsCreated &&
                csvScratch.IsCreated &&
                mockTemperature.IsCreated;

            if (!_vaultInitialized)
                return false;

            RefreshExternalInputHandles(vault);
            RefreshGlobalQualitySnapshot(vault);
            if (!_defaultsInitialized || !ValidateLayout())
            {
                if (!WriteDefaults(vault))
                    return false;
            }

            return true;
        }

        private bool HasCurrentCsvReloadState(IDataVault vault)
        {
            return HasCurrentOwnedCoreState(vault) &&
                IsCurrentOwnedBuffer(vault, in _csvScratchHandle, BufferID.VisualPressureAgingCsvScratch, CsvScratchBytes, out NativeArray<byte> _);
        }

        private bool HasCurrentOwnedCoreState(IDataVault vault)
        {
            return vault != null &&
                IsCurrentOwnedBuffer(vault, in _paramsHandle, BufferID.VisualPressureAgingParams, Capacity, out NativeArray<VisualAgingParamsDTO> _) &&
                IsCurrentOwnedBuffer(vault, in _degradationHandle, BufferID.UberNoirInstanceDegradation, Capacity, out NativeArray<InstanceDegradationDTO> _) &&
                IsCurrentOwnedBuffer(vault, in _runtimeHandle, BufferID.VisualPressureAgingRuntime, 1, out NativeArray<VisualAgingRuntimeDTO> _) &&
                IsCurrentOwnedBuffer(vault, in _telemetryHandle, BufferID.VisualPressureAgingTelemetryRing, TelemetryFrameCount, out NativeArray<VisualAgingTelemetryEntry> _) &&
                IsCurrentOwnedBuffer(vault, in _telemetryCursorHandle, BufferID.VisualPressureAgingTelemetryCursor, 1, out NativeArray<int> _) &&
                IsCurrentOwnedBuffer(vault, in _degradationTelemetryHandle, BufferID.UberNoirDegradationTelemetryRing, TelemetryFrameCount, out NativeArray<DegradationTelemetryEntry> _) &&
                IsCurrentOwnedBuffer(vault, in _degradationTelemetryCursorHandle, BufferID.UberNoirDegradationTelemetryCursor, 1, out NativeArray<int> _) &&
                IsCurrentOwnedBuffer(vault, in _tuningHandle, BufferID.VisualPressureAgingTuning, 1, out NativeArray<VisualAgingTuningDTO> _);
        }

        private static bool IsCurrentOwnedBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return requiredLength > 0 &&
                IsHandleForBuffer(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private static bool EnsureVaultBufferForInit<T>(
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

            if (IsHandleForBuffer(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                IsHandleForBuffer(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsHandleForBuffer(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private bool WriteDefaults(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                if (!locked)
                    return false;

                vault.TryResolveHandle(in _paramsHandle, out NativeArray<VisualAgingParamsDTO> output);
                if (!output.IsCreated || output.Length == 0)
                    return false;

                output[0] = default;
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            }

            locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
                if (!locked)
                    return false;

                vault.TryResolveHandle(in _degradationHandle, out NativeArray<InstanceDegradationDTO> degradation);
                if (!degradation.IsCreated || degradation.Length == 0)
                    return false;

                degradation[0] = default;
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
            }

            locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                if (!locked)
                    return false;

                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                tuning[0] = s_pendingEditorTuning = DefaultTuning(_csvGeneration);
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }

            locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
                if (!locked)
                    return false;

                vault.TryResolveHandle(in _mockTemperatureHandle, out NativeArray<float> mockTemperature);
                if (!mockTemperature.IsCreated || mockTemperature.Length == 0)
                    return false;

                mockTemperature[0] = 42.0f;
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
            }

            locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
                if (!locked)
                    return false;

                vault.TryResolveHandle(in _runtimeHandle, out NativeArray<VisualAgingRuntimeDTO> runtime);
                if (!runtime.IsCreated || runtime.Length == 0)
                    return false;

                VisualAgingRuntimeDTO runtimeDefault = default;
                runtimeDefault.Flags = FlagMockSource | FlagNoRollbackState;
                runtimeDefault.LayoutHash = ResolveLayoutHash();
                runtimeDefault.GlobalQualityWeight = ResolveGlobalQualityWeight();
                runtimeDefault.CsvGeneration = _csvGeneration;
                runtime[0] = runtimeDefault;
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            }

            _runtimeFlags = FlagMockSource | FlagNoRollbackState;
            _publishedFlags = _runtimeFlags;
            _publishedUploadMicroseconds = 0.0f;
            _activeCount = 0;
            _uploadedCount = 0;
            _degradationUploadedCount = 0;
            _payloadBlend01 = 0.0f;
            _hasGeneratedPayload = false;
            _agingDirty = true;
            _degradationDirty = true;
            _defaultsInitialized = true;
            return true;
        }

        private bool BindLockedJobBuffersForSchedule(
            IDataVault vault,
            out NativeArray<VisualAgingParamsDTO> output,
            out NativeArray<InstanceDegradationDTO> degradation,
            out NativeArray<VisualAgingRuntimeDTO> runtime,
            out NativeArray<VisualAgingTuningDTO> tuning,
            out NativeArray<VisualAgingTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<DegradationTelemetryEntry> degradationTelemetry,
            out NativeArray<int> degradationTelemetryCursor)
        {
            output = default;
            degradation = default;
            runtime = default;
            tuning = default;
            telemetry = default;
            telemetryCursor = default;
            degradationTelemetry = default;
            degradationTelemetryCursor = default;
            return vault != null &&
                vault.TryResolveHandle(in _paramsHandle, out output) &&
                vault.TryResolveHandle(in _degradationHandle, out degradation) &&
                vault.TryResolveHandle(in _runtimeHandle, out runtime) &&
                vault.TryResolveHandle(in _tuningHandle, out tuning) &&
                vault.TryResolveHandle(in _telemetryHandle, out telemetry) &&
                vault.TryResolveHandle(in _telemetryCursorHandle, out telemetryCursor) &&
                vault.TryResolveHandle(in _degradationTelemetryHandle, out degradationTelemetry) &&
                vault.TryResolveHandle(in _degradationTelemetryCursorHandle, out degradationTelemetryCursor) &&
                output.IsCreated &&
                degradation.IsCreated &&
                runtime.IsCreated &&
                tuning.IsCreated &&
                telemetry.IsCreated &&
                telemetryCursor.IsCreated &&
                degradationTelemetry.IsCreated &&
                degradationTelemetryCursor.IsCreated &&
                output.Length > 0 &&
                degradation.Length > 0 &&
                runtime.Length > 0 &&
                tuning.Length > 0 &&
                telemetry.Length > 0 &&
                telemetryCursor.Length > 0 &&
                degradationTelemetry.Length > 0 &&
                degradationTelemetryCursor.Length > 0;
        }

        private bool AcquireStructuralInputsForSchedule(
            IDataVault vault,
            out NativeArray<IntegrityStateDTO> states,
            out NativeArray<double3> nodeAups)
        {
            states = default;
            nodeAups = default;
            if (!TryLockStructuralInputs(vault))
            {
                return false;
            }

            bool resolved =
                IsCurrentExternalBuffer(vault, in _structuralStatesHandle, BufferID.StructuralIntegrityStates, 1, out states) &&
                IsCurrentExternalBuffer(vault, in _structuralNodeAupsHandle, BufferID.StructuralIntegrityNodeAups, 1, out nodeAups);
            if (!resolved)
            {
                UnlockOptional(vault, BufferID.StructuralIntegrityNodeAups, 1 << 9);
                UnlockOptional(vault, BufferID.StructuralIntegrityStates, 1 << 8);
            }

            return resolved;
        }

        private bool AcquireStructuralTuningForSchedule(IDataVault vault, out NativeArray<StructuralTuningDTO> structuralTuning)
        {
            structuralTuning = default;
            if (!TryLockOptional(vault, BufferID.StructuralIntegrityTuning, 1 << 10))
            {
                return false;
            }

            bool resolved = IsCurrentExternalBuffer(vault, in _structuralTuningHandle, BufferID.StructuralIntegrityTuning, 1, out structuralTuning);
            if (!resolved)
                UnlockOptional(vault, BufferID.StructuralIntegrityTuning, 1 << 10);

            return resolved;
        }

        private bool ApplyPendingEditorTuningImmediate()
        {
            if (!s_hasPendingEditorTuning)
                return true;

            IDataVault vault = ResolveVault();
            if (vault == null || !HasCurrentOwnedCoreState(vault))
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
                _degradationDirty = true;
                _publishedFlags = _runtimeFlags;
                s_hasPendingEditorTuning = false;
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }
        }

        private bool AcquireThermalInputForSchedule(IDataVault vault, out NativeArray<float> temperatures)
        {
            temperatures = default;
            if (!TryLockOptional(vault, BufferID.ThermodynamicsTemperatureFrontMirror, 1 << 7))
                return false;

            bool resolved = IsCurrentExternalBuffer(vault, in _thermalFrontMirrorHandle, BufferID.ThermodynamicsTemperatureFrontMirror, 1, out temperatures);
            if (!resolved)
            {
                UnlockOptional(vault, BufferID.ThermodynamicsTemperatureFrontMirror, 1 << 7);
                return false;
            }

            _runtimeFlags |= FlagThermalSource;
            return true;
        }

        private bool TryLockJobBuffers(IDataVault vault, bool tryLockMockTemperature, out bool mockTemperatureLocked)
        {
            mockTemperatureLocked = false;
            if (!TryLock(vault, BufferID.VisualPressureAgingParams, 1 << 0)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingRuntime, 1 << 1)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTelemetryRing, 1 << 2)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTelemetryCursor, 1 << 3)) return false;
            if (!TryLock(vault, BufferID.VisualPressureAgingTuning, 1 << 4)) return false;
            if (tryLockMockTemperature)
                mockTemperatureLocked = TryLockOptional(vault, BufferID.VisualPressureAgingMockTemperature, 1 << 5);
            if (!TryLock(vault, BufferID.UberNoirInstanceDegradation, 1 << 6)) return false;
            if (!TryLock(vault, BufferID.UberNoirDegradationTelemetryRing, 1 << 11)) return false;
            if (!TryLock(vault, BufferID.UberNoirDegradationTelemetryCursor, 1 << 12)) return false;
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

            if ((_lockedBufferMask & (1 << 12)) != 0) vault.TryUnlockBuffer(BufferID.UberNoirDegradationTelemetryCursor, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 11)) != 0) vault.TryUnlockBuffer(BufferID.UberNoirDegradationTelemetryRing, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 6)) != 0) vault.TryUnlockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 5)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingMockTemperature, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 4)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 3)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryCursor, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 2)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingTelemetryRing, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 1)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 0)) != 0) vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 10)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityTuning, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 9)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityNodeAups, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 8)) != 0) vault.TryUnlockBuffer(BufferID.StructuralIntegrityStates, OwnerSystemId);
            if ((_lockedBufferMask & (1 << 7)) != 0) vault.TryUnlockBuffer(BufferID.ThermodynamicsTemperatureFrontMirror, OwnerSystemId);
            _lockedBufferMask = 0;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultGenerationHandle(vault, ref _mockTemperatureHandle);
                ReleaseVaultGenerationHandle(vault, ref _csvScratchHandle);
                ReleaseVaultGenerationHandle(vault, ref _tuningHandle);
                ReleaseVaultGenerationHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultGenerationHandle(vault, ref _telemetryHandle);
                ReleaseVaultGenerationHandle(vault, ref _degradationTelemetryCursorHandle);
                ReleaseVaultGenerationHandle(vault, ref _degradationTelemetryHandle);
                ReleaseVaultGenerationHandle(vault, ref _runtimeHandle);
                ReleaseVaultGenerationHandle(vault, ref _degradationHandle);
                ReleaseVaultGenerationHandle(vault, ref _paramsHandle);
            }

            _thermalFrontMirrorHandle = default;
            _structuralTuningHandle = default;
            _structuralNodeAupsHandle = default;
            _structuralStatesHandle = default;
            _defaultsInitialized = false;
            _hasGeneratedPayload = false;
            _activeCount = 0;
            _uploadedCount = 0;
            _degradationUploadedCount = 0;
            _payloadBlend01 = 0.0f;
            _agingDirty = true;
            _degradationDirty = true;
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

        private static bool IsHandleForBuffer<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId && IsHandleValid(in handle);
        }

        private static bool IsExternalHandleValid<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                handle.SystemID != 0u &&
                handle.Generation != 0u;
        }

        private static bool IsCurrentExternalBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                requiredLength > 0 &&
                IsExternalHandleValid(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private void RefreshExternalInputHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            bool stale =
                IsExternalGenerationStale(vault, in _structuralStatesHandle, BufferID.StructuralIntegrityStates) ||
                IsExternalGenerationStale(vault, in _structuralNodeAupsHandle, BufferID.StructuralIntegrityNodeAups) ||
                IsExternalGenerationStale(vault, in _structuralTuningHandle, BufferID.StructuralIntegrityTuning) ||
                IsExternalGenerationStale(vault, in _thermalFrontMirrorHandle, BufferID.ThermodynamicsTemperatureFrontMirror);
            vault.TryGetGenerationHandle(BufferID.StructuralIntegrityStates, out _structuralStatesHandle);
            vault.TryGetGenerationHandle(BufferID.StructuralIntegrityNodeAups, out _structuralNodeAupsHandle);
            vault.TryGetGenerationHandle(BufferID.StructuralIntegrityTuning, out _structuralTuningHandle);
            vault.TryGetGenerationHandle(BufferID.ThermodynamicsTemperatureFrontMirror, out _thermalFrontMirrorHandle);
            if (stale)
                _runtimeFlags |= FlagExternalGenerationRefresh;
        }

        private static bool IsExternalGenerationStale<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return vault != null &&
                IsExternalHandleValid(in handle, bufferId) &&
                (!vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                 !buffer.IsCreated);
        }

        private bool EnsureGraphicsBuffers()
        {
            int stride = UnsafeUtility.SizeOf<VisualAgingParamsDTO>();
            int degradationStride = UnsafeUtility.SizeOf<InstanceDegradationDTO>();
            bool changedA = EnsureBuffer(ref _agingBufferA, Capacity, stride);
            bool changedB = EnsureBuffer(ref _agingBufferB, Capacity, stride);
            bool degradationChangedA = EnsureBuffer(ref _degradationBufferA, Capacity, degradationStride);
            bool degradationChangedB = EnsureBuffer(ref _degradationBufferB, Capacity, degradationStride);
            if (changedA || changedB || degradationChangedA || degradationChangedB)
            {
                _readBufferIndex = 0;
                _degradationReadBufferIndex = 0;
                _uploadedCount = 0;
                _degradationUploadedCount = 0;
                _agingDirty = true;
                _degradationDirty = true;
            }

            return _agingBufferA != null && _agingBufferB != null && _degradationBufferA != null && _degradationBufferB != null;
        }

        private bool AreGraphicsBuffersReady()
        {
            int stride = UnsafeUtility.SizeOf<VisualAgingParamsDTO>();
            int degradationStride = UnsafeUtility.SizeOf<InstanceDegradationDTO>();
            return _agingBufferA != null &&
                _agingBufferB != null &&
                _degradationBufferA != null &&
                _degradationBufferB != null &&
                _agingBufferA.count == Capacity &&
                _agingBufferB.count == Capacity &&
                _degradationBufferA.count == Capacity &&
                _degradationBufferB.count == Capacity &&
                _agingBufferA.stride == stride &&
                _agingBufferB.stride == stride &&
                _degradationBufferA.stride == degradationStride &&
                _degradationBufferB.stride == degradationStride;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);
            // COLD ALLOC: GraphicsBuffer - double-buffered LockBufferForWrite upload lane - owner: SHINOBU_219
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
            return true;
        }

        private void ReleaseGraphicsBuffers()
        {
            Shader.SetGlobalVector(AgingRuntimeId, Vector4.zero);
            Shader.SetGlobalVector(DegradationRuntimeId, Vector4.zero);
            ReleaseBuffer(ref _agingBufferA);
            ReleaseBuffer(ref _agingBufferB);
            ReleaseBuffer(ref _degradationBufferA);
            ReleaseBuffer(ref _degradationBufferB);
            _readBufferIndex = 0;
            _degradationReadBufferIndex = 0;
            _uploadedCount = 0;
            _degradationUploadedCount = 0;
            _payloadBlend01 = 0.0f;
            _publishedUploadMicroseconds = 0.0f;
            _agingDirty = true;
            _degradationDirty = true;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void EnsureDumpStreams()
        {
            if (_dumpStream == null)
                _dumpStream = OpenDumpStreamCold(_dumpPath, ref _dumpWriteFailureLogged);

            if (_degradationDumpStream == null)
                _degradationDumpStream = OpenDumpStreamCold(_degradationDumpPath, ref _degradationDumpWriteFailureLogged);
        }

        private static FileStream OpenDumpStreamCold(string path, ref bool failureLogged)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // COLD ALLOC: pre-opened black-box dump stream. Fault path writes spans only.
                return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, TelemetryDumpSnapshotBytes, FileOptions.WriteThrough);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                if (!failureLogged)
                {
#if UNITY_EDITOR
                    Hecton8.Core.H8Debug.LogError("Hecton8 VisualPressureAgingRuntime failed to open black-box dump stream.");
#endif
                    failureLogged = true;
                }

                return null;
            }
        }

        private void ReleaseDumpStreams()
        {
            ReleaseDumpStream(ref _degradationDumpStream);
            ReleaseDumpStream(ref _dumpStream);
        }

        private static void ReleaseDumpStream(ref FileStream stream)
        {
            if (stream == null)
                return;

            try
            {
                stream.Dispose();
            }
            catch (Exception exception) when (exception is IOException || exception is ObjectDisposedException)
            {
            }
            finally
            {
                stream = null;
            }
        }

        private GraphicsBuffer SelectAgingBuffer(int index)
        {
            return (index & 1) == 0 ? _agingBufferA : _agingBufferB;
        }

        private GraphicsBuffer SelectDegradationBuffer(int index)
        {
            return (index & 1) == 0 ? _degradationBufferA : _degradationBufferB;
        }

        private bool TryUploadAgingSnapshot(IDataVault vault, GraphicsBuffer destination, int count, out int uploadedCount)
        {
            uploadedCount = 0;
            if (vault == null || destination == null || count <= 0)
                return false;

            bool locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
                if (!locked)
                    return false;

                if (!vault.TryResolveHandle(in _paramsHandle, out NativeArray<VisualAgingParamsDTO> source))
                    return false;

                return TryUploadNativeArray(destination, source, count, out uploadedCount);
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);
            }
        }

        private bool TryUploadDegradationSnapshot(IDataVault vault, GraphicsBuffer destination, int count, out int uploadedCount)
        {
            uploadedCount = 0;
            if (vault == null || destination == null || count <= 0)
                return false;

            bool locked = false;
            try
            {
                locked = vault.TryLockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
                if (!locked)
                    return false;

                if (!vault.TryResolveHandle(in _degradationHandle, out NativeArray<InstanceDegradationDTO> source))
                    return false;

                return TryUploadDegradationNativeArray(destination, source, count, out uploadedCount);
            }
            finally
            {
                if (locked)
                    vault.TryUnlockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);
            }
        }

        private static bool TryUploadNativeArray(GraphicsBuffer destination, NativeArray<VisualAgingParamsDTO> source, int count, out int uploadedCount)
        {
            uploadedCount = 0;
            if (destination == null || !source.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<VisualAgingParamsDTO>())
                return false;

            NativeArray<VisualAgingParamsDTO> mapped = destination.LockBufferForWrite<VisualAgingParamsDTO>(0, safeCount);
            try
            {
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<VisualAgingParamsDTO>());
            }
            finally
            {
                destination.UnlockBufferAfterWrite<VisualAgingParamsDTO>(safeCount);
            }

            uploadedCount = safeCount;
            return true;
        }

        private static bool TryUploadDegradationNativeArray(GraphicsBuffer destination, NativeArray<InstanceDegradationDTO> source, int count, out int uploadedCount)
        {
            uploadedCount = 0;
            if (destination == null || !source.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<InstanceDegradationDTO>())
                return false;

            NativeArray<InstanceDegradationDTO> mapped = destination.LockBufferForWrite<InstanceDegradationDTO>(0, safeCount);
            try
            {
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<InstanceDegradationDTO>());
            }
            finally
            {
                destination.UnlockBufferAfterWrite<InstanceDegradationDTO>(safeCount);
            }

            uploadedCount = safeCount;
            return true;
        }

#if UNITY_EDITOR
        private bool ReloadCsvFromDisk(IDataVault vault, string csvPath, ref long lastWriteTicks, bool force)
        {
            if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            long ticks = File.GetLastWriteTimeUtc(csvPath).Ticks;
            if (!force && ticks == lastWriteTicks)
                return false;

            if (!EnsureCsvManagedScratchCold())
                return false;

            int bytesRead = ReadFileIntoScratch(csvPath, _csvManagedScratch);
            if (bytesRead <= 0)
                return false;

            VisualAgingTuningDTO dto;
            bool tuningLocked = false;
            try
            {
                tuningLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                if (!tuningLocked)
                    return false;

                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                dto = tuning[0];
            }
            finally
            {
                if (tuningLocked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }

            ReadOnlySpan<byte> csvBytes = _csvManagedScratch;
            if (!ParseAgingRulesCsv(csvBytes.Slice(0, bytesRead), ref dto))
                return false;

            dto.CsvGeneration = unchecked(dto.CsvGeneration + 1u);
            dto.RuntimeFlags |= FlagCsvLoaded;

            tuningLocked = false;
            try
            {
                tuningLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
                if (!tuningLocked)
                    return false;

                vault.TryResolveHandle(in _tuningHandle, out NativeArray<VisualAgingTuningDTO> tuning);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                tuning[0] = dto;
            }
            finally
            {
                if (tuningLocked)
                    vault.TryUnlockBuffer(BufferID.VisualPressureAgingTuning, OwnerSystemId);
            }

            s_pendingEditorTuning = dto;
            _csvGeneration = dto.CsvGeneration;
            lastWriteTicks = ticks;
            _runtimeFlags |= FlagCsvLoaded;
            _agingDirty = true;
            _degradationDirty = true;
            return true;
        }

        private bool EnsureCsvManagedScratchCold()
        {
            if (_csvManagedScratch == null || _csvManagedScratch.Length < CsvScratchBytes)
                _csvManagedScratch = new byte[CsvScratchBytes]; // COLD ALLOC: editor CSV import staging; never inside vault lock.

            return _csvManagedScratch.Length >= CsvScratchBytes;
        }

        private static int ReadFileIntoScratch(string path, byte[] scratch)
        {
            if (scratch == null)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = stream.Length;
                if (length <= 0 || length > scratch.Length)
                    return 0;

                int readLength = (int)length;
                int totalRead = 0;
                while (totalRead < readLength)
                {
                    int read = stream.Read(scratch, totalRead, readLength - totalRead);
                    if (read <= 0)
                        return 0;

                    totalRead += read;
                }

                return totalRead;
            }
        }

        private static bool ParseAgingRulesCsv(ReadOnlySpan<byte> bytes, ref VisualAgingTuningDTO tuning)
        {
            bool parsed = false;
            int index = 0;
            int byteCount = bytes.Length;
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
            if (!math.isfinite(value))
                return false;

            switch (key)
            {
                case 0x436ED2B4u: tuning.RustStressMultiplier = math.max(0.0f, value); return true;       // rust_stress
                case 0x67C02865u: tuning.CorrosionPressureMultiplier = math.max(0.0f, value); return true;// corrosion_pressure
                case 0xA173BDF3u: tuning.SaltDepthMultiplier = math.max(0.0f, value); return true;        // salt_depth
                case 0xBFF5E684u: tuning.BiomassTemperatureMultiplier = math.max(0.0f, value); return true;// biomass_temperature
                case 0xEDD0017Fu: tuning.GlassFractureThreshold = math.saturate(value); return true;      // glass_threshold
                case 0x1BC3EDBDu: tuning.TemperatureBoostMultiplier = math.max(0.0f, value); return true; // temperature_boost
                case 0x1ACB3DD7u: tuning.QualityNoiseOctaveScale = math.saturate(value); return true;     // quality_noise
                case 0xBEE9A39Bu: tuning.ScorchIntensityMultiplier = math.max(0.0f, value); return true;  // scorch_intensity
                case 0xFA06E7F3u: tuning.ScorchIntensityMultiplier = math.max(0.0f, value); return true;  // scorch_intensity_multiplier
                default: return false;
            }
        }
#endif

        private int CopyTelemetryDumpSnapshot(IDataVault vault, Span<byte> destinationBytes)
        {
            if (vault == null ||
                destinationBytes.Length < TelemetryDumpHeaderBytes ||
                string.IsNullOrEmpty(_dumpPath))
            {
                return 0;
            }

            int visualEntrySize = UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>();
            int degradationEntrySize = UnsafeUtility.SizeOf<DegradationTelemetryEntry>();

            if (!TryReadTelemetryCursor(vault, in _telemetryCursorHandle, out int telemetryCursor))
                return 0;

            if (!TryCopyTelemetryEntries(
                vault,
                in _telemetryHandle,
                telemetryCursor,
                visualEntrySize,
                destinationBytes,
                TelemetryDumpHeaderBytes,
                out int telemetryLength))
            {
                return 0;
            }

            if (!TryReadTelemetryCursor(vault, in _degradationTelemetryCursorHandle, out int degradationCursor))
                return 0;

            int degradationWriteOffset = TelemetryDumpHeaderBytes + telemetryLength * visualEntrySize;
            if (!TryCopyTelemetryEntries(
                vault,
                in _degradationTelemetryHandle,
                degradationCursor,
                degradationEntrySize,
                destinationBytes,
                degradationWriteOffset,
                out int degradationLength))
            {
                return 0;
            }

            _telemetryCursor = telemetryCursor;
            _degradationTelemetryCursor = degradationCursor;
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(0, 4), DumpMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(4, 4), DumpVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(8, 4), (uint)telemetryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(12, 4), (uint)visualEntrySize);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(16, 4), (uint)degradationLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(20, 4), (uint)degradationEntrySize);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(24, 4), _frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destinationBytes.Slice(28, 4), ResolveLayoutHash());

            return TelemetryDumpHeaderBytes + telemetryLength * visualEntrySize + degradationLength * degradationEntrySize;
        }

        private bool TryReadTelemetryCursor(
            IDataVault vault,
            in VaultGenerationHandle<int> handle,
            out int cursorValue)
        {
            cursorValue = 0;
            vault.TryResolveHandle(in handle, out NativeArray<int> cursor);
            if (!cursor.IsCreated || cursor.Length == 0)
                return false;

            cursorValue = cursor[0];
            return true;
        }

        private bool TryCopyTelemetryEntries<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int cursor,
            int entrySize,
            Span<byte> destinationBytes,
            int writeOffset,
            out int entryCount)
            where T : unmanaged
        {
            entryCount = 0;
            vault.TryResolveHandle(in handle, out NativeArray<T> entries);
            if (!entries.IsCreated || entries.Length == 0)
                return false;

            entryCount = entries.Length;
            int entryBytes = entryCount * entrySize;
            if (destinationBytes.Length < writeOffset + entryBytes)
                return false;

            int cursorStart = WrapTelemetryIndex(cursor, entryCount);
            for (int offset = 0; offset < entryCount; offset++)
            {
                int index = WrapTelemetryIndex(cursorStart + offset, entryCount);
                T entry = entries[index];
                ReadOnlySpan<byte> entryBytesSpan = new ReadOnlySpan<byte>(&entry, entrySize);
                entryBytesSpan.CopyTo(destinationBytes.Slice(writeOffset, entrySize));
                writeOffset += entrySize;
            }

            return true;
        }

        private bool TryWriteTelemetryDumpSnapshot(ReadOnlySpan<byte> snapshot, int byteCount, FileStream stream, ref bool failureLogged)
        {
            if (byteCount <= 0 || byteCount > snapshot.Length || stream == null)
                return false;

            try
            {
                if (!stream.CanWrite)
                    return false;

                ReadOnlySpan<byte> snapshotBytes = snapshot.Slice(0, byteCount);
                stream.SetLength(0L);
                stream.Position = 0L;
                stream.Write(snapshotBytes);
                stream.Flush();

                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ObjectDisposedException ||
                exception is ArgumentException)
            {
                if (!failureLogged)
                {
#if UNITY_EDITOR
                    Hecton8.Core.H8Debug.LogError("Hecton8 VisualPressureAgingRuntime failed to write black-box dump.");
#endif
                    failureLogged = true;
                }

                return false;
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

            float q = math.saturate(math.isfinite(quality) ? quality : 0.0f);
            float smooth = q * q * (3.0f - 2.0f * q);
            float capacityScale = math.lerp(0.125f, 1.0f, smooth);
            int qualityCount = math.max(1, (int)math.ceil(requested * capacityScale));
            return math.clamp(qualityCount, 1, math.min(requested, capacity));
        }

        private static bool ShouldSkipSimulationFrame(uint frame, float quality)
        {
            float keepProbability = ResolveSimulationKeepProbability(quality);
            uint hash = MixFrameHash(frame ^ SystemHash);
            float sample = (hash & 16777215u) * (1.0f / 16777215.0f);
            return sample > keepProbability;
        }

        private static float ResolveSimulationKeepProbability(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 0.0f);
            float smooth = q * q * (3.0f - 2.0f * q);
            float targetHz = math.lerp(5.0f, 60.0f, smooth);
            return math.saturate(targetHz * (1.0f / 60.0f));
        }

        private static uint MixFrameHash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private void MarkExternalGenerationRefresh(IDataVault vault)
        {
            if (vault == null)
                return;

            bool stale =
                IsExternalGenerationStale(vault, in _structuralStatesHandle, BufferID.StructuralIntegrityStates) ||
                IsExternalGenerationStale(vault, in _structuralNodeAupsHandle, BufferID.StructuralIntegrityNodeAups) ||
                IsExternalGenerationStale(vault, in _structuralTuningHandle, BufferID.StructuralIntegrityTuning) ||
                IsExternalGenerationStale(vault, in _thermalFrontMirrorHandle, BufferID.ThermodynamicsTemperatureFrontMirror);
            if (stale)
                _runtimeFlags |= FlagExternalGenerationRefresh;
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
                MockTemperatureC = 42.0f,
                ScorchIntensityMultiplier = 1.0f
            };
        }

        private static uint ResolveLayoutHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingParamsDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<InstanceDegradationDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingTuningDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingRuntimeDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<VisualAgingTelemetryEntry>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<DegradationTelemetryEntry>()) * 16777619u;
            return hash;
        }

        private float RefreshGlobalQualitySnapshot(IDataVault vault)
        {
            _cachedGlobalQualityWeight = SanitizeFloat(SignalBusRegistry.GlobalQualityWeight01, 0.0f);
            return ResolveGlobalQualityWeight();
        }

        private float ResolveGlobalQualityWeight()
        {
            return math.saturate(_cachedGlobalQualityWeight);
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float ElapsedMicroseconds(long start)
        {
            long now = Stopwatch.GetTimestamp();
            long frequency = Stopwatch.Frequency;
            if (frequency <= 0L || now < start)
                return 0.0f;

            double elapsed = (double)(now - start) * 1000000.0 / frequency;
            if (!(elapsed >= 0.0) || elapsed > 3.4028234663852886E+38)
                return 0.0f;

            return (float)elapsed;
        }

#if UNITY_EDITOR
        private static int Offset<T>(string fieldName)
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
#endif

        private static uint HashToken(ReadOnlySpan<byte> bytes, int start, int end)
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

        private static float ParseFloat(ReadOnlySpan<byte> bytes, int byteCount, ref int index, out bool ok)
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

            float result = sign * value;
            ok = digits > 0 && math.isfinite(result);
            return result;
        }

        private static int TrimTokenEnd(ReadOnlySpan<byte> bytes, int start, int end)
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

        private static void SkipWhitespaceAndLineEnds(ReadOnlySpan<byte> bytes, int byteCount, ref int index)
        {
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                    return;
                index++;
            }
        }

        private static void SkipSpaces(ReadOnlySpan<byte> bytes, int byteCount, ref int index)
        {
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    return;
                index++;
            }
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, int byteCount, ref int index)
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
    internal unsafe struct CompileDegradationParametersJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<double3> NodeAups;
        [ReadOnly, NoAlias] public NativeArray<StructuralTuningDTO> StructuralTuning;
        [ReadOnly, NoAlias] public NativeArray<float> Temperatures;
        [WriteOnly, NoAlias] public NativeArray<VisualAgingParamsDTO> Output;
        [WriteOnly, NoAlias] public NativeArray<InstanceDegradationDTO> Degradation;
        public VisualAgingTuningDTO Tuning;
        public double3 OriginAup;
        public uint Frame;
        public int ActiveCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Output.Length || (uint)index >= (uint)Degradation.Length || (uint)index >= (uint)States.Length || (uint)index >= (uint)NodeAups.Length)
                return;

            ref readonly IntegrityStateDTO state = ref UnsafeUtility.AsRef<IntegrityStateDTO>(
                (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(States) + index * UnsafeUtility.SizeOf<IntegrityStateDTO>());
            double3 nodeAup = NodeAups[index];
            double3 seaLevel = StructuralTuning.IsCreated && StructuralTuning.Length > 0 ? StructuralTuning[0].SeaLevelAup : new double3(0.0);
            float pressureScale = math.max(1.0f, FiniteOr(Tuning.PressureScaleKPa, 5500.0f));
            float depthScale = math.max(1.0f, FiniteOr(Tuning.DepthScaleMeters, 1400.0f));
            float3 local = Localize(nodeAup, OriginAup);
            double seaY = math.isfinite(seaLevel.y) ? seaLevel.y : 0.0;
            double nodeY = math.isfinite(nodeAup.y) ? nodeAup.y : seaY;
            float depthMeters = math.max(0.0f, (float)(seaY - nodeY));
            float pressure01 = math.saturate(FiniteOr(state.AppliedPressure, 0.0f) / pressureScale);
            float depth01 = math.saturate(depthMeters / depthScale);
            float stress01 = math.saturate(FiniteOr(state.CurrentStress, 0.0f));
            float buckling01 = math.saturate(FiniteOr(state.BucklingScalar, 0.0f));
            float baseWeakness01 = 1.0f - math.saturate(FiniteOr(state.BaseStrength, 1.0f));
            float temperatureC = ResolveTemperature(index);
            float temperatureBoost = math.saturate(math.max(0.0f, temperatureC - 4.0f) * FiniteOr(Tuning.TemperatureBoostMultiplier, 0.018f));
            float q = math.saturate(FiniteOr(GlobalQualityWeight, 0.0f));
            uint hash = Mix(state.NodeHash ^ ((uint)index * 747796405u) ^ Frame);
            float seed01 = (hash & 65535u) * (1.0f / 65535.0f);
            uint glassStride = math.max(1u, Tuning.GlassHashStride);
            float glassThreshold = math.saturate(FiniteOr(Tuning.GlassFractureThreshold, 0.68f));
            float glassMask = ((hash % glassStride) == 0u) ? 1.0f : 0.0f;
            float rust = math.saturate((stress01 * FiniteOr(Tuning.RustStressMultiplier, 0.78f) + depth01 * 0.35f + baseWeakness01 * 0.22f) * (1.0f + temperatureBoost));
            float corrosion = math.saturate((pressure01 * FiniteOr(Tuning.CorrosionPressureMultiplier, 0.62f) + buckling01 * 0.45f) * (0.35f + q));
            float pitting = math.saturate((stress01 + buckling01 + corrosion) * FiniteOr(Tuning.PittingStressMultiplier, 0.74f) * (0.55f + seed01 * 0.45f));
            float salt = math.saturate(depth01 * FiniteOr(Tuning.SaltDepthMultiplier, 0.58f) + pressure01 * 0.18f);
            float biomass = math.saturate(temperatureBoost * FiniteOr(Tuning.BiomassTemperatureMultiplier, 0.42f) + (1.0f - stress01) * depth01 * 0.18f);
            float scorch = math.saturate((temperatureBoost * FiniteOr(Tuning.ScorchIntensityMultiplier, 1.0f) + buckling01 * 0.18f + pressure01 * 0.08f) * (0.45f + q * 0.55f));
            float fracture = glassMask * math.saturate((stress01 + buckling01 * 0.55f - glassThreshold) * math.rcp(math.max(0.01f, 1.0f - glassThreshold)));

            Output[index] = new VisualAgingParamsDTO
            {
                RustAndCorrosion = new float4(rust, corrosion, pitting, seed01),
                SaltAndBiomass = new float4(salt, biomass, temperatureBoost, glassMask),
                StressAndMicroFractures = new float4(stress01, fracture, buckling01, q),
                DepthAndPressure = new float4(local.x, local.y, local.z, pressure01)
            };

            Degradation[index] = new InstanceDegradationDTO
            {
                InstanceID = (uint)index,
                RustAmount = rust,
                ScorchAmount = scorch,
                BioFouling = biomass,
                StructuralStress = stress01
            };
        }

        private float ResolveTemperature(int index)
        {
            float fallback = FiniteOr(Tuning.MockTemperatureC, 42.0f);
            if (!Temperatures.IsCreated || Temperatures.Length == 0)
                return fallback;

            float temperature = Temperatures[index % Temperatures.Length];
            return math.isfinite(temperature) ? temperature : fallback;
        }

        private static float3 Localize(double3 aup, double3 origin)
        {
            aup = new double3(
                math.isfinite(aup.x) ? aup.x : origin.x,
                math.isfinite(aup.y) ? aup.y : origin.y,
                math.isfinite(aup.z) ? aup.z : origin.z);
            origin = new double3(
                math.isfinite(origin.x) ? origin.x : 0.0,
                math.isfinite(origin.y) ? origin.y : 0.0,
                math.isfinite(origin.z) ? origin.z : 0.0);
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
    internal struct GenerateMockDegradationDataJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<VisualAgingParamsDTO> Output;
        [WriteOnly, NoAlias] public NativeArray<InstanceDegradationDTO> Degradation;
        [ReadOnly, NoAlias] public NativeArray<float> Temperatures;
        public VisualAgingTuningDTO Tuning;
        public double3 OriginAup;
        public uint Frame;
        public int ActiveCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Output.Length || (uint)index >= (uint)Degradation.Length)
                return;

            uint hash = Mix(((uint)index + 1u) * 2891336453u ^ Frame ^ 0x53323139u);
            float t = (hash & 1023u) * (1.0f / 1023.0f);
            float lane = ((hash >> 10) & 1023u) * (1.0f / 1023.0f);
            float depth01 = math.saturate(0.18f + lane * 0.82f);
            float stress01 = math.saturate(0.25f + t * 0.75f);
            float buckling = math.saturate(stress01 * 0.65f + Triangle(Frame * 0.0031f + t) * 0.18f);
            float temperatureC = ResolveTemperature();
            float temperatureBoost = math.saturate(math.max(0.0f, temperatureC - 4.0f) * FiniteOr(Tuning.TemperatureBoostMultiplier, 0.018f));
            float pressure01 = math.saturate(depth01 * 0.75f + stress01 * 0.25f);
            float q = math.saturate(FiniteOr(GlobalQualityWeight, 0.0f));
            float scorch = math.saturate((temperatureBoost * FiniteOr(Tuning.ScorchIntensityMultiplier, 1.0f) + buckling * 0.16f + pressure01 * 0.08f) * (0.45f + q * 0.55f));
            float seed01 = ((hash >> 20) & 1023u) * (1.0f / 1023.0f);
            uint glassStride = math.max(1u, Tuning.GlassHashStride);
            float glassThreshold = math.saturate(FiniteOr(Tuning.GlassFractureThreshold, 0.68f));
            float depthScale = math.max(1.0f, FiniteOr(Tuning.DepthScaleMeters, 1400.0f));
            float glassMask = ((hash % glassStride) == 0u) ? 1.0f : 0.0f;
            double3 mockAup = OriginAup + new double3((index & 31) * 5.0, -depth01 * depthScale, (index >> 5) * 5.0);
            float3 local = Localize(mockAup, OriginAup);
            float fracture = glassMask * math.saturate((stress01 + buckling * 0.55f - glassThreshold) * math.rcp(math.max(0.01f, 1.0f - glassThreshold)));
            float rust = math.saturate((stress01 * FiniteOr(Tuning.RustStressMultiplier, 0.78f) + depth01 * 0.32f) * (1.0f + temperatureBoost));
            float corrosion = math.saturate(pressure01 * FiniteOr(Tuning.CorrosionPressureMultiplier, 0.62f));
            float pitting = math.saturate((stress01 + buckling) * FiniteOr(Tuning.PittingStressMultiplier, 0.74f));
            float salt = math.saturate(depth01 * FiniteOr(Tuning.SaltDepthMultiplier, 0.58f));
            float biomass = math.saturate(temperatureBoost * FiniteOr(Tuning.BiomassTemperatureMultiplier, 0.42f) + depth01 * 0.12f);

            Output[index] = new VisualAgingParamsDTO
            {
                RustAndCorrosion = new float4(
                    rust,
                    corrosion,
                    pitting,
                    seed01),
                SaltAndBiomass = new float4(
                    salt,
                    biomass,
                    temperatureBoost,
                    glassMask),
                StressAndMicroFractures = new float4(stress01, fracture, buckling, q),
                DepthAndPressure = new float4(local.x, local.y, local.z, pressure01)
            };

            Degradation[index] = new InstanceDegradationDTO
            {
                InstanceID = (uint)index,
                RustAmount = rust,
                ScorchAmount = scorch,
                BioFouling = biomass,
                StructuralStress = stress01
            };
        }

        private static float3 Localize(double3 aup, double3 origin)
        {
            aup = new double3(
                math.isfinite(aup.x) ? aup.x : origin.x,
                math.isfinite(aup.y) ? aup.y : origin.y,
                math.isfinite(aup.z) ? aup.z : origin.z);
            origin = new double3(
                math.isfinite(origin.x) ? origin.x : 0.0,
                math.isfinite(origin.y) ? origin.y : 0.0,
                math.isfinite(origin.z) ? origin.z : 0.0);
            double3 delta = aup - origin;
            delta = math.clamp(delta, new double3(-8192.0), new double3(8192.0));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float Triangle(float phase)
        {
            return 1.0f - math.abs(math.frac(phase) * 2.0f - 1.0f);
        }

        private float ResolveTemperature()
        {
            float fallback = FiniteOr(Tuning.MockTemperatureC, 42.0f);
            if (!Temperatures.IsCreated || Temperatures.Length == 0)
                return fallback;

            float temperature = Temperatures[0];
            return math.isfinite(temperature) ? temperature : fallback;
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
        [ReadOnly, NoAlias] public NativeArray<InstanceDegradationDTO> Degradation;
        [NoAlias] public NativeArray<VisualAgingRuntimeDTO> Runtime;
        [NoAlias] public NativeArray<VisualAgingTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<DegradationTelemetryEntry> DegradationTelemetry;
        [NoAlias] public NativeArray<int> DegradationTelemetryCursor;
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
            if (!Output.IsCreated || !Degradation.IsCreated || !Runtime.IsCreated || Runtime.Length == 0 || !Telemetry.IsCreated || Telemetry.Length == 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0 ||
                !DegradationTelemetry.IsCreated || DegradationTelemetry.Length == 0 ||
                !DegradationTelemetryCursor.IsCreated || DegradationTelemetryCursor.Length == 0)
            {
                return;
            }

            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0.0f);
            float uploadUs = math.isfinite(GpuUploadMicroseconds) ? GpuUploadMicroseconds : 0.0f;
            int sampleCount = math.clamp(ActiveCount, 0, math.min(Output.Length, Degradation.Length));
            int sampleBudget = math.max(16, (int)math.round(math.lerp(32.0f, 256.0f, q)));
            int stride = sampleCount > sampleBudget ? math.max(1, sampleCount / sampleBudget) : 1;
            float stressMean = 0.0f;
            float fractureMean = 0.0f;
            float tempMean = 0.0f;
            float maxScorch = 0.0f;
            float maxDepth = 0.0f;
            uint activeGlassFractures = 0u;
            uint hash = 2166136261u;
            uint faults = 0u;
            int sampled = 0;
            for (int i = 0; i < sampleCount && sampled < sampleBudget; i += stride)
            {
                VisualAgingParamsDTO dto = Output[i];
                InstanceDegradationDTO degradation = Degradation[i];
                if (!math.all(math.isfinite(dto.RustAndCorrosion)) ||
                    !math.all(math.isfinite(dto.SaltAndBiomass)) ||
                    !math.all(math.isfinite(dto.StressAndMicroFractures)) ||
                    !math.all(math.isfinite(dto.DepthAndPressure)) ||
                    !math.isfinite(degradation.RustAmount) ||
                    !math.isfinite(degradation.ScorchAmount) ||
                    !math.isfinite(degradation.BioFouling) ||
                    !math.isfinite(degradation.StructuralStress))
                {
                    faults |= 1u << 30;
                    continue;
                }

                stressMean += degradation.StructuralStress;
                fractureMean += dto.StressAndMicroFractures.y;
                tempMean += dto.SaltAndBiomass.z;
                maxScorch = math.max(maxScorch, math.saturate(degradation.ScorchAmount));
                maxDepth = math.max(maxDepth, math.saturate(math.abs(dto.DepthAndPressure.y) * 0.0007142857f));
                if (dto.SaltAndBiomass.w > 0.5f && dto.StressAndMicroFractures.y > 0.01f)
                    activeGlassFractures++;
                hash = (hash ^ math.asuint(dto.RustAndCorrosion.x)) * 16777619u;
                hash = (hash ^ math.asuint(dto.DepthAndPressure.w)) * 16777619u;
                sampled++;
            }

            float inv = sampled > 0 ? math.rcp(sampled) : 0.0f;
            int cursor = WrapTelemetryIndex(TelemetryCursor[0], Telemetry.Length);
            int degradationCursor = WrapTelemetryIndex(DegradationTelemetryCursor[0], DegradationTelemetry.Length);
            uint sequence = Runtime[0].Sequence + 1u;
            float cpuEstimateUs = ActiveCount * math.lerp(0.010f, 0.026f, q);
            VisualAgingTelemetryEntry entry = new VisualAgingTelemetryEntry
            {
                Frame = Frame,
                Flags = RuntimeFlags | faults,
                StateHash = hash,
                LayoutHash = LayoutHash,
                ActiveCount = ActiveCount,
                UploadedCount = UploadedCount,
                UploadedBytes = UploadedBytes,
                GlobalQualityWeight = q,
                MaxDepth01 = maxDepth,
                AverageStress01 = stressMean * inv,
                ActiveGlassFractures = activeGlassFractures,
                MeanTemperatureBoost01 = tempMean * inv,
                CpuEstimateMicroseconds = cpuEstimateUs,
                GpuUploadMicroseconds = uploadUs,
                CsvGeneration = CsvGeneration,
                Sequence = sequence
            };

            Telemetry[cursor] = entry;
            TelemetryCursor[0] = (cursor + 1) % Telemetry.Length;
            DegradationTelemetry[degradationCursor] = new DegradationTelemetryEntry
            {
                Frame = Frame,
                Flags = RuntimeFlags | faults,
                InstancesEvaluated = ActiveCount,
                UploadedCount = UploadedCount,
                AverageBaseStress01 = stressMean * inv,
                MaxScorch01 = maxScorch,
                GpuUploadMicroseconds = uploadUs,
                GlobalQualityWeight = q,
                StateHash = hash,
                LayoutHash = LayoutHash,
                UploadedBytes = UploadedBytes,
                CsvGeneration = CsvGeneration,
                Sequence = sequence
            };
            DegradationTelemetryCursor[0] = (degradationCursor + 1) % DegradationTelemetry.Length;
            Runtime[0] = new VisualAgingRuntimeDTO
            {
                Frame = Frame,
                Flags = RuntimeFlags,
                ActiveCount = ActiveCount,
                UploadedCount = UploadedCount,
                GlobalQualityWeight = q,
                LastUploadMicroseconds = uploadUs,
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
