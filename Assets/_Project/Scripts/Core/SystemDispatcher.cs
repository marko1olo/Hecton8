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
using Hecton8.Core.Scheduling;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Narrative;
using Hecton8.Optimization;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.Visor;
using Hecton8.World;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct CriticalMemoryPressureEvent
    {
        [FieldOffset(0)] public readonly double UsageRatio;
        [FieldOffset(8)] public readonly long ReservedMemoryBytes;
        [FieldOffset(16)] public readonly long PhysicalMemoryBytes;
        [FieldOffset(24)] public readonly int Frame;
        [FieldOffset(28)] private readonly int _pad0;

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
    public sealed class SystemDispatcher : MonoBehaviour, ITickDispatcher, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001SystemDispatcherSignalPushDropCount;
        private const int LaneCount = 4;
        private const double FastTickIntervalSeconds = 1.0 / 60.0;
        private const double SlowTickIntervalSeconds = 0.1;
        private const double ThermalCriticalSlowTickIntervalSeconds = 0.2;
        private const double ColdTickIntervalSeconds = 1.0;
        private const double FrostTickIntervalSeconds = 5.0;
        private const int MaxCadenceSubstepsPerFrame = 4;

        // Largest fixed step a step-bounded headless run can take without ANY lane discarding time.
        //
        // The binding lane is the FIXED-step lane, not the slow lane the discard counters made visible and
        // not the fast lane either. Derivation, because guessing this wrong is the whole failure mode:
        //
        //   Fixed lane (AdvanceFixedStep): temporal compression trips when
        //   residual + dt > FixedStepSeconds * MaxFixedSubstepsPerFrame = 0.02 * 3 = 0.06 s. The residual
        //   carried in from the previous frame is anything under one FixedStepSeconds, so the bound must
        //   leave a whole substep of headroom or a boundary frame compresses on a float hair:
        //   0.02 * (3 - 1) = 0.04 s.
        //
        //   Fast/unscaled-fast lane: the clamp trips at (MaxCadenceSubstepsPerFrame + 1) intervals, and the
        //   residual is under one interval, so the entire substep budget is usable: 4 * 1/60 = 0.0667 s.
        //
        //   Slow lane: 4 * 0.1 = 0.4 s. Cold lane: 4 * 1.0 = 4.0 s. Both far looser.
        //
        // So 0.04 s, and a caller who sized a step against the slow interval (0.4 s) or the fast one
        // (0.0667 s) would silently drop physics/fixed-tick time while believing the run was clean. Both
        // candidates are derived from the live constants and the minimum is taken, so re-tuning any cadence
        // cannot leave this stale or silently invert which lane binds.
        private const double MaxClampFreeFixedStepSeconds = FixedStepSeconds * (MaxFixedSubstepsPerFrame - 1);
        private const double MaxClampFreeCadenceStepSeconds = FastTickIntervalSeconds * MaxCadenceSubstepsPerFrame;
        private const double MaxStepBoundedDeltaSeconds =
            MaxClampFreeFixedStepSeconds < MaxClampFreeCadenceStepSeconds
                ? MaxClampFreeFixedStepSeconds
                : MaxClampFreeCadenceStepSeconds;
        private const byte StepBoundedClampLaneFast = 1 << 0;
        private const byte StepBoundedClampLaneUnscaledFast = 1 << 1;
        private const byte StepBoundedClampLaneSlow = 1 << 2;
        private const byte StepBoundedClampLaneCold = 1 << 3;
        private const byte StepBoundedClampLaneFixed = 1 << 4;
        private const float TimeDilationMinimumScalar = 0f;
        private const float TimeDilationMaximumScalar = 4f;
        private const float HeadlessTimeDilationMaximumScalar = 100f;
        private const float TimeDilationPausedEpsilon = 0.0001f;
        private const float BulletTimePostScalarThreshold = 0.98f;
        private const double SlowJobCompleteWarningMilliseconds = 1.0;
        private const double SlowDispatcherPhaseWarningMilliseconds = 100.0;
        private const float JobAdmissionFrameBudgetMissThresholdSeconds = 1.0f / 60.0f;
        private const int MaxQueuedDispatcherSurfaceProbes = 1024;
        private const int DispatcherBlackBoxFrameCount = 300;
        private const int DispatcherBlackBoxEntrySizeBytes = 64;
        private const int MasterDispatcherMaxSystems = 85;
        private const int MasterDispatcherBucketCount = 64;
        private const int MasterDispatcherBucketMask = MasterDispatcherBucketCount - 1;
        private const int MasterDispatcherBlackBoxFrameCount = 300;
        private const int MasterDispatcherFenceDomainCount = 4;
        private const int MasterDispatcherMockDependencyJobCount = 100;
        private const int MasterDispatcherSignalLaneCount = 33;
        private const int MasterDispatcherDependencyScratchCapacity = 8;
        private const uint MasterDispatcherFlagVisualSyncShed = 1u << 0;
        private const uint MasterDispatcherFlagRollbackFence = 1u << 1;
        private const uint MasterDispatcherFlagHealthPressureShed = 1u << 2;
        private const BufferID MasterRollbackRuntimeStateBuffer = BufferID.ShinobuHydroKccResolvedHits;
        private const uint MasterRollbackRequiredFlag = 1u << 3;
        private const uint MasterRollbackResimulatingFlag = 1u << 4;
        private const uint MasterRollbackHardResyncRequiredFlag = 1u << 14;
        private const float MasterDispatcherStallViolationThresholdMs = 8f;
#if UNITY_EDITOR
        private const string MasterDispatcherPriorityCsvPath = "Docs/Tasks/execution_priorities.csv";
        private const string VaultMemoryProfileCsvPath = "memory_overrides.csv";
#endif
        private const int DispatcherDependencyRetryFrames = 8;
        private static readonly ulong DispatcherSurfaceProbeHitsGuardMask =
            1UL << (unchecked((int)(uint)(int)BufferID.DispatcherRaycastHits) & 31);
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
        private const double HomeostasisEmergencySlowTickIntervalSeconds = 1.0;
#if UNITY_EDITOR
        private const float AupNanInquisitorLogIntervalSeconds = 5f;
        private const float DispatcherPhaseWarningLogIntervalSeconds = 5f;
        private const string HeapLockGuardMessage = "[SystemDispatcher] HEAP LOCK GUARD: managed heap increased during fixed-step dispatch.";
#endif
        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct MasterRollbackRuntimeStateProbeDTO
        {
            [FieldOffset(0)] public ulong LastFrameHash64;
            [FieldOffset(8)] public ulong LastRemoteHash64;
            [FieldOffset(16)] public ulong LastBranchHash64;
            [FieldOffset(24)] public ulong LastRemoteBranchHash64;
            [FieldOffset(32)] public uint CurrentFrame;
            [FieldOffset(36)] public uint LastRollbackFrame;
            [FieldOffset(40)] public uint LastRemoteFrame;
            [FieldOffset(44)] public uint LastMismatchFrame;
            [FieldOffset(48)] public uint FramesResimulated;
            [FieldOffset(52)] public uint RollbacksTriggered;
            [FieldOffset(56)] public float ResimComputeTimeMs;
            [FieldOffset(60)] public float GlobalQualityWeight;
            [FieldOffset(64)] public float MismatchSeverity01;
            [FieldOffset(68)] public uint Flags;
            [FieldOffset(72)] public uint StateSnapshotBytes;
            [FieldOffset(76)] public uint StateMemoryOffset;
            [FieldOffset(80)] public uint DesyncCount;
            [FieldOffset(84)] public uint DesyncRepairAttempts;
            [FieldOffset(88)] public uint FirstMismatchBufferId;
            [FieldOffset(92)] public uint FirstMismatchByteOffset;
        }

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
        private static readonly ProfilerMarker _dispatcherSurfaceProbeScheduleProfilerMarker = new ProfilerMarker("H8.Dispatcher.SurfaceProbe.Schedule");
        private static readonly ProfilerMarker _dispatcherSurfaceProbeCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.SurfaceProbe.Complete");
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
        // Slow-tick surplus discarded by the anti-death-spiral clamp. FNV-1a of
        // "SlowTickSurplusDiscarded", following the literal-hash idiom used by the other warning ids here.
        private const uint _SlowTickSurplusDiscardedHash = 0x5B7C4E11u;
        private const uint _MasterDispatcherHash = 0x4D445350u; // MDSP
        private const uint _PlayerLoopInstallFailureHash = 0x51D10001u;
        private const uint _HeapLockGuardHash = 0x51D10002u;
        private const uint _AupNanInquisitorHash = 0x51D10003u;
        private const uint _DispatcherBlackBoxFaultHash = 0x51D10004u;
        // A cadence lane clamped while step-bounded headless time was active - the one thing that mode
        // exists to make impossible, so it is reported rather than absorbed.
        private const uint _StepBoundedTimeClampHash = 0x51D10005u;
        // A step-bounded step size was accepted that cannot be clamp-free.
        private const uint _StepBoundedTimeConfigHash = 0x51D10006u;
        private const uint _BaseStressCascadeBreakerHash = 3838237614u;
        private const uint _SimulationBucketContextHash = 0x53424B54u; // SBKT
        private const uint _FramePacingWarningHash = 0x4650574Eu; // FPWN
        private const uint _Lane4VfxKillSwitchMask = 1u << 4;
        private const ushort DispatcherBlackBoxFlagPaused = 1 << 0;
        private const ushort DispatcherBlackBoxFlagAupBarrier = 1 << 1;
        private const ushort DispatcherBlackBoxFlagOriginShiftLock = 1 << 2;
        private const ushort DispatcherBlackBoxFlagNonFinite = 1 << 3;
        private const ushort DispatcherBlackBoxFlagCoreDilation = 1 << 4;
        private const ushort DispatcherBlackBoxFlagTemporalCompression = 1 << 5;
        private const ushort DispatcherBlackBoxFlagAdrenalineDilation = 1 << 7;
        private const int DispatcherBlackBoxQualityWeightQ8Shift = 8;
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
        private static float _pendingSimulationBucketInterpolationAlpha;
        private static bool _hasPendingSimulationBucketInterpolationAlpha;
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
            new RegistryBucket<ILateFrameTickable>(128),
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

        // COLD ALLOC: object[85] - GlobalRegistry-facing master dispatcher registrations - owner: SystemDispatcher
        private static readonly object[] _masterRegisteredSystems = new object[MasterDispatcherMaxSystems];
        // COLD ALLOC: object[85] - Kahn-sorted execution order - owner: SystemDispatcher
        private static readonly object[] _masterSortedSystems = new object[MasterDispatcherMaxSystems];
        // COLD ALLOC: object[8] - fixed-only bridge registrations - owner: SystemDispatcher
        private static readonly object[] _masterFixedSystems = new object[8];
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
        // COLD ALLOC: object[256] - dispatcher-owned pending raycast receivers - owner: SystemDispatcher
        private static readonly object[] _pendingDispatcherSurfaceProbeReceivers = new object[MaxQueuedDispatcherSurfaceProbes];
        // COLD ALLOC: int[256] - dispatcher-owned pending raycast request ids - owner: SystemDispatcher
        private static readonly int[] _pendingDispatcherSurfaceProbeRequestIds = new int[MaxQueuedDispatcherSurfaceProbes];
        // COLD ALLOC: object[256] - dispatcher-owned scheduled raycast receivers - owner: SystemDispatcher
        private static readonly object[] _scheduledDispatcherSurfaceProbeReceivers = new object[MaxQueuedDispatcherSurfaceProbes];
        // COLD ALLOC: int[256] - dispatcher-owned scheduled raycast request ids - owner: SystemDispatcher
        private static readonly int[] _scheduledDispatcherSurfaceProbeRequestIds = new int[MaxQueuedDispatcherSurfaceProbes];
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
        private VaultGenerationHandle<double> _h8TimeHandle;
        private VaultGenerationHandle<DispatcherBlackBoxEntry> _dispatcherBlackBoxHandle;
        private VaultGenerationHandle<int> _dispatcherBlackBoxCursorHandle;
        private VaultGenerationHandle<JobHandle> _masterSimulationJobHandlesHandle;
        private VaultGenerationHandle<JobHandle> _masterDependencyScratchHandlesHandle;
        private VaultGenerationHandle<JobDependencyDTO> _masterJobDependencyTelemetryHandle;
        private VaultGenerationHandle<DispatcherTimingDTO> _masterPipelineTelemetryRingHandle;
        private VaultGenerationHandle<int> _masterPipelineTelemetryCursorHandle;
        private VaultGenerationHandle<MockTimeDilationSignal> _masterMockTimeDilationSignalsHandle;
        private VaultGenerationHandle<DispatcherPresentationSuppressionDTO> _masterPresentationSuppressionHandle;
        private VaultGenerationHandle<JobHandle> _masterDomainFenceHandlesHandle;
        private VaultGenerationHandle<DispatcherFenceTelemetryEntry> _masterFenceTelemetryRingHandle;
        private VaultGenerationHandle<int> _masterFenceTelemetryCursorHandle;
        private double _fastTickAccumulator;
        private double _slowTickAccumulator;
        private double _coldTickAccumulator;
        private double _frostTickAccumulator;
        private double _memoryDefragAccumulator;
        private double _unscaledFastTickAccumulator;
        private double _fixedStepAccumulator;
        private double _slowTickDiscardedSeconds;
        private int _slowTickDiscardEvents;
        private IDataVault _dataVault;
        private int _lastVaultGenerationMissCount;
        private float _lastVaultMemoryJobUs;
        private uint _lastVaultMaintenanceFlags;
        private ISimulationBucketer _simulationBucketer;
        private IJobAdmissionService _jobAdmission;
        private IInputDeterminismService _inputDeterminism;
        private IVramBudgetReadModel _vramMonitor;
        private IVramPressureSampleSink _vramPressure;
        private IVramPressureReadModel _vramPressureReadModel;
        private IMacroDatabaseService _macroDatabase;
        private IPhysicsService _physics;
        private IObjectPoolService _objectPool;
        private float _globalQualityWeight01 = 1f;
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
        private uint _aupPreShiftPauseFrameId;
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
        private float _masterLastFixedWaitMs;
        private float _masterLastAupHardFenceMs;
        private float _masterLastPostSimulationMs;
        private float _masterLastVisualSyncMs;
        private ulong _masterLastSimulationHandleBits;
        private ulong _masterLastPhysicsHandleBits;
        private ulong _masterLastAudioHandleBits;
        private ulong _masterLastNetcodeHandleBits;
        private uint _masterFrameTelemetrySequence;
        private int _masterPendingSimulationJobCount;
        private int _masterPendingFixedJobCount;
        private int _masterLastScheduledSimulationJobCount;
        private int _masterPendingSafetyBypassCount;
        private uint _masterActiveDomainMask;
        private int _masterDisabledSystemCount;
        private int _masterCsvPollFrame = -1;
        private DateTime _masterPriorityCsvLastWriteUtc;
        private long _vaultMemoryProfileCsvLastWriteTicks;
        private bool _dispatcherBlackBoxViolationPublished;
        private bool _masterSimulationJobsPending;
        private bool _masterFixedJobsPending;
        private bool _masterPipelineTelemetryViolationPublished;
        private bool _masterFenceTelemetryViolationPublished;
        private bool _masterVisualSyncShedThisFrame;
        private bool _masterRollbackFenceThisFrame;
        private bool _masterHealthPressureShedThisFrame;
        private uint _masterRollbackFenceFlagsThisFrame;
        private bool _serviceRegistered;
        private bool _runtimeGameplayBootstrapGateActive;
        private uint _dispatcherFrameSequence;
        private uint _currentDispatcherFrameId;
        private uint _memoryTelemetrySequence;
        private uint _lastMemoryDefragPressureWarningFrame;
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

        // --- Step-bounded headless time source ---------------------------------------------------------
        // Default is WallClock, so a player build is bit-identical to before: every read below is guarded
        // by a single static byte compare that is false in a player, and none of them sit inside a
        // per-item or per-substep loop - the three wall-clock reads this replaces were already once per
        // dispatcher frame. Never flipped by runtime code; only an explicit driver call flips it.
        private static HeadlessTimeMode _headlessTimeMode = HeadlessTimeMode.WallClock;
        private static float _stepBoundedDeltaSeconds;
        private static double _stepBoundedElapsedSeconds;
        private static long _stepBoundedStepIndex;
        private static byte _stepBoundedClampReportedLanes;

        /// <summary>
        /// Where the dispatcher gets the delta it advances the simulation by.
        /// </summary>
        internal enum HeadlessTimeMode : byte
        {
            /// <summary>
            /// Wall clock (<c>Time.unscaledDeltaTime</c> / <c>Time.smoothDeltaTime</c> under XR). The only
            /// mode a player build ever runs, and the default.
            /// </summary>
            WallClock = 0,

            /// <summary>
            /// A fixed step supplied by the caller. Wall clock is never read for simulation advance, so the
            /// same seed plus the same step count reaches the same state by construction rather than by
            /// luck of machine load.
            /// </summary>
            StepBounded = 1,
        }

        public static float CurrentFrameDeltaTime { get; private set; }

        public static float CurrentFrameUnscaledDeltaTime { get; private set; }

        internal static float CurrentFixedInterpolationAlpha { get; private set; }

        internal static SystemDispatcher ActiveRuntimeInstance { get; private set; }

        internal static bool IsOriginShiftBootstrapLocked => Volatile.Read(ref _originShiftBootstrapLockCount) > 0;

        internal static bool IsOriginShiftFrameLockedForCurrentFrame => Volatile.Read(ref _originShiftFrameLockFrame) == CurrentFrameIndex;

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
#if UNITY_EDITOR
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
        private static VaultGenerationHandle<KinematicSurfaceHit> _scheduledDispatcherSurfaceProbeHitsHandle;
        private static IDataVault _scheduledDispatcherSurfaceProbeHitsGuardVault;
        private static bool _scheduledDispatcherSurfaceProbeHitsGuardHeld;
        private static JobHandle _scheduledDispatcherSurfaceProbeHandle;
        private static bool _dispatcherSurfaceProbesScheduled;
        private static int _pendingDispatcherSurfaceProbeCount;
        private static int _scheduledDispatcherSurfaceProbeCount;

        [StructLayout(LayoutKind.Explicit, Size = DispatcherBlackBoxEntrySizeBytes)]
        private struct DispatcherBlackBoxEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint Sequence;
            [FieldOffset(8)] public double DilatedTime;
            [FieldOffset(16)] public double UnscaledTime;
            [FieldOffset(24)] public float DeltaTime;
            [FieldOffset(28)] public float UnscaledDeltaTime;
            [FieldOffset(32)] public float TimeDilationScalar;
            [FieldOffset(36)] public float TickOverheadMilliseconds;
            [FieldOffset(40)] public ushort Flags;
            [FieldOffset(42)] public ushort PendingSurfaceProbes;
            [FieldOffset(44)] public ushort ScheduledSurfaceProbes;
            [FieldOffset(46)] public byte HomeostasisPressureLevel;
            [FieldOffset(47)] public byte HomeostasisFoveatedTier;
            [FieldOffset(48)] public uint AupPreShiftSequence;
            [FieldOffset(52)] public uint StateHash;
            [FieldOffset(56)] public ulong KillSwitchMask;
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
                if (dispatcher != null)
                    return dispatcher._timeSnapshot.UnscaledTime;

                // No dispatcher yet. Falling through to the wall clock here would leak real time into a
                // step-bounded run during bootstrap, before the first snapshot exists.
                return _headlessTimeMode == HeadlessTimeMode.StepBounded
                    ? _stepBoundedElapsedSeconds
                    : UnityEngine.Time.unscaledTimeAsDouble;
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

#if UNITY_EDITOR
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
            DisposeDispatcherSurfaceProbeBuffers();
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
#if UNITY_EDITOR
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
            _pendingSimulationBucketInterpolationAlpha = 0f;
            _hasPendingSimulationBucketInterpolationAlpha = false;
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
#if UNITY_EDITOR
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
            // L19 hop2 LIVE: late-frame SoftMute - FMOD updateChannels AV under batch
            // with zero listeners (disable-all) after STARTERGRANT. Keep one muted
            // listener; pause+volume=0. Full SoftMute (sources) runs from bootstrap.
            if (Application.isBatchMode)
            {
                AudioListener.pause = true;
                AudioListener.volume = 0f;
                AudioListener[] batchListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                AudioListener kept = null;
                for (int li = 0; li < batchListeners.Length; li++)
                {
                    AudioListener bl = batchListeners[li];
                    if (bl == null)
                        continue;
                    if (kept == null)
                    {
                        kept = bl;
                        bl.enabled = true;
                        continue;
                    }

                    bl.enabled = false;
                }
            }

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
            signal.Frame = ReadPublishedDispatcherFrameId();
            signal.Severity = severity;
            signal.Flags = flags;
            SignalBus<ComplianceViolationSignal>.TryPushTracked(in signal, ref s_x001SystemDispatcherSignalPushDropCount);
        }

        internal static uint ReadPublishedDispatcherFrameId()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher != null && dispatcher._currentDispatcherFrameId != 0u)
                return dispatcher._currentDispatcherFrameId;

            return TimeSliceScheduler.CurrentFrameId;
        }

        public static uint CurrentFrameId => TimeSliceScheduler.CurrentFrameId;

        public static int CurrentFrameIndex => (int)(ReadPublishedDispatcherFrameId() & 0x7FFFFFFFu);

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
            int currentFrame = CurrentFrameIndex;
            if (_baseStressCascadeBreakerFrame != currentFrame)
                _baseStressCascadeBreakerFrame = currentFrame;

            int safeIslandId = math.max(0, islandId);
            int slot = ClaimBaseStressCascadeSlot(safeIslandId);
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

