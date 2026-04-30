using Hecton8.Caves;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Authoritative queued interaction owner for tool hit queries and late-frame signal dispatch.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9935)]
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, IUpdatable
    {
        private const int MaxQueuedSignals = 256;
        private const int MaxQueuedRayRequests = 64;
        private const int MinCommandsPerJob = 1;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinHitDistance = 0.05f;
        private const float AttachedFloraArbitrationRadiusMeters = 0.5f;
        private static int _baseModuleLayer = int.MinValue;

        private static EquipmentInteractionHandler _instance;

        // COLD ALLOC: Collider[256] - queued target side-channel aligned with the native interaction queue - owner: EquipmentInteractionHandler
        private readonly Collider[] _queuedTargetColliders = new Collider[MaxQueuedSignals];
        // COLD ALLOC: ulong[64] - requester ids for the writable raycast staging lane - owner: EquipmentInteractionHandler
        private readonly ulong[] _stagingRequesterIds = new ulong[MaxQueuedRayRequests];
        // COLD ALLOC: ulong[64] - requester ids paired with the scheduled raycast lane - owner: EquipmentInteractionHandler
        private readonly ulong[] _scheduledRequesterIds = new ulong[MaxQueuedRayRequests];
        // COLD ALLOC: ulong[64] - requester ids paired with completed frame-latent raycast results - owner: EquipmentInteractionHandler
        private readonly ulong[] _completedRequesterIds = new ulong[MaxQueuedRayRequests];
        // COLD ALLOC: RaycastHit[64] - completed frame-latent tool raycast results - owner: EquipmentInteractionHandler
        private readonly RaycastHit[] _completedHits = new RaycastHit[MaxQueuedRayRequests];
        // COLD ALLOC: bool[64] - validity bits for completed frame-latent tool raycast results - owner: EquipmentInteractionHandler
        private readonly bool[] _completedHasHit = new bool[MaxQueuedRayRequests];

        private NativeQueue<InteractionSignal> _signalQueue;
        private NativeArray<RaycastCommand> _scheduledCommands;
        private NativeArray<RaycastHit> _scheduledHits;
        private NativeArray<RaycastCommand> _stagingCommands;
        private NativeArray<RaycastHit> _stagingHits;
        private JobHandle _scheduledRaycastHandle;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;
        private int _stagedRequestCount;
        private int _scheduledRequestCount;
        private int _completedResultCount;
        private bool _scheduledRaycastActive;
        private bool _isInitialized;
        private bool _dispatcherRegistered;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Ensures a runtime signal handler exists.
        /// </summary>
        /// <returns>Live handler instance.</returns>
        public static EquipmentInteractionHandler EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[EquipmentInteractionHandler]");
            EquipmentInteractionHandler handler = runtimeRoot.AddComponent<EquipmentInteractionHandler>();
            return handler;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterToDispatcher();
                return;
            }

            GlobalRegistry.RegisterInteractionSignalService(this);
            _isInitialized = true;
            TryRegisterToDispatcher();
        }

        /// <inheritdoc />
        public bool Publish(in InteractionSignal signal, Collider targetCollider)
        {
            if (!_signalQueue.IsCreated || targetCollider == null || _queueCount >= MaxQueuedSignals)
                return false;

            _signalQueue.Enqueue(signal);
            _queuedTargetColliders[_queueTail] = targetCollider;
            _queueTail = (_queueTail + 1) % MaxQueuedSignals;
            _queueCount++;
            return true;
        }

        /// <inheritdoc />
        public bool TryRaycastPrimary(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit)
        {
            hit = default;
            bool hasCompletedHit = TryGetCompletedRaycast(requesterId, out hit);
            if (requesterId == 0UL || range <= 0f || direction.sqrMagnitude < MinDirectionSqr)
                return hasCompletedHit;

            Vector3 normalizedDirection = direction.normalized;
            QueuePrimaryRaycast(requesterId, origin, normalizedDirection, range, layerMask, queryTriggerInteraction);
            return hasCompletedHit;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            CompleteScheduledRaycasts();
        }

        /// <inheritdoc />
        public void ClearQueuedSignals()
        {
            if (_signalQueue.IsCreated)
            {
                int drainIterations = 0;
                while (drainIterations < MaxQueuedSignals && _signalQueue.TryDequeue(out _))
                {
                    drainIterations++;
                }
            }

            System.Array.Clear(_queuedTargetColliders, 0, _queuedTargetColliders.Length);
            _queueHead = 0;
            _queueTail = 0;
            _queueCount = 0;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureLayerCache();

            if (!_signalQueue.IsCreated)
            {
                _signalQueue = new NativeQueue<InteractionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionSignal>(Persistent) - deferred interaction signal bus - owner: EquipmentInteractionHandler
            }

            if (!_scheduledCommands.IsCreated)
            {
                _scheduledCommands = new NativeArray<RaycastCommand>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[64] - scheduled tool raycast lane - owner: EquipmentInteractionHandler
                _scheduledHits = new NativeArray<RaycastHit>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[64] - scheduled tool raycast results - owner: EquipmentInteractionHandler
                _stagingCommands = new NativeArray<RaycastCommand>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[64] - writable tool raycast staging lane - owner: EquipmentInteractionHandler
                _stagingHits = new NativeArray<RaycastHit>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[64] - writable tool raycast staging results - owner: EquipmentInteractionHandler
                ResetCommandLane(_scheduledCommands);
                ResetCommandLane(_stagingCommands);
            }

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private static void EnsureLayerCache()
        {
            if (_baseModuleLayer == int.MinValue)
                _baseModuleLayer = LayerMask.NameToLayer("BaseModule");
        }

        private void LateUpdate()
        {
            FlushSignals();
            ScheduleStagedRaycasts();
        }

        private void OnDestroy()
        {
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterInteractionSignalService(this);
                _isInitialized = false;
            }

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = false;
            }

            ClearQueuedSignals();
            if (_signalQueue.IsCreated)
                _signalQueue.Dispose();

            DisposeRaycastBuffers();

            if (_instance == this)
                _instance = null;
        }

        private void FlushSignals()
        {
            int processedCount = 0;
            while (_queueCount > 0 &&
                   processedCount < MaxQueuedSignals &&
                   _signalQueue.TryDequeue(out InteractionSignal signal))
            {
                processedCount++;
                Collider targetCollider = _queuedTargetColliders[_queueHead];
                _queuedTargetColliders[_queueHead] = null;
                _queueHead = (_queueHead + 1) % MaxQueuedSignals;
                _queueCount--;

                DispatchSignal(signal, targetCollider);
            }
        }

        private void TryRegisterToDispatcher()
        {
            if (_dispatcherRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _dispatcherRegistered = true;
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

        private static bool DispatchPlasmaCut(InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null)
                return false;

            HectonVoxelVolume volume = targetCollider.GetComponent<HectonVoxelVolume>();
            if (volume == null)
                volume = targetCollider.GetComponentInParent<HectonVoxelVolume>();

            if (volume == null)
                return false;

            Vector3 absoluteHitPoint = new Vector3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z);
            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            return volume.ApplyPlasmaCutDda(
                absoluteHitPoint,
                direction,
                signal.Source.Power,
                signal.Source.Range);
        }

        private static void DispatchBoil(InteractionSignal signal)
        {
            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            SubmarineFluidDynamics fluidDynamics = submarine != null ? submarine.FluidDynamics : null;
            if (fluidDynamics == null || !fluidDynamics.isActiveAndEnabled)
                return;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(new Vector3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z));
            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            fluidDynamics.InjectLocalizedWaterHeat(runtimeHitPoint, direction, signal.PowerDelivered, signal.Source.Power);
        }

        private static void DispatchCutDamage(InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null || signal.PowerDelivered <= 0f)
                return;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(new Vector3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z));
            if (ShouldSuppressBaseModuleCutDamage(targetCollider, runtimeHitPoint))
                return;

            if (TryResolveSignalConsumer(targetCollider, out IInteractionSignalConsumer signalConsumer))
            {
                signalConsumer.ApplyInteractionSignal(in signal, runtimeHitPoint);
                return;
            }

            if (TryResolveCuttable(targetCollider, out ICuttable cuttable))
                cuttable.ApplyCutDamage(signal.PowerDelivered, runtimeHitPoint);
        }

        private static bool ShouldSuppressBaseModuleCutDamage(Collider targetCollider, Vector3 runtimeHitPoint)
        {
            EnsureLayerCache();
            if (targetCollider == null || targetCollider.gameObject.layer != _baseModuleLayer)
                return false;

            BaseModule module = targetCollider.GetComponentInParent<BaseModule>();
            if (module == null || module.ParasiteInfectionLevel <= 0.0001f)
                return false;

            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            return organicManager.TryResolveNearestConsumableFlora(
                runtimeHitPoint,
                AttachedFloraArbitrationRadiusMeters,
                out _,
                out _);
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
            if (targetCollider == null)
                return false;

            if (targetCollider.TryGetComponent(out IInteractionSignalConsumer directConsumer))
            {
                signalConsumer = directConsumer;
                return true;
            }

            signalConsumer = targetCollider.GetComponentInParent<IInteractionSignalConsumer>();
            return signalConsumer != null;
        }

        private static bool TryResolveVulnerabilitySource(Collider targetCollider, out IInteractionVulnerabilitySource vulnerabilitySource)
        {
            vulnerabilitySource = null;
            if (targetCollider == null)
                return false;

            if (targetCollider.TryGetComponent(out IInteractionVulnerabilitySource directSource))
            {
                vulnerabilitySource = directSource;
                return true;
            }

            vulnerabilitySource = targetCollider.GetComponentInParent<IInteractionVulnerabilitySource>();
            return vulnerabilitySource != null;
        }

        private static bool TryResolveCuttable(Collider targetCollider, out ICuttable cuttable)
        {
            cuttable = null;
            if (targetCollider == null)
                return false;

            if (targetCollider.TryGetComponent(out ICuttable directCuttable))
            {
                cuttable = directCuttable;
                return true;
            }

            cuttable = targetCollider.GetComponentInParent<ICuttable>();
            return cuttable != null;
        }

        private static bool IsValidHit(Vector3 origin, Vector3 direction, float range, int layerMask, RaycastHit hit)
        {
            if (hit.collider == null || hit.distance <= MinHitDistance || hit.distance > range)
                return false;

            int layer = hit.collider.gameObject.layer;
            if ((layerMask & (1 << layer)) == 0)
                return false;

            Vector3 toHit = hit.point - origin;
            if (Vector3.Dot(hit.normal, direction) >= 0f)
                return false;

            return toHit.sqrMagnitude > 0.0001f;
        }

        private void QueuePrimaryRaycast(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            int requestIndex = FindStagedRequestIndex(requesterId);
            if (requestIndex < 0)
            {
                if (_stagedRequestCount >= MaxQueuedRayRequests)
                    return;

                requestIndex = _stagedRequestCount;
                _stagingRequesterIds[_stagedRequestCount] = requesterId;
                _stagedRequestCount++;
            }

            _stagingCommands[requestIndex] = CreateRaycastCommand(origin, direction, range, layerMask, queryTriggerInteraction);
        }

        private bool TryGetCompletedRaycast(ulong requesterId, out RaycastHit hit)
        {
            hit = default;
            if (requesterId == 0UL)
                return false;

            for (int i = 0; i < _completedResultCount; i++)
            {
                if (_completedRequesterIds[i] != requesterId)
                    continue;

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

        private void CompleteScheduledRaycasts()
        {
            if (!_scheduledRaycastActive || !_scheduledRaycastHandle.IsCompleted)
                return;

            _scheduledRaycastHandle.Complete();
            _completedResultCount = _scheduledRequestCount;

            for (int i = 0; i < _scheduledRequestCount; i++)
            {
                RaycastCommand command = _scheduledCommands[i];
                RaycastHit candidate = _scheduledHits[i];
                int layerMask = command.queryParameters.layerMask;
                _completedRequesterIds[i] = _scheduledRequesterIds[i];
                _completedHasHit[i] = IsValidHit(command.from, command.direction, command.distance, layerMask, candidate);
                _completedHits[i] = _completedHasHit[i] ? candidate : default;
                _scheduledRequesterIds[i] = 0UL;
            }

            for (int i = _scheduledRequestCount; i < MaxQueuedRayRequests; i++)
            {
                _completedRequesterIds[i] = 0UL;
                _completedHasHit[i] = false;
                _completedHits[i] = default;
            }

            _scheduledRequestCount = 0;
            _scheduledRaycastActive = false;
            ResetCommandLane(_scheduledCommands);
        }

        private void ScheduleStagedRaycasts()
        {
            if (_scheduledRaycastActive || _stagedRequestCount <= 0)
                return;

            for (int i = _stagedRequestCount; i < MaxQueuedRayRequests; i++)
            {
                _stagingCommands[i] = CreateInvalidRaycastCommand();
                _stagingRequesterIds[i] = 0UL;
            }

            _scheduledRaycastHandle = RaycastCommand.ScheduleBatch(_stagingCommands, _stagingHits, MinCommandsPerJob, default);
            _scheduledRaycastActive = true;
            _scheduledRequestCount = _stagedRequestCount;

            NativeArray<RaycastCommand> scheduledCommands = _scheduledCommands;
            _scheduledCommands = _stagingCommands;
            _stagingCommands = scheduledCommands;

            NativeArray<RaycastHit> scheduledHits = _scheduledHits;
            _scheduledHits = _stagingHits;
            _stagingHits = scheduledHits;

            System.Array.Copy(_stagingRequesterIds, _scheduledRequesterIds, MaxQueuedRayRequests);
            System.Array.Clear(_stagingRequesterIds, 0, _stagingRequesterIds.Length);

            _stagedRequestCount = 0;
        }

        private void DisposeRaycastBuffers()
        {
            if (_scheduledCommands.IsCreated)
            {
                if (_scheduledRaycastActive)
                    _scheduledCommands.Dispose(_scheduledRaycastHandle);
                else
                    _scheduledCommands.Dispose();

                _scheduledCommands = default;
            }

            if (_scheduledHits.IsCreated)
            {
                if (_scheduledRaycastActive)
                    _scheduledHits.Dispose(_scheduledRaycastHandle);
                else
                    _scheduledHits.Dispose();

                _scheduledHits = default;
            }

            if (_stagingCommands.IsCreated)
            {
                _stagingCommands.Dispose();
                _stagingCommands = default;
            }

            if (_stagingHits.IsCreated)
            {
                _stagingHits.Dispose();
                _stagingHits = default;
            }
        }

        private static RaycastCommand CreateRaycastCommand(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            return new RaycastCommand
            {
                from = origin,
                direction = direction,
                distance = range,
                queryParameters = new QueryParameters
                {
                    layerMask = layerMask,
                    hitTriggers = queryTriggerInteraction,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };
        }

        private static RaycastCommand CreateInvalidRaycastCommand()
        {
            return CreateRaycastCommand(Vector3.zero, Vector3.forward, 0f, 0, QueryTriggerInteraction.Ignore);
        }

        private static void ResetCommandLane(NativeArray<RaycastCommand> commands)
        {
            if (!commands.IsCreated)
                return;

            for (int i = 0; i < commands.Length; i++)
                commands[i] = CreateInvalidRaycastCommand();
        }
    }
}
