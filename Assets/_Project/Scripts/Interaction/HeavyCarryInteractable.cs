// ============================================================================
// HECTON-8 — HeavyCarryInteractable.cs
// Marker-style interactable for large rigidbody cargo that must be dragged
// physically instead of entering the inventory.
// ============================================================================

namespace Hecton8.Interaction
{
    using UnityEngine;

    /// <summary>
    /// Marks a rigidbody object as heavy cargo for <see cref="PhysicalInteractionHandler"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [AddComponentMenu("Hecton8/Interaction/Heavy Carry Interactable")]
    public sealed class HeavyCarryInteractable : MonoBehaviour, IInteractable
    {
        private const int MaxParentResolveDepth = 32;

        [Header("── Cargo ──────────────────")]
        [Tooltip("Optional explicit rigidbody. Falls back to attached or parent rigidbody.")]
        [SerializeField] private Rigidbody carryBody;

        [Tooltip("Prompt shown when the object is idle.")]
        [SerializeField] private string carryPrompt = "Drag Cargo";

        [Tooltip("Prompt shown while the object is being dragged.")]
        [SerializeField] private string releasePrompt = "Release Cargo";

        private InteractionHighlighter _highlighter;
        private string _cachedIdlePrompt = "Drag Cargo";
        private string _cachedReleasePrompt = "Release Cargo";
        private bool _isBeingDragged;

        /// <summary>
        /// Returns the rigidbody that should be moved by the physical interaction system.
        /// </summary>
        public Rigidbody CarryBody => carryBody;

        private void Awake()
        {
            _highlighter = TryGetComponent(out InteractionHighlighter highlighter) ? highlighter : null;
            ResolveCarryBody();
            RebuildPromptCache();
        }

        /// <summary>
        /// Updates the prompt state when dragging begins or ends.
        /// </summary>
        public void SetDraggedState(bool isDragged)
        {
            _isBeingDragged = isDragged;
        }

        /// <summary>
        /// Tries to return the carry rigidbody used for dragging.
        /// </summary>
        public bool TryGetCarryBody(out Rigidbody body)
        {
            if (carryBody == null)
                ResolveCarryBody();

            body = carryBody;
            return body != null;
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
        }

        void IInteractable.OnHoverStart()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(true);
        }

        void IInteractable.OnHoverEnd()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
        }

        void IInteractable.Interact(Transform interactor)
        {
            // PhysicalInteractionHandler intercepts this through PlayerInteraction.
        }

        string IInteractable.GetInteractText()
        {
            return _isBeingDragged ? _cachedReleasePrompt : _cachedIdlePrompt;
        }

        private void ResolveCarryBody()
        {
            if (carryBody == null)
            {
                if (!TryGetComponent(out carryBody))
                    TryResolveParentComponent(transform, out carryBody);
            }
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start != null ? start.parent : null;
            int depth = 0;
            while (current != null && depth++ < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void RebuildPromptCache()
        {
            _cachedIdlePrompt = string.IsNullOrWhiteSpace(carryPrompt)
                ? "Drag Cargo"
                : carryPrompt;
            _cachedReleasePrompt = string.IsNullOrWhiteSpace(releasePrompt)
                ? "Release Cargo"
                : releasePrompt;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ResolveCarryBody();
            RebuildPromptCache();
        }
#endif
    }
}