#if UNITY_EDITOR
        internal static void ResetBaseStressCascadeCircuitBreakerForSmokeTest()
        {
            ResetBaseStressCascadeCircuitBreakerStateForFrame(CurrentFrameIndex);
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

        private static int ClaimBaseStressCascadeSlot(int islandId)
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

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static IDispatcherSystem GetMasterRegisteredSystemAt(int index)
        {
            return _masterRegisteredSystems[index] as IDispatcherSystem;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static IDispatcherSystem GetMasterSortedSystemAt(int index)
        {
            return _masterSortedSystems[index] as IDispatcherSystem;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static IDispatcherFixedSystem GetMasterFixedSystemAt(int index)
        {
            return _masterFixedSystems[index] as IDispatcherFixedSystem;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static IDispatcherSurfaceProbeReceiver GetScheduledDispatcherSurfaceProbeReceiverAt(int index)
        {
            return _scheduledDispatcherSurfaceProbeReceivers[index] as IDispatcherSurfaceProbeReceiver;
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
        public static bool Register(IUpdatable item, PriorityLayer layer)
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
        public static bool Register(IFastTickable item, PriorityLayer layer)
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
        public static bool Register(IFixedTickable item, PriorityLayer layer)
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
        public static bool Register(ISlowTickable item, PriorityLayer layer)
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
        public static bool Register(IColdTickable item, PriorityLayer layer)
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
        public static bool Register(IFrostTickable item, PriorityLayer layer)
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
        public static bool Register(ILateFrameTickable item, PriorityLayer layer)
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
        public static bool Register(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetPostFixedLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers an unscaled fast-tick owner into a fixed priority lane.
        /// </summary>
        public static bool Register(IUnscaledFastTickable item, PriorityLayer layer)
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
                IDispatcherSystem existing = GetMasterRegisteredSystemAt(i);
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
                IDispatcherFixedSystem existing = GetMasterFixedSystemAt(i);
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
        public static void Unregister(IUpdatable item, PriorityLayer layer)
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
        public static void Unregister(IFastTickable item, PriorityLayer layer)
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
        public static void Unregister(IFixedTickable item, PriorityLayer layer)
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
        public static void Unregister(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (GetSlowLane(layer).TryUnregister(item) && item is IBucketedSlowTickable)
                _bucketedSlowTickableCount = math.max(0, _bucketedSlowTickableCount - 1);
        }

        /// <summary>
        /// Unregisters a cold-tick owner from a fixed priority lane.
        /// </summary>
        public static void Unregister(IColdTickable item, PriorityLayer layer)
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
        public static void Unregister(IFrostTickable item, PriorityLayer layer)
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
        public static void Unregister(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetLateFrameLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Removes a deferred late-frame owner from its dispatcher lane without routing through the registry.
        /// Use only when a late-frame owner must retire itself from the dispatcher-owned swap window.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Priority lane.</param>
        public static void UnregisterLateFrameTickableDirect(ILateFrameTickable item, PriorityLayer layer)
        {
            Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a post-fixed-step owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Priority lane.</param>
        public static void Unregister(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetPostFixedLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters an unscaled fast-tick owner from a fixed priority lane.
        /// </summary>
        public static void Unregister(IUnscaledFastTickable item, PriorityLayer layer)
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
            // L19 hop2 LIVE: ScalabilityDictator 0.9 core-tick burst fights probe SIMCLOCK (dil 0.9<->1 hang).
            if (UnityEngine.Application.isBatchMode)
                return;
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
            SetAupJobAdmissionBarrier(active: true);
            _dataVault?.LockAllocationsForAupShift(shiftFrameId);
            ForceCompleteMasterFencesForAupRebase(shiftFrameId);
            _aupPreShiftPauseSequence = shiftFrameId;
            _aupPreShiftPauseFrameId = ResolveCurrentDispatcherFrameId();
        }

        public void ReleaseAupPreShiftPause(uint shiftFrameId)
        {
            SetAupJobAdmissionBarrier(active: false);
        }

        private void SetAupJobAdmissionBarrier(bool active)
        {
            IJobAdmissionService jobAdmission = _jobAdmission;
            if (jobAdmission == null || !jobAdmission.IsInitialized)
                return;

            jobAdmission.SetAupBarrierActive(active);
        }

        public static void RequestDebugAupHardFence()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return;

            dispatcher.RequestAupPreShiftPause(dispatcher.ResolveCurrentDispatcherFrameId());
        }

        public static int ResolveInnerloopBatchCount(int elementCount, int minBatch, int maxBatch)
        {
            int safeMin = math.max(1, minBatch);
            int safeMax = math.max(safeMin, maxBatch);
            if (elementCount <= safeMin)
                return safeMin;

            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight)
                ? HomeostasisBrain.GlobalQualityWeight
                : 1f);
            float frameStress = math.saturate((CurrentFrameUnscaledDeltaTime - (1f / 60f)) / math.max(0.0001f, 0.025f - (1f / 60f)));
            float pressureStress = math.saturate(HomeostasisPressureLevel * 0.125f);
            float schedulerStress = math.saturate(math.max(1f - quality, math.max(frameStress, pressureStress)));
            float curved = schedulerStress * schedulerStress * (3f - 2f * schedulerStress);
            int resolved = (int)math.round(math.lerp(safeMin, safeMax, curved));
            resolved = math.clamp(resolved, safeMin, safeMax);
            return math.min(resolved, math.max(safeMin, elementCount));
        }

        public static bool IsAsyncReadbackReadyNoWait(AsyncGPUReadbackRequest request, out byte statusFlags)
        {
            const byte Pending = 1;
            const byte Error = 2;
            const byte Ready = 4;

            if (request.hasError)
            {
                statusFlags = Error;
                return false;
            }

            if (!request.done)
            {
                statusFlags = Pending;
                return false;
            }

            statusFlags = Ready;
            return true;
        }

        public static JobHandle GenerateMockDependencyChain(
            NativeArray<uint> results,
            uint seed,
            JobHandle dependsOn = default)
        {
            if (!results.IsCreated || results.Length < MasterDispatcherMockDependencyJobCount)
                return dependsOn;

            int iterations = (int)math.round(math.lerp(128f, 1024f, math.saturate(HomeostasisBrain.GlobalQualityWeight)));
            JobHandle previousA = dependsOn;
            JobHandle previousB = dependsOn;
            for (int i = 0; i < MasterDispatcherMockDependencyJobCount; i++)
            {
                JobHandle dependency = i < 2
                    ? dependsOn
                    : JobHandle.CombineDependencies(previousA, previousB);
                DispatcherMockDependencyStressJob job = default;
                job.Results = results;
                job.Seed = seed;
                job.Index = i;
                job.Iterations = iterations + (i & 31);
                JobHandle handle = job.Schedule(dependency);
                previousB = previousA;
                previousA = handle;
            }

            return JobHandle.CombineDependencies(previousA, previousB);
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
            uint frame = ResolveCurrentDispatcherFrameId();
            TimeDilationSignal dilationSignal = default;
            dilationSignal.Scalar = scalar;
            dilationSignal.UnscaledDeltaTime = CurrentFrameUnscaledDeltaTime;
            dilationSignal.Sequence = _timeDilationSequence;
            dilationSignal.Frame = frame;
            dilationSignal.ReasonHash = reasonHash;
            dilationSignal.Flags = (byte)(SimulationPaused ? 1 : 0);
            SimulationSignalRoute.TryQueueTimeDilation(in dilationSignal);

            BulletTimeVisualSignal visualSignal = default;
            visualSignal.Intensity01 = math.saturate((BulletTimePostScalarThreshold - scalar) / BulletTimePostScalarThreshold);
            visualSignal.Scalar = scalar;
            visualSignal.Frame = frame;
            visualSignal.Sequence = _timeDilationSequence;
            visualSignal.QualityWeightBits = math.asuint(_globalQualityWeight01);
            visualSignal.Flags = (byte)(SimulationPaused ? 1 : 0);
            SimulationSignalRoute.TryQueueBulletTimeVisual(in visualSignal);
        }

        private void DrainSimulationPauseSignals()
        {
            while (SignalBus<SimulationPauseSignal>.TryConsumeFrame(out SimulationPauseSignal signal))
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

            _runtimeGameplayBootstrapGateActive = Application.isPlaying;
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
            _dispatcherBlackBoxViolationPublished = false;
            CurrentFixedInterpolationAlpha = 0f;
        }

        private void OnEnable()
        {
            if (!TryClaimActiveRuntimeAfterReloadCold())
                return;

            _runtimeGameplayBootstrapGateActive = Application.isPlaying;
            if (Application.isPlaying)
                InitializeService();
            else if (_serviceRegistered)
                GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void OnDisable()
        {
            _runtimeGameplayBootstrapGateActive = false;
            if (_serviceRegistered)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
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

            _runtimeGameplayBootstrapGateActive = false;
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            if (_serviceRegistered)
            {
                HomeostasisBrain.ShutdownRuntime();
                _foveatedSimulationManager.Dispose();
                DisposeDispatcherSurfaceProbeBuffers();
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
            _aupPreShiftPauseFrameId = 0u;
            ClearCoreTickDilationBurst();
            ClearAdrenalineDilation();
            _dataVault = null;
            _lastVaultGenerationMissCount = 0;
            _lastVaultMemoryJobUs = 0f;
            _lastVaultMaintenanceFlags = 0u;
            _cachedDispatcherDataVault = null;
            _cachedCameraJuiceSystem = null;
            _simulationBucketer = null;
            _jobAdmission = null;
            _inputDeterminism = null;
            _vramMonitor = null;
            _vramPressure = null;
            _macroDatabase = null;
            _physics = null;
            _objectPool = null;
            _globalQualityWeight01 = 1f;
            _timeSnapshot = default;
            _dispatcherBlackBoxSequence = 0u;
            _dispatcherState = default;
            _masterSimulationCombinedHandle = default;
            _masterFixedCombinedHandle = default;
            _masterPendingSimulationJobCount = 0;
            _masterPendingFixedJobCount = 0;
            _masterLastScheduledSimulationJobCount = 0;
            _masterPendingSafetyBypassCount = 0;
            _masterActiveDomainMask = 0u;
            _masterLastFixedWaitMs = 0f;
            _masterLastAupHardFenceMs = 0f;
            _masterLastSimulationHandleBits = 0ul;
            _masterLastPhysicsHandleBits = 0ul;
            _masterLastAudioHandleBits = 0ul;
            _masterLastNetcodeHandleBits = 0ul;
            _masterDisabledSystemCount = 0;
            _masterPipelineTelemetryViolationPublished = false;
            _masterFenceTelemetryViolationPublished = false;
            _masterVisualSyncShedThisFrame = false;
            _masterRollbackFenceThisFrame = false;
            _masterHealthPressureShedThisFrame = false;
            _masterRollbackFenceFlagsThisFrame = 0u;
            _dispatcherBlackBoxViolationPublished = false;
            CurrentFrameDeltaTime = 0f;
            CurrentFrameUnscaledDeltaTime = 0f;
            CurrentFixedInterpolationAlpha = 0f;
            _serviceRegistered = false;
            _dispatcherFrameSequence = 0u;
            _currentDispatcherFrameId = 0u;
            _memoryTelemetrySequence = 0u;
            _lastMemoryDefragPressureWarningFrame = 0u;
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
            if (!TryClaimActiveRuntimeAfterReloadCold())
                return;

            if (_serviceRegistered)
                return;

            ThreadSafeCommandQueue.Initialize();
            RefreshDataVaultDependency();
            JobSchedulingProfileCatalog.LoadColdBootProfiles(_dataVault);
            UIStateStore.EnsureInitialized();
            BaseAirlockEvents.Prewarm();
            RefreshSimulationBucketerDependency();
            RefreshJobAdmissionDependency();
            RefreshInputDeterminismDependency();
            RefreshPeripheralDependencies();
            EnsureDispatcherSurfaceProbeBuffers();
            EnsureH8TimeArray();
            EnsureDispatcherBlackBox();
            InitializeMasterDispatcherRuntime();
            HomeostasisBrain.InitializeRuntime();
            RefreshScalabilityQualityWeight();
            HectonXRRuntimeState.RefreshPlatformStateCold(CurrentFrameIndex);
            HomeostasisBrain.RefreshCadenceSnapshotCold();
            GlobalRegistry.TryRegisterHotSwapListener(this);
            GlobalRegistry.RegisterSystemDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Dispatcher, this);
            if (_serviceRegistered && GameTickManager.ActiveRuntimeInstance != null)
                GameTickManager.ActiveRuntimeInstance.InitializeService();
            CombatDamageRuntime.Prewarm();
            EnsureDispatcherPlayerLoopInstalled();
            PublishTimeDilationState(0u);
        }

        private bool TryClaimActiveRuntimeAfterReloadCold()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return true;

            if (ActiveRuntimeInstance == null)
            {
                ActiveRuntimeInstance = this;
                return true;
            }

            _serviceRegistered = false;
            enabled = false;
            return false;
        }

        private void InitializeMasterDispatcherRuntime()
        {
            EnsureMasterDispatcherNativeBuffers();
            if (_masterRegisteredSystemCount == 0 && !_masterEmergencyTopologyInstalled && !HasLegacyDispatcherTopologyFile())
                GenerateMockSubsystems();

            EnsureMasterDispatcherTopology();
            _dispatcherState = default;
            uint frameId = ResolveCurrentDispatcherFrameId();
            _dispatcherState.CurrentFrame = frameId;
            _dispatcherState.ActiveBucket = ResolveMasterActiveBucket(frameId);
            _dispatcherState.SortedSystemCount = unchecked((uint)math.max(0, _masterSortedSystemCount));
        }

        private void EnsureMasterDispatcherNativeBuffers()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return;

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterSimulationJobHandlesHandle,
                BufferID.SystemDispatcherMasterJobHandles,
                MasterDispatcherMaxSystems,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<JobHandle> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterDependencyScratchHandlesHandle,
                BufferID.SystemDispatcherMasterDependencyScratch,
                MasterDispatcherDependencyScratchCapacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<JobHandle> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterJobDependencyTelemetryHandle,
                BufferID.SystemDispatcherMasterJobDependencyTelemetry,
                MasterDispatcherMaxSystems,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<JobDependencyDTO> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterPipelineTelemetryRingHandle,
                BufferID.SystemDispatcherMasterPipelineTelemetry,
                MasterDispatcherBlackBoxFrameCount,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<DispatcherTimingDTO> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterPipelineTelemetryCursorHandle,
                BufferID.SystemDispatcherMasterPipelineCursor,
                1,
                NativeArrayOptions.ClearMemory,
                out NativeArray<int> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterMockTimeDilationSignalsHandle,
                BufferID.SystemDispatcherMasterMockTimeDilationSignals,
                8,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<MockTimeDilationSignal> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterPresentationSuppressionHandle,
                BufferID.SystemDispatcherMasterPresentationSuppression,
                1,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<DispatcherPresentationSuppressionDTO> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterDomainFenceHandlesHandle,
                BufferID.SystemDispatcherDomainFenceHandles,
                MasterDispatcherFenceDomainCount,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<JobHandle> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterFenceTelemetryRingHandle,
                BufferID.SystemDispatcherFenceTelemetry,
                MasterDispatcherBlackBoxFrameCount,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<DispatcherFenceTelemetryEntry> _);

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _masterFenceTelemetryCursorHandle,
                BufferID.SystemDispatcherFenceTelemetryCursor,
                1,
                NativeArrayOptions.ClearMemory,
                out NativeArray<int> _);
        }

        private void DisposeMasterDispatcherRuntime()
        {
            DisposeMasterDispatcherRuntime(_dataVault);
        }

        private void DisposeMasterDispatcherRuntime(IDataVault dataVault)
        {
            ForceCompleteMasterFencesForAupRebase(ResolveCurrentDispatcherFrameId());

            ReleaseDispatcherVaultHandle(dataVault, ref _masterSimulationJobHandlesHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterDependencyScratchHandlesHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterJobDependencyTelemetryHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterPipelineTelemetryRingHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterPipelineTelemetryCursorHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterMockTimeDilationSignalsHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterPresentationSuppressionHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterDomainFenceHandlesHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterFenceTelemetryRingHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _masterFenceTelemetryCursorHandle);
            _masterSimulationCombinedHandle = default;
            _masterFixedCombinedHandle = default;
            _masterSimulationJobsPending = false;
            _masterFixedJobsPending = false;
        }

        private bool TryEnsureMasterSimulationBuffers(
            out NativeArray<JobHandle> simulationJobHandles,
            out NativeArray<JobHandle> dependencyScratchHandles,
            out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
            out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals)
        {
            return TryReadMasterSimulationBuffers(
                out simulationJobHandles,
                out dependencyScratchHandles,
                out jobDependencyTelemetry,
                out mockTimeDilationSignals);
        }

        private bool TryReadMasterSimulationBuffers(
            out NativeArray<JobHandle> simulationJobHandles,
            out NativeArray<JobHandle> dependencyScratchHandles,
            out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
            out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals)
        {
            simulationJobHandles = default;
            dependencyScratchHandles = default;
            jobDependencyTelemetry = default;
            mockTimeDilationSignals = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterSimulationJobHandlesHandle,
                MasterDispatcherMaxSystems,
                out simulationJobHandles);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterDependencyScratchHandlesHandle,
                MasterDispatcherDependencyScratchCapacity,
                out dependencyScratchHandles);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterJobDependencyTelemetryHandle,
                MasterDispatcherMaxSystems,
                out jobDependencyTelemetry);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterMockTimeDilationSignalsHandle,
                8,
                out mockTimeDilationSignals);

            return simulationJobHandles.IsCreated &&
                   simulationJobHandles.Length >= MasterDispatcherMaxSystems &&
                   dependencyScratchHandles.IsCreated &&
                   dependencyScratchHandles.Length >= MasterDispatcherDependencyScratchCapacity &&
                   jobDependencyTelemetry.IsCreated &&
                   jobDependencyTelemetry.Length >= MasterDispatcherMaxSystems &&
                   mockTimeDilationSignals.IsCreated &&
                   mockTimeDilationSignals.Length >= 8;
        }

        private bool TryEnsureMasterTelemetryBuffers(
            out NativeArray<DispatcherTimingDTO> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            return TryReadMasterTelemetryBuffers(out telemetryRing, out telemetryCursor);
        }

        private bool TryReadMasterTelemetryBuffers(
            out NativeArray<DispatcherTimingDTO> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterPipelineTelemetryRingHandle,
                MasterDispatcherBlackBoxFrameCount,
                out telemetryRing);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterPipelineTelemetryCursorHandle,
                1,
                out telemetryCursor);
            return telemetryRing.IsCreated &&
                   telemetryRing.Length >= MasterDispatcherBlackBoxFrameCount &&
                   telemetryCursor.IsCreated &&
                   telemetryCursor.Length >= 1;
        }

        private bool TryEnsureMasterDomainFenceBuffers(
            out NativeArray<JobHandle> domainFenceHandles,
            out NativeArray<DispatcherFenceTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            return TryReadMasterDomainFenceBuffers(out domainFenceHandles, out telemetryRing, out telemetryCursor);
        }

        private bool TryReadMasterDomainFenceBuffers(
            out NativeArray<JobHandle> domainFenceHandles,
            out NativeArray<DispatcherFenceTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            domainFenceHandles = default;
            telemetryRing = default;
            telemetryCursor = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterDomainFenceHandlesHandle,
                MasterDispatcherFenceDomainCount,
                out domainFenceHandles);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterFenceTelemetryRingHandle,
                MasterDispatcherBlackBoxFrameCount,
                out telemetryRing);
            TryResolveDispatcherVaultBuffer(
                dataVault,
                in _masterFenceTelemetryCursorHandle,
                1,
                out telemetryCursor);
            return domainFenceHandles.IsCreated &&
                   domainFenceHandles.Length >= MasterDispatcherFenceDomainCount &&
                   telemetryRing.IsCreated &&
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

        private static void GenerateMockSubsystems()
        {
            GenerateEmergencyMockTopology();
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

            // L19 hop2 LIVE: Kahn sort first-touches IDispatcherSystem.GetDependencyCount /
            // GetDependencyHash via interface dispatch and has produced mono_jit_compile_method AV
            // under headless batch probes after WORLDDRIVER/INPUTHOP (cs:2542). Registration order
            // is enough for probe moment census - skip dependency-edge walk under batchmode only.
            if (Application.isBatchMode)
            {
                int batchSortedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    IDispatcherSystem system = GetMasterRegisteredSystemAt(i);
                    if (system == null)
                        continue;

                    _masterSortedSystems[batchSortedCount++] = system;
                }

                _masterSortedSystemCount = batchSortedCount;
                _masterTopologyDirty = false;
                _masterTopologyValid = true;
                return;
            }

            for (int i = 0; i < count; i++)
            {
                IDispatcherSystem system = GetMasterRegisteredSystemAt(i);
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
                IDispatcherSystem provider = GetMasterRegisteredSystemAt(providerIndex);
                if (provider == null)
                    continue;

                _masterSortedSystems[sortedCount++] = provider;
                uint providerHash = provider.GetSystemIdHash();
                for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
                {
                    IDispatcherSystem candidate = GetMasterRegisteredSystemAt(candidateIndex);
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
                throw new FatalArchitectureException(BuildMasterCycleTrace(count, sortedCount));

            _masterSortedSystemCount = sortedCount;
            _masterTopologyDirty = false;
            _masterTopologyValid = true;
        }

        private static bool TryReadMasterDispatcherTopology(out int sortedSystemCount)
        {
            sortedSystemCount = 0;
            if (!_masterTopologyValid)
                return false;

            sortedSystemCount = math.clamp(_masterSortedSystemCount, 0, MasterDispatcherMaxSystems);
            return true;
        }

        private static string BuildMasterCycleTrace(int count, int sortedCount)
        {
            char[] buffer = new char[1024];
            int length = 0;
            AppendTraceText(buffer, ref length, "SystemDispatcher Kahn cycle detected. sorted=");
            AppendTraceInt(buffer, ref length, sortedCount);
            AppendTraceText(buffer, ref length, " registered=");
            AppendTraceInt(buffer, ref length, count);
            AppendTraceText(buffer, ref length, " unresolved=");

            bool wroteAny = false;
            for (int i = 0; i < count; i++)
            {
                if (_masterKahnInDegrees[i] <= 0)
                    continue;

                IDispatcherSystem system = GetMasterRegisteredSystemAt(i);
                if (system == null)
                    continue;

                if (wroteAny)
                    AppendTraceText(buffer, ref length, " | ");

                wroteAny = true;
                AppendTraceText(buffer, ref length, "0x");
                AppendTraceHex8(buffer, ref length, system.GetSystemIdHash());
                AppendTraceText(buffer, ref length, "<-");

                int dependencyCount = math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
                bool wroteDependency = false;
                for (int dependencyIndex = 0; dependencyIndex < dependencyCount; dependencyIndex++)
                {
                    uint dependencyHash = system.GetDependencyHash(dependencyIndex);
                    if (dependencyHash == 0u || FindRegisteredMasterSystemIndex(dependencyHash, count) < 0)
                        continue;

                    if (wroteDependency)
                        AppendTraceChar(buffer, ref length, ',');

                    wroteDependency = true;
                    AppendTraceText(buffer, ref length, "0x");
                    AppendTraceHex8(buffer, ref length, dependencyHash);
                }

                if (!wroteDependency)
                    AppendTraceText(buffer, ref length, "none");
            }

            if (!wroteAny)
                AppendTraceText(buffer, ref length, "unknown");

            return new string(buffer, 0, length);
        }

        private static void AppendTraceText(char[] buffer, ref int length, string value)
        {
            if (value == null)
                return;

            for (int i = 0; i < value.Length; i++)
                AppendTraceChar(buffer, ref length, value[i]);
        }

        private static void AppendTraceInt(char[] buffer, ref int length, int value)
        {
            if (value == 0)
            {
                AppendTraceChar(buffer, ref length, '0');
                return;
            }

            if (value < 0)
            {
                AppendTraceChar(buffer, ref length, '-');
                value = -value;
            }

            int start = length;
            while (value > 0)
            {
                int digit = value % 10;
                AppendTraceChar(buffer, ref length, (char)('0' + digit));
                value /= 10;
            }

            int end = length - 1;
            while (start < end)
            {
                char temp = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = temp;
                start++;
                end--;
            }
        }

        private static void AppendTraceHex8(char[] buffer, ref int length, uint value)
        {
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                uint nibble = (value >> shift) & 0xFu;
                AppendTraceChar(buffer, ref length, (char)(nibble < 10u ? '0' + nibble : 'A' + (nibble - 10u)));
            }
        }

        private static void AppendTraceChar(char[] buffer, ref int length, char value)
        {
            if ((uint)length >= (uint)buffer.Length)
                return;

            buffer[length++] = value;
        }

        private static int FindRegisteredMasterSystemIndex(uint systemHash, int count)
        {
            for (int i = 0; i < count; i++)
            {
                IDispatcherSystem system = GetMasterRegisteredSystemAt(i);
                if (system != null && system.GetSystemIdHash() == systemHash)
                    return i;
            }

            return -1;
        }

        private static int FindSortedMasterSystemIndex(uint systemHash)
        {
            for (int i = 0; i < _masterSortedSystemCount; i++)
            {
                IDispatcherSystem system = GetMasterSortedSystemAt(i);
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
            uint frameId = ResolveCurrentDispatcherFrameId();
            uint activeBucket = ResolveMasterActiveBucket(frameId);
            uint activeBucketMask = 1u << (int)(activeBucket & 31u);
            DispatcherTimingDTO timing = default;
            timing.FrameDelta = deltaTime;
            timing.FixedDelta = fixedDeltaTime;
            timing.TimeScale = unscaledDeltaTime > 0f ? math.saturate(deltaTime / unscaledDeltaTime) : 0f;
            timing.ActiveBucketMask = activeBucketMask;
            timing.FrameId = frameId;
            return timing;
        }

        private uint ResolveCurrentDispatcherFrameId()
        {
            if (_currentDispatcherFrameId == 0u)
                return AdvanceDispatcherFrameId();
            return _currentDispatcherFrameId;
        }

        private uint AdvanceDispatcherFrameId()
        {
            _dispatcherFrameSequence++;
            if (_dispatcherFrameSequence == 0u)
                _dispatcherFrameSequence = 1u;
            _currentDispatcherFrameId = _dispatcherFrameSequence;
            return _currentDispatcherFrameId;
        }

        private uint ResolveMasterActiveBucket()
        {
            return ResolveMasterActiveBucket(ResolveCurrentDispatcherFrameId());
        }

        private static uint ResolveMasterActiveBucket(uint frameId)
        {
            return frameId & MasterDispatcherBucketMask;
        }

        private void SetMasterDispatcherPhase(DispatcherPhase phase, in DispatcherTimingDTO timing)
        {
            ref DispatcherStateDTO state = ref GetDispatcherStateRef();
            state.CurrentPhaseId = (uint)phase;
            state.CurrentFrame = timing.FrameId;
            state.ActiveBucket = ResolveMasterActiveBucket(timing.FrameId);
            state.ActiveBucketMask = timing.ActiveBucketMask;
            state.SortedSystemCount = unchecked((uint)math.max(0, _masterSortedSystemCount));
            state.DisabledSystemCount = unchecked((uint)math.max(0, _masterDisabledSystemCount));
            state.PendingSimulationJobCount = unchecked((uint)math.max(0, _masterPendingSimulationJobCount));
            state.Flags = BuildMasterDispatcherTransientFlags();
        }

        private uint BuildMasterDispatcherTransientFlags()
        {
            uint flags = 0u;
            if (_masterVisualSyncShedThisFrame)
                flags |= MasterDispatcherFlagVisualSyncShed;
            if (_masterRollbackFenceThisFrame)
                flags |= MasterDispatcherFlagRollbackFence;
            if (_masterHealthPressureShedThisFrame)
                flags |= MasterDispatcherFlagHealthPressureShed;
            return flags;
        }

        private float ResolveGraphicsUploadPressure01()
        {
            IVramPressureReadModel pressureReadModel = _vramPressureReadModel;
            if (pressureReadModel != null && pressureReadModel.HasSample)
                return math.saturate(pressureReadModel.PressureFactor);

            IVramBudgetReadModel monitor = _vramMonitor;
            if (monitor == null)
                return 0f;

            if (monitor.PressureStateCode == VramPressureStateCodes.Critical || monitor.IsTotalVRAMOverBudget)
                return 1f;
            if (monitor.PressureStateCode == VramPressureStateCodes.Warning ||
                monitor.IsTextureMemoryOverBudget ||
                monitor.IsRenderTextureMemoryOverBudget)
            {
                return 0.72f;
            }

            return 0f;
        }

        private void RunMasterPreSimulationPhase(in DispatcherTimingDTO timing)
        {
            _masterFrameStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _masterPreSimulationStartTimestamp = _masterFrameStartTimestamp;
            _masterVisualSyncShedThisFrame = false;
            _masterRollbackFenceThisFrame = false;
            _masterHealthPressureShedThisFrame = false;
            _masterRollbackFenceFlagsThisFrame = 0u;
            TimeSliceScheduler.BeginFrame(HomeostasisBrain.GlobalQualityWeight, timing.FrameId);
            GraphicsBufferUploadUtility.BeginUploadBudgetFrame(
                HomeostasisBrain.GlobalQualityWeight,
                timing.FrameId,
                ResolveGraphicsUploadPressure01());
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

                    IDispatcherSystem system = GetMasterSortedSystemAt(i);
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
            if (!TryEnsureMasterSimulationBuffers(
                    out NativeArray<JobHandle> simulationJobHandles,
                    out NativeArray<JobHandle> dependencyScratchHandles,
                    out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
                    out NativeArray<MockTimeDilationSignal> mockTimeDilationSignals))
            {
                return;
            }

            bool hasDomainFenceBuffers = TryEnsureMasterDomainFenceBuffers(
                out NativeArray<JobHandle> domainFenceHandles,
                out NativeArray<DispatcherFenceTelemetryEntry> _,
                out NativeArray<int> _);

            _masterPendingSimulationJobCount = 0;
            _masterSimulationJobsPending = false;
            _masterLastScheduledSimulationJobCount = 0;
            _masterActiveDomainMask = 0u;
            _masterLastSimulationHandleBits = 0ul;
            _masterLastPhysicsHandleBits = 0ul;
            _masterLastAudioHandleBits = 0ul;
            _masterLastNetcodeHandleBits = 0ul;

            for (int i = 0; i < simulationJobHandles.Length; i++)
                simulationJobHandles[i] = default;
            for (int i = 0; i < jobDependencyTelemetry.Length; i++)
                jobDependencyTelemetry[i] = default;
            if (hasDomainFenceBuffers)
            {
                for (int i = 0; i < MasterDispatcherFenceDomainCount; i++)
                    domainFenceHandles[i] = default;
            }
            for (int i = 0; i < MasterDispatcherBucketCount; i++)
                _masterBucketLoadCounters[i] = 0u;

            DispatcherJobContext context = default;
            context.MockTimeDilationSignals = mockTimeDilationSignals;
            context.JobDependencyTelemetry = jobDependencyTelemetry;
            context.Frame = timing.FrameId;
            context.ActiveBucket = ResolveMasterActiveBucket(timing.FrameId);

            using (_masterSimulationProfilerMarker.Auto())
            {
                for (int sortedIndex = 0; sortedIndex < _masterSortedSystemCount; sortedIndex++)
                {
                    if (_masterSystemDisabled[sortedIndex])
                        continue;

                    IDispatcherSystem system = GetMasterSortedSystemAt(sortedIndex);
                    if (system == null || system.GetDispatcherPhase() != DispatcherPhase.Simulation)
                        continue;
                    if (!ShouldRunMasterSystemInActiveBucket(system, context.ActiveBucket))
                        continue;

                    JobHandle dependencyHandle = BuildMasterDependencyHandleIntoScratch(
                        system,
                        simulationJobHandles,
                        dependencyScratchHandles);
                    try
                    {
                        JobHandle handle = system.ScheduleSimulation(in timing, in context, dependencyHandle);
                        simulationJobHandles[sortedIndex] = handle;
                        DispatcherFenceDomain domain = ResolveMasterFenceDomain(system);
                        if (hasDomainFenceBuffers)
                            AccumulateDomainFence(domainFenceHandles, domain, ref handle);
                        jobDependencyTelemetry[sortedIndex] = BuildJobDependencyTelemetry(system, domain, ref handle, timing.FrameId);
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
                _masterSimulationCombinedHandle = hasDomainFenceBuffers
                    ? JobHandle.CombineDependencies(domainFenceHandles)
                    : JobHandle.CombineDependencies(simulationJobHandles);
                _masterLastSimulationHandleBits = CaptureJobHandleBits(ref _masterSimulationCombinedHandle);
                if (hasDomainFenceBuffers)
                    CaptureDomainFenceBits(domainFenceHandles);
                _masterLastScheduledSimulationJobCount = _masterPendingSimulationJobCount;
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
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    if (_masterSimulationJobsPending)
                    {
                        long waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        DispatcherJobFence.TryComplete(ref _masterSimulationCombinedHandle, forceComplete: true);
                        _masterLastSimWaitMs = ElapsedMilliseconds(waitStart);
                        _masterSimulationJobsPending = false;
                        _masterPendingSimulationJobCount = 0;
                        ApplyMockTimeDilationSignals(timing.FrameId);
                    }
                    else
                    {
                        _masterLastSimWaitMs = 0f;
                    }
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }

                for (int i = 0; i < _masterSortedSystemCount; i++)
                {
                    if (_masterSystemDisabled[i])
                        continue;

                    IDispatcherSystem system = GetMasterSortedSystemAt(i);
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

                ProcessDeferredArenaGrowthPostSimulation();
                SignalCorridorRuntime.FlushPostSimulation();
            }

            _masterLastPostSimulationMs = ElapsedMilliseconds(_masterPostSimulationStartTimestamp);
            _masterPhaseTimingSnapshotMs[1] = _masterLastSimWaitMs;
            _masterPhaseTimingSnapshotMs[2] = _masterLastPostSimulationMs;
            RecordVaultSovereigntyPostSimulationHeartbeat();
        }

        private void ProcessDeferredArenaGrowthPostSimulation()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null && TryResolveCachedDataVault(out dataVault))
                _dataVault = dataVault;

            if (dataVault is GlobalDataVault globalDataVault)
                globalDataVault.ProcessDeferredArenaGrowth();
        }

        private void RunMasterVisualSyncPhase()
        {
            DispatcherTimingDTO timing = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime);
            _masterVisualSyncStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _masterHealthPressureShedThisFrame = ShouldShedMasterVisualSync();
            _masterRollbackFenceThisFrame = TryFenceRollbackBeforeVisualSync();
            _masterVisualSyncShedThisFrame = _masterHealthPressureShedThisFrame || _masterRollbackFenceThisFrame;
            WriteMasterPresentationSuppression(timing.FrameId);
            SetMasterDispatcherPhase(DispatcherPhase.VisualSync, in timing);
            if (_masterVisualSyncShedThisFrame)
            {
                _masterLastVisualSyncMs = 0f;
                RecordMasterPipelineTelemetry(timing.FrameId);
                return;
            }

            using (_masterVisualSyncProfilerMarker.Auto())
            {
                for (int i = 0; i < _masterSortedSystemCount; i++)
                {
                    if (_masterSystemDisabled[i])
                        continue;

                    IDispatcherSystem system = GetMasterSortedSystemAt(i);
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
            RecordMasterPipelineTelemetry(timing.FrameId);
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
                IDispatcherFixedSystem system = GetMasterFixedSystemAt(i);
                if (system == null)
                    continue;

                JobHandle previous = combined;
                combined = system.ScheduleFixedSimulation(in timing, combined);
                CaptureMasterFixedDomainFence(system, ref combined, ref previous);
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
                long waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
                DispatcherJobFence.BeginPostFixedSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _masterFixedCombinedHandle, forceComplete: true);
                    _masterLastFixedWaitMs = ElapsedMilliseconds(waitStart);
                    _masterFixedJobsPending = false;
                }
                finally
                {
                    DispatcherJobFence.EndPostFixedSwapWindow();
                }
            }
            else
            {
                _masterLastFixedWaitMs = 0f;
            }

            for (int i = 0; i < _masterFixedSystemCount; i++)
            {
                IDispatcherFixedSystem system = GetMasterFixedSystemAt(i);
                if (system == null)
                    continue;

                system.PostFixedSimulation(in timing);
            }

            _masterPendingFixedJobCount = 0;
        }

        private void ForceCompleteMasterFencesForAupRebase(uint shiftFrameId)
        {
            long waitStart = System.Diagnostics.Stopwatch.GetTimestamp();
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (_masterSimulationJobsPending)
                {
                    DispatcherJobFence.TryComplete(ref _masterSimulationCombinedHandle, forceComplete: true);
                    _masterSimulationJobsPending = false;
                    _masterPendingSimulationJobCount = 0;
                }

                if (_masterFixedJobsPending)
                {
                    DispatcherJobFence.TryComplete(ref _masterFixedCombinedHandle, forceComplete: true);
                    _masterFixedJobsPending = false;
                    _masterPendingFixedJobCount = 0;
                }
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            IDataVault dataVault = _dataVault;
            if (TryResolveDispatcherVaultBuffer(
                    dataVault,
                    in _masterDomainFenceHandlesHandle,
                    MasterDispatcherFenceDomainCount,
                    out NativeArray<JobHandle> domainFenceHandles))
            {
                for (int i = 0; i < MasterDispatcherFenceDomainCount; i++)
                    domainFenceHandles[i] = default;
            }

            _masterLastAupHardFenceMs = ElapsedMilliseconds(waitStart);
            _masterActiveDomainMask = 0u;
            _masterLastSimulationHandleBits = 0ul;
            _masterLastPhysicsHandleBits = 0ul;
            _masterLastAudioHandleBits = 0ul;
            _masterLastNetcodeHandleBits = 0ul;
            PublishDispatcherComplianceViolation(_DispatcherBlackBoxFaultHash, shiftFrameId, 3, 0);
        }

        private JobHandle BuildMasterDependencyHandleIntoScratch(
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

        private static DispatcherFenceDomain ResolveMasterFenceDomain(IDispatcherSystem system)
        {
            if (system is IDispatcherFenceDomainProvider provider)
                return provider.GetFenceDomain();

            return DispatcherFenceDomain.Simulation;
        }

        private static DispatcherFenceDomain ResolveMasterFixedFenceDomain(IDispatcherFixedSystem system)
        {
            if (system is IDispatcherFenceDomainProvider provider)
                return provider.GetFenceDomain();

            return DispatcherFenceDomain.Simulation;
        }

        private void CaptureMasterFixedDomainFence(
            IDispatcherFixedSystem system,
            ref JobHandle handle,
            ref JobHandle previousHandle)
        {
            ulong handleBits = CaptureJobHandleBits(ref handle);
            if (handleBits == 0ul || handleBits == CaptureJobHandleBits(ref previousHandle))
                return;

            DispatcherFenceDomain domain = ResolveMasterFixedFenceDomain(system);
            int domainIndex = math.clamp((int)domain, 0, MasterDispatcherFenceDomainCount - 1);
            _masterActiveDomainMask |= 1u << domainIndex;

            switch (domain)
            {
                case DispatcherFenceDomain.Physics:
                    _masterLastPhysicsHandleBits = handleBits;
                    break;
                case DispatcherFenceDomain.Audio:
                    _masterLastAudioHandleBits = handleBits;
                    break;
                case DispatcherFenceDomain.Netcode:
                    _masterLastNetcodeHandleBits = handleBits;
                    break;
            }
        }

        private void AccumulateDomainFence(
            NativeArray<JobHandle> domainFenceHandles,
            DispatcherFenceDomain domain,
            ref JobHandle handle)
        {
            int domainIndex = math.clamp((int)domain, 0, MasterDispatcherFenceDomainCount - 1);
            JobHandle existing = domainFenceHandles[domainIndex];
            domainFenceHandles[domainIndex] = (_masterActiveDomainMask & (1u << domainIndex)) == 0u
                ? handle
                : JobHandle.CombineDependencies(existing, handle);
            _masterActiveDomainMask |= 1u << domainIndex;
        }

        private void CaptureDomainFenceBits(NativeArray<JobHandle> domainFenceHandles)
        {
            JobHandle physics = domainFenceHandles[(int)DispatcherFenceDomain.Physics];
            JobHandle audio = domainFenceHandles[(int)DispatcherFenceDomain.Audio];
            JobHandle netcode = domainFenceHandles[(int)DispatcherFenceDomain.Netcode];
            _masterLastPhysicsHandleBits = CaptureJobHandleBits(ref physics);
            _masterLastAudioHandleBits = CaptureJobHandleBits(ref audio);
            _masterLastNetcodeHandleBits = CaptureJobHandleBits(ref netcode);
        }

        private static JobDependencyDTO BuildJobDependencyTelemetry(
            IDispatcherSystem system,
            DispatcherFenceDomain domain,
            ref JobHandle handle,
            uint frameId)
        {
            JobDependencyDTO dto = default;
            dto.JobHandleBits = CaptureJobHandleBits(ref handle);
            dto.SystemIdHash = system.GetSystemIdHash();
            dto.FrameId = frameId;
            dto.DependencyHash0 = system.GetDependencyCount() > 0 ? system.GetDependencyHash(0) : 0u;
            dto.PhaseId = (byte)system.GetDispatcherPhase();
            dto.DomainId = (byte)domain;
            dto.DependencyCount = (byte)math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
            dto.BucketId = system.GetBucketId();
            dto.Flags = 0u;
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
            if (!TryEnsureMasterSimulationBuffers(
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

        private bool TryFenceRollbackBeforeVisualSync()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                !TryReadExistingDispatcherVaultBuffer(
                    dataVault,
                    MasterRollbackRuntimeStateBuffer,
                    SystemID.CoreDeterminism,
                    1,
                    out NativeArray<MasterRollbackRuntimeStateProbeDTO>.ReadOnly rollbackStateBuffer) ||
                rollbackStateBuffer.Length <= 0)
            {
                return false;
            }

            const uint fenceMask =
                MasterRollbackRequiredFlag |
                MasterRollbackResimulatingFlag |
                MasterRollbackHardResyncRequiredFlag;
            MasterRollbackRuntimeStateProbeDTO rollbackState = rollbackStateBuffer[0];
            uint activeFlags = rollbackState.Flags & fenceMask;
            if (activeFlags == 0u)
                return false;

            _masterRollbackFenceFlagsThisFrame = activeFlags;
            return true;
        }

        private void WriteMasterPresentationSuppression(uint frame)
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return;

            if (!TryResolveDispatcherVaultBuffer(
                    dataVault,
                    in _masterPresentationSuppressionHandle,
                    1,
                    out NativeArray<DispatcherPresentationSuppressionDTO> suppression))
            {
                return;
            }

            uint flags = DispatcherPresentationSuppressionFlags.None;
            if (_masterVisualSyncShedThisFrame)
                flags |= DispatcherPresentationSuppressionFlags.VisualSyncSuppressed;
            if (_masterRollbackFenceThisFrame)
            {
                flags |= DispatcherPresentationSuppressionFlags.RollbackFence |
                         DispatcherPresentationSuppressionFlags.AudioSuppression |
                         DispatcherPresentationSuppressionFlags.ParticleSuppression;
            }
            if (_masterHealthPressureShedThisFrame)
                flags |= DispatcherPresentationSuppressionFlags.HealthPressure;

            DispatcherPresentationSuppressionDTO entry = default;
            entry.FrameId = frame;
            entry.Flags = flags;
            entry.GlobalQualityWeight = math.saturate(math.isfinite(_globalQualityWeight01) ? _globalQualityWeight01 : 1f);
            entry.Suppression01 = flags == 0u ? 0f : 1f;
            entry.RollbackFlags = _masterRollbackFenceFlagsThisFrame;
            suppression[0] = entry;
        }

        private void RecordMasterPipelineTelemetry(uint frame)
        {
            if (!TryEnsureMasterTelemetryBuffers(
                    out NativeArray<DispatcherTimingDTO> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            int cursor = telemetryCursor[0];
            if ((uint)cursor >= (uint)MasterDispatcherBlackBoxFrameCount)
                cursor = 0;

            DispatcherTimingDTO entry = default;
            entry.PreSimMs = SanitizeNonNegativeMilliseconds(_masterLastPreSimulationMs);
            entry.SimWaitMs = SanitizeNonNegativeMilliseconds(_masterLastSimWaitMs);
            entry.PostSimMs = SanitizeNonNegativeMilliseconds(_masterLastPostSimulationMs);
            entry.VisualSyncMs = SanitizeNonNegativeMilliseconds(_masterLastVisualSyncMs);
            entry.FrameId = frame;
            telemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= MasterDispatcherBlackBoxFrameCount)
                cursor = 0;
            telemetryCursor[0] = cursor;
            _masterFrameTelemetrySequence++;

            if (entry.SimWaitMs > MasterDispatcherStallViolationThresholdMs && !_masterPipelineTelemetryViolationPublished)
            {
                _masterPipelineTelemetryViolationPublished = true;
                PublishDispatcherComplianceViolation(_MasterDispatcherHash, _SystemDispatcherHash, 5, 1);
            }

            RecordMasterFenceTelemetry(frame);
        }

        private void RecordMasterFenceTelemetry(uint frame)
        {
            if (!TryEnsureMasterDomainFenceBuffers(
                    out NativeArray<JobHandle> _,
                    out NativeArray<DispatcherFenceTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            int cursor = telemetryCursor[0];
            if ((uint)cursor >= (uint)MasterDispatcherBlackBoxFrameCount)
                cursor = 0;

            DispatcherFenceTelemetryEntry entry = default;
            entry.FrameId = frame;
            entry.ScheduledJobCount = unchecked((uint)math.max(0, _masterLastScheduledSimulationJobCount));
            entry.SafetyBypassCount = unchecked((uint)math.max(0, _masterPendingSafetyBypassCount));
            entry.DomainMask = _masterActiveDomainMask;
            entry.SimulationWaitMs = SanitizeNonNegativeMilliseconds(_masterLastSimWaitMs);
            entry.FixedWaitMs = SanitizeNonNegativeMilliseconds(_masterLastFixedWaitMs);
            entry.AupHardFenceMs = SanitizeNonNegativeMilliseconds(_masterLastAupHardFenceMs);
            entry.GlobalQualityWeight = math.saturate(math.isfinite(_globalQualityWeight01) ? _globalQualityWeight01 : 1f);
            entry.MasterSimulationHandleBits = _masterLastSimulationHandleBits;
            entry.PhysicsHandleBits = _masterLastPhysicsHandleBits;
            entry.AudioHandleBits = _masterLastAudioHandleBits;
            entry.NetcodeHandleBits = _masterLastNetcodeHandleBits;
            telemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= MasterDispatcherBlackBoxFrameCount)
                cursor = 0;
            telemetryCursor[0] = cursor;

            if (ShouldFlagMasterFenceTelemetry(in entry) && !_masterFenceTelemetryViolationPublished)
            {
                _masterFenceTelemetryViolationPublished = true;
                PublishDispatcherComplianceViolation(_MasterDispatcherHash, _SystemDispatcherHash, 6, 1);
            }

            _masterLastAupHardFenceMs = 0f;
        }

        private static bool ShouldFlagMasterFenceTelemetry(in DispatcherFenceTelemetryEntry entry)
        {
            return entry.SimulationWaitMs > MasterDispatcherStallViolationThresholdMs ||
                   entry.FixedWaitMs > MasterDispatcherStallViolationThresholdMs ||
                   entry.AupHardFenceMs > MasterDispatcherStallViolationThresholdMs;
        }

        private void TryReloadMasterExecutionPriorityCsv()
        {
#if UNITY_EDITOR
            int frame = CurrentFrameIndex;
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

#if UNITY_EDITOR
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
                IDispatcherSystem candidate = GetMasterRegisteredSystemAt(i);
                int candidatePriority = ResolveMasterCsvPriority(candidate, entryCount);
                int j = i - 1;
                while (j >= 0 && ResolveMasterCsvPriority(GetMasterRegisteredSystemAt(j), entryCount) > candidatePriority)
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
                    Span<byte> singleByte = stackalloc byte[1];

                    while (true)
                    {
                        int read = stream.Read(singleByte);
                        bool end = read <= 0;
                        read = end ? -1 : singleByte[0];
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
                if (!dispatcher.TryCopyLatestMasterTelemetry(phaseMilliseconds))
                {
                    int count = math.min(phaseMilliseconds.Length, _masterPhaseTimingSnapshotMs.Length);
                    for (int i = 0; i < count; i++)
                        phaseMilliseconds[i] = _masterPhaseTimingSnapshotMs[i];
                }
            }

            if (bucketLoads != null)
            {
                int count = math.min(bucketLoads.Length, _masterBucketLoadCounters.Length);
                for (int i = 0; i < count; i++)
                    bucketLoads[i] = _masterBucketLoadCounters[i];
            }

            return true;
        }

        public static bool TryGetDependencyGraphSnapshot(
            uint[] systemHashes,
            uint[] dependencyHashes,
            byte[] phaseIds,
            byte[] dependencyCounts,
            out int systemCount)
        {
            systemCount = 0;
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return false;

            if (!TryReadMasterDispatcherTopology(out int sortedSystemCount))
                return false;

            int copyCount = sortedSystemCount;
            if (systemHashes != null)
                copyCount = math.min(copyCount, systemHashes.Length);
            if (dependencyHashes != null)
                copyCount = math.min(copyCount, dependencyHashes.Length);
            if (phaseIds != null)
                copyCount = math.min(copyCount, phaseIds.Length);
            if (dependencyCounts != null)
                copyCount = math.min(copyCount, dependencyCounts.Length);

            for (int i = 0; i < copyCount; i++)
            {
                IDispatcherSystem system = GetMasterSortedSystemAt(i);
                if (system == null)
                    continue;

                if (systemHashes != null)
                    systemHashes[i] = system.GetSystemIdHash();

                int dependencyCount = math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
                if (dependencyHashes != null)
                    dependencyHashes[i] = dependencyCount > 0 ? system.GetDependencyHash(0) : 0u;
                if (phaseIds != null)
                    phaseIds[i] = (byte)system.GetDispatcherPhase();
                if (dependencyCounts != null)
                    dependencyCounts[i] = (byte)dependencyCount;
            }

            systemCount = copyCount;
            return true;
        }

        public static bool TryGetDependencyGraphEdges(
            uint[] systemHashes,
            uint[] dependencyHashes,
            byte[] phaseIds,
            out int edgeCount,
            out int systemCount)
        {
            edgeCount = 0;
            systemCount = 0;
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return false;

            if (!TryReadMasterDispatcherTopology(out int sortedSystemCount))
                return false;

            systemCount = sortedSystemCount;
            int capacity = int.MaxValue;
            if (systemHashes != null)
                capacity = math.min(capacity, systemHashes.Length);
            if (dependencyHashes != null)
                capacity = math.min(capacity, dependencyHashes.Length);
            if (phaseIds != null)
                capacity = math.min(capacity, phaseIds.Length);
            if (capacity == int.MaxValue)
                capacity = 0;

            for (int systemIndex = 0; systemIndex < sortedSystemCount; systemIndex++)
            {
                if (edgeCount >= capacity)
                    break;

                IDispatcherSystem system = GetMasterSortedSystemAt(systemIndex);
                if (system == null)
                    continue;

                uint systemHash = system.GetSystemIdHash();
                byte phaseId = (byte)system.GetDispatcherPhase();
                int dependencyCount = math.clamp(system.GetDependencyCount(), 0, MasterDispatcherDependencyScratchCapacity);
                if (dependencyCount == 0)
                {
                    if (systemHashes != null)
                        systemHashes[edgeCount] = systemHash;
                    if (dependencyHashes != null)
                        dependencyHashes[edgeCount] = 0u;
                    if (phaseIds != null)
                        phaseIds[edgeCount] = phaseId;
                    edgeCount++;
                    continue;
                }

                for (int dependencyIndex = 0; dependencyIndex < dependencyCount && edgeCount < capacity; dependencyIndex++)
                {
                    uint dependencyHash = system.GetDependencyHash(dependencyIndex);
                    if (systemHashes != null)
                        systemHashes[edgeCount] = systemHash;
                    if (dependencyHashes != null)
                        dependencyHashes[edgeCount] = dependencyHash;
                    if (phaseIds != null)
                        phaseIds[edgeCount] = phaseId;
                    edgeCount++;
                }
            }

            return true;
        }

        public static bool TryGetJobDependencyTelemetrySnapshot(
            uint[] systemHashes,
            ulong[] jobHandleBits,
            out int jobCount)
        {
            jobCount = 0;
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return false;

            if (!dispatcher.TryReadMasterSimulationBuffers(
                    out NativeArray<JobHandle> _,
                    out NativeArray<JobHandle> _,
                    out NativeArray<JobDependencyDTO> jobDependencyTelemetry,
                    out NativeArray<MockTimeDilationSignal> _))
            {
                return false;
            }

            int capacity = int.MaxValue;
            if (systemHashes != null)
                capacity = math.min(capacity, systemHashes.Length);
            if (jobHandleBits != null)
                capacity = math.min(capacity, jobHandleBits.Length);
            if (capacity == int.MaxValue)
                capacity = 0;

            for (int i = 0; i < jobDependencyTelemetry.Length && jobCount < capacity; i++)
            {
                JobDependencyDTO dto = jobDependencyTelemetry[i];
                if (dto.SystemIdHash == 0u && dto.JobHandleBits == 0ul)
                    continue;

                if (systemHashes != null)
                    systemHashes[jobCount] = dto.SystemIdHash;
                if (jobHandleBits != null)
                    jobHandleBits[jobCount] = dto.JobHandleBits;
                jobCount++;
            }

            return true;
        }

        public static bool TryGetLatestFenceTelemetry(out DispatcherFenceTelemetryEntry entry)
        {
            entry = default;
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return false;

            if (!dispatcher.TryReadMasterDomainFenceBuffers(
                    out NativeArray<JobHandle> _,
                    out NativeArray<DispatcherFenceTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return false;
            }

            int cursor = telemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = MasterDispatcherBlackBoxFrameCount - 1;
            if ((uint)cursor >= (uint)telemetryRing.Length)
                return false;

            entry = telemetryRing[cursor];
            return entry.FrameId != 0u || entry.ScheduledJobCount != 0u;
        }

        private bool TryCopyLatestMasterTelemetry(float[] phaseMilliseconds)
        {
            if (phaseMilliseconds == null || phaseMilliseconds.Length < 4)
                return false;
            if (!TryReadMasterTelemetryBuffers(
                    out NativeArray<DispatcherTimingDTO> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return false;
            }

            int cursor = telemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = MasterDispatcherBlackBoxFrameCount - 1;
            if ((uint)cursor >= (uint)telemetryRing.Length)
                return false;

            DispatcherTimingDTO entry = telemetryRing[cursor];
            if (entry.FrameId == 0u)
                return false;

            phaseMilliseconds[0] = entry.PreSimMs;
            phaseMilliseconds[1] = entry.SimWaitMs;
            phaseMilliseconds[2] = entry.PostSimMs;
            phaseMilliseconds[3] = entry.VisualSyncMs;
            return true;
        }

        private bool EnsureH8TimeArray()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            return TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _h8TimeHandle,
                BufferID.H8Time,
                (int)H8TimeSlot.Count,
                NativeArrayOptions.ClearMemory,
                out NativeArray<double> _);
        }

        private bool TryResolveH8TimeArray(out NativeArray<double> h8Time)
        {
            h8Time = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            return TryResolveDispatcherVaultBuffer(
                dataVault,
                in _h8TimeHandle,
                (int)H8TimeSlot.Count,
                out h8Time);
        }

        private void RefreshDataVaultDependency()
        {
            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault == null)
            {
                _cachedDispatcherDataVault = null;
                return;
            }

            if (_dataVault != null && !ReferenceEquals(_dataVault, dataVault))
                ReleaseSystemDispatcherVaultHandles(_dataVault);

            _dataVault = dataVault;
            _cachedDispatcherDataVault = dataVault;
            VaultSovereigntyTelemetry.EnsureRing(dataVault);
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
                return dataVault != null;
            }

            return false;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsVaultGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool TryResolveDispatcherVaultBuffer<T>(
            IDataVault dataVault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength <= 0 ||
                !IsVaultGenerationHandleCreated(in handle) ||
                !dataVault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryResolveDispatcherVaultBuffer<T>(
            IDataVault dataVault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength <= 0 ||
                !IsVaultGenerationHandleCreated(in handle) ||
                !dataVault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved.AsReadOnly();
            return true;
        }

        private static bool TryReadExistingDispatcherVaultBuffer<T>(
            IDataVault dataVault,
            BufferID bufferId,
            SystemID ownerSystem,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength <= 0 ||
                dataVault.IsCompactionFenceActive ||
                !dataVault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)ownerSystem ||
                handle.Generation == 0u ||
                !dataVault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private static bool TryResolveExistingDispatcherVaultBuffer<T>(
            IDataVault dataVault,
            BufferID bufferId,
            SystemID ownerSystem,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength <= 0 ||
                dataVault.IsCompactionFenceActive ||
                !dataVault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)ownerSystem ||
                handle.Generation == 0u ||
                !dataVault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private static bool TryEnsureDispatcherVaultBuffer<T>(
            IDataVault dataVault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryResolveDispatcherVaultBuffer(dataVault, in handle, requiredLength, out buffer))
                return true;

            buffer = default;
            if (dataVault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)
                return false;

            handle = dataVault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.SystemDispatcher,
                options);

            return TryResolveDispatcherVaultBuffer(dataVault, in handle, requiredLength, out buffer);
        }

        private static void ReleaseDispatcherVaultHandle<T>(
            IDataVault dataVault,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsVaultGenerationHandleCreated(in handle) && dataVault != null)
                dataVault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ReleaseSystemDispatcherVaultHandles(IDataVault dataVault)
        {
            if (!_dispatcherSurfaceProbesScheduled &&
                !_masterSimulationJobsPending &&
                !_masterFixedJobsPending &&
                !IsVaultGenerationHandleCreated(in _scheduledDispatcherSurfaceProbeHitsHandle) &&
                !IsVaultGenerationHandleCreated(in _h8TimeHandle) &&
                !IsVaultGenerationHandleCreated(in _dispatcherBlackBoxHandle) &&
                !IsVaultGenerationHandleCreated(in _dispatcherBlackBoxCursorHandle) &&
                !IsVaultGenerationHandleCreated(in _masterSimulationJobHandlesHandle) &&
                !IsVaultGenerationHandleCreated(in _masterDependencyScratchHandlesHandle) &&
                !IsVaultGenerationHandleCreated(in _masterJobDependencyTelemetryHandle) &&
                !IsVaultGenerationHandleCreated(in _masterPipelineTelemetryRingHandle) &&
                !IsVaultGenerationHandleCreated(in _masterPipelineTelemetryCursorHandle) &&
                !IsVaultGenerationHandleCreated(in _masterMockTimeDilationSignalsHandle) &&
                !IsVaultGenerationHandleCreated(in _masterPresentationSuppressionHandle) &&
                !IsVaultGenerationHandleCreated(in _masterDomainFenceHandlesHandle) &&
                !IsVaultGenerationHandleCreated(in _masterFenceTelemetryRingHandle) &&
                !IsVaultGenerationHandleCreated(in _masterFenceTelemetryCursorHandle))
            {
                return;
            }

            DisposeDispatcherSurfaceProbeBuffers(dataVault);
            DisposeDispatcherBlackBox(dataVault);
            DisposeH8TimeArray(dataVault);
            DisposeMasterDispatcherRuntime(dataVault);
        }

        private void RefreshSimulationBucketerDependency()
        {
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
        }

        private void RefreshJobAdmissionDependency()
        {
            IJobAdmissionService previousAdmission = JobAdmissionSchedulerBridge.Service;
            IJobAdmissionService jobAdmission = GlobalRegistry.JobAdmission;
            if (jobAdmission != null)
            {
                _jobAdmission = jobAdmission;
                JobAdmissionSchedulerBridge.SetService(jobAdmission);
            }
            else
            {
                _jobAdmission = null;
                if (previousAdmission != null)
                    JobAdmissionSchedulerBridge.ClearService(previousAdmission);
            }
        }

        /// <summary>
        /// Caches the input-determinism service, rejecting a service that is not initialized.
        ///
        /// WHY THE NULL CHECK WAS NOT ENOUGH, and this cost the project every movement intent for a whole
        /// session. This method has exactly ONE caller - InitializeService (:2167) - so it is a one-shot cold
        /// read. It ran while this dispatcher was a BootstrapPhase.CoreServices node and InputDispatcher was
        /// still a BootstrapPhase.Player node (GameBootstrapper phase mapping), so the registry's Input slot
        /// was empty. GlobalRegistry.InputDeterminism is `=> Input` (GlobalRegistry.cs:943), and Input
        /// substitutes the NON-NULL NoOpInputService null object for an empty slot, whose IsInitialized is a
        /// hardcoded false. Rejecting only null therefore latched the no-op permanently, the per-frame guard
        /// at the consumer was false forever, PreSimulationInputTick never ran, and every published input
        /// override went unconsumed while the log cheerfully reported inputServiceRegistered=True.
        ///
        /// The one recovery path never fired either: GlobalRegistry.Register queues a rebound only when the
        /// slot ALREADY held a service (GlobalRegistry.cs:7351-7353), and first registration fills an empty
        /// slot, so nobody was told.
        ///
        /// Commit 37438fa9c fixed the two LEAVES of this - the input dispatcher self-pumps its tick and
        /// HectonPlayerMovement rebinds off GlobalRegistry.RegisteredInput, the raw slot that never
        /// substitutes the null object. This is the ROOT: the cache no longer accepts a service that cannot
        /// work, so the field stays null and the self-heal below can succeed later.
        /// </summary>
        private void RefreshInputDeterminismDependency()
        {
            IInputDeterminismService inputDeterminism = GlobalRegistry.InputDeterminism;

            // IsInitialized is the null object's own tell: NoOpInputService hardcodes it false, and a real
            // service reports true once its own initialization completed. Caching only an initialized service
            // means a cold read that lands too early leaves the field null rather than poisoning it.
            if (inputDeterminism != null && inputDeterminism.IsInitialized)
                _inputDeterminism = inputDeterminism;
        }

        /// <summary>
        /// Re-resolves the input-determinism cache when it is empty or holds something that cannot tick.
        /// <para>
        /// HOT-PATH COST, because this runs once per dispatcher frame: one static property read - which is
        /// `=> Input`, itself a field read plus a null-substitution branch (GlobalRegistry.cs:943) - and one
        /// interface bool read, and ONLY when the cache is not already usable. Once the real service is
        /// registered this method is a single reference compare and a bool, forever. No allocation, no
        /// reflection, no lambda, no string. Registration order is a boot-time race that resolves within the
        /// first frames, so the branch is cold almost immediately.
        /// </para>
        /// <para>
        /// This exists rather than a registry callback because the registry does not offer one for a FIRST
        /// registration - see the Register gate cited above. Fixing that gate is the better repair and is
        /// queued; until it lands, a consumer that re-reads its own dependency is strictly better than one
        /// that trusts a cold read taken before the dependency existed.
        /// </para>
        /// </summary>
        private void EnsureInputDeterminismResolved()
        {
            IInputDeterminismService cached = _inputDeterminism;
            if (cached != null && cached.IsInitialized)
                return;

            RefreshInputDeterminismDependency();
        }

        private void RefreshPeripheralDependencies()
        {
            IVramBudgetReadModel vramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            if (vramMonitor != null)
                _vramMonitor = vramMonitor;

            IVramPressureSampleSink vramPressure = GlobalRegistry.VRAMPressureSampleSink;
            if (vramPressure != null)
                _vramPressure = vramPressure;

            IVramPressureReadModel vramPressureReadModel = GlobalRegistry.VRAMPressureReadModel;
            if (vramPressureReadModel != null)
                _vramPressureReadModel = vramPressureReadModel;

            IMacroDatabaseService macroDatabase = GlobalRegistry.MacroDatabase;
            if (macroDatabase != null)
                _macroDatabase = macroDatabase;

            IPhysicsService physics = GlobalRegistry.Physics;
            if (physics != null)
                _physics = physics;

            CacheObjectPoolService(null);

            ICameraJuiceSystem cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
                _cachedCameraJuiceSystem = cameraJuice;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault reboundVault = currentService as IDataVault;
                    if (!ReferenceEquals(_dataVault, reboundVault))
                        ReleaseSystemDispatcherVaultHandles(_dataVault);
                    _dataVault = reboundVault;
                    _cachedDispatcherDataVault = _dataVault;
                    if (_dataVault != null)
                        VaultSovereigntyTelemetry.EnsureRing(_dataVault);
                    JobSchedulingProfileCatalog.LoadColdBootProfiles(_dataVault);
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _inputDeterminism = currentService as IInputDeterminismService;
                    break;
                case GlobalRegistryServiceSlot.JobAdmissionRuntime:
                    IJobAdmissionService previousAdmission = JobAdmissionSchedulerBridge.Service;
                    _jobAdmission = currentService as IJobAdmissionService;
                    if (_jobAdmission != null)
                        JobAdmissionSchedulerBridge.SetService(_jobAdmission);
                    else if (previousAdmission != null)
                        JobAdmissionSchedulerBridge.ClearService(previousAdmission);
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _simulationBucketer = currentService as ISimulationBucketer;
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as IVramPressureSampleSink;
                    _vramPressureReadModel = currentService as IVramPressureReadModel;
                    break;
                case GlobalRegistryServiceSlot.MacroDatabase:
                    _macroDatabase = currentService as IMacroDatabaseService;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physics = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
            }
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault currentVault = currentService as IDataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                ReleaseSystemDispatcherVaultHandles(_dataVault ?? (previousService as IDataVault));

            _dataVault = currentVault;
            _cachedDispatcherDataVault = currentVault;
            if (currentVault != null)
            {
                VaultSovereigntyTelemetry.EnsureRing(currentVault);
                EnsureDispatcherSurfaceProbeBuffers();
                EnsureH8TimeArray();
                EnsureDispatcherBlackBox();
                EnsureMasterDispatcherNativeBuffers();
            }
        }

        private static IVramBudgetReadModel ResolveCachedVramMonitor()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return null;

            return dispatcher._vramMonitor;
        }

        private static IVramPressureSampleSink ResolveCachedVramPressure()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return null;

            return dispatcher._vramPressure;
        }

        private static IMacroDatabaseService ResolveCachedMacroDatabase()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return null;

            return dispatcher._macroDatabase;
        }

        private static IObjectPoolService ResolveCachedObjectPool()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return null;

            return dispatcher.TryResolveCachedObjectPool(out IObjectPoolService pool) ? pool : null;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                ThreadSafeCommandQueue.BindObjectPoolServiceCold(pool);
                return;
            }

            _objectPool = null;
            ThreadSafeCommandQueue.BindObjectPoolServiceCold(null);
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                ThreadSafeCommandQueue.BindObjectPoolServiceCold(resolved);
                pool = resolved;
                return true;
            }

            _objectPool = null;
            ThreadSafeCommandQueue.BindObjectPoolServiceCold(null);
            pool = null;
            return false;
        }

        private static IPhysicsService ResolveCachedPhysicsService()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher == null)
                return null;

            return dispatcher._physics;
        }

        private float RefreshScalabilityQualityWeight()
        {
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            return _globalQualityWeight01;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1f;
        }

        private static byte EncodeGlobalQualityWeightByte(float qualityWeight01)
        {
            float sanitized = math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 0f;
            return (byte)math.clamp((int)math.round(sanitized * byte.MaxValue), 0, byte.MaxValue);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private void RunPreSimulationMemoryDefrag(float unscaledDeltaTime)
        {
            // L19 hop2 LIVE: skip pre-sim memory defrag - PublishDataVaultDefragTelemetry
            // -> SignalBus.MemoryPressureSignal EnsureInitialized/TryAcquireFrameSnapshotBuffer
            // mono AV under headless batch after STARTERGRANT.
            if (Application.isBatchMode)
                return;

            IDataVault dataVault = _dataVault;
            if (dataVault == null || unscaledDeltaTime < 0f)
                return;

            bool forcedByMemoryPressure = Interlocked.Exchange(ref _criticalMemoryPressureDefragRequested, 0) != 0;
            float qualityWeight01 = RefreshScalabilityQualityWeight();
            float qualityCurve01 = SmoothStep01(qualityWeight01);
            double cadenceSeconds = math.lerp(
                (float)ColdTickIntervalSeconds,
                (float)FrostTickIntervalSeconds,
                qualityCurve01);
            _memoryDefragAccumulator += unscaledDeltaTime;
            if (!forcedByMemoryPressure && _memoryDefragAccumulator < cadenceSeconds)
                return;

            float elapsedSeconds = (float)(_memoryDefragAccumulator > 0d ? _memoryDefragAccumulator : cadenceSeconds);
            _memoryDefragAccumulator = 0d;
            float compactionStress01 = ResolveMemoryCompactionStress01(unscaledDeltaTime);
            uint activeBurstLockMask = dataVault.ActiveBurstLockMask;
            uint telemetryFrameId = ResolveMemoryTelemetryFrameId();
            H8Memory.SetTelemetryFrameId(telemetryFrameId);
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            VaultSovereigntyMaintenanceStats sovereigntyStats = default;
            TryPollVaultMemoryProfileCsv(dataVault);
            using (_memoryDefragProfilerMarker.Auto())
            {
                dataVault.FrostTickDefrag(
                    elapsedSeconds,
                    compactionStress01,
                    MemoryDefragPhase.PreSimulation,
                    activeBurstLockMask);
                sovereigntyStats = VaultSovereigntyMaintenance.RunPreSimulationFrost(
                    dataVault,
                    HomeostasisBrain.GlobalQualityWeight,
                    telemetryFrameId);
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            PublishMemoryAddressShiftSignals(dataVault);
            RecordVaultSovereigntyTelemetry(dataVault, elapsedMilliseconds, in sovereigntyStats, telemetryFrameId);
            PublishDataVaultDefragTelemetry(dataVault, elapsedMilliseconds, telemetryFrameId);
            EmitVramPressureDefragSignalIfNeeded();
        }

        private void TryPollVaultMemoryProfileCsv(IDataVault dataVault)
        {
#if UNITY_EDITOR
            if (dataVault == null || dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)
                return;

            VaultLegacyBinaryArchaeology.TryPollMemoryOverridesCsv(
                dataVault,
                VaultMemoryProfileCsvPath,
                ref _vaultMemoryProfileCsvLastWriteTicks);
#endif
        }

        private void RecordVaultSovereigntyTelemetry(
            IDataVault dataVault,
            double elapsedMilliseconds,
            in VaultSovereigntyMaintenanceStats sovereigntyStats,
            uint frameId = 0u)
        {
            if (dataVault == null)
                return;

            int generationMissCount = dataVault.GenerationHandleMissCount;
            int generationMissDelta = math.max(0, generationMissCount - _lastVaultGenerationMissCount);
            _lastVaultGenerationMissCount = generationMissCount;

            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight)
                ? HomeostasisBrain.GlobalQualityWeight
                : 1f);
            int stride = ResolveVaultTelemetryStride(quality);
            float defragUs = (float)math.max(0.0d, elapsedMilliseconds * 1000.0d);
            float maxJobUs = math.max(defragUs, sovereigntyStats.MaxJobUs);
            if (maxJobUs > 0f)
                _lastVaultMemoryJobUs = maxJobUs;
            uint flags = (uint)dataVault.LastDefragFlags | sovereigntyStats.Flags;
            if (flags != 0u)
                _lastVaultMaintenanceFlags = flags;
            VaultSovereigntyTelemetry.TryRecord(
                dataVault,
                frameId != 0u ? frameId : ResolveMemoryTelemetryFrameId(),
                generationMissDelta,
                stride,
                maxJobUs > 0f ? maxJobUs : _lastVaultMemoryJobUs,
                quality,
                VaultSovereigntyMaintenance.SourceHash,
                flags != 0u ? flags : _lastVaultMaintenanceFlags);
        }

        private static int ResolveVaultTelemetryStride(float quality)
        {
            float curved = quality * quality * (3f - (2f * quality));
            return math.clamp((int)math.round(math.lerp(4f, 1f, curved)), 1, 4);
        }

        private uint ResolveMemoryTelemetryFrameId()
        {
            uint frameId = TimeSliceScheduler.CurrentFrameId;
            if (frameId != 0u)
                return frameId;

            unchecked
            {
                _memoryTelemetrySequence++;
            }

            if (_memoryTelemetrySequence == 0u)
                _memoryTelemetrySequence = 1u;
            return _memoryTelemetrySequence;
        }

        private void RecordVaultSovereigntyPostSimulationHeartbeat()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null && TryResolveCachedDataVault(out dataVault))
                _dataVault = dataVault;
            if (dataVault == null)
                return;

            VaultSovereigntyMaintenanceStats stats = default;
            stats.MaxJobUs = _lastVaultMemoryJobUs;
            stats.Flags = _lastVaultMaintenanceFlags;
            RecordVaultSovereigntyTelemetry(dataVault, 0.0d, in stats, ResolveMemoryTelemetryFrameId());
        }

        private void RecordMemoryBlackBoxHeartbeat()
        {
            H8Memory.SetTelemetryFrameId(TimeSliceScheduler.CurrentFrameId);
            H8Memory.RecordHeartbeat();
            IDataVault dataVault = _dataVault;
            dataVault?.RecordHeartbeat();
        }

        private void RecordDispatcherBlackBoxHeartbeat(IDataVault dataVault)
        {
            if (dataVault == null)
                return;

            if (!TryResolveDispatcherVaultBuffer(
                    dataVault,
                    in _dispatcherBlackBoxHandle,
                    DispatcherBlackBoxFrameCount,
                    out NativeArray<DispatcherBlackBoxEntry> ring) ||
                !TryResolveDispatcherVaultBuffer(
                    dataVault,
                    in _dispatcherBlackBoxCursorHandle,
                    1,
                    out NativeArray<int> cursorBuffer))
            {
                return;
            }

            int cursor = cursorBuffer[0];
            if ((uint)cursor >= (uint)DispatcherBlackBoxFrameCount)
                cursor = 0;

            uint dispatcherFrameId = ResolveCurrentDispatcherFrameId();
            bool nonFinite =
                !math.isfinite(CurrentFrameDeltaTime) ||
                !math.isfinite(CurrentFrameUnscaledDeltaTime) ||
                !math.isfinite(_timeDilationScalar) ||
                !math.isfinite(CurrentFixedInterpolationAlpha) ||
                !IsFiniteDouble(_timeSnapshot.Time) ||
                !IsFiniteDouble(_timeSnapshot.UnscaledTime);
            ushort flags = EncodeDispatcherQualityWeightQ8(_globalQualityWeight01);
            if (SimulationPaused)
                flags |= DispatcherBlackBoxFlagPaused;
            if (_aupPreShiftPauseFrameId == dispatcherFrameId)
                flags |= DispatcherBlackBoxFlagAupBarrier;
            if (IsOriginShiftBootstrapLocked || IsOriginShiftFrameLockedForCurrentFrame)
                flags |= DispatcherBlackBoxFlagOriginShiftLock;
            if (nonFinite)
                flags |= DispatcherBlackBoxFlagNonFinite;
            if (_coreTickDilationFramesRemaining > 0 || _coreTickDilationRestorePending)
                flags |= DispatcherBlackBoxFlagCoreDilation;
            if (_temporalCompressionActive)
                flags |= DispatcherBlackBoxFlagTemporalCompression;
            if (_adrenalineDilationPhase != AdrenalineDilationPhaseNone)
                flags |= DispatcherBlackBoxFlagAdrenalineDilation;

            DispatcherBlackBoxEntry entry = default;
            entry.Frame = dispatcherFrameId;
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
            entry.PendingSurfaceProbes = unchecked((ushort)math.min(ushort.MaxValue, math.max(0, _pendingDispatcherSurfaceProbeCount)));
            entry.ScheduledSurfaceProbes = unchecked((ushort)math.min(ushort.MaxValue, math.max(0, _scheduledDispatcherSurfaceProbeCount)));
            entry.HomeostasisPressureLevel = HomeostasisPressureLevel;
            entry.HomeostasisFoveatedTier = HomeostasisFoveatedTier;
            entry.AupPreShiftSequence = _aupPreShiftPauseSequence;
            entry.StateHash = ResolveDispatcherStateHash(flags, dispatcherFrameId);
            entry.KillSwitchMask = KillSwitchMask;
            ring[cursor] = entry;

            cursor++;
            if (cursor >= DispatcherBlackBoxFrameCount)
                cursor = 0;
            cursorBuffer[0] = cursor;

            if (nonFinite && !_dispatcherBlackBoxViolationPublished)
            {
                _dispatcherBlackBoxViolationPublished = true;
                PublishDispatcherComplianceViolation(_DispatcherBlackBoxFaultHash, _SystemDispatcherHash, 4, 1);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_DispatcherBlackBoxFaultHash));
            }
        }

        private uint ResolveDispatcherStateHash(ushort flags, uint dispatcherFrameId)
        {
            uint hash = 2166136261u;
            hash = unchecked((hash ^ dispatcherFrameId) * 16777619u);
            hash = unchecked((hash ^ (uint)_pendingDispatcherSurfaceProbeCount) * 16777619u);
            hash = unchecked((hash ^ (uint)_scheduledDispatcherSurfaceProbeCount) * 16777619u);
            hash = unchecked((hash ^ (uint)flags) * 16777619u);
            hash = unchecked((hash ^ (uint)HomeostasisPressureLevel) * 16777619u);
            hash = unchecked((hash ^ (uint)HomeostasisFoveatedTier) * 16777619u);
            return hash;
        }

        private static ushort EncodeDispatcherQualityWeightQ8(float qualityWeight01)
        {
            int encoded = math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
            return (ushort)(encoded << DispatcherBlackBoxQualityWeightQ8Shift);
        }

        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
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
            if (dataVault == null)
                return;

            int recordCount = dataVault.LastRelocationRecordCount;
            for (int i = 0; i < recordCount; i++)
            {
                if (!dataVault.TryGetLastRelocationRecord(i, out VaultRelocationRecord record))
                    break;

                MemoryAddressShiftSignal signal = default;
                signal.OldOffsetBytes = record.OldOffsetBytes;
                signal.NewOffsetBytes = record.NewOffsetBytes;
                signal.BufferId = record.BufferId;
                signal.ByteLength = record.ByteLength;
                signal.Version = record.Generation;
                signal.Flags = record.Flags;
                signal.SystemId = record.SystemId;
                SignalBus<MemoryAddressShiftSignal>.TryPushTracked(in signal, ref s_x001SystemDispatcherSignalPushDropCount);
            }

            PublishVaultSovereigntyAddressShiftRecords(dataVault);
        }

        private static void PublishVaultSovereigntyAddressShiftRecords(IDataVault dataVault)
        {
            if (!TryResolveExistingDispatcherVaultBuffer(
                    dataVault,
                    BufferID.VaultMemoryAddressShiftCount,
                    SystemID.CoreDataVault,
                    1,
                    out NativeArray<int> shiftCount) ||
                !shiftCount.IsCreated ||
                shiftCount.Length == 0 ||
                !TryReadExistingDispatcherVaultBuffer(
                    dataVault,
                    BufferID.VaultMemoryAddressShiftRecords,
                    SystemID.CoreDataVault,
                    1,
                    out NativeArray<VaultMemoryAddressShiftRecord>.ReadOnly records))
            {
                return;
            }

            int count = math.clamp(shiftCount[0], 0, records.Length);
            for (int i = 0; i < count; i++)
            {
                VaultMemoryAddressShiftRecord record = records[i];
                if (record.BufferId == 0 || record.ByteLength <= 0)
                    continue;

                MemoryAddressShiftSignal signal = default;
                signal.OldOffsetBytes = record.OldOffsetBytes;
                signal.NewOffsetBytes = record.NewOffsetBytes;
                signal.BufferId = record.BufferId;
                signal.ByteLength = record.ByteLength;
                signal.Version = record.Version;
                signal.Flags = record.Flags;
                signal.SystemId = record.SystemId;
                signal.OldIndex = record.OldIndex;
                signal.NewIndex = record.NewIndex;
                signal.MovedEntityId = record.MovedEntityId;
                signal.SourceFrame = record.SourceFrame;
                signal.SourceHash = record.SourceHash;
                signal.CompactedCount = record.CompactedCount;
                SignalBus<MemoryAddressShiftSignal>.TryPushTracked(in signal, ref s_x001SystemDispatcherSignalPushDropCount);
            }

            shiftCount[0] = 0;
        }

        private void PublishDataVaultDefragTelemetry(IDataVault dataVault, double elapsedMilliseconds, uint frameId)
        {
            // L19 hop2 LIVE: skip PublishDataVaultDefragTelemetry under batch.
            if (Application.isBatchMode)
                return;

            if (frameId == 0u)
                frameId = ResolveMemoryTelemetryFrameId();

            float vaultPressure01 = dataVault.CapacityPressure01;
            if (vaultPressure01 >= 0.8f)
            {
                MemoryPressureSignal pressureSignal = default;
                pressureSignal.ReservedMemoryBytes = dataVault.AllocatedBytes;
                pressureSignal.PhysicalMemoryBytes = dataVault.ArenaBytes;
                pressureSignal.UsageRatio = vaultPressure01;
                pressureSignal.Frame = frameId;
                pressureSignal.Severity = vaultPressure01 >= 0.95f ? (byte)2 : (byte)1;
                pressureSignal.Flags = 2;
                SignalBus<MemoryPressureSignal>.TryPushTracked(in pressureSignal, ref s_x001SystemDispatcherSignalPushDropCount);
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
                    GlobalTelemetryBus.ComputeContextHash(nameof(GlobalDataVault)),
                    GlobalTelemetryBus.ComputeContextHash(nameof(RunPreSimulationMemoryDefrag)),
                    (float)elapsedMilliseconds);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultWatchdogHash,
                    _DataVaultDefragContextHash,
                    (float)elapsedMilliseconds);
            }

            if (dataVault.PendingMassiveMoveBytes >= 50L * 1024L * 1024L &&
                _lastMemoryDefragPressureWarningFrame != frameId)
            {
                _lastMemoryDefragPressureWarningFrame = frameId;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultMassiveMoveHash,
                    _DataVaultDefragContextHash,
                    dataVault.PendingMassiveMoveBytes * GlobalTelemetryBus.BytesToMegabytes);
            }
        }

        private static void EmitVramPressureDefragSignalIfNeeded()
        {
            IVramBudgetReadModel monitor = ResolveCachedVramMonitor();
            HardwareTierDetector.EnsureInitialized();
            long vramPressureCeilingBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
            if (vramPressureCeilingBytes <= 0L)
                vramPressureCeilingBytes = 1800L * 1024L * 1024L;

            if (monitor == null || monitor.TotalVRAMBytes <= vramPressureCeilingBytes)
                return;

            GlobalTelemetryBus.PublishVRAMWarningEvent(monitor.TotalVRAMBytes);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DataVaultVramPressureHash,
                _DataVaultDefragContextHash,
                monitor.TotalVRAMBytes * GlobalTelemetryBus.BytesToMegabytes);

            IVramPressureSampleSink pressureMonitor = ResolveCachedVramPressure();
            if (pressureMonitor != null)
                pressureMonitor.ForceImmediateSampleAndResponse();
        }

        private void DisposeH8TimeArray()
        {
            DisposeH8TimeArray(_dataVault);
        }

        private void DisposeH8TimeArray(IDataVault dataVault)
        {
            ReleaseDispatcherVaultHandle(dataVault, ref _h8TimeHandle);
        }

        private bool EnsureDispatcherBlackBox()
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            if (TryEnsureDispatcherVaultBuffer(
                    dataVault,
                    ref _dispatcherBlackBoxHandle,
                    BufferID.SystemDispatcherBlackBox,
                    DispatcherBlackBoxFrameCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<DispatcherBlackBoxEntry> _) &&
                TryEnsureDispatcherVaultBuffer(
                    dataVault,
                    ref _dispatcherBlackBoxCursorHandle,
                    BufferID.SystemDispatcherBlackBoxCursor,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> _))
            {
                return true;
            }

            ReleaseDispatcherVaultHandle(dataVault, ref _dispatcherBlackBoxHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _dispatcherBlackBoxCursorHandle);
            return false;
        }

        private void DisposeDispatcherBlackBox()
        {
            DisposeDispatcherBlackBox(_dataVault);
        }

        private void DisposeDispatcherBlackBox(IDataVault dataVault)
        {
            ReleaseDispatcherVaultHandle(dataVault, ref _dispatcherBlackBoxHandle);
            ReleaseDispatcherVaultHandle(dataVault, ref _dispatcherBlackBoxCursorHandle);
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

            // H8TimeSlot.UnscaledTime is the clock every system reads through the vault, and it is also what
            // CurrentUnscaledTimeSeconds returns. Under step-bounded time it must be step count times step
            // size, not wall clock, or the dt would be deterministic while the absolute clock derived from
            // it was not.
            double unscaledTime = _headlessTimeMode == HeadlessTimeMode.StepBounded
                ? _stepBoundedElapsedSeconds
                : UnityEngine.Time.unscaledTimeAsDouble;
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

        private float ConsumeFrameTimeDilationScalar()
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
            AdvanceDispatcherFrameId();
            FrameTimeWatchdog.TickMathPrecisionTransition(CurrentFrameIndex);
#if UNITY_EDITOR
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
                HectonXRRuntimeState.RefreshFrameState(CurrentFrameIndex);
                float unscaledDeltaTime = ResolveDispatcherUnscaledDeltaTime();
                if (!math.isfinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
                    unscaledDeltaTime = 0f;
                bool previousFrameMissedBudget = CurrentFrameUnscaledDeltaTime > JobAdmissionFrameBudgetMissThresholdSeconds;
                CurrentFrameUnscaledDeltaTime = unscaledDeltaTime;
                HomeostasisBrain.PreSimulationTick(unscaledDeltaTime);

                // Self-heal before reading. The cold resolve in InitializeService runs while the registry's
                // Input slot is still empty, and first registration notifies nobody, so without this the
                // guard below is false for the entire session and the input tick never runs at all.
                EnsureInputDeterminismResolved();
                IInputDeterminismService inputDeterminism = _inputDeterminism;
                if (inputDeterminism != null && inputDeterminism.IsInitialized)
                    inputDeterminism.PreSimulationInputTick(unscaledDeltaTime);
                SignalCorridorRuntime.PreSimulationHeartbeat();
                if (SignalBusRegistry.IsSimulationHalted)
                    return;

                float globalQualityWeight01 = RefreshScalabilityQualityWeight();
                RecordMemoryBlackBoxHeartbeat();
                RunPreSimulationMemoryDefrag(unscaledDeltaTime);
                IJobAdmissionService jobAdmission = _jobAdmission;
                byte globalQualityWeightByte = EncodeGlobalQualityWeightByte(globalQualityWeight01);
                if (jobAdmission != null && jobAdmission.IsInitialized)
                {
                    jobAdmission.Refill(
                        globalQualityWeightByte,
                        unscaledDeltaTime,
                        previousFrameMissedBudget);
                }
                Hecton8.Modding.ModCommandDispatcher.DrainPreSimulation();
                DrainSimulationPauseSignals();
                DrainAdrenalineDilationSignals(unscaledDeltaTime);
                float frameDilationScalar = ConsumeFrameTimeDilationScalar();
                if (!math.isfinite(frameDilationScalar) || frameDilationScalar < 0f)
                    frameDilationScalar = 0f;

                float deltaTime = unscaledDeltaTime * frameDilationScalar;
                if (!math.isfinite(deltaTime) || deltaTime < 0f)
                    deltaTime = 0f;
                CurrentFrameDeltaTime = deltaTime;
                DispatcherTimingDTO masterTiming = BuildMasterDispatcherTiming(deltaTime, unscaledDeltaTime);
                uint dispatcherFrameId = masterTiming.FrameId;
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
                bool aupBarrierActive = IsOriginShiftBootstrapLocked ||
                                        IsOriginShiftFrameLockedForCurrentFrame ||
                                        _aupPreShiftPauseFrameId == dispatcherFrameId;
                if (simulationBucketer != null && simulationBucketer.IsInitialized)
                {
                    simulationBucketer.ReportPreSimulationCostMs(preSimulationCostMs);
                    simulationBucketer.AdvanceFrame(
                        globalQualityWeightByte,
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
                {
                    // L19 hop2 fix: drive fixed accumulator with real dt so HPM.FixedTick
                    // (hop2 / GetState) executes during AUP origin-shift frames.
                    // dt=0f caused an early-return inside RunFixedStepAccumulator — use real dt.
                    // blockGameplayLanes=false: Player lane must run even during shift.
                    RunFixedStepAccumulator(CurrentFrameDeltaTime, blockGameplayLanes: false);
                    return;
                }


                if (_aupPreShiftPauseFrameId == dispatcherFrameId)
                {
                    RunUnscaledFastTick(unscaledDeltaTime, blockGameplayLanes: false);
                    return;
                }


                masterTiming = BuildMasterDispatcherTiming(CurrentFrameDeltaTime, CurrentFrameUnscaledDeltaTime);
                RunMasterSimulationPhase(in masterTiming);
#if UNITY_EDITOR
                long beginDispatcherTimestamp = BeginDispatcherPhaseTiming();
#endif
                _foveatedSimulationManager.BeginDispatcherFrame(deltaTime);
#if UNITY_EDITOR
                EndDispatcherPhaseTiming(beginDispatcherTimestamp, "FoveatedSimulationManager.BeginDispatcherFrame");
#endif
                PredatorCognitionDomain.BeginDispatcherFrame(CurrentFrameIndex);
                bool blockGameplayLanes = _runtimeGameplayBootstrapGateActive &&
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
#if UNITY_EDITOR
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUpdatable));
#endif
                    using (_updateLaneProfilerMarkers[laneIndex].Auto())
                    {
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            IUpdatable updatable = lane.GetAt(itemIndex);
                            if (!_foveatedSimulationManager.TryAdvanceTick(updatable, deltaTime, out float effectiveDeltaTime))
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
                PredatorCognitionDomain.ScheduleFrameEvaluation(CurrentFrameIndex);
                _foveatedSimulationManager.ScheduleFrameJobs();
                RunFastTick(deltaTime, blockGameplayLanes);
                RunUnscaledFastTick(CurrentFrameUnscaledDeltaTime, blockGameplayLanes: false);
                RunFixedStepAccumulator(deltaTime, blockGameplayLanes);
                RunBucketedSlowTick(blockGameplayLanes);
                RunSlowTick(deltaTime, blockGameplayLanes);
                RunColdTick(deltaTime, blockGameplayLanes);
                RunFrostTick(deltaTime, blockGameplayLanes);
                ScheduleDispatcherSurfaceProbes();
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
            _pendingSimulationBucketInterpolationAlpha = alpha;
            _hasPendingSimulationBucketInterpolationAlpha = true;

            SimulationBucketSyncSignal signal = default;
            signal.InterpolationAlpha = alpha;
            signal.Frame = unchecked((uint)math.max(0, frameState.CurrentFrameCount));
            signal.ActiveSlowBucket = frameState.ActiveSlowBucket;
            signal.SlowBucketMask = frameState.SlowBucketMask;
            signal.RebalanceSequence = frameState.RebalanceSequence;
            signal.ActiveSlowBucketCount = frameState.ActiveSlowBucketCount;
            signal.Flags = unchecked((byte)math.min(byte.MaxValue, frameState.FramePacingFlags));
            SignalBus<SimulationBucketSyncSignal>.TryPushTracked(in signal, ref s_x001SystemDispatcherSignalPushDropCount);
        }

        private static void FlushSimulationBucketVisualSync()
        {
            if (!_hasPendingSimulationBucketInterpolationAlpha)
                return;

            _hasPendingSimulationBucketInterpolationAlpha = false;
            Shader.SetGlobalFloat(_SimulationBucketInterpolationAlphaId, _pendingSimulationBucketInterpolationAlpha);
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

            int currentFrame = CurrentFrameIndex;
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
            SignalBus<FramePacingWarningSignal>.TryPushTracked(in warning, ref s_x001SystemDispatcherSignalPushDropCount);
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
            SignalBusRegistry.SetSystemKillSwitchBits(_Lane4VfxKillSwitchMask, true, _FramePacingWarningHash);
        }

        private static float ResolveCurrentFrameMilliseconds()
        {
            // Not just telemetry: this value feeds FramePacingWarningSignal.CurrentFrameMs and
            // ResolveFramePacingSeverity, which is published on a SignalBus that consumers act on. Reading
            // the wall clock here would put machine load back into published simulation state and undo the
            // determinism the step-bounded dt buys.
            if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                return _stepBoundedDeltaSeconds * 1000f;

            float deltaTime = UnityEngine.Time.unscaledDeltaTime;
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

            float now = UnityEngine.Time.unscaledTime;
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
            return _cachedCameraJuiceSystem;
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
#if UNITY_EDITOR
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherLateFrame);
            long completeDispatcherTimestamp = 0L;
            bool dispatcherPhaseTimingStarted = false;
#endif
            // L17: parity with RunDispatcherUpdate — TryFlush before bootstrap-lock hard-return.
            // LateFrame previously returned without draining FO; when SceneRebaseTickLock stuck,
            // InputDispatcher lateFrameTick/pumpFired froze while PreSim still advanced (L16 LIVE:
            // lateFrameTick=49 pumpFired=1 sticky). External TryFlush is the designed drain path
            // (FO.Tick itself is on master lanes blocked by the same lock).
            if (IsOriginShiftBootstrapLocked)
            {
                if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())
                    return;

                if (IsOriginShiftBootstrapLocked)
                    return;
            }
            if (IsOriginShiftFrameLockedForCurrentFrame)
            {
                _dataVault?.UnlockAllocationsAfterAupShift(_aupPreShiftPauseSequence);
                // L18 SURGICAL FIX: Run ILateFrameTickables to prevent InputDispatcher from freezing
                // while keeping Origin Shift safe by skipping visual syncs and job fences.
                SetActiveLateFrameEventLane(_LateFrameTickablesQueueHash);
                using (_lateFrameProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        RegistryBucket<ILateFrameTickable> lane = _lateFramePriorityLanes[laneIndex];
#if UNITY_EDITOR
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ILateFrameTickable));
#endif
                        int count = lane.Count;
                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            lane.GetAt(itemIndex).LateFrameTick();
                    }
                }
                return;
            }

            try
            {
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                CompleteDispatcherSurfaceProbes();
                UpdatePauseFreezeFrameDitherState();
                UpdateVisualStaticGlitchState();
                TickPauseDepthOfField(UnityEngine.Time.unscaledTime);
#if UNITY_EDITOR
                completeDispatcherTimestamp = BeginDispatcherPhaseTiming();
                dispatcherPhaseTimingStarted = true;
#endif
                CompleteFoveatedFrameJobs();
                _foveatedSimulationManager.VisualSyncTick();
                FlushSimulationBucketVisualSync();
                HectonXRRuntimeState.FlushVisualSyncShaderState();
                WfcLaserCutRuntime.FlushVisualSync();
                HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync();
                GlobalRegistry.FlushMathPrecisionShaderState();
                DistanceMath.FlushVisualSyncShaderState();
                ConnectionSplineBatchRenderer.FlushVisualSyncShaderState();
                HomeostasisBrain.FlushVisualSyncShaderState();
                RunMasterVisualSyncPhase();
                BeginLateFrameEventBudget();
                SetActiveLateFrameEventLane(_LateFrameTickablesQueueHash);
                using (_lateFrameProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        RegistryBucket<ILateFrameTickable> lane = _lateFramePriorityLanes[laneIndex];
#if UNITY_EDITOR
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ILateFrameTickable));
#endif
                        int count = lane.Count;
                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            lane.GetAt(itemIndex).LateFrameTick();
                    }
                }
                PredatorCognitionDomain.LateFrameTick();
                CombatDamageRuntime.LateFrameTick();
                VoxelDynamicNavGridRuntime.CompletePendingDynamicObstacleUpdates();
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
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
                    GlobalTelemetryBus.LateFrameUpdate(UnityEngine.Time.unscaledTime);
                    WorldSpatialHashGrid.LateFrameMaintenance(CurrentFrameIndex);
                }
                finally
                {
                    NativeArenaAllocator.Reset();

#if UNITY_EDITOR
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
                    ResolvePhysicsLateFramePendingCount() +
                    ElectrolysisAcousticEvents.PendingCount +
                    AudioCaptionEvents.PendingCount +
                    SpectrumEvents.PendingCount +
                    ProceduralAudioEvents.PendingCount +
                    MapMagicBiomeEvents.PendingCount +
                    MapMagicTerrainTileEvents.PendingCount +
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
                FlushPhysicsLateFrameEvents();

                if (ShouldDropAmbientLateFrameEvents(_EnvironmentEventsArteryHash))
                {
                    DropAmbientEnvironmentEvents();
                    return;
                }

                CelestialEvents.FlushPending();
                EclipseGameplayEvents.FlushPending();
                ElectrolysisAcousticEvents.FlushPending();
                AudioCaptionEvents.FlushPending();
                SpectrumEvents.FlushPending();
                ProceduralAudioEvents.FlushPending();
                MapMagicBiomeEvents.FlushPending();
                MapMagicTerrainTileEvents.FlushPending();
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

        private static int ResolvePhysicsLateFramePendingCount()
        {
            IPhysicsService physics = ResolveCachedPhysicsService();
            return physics != null ? physics.PendingLateFrameEventCount : 0;
        }

        private static void FlushPhysicsLateFrameEvents()
        {
            IPhysicsService physics = ResolveCachedPhysicsService();
            physics?.FlushLateFrameEvents();
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
                    LaserCutterEvents.ReadPendingCount() +
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
            return TryReserveLateFrameEventDispatches(1);
        }

        /// <summary>
        /// Atomically reserves multiple deferred event dispatch slots from the current LateUpdate budget.
        /// </summary>
        /// <returns>True when all requested payloads may dispatch this frame.</returns>
        public static bool TryReserveLateFrameEventDispatches(int dispatchCount)
        {
            if (dispatchCount <= 0)
                return true;

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

            if (_lateFrameEventDispatchBudget >= dispatchCount)
            {
                _lateFrameEventDispatchBudget -= dispatchCount;
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
            SignalBus<MemoryPressureSignal>.TryPushTracked(in pressureSignal, ref s_x001SystemDispatcherSignalPushDropCount);
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

            IObjectPoolService objectPool = ResolveCachedObjectPool();
            if (objectPool != null)
                objectPool.FlushInactivePoolsForMemoryPressure();

            // Manual managed GC is forbidden in gameplay lanes; pressure response stays in pool trim and DataVault defrag routes.
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
            ObjectPoolDiagnostics.TryPublishDataBusDepth(queueHash, pendingCount);
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
            MapMagicTerrainTileEvents.DropPendingAmbient();
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
            float untilTime = UnityEngine.Time.unscaledTime + safeDuration;
            if (untilTime > _visualStaticGlitchUntilTime)
                _visualStaticGlitchUntilTime = untilTime;

            _visualStaticGlitchActive = true;
        }

        private static void UpdateVisualStaticGlitchState()
        {
            if (!_visualStaticGlitchActive)
                return;

            if (UnityEngine.Time.unscaledTime < _visualStaticGlitchUntilTime)
            {
                Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, CurrentFrameIndex & 1023);
                return;
            }

            _visualStaticGlitchActive = false;
            _visualStaticGlitchUntilTime = 0f;
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, 0f);
        }

        private static uint CaptureCriticalPerformanceStackHash(uint laneHash)
        {
            uint frameHash = unchecked((uint)CurrentFrameIndex * 747796405u);
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

                // Temporal compression is the fixed lane's version of the slow lane's discarded surplus:
                // the Math.Min below throws away requested-minus-max seconds of physics and fixed-tick
                // time, and only a frame COUNT was ever recorded for it - never the seconds lost. Under
                // step-bounded time this must not happen at all, so say so once and loudly. This sits
                // inside the already-rare compression branch, so it costs nothing on a normal frame.
                if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                {
                    ReportStepBoundedClamp(
                        StepBoundedClampLaneFixed,
                        FixedStepSeconds,
                        requestedAccumulatedTime - maxAccumulatedTime);
                }
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
#if UNITY_EDITOR
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFixedTickable));
#endif
                    using (_fixedLaneProfilerMarkers[laneIndex].Auto())
                    {
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            lane.GetAt(itemIndex).FixedTick(fixedDeltaTime);
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }

                using (_postFixedProfilerMarker.Auto())
                {
                    DispatcherJobFence.BeginPostFixedSwapWindow();
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
#if UNITY_EDITOR
                            lane.ValidateNoDestroyedEntriesDebug(nameof(IPostFixedTickable));
#endif
                            int count = lane.Count;
                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
#if UNITY_EDITOR
                                IPostFixedTickable postFixedTickable = lane.GetAt(itemIndex);
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
                                        _lastPostFixedGcFrame = CurrentFrameIndex;
                                        _lastPostFixedGcDelta = gen0After - gen0Before;
                                        _lastPostFixedGcLaneIndex = laneIndex;
                                        _lastPostFixedGcItemIndex = itemIndex;
                                    }

                                    _currentPostFixedGcOwner = null;
                                }
