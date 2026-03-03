// ============================================================================
// HECTON-8 — PlayerInteraction.cs
// Core player interaction component. Attach to the Player prefab root.
// Performs throttled raycasts, manages hover state, dispatches interactions.
//
// PERFORMANCE NOTES:
//   - Raycasts throttled to configurable interval (default 100ms).
//   - Zero GC allocations in Update/FixedUpdate loop.
//   - Uses Physics.Raycast (single, non-alloc) — no RaycastAll needed.
//   - Component caching via TryGetComponent (no GetComponent alloc on hit).
//   - String comparisons avoided entirely — interface reference equality only.
//
// REVISION NOTES:
//   - Ray origin offset forward by 0.1m to prevent self-hit on player collider.
//   - Debug.DrawRay added for Scene-view visualization during Play mode.
//   - OnHoverChanged raise paths audited — fires on every hover transition
//     including clear-to-new, old-to-new, and any-to-null.
// ============================================================================

namespace Hecton8.Interaction
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public class PlayerInteraction : MonoBehaviour
    {
        // ====================================================================
        // SERIALIZED CONFIGURATION
        // ====================================================================
        [Header("Raycast Settings")]
        [SerializeField, Tooltip("Maximum interaction reach in meters.")]
        private float reachDistance = 3.5f;

        [SerializeField, Tooltip("Seconds between raycast ticks. 0.1 = 10 checks/sec.")]
        private float raycastInterval = 0.1f;

        [SerializeField, Tooltip("Physics layers to check. Set to 'Interactable' layer for best perf.")]
        private LayerMask interactableMask = ~0; // Default: everything. Narrow this in Inspector.

        [SerializeField, Tooltip("Small offset to push ray origin in front of the camera to avoid hitting the player's own collider.")]
        private float rayOriginOffset = 0.1f;

        [Header("References")]
        [SerializeField, Tooltip("Assign the main camera. Auto-finds if null.")]
        private Camera playerCamera;

        [Header("Debug")]
        [SerializeField, Tooltip("Color of the debug ray when nothing is hovered.")]
        private Color debugRayMissColor = Color.yellow;

        [SerializeField, Tooltip("Color of the debug ray when an interactable is hovered.")]
        private Color debugRayHitColor = Color.green;

        // ====================================================================
        // INTERNAL STATE — Zero heap allocation, all stack/cached.
        // ====================================================================
        private IInteractable _currentHovered;
        private float         _raycastTimer;
        private Transform     _cameraTransform;       // Cached transform — avoids Camera.main.
        private Ray           _ray;                    // Reused ray struct — stack allocated.
        private RaycastHit    _hitInfo;                // Reused hit struct — stack allocated.

        // ====================================================================
        // PUBLIC ACCESSORS (for UI / other systems if they poll instead of event)
        // ====================================================================

        /// <summary>
        /// The currently hovered interactable, or null. Zero-alloc read.
        /// </summary>
        public IInteractable CurrentHovered => _currentHovered;

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            // Resolve camera reference once. Never call Camera.main at runtime.
            if (playerCamera == null)
            {
                playerCamera = Camera.main;

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (playerCamera == null)
                {
                    Debug.LogError(
                        "[PlayerInteraction] No camera assigned and Camera.main is null. " +
                        "Assign the player camera in the Inspector.", this);
                    enabled = false;
                    return;
                }
                #endif
            }

            _cameraTransform = playerCamera.transform;
            _raycastTimer = 0f;
        }

        private void Update()
        {
            // ================================================================
            // THROTTLED RAYCAST — Only fires every 'raycastInterval' seconds.
            // On a 60fps game this reduces raycast calls from 60/s to 10/s.
            // ================================================================
            _raycastTimer += Time.deltaTime;

            if (_raycastTimer >= raycastInterval)
            {
                _raycastTimer = 0f; // Reset, don't subtract — avoids drift/burst.
                PerformRaycast();
            }

            // ================================================================
            // INPUT CHECK — Runs every frame for responsive feel.
            // Only processes if we have a valid hovered target.
            // ================================================================
            if (_currentHovered != null && Input.GetKeyDown(KeyCode.E))
            {
                _currentHovered.Interact(transform);

                // Fire global event so inventory, audio, analytics can react.
                InteractionEvents.RaiseInteractionStarted(_currentHovered, transform);
            }
        }

        private void OnDisable()
        {
            // Clean up hover state if player component is disabled mid-hover.
            ClearHover();
        }

        // ====================================================================
        // CORE RAYCAST LOGIC — Zero GC. All struct-based.
        // ====================================================================

        private void PerformRaycast()
        {
            // Build ray from slightly in front of the camera to avoid hitting
            // the player's own collider. The offset pushes the origin forward
            // along the camera's look direction by rayOriginOffset meters.
            Vector3 origin    = _cameraTransform.position + _cameraTransform.forward * rayOriginOffset;
            Vector3 direction = _cameraTransform.forward;

            _ray.origin    = origin;
            _ray.direction = direction;

            // Effective reach is reduced by the offset so total distance from
            // camera position remains consistent with the configured reachDistance.
            float effectiveReach = reachDistance - rayOriginOffset;

            // ================================================================
            // DEBUG VISUALIZATION — Visible in Scene view during Play mode.
            // Duration matches raycastInterval so the line persists until the
            // next tick, giving a continuous visual without per-frame cost.
            // ================================================================
            Debug.DrawRay(
                origin,
                direction * effectiveReach,
                _currentHovered != null ? debugRayHitColor : debugRayMissColor,
                raycastInterval,        // duration: stays visible until next raycast tick
                false                   // depthTest: false so it's always visible in Scene
            );

            if (Physics.Raycast(_ray, out _hitInfo, effectiveReach, interactableMask,
                                QueryTriggerInteraction.Ignore))
            {
                // TryGetComponent is non-allocating (unlike GetComponent which
                // can box on interface queries in older Unity versions).
                // We check the hit collider's GameObject for the interface.
                if (_hitInfo.collider.TryGetComponent(out IInteractable interactable))
                {
                    // Same object as last frame? Do nothing — avoid redundant calls.
                    if (ReferenceEquals(interactable, _currentHovered))
                        return;

                    // Different object — end old hover, start new one.
                    // ClearHover will raise OnHoverChanged(null) for the old target.
                    // We then immediately set and raise for the new target, so
                    // subscribers always see the full transition:
                    //   old.OnHoverEnd() → event(null) → new.OnHoverStart() → event(new)
                    ClearHover();

                    _currentHovered = interactable;
                    _currentHovered.OnHoverStart();

                    // Notify UI / any listener about the new hover target.
                    InteractionEvents.RaiseHoverChanged(_currentHovered);
                    return;
                }
            }

            // If we reach here: ray hit nothing interactable, or hit nothing at all.
            // ClearHover is idempotent — safe to call even if already null.
            if (_currentHovered != null)
            {
                ClearHover();
            }
        }

        /// <summary>
        /// Ends the current hover cleanly and nulls the reference.
        /// Idempotent — safe to call when _currentHovered is already null.
        /// Always raises OnHoverChanged(null) so subscribers stay in sync.
        /// </summary>
        private void ClearHover()
        {
            if (_currentHovered != null)
            {
                _currentHovered.OnHoverEnd();
                _currentHovered = null;

                // Null signals "no hover target" to UI subscribers.
                InteractionEvents.RaiseHoverChanged(null);
            }
        }

        // ====================================================================
        // EDITOR GIZMO — Visualize reach distance in Scene view (edit mode).
        // ====================================================================
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_cameraTransform == null && playerCamera != null)
                _cameraTransform = playerCamera.transform;

            if (_cameraTransform == null) return;

            Vector3 origin = _cameraTransform.position + _cameraTransform.forward * rayOriginOffset;

            Gizmos.color = _currentHovered != null
                ? debugRayHitColor
                : new Color(1f, 1f, 0f, 0.5f);

            Gizmos.DrawRay(origin, _cameraTransform.forward * (reachDistance - rayOriginOffset));

            // Draw a small sphere at the offset origin so it's clear where the
            // ray actually begins (helps diagnose self-hit issues).
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, 0.03f);
        }
        #endif
    }
}