using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Graphics.Materials
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InstanceMaterialDTO
    {
        [FieldOffset(0)]
        public float WearAge;
        [FieldOffset(4)]
        public float SaltAccumulation;
        [FieldOffset(8)]
        public float BioGrowthMask;
        [FieldOffset(12)]
        public uint TextureSetHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MaterialPowerDTO
    {
        [FieldOffset(0)]
        public float PowerLevel;
        [FieldOffset(4)]
        public float DepthMeters;
        [FieldOffset(8)]
        public float StructuralStress01;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MaterialVisibleDTO
    {
        [FieldOffset(0)]
        public float WearAge;
        [FieldOffset(4)]
        public float SaltAccumulation;
        [FieldOffset(8)]
        public float BioGrowthMask;
        [FieldOffset(12)]
        public uint TextureSetHash;
        [FieldOffset(16)]
        public float PowerLevel;
        [FieldOffset(20)]
        public float Depth01;
        [FieldOffset(24)]
        public float MossLayer01;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct GlobalShaderConstantsDTO
    {
        [FieldOffset(0)]
        public float4 SubsurfaceColor;
        [FieldOffset(16)]
        public float4 CausticSpeed;
        [FieldOffset(32)]
        public float GlobalWearMultiplier;
        [FieldOffset(36)]
        public uint _pad0;
        [FieldOffset(40)]
        public uint _pad1;
        [FieldOffset(44)]
        public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockBiomassDensitySignal
    {
        [FieldOffset(0)]
        public float Density01;
        [FieldOffset(4)]
        public float Pulse01;
        [FieldOffset(8)]
        public uint SectorHash;
        [FieldOffset(12)]
        public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MaterialRuntimeScalarsDTO
    {
        [FieldOffset(0)]
        public float GlobalBiomass01;
        [FieldOffset(4)]
        public float GlobalQualityWeight;
        [FieldOffset(8)]
        public uint VisibleCount;
        [FieldOffset(12)]
        public uint CsvGeneration;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TextureSetMappingDTO
    {
        [FieldOffset(0)]
        public uint TextureSetHash;
        [FieldOffset(4)]
        public uint SliceIndex;
        [FieldOffset(8)]
        public uint Generation;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct WearRateDTO
    {
        [FieldOffset(0)]
        public float IronOxidationRate;
        [FieldOffset(4)]
        public float SaltDepositionRate;
        [FieldOffset(8)]
        public float MossGrowthRate;
        [FieldOffset(12)]
        public float PowerFlickerRate;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MaterialResponseTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint Flags;
        [FieldOffset(8)]
        public uint VisibleCount;
        [FieldOffset(12)]
        public uint UploadedBytes;
        [FieldOffset(16)]
        public float MaterialBufferUploadTimeMs;
        [FieldOffset(20)]
        public float ActiveTriplanarPixels;
        [FieldOffset(24)]
        public float TextureArrayMemoryMB;
        [FieldOffset(28)]
        public float GlobalQualityWeight;
        [FieldOffset(32)]
        public uint StateHash;
        [FieldOffset(36)]
        public uint CsvGeneration;
        [FieldOffset(40)]
        public uint LayoutHash;
        [FieldOffset(44)]
        public uint _pad0;
        [FieldOffset(48)]
        public float WearMean;
        [FieldOffset(52)]
        public float SaltMean;
        [FieldOffset(56)]
        public float BioMean;
        [FieldOffset(60)]
        public float PowerMean;
    }

    public sealed unsafe class ShinobuMaterialResponseRuntime : IGlobalRegistryHotSwapListener, IColdTickable
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsMaterials;
        private const uint SystemHash = 0x53483433u; // SH43
        private const int DefaultMaterialCapacity = 8192;
        private const int TextureMappingCapacity = 64;
        private const int TelemetryFrameCount = 300;
        private const int CsvScratchBytes = 8192;
        private const int JobBatchSize = 64;
        private const int CsvPollCadenceFrames = 64;
        private const int ConstantsCount = 1;
        private const int WearRateCount = 1;
        private const int ScalarCount = 1;
        private const int MockSignalCount = 1;
        private const float UploadFaultMs = 1.0f;
        private const float TextureArrayMemoryBaseMb = 96.0f;
        private const ulong DumpMagic = 0x5348494E4F425534UL; // SHINOBU4
        private const uint DumpVersion = 1u;
        private const string CsvRelativePath = "Data/Visuals/texture_set_indices.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_TECH_ART_DISPATCH.bin";

        private const uint FlagTextureArraysBound = 1u << 0;
        private const uint FlagDebugHeatmap = 1u << 1;
        private const uint FlagEmergencyMockRates = 1u << 2;
        private const uint FlagCsvLoaded = 1u << 3;
        private const uint FlagLayoutFault = 1u << 29;
        private const uint FlagNonFinite = 1u << 30;
        private const uint FlagUploadFault = 1u << 31;

        private static readonly int MaterialStatesBufferId = Shader.PropertyToID("_H8UberNoirMaterialStates");
        private static readonly int MaterialGlobalsBufferId = Shader.PropertyToID("H8UberNoirMaterialGlobals");
        private static readonly ulong JobMutationGuardMask =
            MaterialMutationGuardBit(BufferID.ShinobuMaterialStates) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialPowers) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialVisibleIndices) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialVisiblePayload) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialConstants) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialMockBiomassSignals) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialWearRates) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialBiomassScalar);

        private static readonly ulong DefaultsMutationGuardMask =
            JobMutationGuardMask |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialTextureMappings);

        private static readonly ulong ConstantsScalarsMutationGuardMask =
            MaterialMutationGuardBit(BufferID.ShinobuMaterialConstants) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialBiomassScalar);

        private static readonly ulong VisualTelemetryMutationGuardMask =
            MaterialMutationGuardBit(BufferID.ShinobuMaterialTelemetryRing) |
            MaterialMutationGuardBit(BufferID.ShinobuMaterialBiomassScalar);

        private static ShinobuMaterialResponseRuntime s_active;
        private static bool s_hasPendingEditorTuning;
        private static float s_pendingRustRate = 1.0f;
        private static float s_pendingCausticIntensity = 0.65f;
        private static float s_pendingSssTranslucency = 0.55f;
        private static float s_pendingSaltLineDepth = 0.0f;
        private static uint s_pendingDebugMode;

        private IDataVault _vault;
        private VaultGenerationHandle<InstanceMaterialDTO> _statesHandle;
        private VaultGenerationHandle<MaterialPowerDTO> _powersHandle;
        private VaultGenerationHandle<uint> _visibleIndicesHandle;
        private VaultGenerationHandle<MaterialVisibleDTO> _visiblePayloadHandle;
        private VaultGenerationHandle<GlobalShaderConstantsDTO> _constantsHandle;
        private VaultGenerationHandle<MaterialResponseTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<TextureSetMappingDTO> _mappingHandle;
        private VaultGenerationHandle<MockBiomassDensitySignal> _mockBiomassHandle;
        private VaultGenerationHandle<WearRateDTO> _wearRateHandle;
        private VaultGenerationHandle<MaterialRuntimeScalarsDTO> _scalarsHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;

        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;

        private GraphicsBuffer _materialStateBufferA;
        private GraphicsBuffer _materialStateBufferB;
        private GraphicsBuffer _materialGlobalsBufferA;
        private GraphicsBuffer _materialGlobalsBufferB;
        private string _csvPath;
        private string _dumpPath;
        private uint _lastTelemetryDumpHash;
        private JobHandle _simulationHandle;
        private long _csvLastWriteTicks;
        private IDataVault _jobBufferGuardVault;
        private int _activeVisibleCount;
        private int _lastScheduledCount;
        private int _lastUploadedVisibleCount;
        private int _materialStateReadIndex;
        private int _materialGlobalsReadIndex;
        private int _telemetryCursor;
        private uint _lastDispatcherFrame;
        private uint _csvGeneration = 1u;
        private uint _runtimeFlags = FlagEmergencyMockRates;
        private uint _lastStateHash;
        private int _materialCapacity = DefaultMaterialCapacity;
        private float _publishedShaderQualityWeight = 1.0f;
        private bool _visiblePayloadDirty = true;
        private bool _constantsDirty = true;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredColdTick;
        private bool _registeredHotSwapListener;
        private bool _vaultInitialized;
        private bool _defaultsInitialized;
        private bool _jobBufferGuardHeld;
        private bool _simulationScheduled;
        private bool _dumpedUploadFault;
        private bool _shutdown;
        private bool _vaultRepairRequested;

        public static bool IsActive
        {
            get { return s_active != null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActive();
            s_active = null;
            s_hasPendingEditorTuning = false;
            s_pendingRustRate = 1.0f;
            s_pendingCausticIntensity = 0.65f;
            s_pendingSssTranslucency = 0.55f;
            s_pendingSaltLineDepth = 0.0f;
            s_pendingDebugMode = 0u;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            // COLD ALLOC: ShinobuMaterialResponseRuntime[1] - dispatcher-owned material response service - owner: SHINOBU_43
            ShinobuMaterialResponseRuntime runtime = new ShinobuMaterialResponseRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private static void ShutdownActive()
        {
            ShinobuMaterialResponseRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

        public static bool TryWriteEditorTuning(
            float globalRustRate,
            float causticIntensity,
            float sssTranslucency,
            float saltLineDepth,
            uint debugMode)
        {
            s_pendingRustRate = math.max(0.0f, SanitizeFloat(globalRustRate, 1.0f));
            s_pendingCausticIntensity = math.saturate(SanitizeFloat(causticIntensity, 0.65f));
            s_pendingSssTranslucency = math.saturate(SanitizeFloat(sssTranslucency, 0.55f));
            s_pendingSaltLineDepth = SanitizeFloat(saltLineDepth, 0.0f);
            s_pendingDebugMode = debugMode;
            s_hasPendingEditorTuning = true;

            ShinobuMaterialResponseRuntime active = s_active;
            if (active == null)
                return false;

            return active.ApplyPendingEditorTuningImmediate();
        }

        public static bool TryReadEditorTuning(
            out float globalRustRate,
            out float causticIntensity,
            out float sssTranslucency,
            out float saltLineDepth,
            out uint debugMode,
            out int visibleCount,
            out float lastUploadMs)
        {
            globalRustRate = s_pendingRustRate;
            causticIntensity = s_pendingCausticIntensity;
            sssTranslucency = s_pendingSssTranslucency;
            saltLineDepth = s_pendingSaltLineDepth;
            debugMode = s_pendingDebugMode;
            visibleCount = 0;
            lastUploadMs = 0.0f;

            ShinobuMaterialResponseRuntime active = s_active;
            if (active == null)
                return false;

            visibleCount = active._activeVisibleCount;
            IDataVault vault = active.ResolveVault();
            if (vault == null || !active.HasVaultStateReady(vault))
                return false;

            active.TryOpenVaultBuffer(vault, ref active._constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out NativeArray<GlobalShaderConstantsDTO> constants);
            if (constants.IsCreated && constants.Length > 0)
            {
                GlobalShaderConstantsDTO dto = constants[0];
                globalRustRate = dto.GlobalWearMultiplier;
                causticIntensity = dto.CausticSpeed.y;
                sssTranslucency = dto.SubsurfaceColor.w;
                saltLineDepth = dto.CausticSpeed.z;
                debugMode = dto._pad0;
            }

            active.TryOpenVaultBuffer(vault, ref active._telemetryHandle, BufferID.ShinobuMaterialTelemetryRing, TelemetryFrameCount, out NativeArray<MaterialResponseTelemetryEntry> telemetry);
            if (telemetry.IsCreated && telemetry.Length > 0)
            {
                int index = active._telemetryCursor - 1;
                if (index < 0)
                    index += TelemetryFrameCount;
                MaterialResponseTelemetryEntry entry = telemetry[index % telemetry.Length];
                lastUploadMs = entry.MaterialBufferUploadTimeMs;
            }

            return true;
        }

        private ShinobuMaterialResponseRuntime()
        {
            _materialCapacity = DefaultMaterialCapacity;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _dumpPath = DumpRelativePath;

            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into GlobalRegistry dispatcher - owner: SHINOBU_43
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        private void Initialize()
        {
            _shutdown = false;
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault, null);
            TryRegisterHotSwapListener();
            PrepareRuntimeStateCold();
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
            CompleteSimulationForLifecycle(_vault);
            UnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            ReleaseGraphicsBuffers();
            ReleaseVaultHandles(_vault);
            ResetVaultHandles();
            _vault = null;
            _vaultInitialized = false;
            _simulationScheduled = false;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (!Application.isPlaying || _shutdown || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
            if (!_registeredColdTick && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
                if (!_shutdown)
                    PrepareRuntimeStateCold();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterDispatcherPhases();
                if (!_shutdown && currentService != null)
                    RegisterDispatcherPhases();
            }
        }

        public void ColdTick()
        {
            if (_shutdown)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null)
                return;

            if (!_vaultRepairRequested && HasVaultStateReady(vault) && AreGraphicsBuffersReady())
                return;

            if (_simulationScheduled)
                return;

            PrepareRuntimeStateCold();
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault releaseVaultFallback)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            IDataVault releaseVault = _vault ?? releaseVaultFallback;
            CompleteSimulationForLifecycle(releaseVault);
            ReleaseVaultHandles(releaseVault);
            ResetVaultHandles();
            _vault = nextVault;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _activeVisibleCount = 0;
            _lastScheduledCount = 0;
            _lastUploadedVisibleCount = 0;
            _telemetryCursor = 0;
            _lastDispatcherFrame = 0u;
            _lastStateHash = 0u;
            _runtimeFlags = FlagEmergencyMockRates;
            _csvLastWriteTicks = 0L;
            _visiblePayloadDirty = true;
            _constantsDirty = true;
            _dumpedUploadFault = false;
            _vaultRepairRequested = true;
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

        private IDataVault ResolveVault()
        {
            return _vault;
        }

        private bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = default;
            buffer = default;
            return false;
        }

        private bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static ulong MaterialMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private void ResetVaultHandles()
        {
            _statesHandle = default;
            _powersHandle = default;
            _visibleIndicesHandle = default;
            _visiblePayloadHandle = default;
            _constantsHandle = default;
            _telemetryHandle = default;
            _mappingHandle = default;
            _mockBiomassHandle = default;
            _wearRateHandle = default;
            _scalarsHandle = default;
            _csvScratchHandle = default;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _powersHandle);
            ReleaseVaultHandle(vault, ref _visibleIndicesHandle);
            ReleaseVaultHandle(vault, ref _visiblePayloadHandle);
            ReleaseVaultHandle(vault, ref _constantsHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _mappingHandle);
            ReleaseVaultHandle(vault, ref _mockBiomassHandle);
            ReleaseVaultHandle(vault, ref _wearRateHandle);
            ReleaseVaultHandle(vault, ref _scalarsHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)OwnerSystemId)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault))
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!vault.TryAcquireMutationGuard(ConstantsScalarsMutationGuardMask))
                return;
            try
            {
                ApplyQualityAndEditorTuning(vault, SanitizeDelta(timing.FrameDelta));
            }
            finally
            {
                vault.ReleaseMutationGuard(ConstantsScalarsMutationGuardMask);
            }