#else
                                lane.GetAt(itemIndex).PostFixedTick(fixedDeltaTime);
#endif
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                    finally
                    {
                        DispatcherJobFence.EndPostFixedSwapWindow();
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

#if UNITY_EDITOR
            float now = UnityEngine.Time.unscaledTime;
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
#if UNITY_EDITOR
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IFastTickable));
#endif
                        using (_fastLaneProfilerMarkers[laneIndex].Auto())
                        {
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                lane.GetAt(itemIndex).FastTick((float)FastTickIntervalSeconds);
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _fastTickAccumulator >= FastTickIntervalSeconds)
            {
                // This branch is already the rare path, so the step-bounded check costs nothing on a normal
                // frame and nothing at all in a player build, where the mode is never enabled.
                if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                {
                    ReportStepBoundedClamp(
                        StepBoundedClampLaneFast,
                        FastTickIntervalSeconds,
                        _fastTickAccumulator - FastTickIntervalSeconds);
                }

                _fastTickAccumulator = FastTickIntervalSeconds;
            }
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
#if UNITY_EDITOR
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUnscaledFastTickable));
#endif
                    using (_unscaledFastLaneProfilerMarkers[laneIndex].Auto())
                    {
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            lane.GetAt(itemIndex).UnscaledFastTick((float)FastTickIntervalSeconds);
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _unscaledFastTickAccumulator >= FastTickIntervalSeconds)
            {
                if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                {
                    ReportStepBoundedClamp(
                        StepBoundedClampLaneUnscaledFast,
                        FastTickIntervalSeconds,
                        _unscaledFastTickAccumulator - FastTickIntervalSeconds);
                }

                _unscaledFastTickAccumulator = FastTickIntervalSeconds;
            }
        }

        private void RunSlowTick(float deltaTime, bool blockGameplayLanes)
        {
            // L19 hop2 LIVE: Debug.Log enter breadcrumbs FORBIDDEN under batch -
            // EditorMonoConsole.FlushLogEntries tlsf_memalign OOM/AV (Crash!!! at 4038 enters).
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
                    HectonXRRuntimeState.RefreshPlatformStateCold(CurrentFrameIndex);
                    HomeostasisBrain.RefreshCadenceSnapshotCold();
                    HectonXRRuntimeState.SlowTickHeadAupCache();

                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (SignalBusRegistry.IsSimulationHalted)
                            return;

                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<ISlowTickable> lane = _slowPriorityLanes[laneIndex];
#if UNITY_EDITOR
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                        using (_slowLaneProfilerMarkers[laneIndex].Auto())
                        {
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                ISlowTickable tickable = lane.GetAt(itemIndex);
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
            {
                // Anti-death-spiral clamp, and it must stay: trying to catch up on a frame that is already
                // late makes the next frame later still. But the discarded surplus is simulation time that
                // every ISlowTickable consumer silently never receives - EcosystemDirector,
                // ShinobuEcosystemBalancer, EcosystemPopulationBalancer, HectonSurfaceWeatherDirector,
                // ShinobuStormPropagationRuntime, WorldSpatialHashGrid, CombatDamageRuntime.
                //
                // Headless makes this the steady state rather than a rare hitch. Measured on
                // Logs/omega_route28.log: gameFramesPerWallSecond=0.75, so dt is roughly 1.33 s per frame,
                // which needs 13 substeps of the 0.1 s interval. Four run. The lane advances at about 30
                // percent of its rate, so a headless session does not merely run slower than a player
                // session - it runs a DIFFERENT simulation, which invalidates any comparison between them.
                //
                // Deliberately not "fixed" here, because the clamp is correct. Made VISIBLE here, because a
                // system that can collapse silently must fail loudly instead.
                //
                // The actual cure is not in this branch: it is EnableStepBoundedTime, which replaces the
                // wall-clock dt that makes this the steady state. Under a clamp-free step this branch never
                // runs. If it runs anyway while step-bounded time is active, the step size is too coarse and
                // ReportStepBoundedClamp says so once, loudly - a step-bounded run that still discards time
                // is repeatable but is NOT simulating what its step count implies.
                double discardedSeconds = _slowTickAccumulator - slowTickIntervalSeconds;
                _slowTickAccumulator = slowTickIntervalSeconds;
                if (discardedSeconds > 0.0)
                {
                    _slowTickDiscardedSeconds += discardedSeconds;
                    _slowTickDiscardEvents++;
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _SlowTickSurplusDiscardedHash,
                        _SystemDispatcherHash,
                        (float)discardedSeconds);
                    if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                        ReportStepBoundedClamp(StepBoundedClampLaneSlow, slowTickIntervalSeconds, discardedSeconds);
                }
            }
        }

        /// <summary>
        /// Total simulation seconds the slow-tick lane was owed and never received. A run reporting a large
        /// value here has not simulated what its wall clock suggests, and any ecosystem, weather or
        /// population state it produced is at a different point in time than the frame count implies.
        /// </summary>
        internal static double SlowTickDiscardedSeconds =>
            ActiveRuntimeInstance != null ? ActiveRuntimeInstance._slowTickDiscardedSeconds : 0.0;

        /// <summary>Frames in which slow-tick surplus was discarded.</summary>
        internal static int SlowTickDiscardEvents =>
            ActiveRuntimeInstance != null ? ActiveRuntimeInstance._slowTickDiscardEvents : 0;

        // ===========================================================================================
        // Step-bounded headless time
        //
        // The discard counters above made a headless run's divergence VISIBLE. They could not make it go
        // away, because the cause is not the clamp - the clamp is correct, and removing it trades a wrong
        // answer for a death spiral. The cause is that dt came from the wall clock, so a headless frame
        // was ~1.33 s (gameFramesPerWallSecond=0.75) where a player frame is ~0.016 s, and the surplus the
        // clamp threw away was the difference between two different simulations.
        //
        // This is the second time source. In StepBounded mode dt is a constant the caller chose, so:
        //   - same seed + same step count -> same state, by construction, not by machine load;
        //   - a step at or below MaxStepBoundedDeltaSeconds cannot clamp any lane, so nothing is silently
        //     discarded;
        //   - a step above it CAN still clamp, and that is reported loudly exactly once per lane instead
        //     of being absorbed - the original sin here was silence, not the clamp.
        //
        // Accessibility: SystemDispatcher is in Hecton8.Core, and Assets/_Project/Scripts/AssemblyInfo.cs
        // grants InternalsVisibleTo only to Hecton8.Editor, Hecton8.Plugins, Hecton8.SaveSystem.Editor and
        // Hecton8.SaveSystem.EditModeTests. So `internal` reaches a driver under
        // Assets/_Project/Scripts/Editor/** (assembly Hecton8.Editor) - the same assembly and the same
        // access level H8_HeadlessPlayModeProbe already uses to read SlowTickDiscardedSeconds - but does
        // NOT reach Assets/_Project/Editor/** (assembly Hecton8.Project.Editor), which is not on that
        // list. A driver there needs its own InternalsVisibleTo entry, not a widening to public.
        // ===========================================================================================

        /// <summary>Active dispatcher time source. <see cref="HeadlessTimeMode.WallClock"/> unless a driver opted in.</summary>
        internal static HeadlessTimeMode ActiveHeadlessTimeMode => _headlessTimeMode;

        /// <summary>True while the dispatcher advances by a caller-supplied fixed step instead of the wall clock.</summary>
        internal static bool IsStepBoundedTimeActive => _headlessTimeMode == HeadlessTimeMode.StepBounded;

        /// <summary>Fixed step in seconds the dispatcher advances by, or 0 when step-bounded time is off.</summary>
        internal static float StepBoundedDeltaSeconds =>
            _headlessTimeMode == HeadlessTimeMode.StepBounded ? _stepBoundedDeltaSeconds : 0f;

        /// <summary>
        /// Dispatcher frames advanced since step-bounded time was enabled. This, not a wall-clock duration,
        /// is what a determinism comparison must hold equal between two runs.
        /// </summary>
        internal static long StepBoundedStepIndex => _stepBoundedStepIndex;

        /// <summary>Simulated seconds elapsed under step-bounded time: step count times the fixed step.</summary>
        internal static double StepBoundedElapsedSeconds => _stepBoundedElapsedSeconds;

        /// <summary>
        /// Largest fixed step that discards no simulation time in any lane, in seconds. Currently 0.04 s,
        /// bounded by the FIXED-step lane's temporal compression - not by the slow lane (0.4 s) and not by
        /// the fast lane (0.0667 s). Sizing a headless step against either looser lane silently drops
        /// physics and fixed-tick time.
        /// </summary>
        internal static double MaxClampFreeStepSeconds => MaxStepBoundedDeltaSeconds;

        /// <summary>
        /// Bitmask of lanes that discarded simulation time while step-bounded time was active:
        /// bit0 fast, bit1 unscaled-fast, bit2 slow, bit3 cold, bit4 fixed-step temporal compression.
        /// NON-ZERO MEANS THE RUN IS NOT COMPARABLE to another run - a determinism harness should treat a
        /// non-zero mask as a failed run, not as a passing one, because the step size was too coarse.
        /// </summary>
        internal static byte StepBoundedClampedLaneMask => _stepBoundedClampReportedLanes;

        /// <summary>
        /// Switches the dispatcher onto a fixed step and resets the step clock. Call before pumping the
        /// player loop (<c>EditorApplication.QueuePlayerLoopUpdate</c>); the existing player-loop node then
        /// consumes exactly <paramref name="fixedDeltaSeconds"/> per pump no matter how long the pump took
        /// in wall time.
        /// </summary>
        /// <param name="fixedDeltaSeconds">
        /// Simulated seconds per dispatcher frame. Must be finite and positive. Use
        /// <see cref="MaxClampFreeStepSeconds"/> or smaller for a run whose state is comparable.
        /// </param>
        /// <returns>
        /// True when the step is clamp-free. False when it will still clamp a cadence lane - the mode is
        /// enabled either way, deliberately: silently leaving the run on the wall clock after the caller
        /// asked for determinism would be the same quiet divergence this mode exists to end.
        /// </returns>
        internal static bool EnableStepBoundedTime(float fixedDeltaSeconds)
        {
            if (!math.isfinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0f)
            {
                ReportStepBoundedConfigRejected(fixedDeltaSeconds);
                return false;
            }

            _headlessTimeMode = HeadlessTimeMode.StepBounded;
            _stepBoundedDeltaSeconds = fixedDeltaSeconds;
            _stepBoundedElapsedSeconds = 0d;
            _stepBoundedStepIndex = 0L;
            _stepBoundedClampReportedLanes = 0;
            if (fixedDeltaSeconds <= MaxStepBoundedDeltaSeconds)
                return true;

            ReportStepBoundedStepTooCoarse(fixedDeltaSeconds);
            return false;
        }

        /// <summary>
        /// Returns the dispatcher to the wall clock. Player behaviour is the default and does not need this.
        /// </summary>
        internal static void DisableStepBoundedTime()
        {
            _headlessTimeMode = HeadlessTimeMode.WallClock;
            _stepBoundedDeltaSeconds = 0f;
            _stepBoundedElapsedSeconds = 0d;
            _stepBoundedStepIndex = 0L;
            _stepBoundedClampReportedLanes = 0;
        }

        /// <summary>
        /// Consumes one fixed step. Called once per dispatcher frame from
        /// <see cref="ResolveDispatcherUnscaledDeltaTime"/>, never from a substep or per-item loop.
        /// </summary>
        private static float AdvanceStepBoundedClock()
        {
            float step = _stepBoundedDeltaSeconds;
            _stepBoundedElapsedSeconds += step;
            _stepBoundedStepIndex++;
            return step;
        }

        /// <summary>
        /// The dispatcher's single unscaled-delta source. In <see cref="HeadlessTimeMode.WallClock"/> this
        /// is the previous wall-clock read unchanged, XR smoothing included.
        /// </summary>
        private static float ResolveDispatcherUnscaledDeltaTime()
        {
            if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                return AdvanceStepBoundedClock();

            float measuredUnscaledDeltaTime = HectonXRRuntimeState.IsXRActive
                ? UnityEngine.Time.smoothDeltaTime
                : UnityEngine.Time.unscaledDeltaTime;
            return HectonXRRuntimeState.ResolveDispatcherDeltaTime(measuredUnscaledDeltaTime);
        }

        /// <summary>
        /// Reports a cadence lane clamping under step-bounded time. Once per lane per session: the message
        /// names the fix, so repeating it every frame would only bury it.
        /// </summary>
        private static void ReportStepBoundedClamp(byte laneBit, double intervalSeconds, double surplusSeconds)
        {
            if ((_stepBoundedClampReportedLanes & laneBit) != 0)
                return;

            _stepBoundedClampReportedLanes |= laneBit;
            PublishDispatcherComplianceViolation(_StepBoundedTimeClampHash, _SystemDispatcherHash, 4, laneBit);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _StepBoundedTimeClampHash,
                _SystemDispatcherHash,
                (float)surplusSeconds);
