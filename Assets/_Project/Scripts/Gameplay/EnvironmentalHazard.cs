// ============================================================================
// HECTON-8 — EnvironmentalHazard.cs
// Environmental hazard zone that damages the player.
//
// ARCHITECTURE:
//   • ITickable for periodic damage logic (no Update)
//   • Trigger-based or radius-based detection
//   • Distance-scaled damage intensity
//   • MaterialPropertyBlock for visual feedback (zero GC)
//
// HAZARD TYPES:
//   • Radiation — ionizing radiation damage
//   • Heat — high temperature damage
//   • Toxic — poisonous gas/liquid damage
//
// INTEGRATION:
//   • UnityEvent OnIntensityChanged for post-process effects
//   • UnityEvent OnDamageDealt for UI/audio feedback
// ============================================================================

using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Environmental hazard zone that damages the player.
    /// Implements ITickable for periodic damage logic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Environmental Hazard")]
    public sealed class EnvironmentalHazard : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private static int PlayerLayerIndex = -1;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Hazard Settings ────────────────────────────")]
        [Tooltip("Type of hazard.")]
        [SerializeField] private HazardType hazardType = HazardType.Radiation;

        [Tooltip("Base damage per second at center.")]
        [SerializeField, Range(0.1f, 50f)] private float baseDamagePerSecond = 5f;

        [Tooltip("Radius of the hazard zone (used if no trigger).")]
        [SerializeField, Range(1f, 100f)] private float hazardRadius = 10f;

        [Tooltip("Use trigger collider instead of radius.")]
        [SerializeField] private bool useTriggerCollider = true;

        [Tooltip("Minimum distance for full damage (closer = max damage).")]
        [SerializeField, Range(0f, 5f)] private float fullDamageRadius = 1f;

        [Tooltip("Damage interval (seconds).")]
        [SerializeField, Range(0.1f, 2f)] private float damageInterval = 0.5f;

        [Header("── Detection ──────────────────────────────────")]
        [Tooltip("Layer mask for player detection.")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("Tag to compare for player (zero GC).")]
        [SerializeField, TagSelector] private string playerTag = "Player";

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Renderer for hazard indicator.")]
        [SerializeField] private Renderer hazardIndicator;

        [Tooltip("Material property for indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when player is in hazard.")]
        [SerializeField] private Color activeColor = new Color(1f, 0.3f, 0.1f);

        [Tooltip("Color when no player in hazard.")]
        [SerializeField] private Color inactiveColor = new Color(0.3f, 0.1f, 0.1f);

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when player enters hazard zone.")]
        [SerializeField] private UnityEvent OnPlayerEnter;

        [Tooltip("Fired when player exits hazard zone.")]
        [SerializeField] private UnityEvent OnPlayerExit;

        [Tooltip("Fired when damage is dealt. Parameter: damage amount.")]
        [SerializeField] private UnityEvent<float> OnDamageDealt;

        [Tooltip("Fired when intensity changes. Parameter: normalized intensity (0-1).")]
        [SerializeField] private UnityEvent<float> OnIntensityChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private bool _playerInHazard;
        private float _damageTimer;
        private float _currentIntensity;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _radiationSourceRegistered;
        private int _radiationSourceId;
        private int _emissionPropertyId;
        private IThermodynamicsService _thermodynamicsService;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Pre-cached strings for zero GC
        private static readonly string _radiationText = "Radiation Hazard";
        private static readonly string _heatText = "Heat Hazard";
        private static readonly string _toxicText = "Toxic Hazard";

        // COLD ALLOC: Collider[8] — player radius overlap buffer — owner: EnvironmentalHazard
        private readonly Collider[] _playerOverlapBuffer = new Collider[8];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Type of hazard.</summary>
        public HazardType HazardTypeValue => hazardType;

        /// <summary>True if player is currently in hazard zone.</summary>
        public bool PlayerInHazard => _playerInHazard;

        /// <summary>Current damage intensity (0-1).</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>Hazard radius.</summary>
        public float HazardRadius => hazardRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            PlayerLayerIndex = -1;
        }

        private static void EnsureLayerCache()
        {
            if (PlayerLayerIndex >= 0)
                return;

            PlayerLayerIndex = Hecton8.Core.HectonLayerMasks.Player;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureLayerCache();
            _cachedTransform = transform;
            RefreshColdRegistryReferences();
            _radiationSourceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: EnvironmentalHazard

            if (hazardIndicator == null)
                hazardIndicator = GetComponent<Renderer>();

            // Cache player layer if not set
            if (playerLayer == 0 && PlayerLayerIndex >= 0)
            {
                playerLayer = 1 << PlayerLayerIndex;
            }
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            TryRegister();
            TryRegisterHotSwapListener();
            TryRegisterRadiationSource();
            UpdateIndicator();
        }

        private void OnDisable()
        {
            TryUnregisterRadiationSource();
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearExposureState();
        }

        private void OnDestroy()
        {
            TryUnregisterRadiationSource();
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearExposureState();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (hazardType == HazardType.Radiation)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RefreshColdRegistryReferences()
        {
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
                _thermodynamicsService = currentService as IThermodynamicsService;
        }

        private void ClearExposureState()
        {
            if (_playerInHazard)
                HazardExposureNotifier.Exit(hazardType);

            _playerInHazard = false;
            _playerTransform = null;
            _currentIntensity = 0f;
            _damageTimer = 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  TRIGGER DETECTION (Optional)
        // ══════════════════════════════════════════════════════════

        private void OnTriggerEnter(Collider other)
        {
            if (hazardType == HazardType.Radiation || hazardType == HazardType.Heat)
                return;

            if (!useTriggerCollider)
                return;

            if (!other.CompareTag(playerTag))
                return;

            _playerTransform = other.transform;
            bool wasInHazard = _playerInHazard;
            _playerInHazard = true;

            if (!wasInHazard)
            {
                HazardExposureNotifier.Enter(hazardType);
                OnPlayerEnter?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (hazardType == HazardType.Radiation || hazardType == HazardType.Heat)
                return;

            if (!useTriggerCollider)
                return;

            if (!other.CompareTag(playerTag))
                return;

            if (_playerTransform == other.transform)
            {
                if (_playerInHazard)
                    HazardExposureNotifier.Exit(hazardType);

                _playerTransform = null;
                _playerInHazard = false;
                _currentIntensity = 0f;

                OnPlayerExit?.Invoke();
                OnIntensityChanged?.Invoke(0f);
                UpdateIndicator();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — DAMAGE LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles damage logic.
        /// Zero GC: no allocations, uses CompareTag.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (hazardType == HazardType.Radiation)
                return;

            if (hazardType == HazardType.Heat)
            {
                TryPublishThermalFieldSource();
                return;
            }

            // If not using trigger, check radius
            if (!useTriggerCollider)
            {
                CheckPlayerInRadius();
            }

            if (!_playerInHazard || _playerTransform == null)
                return;

            double distanceSq = ResolvePlayerDistanceSq();
            float newIntensity = CalculateIntensityFromDistanceSq(distanceSq);

            // Fire intensity changed event
            if (Mathf.Abs(newIntensity - _currentIntensity) > 0.01f)
            {
                _currentIntensity = newIntensity;
                OnIntensityChanged?.Invoke(_currentIntensity);
                UpdateIndicator();
            }

            // Apply damage at intervals
            _damageTimer += deltaTime;
            if (_damageTimer >= damageInterval)
            {
                _damageTimer = 0f;
                ApplyDamage();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DETECTION LOGIC
        // ══════════════════════════════════════════════════════════

        private void CheckPlayerInRadius()
        {
            if (hazardType == HazardType.Radiation)
                return;

            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                _cachedTransform.position,
                hazardRadius,
                _playerOverlapBuffer,
                playerLayer,
                QueryTriggerInteraction.Ignore
            );

            bool foundPlayer = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _playerOverlapBuffer[i];
                if (hit != null && hit.CompareTag(playerTag))
                {
                    _playerTransform = hit.transform;
                    foundPlayer = true;
                    break;
                }
            }

            if (foundPlayer && !_playerInHazard)
            {
                _playerInHazard = true;
                HazardExposureNotifier.Enter(hazardType);
                OnPlayerEnter?.Invoke();
            }
            else if (!foundPlayer && _playerInHazard)
            {
                HazardExposureNotifier.Exit(hazardType);
                _playerInHazard = false;
                _playerTransform = null;
                _currentIntensity = 0f;
                OnPlayerExit?.Invoke();
                OnIntensityChanged?.Invoke(0f);
                UpdateIndicator();
            }
        }

        /// <summary>
        /// Calculates damage intensity from squared distance.
        /// 1.0 at center, 0.0 at edge.
        /// </summary>
        private float CalculateIntensityFromDistanceSq(double distanceSq)
        {
            double fullDamageRadiusSq = (double)fullDamageRadius * fullDamageRadius;
            if (distanceSq <= fullDamageRadiusSq)
                return 1f;

            double hazardRadiusSq = (double)hazardRadius * hazardRadius;
            if (distanceSq >= hazardRadiusSq)
                return 0f;

            double rangeSq = hazardRadiusSq - fullDamageRadiusSq;
            if (rangeSq <= double.Epsilon)
                return 0f;

            float normalizedSq = Mathf.Clamp01((float)((distanceSq - fullDamageRadiusSq) / rangeSq));
            return 1f - normalizedSq;
        }

        private double ResolvePlayerDistanceSq()
        {
            Vector3 hazardPosition = _cachedTransform.position;
            Vector3 playerPosition = _playerTransform.position;

            if (hazardRadius <= 50f)
            {
                Vector3 offset = playerPosition - hazardPosition;
                return offset.sqrMagnitude;
            }

            if (!TryResolveAupFromRuntimeOrigin(hazardPosition, out AbsoluteUniversePosition hazardAup) ||
                !TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup))
            {
                double radius = math.max((double)hazardRadius, 0d);
                return radius * radius;
            }

            return AbsoluteUniversePosition.DistanceSq(in hazardAup, in playerAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        // ══════════════════════════════════════════════════════════
        //  DAMAGE APPLICATION
        // ══════════════════════════════════════════════════════════

        private void ApplyDamage()
        {
            if (hazardType == HazardType.Radiation)
                return;

            if (_playerTransform == null || _currentIntensity <= 0f)
                return;

            float damage = baseDamagePerSecond * _currentIntensity * damageInterval;

            // Fire damage event for external systems (survival, UI, audio)
            OnDamageDealt?.Invoke(damage);

            // ── Interrupt player action (eating, healing) ──
            PlayerActionController actionController = _playerTransform.GetComponent<PlayerActionController>();
            if (actionController != null)
            {
                actionController.OnDamageTaken();
            }

            // Future: Direct integration with HectonSurvivalSystem
            // var survival = _playerTransform.GetComponent<HectonSurvivalSystem>();
            // if (survival != null)
            // {
            //     survival.TakeDamage(damage, hazardType);
            // }
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the hazard indicator using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateIndicator()
        {
            if (hazardIndicator == null)
                return;

            Color indicatorColor = _playerInHazard
                ? Color.Lerp(inactiveColor, activeColor, _currentIntensity)
                : inactiveColor;

            hazardIndicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            hazardIndicator.SetPropertyBlock(_mpb);
        }

        private void TryRegisterRadiationSource()
        {
            if (hazardType != HazardType.Radiation || _cachedTransform == null)
                return;

            float intensity = Mathf.Max(0f, baseDamagePerSecond) * 10f;
            RadiationHazardGrid.RegisterSource(_radiationSourceId, _cachedTransform.position, intensity, hazardRadius);
            _radiationSourceRegistered = Application.isPlaying;
        }

        private void TryUnregisterRadiationSource()
        {
            if (!_radiationSourceRegistered)
                return;

            RadiationHazardGrid.UnregisterSource(_radiationSourceId);
            _radiationSourceRegistered = false;
        }

        private void TryPublishThermalFieldSource()
        {
            if (hazardType != HazardType.Heat || _cachedTransform == null)
                return;

            IThermodynamicsService thermodynamics = _thermodynamicsService;
            if (thermodynamics != null && thermodynamics.IsInitialized)
                thermodynamics.TryInjectTransientHeatSource(_cachedTransform.position, hazardRadius, baseDamagePerSecond, unchecked((uint)_radiationSourceId));
        }

        /// <summary>
        /// Gets the hazard name for UI display.
        /// </summary>
        public string GetHazardName()
        {
            switch (hazardType)
            {
                case HazardType.Radiation: return _radiationText;
                case HazardType.Heat: return _heatText;
                case HazardType.Toxicity: return _toxicText;
                case HazardType.Biohazard: return "Biohazard";
                default: return string.Empty;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (baseDamagePerSecond < 0.1f) baseDamagePerSecond = 0.1f;
            if (hazardRadius < fullDamageRadius) hazardRadius = fullDamageRadius + 1f;
            if (damageInterval < 0.1f) damageInterval = 0.1f;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw hazard radius
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, hazardRadius);

            // Draw full damage radius
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, fullDamageRadius);

            // Draw hazard type indicator
            if (Application.isPlaying && _playerInHazard)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _playerTransform.position);
            }
        }
#endif
    }

    /// <summary>
    /// Attribute for tag selector in inspector.
    /// </summary>
    public class TagSelectorAttribute : PropertyAttribute { }
}