#if UNITY_EDITOR
            uint frame = unchecked(_lastDispatcherFrame + 1u);
            _lastDispatcherFrame = frame;
            if ((frame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorTextureCsv(vault);
#endif
        }

        private JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (_simulationScheduled)
                return dependsOn;

            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault))
            {
                _vaultRepairRequested = true;
                return dependsOn;
            }

            _lastDispatcherFrame = context.Frame;
            if (!TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars))
                return dependsOn;

            float quality = math.saturate(scalars[0].GlobalQualityWeight);
            int cadence = ResolveUpdateCadence(quality);
            if (cadence > 1 && (context.Frame % (uint)cadence) != 0u)
                return dependsOn;

            if (!TryPinJobBuffers(vault))
                return dependsOn;

            bool scheduled = false;
            try
            {
                if (!TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out scalars))
                    return dependsOn;

                quality = math.saturate(scalars[0].GlobalQualityWeight);
                if (!TryResolveSimulationBuffers(
                        vault,
                        out NativeArray<InstanceMaterialDTO> states,
                        out NativeArray<MaterialPowerDTO> powers,
                        out NativeArray<uint> visibleIndices,
                        out NativeArray<MaterialVisibleDTO> visiblePayload,
                        out NativeArray<GlobalShaderConstantsDTO> constants,
                        out NativeArray<MockBiomassDensitySignal> biomassSignals,
                        out NativeArray<WearRateDTO> wearRates))
                {
                    return dependsOn;
                }

                int sourceCount = math.min(states.Length, powers.Length);
                int requestedVisibleCount = (int)math.min(scalars[0].VisibleCount, (uint)math.min(visibleIndices.Length, visiblePayload.Length));
                int visibleCount = math.clamp(requestedVisibleCount <= 0 ? sourceCount : requestedVisibleCount, 1, math.min(sourceCount, visiblePayload.Length));
                int simulationCount = math.clamp(ResolveSimulationBudget(sourceCount, quality), 1, sourceCount);
                _lastScheduledCount = simulationCount;
                _activeVisibleCount = visibleCount;

                JobHandle handle = new MockBiomassScalarJob
                {
                    Signals = biomassSignals,
                    Scalars = scalars,
                    Frame = context.Frame,
                    SectorHash = SystemHash,
                    GlobalQualityWeight = quality
                }.Schedule(dependsOn);

                handle = new MaterialWearUpdateJob
                {
                    States = states,
                    Powers = powers,
                    WearRates = wearRates,
                    Scalars = scalars,
                    Constants = constants,
                    Frame = context.Frame,
                    DeltaSeconds = SanitizeDelta(timing.FixedDelta > 0.0f ? timing.FixedDelta : timing.FrameDelta),
                    GlobalQualityWeight = quality
                }.Schedule(simulationCount, JobBatchSize, handle);

                handle = new VisibleMaterialPackJob
                {
                    States = states,
                    Powers = powers,
                    VisibleIndices = visibleIndices,
                    VisiblePayload = visiblePayload,
                    Scalars = scalars,
                    GlobalQualityWeight = quality,
                    VisibleCount = visibleCount
                }.Schedule(visibleCount, JobBatchSize, handle);

                _simulationScheduled = true;
                _simulationHandle = handle;
                _visiblePayloadDirty = true;
                H8Memory.RegisterActiveJob(OwnerSystemId, handle);
                scheduled = true;
                return handle;
            }
            finally
            {
                if (!scheduled)
                    UnlockJobBuffers();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                    return;

                UnlockJobBuffers();
                _simulationScheduled = false;
            }
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault) || !AreGraphicsBuffersReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!TryOpenVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, math.max(1, _materialCapacity), out NativeArray<MaterialVisibleDTO> visiblePayload) ||
                !TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out NativeArray<GlobalShaderConstantsDTO> constants) ||
                !TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars) ||
                !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMaterialTelemetryRing, TelemetryFrameCount, out NativeArray<MaterialResponseTelemetryEntry> telemetry))
            {
                return;
            }

            GraphicsBuffer materialReadBuffer = SelectMaterialStateBuffer(_materialStateReadIndex);
            GraphicsBuffer globalsReadBuffer = SelectMaterialGlobalsBuffer(_materialGlobalsReadIndex);
            if (materialReadBuffer == null || globalsReadBuffer == null)
                return;

            int uploadCount = math.clamp(_activeVisibleCount <= 0 ? 1 : _activeVisibleCount, 1, math.min(visiblePayload.Length, materialReadBuffer.count));
            long start = Stopwatch.GetTimestamp();
            if (_visiblePayloadDirty || uploadCount != _lastUploadedVisibleCount)
            {
                int writeIndex = _materialStateReadIndex ^ 1;
                GraphicsBuffer materialWriteBuffer = SelectMaterialStateBuffer(writeIndex);
                UploadNativeArray(materialWriteBuffer, visiblePayload, uploadCount);
                _materialStateReadIndex = writeIndex;
                _lastUploadedVisibleCount = uploadCount;
                _visiblePayloadDirty = false;
                materialReadBuffer = materialWriteBuffer;
            }

            if (_constantsDirty)
            {
                int writeIndex = _materialGlobalsReadIndex ^ 1;
                GraphicsBuffer globalsWriteBuffer = SelectMaterialGlobalsBuffer(writeIndex);
                UploadConstants(globalsWriteBuffer, constants[0]);
                _materialGlobalsReadIndex = writeIndex;
                _constantsDirty = false;
                globalsReadBuffer = globalsWriteBuffer;
            }

            Shader.SetGlobalBuffer(MaterialStatesBufferId, materialReadBuffer);
            Shader.SetGlobalConstantBuffer(MaterialGlobalsBufferId, globalsReadBuffer, 0, UnsafeUtility.SizeOf<GlobalShaderConstantsDTO>());
            float uploadMs = ElapsedMs(start);

            uint flags = _runtimeFlags;
            if (uploadMs > UploadFaultMs)
                flags |= FlagUploadFault;
            if (!IsFinite(uploadMs) || !IsLayoutValid())
                flags |= FlagLayoutFault | FlagNonFinite;

            bool dumpRequested = false;
            if (vault.TryAcquireMutationGuard(VisualTelemetryMutationGuardMask))
            {
                try
                {
                    MaterialRuntimeScalarsDTO scalar = scalars[0];
                    scalar.VisibleCount = (uint)uploadCount;
                    scalars[0] = scalar;
                    RecordTelemetry(vault, telemetry, uploadCount, uploadMs, flags);
                    dumpRequested = (flags & (FlagUploadFault | FlagLayoutFault | FlagNonFinite)) != 0u && !_dumpedUploadFault;
                }
                finally
                {
                    vault.ReleaseMutationGuard(VisualTelemetryMutationGuardMask);
                }
            }

            if (dumpRequested)
            {
                DumpTelemetry(vault, telemetry);
                _dumpedUploadFault = true;
            }
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            int capacity = math.max(1, _materialCapacity);
            _vaultInitialized = OpenOrAcquireVaultBuffer(vault, ref _statesHandle, BufferID.ShinobuMaterialStates, capacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _powersHandle, BufferID.ShinobuMaterialPowers, capacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _visibleIndicesHandle, BufferID.ShinobuMaterialVisibleIndices, capacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, capacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMaterialTelemetryRing, TelemetryFrameCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _mappingHandle, BufferID.ShinobuMaterialTextureMappings, TextureMappingCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _mockBiomassHandle, BufferID.ShinobuMaterialMockBiomassSignals, MockSignalCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _wearRateHandle, BufferID.ShinobuMaterialWearRates, WearRateCount, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(vault, ref _csvScratchHandle, BufferID.ShinobuMaterialCsvScratch, CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _);

            if (!_vaultInitialized)
                return false;

            if (!_defaultsInitialized || !IsLayoutValid())
                GenerateEmergencyMockWearRates(vault);

            return true;
        }

        private void PrepareRuntimeStateCold()
        {
            IDataVault vault = ResolveVault();
            bool vaultReady = vault != null && EnsureVaultState(vault);
            bool graphicsReady = EnsureGraphicsBuffers();
            _vaultRepairRequested = !vaultReady || !graphicsReady;
        }

        private bool HasVaultStateReady(IDataVault vault)
        {
            if (vault == null)
                return false;

            int capacity = math.max(1, _materialCapacity);
            return TryOpenVaultBuffer(vault, ref _statesHandle, BufferID.ShinobuMaterialStates, capacity, out _) &&
                TryOpenVaultBuffer(vault, ref _powersHandle, BufferID.ShinobuMaterialPowers, capacity, out _) &&
                TryOpenVaultBuffer(vault, ref _visibleIndicesHandle, BufferID.ShinobuMaterialVisibleIndices, capacity, out _) &&
                TryOpenVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, capacity, out _) &&
                TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out _) &&
                TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMaterialTelemetryRing, TelemetryFrameCount, out _) &&
                TryOpenVaultBuffer(vault, ref _mappingHandle, BufferID.ShinobuMaterialTextureMappings, TextureMappingCapacity, out _) &&
                TryOpenVaultBuffer(vault, ref _mockBiomassHandle, BufferID.ShinobuMaterialMockBiomassSignals, MockSignalCount, out _) &&
                TryOpenVaultBuffer(vault, ref _wearRateHandle, BufferID.ShinobuMaterialWearRates, WearRateCount, out _) &&
                TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out _) &&
                TryOpenVaultBuffer(vault, ref _csvScratchHandle, BufferID.ShinobuMaterialCsvScratch, CsvScratchBytes, out _);
        }

        private void GenerateEmergencyMockWearRates(IDataVault vault)
        {
            int capacity = math.max(1, _materialCapacity);
            if (vault == null || !vault.TryAcquireMutationGuard(DefaultsMutationGuardMask))
                return;

            try
            {
                if (!TryOpenVaultBuffer(vault, ref _statesHandle, BufferID.ShinobuMaterialStates, capacity, out NativeArray<InstanceMaterialDTO> states) ||
                    !TryOpenVaultBuffer(vault, ref _powersHandle, BufferID.ShinobuMaterialPowers, capacity, out NativeArray<MaterialPowerDTO> powers) ||
                    !TryOpenVaultBuffer(vault, ref _visibleIndicesHandle, BufferID.ShinobuMaterialVisibleIndices, capacity, out NativeArray<uint> visibleIndices) ||
                    !TryOpenVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, capacity, out NativeArray<MaterialVisibleDTO> visiblePayload) ||
                    !TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out NativeArray<GlobalShaderConstantsDTO> constants) ||
                    !TryOpenVaultBuffer(vault, ref _mappingHandle, BufferID.ShinobuMaterialTextureMappings, TextureMappingCapacity, out NativeArray<TextureSetMappingDTO> mappings) ||
                    !TryOpenVaultBuffer(vault, ref _wearRateHandle, BufferID.ShinobuMaterialWearRates, WearRateCount, out NativeArray<WearRateDTO> wearRates) ||
                    !TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars) ||
                    !TryOpenVaultBuffer(vault, ref _mockBiomassHandle, BufferID.ShinobuMaterialMockBiomassSignals, MockSignalCount, out NativeArray<MockBiomassDensitySignal> biomass))
                {
                    return;
                }

                wearRates[0] = new WearRateDTO
                {
                    IronOxidationRate = 0.010f,
                    SaltDepositionRate = 0.018f,
                    MossGrowthRate = 0.0075f,
                    PowerFlickerRate = 0.35f
                };

                for (int i = 0; i < mappings.Length; i++)
                {
                    mappings[i] = new TextureSetMappingDTO
                    {
                        TextureSetHash = HashMaterialSeed((uint)i),
                        SliceIndex = (uint)(i % 12),
                        Generation = _csvGeneration,
                        Flags = 0u
                    };
                }

                for (int i = 0; i < states.Length; i++)
                {
                    uint hash = HashMaterialSeed((uint)i);
                    float t = ((hash >> 8) & 1023u) * (1.0f / 1023.0f);
                    float depth = -math.lerp(8.0f, 850.0f, ((hash >> 18) & 1023u) * (1.0f / 1023.0f));
                    states[i] = new InstanceMaterialDTO
                    {
                        WearAge = math.saturate(0.08f + t * 0.75f),
                        SaltAccumulation = math.saturate((depth > -12.0f ? 0.8f : 0.2f) * (1.0f - t * 0.35f)),
                        BioGrowthMask = math.saturate(0.05f + ((hash >> 3) & 255u) * (1.0f / 255.0f) * 0.45f),
                        TextureSetHash = EncodeTextureSetHash(mappings[i % mappings.Length].TextureSetHash, mappings[i % mappings.Length].SliceIndex)
                    };
                    powers[i] = new MaterialPowerDTO
                    {
                        PowerLevel = math.saturate(0.35f + ((hash >> 11) & 255u) * (1.0f / 255.0f) * 0.65f),
                        DepthMeters = depth,
                        StructuralStress01 = math.saturate(t * 0.6f),
                        Flags = ((hash & 3u) == 0u) ? 1u : 0u
                    };
                    visibleIndices[i] = (uint)i;
                    visiblePayload[i] = default;
                }

                constants[0] = BuildConstants(
                    new float4(0.14f, 0.72f, 0.48f, s_pendingSssTranslucency),
                    s_pendingCausticIntensity,
                    s_pendingSaltLineDepth,
                    ResolvePublishedShaderQualityWeight(ResolveGlobalQualityWeight(), 1.0f / 60.0f),
                    s_pendingRustRate,
                    s_pendingDebugMode,
                    12u,
                    _runtimeFlags);

                scalars[0] = new MaterialRuntimeScalarsDTO
                {
                    GlobalBiomass01 = 0.25f,
                    GlobalQualityWeight = ResolveGlobalQualityWeight(),
                    VisibleCount = (uint)math.min(states.Length, _materialCapacity),
                    CsvGeneration = _csvGeneration
                };
                biomass[0] = new MockBiomassDensitySignal
                {
                    Density01 = 0.25f,
                    Pulse01 = 0.5f,
                    SectorHash = SystemHash,
                    Frame = 0u
                };

                _activeVisibleCount = math.min(states.Length, _materialCapacity);
                _lastUploadedVisibleCount = 0;
                _visiblePayloadDirty = true;
                _constantsDirty = true;
                _defaultsInitialized = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(DefaultsMutationGuardMask);
            }
        }

        private void ApplyQualityAndEditorTuning(IDataVault vault, float frameDeltaSeconds)
        {
            if (!TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out NativeArray<GlobalShaderConstantsDTO> constants) ||
                !TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars))
            {
                return;
            }

            float quality = ResolveGlobalQualityWeight();
            float shaderQuality = ResolvePublishedShaderQualityWeight(quality, frameDeltaSeconds);
            GlobalShaderConstantsDTO dto = constants[0];
            bool constantsChanged = math.abs(dto.CausticSpeed.w - shaderQuality) > 0.0001f;
            dto.CausticSpeed.w = shaderQuality;
            if (s_hasPendingEditorTuning)
            {
                dto.GlobalWearMultiplier = s_pendingRustRate;
                dto.CausticSpeed.y = s_pendingCausticIntensity;
                dto.CausticSpeed.z = s_pendingSaltLineDepth;
                dto.SubsurfaceColor.w = s_pendingSssTranslucency;
                dto._pad0 = s_pendingDebugMode;
                s_hasPendingEditorTuning = false;
                constantsChanged = true;
            }
            uint nextFlags = (dto._pad0 != 0u) ? (_runtimeFlags | FlagDebugHeatmap) : (_runtimeFlags & ~FlagDebugHeatmap);
            if (dto._pad2 != nextFlags)
                constantsChanged = true;
            _runtimeFlags = nextFlags;
            dto._pad2 = nextFlags;
            constants[0] = dto;
            _constantsDirty |= constantsChanged;

            MaterialRuntimeScalarsDTO scalar = scalars[0];
            scalar.GlobalQualityWeight = quality;
            scalar.CsvGeneration = _csvGeneration;
            if (scalar.VisibleCount == 0u)
                scalar.VisibleCount = (uint)math.max(1, _materialCapacity);
            scalars[0] = scalar;
        }

        private bool ApplyPendingEditorTuningImmediate()
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return false;

            if (!vault.TryAcquireMutationGuard(ConstantsScalarsMutationGuardMask))
                return false;
            try
            {
                ApplyQualityAndEditorTuning(vault, 1.0f / 60.0f);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(ConstantsScalarsMutationGuardMask);
            }
        }

        private bool TryResolveSimulationBuffers(
            IDataVault vault,
            out NativeArray<InstanceMaterialDTO> states,
            out NativeArray<MaterialPowerDTO> powers,
            out NativeArray<uint> visibleIndices,
            out NativeArray<MaterialVisibleDTO> visiblePayload,
            out NativeArray<GlobalShaderConstantsDTO> constants,
            out NativeArray<MockBiomassDensitySignal> biomassSignals,
            out NativeArray<WearRateDTO> wearRates)
        {
            states = default;
            powers = default;
            visibleIndices = default;
            visiblePayload = default;
            constants = default;
            biomassSignals = default;
            wearRates = default;

            int capacity = math.max(1, _materialCapacity);
            if (!TryOpenVaultBuffer(vault, ref _statesHandle, BufferID.ShinobuMaterialStates, capacity, out states))
                return false;
            if (!TryOpenVaultBuffer(vault, ref _powersHandle, BufferID.ShinobuMaterialPowers, capacity, out powers))
                return false;
            if (!TryOpenVaultBuffer(vault, ref _visibleIndicesHandle, BufferID.ShinobuMaterialVisibleIndices, capacity, out visibleIndices))
                return false;
            if (!TryOpenVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, capacity, out visiblePayload))
                return false;
            if (!TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out constants))
                return false;
            if (!TryOpenVaultBuffer(vault, ref _mockBiomassHandle, BufferID.ShinobuMaterialMockBiomassSignals, MockSignalCount, out biomassSignals))
                return false;
            return TryOpenVaultBuffer(vault, ref _wearRateHandle, BufferID.ShinobuMaterialWearRates, WearRateCount, out wearRates);
        }

        private bool TryPinJobBuffers(IDataVault vault)
        {
            UnlockJobBuffers();
            if (vault == null || _jobBufferGuardHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            _jobBufferGuardVault = vault;
            _jobBufferGuardHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            UnlockJobBuffers(_vault);
        }

        private void UnlockJobBuffers(IDataVault vault)
        {
            if (!_jobBufferGuardHeld)
                return;

            IDataVault guardVault = _jobBufferGuardVault ?? vault;
            _jobBufferGuardVault = null;
            _jobBufferGuardHeld = false;
            guardVault?.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private void CompleteSimulationForLifecycle(IDataVault lockVault)
        {
            if (_simulationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
                _simulationScheduled = false;
            }

            UnlockJobBuffers(lockVault);
        }

        private bool EnsureGraphicsBuffers()
        {
            int capacity = math.max(1, _materialCapacity);
            int visibleStride = UnsafeUtility.SizeOf<MaterialVisibleDTO>();
            bool stateAChanged = EnsureBuffer(
                ref _materialStateBufferA,
                GraphicsBuffer.Target.Structured,
                capacity,
                visibleStride); // COLD ALLOC: GraphicsBuffer[MaterialVisibleDTO] - UberNoir material response A-buffer - owner: SHINOBU_43
            bool stateBChanged = EnsureBuffer(
                ref _materialStateBufferB,
                GraphicsBuffer.Target.Structured,
                capacity,
                visibleStride); // COLD ALLOC: GraphicsBuffer[MaterialVisibleDTO] - UberNoir material response B-buffer - owner: SHINOBU_43

            int constantsStride = UnsafeUtility.SizeOf<GlobalShaderConstantsDTO>();
            bool globalsAChanged = EnsureBuffer(
                ref _materialGlobalsBufferA,
                GraphicsBuffer.Target.Constant,
                1,
                constantsStride); // COLD ALLOC: GraphicsBuffer[GlobalShaderConstantsDTO] - 48B material CBuffer A - owner: SHINOBU_43
            bool globalsBChanged = EnsureBuffer(
                ref _materialGlobalsBufferB,
                GraphicsBuffer.Target.Constant,
                1,
                constantsStride); // COLD ALLOC: GraphicsBuffer[GlobalShaderConstantsDTO] - 48B material CBuffer B - owner: SHINOBU_43

            if (stateAChanged || stateBChanged)
            {
                _materialStateReadIndex = 0;
                _lastUploadedVisibleCount = 0;
                _visiblePayloadDirty = true;
            }

            if (globalsAChanged || globalsBChanged)
            {
                _materialGlobalsReadIndex = 0;
                _constantsDirty = true;
            }

            return _materialStateBufferA != null &&
                _materialStateBufferB != null &&
                _materialGlobalsBufferA != null &&
                _materialGlobalsBufferB != null;
        }

        private bool AreGraphicsBuffersReady()
        {
            int capacity = math.max(1, _materialCapacity);
            int visibleStride = UnsafeUtility.SizeOf<MaterialVisibleDTO>();
            int constantsStride = UnsafeUtility.SizeOf<GlobalShaderConstantsDTO>();
            return IsGraphicsBufferReady(_materialStateBufferA, capacity, visibleStride) &&
                IsGraphicsBufferReady(_materialStateBufferB, capacity, visibleStride) &&
                IsGraphicsBufferReady(_materialGlobalsBufferA, 1, constantsStride) &&
                IsGraphicsBufferReady(_materialGlobalsBufferB, 1, constantsStride);
        }

        private static bool IsGraphicsBufferReady(GraphicsBuffer buffer, int count, int stride)
        {
            return buffer != null &&
                buffer.count == count &&
                buffer.stride == stride;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, GraphicsBuffer.Target target, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
                return false;

            ReleaseBuffer(ref buffer);
            // COLD ALLOC: GraphicsBuffer[count] - SHINOBU double-buffer GPU upload lane - owner: SHINOBU_43
            buffer = new GraphicsBuffer(
                target,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
            return true;
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseBuffer(ref _materialStateBufferA);
            ReleaseBuffer(ref _materialStateBufferB);
            ReleaseBuffer(ref _materialGlobalsBufferA);
            ReleaseBuffer(ref _materialGlobalsBufferB);
            _materialStateReadIndex = 0;
            _materialGlobalsReadIndex = 0;
            _lastUploadedVisibleCount = 0;
            _visiblePayloadDirty = true;
            _constantsDirty = true;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            if (destination == null || !source.IsCreated || count <= 0)
                return;

            int safeCount = math.min(math.min(count, source.Length), destination.count);
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<T>())
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(source);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<T>());
            }
            finally
            {
                destination.UnlockBufferAfterWrite<T>(safeCount);
            }
        }

        private static void UploadConstants(GraphicsBuffer destination, GlobalShaderConstantsDTO constants)
        {
            if (destination == null)
                return;

            NativeArray<GlobalShaderConstantsDTO> mapped = destination.LockBufferForWrite<GlobalShaderConstantsDTO>(0, 1);
            try
            {
                mapped[0] = constants;
            }
            finally
            {
                destination.UnlockBufferAfterWrite<GlobalShaderConstantsDTO>(1);
            }
        }

        private GraphicsBuffer SelectMaterialStateBuffer(int index)
        {
            return (index & 1) == 0 ? _materialStateBufferA : _materialStateBufferB;
        }

        private GraphicsBuffer SelectMaterialGlobalsBuffer(int index)
        {
            return (index & 1) == 0 ? _materialGlobalsBufferA : _materialGlobalsBufferB;
        }

