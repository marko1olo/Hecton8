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
    using Hecton8.Inventory;
    using Hecton8.UI;
    using Unity.Mathematics;
    using UnityEngine;
    using Hecton8.Audio;
    using Hecton8.Physics;
    using Hecton8.Input;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public sealed class PlayerInteraction : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
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
        private IInventoryPickupSource _currentPickupSource;
        private float         _raycastTimer;
        private Transform     _cameraTransform;
        private Hecton8.Interaction.PhysicalInteractionHandler _physicalInteractionHandler;
        private IInputService _subscribedInputService;
        private QueryCacheContext _playerLookQueryCache;
        private Ray           _ray;
        private RaycastHit    _hitInfo;
        // COLD ALLOC: RaycastHit[4] - bounded interaction probe buffer - owner: PlayerInteraction
        private const int MaxRaycastHits = 4;
        private readonly RaycastHit[] _raycastHits = new RaycastHit[MaxRaycastHits];
        private static readonly int _DefaultInteractableLayerMask = HectonLayerMasks.InteractableLayerMask;
        private static string _activeInteractKey = "E";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _raycastBufferSaturationLogged;
#endif

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register (OnEnable +
        /// Start both succeeding) and orphan unregister.
        /// </summary>
        private bool          _registeredToTickManager;
        private bool          _hotSwapListenerRegistered;

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
            get { return _activeInteractKey; }
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
                playerCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);

                if (playerCamera == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        "[PlayerInteraction] No player camera assigned or found in the local player hierarchy. " +
                        "Assign the player camera in the Inspector.", this);
