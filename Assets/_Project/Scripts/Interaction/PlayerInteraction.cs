// ============================================================================
// HECTON-8 — PlayerInteraction.cs
// Core player interaction component. Attach to the Player prefab root.
// Performs throttled raycasts, manages hover state, dispatches interactions.
//
// PERFORMANCE NOTES:
//   - Raycasts throttled to 0.05s (20 checks/sec) — smooth hover, low CPU.
//   - LayerMask MUST be set to 'Interactable' layer — no full-scene sweeps.
//   - Zero GC allocations in Tick loop.
//   - Uses Physics.RaycastNonAlloc with QueryTriggerInteraction.Ignore.
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
//   - [REFACTOR v3] Unity.Mathematics for raycast origin offset calculation.
//     float3 arithmetic replaces Vector3 operator+ (same perf, consistent style).
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.UI;
    using Unity.Mathematics;
    using UnityEngine;
    using Hecton8.Audio;
    using Hecton8.Physics;
    using Hecton8.Input;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public sealed class PlayerInteraction : MonoBehaviour, ITickable
    {
        // ====================================================================
        // SERIALIZED CONFIGURATION
        // ====================================================================

        [Header("Raycast Settings")]

        [SerializeField,
         Tooltip("Maximum interaction reach in meters.")]
        private float reachDistance = 3.5f;

        [SerializeField,
         Tooltip("Seconds between raycast ticks. " +
                 "0.05 = 20 checks/sec for smooth hover.")]
        private float raycastInterval = 0.05f;

        [SerializeField,
         Tooltip("REQUIRED: Set to 'Interactable' layer. " +
                 "Never leave as Everything.")]
        private LayerMask interactableMask = 0;

        [SerializeField,
         Tooltip("Small offset to push ray origin forward, " +
                 "avoiding the player's own collider.")]
        private float rayOriginOffset = 0.1f;

        [Header("Audio Feedback")]

        [SerializeField,
         Tooltip("Quiet metallic click played when hovering " +
                 "over a new interactable.")]
        private AudioClip hoverSound;

        [SerializeField,
         Tooltip("Firm confirmation sound played on " +
                 "successful interaction.")]
        private AudioClip interactSound;

        [Header("References")]

        [SerializeField,
         Tooltip("Assign the player camera. If null, resolves " +
                 "from the local player hierarchy.")]
        private Camera playerCamera;

        [Header("Debug")]

        [SerializeField,
         Tooltip("Debug ray color when nothing is hovered.")]
        private Color debugRayMissColor = Color.yellow;

        [SerializeField,
         Tooltip("Debug ray color when an interactable is hovered.")]
        private Color debugRayHitColor = Color.green;

        // ====================================================================
        // INTERNAL STATE — Zero heap allocation, all stack/cached.
        // ====================================================================

        private IInteractable _currentHovered;
        private float         _raycastTimer;
        private Transform     _cameraTransform;
        private Hecton8.Interaction.PhysicalInteractionHandler _physicalInteractionHandler;
        private Ray           _ray;
        private RaycastHit    _hitInfo;
        private readonly RaycastHit[] _raycastHits = new RaycastHit[1]; // COLD ALLOC: single-hit interaction probe buffer.

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register (OnEnable +
        /// Start both succeeding) and orphan unregister.
        /// </summary>
        private bool          _registeredToTickManager;

        // ====================================================================
        // PUBLIC ACCESSORS
        // ====================================================================

        public IInteractable CurrentHovered => _currentHovered;

        /// <summary>
        /// Актуальная клавиша взаимодействия для подсказок UI.
        /// Возвращает строку (например, "E" или "Mouse0").
        /// </summary>
        public static string ActiveInteractKey
        {
            get
            {
                if (InputManager.Instance != null)
                    return InputManager.Instance.GetBindingDisplayString("Interact");
                return "E";
            }
        }

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            // ────────────────────────────────────────────────────
            // Camera resolution — done once, cached forever.
            // ────────────────────────────────────────────────────
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (playerCamera == null)
                {
                    Debug.LogError(
                        "[PlayerInteraction] No player camera assigned or found in the local player hierarchy. " +
                        "Assign the player camera in the Inspector.", this);
                    enabled = false;
                    return;
                }
#endif
            }

            _cameraTransform = playerCamera.transform;
            _raycastTimer    = 0f;
            _registeredToTickManager = false;
            TryGetComponent(out _physicalInteractionHandler);

            // ────────────────────────────────────────────────────
            // Layer mask validation — catch misconfiguration early.
            // ────────────────────────────────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (interactableMask.value == 0)
            {
                Debug.LogWarning(
                    "[PlayerInteraction] interactableMask is set to " +
                    "Nothing. Raycasts will hit nothing. Set it to " +
                    "the 'Interactable' layer.", this);
            }
            else if (interactableMask.value == ~0)
            {
                Debug.LogWarning(
                    "[PlayerInteraction] interactableMask is set to " +
                    "Everything. For performance, narrow it to the " +
                    "'Interactable' layer only.", this);
            }
#endif
        }

        // ====================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        //
        // Phase 1 (OnEnable): Attempt registration. If Instance is null
        //   (script execution order, scene loading), silently skip.
        //
        // Phase 2 (Start): Guaranteed retry. All Awake() calls completed.
        //   Debug.LogError ONLY if Instance still null.
        //
        // _registeredToTickManager flag guarantees:
        //   - Registration exactly once (no double-tick).
        //   - Unregister only if registered.
        //   - Safe during scene teardown.
        // ====================================================================

        private void OnEnable()
        {
            // Guard: GameTickManager may not exist yet (execution order).
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register(this);
                _registeredToTickManager = true;
            }

            // Subscribe to InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnInteract += HandleInteractInput;
            }
        }

        private void Start()
        {
            // Phase 2: deferred fallback. All Awake() calls completed.
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
                    "[PlayerInteraction] GameTickManager.Instance is " +
                    "null even at Start(). Tick-based interaction will " +
                    "NOT work. Ensure GameTickManager exists in the " +
                    "scene and is active.", this);
            }
        }

        private void OnDisable()
        {
            // Guard: singleton may be destroyed before this component.
            if (GameTickManager.Instance != null && _registeredToTickManager)
            {
                GameTickManager.Instance.Unregister(this);
                _registeredToTickManager = false;
            }

            // Unsubscribe from InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnInteract -= HandleInteractInput;
            }

            // Clean up hover state if disabled mid-hover.
            ClearHover();
        }

        private void HandleInteractInput()
        {
            if (_currentHovered == null)
                return;

            if (IsGameplayInputBlockedByMenu())
                return;

            ExecuteInteraction();
        }

        // ====================================================================
        // ITickable IMPLEMENTATION — Replaces native Update().
        // ====================================================================

        /// <summary>
        /// Main tick loop. Called by GameTickManager every frame.
        ///
        /// Phase 1: Throttled raycast (20Hz) — target acquisition.
        ///          NOT blocked by UI state — hover prompt must be
        ///          visible the instant a menu closes.
        ///
        /// Phase 2: Input poll (every tick) — action execution.
        ///          Blocked by UI state (HectonFabricatorUI.IsMenuOpen).
        ///
        /// Zero GC: Physics.RaycastNonAlloc, TryGetComponent,
        ///          ReferenceEquals, Input.GetKeyDown — all allocation-free.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Input is now handled via HandleInteractInput event callback.

            // ════════════════════════════════════════════════════
            // PHASE 1: THROTTLED RAYCAST — Target acquisition.
            // ════════════════════════════════════════════════════
            _raycastTimer += deltaTime;

            if (_raycastTimer >= raycastInterval)
            {
                _raycastTimer = 0f;
                PerformRaycast();
            }
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        // ====================================================================
        // CORE RAYCAST — Zero GC. Struct-only. Layer-targeted.
        // ====================================================================

        private void PerformRaycast()
        {
            // ── Unity.Mathematics for offset calculation ──
            Unity.Mathematics.float3 camPos = _cameraTransform.position;
            Unity.Mathematics.float3 camFwd = _cameraTransform.forward;

            Unity.Mathematics.float3 origin = camPos + camFwd * rayOriginOffset;

            _ray.origin    = origin;
            _ray.direction = camFwd;

            float effectiveReach = reachDistance - rayOriginOffset;

#if UNITY_EDITOR
            Debug.DrawRay(
                (Vector3)origin,
                (Vector3)(camFwd * effectiveReach),
                _currentHovered != null ? debugRayHitColor : debugRayMissColor,
                raycastInterval,
                false);
#endif

            // USE GLOBAL CACHE — Zero Redundancy
            var cache = GlobalQueryCacheManager.GetContext("PlayerLook");
            if (!cache.TryGet(_ray, effectiveReach, (int)interactableMask, out QueryResult qResult))
            {
                int hitCount = Physics.RaycastNonAlloc(
                    _ray,
                    _raycastHits,
                    effectiveReach,
                    interactableMask,
                    QueryTriggerInteraction.Ignore);

                bool hit = hitCount > 0;
                _hitInfo = hit ? _raycastHits[0] : default;
                qResult = new QueryResult { hasHit = hit, hit = hit ? _hitInfo : default };
                cache.Set(_ray, effectiveReach, (int)interactableMask, qResult);
            }
            else
            {
                _hitInfo = qResult.hit;
            }

            if (qResult.hasHit)
            {
                if (qResult.hit.collider.TryGetComponent(out IInteractable interactable))
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
            if (hoverSound != null
                && SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayStatic2D(
                    hoverSound, 0.3f);
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
            if (interactSound != null
                && SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayStatic2D(
                    interactSound, 0.6f);
            }

            if (_physicalInteractionHandler != null &&
                _physicalInteractionHandler.TryHandleInteraction(_currentHovered, transform))
            {
                InteractionEvents.RaiseInteractionStarted(
                    _currentHovered, transform);
                return;
            }

            _currentHovered.Interact(transform);
            InteractionEvents.RaiseInteractionStarted(
                _currentHovered, transform);
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

            Vector3 origin =
                _cameraTransform.position +
                _cameraTransform.forward * rayOriginOffset;
            float effectiveReach = reachDistance - rayOriginOffset;

            Gizmos.color = _currentHovered != null
                ? debugRayHitColor
                : new Color(1f, 1f, 0f, 0.5f);

            Gizmos.DrawRay(
                origin,
                _cameraTransform.forward * effectiveReach);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, 0.03f);

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(
                origin + _cameraTransform.forward * effectiveReach,
                0.05f);
        }
#endif
    }
}
