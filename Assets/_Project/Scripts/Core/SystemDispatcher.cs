using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Narrative;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.Visor;
using Hecton8.VFX;
using Hecton8.World;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public readonly struct CriticalMemoryPressureEvent
    {
        public readonly double UsageRatio;
        public readonly long ReservedMemoryBytes;
        public readonly long PhysicalMemoryBytes;
        public readonly int Frame;
        private readonly int _pad0;

        public CriticalMemoryPressureEvent(
            int frame,
            long reservedMemoryBytes,
            long physicalMemoryBytes,
            double usageRatio)
        {
            UsageRatio = usageRatio;
            Frame = frame;
            ReservedMemoryBytes = reservedMemoryBytes;
            PhysicalMemoryBytes = physicalMemoryBytes;
            _pad0 = 0;
        }
    }

    /// <summary>
    /// Priority-lane dispatcher for registry-managed <see cref="IUpdatable"/> systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9950)]
    public sealed class SystemDispatcher : MonoBehaviour, ITickDispatcher, IServiceHeartbeat, IServiceShutdown
    {
        private const int LaneCount = 4;
        private const double FastTickIntervalSeconds = 1.0 / 60.0;
        private const double SlowTickIntervalSeconds = 0.1;
        private const double ThermalCriticalSlowTickIntervalSeconds = 0.2;
        private const double ColdTickIntervalSeconds = 1.0;
        private const double FrostTickIntervalSeconds = 5.0;
        private const int MaxCadenceSubstepsPerFrame = 4;
        private const float TimeDilationMinimumScalar = 0f;
        private const float TimeDilationMaximumScalar = 4f;
        private const float HeadlessTimeDilationMaximumScalar = 100f;
        private const float TimeDilationPausedEpsilon = 0.0001f;
        private const float BulletTimePostScalarThreshold = 0.98f;
        private const double SlowJobCompleteWarningMilliseconds = 1.0;
        private const double SlowDispatcherPhaseWarningMilliseconds = 100.0;
        private const float JobAdmissionFrameBudgetMissThresholdSeconds = 1.0f / 60.0f;
        private const int MaxQueuedDispatcherRaycasts = 1024;
        private const int DispatcherRaycastMinCommandsPerJob = 1;
        private const int DispatcherBlackBoxFrameCount = 300;
        private const int DispatcherBlackBoxEntrySizeBytes = 64;
        private const string DispatcherBlackBoxDumpPath = "Docs/AgentLogs/Dump_CORE_TICK_DILATION.bin";
        private const string DispatcherBlackBoxMirrorDumpPath = "Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin";
        private const int MasterDispatcherMaxSystems = 85;
        private const int MasterDispatcherBucketCount = 64;
        private const int MasterDispatcherBucketMask = MasterDispatcherBucketCount - 1;
        private const int MasterDispatcherBlackBoxFrameCount = 300;
        private const int MasterDispatcherSignalLaneCount = 33;
        private const int MasterDispatcherDependencyScratchCapacity = 8;
        private const float MasterDispatcherStallDumpThresholdMs = 8f;
        private const string MasterDispatcherDumpPath = "Docs/AgentLogs/Dump_SYSTEM_DISPATCHER.bin";
        private const string MasterDispatcherPriorityCsvPath = "Docs/Tasks/execution_priorities.csv";
        private const ulong DispatcherBlackBoxDumpMagic = 0x00384E4F54434548ul; // HECTON8\0
        private const uint DispatcherBlackBoxDumpVersion = 1u;
        private const uint MasterDispatcherDumpVersion = 1u;
        private const int CameraJuiceResolveRetryFrames = 30;
        private const float AdrenalineHealthThreshold01 = 0.1f;
        private const float AdrenalineTargetTimeDilationScalar = 0.5f;
        private const float AdrenalineRampSeconds = 1.0f;
        private const float AdrenalineInvRampSeconds = 1.0f / AdrenalineRampSeconds;
        private const uint AdrenalineDilationReasonHash = 0x41445245u; // ADRE
        private const double FixedStepSeconds = 0.02;
        private const int MaxFixedSubstepsPerFrame = 3;
        private const int MaxLateFrameEventsPerFrame = 1000;
        private const int MaxPdaEventsPerFrame = 30;
        private const int PdaCongestionWarningFrameThreshold = 5;
        private const int LateFrameCircuitBreakerLaneCapacity = 32;
        private const int BaseStressCascadeCircuitBreakerCapacity = 64;
        private const int MaxBaseStressCascadeEventsPerIslandPerFrame = 16;
        private const int ArteryFlushSampleCapacity = 64;
        private const double LateFrameEventFlushBudgetMilliseconds = 2.0;
        private const double LateFrameFlushPassSpikeMilliseconds = 0.5;
        private const float PauseDepthOfFieldBlendSeconds = 0.2f;
        private const float VisualStaticGlitchDurationSeconds = 1f;
        private const float SafeGcCollectFrameBudgetSeconds = 0.014f;
        private const double HomeostasisEmergencySlowTickIntervalSeconds = 1.0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float AupNanInquisitorLogIntervalSeconds = 5f;
        private const float DispatcherPhaseWarningLogIntervalSeconds = 5f;
        private const string HeapLockGuardMessage = "[SystemDispatcher] HEAP LOCK GUARD: managed heap increased during fixed-step dispatch.";
#endif
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.Dispatcher.Update");
        private static readonly ProfilerMarker _fastTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.FastTick");
        private static readonly ProfilerMarker _fixedUpdateProfilerMarker = new ProfilerMarker("H8.Dispatcher.FixedUpdate");
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.SlowTick");
        private static readonly ProfilerMarker _coldTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.ColdTick");
        private static readonly ProfilerMarker _frostTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.FrostTick");
        private static readonly ProfilerMarker _memoryDefragProfilerMarker = new ProfilerMarker("H8.Dispatcher.MemoryDefrag.PreSimulation");
        private static readonly ProfilerMarker _lateFrameProfilerMarker = new ProfilerMarker("H8.Dispatcher.LateFrame");
        private static readonly ProfilerMarker _postFixedProfilerMarker = new ProfilerMarker("H8.Dispatcher.PostFixed");
        private static readonly ProfilerMarker _lateFrameCommandQueueDrainProfilerMarker = new ProfilerMarker("H8.Dispatcher.CommandQueue.Drain");
        private static readonly ProfilerMarker _foveatedCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Foveated.Complete");
        private static readonly ProfilerMarker _dispatcherRaycastScheduleProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Schedule");
        private static readonly ProfilerMarker _dispatcherRaycastCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Complete");
        private static readonly ProfilerMarker _masterPreSimulationProfilerMarker = new ProfilerMarker("H8.Dispatcher.Master.PreSimulation");
        private static readonly ProfilerMarker _masterSimulationProfilerMarker = new ProfilerMarker("H8.Dispatcher.Master.Simulation");
        private static readonly ProfilerMarker _masterPostSimulationProfilerMarker = new ProfilerMarker("H8.Dispatcher.Master.PostSimulation");
        private static readonly ProfilerMarker _masterVisualSyncProfilerMarker = new ProfilerMarker("H8.Dispatcher.Master.VisualSync");
        private const uint _ThreadSafeCommandQueueHash = 2371163900u;
        private const uint _StorageReservationCommitResolvedQueueHash = 1402202258u;
        private const uint _ModCommandDispatcherQueueHash = 1692095755u;
        private const uint _CoreEventsArteryHash = 1115213285u;
        private const uint _EnvironmentEventsArteryHash = 907195043u;
        private const uint _PlayerEventsArteryHash = 4083807397u;
        private const uint _BaseEventsArteryHash = 1825517483u;
        private const uint _AIEventsArteryHash = 2440840446u;
        private const uint _PdaEventsQueueHash = 3608173543u;
        private const uint _LateFrameTickablesQueueHash = 1655194628u;
        private const uint _LateFrameBudgetWarningHash = 3118918745u;
        private const uint _AmbientEventsDropHash = 3299023854u;
        private const uint _CriticalPerformanceSpikeHash = 3729248491u;
        private const uint _DataVaultDefragContextHash = 0xDADA7048u;
        private const uint _HeapFragmentationRatioHash = 0xF9A60001u;
        private const uint _DataVaultMovedBytesHash = 0xDADA7049u;
        private const uint _DataVaultWatchdogHash = 0xDADA7050u;
        private const uint _DataVaultMassiveMoveHash = 0xDADA7051u;
        private const uint _DataVaultVramPressureHash = 0xDADA7052u;
        private const uint _SystemDispatcherHash = 0x51D15A7Cu;
        private const uint _PlayerLoopInstallFailureHash = 0x51D10001u;
        private const uint _HeapLockGuardHash = 0x51D10002u;
        private const uint _AupNanInquisitorHash = 0x51D10003u;
        private const uint _DispatcherBlackBoxFaultHash = 0x51D10004u;
        private const uint _BaseStressCascadeBreakerHash = 3838237614u;
        private const uint _SimulationBucketContextHash = 0x53424B54u; // SBKT
        private const uint _FramePacingWarningHash = 0x4650574Eu; // FPWN
        private const ushort DispatcherBlackBoxFlagPaused = 1 << 0;
        private const ushort DispatcherBlackBoxFlagAupBarrier = 1 << 1;
        private const ushort DispatcherBlackBoxFlagOriginShiftLock = 1 << 2;
        private const ushort DispatcherBlackBoxFlagNonFinite = 1 << 3;
        private const ushort DispatcherBlackBoxFlagCoreDilation = 1 << 4;
        private const ushort DispatcherBlackBoxFlagTemporalCompression = 1 << 5;
        private const ushort DispatcherBlackBoxFlagLowTier = 1 << 6;
        private const ushort DispatcherBlackBoxFlagAdrenalineDilation = 1 << 7;
        private const byte AdrenalineDilationPhaseNone = 0;
        private const byte AdrenalineDilationPhaseRampDown = 1;
        private const byte AdrenalineDilationPhaseHold = 2;
        private const byte AdrenalineDilationPhaseRestore = 3;
        private const ulong _FramePacingEmergencyKillMask =
            (ulong)SystemBit.NonCriticalVfx |
            (ulong)SystemBit.ParticleAdvection |
            (ulong)SystemBit.DistantFaunaSteering |
            (ulong)SystemBit.SlowTick2Hz |
            (ulong)SystemBit.TimeDilation08;
        private static readonly int _HectonFreezeFrameDitherId = Shader.PropertyToID("_HectonFreezeFrameDither");
        private static readonly int _GamePausedId = Shader.PropertyToID("_GamePaused");
        private static readonly int _HectonVisualStaticGlitchId = Shader.PropertyToID("_HectonVisualStaticGlitch");
        private static readonly int _HectonVisualStaticGlitchSeedId = Shader.PropertyToID("_HectonVisualStaticGlitchSeed");
        private static readonly int _SimulationBucketInterpolationAlphaId = Shader.PropertyToID("_SimulationBucketInterpolationAlpha");
        private static readonly float[] _arteryFlushMilliseconds = new float[ArteryFlushSampleCapacity];
        private static int _arteryFlushSampleCursor;
        private static readonly ProfilerMarker[] _updateLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Update.Core"),
            new ProfilerMarker("H8.Dispatcher.Update.Environment"),
            new ProfilerMarker("H8.Dispatcher.Update.Player"),
            new ProfilerMarker("H8.Dispatcher.Update.UI"),
        };
        private static readonly ProfilerMarker[] _fixedLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Fixed.Core"),
            new ProfilerMarker("H8.Dispatcher.Fixed.Environment"),
            new ProfilerMarker("H8.Dispatcher.Fixed.Player"),
            new ProfilerMarker("H8.Dispatcher.Fixed.UI"),
        };
        private static readonly ProfilerMarker[] _fastLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Fast.Core"),
            new ProfilerMarker("H8.Dispatcher.Fast.Environment"),
            new ProfilerMarker("H8.Dispatcher.Fast.Player"),
            new ProfilerMarker("H8.Dispatcher.Fast.UI"),
        };
        private static readonly ProfilerMarker[] _slowLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Slow.Core"),
            new ProfilerMarker("H8.Dispatcher.Slow.Environment"),
            new ProfilerMarker("H8.Dispatcher.Slow.Player"),
            new ProfilerMarker("H8.Dispatcher.Slow.UI"),
        };
        private static readonly ProfilerMarker[] _coldLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Cold.Core"),
            new ProfilerMarker("H8.Dispatcher.Cold.Environment"),
            new ProfilerMarker("H8.Dispatcher.Cold.Player"),
            new ProfilerMarker("H8.Dispatcher.Cold.UI"),
        };
        private static readonly ProfilerMarker[] _frostLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Frost.Core"),
            new ProfilerMarker("H8.Dispatcher.Frost.Environment"),
            new ProfilerMarker("H8.Dispatcher.Frost.Player"),
            new ProfilerMarker("H8.Dispatcher.Frost.UI"),
        };
        private static readonly ProfilerMarker[] _unscaledFastLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Core"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Environment"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Player"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.UI"),
        };

        // COLD ALLOC: RegistryBucket<IUpdatable>[4] - fixed dispatcher lanes ordered by bootstrap layer - owner: SystemDispatcher
        private static readonly RegistryBucket<IUpdatable>[] _priorityLanes =
        {
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(128),
            new RegistryBucket<IUpdatable>(64),
        };
        private static readonly RegistryBucket<IFastTickable>[] _fastPriorityLanes =
        {
            new RegistryBucket<IFastTickable>(128),
            new RegistryBucket<IFastTickable>(128),
            new RegistryBucket<IFastTickable>(96),
            new RegistryBucket<IFastTickable>(32),
        };
        private static readonly RegistryBucket<IFixedTickable>[] _fixedPriorityLanes =
        {
            new RegistryBucket<IFixedTickable>(128),
            new RegistryBucket<IFixedTickable>(128),
            new RegistryBucket<IFixedTickable>(96),
            new RegistryBucket<IFixedTickable>(32),
        };
        private static readonly RegistryBucket<ISlowTickable>[] _slowPriorityLanes =
        {
            new RegistryBucket<ISlowTickable>(128),
            new RegistryBucket<ISlowTickable>(128),
            new RegistryBucket<ISlowTickable>(96),
            new RegistryBucket<ISlowTickable>(32),
        };
        private static int _bucketedSlowTickableCount;
        private static readonly RegistryBucket<IColdTickable>[] _coldPriorityLanes =
        {
            new RegistryBucket<IColdTickable>(64),
            new RegistryBucket<IColdTickable>(64),
            new RegistryBucket<IColdTickable>(48),
            new RegistryBucket<IColdTickable>(16),
        };
        private static readonly RegistryBucket<IFrostTickable>[] _frostPriorityLanes =
        {
            new RegistryBucket<IFrostTickable>(48),
            new RegistryBucket<IFrostTickable>(48),
            new RegistryBucket<IFrostTickable>(24),
            new RegistryBucket<IFrostTickable>(8),
        };
        private static readonly RegistryBucket<ILateFrameTickable>[] _lateFramePriorityLanes =
        {
            new RegistryBucket<ILateFrameTickable>(128),
            new RegistryBucket<ILateFrameTickable>(128),
            new RegistryBucket<ILateFrameTickable>(96),
            new RegistryBucket<ILateFrameTickable>(32),
        };
        private static readonly RegistryBucket<IPostFixedTickable>[] _postFixedPriorityLanes =
        {
            new RegistryBucket<IPostFixedTickable>(128),
            new RegistryBucket<IPostFixedTickable>(128),
            new RegistryBucket<IPostFixedTickable>(96),
            new RegistryBucket<IPostFixedTickable>(32),
        };
        private static readonly RegistryBucket<IUnscaledFastTickable>[] _unscaledFastPriorityLanes =
        {
            new RegistryBucket<IUnscaledFastTickable>(64),
            new RegistryBucket<IUnscaledFastTickable>(64),
            new RegistryBucket<IUnscaledFastTickable>(48),
            new RegistryBucket<IUnscaledFastTickable>(32),
        };

        // COLD ALLOC: IDispatcherSystem[85] - GlobalRegistry-facing master dispatcher registrations - owner: SystemDispatcher
        private static readonly IDispatcherSystem[] _masterRegisteredSystems = new IDispatcherSystem[MasterDispatcherMaxSystems];
        // COLD ALLOC: IDispatcherSystem[85] - Kahn-sorted execution order - owner: SystemDispatcher
        private static readonly IDispatcherSystem[] _masterSortedSystems = new IDispatcherSystem[MasterDispatcherMaxSystems];
        // COLD ALLOC: IDispatcherFixedSystem[8] - fixed-only bridge registrations - owner: SystemDispatcher
        private static readonly IDispatcherFixedSystem[] _masterFixedSystems = new IDispatcherFixedSystem[8];
        // COLD ALLOC: int[85] - Kahn in-degree scratch - owner: SystemDispatcher
        private static readonly int[] _masterKahnInDegrees = new int[MasterDispatcherMaxSystems];
        // COLD ALLOC: int[85] - Kahn queue scratch - owner: SystemDispatcher
        private static readonly int[] _masterKahnQueue = new int[MasterDispatcherMaxSystems];
        // COLD ALLOC: int[85] - stable reverse lookup scratch - owner: SystemDispatcher
        private static readonly int[] _masterDependencySystemIndices = new int[MasterDispatcherMaxSystems];
        // COLD ALLOC: bool[85] - fault-disabled master systems - owner: SystemDispatcher
        private static readonly bool[] _masterSystemDisabled = new bool[MasterDispatcherMaxSystems];
        // COLD ALLOC: uint[64] - visible bucket load counters - owner: SystemDispatcher
        private static readonly uint[] _masterBucketLoadCounters = new uint[MasterDispatcherBucketCount];
        // COLD ALLOC: uint[85] - CSV priority hash scratch - owner: SystemDispatcher
        private static readonly uint[] _masterCsvSystemHashes = new uint[MasterDispatcherMaxSystems];
        // COLD ALLOC: int[85] - CSV priority rank scratch - owner: SystemDispatcher
        private static readonly int[] _masterCsvSystemPriorities = new int[MasterDispatcherMaxSystems];
        // COLD ALLOC: float[4] - editor phase timing snapshot - owner: SystemDispatcher
        private static readonly float[] _masterPhaseTimingSnapshotMs = new float[4];
        // COLD ALLOC: uint[33] - deterministic late signal-lane facade hashes - owner: SystemDispatcher
        private static readonly uint[] _masterSignalLaneHashes =
        {
            0x53494700u, 0x53494701u, 0x53494702u, 0x53494703u, 0x53494704u, 0x53494705u,
            0x53494706u, 0x53494707u, 0x53494708u, 0x53494709u, 0x5349470Au, 0x5349470Bu,
            0x5349470Cu, 0x5349470Du, 0x5349470Eu, 0x5349470Fu, 0x53494710u, 0x53494711u,
            0x53494712u, 0x53494713u, 0x53494714u, 0x53494715u, 0x53494716u, 0x53494717u,
            0x53494718u, 0x53494719u, 0x5349471Au, 0x5349471Bu, 0x5349471Cu, 0x5349471Du,
            0x5349471Eu, 0x5349471Fu, 0x53494720u
        };
        private static int _masterRegisteredSystemCount;
        private static int _masterSortedSystemCount;
        private static int _masterFixedSystemCount;
        private static bool _masterTopologyDirty = true;
        private static bool _masterTopologyValid;
        private static bool _masterEmergencyTopologyInstalled;

        private static IFoveatedDispatcher _foveatedSimulationManager = new FoveatedSimulationManager();
        private static long _homeostasisKillSwitchMaskBits;
        private static int _homeostasisPressureLevel;
        private static int _homeostasisSlowTick2Hz;
        private static int _homeostasisFoveatedTier;
        private static IModdingBridge _moddingBridgeProjectionRuntime;
        // COLD ALLOC: IDispatcherRaycastReceiver[256] - dispatcher-owned pending raycast receivers - owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _pendingDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] - dispatcher-owned pending raycast request ids - owner: SystemDispatcher
        private static readonly int[] _pendingDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: IDispatcherRaycastReceiver[256] - dispatcher-owned scheduled raycast receivers - owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _scheduledDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] - dispatcher-owned scheduled raycast request ids - owner: SystemDispatcher
        private static readonly int[] _scheduledDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: uint[32] - late-frame circuit-breaker lane hash counters - owner: SystemDispatcher
        private static readonly uint[] _lateFrameCircuitBreakerLaneHashes = new uint[LateFrameCircuitBreakerLaneCapacity];
        // COLD ALLOC: ushort[32] - late-frame circuit-breaker lane hit counters - owner: SystemDispatcher
        private static readonly ushort[] _lateFrameCircuitBreakerLaneCounts = new ushort[LateFrameCircuitBreakerLaneCapacity];
        // COLD ALLOC: int[64] - per-frame BaseEvents IslandID cascade breaker keys - owner: SystemDispatcher
        private static readonly int[] _baseStressCascadeIslandIds = new int[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: int[64] - lazy frame stamps for BaseEvents cascade breaker slots - owner: SystemDispatcher
        private static readonly int[] _baseStressCascadeSlotFrames = new int[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: ushort[64] - per-frame BaseEvents IslandID cascade event counts - owner: SystemDispatcher
        private static readonly ushort[] _baseStressCascadeIslandCounts = new ushort[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: ushort[64] - per-frame BaseEvents IslandID dropped event counts - owner: SystemDispatcher
        private static readonly ushort[] _baseStressCascadeDroppedCounts = new ushort[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: bool[64] - single telemetry gate per IslandID per frame - owner: SystemDispatcher
        private static readonly bool[] _baseStressCascadeTelemetryEmitted = new bool[BaseStressCascadeCircuitBreakerCapacity];
        private VaultBufferHandle<double> _h8TimeHandle;
        private VaultBufferHandle<DispatcherBlackBoxEntry> _dispatcherBlackBoxHandle;
        private VaultBufferHandle<int> _dispatcherBlackBoxCursorHandle;
        private VaultBufferHandle<JobHandle> _masterSimulationJobHandlesHandle;
        private VaultBufferHandle<JobHandle> _masterDependencyScratchHandlesHandle;
        private VaultBufferHandle<JobDependencyDTO> _masterJobDependencyTelemetryHandle;
        private VaultBufferHandle<DispatcherPipelineTelemetryEntry> _masterPipelineTelemetryRingHandle;
        private VaultBufferHandle<int> _masterPipelineTelemetryCursorHandle;
        private VaultBufferHandle<MockTimeDilationSignal> _masterMockTimeDilationSignalsHandle;
        private double _fastTickAccumulator;
        private double _slowTickAccumulator;
        private double _coldTickAccumulator;
        private double _frostTickAccumulator;
        private double _memoryDefragAccumulator;
        private double _unscaledFastTickAccumulator;
        private double _fixedStepAccumulator;
        private IDataVault _dataVault;
        private ISimulationBucketer _simulationBucketer;
        private IJobAdmissionService _jobAdmission;
        private IInputDeterminismService _inputDeterminism;
        private VRAMMonitor _vramMonitor;
        private VRAMPressureMonitor _vramPressure;
        private IMacroDatabaseService _macroDatabase;
        private ObjectPoolManager _objectPool;
        private byte _scalabilityTierProfileByte;
        private float _timeDilationScalar = 1f;
        private float _prePauseTimeDilationScalar = 1f;
        private float _coreTickDilationScalar = 1f;
        private float _coreTickDilationRestoreScalar = 1f;
        private float _adrenalineDilationStartScalar = 1f;
        private float _adrenalineDilationRestoreScalar = 1f;
        private float _adrenalineDilationElapsedSeconds;
        private int _coreTickDilationFramesRemaining;
        private uint _coreTickDilationReasonHash;
        private bool _simulationPaused;
        private bool _thermalCriticalSlowTickActive;
        private uint _timeDilationSequence;
        private uint _lastPublishedTimeDilationSequence;
        private uint _aupPreShiftPauseSequence;
        private int _aupPreShiftPauseFrame = -1;
        private uint _adrenalineDilationSourceHash;
        private byte _adrenalineDilationPhase;
        private bool _coreTickDilationRestorePending;
        private H8TimeSnapshot _timeSnapshot;
        private uint _dispatcherBlackBoxSequence;
        private DispatcherStateDTO _dispatcherState;
        private JobHandle _masterSimulationCombinedHandle;
        private JobHandle _masterFixedCombinedHandle;
        private long _masterFrameStartTimestamp;
        private long _masterPreSimulationStartTimestamp;
        private long _masterPostSimulationStartTimestamp;
        private long _masterVisualSyncStartTimestamp;
        private float _masterLastPreSimulationMs;
        private float _masterLastSimWaitMs;
        private float _masterLastPostSimulationMs;
        private float _masterLastVisualSyncMs;
        private uint _masterFrameTelemetrySequence;
        private int _masterPendingSimulationJobCount;
        private int _masterPendingFixedJobCount;
        private int _masterDisabledSystemCount;
        private int _masterCsvPollFrame = -1;
        private DateTime _masterPriorityCsvLastWriteUtc;
        private bool _dispatcherBlackBoxDumped;
        private bool _masterSimulationJobsPending;
        private bool _masterFixedJobsPending;
        private bool _masterPipelineTelemetryDumped;
        private bool _masterVisualSyncShedThisFrame;
        private bool _serviceRegistered;
        private int _lastMemoryDefragPressureWarningFrame = -1;
        private static int _lateFrameEventDispatchBudget;
        private static bool _lateFrameEventBudgetActive;
        private static bool _lateFrameCircuitBreakerTripped;
        private static bool _lateFrameTimeBudgetExhausted;
        private static uint _activeLateFrameEventLaneHash;
        private static uint _dominantLateFrameCircuitBreakerLaneHash;
        private static long _lateFrameEventBudgetStartTimestamp;
        private static bool _criticalPerformanceSpikeReported;
        private static bool _pauseFreezeFrameDitherActive;
        private static int _droppedEventsCounter;
        private static bool _visualStaticGlitchActive;
        private static float _visualStaticGlitchUntilTime;
        private static int _baseStressCascadeBreakerFrame = -1;
        private static int _baseStressCascadeTableOverflowTelemetryFrame = -1;
        private static int _lastFramePacingWarningFrame = -1;
        private static int _lastFramePacingHomeostasisFrame = -1;
        private static int _originShiftBootstrapLockCount;
        private static int _originShiftFrameLockFrame = -1;
        private static int _criticalMemoryPressureDefragRequested;
        private static int _streamingStorageDebtMilli;
        private static int _streamingStorageDebtSequence;
        private static bool _dispatcherPlayerLoopInstalled;
        private static IDataVault _cachedDispatcherDataVault;
        private static ICameraJuiceSystem _cachedCameraJuiceSystem;
        private static int _nextCameraJuiceResolveFrame;

        internal static float CurrentFrameDeltaTime { get; private set; }

        internal static float CurrentFrameUnscaledDeltaTime { get; private set; }

        internal static float CurrentFixedInterpolationAlpha { get; private set; }

        internal static SystemDispatcher ActiveRuntimeInstance { get; private set; }

        internal static bool IsOriginShiftBootstrapLocked => Volatile.Read(ref _originShiftBootstrapLockCount) > 0;

        internal static bool IsOriginShiftFrameLockedForCurrentFrame => Volatile.Read(ref _originShiftFrameLockFrame) == Time.frameCount;

        public float TimeDilationScalar => _timeDilationScalar;

        public static ulong KillSwitchMask => unchecked((ulong)Volatile.Read(ref _homeostasisKillSwitchMaskBits));

        internal static byte HomeostasisPressureLevel => (byte)Volatile.Read(ref _homeostasisPressureLevel);

        internal static byte HomeostasisFoveatedTier => (byte)Volatile.Read(ref _homeostasisFoveatedTier);

        public bool SimulationPaused => _simulationPaused || _timeDilationScalar <= TimeDilationPausedEpsilon;

        public double DilatedTimeSeconds => _timeSnapshot.Time;

        public double UnscaledTimeSeconds => _timeSnapshot.UnscaledTime;

        public H8TimeSnapshot TimeSnapshot => _timeSnapshot;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static object _currentPostFixedGcOwner;
        private static object _lastPostFixedGcOwner;
        private static int _lastPostFixedGcFrame = -1;
        private static int _lastPostFixedGcDelta;
        private static int _lastPostFixedGcLaneIndex = -1;
        private static int _lastPostFixedGcItemIndex = -1;
        private static float _nextAupNanInquisitorLogTime;
        private static float _nextDispatcherPhaseWarningLogTime;
        private static float _nextFoveatedFrameWarningLogTime;
#endif
        private static ushort _dominantLateFrameCircuitBreakerLaneCount;
        private static float _pauseDepthOfFieldWeight;
        private static float _pauseDepthOfFieldBlendStartTime;
        private static float _pauseDepthOfFieldBlendStartWeight;
        private static float _pauseDepthOfFieldTargetWeight;
        private static bool _temporalCompressionActive;
        private static bool _pauseMenuDepthOfFieldRequested;
        private static bool _pdaDepthOfFieldRequested;
        private static bool _pauseDepthOfFieldTargetActive;
        private static int _temporalCompressionFrameCount;
        private static int _pdaOverBudgetConsecutiveFrames;
        private static VaultBufferHandle<RaycastCommand> _pendingDispatcherRaycastCommandsHandle;
        private static VaultBufferHandle<RaycastCommand> _scheduledDispatcherRaycastCommandsHandle;
        private static VaultBufferHandle<RaycastHit> _scheduledDispatcherRaycastHitsHandle;
        private static bool _scheduledDispatcherRaycastCommandsVaultLocked;
        private static bool _scheduledDispatcherRaycastHitsVaultLocked;
        private static JobHandle _scheduledDispatcherRaycastHandle;
        private static bool _dispatcherRaycastsScheduled;
        private static int _pendingDispatcherRaycastCount;
        private static int _scheduledDispatcherRaycastCount;

        [StructLayout(LayoutKind.Sequential, Size = DispatcherBlackBoxEntrySizeBytes)]
        private struct DispatcherBlackBoxEntry
        {
            public uint Frame;
            public uint Sequence;
            public double DilatedTime;
            public double UnscaledTime;
            public float DeltaTime;
            public float UnscaledDeltaTime;
            public float TimeDilationScalar;
            public float TickOverheadMilliseconds;
            public ushort Flags;
            public ushort PendingRaycasts;
            public ushort ScheduledRaycasts;
            public byte HomeostasisPressureLevel;
            public byte HomeostasisFoveatedTier;
            public uint AupPreShiftSequence;
            public uint StateHash;
            public ulong KillSwitchMask;
        }

        static SystemDispatcher()
        {
            _foveatedSimulationManager.InitializeRuntime();
            ResetBaseStressCascadeCircuitBreakerState();
        }

        /// <summary>
        /// True when this frame dropped excess fixed-step catch-up time instead of exceeding the substep cap.
        /// </summary>
        public static bool IsTemporalCompressionActive => _temporalCompressionActive;

        /// <summary>
        /// Total frame count where temporal compression was entered since subsystem reset.
        /// </summary>
        public static int TemporalCompressionFrameCount => _temporalCompressionFrameCount;

        public static float StreamingStorageDebt01 => math.saturate(Volatile.Read(ref _streamingStorageDebtMilli) * 0.001f);

        public static uint StreamingStorageDebtSequence => unchecked((uint)Volatile.Read(ref _streamingStorageDebtSequence));

        public static double CurrentUnscaledTimeSeconds
        {
            get
            {
                SystemDispatcher dispatcher = ActiveRuntimeInstance;
                return dispatcher != null ? dispatcher._timeSnapshot.UnscaledTime : Time.unscaledTimeAsDouble;
            }
        }

        public static void PublishStreamingStorageDebt(float debt01)
        {
            Volatile.Write(ref _streamingStorageDebtMilli, (int)math.round(math.saturate(debt01) * 1000f));
            Volatile.Write(ref _streamingStorageDebtSequence, unchecked(Volatile.Read(ref _streamingStorageDebtSequence) + 1));
        }

        internal static void SetModdingBridgeProjectionRuntime(IModdingBridge bridge)
        {
            if (bridge != null)
                _moddingBridgeProjectionRuntime = bridge;
        }

        internal static void ClearModdingBridgeProjectionRuntime(IModdingBridge bridge)
        {
            if (ReferenceEquals(_moddingBridgeProjectionRuntime, bridge))
                _moddingBridgeProjectionRuntime = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool TryGetLastPostFixedGcAttribution(
            out string ownerName,
            out int frame,
            out int delta,
            out int laneIndex,
            out int itemIndex)
        {
            object owner = _lastPostFixedGcOwner ?? _currentPostFixedGcOwner;
            if (owner == null)
            {
                ownerName = "UNATTRIBUTED_POSTFIXED_SYSTEM";
                frame = _lastPostFixedGcFrame;
                delta = _lastPostFixedGcDelta;
                laneIndex = _lastPostFixedGcLaneIndex;
                itemIndex = _lastPostFixedGcItemIndex;
                return false;
            }

            ownerName = owner.GetType().Name;
            frame = _lastPostFixedGcFrame;
            delta = _lastPostFixedGcDelta;
            laneIndex = _lastPostFixedGcLaneIndex;
            itemIndex = _lastPostFixedGcItemIndex;
            return true;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HomeostasisBrain.ShutdownRuntime();
            _foveatedSimulationManager.Dispose();
            _foveatedSimulationManager = new FoveatedSimulationManager();
            _foveatedSimulationManager.InitializeRuntime();
            DisposeDispatcherRaycastBuffers();
            ThreadSafeCommandQueue.Shutdown();
            ClearAllLanes();
            _lateFrameEventDispatchBudget = 0;
            _lateFrameEventBudgetActive = false;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _activeLateFrameEventLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneHash = 0u;
            _lateFrameEventBudgetStartTimestamp = 0L;
            _dominantLateFrameCircuitBreakerLaneCount = 0;
            _arteryFlushSampleCursor = 0;
            System.Array.Clear(_lateFrameCircuitBreakerLaneHashes, 0, _lateFrameCircuitBreakerLaneHashes.Length);
            System.Array.Clear(_lateFrameCircuitBreakerLaneCounts, 0, _lateFrameCircuitBreakerLaneCounts.Length);
            ResetBaseStressCascadeCircuitBreakerState();
            System.Array.Clear(_arteryFlushMilliseconds, 0, _arteryFlushMilliseconds.Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _nextAupNanInquisitorLogTime = 0f;
            _nextDispatcherPhaseWarningLogTime = 0f;
            _nextFoveatedFrameWarningLogTime = 0f;
#endif
            _pauseDepthOfFieldWeight = 0f;
            _pauseDepthOfFieldBlendStartTime = 0f;
            _pauseDepthOfFieldBlendStartWeight = 0f;
            _pauseDepthOfFieldTargetWeight = 0f;
            _pauseMenuDepthOfFieldRequested = false;
            _pdaDepthOfFieldRequested = false;
            _pauseDepthOfFieldTargetActive = false;
            _pauseFreezeFrameDitherActive = false;
            _droppedEventsCounter = 0;
            _visualStaticGlitchActive = false;
            _visualStaticGlitchUntilTime = 0f;
            Volatile.Write(ref _streamingStorageDebtMilli, 0);
            Volatile.Write(ref _streamingStorageDebtSequence, 0);
            Volatile.Write(ref _originShiftBootstrapLockCount, 0);
            Volatile.Write(ref _originShiftFrameLockFrame, -1);
            _lastFramePacingWarningFrame = -1;
            _lastFramePacingHomeostasisFrame = -1;
            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, 0f);
            Shader.SetGlobalFloat(_GamePausedId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, 0f);
            Shader.SetGlobalFloat(_SimulationBucketInterpolationAlphaId, 0f);
            _criticalPerformanceSpikeReported = false;
            _temporalCompressionActive = false;
            _temporalCompressionFrameCount = 0;
            _pdaOverBudgetConsecutiveFrames = 0;
            _moddingBridgeProjectionRuntime = null;
            _dispatcherPlayerLoopInstalled = false;
            _cachedDispatcherDataVault = null;
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, 0L);
            Volatile.Write(ref _homeostasisPressureLevel, 0);
            Volatile.Write(ref _homeostasisSlowTick2Hz, 0);
            Volatile.Write(ref _homeostasisFoveatedTier, 0);
            ActiveRuntimeInstance = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _currentPostFixedGcOwner = null;
            _lastPostFixedGcOwner = null;
            _lastPostFixedGcFrame = -1;
            _lastPostFixedGcDelta = 0;
            _lastPostFixedGcLaneIndex = -1;
            _lastPostFixedGcItemIndex = -1;
#endif
        }

        private struct DispatcherUpdatePlayerLoopNode
        {
        }

        private struct DispatcherLateFramePlayerLoopNode
        {
        }

        private static void EnsureDispatcherPlayerLoopInstalled()
        {
            if (_dispatcherPlayerLoopInstalled)
                return;

            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            bool updateReady = TryEnsurePlayerLoopNode(
                ref root,
                typeof(UnityEngine.PlayerLoop.Update),
                typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                typeof(DispatcherUpdatePlayerLoopNode),
                RunDispatcherUpdateFromPlayerLoop);
            bool lateReady = TryEnsurePlayerLoopNode(
                ref root,
                typeof(UnityEngine.PlayerLoop.PreLateUpdate),
                typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
                typeof(DispatcherLateFramePlayerLoopNode),
                RunDispatcherLateFrameFromPlayerLoop);

            if (updateReady && lateReady)
            {
                PlayerLoop.SetPlayerLoop(root);
                _dispatcherPlayerLoopInstalled = true;
                return;
            }

            PublishDispatcherComplianceViolation(_PlayerLoopInstallFailureHash, _SystemDispatcherHash, 4, 0);
            GlobalTelemetryBus.PublishPerformanceWarning(_PlayerLoopInstallFailureHash, _SystemDispatcherHash, 1f);
        }

        private static bool TryEnsurePlayerLoopNode(
            ref PlayerLoopSystem root,
            System.Type parentType,
            System.Type beforeType,
            System.Type nodeType,
            PlayerLoopSystem.UpdateFunction updateFunction)
        {
            if (ContainsPlayerLoopNode(root, nodeType))
                return true;

            PlayerLoopSystem node = new PlayerLoopSystem
            {
                type = nodeType,
                updateDelegate = updateFunction
            };
            return TryInsertPlayerLoopNode(ref root, parentType, beforeType, in node);
        }

        private static bool ContainsPlayerLoopNode(PlayerLoopSystem system, System.Type nodeType)
        {
            if (system.type == nodeType)
                return true;

            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (ContainsPlayerLoopNode(children[i], nodeType))
                    return true;
            }

            return false;
        }

        private static bool TryInsertPlayerLoopNode(
            ref PlayerLoopSystem system,
            System.Type parentType,
            System.Type beforeType,
            in PlayerLoopSystem node)
        {
            if (system.type == parentType)
            {
                InsertPlayerLoopNodeBefore(ref system, beforeType, in node);
                return true;
            }

            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                PlayerLoopSystem child = children[i];
                if (!TryInsertPlayerLoopNode(ref child, parentType, beforeType, in node))
                    continue;

                children[i] = child;
                system.subSystemList = children;
                return true;
            }

            return false;
        }

        private static void InsertPlayerLoopNodeBefore(
            ref PlayerLoopSystem parent,
            System.Type beforeType,
            in PlayerLoopSystem node)
        {
            PlayerLoopSystem[] children = parent.subSystemList;
            if (children == null || children.Length == 0)
            {
                parent.subSystemList = new[] { node };
                return;
            }

            int insertIndex = children.Length;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type != beforeType)
                    continue;

                insertIndex = i;
                break;
            }

            PlayerLoopSystem[] expanded = new PlayerLoopSystem[children.Length + 1];
            for (int i = 0; i < insertIndex; i++)
                expanded[i] = children[i];

            expanded[insertIndex] = node;
            for (int i = insertIndex; i < children.Length; i++)
                expanded[i + 1] = children[i];

            parent.subSystemList = expanded;
        }

        private static void RunDispatcherUpdateFromPlayerLoop()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null || !dispatcher._serviceRegistered || !dispatcher.isActiveAndEnabled)
                return;

            dispatcher.RunDispatcherUpdate();
        }

        private static void RunDispatcherLateFrameFromPlayerLoop()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null || !dispatcher._serviceRegistered || !dispatcher.isActiveAndEnabled)
                return;

            dispatcher.RunDispatcherLateFrame();
        }

        private static void PublishDispatcherComplianceViolation(uint ruleHash, uint contextHash, byte severity, byte flags)
        {
            ComplianceViolationSignal signal = default;
            signal.RuleHash = ruleHash;
            signal.SystemHash = _SystemDispatcherHash;
            signal.ContextHash = contextHash;
            signal.Frame = unchecked((uint)math.max(0, Time.frameCount));
            signal.Severity = severity;
            signal.Flags = flags;
            GlobalSignals.Publish(in signal);
        }

        internal static void ApplyHomeostasisKillSwitch(
            ulong mask,
            byte pressureLevel,
            byte foveatedTier,
            bool slowTick2Hz,
            bool forceTimeDilation09,
            uint reasonHash)
        {
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, unchecked((long)mask));
            Volatile.Write(ref _homeostasisPressureLevel, pressureLevel);
            Volatile.Write(ref _homeostasisSlowTick2Hz, slowTick2Hz ? 1 : 0);
            Volatile.Write(ref _homeostasisFoveatedTier, foveatedTier);
            _foveatedSimulationManager.ApplyHomeostasisPressureTier(foveatedTier);

            if (forceTimeDilation09 && ActiveRuntimeInstance != null)
                ActiveRuntimeInstance.RequestCoreTickDilation(0.9f, 2, reasonHash);
        }

        /// <summary>
        /// Drops same-frame habitat damage/stress cascades once one IslandID exceeds the hard budget.
        /// </summary>
        /// <param name="islandId">Logistics component IslandID. Negative values are coerced to zero.</param>
        /// <param name="eventHash">Stable event hash for telemetry context.</param>
        /// <returns>True when the caller may process this cascade event.</returns>
        internal static bool TryConsumeBaseStressCascadeEvent(int islandId, uint eventHash)
        {
            int currentFrame = Time.frameCount;
            if (_baseStressCascadeBreakerFrame != currentFrame)
                _baseStressCascadeBreakerFrame = currentFrame;

            int safeIslandId = math.max(0, islandId);
            int slot = ResolveBaseStressCascadeSlot(safeIslandId);
            if (slot < 0)
            {
                if (_baseStressCascadeTableOverflowTelemetryFrame != currentFrame)
                {
                    _baseStressCascadeTableOverflowTelemetryFrame = currentFrame;
                    CrashTelemetryBuffer.ReportEventCascadeWarning();
                    GlobalTelemetryBus.PublishCatastrophicCascadePrevented(
                        unchecked((uint)safeIslandId),
                        eventHash != 0u ? eventHash : _BaseStressCascadeBreakerHash,
                        1);
                }

                return false;
            }

            ushort currentCount = _baseStressCascadeIslandCounts[slot];
            if (currentCount < MaxBaseStressCascadeEventsPerIslandPerFrame)
            {
                _baseStressCascadeIslandCounts[slot] = (ushort)(currentCount + 1);
                return true;
            }

            ushort droppedCount = _baseStressCascadeDroppedCounts[slot];
            if (droppedCount < ushort.MaxValue)
                droppedCount++;

            _baseStressCascadeDroppedCounts[slot] = droppedCount;
            if (!_baseStressCascadeTelemetryEmitted[slot])
            {
                _baseStressCascadeTelemetryEmitted[slot] = true;
                CrashTelemetryBuffer.ReportEventCascadeWarning();
                GlobalTelemetryBus.PublishCatastrophicCascadePrevented(
                    unchecked((uint)safeIslandId),
                    eventHash != 0u ? eventHash : _BaseStressCascadeBreakerHash,
                    droppedCount);
            }

            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void ResetBaseStressCascadeCircuitBreakerForSmokeTest()
        {
            ResetBaseStressCascadeCircuitBreakerStateForFrame(Time.frameCount);
        }

        internal static int DebugGetBaseStressCascadeDroppedCount(int islandId)
        {
            int safeIslandId = math.max(0, islandId);
            int slot = FindBaseStressCascadeSlot(safeIslandId);
            return slot >= 0 ? _baseStressCascadeDroppedCounts[slot] : 0;
        }

        internal static int DebugGetBaseStressCascadeConsumedCount(int islandId)
        {
            int safeIslandId = math.max(0, islandId);
            int slot = FindBaseStressCascadeSlot(safeIslandId);
            return slot >= 0 ? _baseStressCascadeIslandCounts[slot] : 0;
        }

        internal static int DebugGetBaseStressCascadeActiveSlotCount()
        {
            int activeSlotCount = 0;
            for (int i = 0; i < BaseStressCascadeCircuitBreakerCapacity; i++)
            {
                if (_baseStressCascadeSlotFrames[i] == _baseStressCascadeBreakerFrame)
                    activeSlotCount++;
            }

            return activeSlotCount;
        }
#endif

        private static void ResetBaseStressCascadeCircuitBreakerState()
        {
            ResetBaseStressCascadeCircuitBreakerStateForFrame(-1);
        }

        private static void ResetBaseStressCascadeCircuitBreakerStateForFrame(int frame)
        {
            _baseStressCascadeBreakerFrame = frame;
            _baseStressCascadeTableOverflowTelemetryFrame = -1;
            for (int i = 0; i < BaseStressCascadeCircuitBreakerCapacity; i++)
            {
                _baseStressCascadeIslandIds[i] = int.MinValue;
                _baseStressCascadeSlotFrames[i] = int.MinValue;
                _baseStressCascadeIslandCounts[i] = 0;
                _baseStressCascadeDroppedCounts[i] = 0;
                _baseStressCascadeTelemetryEmitted[i] = false;
            }
        }

        private static int ResolveBaseStressCascadeSlot(int islandId)
        {
            int slot = FindBaseStressCascadeSlot(islandId);
            if (slot >= 0)
                return slot;

            int startSlot = islandId & (BaseStressCascadeCircuitBreakerCapacity - 1);
            for (int probe = 0; probe < BaseStressCascadeCircuitBreakerCapacity; probe++)
            {
                int candidateSlot = (startSlot + probe) & (BaseStressCascadeCircuitBreakerCapacity - 1);
                if (_baseStressCascadeSlotFrames[candidateSlot] == _baseStressCascadeBreakerFrame)
                    continue;

                _baseStressCascadeIslandIds[candidateSlot] = islandId;
                _baseStressCascadeSlotFrames[candidateSlot] = _baseStressCascadeBreakerFrame;
                _baseStressCascadeIslandCounts[candidateSlot] = 0;
                _baseStressCascadeDroppedCounts[candidateSlot] = 0;
                _baseStressCascadeTelemetryEmitted[candidateSlot] = false;
                return candidateSlot;
            }

            return -1;
        }

        private static int FindBaseStressCascadeSlot(int islandId)
        {
            int startSlot = islandId & (BaseStressCascadeCircuitBreakerCapacity - 1);
            for (int probe = 0; probe < BaseStressCascadeCircuitBreakerCapacity; probe++)
            {
                int candidateSlot = (startSlot + probe) & (BaseStressCascadeCircuitBreakerCapacity - 1);
                if (_baseStressCascadeSlotFrames[candidateSlot] != _baseStressCascadeBreakerFrame)
                    return -1;

                int candidateIsland = _baseStressCascadeIslandIds[candidateSlot];
                if (candidateIsland == islandId)
                    return candidateSlot;
            }

            return -1;
        }

        internal static void SetVoxelTeardownBackpressure(bool active, int pendingChunkCount)
        {
            _foveatedSimulationManager.SetVoxelTeardownBackpressure(active, pendingChunkCount);
        }

        /// <summary>
        /// Requests pause-menu depth-of-field isolation on the dispatcher cadence.
        /// </summary>
        public static void RequestPauseDepthOfField(bool active)
        {
            _pauseMenuDepthOfFieldRequested = active;
            RefreshPauseDepthOfFieldTarget();
        }

        /// <summary>
        /// Requests PDA depth-of-field isolation without overriding the pause menu request.
        /// </summary>
        public static void RequestPdaDepthOfField(bool active)
        {
            _pdaDepthOfFieldRequested = active;
            RefreshPauseDepthOfFieldTarget();
        }

        internal static int DroppedEventsCounter => _droppedEventsCounter;

        internal static bool QueueDispatcherRaycast(IDispatcherRaycastReceiver receiver, int requestId, in RaycastCommand command)
        {
            if (receiver == null)
                return false;

            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null || !dispatcher._serviceRegistered)
                return false;

            EnsureDispatcherRaycastBuffers();
            if (!TryResolveDispatcherRaycastCommands(
                    ref _pendingDispatcherRaycastCommandsHandle,
                    BufferID.SystemDispatcherRaycastPendingCommands,
                    out NativeArray<RaycastCommand> pendingCommands) ||
                _pendingDispatcherRaycastCount >= MaxQueuedDispatcherRaycasts)
                return false;

            int writeIndex = _pendingDispatcherRaycastCount++;
            _pendingDispatcherRaycastReceivers[writeIndex] = receiver;
            _pendingDispatcherRaycastRequestIds[writeIndex] = requestId;
            pendingCommands[writeIndex] = command;
            return true;
        }

        /// <summary>
        /// Returns the registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense lane bucket.</returns>
        public static RegistryBucket<IUpdatable> GetLane(PriorityLayer layer)
        {
            return _priorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the 60 Hz fast-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IFastTickable> GetFastLane(PriorityLayer layer)
        {
            return _fastPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the fixed-step registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense fixed-step lane bucket.</returns>
        public static RegistryBucket<IFixedTickable> GetFixedLane(PriorityLayer layer)
        {
            return _fixedPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the slow-tick registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense slow-tick lane bucket.</returns>
        public static RegistryBucket<ISlowTickable> GetSlowLane(PriorityLayer layer)
        {
            return _slowPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the 1 Hz cold-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IColdTickable> GetColdLane(PriorityLayer layer)
        {
            return _coldPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the frost maintenance registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense frost-tick lane bucket.</returns>
        public static RegistryBucket<IFrostTickable> GetFrostLane(PriorityLayer layer)
        {
            return _frostPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the late-frame registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense late-frame lane bucket.</returns>
        public static RegistryBucket<ILateFrameTickable> GetLateFrameLane(PriorityLayer layer)
        {
            return _lateFramePriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the post-fixed-step registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense post-fixed lane bucket.</returns>
        public static RegistryBucket<IPostFixedTickable> GetPostFixedLane(PriorityLayer layer)
        {
            return _postFixedPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the unscaled UI/menu fast-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IUnscaledFastTickable> GetUnscaledFastLane(PriorityLayer layer)
        {
            return _unscaledFastPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Registers an update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!GetLane(layer).TryRegister(item))
                return false;

            if (item is IFoveatedSimulationTarget foveatedTarget)
                _foveatedSimulationManager.RegisterTarget(foveatedTarget);

            return true;
        }

        /// <summary>
        /// Registers a 60 Hz fast-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFastLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a fixed-update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFixedLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a slow-tick owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!GetSlowLane(layer).TryRegister(item))
                return false;

            if (item is IBucketedSlowTickable)
                _bucketedSlowTickableCount++;

            return true;
        }

        /// <summary>
        /// Registers a 1 Hz cold-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetColdLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a frost maintenance owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFrostLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a late-frame owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetLateFrameLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a post-fixed-step owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetPostFixedLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers an unscaled fast-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetUnscaledFastLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a master-dispatcher system into the Kahn-sorted pipeline.
        /// </summary>
        public static bool Register(IDispatcherSystem item)
        {
            if (item == null)
                return false;

            uint hash = item.GetSystemIdHash();
            if (hash == 0u)
                return false;

            for (int i = 0; i < _masterRegisteredSystemCount; i++)
            {
                IDispatcherSystem existing = _masterRegisteredSystems[i];
                if (ReferenceEquals(existing, item))
                    return true;
                if (existing != null && existing.GetSystemIdHash() == hash)
                    return false;
            }

            if (_masterRegisteredSystemCount >= MasterDispatcherMaxSystems)
                return false;

            _masterRegisteredSystems[_masterRegisteredSystemCount++] = item;
            _masterTopologyDirty = true;
            _masterTopologyValid = false;
            return true;
        }

        /// <summary>
        /// Registers a fixed-only dispatcher system. Fixed jobs stay outside the frame job barrier.
        /// </summary>
        public static bool Register(IDispatcherFixedSystem item)
        {
            if (item == null)
                return false;

            uint hash = item.GetFixedSystemIdHash();
            if (hash == 0u)
                return false;

            for (int i = 0; i < _masterFixedSystemCount; i++)
            {
                IDispatcherFixedSystem existing = _masterFixedSystems[i];
                if (ReferenceEquals(existing, item))
                    return true;
                if (existing != null && existing.GetFixedSystemIdHash() == hash)
                    return false;
            }

            if (_masterFixedSystemCount >= _masterFixedSystems.Length)
                return false;

            _masterFixedSystems[_masterFixedSystemCount++] = item;
            return true;
        }

        /// <summary>
        /// Unregisters an update owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (item is IFoveatedSimulationTarget foveatedTarget)
                _foveatedSimulationManager.UnregisterTarget(foveatedTarget);

            GetLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a fast-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFastLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a fixed-update owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFixedLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a slow-tick owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (GetSlowLane(layer).TryUnregister(item) && item is IBucketedSlowTickable)
                _bucketedSlowTickableCount = math.max(0, _bucketedSlowTickableCount - 1);
        }

        /// <summary>
        /// Unregisters a cold-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetColdLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a frost maintenance owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFrostLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a late-frame owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetLateFrameLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a post-fixed-step owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetPostFixedLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters an unscaled fast-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetUnscaledFastLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Removes a master-dispatcher system from the sorted pipeline.
        /// </summary>
        public static void Unregister(IDispatcherSystem item)
        {
            if (item == null)
                return;

            for (int i = 0; i < _masterRegisteredSystemCount; i++)
            {
                if (!ReferenceEquals(_masterRegisteredSystems[i], item))
                    continue;

                int last = _masterRegisteredSystemCount - 1;
                _masterRegisteredSystems[i] = _masterRegisteredSystems[last];
                _masterRegisteredSystems[last] = null;
                _masterRegisteredSystemCount = last;
                _masterTopologyDirty = true;
                _masterTopologyValid = false;
                return;
            }
        }

        /// <summary>
        /// Removes a fixed-only dispatcher system.
        /// </summary>
        public static void Unregister(IDispatcherFixedSystem item)
        {
            if (item == null)
                return;

            for (int i = 0; i < _masterFixedSystemCount; i++)
            {
                if (!ReferenceEquals(_masterFixedSystems[i], item))
                    continue;

                int last = _masterFixedSystemCount - 1;
                _masterFixedSystems[i] = _masterFixedSystems[last];
                _masterFixedSystems[last] = null;
                _masterFixedSystemCount = last;
                return;
            }
        }

        /// <summary>
        /// Clears every dispatcher lane.
        /// </summary>
        public static void ClearAllLanes()
        {
            for (int i = 0; i < LaneCount; i++)
            {
                _priorityLanes[i].Clear();
                _fastPriorityLanes[i].Clear();
                _fixedPriorityLanes[i].Clear();
                _slowPriorityLanes[i].Clear();
                _coldPriorityLanes[i].Clear();
                _frostPriorityLanes[i].Clear();
                _lateFramePriorityLanes[i].Clear();
                _postFixedPriorityLanes[i].Clear();
                _unscaledFastPriorityLanes[i].Clear();
            }

            _bucketedSlowTickableCount = 0;
            for (int i = 0; i < MasterDispatcherMaxSystems; i++)
            {
                _masterRegisteredSystems[i] = null;
                _masterSortedSystems[i] = null;
                _masterKahnInDegrees[i] = 0;
                _masterKahnQueue[i] = 0;
                _masterDependencySystemIndices[i] = -1;
                _masterSystemDisabled[i] = false;
            }

            for (int i = 0; i < _masterFixedSystems.Length; i++)
                _masterFixedSystems[i] = null;

            for (int i = 0; i < MasterDispatcherBucketCount; i++)
                _masterBucketLoadCounters[i] = 0u;

            _masterRegisteredSystemCount = 0;
            _masterSortedSystemCount = 0;
            _masterFixedSystemCount = 0;
            _masterTopologyDirty = true;
            _masterTopologyValid = false;
            _masterEmergencyTopologyInstalled = false;
            _foveatedSimulationManager.ResetRuntimeState();
        }

        internal static void RequestOriginShiftBootstrapLock()
        {
            Interlocked.Increment(ref _originShiftBootstrapLockCount);
        }

        internal static void RequestOriginShiftFrameLock(int frame)
        {
            Volatile.Write(ref _originShiftFrameLockFrame, frame);
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher != null)
                dispatcher.RequestAupPreShiftPause(unchecked((uint)frame));
        }

        public void RequestTimeDilation(float scalar, uint reasonHash = 0u)
        {
            ClearCoreTickDilationBurst();
            ClearAdrenalineDilation();
            SetTimeDilationScalar(scalar, reasonHash, publishImmediate: true);
        }

        public void RequestHeadlessTimeDilation(float scalar, uint reasonHash = 0u)
        {
            ClearCoreTickDilationBurst();
            ClearAdrenalineDilation();
            SetTimeDilationScalar(
                scalar,
                TimeDilationMinimumScalar,
                HeadlessTimeDilationMaximumScalar,
                reasonHash,
                publishImmediate: true);
        }

        /// <summary>
        /// Requests an exact frame-count core tick dilation burst.
        /// </summary>
        public void RequestCoreTickDilation(float scalar, int frameCount, uint reasonHash = 0u)
        {
            if (frameCount <= 0)
                return;

            if (SimulationPaused)
                return;

            float safeScalar = math.isfinite(scalar)
                ? math.clamp(scalar, TimeDilationPausedEpsilon, TimeDilationMaximumScalar)
                : 1f;

            if (_coreTickDilationRestorePending)
            {
                _coreTickDilationRestorePending = false;
            }
            else if (_coreTickDilationFramesRemaining <= 0)
            {
                _coreTickDilationRestoreScalar = _timeDilationScalar;
            }

            _coreTickDilationScalar = safeScalar;
            _coreTickDilationFramesRemaining = math.max(_coreTickDilationFramesRemaining, frameCount);
            _coreTickDilationReasonHash = reasonHash;
            SetTimeDilationScalar(safeScalar, reasonHash, publishImmediate: true);
        }

        public void SetThermalCriticalSlowTick(bool active)
        {
            _thermalCriticalSlowTickActive = active;
            if (active && _slowTickAccumulator > ThermalCriticalSlowTickIntervalSeconds)
                _slowTickAccumulator = ThermalCriticalSlowTickIntervalSeconds;
        }

        public void RequestSimulationPause(bool paused, uint reasonHash = 0u)
        {
            if (paused)
            {
                if (!_simulationPaused)
                    _prePauseTimeDilationScalar = ResolvePauseRestoreScalar();

                ClearCoreTickDilationBurst();
                ClearAdrenalineDilation();
                _simulationPaused = true;
                SetTimeDilationScalar(0f, reasonHash, publishImmediate: true);
                return;
            }

            _simulationPaused = false;
            SetTimeDilationScalar(_prePauseTimeDilationScalar, reasonHash, publishImmediate: true);
        }

        public void RequestAupPreShiftPause(uint shiftFrameId)
        {
            RefreshDataVaultDependency();
            _dataVault?.LockAllocationsForAupShift(shiftFrameId);
            _aupPreShiftPauseSequence = shiftFrameId;
            _aupPreShiftPauseFrame = Time.frameCount;
        }

        public async Awaitable DelayDilated(float seconds, CancellationToken cancellationToken = default)
        {
            await AwaitableExtension.DelayDilated(seconds, cancellationToken);
        }

        internal static void ReleaseOriginShiftBootstrapLock()
        {
            int current = Volatile.Read(ref _originShiftBootstrapLockCount);
            while (current > 0)
            {
                int next = current - 1;
                int observed = Interlocked.CompareExchange(ref _originShiftBootstrapLockCount, next, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }

        private void SetTimeDilationScalar(float scalar, uint reasonHash, bool publishImmediate)
        {
            SetTimeDilationScalar(
                scalar,
                TimeDilationMinimumScalar,
                TimeDilationMaximumScalar,
                reasonHash,
                publishImmediate);
        }

        private void SetTimeDilationScalar(
            float scalar,
            float minimumScalar,
            float maximumScalar,
            uint reasonHash,
            bool publishImmediate)
        {
            float safeScalar = math.isfinite(scalar)
                ? math.clamp(scalar, minimumScalar, maximumScalar)
                : 1f;
            if (math.abs(_timeDilationScalar - safeScalar) <= 0.0001f)
                return;

            _timeDilationScalar = safeScalar;
            _timeDilationSequence++;
            if (_timeDilationSequence == 0u)
                _timeDilationSequence = 1u;

            if (publishImmediate)
                PublishTimeDilationState(reasonHash);
        }

        private void PublishTimeDilationState(uint reasonHash)
        {
            if (_lastPublishedTimeDilationSequence == _timeDilationSequence)
                return;

            _lastPublishedTimeDilationSequence = _timeDilationSequence;
            float scalar = _timeDilationScalar;
            uint frame = unchecked((uint)Time.frameCount);
            TimeDilationSignal dilationSignal = default;
            dilationSignal.Scalar = scalar;
            dilationSignal.UnscaledDeltaTime = CurrentFrameUnscaledDeltaTime;
            dilationSignal.Sequence = _timeDilationSequence;
            dilationSignal.Frame = frame;
            dilationSignal.ReasonHash = reasonHash;
            dilationSignal.Flags = (byte)(SimulationPaused ? 1 : 0);
            GlobalSignals.Publish(in dilationSignal);

            BulletTimeVisualSignal visualSignal = default;
            visualSignal.Intensity01 = math.saturate((BulletTimePostScalarThreshold - scalar) / BulletTimePostScalarThreshold);
            visualSignal.Scalar = scalar;
            visualSignal.Frame = frame;
            visualSignal.Sequence = _timeDilationSequence;
            visualSignal.QualityTier = _scalabilityTierProfileByte;
            visualSignal.Flags = (byte)(SimulationPaused ? 1 : 0);
            GlobalSignals.Publish(in visualSignal);
        }

        private void DrainSimulationPauseSignals()
        {
            while (GlobalSignals.TryDequeueSimulationPause(out SimulationPauseSignal signal))
            {
                if (signal.Paused == 0 && math.isfinite(signal.RestoreScalar) && signal.RestoreScalar > 0f)
                    _prePauseTimeDilationScalar = signal.RestoreScalar;

                RequestSimulationPause(signal.Paused != 0, signal.SourceHash);
            }
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            _slowTickAccumulator = 0f;
            _fixedStepAccumulator = 0f;
            _fastTickAccumulator = 0f;
            _coldTickAccumulator = 0f;
            _frostTickAccumulator = 0f;
            _memoryDefragAccumulator = 0f;
            _unscaledFastTickAccumulator = 0f;
            _timeDilationScalar = 1f;
            _prePauseTimeDilationScalar = 1f;
            _coreTickDilationScalar = 1f;
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationFramesRemaining = 0;
            _coreTickDilationReasonHash = 0u;
            ClearAdrenalineDilation();
            _coreTickDilationRestorePending = false;
            _simulationPaused = false;
            _thermalCriticalSlowTickActive = false;
            _timeSnapshot = default;
            _dispatcherBlackBoxSequence = 0u;
            _dispatcherBlackBoxDumped = false;
            CurrentFixedInterpolationAlpha = 0f;
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_serviceRegistered)
            {
                HomeostasisBrain.ShutdownRuntime();
                _foveatedSimulationManager.Dispose();
                DisposeDispatcherRaycastBuffers();
                DisposeDispatcherBlackBox();
                DisposeH8TimeArray();
                DisposeMasterDispatcherRuntime();
                ThreadSafeCommandQueue.Shutdown();
                if (ReferenceEquals(GlobalRegistry.Dispatcher, this))
                    GlobalRegistry.UnregisterSystemDispatcher(this);
            }

            _slowTickAccumulator = 0f;
            _fixedStepAccumulator = 0f;
            _fastTickAccumulator = 0f;
            _coldTickAccumulator = 0f;
            _frostTickAccumulator = 0f;
            _memoryDefragAccumulator = 0f;
            _unscaledFastTickAccumulator = 0f;
            _timeDilationScalar = 1f;
            _prePauseTimeDilationScalar = 1f;
            _simulationPaused = false;
            _thermalCriticalSlowTickActive = false;
            _timeDilationSequence = 0u;
            _lastPublishedTimeDilationSequence = 0u;
            _aupPreShiftPauseSequence = 0u;
            _aupPreShiftPauseFrame = -1;
            ClearCoreTickDilationBurst();
            ClearAdrenalineDilation();
            _dataVault = null;
            _cachedDispatcherDataVault = null;
            _cachedCameraJuiceSystem = null;
            _nextCameraJuiceResolveFrame = 0;
            _simulationBucketer = null;
            _jobAdmission = null;
            _inputDeterminism = null;
            _vramMonitor = null;
            _vramPressure = null;
            _macroDatabase = null;
            _objectPool = null;
            _timeSnapshot = default;
            _dispatcherBlackBoxSequence = 0u;
            _dispatcherState = default;
            _masterSimulationCombinedHandle = default;
            _masterFixedCombinedHandle = default;
            _masterPendingSimulationJobCount = 0;
            _masterPendingFixedJobCount = 0;
            _masterDisabledSystemCount = 0;
            _masterPipelineTelemetryDumped = false;
            _masterVisualSyncShedThisFrame = false;
            _dispatcherBlackBoxDumped = false;
            CurrentFrameDeltaTime = 0f;
            CurrentFrameUnscaledDeltaTime = 0f;
            CurrentFixedInterpolationAlpha = 0f;
            _serviceRegistered = false;
            _lastMemoryDefragPressureWarningFrame = -1;
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, 0L);
            Volatile.Write(ref _homeostasisPressureLevel, 0);
            Volatile.Write(ref _homeostasisSlowTick2Hz, 0);
            Volatile.Write(ref _homeostasisFoveatedTier, 0);
        }

        /// <summary>
        /// Registers this dispatcher as the authoritative runtime dispatcher service.
        /// </summary>
        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            ThreadSafeCommandQueue.Initialize();
            UIStateStore.EnsureInitialized();
            BaseAirlockEvents.Prewarm();
            RefreshDataVaultDependency();
            RefreshSimulationBucketerDependency();
            RefreshJobAdmissionDependency();
            RefreshInputDeterminismDependency();
            RefreshPeripheralDependencies();
            RefreshScalabilityTierProfile();
            EnsureDispatcherRaycastBuffers();
            EnsureH8TimeArray();
            EnsureDispatcherBlackBox();
            InitializeMasterDispatcherRuntime();
            HomeostasisBrain.InitializeRuntime();
            GlobalRegistry.RegisterSystemDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Dispatcher, this);
            EnsureDispatcherPlayerLoopInstalled();
            PublishTimeDilationState(0u);
        }

        private void InitializeMasterDispatcherRuntime()
        {
            EnsureMasterDispatcherNativeBuffers();
            if (_masterRegisteredSystemCount == 0 && !_masterEmergencyTopologyInstalled && !HasLegacyDispatcherTopologyFile())
                GenerateEmergencyMockTopology();

            EnsureMasterDispatcherTopology();
            _dispatcherState = default;
            _dispatcherState.CurrentFrame = unchecked((uint)math.max(0, Time.frameCount));
            _dispatcherState.ActiveBucket = unchecked((uint)(Time.frameCount & MasterDispatcherBucketMask));
            _dispatcherState.SortedSystemCount = unchecked((uint)math.max(0, _masterSortedSystemCount));
        }

        private void EnsureMasterDispatcherNativeBuffers()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null)
                return;

            if (!_masterSimulationJobHandlesHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterSimulationJobHandlesHandle))
            {
                _masterSimulationJobHandlesHandle = dataVault.GetBufferHandle<JobHandle>(
                    BufferID.SystemDispatcherMasterJobHandles,
                    MasterDispatcherMaxSystems,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_masterDependencyScratchHandlesHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterDependencyScratchHandlesHandle))
            {
                _masterDependencyScratchHandlesHandle = dataVault.GetBufferHandle<JobHandle>(
                    BufferID.SystemDispatcherMasterDependencyScratch,
                    MasterDispatcherDependencyScratchCapacity,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_masterJobDependencyTelemetryHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterJobDependencyTelemetryHandle))
            {
                _masterJobDependencyTelemetryHandle = dataVault.GetBufferHandle<JobDependencyDTO>(
                    BufferID.SystemDispatcherMasterJobDependencyTelemetry,
                    MasterDispatcherMaxSystems,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_masterPipelineTelemetryRingHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterPipelineTelemetryRingHandle))
            {
                _masterPipelineTelemetryRingHandle = dataVault.GetBufferHandle<DispatcherPipelineTelemetryEntry>(
                    BufferID.SystemDispatcherMasterPipelineTelemetry,
                    MasterDispatcherBlackBoxFrameCount,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_masterPipelineTelemetryCursorHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterPipelineTelemetryCursorHandle))
            {
                _masterPipelineTelemetryCursorHandle = dataVault.GetBufferHandle<int>(
                    BufferID.SystemDispatcherMasterPipelineCursor,
                    1,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_masterMockTimeDilationSignalsHandle.IsCreated ||
                !dataVault.ResolveBuffer(ref _masterMockTimeDilationSignalsHandle))
            {
                _masterMockTimeDilationSignalsHandle = dataVault.GetBufferHandle<MockTimeDilationSignal>(
                    BufferID.SystemDispatcherMasterMockTimeDilationSignals,
                    8,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.UninitializedMemory);
            }
        }

        private void DisposeMasterDispatcherRuntime()
        {
            if (_masterSimulationJobsPending)
                _masterSimulationCombinedHandle.Complete();
            if (_masterFixedJobsPending)
                _masterFixedCombinedHandle.Complete();

            _masterSimulationJobHandlesHandle = default;
            _masterDependencyScratchHandlesHandle = default;
            _masterJobDependencyTelemetryHandle = default;
            _masterPipelineTelemetryRingHandle = default;
            _masterPipelineTelemetryCursorHandle = default;
            _masterMockTimeDilationSignalsHandle = default;
            _masterSimulationCombinedHandle = default;
            _masterFixedCombinedHandle = default;
            _masterSimulationJobsPending = false;
            _masterFixedJobsPending = false;
        }

        private bool TryResolveMasterSimulationBuffers(
            out NativeArray<JobHandle> simulationJobHandles,
            out NativeArray<JobHandle> dependencyScratchHandles,
            out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
            out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals)
        {
            simulationJobHandles = default;
            dependencyScratchHandles = default;
            jobDependencyTelemetry = default;
            mockTimeDilationSignals = default;
            EnsureMasterDispatcherNativeBuffers();

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            simulationJobHandles = _masterSimulationJobHandlesHandle.Resolve(dataVault);
            dependencyScratchHandles = _masterDependencyScratchHandlesHandle.Resolve(dataVault);
            jobDependencyTelemetry = _masterJobDependencyTelemetryHandle.Resolve(dataVault);
            mockTimeDilationSignals = _masterMockTimeDilationSignalsHandle.Resolve(dataVault);

            return simulationJobHandles.IsCreated &&
                   simulationJobHandles.Length >= MasterDispatcherMaxSystems &&
                   dependencyScratchHandles.IsCreated &&
                   dependencyScratchHandles.Length >= MasterDispatcherDependencyScratchCapacity &&
                   jobDependencyTelemetry.IsCreated &&
                   jobDependencyTelemetry.Length >= MasterDispatcherMaxSystems &&
                   mockTimeDilationSignals.IsCreated &&
                   mockTimeDilationSignals.Length >= 8;
        }

        private bool TryResolveMasterTelemetryBuffers(
            out NativeArray<DispatcherPipelineTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            EnsureMasterDispatcherNativeBuffers();

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            telemetryRing = _masterPipelineTelemetryRingHandle.Resolve(dataVault);
            telemetryCursor = _masterPipelineTelemetryCursorHandle.Resolve(dataVault);
            return telemetryRing.IsCreated &&
                   telemetryRing.Length >= MasterDispatcherBlackBoxFrameCount &&
                   telemetryCursor.IsCreated &&
                   telemetryCursor.Length >= 1;
        }

        private static bool HasLegacyDispatcherTopologyFile()
        {
            return File.Exists("Docs/Archive/dispatcher_phases.h8bin") ||
                   File.Exists("Docs/Archive/system_priorities.bin");
        }

        private static void GenerateEmergencyMockTopology()
        {
            if (_masterRegisteredSystemCount > 0)
                return;

            MockTickableSystem input = default;
            input.SystemIdHash = 0x53483001u;
            input.PhaseId = (byte)DispatcherPhase.PreSimulation;
            input.BucketId = byte.MaxValue;
            input.SignalIndex = 0;
            input.CostMicroseconds = 4u;
            Register(input);

            MockTickableSystem physics = default;
            physics.SystemIdHash = 0x53483002u;
            physics.Dependency0 = input.SystemIdHash;
            physics.DependencyCount = 1;
            physics.PhaseId = (byte)DispatcherPhase.Simulation;
            physics.BucketId = 1;
            physics.SignalIndex = 1;
            physics.CostMicroseconds = 12u;
            Register(physics);

            MockTickableSystem ai = default;
            ai.SystemIdHash = 0x53483003u;
            ai.Dependency0 = physics.SystemIdHash;
            ai.DependencyCount = 1;
            ai.PhaseId = (byte)DispatcherPhase.Simulation;
            ai.BucketId = 2;
            ai.SignalIndex = 2;
            ai.CostMicroseconds = 8u;
            Register(ai);

            MockTickableSystem visual = default;
            visual.SystemIdHash = 0x53483004u;
            visual.Dependency0 = physics.SystemIdHash;
            visual.Dependency1 = ai.SystemIdHash;
            visual.DependencyCount = 2;
            visual.PhaseId = (byte)DispatcherPhase.VisualSync;
            visual.BucketId = byte.MaxValue;
            visual.SignalIndex = 3;
            visual.CostMicroseconds = 6u;
            Register(visual);

            _masterEmergencyTopologyInstalled = true;
            _masterTopologyDirty = true;
        }

        private static void EnsureMasterDispatcherTopology()
        {
            if (!_masterTopologyDirty && _masterTopologyValid)
                return;

            int count = _masterRegisteredSystemCount;
            for (int i = 0; i < MasterDispatcherMaxSystems; i++)
            {
                _masterSortedSystems[i] = null;
                _masterKahnInDegrees[i] = 0;
                _masterKahnQueue[i] = 0;
                _masterDependencySystemIndices[i] = -1;
            }

            for (int i = 0; i < count; i++)
            {
                IDispatcherSystem system = _masterRegisteredSystems[i];
                if (system == null)
                    continue;

                int dependencyCount = math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
                for (int dependencyIndex = 0; dependencyIndex < dependencyCount; dependencyIndex++)
                {
                    uint dependencyHash = system.GetDependencyHash(dependencyIndex);
                    if (dependencyHash == 0u)
                        continue;

                    int providerIndex = FindRegisteredMasterSystemIndex(dependencyHash, count);
                    if (providerIndex < 0)
                        continue;

                    _masterKahnInDegrees[i]++;
                }
            }

            int head = 0;
            int tail = 0;
            for (int i = 0; i < count; i++)
            {
                if (_masterKahnInDegrees[i] == 0)
                    _masterKahnQueue[tail++] = i;
            }

            int sortedCount = 0;
            while (head < tail)
            {
                int providerIndex = _masterKahnQueue[head++];
                IDispatcherSystem provider = _masterRegisteredSystems[providerIndex];
                if (provider == null)
                    continue;

                _masterSortedSystems[sortedCount++] = provider;
                uint providerHash = provider.GetSystemIdHash();
                for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
                {
                    IDispatcherSystem candidate = _masterRegisteredSystems[candidateIndex];
                    if (candidate == null || candidateIndex == providerIndex)
                        continue;

                    int dependencyCount = math.clamp(candidate.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
                    for (int dependencyIndex = 0; dependencyIndex < dependencyCount; dependencyIndex++)
                    {
                        if (candidate.GetDependencyHash(dependencyIndex) != providerHash)
                            continue;

                        _masterKahnInDegrees[candidateIndex]--;
                        if (_masterKahnInDegrees[candidateIndex] == 0)
                            _masterKahnQueue[tail++] = candidateIndex;
                        break;
                    }
                }
            }

            if (sortedCount != count)
                throw new FatalArchitectureException("SystemDispatcher Kahn cycle detected in master topology.");

            _masterSortedSystemCount = sortedCount;
            _masterTopologyDirty = false;
            _masterTopologyValid = true;
        }

        private static int FindRegisteredMasterSystemIndex(uint systemHash, int count)
        {
            for (int i = 0; i < count; i++)
            {
                IDispatcherSystem system = _masterRegisteredSystems[i];
                if (system != null && system.GetSystemIdHash() == systemHash)
                    return i;
            }

            return -1;
        }

        private static int FindSortedMasterSystemIndex(uint systemHash)
        {
            for (int i = 0; i < _masterSortedSystemCount; i++)
            {
                IDispatcherSystem system = _masterSortedSystems[i];
                if (system != null && system.GetSystemIdHash() == systemHash)
                    return i;
            }

            return -1;
        }

        private ref DispatcherStateDTO GetDispatcherStateRef()
        {
            return ref _dispatcherState;
        }

        private DispatcherTimingDTO BuildMasterDispatcherTiming(float deltaTime, float unscaledDeltaTime, float fixedDeltaTime = 0f)
        {
            uint activeBucket = ResolveMasterActiveBucket();
            uint activeBucketMask = 1u << (int)(activeBucket & 31u);
            DispatcherTimingDTO timing = default;
            timing.FrameDelta = deltaTime;
            timing.FixedDelta = fixedDeltaTime;
            timing.TimeScale = unscaledDeltaTime > 0f ? math.saturate(deltaTime / unscaledDeltaTime) : 0f;
            timing.ActiveBucketMask = activeBucketMask;
            return timing;
        }

        private static uint ResolveMasterActiveBucket()
        {
            return unchecked((uint)math.max(0, Time.frameCount)) & MasterDispatcherBucketMask;
        }

        private void SetMasterDispatcherPhase(DispatcherPhase phase, in DispatcherTimingDTO timing)
        {
            ref DispatcherStateDTO state = ref GetDispatcherStateRef();
            state.CurrentPhaseId = (uint)phase;
            state.CurrentFrame = unchecked((uint)math.max(0, Time.frameCount));
            state.ActiveBucket = ResolveMasterActiveBucket();
            state.ActiveBucketMask = timing.ActiveBucketMask;
            state.SortedSystemCount = unchecked((uint)math.max(0, _masterSortedSystemCount));
            state.DisabledSystemCount = unchecked((uint)math.max(0, _masterDisabledSystemCount));
            state.PendingSimulationJobCount = unchecked((uint)math.max(0, _masterPendingSimulationJobCount));
            state.Flags = _masterVisualSyncShedThisFrame ? 1u : 0u;
        }

        private void RunMasterPreSimulationPhase(in DispatcherTimingDTO timing)
        {
            _masterFrameStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _masterPreSimulationStartTimestamp = _masterFrameStartTimestamp;
            SetMasterDispatcherPhase(DispatcherPhase.PreSimulation, in timing);
            TryReloadMasterExecutionPriorityCsv();
            EnsureMasterDispatcherTopology();
            uint activeBucket = ResolveMasterActiveBucket();

            using (_masterPreSimulationProfilerMarker.Auto())
            {
                for (int i = 0; i < _masterSortedSystemCount; i++)
                {
                    if (_masterSystemDisabled[i])
                        continue;

                    IDispatcherSystem system = _masterSortedSystems[i];
                    if (system == null || system.GetDispatcherPhase() != DispatcherPhase.PreSimulation)
                        continue;
                    if (!ShouldRunMasterSystemInActiveBucket(system, activeBucket))
                        continue;

                    try
                    {
                        system.PreSimulationTick(in timing);
                    }
                    catch (Exception exception) when (!(exception is FatalArchitectureException))
                    {
                        DisableMasterSystem(i, system);
                    }
                }
            }

            _masterLastPreSimulationMs = ElapsedMilliseconds(_masterPreSimulationStartTimestamp);
            _masterPhaseTimingSnapshotMs[0] = _masterLastPreSimulationMs;
        }

        private void RunMasterSimulationPhase(in DispatcherTimingDTO timing)
        {
            SetMasterDispatcherPhase(DispatcherPhase.Simulation, in timing);
            EnsureMasterDispatcherTopology();
            if (!TryResolveMasterSimulationBuffers(
                    out NativeArray<JobHandle> simulationJobHandles,
                    out NativeArray<JobHandle> dependencyScratchHandles,
                    out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
                    out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals))
            {
                return;
            }

            _masterPendingSimulationJobCount = 0;
            _masterSimulationJobsPending = false;

            for (int i = 0; i < simulationJobHandles.Length; i++)
                simulationJobHandles[i] = default;
            for (int i = 0; i < jobDependencyTelemetry.Length; i++)
                jobDependencyTelemetry[i] = default;
            for (int i = 0; i < MasterDispatcherBucketCount; i++)
                _masterBucketLoadCounters[i] = 0u;

            DispatcherJobContext context = default;
            context.MockTimeDilationSignals = mockTimeDilationSignals;
            context.JobDependencyTelemetry = jobDependencyTelemetry;
            context.Frame = unchecked((uint)math.max(0, Time.frameCount));
            context.ActiveBucket = ResolveMasterActiveBucket();

            using (_masterSimulationProfilerMarker.Auto())
            {
                for (int sortedIndex = 0; sortedIndex < _masterSortedSystemCount; sortedIndex++)
                {
                    if (_masterSystemDisabled[sortedIndex])
                        continue;

                    IDispatcherSystem system = _masterSortedSystems[sortedIndex];
                    if (system == null || system.GetDispatcherPhase() != DispatcherPhase.Simulation)
                        continue;
                    if (!ShouldRunMasterSystemInActiveBucket(system, context.ActiveBucket))
                        continue;

                    JobHandle dependencyHandle = ResolveMasterDependencyHandle(
                        system,
                        simulationJobHandles,
                        dependencyScratchHandles);
                    try
                    {
                        JobHandle handle = system.ScheduleSimulation(in timing, in context, dependencyHandle);
                        simulationJobHandles[sortedIndex] = handle;
                        jobDependencyTelemetry[sortedIndex] = BuildJobDependencyTelemetry(system.GetSystemIdHash(), ref handle);
                        _masterPendingSimulationJobCount++;
                        int bucket = system.GetBucketId() & MasterDispatcherBucketMask;
                        _masterBucketLoadCounters[bucket]++;
                    }
                    catch (Exception exception) when (!(exception is FatalArchitectureException))
                    {
                        DisableMasterSystem(sortedIndex, system);
                    }
                }
            }

            if (_masterPendingSimulationJobCount > 0)
            {
                _masterSimulationCombinedHandle = JobHandle.CombineDependencies(simulationJobHandles);
                _masterSimulationJobsPending = true;
                H8Memory.RegisterActiveJob(SystemID.SystemDispatcher, _masterSimulationCombinedHandle);
            }

            SetMasterDispatcherPhase(DispatcherPhase.Simulation, in timing);
        }

        private void RunMasterPostSimulationPhase(in DispatcherTimingDTO timing)
        {
            _masterPostSimulationStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            SetMasterDispatcherPhase(DispatcherPhase.PostSimulation, in timing);

            using (_masterPostSimulationProfilerMarker.Auto())
            {
                if (_masterSimulationJobsPending)
                {
                    long waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    _masterSimulationCombinedHandle.Complete();
                    _masterLastSimWaitMs = ElapsedMilliseconds(waitStart);
                    _masterSimulationCombinedHandle = default;
                    _masterSimulationJobsPending = false;
                    _masterPendingSimulationJobCount = 0;
                    ApplyMockTimeDilationSignals(unchecked((uint)math.max(0, Time.frameCount)));
                }
                else
                {
                    _masterLastSimWaitMs = 0f;
                }

                for (int i = 0; i < _masterSortedSystemCount; i++)
                {
                    if (_masterSystemDisabled[i])
                        continue;

                    IDispatcherSystem system = _masterSortedSystems[i];
                    if (system == null || system.GetDispatcherPhase() != DispatcherPhase.PostSimulation)
                        continue;
                    if (!ShouldRunMasterSystemInActiveBucket(system, ResolveMasterActiveBucket()))
                        continue;

                    try
                    {
                        system.PostSimulationTick(in timing);
                    }
                    catch (Exception exception) when (!(exception is FatalArchitectureException))
                    {
                        DisableMasterSystem(i, system);
                    }
                }
            }

            _masterLastPostSimulationMs = ElapsedMilliseconds(_masterPostSimulationStartTimestamp);
            _masterPhaseTimingSnapshotMs[1] = _masterLastSimWaitMs;
            _masterPhaseTimingSnapshotMs[2] = _masterLastPostSimulationMs;
        }

        private void RunMasterVisualSyncPhase()
        {
            DispatcherTimingDTO timing = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime);
            _masterVisualSyncStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            SetMasterDispatcherPhase(DispatcherPhase.VisualSync, in timing);
            _masterVisualSyncShedThisFrame = ShouldShedMasterVisualSync();
            if (_masterVisualSyncShedThisFrame)
            {
                _masterLastVisualSyncMs = 0f;
                RecordMasterPipelineTelemetry(unchecked((uint)math.max(0, Time.frameCount)));
                return;
            }

            using (_masterVisualSyncProfilerMarker.Auto())
            {
                for (int i = 0; i < _masterSortedSystemCount; i++)
                {
                    if (_masterSystemDisabled[i])
                        continue;

                    IDispatcherSystem system = _masterSortedSystems[i];
                    if (system == null || system.GetDispatcherPhase() != DispatcherPhase.VisualSync)
                        continue;
                    if (!ShouldRunMasterSystemInActiveBucket(system, ResolveMasterActiveBucket()))
                        continue;

                    try
                    {
                        system.VisualSyncTick(in timing);
                    }
                    catch (Exception exception) when (!(exception is FatalArchitectureException))
                    {
                        DisableMasterSystem(i, system);
                    }
                }
            }

            _masterLastVisualSyncMs = ElapsedMilliseconds(_masterVisualSyncStartTimestamp);
            _masterPhaseTimingSnapshotMs[3] = _masterLastVisualSyncMs;
            RecordMasterPipelineTelemetry(unchecked((uint)math.max(0, Time.frameCount)));
        }

        private void RunMasterFixedSimulationBridge(float fixedDeltaTime)
        {
            if (_masterFixedSystemCount <= 0)
                return;

            DispatcherTimingDTO timing = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime, fixedDeltaTime);
            JobHandle combined = default;
            _masterPendingFixedJobCount = 0;
            for (int i = 0; i < _masterFixedSystemCount; i++)
            {
                IDispatcherFixedSystem system = _masterFixedSystems[i];
                if (system == null)
                    continue;

                combined = system.ScheduleFixedSimulation(in timing, combined);
                _masterPendingFixedJobCount++;
            }

            if (_masterPendingFixedJobCount > 0)
            {
                _masterFixedCombinedHandle = combined;
                _masterFixedJobsPending = true;
                H8Memory.RegisterActiveJob(SystemID.SystemDispatcher, _masterFixedCombinedHandle);
            }
        }

        private void CompleteMasterFixedSimulationBridge(float fixedDeltaTime)
        {
            if (_masterFixedSystemCount <= 0)
                return;

            DispatcherTimingDTO timing = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime, fixedDeltaTime);
            if (_masterFixedJobsPending)
            {
                _masterFixedCombinedHandle.Complete();
                _masterFixedCombinedHandle = default;
                _masterFixedJobsPending = false;
            }

            for (int i = 0; i < _masterFixedSystemCount; i++)
            {
                IDispatcherFixedSystem system = _masterFixedSystems[i];
                if (system == null)
                    continue;

                system.PostFixedSimulation(in timing);
            }

            _masterPendingFixedJobCount = 0;
        }

        private JobHandle ResolveMasterDependencyHandle(
            IDispatcherSystem system,
            NativeArray<JobHandle> simulationJobHandles,
            NativeArray<JobHandle> dependencyScratchHandles)
        {
            int dependencyCount = math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
            for (int i = 0; i < MasterDispatcherDependencyScratchCapacity; i++)
                dependencyScratchHandles[i] = default;

            int scratchCount = 0;
            for (int i = 0; i < dependencyCount; i++)
            {
                uint dependencyHash = system.GetDependencyHash(i);
                if (dependencyHash == 0u)
                    continue;

                int sortedIndex = FindSortedMasterSystemIndex(dependencyHash);
                if (sortedIndex < 0)
                    continue;

                dependencyScratchHandles[scratchCount++] = simulationJobHandles[sortedIndex];
            }

            if (scratchCount == 0)
                return default;
            if (scratchCount == 1)
                return dependencyScratchHandles[0];

            return JobHandle.CombineDependencies(dependencyScratchHandles);
        }

        private static bool ShouldRunMasterSystemInActiveBucket(IDispatcherSystem system, uint activeBucket)
        {
            byte bucket = system.GetBucketId();
            if (bucket == byte.MaxValue)
                return true;

            return (bucket & MasterDispatcherBucketMask) == (activeBucket & MasterDispatcherBucketMask);
        }

        private static JobDependencyDTO BuildJobDependencyTelemetry(uint systemHash, ref JobHandle handle)
        {
            JobDependencyDTO dto = default;
            dto.JobHandlePtr = CaptureJobHandleBits(ref handle);
            dto.SystemIdHash = systemHash;
            dto._pad0 = 0u;
            return dto;
        }

        private static unsafe ulong CaptureJobHandleBits(ref JobHandle handle)
        {
            void* ptr = UnsafeUtility.AddressOf(ref handle);
            return ptr != null ? *((ulong*)ptr) : 0ul;
        }

        private void ApplyMockTimeDilationSignals(uint frame)
        {
            if (!TryResolveMasterSimulationBuffers(
                    out NativeArray<JobHandle> _,
                    out NativeArray<JobHandle> _,
                    out NativeArray<JobDependencyDTO> _,
                    out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals))
            {
                return;
            }

            float scalar = 1f;
            for (int i = 0; i < mockTimeDilationSignals.Length; i++)
            {
                MockTimeDilationSignal signal = mockTimeDilationSignals[i];
                if (signal.Frame != frame)
                    continue;
                if (math.isfinite(signal.TimeScale) && signal.TimeScale > 0f)
                    scalar = math.min(scalar, signal.TimeScale);
            }

            if (scalar < 0.999f)
                CurrentFrameDeltaTime = CurrentFrameUnscaledDeltaTime * scalar;
        }

        private void DisableMasterSystem(int sortedIndex, IDispatcherSystem system)
        {
            if ((uint)sortedIndex >= (uint)MasterDispatcherMaxSystems || _masterSystemDisabled[sortedIndex])
                return;

            _masterSystemDisabled[sortedIndex] = true;
            _masterDisabledSystemCount++;
            uint systemHash = system != null ? system.GetSystemIdHash() : _SystemDispatcherHash;
            PublishDispatcherComplianceViolation(_DispatcherBlackBoxFaultHash, systemHash, 4, 2);
            GlobalTelemetryBus.PublishPerformanceWarning(_DispatcherBlackBoxFaultHash, systemHash, 1f);
        }

        private static bool ShouldShedMasterVisualSync()
        {
            System.ReadOnlySpan<SystemHealthIndexSignal> snapshot = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                SystemHealthIndexSignal signal = snapshot[i];
                if ((math.isfinite(signal.Health01) && signal.Health01 > 0.9f) ||
                    (math.isfinite(signal.Pressure01) && signal.Pressure01 > 0.9f))
                {
                    return true;
                }
            }

            return false;
        }

        private void RecordMasterPipelineTelemetry(uint frame)
        {
            if (!TryResolveMasterTelemetryBuffers(
                    out NativeArray<DispatcherPipelineTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            int cursor = telemetryCursor[0];
            if ((uint)cursor >= (uint)MasterDispatcherBlackBoxFrameCount)
                cursor = 0;

            DispatcherPipelineTelemetryEntry entry = default;
            entry.Frame = frame;
            entry.PreSimulationTimeMs = SanitizeNonNegativeMilliseconds(_masterLastPreSimulationMs);
            entry.SimWaitTimeMs = SanitizeNonNegativeMilliseconds(_masterLastSimWaitMs);
            entry.PostSimulationTimeMs = SanitizeNonNegativeMilliseconds(_masterLastPostSimulationMs);
            entry.VisualSyncTimeMs = SanitizeNonNegativeMilliseconds(_masterLastVisualSyncMs);
            entry.ActiveBucket = _dispatcherState.ActiveBucket;
            entry.SystemCount = unchecked((uint)math.max(0, _masterSortedSystemCount));
            entry.Flags = _masterVisualSyncShedThisFrame ? 1u : 0u;
            telemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= MasterDispatcherBlackBoxFrameCount)
                cursor = 0;
            telemetryCursor[0] = cursor;
            _masterFrameTelemetrySequence++;

            if (entry.SimWaitTimeMs > MasterDispatcherStallDumpThresholdMs && !_masterPipelineTelemetryDumped)
            {
                _masterPipelineTelemetryDumped = true;
                DumpMasterPipelineTelemetry(telemetryRing, cursor);
            }
        }

        private static void DumpMasterPipelineTelemetry(NativeArray<DispatcherPipelineTelemetryEntry> ring, int cursor)
        {
            if (!ring.IsCreated || ring.Length < MasterDispatcherBlackBoxFrameCount)
                return;

            try
            {
                System.IO.Directory.CreateDirectory("Docs/AgentLogs");
                using (System.IO.FileStream stream = System.IO.File.Open(
                    MasterDispatcherDumpPath,
                    System.IO.FileMode.Create,
                    System.IO.FileAccess.Write,
                    System.IO.FileShare.Read))
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(DispatcherBlackBoxDumpMagic);
                    writer.Write(MasterDispatcherDumpVersion);
                    writer.Write(MasterDispatcherBlackBoxFrameCount);
                    writer.Write(32);
                    writer.Write(cursor);
                    for (int i = 0; i < MasterDispatcherBlackBoxFrameCount; i++)
                    {
                        int index = cursor + i;
                        if (index >= MasterDispatcherBlackBoxFrameCount)
                            index -= MasterDispatcherBlackBoxFrameCount;

                        DispatcherPipelineTelemetryEntry entry = ring[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.PreSimulationTimeMs);
                        writer.Write(entry.SimWaitTimeMs);
                        writer.Write(entry.PostSimulationTimeMs);
                        writer.Write(entry.VisualSyncTimeMs);
                        writer.Write(entry.ActiveBucket);
                        writer.Write(entry.SystemCount);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (System.IO.IOException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }
        }

        private void TryReloadMasterExecutionPriorityCsv()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int frame = Time.frameCount;
            if (_masterCsvPollFrame == frame || (frame & MasterDispatcherBucketMask) != 0)
                return;

            _masterCsvPollFrame = frame;
            if (!File.Exists(MasterDispatcherPriorityCsvPath))
                return;

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(MasterDispatcherPriorityCsvPath);
            if (writeTimeUtc == _masterPriorityCsvLastWriteUtc)
                return;

            _masterPriorityCsvLastWriteUtc = writeTimeUtc;
            ApplyMasterExecutionPriorityCsv();
            _masterTopologyDirty = true;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ApplyMasterExecutionPriorityCsv()
        {
            for (int i = 0; i < MasterDispatcherMaxSystems; i++)
            {
                _masterCsvSystemHashes[i] = 0u;
                _masterCsvSystemPriorities[i] = int.MaxValue;
            }

            int entryCount = ParseMasterExecutionPriorityCsv();
            if (entryCount <= 0)
                return;

            for (int i = 1; i < _masterRegisteredSystemCount; i++)
            {
                IDispatcherSystem candidate = _masterRegisteredSystems[i];
                int candidatePriority = ResolveMasterCsvPriority(candidate, entryCount);
                int j = i - 1;
                while (j >= 0 && ResolveMasterCsvPriority(_masterRegisteredSystems[j], entryCount) > candidatePriority)
                {
                    _masterRegisteredSystems[j + 1] = _masterRegisteredSystems[j];
                    j--;
                }

                _masterRegisteredSystems[j + 1] = candidate;
            }
        }

        private static int ResolveMasterCsvPriority(IDispatcherSystem system, int entryCount)
        {
            if (system == null)
                return int.MaxValue;

            uint hash = system.GetSystemIdHash();
            for (int i = 0; i < entryCount; i++)
            {
                if (_masterCsvSystemHashes[i] == hash)
                    return _masterCsvSystemPriorities[i];
            }

            return int.MaxValue - 1;
        }

        private static int ParseMasterExecutionPriorityCsv()
        {
            int entryCount = 0;
            try
            {
                using (FileStream stream = File.Open(
                    MasterDispatcherPriorityCsvPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    uint hash = 0u;
                    int priority = 0;
                    int column = 0;
                    bool hasValue = false;
                    bool hasHash = false;
                    bool hasPriority = false;
                    bool hex = false;
                    bool comment = false;
                    uint value = 0u;

                    while (true)
                    {
                        int read = stream.ReadByte();
                        bool end = read < 0;
                        byte c = end ? (byte)'\n' : (byte)read;

                        if (comment)
                        {
                            if (c == '\n' || c == '\r' || end)
                                comment = false;
                            else
                                continue;
                        }

                        if (c == '#')
                        {
                            comment = true;
                            continue;
                        }

                        bool separator = c == ',' || c == ';' || c == '\t' || c == ' ' || c == '\r' || c == '\n' || end;
                        if (!separator)
                        {
                            if ((c == 'x' || c == 'X') && hasValue && value == 0u)
                            {
                                hex = true;
                                continue;
                            }

                            int digit = ParseCsvDigit(c);
                            if (digit >= 0)
                            {
                                uint radix = hex ? 16u : 10u;
                                value = unchecked(value * radix + (uint)digit);
                                hasValue = true;
                            }

                            if (!end)
                                continue;
                        }

                        if (hasValue)
                        {
                            if (column == 0)
                            {
                                hash = value;
                                hasHash = true;
                            }
                            else if (column == 1)
                            {
                                priority = unchecked((int)value);
                                hasPriority = true;
                            }

                            value = 0u;
                            hasValue = false;
                            hex = false;
                            column++;
                        }

                        if (c == '\n' || end)
                        {
                            if (hasHash && hasPriority && entryCount < MasterDispatcherMaxSystems)
                            {
                                _masterCsvSystemHashes[entryCount] = hash;
                                _masterCsvSystemPriorities[entryCount] = priority;
                                entryCount++;
                            }

                            hash = 0u;
                            priority = 0;
                            column = 0;
                            hasHash = false;
                            hasPriority = false;
                        }

                        if (end)
                            break;
                    }
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

            return entryCount;
        }

        private static int ParseCsvDigit(byte c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }
#endif

        private static float ElapsedMilliseconds(long startTimestamp)
        {
            return (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d /
                           System.Diagnostics.Stopwatch.Frequency);
        }

        public static bool TryGetExecutionPipelineXRaySnapshot(
            float[] phaseMilliseconds,
            uint[] bucketLoads,
            out DispatcherStateDTO state)
        {
            state = default;
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return false;

            state = dispatcher._dispatcherState;
            if (phaseMilliseconds != null)
            {
                int count = math.min(phaseMilliseconds.Length, _masterPhaseTimingSnapshotMs.Length);
                for (int i = 0; i < count; i++)
                    phaseMilliseconds[i] = _masterPhaseTimingSnapshotMs[i];
            }

            if (bucketLoads != null)
            {
                int count = math.min(bucketLoads.Length, _masterBucketLoadCounters.Length);
                for (int i = 0; i < count; i++)
                    bucketLoads[i] = _masterBucketLoadCounters[i];
            }

            return true;
        }

        private bool EnsureH8TimeArray()
        {
            if (_h8TimeHandle.IsCreated)
                return true;

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null)
                return false;

            _h8TimeHandle = dataVault.GetBufferHandle<double>(
                BufferID.H8Time,
                (int)H8TimeSlot.Count,
                SystemID.SystemDispatcher,
                NativeArrayOptions.ClearMemory);

            NativeArray<double> h8Time = _h8TimeHandle.Resolve(dataVault);
            if (h8Time.IsCreated && h8Time.Length >= (int)H8TimeSlot.Count)
                return true;

            _h8TimeHandle = default;
            return false;
        }

        private bool TryResolveH8TimeArray(out NativeArray<double> h8Time)
        {
            h8Time = default;
            if (!EnsureH8TimeArray())
                return false;

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null)
                return false;

            NativeArray<double> resolved = _h8TimeHandle.Resolve(dataVault);
            if (!resolved.IsCreated || resolved.Length < (int)H8TimeSlot.Count)
                return false;

            h8Time = resolved;
            return true;
        }

        private void RefreshDataVaultDependency()
        {
            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault != null)
            {
                _dataVault = dataVault;
                _cachedDispatcherDataVault = dataVault;
            }
        }

        private static bool TryResolveCachedDataVault(out IDataVault dataVault)
        {
            dataVault = _cachedDispatcherDataVault;
            if (dataVault != null)
                return true;

            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher != null)
            {
                dataVault = dispatcher._dataVault;
                if (dataVault == null)
                {
                    dispatcher.RefreshDataVaultDependency();
                    dataVault = dispatcher._dataVault;
                }

                if (dataVault != null)
                {
                    _cachedDispatcherDataVault = dataVault;
                    return true;
                }
            }

            dataVault = GlobalRegistry.DataVault;
            if (dataVault == null)
                return false;

            _cachedDispatcherDataVault = dataVault;
            return true;
        }

        private void RefreshSimulationBucketerDependency()
        {
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
        }

        private void RefreshJobAdmissionDependency()
        {
            IJobAdmissionService jobAdmission = GlobalRegistry.JobAdmission;
            if (jobAdmission != null)
                _jobAdmission = jobAdmission;
        }

        private void RefreshInputDeterminismDependency()
        {
            IInputDeterminismService inputDeterminism = GlobalRegistry.InputDeterminism;
            if (inputDeterminism != null)
                _inputDeterminism = inputDeterminism;
        }

        private void RefreshPeripheralDependencies()
        {
            VRAMMonitor vramMonitor = GlobalRegistry.VRAMMonitor;
            if (vramMonitor != null)
                _vramMonitor = vramMonitor;

            VRAMPressureMonitor vramPressure = GlobalRegistry.VRAMPressure;
            if (vramPressure != null)
                _vramPressure = vramPressure;

            IMacroDatabaseService macroDatabase = GlobalRegistry.MacroDatabase;
            if (macroDatabase != null)
                _macroDatabase = macroDatabase;

            ObjectPoolManager objectPool = GlobalRegistry.ObjectPool;
            if (objectPool != null)
                _objectPool = objectPool;
        }

        private static VRAMMonitor ResolveCachedVramMonitor()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return GlobalRegistry.VRAMMonitor;

            VRAMMonitor monitor = dispatcher._vramMonitor;
            if (monitor != null)
                return monitor;

            dispatcher.RefreshPeripheralDependencies();
            return dispatcher._vramMonitor;
        }

        private static VRAMPressureMonitor ResolveCachedVramPressure()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return GlobalRegistry.VRAMPressure;

            VRAMPressureMonitor pressure = dispatcher._vramPressure;
            if (pressure != null)
                return pressure;

            dispatcher.RefreshPeripheralDependencies();
            return dispatcher._vramPressure;
        }

        private static IMacroDatabaseService ResolveCachedMacroDatabase()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return GlobalRegistry.MacroDatabase;

            IMacroDatabaseService macroDatabase = dispatcher._macroDatabase;
            if (macroDatabase != null)
                return macroDatabase;

            dispatcher.RefreshPeripheralDependencies();
            return dispatcher._macroDatabase;
        }

        private static ObjectPoolManager ResolveCachedObjectPool()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return GlobalRegistry.ObjectPool;

            ObjectPoolManager objectPool = dispatcher._objectPool;
            if (objectPool != null)
                return objectPool;

            dispatcher.RefreshPeripheralDependencies();
            return dispatcher._objectPool;
        }

        private void RefreshScalabilityTierProfile()
        {
            _scalabilityTierProfileByte = ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
        }

        private void DrainScalabilityTierSignals()
        {
            System.ReadOnlySpan<ScalabilityChangedEvent> snapshot = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
                _scalabilityTierProfileByte = snapshot[i].CurrentTier;
        }

        private void RunPreSimulationMemoryDefrag(float unscaledDeltaTime)
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null || unscaledDeltaTime < 0f)
                return;

            bool forcedByMemoryPressure = Interlocked.Exchange(ref _criticalMemoryPressureDefragRequested, 0) != 0;
            double cadenceSeconds = _scalabilityTierProfileByte == 0
                ? ColdTickIntervalSeconds
                : FrostTickIntervalSeconds;
            _memoryDefragAccumulator += unscaledDeltaTime;
            if (!forcedByMemoryPressure && _memoryDefragAccumulator < cadenceSeconds)
                return;

            float elapsedSeconds = (float)(_memoryDefragAccumulator > 0d ? _memoryDefragAccumulator : cadenceSeconds);
            _memoryDefragAccumulator = 0d;
            float compactionStress01 = ResolveMemoryCompactionStress01(unscaledDeltaTime);
            uint activeBurstLockMask = dataVault.ActiveBurstLockMask;
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_memoryDefragProfilerMarker.Auto())
            {
                dataVault.FrostTickDefrag(
                    elapsedSeconds,
                    compactionStress01,
                    MemoryDefragPhase.PreSimulation,
                    activeBurstLockMask);
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            PublishMemoryAddressShiftSignals(dataVault);
            PublishDataVaultDefragTelemetry(dataVault, elapsedMilliseconds);
            EmitVramPressureDefragSignalIfNeeded();
        }

        private void RecordMemoryBlackBoxHeartbeat()
        {
            H8Memory.RecordHeartbeat();
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            dataVault?.RecordHeartbeat();
        }

        private void RecordDispatcherBlackBoxHeartbeat(IDataVault dataVault)
        {
            if (dataVault == null || !EnsureDispatcherBlackBox())
                return;

            NativeArray<DispatcherBlackBoxEntry> ring = _dispatcherBlackBoxHandle.Resolve(dataVault);
            NativeArray<int> cursorBuffer = _dispatcherBlackBoxCursorHandle.Resolve(dataVault);
            if (!ring.IsCreated ||
                ring.Length < DispatcherBlackBoxFrameCount ||
                !cursorBuffer.IsCreated ||
                cursorBuffer.Length < 1)
            {
                return;
            }

            int cursor = cursorBuffer[0];
            if ((uint)cursor >= (uint)DispatcherBlackBoxFrameCount)
                cursor = 0;

            bool nonFinite =
                !math.isfinite(CurrentFrameDeltaTime) ||
                !math.isfinite(CurrentFrameUnscaledDeltaTime) ||
                !math.isfinite(_timeDilationScalar) ||
                !math.isfinite(CurrentFixedInterpolationAlpha) ||
                !IsFiniteDouble(_timeSnapshot.Time) ||
                !IsFiniteDouble(_timeSnapshot.UnscaledTime);
            ushort flags = 0;
            if (SimulationPaused)
                flags |= DispatcherBlackBoxFlagPaused;
            if (_aupPreShiftPauseFrame == Time.frameCount)
                flags |= DispatcherBlackBoxFlagAupBarrier;
            if (IsOriginShiftBootstrapLocked || IsOriginShiftFrameLockedForCurrentFrame)
                flags |= DispatcherBlackBoxFlagOriginShiftLock;
            if (nonFinite)
                flags |= DispatcherBlackBoxFlagNonFinite;
            if (_coreTickDilationFramesRemaining > 0 || _coreTickDilationRestorePending)
                flags |= DispatcherBlackBoxFlagCoreDilation;
            if (_temporalCompressionActive)
                flags |= DispatcherBlackBoxFlagTemporalCompression;
            if (_scalabilityTierProfileByte == 0)
                flags |= DispatcherBlackBoxFlagLowTier;
            if (_adrenalineDilationPhase != AdrenalineDilationPhaseNone)
                flags |= DispatcherBlackBoxFlagAdrenalineDilation;

            DispatcherBlackBoxEntry entry = default;
            entry.Frame = unchecked((uint)math.max(0, Time.frameCount));
            entry.Sequence = ++_dispatcherBlackBoxSequence;
            entry.DilatedTime = IsFiniteDouble(_timeSnapshot.Time) && _timeSnapshot.Time >= 0d ? _timeSnapshot.Time : 0d;
            entry.UnscaledTime = IsFiniteDouble(_timeSnapshot.UnscaledTime) && _timeSnapshot.UnscaledTime >= 0d ? _timeSnapshot.UnscaledTime : 0d;
            entry.DeltaTime = math.isfinite(CurrentFrameDeltaTime) && CurrentFrameDeltaTime >= 0f ? CurrentFrameDeltaTime : 0f;
            entry.UnscaledDeltaTime = math.isfinite(CurrentFrameUnscaledDeltaTime) && CurrentFrameUnscaledDeltaTime >= 0f ? CurrentFrameUnscaledDeltaTime : 0f;
            entry.TimeDilationScalar = math.isfinite(_timeDilationScalar)
                ? math.clamp(_timeDilationScalar, TimeDilationMinimumScalar, HeadlessTimeDilationMaximumScalar)
                : 0f;
            entry.TickOverheadMilliseconds = SanitizeNonNegativeMilliseconds(ResolveCurrentFrameMilliseconds());
            entry.Flags = flags;
            entry.PendingRaycasts = unchecked((ushort)math.min(ushort.MaxValue, math.max(0, _pendingDispatcherRaycastCount)));
            entry.ScheduledRaycasts = unchecked((ushort)math.min(ushort.MaxValue, math.max(0, _scheduledDispatcherRaycastCount)));
            entry.HomeostasisPressureLevel = HomeostasisPressureLevel;
            entry.HomeostasisFoveatedTier = HomeostasisFoveatedTier;
            entry.AupPreShiftSequence = _aupPreShiftPauseSequence;
            entry.StateHash = ResolveDispatcherStateHash(flags);
            entry.KillSwitchMask = KillSwitchMask;
            ring[cursor] = entry;

            cursor++;
            if (cursor >= DispatcherBlackBoxFrameCount)
                cursor = 0;
            cursorBuffer[0] = cursor;

            if (nonFinite && !_dispatcherBlackBoxDumped)
            {
                _dispatcherBlackBoxDumped = true;
                DumpDispatcherBlackBox(ring, cursor);
                PublishDispatcherComplianceViolation(_DispatcherBlackBoxFaultHash, _SystemDispatcherHash, 4, 1);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_DispatcherBlackBoxFaultHash));
            }
        }

        private uint ResolveDispatcherStateHash(ushort flags)
        {
            uint hash = 2166136261u;
            hash = unchecked((hash ^ (uint)Time.frameCount) * 16777619u);
            hash = unchecked((hash ^ (uint)_pendingDispatcherRaycastCount) * 16777619u);
            hash = unchecked((hash ^ (uint)_scheduledDispatcherRaycastCount) * 16777619u);
            hash = unchecked((hash ^ (uint)flags) * 16777619u);
            hash = unchecked((hash ^ (uint)HomeostasisPressureLevel) * 16777619u);
            hash = unchecked((hash ^ (uint)HomeostasisFoveatedTier) * 16777619u);
            return hash;
        }

        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void DumpDispatcherBlackBox(NativeArray<DispatcherBlackBoxEntry> ring, int cursor)
        {
            if (!ring.IsCreated || ring.Length < DispatcherBlackBoxFrameCount)
                return;

            try
            {
                System.IO.Directory.CreateDirectory("Docs/AgentLogs");
                WriteDispatcherBlackBoxDump(DispatcherBlackBoxDumpPath, ring, cursor);
                WriteDispatcherBlackBoxDump(DispatcherBlackBoxMirrorDumpPath, ring, cursor);
            }
            catch (System.IO.IOException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }
        }

        private static void WriteDispatcherBlackBoxDump(
            string path,
            NativeArray<DispatcherBlackBoxEntry> ring,
            int cursor)
        {
            using (System.IO.FileStream stream = System.IO.File.Open(
                path,
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.Read))
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(DispatcherBlackBoxDumpMagic);
                writer.Write(DispatcherBlackBoxDumpVersion);
                writer.Write(DispatcherBlackBoxFrameCount);
                writer.Write(DispatcherBlackBoxEntrySizeBytes);
                writer.Write(cursor);
                for (int i = 0; i < DispatcherBlackBoxFrameCount; i++)
                {
                    int index = cursor + i;
                    if (index >= DispatcherBlackBoxFrameCount)
                        index -= DispatcherBlackBoxFrameCount;

                    DispatcherBlackBoxEntry entry = ring[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.DilatedTime);
                    writer.Write(entry.UnscaledTime);
                    writer.Write(entry.DeltaTime);
                    writer.Write(entry.UnscaledDeltaTime);
                    writer.Write(entry.TimeDilationScalar);
                    writer.Write(entry.TickOverheadMilliseconds);
                    writer.Write(entry.Flags);
                    writer.Write(entry.PendingRaycasts);
                    writer.Write(entry.ScheduledRaycasts);
                    writer.Write(entry.HomeostasisPressureLevel);
                    writer.Write(entry.HomeostasisFoveatedTier);
                    writer.Write(entry.AupPreShiftSequence);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.KillSwitchMask);
                }
            }
        }

        private static float ResolveMemoryCompactionStress01(float unscaledDeltaTime)
        {
            if (!math.isfinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
                return 1f;

            float frameStress = math.saturate(unscaledDeltaTime / 0.05f);
            float pressureStress = math.saturate(HomeostasisPressureLevel * 0.125f);
            return math.max(frameStress, pressureStress);
        }

        private static void PublishMemoryAddressShiftSignals(IDataVault dataVault)
        {
            int recordCount = dataVault.LastRelocationRecordCount;
            for (int i = 0; i < recordCount; i++)
            {
                if (!dataVault.TryGetLastRelocationRecord(i, out VaultRelocationRecord record))
                    break;

                MemoryAddressShiftSignal signal = default;
                signal.OldPointer = record.OldPointer;
                signal.NewPointer = record.NewPointer;
                signal.BufferId = record.BufferId;
                signal.ByteLength = record.ByteLength;
                signal.Version = record.Generation;
                signal.Flags = record.Flags;
                signal.SystemId = record.SystemId;
                GlobalSignals.Publish(in signal);
            }
        }

        private void PublishDataVaultDefragTelemetry(IDataVault dataVault, double elapsedMilliseconds)
        {
            float vaultPressure01 = dataVault.CapacityPressure01;
            if (vaultPressure01 >= 0.8f)
            {
                MemoryPressureSignal pressureSignal = default;
                pressureSignal.ReservedMemoryBytes = dataVault.AllocatedBytes;
                pressureSignal.PhysicalMemoryBytes = dataVault.ArenaBytes;
                pressureSignal.UsageRatio = vaultPressure01;
                pressureSignal.Frame = unchecked((uint)Time.frameCount);
                pressureSignal.Severity = vaultPressure01 >= 0.95f ? (byte)2 : (byte)1;
                pressureSignal.Flags = 2;
                GlobalSignals.Publish(in pressureSignal);
            }

            if (dataVault.HeapFragmentationRatio > 0f)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _HeapFragmentationRatioHash,
                    _DataVaultDefragContextHash,
                    dataVault.HeapFragmentationRatio);
            }

            if (dataVault.LastDefragMovedBytes > 0L)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultMovedBytesHash,
                    _DataVaultDefragContextHash,
                    dataVault.LastDefragMovedBytes * GlobalTelemetryBus.BytesToMegabytes);
            }

            if (dataVault.LastDefragWatchdogExceeded || elapsedMilliseconds > 1.0d)
            {
                GlobalTelemetryBus.PublishJobBarrierStall(
                    nameof(GlobalDataVault),
                    nameof(RunPreSimulationMemoryDefrag),
                    (float)elapsedMilliseconds);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultWatchdogHash,
                    _DataVaultDefragContextHash,
                    (float)elapsedMilliseconds);
            }

            if (dataVault.PendingMassiveMoveBytes >= 50L * 1024L * 1024L &&
                _lastMemoryDefragPressureWarningFrame != Time.frameCount)
            {
                _lastMemoryDefragPressureWarningFrame = Time.frameCount;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultMassiveMoveHash,
                    _DataVaultDefragContextHash,
                    dataVault.PendingMassiveMoveBytes * GlobalTelemetryBus.BytesToMegabytes);
            }
        }

        private static void EmitVramPressureDefragSignalIfNeeded()
        {
            VRAMMonitor monitor = ResolveCachedVramMonitor();
            if (monitor == null || monitor.TotalVRAMBytes <= 1800L * 1024L * 1024L)
                return;

            GlobalTelemetryBus.PublishVRAMWarningEvent(monitor.TotalVRAMBytes);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DataVaultVramPressureHash,
                _DataVaultDefragContextHash,
                monitor.TotalVRAMBytes * GlobalTelemetryBus.BytesToMegabytes);

            VRAMPressureMonitor pressureMonitor = ResolveCachedVramPressure();
            if (pressureMonitor != null)
                pressureMonitor.ForceImmediateSampleAndResponse();
        }

        private void DisposeH8TimeArray()
        {
            _h8TimeHandle = default;
        }

        private bool EnsureDispatcherBlackBox()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null)
                return false;

            if (!_dispatcherBlackBoxHandle.IsCreated || !dataVault.ResolveBuffer(ref _dispatcherBlackBoxHandle))
            {
                _dispatcherBlackBoxHandle = dataVault.GetBufferHandle<DispatcherBlackBoxEntry>(
                    BufferID.SystemDispatcherBlackBox,
                    DispatcherBlackBoxFrameCount,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_dispatcherBlackBoxCursorHandle.IsCreated || !dataVault.ResolveBuffer(ref _dispatcherBlackBoxCursorHandle))
            {
                _dispatcherBlackBoxCursorHandle = dataVault.GetBufferHandle<int>(
                    BufferID.SystemDispatcherBlackBoxCursor,
                    1,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
            }

            NativeArray<DispatcherBlackBoxEntry> ring = _dispatcherBlackBoxHandle.Resolve(dataVault);
            NativeArray<int> cursor = _dispatcherBlackBoxCursorHandle.Resolve(dataVault);
            if (ring.IsCreated &&
                ring.Length >= DispatcherBlackBoxFrameCount &&
                cursor.IsCreated &&
                cursor.Length >= 1)
            {
                return true;
            }

            _dispatcherBlackBoxHandle = default;
            _dispatcherBlackBoxCursorHandle = default;
            return false;
        }

        private void DisposeDispatcherBlackBox()
        {
            _dispatcherBlackBoxHandle = default;
            _dispatcherBlackBoxCursorHandle = default;
        }

        private void UpdateH8TimeState(float dilatedDeltaTime, float unscaledDeltaTime)
        {
            if (!TryResolveH8TimeArray(out NativeArray<double> h8Time))
                return;

            float safeDilatedDeltaTime = math.isfinite(dilatedDeltaTime) && dilatedDeltaTime >= 0f ? dilatedDeltaTime : 0f;
            float safeUnscaledDeltaTime = math.isfinite(unscaledDeltaTime) && unscaledDeltaTime >= 0f ? unscaledDeltaTime : 0f;
            double previousDilatedTime = h8Time[(int)H8TimeSlot.Time];
            if (!IsFiniteDouble(previousDilatedTime) || previousDilatedTime < 0d)
                previousDilatedTime = 0d;

            double dilatedTime = previousDilatedTime + safeDilatedDeltaTime;
            if (!IsFiniteDouble(dilatedTime) || dilatedTime < 0d)
                dilatedTime = previousDilatedTime;

            double unscaledTime = Time.unscaledTimeAsDouble;
            if (!IsFiniteDouble(unscaledTime) || unscaledTime < 0d)
                unscaledTime = h8Time[(int)H8TimeSlot.UnscaledTime];
            if (!IsFiniteDouble(unscaledTime) || unscaledTime < 0d)
                unscaledTime = 0d;

            h8Time[(int)H8TimeSlot.Time] = dilatedTime;
            h8Time[(int)H8TimeSlot.DeltaTime] = safeDilatedDeltaTime;
            h8Time[(int)H8TimeSlot.UnscaledTime] = unscaledTime;
            h8Time[(int)H8TimeSlot.UnscaledDeltaTime] = safeUnscaledDeltaTime;
            _timeSnapshot = new H8TimeSnapshot(dilatedTime, safeDilatedDeltaTime, unscaledTime, safeUnscaledDeltaTime);
        }

        private float ResolveFrameTimeDilationScalar()
        {
            if (_simulationPaused || _timeDilationScalar <= TimeDilationPausedEpsilon)
                return 0f;

            if (_coreTickDilationFramesRemaining <= 0)
                return _timeDilationScalar;

            float scalar = _coreTickDilationScalar;
            _coreTickDilationFramesRemaining--;
            if (_coreTickDilationFramesRemaining == 0)
            {
                _coreTickDilationScalar = 1f;
                _coreTickDilationRestorePending = true;
            }

            return scalar;
        }

        private float ResolvePauseRestoreScalar()
        {
            float restoreScalar = _coreTickDilationFramesRemaining > 0 || _coreTickDilationRestorePending
                ? _coreTickDilationRestoreScalar
                : _timeDilationScalar;
            return math.isfinite(restoreScalar)
                ? math.max(TimeDilationPausedEpsilon, restoreScalar)
                : 1f;
        }

        private void ClearCoreTickDilationBurst()
        {
            _coreTickDilationScalar = 1f;
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationFramesRemaining = 0;
            _coreTickDilationReasonHash = 0u;
            _coreTickDilationRestorePending = false;
        }

        private void ClearAdrenalineDilation()
        {
            _adrenalineDilationStartScalar = 1f;
            _adrenalineDilationRestoreScalar = 1f;
            _adrenalineDilationElapsedSeconds = 0f;
            _adrenalineDilationSourceHash = 0u;
            _adrenalineDilationPhase = AdrenalineDilationPhaseNone;
        }

        private void DrainAdrenalineDilationSignals(float unscaledDeltaTime)
        {
            if (_simulationPaused)
                return;

            bool triggered = TryResolveAdrenalineDilationTrigger(out uint sourceHash);
            if (triggered &&
                (_adrenalineDilationPhase == AdrenalineDilationPhaseNone ||
                 _adrenalineDilationPhase == AdrenalineDilationPhaseRestore))
            {
                BeginAdrenalineDilation(sourceHash);
            }

            if (_adrenalineDilationPhase == AdrenalineDilationPhaseNone)
                return;

            if (!triggered && _adrenalineDilationPhase == AdrenalineDilationPhaseHold)
                BeginAdrenalineDilationRestore();

            TickAdrenalineDilation(unscaledDeltaTime);
        }

        private static bool TryResolveAdrenalineDilationTrigger(out uint sourceHash)
        {
            sourceHash = 0u;
            System.ReadOnlySpan<SystemHealthIndexSignal> snapshot = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                SystemHealthIndexSignal signal = snapshot[i];
                bool explicitAdrenaline = (signal.Flags & SystemHealthIndexSignal.FlagAdrenaline) != 0;
                bool lowHealth = math.isfinite(signal.Health01) && signal.Health01 <= AdrenalineHealthThreshold01;
                if (!explicitAdrenaline && !lowHealth)
                    continue;

                sourceHash = signal.SourceHash != 0u ? signal.SourceHash : AdrenalineDilationReasonHash;
                return true;
            }

            return false;
        }

        private void BeginAdrenalineDilation(uint sourceHash)
        {
            float currentScalar = math.isfinite(_timeDilationScalar)
                ? math.clamp(_timeDilationScalar, TimeDilationPausedEpsilon, TimeDilationMaximumScalar)
                : 1f;
            float targetScalar = math.min(currentScalar, AdrenalineTargetTimeDilationScalar);
            _adrenalineDilationStartScalar = currentScalar;
            _adrenalineDilationRestoreScalar = currentScalar;
            _adrenalineDilationElapsedSeconds = 0f;
            _adrenalineDilationSourceHash = sourceHash != 0u ? sourceHash : AdrenalineDilationReasonHash;
            _adrenalineDilationPhase = currentScalar <= targetScalar + 0.0001f
                ? AdrenalineDilationPhaseHold
                : AdrenalineDilationPhaseRampDown;
        }

        private void BeginAdrenalineDilationRestore()
        {
            _adrenalineDilationStartScalar = math.isfinite(_timeDilationScalar)
                ? math.clamp(_timeDilationScalar, TimeDilationPausedEpsilon, TimeDilationMaximumScalar)
                : AdrenalineTargetTimeDilationScalar;
            _adrenalineDilationElapsedSeconds = 0f;
            _adrenalineDilationPhase = AdrenalineDilationPhaseRestore;
        }

        private void TickAdrenalineDilation(float unscaledDeltaTime)
        {
            if (_coreTickDilationFramesRemaining > 0 || _coreTickDilationRestorePending)
                return;

            float safeDeltaTime = math.isfinite(unscaledDeltaTime) && unscaledDeltaTime > 0f ? unscaledDeltaTime : 0f;
            _adrenalineDilationElapsedSeconds = math.min(
                AdrenalineRampSeconds,
                _adrenalineDilationElapsedSeconds + safeDeltaTime);
            float t = math.saturate(_adrenalineDilationElapsedSeconds * AdrenalineInvRampSeconds);
            if (_adrenalineDilationPhase == AdrenalineDilationPhaseRampDown)
            {
                float scalar = math.lerp(
                    _adrenalineDilationStartScalar,
                    AdrenalineTargetTimeDilationScalar,
                    t);
                SetTimeDilationScalar(scalar, _adrenalineDilationSourceHash, publishImmediate: true);
                if (t >= 1f)
                    _adrenalineDilationPhase = AdrenalineDilationPhaseHold;
                return;
            }

            if (_adrenalineDilationPhase != AdrenalineDilationPhaseRestore)
                return;

            float restoredScalar = math.lerp(
                _adrenalineDilationStartScalar,
                _adrenalineDilationRestoreScalar,
                t);
            SetTimeDilationScalar(restoredScalar, _adrenalineDilationSourceHash, publishImmediate: true);
            if (t < 1f)
                return;

            float finalScalar = math.isfinite(_adrenalineDilationRestoreScalar)
                ? math.clamp(_adrenalineDilationRestoreScalar, TimeDilationPausedEpsilon, TimeDilationMaximumScalar)
                : 1f;
            ClearAdrenalineDilation();
            SetTimeDilationScalar(finalScalar, AdrenalineDilationReasonHash, publishImmediate: true);
        }

        private void ApplyPendingCoreTickDilationRestore()
        {
            if (!_coreTickDilationRestorePending)
                return;

            _coreTickDilationRestorePending = false;
            SetTimeDilationScalar(_coreTickDilationRestoreScalar, _coreTickDilationReasonHash, publishImmediate: true);
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationReasonHash = 0u;
        }

        private void RunDispatcherUpdate()
        {
            GlobalRegistry.PublishAbsoluteUniverseTime(Time.timeAsDouble);
            GlobalRegistry.TickMathPrecisionTransition(Time.frameCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherUpdate);
#endif
            using (_updateProfilerMarker.Auto())
            {
#if UNITY_EDITOR
                NativeAllocationTrackerRuntimeBridge.NotifyDispatcherHeartbeat();
#endif
                ApplyPendingCoreTickDilationRestore();
                if (BootstrapStatus.TryTriggerSafeHalt())
                    BootstrapBiosErrorOverlay.Show(BootstrapStatus.SafeHaltDisplayMessage);

                long dispatcherTickStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                long preSimulationStartTimestamp = dispatcherTickStartTimestamp;
                HectonXRRuntimeState.RefreshFrameState(Time.frameCount);
                float measuredUnscaledDeltaTime = HectonXRRuntimeState.IsXRActive ? Time.smoothDeltaTime : Time.unscaledDeltaTime;
                float unscaledDeltaTime = HectonXRRuntimeState.ResolveDispatcherDeltaTime(measuredUnscaledDeltaTime);
                if (!math.isfinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
                    unscaledDeltaTime = 0f;
                bool previousFrameMissedBudget = CurrentFrameUnscaledDeltaTime > JobAdmissionFrameBudgetMissThresholdSeconds;
                CurrentFrameUnscaledDeltaTime = unscaledDeltaTime;
                HomeostasisBrain.PreSimulationTick(unscaledDeltaTime);
                IInputDeterminismService inputDeterminism = _inputDeterminism;
                if (inputDeterminism == null || !inputDeterminism.IsInitialized)
                {
                    RefreshInputDeterminismDependency();
                    inputDeterminism = _inputDeterminism;
                }

                inputDeterminism?.PreSimulationInputTick(unscaledDeltaTime);
                GlobalSignals.FlushPreSimulation();
                if (SignalBusRegistry.IsSimulationHalted)
                    return;

                DrainScalabilityTierSignals();
                byte scalabilityTierProfile = _scalabilityTierProfileByte;
                RecordMemoryBlackBoxHeartbeat();
                RunPreSimulationMemoryDefrag(unscaledDeltaTime);
                IJobAdmissionService jobAdmission = _jobAdmission;
                if (jobAdmission == null || !jobAdmission.IsInitialized)
                {
                    RefreshJobAdmissionDependency();
                    jobAdmission = _jobAdmission;
                }

                jobAdmission?.Refill(
                    scalabilityTierProfile,
                    unscaledDeltaTime,
                    previousFrameMissedBudget);
                Hecton8.Modding.ModCommandDispatcher.DrainPreSimulation();
                DrainSimulationPauseSignals();
                DrainAdrenalineDilationSignals(unscaledDeltaTime);
                float frameDilationScalar = ResolveFrameTimeDilationScalar();
                if (!math.isfinite(frameDilationScalar) || frameDilationScalar < 0f)
                    frameDilationScalar = 0f;

                float deltaTime = unscaledDeltaTime * frameDilationScalar;
                if (!math.isfinite(deltaTime) || deltaTime < 0f)
                    deltaTime = 0f;
                CurrentFrameDeltaTime = deltaTime;
                DispatcherTimingDTO masterTiming = BuildMasterDispatcherTiming(deltaTime, unscaledDeltaTime);
                RunMasterPreSimulationPhase(in masterTiming);
                UpdateH8TimeState(deltaTime, unscaledDeltaTime);
                PublishTimeDilationState(0u);
                IDataVault dispatcherDataVault = _dataVault;
                if (dispatcherDataVault == null && TryResolveCachedDataVault(out dispatcherDataVault))
                    _dataVault = dispatcherDataVault;
                RecordDispatcherBlackBoxHeartbeat(dispatcherDataVault);
                float preSimulationCostMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - preSimulationStartTimestamp) * 1000.0 /
                                                    System.Diagnostics.Stopwatch.Frequency);
                ISimulationBucketer simulationBucketer = _simulationBucketer;
                if (simulationBucketer == null || !simulationBucketer.IsInitialized)
                {
                    RefreshSimulationBucketerDependency();
                    simulationBucketer = _simulationBucketer;
                }

                bool aupBarrierActive = IsOriginShiftBootstrapLocked ||
                                        IsOriginShiftFrameLockedForCurrentFrame ||
                                        _aupPreShiftPauseFrame == Time.frameCount;
                if (simulationBucketer != null && simulationBucketer.IsInitialized)
                {
                    simulationBucketer.ReportPreSimulationCostMs(preSimulationCostMs);
                    simulationBucketer.AdvanceFrame(
                        scalabilityTierProfile,
                        unscaledDeltaTime,
                        jobAdmission != null ? jobAdmission.CriticalDebtFrameCount : 0,
                        aupBarrierActive);
                    SimulationBucketFrameState bucketFrameState = simulationBucketer.CaptureFrameState();
                    PublishSimulationBucketSync(in bucketFrameState);
                    PublishFramePacingWarningIfNeeded(in bucketFrameState);
                }

                if (IsOriginShiftBootstrapLocked)
                {
                    if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())
                        return;

                    if (IsOriginShiftBootstrapLocked)
                        return;
                }

                if (IsOriginShiftFrameLockedForCurrentFrame)
                    return;

                if (_aupPreShiftPauseFrame == Time.frameCount)
                {
                    RunUnscaledFastTick(unscaledDeltaTime, blockGameplayLanes: false);
                    return;
                }

                masterTiming = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime);
                RunMasterSimulationPhase(in masterTiming);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long beginDispatcherTimestamp = BeginDispatcherPhaseTiming();
