// ============================================================================
// HECTON-8 â€” ItemHighlight.cs
// Sinica-style stencil visibility for resource items hidden in dense flora.
//
// FEATURES:
//   â€¢ Zero-GC ITickable implementation
//   â€¢ MaterialPropertyBlock for outline/glow effect
//   â€¢ Distance-based activation (5-10m range)
//   â€¢ "Always On Top" stencil effect when player is near
//   â€¢ Smooth fade-in/out transitions
//
// USAGE:
//   Attach to any resource prefab (Copper, Titanium, etc.)
//   Assign a Renderer reference for the highlight target.
//   The effect activates when player enters detection range.
//
// ZERO GC:
//   â€¢ MaterialPropertyBlock allocated once in Awake
//   â€¢ Shader.PropertyToID cached as static readonly
//   â€¢ sqrMagnitude comparison (no sqrt)
//   â€¢ No string operations in Tick
// ============================================================================

using Hecton8.Core;
using Hecton8.Scavenging;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Sinica-style stencil visibility for resource items.
    /// Creates a shimmer/outline effect when player is within range,
    /// making items visible through grass/kelp.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class ItemHighlight : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Distance at which highlight activates (meters).")]
        [SerializeField, Range(3f, 15f)] private float activationDistance = 8f;

        [Tooltip("Distance at which highlight reaches full intensity.")]
        [SerializeField, Range(1f, 10f)] private float fullIntensityDistance = 3f;

        [Header("â”€â”€ Visual â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Highlight color (typically cyan or gold for resources).")]
        [SerializeField] private Color highlightColor = new Color(0f, 0.9f, 1f, 1f);

        [Tooltip("Outline thickness (0 = thin, 1 = thick).")]
        [SerializeField, Range(0f, 1f)] private float outlineThickness = 0.5f;

        [Tooltip("Pulse speed for shimmer effect (0 = no pulse).")]
        [SerializeField, Range(0f, 5f)] private float pulseSpeed = 2f;

        [Tooltip("Pulse intensity amplitude (0 = no pulse).")]
        [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.3f;

        [Header("â”€â”€ Stencil â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Enable 'always on top' stencil rendering when highlighted.")]
        [SerializeField] private bool enableStencil = true;

        [Tooltip("Renderer to apply highlight to. If null, uses GetComponent<Renderer>().")]
        [SerializeField] private Renderer targetRenderer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private ResourceNode _resourceNode;
        private MaterialPropertyBlock _mpb;
        private bool _isHighlighted;
        private float _currentIntensity;
        private float _targetIntensity;
        private float _activationSqrDist;
        private float _fullIntensitySqrDist;
        private float _inverseIntensitySqrRange;
        private float _pulsePhase;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private HectonPlayerMovement _cachedPlayerMovement;
        private bool _tickRegistered;
        private bool _hotSwapRegistered;

        // â”€â”€ Shader property IDs (cached once) â”€â”€
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightIntensityId = Shader.PropertyToID("_HighlightIntensity");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");

        // â”€â”€ Stencil reference values â”€â”€
        private const int StencilRefNormal = 0;
        private const int StencilRefHighlight = 1;

        // â”€â”€ Animation constants â”€â”€
        private const float FadeSpeed = 4f;
        private const float Epsilon = 0.01f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            // COLD ALLOC: MaterialPropertyBlock[1] â€” per-object highlight props â€” owner: self
            _mpb = new MaterialPropertyBlock();
            _resourceNode = GetComponent<ResourceNode>();

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            _activationSqrDist = activationDistance * activationDistance;
            _fullIntensitySqrDist = fullIntensityDistance * fullIntensityDistance;
            _inverseIntensitySqrRange = 1f / math.max(Epsilon, _activationSqrDist - _fullIntensitySqrDist);

            // Initialize to no highlight
            _currentIntensity = 0f;
            _targetIntensity = 0f;
            CachePlayerContextCold();
            ApplyHighlightProperties();
        }

        private void OnEnable()
        {
            CachePlayerContextCold();
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();

            // Reset highlight on disable
            if (targetRenderer != null && _mpb != null)
            {
                _mpb.SetFloat(HighlightIntensityId, 0f);
                if (enableStencil)
                    _mpb.SetFloat(StencilRefId, StencilRefNormal);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            _cachedPlayerContext = null;
            _cachedPlayerMovement = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            _cachedPlayerMovement = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerMovement : null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (targetRenderer == null) return;

            // â”€â”€ Find player if not cached â”€â”€
            if (!TryResolveHighlightDistanceSq(out float sqrDist))
            {
                _targetIntensity = 0f;
                ApplyHighlightProperties();
                return;
            }

            // â”€â”€ Calculate distance to player â”€â”€
            // â”€â”€ Determine target intensity â”€â”€
            if (sqrDist <= _fullIntensitySqrDist)
            {
                _targetIntensity = 1f;
            }
            else if (sqrDist <= _activationSqrDist)
            {
                _targetIntensity = math.saturate((_activationSqrDist - sqrDist) * _inverseIntensitySqrRange);
            }
            else
            {
                _targetIntensity = 0f;
            }

            // â”€â”€ Apply pulse modulation â”€â”€
            if (_targetIntensity > 0f && pulseSpeed > 0f)
            {
                _pulsePhase = math.frac(_pulsePhase + math.max(0f, deltaTime) * pulseSpeed);
                float pulse = EvaluateSignedTriangle(_pulsePhase) * pulseAmplitude;
                _targetIntensity = math.saturate(_targetIntensity + pulse * _targetIntensity);
            }

            // â”€â”€ Smooth transition â”€â”€
            if (Mathf.Abs(_currentIntensity - _targetIntensity) > Epsilon)
            {
                _currentIntensity = math.lerp(_currentIntensity, _targetIntensity, math.saturate(deltaTime * FadeSpeed));
                ApplyHighlightProperties();
            }
            else if (_currentIntensity != _targetIntensity)
            {
                _currentIntensity = _targetIntensity;
                ApplyHighlightProperties();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE METHODS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyHighlightProperties()
        {
            if (targetRenderer == null || _mpb == null) return;

            // â”€â”€ Set highlight properties â”€â”€
            _mpb.SetColor(HighlightColorId, highlightColor);
            _mpb.SetFloat(HighlightIntensityId, _currentIntensity);
            _mpb.SetFloat(OutlineThicknessId, outlineThickness * _currentIntensity);

            // â”€â”€ Set stencil reference for "always on top" effect â”€â”€
            if (enableStencil)
            {
                int stencilRef = _currentIntensity > 0.1f ? StencilRefHighlight : StencilRefNormal;
                _mpb.SetFloat(StencilRefId, stencilRef);
            }

            targetRenderer.SetPropertyBlock(_mpb);
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void CachePlayerContextCold()
        {
            _cachedPlayerContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            _cachedPlayerMovement = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerMovement : null;
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Forces the highlight to a specific intensity (0-1).
        /// Used by external systems (e.g., scanner ping).
        /// </summary>
        public void SetHighlightIntensity(float intensity)
        {
            _targetIntensity = math.saturate(intensity);
        }

        /// <summary>
        /// Sets a custom highlight color at runtime.
        /// </summary>
        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
        }

        /// <summary>
        /// Current highlight intensity (0-1).
        /// </summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>
        /// Whether the item is currently being highlighted.
        /// </summary>
        public bool IsHighlighted => _currentIntensity > 0.1f;

        private bool TryResolveHighlightDistanceSq(out float sqrDist)
        {
            sqrDist = float.MaxValue;
            if (!TryResolveItemAup(out AbsoluteUniversePosition itemAup) ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                return false;
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in itemAup, in playerAup);
            if (!(distanceSq >= 0d))
                return false;

            const double MaxFloatAsDouble = 3.4028234663852886e38d;
            sqrDist = distanceSq > MaxFloatAsDouble ? float.MaxValue : (float)distanceSq;
            return true;
        }

        private bool TryResolveItemAup(out AbsoluteUniversePosition itemAup)
        {
            if (_resourceNode != null &&
                _resourceNode.TryGetPersistentAup(out itemAup) &&
                itemAup.IsFinite())
            {
                return true;
            }

            itemAup = default;
            return false;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (_cachedPlayerMovement == null && playerContext != null)
                _cachedPlayerMovement = playerContext.PlayerMovement;

            HectonPlayerMovement playerMovement = _cachedPlayerMovement;
            if (playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    playerAup = currentAup;
                    return true;
                }
            }

            playerAup = default;
            return false;
        }

        private static float EvaluateSignedTriangle(float phase)
        {
            float wrapped = math.frac(phase);
            return (1f - math.abs((wrapped * 2f) - 1f)) * 2f - 1f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Ensure distances are valid
            if (fullIntensityDistance > activationDistance)
                fullIntensityDistance = activationDistance * 0.5f;

            // Cache squared distances
            _activationSqrDist = activationDistance * activationDistance;
            _fullIntensitySqrDist = fullIntensityDistance * fullIntensityDistance;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw activation range
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, activationDistance);

            // Draw full intensity range
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, fullIntensityDistance);
        }
#endif
    }
}
