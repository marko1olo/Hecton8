using Hecton8.Caves;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
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
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const int MaxQueuedSignals = 256;
        private const int MaxInteractionPacketsPerFrame = 256;
        private const int MaxQueuedRayRequests = 64;
        private const int MaxParentResolveDepth = 32;
        private const int MinCommandsPerJob = 1;
        private const int MaxCompletedRaycastAgeFrames = 1;
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
        // COLD ALLOC: int[64] - frame stamps for completed frame-latent raycast results - owner: EquipmentInteractionHandler
        private readonly int[] _completedHitFrames = new int[MaxQueuedRayRequests];
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
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;

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
        public bool IsServiceReady => _isInitialized && _serviceRegistered && ReferenceEquals(GlobalRegistry.InteractionSignals, this);

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
            if (!_signalQueue.IsCreated || targetCollider == null || !IsValidSignal(in signal))
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
            Vector3 absoluteOrigin = new Vector3(packet.Origin.x, packet.Origin.y, packet.Origin.z);
            Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(absoluteOrigin);
            Vector3 direction = new Vector3(packet.Direction.x, packet.Direction.y, packet.Direction.z);
            return TryRaycastPrimary(requesterId, origin, direction, packet.Range, layerMask, queryTriggerInteraction, out hit);
        }

        /// <inheritdoc />
        public bool TryRaycastPrimary(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit)
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

            bool hasCompletedHit = TryGetCompletedRaycast(requesterId, Time.frameCount, out hit);
            Vector3 normalizedDirection = NormalizeFinite(direction, Vector3.forward);
            QueuePrimaryRaycast(requesterId, origin, normalizedDirection, range, layerMask, queryTriggerInteraction);
            return hasCompletedHit;
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
                PrewarmQueue(ref _signalQueue, MaxQueuedSignals);
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

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
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
            _isInitialized = false;

            ClearQueuedSignals();
            if (_signalQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EquipmentInteractionHandler), nameof(_signalQueue));
                _signalQueue.Dispose();
                _signalQueue = default;
            }

            DisposeRaycastBuffers();
            _scheduledRaycastHandle = default;
            _scheduledRaycastActive = false;
            _scheduledRequestCount = 0;
            _stagedRequestCount = 0;
            _completedResultCount = 0;
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

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            _dispatcherRegistered = _lateFrameRegistered;
        }

        private void TryUnregisterFromDispatcher()
        {
            if (!_dispatcherRegistered)
                return;

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            _dispatcherRegistered = false;
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
            int depth = 0;
            while (current != null && depth < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out ITransportPlatform platform) && platform.PlatformTransform != null)
                {
                    platformTransform = platform.PlatformTransform;
                    return true;
                }

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
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

            if (!targetCollider.TryGetComponent(out HectonVoxelVolume volume))
                TryResolveParentComponent(targetCollider.transform, out volume);

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

            if (!TryResolveParentComponent(targetCollider.transform, out BaseModule _))
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
                direction = NormalizeFinite(direction, Vector3.forward);

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

            return true;
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

            return TryResolveParentComponent(targetCollider.transform, out signalConsumer);
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

            return TryResolveParentComponent(targetCollider.transform, out vulnerabilitySource);
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

            return TryResolveParentComponent(targetCollider.transform, out cuttable);
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component)
        {
            component = default;
            Transform current = start;
            int depth = 0;
            while (current != null && depth < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool IsValidHit(Vector3 origin, Vector3 direction, float range, int layerMask, RaycastHit hit)
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

        private void QueuePrimaryRaycast(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            if (!_stagingCommands.IsCreated || !_stagingHits.IsCreated)
                return;

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

        private bool TryGetCompletedRaycast(ulong requesterId, int currentFrame, out RaycastHit hit)
        {
            hit = default;
            if (requesterId == 0UL)
                return false;

            for (int i = 0; i < _completedResultCount; i++)
            {
                if (_completedRequesterIds[i] != requesterId)
                    continue;

                if (currentFrame - _completedHitFrames[i] > MaxCompletedRaycastAgeFrames)
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
                _completedHitFrames[i] = Time.frameCount;
                _scheduledRequesterIds[i] = 0UL;
            }

            for (int i = _scheduledRequestCount; i < MaxQueuedRayRequests; i++)
            {
                _completedRequesterIds[i] = 0UL;
                _completedHasHit[i] = false;
                _completedHits[i] = default;
                _completedHitFrames[i] = -1;
            }

            _scheduledRequestCount = 0;
            _scheduledRaycastActive = false;
            ResetCommandLane(_scheduledCommands);
        }

        private void ScheduleStagedRaycasts()
        {
            if (_scheduledRaycastActive || _stagedRequestCount <= 0)
                return;

            int scheduledCount = _stagedRequestCount;
            NativeArray<RaycastCommand> commandBatch = _stagingCommands.GetSubArray(0, scheduledCount);
            NativeArray<RaycastHit> hitBatch = _stagingHits.GetSubArray(0, scheduledCount);
            _scheduledRaycastHandle = RaycastCommand.ScheduleBatch(commandBatch, hitBatch, MinCommandsPerJob, default);
            _scheduledRaycastActive = true;
            _scheduledRequestCount = scheduledCount;

            NativeArray<RaycastCommand> scheduledCommands = _scheduledCommands;
            _scheduledCommands = _stagingCommands;
            _stagingCommands = scheduledCommands;

            NativeArray<RaycastHit> scheduledHits = _scheduledHits;
            _scheduledHits = _stagingHits;
            _stagingHits = scheduledHits;

            System.Array.Copy(_stagingRequesterIds, _scheduledRequesterIds, scheduledCount);
            System.Array.Clear(_stagingRequesterIds, 0, scheduledCount);

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