#endif
                _foveatedSimulationManager.BeginDispatcherFrame(deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EndDispatcherPhaseTiming(beginDispatcherTimestamp, "FoveatedSimulationManager.BeginDispatcherFrame");
#endif
                PredatorCognitionDomain.BeginDispatcherFrame(Time.frameCount);
                bool blockGameplayLanes = Application.isPlaying &&
                                          BootstrapState.HasActiveInstance &&
                                          !BootstrapState.IsGameReady;
                long bucketWorkStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (SignalBusRegistry.IsSimulationHalted)
                        return;

                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IUpdatable> lane = _priorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUpdatable));
#endif
                    using (_updateLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IUpdatable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            IUpdatable updatable = rawArray[itemIndex];
                            if (!_foveatedSimulationManager.TryResolveTick(updatable, deltaTime, out float effectiveDeltaTime))
                                continue;

                            updatable.Tick(effectiveDeltaTime);
                            _foveatedSimulationManager.NotifyTickCompleted(updatable);
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;

                            if (IsOriginShiftFrameLockedForCurrentFrame)
                                return;
                        }
                    }
                }

                if (SignalBusRegistry.IsSimulationHalted)
                    return;

                CombatDamageRuntime.FrameTick(deltaTime);
                PredatorCognitionDomain.ScheduleFrameEvaluation(Time.frameCount);
                _foveatedSimulationManager.ScheduleFrameJobs();
                RunFastTick(deltaTime, blockGameplayLanes);
                RunUnscaledFastTick(CurrentFrameUnscaledDeltaTime, blockGameplayLanes: false);
                RunFixedStepAccumulator(deltaTime, blockGameplayLanes);
                RunBucketedSlowTick(blockGameplayLanes);
                RunSlowTick(deltaTime, blockGameplayLanes);
                RunColdTick(deltaTime, blockGameplayLanes);
                RunFrostTick(deltaTime, blockGameplayLanes);
                ScheduleDispatcherRaycasts();
                masterTiming = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime);
                RunMasterPostSimulationPhase(in masterTiming);
                IModdingBridge moddingBridge = _moddingBridgeProjectionRuntime;
                if (moddingBridge != null)
                    moddingBridge.ProjectPostSimulation();
                if (simulationBucketer != null)
                {
                    float activeBucketLoadMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - bucketWorkStartTimestamp) * 1000.0 /
                                                       System.Diagnostics.Stopwatch.Frequency);
                    simulationBucketer.ReportActiveBucketLoadMs(activeBucketLoadMs);
                    SimulationBucketFrameState bucketFrameState = simulationBucketer.CaptureFrameState();
                    PublishFramePacingWarningIfNeeded(in bucketFrameState);
                    CrashTelemetryBuffer.ReportSimulationBucketFrame(bucketFrameState);
                }

                double tickOverheadMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - dispatcherTickStartTimestamp) * 1000.0 /
                    System.Diagnostics.Stopwatch.Frequency;
                CrashTelemetryBuffer.ReportTimeDilationState(_timeDilationScalar, tickOverheadMilliseconds);
            }
        }

        private void PublishSimulationBucketSync(in SimulationBucketFrameState frameState)
        {
            float alpha = math.isfinite(frameState.SimulationBucketInterpolationAlpha)
                ? math.saturate(frameState.SimulationBucketInterpolationAlpha)
                : 0f;
            Shader.SetGlobalFloat(_SimulationBucketInterpolationAlphaId, alpha);

            SimulationBucketSyncSignal signal = default;
            signal.InterpolationAlpha = alpha;
            signal.Frame = unchecked((uint)math.max(0, frameState.CurrentFrameCount));
            signal.ActiveSlowBucket = frameState.ActiveSlowBucket;
            signal.SlowBucketMask = frameState.SlowBucketMask;
            signal.RebalanceSequence = frameState.RebalanceSequence;
            signal.ActiveSlowBucketCount = frameState.ActiveSlowBucketCount;
            signal.Flags = unchecked((byte)math.min(byte.MaxValue, frameState.FramePacingFlags));
            SignalBus<SimulationBucketSyncSignal>.Push(in signal);
        }

        private void PublishFramePacingWarningIfNeeded(in SimulationBucketFrameState frameState)
        {
            uint flags = frameState.FramePacingFlags;
            uint criticalFlags =
                SimulationBucketPacingFlags.Impossible60Fps |
                SimulationBucketPacingFlags.PreSimulationOverBudget |
                SimulationBucketPacingFlags.NonFiniteCost;
            if ((flags & criticalFlags) == 0u)
                return;

            int currentFrame = Time.frameCount;
            float currentFrameMs = ResolveCurrentFrameMilliseconds();
            if ((flags & SimulationBucketPacingFlags.HomeostasisKillRequested) != 0u &&
                _lastFramePacingHomeostasisFrame != currentFrame)
            {
                _lastFramePacingHomeostasisFrame = currentFrame;
                ApplyFramePacingHomeostasisKill();
            }

            if (_lastFramePacingWarningFrame == currentFrame)
                return;

            _lastFramePacingWarningFrame = currentFrame;
            FramePacingWarningSignal warning = default;
            warning.Frame = unchecked((uint)math.max(0, currentFrame));
            warning.SourceHash = _FramePacingWarningHash;
            warning.Flags = flags;
            warning.CurrentFrameMs = currentFrameMs;
            warning.TargetFrameMs = SimulationBucketConstants.TargetFrameMilliseconds;
            warning.PreSimulationMs = SanitizeNonNegativeMilliseconds(frameState.PreSimulationCostMs);
            warning.ActiveBucketLoadMs = SanitizeNonNegativeMilliseconds(frameState.ActiveBucketLoadMs);
            warning.JitterVarianceMs = SanitizeNonNegativeMilliseconds(frameState.JitterVarianceMs);
            warning.ExpectedMaxBucketLoadMs = SanitizeNonNegativeMilliseconds(frameState.ExpectedMaxBucketLoadMs);
            warning.ExpectedMeanBucketLoadMs = SanitizeNonNegativeMilliseconds(frameState.ExpectedMeanBucketLoadMs);
            warning.ActiveSlowBucket = frameState.ActiveSlowBucket;
            warning.SlowBucketMask = frameState.SlowBucketMask;
            warning.RebalanceSequence = frameState.RebalanceSequence;
            warning.Severity = ResolveFramePacingSeverity(flags, currentFrameMs, warning.PreSimulationMs);
            SignalBus<FramePacingWarningSignal>.Push(in warning);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _FramePacingWarningHash,
                _SimulationBucketContextHash,
                currentFrameMs);
        }

        private static void ApplyFramePacingHomeostasisKill()
        {
            ulong mask = KillSwitchMask | _FramePacingEmergencyKillMask;
            byte pressureLevel = HomeostasisPressureLevel >= 3 ? HomeostasisPressureLevel : (byte)3;
            byte foveatedTier = HomeostasisFoveatedTier >= 3 ? HomeostasisFoveatedTier : (byte)3;
            ApplyHomeostasisKillSwitch(
                mask,
                pressureLevel,
                foveatedTier,
                slowTick2Hz: true,
                forceTimeDilation09: true,
                _FramePacingWarningHash);
            GlobalRegistry.SetSystemKillSwitchBits(GlobalRegistry.SystemKillSwitchLane4VfxMask, true);
        }

        private static float ResolveCurrentFrameMilliseconds()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
                return 0f;

            return deltaTime * 1000f;
        }

        private static float SanitizeNonNegativeMilliseconds(float milliseconds)
        {
            return math.isfinite(milliseconds) && milliseconds > 0f ? milliseconds : 0f;
        }

        private static byte ResolveFramePacingSeverity(uint flags, float currentFrameMs, float preSimulationMs)
        {
            byte severity = 1;
            if ((flags & SimulationBucketPacingFlags.PreSimulationOverBudget) != 0u ||
                preSimulationMs > SimulationBucketConstants.PreSimulationBudgetMilliseconds)
                severity = 2;
            if ((flags & SimulationBucketPacingFlags.Impossible60Fps) != 0u ||
                currentFrameMs > SimulationBucketConstants.TargetFrameMilliseconds)
                severity = 3;
            if ((flags & SimulationBucketPacingFlags.NonFiniteCost) != 0u)
                severity = 4;

            return severity;
        }

        private static void RefreshPauseDepthOfFieldTarget()
        {
            bool active = _pauseMenuDepthOfFieldRequested || _pdaDepthOfFieldRequested;
            float targetWeight = active ? 1f : 0f;
            if (_pauseDepthOfFieldTargetActive == active &&
                math.abs(_pauseDepthOfFieldTargetWeight - targetWeight) <= 0.0001f)
                return;

            float now = Time.unscaledTime;
            _pauseDepthOfFieldBlendStartWeight = ResolvePauseDepthOfFieldWeight(now);
            _pauseDepthOfFieldWeight = _pauseDepthOfFieldBlendStartWeight;
            _pauseDepthOfFieldTargetWeight = targetWeight;
            _pauseDepthOfFieldBlendStartTime = now;
            _pauseDepthOfFieldTargetActive = active;
        }

        private static float ResolvePauseDepthOfFieldWeight(float unscaledTime)
        {
            float normalized = PauseDepthOfFieldBlendSeconds > 0f
                ? math.saturate((unscaledTime - _pauseDepthOfFieldBlendStartTime) / PauseDepthOfFieldBlendSeconds)
                : 1f;
            return math.lerp(_pauseDepthOfFieldBlendStartWeight, _pauseDepthOfFieldTargetWeight, normalized);
        }

        private static ICameraJuiceSystem ResolveCameraJuiceSystem()
        {
            ICameraJuiceSystem cameraJuice = _cachedCameraJuiceSystem;
            if (cameraJuice != null)
                return cameraJuice;

            int frame = Time.frameCount;
            if (frame < _nextCameraJuiceResolveFrame)
                return null;

            _nextCameraJuiceResolveFrame = frame + CameraJuiceResolveRetryFrames;
            cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
            {
                _cachedCameraJuiceSystem = cameraJuice;
                _nextCameraJuiceResolveFrame = 0;
            }

            return cameraJuice;
        }

        private static void TickPauseDepthOfField(float unscaledTime)
        {
            _pauseDepthOfFieldWeight = ResolvePauseDepthOfFieldWeight(unscaledTime);

            ICameraJuiceSystem cameraJuice = ResolveCameraJuiceSystem();
            if (cameraJuice != null)
                cameraJuice.ApplyPauseDepthOfFieldWeight(_pauseDepthOfFieldWeight);
        }

        private void RunDispatcherLateFrame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherLateFrame);
            long completeDispatcherTimestamp = 0L;
            bool dispatcherPhaseTimingStarted = false;
