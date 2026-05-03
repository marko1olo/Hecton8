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
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, IUpdatable, ILateFrameTickable
    {
        private const int MaxQueuedSignals = 256;
        private const int MaxInteractionPacketsPerFrame = 256;
        private const int MaxQueuedRayRequests = 64;
        private const int MaxHitArbitrationHits = 8;
        private const int MinCommandsPerJob = 1;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinHitDistance = 0.05f;
        private const float AttachedFloraArbitrationRadiusMeters = 0.5f;
        private static int _baseModuleLayer = int.MinValue;
        private static int _interactableLayer = int.MinValue;
        private static int _voxelLayer = int.MinValue;

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
        // COLD ALLOC: RaycastHit[8] - fixed flora/base overlap arbitration buffer - owner: EquipmentInteractionHandler
        private static readonly RaycastHit[] _hitArbitrationHits = new RaycastHit[MaxHitArbitrationHits];
        // COLD ALLOC: Transform[256] - platform-local hit point side-channel aligned with the native signal queue - owner: EquipmentInteractionHandler
        private readonly Transform[] _queuedPlatformTransforms = new Transform[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit points aligned with the native signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitPoints = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit normals aligned with the native signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitNormals = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: bool[256] - platform-local hit validity bits aligned with the native signal queue - owner: EquipmentInteractionHandler
        private readonly bool[] _queuedHasPlatformLocalHit = new bool[MaxQueuedSignals];

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
        private int _packetAdmissionFrame = -1;
        private int _packetAdmissionCount;
        private int _lastOverflowWarningFrame = -1;
        private bool _scheduledRaycastActive;
        private bool _isInitialized;
        private bool _dispatcherRegistered;
        private bool _serviceRegistered;

        internal static EquipmentInteractionHandler ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
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
            if (!_signalQueue.IsCreated || targetCollider == null)
                return false;

            int currentFrame = Time.frameCount;
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

            _signalQueue.Enqueue(signal);
            _queuedTargetColliders[_queueTail] = targetCollider;
            CachePlatformRelativeHit(_queueTail, in signal, targetCollider);
            _queueTail = (_queueTail + 1) % MaxQueuedSignals;
            _queueCount++;
            _packetAdmissionCount++;
            return true;
        }

        /// <inheritdoc />
        public bool TryRaycastPrimary(ulong requesterId, in InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit)
        {
            Vector3 origin = new Vector3(packet.Origin.x, packet.Origin.y, packet.Origin.z);
            Vector3 direction = new Vector3(packet.Direction.x, packet.Direction.y, packet.Direction.z);
            return TryRaycastPrimary(requesterId, origin, direction, packet.Range, layerMask, queryTriggerInteraction, out hit);
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

            EnsureLayerCache();

            if (!_signalQueue.IsCreated)
            {
                _signalQueue = new NativeQueue<InteractionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionSignal>(Persistent) - deferred interaction signal bus - owner: EquipmentInteractionHandler
                NativeMemorySentinel.RegisterNativeQueue(
                    _signalQueue,
                    MaxQueuedSignals,
                    nameof(EquipmentInteractionHandler),
                    nameof(_signalQueue),
                    NativeAllocationLifetime.Session);
            }

            if (!_scheduledCommands.IsCreated)
            {
                _scheduledCommands = new NativeArray<RaycastCommand>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[64] - scheduled tool raycast lane - owner: EquipmentInteractionHandler
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledCommands,
                    nameof(EquipmentInteractionHandler),
                    nameof(_scheduledCommands),
                    NativeAllocationLifetime.Session);
                _scheduledHits = new NativeArray<RaycastHit>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[64] - scheduled tool raycast results - owner: EquipmentInteractionHandler
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledHits,
                    nameof(EquipmentInteractionHandler),
                    nameof(_scheduledHits),
                    NativeAllocationLifetime.Session);
                _stagingCommands = new NativeArray<RaycastCommand>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[64] - writable tool raycast staging lane - owner: EquipmentInteractionHandler
                NativeMemorySentinel.RegisterNativeArray(
                    _stagingCommands,
                    nameof(EquipmentInteractionHandler),
                    nameof(_stagingCommands),
                    NativeAllocationLifetime.Session);
                _stagingHits = new NativeArray<RaycastHit>(MaxQueuedRayRequests, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[64] - writable tool raycast staging results - owner: EquipmentInteractionHandler
                NativeMemorySentinel.RegisterNativeArray(
                    _stagingHits,
                    nameof(EquipmentInteractionHandler),
                    nameof(_stagingHits),
                    NativeAllocationLifetime.Session);
                ResetCommandLane(_scheduledCommands);
                ResetCommandLane(_stagingCommands);
            }
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

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
            CompleteScheduledRaycasts();
            FlushSignals();
            ScheduleStagedRaycasts();
        }

        private void OnDestroy()
        {
            TryUnregisterFromDispatcher();
            TryUnregisterSignalService();
            _isInitialized = false;

            ClearQueuedSignals();
            if (_signalQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EquipmentInteractionHandler), nameof(_signalQueue));
                _signalQueue.Dispose();
            }

            DisposeRaycastBuffers();
        }

        private void FlushSignals()
        {
            int processedCount = 0;
            while (_queueCount > 0 &&
                   processedCount < MaxQueuedSignals)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_signalQueue.TryDequeue(out InteractionSignal signal))
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EquipmentInteractionHandler] Interaction packet capacity exceeded. Excess packets were dropped for this frame.");
#endif
        }

        private void TryRegisterToDispatcher()
        {
            if (_dispatcherRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
            _dispatcherRegistered = true;
        }

        private void TryUnregisterFromDispatcher()
        {
            if (!_dispatcherRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _dispatcherRegistered = false;
        }

        private void TryRegisterSignalService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterInteractionSignalService(this);
            _serviceRegistered = true;
        }

        private void TryUnregisterSignalService()
        {
            if (!_serviceRegistered)
                return;

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

            Vector3 absoluteHitPoint = new Vector3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z);
            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(absoluteHitPoint);
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

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeHitPoint);
            signal.HitPoint = new Unity.Mathematics.float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z);
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
            Transform current = targetCollider != null ? targetCollider.transform : null;
            while (current != null)
            {
                if (current.TryGetComponent(out ITransportPlatform platform) && platform.PlatformTransform != null)
                {
                    platformTransform = platform.PlatformTransform;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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

            BaseModule module = targetCollider.GetComponentInParent<BaseModule>();
            if (module == null)
                return false;

            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            if (!organicManager.TryResolveNearestConsumableFlora(
                runtimeHitPoint,
                AttachedFloraArbitrationRadiusMeters,
                out Vector3 floraPosition,
                out _))
            {
                return false;
            }

            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            if (direction.sqrMagnitude < MinDirectionSqr)
                direction = Vector3.forward;
            else
                direction.Normalize();

            int layerMask = BuildHitArbitrationLayerMask(targetCollider.gameObject.layer);
            Vector3 castOrigin = runtimeHitPoint - direction * AttachedFloraArbitrationRadiusMeters;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                castOrigin,
                AttachedFloraArbitrationRadiusMeters,
                direction,
                _hitArbitrationHits,
                AttachedFloraArbitrationRadiusMeters * 2f,
                layerMask,
                QueryTriggerInteraction.Collide);
            if (hitCount == _hitArbitrationHits.Length)
                SortHitArbitrationHitsByDistance(hitCount);

            bool sawHostModule = hitCount <= 0;
            bool sawFloraCandidate = hitCount <= 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _hitArbitrationHits[i].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider == targetCollider)
                {
                    sawHostModule = true;
                    continue;
                }

                BaseModule hitModule = hitCollider.GetComponentInParent<BaseModule>();
                if (hitModule == module)
                {
                    sawHostModule = true;
                    continue;
                }

                Vector3 hitPoint = _hitArbitrationHits[i].point;
                if ((hitPoint - floraPosition).sqrMagnitude <= AttachedFloraArbitrationRadiusMeters * AttachedFloraArbitrationRadiusMeters)
                    sawFloraCandidate = true;
            }

            if (sawHostModule || sawFloraCandidate)
            {
                FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
                uint capabilityMask = ToolCapabilityMasks.ResolveCapabilityMask((InteractionEffectType)signal.EffectType);
                Vector3 hitNormal = new Vector3(signal.HitNormal.x, signal.HitNormal.y, signal.HitNormal.z);
                if (floraInteractionManager != null &&
                    floraInteractionManager.TryApplyModuleParasiteCut(
                        floraPosition,
                        hitNormal,
                        direction,
                        signal.PowerDelivered,
                        signal.Source.Power,
                        capabilityMask))
                {
                    return true;
                }
            }

            return true;
        }

        private static int BuildHitArbitrationLayerMask(int targetLayer)
        {
            EnsureLayerCache();
            int layerMask = 0;
            if (targetLayer >= 0 && targetLayer < 32)
                layerMask |= 1 << targetLayer;
            if (_baseModuleLayer >= 0 && _baseModuleLayer < 32)
                layerMask |= 1 << _baseModuleLayer;
            if (_interactableLayer >= 0 && _interactableLayer < 32)
                layerMask |= 1 << _interactableLayer;
            if (_voxelLayer >= 0 && _voxelLayer < 32)
                layerMask |= 1 << _voxelLayer;
            return layerMask;
        }

        private static void SortHitArbitrationHitsByDistance(int hitCount)
        {
            int safeCount = Mathf.Clamp(hitCount, 0, _hitArbitrationHits.Length);
            for (int i = 1; i < safeCount; i++)
            {
                RaycastHit key = _hitArbitrationHits[i];
                float keyDistance = key.distance;
                int j = i - 1;
                while (j >= 0 && _hitArbitrationHits[j].distance > keyDistance)
                {
                    _hitArbitrationHits[j + 1] = _hitArbitrationHits[j];
                    j--;
                }

                _hitArbitrationHits[j + 1] = key;
            }
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
            if (!_scheduledRaycastActive)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledRaycastHandle, forceComplete: false))
                return;

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
                NativeMemorySentinel.UnregisterNativeArray(_scheduledCommands);
                if (_scheduledRaycastActive)
                    _scheduledCommands.Dispose(_scheduledRaycastHandle);
                else
                    _scheduledCommands.Dispose();

                _scheduledCommands = default;
            }

            if (_scheduledHits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scheduledHits);
                if (_scheduledRaycastActive)
                    _scheduledHits.Dispose(_scheduledRaycastHandle);
                else
                    _scheduledHits.Dispose();

                _scheduledHits = default;
            }

            if (_stagingCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_stagingCommands);
                _stagingCommands.Dispose();
                _stagingCommands = default;
            }

            if (_stagingHits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_stagingHits);
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
