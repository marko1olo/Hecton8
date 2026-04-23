using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Authoritative queued interaction owner for tool hit queries and late-frame signal dispatch.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9935)]
    public sealed class EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService
    {
        private const int MaxQueuedSignals = 256;
        private const int MaxRayHits = 8;
        private const int MaxSpatialHits = 16;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinHitDistance = 0.05f;
        private const float MaxBroadPhaseRadius = 12f;

        private static EquipmentInteractionHandler _instance;

        // COLD ALLOC: RaycastHit[8] - shared narrow-phase beam query buffer - owner: EquipmentInteractionHandler
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[MaxRayHits];
        // COLD ALLOC: SpatialQueryHit[16] - shared broad-phase tool contact buffer - owner: EquipmentInteractionHandler
        private readonly SpatialQueryHit[] _spatialBuffer = new SpatialQueryHit[MaxSpatialHits];
        // COLD ALLOC: Collider[256] - queued target side-channel aligned with the native interaction queue - owner: EquipmentInteractionHandler
        private readonly Collider[] _queuedTargetColliders = new Collider[MaxQueuedSignals];

        private NativeQueue<InteractionSignal> _signalQueue;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;
        private bool _isInitialized;

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
                return;

            GlobalRegistry.RegisterInteractionSignalService(this);
            _isInitialized = true;
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
        public bool TryRaycastPrimary(Vector3 origin, Vector3 direction, float range, int layerMask, out RaycastHit hit)
        {
            hit = default;
            if (range <= 0f || direction.sqrMagnitude < MinDirectionSqr)
                return false;

            Vector3 normalizedDirection = direction.normalized;
            WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                Mathf.Min(range * 0.5f, MaxBroadPhaseRadius),
                SpatialTargetKind.Resource | SpatialTargetKind.Bioform | SpatialTargetKind.Module,
                _spatialBuffer);

            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                normalizedDirection,
                _raycastBuffer,
                range,
                layerMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return false;

            SortRayHitsByDistance(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _raycastBuffer[i];
                if (!IsValidHit(origin, normalizedDirection, range, layerMask, candidate))
                    continue;

                hit = candidate;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void ClearQueuedSignals()
        {
            if (_signalQueue.IsCreated)
            {
                while (_signalQueue.TryDequeue(out _))
                {
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

            if (!_signalQueue.IsCreated)
            {
                _signalQueue = new NativeQueue<InteractionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionSignal>(Persistent) - deferred interaction signal bus - owner: EquipmentInteractionHandler
            }

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void LateUpdate()
        {
            FlushSignals();
        }

        private void OnDestroy()
        {
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterInteractionSignalService(this);
                _isInitialized = false;
            }

            ClearQueuedSignals();
            if (_signalQueue.IsCreated)
                _signalQueue.Dispose();

            if (_instance == this)
                _instance = null;
        }

        private void FlushSignals()
        {
            while (_queueCount > 0 && _signalQueue.TryDequeue(out InteractionSignal signal))
            {
                Collider targetCollider = _queuedTargetColliders[_queueHead];
                _queuedTargetColliders[_queueHead] = null;
                _queueHead = (_queueHead + 1) % MaxQueuedSignals;
                _queueCount--;

                DispatchSignal(signal, targetCollider);
            }
        }

        private static void DispatchSignal(InteractionSignal signal, Collider targetCollider)
        {
            switch ((InteractionEffectType)signal.EffectType)
            {
                case InteractionEffectType.PlasmaCut:
                    if (DispatchPlasmaCut(signal, targetCollider))
                        return;

                    DispatchCutDamage(signal, targetCollider);
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

        private static void DispatchCutDamage(InteractionSignal signal, Collider targetCollider)
        {
            if (targetCollider == null || signal.PowerDelivered <= 0f)
                return;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(new Vector3(signal.HitPoint.x, signal.HitPoint.y, signal.HitPoint.z));
            if (TryResolveCuttable(targetCollider, out ICuttable cuttable))
                cuttable.ApplyCutDamage(signal.PowerDelivered, runtimeHitPoint);
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

        private void SortRayHitsByDistance(int hitCount)
        {
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit key = _raycastBuffer[i];
                int j = i - 1;
                while (j >= 0 && _raycastBuffer[j].distance > key.distance)
                {
                    _raycastBuffer[j + 1] = _raycastBuffer[j];
                    j--;
                }

                _raycastBuffer[j + 1] = key;
            }
        }
    }
}