#endif
            if (IsOriginShiftBootstrapLocked)
                return;
            if (IsOriginShiftFrameLockedForCurrentFrame)
            {
                _dataVault?.UnlockAllocationsAfterAupShift(_aupPreShiftPauseSequence);
                return;
            }

            try
            {
            DispatcherJobSwap.BeginLateFrameSwapWindow();
            try
            {
                CompleteDispatcherRaycasts();
                UpdatePauseFreezeFrameDitherState();
                UpdateVisualStaticGlitchState();
                TickPauseDepthOfField(Time.unscaledTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                completeDispatcherTimestamp = BeginDispatcherPhaseTiming();
                dispatcherPhaseTimingStarted = true;
#endif
                CompleteFoveatedFrameJobs();
                RunMasterVisualSyncPhase();
                BeginLateFrameEventBudget();
                SetActiveLateFrameEventLane(_LateFrameTickablesQueueHash);
                using (_lateFrameProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        RegistryBucket<ILateFrameTickable> lane = _lateFramePriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ILateFrameTickable));
#endif
                        ILateFrameTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;
                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            rawArray[itemIndex].LateFrameTick();
                    }
                }
                AcousticOcclusionUtility.LateFrameTick();
                PredatorCognitionDomain.LateFrameTick();
                CombatDamageRuntime.LateFrameTick();
                VoxelDynamicNavGridRuntime.CompletePendingDynamicObstacleUpdates();
            }
            finally
            {
                DispatcherJobSwap.EndLateFrameSwapWindow();
            }

            using (_lateFrameCommandQueueDrainProfilerMarker.Auto())
            {
                SetActiveLateFrameEventLane(_ThreadSafeCommandQueueHash);
                ReportLateFrameQueueDepth(_ThreadSafeCommandQueueHash, ThreadSafeCommandQueue.PendingCount);
                if (!ThreadSafeCommandQueue.DrainMainThread())
                    MarkLateFrameEventDispatchDeferred();
            }
            SetActiveLateFrameEventLane(_StorageReservationCommitResolvedQueueHash);
            ReportLateFrameQueueDepth(_StorageReservationCommitResolvedQueueHash, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
            if (!ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents())
                MarkLateFrameEventDispatchDeferred();
            SetActiveLateFrameEventLane(_ModCommandDispatcherQueueHash);
            Hecton8.Modding.ModCommandDispatcher.DrainLateFrame();
            FlushCoreEventsArtery();
            if (!SimulationPaused)
            {
                FlushEnvironmentEventsArtery();
                FlushPlayerEventsArtery();
                FlushBaseEventsArtery();
                FlushAIEventsArtery();
            }
            }
            finally
            {
                ClearActiveLateFrameEventLane();
                if (_lateFrameEventBudgetActive)
                    EndLateFrameEventBudget();

                try
                {
                    GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime);
                    WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount);
                }
                finally
                {
                    GlobalSignals.ClearPostSimulationSnapshots();
                    NativeArenaAllocator.Reset();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (dispatcherPhaseTimingStarted)
                        EndDispatcherPhaseTiming(completeDispatcherTimestamp, "FoveatedSimulationManager.CompleteFrameJobs");
#endif
                }
            }
        }

        private static void FlushCoreEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_CoreEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _CoreEventsArteryHash,
                    Hecton8.Modding.ModRegistryEvents.PendingCount +
                    BootstrapEvents.PendingCount +
                    Hecton.Localization.LocalizationEvents.PendingCount +
                    NarrativeEvents.PendingCount +
                    SaveEvents.PendingCount +
                    QuestEvents.PendingCount +
                    Hecton8.Bootstrap.GameBootstrapper.PendingEventCount +
                    ObjectPoolDiagnostics.PendingCount +
                    PerformanceEvents.PendingCount +
                    ScalabilityEvents.PendingCount +
                    GlobalRegistry.PendingServiceReboundCount);

                BootstrapEvents.FlushPending();
                Hecton8.Bootstrap.GameBootstrapper.FlushPendingEvents();
                PerformanceEvents.FlushPending();
                ObjectPoolDiagnostics.FlushPending();
                ScalabilityEvents.FlushPending();
                GlobalRegistry.FlushPendingServiceReboundEvents();
                Hecton8.Modding.ModRegistryEvents.FlushPending();
                Hecton.Localization.LocalizationEvents.FlushPending();
                NarrativeEvents.FlushPending();
                SaveEvents.FlushPending();
                QuestEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_CoreEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushEnvironmentEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_EnvironmentEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _EnvironmentEventsArteryHash,
                    AtmosphereEvents.PendingCount +
                    HighPressureEvents.PendingCount +
                    FatalPressureImplosionEvents.PendingCount +
                    CelestialEvents.PendingCount +
                    EclipseGameplayEvents.PendingCount +
                    AcousticZoneEvents.PendingCount +
                    PhysicsEventBus.PendingCount +
                    FluidFeedbackEvents.PendingCount +
                    ElectrolysisAcousticEvents.PendingCount +
                    AudioCaptionEvents.PendingCount +
                    SpectrumEvents.PendingCount +
                    ProceduralAudioEvents.PendingCount +
                    MapMagicBiomeEvents.PendingCount +
                    BiomeMatrixEvents.PendingCount +
                    WeatherEvents.PendingCount +
                    RandomEventEvents.PendingCount +
                    DepthZoneEvents.PendingCount +
                    SoundscapeEvents.PendingCount +
                    SargassumGlobalDragManager.PendingEventCount +
                    Hecton8.AtlasSignal.AtlasSignalEvents.PendingCount +
                    Hecton8.AtlasSignal.Atlas6Events.PendingCount);

                AtmosphereEvents.FlushPending();
                HighPressureEvents.FlushPending();
                FatalPressureImplosionEvents.FlushPending();
                PhysicsEventBus.FlushPending();
                FluidFeedbackEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_EnvironmentEventsArteryHash))
                {
                    DropAmbientEnvironmentEvents();
                    return;
                }

                CelestialEvents.FlushPending();
                EclipseGameplayEvents.FlushPending();
                AcousticZoneEvents.FlushPending();
                ElectrolysisAcousticEvents.FlushPending();
                AudioCaptionEvents.FlushPending();
                SpectrumEvents.FlushPending();
                ProceduralAudioEvents.FlushPending();
                MapMagicBiomeEvents.FlushPending();
                BiomeMatrixEvents.FlushPending();
                WeatherEvents.FlushPending();
                RandomEventEvents.FlushPending();
                DepthZoneEvents.FlushPending();
                SoundscapeEvents.FlushPending();
                SargassumGlobalDragManager.FlushPendingEvents();
                Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending();
                Hecton8.AtlasSignal.Atlas6Events.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_EnvironmentEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushPlayerEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_PlayerEventsArteryHash);
                int pdaPendingBeforeFlush = Hecton8.UI.PDAEvents.PendingCount;
                ReportLateFrameQueueDepth(
                    _PlayerEventsArteryHash,
                    Hecton8.Interaction.InteractionEvents.PendingCount +
                    Hecton8.Crafting.CraftingEvents.PendingCount +
                    ScanEvents.PendingCount +
                    FirstHourEvents.PendingCount +
                    EndingEvents.PendingCount +
                    AudioLogEvents.PendingCount +
                    HectonSubmarineOsEvents.PendingCount +
                    FlashlightEvents.PendingCount +
                    LaserCutterEvents.PendingCount +
                    SuitMeshUpdateEvents.PendingCount +
                    PlayerSignalEvents.PendingCount +
                    InventoryEvents.PendingCount +
                    PlayerExpressionEvents.PendingCount +
                    Hecton8.UI.BaseIntegrityEvents.PendingCount +
                    Hecton8.UI.NotificationEvents.PendingCount +
                    Hecton8.UI.PDAIntrusionEvents.PendingCount +
                    pdaPendingBeforeFlush);

                SuitMeshUpdateEvents.FlushPending();
                PlayerSignalEvents.FlushPending();
                Hecton8.UI.BaseIntegrityEvents.FlushPending();
                FlashlightEvents.FlushPending();
                LaserCutterEvents.FlushPending();
                InventoryEvents.FlushPending();
                Hecton8.Interaction.InteractionEvents.FlushPending();
                Hecton8.Crafting.CraftingEvents.FlushPending();
                ScanEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_PlayerEventsArteryHash))
                    return;

                FirstHourEvents.FlushPending();
                EndingEvents.FlushPending();
                AudioLogEvents.FlushPending();
                HectonSubmarineOsEvents.FlushPending();
                PlayerExpressionEvents.FlushPending();
                Hecton8.UI.NotificationEvents.FlushPending();
                Hecton8.UI.PDAIntrusionEvents.FlushPending();
                Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame);
                TrackPdaBusCongestion(pdaPendingBeforeFlush, Hecton8.UI.PDAEvents.PendingCount);
            }
            finally
            {
                EndLateFrameFlushPass(_PlayerEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushBaseEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_BaseEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _BaseEventsArteryHash,
                    PowerGridTelemetryEvents.PendingCount +
                    ModuleStatusEvents.PendingCount +
                    BaseAirlockEvents.PendingCount +
                    EmergencyServiceRelayEvents.PendingCount);

                BaseAirlockEvents.FlushPending();
                EmergencyServiceRelayEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_BaseEventsArteryHash))
                    return;

                PowerGridTelemetryEvents.FlushPending();
                ModuleStatusEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_BaseEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushAIEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_AIEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _AIEventsArteryHash,
                    DirectorAIEvents.PendingCount +
                    HectonDroneFleetEvents.PendingCount +
                    RepairDroneTorchAcousticEvents.PendingCount);

                DirectorAIEvents.FlushPending();
                HectonDroneFleetEvents.FlushPending();
                RepairDroneTorchAcousticEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_AIEventsArteryHash, passStartTimestamp);
            }
        }

        /// <summary>
        /// Consumes one deferred event dispatch slot from the current LateUpdate budget.
        /// </summary>
        /// <returns>True when an event queue may dispatch one payload this frame.</returns>
        public static bool TryConsumeLateFrameEventDispatch()
        {
            if (!_lateFrameEventBudgetActive)
                return true;

            if (LateFrameFlushBudgetExhausted)
            {
                _lateFrameCircuitBreakerTripped = true;
                RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
                if (!_lateFrameTimeBudgetExhausted)
                {
                    _lateFrameTimeBudgetExhausted = true;
                    CrashTelemetryBuffer.ReportLateFrameLoadShedding(
                        _activeLateFrameEventLaneHash,
                        _lateFrameEventDispatchBudget);
                }

                return false;
            }

            if (_lateFrameEventDispatchBudget > 0)
            {
                _lateFrameEventDispatchBudget--;
                return true;
            }

            _lateFrameCircuitBreakerTripped = true;
            RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
            return false;
        }

        /// <summary>
        /// True while the late-frame ambient event lane has crossed its time budget for this frame.
        /// </summary>
        public static bool IsLateFrameAmbientEventSheddingActive =>
            _lateFrameEventBudgetActive && LateFrameFlushBudgetExhausted;

        public static void DispatchCriticalMemoryPressure(in CriticalMemoryPressureEvent memoryPressureEvent)
        {
            RequestVisualStaticGlitch();
            MemoryPressureSignal pressureSignal = default;
            pressureSignal.ReservedMemoryBytes = memoryPressureEvent.ReservedMemoryBytes;
            pressureSignal.PhysicalMemoryBytes = memoryPressureEvent.PhysicalMemoryBytes;
            pressureSignal.UsageRatio = (float)memoryPressureEvent.UsageRatio;
            pressureSignal.Frame = unchecked((uint)memoryPressureEvent.Frame);
            pressureSignal.Severity = 2;
            pressureSignal.Flags = 1;
            GlobalSignals.Publish(in pressureSignal);
            IMacroDatabaseService macroDatabase = ResolveCachedMacroDatabase();
            macroDatabase?.NotifyCriticalMemoryPressure(
                memoryPressureEvent.ReservedMemoryBytes,
                memoryPressureEvent.PhysicalMemoryBytes,
                pressureSignal.UsageRatio,
                pressureSignal.Frame,
                pressureSignal.Severity);
            Interlocked.Exchange(ref _criticalMemoryPressureDefragRequested, 1);
            CrashTelemetryBuffer.ReportCriticalMemoryPressure(
                memoryPressureEvent.ReservedMemoryBytes,
                memoryPressureEvent.PhysicalMemoryBytes,
                memoryPressureEvent.UsageRatio);

            ObjectPoolManager objectPool = ResolveCachedObjectPool();
            if (objectPool != null)
                objectPool.FlushInactivePoolsForMemoryPressure();

            if (CurrentFrameDeltaTime > 0f && CurrentFrameDeltaTime < SafeGcCollectFrameBudgetSeconds)
                System.GC.Collect(0, System.GCCollectionMode.Optimized, false);
        }

        internal static void MarkLateFrameEventDispatchDeferred()
        {
            if (_lateFrameEventBudgetActive)
            {
                _lateFrameCircuitBreakerTripped = true;
                RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
            }
        }

        private static void SetActiveLateFrameEventLane(uint queueHash)
        {
            _activeLateFrameEventLaneHash = queueHash;
        }

        private static void ClearActiveLateFrameEventLane()
        {
            _activeLateFrameEventLaneHash = 0u;
        }

        private static void ReportLateFrameQueueDepth(uint queueHash, int pendingCount)
        {
            ObjectPoolDiagnostics.PublishDataBusDepth(queueHash, pendingCount);
        }

        private static long BeginLateFrameFlushPass()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        private static void EndLateFrameFlushPass(uint laneHash, long passStartTimestamp)
        {
            if (passStartTimestamp == 0L)
                return;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - passStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            RecordArteryFlushSample(elapsedMilliseconds);
            if (elapsedMilliseconds <= LateFrameFlushPassSpikeMilliseconds || _criticalPerformanceSpikeReported)
                return;

            _criticalPerformanceSpikeReported = true;
            uint stackHash = CaptureCriticalPerformanceStackHash(laneHash);
            CrashTelemetryBuffer.ReportCriticalPerformanceSpike(laneHash, elapsedMilliseconds, stackHash);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _CriticalPerformanceSpikeHash,
                laneHash,
                (float)elapsedMilliseconds);
        }

        internal static int CopyArteryFlushMilliseconds(float[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int available = math.min(_arteryFlushSampleCursor, ArteryFlushSampleCapacity);
            int copyCount = math.min(available, destination.Length);
            int start = (_arteryFlushSampleCursor - copyCount) % ArteryFlushSampleCapacity;
            if (start < 0)
                start += ArteryFlushSampleCapacity;

            for (int i = 0; i < copyCount; i++)
                destination[i] = _arteryFlushMilliseconds[(start + i) % ArteryFlushSampleCapacity];

            return copyCount;
        }

        private static void RecordArteryFlushSample(double elapsedMilliseconds)
        {
            int writeIndex = _arteryFlushSampleCursor % ArteryFlushSampleCapacity;
            _arteryFlushMilliseconds[writeIndex] = elapsedMilliseconds > float.MaxValue
                ? float.MaxValue
                : (float)math.max(0d, elapsedMilliseconds);
            _arteryFlushSampleCursor++;
        }

        private static bool ShouldDropAmbientLateFrameEvents(uint laneHash)
        {
            if (!_lateFrameEventBudgetActive || !LateFrameFlushBudgetExhausted)
                return false;

            _lateFrameCircuitBreakerTripped = true;
            RecordLateFrameCircuitBreakerLane(laneHash);
            if (!_lateFrameTimeBudgetExhausted)
            {
                _lateFrameTimeBudgetExhausted = true;
                CrashTelemetryBuffer.ReportLateFrameLoadShedding(laneHash, _lateFrameEventDispatchBudget);
            }

            if (laneHash == _EnvironmentEventsArteryHash && _droppedEventsCounter < int.MaxValue)
                _droppedEventsCounter++;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _AmbientEventsDropHash,
                laneHash,
                laneHash == _EnvironmentEventsArteryHash ? 1f : _lateFrameEventDispatchBudget);
            return true;
        }

        private static void DropAmbientEnvironmentEvents()
        {
            WeatherEvents.DropPendingAmbient();
            RandomEventEvents.DropPendingAmbient();
            SoundscapeEvents.DropPendingAmbient();
        }

        private static void UpdatePauseFreezeFrameDitherState()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            bool paused = dispatcher != null && dispatcher.SimulationPaused;
            if (_pauseFreezeFrameDitherActive == paused)
                return;

            _pauseFreezeFrameDitherActive = paused;
            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, paused ? 1f : 0f);
            Shader.SetGlobalFloat(_GamePausedId, paused ? 1f : 0f);
        }

        public static void RequestVisualStaticGlitch()
        {
            RequestVisualStaticGlitch(VisualStaticGlitchDurationSeconds);
        }

        public static void RequestVisualStaticGlitch(float durationSeconds)
        {
            float safeDuration = math.max(0.05f, durationSeconds);
            float untilTime = Time.unscaledTime + safeDuration;
            if (untilTime > _visualStaticGlitchUntilTime)
                _visualStaticGlitchUntilTime = untilTime;

            if (!_visualStaticGlitchActive)
            {
                _visualStaticGlitchActive = true;
                Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 1f);
            }

            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, Time.frameCount & 1023);
        }

        private static void UpdateVisualStaticGlitchState()
        {
            if (!_visualStaticGlitchActive)
                return;

            if (Time.unscaledTime < _visualStaticGlitchUntilTime)
            {
                Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, Time.frameCount & 1023);
                return;
            }

            _visualStaticGlitchActive = false;
            _visualStaticGlitchUntilTime = 0f;
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, 0f);
        }

        private static uint CaptureCriticalPerformanceStackHash(uint laneHash)
        {
            uint frameHash = unchecked((uint)Time.frameCount * 747796405u);
            uint budgetHash = unchecked((uint)_lateFrameEventDispatchBudget * 2891336453u);
            return laneHash ^ _CriticalPerformanceSpikeHash ^ frameHash ^ budgetHash;
        }

        private static void TrackPdaBusCongestion(int pendingBeforeFlush, int pendingAfterFlush)
        {
            if (pendingBeforeFlush <= MaxPdaEventsPerFrame && pendingAfterFlush <= 0)
            {
                _pdaOverBudgetConsecutiveFrames = 0;
                return;
            }

            _pdaOverBudgetConsecutiveFrames++;
            if (_pdaOverBudgetConsecutiveFrames != PdaCongestionWarningFrameThreshold)
                return;

            CrashTelemetryBuffer.ReportBusCongestionWarning(
                _PdaEventsQueueHash,
                math.max(pendingBeforeFlush, pendingAfterFlush),
                WorldSpatialHashGrid.ActiveEntityCount);
        }

        private static void BeginLateFrameEventBudget()
        {
            _lateFrameEventDispatchBudget = MaxLateFrameEventsPerFrame;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _lateFrameEventBudgetActive = true;
            _activeLateFrameEventLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneCount = 0;
            _lateFrameEventBudgetStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            System.Array.Clear(_lateFrameCircuitBreakerLaneHashes, 0, _lateFrameCircuitBreakerLaneHashes.Length);
            System.Array.Clear(_lateFrameCircuitBreakerLaneCounts, 0, _lateFrameCircuitBreakerLaneCounts.Length);
        }

        private static void EndLateFrameEventBudget()
        {
            bool circuitBreakerTripped = _lateFrameCircuitBreakerTripped;
            _lateFrameEventBudgetActive = false;
            _lateFrameEventDispatchBudget = 0;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _lateFrameEventBudgetStartTimestamp = 0L;

            if (circuitBreakerTripped)
            {
                CrashTelemetryBuffer.ReportEventCascadeWarning();
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _LateFrameBudgetWarningHash,
                    _dominantLateFrameCircuitBreakerLaneHash,
                    _dominantLateFrameCircuitBreakerLaneCount);
            }
        }

        private static bool LateFrameFlushBudgetExhausted
        {
            get
            {
                if (_lateFrameEventBudgetStartTimestamp == 0L)
                    return false;

                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _lateFrameEventBudgetStartTimestamp;
                double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return elapsedMilliseconds >= LateFrameEventFlushBudgetMilliseconds;
            }
        }

        private static void RecordLateFrameCircuitBreakerLane(uint queueHash)
        {
            if (queueHash == 0u)
                return;

            for (int i = 0; i < LateFrameCircuitBreakerLaneCapacity; i++)
            {
                uint recordedHash = _lateFrameCircuitBreakerLaneHashes[i];
                if (recordedHash == queueHash)
                {
                    ushort nextCount = _lateFrameCircuitBreakerLaneCounts[i] == ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)(_lateFrameCircuitBreakerLaneCounts[i] + 1);
                    _lateFrameCircuitBreakerLaneCounts[i] = nextCount;
                    if (nextCount >= _dominantLateFrameCircuitBreakerLaneCount)
                    {
                        _dominantLateFrameCircuitBreakerLaneHash = queueHash;
                        _dominantLateFrameCircuitBreakerLaneCount = nextCount;
                    }

                    return;
                }

                if (recordedHash != 0u)
                    continue;

                _lateFrameCircuitBreakerLaneHashes[i] = queueHash;
                _lateFrameCircuitBreakerLaneCounts[i] = 1;
                if (_dominantLateFrameCircuitBreakerLaneCount == 0)
                {
                    _dominantLateFrameCircuitBreakerLaneHash = queueHash;
                    _dominantLateFrameCircuitBreakerLaneCount = 1;
                }

                return;
            }
        }

        private void RunFixedStepAccumulator(float dilatedDeltaTime, bool blockGameplayLanes)
        {
            if (SignalBusRegistry.IsSimulationHalted)
                return;

            if (dilatedDeltaTime <= 0f)
            {
                _temporalCompressionActive = false;
                RefreshFixedInterpolationAlpha();
                return;
            }

            double maxAccumulatedTime = FixedStepSeconds * MaxFixedSubstepsPerFrame;
            double requestedAccumulatedTime = _fixedStepAccumulator + dilatedDeltaTime;
            bool compressionActive = requestedAccumulatedTime > maxAccumulatedTime;
            _temporalCompressionActive = compressionActive;
            if (compressionActive)
            {
                _temporalCompressionFrameCount++;
                CrashTelemetryBuffer.ReportTemporalCompression();
            }

            _fixedStepAccumulator = System.Math.Min(requestedAccumulatedTime, maxAccumulatedTime);

            int substepCount = 0;
            while (_fixedStepAccumulator >= FixedStepSeconds && substepCount < MaxFixedSubstepsPerFrame)
            {
                if (SignalBusRegistry.IsSimulationHalted)
                    break;

                DispatchFixedStep((float)FixedStepSeconds, blockGameplayLanes);
                _fixedStepAccumulator -= FixedStepSeconds;
                substepCount++;
            }

            if (substepCount >= MaxFixedSubstepsPerFrame && _fixedStepAccumulator >= FixedStepSeconds)
                _fixedStepAccumulator = 0d;

            RefreshFixedInterpolationAlpha();
        }

        private void RefreshFixedInterpolationAlpha()
        {
            CurrentFixedInterpolationAlpha = math.saturate((float)(_fixedStepAccumulator / FixedStepSeconds));
        }

        private void DispatchFixedStep(float fixedDeltaTime, bool blockGameplayLanes)
        {
#if UNITY_EDITOR && HECTON_HEAP_LOCK_GUARD
            long heapBefore = Profiler.GetMonoUsedSizeLong();
#endif
            using (_fixedUpdateProfilerMarker.Auto())
            {
                RunMasterFixedSimulationBridge(fixedDeltaTime);
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (SignalBusRegistry.IsSimulationHalted)
                        return;

                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IFixedTickable> lane = _fixedPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFixedTickable));