#if UNITY_EDITOR
            // Editor-guarded and latched to once per lane, so this is not a hot-path log and the
            // concatenation below is not a hot-path allocation. Headless batchmode is the editor, which is
            // exactly where this has to be readable. InvariantCulture because this host is Russian-locale
            // and a comma decimal separator would break the numeric greps that read these logs.
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            UnityEngine.Debug.LogError(
                "[SystemDispatcher] STEP-BOUNDED DETERMINISM BROKEN: lane mask 0x" +
                laneBit.ToString("X2", invariant) + " (bit0=fast bit1=unscaledFast bit2=slow bit3=cold " +
                "bit4=fixed) hit its substep cap on a " +
                intervalSeconds.ToString("F4", invariant) + " s interval and discarded " +
                surplusSeconds.ToString("F4", invariant) + " s of simulation time. Step is " +
                _stepBoundedDeltaSeconds.ToString("F4", invariant) + " s at step index " +
                _stepBoundedStepIndex.ToString(invariant) + ". This run's state is NOT comparable to " +
                "another run. Use a step of at most " + MaxStepBoundedDeltaSeconds.ToString("F4", invariant) +
                " s (SystemDispatcher.MaxClampFreeStepSeconds), and note that a time-dilation scalar above " +
                "1 multiplies the effective step and can reintroduce this.");
