using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Pooled repair drone that flies from a hub to a damaged BaseModule, performs field repair, then returns home.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton8/Construction/Repair Drone Entity")]
    public sealed class RepairDroneEntity : MonoBehaviour, ITickable, IUpdatable, IFixedTickable, IPoolable
    {
        private enum DroneMissionState : byte
        {
            Idle = 0,
            Travel = 1,
            Repair = 2,
            Return = 3
        }

        [Header("── Flight Profile ─────────────────────")]
        [Tooltip("Cruise speed while moving between hub and target.")]
        [SerializeField, Range(0.5f, 30f)] private float cruiseSpeed = 6.5f;

        [Tooltip("How quickly current velocity converges toward the requested flight vector.")]
        [SerializeField, Range(0.5f, 40f)] private float acceleration = 14f;

        [Tooltip("How close the drone must get before it can start service work.")]
        [SerializeField, Range(0.2f, 4f)] private float serviceRadius = 1.1f;

        [Tooltip("Hover offset above the module while repairs are running.")]
        [SerializeField, Range(0f, 3f)] private float hoverHeight = 0.8f;

        [Tooltip("Distance from hub at which the drone settles and despawns.")]
        [SerializeField, Range(0.1f, 2f)] private float returnStopDistance = 0.4f;

        [Tooltip("Extra slow-down envelope used to avoid hard overshoot on arrival.")]
        [SerializeField, Range(0.2f, 6f)] private float arrivalSlowdownDistance = 1.75f;

        [Tooltip("How quickly the drone body yaws toward its current travel vector.")]
        [SerializeField, Range(0.5f, 30f)] private float turnSharpness = 12f;

        [Header("── Repair Profile ─────────────────────")]
        [Tooltip("Fallback repair rate applied when the hub does not override mission throughput.")]
        [SerializeField, Range(1f, 100f)] private float repairRatePerSecond = 18f;

        [SerializeField] private bool _debugMissionActive;
        [SerializeField] private string _debugState = "Idle";

        private Transform _cachedTransform;
        private Rigidbody _rigidbody;
        private RepairDroneHub _hub;
        private BaseModule _target;
        private bool _registered;
        private float _activeRepairRate;
        private Vector3 _homePosition;
        private DroneMissionState _state;

        /// <summary>True while the drone still owns a live mission.</summary>
        public bool HasActiveMission => _state != DroneMissionState.Idle;

        /// <summary>Current target assigned by the hub.</summary>
        public BaseModule CurrentTarget => _target;

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _rigidbody);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        public void OnSpawn()
        {
            TryRegister();
            ResetRuntimeState();
        }

        public void OnDespawn()
        {
            TryUnregister();
            ResetRuntimeState();
        }

        public void Tick(float dt)
        {
            if (_state == DroneMissionState.Idle)
                return;

            if (_state == DroneMissionState.Travel)
            {
                if (TryReachRepairPosition())
                    _state = DroneMissionState.Repair;

                return;
            }

            if (_state == DroneMissionState.Repair)
            {
                if (_target == null || !_target.gameObject.activeInHierarchy)
                {
                    BeginReturn();
                    return;
                }

                _target.Repair(_activeRepairRate * dt);

                if (IsMissionComplete())
                    BeginReturn();

                return;
            }

            if (_state == DroneMissionState.Return && HasReachedHome())
                CompleteMission();
        }

        public void FixedTick(float fdt)
        {
            if (_state == DroneMissionState.Idle || _rigidbody == null)
                return;

            Vector3 destination = ResolveDestination();
            float stopDistance = _state == DroneMissionState.Return ? returnStopDistance : serviceRadius;
            Vector3 offset = destination - _cachedTransform.position;
            float distance = offset.magnitude;

            if (distance <= stopDistance)
            {
                _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, Vector3.zero, acceleration * fdt);
                return;
            }

            Vector3 direction = offset / Mathf.Max(distance, 0.0001f);
            float speedScale = distance < arrivalSlowdownDistance
                ? Mathf.Clamp01(distance / Mathf.Max(arrivalSlowdownDistance, 0.01f))
                : 1f;
            float targetSpeed = Mathf.Max(0.2f, cruiseSpeed * speedScale);
            Vector3 desiredVelocity = direction * targetSpeed;
            _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, desiredVelocity, acceleration * fdt);

            if (_rigidbody.linearVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(_rigidbody.linearVelocity.normalized, Vector3.up);
                Quaternion nextRotation = Quaternion.Slerp(
                    _cachedTransform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-turnSharpness * fdt));
                _rigidbody.MoveRotation(nextRotation);
            }
        }

        /// <summary>Assigns a fresh mission to this pooled drone.</summary>
        public void AssignMission(RepairDroneHub hub, BaseModule target, Vector3 homePosition, float repairRateOverride)
        {
            _hub = hub;
            _target = target;
            _homePosition = homePosition;
            _activeRepairRate = repairRateOverride > 0f ? repairRateOverride : repairRatePerSecond;
            _state = target != null ? DroneMissionState.Travel : DroneMissionState.Return;
            _debugMissionActive = _state != DroneMissionState.Idle;
            _debugState = _state.ToString();

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Interrupts the current mission and sends the drone back to the hub.</summary>
        public void AbortMission()
        {
            if (_state == DroneMissionState.Idle)
                return;

            BeginReturn();
        }

        private void ResetRuntimeState()
        {
            _hub = null;
            _target = null;
            _activeRepairRate = repairRatePerSecond;
            _homePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            _state = DroneMissionState.Idle;
            _debugMissionActive = false;
            _debugState = DroneMissionState.Idle.ToString();

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private Vector3 ResolveDestination()
        {
            if (_state == DroneMissionState.Return || _target == null)
                return _homePosition;

            Vector3 targetPosition = _target.transform.position;
            targetPosition.y += hoverHeight;
            return targetPosition;
        }

        private bool TryReachRepairPosition()
        {
            if (_target == null)
                return false;

            Vector3 offset = ResolveDestination() - _cachedTransform.position;
            return offset.sqrMagnitude <= serviceRadius * serviceRadius;
        }

        private bool HasReachedHome()
        {
            Vector3 offset = _homePosition - _cachedTransform.position;
            return offset.sqrMagnitude <= returnStopDistance * returnStopDistance;
        }

        private bool IsMissionComplete()
        {
            if (_target == null)
                return true;

            return _target.CurrentIntegrity >= _target.MaxRecoverableIntegrity && !_target.IsFlooded;
        }

        private void BeginReturn()
        {
            _target = null;
            _state = DroneMissionState.Return;
            _debugState = _state.ToString();
        }

        private void CompleteMission()
        {
            _state = DroneMissionState.Idle;
            _debugMissionActive = false;
            _debugState = DroneMissionState.Idle.ToString();

            if (_hub != null)
                _hub.NotifyDroneReturned(this);

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
                pool.Despawn(gameObject);
        }
    }
}
