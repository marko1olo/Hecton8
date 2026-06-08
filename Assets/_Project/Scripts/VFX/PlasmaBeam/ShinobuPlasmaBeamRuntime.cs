using System;
using System.IO;
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
using UnityEngine.Rendering;

namespace Hecton8.VFX.PlasmaBeam
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BeamVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint ColorPacked;
        [FieldOffset(16)] public float2 UV;
        [FieldOffset(24)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BeamStateDTO
    {
        [FieldOffset(0)] public double3 ToolAup;
        [FieldOffset(24)] public double3 TargetAup;
        [FieldOffset(48)] public double3 CameraAup;
        [FieldOffset(72)] public float Radius;
        [FieldOffset(76)] public float HeatLevel;
        [FieldOffset(80)] public float EnergyRemaining;
        [FieldOffset(84)] public float NoiseFrequency;
        [FieldOffset(88)] public float NoiseAmplitude;
        [FieldOffset(92)] public float GlobalQualityWeight;
        [FieldOffset(96)] public float TimeSeconds;
        [FieldOffset(100)] public float BiomeExtinction01;
        [FieldOffset(104)] public uint ColorPacked;
        [FieldOffset(108)] public uint BiomeHash;
        [FieldOffset(112)] public uint BeamId;
        [FieldOffset(116)] public uint NoiseSeed;
        [FieldOffset(120)] public uint Flags;
        [FieldOffset(124)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct BeamTrigLutEntry
    {
        [FieldOffset(0)] public float Cos;
        [FieldOffset(4)] public float Sin;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlasmaBeamRuntimeScalarsDTO
    {
        [FieldOffset(0)] public float BaseRadius;
        [FieldOffset(4)] public float NoiseFrequency;
        [FieldOffset(8)] public float NoiseAmplitude;
        [FieldOffset(12)] public float UvScrollSpeed;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float HeatLevel;
        [FieldOffset(24)] public float EnergyRemaining;
        [FieldOffset(28)] public float BiomeExtinction01;
        [FieldOffset(32)] public uint ActiveBeamCount;
        [FieldOffset(36)] public uint RequestedBeamCount;
        [FieldOffset(40)] public uint ForcedRadialSegments;
        [FieldOffset(44)] public uint CsvGeneration;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint NonFiniteCount;
        [FieldOffset(56)] public uint SectorHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockLaserFireSignal : ISignal
    {
        [FieldOffset(0)] public float3 Start;
        [FieldOffset(12)] public float3 End;
        [FieldOffset(24)] public float HeatLevel;
        [FieldOffset(28)] public float EnergyRemaining;
        [FieldOffset(32)] public uint BeamId;
        [FieldOffset(36)] public uint NoiseSeed;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct PlasmaBeamAcousticEchoTap : ISignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Intensity01;
        [FieldOffset(16)] public uint BeamId;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint NoiseSeed;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PlasmaBeamIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlasmaBeamTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveBeams;
        [FieldOffset(8)] public uint VerticesGenerated;
        [FieldOffset(12)] public uint NonFiniteCount;
        [FieldOffset(16)] public float MeshingComputeTimeMs;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float MaxNoiseAmplitude;
        [FieldOffset(28)] public float MaxBeamLength;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint LengthSegments;
        [FieldOffset(40)] public uint RadialSegments;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PlasmaBeamDumpHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint FrameCount;
        [FieldOffset(16)] public uint EntrySize;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    public sealed unsafe class ShinobuPlasmaBeamRuntime : IGlobalRegistryHotSwapListener, IColdTickable
    {
        private static int s_x001ShinobuPlasmaBeamRuntimeSignalPushDropCount;
        public const int MaxBeamCount = 20;
        public const int MinRadialSegments = 3;
        public const int MaxRadialSegments = 8;
        public const int MinLengthSegments = 2;
        public const int MaxLengthSegments = 20;
        public const int MaxVerticesPerBeam = MaxLengthSegments * MaxRadialSegments * 6;
        public const int MaxVertexCount = MaxBeamCount * MaxVerticesPerBeam;
        public const int TrigLutCount = (MaxRadialSegments + 1) * MaxRadialSegments;
        public const int TelemetryFrameCount = 300;

        private const SystemID OwnerSystemId = SystemID.Vfx;
        private const uint SystemHash = 0x53363950u; // S69P
        private const uint FlagMockInputEnabled = 1u << 0;
        private const uint FlagCsvLoaded = 1u << 1;
        private const uint FlagDumpFailed = 1u << 28;
        private const uint FlagLayoutFault = 1u << 29;
        private const uint FlagNonFinite = 1u << 30;
        private const uint FlagShaderMissing = 1u << 31;
        private const uint WhiteRgba = 0xFFFFFFFFu;
        private const uint DefaultBeamRgba = 0xF2FFB826u;
        private const uint MuddyBeamRgba = 0xF2A8B36Fu;
        private const ulong DumpMagic = 0x5348494E4F363950UL; // SHINO69P
        private const uint DumpVersion = 1u;
        private const int CsvScratchBytes = 4096;
        private const int CsvPollCadenceFrames = 64;
        private const ulong PlasmaBeamJobMutationGuardMask = 0x0000000000FF0000UL;
        private const string CsvRelativePath = "Assets/_Project/Data/VFX/Beam/beam_visuals.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_LASER_SURGEON.bin";
        private const string DumpPayloadOwner = nameof(ShinobuPlasmaBeamRuntime);
        private const string DumpPayloadLabel = "PlasmaBeamTelemetryDumpPayload";
        private const uint HashRadius = 0x0DBA4CB3u;
        private const uint HashNoiseFrequency = 0x451093ACu;
        private const uint HashNoiseAmplitude = 0xF2F668B3u;
        private const uint HashHeat = 0x22693293u;
        private const uint HashEnergy = 0x29CC5095u;
        private const uint HashQualityWeight = 0x09B39C97u;
        private const uint HashRadialSegments = 0xEAADBB8Du;
        private const uint HashBiomeExtinction = 0x45416187u;
        private const uint HashRequestedBeams = 0x0D219280u;

        private static readonly int VerticesBufferId = Shader.PropertyToID("_H8PlasmaBeamVertices");
        private static readonly int UvScrollId = Shader.PropertyToID("_H8PlasmaUvScroll");
        private static readonly int IntensityId = Shader.PropertyToID("_H8PlasmaIntensity");
        private static readonly int QualityId = Shader.PropertyToID("_H8PlasmaGlobalQualityWeight");
        private static readonly int ScatterId = Shader.PropertyToID("_H8PlasmaNoirScatter");
        private static readonly int FrameTimeId = Shader.PropertyToID("_H8PlasmaFrameTime");

        private static ShinobuPlasmaBeamRuntime s_active;
        private static bool s_hasPendingEditorTuning;
        private static float s_pendingRadius = 0.045f;
        private static float s_pendingNoiseFrequency = 5.5f;
        private static float s_pendingNoiseAmplitude = 0.028f;
        private static uint s_pendingRadialSegments = 0u;

        private IDataVault _vault;
        private VaultGenerationHandle<BeamStateDTO> _statesHandle;
        private VaultGenerationHandle<BeamVertexDTO> _verticesHandle;
        private VaultGenerationHandle<BeamTrigLutEntry> _trigHandle;
        private VaultGenerationHandle<PlasmaBeamRuntimeScalarsDTO> _scalarsHandle;
        private VaultGenerationHandle<PlasmaBeamIndirectArgsDTO> _argsHandle;
        private VaultGenerationHandle<PlasmaBeamTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<MockLaserFireSignal> _mockSignalsHandle;
        private VaultGenerationHandle<PlasmaBeamAcousticEchoTap> _acousticTapsHandle;

        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;

        private GraphicsBuffer _vertexGpuBufferA;
        private GraphicsBuffer _vertexGpuBufferB;
        private GraphicsBuffer _activeVertexGpuBuffer;
        private GraphicsBuffer _indirectArgsGpuBufferA;
        private GraphicsBuffer _indirectArgsGpuBufferB;
        private GraphicsBuffer _activeIndirectArgsGpuBuffer;
        private Material _material;
        // COLD ALLOC: MaterialPropertyBlock[1] - indirect beam draw payload - owner: SHINOBU_69
        private MaterialPropertyBlock _drawProperties;
        private Bounds _drawBounds;
        private string _csvPath;
        private string _dumpPath;
        private long _csvLastWriteTicks;
        private long _jobScheduleTimestamp;
        private JobHandle _simulationHandle;
        private ulong _activeJobMutationGuardMask;
        private IDataVault _activeJobMutationGuardVault;
        private int _vertexGpuUploadBufferIndex;
        private int _indirectArgsGpuUploadBufferIndex;
        private int _lastVertexCount;
        private int _lastActiveBeamCount;
        private float _lastDeterministicTimeSeconds;
        private uint _lastDispatcherFrame;
        private uint _csvGeneration = 1u;
        private uint _runtimeFlags = FlagMockInputEnabled;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredColdTick;
        private bool _vaultInitialized;
        private bool _defaultsInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _simulationScheduled;
        private bool _dumpedNonFinite;
        private bool _shutdown;
        private bool _registeredHotSwapListener;
        private bool _vaultRepairRequested;
        private readonly PlasmaBeamAcousticEchoTap[] _tapPublishSnapshot = new PlasmaBeamAcousticEchoTap[MaxBeamCount];

        public static bool TryWriteEditorTuning(float radius, float noiseFrequency, float noiseAmplitude, uint radialSegments)
        {
            s_pendingRadius = math.clamp(SanitizeFloat(radius, 0.045f), 0.002f, 0.4f);
            s_pendingNoiseFrequency = math.clamp(SanitizeFloat(noiseFrequency, 5.5f), 0.0f, 48.0f);
            s_pendingNoiseAmplitude = math.clamp(SanitizeFloat(noiseAmplitude, 0.028f), 0.0f, 0.35f);
            s_pendingRadialSegments = radialSegments > 0u ? (uint)math.clamp((int)radialSegments, MinRadialSegments, MaxRadialSegments) : 0u;
            s_hasPendingEditorTuning = true;

            ShinobuPlasmaBeamRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled)
                return false;

            return active.ApplyPendingEditorTuningImmediate();
        }

        public static bool TryReadEditorTuning(
            out float radius,
            out float noiseFrequency,
            out float noiseAmplitude,
            out uint radialSegments,
            out int activeBeams,
            out int verticesGenerated,
            out float qualityWeight)
        {
            radius = s_pendingRadius;
            noiseFrequency = s_pendingNoiseFrequency;
            noiseAmplitude = s_pendingNoiseAmplitude;
            radialSegments = s_pendingRadialSegments;
            activeBeams = 0;
            verticesGenerated = 0;
            qualityWeight = 1.0f;

            ShinobuPlasmaBeamRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled)
                return false;

            IDataVault vault = active.ResolveVault();
            if (vault == null || !active.HasVaultStateReady(vault))
                return false;

            if (active.TryResolveVaultBuffer(
                    vault,
                    in active._scalarsHandle,
                    BufferID.ShinobuPlasmaBeamRuntimeScalars,
                    1,
                    out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars))
            {
                PlasmaBeamRuntimeScalarsDTO dto = scalars[0];
                radius = dto.BaseRadius;
                noiseFrequency = dto.NoiseFrequency;
                noiseAmplitude = dto.NoiseAmplitude;
                radialSegments = dto.ForcedRadialSegments;
                activeBeams = (int)math.min(dto.ActiveBeamCount, (uint)MaxBeamCount);
                qualityWeight = dto.GlobalQualityWeight;
            }

            verticesGenerated = active._lastVertexCount;
            return true;
        }

        public static bool TryGetEditorMeshSnapshot(
            out NativeArray<BeamVertexDTO>.ReadOnly vertices,
            out int vertexCount,
            out int activeBeams)
        {
            vertices = default;
            vertexCount = 0;
            activeBeams = 0;

            ShinobuPlasmaBeamRuntime active = s_active;
            if (active == null)
                return false;

            if (active._simulationScheduled)
                return false;

            IDataVault vault = active.ResolveVault();
            if (vault == null || !active.HasVaultStateReady(vault))
                return false;

            if (!active.TryResolveVaultBuffer(
                    vault,
                    in active._verticesHandle,
                    BufferID.ShinobuPlasmaBeamVertices,
                    MaxVertexCount,
                    out NativeArray<BeamVertexDTO> mutableVertices))
            {
                return false;
            }

            vertices = mutableVertices.AsReadOnly();
            vertexCount = active._lastVertexCount;
            activeBeams = active._lastActiveBeamCount;
            return vertices.Length > 0 && vertexCount > 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActive();
            s_active = null;
            s_hasPendingEditorTuning = false;
            s_pendingRadius = 0.045f;
            s_pendingNoiseFrequency = 5.5f;
            s_pendingNoiseAmplitude = 0.028f;
            s_pendingRadialSegments = 0u;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            // COLD ALLOC: ShinobuPlasmaBeamRuntime[1] - dispatcher-owned indirect beam service - owner: SHINOBU_69
            ShinobuPlasmaBeamRuntime runtime = new ShinobuPlasmaBeamRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private static void ShutdownActive()
        {
            ShinobuPlasmaBeamRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownActive;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.quitting -= ShutdownActive;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownActive;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.quitting += ShutdownActive;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ShutdownActive();
            }
        }
#endif

        private ShinobuPlasmaBeamRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
            _dumpPath = DumpRelativePath;
            _drawBounds = new Bounds(Vector3.zero, new Vector3(256.0f, 256.0f, 256.0f));

            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into GlobalRegistry dispatcher - owner: SHINOBU_69
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        private void Initialize()
        {
            _shutdown = false;
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            TryRegisterHotSwapListener();
            SignalBus<PlasmaBeamAcousticEchoTap>.Configure(MaxBeamCount, maxFrameSignals: MaxBeamCount, lowTierFrameSignals: MaxBeamCount, laneHash: 0x504C4153u);
            SignalBus<PlasmaBeamAcousticEchoTap>.EnsureInitialized();
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
            CompleteSimulationForLifecycle();
            ReleasePlasmaBeamJobMutationGuard();
            TryUnregisterHotSwapListener();
            UnregisterDispatcherPhases();
            ReleaseVaultHandles(_vault);
            ReleaseGraphicsResources();
            _vault = null;
            ResetVaultEpochState();
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

        private IDataVault ResolveVault()
        {
            return _vault;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
                RebindDataVaultForLifecycle(nextVault);
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

            if (!_vaultRepairRequested && HasVaultStateReady(vault) && HasGraphicsResourcesReady())
                return;

            if (_simulationScheduled)
                return;

            PrepareRuntimeStateCold();
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            CompleteSimulationForLifecycle();
            ReleasePlasmaBeamJobMutationGuard();
            ReleaseVaultHandles(_vault);
            _vault = vault;
            ResetVaultEpochState();
        }

        private void ResetVaultEpochState()
        {
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _layoutChecked = false;
            _layoutValid = false;
            _simulationScheduled = false;
            _dumpedNonFinite = false;
            _activeJobMutationGuardMask = 0UL;
            _activeJobMutationGuardVault = null;
            _lastVertexCount = 0;
            _lastActiveBeamCount = 0;
            _vaultRepairRequested = true;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _statesHandle, BufferID.ShinobuPlasmaBeamStates);
            ReleaseVaultHandle(vault, ref _verticesHandle, BufferID.ShinobuPlasmaBeamVertices);
            ReleaseVaultHandle(vault, ref _trigHandle, BufferID.ShinobuPlasmaBeamTrigLut);
            ReleaseVaultHandle(vault, ref _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars);
            ReleaseVaultHandle(vault, ref _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuPlasmaBeamTelemetryRing);
            ReleaseVaultHandle(vault, ref _mockSignalsHandle, BufferID.ShinobuPlasmaBeamMockSignals);
            ReleaseVaultHandle(vault, ref _acousticTapsHandle, BufferID.ShinobuPlasmaBeamAcousticTaps);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsOwnedHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
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

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault))
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!vault.TryAcquireMutationGuard(PlasmaBeamJobMutationGuardMask))
                return;
            try
            {
                ApplyQualityAndEditorTuning(vault);
            }
            finally
            {
                vault.ReleaseMutationGuard(PlasmaBeamJobMutationGuardMask);
            }
#if UNITY_EDITOR
            uint frame = unchecked(_lastDispatcherFrame + 1u);
            if ((frame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorBeamCsv(vault);
#endif
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                    return JobHandle.CombineDependencies(dependsOn, _simulationHandle);

                _simulationScheduled = false;
                ReleasePlasmaBeamJobMutationGuard();
            }

            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault))
            {
                _vaultRepairRequested = true;
                return dependsOn;
            }

            _lastDispatcherFrame = context.Frame;

            if (!TryAcquirePlasmaBeamJobMutationGuard(vault))
                return dependsOn;

            bool scheduled = false;
            try
            {
                if (!TryResolveVaultBuffer(vault, in _statesHandle, BufferID.ShinobuPlasmaBeamStates, MaxBeamCount, out NativeArray<BeamStateDTO> states) ||
                    !TryResolveVaultBuffer(vault, in _verticesHandle, BufferID.ShinobuPlasmaBeamVertices, MaxVertexCount, out NativeArray<BeamVertexDTO> vertices) ||
                    !TryResolveVaultBuffer(vault, in _trigHandle, BufferID.ShinobuPlasmaBeamTrigLut, TrigLutCount, out NativeArray<BeamTrigLutEntry> trig) ||
                    !TryResolveVaultBuffer(vault, in _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars) ||
                    !TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> args) ||
                    !TryResolveVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuPlasmaBeamTelemetryRing, TelemetryFrameCount, out NativeArray<PlasmaBeamTelemetryEntry> telemetry) ||
                    !TryResolveVaultBuffer(vault, in _mockSignalsHandle, BufferID.ShinobuPlasmaBeamMockSignals, MaxBeamCount, out NativeArray<MockLaserFireSignal> mockSignals) ||
                    !TryResolveVaultBuffer(vault, in _acousticTapsHandle, BufferID.ShinobuPlasmaBeamAcousticTaps, MaxBeamCount, out NativeArray<PlasmaBeamAcousticEchoTap> acousticTaps))
                {
                    return dependsOn;
                }

                float tickDelta = ResolveSimulationTickDelta(in timing);
                float timeSeconds = (float)((double)context.Frame * (double)tickDelta);
                _lastDeterministicTimeSeconds = timeSeconds;
                PlasmaBeamRuntimeScalarsDTO scalar = scalars[0];
                uint mockEnabled = (scalar.Flags & FlagMockInputEnabled) != 0u ? 1u : 0u;

                PlasmaBeamMockLaserFireJob mockJob = default;
                mockJob.States = states;
                mockJob.MockSignals = mockSignals;
                mockJob.Scalars = scalars;
                mockJob.Frame = context.Frame;
                mockJob.SystemHash = SystemHash;
                mockJob.TimeSeconds = timeSeconds;
                mockJob.MockEnabled = mockEnabled;
                JobHandle mockHandle = mockJob.Schedule(dependsOn);

                PlasmaBeamTubeMeshingJob meshJob = default;
                meshJob.States = states;
                meshJob.Vertices = vertices;
                meshJob.TrigLut = trig;
                meshJob.Scalars = scalars;
                meshJob.MaxRadialSegmentsValue = MaxRadialSegments;
                meshJob.MaxVerticesPerBeamValue = MaxVerticesPerBeam;
                meshJob.Frame = context.Frame;
                JobHandle meshHandle = meshJob.Schedule(MaxBeamCount, 1, mockHandle);

                PlasmaBeamArgsTelemetryJob argsJob = default;
                argsJob.States = states;
                argsJob.Scalars = scalars;
                argsJob.Args = args;
                argsJob.TelemetryRing = telemetry;
                argsJob.AcousticTaps = acousticTaps;
                argsJob.Frame = context.Frame;
                argsJob.TelemetryFrameCountValue = TelemetryFrameCount;
                JobHandle handle = argsJob.Schedule(meshHandle);

                _simulationScheduled = true;
                _simulationHandle = handle;
                _jobScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(OwnerSystemId, handle);
                scheduled = true;
                return handle;
            }
            finally
            {
                if (!scheduled)
                    ReleasePlasmaBeamJobMutationGuard();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !_simulationScheduled)
            {
                ReleasePlasmaBeamJobMutationGuard();
                return;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;

            _simulationScheduled = false;

            bool dumpNonFinite = false;
            int tapSnapshotCount = 0;
            NativeArray<PlasmaBeamTelemetryEntry> telemetryForDump = default;
            try
            {
                TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> args);
                TryResolveVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuPlasmaBeamTelemetryRing, TelemetryFrameCount, out NativeArray<PlasmaBeamTelemetryEntry> telemetry);
                TryResolveVaultBuffer(vault, in _acousticTapsHandle, BufferID.ShinobuPlasmaBeamAcousticTaps, MaxBeamCount, out NativeArray<PlasmaBeamAcousticEchoTap> taps);
                if (args.IsCreated && args.Length > 0)
                    _lastVertexCount = (int)math.min(args[0].VertexCountPerInstance, (uint)MaxVertexCount);

                int telemetryIndex = (int)(_lastDispatcherFrame % TelemetryFrameCount);
                if (telemetry.IsCreated && telemetry.Length > telemetryIndex)
                {
                    PlasmaBeamTelemetryEntry entry = telemetry[telemetryIndex];
                    entry.MeshingComputeTimeMs = ElapsedMilliseconds(_jobScheduleTimestamp);
                    telemetry[telemetryIndex] = entry;
                    telemetryForDump = telemetry;
                    _lastActiveBeamCount = (int)math.min(entry.ActiveBeams, (uint)MaxBeamCount);

                    if (entry.NonFiniteCount > 0u && !_dumpedNonFinite)
                        dumpNonFinite = true;
                }

                if (taps.IsCreated)
                {
                    int tapCount = math.min(_lastActiveBeamCount, taps.Length);
                    for (int i = 0; i < tapCount; i++)
                    {
                        PlasmaBeamAcousticEchoTap tap = taps[i];
                        if (tap.Intensity01 > 0.001f)
                            _tapPublishSnapshot[tapSnapshotCount++] = tap;
                    }
                }
            }
            finally
            {
                ReleasePlasmaBeamJobMutationGuard();
            }

            if (dumpNonFinite)
            {
                DumpTelemetry(vault, telemetryForDump);
                _dumpedNonFinite = true;
            }

            for (int i = 0; i < tapSnapshotCount; i++)
            {
                PlasmaBeamAcousticEchoTap tap = _tapPublishSnapshot[i];
                SignalBus<PlasmaBeamAcousticEchoTap>.TryPushTracked(in tap, ref s_x001ShinobuPlasmaBeamRuntimeSignalPushDropCount);
                _tapPublishSnapshot[i] = default;
            }
        }

        private void CompleteSimulationForLifecycle()
        {
            if (!_simulationScheduled)
                return;

            try
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }

                _simulationScheduled = false;
            }
            finally
            {
                ReleasePlasmaBeamJobMutationGuard();
            }
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady(vault) || !HasGraphicsResourcesReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!TryResolveVaultBuffer(vault, in _verticesHandle, BufferID.ShinobuPlasmaBeamVertices, MaxVertexCount, out NativeArray<BeamVertexDTO> vertices) ||
                !TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> args) ||
                !TryResolveVaultBuffer(vault, in _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars))
            {
                return;
            }

            int vertexCount = math.min(_lastVertexCount, math.min(vertices.Length, MaxVertexCount));
            if (vertexCount <= 0)
                return;

            GraphicsBuffer vertexWriteBuffer = (_vertexGpuUploadBufferIndex & 1) == 0
                ? _vertexGpuBufferA
                : _vertexGpuBufferB;
            GraphicsBuffer argsWriteBuffer = (_indirectArgsGpuUploadBufferIndex & 1) == 0
                ? _indirectArgsGpuBufferA
                : _indirectArgsGpuBufferB;
            if (vertexWriteBuffer == null ||
                !vertexWriteBuffer.IsValid() ||
                argsWriteBuffer == null ||
                !argsWriteBuffer.IsValid())
            {
                return;
            }

            UploadNativeArray(vertexWriteBuffer, vertices, vertexCount);
            UploadNativeArray(argsWriteBuffer, args, 1);
            _activeVertexGpuBuffer = vertexWriteBuffer;
            _activeIndirectArgsGpuBuffer = argsWriteBuffer;
            _vertexGpuUploadBufferIndex ^= 1;
            _indirectArgsGpuUploadBufferIndex ^= 1;

            PlasmaBeamRuntimeScalarsDTO scalar = scalars[0];
            if (_drawProperties == null)
                return;

            _drawProperties.Clear();
            _drawProperties.SetBuffer(VerticesBufferId, _activeVertexGpuBuffer);
            _drawProperties.SetFloat(UvScrollId, scalar.UvScrollSpeed);
            _drawProperties.SetFloat(IntensityId, math.lerp(1.15f, 4.0f, SmoothStep01(scalar.GlobalQualityWeight)));
            _drawProperties.SetFloat(QualityId, scalar.GlobalQualityWeight);
            _drawProperties.SetFloat(ScatterId, scalar.BiomeExtinction01);
            _drawProperties.SetFloat(FrameTimeId, _lastDeterministicTimeSeconds);

            UnityEngine.Graphics.DrawProceduralIndirect(
                _material,
                _drawBounds,
                MeshTopology.Triangles,
                _activeIndirectArgsGpuBuffer,
                0,
                null,
                _drawProperties,
                ShadowCastingMode.Off,
                false,
                0);
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (_vaultInitialized && _defaultsInitialized)
                return true;

            _statesHandle = vault.EnsureGenerationHandle<BeamStateDTO>(BufferID.ShinobuPlasmaBeamStates, MaxBeamCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _verticesHandle = vault.EnsureGenerationHandle<BeamVertexDTO>(BufferID.ShinobuPlasmaBeamVertices, MaxVertexCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _trigHandle = vault.EnsureGenerationHandle<BeamTrigLutEntry>(BufferID.ShinobuPlasmaBeamTrigLut, TrigLutCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _scalarsHandle = vault.EnsureGenerationHandle<PlasmaBeamRuntimeScalarsDTO>(BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _argsHandle = vault.EnsureGenerationHandle<PlasmaBeamIndirectArgsDTO>(BufferID.ShinobuPlasmaBeamIndirectArgs, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<PlasmaBeamTelemetryEntry>(BufferID.ShinobuPlasmaBeamTelemetryRing, TelemetryFrameCount, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _mockSignalsHandle = vault.EnsureGenerationHandle<MockLaserFireSignal>(BufferID.ShinobuPlasmaBeamMockSignals, MaxBeamCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _acousticTapsHandle = vault.EnsureGenerationHandle<PlasmaBeamAcousticEchoTap>(BufferID.ShinobuPlasmaBeamAcousticTaps, MaxBeamCount, OwnerSystemId, NativeArrayOptions.UninitializedMemory);

            _vaultInitialized =
                TryResolveVaultBuffer(vault, in _statesHandle, BufferID.ShinobuPlasmaBeamStates, MaxBeamCount, out NativeArray<BeamStateDTO> _) &&
                TryResolveVaultBuffer(vault, in _verticesHandle, BufferID.ShinobuPlasmaBeamVertices, MaxVertexCount, out NativeArray<BeamVertexDTO> _) &&
                TryResolveVaultBuffer(vault, in _trigHandle, BufferID.ShinobuPlasmaBeamTrigLut, TrigLutCount, out NativeArray<BeamTrigLutEntry> _) &&
                TryResolveVaultBuffer(vault, in _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, out NativeArray<PlasmaBeamRuntimeScalarsDTO> _) &&
                TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> _) &&
                TryResolveVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuPlasmaBeamTelemetryRing, TelemetryFrameCount, out NativeArray<PlasmaBeamTelemetryEntry> _) &&
                TryResolveVaultBuffer(vault, in _mockSignalsHandle, BufferID.ShinobuPlasmaBeamMockSignals, MaxBeamCount, out NativeArray<MockLaserFireSignal> _) &&
                TryResolveVaultBuffer(vault, in _acousticTapsHandle, BufferID.ShinobuPlasmaBeamAcousticTaps, MaxBeamCount, out NativeArray<PlasmaBeamAcousticEchoTap> _);

            if (!_vaultInitialized)
                return false;

            if (!_layoutChecked)
            {
                _layoutValid = IsLayoutValid();
                _layoutChecked = true;
            }

            if (!_defaultsInitialized || !_layoutValid)
                GenerateEmergencyMockBeams(vault);

            return true;
        }

        private void PrepareRuntimeStateCold()
        {
            IDataVault vault = ResolveVault();
            bool vaultReady = vault != null && EnsureVaultState(vault);
            bool graphicsReady = EnsureGraphicsResources(allowAllocation: true);
            _vaultRepairRequested = !vaultReady || !graphicsReady;
        }

        private bool HasVaultStateReady(IDataVault vault)
        {
            return vault != null &&
                _vaultInitialized &&
                _defaultsInitialized &&
                TryResolveVaultBuffer(vault, in _statesHandle, BufferID.ShinobuPlasmaBeamStates, MaxBeamCount, out NativeArray<BeamStateDTO> _) &&
                TryResolveVaultBuffer(vault, in _verticesHandle, BufferID.ShinobuPlasmaBeamVertices, MaxVertexCount, out NativeArray<BeamVertexDTO> _) &&
                TryResolveVaultBuffer(vault, in _trigHandle, BufferID.ShinobuPlasmaBeamTrigLut, TrigLutCount, out NativeArray<BeamTrigLutEntry> _) &&
                TryResolveVaultBuffer(vault, in _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, out NativeArray<PlasmaBeamRuntimeScalarsDTO> _) &&
                TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> _) &&
                TryResolveVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuPlasmaBeamTelemetryRing, TelemetryFrameCount, out NativeArray<PlasmaBeamTelemetryEntry> _) &&
                TryResolveVaultBuffer(vault, in _mockSignalsHandle, BufferID.ShinobuPlasmaBeamMockSignals, MaxBeamCount, out NativeArray<MockLaserFireSignal> _) &&
                TryResolveVaultBuffer(vault, in _acousticTapsHandle, BufferID.ShinobuPlasmaBeamAcousticTaps, MaxBeamCount, out NativeArray<PlasmaBeamAcousticEchoTap> _);
        }

        private bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsOwnedHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void GenerateEmergencyMockBeams(IDataVault vault)
        {
            if (vault == null || !vault.TryAcquireMutationGuard(PlasmaBeamJobMutationGuardMask))
                return;

            try
            {
                if (!TryResolveVaultBuffer(vault, in _statesHandle, BufferID.ShinobuPlasmaBeamStates, MaxBeamCount, out NativeArray<BeamStateDTO> states) ||
                    !TryResolveVaultBuffer(vault, in _trigHandle, BufferID.ShinobuPlasmaBeamTrigLut, TrigLutCount, out NativeArray<BeamTrigLutEntry> trig) ||
                    !TryResolveVaultBuffer(vault, in _scalarsHandle, BufferID.ShinobuPlasmaBeamRuntimeScalars, 1, out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars) ||
                    !TryResolveVaultBuffer(vault, in _argsHandle, BufferID.ShinobuPlasmaBeamIndirectArgs, 1, out NativeArray<PlasmaBeamIndirectArgsDTO> args) ||
                    !TryResolveVaultBuffer(vault, in _mockSignalsHandle, BufferID.ShinobuPlasmaBeamMockSignals, MaxBeamCount, out NativeArray<MockLaserFireSignal> mockSignals))
                {
                    return;
                }

                if (!_layoutChecked)
                {
                    _layoutValid = IsLayoutValid();
                    _layoutChecked = true;
                }

                uint layoutFlags = _layoutValid ? 0u : FlagLayoutFault;
                PlasmaBeamRuntimeScalarsDTO scalar = default;
                scalar.BaseRadius = s_pendingRadius;
                scalar.NoiseFrequency = s_pendingNoiseFrequency;
                scalar.NoiseAmplitude = s_pendingNoiseAmplitude;
                scalar.UvScrollSpeed = 9.0f;
                scalar.GlobalQualityWeight = ResolveGlobalQualityWeight();
                scalar.HeatLevel = 1.0f;
                scalar.EnergyRemaining = 1.0f;
                scalar.BiomeExtinction01 = 0.18f;
                scalar.ActiveBeamCount = 4u;
                scalar.RequestedBeamCount = 4u;
                scalar.ForcedRadialSegments = s_pendingRadialSegments;
                scalar.CsvGeneration = _csvGeneration;
                scalar.Flags = FlagMockInputEnabled | layoutFlags;
                scalar.SectorHash = 0x53363953u; // S69S
                scalars[0] = scalar;

                for (int radial = 1; radial <= MaxRadialSegments; radial++)
                {
                    for (int i = 0; i < MaxRadialSegments; i++)
                    {
                        float angle = (i % math.max(1, radial)) * (math.PI * 2.0f / math.max(1, radial));
                        MathLodApproximation.ApproxSinCosBhaskara(angle, out float sinValue, out float cosValue);
                        trig[radial * MaxRadialSegments + i] = new BeamTrigLutEntry
                        {
                            Cos = cosValue,
                            Sin = sinValue
                        };
                    }
                }

                for (int i = 0; i < mockSignals.Length; i++)
                {
                    MockLaserFireSignal signal = default;
                    signal.Start = new float3(-0.4f + i * 0.04f, 0.12f + i * 0.015f, 0.0f);
                    signal.End = new float3(1.8f + i * 0.06f, 0.15f + i * 0.02f, 4.5f + i * 0.15f);
                    signal.HeatLevel = 1.0f;
                    signal.EnergyRemaining = 1.0f;
                    signal.BeamId = (uint)i;
                    signal.NoiseSeed = HashU32((uint)i ^ 0xB34D69u);
                    signal.Flags = i < (int)scalar.RequestedBeamCount ? 1u : 0u;
                    mockSignals[i] = signal;
                    states[i] = default;
                }

                args[0] = default;
                _runtimeFlags = scalar.Flags;
                _lastVertexCount = 0;
                _lastActiveBeamCount = 0;
                _defaultsInitialized = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PlasmaBeamJobMutationGuardMask);
            }
        }

        private void ApplyQualityAndEditorTuning(IDataVault vault)
        {
            if (!TryResolveVaultBuffer(
                    vault,
                    in _scalarsHandle,
                    BufferID.ShinobuPlasmaBeamRuntimeScalars,
                    1,
                    out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars))
            {
                return;
            }

            PlasmaBeamRuntimeScalarsDTO dto = scalars[0];
            dto.GlobalQualityWeight = ResolveGlobalQualityWeight();
            dto.CsvGeneration = _csvGeneration;

            if (s_hasPendingEditorTuning)
            {
                dto.BaseRadius = s_pendingRadius;
                dto.NoiseFrequency = s_pendingNoiseFrequency;
                dto.NoiseAmplitude = s_pendingNoiseAmplitude;
                dto.ForcedRadialSegments = s_pendingRadialSegments;
                s_hasPendingEditorTuning = false;
            }

            dto.Flags = _runtimeFlags;
            scalars[0] = dto;
        }

        private bool ApplyPendingEditorTuningImmediate()
        {
            if (_simulationScheduled)
                return false;

            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultState(vault))
                return false;

            if (!vault.TryAcquireMutationGuard(PlasmaBeamJobMutationGuardMask))
                return false;
            try
            {
                ApplyQualityAndEditorTuning(vault);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PlasmaBeamJobMutationGuardMask);
            }
        }

        private bool EnsureGraphicsResources(bool allowAllocation)
        {
            if (_vertexGpuBufferA == null ||
                !_vertexGpuBufferA.IsValid() ||
                _vertexGpuBufferB == null ||
                !_vertexGpuBufferB.IsValid())
            {
                if (!allowAllocation)
                    return false;

                ReleaseGraphicsBuffer(ref _vertexGpuBufferA);
                ReleaseGraphicsBuffer(ref _vertexGpuBufferB);
                // COLD ALLOC: GraphicsBuffer[19200] - persistent procedural beam vertex stream A - owner: SHINOBU_69
                _vertexGpuBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    MaxVertexCount,
                    UnsafeUtility.SizeOf<BeamVertexDTO>());
                // COLD ALLOC: GraphicsBuffer[19200] - persistent procedural beam vertex stream B - owner: SHINOBU_69
                _vertexGpuBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    MaxVertexCount,
                    UnsafeUtility.SizeOf<BeamVertexDTO>());
                _activeVertexGpuBuffer = _vertexGpuBufferA;
                _vertexGpuUploadBufferIndex = 1;
            }

            if (_indirectArgsGpuBufferA == null ||
                !_indirectArgsGpuBufferA.IsValid() ||
                _indirectArgsGpuBufferB == null ||
                !_indirectArgsGpuBufferB.IsValid())
            {
                if (!allowAllocation)
                    return false;

                ReleaseGraphicsBuffer(ref _indirectArgsGpuBufferA);
                ReleaseGraphicsBuffer(ref _indirectArgsGpuBufferB);
                // COLD ALLOC: GraphicsBuffer[1] - persistent DrawProceduralIndirect args A - owner: SHINOBU_69
                _indirectArgsGpuBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    UnsafeUtility.SizeOf<PlasmaBeamIndirectArgsDTO>());
                // COLD ALLOC: GraphicsBuffer[1] - persistent DrawProceduralIndirect args B - owner: SHINOBU_69
                _indirectArgsGpuBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    UnsafeUtility.SizeOf<PlasmaBeamIndirectArgsDTO>());
                _activeIndirectArgsGpuBuffer = _indirectArgsGpuBufferA;
                _indirectArgsGpuUploadBufferIndex = 1;
            }

            if (_activeVertexGpuBuffer == null || !_activeVertexGpuBuffer.IsValid())
                _activeVertexGpuBuffer = _vertexGpuBufferA;
            if (_activeIndirectArgsGpuBuffer == null || !_activeIndirectArgsGpuBuffer.IsValid())
                _activeIndirectArgsGpuBuffer = _indirectArgsGpuBufferA;
            if (_activeVertexGpuBuffer == null ||
                !_activeVertexGpuBuffer.IsValid() ||
                _activeIndirectArgsGpuBuffer == null ||
                !_activeIndirectArgsGpuBuffer.IsValid())
            {
                return false;
            }

            if (_material == null)
            {
                if (!RuntimeShaderReferenceCatalog.TryGetPlasmaBeamIndirectMaterial(out _material))
                {
                    _runtimeFlags |= FlagShaderMissing;
                    return false;
                }
            }

            EnsureDrawPropertiesCold();
            return true;
        }

        private void EnsureDrawPropertiesCold()
        {
            if (_drawProperties != null)
                return;

            _drawProperties = new MaterialPropertyBlock();
        }

        private bool HasGraphicsResourcesReady()
        {
            return _vertexGpuBufferA != null &&
                _vertexGpuBufferA.IsValid() &&
                _vertexGpuBufferA.count == MaxVertexCount &&
                _vertexGpuBufferA.stride == UnsafeUtility.SizeOf<BeamVertexDTO>() &&
                _vertexGpuBufferB != null &&
                _vertexGpuBufferB.IsValid() &&
                _vertexGpuBufferB.count == MaxVertexCount &&
                _vertexGpuBufferB.stride == UnsafeUtility.SizeOf<BeamVertexDTO>() &&
                _indirectArgsGpuBufferA != null &&
                _indirectArgsGpuBufferA.IsValid() &&
                _indirectArgsGpuBufferA.count == 1 &&
                _indirectArgsGpuBufferA.stride == UnsafeUtility.SizeOf<PlasmaBeamIndirectArgsDTO>() &&
                _indirectArgsGpuBufferB != null &&
                _indirectArgsGpuBufferB.IsValid() &&
                _indirectArgsGpuBufferB.count == 1 &&
                _indirectArgsGpuBufferB.stride == UnsafeUtility.SizeOf<PlasmaBeamIndirectArgsDTO>() &&
                _activeVertexGpuBuffer != null &&
                _activeVertexGpuBuffer.IsValid() &&
                _activeIndirectArgsGpuBuffer != null &&
                _activeIndirectArgsGpuBuffer.IsValid();
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseGraphicsBuffer(ref _vertexGpuBufferA);
            ReleaseGraphicsBuffer(ref _vertexGpuBufferB);
            _activeVertexGpuBuffer = null;
            _vertexGpuUploadBufferIndex = 0;

            ReleaseGraphicsBuffer(ref _indirectArgsGpuBufferA);
            ReleaseGraphicsBuffer(ref _indirectArgsGpuBufferB);
            _activeIndirectArgsGpuBuffer = null;
            _indirectArgsGpuUploadBufferIndex = 0;

            if (_drawProperties != null)
                _drawProperties.Clear();
            _material = null;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private bool TryAcquirePlasmaBeamJobMutationGuard(IDataVault vault)
        {
            if (_activeJobMutationGuardMask != 0UL)
                return true;

            if (vault == null || !vault.TryAcquireMutationGuard(PlasmaBeamJobMutationGuardMask))
                return false;

            _activeJobMutationGuardMask = PlasmaBeamJobMutationGuardMask;
            _activeJobMutationGuardVault = vault;
            return true;
        }

        private void ReleasePlasmaBeamJobMutationGuard()
        {
            ulong guardMask = _activeJobMutationGuardMask;
            IDataVault vault = _activeJobMutationGuardVault;
            if (guardMask != 0UL)
                vault?.ReleaseMutationGuard(guardMask);
            _activeJobMutationGuardMask = 0UL;
            _activeJobMutationGuardVault = null;
        }

#if UNITY_EDITOR
        private void MonitorBeamCsv(IDataVault vault)
        {
            if (!File.Exists(_csvPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (ticks == _csvLastWriteTicks)
                return;

            _csvLastWriteTicks = ticks;
            if (!TryResolveVaultBuffer(
                    vault,
                    in _scalarsHandle,
                    BufferID.ShinobuPlasmaBeamRuntimeScalars,
                    1,
                    out NativeArray<PlasmaBeamRuntimeScalarsDTO> scalars))
            {
                return;
            }

            PlasmaBeamRuntimeScalarsDTO parsed = scalars[0];
            Span<byte> scratch = stackalloc byte[CsvScratchBytes];
            int read;
            using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long cappedLength = math.min(stream.Length, (long)CsvScratchBytes);
                int maxBytes = (int)math.max(0L, cappedLength);
                read = stream.Read(scratch.Slice(0, maxBytes));
            }

            if (!TryParseBeamCsv(scratch, read, ref parsed))
                return;

            if (!vault.TryAcquireMutationGuard(PlasmaBeamJobMutationGuardMask))
                return;

            try
            {
                if (!TryResolveVaultBuffer(
                        vault,
                        in _scalarsHandle,
                        BufferID.ShinobuPlasmaBeamRuntimeScalars,
                        1,
                        out scalars))
                {
                    return;
                }

                _csvGeneration++;
                parsed.CsvGeneration = _csvGeneration;
                parsed.Flags |= FlagCsvLoaded;
                _runtimeFlags = parsed.Flags;
                scalars[0] = parsed;
            }
            finally
            {
                vault.ReleaseMutationGuard(PlasmaBeamJobMutationGuardMask);
            }
        }

        private static bool TryParseBeamCsv(ReadOnlySpan<byte> scratch, int length, ref PlasmaBeamRuntimeScalarsDTO dto)
        {
            if (length <= 0)
                return false;

            length = math.min(length, scratch.Length);
            int i = 0;
            while (i < length)
            {
                while (i < length && (scratch[i] == (byte)'\r' || scratch[i] == (byte)'\n' || scratch[i] == (byte)' ' || scratch[i] == (byte)'\t'))
                    i++;

                if (i >= length)
                    break;

                if (scratch[i] == (byte)'#')
                {
                    while (i < length && scratch[i] != (byte)'\n')
                        i++;
                    continue;
                }

                uint keyHash = 2166136261u;
                while (i < length && scratch[i] != (byte)',' && scratch[i] != (byte)'=' && scratch[i] != (byte)'\n' && scratch[i] != (byte)'\r')
                {
                    byte b = scratch[i];
                    if (b >= (byte)'A' && b <= (byte)'Z')
                        b = (byte)(b + 32);
                    if (b != (byte)' ' && b != (byte)'\t')
                    {
                        keyHash ^= b;
                        keyHash *= 16777619u;
                    }

                    i++;
                }

                if (i < length && (scratch[i] == (byte)',' || scratch[i] == (byte)'='))
                    i++;

                float value = ParseFloat(scratch, ref i, length);
                ApplyCsvValue(ref dto, keyHash, value);

                while (i < length && scratch[i] != (byte)'\n')
                    i++;
            }

            return true;
        }
#endif

        private static void ApplyCsvValue(ref PlasmaBeamRuntimeScalarsDTO dto, uint keyHash, float value)
        {
            float safe = SanitizeFloat(value, 0.0f);
            switch (keyHash)
            {
                case HashRadius:
                    dto.BaseRadius = math.clamp(safe, 0.002f, 0.4f);
                    break;
                case HashNoiseFrequency:
                    dto.NoiseFrequency = math.clamp(safe, 0.0f, 48.0f);
                    break;
                case HashNoiseAmplitude:
                    dto.NoiseAmplitude = math.clamp(safe, 0.0f, 0.35f);
                    break;
                case HashHeat:
                    dto.HeatLevel = math.saturate(safe);
                    break;
                case HashEnergy:
                    dto.EnergyRemaining = math.saturate(safe);
                    break;
                case HashQualityWeight:
                    dto.GlobalQualityWeight = math.saturate(safe);
                    break;
                case HashRadialSegments:
                    dto.ForcedRadialSegments = safe > 0.0f ? (uint)math.clamp((int)math.round(safe), MinRadialSegments, MaxRadialSegments) : 0u;
                    break;
                case HashBiomeExtinction:
                    dto.BiomeExtinction01 = math.saturate(safe);
                    break;
                case HashRequestedBeams:
                    dto.RequestedBeamCount = (uint)math.clamp((int)math.round(safe), 0, MaxBeamCount);
                    break;
            }
        }

        private static float ParseFloat(ReadOnlySpan<byte> scratch, ref int index, int length)
        {
            while (index < length && (scratch[index] == (byte)' ' || scratch[index] == (byte)'\t'))
                index++;

            float sign = 1.0f;
            if (index < length && scratch[index] == (byte)'-')
            {
                sign = -1.0f;
                index++;
            }

            float value = 0.0f;
            while (index < length && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
            {
                value = value * 10.0f + (scratch[index] - (byte)'0');
                index++;
            }

            if (index < length && scratch[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < length && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
                {
                    value += (scratch[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            return sign * value;
        }

        private static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count)
            where T : unmanaged
        {
            if (destination == null || !destination.IsValid() || !source.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, math.min(source.Length, destination.count));
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
            }
            finally
            {
                destination.UnlockBufferAfterWrite<T>(safeCount);
            }
        }

        private void DumpTelemetry(IDataVault vault, NativeArray<PlasmaBeamTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int headerBytes = UnsafeUtility.SizeOf<PlasmaBeamDumpHeader>();
                int entryBytes = UnsafeUtility.SizeOf<PlasmaBeamTelemetryEntry>();
                int count = math.min(telemetry.Length, TelemetryFrameCount);
                int byteCount = headerBytes + (count * entryBytes);
                PlasmaBeamDumpHeader header = default;
                header.Magic = DumpMagic;
                header.Version = DumpVersion;
                header.FrameCount = (uint)count;
                header.EntrySize = (uint)entryBytes;
                header.Flags = _runtimeFlags;

                // STATIC_SOURCE_REVIEWED: no owner-local dump scratch buffer exists here; use the
                // central transient payload route so the Temp dump allocation is labeled/tracked.
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    DumpPayloadOwner,
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(payloadPtr, &header, headerBytes);
                if (count > 0)
                {
                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(payloadPtr + headerBytes, telemetryPtr, (long)count * entryBytes);
                }

                if (!NativeFaultDumpWriter.TryWriteAll(_dumpPath, payload, byteCount))
                    _runtimeFlags |= FlagDumpFailed;
            }
            catch (Exception)
            {
                _runtimeFlags |= FlagDumpFailed;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    DumpPayloadOwner,
                    DumpPayloadLabel);
            }
        }

        private static bool IsLayoutValid()
        {
            return UnsafeUtility.SizeOf<BeamVertexDTO>() == 32 &&
                UnsafeUtility.SizeOf<BeamStateDTO>() == 128 &&
                UnsafeUtility.SizeOf<BeamTrigLutEntry>() == 8 &&
                UnsafeUtility.SizeOf<PlasmaBeamRuntimeScalarsDTO>() == 64 &&
                UnsafeUtility.SizeOf<MockLaserFireSignal>() == 64 &&
                UnsafeUtility.SizeOf<PlasmaBeamAcousticEchoTap>() == 32 &&
                UnsafeUtility.SizeOf<PlasmaBeamIndirectArgsDTO>() == 16 &&
                UnsafeUtility.SizeOf<PlasmaBeamTelemetryEntry>() == 64;
        }

        private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = SanitizeFloat(timing.FixedDelta, 0.0f);
            return fixedDelta > 0.00001f ? math.clamp(fixedDelta, 1.0f / 240.0f, 1.0f / 5.0f) : 1.0f / 60.0f;
        }

        private static float ResolveGlobalQualityWeight()
        {
            return math.saturate(SanitizeFloat(HomeostasisBrain.GlobalQualityWeight, 1.0f));
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - 2.0f * t);
        }

        private static uint HashU32(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        private static float ElapsedMilliseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0.0f;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double ms = (double)delta * 1000.0 / (double)System.Diagnostics.Stopwatch.Frequency;
            return math.isfinite((float)ms) ? (float)ms : 0.0f;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuPlasmaBeamRuntime _owner;

            public PreSimulationPhaseSystem(ShinobuPlasmaBeamRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53365052u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuPlasmaBeamRuntime _owner;

            public SimulationPhaseSystem(ShinobuPlasmaBeamRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53365349u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuPlasmaBeamRuntime _owner;

            public PostSimulationPhaseSystem(ShinobuPlasmaBeamRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x5336504Fu; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuPlasmaBeamRuntime _owner;

            public VisualSyncPhaseSystem(ShinobuPlasmaBeamRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53365649u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlasmaBeamMockLaserFireJob : IJob
    {
        [NoAlias] public NativeArray<BeamStateDTO> States;
        [NoAlias] public NativeArray<MockLaserFireSignal> MockSignals;
        [NoAlias] public NativeArray<PlasmaBeamRuntimeScalarsDTO> Scalars;
        public uint Frame;
        public uint SystemHash;
        public float TimeSeconds;
        public uint MockEnabled;

        public void Execute()
        {
            if (!States.IsCreated || !MockSignals.IsCreated || !Scalars.IsCreated || Scalars.Length == 0)
                return;

            PlasmaBeamRuntimeScalarsDTO scalar = Scalars[0];
            int activeCount = math.clamp((int)scalar.RequestedBeamCount, 0, math.min(ShinobuPlasmaBeamRuntime.MaxBeamCount, States.Length));
            float q = math.saturate(scalar.GlobalQualityWeight);
            float heat = math.saturate(scalar.HeatLevel);
            float energy = math.saturate(scalar.EnergyRemaining);
            uint nonFinite = 0u;

            if (MockEnabled != 0u)
            {
                uint seed = math.max(1u, scalar.SectorHash ^ SystemHash ^ (Frame * 747796405u) ^ 0x4C415345u);
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
                for (int i = 0; i < MockSignals.Length; i++)
                {
                    float lane = (float)i - (float)(activeCount - 1) * 0.5f;
                    float3 start = new float3(lane * 0.08f, 0.08f + lane * 0.015f, 0.0f);
                    float3 end = start + new float3(
                        random.NextFloat(-0.08f, 0.08f),
                        random.NextFloat(-0.04f, 0.10f),
                        random.NextFloat(3.6f, 7.5f));

                    MockLaserFireSignal signal = default;
                    signal.Start = start;
                    signal.End = end;
                    signal.HeatLevel = heat;
                    signal.EnergyRemaining = energy;
                    signal.BeamId = (uint)i;
                    signal.NoiseSeed = HashU32(seed ^ (uint)i * 2654435761u);
                    signal.Flags = i < activeCount ? 1u : 0u;
                    MockSignals[i] = signal;
                }
            }

            for (int i = 0; i < States.Length; i++)
            {
                MockLaserFireSignal signal = i < MockSignals.Length ? MockSignals[i] : default;
                float3 start = signal.Start;
                float3 end = signal.End;
                float3 delta = end - start;
                bool finite = math.all(math.isfinite(start)) && math.all(math.isfinite(end)) && math.lengthsq(delta) > 0.000001f;
                uint flags = signal.Flags;
                if (!finite)
                {
                    flags |= 1u << 30;
                    nonFinite++;
                    start = float3.zero;
                    end = new float3(0.0f, 0.0f, 1.0f);
                }

                BeamStateDTO state = default;
                state.ToolAup = new double3(start.x, start.y, start.z);
                state.TargetAup = new double3(end.x, end.y, end.z);
                state.CameraAup = double3.zero;
                state.Radius = math.max(0.001f, scalar.BaseRadius);
                state.HeatLevel = math.saturate(signal.HeatLevel);
                state.EnergyRemaining = math.saturate(signal.EnergyRemaining);
                state.NoiseFrequency = math.max(0.0f, scalar.NoiseFrequency);
                state.NoiseAmplitude = math.max(0.0f, scalar.NoiseAmplitude);
                state.GlobalQualityWeight = q;
                state.TimeSeconds = TimeSeconds;
                state.BiomeExtinction01 = math.saturate(scalar.BiomeExtinction01);
                state.ColorPacked = LerpPackedRgba(0xF2FFB826u, 0xF2A8B36Fu, state.BiomeExtinction01);
                state.BiomeHash = 0x42504C41u;
                state.BeamId = signal.BeamId;
                state.NoiseSeed = signal.NoiseSeed;
                state.Flags = flags;
                States[i] = state;
            }

            scalar.ActiveBeamCount = (uint)activeCount;
            scalar.NonFiniteCount = nonFinite;
            Scalars[0] = scalar;
        }

        private static uint HashU32(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        private static uint LerpPackedRgba(uint a, uint b, float t)
        {
            float blend = math.saturate(t);
            uint ar = a & 255u;
            uint ag = (a >> 8) & 255u;
            uint ab = (a >> 16) & 255u;
            uint aa = (a >> 24) & 255u;
            uint br = b & 255u;
            uint bg = (b >> 8) & 255u;
            uint bb = (b >> 16) & 255u;
            uint ba = (b >> 24) & 255u;
            uint rr = (uint)math.round(math.lerp(ar, br, blend));
            uint rg = (uint)math.round(math.lerp(ag, bg, blend));
            uint rb = (uint)math.round(math.lerp(ab, bb, blend));
            uint ra = (uint)math.round(math.lerp(aa, ba, blend));
            return rr | (rg << 8) | (rb << 16) | (ra << 24);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlasmaBeamTubeMeshingJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<BeamStateDTO> States;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<BeamVertexDTO> Vertices;
        [NoAlias] public NativeArray<BeamTrigLutEntry> TrigLut;
        [NoAlias] public NativeArray<PlasmaBeamRuntimeScalarsDTO> Scalars;
        public int MaxRadialSegmentsValue;
        public int MaxVerticesPerBeamValue;
        public uint Frame;

        public void Execute(int beamIndex)
        {
            if (!States.IsCreated || !Vertices.IsCreated || !TrigLut.IsCreated || !Scalars.IsCreated || Scalars.Length == 0)
                return;

            PlasmaBeamRuntimeScalarsDTO scalar = Scalars[0];
            int activeCount = math.clamp((int)scalar.ActiveBeamCount, 0, math.min(States.Length, ShinobuPlasmaBeamRuntime.MaxBeamCount));
            if (beamIndex >= activeCount)
                return;

            float q = math.saturate(scalar.GlobalQualityWeight);
            float qCurve = ResolveLengthQualityCurve(q);
            int lengthSegments = math.clamp((int)math.round(math.lerp(
                ShinobuPlasmaBeamRuntime.MinLengthSegments,
                ShinobuPlasmaBeamRuntime.MaxLengthSegments,
                qCurve)), ShinobuPlasmaBeamRuntime.MinLengthSegments, ShinobuPlasmaBeamRuntime.MaxLengthSegments);
            float radialCurve = SmoothStep01(q);
            int radialSegments = scalar.ForcedRadialSegments > 0u
                ? math.clamp((int)scalar.ForcedRadialSegments, ShinobuPlasmaBeamRuntime.MinRadialSegments, ShinobuPlasmaBeamRuntime.MaxRadialSegments)
                : math.clamp((int)math.round(math.lerp(
                    ShinobuPlasmaBeamRuntime.MinRadialSegments,
                    ShinobuPlasmaBeamRuntime.MaxRadialSegments,
                    radialCurve)), ShinobuPlasmaBeamRuntime.MinRadialSegments, ShinobuPlasmaBeamRuntime.MaxRadialSegments);

            int verticesPerBeam = lengthSegments * radialSegments * 6;
            int beamBase = beamIndex * verticesPerBeam;
            if (beamBase < 0 || beamBase + verticesPerBeam > Vertices.Length)
                return;

            BeamStateDTO state = States[beamIndex];
            float3 start = ToFloat3(state.ToolAup - state.CameraAup);
            float3 end = start + ToFloat3(state.TargetAup - state.ToolAup);
            float3 delta = end - start;
            bool finite = math.all(math.isfinite(start)) && math.all(math.isfinite(end));
            float lengthSq = math.lengthsq(delta);
            if (!finite || lengthSq <= 0.000001f)
            {
                state.Flags |= 1u << 30;
                States[beamIndex] = state;
                start = float3.zero;
                end = new float3(0.0f, 0.0f, 1.0f);
                delta = end - start;
                lengthSq = 1.0f;
            }

            float length = math.sqrt(math.max(lengthSq, 0.000001f));
            float3 forward = delta * math.rsqrt(math.max(lengthSq, 0.000001f));
            float3 reference = math.select(new float3(0.0f, 1.0f, 0.0f), new float3(1.0f, 0.0f, 0.0f), math.abs(forward.y) > 0.92f);
            float3 right = SafeNormalize(math.cross(reference, forward), new float3(1.0f, 0.0f, 0.0f));
            float3 up = SafeNormalize(math.cross(forward, right), new float3(0.0f, 1.0f, 0.0f));

            int write = beamBase;
            for (int segment = 0; segment < lengthSegments; segment++)
            {
                int nextSegment = segment + 1;
                for (int radial = 0; radial < radialSegments; radial++)
                {
                    int nextRadial = radial + 1;
                    if (nextRadial >= radialSegments)
                        nextRadial = 0;

                    WriteVertex(ref write, state, start, forward, right, up, length, segment, radial, lengthSegments, radialSegments, q);
                    WriteVertex(ref write, state, start, forward, right, up, length, nextSegment, radial, lengthSegments, radialSegments, q);
                    WriteVertex(ref write, state, start, forward, right, up, length, nextSegment, nextRadial, lengthSegments, radialSegments, q);
                    WriteVertex(ref write, state, start, forward, right, up, length, segment, radial, lengthSegments, radialSegments, q);
                    WriteVertex(ref write, state, start, forward, right, up, length, nextSegment, nextRadial, lengthSegments, radialSegments, q);
                    WriteVertex(ref write, state, start, forward, right, up, length, segment, nextRadial, lengthSegments, radialSegments, q);
                }
            }
        }

        private void WriteVertex(
            ref int writeIndex,
            BeamStateDTO state,
            float3 start,
            float3 forward,
            float3 right,
            float3 up,
            float length,
            int segment,
            int radial,
            int lengthSegments,
            int radialSegments,
            float quality)
        {
            float lengthRatio = (float)segment / math.max(1.0f, (float)lengthSegments);
            int trigIndex = radialSegments * MaxRadialSegmentsValue + radial;
            BeamTrigLutEntry trig = TrigLut[trigIndex];
            float3 radialVector = right * trig.Cos + up * trig.Sin;
            float heatScalar = math.saturate(state.HeatLevel) * math.saturate(state.EnergyRemaining);
            float baseRadius = math.max(0.001f, state.Radius) * math.lerp(0.18f, 1.0f, heatScalar);
            float finalRing = segment == lengthSegments ? 1.0f : 0.0f;
            float flare = math.lerp(1.0f, 1.5f, finalRing);
            float noiseWeight = SmoothStep01(math.saturate((quality - 0.30f) * 1.4285715f));
            float offset = 0.0f;
            if (noiseWeight > 0.0001f)
            {
                float seed = (state.NoiseSeed & 1023u) * (1.0f / 1023.0f);
                float phase = lengthRatio * math.max(0.01f, state.NoiseFrequency) + state.TimeSeconds * 2.1f + seed * 19.0f;
                float wave = math.frac(phase + seed + radial * 0.173f);
                float signedTriangle = (math.abs(wave * 2.0f - 1.0f) * 2.0f) - 1.0f;
                offset = signedTriangle * state.NoiseAmplitude * noiseWeight;
            }

            float radius = math.max(0.001f, baseRadius * flare + offset);
            float3 position = start + forward * (length * lengthRatio) + radialVector * radius;
            if (!math.all(math.isfinite(position)))
                position = start;

            BeamVertexDTO vertex = default;
            vertex.Position = position;
            vertex.ColorPacked = finalRing > 0.5f ? 0xFFFFFFFFu : state.ColorPacked;
            vertex.UV = new float2((float)radial / math.max(1.0f, (float)radialSegments), lengthRatio);
            vertex._pad0 = 0UL;
            Vertices[writeIndex] = vertex;
            writeIndex++;
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return lenSq > 0.000001f && math.all(math.isfinite(value)) ? value * math.rsqrt(math.max(lenSq, 0.000001f)) : fallback;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - 2.0f * t);
        }

        private static float ResolveLengthQualityCurve(float quality)
        {
            return SmoothStep01(math.saturate((quality - 0.30f) * 1.4285715f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlasmaBeamArgsTelemetryJob : IJob
    {
        [NoAlias] public NativeArray<BeamStateDTO> States;
        [NoAlias] public NativeArray<PlasmaBeamRuntimeScalarsDTO> Scalars;
        [NoAlias] public NativeArray<PlasmaBeamIndirectArgsDTO> Args;
        [NoAlias] public NativeArray<PlasmaBeamTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<PlasmaBeamAcousticEchoTap> AcousticTaps;
        public uint Frame;
        public int TelemetryFrameCountValue;

        public void Execute()
        {
            if (!States.IsCreated || !Scalars.IsCreated || Scalars.Length == 0 || !Args.IsCreated || Args.Length == 0)
                return;

            PlasmaBeamRuntimeScalarsDTO scalar = Scalars[0];
            float q = math.saturate(scalar.GlobalQualityWeight);
            float qCurve = ResolveLengthQualityCurve(q);
            int activeCount = math.clamp((int)scalar.ActiveBeamCount, 0, math.min(States.Length, ShinobuPlasmaBeamRuntime.MaxBeamCount));
            int lengthSegments = math.clamp((int)math.round(math.lerp(
                ShinobuPlasmaBeamRuntime.MinLengthSegments,
                ShinobuPlasmaBeamRuntime.MaxLengthSegments,
                qCurve)), ShinobuPlasmaBeamRuntime.MinLengthSegments, ShinobuPlasmaBeamRuntime.MaxLengthSegments);
            float radialCurve = SmoothStep01(q);
            int radialSegments = scalar.ForcedRadialSegments > 0u
                ? math.clamp((int)scalar.ForcedRadialSegments, ShinobuPlasmaBeamRuntime.MinRadialSegments, ShinobuPlasmaBeamRuntime.MaxRadialSegments)
                : math.clamp((int)math.round(math.lerp(
                    ShinobuPlasmaBeamRuntime.MinRadialSegments,
                    ShinobuPlasmaBeamRuntime.MaxRadialSegments,
                    radialCurve)), ShinobuPlasmaBeamRuntime.MinRadialSegments, ShinobuPlasmaBeamRuntime.MaxRadialSegments);
            uint verticesPerBeam = (uint)(lengthSegments * radialSegments * 6);
            uint totalVertices = verticesPerBeam * (uint)activeCount;

            Args[0] = new PlasmaBeamIndirectArgsDTO
            {
                VertexCountPerInstance = totalVertices,
                InstanceCount = totalVertices > 0u ? 1u : 0u,
                StartVertex = 0u,
                StartInstance = 0u
            };

            uint nonFinite = scalar.NonFiniteCount;
            uint stateHash = 2166136261u;
            float maxNoise = 0.0f;
            float maxLength = 0.0f;
            for (int i = 0; i < activeCount; i++)
            {
                BeamStateDTO state = States[i];
                float3 start = ToFloat3(state.ToolAup - state.CameraAup);
                float3 end = start + ToFloat3(state.TargetAup - state.ToolAup);
                float3 delta = end - start;
                bool finite = math.all(math.isfinite(start)) && math.all(math.isfinite(end)) && math.lengthsq(delta) > 0.000001f;
                nonFinite += finite ? 0u : 1u;
                maxNoise = math.max(maxNoise, math.max(0.0f, state.NoiseAmplitude));
                float length = math.sqrt(math.max(0.0f, math.lengthsq(delta)));
                maxLength = math.max(maxLength, length);
                stateHash = HashMix(stateHash, state.BeamId);
                stateHash = HashMix(stateHash, state.NoiseSeed);
                stateHash = HashMix(stateHash, math.asuint(length));

                if (AcousticTaps.IsCreated && i < AcousticTaps.Length)
                {
                    PlasmaBeamAcousticEchoTap tap = default;
                    tap.Position = end;
                    tap.Intensity01 = math.saturate(state.NoiseAmplitude * math.lerp(4.0f, 22.0f, q) * math.saturate(state.HeatLevel));
                    tap.BeamId = state.BeamId;
                    tap.Frame = Frame;
                    tap.NoiseSeed = state.NoiseSeed;
                    tap.Flags = state.Flags;
                    AcousticTaps[i] = tap;
                }
            }

            scalar.NonFiniteCount = nonFinite;
            scalar.Flags = nonFinite > 0u ? (scalar.Flags | (1u << 30)) : (scalar.Flags & ~(1u << 30));
            Scalars[0] = scalar;

            if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
            {
                int index = (int)(Frame % (uint)math.max(1, math.min(TelemetryRing.Length, TelemetryFrameCountValue)));
                PlasmaBeamTelemetryEntry entry = default;
                entry.Frame = Frame;
                entry.ActiveBeams = (uint)activeCount;
                entry.VerticesGenerated = totalVertices;
                entry.NonFiniteCount = nonFinite;
                entry.MeshingComputeTimeMs = totalVertices * 0.000003f;
                entry.GlobalQualityWeight = q;
                entry.MaxNoiseAmplitude = maxNoise;
                entry.MaxBeamLength = maxLength;
                entry.StateHash = stateHash;
                entry.LengthSegments = (uint)lengthSegments;
                entry.RadialSegments = (uint)radialSegments;
                entry.Flags = scalar.Flags;
                TelemetryRing[index] = entry;
            }
        }

        private static uint HashMix(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - 2.0f * t);
        }

        private static float ResolveLengthQualityCurve(float quality)
        {
            return SmoothStep01(math.saturate((quality - 0.30f) * 1.4285715f));
        }
    }
}
