using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Acoustic payload emitted when electrolysis dumps excess grid power into surrounding water.
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
    /// Dumps excess electrical power into seawater to generate oxygen for one submarine room.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton/Gameplay/Submarine Electrolysis Module")]
    public sealed class SubmarineElectrolysisModule : MonoBehaviour, ISlowTickable
    {
        private const float SlowTickDeltaTime = 0.5f;

        [Header("── References ──────────────────")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;
        [SerializeField] private PowerNode powerNode;

        [Header("── Output ──────────────────")]
        [Tooltip("Target room index that receives generated oxygen.")]
        [SerializeField, Min(0)] private int targetRoomIndex;

        [Tooltip("Upper bound on excess power converted into electrolysis heat and oxygen.")]
        [SerializeField, Min(0f)] private float maxDumpPowerWatts = 1800f;

        [Tooltip("Reference-gas-volume oxygen units produced per kilowatt-second of dumped power.")]
        [SerializeField, Min(0f)] private float oxygenUnitsPerKilowattSecond = 0.02f;

        [Header("── Consequence ──────────────────")]
        [Tooltip("Threat radius applied to the local ocean threat grid when electrolysis boils hard.")]
        [SerializeField, Min(1f)] private float threatRadiusMeters = 55f;

        [Tooltip("Threat-grid strength injected when the module is at full dump power.")]
        [SerializeField, Min(0f)] private float threatStrengthAtFullDump = 90f;

        [Tooltip("How long the threat pulse persists after each dump step.")]
        [SerializeField, Min(0.1f)] private float threatHoldSeconds = 2.5f;

        [Tooltip("Upward convection speed injected into the abyssal flow field.")]
        [SerializeField, Min(0f)] private float thermalUpdraftMetersPerSecond = 4f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private float _debugLastDumpedPowerWatts;
        [SerializeField] private float _debugLastOxygenUnits;
        [SerializeField] private float _debugLastThreatStrength;

        private Transform _cachedTransform;
        private bool _registered;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        public void SlowTick()
        {
            CacheReferences();
            PowerGrid grid = powerNode != null ? powerNode.Grid : null;
            if (grid == null || atmosphereSystem == null)
                return;

            float dumpedPowerWatts = math.min(math.max(0f, grid.Balance), math.max(0f, maxDumpPowerWatts));
            if (dumpedPowerWatts <= 0f)
                return;

            float oxygenUnits = (dumpedPowerWatts * SlowTickDeltaTime * 0.001f) * math.max(0f, oxygenUnitsPerKilowattSecond);
            if (oxygenUnits <= 0f)
                return;

            atmosphereSystem.InjectOxygenUnits(targetRoomIndex, oxygenUnits);

            float dumpRatio = dumpedPowerWatts / math.max(1f, maxDumpPowerWatts);
            float threatStrength = math.max(0f, threatStrengthAtFullDump) * dumpRatio;
            Vector3 position = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null)
            {
                bridge.ApplyExternalThreatPulse(position, threatRadiusMeters, threatStrength, threatHoldSeconds);
                bridge.RegisterSwarmWakeImpulse(position, Vector3.up * (thermalUpdraftMetersPerSecond * dumpRatio), threatRadiusMeters, threatHoldSeconds);
            }

            ElectrolysisAcousticEvent acousticEvent = new ElectrolysisAcousticEvent(
                position,
                dumpedPowerWatts,
                oxygenUnits,
                threatStrength,
                threatRadiusMeters);
            ElectrolysisAcousticEvents.Notify(in acousticEvent);

            _debugLastDumpedPowerWatts = dumpedPowerWatts;
            _debugLastOxygenUnits = oxygenUnits;
            _debugLastThreatStrength = threatStrength;
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (powerNode == null)
                TryGetComponent(out powerNode);

            if (atmosphereSystem == null)
                atmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();
        }

        private void TryRegister()
        {
            if (_registered)
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
    }
}