#endif
                    using (_fixedLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IFixedTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            rawArray[itemIndex].FixedTick(fixedDeltaTime);
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }

                using (_postFixedProfilerMarker.Auto())
                {
                    DispatcherJobSwap.BeginPostFixedSwapWindow();
                    try
                    {
                        CompleteMasterFixedSimulationBridge(fixedDeltaTime);
                        for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                        {
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;

                            if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                                continue;

                            RegistryBucket<IPostFixedTickable> lane = _postFixedPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            lane.ValidateNoDestroyedEntriesDebug(nameof(IPostFixedTickable));
#endif
                            IPostFixedTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;
                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                IPostFixedTickable postFixedTickable = rawArray[itemIndex];
                                int gen0Before = System.GC.CollectionCount(0);
                                _currentPostFixedGcOwner = postFixedTickable;
                                try
                                {
                                    postFixedTickable.PostFixedTick(fixedDeltaTime);
                                }
                                finally
                                {
                                    int gen0After = System.GC.CollectionCount(0);
                                    if (gen0After != gen0Before)
                                    {
                                        _lastPostFixedGcOwner = postFixedTickable;
                                        _lastPostFixedGcFrame = Time.frameCount;
                                        _lastPostFixedGcDelta = gen0After - gen0Before;
                                        _lastPostFixedGcLaneIndex = laneIndex;
                                        _lastPostFixedGcItemIndex = itemIndex;
                                    }

                                    _currentPostFixedGcOwner = null;
                                }
#else
                                rawArray[itemIndex].PostFixedTick(fixedDeltaTime);
#endif
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                    finally
                    {
                        DispatcherJobSwap.EndPostFixedSwapWindow();
                    }
                }

                DrainAupNanInquisitor();
            }
#if UNITY_EDITOR && HECTON_HEAP_LOCK_GUARD
            long heapAfter = Profiler.GetMonoUsedSizeLong();
            if (heapAfter > heapBefore)
            {
                PublishDispatcherComplianceViolation(_HeapLockGuardHash, _SystemDispatcherHash, 4, 1);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _HeapLockGuardHash,
                    _SystemDispatcherHash,
                    math.max(1f, heapAfter - heapBefore));
                throw new System.InvalidOperationException(HeapLockGuardMessage);
            }
