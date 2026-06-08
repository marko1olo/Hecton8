// ============================================================================
// HECTON-8 - AirlockPressurizationRuntimeOwner.cs
// SHINOBU_338 dispatcher lifecycle owner for airlock pressure jobs.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;

namespace Hecton8.Gameplay.AirlockPressurization
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Gameplay/Airlock Pressurization Runtime Owner")]
    public sealed class AirlockPressurizationRuntimeOwner : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private const string RuntimeObjectName = "SHINOBU_338_AirlockPressurizationRuntimeOwner";
        private const int RuntimeWarningCooldownFrames = 60;
        private const uint RuntimeWarningContextHash = 0x4150524Fu; // "APRO"
        private const uint DuplicateOwnerWarningHash = 0x4150444Fu; // "APDO"
        private const uint DispatcherMissingWarningHash = 0x41504453u; // "APDS"
        private const uint DataVaultMissingWarningHash = 0x41504456u; // "APDV"
        private const uint HandleAcquireWarningHash = 0x41504841u; // "APHA"
        private const uint SimulationResolveViewsWarningHash = 0x41505253u; // "APRS"
        private const uint PostResolveViewsWarningHash = 0x41505250u; // "APRP"

        private static AirlockPressurizationRuntimeOwner s_activeOwner;

        [SerializeField, Range(1, AirlockPressurizationConstants.MaxActiveAirlocks)]
        private int capacity = AirlockPressurizationConstants.MaxActiveAirlocks;

        [SerializeField, Range(0f, 1f)]
        private float globalQualityWeight = AirlockPressurizationConstants.AuthoritativeQualityWeight;

        private AirlockPressurizationVaultHandles _handles;
        private AirlockPressurizationScheduleState _scheduleState;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private bool _handlesReady;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _hotSwapRegistered;
        private bool _simulationScheduled;
        private bool _claimedOwner;
        private int _lastScheduledActiveCount;
        private int _nextDuplicateOwnerWarningFrame;
        private int _nextDispatcherWarningFrame;
        private int _nextDataVaultWarningFrame;
        private int _nextHandleWarningFrame;
        private int _nextSimulationResolveWarningFrame;
        private int _nextPostResolveWarningFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeOwner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_activeOwner != null)
                return;

            GameObject host = new GameObject(RuntimeObjectName); // COLD ALLOC: GameObject[1] - persistent airlock pressure dispatcher owner - owner: SHINOBU_338
            host.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            DontDestroyOnLoad(host);
            host.AddComponent<AirlockPressurizationRuntimeOwner>();
        }

        private void Awake()
        {
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !TryClaimActiveOwner())
                return;

            EnsureSignalLanes();
            TryRegisterHotSwapListener();
            RefreshHandles();
            TryRegisterDispatcherPhases();
        }

        private void OnDisable()
        {
            if (!_claimedOwner)
                return;

            TryUnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            _handles = default;
            _handlesReady = false;
            _simulationScheduled = false;
            _lastScheduledActiveCount = 0;
            ResetRuntimeWarningCooldowns();
            ReleaseActiveOwner();
        }

        private void OnDestroy()
        {
            if (!_claimedOwner)
                return;

            TryUnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            ReleaseActiveOwner();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!_claimedOwner)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _handles = default;
                    _handlesReady = false;
                    RefreshHandles();
                    TryRegisterDispatcherPhases();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherPhases();
                    if (currentService != null)
                        TryRegisterDispatcherPhases();
                    else
                        PublishRuntimeWarning(DispatcherMissingWarningHash, 0u, 0f, ref _nextDispatcherWarningFrame);
                    break;
            }
        }

        private bool TryClaimActiveOwner()
        {
            if (s_activeOwner != null && !ReferenceEquals(s_activeOwner, this))
            {
                PublishRuntimeWarning(DuplicateOwnerWarningHash, 0u, 1f, ref _nextDuplicateOwnerWarningFrame);
                _claimedOwner = false;
                return false;
            }

            s_activeOwner = this;
            _claimedOwner = true;
            return true;
        }

        private void ReleaseActiveOwner()
        {
            if (ReferenceEquals(s_activeOwner, this))
                s_activeOwner = null;

            _claimedOwner = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !_claimedOwner)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterDispatcherPhases()
        {
            if (!_claimedOwner)
                return;

            TryRegisterHotSwapListener();

            if (GlobalRegistry.Dispatcher == null)
            {
                PublishRuntimeWarning(DispatcherMissingWarningHash, 0u, 0f, ref _nextDispatcherWarningFrame);
                return;
            }

            if (!EnsureHandlesReady())
                PublishMissingHandlesWarning(GlobalRegistry.DataVault, 0u);

            if (!_registeredSimulation)
                _registeredSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase);
            if (!_registeredPostSimulation)
                _registeredPostSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
        }

        private void TryUnregisterDispatcherPhases()
        {
            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }

            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }
        }

        private bool EnsureHandlesReady()
        {
            return _handlesReady || RefreshHandles();
        }

        private bool RefreshHandles()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            _handlesReady = AirlockPressurizationVault.AcquireHandles(
                vault,
                math.clamp(capacity, 1, AirlockPressurizationConstants.MaxActiveAirlocks),
                out _handles);
            return _handlesReady;
        }

        private JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            _simulationScheduled = false;
            _lastScheduledActiveCount = 0;
            TryRegisterHotSwapListener();

            IDataVault vault = GlobalRegistry.DataVault;
            if (!EnsureHandlesReady())
            {
                PublishMissingHandlesWarning(vault, timing.FrameId);
                return dependsOn;
            }

            vault = GlobalRegistry.DataVault;
            if (!AirlockPressurizationVault.ResolveViews(vault, in _handles, out AirlockPressurizationVaultBuffers buffers))
            {
                PublishRuntimeWarning(
                    SimulationResolveViewsWarningHash,
                    timing.FrameId,
                    ResolveWarningCapacityScalar(),
                    ref _nextSimulationResolveWarningFrame);
                return dependsOn;
            }

            float quality = ResolveGlobalQualityWeight(buffers.Tunings);
            if (!AirlockPressurizationVault.AdvanceCadence(
                    ref _scheduleState,
                    timing.FrameDelta,
                    quality,
                    timing.FrameId,
                    out float admittedDeltaSeconds,
                    out _))
            {
                return dependsOn;
            }

            _lastScheduledActiveCount = ResolveActiveAirlockRange(in buffers);
            if (!AirlockPressurizationVault.ScheduleSimulation(
                    in buffers,
                    default(NativeArray<FluidCompartmentDTO>),
                    default(NativeArray<AtmosphereCellDTO>),
                    _lastScheduledActiveCount,
                    timing.FrameId,
                    admittedDeltaSeconds,
                    quality,
                    0u,
                    dependsOn,
                    out JobHandle outputDependency))
            {
                return dependsOn;
            }

            _simulationScheduled = true;
            return outputDependency;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!_simulationScheduled)
                return;

            _simulationScheduled = false;
            IDataVault vault = GlobalRegistry.DataVault;
            if (!EnsureHandlesReady())
            {
                PublishMissingHandlesWarning(vault, timing.FrameId);
                return;
            }

            vault = GlobalRegistry.DataVault;
            if (!AirlockPressurizationVault.ResolveViews(vault, in _handles, out AirlockPressurizationVaultBuffers buffers))
            {
                PublishRuntimeWarning(
                    PostResolveViewsWarningHash,
                    timing.FrameId,
                    _lastScheduledActiveCount,
                    ref _nextPostResolveWarningFrame);
                return;
            }

            EnsureSignalLanes();
            AirlockPressurizationVault.FlushCompletedOutputs(
                buffers,
                _lastScheduledActiveCount,
                dispatcherCompletionConfirmed: true);
        }

        private float ResolveGlobalQualityWeight(NativeArray<AirlockTuningDTO> tunings)
        {
            float quality = AirlockPressurizationMath.FiniteOr(globalQualityWeight, AirlockPressurizationConstants.AuthoritativeQualityWeight);
            if (!tunings.IsCreated || tunings.Length <= 0)
                return math.saturate(quality);

            for (int i = 0; i < tunings.Length; i++)
            {
                if (tunings[i].Frame != 0u)
                    return math.saturate(AirlockPressurizationMath.FiniteOr(tunings[i].GlobalQualityWeight, quality));
            }

            return math.saturate(quality);
        }

        private static int ResolveActiveAirlockRange(in AirlockPressurizationVaultBuffers buffers)
        {
            if (!buffers.DoorPoses.IsCreated)
                return 0;

            int capacity = math.min(buffers.DoorPoses.Length, AirlockPressurizationConstants.MaxActiveAirlocks);
            int lastActive = 0;
            for (int i = 0; i < capacity; i++)
            {
                if (buffers.DoorPoses[i].EdgeHashID != 0u)
                    lastActive = i + 1;
            }

            return lastActive;
        }

        private float ResolveWarningCapacityScalar()
        {
            return math.clamp(capacity, 1, AirlockPressurizationConstants.MaxActiveAirlocks);
        }

        private void PublishMissingHandlesWarning(IDataVault vault, uint frame)
        {
            if (vault == null)
            {
                PublishRuntimeWarning(
                    DataVaultMissingWarningHash,
                    frame,
                    ResolveWarningCapacityScalar(),
                    ref _nextDataVaultWarningFrame);
                return;
            }

            PublishRuntimeWarning(
                HandleAcquireWarningHash,
                frame,
                ResolveWarningCapacityScalar(),
                ref _nextHandleWarningFrame);
        }

        private static void PublishRuntimeWarning(uint warningHash, uint frame, float scalarValue, ref int nextWarningFrame)
        {
            int frameIndex = ResolveWarningFrameIndex(frame);
            if (frameIndex < nextWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, RuntimeWarningContextHash, scalarValue);
            nextWarningFrame = frameIndex + RuntimeWarningCooldownFrames;
        }

        private static int ResolveWarningFrameIndex(uint frame)
        {
            uint resolvedFrame = frame != 0u ? frame : SystemDispatcher.CurrentFrameId;
            int frameIndex = (int)(resolvedFrame & 0x7FFFFFFFu);
            return frameIndex > 0 ? frameIndex : SystemDispatcher.CurrentFrameIndex;
        }

        private void ResetRuntimeWarningCooldowns()
        {
            _nextDuplicateOwnerWarningFrame = 0;
            _nextDispatcherWarningFrame = 0;
            _nextDataVaultWarningFrame = 0;
            _nextHandleWarningFrame = 0;
            _nextSimulationResolveWarningFrame = 0;
            _nextPostResolveWarningFrame = 0;
        }

        private static void EnsureSignalLanes()
        {
            SignalBus<MovementAcousticSignal>.EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.EnsureInitialized();
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AirlockPressurizationRuntimeOwner _owner;

            public SimulationPhaseSystem(AirlockPressurizationRuntimeOwner owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => AirlockPressurizationConstants.SimulationHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;
            public byte GetBucketId() => byte.MaxValue;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return _owner != null ? _owner.ScheduleSimulation(in timing, in context, dependsOn) : dependsOn;
            }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AirlockPressurizationRuntimeOwner _owner;

            public PostSimulationPhaseSystem(AirlockPressurizationRuntimeOwner owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => AirlockPressurizationConstants.PostSimulationHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;
            public byte GetBucketId() => byte.MaxValue;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }
    }
}
