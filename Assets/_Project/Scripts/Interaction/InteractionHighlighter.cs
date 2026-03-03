// ============================================================================
// HECTON-8 — InteractionHighlighter.cs
// Attach to any interactable object with a Renderer.
// Uses MaterialPropertyBlock for ZERO material instance allocations.
//
// PERFORMANCE NOTES:
//   - MaterialPropertyBlock is stack-allocated internally by Unity.
//   - No new Material instances are ever created.
//   - No GC pressure whatsoever.
//   - Compatible with URP, HDRP, and Built-in pipeline.
//   - Supports multi-renderer objects (e.g., a crate with separate lid mesh).
// ============================================================================

namespace Hecton8.Interaction
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Interaction Highlighter")]
    public class InteractionHighlighter : MonoBehaviour
    {
        // ====================================================================
        // CONFIGURATION
        // ====================================================================
        [Header("Highlight Settings")]
        [SerializeField, ColorUsage(false, true)]
        [Tooltip("HDR emission color when highlighted. Use high intensity for bloom.")]
        private Color highlightEmissionColor = new Color(0.8f, 1.2f, 2.0f, 1f);

        [SerializeField, Tooltip("Fallback: tint the base color instead of emission.")]
        private Color highlightBaseColorTint = new Color(0.7f, 0.85f, 1.0f, 1f);

        [SerializeField, Tooltip("Use emission (true) or base color tint (false).")]
        private bool useEmission = true;

        [Header("References (Auto-populated if empty)")]
        [SerializeField, Tooltip("All renderers to apply the highlight to.")]
        private Renderer[] targetRenderers;

        // ====================================================================
        // INTERNAL STATE
        // ====================================================================
        private MaterialPropertyBlock _propBlock;      // Reused every call — zero alloc.
        private bool                  _isHighlighted;

        // Pre-cached shader property IDs — integer lookups, no string hashing at runtime.
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionKeyword  = Shader.PropertyToID("_EMISSION");

        // Store original colors so we can restore perfectly.
        private Color[] _originalEmissionColors;
        private Color[] _originalBaseColors;

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            // Allocate the property block once — reused for the lifetime of this object.
            _propBlock = new MaterialPropertyBlock();

            // Auto-find renderers if not assigned in Inspector.
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            // Cache original colors from each renderer.
            CacheOriginalColors();
        }

        private void OnDisable()
        {
            // Always restore original appearance when disabled/destroyed.
            if (_isHighlighted)
            {
                SetHighlight(false);
            }
        }

        // ====================================================================
        // PUBLIC API — Called by IInteractable implementations.
        // ====================================================================

        /// <summary>
        /// Activates or deactivates the highlight. Zero allocations.
        /// Call from OnHoverStart() with true, OnHoverEnd() with false.
        /// </summary>
        public void SetHighlight(bool active)
        {
            // Early-out if state hasn't changed — avoid redundant GPU property sets.
            if (_isHighlighted == active)
                return;

            _isHighlighted = active;

            for (int i = 0, count = targetRenderers.Length; i < count; i++)
            {
                Renderer rend = targetRenderers[i];

                if (rend == null) continue; // Safety for destroyed child renderers.

                // Get current property block (preserves other properties set elsewhere).
                rend.GetPropertyBlock(_propBlock);

                if (useEmission)
                {
                    _propBlock.SetColor(EmissionColorID,
                        active ? highlightEmissionColor : _originalEmissionColors[i]);
                }
                else
                {
                    _propBlock.SetColor(BaseColorID,
                        active ? highlightBaseColorTint : _originalBaseColors[i]);
                }

                // Apply — this does NOT create a new material instance.
                rend.SetPropertyBlock(_propBlock);
            }
        }

        // ====================================================================
        // INTERNAL — One-time color caching at Awake.
        // ====================================================================

        private void CacheOriginalColors()
        {
            int count = targetRenderers.Length;
            _originalEmissionColors = new Color[count];
            _originalBaseColors     = new Color[count];

            for (int i = 0; i < count; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend == null) continue;

                // Read from the shared material — no instantiation.
                Material sharedMat = rend.sharedMaterial;

                if (sharedMat != null)
                {
                    _originalEmissionColors[i] = sharedMat.HasProperty(EmissionColorID)
                        ? sharedMat.GetColor(EmissionColorID)
                        : Color.black;

                    _originalBaseColors[i] = sharedMat.HasProperty(BaseColorID)
                        ? sharedMat.GetColor(BaseColorID)
                        : Color.white;
                }
                else
                {
                    _originalEmissionColors[i] = Color.black;
                    _originalBaseColors[i]     = Color.white;
                }
            }
        }
    }
}