// ============================================================================
// HECTON-8 — InteractionUI.cs
// Strictly event-driven UI component. Zero polling, zero Update() overhead.
// Subscribes exclusively to InteractionEvents.OnHoverChanged.
// No direct reference to PlayerInteraction — pure decoupled architecture.
//
// PERFORMANCE NOTES:
//   - No Update() method exists — nothing runs per-frame.
//   - Handler fires only on hover state transitions.
//   - GameObject.SetActive used over CanvasGroup.alpha for zero overdraw
//     when hidden (Unity skips entire subtree in canvas rebuild).
//   - GetInteractText() returns a pre-cached string from the interactable,
//     so the only allocation is the single string.Concat per hover change.
//   - SetCharArray zero-alloc path provided and documented for extreme cases.
// ============================================================================

namespace Hecton8.Interaction
{
    using UnityEngine;
    using TMPro;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Interaction UI")]
    public class InteractionUI : MonoBehaviour
    {
        // ====================================================================
        // SERIALIZED CONFIGURATION
        // ====================================================================

        [Header("UI References")]
        [SerializeField, Tooltip("The TextMeshProUGUI label that displays the interaction prompt.")]
        private TextMeshProUGUI promptLabel;

        [SerializeField, Tooltip("Parent container GameObject to activate/deactivate. " +
                                  "Should wrap the prompt label and any background/icon elements.")]
        private GameObject promptContainer;

        [Header("Formatting")]
        [SerializeField, Tooltip("Input hint prefix prepended to the interact text. e.g. '[E]  '")]
        private string inputPrefix = "[E]  ";

        // ====================================================================
        // INTERNAL STATE
        // ====================================================================

        // Tracks the currently displayed target to avoid redundant UI updates
        // if the same event fires multiple times for the same object.
        private IInteractable _lastDisplayedTarget;

        // Pre-allocated character buffer for the zero-alloc SetCharArray path.
        // 96 chars is generous for any interaction prompt string.
        private readonly char[] _charBuffer = new char[96];

        // ====================================================================
        // UNITY LIFECYCLE — Subscribe / Unsubscribe to global event bus.
        // No Update(), no LateUpdate(), no coroutines. Purely reactive.
        // ====================================================================

        private void OnEnable()
        {
            InteractionEvents.OnHoverChanged += HandleHoverChanged;

            // Guarantee clean initial state — prompt hidden on enable.
            HidePrompt();
        }

        private void OnDisable()
        {
            InteractionEvents.OnHoverChanged -= HandleHoverChanged;

            // Clean up visual state so re-enabling doesn't show stale prompt.
            HidePrompt();
        }

        // ====================================================================
        // EVENT HANDLER — Fires exclusively on hover state transitions.
        // This is the ONLY entry point for all UI logic in this class.
        // ====================================================================

        private void HandleHoverChanged(IInteractable target)
        {
            if (target != null)
            {
                // Guard: skip redundant updates if hovering the same object.
                // ReferenceEquals avoids any virtual dispatch or boxing.
                if (ReferenceEquals(target, _lastDisplayedTarget))
                    return;

                _lastDisplayedTarget = target;
                ShowPrompt(target);
            }
            else
            {
                _lastDisplayedTarget = null;
                HidePrompt();
            }
        }

        // ====================================================================
        // INTERNAL — UI State Management
        // ====================================================================

        /// <summary>
        /// Activates the prompt container and populates the label text.
        /// Uses the zero-alloc SetCharArray path to avoid any GC pressure.
        /// </summary>
        private void ShowPrompt(IInteractable target)
        {
            if (promptLabel != null)
            {
                // Retrieve the pre-cached interact string from the interactable.
                // Contract: GetInteractText() must return a cached string, never allocate.
                string interactText = target.GetInteractText();

                // Zero-alloc text assembly using pre-allocated char buffer.
                int totalLength = WriteToBuffer(inputPrefix, interactText);
                promptLabel.SetCharArray(_charBuffer, 0, totalLength);
            }

            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
            }
        }

        /// <summary>
        /// Deactivates the prompt container. Unity skips the entire UI subtree
        /// when a GameObject is inactive — zero layout/render cost.
        /// </summary>
        private void HidePrompt()
        {
            if (promptContainer != null)
            {
                promptContainer.SetActive(false);
            }

            _lastDisplayedTarget = null;
        }

        // ====================================================================
        // ZERO-ALLOC TEXT ASSEMBLY
        // Copies prefix + interact text into the pre-allocated char buffer.
        // Returns the total number of characters written.
        // No string.Concat, no StringBuilder, no GC. Ever.
        // ====================================================================

        /// <summary>
        /// Writes prefix and body strings into _charBuffer sequentially.
        /// Clamps to buffer length to prevent overflow.
        /// </summary>
        /// <returns>Total characters written.</returns>
        private int WriteToBuffer(string prefix, string body)
        {
            int bufferLength = _charBuffer.Length;
            int offset = 0;

            // Copy prefix characters.
            if (prefix != null)
            {
                int prefixLen = Mathf.Min(prefix.Length, bufferLength);
                for (int i = 0; i < prefixLen; i++)
                {
                    _charBuffer[offset++] = prefix[i];
                }
            }

            // Copy body characters.
            if (body != null)
            {
                int remaining = bufferLength - offset;
                int bodyLen = Mathf.Min(body.Length, remaining);
                for (int i = 0; i < bodyLen; i++)
                {
                    _charBuffer[offset++] = body[i];
                }
            }

            return offset;
        }
    }
}