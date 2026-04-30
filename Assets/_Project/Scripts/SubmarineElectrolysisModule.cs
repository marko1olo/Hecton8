using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Acoustic payload emitted when electrolysis boils surrounding water.
    /// </summary>
    public readonly struct ElectrolysisAcousticEvent
    {
        public ElectrolysisAcousticEvent(Vector3 position, float dumpedPowerWatts, float oxygenUnits, float threatStrength, float radiusMeters)
        {
            Position = position;
            DumpedPowerWatts = dumpedPowerWatts;
            OxygenUnits = oxygenUnits;
            ThreatStrength = threatStrength;
            RadiusMeters = radiusMeters;
        }

        public Vector3 Position { get; }
        public float DumpedPowerWatts { get; }
        public float OxygenUnits { get; }
        public float ThreatStrength { get; }
        public float RadiusMeters { get; }
    }

    public static class ElectrolysisAcousticEvents
    {
        public delegate void ElectrolysisAcousticEventHandler(in ElectrolysisAcousticEvent acousticEvent);

        public static event ElectrolysisAcousticEventHandler OnElectrolysisAcoustic;

        public static void Notify(in ElectrolysisAcousticEvent acousticEvent)
        {
            OnElectrolysisAcoustic?.Invoke(acousticEvent);
        }
    }

    /// <summary>
    /// Grid-powered electrolysis stack that converts local seawater into breathable oxygen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton/Gameplay/Submarine Electrolysis Module")]
    public sealed class SubmarineElectrolysisModule : MonoBehaviour, ISlowTickable, IPowerComponent
    {
        private const float SlowTickDeltaTime = 0.5f;

        [Header("References")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;
        [SerializeField] private BaseModule hostModule;
        [SerializeField] private PowerNode powerNode;

        [Header("Process")]
        [Tooltip("Target room index that receives generated oxygen.")]
        [SerializeField, Min(0)] private int targetRoomIndex;

        [Tooltip("Continuous electrical draw while the electrolysis stack is active.")]
        [SerializeField, Min(0f)] private float powerDrawWatts = 500000f;

        [Tooltip("Priority used when industrial loads start getting shed by the grid.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 12;

        [Tooltip("Minimum local flood volume required before the stack can source internal water.")]
        [SerializeField, Min(0f)] private float minimumFloodWaterVolumeCubicMeters = 0.05f;

        [Tooltip("If true, the module may run while dry as long as the external ocean runtime is available.")]
        [SerializeField] private bool allowOceanWaterFallback = true;

        [Tooltip("Reference-gas-volume oxygen units produced per kilowatt-second of electrical input.")]
        [SerializeField, Min(0f)] private float oxygenUnitsPerKilowattSecond = 0.02f;

        [Tooltip("Direct room-temperature rise applied each SlowTick while electrolysis is active.")]
        [SerializeField, Min(0f)] private float temperatureRisePerSlowTickCelsius = 10f;

        [Header("Consequence")]
        [Tooltip("Threat radius applied to the local ocean threat grid when electrolysis boils hard.")]
        [SerializeField, Min(1f)] private float threatRadiusMeters = 55f;

        [Tooltip("Threat-grid strength injected each SlowTick while electrolysis is active.")]
        [SerializeField, Min(0f)] private float threatStrength = 90f;

        [Tooltip("How long the threat pulse persists after each electrolysis step.")]
        [SerializeField, Min(0.1f)] private float threatHoldSeconds = 2.5f;

        [Tooltip("Upward convection speed injected into the abyssal flow field.")]
        [SerializeField, Min(0f)] private float thermalUpdraftMetersPerSecond = 4f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugHasWaterSource;
        [SerializeField] private bool _debugIsOperating;
        [SerializeField] private float _debugLastDumpedPowerWatts;
        [SerializeField] private float _debugLastOxygenUnits;
        [SerializeField] private float _debugLastThreatStrength;

        private Transform _cachedTransform;
        private bool _hasPower = true;
        private bool _hasWaterSource;
        private bool _isOperating;
        private bool _registered;

        /// <inheritdoc />
        public float PowerRating => _isOperating ? -math.max(0f, powerDrawWatts) : 0f;

        /// <inheritdoc />
        public int PowerPriority => powerPriority;

        /// <inheritdoc />
        public bool HasPower => _hasPower;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            TryStartRuntimeLifecycle();
        }

        private void Start()
        {
            TryStartRuntimeLifecycle();
        }

        private void TryStartRuntimeLifecycle()
        {
            CacheReferences();
            if (!CanUseRuntimeDispatcher())
                return;

            TryRegister();
            _hasWaterSource = ResolveWaterSourceAvailability();
            _debugHasWaterSource = _hasWaterSource;
            _debugHasPower = _hasPower;
            _debugIsOperating = _isOperating;
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            if (atmosphereSystem == null || powerNode == null)
                return;

            bool nextWaterSource = ResolveWaterSourceAvailability();
            _hasWaterSource = nextWaterSource;
            _debugHasWaterSource = nextWaterSource;

            bool nextOperating = _hasPower && nextWaterSource;
            if (_isOperating != nextOperating)
            {
                _isOperating = nextOperating;
                _debugIsOperating = nextOperating;
                NotifyGridBalanceChanged();
            }

            if (!_isOperating)
                return;

            float consumedPowerWatts = math.max(0f, powerDrawWatts);
            float oxygenUnits = (consumedPowerWatts * SlowTickDeltaTime * 0.001f) * math.max(0f, oxygenUnitsPerKilowattSecond);
            if (oxygenUnits <= 0f)
                return;

            atmosphereSystem.InjectOxygenUnits(targetRoomIndex, oxygenUnits);
            atmosphereSystem.InjectRoomTemperatureDeltaCelsius(targetRoomIndex, math.max(0f, temperatureRisePerSlowTickCelsius));

            Vector3 position = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null)
            {
                bridge.ApplyExternalThreatPulse(position, threatRadiusMeters, threatStrength, threatHoldSeconds);
                bridge.RegisterSwarmWakeImpulse(position, Vector3.up * math.max(0f, thermalUpdraftMetersPerSecond), threatRadiusMeters, threatHoldSeconds);
            }

            ElectrolysisAcousticEvent acousticEvent = new ElectrolysisAcousticEvent(
                position,
                consumedPowerWatts,
                oxygenUnits,
                threatStrength,
                threatRadiusMeters);
            ElectrolysisAcousticEvents.Notify(in acousticEvent);

            _debugLastDumpedPowerWatts = consumedPowerWatts;
            _debugLastOxygenUnits = oxygenUnits;
            _debugLastThreatStrength = threatStrength;
        }

        /// <inheritdoc />
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (hasPower || !_isOperating)
                return;

            _isOperating = false;
            _debugIsOperating = false;
            NotifyGridBalanceChanged();
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (powerNode == null)
                TryGetComponent(out powerNode);

            if (hostModule == null)
                hostModule = GetComponent<BaseModule>() ?? GetComponentInParent<BaseModule>();

            if (atmosphereSystem == null)
                atmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();

            if (fluidDynamics == null && atmosphereSystem != null)
                fluidDynamics = atmosphereSystem.GetComponent<SubmarineFluidDynamics>();
        }

        private bool ResolveWaterSourceAvailability()
        {
            if (fluidDynamics != null &&
                targetRoomIndex >= 0 &&
                targetRoomIndex < fluidDynamics.CompartmentCount &&
                fluidDynamics.GetCompartmentFloodVolumeCubicMeters(targetRoomIndex) >= math.max(0f, minimumFloodWaterVolumeCubicMeters))
            {
                return true;
            }

            if (hostModule != null && hostModule.IsFlooded)
                return true;

            if (!allowOceanWaterFallback)
                return false;

            IHectonOceanKinematicsService oceanService = GlobalRegistry.OceanKinematics;
            return oceanService != null && oceanService.ActiveProvider != null;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = powerNode != null ? powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!CanUseRuntimeDispatcher())
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private static bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
                return false;
#endif

            return true;
        }
    }
}