#endif
        }

        private static void DrainAupNanInquisitor()
        {
            int invalidResultCount = AUPMath.ConsumeInvalidResultCount();
            if (invalidResultCount <= 0)
                return;

            CrashTelemetryBuffer.ReportNanPhysicsRecovery();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float now = Time.unscaledTime;
            if (now < _nextAupNanInquisitorLogTime)
                return;

            _nextAupNanInquisitorLogTime = now + AupNanInquisitorLogIntervalSeconds;
            PublishDispatcherComplianceViolation(_AupNanInquisitorHash, _SystemDispatcherHash, 4, 0);
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_AupNanInquisitorHash));
#endif
        }

        private void RunFastTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f || SignalBusRegistry.IsSimulationHalted)
                return;

            _fastTickAccumulator += deltaTime;
            int substeps = 0;
            while (_fastTickAccumulator >= FastTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _fastTickAccumulator -= FastTickIntervalSeconds;
                substeps++;

                using (_fastTickProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (SignalBusRegistry.IsSimulationHalted)
                            return;

                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<IFastTickable> lane = _fastPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IFastTickable));
#endif
                        using (_fastLaneProfilerMarkers[laneIndex].Auto())
                        {
                            IFastTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                rawArray[itemIndex].FastTick((float)FastTickIntervalSeconds);
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _fastTickAccumulator >= FastTickIntervalSeconds)
                _fastTickAccumulator = FastTickIntervalSeconds;
        }

        private void RunUnscaledFastTick(float unscaledDeltaTime, bool blockGameplayLanes)
        {
            if (unscaledDeltaTime <= 0f || SignalBusRegistry.IsSimulationHalted)
                return;

            _unscaledFastTickAccumulator += unscaledDeltaTime;
            int substeps = 0;
            while (_unscaledFastTickAccumulator >= FastTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _unscaledFastTickAccumulator -= FastTickIntervalSeconds;
                substeps++;

                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (SignalBusRegistry.IsSimulationHalted)
                        return;

                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IUnscaledFastTickable> lane = _unscaledFastPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUnscaledFastTickable));
