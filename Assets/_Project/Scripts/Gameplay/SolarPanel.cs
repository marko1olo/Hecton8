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
    public sealed class SolarPanel : MonoBehaviour, IPowerComponent, ITickable
    {
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

        // Pre-allocated raycast hit for sky check
        private RaycastHit _skyHit;

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
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

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

            // Linear interpolation
            float t = (depth - minDepth) / (maxEffectiveDepth - minDepth);
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
            float dayProgress = hour - dayStartHour;

            // Ramp up in first 20% of day, ramp down in last 20%
            float rampDuration = dayLength * 0.2f;

            if (dayProgress < rampDuration)
            {
                return dayProgress / rampDuration;
            }
            else if (dayProgress > dayLength - rampDuration)
            {
                return (dayLength - dayProgress) / rampDuration;
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

            if (UnityEngine.Physics.Raycast(origin, direction, out _skyHit, skyCheckDistance, obstructionLayers, QueryTriggerInteraction.Ignore))
            {
                // Obstructed - reduce power based on distance to obstruction
                float obstructionDistance = _skyHit.distance;
                return Mathf.Clamp01(obstructionDistance / skyCheckDistance * 0.5f);
            }

            return 1f; // Clear sky
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
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0 && (obstructionLayers & (1 << waterLayer)) != 0)
            {
                obstructionLayers &= ~(1 << waterLayer);
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
