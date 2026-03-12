// ============================================================================
// HECTON-8 — PlayerInteraction.cs
// Core player interaction component. Attach to the Player prefab root.
// Performs throttled raycasts, manages hover state, dispatches interactions.
//
// PERFORMANCE NOTES:
//   - Raycasts throttled to 0.05s (20 checks/sec) — smooth hover, low CPU.
//   - LayerMask MUST be set to 'Interactable' layer — no full-scene sweeps.
//   - Zero GC allocations in Tick loop.
//   - Uses Physics.Raycast (single, non-alloc) with QueryTriggerInteraction.Ignore.
//   - Component caching via TryGetComponent (no GetComponent alloc on hit).
//   - ReferenceEquals for hover comparison — no boxing, no vtable dispatch.
//
// ARCHITECTURE:
//   - Integrated with GameTickManager via ITickable — native Update() is PROHIBITED.
//   - Raycast tick (throttled) → updates _currentHovered target.
//   - Input poll (every tick) → reads _currentHovered, fires Interact().
//   - These two paths are fully decoupled: input is never gated by raycast timer.
//   - UI State Guard: interaction input is blocked when any menu is open,
//     but raycasts continue so the hover prompt is immediately visible on close.
//
// AUDIO FEEDBACK:
//   - Hover transition → SpatialAudioManager.PlayStatic2D(hoverSound, 0.3f)
//   - Interact confirm → SpatialAudioManager.PlayStatic2D(interactSound, 0.6f)
//   - All clips are optional — null clips are silently skipped.
//
// REVISION NOTES:
//   - raycastInterval reduced to 0.05f for zero-latency hover feel.
//   - interactableMask defaults to Nothing — MUST be configured in Inspector.
//   - Debug.DrawRay persists for raycastInterval duration (continuous line).
//   - Hover events fire on every transition: clear→new, old→new, any→null.
//   - Migrated from Update() to ITickable.Tick(float) via GameTickManager.
//   - Added UI State Guard: interaction blocked while HectonFabricatorUI.IsMenuOpen.
//   - [FIX] OnEnable/OnDisable now null-safe against missing GameTickManager.
//   - [FIX] All singleton access guarded with null-checks.
//   - [FIX] Registration tracking flag prevents double-register/unregister.
//   - [REFACTOR] Deferred registration: OnEnable → attempt, Start → fallback,
//     Debug.LogError only if Instance still null at Start.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.UI;
    using UnityEngine;
    using Hecton8.Audio;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public sealed class PlayerInteraction : MonoBehaviour, ITickable
    {
        // ====================================================================
        // SERIALIZED CONFIGURATION
        // ====================================================================

        [Header("Raycast Settings")]

        [SerializeField, Tooltip("Maximum interaction reach in meters.")]
        private float reachDistance = 3.5f;

        [SerializeField, Tooltip("Seconds between raycast ticks. 0.05 = 20 checks/sec for smooth hover.")]
        private float raycastInterval = 0.05f;

        [SerializeField, Tooltip("REQUIRED: Set to 'Interactable' layer. Never leave as Everything.")]
        private LayerMask interactableMask = 0;

        [SerializeField, Tooltip("Small offset to push ray origin forward, avoiding the player's own collider.")]
        private float rayOriginOffset = 0.1f;

        [Header("Audio Feedback")]

        [SerializeField, Tooltip("Quiet metallic click played when hovering over a new interactable.")]
        private AudioClip hoverSound;

        [SerializeField, Tooltip("Firm confirmation sound played on successful interaction.")]
        private AudioClip interactSound;

        [Header("References")]

        [SerializeField, Tooltip("Assign the main camera. Auto-resolves via Camera.main if null.")]
        private Camera playerCamera;

        [Header("Debug")]

        [SerializeField, Tooltip("Debug ray color when nothing is hovered.")]
        private Color debugRayMissColor = Color.yellow;

        [SerializeField, Tooltip("Debug ray color when an interactable is hovered.")]
        private Color debugRayHitColor = Color.green;

        // ====================================================================
        // INTERNAL STATE — Zero heap allocation, all stack/cached.
        // ====================================================================

        private IInteractable _currentHovered;
        private float         _raycastTimer;
        private Transform     _cameraTransform;
        private Ray           _ray;
        private RaycastHit    _hitInfo;

        /// <summary>
        /// Tracks whether this component successfully registered with GameTickManager.
        /// Prevents:
        ///   - Double-register (OnEnable + Start both succeeding).
        ///   - Unregister when we never registered in the first place.
        ///   - Double-unregister during scene teardown.
        /// </summary>
        private bool          _registeredToTickManager;

        // ====================================================================
        // PUBLIC ACCESSORS
        // ====================================================================

        public IInteractable CurrentHovered => _currentHovered;

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            // ----------------------------------------------------------------
            // Camera resolution — done once, cached forever.
            // ----------------------------------------------------------------
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
            _registeredToTickManager = false;

            // ----------------------------------------------------------------
            // Layer mask validation — catch misconfiguration early.
            // ----------------------------------------------------------------
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (interactableMask.value == 0)
            {
                Debug.LogWarning(
                    "[PlayerInteraction] interactableMask is set to Nothing. " +
                    "Raycasts will hit nothing. Set it to the 'Interactable' layer.", this);
            }
            else if (interactableMask.value == ~0)
            {
                Debug.LogWarning(
                    "[PlayerInteraction] interactableMask is set to Everything. " +
                    "For performance, narrow it to the 'Interactable' layer only.", this);
            }
            #endif
        }

        // ====================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        //
        // Phase 1 (OnEnable): Attempt registration. If GameTickManager.Instance
        //   is null (script execution order, scene loading), silently skip.
        //   No warning, no error — Start will handle it.
        //
        // Phase 2 (Start): If OnEnable failed to register, retry here.
        //   By Start(), all Awake() calls have completed — Instance should exist.
        //   Debug.LogError ONLY if Instance is still null at this point.
        //
        // The _registeredToTickManager flag guarantees:
        //   - Registration happens exactly once (no double-tick).
        //   - Unregister only if we actually registered.
        //   - Safe during scene teardown (OnDisable checks flag + Instance).
        // ====================================================================

        private void OnEnable()
        {
            // Phase 1: early registration attempt.
            // GameTickManager may not have initialized yet — that's OK.
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            // Phase 2: deferred registration fallback.
            // All Awake() calls have completed by now.
            if (_registeredToTickManager)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registeredToTickManager = true;
            }
            else
            {
                Debug.LogError(
                    "[PlayerInteraction] GameTickManager.Instance is null even at Start(). " +
                    "Tick-based interaction will NOT work. " +
                    "Ensure GameTickManager exists in the scene and is active.", this);
            }
        }

        private void OnDisable()
        {
            // Guard: Only unregister if we actually registered AND the manager still exists.
            // During scene teardown, singletons may be destroyed before this component.
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registeredToTickManager = false;
            }

            // Clean up hover state if component is disabled mid-hover.
            ClearHover();
        }

        // ====================================================================
        // ITickable IMPLEMENTATION — Replaces native Update().
        // ====================================================================

        public void Tick(float deltaTime)
        {
            // ================================================================
            // PHASE 1: THROTTLED RAYCAST — Target acquisition.
            // Runs at 20Hz (0.05s interval). Updates _currentHovered.
            // Raycasts are NOT blocked by UI state — the hover prompt
            // must be visible the instant a menu closes.
            // ================================================================
            _raycastTimer += deltaTime;

            if (_raycastTimer >= raycastInterval)
            {
                _raycastTimer = 0f;
                PerformRaycast();
            }

            // ================================================================
            // PHASE 2: INPUT POLL — Action execution.
            // Runs EVERY tick for zero-latency response.
            //
            // UI STATE GUARD: If any menu is open, the interaction key
            // is silently consumed. HectonFabricatorUI.IsMenuOpen is a
            // static property — safe to read even if the instance is null
            // (static fields persist independently of MonoBehaviour lifecycle).
            // ================================================================
            if (_currentHovered != null
                && !HectonFabricatorUI.IsMenuOpen
                && Input.GetKeyDown(KeyCode.E))
            {
                ExecuteInteraction();
            }
        }

        // ====================================================================
        // CORE RAYCAST — Zero GC. Struct-only. Layer-targeted.
        // ====================================================================

        private void PerformRaycast()
        {
            Vector3 origin    = _cameraTransform.position + _cameraTransform.forward * rayOriginOffset;
            Vector3 direction = _cameraTransform.forward;

            _ray.origin    = origin;
            _ray.direction = direction;

            float effectiveReach = reachDistance - rayOriginOffset;

            #if UNITY_EDITOR
            Debug.DrawRay(
                origin,
                direction * effectiveReach,
                _currentHovered != null ? debugRayHitColor : debugRayMissColor,
                raycastInterval,
                false);
            #endif

            if (Physics.Raycast(_ray, out _hitInfo, effectiveReach, interactableMask,
                                QueryTriggerInteraction.Ignore))
            {
                if (_hitInfo.collider.TryGetComponent(out IInteractable interactable))
                {
                    if (ReferenceEquals(interactable, _currentHovered))
                        return;

                    ClearHover();
                    SetHover(interactable);
                    return;
                }
            }

            if (_currentHovered != null)
            {
                ClearHover();
            }
        }

        // ====================================================================
        // HOVER STATE MANAGEMENT
        // ====================================================================

        private void SetHover(IInteractable target)
        {
            _currentHovered = target;
            _currentHovered.OnHoverStart();

            // Audio: subtle metallic click on hover acquisition.
            // Guard: SpatialAudioManager may not exist in minimal test scenes.
            if (hoverSound != null && SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayStatic2D(hoverSound, 0.3f);
            }

            InteractionEvents.RaiseHoverChanged(_currentHovered);
        }

        private void ClearHover()
        {
            if (_currentHovered == null)
                return;

            _currentHovered.OnHoverEnd();
            _currentHovered = null;

            InteractionEvents.RaiseHoverChanged(null);
        }

        // ====================================================================
        // INTERACTION EXECUTION
        // ====================================================================

        private void ExecuteInteraction()
        {
            // Audio: firm metallic confirmation.
            // Guard: SpatialAudioManager may not exist in minimal test scenes.
            if (interactSound != null && SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayStatic2D(interactSound, 0.6f);
            }

            _currentHovered.Interact(transform);
            InteractionEvents.RaiseInteractionStarted(_currentHovered, transform);
        }

        // ====================================================================
        // EDITOR GIZMO
        // ====================================================================
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_cameraTransform == null && playerCamera != null)
                _cameraTransform = playerCamera.transform;

            if (_cameraTransform == null)
                return;

            Vector3 origin = _cameraTransform.position + _cameraTransform.forward * rayOriginOffset;
            float effectiveReach = reachDistance - rayOriginOffset;

            Gizmos.color = _currentHovered != null
                ? debugRayHitColor
                : new Color(1f, 1f, 0f, 0.5f);

            Gizmos.DrawRay(origin, _cameraTransform.forward * effectiveReach);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, 0.03f);

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(origin + _cameraTransform.forward * effectiveReach, 0.05f);
        }
        #endif
    }
}