#endif
                    using (_unscaledFastLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IUnscaledFastTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            rawArray[itemIndex].UnscaledFastTick((float)FastTickIntervalSeconds);
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _unscaledFastTickAccumulator >= FastTickIntervalSeconds)
                _unscaledFastTickAccumulator = FastTickIntervalSeconds;
        }

        private void RunSlowTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f || SignalBusRegistry.IsSimulationHalted)
                return;

            double slowTickIntervalSeconds = ResolveSlowTickIntervalSeconds();
            _slowTickAccumulator += deltaTime;
            int substeps = 0;
            while (_slowTickAccumulator >= slowTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _slowTickAccumulator -= slowTickIntervalSeconds;
                substeps++;

                using (_slowTickProfilerMarker.Auto())
                {
                    HectonXRRuntimeState.SlowTickHeadAupCache();

                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (SignalBusRegistry.IsSimulationHalted)
                            return;

                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<ISlowTickable> lane = _slowPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                        using (_slowLaneProfilerMarkers[laneIndex].Auto())
                        {
                            ISlowTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                ISlowTickable tickable = rawArray[itemIndex];
                                if (tickable is IBucketedSlowTickable)
                                    continue;

                                tickable.SlowTick();
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }

                    float slowTickDeltaSeconds = (float)slowTickIntervalSeconds;
                    WorldSpatialHashGrid.SlowTickMaintenance(slowTickDeltaSeconds);
                    CombatDamageRuntime.SlowTick(slowTickDeltaSeconds);
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _slowTickAccumulator >= slowTickIntervalSeconds)
                _slowTickAccumulator = slowTickIntervalSeconds;
        }

        private void RunBucketedSlowTick(bool blockGameplayLanes)
        {
            if (SignalBusRegistry.IsSimulationHalted)
                return;

            ISimulationBucketer bucketer = _simulationBucketer;
            if (_bucketedSlowTickableCount <= 0 || bucketer == null || !bucketer.IsInitialized)
                return;

            using (_slowTickProfilerMarker.Auto())
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (SignalBusRegistry.IsSimulationHalted)
                        return;

                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<ISlowTickable> lane = _slowPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                    using (_slowLaneProfilerMarkers[laneIndex].Auto())
                    {
                        ISlowTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            if (rawArray[itemIndex] is IBucketedSlowTickable bucketedTickable &&
                                bucketer.IsSlowBucketActive(bucketedTickable.SimulationBucketId))
                            {
                                bucketedTickable.SlowTick();
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                }
            }
        }

        private double ResolveSlowTickIntervalSeconds()
        {
            if (Volatile.Read(ref _homeostasisSlowTick2Hz) != 0)
                return HomeostasisEmergencySlowTickIntervalSeconds;

            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer != null &&
                bucketer.IsInitialized &&
                bucketer.SlowBucketCount == SimulationBucketConstants.StandardSlowBucketCount &&
                bucketer.ActiveSlowBucketCount <= SimulationBucketConstants.MinimumActiveSlowBucketCount)
            {
                return math.max(
                    _thermalCriticalSlowTickActive ? ThermalCriticalSlowTickIntervalSeconds : SlowTickIntervalSeconds,
                    SlowTickIntervalSeconds * 2.0);
            }

            return _thermalCriticalSlowTickActive ? ThermalCriticalSlowTickIntervalSeconds : SlowTickIntervalSeconds;
        }

        private void RunColdTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f || SignalBusRegistry.IsSimulationHalted)
                return;

            _coldTickAccumulator += deltaTime;
            int substeps = 0;
            while (_coldTickAccumulator >= ColdTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _coldTickAccumulator -= ColdTickIntervalSeconds;
                substeps++;

                using (_coldTickProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (SignalBusRegistry.IsSimulationHalted)
                            return;

                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<IColdTickable> lane = _coldPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IColdTickable));
