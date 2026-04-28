// ============================================================================
// HECTON-8 — ItemHighlight.cs
// Sinica-style stencil visibility for resource items hidden in dense flora.
//
// FEATURES:
//   • Zero-GC ITickable implementation
//   • MaterialPropertyBlock for outline/glow effect
//   • Distance-based activation (5-10m range)
//   • "Always On Top" stencil effect when player is near
//   • Smooth fade-in/out transitions
//
// USAGE:
//   Attach to any resource prefab (Copper, Titanium, etc.)
//   Assign a Renderer reference for the highlight target.
//   The effect activates when player enters detection range.
//
// ZERO GC:
//   • MaterialPropertyBlock allocated once in Awake
//   • Shader.PropertyToID cached as static readonly
//   • sqrMagnitude comparison (no sqrt)
//   • No string operations in Tick
// ============================================================================

using Hecton8.Bootstrap;
using Hecton8.Core;
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
    public sealed class ItemHighlight : MonoBehaviour, ITickable, IUpdatable
    {
        // ═════════════════════════════════════════════════════════
        //  INSPECTOR
        // ═════════════════════════════════════════════════════════

        [Header("── Detection ───────────────────────────────")]
        [Tooltip("Distance at which highlight activates (meters).")]
        [SerializeField, Range(3f, 15f)] private float activationDistance = 8f;

        [Tooltip("Distance at which highlight reaches full intensity.")]
        [SerializeField, Range(1f, 10f)] private float fullIntensityDistance = 3f;

        [Header("── Visual ──────────────────────────────────")]
        [Tooltip("Highlight color (typically cyan or gold for resources).")]
        [SerializeField] private Color highlightColor = new Color(0f, 0.9f, 1f, 1f);

        [Tooltip("Outline thickness (0 = thin, 1 = thick).")]
        [SerializeField, Range(0f, 1f)] private float outlineThickness = 0.5f;

        [Tooltip("Pulse speed for shimmer effect (0 = no pulse).")]
        [SerializeField, Range(0f, 5f)] private float pulseSpeed = 2f;

        [Tooltip("Pulse intensity amplitude (0 = no pulse).")]
        [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.3f;

        [Header("── Stencil ─────────────────────────────────")]
        [Tooltip("Enable 'always on top' stencil rendering when highlighted.")]
        [SerializeField] private bool enableStencil = true;

        [Tooltip("Renderer to apply highlight to. If null, uses GetComponent<Renderer>().")]
        [SerializeField] private Renderer targetRenderer;

        // ═════════════════════════════════════════════════════════
        //  CACHED STATE
        // ═════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private MaterialPropertyBlock _mpb;
        private bool _isHighlighted;
        private float _currentIntensity;
        private float _targetIntensity;
        private float _activationSqrDist;
        private float _fullIntensitySqrDist;
        private bool _tickRegistered;

        // ── Shader property IDs (cached once) ──
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightIntensityId = Shader.PropertyToID("_HighlightIntensity");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");

        // ── Stencil reference values ──
        private const int StencilRefNormal = 0;
        private const int StencilRefHighlight = 1;

        // ── Animation constants ──
        private const float FadeSpeed = 4f;
        private const float Epsilon = 0.01f;

        // ═════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════════

        private void Awake()
        {
            // COLD ALLOC: MaterialPropertyBlock[1] — per-object highlight props — owner: self
            _mpb = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            _activationSqrDist = activationDistance * activationDistance;
            _fullIntensitySqrDist = fullIntensityDistance * fullIntensityDistance;

            // Initialize to no highlight
            _currentIntensity = 0f;
            _targetIntensity = 0f;
            ApplyHighlightProperties();
        }

        private void OnEnable()
        {
            TryFindPlayer();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            _playerTransform = null;

            // Reset highlight on disable
            if (targetRenderer != null && _mpb != null)
            {
                _mpb.SetFloat(HighlightIntensityId, 0f);
                if (enableStencil)
                    _mpb.SetFloat(StencilRefId, StencilRefNormal);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }

        // ═════════════════════════════════════════════════════════
        //  ITickable
        // ═════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (targetRenderer == null) return;

            // ── Find player if not cached ──
            if (_playerTransform == null)
            {
                TryFindPlayer();
                if (_playerTransform == null) return;
            }

            // ── Calculate distance to player ──
            float sqrDist = (transform.position - _playerTransform.position).sqrMagnitude;

            // ── Determine target intensity ──
            if (sqrDist <= _fullIntensitySqrDist)
            {
                _targetIntensity = 1f;
            }
            else if (sqrDist <= _activationSqrDist)
            {
                // Linear interpolation between full intensity and activation distance
                float t = Mathf.InverseLerp(_activationSqrDist, _fullIntensitySqrDist, sqrDist);
                _targetIntensity = t;
            }
            else
            {
                _targetIntensity = 0f;
            }

            // ── Apply pulse modulation ──
            if (_targetIntensity > 0f && pulseSpeed > 0f)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
                _targetIntensity = Mathf.Clamp01(_targetIntensity + pulse * _targetIntensity);
            }

            // ── Smooth transition ──
            if (Mathf.Abs(_currentIntensity - _targetIntensity) > Epsilon)
            {
                _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, deltaTime * FadeSpeed);
                ApplyHighlightProperties();
            }
            else if (_currentIntensity != _targetIntensity)
            {
                _currentIntensity = _targetIntensity;
                ApplyHighlightProperties();
            }
        }

        // ═════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ═════════════════════════════════════════════════════════

        private void ApplyHighlightProperties()
        {
            if (targetRenderer == null || _mpb == null) return;

            // ── Set highlight properties ──
            _mpb.SetColor(HighlightColorId, highlightColor);
            _mpb.SetFloat(HighlightIntensityId, _currentIntensity);
            _mpb.SetFloat(OutlineThicknessId, outlineThickness * _currentIntensity);

            // ── Set stencil reference for "always on top" effect ──
            if (enableStencil)
            {
                int stencilRef = _currentIntensity > 0.1f ? StencilRefHighlight : StencilRefNormal;
                _mpb.SetFloat(StencilRefId, stencilRef);
            }

            targetRenderer.SetPropertyBlock(_mpb);
        }

        private void TryFindPlayer()
        {
            if (_playerTransform != null) return;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform player))
            {
                _playerTransform = player;
            }
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered) return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        // ═════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Forces the highlight to a specific intensity (0-1).
        /// Used by external systems (e.g., scanner ping).
        /// </summary>
        public void SetHighlightIntensity(float intensity)
        {
            _targetIntensity = Mathf.Clamp01(intensity);
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

        // ═════════════════════════════════════════════════════════
        //  EDITOR
        // ═════════════════════════════════════════════════════════

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
