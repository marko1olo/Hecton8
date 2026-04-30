using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Acoustic payload raised by repair drones while their weld torch is active.
    /// </summary>
    public readonly struct RepairDroneTorchAcousticEvent
    {
        public RepairDroneTorchAcousticEvent(Vector3 position, AudioClip clip, float volume, float pitch)
        {
            Position = position;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
        }

        public Vector3 Position { get; }
        public AudioClip Clip { get; }
        public float Volume { get; }
        public float Pitch { get; }
    }

    /// <summary>
    /// Static event bridge that lets the audio owner consume repair-torch pulses without scene scans.
    /// </summary>
    public static class RepairDroneTorchAcousticEvents
    {
        public delegate void RepairDroneTorchAcousticEventHandler(in RepairDroneTorchAcousticEvent acousticEvent);

        public static event RepairDroneTorchAcousticEventHandler OnTorchAcoustic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnTorchAcoustic = null;
        }

        public static void Notify(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            OnTorchAcoustic?.Invoke(acousticEvent);
        }
    }

    /// <summary>
    /// Pooled repair drone that routes through the hybrid navgrid, consumes finite solder and battery charge,
    /// and follows fleet-level swarm arbitration instead of isolated per-hub heuristics.
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
            Return = 3,
            Docking = 4,
            Stasis = 5,
            ResupplyTravel = 6
        }

        private const int MaxRouteWaypointCount = 16;
        private const float CorridorCohesionWeight = 0.1f;
        private const float OpenWaterCohesionWeight = 0.8f;
        private const float RouteWaypointReachDistance = 0.65f;
        private const float IntegrityPercentPerSolderUnit = 10f;
        private const float MinimumWeldDirectionEpsilon = 0.0001f;
        private const float MinimumDockingDurationSeconds = 1f;
        private const float ParasiteCutDamage = 9999f;
        private const float WarningFlashPeriodSeconds = 0.5f;

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

        [Tooltip("How often the drone can rebuild its macro route while the mission is active.")]
        [SerializeField, Range(0.1f, 3f)] private float repathIntervalSeconds = 0.6f;

        [Tooltip("Returning drones stop free-flight steering and enter the docking spline once they get this close to the hub.")]
        [SerializeField, Range(0.5f, 4f)] private float dockApproachDistance = 2f;

        [Tooltip("Empty drones gain this much cruise-speed bonus versus a fully loaded deployment leg.")]
        [SerializeField, Range(0f, 0.5f)] private float emptyCargoSpeedBonus = 0.3f;

        [Header("── Swarm Avoidance ───────────────────")]
        [Tooltip("Preferred separation radius from other repair drones.")]
        [SerializeField, Range(0.25f, 6f)] private float droneSeparationRadius = 1.8f;

        [Tooltip("Preferred separation radius from the player.")]
        [SerializeField, Range(0.5f, 8f)] private float playerSeparationRadius = 2.5f;

        [Tooltip("Neighbor radius used for fleet alignment and cohesion steering.")]
        [SerializeField, Range(2f, 16f)] private float boidPerceptionRadius = 8f;

        [Header("── Repair Profile ────────────────────")]
        [Tooltip("Fallback repair rate applied when the hub does not override mission throughput.")]
        [SerializeField, Range(1f, 100f)] private float repairRatePerSecond = 18f;

        [Tooltip("Normalized weld power passed into the additive DDA repair stamp.")]
        [SerializeField, Range(0.1f, 1f)] private float weldPowerNormalized = 0.65f;

        [Tooltip("Maximum repair-beam travel used by the additive DDA weld stamp.")]
        [SerializeField, Range(0.5f, 12f)] private float weldRangeMeters = 3.5f;

        [Header("── Battery Profile ───────────────────")]
        [Tooltip("Normalized battery threshold below which the drone aborts field work and returns to dock.")]
        [SerializeField, Range(0.05f, 0.9f)] private float lowBatteryThreshold = 0.2f;

        [Tooltip("Battery drain per second while traveling.")]
        [SerializeField, Range(0.001f, 1f)] private float travelBatteryDrainPerSecond = 0.025f;

        [Tooltip("Battery drain per second while welding.")]
        [SerializeField, Range(0.001f, 1f)] private float repairBatteryDrainPerSecond = 0.04f;

        [Tooltip("Battery drain per second while returning to dock.")]
        [SerializeField, Range(0.001f, 1f)] private float returnBatteryDrainPerSecond = 0.02f;

        [Header("── Docking & Repair Feedback ───────────")]
        [Tooltip("Optional authored docked pose socket. Falls back to the drone root when omitted.")]
        [SerializeField] private Transform repairNozzle;

        [Tooltip("Authored spark particle system attached to the drone nozzle. Reused in-place, never instantiated at runtime.")]
        [SerializeField] private ParticleSystem repairSparkVfx;

        [Tooltip("Short pooled torch clip re-fired through SpatialAudioManager while the weld is active.")]
        [SerializeField] private AudioClip repairTorchClip;

        [Tooltip("Optional authored yellow warning light enabled when the drone cannot acquire solder.")]
        [SerializeField] private Light supplyWarningLight;

        [Tooltip("Seconds between pooled weld-torch acoustic pulses while repairing.")]
        [SerializeField, Range(0.05f, 0.5f)] private float repairTorchEventIntervalSeconds = 0.18f;

        [Tooltip("Volume passed into the pooled weld-torch acoustic pulses.")]
        [SerializeField, Range(0f, 1f)] private float repairTorchVolume = 0.42f;

        [Tooltip("Pitch passed into the pooled weld-torch acoustic pulses.")]
        [SerializeField, Range(0.5f, 2f)] private float repairTorchPitch = 1.18f;

        [SerializeField] private bool _debugMissionActive;
        [SerializeField] private string _debugState = "Idle";
        [SerializeField] private float _debugBatteryNormalized = 1f;
        [SerializeField] private int _debugSolderUnitsRemaining;

        // COLD ALLOC: Vector3[16] — macro-portal waypoint scratch for hybrid navgrid routing — owner: RepairDroneEntity
        private readonly Vector3[] _routeWaypoints = new Vector3[MaxRouteWaypointCount];

        private Transform _cachedTransform;
        private Rigidbody _rigidbody;
        private RepairDroneHub _hub;
        private BaseModule _target;
        private HectonVoxelVolume _targetVoxelVolume;
        private DroneFleetTaskKind _taskKind;
        private bool _registered;
        private float _activeRepairRate;
        private float _batteryNormalized = 1f;
        private float _repairPercentAccumulator;
        private float _repathTimer;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Vector3 _taskPosition;
        private Vector3 _supplyEndpointPosition;
        private DroneMissionState _state;
        private float _taskRadius;
        private int _routeWaypointCount;
        private int _routeWaypointIndex;
        private int _solderUnitsRemaining;
        private int _loadedSolderUnitCapacity;
        private float _dockBlendElapsed;
        private float _repairTorchEventTimer;
        private float _supplyWarningTimer;
        private Vector3 _dockBlendStartPosition;
        private Vector3 _dockBlendTargetPosition;
        private Quaternion _dockBlendStartRotation;
        private Quaternion _dockBlendTargetRotation;
        private bool _supplyWarningActive;

        /// <summary>True while the drone still owns a live mission.</summary>
        public bool HasActiveMission => _state != DroneMissionState.Idle;

        /// <summary>Current target assigned by the hub.</summary>
        public BaseModule CurrentTarget => _target;

        /// <summary>Current mission battery state used by fleet diagnostics.</summary>
        public float BatteryNormalized => _batteryNormalized;

        internal bool IsSwarmAvoidanceParticipant => isActiveAndEnabled && _state != DroneMissionState.Idle;

        internal Vector3 SwarmPosition => _cachedTransform != null ? _cachedTransform.position : transform.position;

        internal Vector3 SwarmVelocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;

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
            StopRepairFeedback();
            DroneFleetManager.UnregisterActiveDrone(this);
            TryUnregister();
        }

        public void OnSpawn()
        {
            TryRegister();
            ResetRuntimeState();
            DroneFleetManager.RegisterActiveDrone(this);
        }

        public void OnDespawn()
        {
            DroneFleetManager.UnregisterActiveDrone(this);
            TryUnregister();
            ResetRuntimeState();
        }

        public void Tick(float dt)
        {
            if (_supplyWarningActive)
                TickSupplyWarning(dt);

            if (_state == DroneMissionState.Idle || _state == DroneMissionState.Stasis)
                return;

            if (_state == DroneMissionState.Docking)
            {
                TickDocking(dt);
                return;
            }

            DrainBattery(dt);
            if (_state == DroneMissionState.Stasis || _state == DroneMissionState.Idle)
                return;

            UpdateRouteProgress();
            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                RebuildRoute();
                _repathTimer = repathIntervalSeconds;
            }

            switch (_state)
            {
                case DroneMissionState.Travel:
                    if (TryReachRepairPosition())
                    {
                        _state = DroneMissionState.Repair;
                        _debugState = _state.ToString();
                        _repairTorchEventTimer = 0f;
                    }
                    break;

                case DroneMissionState.Repair:
                    TickRepair(dt);
                    break;

                case DroneMissionState.Return:
                    if (TryBeginDocking() || HasReachedHome())
                    {
                        if (_state != DroneMissionState.Docking)
                            CompleteMission(false);
                    }
                    break;

                case DroneMissionState.ResupplyTravel:
                    if (TryReachSupplyEndpoint())
                        CompleteResupply();
                    break;
            }
        }

        public void FixedTick(float fdt)
        {
            if (_state == DroneMissionState.Idle ||
                _state == DroneMissionState.Stasis ||
                _state == DroneMissionState.Docking ||
                _rigidbody == null)
            {
                return;
            }

            Vector3 currentPosition = _cachedTransform.position;
            Vector3 destination = ResolveDestination();
            float stopDistance = _state == DroneMissionState.Return ? returnStopDistance : serviceRadius;
            Vector3 offset = destination - currentPosition;
            float distance = offset.magnitude;

            if (distance <= stopDistance)
            {
                _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, Vector3.zero, acceleration * fdt);
                return;
            }

            Vector3 pathDirection = offset / Mathf.Max(distance, 0.0001f);
            float cohesionWeight = ResolveCohesionWeight();
            Vector3 steering = DroneFleetManager.ResolveSwarmSteering(
                this,
                currentPosition,
                _rigidbody.linearVelocity,
                pathDirection,
                boidPerceptionRadius,
                droneSeparationRadius,
                playerSeparationRadius,
                cohesionWeight);
            if (steering.sqrMagnitude <= 0.0001f)
                steering = pathDirection;

            Vector3 direction = steering.normalized;
            float speedScale = distance < arrivalSlowdownDistance
                ? Mathf.Clamp01(distance / Mathf.Max(arrivalSlowdownDistance, 0.01f))
                : 1f;
            float targetSpeed = Mathf.Max(0.2f, cruiseSpeed * ResolveCargoSpeedMultiplier() * DroneFleetManager.ResolveThrusterSpeedMultiplier() * speedScale);
            Vector3 desiredVelocity = direction * targetSpeed;
            _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, desiredVelocity, acceleration * fdt);

            if (_rigidbody.linearVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(_rigidbody.linearVelocity.normalized, Vector3.up);
                Quaternion nextRotation = Quaternion.Slerp(
                    _cachedTransform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-(turnSharpness * ResolveCargoSpeedMultiplier()) * fdt));
                _rigidbody.MoveRotation(nextRotation);
            }
        }

        /// <summary>Assigns a fresh mission to this pooled drone.</summary>
        public void AssignMission(RepairDroneHub hub, BaseModule target, Vector3 homePosition, float repairRateOverride, int loadedSolderUnits)
        {
            Vector3 taskPosition = target != null ? target.transform.position : homePosition;
            DroneFleetTask task = new DroneFleetTask(DroneFleetTaskKind.RepairModule, target, taskPosition, 0f);
            AssignMission(hub, in task, homePosition, repairRateOverride, loadedSolderUnits);
        }

        internal void AssignMission(RepairDroneHub hub, in DroneFleetTask task, Vector3 homePosition, float repairRateOverride, int loadedSolderUnits)
        {
            _hub = hub;
            _target = task.Module;
            _taskKind = task.Kind;
            _taskPosition = task.Position;
            _taskRadius = task.Radius;
            _supplyEndpointPosition = homePosition;
            _homePosition = homePosition;
            _homeRotation = hub != null ? hub.DockRotation : _cachedTransform.rotation;
            _activeRepairRate = repairRateOverride > 0f ? repairRateOverride : repairRatePerSecond;
            _batteryNormalized = 1f;
            _repairPercentAccumulator = 0f;
            _solderUnitsRemaining = Mathf.Max(0, loadedSolderUnits);
            _loadedSolderUnitCapacity = Mathf.Max(1, _solderUnitsRemaining);
            _targetVoxelVolume = TryResolveTargetVoxelVolume(_target);
            _state = task.IsValid ? DroneMissionState.Travel : DroneMissionState.Return;
            _debugMissionActive = _state != DroneMissionState.Idle;
            _debugState = _state.ToString();
            _debugBatteryNormalized = _batteryNormalized;
            _debugSolderUnitsRemaining = _solderUnitsRemaining;
            _routeWaypointCount = 0;
            _routeWaypointIndex = 0;
            _repathTimer = 0f;
            _dockBlendElapsed = 0f;
            _repairTorchEventTimer = 0f;
            StopRepairFeedback();
            SetSupplyWarning(false);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            RebuildRoute();
            DroneFleetManager.NotifyFleetStateChanged();
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
            StopRepairFeedback();
            _hub = null;
            _target = null;
            _targetVoxelVolume = null;
            _taskKind = DroneFleetTaskKind.None;
            _taskPosition = Vector3.zero;
            _taskRadius = 0f;
            _supplyEndpointPosition = _homePosition;
            _activeRepairRate = repairRatePerSecond;
            _batteryNormalized = 1f;
            _repairPercentAccumulator = 0f;
            _solderUnitsRemaining = 0;
            _loadedSolderUnitCapacity = 0;
            _homePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            _homeRotation = _cachedTransform != null ? _cachedTransform.rotation : Quaternion.identity;
            _state = DroneMissionState.Idle;
            _routeWaypointCount = 0;
            _routeWaypointIndex = 0;
            _repathTimer = 0f;
            _dockBlendElapsed = 0f;
            _repairTorchEventTimer = 0f;
            _debugMissionActive = false;
            _debugState = DroneMissionState.Idle.ToString();
            _debugBatteryNormalized = 1f;
            _debugSolderUnitsRemaining = 0;
            SetSupplyWarning(false);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
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

        private void TickRepair(float dt)
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                BeginReturn();
                return;
            }

            TickRepairFeedback(dt);

            if (_taskKind == DroneFleetTaskKind.CutParasite)
            {
                TickParasiteCut();
                return;
            }

            float recoverableIntegrity = Mathf.Max(1f, _target.MaxRecoverableIntegrity);
            if (DroneFleetManager.ConsumeFleetSacrificeFlag() && (_target.CurrentIntegrity / recoverableIntegrity) <= 0.05f)
            {
                ExecuteSacrificePatch();
                return;
            }

            if (_solderUnitsRemaining <= 0)
            {
                BeginResupply();
                return;
            }

            float integrityBefore = _target.CurrentIntegrity;
            _target.Repair(_activeRepairRate * dt);
            DispatchAdditiveRepairWeld();

            float restoredIntegrity = Mathf.Max(0f, _target.CurrentIntegrity - integrityBefore);
            if (restoredIntegrity > 0f)
            {
                float restoredPercent = (restoredIntegrity / recoverableIntegrity) * 100f;
                _repairPercentAccumulator += restoredPercent;
                while (_repairPercentAccumulator >= IntegrityPercentPerSolderUnit && _solderUnitsRemaining > 0)
                {
                    _repairPercentAccumulator -= IntegrityPercentPerSolderUnit;
                    _solderUnitsRemaining--;
                }

                _debugSolderUnitsRemaining = _solderUnitsRemaining;
            }

            if (IsMissionComplete())
                BeginReturn();
            else if (_solderUnitsRemaining <= 0)
                BeginResupply();
        }

        private void DrainBattery(float dt)
        {
            float drainPerSecond = 0f;
            switch (_state)
            {
                case DroneMissionState.Travel:
                case DroneMissionState.ResupplyTravel:
                    drainPerSecond = travelBatteryDrainPerSecond;
                    break;
                case DroneMissionState.Repair:
                    drainPerSecond = repairBatteryDrainPerSecond;
                    break;
                case DroneMissionState.Return:
                    drainPerSecond = returnBatteryDrainPerSecond;
                    break;
            }

            if (drainPerSecond <= 0f)
                return;

            _batteryNormalized = Mathf.Max(0f, _batteryNormalized - (drainPerSecond * DroneFleetManager.ResolveBatteryDrainMultiplier() * dt));
            _debugBatteryNormalized = _batteryNormalized;

            if (_batteryNormalized > lowBatteryThreshold)
                return;

            bool canReturnToPoweredDock = _hub != null && _hub.HasOperationalPower;
            if (canReturnToPoweredDock)
                BeginReturn();
            else
                EnterStasis();
        }

        private void RebuildRoute()
        {
            _routeWaypointCount = 0;
            _routeWaypointIndex = 0;

            Vector3 destination = ResolveDirectDestination();
            if (VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc(_cachedTransform.position, destination, _routeWaypoints, out int waypointCount))
            {
                _routeWaypointCount = waypointCount;
                _routeWaypointIndex = waypointCount > 1 ? 1 : 0;
            }
        }

        private void UpdateRouteProgress()
        {
            if (_routeWaypointCount <= 0 || _routeWaypointIndex < 0 || _routeWaypointIndex >= _routeWaypointCount)
                return;

            Vector3 offset = _routeWaypoints[_routeWaypointIndex] - _cachedTransform.position;
            if (offset.sqrMagnitude > RouteWaypointReachDistance * RouteWaypointReachDistance)
                return;

            _routeWaypointIndex++;
            if (_routeWaypointIndex >= _routeWaypointCount)
                _routeWaypointIndex = _routeWaypointCount - 1;
        }

        private Vector3 ResolveDestination()
        {
            if (_routeWaypointCount > 0 && _routeWaypointIndex >= 0 && _routeWaypointIndex < _routeWaypointCount)
                return _routeWaypoints[_routeWaypointIndex];

            return ResolveDirectDestination();
        }

        private Vector3 ResolveDirectDestination()
        {
            if (_state == DroneMissionState.ResupplyTravel)
                return _supplyEndpointPosition;

            if (_state == DroneMissionState.Return || _target == null)
                return _homePosition;

            if (_taskKind == DroneFleetTaskKind.CutParasite)
            {
                Vector3 parasiteStandOff = _taskPosition;
                parasiteStandOff.y += hoverHeight;
                return parasiteStandOff;
            }

            Vector3 targetPosition = _target.transform.position;
            targetPosition.y += hoverHeight;
            return targetPosition;
        }

        private bool TryReachRepairPosition()
        {
            if (_target == null)
                return false;

            Vector3 offset = ResolveDirectDestination() - _cachedTransform.position;
            return offset.sqrMagnitude <= serviceRadius * serviceRadius;
        }

        private bool TryReachSupplyEndpoint()
        {
            Vector3 offset = _supplyEndpointPosition - _cachedTransform.position;
            return offset.sqrMagnitude <= serviceRadius * serviceRadius;
        }

        private bool TryBeginDocking()
        {
            Vector3 dockOffset = ResolveDockPosition() - _cachedTransform.position;
            if (dockOffset.sqrMagnitude > dockApproachDistance * dockApproachDistance)
                return false;

            _state = DroneMissionState.Docking;
            _debugState = _state.ToString();
            _dockBlendElapsed = 0f;
            _dockBlendStartPosition = _cachedTransform.position;
            _dockBlendTargetPosition = ResolveDockPosition();
            _dockBlendStartRotation = _cachedTransform.rotation;
            _dockBlendTargetRotation = ResolveDockRotation();
            StopRepairFeedback();

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            DroneFleetManager.NotifyFleetStateChanged();
            return true;
        }

        private void TickDocking(float dt)
        {
            _dockBlendElapsed = Mathf.Min(MinimumDockingDurationSeconds, _dockBlendElapsed + dt);
            float t = MinimumDockingDurationSeconds > 0f
                ? Mathf.Clamp01(_dockBlendElapsed / MinimumDockingDurationSeconds)
                : 1f;

            float3 position = math.lerp(_dockBlendStartPosition, _dockBlendTargetPosition, t);
            quaternion rotation = math.slerp((quaternion)_dockBlendStartRotation, (quaternion)_dockBlendTargetRotation, t);
            _cachedTransform.SetPositionAndRotation(position, rotation);

            if (_dockBlendElapsed >= MinimumDockingDurationSeconds)
                CompleteMission(false);
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

            if (_taskKind == DroneFleetTaskKind.CutParasite)
                return _target.ParasiteInfectionLevel <= 0.0001f;

            return _target.CurrentIntegrity >= _target.MaxRecoverableIntegrity && !_target.IsFlooded;
        }

        private void BeginReturn()
        {
            StopRepairFeedback();
            SetSupplyWarning(false);
            _target = null;
            _targetVoxelVolume = null;
            _taskKind = DroneFleetTaskKind.None;
            _state = DroneMissionState.Return;
            _debugState = _state.ToString();
            _repathTimer = 0f;
            RebuildRoute();
            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void BeginResupply()
        {
            StopRepairFeedback();
            if (_hub == null || !_hub.TryResolveNearestSupplyEndpoint(_cachedTransform.position, out _supplyEndpointPosition))
            {
                EnterStasis(true);
                return;
            }

            SetSupplyWarning(false);
            _state = DroneMissionState.ResupplyTravel;
            _debugState = _state.ToString();
            _repathTimer = 0f;
            RebuildRoute();
            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void CompleteResupply()
        {
            if (_hub == null)
            {
                EnterStasis(true);
                return;
            }

            int requestedUnits = Mathf.Max(1, _loadedSolderUnitCapacity - _solderUnitsRemaining);
            if (!_hub.TryAcquireDroneResupply(requestedUnits, out int grantedUnits) || grantedUnits <= 0)
            {
                EnterStasis(true);
                return;
            }

            _solderUnitsRemaining += grantedUnits;
            _loadedSolderUnitCapacity = Mathf.Max(_loadedSolderUnitCapacity, _solderUnitsRemaining);
            _debugSolderUnitsRemaining = _solderUnitsRemaining;
            SetSupplyWarning(false);

            if (_target != null && _target.gameObject.activeInHierarchy && !IsMissionComplete())
                _state = DroneMissionState.Travel;
            else
                _state = DroneMissionState.Return;

            _debugState = _state.ToString();
            _repathTimer = 0f;
            RebuildRoute();
            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void EnterStasis(bool supplyWarning = false)
        {
            StopRepairFeedback();
            SetSupplyWarning(supplyWarning);
            _target = null;
            _targetVoxelVolume = null;
            _taskKind = DroneFleetTaskKind.None;
            _state = DroneMissionState.Stasis;
            _debugState = _state.ToString();
            _debugMissionActive = true;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void TickParasiteCut()
        {
            if (_solderUnitsRemaining <= 0)
            {
                BeginResupply();
                return;
            }

            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager == null)
            {
                BeginReturn();
                return;
            }

            Vector3 nozzlePosition = ResolveNozzlePosition();
            Vector3 cutDirection = _taskPosition - nozzlePosition;
            bool applied = floraInteractionManager.TryApplyDroneParasiteCut(
                _taskPosition,
                cutDirection,
                ParasiteCutDamage,
                weldPowerNormalized);

            if (applied)
            {
                _solderUnitsRemaining--;
                _debugSolderUnitsRemaining = _solderUnitsRemaining;
            }

            BeginReturn();
        }

        private void ExecuteSacrificePatch()
        {
            if (_target != null)
            {
                float missingIntegrity = Mathf.Max(0f, _target.MaxRecoverableIntegrity - _target.CurrentIntegrity);
                if (missingIntegrity > 0f)
                    _target.Repair(missingIntegrity);

                if (_target.IsFlooded)
                    _target.ForceDrainComplete();

                DispatchAdditiveRepairWeld();
            }

            CompleteMission(true);
        }

        private void TickRepairFeedback(float dt)
        {
            if (repairSparkVfx != null && !repairSparkVfx.isPlaying)
                repairSparkVfx.Play(true);

            if (repairTorchClip == null || repairTorchEventIntervalSeconds <= 0f)
                return;

            _repairTorchEventTimer -= dt;
            if (_repairTorchEventTimer > 0f)
                return;

            _repairTorchEventTimer = repairTorchEventIntervalSeconds;
            RepairDroneTorchAcousticEvent acousticEvent = new RepairDroneTorchAcousticEvent(
                ResolveNozzlePosition(),
                repairTorchClip,
                repairTorchVolume,
                repairTorchPitch);
            RepairDroneTorchAcousticEvents.Notify(in acousticEvent);
        }

        private void StopRepairFeedback()
        {
            _repairTorchEventTimer = 0f;
            if (repairSparkVfx != null)
                repairSparkVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void SetSupplyWarning(bool active)
        {
            _supplyWarningActive = active;
            _supplyWarningTimer = 0f;
            if (supplyWarningLight == null)
                return;

            supplyWarningLight.color = Color.yellow;
            supplyWarningLight.enabled = active;
        }

        private void TickSupplyWarning(float dt)
        {
            if (supplyWarningLight == null)
                return;

            _supplyWarningTimer += dt;
            if (_supplyWarningTimer < WarningFlashPeriodSeconds)
                return;

            _supplyWarningTimer = 0f;
            supplyWarningLight.enabled = !supplyWarningLight.enabled;
        }

        private void DispatchAdditiveRepairWeld()
        {
            if (_targetVoxelVolume == null || _target == null)
                return;

            Vector3 nozzlePosition = ResolveNozzlePosition();
            Vector3 weldDirection = _target.transform.position - nozzlePosition;
            if (weldDirection.sqrMagnitude <= MinimumWeldDirectionEpsilon)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(nozzlePosition + (weldDirection.normalized * 0.35f));
            _targetVoxelVolume.ApplyRepairWeldDda(
                absoluteHitPoint,
                weldDirection.normalized,
                weldPowerNormalized,
                weldRangeMeters);
        }

        private HectonVoxelVolume TryResolveTargetVoxelVolume(BaseModule target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out HectonVoxelVolume localVolume))
                return localVolume;

            return target.GetComponentInParent<HectonVoxelVolume>();
        }

        private float ResolveCohesionWeight()
        {
            return VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(_cachedTransform.position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                   sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel
                ? CorridorCohesionWeight
                : OpenWaterCohesionWeight;
        }

        private float ResolveCargoSpeedMultiplier()
        {
            if (_loadedSolderUnitCapacity <= 0)
                return 1f;

            float load01 = Mathf.Clamp01((float)_solderUnitsRemaining / _loadedSolderUnitCapacity);
            return 1f + ((1f - load01) * emptyCargoSpeedBonus);
        }

        private Vector3 ResolveNozzlePosition()
        {
            return repairNozzle != null ? repairNozzle.position : _cachedTransform.position;
        }

        private Vector3 ResolveDockPosition()
        {
            return _hub != null ? _hub.DockPosition : _homePosition;
        }

        private Quaternion ResolveDockRotation()
        {
            return _hub != null ? _hub.DockRotation : _homeRotation;
        }

        private void CompleteMission(bool droneDestroyed)
        {
            StopRepairFeedback();
            _state = DroneMissionState.Idle;
            _debugMissionActive = false;
            _debugState = DroneMissionState.Idle.ToString();

            if (_hub != null)
                _hub.NotifyDroneReturned(this);

            if (droneDestroyed)
                DroneFleetManager.ReportDroneDestroyed();
            else
                DroneFleetManager.NotifyFleetStateChanged();

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
                pool.Despawn(gameObject);
        }
    }
}