#endif
        }

        private static void ReportStepBoundedConfigRejected(float requestedDeltaSeconds)
        {
            PublishDispatcherComplianceViolation(_StepBoundedTimeConfigHash, _SystemDispatcherHash, 4, 1);
#if UNITY_EDITOR
            UnityEngine.Debug.LogError(
                "[SystemDispatcher] EnableStepBoundedTime rejected a step of " +
                requestedDeltaSeconds.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) +
                " s: it must be finite and positive. Time source left as " + _headlessTimeMode.ToString() +
                "; this run is NOT step-bounded.");
#endif
        }

        private static void ReportStepBoundedStepTooCoarse(float requestedDeltaSeconds)
        {
            PublishDispatcherComplianceViolation(_StepBoundedTimeConfigHash, _SystemDispatcherHash, 4, 2);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _StepBoundedTimeConfigHash,
                _SystemDispatcherHash,
                requestedDeltaSeconds);
#if UNITY_EDITOR
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            UnityEngine.Debug.LogError(
                "[SystemDispatcher] Step-bounded time ENABLED with a step of " +
                requestedDeltaSeconds.ToString("F4", invariant) + " s, which EXCEEDS the clamp-free maximum of " +
                MaxStepBoundedDeltaSeconds.ToString("F4", invariant) +
                " s. A lane will discard simulation time every frame, so this run is repeatable but still " +
                "does not simulate what its step count implies. The binding lane is the FIXED-step lane " +
                "(temporal compression above " + MaxClampFreeFixedStepSeconds.ToString("F4", invariant) +
                " s), NOT the 0.1 s slow lane and not the fast lane at " +
                MaxClampFreeCadenceStepSeconds.ToString("F4", invariant) + " s.");
