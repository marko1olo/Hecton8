// ============================================================================
// HECTON-8 — SolarPanel.cs
// Base power generator that produces energy from sunlight.
//
// ARCHITECTURE:
//   • IPowerComponent for power grid integration
//   • ITickable for production calculation (no Update)
//   • MaterialPropertyBlock for status indicator (zero GC)
//   • Depth and time-of-day based production
//
// PRODUCTION FORMULA:
//   powerOutput = basePower * depthFactor * timeFactor * skyClearFactor
//
// INTEGRATION:
//   • IPowerComponent.PowerRating returns current production
//   • UnityEvent for UI/status updates
// ============================================================================

using Hecton8.Core;
using Hecton8.Power;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Solar panel power generator.
    /// Implements IPowerComponent for power grid integration.
    /// Production depends on depth, time of day, and sky visibility.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Hecton/Gameplay/Solar Panel")]
    public sealed class SolarPanel : MonoBehaviour, IPowerComponent, ITickable, IUpdatable
    {
        private static readonly int _WaterLayer;

        static SolarPanel()
        {
            _WaterLayer = LayerMask.NameToLayer("Water");
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Power Settings ─────────────────────────────")]
        [Tooltip("Maximum power output in full sunlight at surface (Watts).")]
        [SerializeField, Range(10f, 500f)] private float basePower = 200f;

        [Tooltip("Depth at which power production drops to 10% (meters).")]
        [SerializeField, Range(10f, 200f)] private float maxEffectiveDepth = 100f;

        [Tooltip("Minimum depth for full power production (meters).")]
        [SerializeField, Range(0f, 50f)] private float minDepth = 5f;

        [Header("── Time of Day ────────────────────────────────")]
        [Tooltip("Use Unity's ambient light intensity for day/night cycle.")]
        [SerializeField] private bool useAmbientLight = true;

        [Tooltip("Day start hour (0-24). Power ramps up from here.")]
        [SerializeField, Range(0f, 12f)] private float dayStartHour = 6f;

        [Tooltip("Day end hour (0-24). Power ramps down after here.")]
        [SerializeField, Range(12f, 24f)] private float dayEndHour = 18f;

        [Tooltip("Current hour override (for testing). Ignored if useAmbientLight is true.")]
        [SerializeField, Range(0f, 24f)] private float debugHour = 12f;

        [Header("── Sky Visibility ─────────────────────────────")]
        [Tooltip("Check for sky obstruction using raycast.")]
        [SerializeField] private bool checkSkyVisibility = true;

        [Tooltip("Maximum distance to check for sky obstruction.")]
        [SerializeField, Range(10f, 500f)] private float skyCheckDistance = 200f;

        [Tooltip("Layers considered as sky obstruction.")]
        [SerializeField] private LayerMask obstructionLayers = -1;

        [Header("── Status Indicator ───────────────────────────")]
        [Tooltip("Renderer for the status indicator light.")]
        [SerializeField] private Renderer statusIndicator;

        [Tooltip("Material property for indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when producing power.")]
        [SerializeField] private Color producingColor = new Color(1f, 0.9f, 0.2f);

        [Tooltip("Color when not producing (night/obstructed).")]
        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f);

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when power output changes. Parameter: current power (Watts).")]
        [SerializeField] private UnityEvent<float> OnPowerChanged;

        [Tooltip("Fired when production state changes. Parameter: isProducing.")]
        [SerializeField] private UnityEvent<bool> OnProductionStateChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _currentPower;
        private float _depthFactor;
        private float _timeFactor;
        private float _skyFactor = 1f;
        private bool _isProducing;
        private bool _wasProducing;
        private bool _registered;
        private bool _hasPower = true; // IPowerComponent requirement
        private int _emissionPropertyId;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // COLD ALLOC: RaycastHit[4] - synchronous sky-occlusion probe buffer - owner: SolarPanel
        private readonly RaycastHit[] _skyHitBuffer = new RaycastHit[4];

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current power output (positive = generation).
        /// </summary>
        public float PowerRating => _currentPower;

        /// <summary>
        /// Priority: generators are never disconnected.
        /// </summary>
        public int PowerPriority => 0;

        /// <summary>
        /// Always true for generators.
        /// </summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Called by PowerGrid. Generators ignore this.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            // Generators don't respond to power status changes
            _hasPower = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>True if currently producing power.</summary>
        public bool IsProducing => _isProducing;

        /// <summary>Current power output in Watts.</summary>
        public float CurrentPower => _currentPower;

        /// <summary>Depth factor (0-1).</summary>
        public float DepthFactor => _depthFactor;

        /// <summary>Time of day factor (0-1).</summary>
        public float TimeFactor => _timeFactor;

        /// <summary>Sky visibility factor (0-1).</summary>
        public float SkyFactor => _skyFactor;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: SolarPanel

            if (statusIndicator == null)
                statusIndicator = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            TryRegister();

            UpdateProduction();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — PRODUCTION CALCULATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Updates power production.
        /// Zero GC: no allocations, uses cached values.
        /// </summary>
        public void Tick(float deltaTime)
        {
            UpdateProduction();
        }

        // ══════════════════════════════════════════════════════════
        //  PRODUCTION LOGIC
        // ══════════════════════════════════════════════════════════

        private void UpdateProduction()
        {
            // Calculate depth factor
            float depth = -_cachedTransform.position.y; // Assuming Y=0 is water surface
            _depthFactor = CalculateDepthFactor(depth);

            // Calculate time of day factor
            _timeFactor = CalculateTimeFactor();

            // Calculate sky visibility factor
            if (checkSkyVisibility)
            {
                _skyFactor = CalculateSkyFactor();
            }

            // Calculate final power output
            float previousPower = _currentPower;
            _currentPower = basePower * _depthFactor * _timeFactor * _skyFactor;

            // Update production state
            _isProducing = _currentPower > 0.1f;

            // Fire events if changed
            if (Mathf.Abs(_currentPower - previousPower) > 0.1f)
            {
                OnPowerChanged?.Invoke(_currentPower);
            }

            if (_isProducing != _wasProducing)
            {
                _wasProducing = _isProducing;
                OnProductionStateChanged?.Invoke(_isProducing);
            }

            // Update visual indicator
            UpdateStatusIndicator();
        }

        /// <summary>
        /// Calculates depth factor (1.0 at surface, 0.1 at maxEffectiveDepth).
        /// </summary>
        private float CalculateDepthFactor(float depth)
        {
            if (depth <= minDepth)
                return 1f;

            if (depth >= maxEffectiveDepth)
                return 0.1f;

            if (!TryResolveSafeReciprocal(maxEffectiveDepth - minDepth, out float inverseDepthRange))
                return 0.1f;

            float t = (depth - minDepth) * inverseDepthRange;
            return Mathf.Lerp(1f, 0.1f, t);
        }

        /// <summary>
        /// Calculates time of day factor based on sun position or debug value.
        /// </summary>
        private float CalculateTimeFactor()
        {
            float hour;

            if (useAmbientLight)
            {
                // Use sun position to determine time
                Light sun = RenderSettings.sun;
                if (sun != null)
                {
                    // Sun angle above horizon determines time factor
                    float sunAngle = Vector3.Dot(sun.transform.forward, Vector3.down);
                    return Mathf.Clamp01(sunAngle);
                }

                // Fallback: use system time (for testing)
                hour = (System.DateTime.Now.Hour + System.DateTime.Now.Minute / 60f);
            }
            else
            {
                hour = debugHour;
            }

            // Calculate time factor based on hour
            if (hour < dayStartHour || hour > dayEndHour)
            {
                return 0f; // Night
            }

            // Smooth ramp up/down at day boundaries
            float dayLength = dayEndHour - dayStartHour;
            if (!TryResolveSafeReciprocal(dayLength * 0.2f, out float inverseRampDuration))
                return 0f;

            float dayProgress = hour - dayStartHour;
            float rampDuration = dayLength * 0.2f;

            if (dayProgress < rampDuration)
            {
                return dayProgress * inverseRampDuration;
            }
            else if (dayProgress > dayLength - rampDuration)
            {
                return (dayLength - dayProgress) * inverseRampDuration;
            }

            return 1f; // Full day
        }

        /// <summary>
        /// Checks if the panel has clear sky above.
        /// </summary>
        private float CalculateSkyFactor()
        {
            Vector3 origin = _cachedTransform.position;
            Vector3 direction = Vector3.up;
            if (!TryResolveSafeReciprocal(skyCheckDistance, out float inverseSkyDistance))
                return 1f;

            if (TryResolveNearestSkyHit(origin, direction, skyCheckDistance, out RaycastHit hit))
            {
                // Obstructed - reduce power based on distance to obstruction
                return Mathf.Clamp01(hit.distance * inverseSkyDistance * 0.5f);
            }

            return 1f; // Clear sky
        }

        private bool TryResolveNearestSkyHit(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit nearestHit)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                direction,
                _skyHitBuffer,
                maxDistance,
                obstructionLayers,
                QueryTriggerInteraction.Ignore);

            nearestHit = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _skyHitBuffer[i];
                if (candidate.collider == null || float.IsNaN(candidate.distance) || float.IsInfinity(candidate.distance))
                    continue;

                if (candidate.distance >= nearestDistance)
                    continue;

                nearestDistance = candidate.distance;
                nearestHit = candidate;
            }

            return nearestHit.collider != null;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return !float.IsNaN(reciprocal) && !float.IsInfinity(reciprocal);
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the status indicator using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateStatusIndicator()
        {
            if (statusIndicator == null)
                return;

            Color indicatorColor;

            if (_isProducing)
            {
                // Interpolate based on power level
                float intensity = _currentPower / basePower;
                indicatorColor = Color.Lerp(inactiveColor, producingColor, intensity);
            }
            else
            {
                indicatorColor = inactiveColor;
            }

            statusIndicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            statusIndicator.SetPropertyBlock(_mpb);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (basePower < 1f) basePower = 1f;
            if (maxEffectiveDepth < minDepth) maxEffectiveDepth = minDepth + 10f;
            if (skyCheckDistance < 1f) skyCheckDistance = 1f;

            // Ensure Water layer is excluded from obstruction check
            if (_WaterLayer >= 0 && (obstructionLayers & (1 << _WaterLayer)) != 0)
            {
                obstructionLayers &= ~(1 << _WaterLayer);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw sky check ray
            if (checkSkyVisibility)
            {
                Gizmos.color = _skyFactor > 0.5f ? new Color(1f, 0.9f, 0.2f, 0.5f) : new Color(1f, 0.2f, 0.2f, 0.5f);
                Vector3 origin = transform.position;
                Gizmos.DrawLine(origin, origin + Vector3.up * skyCheckDistance);
            }

            // Draw production info
            if (Application.isPlaying)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Power: {_currentPower:F1}W\nDepth: {-transform.position.y:F1}m\nTime: {_timeFactor:P0}"
                );
            }
        }
#endif
    }
}