#endif
                        using (_coldLaneProfilerMarkers[laneIndex].Auto())
                        {
                            IColdTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                rawArray[itemIndex].ColdTick();
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _coldTickAccumulator >= ColdTickIntervalSeconds)
                _coldTickAccumulator = ColdTickIntervalSeconds;
        }

        private void RunFrostTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f || SignalBusRegistry.IsSimulationHalted)
                return;

            _frostTickAccumulator += deltaTime;
            if (_frostTickAccumulator < FrostTickIntervalSeconds)
                return;

            _frostTickAccumulator -= FrostTickIntervalSeconds;

            using (_frostTickProfilerMarker.Auto())
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (SignalBusRegistry.IsSimulationHalted)
                        return;

                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IFrostTickable> lane = _frostPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFrostTickable));
#endif
                    using (_frostLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IFrostTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            rawArray[itemIndex].FrostTick();
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }
            }
        }

        private static void EnsureDispatcherRaycastBuffers()
        {
            TryResolveDispatcherRaycastCommands(
                ref _pendingDispatcherRaycastCommandsHandle,
                BufferID.SystemDispatcherRaycastPendingCommands,
                out NativeArray<RaycastCommand> _);
            TryResolveDispatcherRaycastCommands(
                ref _scheduledDispatcherRaycastCommandsHandle,
                BufferID.SystemDispatcherRaycastScheduledCommands,
                out NativeArray<RaycastCommand> _);

            if (!_scheduledDispatcherRaycastHitsHandle.IsCreated)
            {
                if (!TryResolveCachedDataVault(out IDataVault dataVault))
                    return;

                _scheduledDispatcherRaycastHitsHandle = dataVault.GetBufferHandle<RaycastHit>(
                    BufferID.DispatcherRaycastHits,
                    MaxQueuedDispatcherRaycasts,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
                NativeArray<RaycastHit> scheduledHits = _scheduledDispatcherRaycastHitsHandle.Resolve(dataVault);
                if (!scheduledHits.IsCreated || scheduledHits.Length < MaxQueuedDispatcherRaycasts)
                    _scheduledDispatcherRaycastHitsHandle = default;
            }
        }

        private static bool TryResolveDispatcherRaycastCommands(
            ref VaultBufferHandle<RaycastCommand> handle,
            BufferID bufferId,
            out NativeArray<RaycastCommand> commands)
        {
            commands = default;
            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return false;

            if (!handle.IsCreated || handle.Length < MaxQueuedDispatcherRaycasts)
            {
                handle = dataVault.GetBufferHandle<RaycastCommand>(
                    bufferId,
                    MaxQueuedDispatcherRaycasts,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
                if (!handle.IsCreated)
                    return false;
            }

            commands = handle.Resolve(dataVault);
            return commands.IsCreated && commands.Length >= MaxQueuedDispatcherRaycasts;
        }

        private static bool TryResolveDispatcherRaycastHits(out NativeArray<RaycastHit> scheduledHits)
        {
            scheduledHits = default;
            EnsureDispatcherRaycastBuffers();

            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return false;

            NativeArray<RaycastHit> resolved = _scheduledDispatcherRaycastHitsHandle.Resolve(dataVault);
            if (!resolved.IsCreated || resolved.Length < MaxQueuedDispatcherRaycasts)
                return false;

            scheduledHits = resolved;
            return true;
        }

        private static bool TryLockDispatcherRaycastScheduledVaultBuffers()
        {
            if (_scheduledDispatcherRaycastCommandsVaultLocked && _scheduledDispatcherRaycastHitsVaultLocked)
                return true;

            EnsureDispatcherRaycastBuffers();
            if (!_scheduledDispatcherRaycastCommandsHandle.IsCreated || !_scheduledDispatcherRaycastHitsHandle.IsCreated)
                return false;

            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return false;

            bool lockedCommandsHere = false;
            if (!_scheduledDispatcherRaycastCommandsVaultLocked)
            {
                if (!dataVault.TryLockBuffer(BufferID.SystemDispatcherRaycastScheduledCommands, SystemID.SystemDispatcher))
                    return false;

                _scheduledDispatcherRaycastCommandsVaultLocked = true;
                lockedCommandsHere = true;
            }

            if (!_scheduledDispatcherRaycastHitsVaultLocked &&
                !dataVault.TryLockBuffer(BufferID.DispatcherRaycastHits, SystemID.SystemDispatcher))
            {
                if (lockedCommandsHere)
                {
                    dataVault.TryUnlockBuffer(BufferID.SystemDispatcherRaycastScheduledCommands, SystemID.SystemDispatcher);
                    _scheduledDispatcherRaycastCommandsVaultLocked = false;
                }

                return false;
            }

            _scheduledDispatcherRaycastHitsVaultLocked = true;
            return true;
        }

        private static void UnlockDispatcherRaycastScheduledVaultBuffers()
        {
            if (!_scheduledDispatcherRaycastCommandsVaultLocked && !_scheduledDispatcherRaycastHitsVaultLocked)
                return;

            if (TryResolveCachedDataVault(out IDataVault dataVault))
            {
                if (_scheduledDispatcherRaycastCommandsVaultLocked)
                    dataVault.TryUnlockBuffer(BufferID.SystemDispatcherRaycastScheduledCommands, SystemID.SystemDispatcher);

                if (_scheduledDispatcherRaycastHitsVaultLocked)
                    dataVault.TryUnlockBuffer(BufferID.DispatcherRaycastHits, SystemID.SystemDispatcher);
            }

            _scheduledDispatcherRaycastCommandsVaultLocked = false;
            _scheduledDispatcherRaycastHitsVaultLocked = false;
        }

        private static void ScheduleDispatcherRaycasts()
        {
            if (_dispatcherRaycastsScheduled || _pendingDispatcherRaycastCount <= 0)
                return;

            EnsureDispatcherRaycastBuffers();
            if (!TryResolveDispatcherRaycastCommands(
                    ref _pendingDispatcherRaycastCommandsHandle,
                    BufferID.SystemDispatcherRaycastPendingCommands,
                    out NativeArray<RaycastCommand> pendingCommands) ||
                !TryResolveDispatcherRaycastCommands(
                    ref _scheduledDispatcherRaycastCommandsHandle,
                    BufferID.SystemDispatcherRaycastScheduledCommands,
                    out NativeArray<RaycastCommand> scheduledCommands) ||
                !TryResolveDispatcherRaycastHits(out NativeArray<RaycastHit> scheduledHits))
            {
                return;
            }

            if (!TryLockDispatcherRaycastScheduledVaultBuffers())
                return;

            using (_dispatcherRaycastScheduleProfilerMarker.Auto())
            {
                int pendingCount = _pendingDispatcherRaycastCount;
                int scheduledCount = math.min(pendingCount, MaxQueuedDispatcherRaycasts);
                for (int i = 0; i < scheduledCount; i++)
                {
                    scheduledCommands[i] = pendingCommands[i];
                    _scheduledDispatcherRaycastReceivers[i] = _pendingDispatcherRaycastReceivers[i];
                    _scheduledDispatcherRaycastRequestIds[i] = _pendingDispatcherRaycastRequestIds[i];
                    _pendingDispatcherRaycastReceivers[i] = null;
                    _pendingDispatcherRaycastRequestIds[i] = 0;
                }

                for (int clearIndex = scheduledCount; clearIndex < pendingCount; clearIndex++)
                {
                    _pendingDispatcherRaycastReceivers[clearIndex] = null;
                    _pendingDispatcherRaycastRequestIds[clearIndex] = 0;
                }

                ClearRaycastCommandRange(pendingCommands, pendingCount);
                _pendingDispatcherRaycastCount = 0;
                if (scheduledCount <= 0)
                {
                    UnlockDispatcherRaycastScheduledVaultBuffers();
                    return;
                }

                _scheduledDispatcherRaycastCount = scheduledCount;
                _scheduledDispatcherRaycastHandle = RaycastCommand.ScheduleBatch(
                    scheduledCommands.GetSubArray(0, scheduledCount),
                    scheduledHits.GetSubArray(0, scheduledCount),
                    DispatcherRaycastMinCommandsPerJob,
                    default);
                _dispatcherRaycastsScheduled = true;
            }
        }

        private static void CompleteDispatcherRaycasts()
        {
            if (!_dispatcherRaycastsScheduled)
                return;

            using (_dispatcherRaycastCompleteProfilerMarker.Auto())
            {
                if (!DispatcherJobSwap.TryComplete(ref _scheduledDispatcherRaycastHandle, forceComplete: false))
                    return;

                _dispatcherRaycastsScheduled = false;
                if (!TryResolveDispatcherRaycastHits(out NativeArray<RaycastHit> scheduledHits))
                {
                    for (int i = 0; i < _scheduledDispatcherRaycastCount; i++)
                    {
                        _scheduledDispatcherRaycastReceivers[i] = null;
                        _scheduledDispatcherRaycastRequestIds[i] = 0;
                    }

                    _scheduledDispatcherRaycastCount = 0;
                    UnlockDispatcherRaycastScheduledVaultBuffers();
                    return;
                }

                for (int i = 0; i < _scheduledDispatcherRaycastCount; i++)
                {
                    IDispatcherRaycastReceiver receiver = _scheduledDispatcherRaycastReceivers[i];
                    if (receiver == null)
                        continue;

                    receiver.ConsumeDispatcherRaycastHit(_scheduledDispatcherRaycastRequestIds[i], scheduledHits[i]);
                    _scheduledDispatcherRaycastReceivers[i] = null;
                    _scheduledDispatcherRaycastRequestIds[i] = 0;
                }

                _scheduledDispatcherRaycastCount = 0;
                UnlockDispatcherRaycastScheduledVaultBuffers();
            }
        }

        private static void ClearRaycastCommandRange(NativeArray<RaycastCommand> commands, int count)
        {
            if (!commands.IsCreated || count <= 0)
                return;

            int clampedCount = math.min(count, commands.Length);
            for (int i = 0; i < clampedCount; i++)
                commands[i] = default;
        }

        private static void DisposeDispatcherRaycastBuffers()
        {
            if (_dispatcherRaycastsScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _scheduledDispatcherRaycastHandle, forceComplete: true);
                _dispatcherRaycastsScheduled = false;
                UnlockDispatcherRaycastScheduledVaultBuffers();
            }
            else
            {
                _scheduledDispatcherRaycastHandle = default;
                UnlockDispatcherRaycastScheduledVaultBuffers();
            }

            _pendingDispatcherRaycastCommandsHandle = default;
            _scheduledDispatcherRaycastCommandsHandle = default;
            _scheduledDispatcherRaycastHitsHandle = default;

            _pendingDispatcherRaycastCount = 0;
            _scheduledDispatcherRaycastCount = 0;
            System.Array.Clear(_pendingDispatcherRaycastReceivers, 0, _pendingDispatcherRaycastReceivers.Length);
            System.Array.Clear(_pendingDispatcherRaycastRequestIds, 0, _pendingDispatcherRaycastRequestIds.Length);
            System.Array.Clear(_scheduledDispatcherRaycastReceivers, 0, _scheduledDispatcherRaycastReceivers.Length);
            System.Array.Clear(_scheduledDispatcherRaycastRequestIds, 0, _scheduledDispatcherRaycastRequestIds.Length);
        }

        private static bool ShouldSkipLaneDuringBootstrap(int laneIndex, bool blockGameplayLanes)
        {
            if (!blockGameplayLanes)
                return false;

            // Bootstrap gates the player lane only. World/environment systems must keep
            // ticking so startup queues, residency, and spawn drains can complete.
            return laneIndex == GetLaneIndex(PriorityLayer.Player);
        }

        private static int GetLaneIndex(PriorityLayer layer)
        {
            switch (layer)
            {
                case PriorityLayer.Core:
                    return 0;
                case PriorityLayer.Environment:
                    return 1;
                case PriorityLayer.Player:
                    return 2;
                case PriorityLayer.UI:
                    return 3;
                default:
                    return 0;
            }
        }

        private static long BeginDispatcherPhaseTiming()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return System.Diagnostics.Stopwatch.GetTimestamp();
#else
            return 0L;
#endif
        }

        private static void EndDispatcherPhaseTiming(long startTimestamp, string phaseName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowDispatcherPhaseWarningMilliseconds)
            {
                float now = Time.unscaledTime;
                if (now >= _nextDispatcherPhaseWarningLogTime)
                {
                    _nextDispatcherPhaseWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        nameof(SystemDispatcher),
                        phaseName,
                        (float)elapsedMilliseconds);
                }
            }