#endif
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
#if UNITY_EDITOR
                    lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                    using (_slowLaneProfilerMarkers[laneIndex].Auto())
                    {
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            if (lane.GetAt(itemIndex) is IBucketedSlowTickable bucketedTickable &&
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
                bucketer.ActiveSlowBucketCount <= SimulationBucketConstants.MinimumActiveSlowBucketCount)
            {
                float survivalPressure01 = 1f - SmoothStep01(_globalQualityWeight01);
                double qualityIntervalSeconds = math.lerp(
                    (float)SlowTickIntervalSeconds,
                    (float)(SlowTickIntervalSeconds * 2.0),
                    survivalPressure01);
                return math.max(
                    _thermalCriticalSlowTickActive ? ThermalCriticalSlowTickIntervalSeconds : SlowTickIntervalSeconds,
                    qualityIntervalSeconds);
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
                    PredatorCognitionDomain.DrainPendingBlackBoxDumpsCold();

                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (SignalBusRegistry.IsSimulationHalted)
                            return;

                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<IColdTickable> lane = _coldPriorityLanes[laneIndex];
#if UNITY_EDITOR
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IColdTickable));
#endif
                        using (_coldLaneProfilerMarkers[laneIndex].Auto())
                        {
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                lane.GetAt(itemIndex).ColdTick();
                                if (SignalBusRegistry.IsSimulationHalted)
                                    return;
                            }
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _coldTickAccumulator >= ColdTickIntervalSeconds)
            {
                if (_headlessTimeMode == HeadlessTimeMode.StepBounded)
                {
                    ReportStepBoundedClamp(
                        StepBoundedClampLaneCold,
                        ColdTickIntervalSeconds,
                        _coldTickAccumulator - ColdTickIntervalSeconds);
                }

                _coldTickAccumulator = ColdTickIntervalSeconds;
            }
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
#if UNITY_EDITOR
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFrostTickable));
#endif
                    using (_frostLaneProfilerMarkers[laneIndex].Auto())
                    {
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            lane.GetAt(itemIndex).FrostTick();
                            if (SignalBusRegistry.IsSimulationHalted)
                                return;
                        }
                    }
                }
            }
        }

        private static void EnsureDispatcherSurfaceProbeBuffers()
        {
            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return;

            TryEnsureDispatcherVaultBuffer(
                dataVault,
                ref _scheduledDispatcherSurfaceProbeHitsHandle,
                BufferID.DispatcherRaycastHits,
                MaxQueuedDispatcherSurfaceProbes,
                NativeArrayOptions.ClearMemory,
                out NativeArray<KinematicSurfaceHit> _);
        }

        private static bool TryResolveDispatcherSurfaceProbeHits(out NativeArray<KinematicSurfaceHit> scheduledHits)
        {
            scheduledHits = default;
            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return false;

            return TryResolveDispatcherVaultBuffer(
                dataVault,
                in _scheduledDispatcherSurfaceProbeHitsHandle,
                MaxQueuedDispatcherSurfaceProbes,
                out scheduledHits);
        }

        private static bool TryLockDispatcherSurfaceProbeScheduledVaultBuffers()
        {
            if (_scheduledDispatcherSurfaceProbeHitsGuardHeld)
                return true;

            if (!IsVaultGenerationHandleCreated(in _scheduledDispatcherSurfaceProbeHitsHandle))
                return false;

            if (!TryResolveCachedDataVault(out IDataVault dataVault))
                return false;

            if (!dataVault.TryAcquireMutationGuard(DispatcherSurfaceProbeHitsGuardMask))
                return false;

            _scheduledDispatcherSurfaceProbeHitsGuardVault = dataVault;
            _scheduledDispatcherSurfaceProbeHitsGuardHeld = true;
            return true;
        }

        private static void UnlockDispatcherSurfaceProbeScheduledVaultBuffers()
        {
            TryResolveCachedDataVault(out IDataVault dataVault);
            UnlockDispatcherSurfaceProbeScheduledVaultBuffers(dataVault);
        }

        private static void UnlockDispatcherSurfaceProbeScheduledVaultBuffers(IDataVault dataVault)
        {
            if (!_scheduledDispatcherSurfaceProbeHitsGuardHeld)
                return;

            IDataVault guardVault = _scheduledDispatcherSurfaceProbeHitsGuardVault ?? dataVault;
            _scheduledDispatcherSurfaceProbeHitsGuardVault = null;
            _scheduledDispatcherSurfaceProbeHitsGuardHeld = false;

            if (guardVault != null)
                guardVault.ReleaseMutationGuard(DispatcherSurfaceProbeHitsGuardMask);
        }

        private static void ScheduleDispatcherSurfaceProbes()
        {
            if (_pendingDispatcherSurfaceProbeCount <= 0)
                return;

            using (_dispatcherSurfaceProbeScheduleProfilerMarker.Auto())
            {
                int pendingCount = _pendingDispatcherSurfaceProbeCount;
                for (int clearIndex = 0; clearIndex < pendingCount; clearIndex++)
                {
                    _pendingDispatcherSurfaceProbeReceivers[clearIndex] = null;
                    _pendingDispatcherSurfaceProbeRequestIds[clearIndex] = 0;
                }

                _pendingDispatcherSurfaceProbeCount = 0;
                _scheduledDispatcherSurfaceProbeCount = 0;
                _dispatcherSurfaceProbesScheduled = false;
            }
        }

        private static void CompleteDispatcherSurfaceProbes()
        {
            if (!_dispatcherSurfaceProbesScheduled)
                return;

            using (_dispatcherSurfaceProbeCompleteProfilerMarker.Auto())
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledDispatcherSurfaceProbeHandle))
                    return;

                _dispatcherSurfaceProbesScheduled = false;
                if (!TryResolveDispatcherSurfaceProbeHits(out NativeArray<KinematicSurfaceHit> scheduledHits))
                {
                    for (int i = 0; i < _scheduledDispatcherSurfaceProbeCount; i++)
                    {
                        _scheduledDispatcherSurfaceProbeReceivers[i] = null;
                        _scheduledDispatcherSurfaceProbeRequestIds[i] = 0;
                    }

                    _scheduledDispatcherSurfaceProbeCount = 0;
                    UnlockDispatcherSurfaceProbeScheduledVaultBuffers();
                    return;
                }

                int scheduledCount = _scheduledDispatcherSurfaceProbeCount;
                try
                {
                    for (int i = 0; i < scheduledCount; i++)
                    {
                        IDispatcherSurfaceProbeReceiver receiver = GetScheduledDispatcherSurfaceProbeReceiverAt(i);
                        if (receiver == null)
                            continue;

                        receiver.ConsumeDispatcherSurfaceHit(_scheduledDispatcherSurfaceProbeRequestIds[i], scheduledHits[i]);
                    }
                }
                finally
                {
                    for (int i = 0; i < scheduledCount; i++)
                    {
                        _scheduledDispatcherSurfaceProbeReceivers[i] = null;
                        _scheduledDispatcherSurfaceProbeRequestIds[i] = 0;
                    }

                    _scheduledDispatcherSurfaceProbeCount = 0;
                    UnlockDispatcherSurfaceProbeScheduledVaultBuffers();
                }
            }
        }

        private static void DisposeDispatcherSurfaceProbeBuffers()
        {
            TryResolveCachedDataVault(out IDataVault dataVault);
            DisposeDispatcherSurfaceProbeBuffers(dataVault);
        }

        private static void DisposeDispatcherSurfaceProbeBuffers(IDataVault dataVault)
        {
            if (_dispatcherSurfaceProbesScheduled)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _scheduledDispatcherSurfaceProbeHandle, forceComplete: true);
                    _dispatcherSurfaceProbesScheduled = false;
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                    UnlockDispatcherSurfaceProbeScheduledVaultBuffers(dataVault);
                }
            }
            else
            {
                _scheduledDispatcherSurfaceProbeHandle = default;
                UnlockDispatcherSurfaceProbeScheduledVaultBuffers(dataVault);
            }

            ReleaseDispatcherVaultHandle(dataVault, ref _scheduledDispatcherSurfaceProbeHitsHandle);

            _pendingDispatcherSurfaceProbeCount = 0;
            _scheduledDispatcherSurfaceProbeCount = 0;
            System.Array.Clear(_pendingDispatcherSurfaceProbeReceivers, 0, _pendingDispatcherSurfaceProbeReceivers.Length);
            System.Array.Clear(_pendingDispatcherSurfaceProbeRequestIds, 0, _pendingDispatcherSurfaceProbeRequestIds.Length);
            System.Array.Clear(_scheduledDispatcherSurfaceProbeReceivers, 0, _scheduledDispatcherSurfaceProbeReceivers.Length);
            System.Array.Clear(_scheduledDispatcherSurfaceProbeRequestIds, 0, _scheduledDispatcherSurfaceProbeRequestIds.Length);
        }

        private static bool ShouldSkipLaneDuringBootstrap(int laneIndex, bool blockGameplayLanes)
        {
            if (!blockGameplayLanes)
                return false;

            // L14: Player fixed/update locomotion is input-authoritative simulation, not optional
            // bootstrap garnish. Skipping PriorityLayer.Player while !BootstrapState.IsGameReady
            // starves HPM.FixedTick -> Sample -> GetState (hop2) even when InputDispatcher already
            // holds non-zero MoveDelta (L12/L13 LIVE: hop1 healthy, hop2 ABSENT, intent=0).
            // World/environment systems still run; Player lane must also run once registered so
            // scripted and human locomotion can sample the open input path during handoff.
            // (Previously: return laneIndex == GetLaneIndex(PriorityLayer.Player);)
            return false;
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
#if UNITY_EDITOR
            return System.Diagnostics.Stopwatch.GetTimestamp();
#else
            return 0L;
#endif
        }

        private static void EndDispatcherPhaseTiming(long startTimestamp, string phaseName)
        {
#if UNITY_EDITOR
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowDispatcherPhaseWarningMilliseconds)
            {
                float now = UnityEngine.Time.unscaledTime;
                if (now >= _nextDispatcherPhaseWarningLogTime)
                {
                    _nextDispatcherPhaseWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        GlobalTelemetryBus.ComputeContextHash(nameof(SystemDispatcher)),
                        GlobalTelemetryBus.ComputeContextHash(phaseName),
                        (float)elapsedMilliseconds);
                }
            }
