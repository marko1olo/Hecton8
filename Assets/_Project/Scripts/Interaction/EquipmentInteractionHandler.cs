using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Authoritative queued interaction owner for tool hit queries and late-frame signal dispatch.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9935)]
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int MaxQueuedSignals = 256;
        private const int MaxInteractionPacketsPerFrame = 256;
        private const int MaxQueuedSurfaceRequests = 64;
        private const int MaxEquipmentSocketSlots = VRInteractionKinematicBridgeConstants.SocketCapacity;
        private const int MaxLateFramePendingSocketPublishes = 8;
        private const int MaxLateFramePendingSocketClears = 8;
        private const int MaxCompletedSurfaceAgeFrames = 1;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinHitDistance = 0.05f;
        private const float AttachedFloraArbitrationRadiusMeters = 0.5f;
        private const int MutationGuardLaneMask = 31;
        private static readonly ulong SignalQueueMutationGuardMask =
            1UL << (unchecked((int)(uint)(int)BufferID.InteractionSignalQueue) & MutationGuardLaneMask);
        private static readonly ulong StagingCommandsMutationGuardMask =
            1UL << (unchecked((int)(uint)(int)BufferID.InteractionRaycastStagingCommands) & MutationGuardLaneMask);
        private static readonly ulong SurfaceQueryScheduledMutationGuardMask =
            (1UL << (unchecked((int)(uint)(int)BufferID.InteractionRaycastScheduledCommands) & MutationGuardLaneMask)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.InteractionRaycastScheduledHits) & MutationGuardLaneMask));
        private static readonly ulong SurfaceQueryScheduleMutationGuardMask =
            SurfaceQueryScheduledMutationGuardMask |
            (1UL << (unchecked((int)(uint)(int)BufferID.InteractionRaycastStagingCommands) & MutationGuardLaneMask));
        private static int _baseModuleLayer = int.MinValue;
        private static int _interactableLayer = int.MinValue;
        private static int _voxelLayer = int.MinValue;
        private static ISubmarineRuntimeContext s_submarineRuntimeContext;
        private static IOrganicToolHitService s_organicToolHits;
        private static readonly uint _surfaceQuerySimulationSystemHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("equipment_interaction_surface_query_simulation");
        private static readonly uint _surfaceQueryPostSimulationSystemHash = Hecton8.Data.H8DataHash.ComputeFnv1A32("equipment_interaction_surface_query_post_simulation");

        // COLD ALLOC: Collider[256] - queued target side-channel aligned with the vault interaction queue - owner: EquipmentInteractionHandler
        private readonly Collider[] _queuedTargetColliders = new Collider[MaxQueuedSignals];
        // COLD ALLOC: ulong[64] - requester ids for the writable surface-query staging lane - owner: EquipmentInteractionHandler
        private readonly ulong[] _stagingRequesterIds = new ulong[MaxQueuedSurfaceRequests];
        // COLD ALLOC: InteractionSurfaceQueryDTO[64] - owner-local surface-query staging lane mirrored to DataVault - owner: EquipmentInteractionHandler
        private readonly InteractionSurfaceQueryDTO[] _stagedSurfaceRequests = new InteractionSurfaceQueryDTO[MaxQueuedSurfaceRequests];
        // COLD ALLOC: ulong[64] - requester ids paired with the scheduled surface-query lane - owner: EquipmentInteractionHandler
        private readonly ulong[] _scheduledRequesterIds = new ulong[MaxQueuedSurfaceRequests];
        // COLD ALLOC: InteractionSurfaceQueryDTO[64] - scheduled surface-query lane owned by the SIMULATION->POST_SIMULATION bridge - owner: EquipmentInteractionHandler
        private readonly InteractionSurfaceQueryDTO[] _scheduledSurfaceRequests = new InteractionSurfaceQueryDTO[MaxQueuedSurfaceRequests];
        // COLD ALLOC: InteractionSurfaceQueryDTO[64] - lock-flattened scheduled query snapshot - owner: EquipmentInteractionHandler
        private readonly InteractionSurfaceQueryDTO[] _scheduledResolveRequests = new InteractionSurfaceQueryDTO[MaxQueuedSurfaceRequests];
        // COLD ALLOC: InteractionSurfaceHitDTO[64] - lock-flattened completed hit writeback snapshot - owner: EquipmentInteractionHandler
        private readonly InteractionSurfaceHitDTO[] _scheduledResolveHitDtos = new InteractionSurfaceHitDTO[MaxQueuedSurfaceRequests];
        // COLD ALLOC: ulong[64] - requester ids paired with completed frame-latent surface results - owner: EquipmentInteractionHandler
        private readonly ulong[] _completedRequesterIds = new ulong[MaxQueuedSurfaceRequests];
        // COLD ALLOC: InteractionSurfaceHit[64] - completed frame-latent tool surface results - owner: EquipmentInteractionHandler
        private readonly InteractionSurfaceHit[] _completedHits = new InteractionSurfaceHit[MaxQueuedSurfaceRequests];
        // COLD ALLOC: bool[64] - validity bits for completed frame-latent tool surface results - owner: EquipmentInteractionHandler
        private readonly bool[] _completedHasHit = new bool[MaxQueuedSurfaceRequests];
        // COLD ALLOC: int[64] - frame stamps for completed frame-latent surface results - owner: EquipmentInteractionHandler
        private readonly int[] _completedHitFrames = new int[MaxQueuedSurfaceRequests];
        // COLD ALLOC: Transform[256] - platform-local hit point side-channel aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Transform[] _queuedPlatformTransforms = new Transform[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit points aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitPoints = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit normals aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitNormals = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: bool[256] - platform-local hit validity bits aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly bool[] _queuedHasPlatformLocalHit = new bool[MaxQueuedSignals];
        // COLD ALLOC: EquipmentMetadata[128] - runtime equipment socket slot ownership, one entry per bridge socket slot - owner: EquipmentInteractionHandler
        private readonly EquipmentMetadata[] _equipmentSocketSlotOwners = new EquipmentMetadata[MaxEquipmentSocketSlots];
        // COLD ALLOC: VRInteractionSocketDTO[128] - transient equipment socket publication source copied into DataVault under bridge guard - owner: EquipmentInteractionHandler
        private readonly VRInteractionSocketDTO[] _equipmentSocketScratch = new VRInteractionSocketDTO[MaxEquipmentSocketSlots];
        // COLD ALLOC: bool[128] - deferred socket-slot clears when compaction fence blocks unregister writes - owner: EquipmentInteractionHandler
        private readonly bool[] _pendingEquipmentSocketClears = new bool[MaxEquipmentSocketSlots];

        private IDataVault _dataVault;
        private IDataVault _pendingDataVault;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _voxelSdfReadModel;
        private ITerrainProvider _terrainProvider;
        private VaultGenerationHandle<InteractionSignal> _signalQueueHandle;
        private VaultGenerationHandle<InteractionSurfaceQueryDTO> _scheduledRequestsHandle;
        private VaultGenerationHandle<InteractionSurfaceHitDTO> _scheduledHitsHandle;
        private VaultGenerationHandle<InteractionSurfaceQueryDTO> _stagingRequestsHandle;
        private SimulationPhaseSystem _surfaceQuerySimulationPhase;
        private PostSimulationPhaseSystem _surfaceQueryPostSimulationPhase;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;
        private int _stagedRequestCount;
        private int _scheduledRequestCount;
        private int _completedResultCount;
        private int _previousStagedRequestCount;
        private int _previousScheduledRequestCount;
        private int _previousScheduledHitCount;
        private int _pendingEquipmentSocketClearCount;
        private int _packetAdmissionFrame = -1;
        private int _packetAdmissionCount;
        private int _lastOverflowWarningFrame = -1;
        private int _dispatcherRearmFrame = -1;
        private bool _scheduledSurfaceQueryActive;
        private bool _pendingDataVaultRebind;
        private bool _isInitialized;
        private bool _dispatcherRegistered;
        private bool _simulationSystemRegistered;
        private bool _postSimulationSystemRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;

        internal static EquipmentInteractionHandler ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized && _serviceRegistered;

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            CacheRegistryDependenciesCold();
            TryRegisterHotSwapListenerCold();
            FlushPendingEquipmentSocketPublications(MaxEquipmentSocketSlots);
            if (_isInitialized)
            {
                TryRegisterSignalService();
                TryRegisterToDispatcher();
                return;
            }

            _isInitialized = true;
            TryRegisterSignalService();
            TryRegisterToDispatcher();
        }

        /// <inheritdoc />
        public bool Publish(in InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null || !IsValidSignal(in signal) || !EnsureSignalQueueHandle(createIfMissing: false))
                return false;

            int currentFrame = ResolveSimulationFrameIndex();
            EnsureDispatcherLanesForFrame(currentFrame);
            if (_packetAdmissionFrame != currentFrame)
            {
                _packetAdmissionFrame = currentFrame;
                _packetAdmissionCount = 0;
            }

            if (_packetAdmissionCount >= MaxInteractionPacketsPerFrame || _queueCount >= MaxQueuedSignals)
            {
                LogInteractionOverflowOncePerFrame(currentFrame);
                return false;
            }

            IDataVault vault = ResolveDataVault();
            if (!TryAcquireInteractionGuard(vault, SignalQueueMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _signalQueueHandle,
                        BufferID.InteractionSignalQueue,
                        MaxQueuedSignals,
                        out NativeArray<InteractionSignal> signalQueue))
                {
                    return false;
                }

                signalQueue[_queueTail] = signal;
                _queuedTargetColliders[_queueTail] = targetCollider;
                CachePlatformRelativeHit(_queueTail, in signal, targetCollider);
                _queueTail = (_queueTail + 1) % MaxQueuedSignals;
                _queueCount++;
                _packetAdmissionCount++;
                return true;
            }
            finally
            {
                ReleaseInteractionGuard(vault, SignalQueueMutationGuardMask);
            }
        }

        public bool TryRegisterEquipmentSockets(
            EquipmentMetadata metadata,
            Transform equipmentRoot,
            out int startIndex,
            out int slotCount)
        {
            startIndex = -1;
            slotCount = 0;
            if (metadata == null ||
                equipmentRoot == null ||
                _dataVault == null ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!TryPrepareEquipmentSocketScratch(metadata, equipmentRoot, out int socketCount))
                return false;

            if (!TryReserveEquipmentSocketSlots(metadata, socketCount, out startIndex))
                return false;

            slotCount = socketCount;
            if (!TryWritePreparedEquipmentSocketRange(startIndex, slotCount, socketCount, out int written) ||
                written != socketCount)
            {
                ReleaseEquipmentSocketSlots(metadata, startIndex, slotCount);
                ClearOrQueueEquipmentSocketRange(startIndex, slotCount);
                startIndex = -1;
                slotCount = 0;
                return false;
            }

            RemovePendingEquipmentSocketClearRange(startIndex, slotCount);
            return true;
        }

        public void UnregisterEquipmentSockets(EquipmentMetadata metadata, int startIndex, int slotCount)
        {
            if (metadata == null ||
                startIndex < 0 ||
                slotCount <= 0 ||
                startIndex + slotCount > MaxEquipmentSocketSlots)
            {
                return;
            }

            ReleaseEquipmentSocketSlots(metadata, startIndex, slotCount);
            ClearOrQueueEquipmentSocketRange(startIndex, slotCount);
        }

        /// <inheritdoc />
        public bool RequestPrimarySurfaceHit(ulong requesterId, in InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit)
        {
            Vector3 absoluteOrigin = new Vector3(packet.Origin.x, packet.Origin.y, packet.Origin.z);
            Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(absoluteOrigin);
            Vector3 direction = new Vector3(packet.Direction.x, packet.Direction.y, packet.Direction.z);
            return RequestPrimarySurfaceHit(requesterId, origin, direction, packet.Range, layerMask, queryTriggerInteraction, out hit);
        }

        /// <inheritdoc />
        public bool RequestPrimarySurfaceHit(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit)
        {
            hit = default;
            if (requesterId == 0UL ||
                !IsFinite(origin) ||
                !IsFinite(direction) ||
                !math.isfinite(range) ||
                range <= 0f ||
                direction.sqrMagnitude < MinDirectionSqr)
            {
                return false;
            }

            int currentFrame = ResolveSimulationFrameIndex();
            EnsureDispatcherLanesForFrame(currentFrame);
            bool hasCompletedHit = TryGetCompletedSurfaceHit(requesterId, currentFrame, out InteractionSurfaceHit completedHit);
            Vector3 normalizedDirection = NormalizeFinite(direction, Vector3.forward);
            InteractionSurfaceQueryDTO request = CreateSurfaceQueryRequest(origin, normalizedDirection, range, layerMask, queryTriggerInteraction);
            bool hasCurrentHit = TryResolveKinematicSurfaceHit(in request, out InteractionSurfaceHit currentHit);
            QueuePrimarySurfaceQuery(requesterId, in request);
            if (hasCurrentHit)
            {
                hit = currentHit;
                return true;
            }

            if (hasCompletedHit)
            {
                hit = completedHit;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void ClearQueuedSignals()
        {
            ClearQueuedSignals(createVaultLane: true);
        }

        private void ClearQueuedSignals(bool createVaultLane)
        {
            IDataVault vault = ResolveDataVault();
            bool canClearVaultQueue = false;
            if (vault != null)
            {
                canClearVaultQueue = createVaultLane
                    ? EnsureSignalQueueHandle(createIfMissing: true)
                    : TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _signalQueueHandle,
                        BufferID.InteractionSignalQueue,
                        MaxQueuedSignals,
                        out NativeArray<InteractionSignal> _);
            }

            if (vault != null && canClearVaultQueue && TryAcquireInteractionGuard(vault, SignalQueueMutationGuardMask))
            {
                try
                {
                    if (TryOpenExistingInteractionVaultBuffer(
                            vault,
                            ref _signalQueueHandle,
                            BufferID.InteractionSignalQueue,
                            MaxQueuedSignals,
                            out NativeArray<InteractionSignal> signalQueue))
                    {
                        for (int i = 0; i < signalQueue.Length; i++)
                            signalQueue[i] = default;
                    }
                }
                finally
                {
                    ReleaseInteractionGuard(vault, SignalQueueMutationGuardMask);
                }
            }

            System.Array.Clear(_queuedTargetColliders, 0, _queuedTargetColliders.Length);
            System.Array.Clear(_queuedPlatformTransforms, 0, _queuedPlatformTransforms.Length);
            System.Array.Clear(_queuedPlatformLocalHitPoints, 0, _queuedPlatformLocalHitPoints.Length);
            System.Array.Clear(_queuedPlatformLocalHitNormals, 0, _queuedPlatformLocalHitNormals.Length);
            System.Array.Clear(_queuedHasPlatformLocalHit, 0, _queuedHasPlatformLocalHit.Length);
            _queueHead = 0;
            _queueTail = 0;
            _queueCount = 0;
            _packetAdmissionFrame = -1;
            _packetAdmissionCount = 0;
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            CacheRegistryDependenciesCold();
            TryRegisterHotSwapListenerCold();
            EnsureLayerCache();
            EnsureSignalQueueHandle(createIfMissing: true);

            if (EnsureSurfaceQueryBufferHandles(createIfMissing: true))
            {
                IDataVault vault = ResolveDataVault();
                ResetRequestLaneGuarded(
                    vault,
                    ref _scheduledRequestsHandle,
                    BufferID.InteractionRaycastScheduledCommands);
                ResetRequestLaneGuarded(
                    vault,
                    ref _stagingRequestsHandle,
                    BufferID.InteractionRaycastStagingCommands);
            }

            FlushPendingEquipmentSocketPublications(MaxEquipmentSocketSlots);
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            CacheRegistryDependenciesCold();
            TryRegisterHotSwapListenerCold();
            FlushPendingEquipmentSocketPublications(MaxEquipmentSocketSlots);

            if (!_isInitialized)
                return;

            TryRegisterSignalService();
            TryRegisterToDispatcher();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterFromDispatcher();
            TryUnregisterSignalService();
            TryUnregisterHotSwapListenerCold();
        }

        private static void EnsureLayerCache()
        {
            if (_baseModuleLayer == int.MinValue)
            {
                _baseModuleLayer = Hecton8.Core.HectonLayerMasks.BaseModule;
            }

            if (_interactableLayer == int.MinValue)
            {
                _interactableLayer = Hecton8.Core.HectonLayerMasks.Interactable;
            }

            if (_voxelLayer == int.MinValue)
            {
                _voxelLayer = Hecton8.Core.HectonLayerMasks.VoxelCave;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_pendingDataVaultRebind && !TryApplyPendingDataVaultRebindCold())
                return;

            if (EquipmentMetadata.HasPendingRuntimeSocketPublications)
                FlushPendingEquipmentSocketPublications(MaxLateFramePendingSocketPublishes);
            if (_pendingEquipmentSocketClearCount > 0)
                FlushPendingEquipmentSocketClears(MaxLateFramePendingSocketClears);
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
            TryUnregisterFromDispatcher();
            TryUnregisterSignalService();
            TryUnregisterHotSwapListenerCold();
            _isInitialized = false;

            ClearQueuedSignals(createVaultLane: false);

            bool signalDescriptorReleased = ReleaseInteractionVaultDescriptor(_dataVault, ref _signalQueueHandle);
            TryClearEquipmentSocketRange(0, MaxEquipmentSocketSlots);
            bool surfaceDescriptorsReleased = DisposeSurfaceQueryBuffers();
            if (signalDescriptorReleased && surfaceDescriptorsReleased)
                _dataVault = null;
            System.Array.Clear(_equipmentSocketSlotOwners, 0, _equipmentSocketSlotOwners.Length);
            System.Array.Clear(_equipmentSocketScratch, 0, _equipmentSocketScratch.Length);
            System.Array.Clear(_pendingEquipmentSocketClears, 0, _pendingEquipmentSocketClears.Length);
            _scheduledSurfaceQueryActive = false;
            _dispatcherRearmFrame = -1;
            _scheduledRequestCount = 0;
            _stagedRequestCount = 0;
            _completedResultCount = 0;
            _previousStagedRequestCount = 0;
            _previousScheduledRequestCount = 0;
            _previousScheduledHitCount = 0;
            _pendingDataVault = null;
            _pendingDataVaultRebind = false;
            System.Array.Clear(_stagedSurfaceRequests, 0, _stagedSurfaceRequests.Length);
            System.Array.Clear(_scheduledSurfaceRequests, 0, _scheduledSurfaceRequests.Length);
            System.Array.Clear(_scheduledResolveRequests, 0, _scheduledResolveRequests.Length);
            System.Array.Clear(_scheduledResolveHitDtos, 0, _scheduledResolveHitDtos.Length);
            System.Array.Clear(_stagingRequesterIds, 0, _stagingRequesterIds.Length);
            System.Array.Clear(_scheduledRequesterIds, 0, _scheduledRequesterIds.Length);
            System.Array.Clear(_completedRequesterIds, 0, _completedRequesterIds.Length);
            System.Array.Clear(_completedHasHit, 0, _completedHasHit.Length);
            System.Array.Clear(_completedHits, 0, _completedHits.Length);
            System.Array.Clear(_completedHitFrames, 0, _completedHitFrames.Length);
            _pendingEquipmentSocketClearCount = 0;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void FlushSignals()
        {
            int processedCount = 0;
            while (_queueCount > 0 &&
                   processedCount < MaxQueuedSignals)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryReadQueuedSignal(_queueHead, out InteractionSignal signal))
                    break;

                processedCount++;
                Collider targetCollider = _queuedTargetColliders[_queueHead];
                _queuedTargetColliders[_queueHead] = null;
                RehydratePlatformRelativeHit(_queueHead, ref signal);
                ClearPlatformRelativeHit(_queueHead);
                _queueHead = (_queueHead + 1) % MaxQueuedSignals;
                _queueCount--;

                DispatchSignal(signal, targetCollider);
            }
        }

        private void LogInteractionOverflowOncePerFrame(int currentFrame)
        {
            if (_lastOverflowWarningFrame == currentFrame)
                return;

            _lastOverflowWarningFrame = currentFrame;
            GlobalTelemetryBus.PublishInteractionPacketOverflow(MaxInteractionPacketsPerFrame, _queueCount);
        }

        /// <summary>
        /// Re-arms the dispatcher registrations from a tool-driven entry point, at most once per
        /// simulation frame. This is the only live re-arm route after a scene unload:
        /// <c>SceneRuntimeService.HandleSceneUnloaded</c> (SceneRuntimeService.cs:529) calls
        /// <c>ClearRuntimeState</c> :542, which empties every dispatcher lane and the whole master
        /// system array through <c>GlobalRegistry.ClearRuntimeBuckets</c> :545 ->
        /// <c>SystemDispatcher.ClearAllLanes</c> (SystemDispatcher.cs:1602-1641) and then, at :553-554,
        /// clears this service's queue - proving the service object itself survives the unload while its
        /// drain does not. Nothing re-registers it: <c>InitializeService</c> is only reached from
        /// <c>GameBootstrapper.EnsureEquipmentInteractionServiceRegistered</c>, which early-returns
        /// because <c>GlobalRegistry.InteractionSignals</c> is still this instance (GameBootstrapper.cs
        /// :6033-6034), and <c>OnEnable</c> does not fire again on a GameObject that was never disabled.
        /// Without this the queue filled to <see cref="MaxQueuedSignals"/> and <see cref="Publish"/>
        /// refused every later cut, drill, weld, and boil, so no interaction reached the target.
        /// </summary>
        /// <param name="currentFrame">Simulation frame index the caller already resolved.</param>
        private void EnsureDispatcherLanesForFrame(int currentFrame)
        {
            if (!_isInitialized || _dispatcherRearmFrame == currentFrame)
                return;

            _dispatcherRearmFrame = currentFrame;
            TryRegisterToDispatcher();
        }

        private void TryRegisterToDispatcher()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_surfaceQuerySimulationPhase == null)
                _surfaceQuerySimulationPhase = new SimulationPhaseSystem(this); // COLD ALLOC: IDispatcherSystem[1] - interaction surface query schedule bridge - owner: EquipmentInteractionHandler
            if (_surfaceQueryPostSimulationPhase == null)
                _surfaceQueryPostSimulationPhase = new PostSimulationPhaseSystem(this); // COLD ALLOC: IDispatcherSystem[1] - interaction surface query completion bridge - owner: EquipmentInteractionHandler

            // Deliberately NOT gated on the registered flags. The lanes are emptied behind this owner's
            // back by ClearAllLanes, so a flag claiming registration is not evidence of it. Re-attempts
            // are free and cannot double-register: SystemDispatcher.Register(IDispatcherSystem) returns
            // true for a ReferenceEquals duplicate without inserting (SystemDispatcher.cs:1386-1387), and
            // the late-frame lane is a RegistryBucket whose TryRegister rejects a duplicate via Contains
            // (RegistryBucket.cs:112-116). The pairing below is kept because a Simulation registration
            // without its PostSimulation partner would schedule surface queries that nothing completes,
            // stranding _scheduledSurfaceQueryActive and its JobHandle forever.
            bool simulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_surfaceQuerySimulationPhase);
            bool postSimulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_surfaceQueryPostSimulationPhase);
            if (simulationRegistered && postSimulationRegistered)
            {
                _simulationSystemRegistered = true;
                _postSimulationSystemRegistered = true;
            }
            else
            {
                if (simulationRegistered)
                    GlobalRegistry.UnregisterDispatcherSystem(_surfaceQuerySimulationPhase);
                if (postSimulationRegistered)
                    GlobalRegistry.UnregisterDispatcherSystem(_surfaceQueryPostSimulationPhase);
                _simulationSystemRegistered = false;
                _postSimulationSystemRegistered = false;
            }

            // Raised only on a successful insert and never cleared here: the late-frame bucket answers
            // false BOTH when the entry is already present and when the insert failed, so clearing on
            // false would strand a live lane entry with no owner willing to unregister it on teardown.
            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core))
                _lateFrameRegistered = true;

            _dispatcherRegistered = _simulationSystemRegistered || _postSimulationSystemRegistered || _lateFrameRegistered;
        }

        private void TryUnregisterFromDispatcher()
        {
            if (!_dispatcherRegistered &&
                !_simulationSystemRegistered &&
                !_postSimulationSystemRegistered &&
                !_lateFrameRegistered)
            {
                return;
            }

            if (_simulationSystemRegistered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_surfaceQuerySimulationPhase);
                _simulationSystemRegistered = false;
            }

            if (_postSimulationSystemRegistered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_surfaceQueryPostSimulationPhase);
                _postSimulationSystemRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            _dispatcherRegistered = false;
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly EquipmentInteractionHandler _owner;

            public SimulationPhaseSystem(EquipmentInteractionHandler owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => _surfaceQuerySimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                _owner?.ScheduleStagedSurfaceQueries();
                return dependsOn;
            }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly EquipmentInteractionHandler _owner;

            public PostSimulationPhaseSystem(EquipmentInteractionHandler owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => _surfaceQueryPostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                if (_owner == null)
                    return;

                _owner.CompleteScheduledSurfaceQueries();
                _owner.FlushSignals();
            }
        }

        private void TryRegisterSignalService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterInteractionSignalService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.InteractionSignals, this);
        }

        private void TryUnregisterSignalService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.InteractionSignals, this))
                GlobalRegistry.UnregisterInteractionSignalService(this);
            _serviceRegistered = false;
        }

        private static void DispatchSignal(InteractionSignal signal, Collider targetCollider)
        {
            if (!CanApplyInteraction(in signal, targetCollider))
                return;

            switch ((InteractionEffectType)signal.EffectType)
            {
                case InteractionEffectType.PlasmaCut:
                    if (DispatchPlasmaCut(signal, targetCollider))
                        return;

                    DispatchCutDamage(signal, targetCollider);
                    return;

                case InteractionEffectType.Boil:
                    DispatchBoil(signal);
                    return;

                default:
                    DispatchCutDamage(signal, targetCollider);
                    return;
            }
        }

        private void CachePlatformRelativeHit(int queueIndex, in InteractionSignal signal, Collider targetCollider)
        {
            ClearPlatformRelativeHit(queueIndex);
            if (targetCollider == null || !TryResolvePlatformTransform(targetCollider, out Transform platformTransform))
                return;

            if (!TryResolveSignalRuntimeHitPoint(in signal, out Vector3 runtimeHitPoint))
                return;

            Vector3 hitNormal = new Vector3(signal.HitNormal.x, signal.HitNormal.y, signal.HitNormal.z);
            if (!IsFinite(runtimeHitPoint) || !IsFinite(hitNormal))
                return;

            _queuedPlatformTransforms[queueIndex] = platformTransform;
            _queuedPlatformLocalHitPoints[queueIndex] = platformTransform.InverseTransformPoint(runtimeHitPoint);
            _queuedPlatformLocalHitNormals[queueIndex] = platformTransform.InverseTransformDirection(hitNormal);
            _queuedHasPlatformLocalHit[queueIndex] = true;
        }

        private void RehydratePlatformRelativeHit(int queueIndex, ref InteractionSignal signal)
        {
            if (!_queuedHasPlatformLocalHit[queueIndex])
                return;

            Transform platformTransform = _queuedPlatformTransforms[queueIndex];
            if (platformTransform == null)
                return;

            Vector3 runtimeHitPoint = platformTransform.TransformPoint(_queuedPlatformLocalHitPoints[queueIndex]);
            Vector3 runtimeHitNormal = platformTransform.TransformDirection(_queuedPlatformLocalHitNormals[queueIndex]);
            if (!IsFinite(runtimeHitPoint) || !IsFinite(runtimeHitNormal))
                return;

            if (!TryResolveRuntimeAup(runtimeHitPoint, out double3 absoluteHitPoint))
                return;

            signal.HitPoint = new float3((float)absoluteHitPoint.x, (float)absoluteHitPoint.y, (float)absoluteHitPoint.z);
            signal.SetHitPointAupDouble(absoluteHitPoint);
            signal.HitNormal = new Unity.Mathematics.float3(runtimeHitNormal.x, runtimeHitNormal.y, runtimeHitNormal.z);
        }

        private void ClearPlatformRelativeHit(int queueIndex)
        {
            _queuedPlatformTransforms[queueIndex] = null;
            _queuedPlatformLocalHitPoints[queueIndex] = Vector3.zero;
            _queuedPlatformLocalHitNormals[queueIndex] = Vector3.zero;
            _queuedHasPlatformLocalHit[queueIndex] = false;
        }

        private static bool TryResolvePlatformTransform(Collider targetCollider, out Transform platformTransform)
        {
            platformTransform = null;
            if (targetCollider == null ||
                !InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.TransportPlatform == null ||
                targetInfo.TransportPlatform.PlatformTransform == null)
                return false;

            platformTransform = targetInfo.TransportPlatform.PlatformTransform;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            var originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            var resolvedAup = originAup.OffsetMeters(new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(positionAup));
        }

        private static bool TryResolveSignalHitPointDouble(in InteractionSignal signal, out double3 absoluteHitPoint)
        {
            if (signal.TryGetHitPointAupDouble(out absoluteHitPoint))
                return true;

            if (!IsFinite(signal.HitPoint))
            {
                absoluteHitPoint = default;
                return false;
            }

            absoluteHitPoint = new double3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z);
            return math.all(math.isfinite(absoluteHitPoint));
        }

        private static bool TryResolveSignalRuntimeHitPoint(in InteractionSignal signal, out Vector3 runtimeHitPoint)
        {
            runtimeHitPoint = default;
            if (!TryResolveSignalHitPointDouble(in signal, out double3 absoluteHitPoint))
                return false;

            Vector3 candidate = HectonFloatingOrigin.ToRuntimePosition(absoluteHitPoint);
            if (!IsFinite(candidate))
                return false;

            runtimeHitPoint = candidate;
            return true;
        }

        private static bool IsValidSignal(in InteractionSignal signal)
        {
            return IsFinite(signal.Source.Origin) &&
                   IsFinite(signal.Source.Direction) &&
                   IsFinite(signal.HitPoint) &&
                   IsFinite(signal.HitNormal) &&
                   math.isfinite(signal.Source.Power) &&
                   math.isfinite(signal.Source.Range) &&
                   math.isfinite(signal.PowerDelivered);
        }

        private static Vector3 NormalizeFinite(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= MinDirectionSqr || !IsFinite(value))
                return fallback;

            return value * math.rcp(math.max(ApproximateMagnitudeNoSqrt(value), MinDirectionSqr));
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs(new float3(value.x, value.y, value.z));
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static bool DispatchPlasmaCut(InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null)
                return false;

            if (!TryResolveVoxelPlasmaCutTarget(targetCollider, out IVoxelPlasmaCutTarget volume))
                return false;

            if (!TryResolveSignalHitPointDouble(in signal, out double3 absoluteHitPoint))
                return false;

            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            return volume.TryApplyPlasmaCutDda(
                absoluteHitPoint,
                direction,
                signal.Source.Power,
                signal.Source.Range);
        }

        private static void DispatchBoil(InteractionSignal signal)
        {
            ISubmarineRuntimeContext submarine = s_submarineRuntimeContext;
            IWaterHeatInjectionService waterHeatInjection = submarine != null ? submarine.WaterHeatInjectionService : null;
            if (waterHeatInjection == null)
                return;

            if (!TryResolveSignalRuntimeHitPoint(in signal, out Vector3 runtimeHitPoint))
                return;

            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            waterHeatInjection.TryInjectLocalizedWaterHeat(runtimeHitPoint, direction, signal.PowerDelivered, signal.Source.Power);
        }

        private static void DispatchCutDamage(InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null || signal.PowerDelivered <= 0f)
                return;

            if (!TryResolveSignalRuntimeHitPoint(in signal, out Vector3 runtimeHitPoint))
                return;

            if (TryRouteBaseModuleAttachedFloraCut(targetCollider, runtimeHitPoint, in signal))
                return;

            if (TryResolveSignalConsumer(targetCollider, out IInteractionSignalConsumer signalConsumer))
            {
                signalConsumer.ApplyInteractionSignal(in signal, runtimeHitPoint);
                return;
            }

            if (TryResolveCuttable(targetCollider, out ICuttable cuttable))
                cuttable.ApplyCutDamage(signal.PowerDelivered, runtimeHitPoint);
        }

        private static bool TryRouteBaseModuleAttachedFloraCut(Collider targetCollider, Vector3 runtimeHitPoint, in InteractionSignal signal)
        {
            EnsureLayerCache();
            if (targetCollider == null || targetCollider.gameObject.layer != _baseModuleLayer)
                return false;

            if (!InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                (targetInfo.ModuleHost == null && targetInfo.BaseModule == null))
                return false;

            IOrganicToolHitService organicManager = s_organicToolHits;
            if (organicManager == null)
                return false;

            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            if (direction.sqrMagnitude < MinDirectionSqr)
                direction = Vector3.forward;
            else
                direction = NormalizeFinite(direction, Vector3.forward);

            uint capabilityMask = ToolCapabilityMasks.ResolveCapabilityMask((InteractionEffectType)signal.EffectType);
            Vector3 hitNormal = new Vector3(signal.HitNormal.x, signal.HitNormal.y, signal.HitNormal.z);
            return organicManager.TryApplyAttachedFloraToolHit(
                runtimeHitPoint,
                AttachedFloraArbitrationRadiusMeters,
                hitNormal,
                direction,
                signal.PowerDelivered,
                signal.Source.Power,
                capabilityMask);
        }

        private static bool CanApplyInteraction(in InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null)
                return false;

            if (!TryResolveVulnerabilitySource(targetCollider, out IInteractionVulnerabilitySource vulnerabilitySource))
                return true;

            uint capabilityMask = ToolCapabilityMasks.ResolveCapabilityMask((InteractionEffectType)signal.EffectType);
            if (capabilityMask == 0u)
                return true;

            return (vulnerabilitySource.VulnerabilityMask & capabilityMask) != 0u;
        }

        private static bool TryResolveSignalConsumer(Collider targetCollider, out IInteractionSignalConsumer signalConsumer)
        {
            signalConsumer = null;
            if (targetCollider == null ||
                !InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.InteractionSignalConsumer == null)
                return false;

            signalConsumer = targetInfo.InteractionSignalConsumer;
            return true;
        }

        private static bool TryResolveVulnerabilitySource(Collider targetCollider, out IInteractionVulnerabilitySource vulnerabilitySource)
        {
            vulnerabilitySource = null;
            if (targetCollider == null ||
                !InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.InteractionVulnerabilitySource == null)
                return false;

            vulnerabilitySource = targetInfo.InteractionVulnerabilitySource;
            return true;
        }

        private static bool TryResolveCuttable(Collider targetCollider, out ICuttable cuttable)
        {
            cuttable = null;
            if (targetCollider == null ||
                !InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.Cuttable == null)
                return false;

            cuttable = targetInfo.Cuttable;
            return true;
        }

        private static bool TryResolveVoxelPlasmaCutTarget(Collider targetCollider, out IVoxelPlasmaCutTarget volume)
        {
            volume = null;
            if (targetCollider == null ||
                !InteractableRegistry.TryResolve(targetCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.VoxelPlasmaCutTarget == null)
                return false;

            volume = targetInfo.VoxelPlasmaCutTarget;
            return true;
        }

        private static bool IsValidHit(Vector3 origin, Vector3 direction, float range, int layerMask, InteractionSurfaceHit hit)
        {
            if (hit.collider == null ||
                !IsFinite(origin) ||
                !IsFinite(direction) ||
                !IsFinite(hit.point) ||
                !IsFinite(hit.normal) ||
                !math.isfinite(range) ||
                !math.isfinite(hit.distance) ||
                hit.distance <= MinHitDistance ||
                hit.distance > range)
            {
                return false;
            }

            int layer = hit.collider.gameObject.layer;
            if ((layerMask & (1 << layer)) == 0)
                return false;

            Vector3 toHit = hit.point - origin;
            if (math.dot((float3)hit.normal, (float3)direction) >= 0f)
                return false;

            return toHit.sqrMagnitude > 0.0001f;
        }

        private bool TryResolveKinematicSurfaceHit(in InteractionSurfaceQueryDTO request, out InteractionSurfaceHit hit)
        {
            hit = default;
            if (request.Valid == 0u ||
                !IsFinite(request.Origin) ||
                !IsFinite(request.Direction) ||
                !math.isfinite(request.Range) ||
                request.Range <= MinHitDistance)
            {
                return false;
            }

            Vector3 normalizedDirection = NormalizeFinite(request.Direction, Vector3.forward);
            InteractionSurfaceHit bestHit = default;
            float bestDistance = request.Range;
            bool hasBestHit = false;

            Ray ray = new Ray(request.Origin, normalizedDirection);
            if (InteractableRegistry.TryResolveSpatialTarget(
                    in ray,
                    request.Range,
                    request.LayerMask,
                    ResolveQueryTriggerInteraction(request.TriggerMode),
                    out InteractableRegistry.SpatialHit spatialHit))
            {
                InteractionSurfaceHit colliderHit = InteractionSurfaceHit.FromSurface(
                    spatialHit.Point,
                    spatialHit.Normal,
                    spatialHit.Distance,
                    spatialHit.Collider,
                    spatialHit.Collider != null ? spatialHit.Collider.gameObject.layer : -1);
                TrySelectNearestSurfaceHit(in colliderHit, ref bestHit, ref bestDistance, ref hasBestHit);
            }

            if (TryResolveSdfSurfaceHit(request.Origin, normalizedDirection, request.Range, request.LayerMask, out InteractionSurfaceHit sdfHit))
                TrySelectNearestSurfaceHit(in sdfHit, ref bestHit, ref bestDistance, ref hasBestHit);

            if (TryResolveTerrainSurfaceHit(request.Origin, normalizedDirection, request.Range, request.LayerMask, out InteractionSurfaceHit terrainHit))
                TrySelectNearestSurfaceHit(in terrainHit, ref bestHit, ref bestDistance, ref hasBestHit);

            hit = bestHit;
            return hasBestHit;
        }

        private bool TryResolveSdfSurfaceHit(Vector3 origin, Vector3 direction, float range, int layerMask, out InteractionSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
                return false;

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel readModel = _voxelSdfReadModel;
            if (readModel == null)
                return false;

            float3 origin3 = new float3(origin.x, origin.y, origin.z);
            float3 direction3 = math.normalizesafe(new float3(direction.x, direction.y, direction.z), new float3(0f, 0f, 1f));
            float stepMeters = ResolveToolSdfStepMeters(range);
            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    origin3,
                    direction3,
                    range,
                    stepMeters,
                    out VoxelSonarSdfRaycastHit sdfHit) ||
                (sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.all(math.isfinite(sdfHit.Point)) ||
                !math.all(math.isfinite(sdfHit.Normal)) ||
                !math.isfinite(sdfHit.Distance) ||
                sdfHit.Distance <= MinHitDistance ||
                sdfHit.Distance > range)
            {
                return false;
            }

            float normalSq = math.lengthsq(sdfHit.Normal);
            if (!math.isfinite(normalSq) || normalSq <= 0.0001f)
                return false;

            float3 normal = sdfHit.Normal * math.rsqrt(math.max(normalSq, 0.0001f));
            if (math.dot(normal, direction3) >= 0f)
                normal = -normal;

            int sdfLayer = IncludesAnyLayer(layerMask, HectonLayerMasks.VoxelCaveLayerMask)
                ? HectonLayerMasks.VoxelCave
                : HectonLayerMasks.VoxelProxy;
            hit = InteractionSurfaceHit.FromSurface(
                new Vector3(sdfHit.Point.x, sdfHit.Point.y, sdfHit.Point.z),
                new Vector3(normal.x, normal.y, normal.z),
                math.max(0f, sdfHit.Distance),
                null,
                sdfLayer);
            return true;
        }

        private bool TryResolveTerrainSurfaceHit(Vector3 origin, Vector3 direction, float range, int layerMask, out InteractionSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.TerrainLayerMask) ||
                direction.y >= -0.0001f)
            {
                return false;
            }

            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider == null ||
                !terrainProvider.IsAvailable ||
                !terrainProvider.TryGetHeight(origin.x, origin.z, out float terrainHeight) ||
                !math.isfinite(terrainHeight))
            {
                return false;
            }

            float distance = (terrainHeight - origin.y) / direction.y;
            if (!math.isfinite(distance) ||
                distance <= MinHitDistance ||
                distance > range)
            {
                return false;
            }

            Vector3 point = origin + (direction * distance);
            Vector3 normal = Vector3.up;
            if (terrainProvider.TryGetNormal(point.x, point.z, 1f, out Vector3 sampledNormal) && IsFinite(sampledNormal))
                normal = NormalizeFinite(sampledNormal, Vector3.up);

            if (Vector3.Dot(normal, direction) >= 0f)
                normal = -normal;

            hit = InteractionSurfaceHit.FromSurface(point, normal, distance, null, HectonLayerMasks.Terrain);
            return true;
        }

        private static void TrySelectNearestSurfaceHit(in InteractionSurfaceHit candidate, ref InteractionSurfaceHit bestHit, ref float bestDistance, ref bool hasBestHit)
        {
            if (!candidate.HasHit || !math.isfinite(candidate.distance) || candidate.distance < 0f || candidate.distance > bestDistance)
                return;

            bestHit = candidate;
            bestDistance = candidate.distance;
            hasBestHit = true;
        }

        private static QueryTriggerInteraction ResolveQueryTriggerInteraction(int triggerMode)
        {
            return triggerMode == (int)QueryTriggerInteraction.Ignore ||
                   triggerMode == (int)QueryTriggerInteraction.Collide
                ? (QueryTriggerInteraction)triggerMode
                : QueryTriggerInteraction.UseGlobal;
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static float ResolveToolSdfStepMeters(float range)
        {
            float signalQuality = SignalBusRegistry.GlobalQualityWeight01;
            float quality = math.saturate(math.select(1f, signalQuality, math.isfinite(signalQuality)));
            float coarse = math.max(0.12f, range * 0.04f);
            float fine = math.max(0.04f, range * 0.015f);
            return math.lerp(coarse, fine, quality);
        }

        private void QueuePrimarySurfaceQuery(ulong requesterId, in InteractionSurfaceQueryDTO request)
        {
            int requestIndex = FindStagedRequestIndex(requesterId);
            bool newRequest = requestIndex < 0;
            if (requestIndex < 0)
            {
                if (_stagedRequestCount >= MaxQueuedSurfaceRequests)
                    return;

                requestIndex = _stagedRequestCount;
            }

            if (newRequest)
            {
                _stagingRequesterIds[_stagedRequestCount] = requesterId;
                _stagedRequestCount++;
            }

            _stagedSurfaceRequests[requestIndex] = request;
            if (_stagedRequestCount > _previousStagedRequestCount)
                _previousStagedRequestCount = _stagedRequestCount;

            IDataVault vault = ResolveDataVault();
            if (!TryAcquireInteractionGuard(vault, StagingCommandsMutationGuardMask))
                return;

            try
            {
                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _stagingRequestsHandle,
                        BufferID.InteractionRaycastStagingCommands,
                        MaxQueuedSurfaceRequests,
                        out NativeArray<InteractionSurfaceQueryDTO> stagingRequests))
                {
                    return;
                }

                stagingRequests[requestIndex] = request;
            }
            finally
            {
                ReleaseInteractionGuard(vault, StagingCommandsMutationGuardMask);
            }
        }

        private bool TryGetCompletedSurfaceHit(ulong requesterId, int currentFrame, out InteractionSurfaceHit hit)
        {
            hit = default;
            if (requesterId == 0UL)
                return false;

            for (int i = 0; i < _completedResultCount; i++)
            {
                if (_completedRequesterIds[i] != requesterId)
                    continue;

                if (currentFrame - _completedHitFrames[i] > MaxCompletedSurfaceAgeFrames)
                    return false;

                if (!_completedHasHit[i])
                    return false;

                hit = _completedHits[i];
                return true;
            }

            return false;
        }

        private int FindStagedRequestIndex(ulong requesterId)
        {
            for (int i = 0; i < _stagedRequestCount; i++)
            {
                if (_stagingRequesterIds[i] == requesterId)
                    return i;
            }

            return -1;
        }

        private void CompleteScheduledSurfaceQueries()
        {
            if (!_scheduledSurfaceQueryActive)
                return;

            int scheduledCount = math.min(_scheduledRequestCount, MaxQueuedSurfaceRequests);
            int staleRequestCount = math.min(math.max(_previousScheduledRequestCount, scheduledCount), MaxQueuedSurfaceRequests);
            for (int i = 0; i < scheduledCount; i++)
            {
                _scheduledResolveRequests[i] = _scheduledSurfaceRequests[i];
                _completedRequesterIds[i] = _scheduledRequesterIds[i];
                _scheduledSurfaceRequests[i] = default;
                _scheduledRequesterIds[i] = 0UL;
            }

            for (int i = scheduledCount; i < staleRequestCount; i++)
            {
                _scheduledSurfaceRequests[i] = default;
                _scheduledResolveRequests[i] = default;
                _scheduledResolveHitDtos[i] = default;
                _completedRequesterIds[i] = 0UL;
                _completedHasHit[i] = false;
                _completedHits[i] = default;
                _completedHitFrames[i] = -1;
            }

            _scheduledRequestCount = 0;
            _scheduledSurfaceQueryActive = false;
            _previousScheduledRequestCount = 0;
            _completedResultCount = scheduledCount;

            int completionFrame = ResolveSimulationFrameIndex();
            for (int i = 0; i < scheduledCount; i++)
            {
                InteractionSurfaceQueryDTO request = _scheduledResolveRequests[i];
                bool hasAuthoritativeHit = TryResolveKinematicSurfaceHit(in request, out InteractionSurfaceHit candidate);
                _scheduledResolveHitDtos[i] = hasAuthoritativeHit ? candidate.Dto : default;
                _completedHasHit[i] = hasAuthoritativeHit;
                _completedHits[i] = hasAuthoritativeHit ? candidate : default;
                _completedHitFrames[i] = completionFrame;
                _scheduledResolveRequests[i] = default;
            }

            MirrorCompletedSurfaceQueriesToVault(scheduledCount, staleRequestCount);
        }

        private void MirrorCompletedSurfaceQueriesToVault(int completedCount, int staleRequestCount)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || !EnsureSurfaceQueryBufferHandles(createIfMissing: false))
                return;

            bool mutationGuardAcquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(SurfaceQueryScheduledMutationGuardMask))
                    return;

                mutationGuardAcquired = true;
                if (vault.IsCompactionFenceActive)
                    return;

                bool hasScheduledRequests = TryOpenExistingInteractionVaultBuffer(
                    vault,
                    ref _scheduledRequestsHandle,
                    BufferID.InteractionRaycastScheduledCommands,
                    MaxQueuedSurfaceRequests,
                    out NativeArray<InteractionSurfaceQueryDTO> scheduledRequests);
                bool hasScheduledHits = TryOpenExistingInteractionVaultBuffer(
                    vault,
                    ref _scheduledHitsHandle,
                    BufferID.InteractionRaycastScheduledHits,
                    MaxQueuedSurfaceRequests,
                    out NativeArray<InteractionSurfaceHitDTO> scheduledHits);

                if (hasScheduledRequests)
                {
                    int clearCount = math.min(staleRequestCount, scheduledRequests.Length);
                    for (int i = 0; i < clearCount; i++)
                        scheduledRequests[i] = default;
                }

                if (hasScheduledHits)
                {
                    int writeCount = math.min(completedCount, scheduledHits.Length);
                    int clearCount = math.min(math.max(_previousScheduledHitCount, writeCount), scheduledHits.Length);
                    for (int i = 0; i < writeCount; i++)
                        scheduledHits[i] = _scheduledResolveHitDtos[i];
                    for (int i = writeCount; i < clearCount; i++)
                        scheduledHits[i] = default;
                    _previousScheduledHitCount = writeCount;
                }
            }
            finally
            {
                if (mutationGuardAcquired)
                    vault.ReleaseMutationGuard(SurfaceQueryScheduledMutationGuardMask);
            }
        }

        private void ScheduleStagedSurfaceQueries()
        {
            if (_scheduledSurfaceQueryActive || _stagedRequestCount <= 0)
                return;

            int scheduledCount = math.min(_stagedRequestCount, MaxQueuedSurfaceRequests);
            int staleStagedCount = math.min(math.max(_previousStagedRequestCount, scheduledCount), MaxQueuedSurfaceRequests);
            int staleScheduledCount = math.min(math.max(_previousScheduledRequestCount, scheduledCount), MaxQueuedSurfaceRequests);
            int staleHitCount = math.min(_previousScheduledHitCount, MaxQueuedSurfaceRequests);

            for (int i = 0; i < scheduledCount; i++)
            {
                _scheduledSurfaceRequests[i] = _stagedSurfaceRequests[i];
                _scheduledRequesterIds[i] = _stagingRequesterIds[i];
                _stagedSurfaceRequests[i] = default;
                _stagingRequesterIds[i] = 0UL;
            }

            for (int i = scheduledCount; i < staleScheduledCount; i++)
            {
                _scheduledSurfaceRequests[i] = default;
                _scheduledRequesterIds[i] = 0UL;
            }

            for (int i = scheduledCount; i < staleStagedCount; i++)
            {
                _stagedSurfaceRequests[i] = default;
                _stagingRequesterIds[i] = 0UL;
            }

            _scheduledSurfaceQueryActive = true;
            _scheduledRequestCount = scheduledCount;
            _stagedRequestCount = 0;
            _previousStagedRequestCount = 0;
            _previousScheduledRequestCount = scheduledCount;

            MirrorScheduledSurfaceQueriesToVault(scheduledCount, staleScheduledCount, staleStagedCount, staleHitCount);
        }

        private void MirrorScheduledSurfaceQueriesToVault(int scheduledCount, int staleScheduledCount, int staleStagedCount, int staleHitCount)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || !EnsureSurfaceQueryBufferHandles(createIfMissing: false))
                return;

            bool mutationGuardAcquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(SurfaceQueryScheduleMutationGuardMask))
                    return;

                mutationGuardAcquired = true;
                if (vault.IsCompactionFenceActive)
                    return;

                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _stagingRequestsHandle,
                        BufferID.InteractionRaycastStagingCommands,
                        MaxQueuedSurfaceRequests,
                        out NativeArray<InteractionSurfaceQueryDTO> stagingRequests) ||
                    !TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _scheduledRequestsHandle,
                        BufferID.InteractionRaycastScheduledCommands,
                        MaxQueuedSurfaceRequests,
                        out NativeArray<InteractionSurfaceQueryDTO> scheduledRequests) ||
                    !TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _scheduledHitsHandle,
                        BufferID.InteractionRaycastScheduledHits,
                        MaxQueuedSurfaceRequests,
                        out NativeArray<InteractionSurfaceHitDTO> scheduledHits))
                {
                    return;
                }

                int scheduledWriteCount = math.min(scheduledCount, scheduledRequests.Length);
                int scheduledClearCount = math.min(staleScheduledCount, scheduledRequests.Length);
                for (int i = 0; i < scheduledWriteCount; i++)
                    scheduledRequests[i] = _scheduledSurfaceRequests[i];
                for (int i = scheduledWriteCount; i < scheduledClearCount; i++)
                    scheduledRequests[i] = default;

                int stagingClearCount = math.min(staleStagedCount, stagingRequests.Length);
                for (int i = 0; i < stagingClearCount; i++)
                    stagingRequests[i] = default;

                int hitClearCount = math.min(staleHitCount, scheduledHits.Length);
                for (int i = 0; i < hitClearCount; i++)
                    scheduledHits[i] = default;
                _previousScheduledHitCount = 0;
            }
            finally
            {
                if (mutationGuardAcquired)
                    vault.ReleaseMutationGuard(SurfaceQueryScheduleMutationGuardMask);
            }
        }

        private bool DisposeSurfaceQueryBuffers()
        {
            bool released = true;
            released &= ReleaseInteractionVaultDescriptor(_dataVault, ref _scheduledRequestsHandle);
            released &= ReleaseInteractionVaultDescriptor(_dataVault, ref _scheduledHitsHandle);
            released &= ReleaseInteractionVaultDescriptor(_dataVault, ref _stagingRequestsHandle);
            return released;
        }

        private bool TryReserveEquipmentSocketSlots(EquipmentMetadata metadata, int slotCount, out int startIndex)
        {
            startIndex = -1;
            if (metadata == null || slotCount <= 0 || slotCount > MaxEquipmentSocketSlots)
                return false;

            int lastStart = MaxEquipmentSocketSlots - slotCount;
            for (int candidate = 0; candidate <= lastStart; candidate++)
            {
                bool free = true;
                for (int offset = 0; offset < slotCount; offset++)
                {
                    EquipmentMetadata owner = _equipmentSocketSlotOwners[candidate + offset];
                    if (owner != null && !ReferenceEquals(owner, metadata))
                    {
                        free = false;
                        candidate += offset;
                        break;
                    }
                }

                if (!free)
                    continue;

                for (int offset = 0; offset < slotCount; offset++)
                    _equipmentSocketSlotOwners[candidate + offset] = metadata;

                startIndex = candidate;
                return true;
            }

            return false;
        }

        private void ReleaseEquipmentSocketSlots(EquipmentMetadata metadata, int startIndex, int slotCount)
        {
            if (metadata == null ||
                startIndex < 0 ||
                slotCount <= 0 ||
                startIndex + slotCount > MaxEquipmentSocketSlots)
            {
                return;
            }

            for (int i = 0; i < slotCount; i++)
            {
                int index = startIndex + i;
                if (ReferenceEquals(_equipmentSocketSlotOwners[index], metadata))
                    _equipmentSocketSlotOwners[index] = null;
            }
        }

        private bool TryPrepareEquipmentSocketScratch(EquipmentMetadata metadata, Transform equipmentRoot, out int socketCount)
        {
            socketCount = 0;
            if (metadata == null ||
                equipmentRoot == null ||
                _dataVault == null ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            Vector3 rootPosition = equipmentRoot.position;
            if (!IsFinite(rootPosition))
                return false;

            double3 rootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(rootPosition);
            if (!math.all(math.isfinite(rootAup)))
                return false;

            float4x4 localToWorldMatrix = ToFloat4x4(equipmentRoot.localToWorldMatrix);
            float3 rootRuntimePosition = new float3(rootPosition.x, rootPosition.y, rootPosition.z);
            socketCount = metadata.CopyAnchorsToSockets(
                _equipmentSocketScratch,
                0,
                rootAup,
                rootRuntimePosition,
                localToWorldMatrix);
            return socketCount > 0 && socketCount <= MaxEquipmentSocketSlots;
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        private bool TryWritePreparedEquipmentSocketRange(int startIndex, int slotCount, int socketCount, out int written)
        {
            written = 0;
            if (_dataVault == null ||
                _dataVault.IsCompactionFenceActive ||
                startIndex < 0 ||
                slotCount <= 0 ||
                socketCount < 0 ||
                socketCount > slotCount ||
                startIndex + slotCount > MaxEquipmentSocketSlots)
            {
                return false;
            }

            return VRInteractionKinematicBridgeVault.TryReplaceSocketRange(
                _dataVault,
                _equipmentSocketScratch,
                socketCount,
                startIndex,
                slotCount,
                out written);
        }

        private bool TryClearEquipmentSocketRange(int startIndex, int slotCount)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                startIndex < 0 ||
                slotCount <= 0 ||
                startIndex + slotCount > MaxEquipmentSocketSlots)
            {
                return false;
            }

            return VRInteractionKinematicBridgeVault.TryReplaceSocketRange(
                vault,
                (VRInteractionSocketDTO[])null,
                0,
                startIndex,
                slotCount,
                out int written) &&
                written == 0;
        }

        private void ClearOrQueueEquipmentSocketRange(int startIndex, int slotCount)
        {
            if (TryClearEquipmentSocketRange(startIndex, slotCount))
            {
                RemovePendingEquipmentSocketClearRange(startIndex, slotCount);
                return;
            }

            QueuePendingEquipmentSocketClearRange(startIndex, slotCount);
        }

        private void QueuePendingEquipmentSocketClearRange(int startIndex, int slotCount)
        {
            if (startIndex < 0 ||
                slotCount <= 0 ||
                startIndex + slotCount > MaxEquipmentSocketSlots)
            {
                return;
            }

            for (int offset = 0; offset < slotCount; offset++)
            {
                int index = startIndex + offset;
                if (_pendingEquipmentSocketClears[index])
                    continue;

                _pendingEquipmentSocketClears[index] = true;
                _pendingEquipmentSocketClearCount++;
            }
        }

        private void RemovePendingEquipmentSocketClearRange(int startIndex, int slotCount)
        {
            if (startIndex < 0 ||
                slotCount <= 0 ||
                startIndex + slotCount > MaxEquipmentSocketSlots ||
                _pendingEquipmentSocketClearCount <= 0)
            {
                return;
            }

            for (int offset = 0; offset < slotCount; offset++)
            {
                int index = startIndex + offset;
                if (!_pendingEquipmentSocketClears[index])
                    continue;

                _pendingEquipmentSocketClears[index] = false;
                _pendingEquipmentSocketClearCount--;
            }
        }

        private void FlushPendingEquipmentSocketClears(int maxRanges)
        {
            if (maxRanges <= 0 ||
                _pendingEquipmentSocketClearCount <= 0 ||
                _dataVault == null ||
                _dataVault.IsCompactionFenceActive)
            {
                return;
            }

            int index = 0;
            int rangesCleared = 0;
            while (index < MaxEquipmentSocketSlots &&
                   _pendingEquipmentSocketClearCount > 0 &&
                   rangesCleared < maxRanges)
            {
                if (!_pendingEquipmentSocketClears[index])
                {
                    index++;
                    continue;
                }

                if (_equipmentSocketSlotOwners[index] != null)
                {
                    RemovePendingEquipmentSocketClearRange(index, 1);
                    index++;
                    continue;
                }

                int startIndex = index;
                int slotCount = 1;
                while (startIndex + slotCount < MaxEquipmentSocketSlots &&
                       _pendingEquipmentSocketClears[startIndex + slotCount] &&
                       _equipmentSocketSlotOwners[startIndex + slotCount] == null)
                {
                    slotCount++;
                }

                if (!TryClearEquipmentSocketRange(startIndex, slotCount))
                    return;

                RemovePendingEquipmentSocketClearRange(startIndex, slotCount);
                rangesCleared++;
                index = startIndex + slotCount;
            }
        }

        private void RepublishOwnedEquipmentSocketsCold()
        {
            if (_dataVault == null || _dataVault.IsCompactionFenceActive)
                return;

            int index = 0;
            while (index < MaxEquipmentSocketSlots)
            {
                EquipmentMetadata metadata = _equipmentSocketSlotOwners[index];
                if (metadata == null)
                {
                    index++;
                    continue;
                }

                int startIndex = index;
                int slotCount = 1;
                while (startIndex + slotCount < MaxEquipmentSocketSlots &&
                       ReferenceEquals(_equipmentSocketSlotOwners[startIndex + slotCount], metadata))
                {
                    slotCount++;
                }

                if (metadata.isActiveAndEnabled &&
                    TryPrepareEquipmentSocketScratch(metadata, metadata.transform, out int socketCount))
                {
                    if (TryWritePreparedEquipmentSocketRange(startIndex, slotCount, socketCount, out _))
                        RemovePendingEquipmentSocketClearRange(startIndex, slotCount);
                }

                index = startIndex + slotCount;
            }
        }

        private void FlushPendingEquipmentSocketPublications(int maxAttempts)
        {
            if (!Application.isPlaying ||
                maxAttempts <= 0 ||
                _dataVault == null ||
                _dataVault.IsCompactionFenceActive)
            {
                return;
            }

            EquipmentMetadata.FlushPendingRuntimeSocketPublications(this, maxAttempts);
        }

        private void CacheRegistryDependenciesCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _terrainProvider = GlobalRegistry.Terrain;
            s_submarineRuntimeContext = GlobalRegistry.Submarine;
            s_organicToolHits = GlobalRegistry.OrganicToolHits;
        }

        private void RebindDataVaultCold(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            _pendingDataVault = dataVault;
            _pendingDataVaultRebind = true;
            TryApplyPendingDataVaultRebindCold();
        }

        private bool TryApplyPendingDataVaultRebindCold()
        {
            if (!_pendingDataVaultRebind)
                return true;

            IDataVault oldVault = _dataVault;
            IDataVault nextVault = _pendingDataVault;
            if (ReferenceEquals(oldVault, nextVault))
            {
                _pendingDataVault = null;
                _pendingDataVaultRebind = false;
                return true;
            }

            if ((oldVault != null && oldVault.IsCompactionFenceActive) ||
                (nextVault != null && nextVault.IsCompactionFenceActive))
            {
                return false;
            }

            bool releasedOldDescriptors = ReleaseAllInteractionVaultDescriptors(oldVault);
            if (!releasedOldDescriptors)
                return false;

            _dataVault = nextVault;
            _pendingDataVault = null;
            _pendingDataVaultRebind = false;
            _scheduledSurfaceQueryActive = false;
            _scheduledRequestCount = 0;
            _stagedRequestCount = 0;
            _completedResultCount = 0;
            _previousStagedRequestCount = 0;
            _previousScheduledRequestCount = 0;
            _previousScheduledHitCount = 0;
            System.Array.Clear(_pendingEquipmentSocketClears, 0, _pendingEquipmentSocketClears.Length);
            System.Array.Clear(_stagedSurfaceRequests, 0, _stagedSurfaceRequests.Length);
            System.Array.Clear(_scheduledSurfaceRequests, 0, _scheduledSurfaceRequests.Length);
            System.Array.Clear(_scheduledResolveRequests, 0, _scheduledResolveRequests.Length);
            System.Array.Clear(_scheduledResolveHitDtos, 0, _scheduledResolveHitDtos.Length);
            System.Array.Clear(_stagingRequesterIds, 0, _stagingRequesterIds.Length);
            System.Array.Clear(_scheduledRequesterIds, 0, _scheduledRequesterIds.Length);
            System.Array.Clear(_completedRequesterIds, 0, _completedRequesterIds.Length);
            System.Array.Clear(_completedHasHit, 0, _completedHasHit.Length);
            System.Array.Clear(_completedHits, 0, _completedHits.Length);
            System.Array.Clear(_completedHitFrames, 0, _completedHitFrames.Length);
            _pendingEquipmentSocketClearCount = 0;

            if (_dataVault == null)
                return false;

            EnsureSignalQueueHandle(createIfMissing: true);
            EnsureSurfaceQueryBufferHandles(createIfMissing: true);
            RepublishOwnedEquipmentSocketsCold();
            FlushPendingEquipmentSocketPublications(MaxEquipmentSocketSlots);
            return true;
        }

        private void TryRegisterHotSwapListenerCold()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListenerCold()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultCold(currentService as IDataVault);
                    break;

                case GlobalRegistryServiceSlot.Submarine:
                    s_submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;

                case GlobalRegistryServiceSlot.DestructibleOrganicRuntime:
                    s_organicToolHits = currentService as IOrganicToolHitService;
                    break;

                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;
                    break;

                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _terrainProvider = currentService as ITerrainProvider;
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterFromDispatcher();
                    if (currentService != null && _isInitialized && isActiveAndEnabled)
                        TryRegisterToDispatcher();
                    break;

                case GlobalRegistryServiceSlot.InteractionSignals:
                    _serviceRegistered = ReferenceEquals(currentService, this);
                    break;
            }
        }

        private IDataVault ResolveDataVault()
        {
            return _pendingDataVaultRebind ? null : _dataVault;
        }

        private static int ResolveSimulationFrameIndex()
        {
            return unchecked((int)SystemDispatcher.CurrentFrameId);
        }

        private bool EnsureSurfaceQueryBufferHandles(bool createIfMissing)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureSurfaceQueryBufferHandle(
                    vault,
                    ref _scheduledRequestsHandle,
                    BufferID.InteractionRaycastScheduledCommands,
                    MaxQueuedSurfaceRequests,
                    createIfMissing))
                return false;

            if (!EnsureSurfaceQueryBufferHandle(
                    vault,
                    ref _scheduledHitsHandle,
                    BufferID.InteractionRaycastScheduledHits,
                    MaxQueuedSurfaceRequests,
                    createIfMissing))
                return false;

            if (!EnsureSurfaceQueryBufferHandle(
                    vault,
                    ref _stagingRequestsHandle,
                    BufferID.InteractionRaycastStagingCommands,
                    MaxQueuedSurfaceRequests,
                    createIfMissing))
                return false;

            return true;
        }

        private bool EnsureSignalQueueHandle(bool createIfMissing)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!TryOpenExistingInteractionVaultBuffer(
                    vault,
                    ref _signalQueueHandle,
                    BufferID.InteractionSignalQueue,
                    MaxQueuedSignals,
                    out NativeArray<InteractionSignal> _))
            {
                if (!createIfMissing)
                    return false;

                if (!EnsureInteractionVaultBuffer(
                        vault,
                        ref _signalQueueHandle,
                        BufferID.InteractionSignalQueue,
                        MaxQueuedSignals,
                        createIfMissing,
                        out NativeArray<InteractionSignal> _))
                {
                    return false;
                }
            }

            return TryOpenExistingInteractionVaultBuffer(
                vault,
                ref _signalQueueHandle,
                BufferID.InteractionSignalQueue,
                MaxQueuedSignals,
                out NativeArray<InteractionSignal> _);
        }

        private bool TryReadQueuedSignal(int queueIndex, out InteractionSignal signal)
        {
            signal = default;
            if ((uint)queueIndex >= MaxQueuedSignals || !EnsureSignalQueueHandle(createIfMissing: false))
                return false;

            IDataVault vault = ResolveDataVault();
            if (!TryAcquireInteractionGuard(vault, SignalQueueMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _signalQueueHandle,
                        BufferID.InteractionSignalQueue,
                        MaxQueuedSignals,
                        out NativeArray<InteractionSignal> signalQueue))
                {
                    return false;
                }

                signal = signalQueue[queueIndex];
                signalQueue[queueIndex] = default;
                return true;
            }
            finally
            {
                ReleaseInteractionGuard(vault, SignalQueueMutationGuardMask);
            }
        }

        private static bool EnsureSurfaceQueryBufferHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            bool createIfMissing)
            where T : struct
        {
            return EnsureInteractionVaultBuffer(
                vault,
                ref handle,
                bufferId,
                requiredLength,
                createIfMissing,
                out NativeArray<T> _);
        }

        private static bool EnsureInteractionVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            bool createIfMissing,
            out NativeArray<T> buffer)
            where T : struct
        {
            if (TryOpenExistingInteractionVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0 || !createIfMissing)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryOpenExistingInteractionVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);
            return TryOpenExistingInteractionVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingInteractionVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsGameplayToolsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsGameplayToolsVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.GameplayTools &&
                   handle.Generation != 0u;
        }

        private static bool TryAcquireInteractionGuard(IDataVault vault, ulong guardMask)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   guardMask != 0UL &&
                   vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseInteractionGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static void ResetRequestLaneGuarded(
            IDataVault vault,
            ref VaultGenerationHandle<InteractionSurfaceQueryDTO> handle,
            BufferID bufferId)
        {
            if (vault == null || !IsGameplayToolsVaultHandle(in handle, bufferId))
                return;

            ulong guardMask = 1UL << (unchecked((int)(uint)(int)bufferId) & MutationGuardLaneMask);
            if (!TryAcquireInteractionGuard(vault, guardMask))
                return;

            try
            {
                if (TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref handle,
                        bufferId,
                        MaxQueuedSurfaceRequests,
                        out NativeArray<InteractionSurfaceQueryDTO> requests))
                {
                    for (int i = 0; i < requests.Length; i++)
                        requests[i] = default;
                }
            }
            finally
            {
                ReleaseInteractionGuard(vault, guardMask);
            }
        }

        private bool ReleaseAllInteractionVaultDescriptors(IDataVault vault)
        {
            bool released = true;
            released &= ReleaseInteractionVaultDescriptor(vault, ref _signalQueueHandle);
            released &= ReleaseInteractionVaultDescriptor(vault, ref _scheduledRequestsHandle);
            released &= ReleaseInteractionVaultDescriptor(vault, ref _scheduledHitsHandle);
            released &= ReleaseInteractionVaultDescriptor(vault, ref _stagingRequestsHandle);
            return released;
        }

        private static bool ReleaseInteractionVaultDescriptor<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault == null || handle.BufferID == 0u)
            {
                handle = default;
                return true;
            }

            if (!vault.ReleaseBuffer(in handle))
                return false;

            handle = default;
            return true;
        }

        private static InteractionSurfaceQueryDTO CreateSurfaceQueryRequest(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            return new InteractionSurfaceQueryDTO
            {
                Origin = origin,
                Direction = direction,
                Range = range,
                LayerMask = layerMask,
                TriggerMode = (int)queryTriggerInteraction,
                Valid = 1u
            };
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct InteractionSurfaceQueryDTO
        {
            [FieldOffset(0)] public Vector3 Origin;
            [FieldOffset(12)] public Vector3 Direction;
            [FieldOffset(24)] public float Range;
            [FieldOffset(28)] public int LayerMask;
            [FieldOffset(32)] public int TriggerMode;
            [FieldOffset(36)] public uint Valid;
        }
    }
}