#endif
        }

        private void CompleteFoveatedFrameJobs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_foveatedCompleteProfilerMarker.Auto())
            {
                _foveatedSimulationManager.TryCompleteFrameJobs();
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                float now = Time.unscaledTime;
                if (now >= _nextFoveatedFrameWarningLogTime)
                {
                    _nextFoveatedFrameWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        "FoveatedSimulationManager",
                        "LateFrameComplete",
                        (float)elapsedMilliseconds);
                }
            }
#else
            using (_foveatedCompleteProfilerMarker.Auto())
            {
                _foveatedSimulationManager.TryCompleteFrameJobs();
            }
#endif
        }

    }

    /// <summary>
    /// Per-camera SRP callback context exposed to registry-managed render owners.
    /// </summary>
    public static class GlobalRenderContext
    {
        private static Camera _currentCamera;
        private static ScriptableRenderContext _currentContext;

        /// <summary>
        /// Camera currently being rendered by the SRP dispatcher.
        /// </summary>
        public static Camera CurrentCamera => _currentCamera;

        /// <summary>
        /// Scriptable render context currently being processed.
        /// </summary>
        public static ScriptableRenderContext CurrentContext => _currentContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _currentCamera = null;
            _currentContext = default;
        }

        internal static void SetCurrent(ScriptableRenderContext context, Camera camera)
        {
            _currentContext = context;
            _currentCamera = camera;
            PublishCameraSignals(camera);
        }

        private static void PublishCameraSignals(Camera camera)
        {
            if (camera == null)
                return;

            Transform cameraTransform = camera.transform;
            if (cameraTransform == null)
                return;

            uint frame = unchecked((uint)Mathf.Max(0, Time.frameCount));
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            Vector3 up = cameraTransform.up;

            Hecton8.Core.Contracts.Signals.CameraPositionSignal positionSignal = default;
            positionSignal.Position = (float3)position;
            positionSignal.Frame = frame;
            positionSignal.Forward = (float3)forward;
            positionSignal.Flags = 1;
            SignalBus<Hecton8.Core.Contracts.Signals.CameraPositionSignal>.Push(in positionSignal);

            Hecton8.Core.Contracts.Signals.CameraFrustumSignal frustumSignal = default;
            frustumSignal.Position = (float3)position;
            frustumSignal.Forward = (float3)forward;
            frustumSignal.Up = (float3)up;
            frustumSignal.FieldOfViewDegrees = camera.fieldOfView;
            frustumSignal.NearClipMeters = camera.nearClipPlane;
            frustumSignal.FarClipMeters = camera.farClipPlane;
            frustumSignal.Frame = frame;
            frustumSignal.Flags = 1;
            SignalBus<Hecton8.Core.Contracts.Signals.CameraFrustumSignal>.Push(in frustumSignal);
        }

        internal static void Clear()
        {
            _currentContext = default;
            _currentCamera = null;
        }
    }

    /// <summary>
    /// Bootstrap-owned SRP callback fan-out for registry-managed render owners.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class RenderDispatcher : MonoBehaviour, IServiceHeartbeat, IServiceShutdown
    {
        internal static RenderDispatcher ActiveRuntimeInstance { get; private set; }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private struct RenderSettingsSnapshot
        {
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity;
            public Material Skybox;
            public AmbientMode AmbientMode;
            public Color AmbientLight;
            public Color AmbientSkyColor;
            public Color AmbientEquatorColor;
            public Color AmbientGroundColor;
            public float AmbientIntensity;
            public float ReflectionIntensity;

            public static RenderSettingsSnapshot Capture()
            {
                return new RenderSettingsSnapshot
                {
                    Fog = RenderSettings.fog,
                    FogMode = RenderSettings.fogMode,
                    FogColor = RenderSettings.fogColor,
                    FogDensity = RenderSettings.fogDensity,
                    Skybox = AtmosphereDirector.Skybox,
                    AmbientMode = RenderSettings.ambientMode,
                    AmbientLight = RenderSettings.ambientLight,
                    AmbientSkyColor = RenderSettings.ambientSkyColor,
                    AmbientEquatorColor = RenderSettings.ambientEquatorColor,
                    AmbientGroundColor = RenderSettings.ambientGroundColor,
                    AmbientIntensity = RenderSettings.ambientIntensity,
                    ReflectionIntensity = RenderSettings.reflectionIntensity
                };
            }

            public void Restore(IGIRelaySystem giRelay)
            {
                RenderSettings.fog = Fog;
                RenderSettings.fogMode = FogMode;
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogDensity = FogDensity;
                AtmosphereDirector.SetSkybox(Skybox);
                bool giRelayAmbientAuthority = giRelay != null && giRelay.IsAmbientProbeAuthorityActive;
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode = AmbientMode;
                    RenderSettings.ambientLight = AmbientLight;
                    RenderSettings.ambientSkyColor = AmbientSkyColor;
                    RenderSettings.ambientEquatorColor = AmbientEquatorColor;
                    RenderSettings.ambientGroundColor = AmbientGroundColor;
                    RenderSettings.ambientIntensity = AmbientIntensity;
                }
                RenderSettings.reflectionIntensity = ReflectionIntensity;
            }
        }

        private bool _hasPendingRenderSettingsRestore;
        private Camera _pendingRenderSettingsCamera;
        private RenderSettingsSnapshot _pendingRenderSettingsSnapshot;
        private RegistryBucket<IRenderable> _renderables;
        private IGIRelaySystem _giRelay;
        private bool _serviceRegistered;

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            _hasPendingRenderSettingsRestore = false;
            _pendingRenderSettingsCamera = null;
        }

        /// <summary>
        /// Registers this dispatcher as the authoritative SRP render dispatcher.
        /// </summary>
        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            RenderDispatcher registeredDispatcher = GlobalRegistry.RenderDispatcher;
            if (registeredDispatcher != null && !ReferenceEquals(registeredDispatcher, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterRenderDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.RenderDispatcher, this);
            RefreshRenderDependencies();
        }

        private void OnEnable()
        {
            RefreshRenderDependencies();
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestorePendingRenderSettings();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            OnDisable();
            GlobalRenderContext.Clear();
            _pendingRenderSettingsCamera = null;
            _hasPendingRenderSettingsRestore = false;
            _renderables = null;
            _giRelay = null;

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.RenderDispatcher, this))
                GlobalRegistry.UnregisterRenderDispatcher(this);

            _serviceRegistered = false;
        }

        private void RefreshRenderDependencies()
        {
            _renderables = GlobalRegistry.Renderables;
            IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
            if (giRelay != null)
                _giRelay = giRelay;
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            HectonFloatingOrigin.PublishCurrentGlobalOffsetsForRenderLoop();
            RestorePendingRenderSettings();

            RegistryBucket<IRenderable> renderables = _renderables;
            if (renderables == null)
            {
                RefreshRenderDependencies();
                renderables = _renderables;
            }

            if (renderables == null)
                return;

            int count = renderables.Count;
            if (count <= 0)
                return;

            IRenderable[] rawArray = renderables.RawArray;
            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            _pendingRenderSettingsSnapshot = RenderSettingsSnapshot.Capture();
            _pendingRenderSettingsCamera = camera;
            _hasPendingRenderSettingsRestore = true;
            GlobalRenderContext.SetCurrent(context, camera);

            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    IRenderable renderable = rawArray[i];
                    if (renderable == null)
                        continue;

                    renderable.Render(deltaTime);
                }
            }
            finally
            {
                GlobalRenderContext.Clear();
            }
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_hasPendingRenderSettingsRestore)
            {
                InputLatencyTracker.MarkRenderCompleted();
                return;
            }

            if (_pendingRenderSettingsCamera != null && camera != _pendingRenderSettingsCamera)
                return;

            RestorePendingRenderSettings();
            InputLatencyTracker.MarkRenderCompleted();
        }

        private void RestorePendingRenderSettings()
        {
            if (!_hasPendingRenderSettingsRestore)
                return;

            if (_giRelay == null)
                RefreshRenderDependencies();

            _pendingRenderSettingsSnapshot.Restore(_giRelay);
            _pendingRenderSettingsSnapshot = default;
            _pendingRenderSettingsCamera = null;
            _hasPendingRenderSettingsRestore = false;
        }
    }

    /// <summary>
    /// LockBufferForWrite upload helpers used by owned runtime graphics buffers.
    /// </summary>
    internal static class GraphicsBufferUploadUtility
    {
        /// <summary>
        /// Creates a structured buffer configured for standard SetData uploads.
        /// </summary>
        public static GraphicsBuffer CreateStructuredBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Creates a structured buffer configured for direct CPU writes.
        /// </summary>
        public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Uploads a blittable native array into a graphics buffer using one memcpy.
        /// </summary>
        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            unsafe
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SystemDispatcher));
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        /// <summary>
        /// Uploads a blittable managed array into a graphics buffer using one memcpy.
        /// </summary>
        public static unsafe void UploadArray<T>(GraphicsBuffer destination, T[] source, int count) where T : unmanaged
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            fixed (T* sourcePtr = source)
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SystemDispatcher));
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        /// <summary>
        /// Uploads a blittable managed array into a graphics buffer using SetData.
        /// Use for infrequent uploads where avoiding lock-contention stalls matters more than raw memcpy throughput.
        /// </summary>
        public static void UploadArraySetData<T>(GraphicsBuffer destination, T[] source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            destination.SetData(source, 0, 0, safeCount);
        }

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : struct
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            int stride = UnsafeUtility.SizeOf<T>();
            if (destination.stride != stride)
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }
    }
}