#endif
        }

        private void CompleteFoveatedFrameJobs()
        {
#if UNITY_EDITOR
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_foveatedCompleteProfilerMarker.Auto())
            {
                _foveatedSimulationManager.TryCompleteFrameJobs();
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                float now = UnityEngine.Time.unscaledTime;
                if (now >= _nextFoveatedFrameWarningLogTime)
                {
                    _nextFoveatedFrameWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        GlobalTelemetryBus.ComputeContextHash("FoveatedSimulationManager"),
                        GlobalTelemetryBus.ComputeContextHash("LateFrameComplete"),
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
        private static int s_x001SystemDispatcherSignalPushDropCount;
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

            uint frame = SystemDispatcher.ReadPublishedDispatcherFrameId();
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            Vector3 up = cameraTransform.up;

            Hecton8.Core.Contracts.Signals.CameraPositionSignal positionSignal = default;
            positionSignal.Position = (float3)position;
            positionSignal.Frame = frame;
            positionSignal.Forward = (float3)forward;
            positionSignal.Flags = 1;
            SignalBus<Hecton8.Core.Contracts.Signals.CameraPositionSignal>.TryPushTracked(in positionSignal, ref s_x001SystemDispatcherSignalPushDropCount);

            Hecton8.Core.Contracts.Signals.CameraFrustumSignal frustumSignal = default;
            frustumSignal.Position = (float3)position;
            frustumSignal.Forward = (float3)forward;
            frustumSignal.Up = (float3)up;
            frustumSignal.FieldOfViewDegrees = camera.fieldOfView;
            frustumSignal.NearClipMeters = camera.nearClipPlane;
            frustumSignal.FarClipMeters = camera.farClipPlane;
            frustumSignal.Frame = frame;
            frustumSignal.Flags = 1;
            SignalBus<Hecton8.Core.Contracts.Signals.CameraFrustumSignal>.TryPushTracked(in frustumSignal, ref s_x001SystemDispatcherSignalPushDropCount);
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

        internal static void BindGIRelayCold(IGIRelaySystem giRelay)
        {
            RenderDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher != null)
                dispatcher._giRelay = giRelay;
        }

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
                return;

            int count = renderables.Count;
            if (count <= 0)
                return;

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            _pendingRenderSettingsSnapshot = RenderSettingsSnapshot.Capture();
            _pendingRenderSettingsCamera = camera;
            _hasPendingRenderSettingsRestore = true;
            GlobalRenderContext.SetCurrent(context, camera);

            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    IRenderable renderable = renderables.GetAt(i);
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

            _pendingRenderSettingsSnapshot.Restore(_giRelay);
            _pendingRenderSettingsSnapshot = default;
            _pendingRenderSettingsCamera = null;
            _hasPendingRenderSettingsRestore = false;
        }
    }


    public static class TimeSliceScheduler
    {
        private const float MinimumBudgetMs = 0.10f;
        private const float MiddleBudgetMs = 0.45f;
        private const float HighBudgetMs = 1.10f;
        private const float UltraBudgetMs = 2.00f;

        private static long _frameStartTimestamp;
        private static float _budgetMs;
        private static uint _frameId;

        public static float CurrentBudgetMs => _budgetMs;

        public static uint CurrentFrameId => _frameId;

        public static float ConsumedMs
        {
            get
            {
                if (_frameStartTimestamp == 0L)
                    return 0f;

                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _frameStartTimestamp;
                double elapsedMs = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (elapsedMs <= 0d)
                    return 0f;
                return elapsedMs > float.MaxValue ? float.MaxValue : (float)elapsedMs;
            }
        }

        public static float RemainingMs => math.max(0f, _budgetMs - ConsumedMs);

        public static void BeginFrame(float globalQualityWeight, uint frameId)
        {
            _frameId = frameId;
            _frameStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _budgetMs = ResolveBudgetMs(globalQualityWeight);
        }

        public static bool HasBudgetRemaining(float estimatedCostMs = 0f)
        {
            float cost = SanitizeNonNegative(estimatedCostMs);
            return ConsumedMs + cost <= _budgetMs;
        }

        public static bool TryConsume(float estimatedCostMs)
        {
            return HasBudgetRemaining(estimatedCostMs);
        }

        private static float ResolveBudgetMs(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float curved = quality * quality * (3f - 2f * quality);
            if (curved < 0.33333334f)
                return math.lerp(MinimumBudgetMs, MiddleBudgetMs, curved * 3f);
            if (curved < 0.6666667f)
                return math.lerp(MiddleBudgetMs, HighBudgetMs, (curved - 0.33333334f) * 3f);
            return math.lerp(HighBudgetMs, UltraBudgetMs, (curved - 0.6666667f) * 3f);
        }

        private static float SanitizeNonNegative(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0f;
            return value;
        }
    }
}
