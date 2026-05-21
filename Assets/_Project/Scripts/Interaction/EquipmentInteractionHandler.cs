using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
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
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
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
        private static ISubmarineRuntimeContext s_submarineRuntimeContext;
        private static IOrganicToolHitService s_organicToolHits;

        // COLD ALLOC: Collider[256] - queued target side-channel aligned with the vault interaction queue - owner: EquipmentInteractionHandler
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
        // COLD ALLOC: Transform[256] - platform-local hit point side-channel aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Transform[] _queuedPlatformTransforms = new Transform[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit points aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitPoints = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: Vector3[256] - local platform hit normals aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly Vector3[] _queuedPlatformLocalHitNormals = new Vector3[MaxQueuedSignals];
        // COLD ALLOC: bool[256] - platform-local hit validity bits aligned with the vault signal queue - owner: EquipmentInteractionHandler
        private readonly bool[] _queuedHasPlatformLocalHit = new bool[MaxQueuedSignals];

        private IDataVault _dataVault;
        private VaultGenerationHandle<InteractionSignal> _signalQueueHandle;
        private VaultGenerationHandle<RaycastCommand> _scheduledCommandsHandle;
        private VaultGenerationHandle<RaycastHit> _scheduledHitsHandle;
        private VaultGenerationHandle<RaycastCommand> _stagingCommandsHandle;
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
        private bool _scheduledRaycastVaultLocked;
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
        public bool IsServiceReady => _isInitialized && _serviceRegistered && ReferenceEquals(GlobalRegistry.InteractionSignals, this);

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            CacheRegistryDependenciesCold();
            TryRegisterHotSwapListenerCold();
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
            if (vault == null || !vault.TryLockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools))
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
                vault.TryUnlockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools);
            }
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

            bool hasCompletedHit = TryGetCompletedRaycast(requesterId, ResolveSimulationFrameIndex(), out hit);
            Vector3 normalizedDirection = NormalizeFinite(direction, Vector3.forward);
            QueuePrimaryRaycast(requesterId, origin, normalizedDirection, range, layerMask, queryTriggerInteraction);
            return hasCompletedHit;
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

            if (vault != null && canClearVaultQueue && vault.TryLockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools))
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
                    vault.TryUnlockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools);
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

            if (EnsureRaycastBufferHandles(createIfMissing: true))
            {
                IDataVault vault = ResolveDataVault();
                ResetCommandLaneLocked(
                    vault,
                    ref _scheduledCommandsHandle,
                    BufferID.InteractionRaycastScheduledCommands);
                ResetCommandLaneLocked(
                    vault,
                    ref _stagingCommandsHandle,
                    BufferID.InteractionRaycastStagingCommands);
            }
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            CacheRegistryDependenciesCold();
            TryRegisterHotSwapListenerCold();

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
            TryUnregisterHotSwapListenerCold();
            _isInitialized = false;

            ClearQueuedSignals(createVaultLane: false);

            ReleaseInteractionVaultDescriptor(_dataVault, ref _signalQueueHandle);
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

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            var originAup = GlobalSignals.CurrentRuntimeOriginAup();
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

            if (!targetCollider.TryGetComponent(out IVoxelPlasmaCutTarget volume))
                TryResolveParentComponent(targetCollider.transform, out volume);

            if (volume == null)
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

            if (!TryResolveParentComponent(targetCollider.transform, out IBaseModuleInteractionHost _))
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
            if (!EnsureRaycastBufferHandles(createIfMissing: false))
                return;

            int requestIndex = FindStagedRequestIndex(requesterId);
            bool newRequest = requestIndex < 0;
            if (requestIndex < 0)
            {
                if (_stagedRequestCount >= MaxQueuedRayRequests)
                    return;

                requestIndex = _stagedRequestCount;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null || !vault.TryLockBuffer(BufferID.InteractionRaycastStagingCommands, SystemID.GameplayTools))
                return;

            try
            {
                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _stagingCommandsHandle,
                        BufferID.InteractionRaycastStagingCommands,
                        MaxQueuedRayRequests,
                        out NativeArray<RaycastCommand> stagingCommands))
                {
                    return;
                }

                if (newRequest)
                {
                    _stagingRequesterIds[_stagedRequestCount] = requesterId;
                    _stagedRequestCount++;
                }

                stagingCommands[requestIndex] = CreateRaycastCommand(origin, direction, range, layerMask, queryTriggerInteraction);
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.InteractionRaycastStagingCommands, SystemID.GameplayTools);
            }
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

            if (!EnsureRaycastBufferHandles(createIfMissing: false))
            {
                UnlockScheduledRaycastVaultBuffers();
                _scheduledRequestCount = 0;
                _scheduledRaycastActive = false;
                return;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                UnlockScheduledRaycastVaultBuffers();
                _scheduledRequestCount = 0;
                _scheduledRaycastActive = false;
                return;
            }

            if (!TryOpenExistingInteractionVaultBuffer(
                    vault,
                    ref _scheduledCommandsHandle,
                    BufferID.InteractionRaycastScheduledCommands,
                    MaxQueuedRayRequests,
                    out NativeArray<RaycastCommand> scheduledCommands) ||
                !TryOpenExistingInteractionVaultBuffer(
                    vault,
                    ref _scheduledHitsHandle,
                    BufferID.InteractionRaycastScheduledHits,
                    MaxQueuedRayRequests,
                    out NativeArray<RaycastHit> scheduledHits))
            {
                UnlockScheduledRaycastVaultBuffers();
                _scheduledRequestCount = 0;
                _scheduledRaycastActive = false;
                return;
            }

            _completedResultCount = _scheduledRequestCount;

            int completionFrame = ResolveSimulationFrameIndex();
            for (int i = 0; i < _scheduledRequestCount; i++)
            {
                RaycastCommand command = scheduledCommands[i];
                RaycastHit candidate = scheduledHits[i];
                int layerMask = command.queryParameters.layerMask;
                _completedRequesterIds[i] = _scheduledRequesterIds[i];
                _completedHasHit[i] = IsValidHit(command.from, command.direction, command.distance, layerMask, candidate);
                _completedHits[i] = _completedHasHit[i] ? candidate : default;
                _completedHitFrames[i] = completionFrame;
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
            for (int i = 0; i < scheduledCommands.Length; i++)
                scheduledCommands[i] = CreateInvalidRaycastCommand();
            UnlockScheduledRaycastVaultBuffers();
        }

        private void ScheduleStagedRaycasts()
        {
            if (_scheduledRaycastActive || _stagedRequestCount <= 0)
                return;

            if (!EnsureRaycastBufferHandles(createIfMissing: false))
                return;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            bool stagingCommandsLocked = false;
            bool scheduledCommandsLocked = false;
            bool scheduledHitsLocked = false;

            try
            {
                if (!vault.TryLockBuffer(BufferID.InteractionRaycastStagingCommands, SystemID.GameplayTools))
                    return;

                stagingCommandsLocked = true;

                if (!vault.TryLockBuffer(BufferID.InteractionRaycastScheduledCommands, SystemID.GameplayTools))
                    return;

                scheduledCommandsLocked = true;

                if (!vault.TryLockBuffer(BufferID.InteractionRaycastScheduledHits, SystemID.GameplayTools))
                    return;

                scheduledHitsLocked = true;

                if (!TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _stagingCommandsHandle,
                        BufferID.InteractionRaycastStagingCommands,
                        MaxQueuedRayRequests,
                        out NativeArray<RaycastCommand> stagingCommands) ||
                    !TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _scheduledCommandsHandle,
                        BufferID.InteractionRaycastScheduledCommands,
                        MaxQueuedRayRequests,
                        out NativeArray<RaycastCommand> scheduledCommands) ||
                    !TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref _scheduledHitsHandle,
                        BufferID.InteractionRaycastScheduledHits,
                        MaxQueuedRayRequests,
                        out NativeArray<RaycastHit> scheduledHits))
                {
                    return;
                }

                int scheduledCount = _stagedRequestCount;
                for (int i = 0; i < scheduledCommands.Length; i++)
                    scheduledCommands[i] = CreateInvalidRaycastCommand();

                for (int i = 0; i < scheduledCount; i++)
                    scheduledCommands[i] = stagingCommands[i];

                var commandBatch = scheduledCommands.GetSubArray(0, scheduledCount);
                var hitBatch = scheduledHits.GetSubArray(0, scheduledCount);
                _scheduledRaycastHandle = RaycastCommand.ScheduleBatch(commandBatch, hitBatch, MinCommandsPerJob, default);
                _scheduledRaycastActive = true;
                _scheduledRequestCount = scheduledCount;
                _scheduledRaycastVaultLocked = true;

                System.Array.Copy(_stagingRequesterIds, _scheduledRequesterIds, scheduledCount);
                System.Array.Clear(_stagingRequesterIds, 0, scheduledCount);
                for (int i = 0; i < stagingCommands.Length; i++)
                    stagingCommands[i] = CreateInvalidRaycastCommand();

                _stagedRequestCount = 0;
                scheduledCommandsLocked = false;
                scheduledHitsLocked = false;
            }
            finally
            {
                if (stagingCommandsLocked)
                    vault.TryUnlockBuffer(BufferID.InteractionRaycastStagingCommands, SystemID.GameplayTools);

                if (scheduledCommandsLocked)
                    vault.TryUnlockBuffer(BufferID.InteractionRaycastScheduledCommands, SystemID.GameplayTools);

                if (scheduledHitsLocked)
                    vault.TryUnlockBuffer(BufferID.InteractionRaycastScheduledHits, SystemID.GameplayTools);
            }
        }

        private void DisposeRaycastBuffers()
        {
            if (_scheduledRaycastActive)
                DispatcherJobSwap.TryComplete(ref _scheduledRaycastHandle, forceComplete: true);

            UnlockScheduledRaycastVaultBuffers();
            ReleaseInteractionVaultDescriptor(_dataVault, ref _scheduledCommandsHandle);
            ReleaseInteractionVaultDescriptor(_dataVault, ref _scheduledHitsHandle);
            ReleaseInteractionVaultDescriptor(_dataVault, ref _stagingCommandsHandle);
            _dataVault = null;
        }

        private void CacheRegistryDependenciesCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
            s_submarineRuntimeContext = GlobalRegistry.Submarine;
            s_organicToolHits = GlobalRegistry.OrganicToolHits;
        }

        private void RebindDataVaultCold(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            IDataVault oldVault = _dataVault;
            if (_scheduledRaycastActive)
                DispatcherJobSwap.TryComplete(ref _scheduledRaycastHandle, forceComplete: true);

            UnlockScheduledRaycastVaultBuffers();
            ReleaseAllInteractionVaultDescriptors(oldVault);
            _dataVault = dataVault;
            _scheduledRaycastActive = false;
            _scheduledRequestCount = 0;
            _stagedRequestCount = 0;

            if (_dataVault == null)
                return;

            EnsureSignalQueueHandle(createIfMissing: true);
            EnsureRaycastBufferHandles(createIfMissing: true);
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
            }
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private static int ResolveSimulationFrameIndex()
        {
            return unchecked((int)SystemDispatcher.CurrentFrameId);
        }

        private bool EnsureRaycastBufferHandles(bool createIfMissing)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureRaycastBufferHandle(
                    vault,
                    ref _scheduledCommandsHandle,
                    BufferID.InteractionRaycastScheduledCommands,
                    MaxQueuedRayRequests,
                    createIfMissing))
                return false;

            if (!EnsureRaycastBufferHandle(
                    vault,
                    ref _scheduledHitsHandle,
                    BufferID.InteractionRaycastScheduledHits,
                    MaxQueuedRayRequests,
                    createIfMissing))
                return false;

            if (!EnsureRaycastBufferHandle(
                    vault,
                    ref _stagingCommandsHandle,
                    BufferID.InteractionRaycastStagingCommands,
                    MaxQueuedRayRequests,
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
            if (vault == null || !vault.TryLockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools))
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
                vault.TryUnlockBuffer(BufferID.InteractionSignalQueue, SystemID.GameplayTools);
            }
        }

        private static bool EnsureRaycastBufferHandle<T>(
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
            if (vault == null || requiredLength <= 0 || !createIfMissing)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryOpenExistingInteractionVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.GetGenerationHandle<T>(
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

        private void UnlockScheduledRaycastVaultBuffers()
        {
            if (!_scheduledRaycastVaultLocked)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault != null)
            {
                vault.TryUnlockBuffer(BufferID.InteractionRaycastScheduledCommands, SystemID.GameplayTools);
                vault.TryUnlockBuffer(BufferID.InteractionRaycastScheduledHits, SystemID.GameplayTools);
            }

            _scheduledRaycastVaultLocked = false;
        }

        private static void ResetCommandLaneLocked(
            IDataVault vault,
            ref VaultGenerationHandle<RaycastCommand> handle,
            BufferID bufferId)
        {
            if (vault == null || !IsGameplayToolsVaultHandle(in handle, bufferId))
                return;

            if (!vault.TryLockBuffer(bufferId, SystemID.GameplayTools))
                return;

            try
            {
                if (TryOpenExistingInteractionVaultBuffer(
                        vault,
                        ref handle,
                        bufferId,
                        MaxQueuedRayRequests,
                        out NativeArray<RaycastCommand> commands))
                {
                    for (int i = 0; i < commands.Length; i++)
                        commands[i] = CreateInvalidRaycastCommand();
                }
            }
            finally
            {
                vault.TryUnlockBuffer(bufferId, SystemID.GameplayTools);
            }
        }

        private void ReleaseAllInteractionVaultDescriptors(IDataVault vault)
        {
            ReleaseInteractionVaultDescriptor(vault, ref _signalQueueHandle);
            ReleaseInteractionVaultDescriptor(vault, ref _scheduledCommandsHandle);
            ReleaseInteractionVaultDescriptor(vault, ref _scheduledHitsHandle);
            ReleaseInteractionVaultDescriptor(vault, ref _stagingCommandsHandle);
        }

        private static void ReleaseInteractionVaultDescriptor<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
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

    }
}