#if UNITY_EDITOR
        private void MonitorTextureCsv(IDataVault vault)
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (ticks == _csvLastWriteTicks)
                return;

            int capacity = math.max(1, _materialCapacity);
            if (!TryOpenVaultBuffer(vault, ref _csvScratchHandle, BufferID.ShinobuMaterialCsvScratch, CsvScratchBytes, out NativeArray<byte> scratch) ||
                !TryOpenVaultBuffer(vault, ref _mappingHandle, BufferID.ShinobuMaterialTextureMappings, TextureMappingCapacity, out NativeArray<TextureSetMappingDTO> mappings) ||
                !TryOpenVaultBuffer(vault, ref _statesHandle, BufferID.ShinobuMaterialStates, capacity, out NativeArray<InstanceMaterialDTO> states) ||
                !TryOpenVaultBuffer(vault, ref _constantsHandle, BufferID.ShinobuMaterialConstants, ConstantsCount, out NativeArray<GlobalShaderConstantsDTO> constants) ||
                !TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars))
            {
                return;
            }

            int bytesRead = ReadFileIntoScratch(_csvPath, scratch);
            if (bytesRead <= 0)
                return;

            uint generation = unchecked(_csvGeneration + 1u);
            int rowCount = ParseTextureSetCsv(scratch, bytesRead, mappings, generation);
            if (rowCount <= 0)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                InstanceMaterialDTO dto = states[i];
                TextureSetMappingDTO mapping = mappings[i % rowCount];
                dto.TextureSetHash = EncodeTextureSetHash(mapping.TextureSetHash, mapping.SliceIndex);
                states[i] = dto;
            }

            GlobalShaderConstantsDTO globals = constants[0];
            globals._pad1 = (uint)math.min(rowCount, 12);
            globals._pad2 = globals._pad2 | FlagTextureArraysBound | FlagCsvLoaded;
            constants[0] = globals;

            MaterialRuntimeScalarsDTO scalar = scalars[0];
            scalar.CsvGeneration = generation;
            scalars[0] = scalar;
            _runtimeFlags |= FlagCsvLoaded | FlagTextureArraysBound;
            _csvGeneration = generation;
            _csvLastWriteTicks = ticks;
            _visiblePayloadDirty = true;
            _constantsDirty = true;
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

        private static int ParseTextureSetCsv(NativeArray<byte> bytes, int byteCount, NativeArray<TextureSetMappingDTO> mappings, uint generation)
        {
            int rows = 0;
            int index = 0;
            while (index < byteCount && rows < mappings.Length)
            {
                SkipWhitespaceAndLineEnds(bytes, byteCount, ref index);
                if (index >= byteCount)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                int tokenStart = index;
                while (index < byteCount && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                if (index >= byteCount || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                int tokenEnd = TrimTokenEnd(bytes, tokenStart, index);
                index++;
                uint slice = ParseUnsigned(bytes, byteCount, ref index, out bool sliceOk);
                if (!sliceOk)
                {
                    SkipLine(bytes, byteCount, ref index);
                    continue;
                }

                uint hash = ParseHashToken(bytes, tokenStart, tokenEnd);
                mappings[rows] = new TextureSetMappingDTO
                {
                    TextureSetHash = hash,
                    SliceIndex = slice,
                    Generation = generation,
                    Flags = 0u
                };
                rows++;
                SkipLine(bytes, byteCount, ref index);
            }

            return rows;
        }

        private static uint ParseHashToken(NativeArray<byte> bytes, int start, int end)
        {
            int index = start;
            uint parsed = ParseUnsigned(bytes, end, ref index, out bool ok);
            if (ok && index >= end)
                return parsed;

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }

        private static uint ParseUnsigned(NativeArray<byte> bytes, int byteCount, ref int index, out bool ok)
        {
            SkipSpaces(bytes, byteCount, ref index);
            uint value = 0u;
            int start = index;
            while (index < byteCount)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = value * 10u + (uint)(c - (byte)'0');
                index++;
            }

            ok = index > start;
            return value;
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
#endif

        private void RecordTelemetry(
            IDataVault vault,
            NativeArray<MaterialResponseTelemetryEntry> telemetry,
            int uploadCount,
            float uploadMs,
            uint flags)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            TryOpenVaultBuffer(vault, ref _visiblePayloadHandle, BufferID.ShinobuMaterialVisiblePayload, math.max(1, _materialCapacity), out NativeArray<MaterialVisibleDTO> visiblePayload);
            TryOpenVaultBuffer(vault, ref _scalarsHandle, BufferID.ShinobuMaterialBiomassScalar, ScalarCount, out NativeArray<MaterialRuntimeScalarsDTO> scalars);
            float quality = scalars.IsCreated && scalars.Length > 0 ? scalars[0].GlobalQualityWeight : ResolveGlobalQualityWeight();
            float wearMean = 0.0f;
            float saltMean = 0.0f;
            float bioMean = 0.0f;
            float powerMean = 0.0f;
            uint hash = 2166136261u;
            int sampleCount = visiblePayload.IsCreated ? math.min(uploadCount, visiblePayload.Length) : 0;
            int sampleBudget = ResolveTelemetrySampleBudget(quality);
            int sampleStride = sampleCount > sampleBudget ? math.max(1, sampleCount / sampleBudget) : 1;
            int sampled = 0;
            for (int i = 0; i < sampleCount && sampled < sampleBudget; i += sampleStride)
            {
                MaterialVisibleDTO dto = visiblePayload[i];
                wearMean += dto.WearAge;
                saltMean += dto.SaltAccumulation;
                bioMean += dto.BioGrowthMask;
                powerMean += dto.PowerLevel;
                hash = (hash ^ math.asuint(dto.WearAge)) * 16777619u;
                hash = (hash ^ dto.TextureSetHash) * 16777619u;
                sampled++;
            }

            float inv = sampled > 0 ? math.rcp(sampled) : 0.0f;
            _lastStateHash = hash;
            uint csvGeneration = scalars.IsCreated && scalars.Length > 0 ? scalars[0].CsvGeneration : _csvGeneration;
            int cursor = _telemetryCursor % telemetry.Length;
            telemetry[cursor] = new MaterialResponseTelemetryEntry
            {
                Frame = _lastDispatcherFrame,
                Flags = flags,
                VisibleCount = (uint)uploadCount,
                UploadedBytes = (uint)(uploadCount * UnsafeUtility.SizeOf<MaterialVisibleDTO>()),
                MaterialBufferUploadTimeMs = uploadMs,
                ActiveTriplanarPixels = uploadCount * math.saturate((quality - 0.5f) * 2.0f) * 128.0f,
                TextureArrayMemoryMB = ResolveTextureArrayMemoryMb(quality),
                GlobalQualityWeight = quality,
                StateHash = hash,
                CsvGeneration = csvGeneration,
                LayoutHash = ResolveLayoutHash(),
                WearMean = wearMean * inv,
                SaltMean = saltMean * inv,
                BioMean = bioMean * inv,
                PowerMean = powerMean * inv
            };
            _telemetryCursor = (_telemetryCursor + 1) % telemetry.Length;
        }

        private void DumpTelemetry(IDataVault vault, NativeArray<MaterialResponseTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || string.IsNullOrEmpty(_dumpPath))
                return;

            int entryBytes = UnsafeUtility.SizeOf<MaterialResponseTelemetryEntry>();
            int totalBytes = 24 + telemetry.Length * entryBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(ShinobuMaterialResponseRuntime),
                    "shinobuMaterialResponseTelemetryDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> bytes = new Span<byte>(target, totalBytes);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(0, 8), DumpMagic);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(8, 4), DumpVersion);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(12, 4), (uint)telemetry.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(16, 4), (uint)entryBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(20, 4), _lastStateHash);

                uint hash = unchecked((uint)DumpMagic) ^
                            unchecked((uint)(DumpMagic >> 32)) ^
                            DumpVersion ^
                            (uint)telemetry.Length ^
                            _lastStateHash;
                for (int byteIndex = 0; byteIndex < 24; byteIndex++)
                    hash = (hash * 16777619u) ^ bytes[byteIndex];

                int writeOffset = 24;
                for (int offset = 0; offset < telemetry.Length; offset++)
                {
                    int index = (_telemetryCursor + offset) % telemetry.Length;
                    MaterialResponseTelemetryEntry entry = telemetry[index];
                    ReadOnlySpan<byte> entrySpan = new ReadOnlySpan<byte>(&entry, entryBytes);
                    entrySpan.CopyTo(bytes.Slice(writeOffset, entryBytes));
                    for (int byteIndex = 0; byteIndex < entryBytes; byteIndex++)
                        hash = (hash * 16777619u) ^ bytes[writeOffset + byteIndex];

                    writeOffset += entryBytes;
                }

                if (NativeFaultDumpWriter.TryWriteAll(_dumpPath, payload, totalBytes))
                    _lastTelemetryDumpHash = hash;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ShinobuMaterialResponseRuntime),
                    "shinobuMaterialResponseTelemetryDumpPayload");
            }
        }

        private static GlobalShaderConstantsDTO BuildConstants(
            float4 subsurfaceColor,
            float causticIntensity,
            float saltLineDepth,
            float quality,
            float wearMultiplier,
            uint debugMode,
            uint textureSetCount,
            uint flags)
        {
            return new GlobalShaderConstantsDTO
            {
                SubsurfaceColor = new float4(
                    math.saturate(subsurfaceColor.x),
                    math.saturate(subsurfaceColor.y),
                    math.saturate(subsurfaceColor.z),
                    math.saturate(subsurfaceColor.w)),
                CausticSpeed = new float4(0.17f, math.saturate(causticIntensity), saltLineDepth, math.saturate(quality)),
                GlobalWearMultiplier = math.max(0.0f, SanitizeFloat(wearMultiplier, 1.0f)),
                _pad0 = debugMode,
                _pad1 = math.max(1u, textureSetCount),
                _pad2 = flags
            };
        }

        private static int ResolveSimulationBudget(int capacity, float quality)
        {
            float q = math.saturate(quality);
            float curved = q * q * (3.0f - 2.0f * q);
            return math.max(1, (int)math.round(math.lerp(128.0f, capacity, curved)));
        }

        private static int ResolveUpdateCadence(float quality)
        {
            float q = math.saturate(quality);
            float continuous = math.lerp(12.0f, 1.0f, q * q);
            return math.max(1, (int)math.round(continuous));
        }

        private static int ResolveTelemetrySampleBudget(float quality)
        {
            float q = math.saturate(quality);
            float curved = q * q * (3.0f - 2.0f * q);
            return math.max(16, (int)math.round(math.lerp(32.0f, 384.0f, curved)));
        }

        private static float ResolveGlobalQualityWeight()
        {
            return math.saturate(HomeostasisBrain.GlobalQualityWeight);
        }

        private float ResolvePublishedShaderQualityWeight(float targetQuality, float frameDeltaSeconds)
        {
            float target = math.saturate(SanitizeFloat(targetQuality, 1.0f));
            float previous = math.saturate(SanitizeFloat(_publishedShaderQualityWeight, target));
            float dt = math.clamp(SanitizeFloat(frameDeltaSeconds, 1.0f / 60.0f), 0.0001f, 0.05f);
            float falling = math.step(target, previous);
            float pressure = math.saturate(math.abs(target - previous));
            float downRate = math.lerp(8.0f, 18.0f, pressure);
            float upRate = math.lerp(0.85f, 3.0f, target * target);
            float t = math.saturate(dt * math.lerp(upRate, downRate, falling));
            t = t * t * (3.0f - 2.0f * t);
            _publishedShaderQualityWeight = math.saturate(math.lerp(previous, target, t));
            return _publishedShaderQualityWeight;
        }

        private static float ResolveTextureArrayMemoryMb(float quality)
        {
            float q = math.saturate(quality);
            float mipScale = math.lerp(0.125f, 1.0f, q * q);
            return TextureArrayMemoryBaseMb * mipScale;
        }

        private static float SanitizeDelta(float delta)
        {
            return math.clamp(SanitizeFloat(delta, 1.0f / 60.0f), 0.0001f, 0.05f);
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !(float.IsNaN(value) || float.IsInfinity(value));
        }

        private static float ElapsedMs(long start)
        {
            long now = Stopwatch.GetTimestamp();
            return (now - start) * 1000.0f / Stopwatch.Frequency;
        }

        private static uint HashMaterialSeed(uint index)
        {
            uint x = index + 0x9E3779B9u;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x == 0u ? 1u : x;
        }

        private static uint EncodeTextureSetHash(uint hash, uint sliceIndex)
        {
            return (hash & 0xFFFF0000u) | (sliceIndex & 0xFFFFu);
        }

        private static uint ResolveLayoutHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<InstanceMaterialDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<MaterialPowerDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<MaterialVisibleDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<GlobalShaderConstantsDTO>()) * 16777619u;
            hash = (hash ^ (uint)UnsafeUtility.SizeOf<MaterialResponseTelemetryEntry>()) * 16777619u;
            return hash;
        }

        private static bool IsLayoutValid()
        {
            return UnsafeUtility.SizeOf<InstanceMaterialDTO>() == 16 &&
                UnsafeUtility.SizeOf<MaterialPowerDTO>() == 16 &&
                UnsafeUtility.SizeOf<MaterialVisibleDTO>() == 32 &&
                UnsafeUtility.SizeOf<GlobalShaderConstantsDTO>() == 48 &&
                UnsafeUtility.SizeOf<MaterialResponseTelemetryEntry>() == 64;
        }

        private sealed class PreSimulationPhaseSystem : PhaseSystemBase
        {
            public PreSimulationPhaseSystem(ShinobuMaterialResponseRuntime owner) : base(owner, DispatcherPhase.PreSimulation, 0x53485052u) { }
            public override void PreSimulationTick(in DispatcherTimingDTO timing) { Owner.PreSimulationTick(in timing); }
        }

        private sealed class SimulationPhaseSystem : PhaseSystemBase
        {
            public SimulationPhaseSystem(ShinobuMaterialResponseRuntime owner) : base(owner, DispatcherPhase.Simulation, 0x53485349u) { }
            public override JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return Owner.ScheduleSimulation(in timing, in context, dependsOn);
            }
        }

        private sealed class PostSimulationPhaseSystem : PhaseSystemBase
        {
            public PostSimulationPhaseSystem(ShinobuMaterialResponseRuntime owner) : base(owner, DispatcherPhase.PostSimulation, 0x5348504Fu) { }
            public override void PostSimulationTick(in DispatcherTimingDTO timing) { Owner.PostSimulationTick(in timing); }
        }

        private sealed class VisualSyncPhaseSystem : PhaseSystemBase
        {
            public VisualSyncPhaseSystem(ShinobuMaterialResponseRuntime owner) : base(owner, DispatcherPhase.VisualSync, 0x53485649u) { }
            public override void VisualSyncTick(in DispatcherTimingDTO timing) { Owner.VisualSyncTick(in timing); }
        }

        private abstract class PhaseSystemBase : IDispatcherSystem
        {
            protected readonly ShinobuMaterialResponseRuntime Owner;
            private readonly DispatcherPhase _phase;
            private readonly uint _hash;

            protected PhaseSystemBase(ShinobuMaterialResponseRuntime owner, DispatcherPhase phase, uint hash)
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
    internal struct MockBiomassScalarJob : IJob
    {
        [NoAlias] public NativeArray<MockBiomassDensitySignal> Signals;
        [NoAlias] public NativeArray<MaterialRuntimeScalarsDTO> Scalars;
        public uint Frame;
        public uint SectorHash;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Signals.IsCreated || Signals.Length == 0 || !Scalars.IsCreated || Scalars.Length == 0)
                return;

            uint seed = math.max(1u, SectorHash ^ (Frame * 747796405u) ^ 0xA511E9B3u);
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            float q = math.saturate(GlobalQualityWeight);
            float randomDensity = random.NextFloat(0.08f, 0.95f);
            MaterialRuntimeScalarsDTO scalar = Scalars[0];
            float blend = math.lerp(0.004f, 0.035f, q);
            scalar.GlobalBiomass01 = math.saturate(math.lerp(scalar.GlobalBiomass01, randomDensity, blend));
            scalar.GlobalQualityWeight = q;
            Scalars[0] = scalar;

            Signals[0] = new MockBiomassDensitySignal
            {
                Density01 = scalar.GlobalBiomass01,
                Pulse01 = random.NextFloat(0.0f, 1.0f),
                SectorHash = SectorHash,
                Frame = Frame
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct MaterialWearUpdateJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InstanceMaterialDTO> States;
        [NoAlias] public NativeArray<MaterialPowerDTO> Powers;
        [NoAlias] public NativeArray<WearRateDTO> WearRates;
        [NoAlias] public NativeArray<MaterialRuntimeScalarsDTO> Scalars;
        [NoAlias] public NativeArray<GlobalShaderConstantsDTO> Constants;
        public uint Frame;
        public float DeltaSeconds;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)States.Length || (uint)index >= (uint)Powers.Length ||
                !WearRates.IsCreated || WearRates.Length == 0 || !Scalars.IsCreated || Scalars.Length == 0 ||
                !Constants.IsCreated || Constants.Length == 0)
            {
                return;
            }

            InstanceMaterialDTO state = States[index];
            MaterialPowerDTO power = Powers[index];
            WearRateDTO rates = WearRates[0];
            MaterialRuntimeScalarsDTO scalar = Scalars[0];
            GlobalShaderConstantsDTO constants = Constants[0];

            float q = math.saturate(GlobalQualityWeight);
            float dt = math.clamp(DeltaSeconds, 0.0001f, 0.05f);
            uint seed = math.max(1u, ((uint)index * 2891336453u) ^ (Frame * 747796405u) ^ state.TextureSetHash);
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            float jitter = random.NextFloat(0.85f, 1.15f);
            float depth = SanitizeFinite(power.DepthMeters, -64.0f);
            float underwater01 = math.saturate((-depth) * 0.0025f);
            float nearSaltLine01 = 1.0f - math.saturate(math.abs(depth - constants.CausticSpeed.z) * 0.05f);
            float wearRate = rates.IronOxidationRate * (0.25f + underwater01) * constants.GlobalWearMultiplier * jitter;
            float saltRate = rates.SaltDepositionRate * (0.10f + nearSaltLine01);
            float mossRate = rates.MossGrowthRate * (0.15f + scalar.GlobalBiomass01) * (0.35f + q);
            state.WearAge = math.saturate(SanitizeFinite(state.WearAge, 0.0f) + wearRate * dt);
            state.SaltAccumulation = math.saturate(SanitizeFinite(state.SaltAccumulation, 0.0f) + saltRate * dt);
            state.BioGrowthMask = math.saturate(SanitizeFinite(state.BioGrowthMask, 0.0f) + mossRate * dt);
            float flicker = TriangleSigned(HashPowerFlicker((uint)index, Frame));
            power.PowerLevel = math.saturate(SanitizeFinite(power.PowerLevel, 1.0f) + flicker * rates.PowerFlickerRate * 0.0005f);
            power.StructuralStress01 = math.saturate(SanitizeFinite(power.StructuralStress01, 0.0f) + state.WearAge * 0.0001f);
            States[index] = state;
            Powers[index] = power;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static uint HashPowerFlicker(uint index, uint frame)
        {
            uint x = index * 2891336453u ^ frame * 747796405u ^ 0x4D415433u;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private static float TriangleSigned(uint hash)
        {
            float phase = (hash & 1023u) * (1.0f / 1023.0f);
            float triangle = 1.0f - math.abs(math.frac(phase) * 2.0f - 1.0f);
            return triangle * 2.0f - 1.0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct VisibleMaterialPackJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InstanceMaterialDTO> States;
        [NoAlias] public NativeArray<MaterialPowerDTO> Powers;
        [NoAlias] public NativeArray<uint> VisibleIndices;
        [NoAlias] public NativeArray<MaterialVisibleDTO> VisiblePayload;
        [NoAlias] public NativeArray<MaterialRuntimeScalarsDTO> Scalars;
        public float GlobalQualityWeight;
        public int VisibleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VisiblePayload.Length || !States.IsCreated || States.Length == 0 || !Powers.IsCreated || Powers.Length == 0)
                return;

            uint rawIndex = (VisibleIndices.IsCreated && (uint)index < (uint)VisibleIndices.Length) ? VisibleIndices[index] : (uint)index;
            int sourceIndex = (int)(rawIndex % (uint)math.min(States.Length, Powers.Length));
            InstanceMaterialDTO state = States[sourceIndex];
            MaterialPowerDTO power = Powers[sourceIndex];
            float q = math.saturate(GlobalQualityWeight);
            float wear = FiniteOr(state.WearAge, 0.0f);
            float salt = FiniteOr(state.SaltAccumulation, 0.0f);
            float bio = FiniteOr(state.BioGrowthMask, 0.0f);
            float powerLevel = FiniteOr(power.PowerLevel, 1.0f);
            float depthMeters = FiniteOr(power.DepthMeters, 0.0f);
            float moss = math.saturate(bio * math.lerp(0.35f, 1.0f, q));
            VisiblePayload[index] = new MaterialVisibleDTO
            {
                WearAge = math.saturate(wear),
                SaltAccumulation = math.saturate(salt),
                BioGrowthMask = math.saturate(bio),
                TextureSetHash = state.TextureSetHash,
                PowerLevel = math.saturate(powerLevel),
                Depth01 = math.saturate((-depthMeters) * 0.001f),
                MossLayer01 = moss,
                Flags = power.Flags
            };

            if (index == 0 && Scalars.IsCreated && Scalars.Length > 0)
            {
                MaterialRuntimeScalarsDTO scalar = Scalars[0];
                scalar.VisibleCount = (uint)math.max(0, VisibleCount);
                Scalars[0] = scalar;
            }
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }
}