#endif
                    enabled = false;
                    return;
                }
            }

            _cameraTransform = playerCamera.transform;
            _raycastTimer    = 0f;
            _registeredToTickManager = false;
            _hotSwapListenerRegistered = false;
            TryGetComponent(out _physicalInteractionHandler);
            _playerLookQueryCache = GlobalQueryCacheManager.PlayerLook;
            RefreshActiveInteractKeyCache();

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
            else if (HectonLayerMasks.IsEverythingLayerMask(interactableMask.value))
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
            if (!_registeredToTickManager && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            TryRegisterHotSwapListener();
            SubscribeInputServiceIfAvailable();
            RefreshActiveInteractKeyCache();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToTickManager && GlobalRegistry.Dispatcher != null)
            {
                _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }

            TryRegisterHotSwapListener();
            SubscribeInputServiceIfAvailable();
            RefreshActiveInteractKeyCache();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredToTickManager = false;
            }

            TryUnregisterHotSwapListener();
            UnsubscribeInputService();

            // Clean up hover state if disabled mid-hover.
            ClearHover();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            UnsubscribeInputService();

            if (!isActiveAndEnabled)
                return;

            SubscribeInputServiceIfAvailable(currentService as IInputService);
            RefreshActiveInteractKeyCache();
        }

        private void HandleInteractInput()
        {
            if (IsGameplayInputBlockedByMenu())
                return;

            if (_currentHovered == null)
            {
                if (_physicalInteractionHandler != null &&
                    _physicalInteractionHandler.TryBeginFloraHarvestSnap())
                {
                    return;
                }

                return;
            }

            ExecuteInteraction();
        }

        private void SubscribeInputServiceIfAvailable()
        {
            SubscribeInputServiceIfAvailable(GlobalRegistry.Input);
        }

        private void SubscribeInputServiceIfAvailable(IInputService inputService)
        {
            if (_subscribedInputService != null)
                return;

            if (inputService == null || !inputService.IsInitialized)
                return;

            _subscribedInputService = inputService;
            _subscribedInputService.OnInteract += HandleInteractInput;
        }

        private void UnsubscribeInputService()
        {
            if (_subscribedInputService == null)
                return;

            _subscribedInputService.OnInteract -= HandleInteractInput;
            _subscribedInputService = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static void RefreshActiveInteractKeyCache()
        {
            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null)
            {
                _activeInteractKey = "E";
                return;
            }

            string display = inputManager.GetBindingDisplayString("Interact");
            _activeInteractKey = string.IsNullOrEmpty(display) ? "E" : display;
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

            float effectiveReach = math.max(0.01f, reachDistance - rayOriginOffset);

#if UNITY_EDITOR
            Debug.DrawRay(
                (Vector3)origin,
                (Vector3)(camFwd * effectiveReach),
                _currentHovered != null ? debugRayHitColor : debugRayMissColor,
                raycastInterval,
                false);
#endif

            // USE GLOBAL CACHE — Zero Redundancy
            QueryCacheContext cache = _playerLookQueryCache ?? GlobalQueryCacheManager.PlayerLook;
            _playerLookQueryCache = cache;
            int resolvedInteractableMask = ResolveInteractableLayerMask();
            const QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Ignore;
            if (!cache.TryGet(_ray, effectiveReach, resolvedInteractableMask, triggerMode, out QueryResult qResult))
            {
                int hitCount = Physics.RaycastNonAlloc(
                    _ray,
                    _raycastHits,
                    effectiveReach,
                    resolvedInteractableMask,
                    triggerMode);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (hitCount >= _raycastHits.Length && !_raycastBufferSaturationLogged)
                {
                    _raycastBufferSaturationLogged = true;
                    Debug.LogWarning(
                        "[PlayerInteraction] Interaction raycast buffer saturated. " +
                        "Increase MaxRaycastHits or narrow interactableMask.", this);
                }
#endif

                bool hit = TryResolveNearestRegisteredRaycastHit(hitCount, out _hitInfo);
                qResult = new QueryResult { hasHit = hit, hit = hit ? _hitInfo : default };
                cache.Set(_ray, effectiveReach, resolvedInteractableMask, triggerMode, qResult);
            }
            else
            {
                _hitInfo = qResult.hit;
            }

            if (qResult.hasHit)
            {
                Collider hitCollider = qResult.hit.collider;
                if (InteractableRegistry.TryResolve(hitCollider, out InteractableRegistry.TargetInfo targetInfo) &&
                    targetInfo.Interactable != null)
                {
                    IInteractable interactable = targetInfo.Interactable;
                    if (ReferenceEquals(interactable, _currentHovered))
                        return;

                    ClearHover();
                    SetHover(interactable, targetInfo.PickupSource);
                    return;
                }
            }

            if (_currentHovered != null)
            {
                ClearHover();
            }
        }

        private bool TryResolveNearestRegisteredRaycastHit(int hitCount, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = float.MaxValue;
            int count = math.min(hitCount, _raycastHits.Length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _raycastHits[i];
                _raycastHits[i] = default;
                Collider candidateCollider = candidate.collider;
                if (candidateCollider == null ||
                    candidate.distance >= nearestDistance ||
                    !InteractableRegistry.TryResolve(candidateCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                    targetInfo.Interactable == null)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                nearestHit = candidate;
            }

            return nearestDistance < float.MaxValue;
        }

        private int ResolveInteractableLayerMask()
        {
            int mask = interactableMask.value;
            return HectonLayerMasks.IsEverythingLayerMask(mask) ? _DefaultInteractableLayerMask : mask;
        }

        // ====================================================================
        // HOVER STATE MANAGEMENT
        // ====================================================================

        private void SetHover(IInteractable target, IInventoryPickupSource pickupSource)
        {
            _currentHovered = target;
            _currentPickupSource = pickupSource;
            _currentHovered.OnHoverStart();

            // Audio: subtle metallic click on hover acquisition.
            if (hoverSound != null
                && Hecton8.Core.GlobalRegistry.Audio != null)
            {
                Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(
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
            _currentPickupSource = null;

            InteractionEvents.RaiseHoverChanged(null);
        }

        // ====================================================================
        // INTERACTION EXECUTION
        // ====================================================================

        private void ExecuteInteraction()
        {
            // Audio: firm metallic confirmation.
            if (interactSound != null
                && Hecton8.Core.GlobalRegistry.Audio != null)
            {
                Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(
                    interactSound, 0.6f);
            }

            if (_currentPickupSource != null &&
                _currentPickupSource.TryHandleInventoryPickup(Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime, transform))
            {
                InteractionEvents.RaiseInteractionStarted(
                    _currentHovered, transform);
                return;
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
            float effectiveReach = math.max(0.01f, reachDistance - rayOriginOffset);

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

