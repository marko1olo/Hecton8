// ============================================================================
// HECTON-8 — PlayerInteraction.cs
// Core player interaction component. Attach to the Player prefab root.
// Queues throttled async raycasts, manages hover state, dispatches interactions.
//
// PERFORMANCE NOTES:
//   - Async raycasts throttled to 0.05s (20 checks/sec) — smooth hover, low CPU.
//   - LayerMask MUST be set to 'Interactable' layer — no full-scene sweeps.
//   - Zero GC allocations in Tick loop.
//   - Uses dispatcher-owned RaycastCommand with QueryTriggerInteraction.Ignore.
//   - Component caching via TryGetComponent (no GetComponent alloc on hit).
//   - ReferenceEquals for hover comparison — no boxing, no vtable dispatch.
//
// ARCHITECTURE:
//   - Integrated with GameTickManager via ITickable — native Update() is PROHIBITED.
//   - Async raycast tick (throttled) → updates _currentHovered from a late-frame result.
//   - Input poll (every tick) → reads _currentHovered, fires Interact().
//   - These two paths are fully decoupled: input is never gated by raycast timer.
//   - UI State Guard: interaction input is blocked when any menu is open,
//     but async raycasts continue so the hover prompt is refreshed immediately on close.
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
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Inventory;
    using Hecton8.UI;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;
    using Hecton8.Audio;
    using Hecton8.Physics;
    using Hecton8.Input;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public sealed class PlayerInteraction : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener, IDispatcherRaycastReceiver
    {
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

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
        private QueryCacheContext _playerLookQueryCache;
        private Ray           _ray;
        private Ray           _pendingRaycastRay;
        private float         _pendingRaycastReach;
        private int           _pendingRaycastMask;
        private int           _raycastRequestSequence;
        private int           _pendingRaycastRequestId;
        private bool          _raycastPending;
        private QueryTriggerInteraction _pendingRaycastTriggerMode;
        private static readonly int _DefaultInteractableLayerMask = HectonLayerMasks.InteractableLayerMask;
        private static string _activeInteractKey = "E";

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register (OnEnable +
        /// Start both succeeding) and orphan unregister.
        /// </summary>
        private bool          _registeredToTickManager;
        private bool          _hotSwapListenerRegistered;
        private uint          _lastPlayerInputSignalSequence;

        // ====================================================================
        // PUBLIC ACCESSORS
        // ====================================================================

        public IInteractable CurrentHovered => _currentHovered;

        /// <summary>
        /// Aktualnaya klavisha vzaimodeystviya dlya podskazok UI.
        /// Vozvraschaet stroku (naprimer, "E" ili "Mouse0").
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
            if (Application.isPlaying)
                BaselineInteractInputSignalSequence();
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
            if (Application.isPlaying)
                BaselineInteractInputSignalSequence();
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
            _raycastPending = false;
            _pendingRaycastRequestId = 0;

            // Clean up hover state if disabled mid-hover.
            ClearHover();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input &&
                serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                return;

            if (!isActiveAndEnabled)
                return;

            BaselineInteractInputSignalSequence();
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

        private void ConsumeInteractInputSignals()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    signal.Command != PlayerInputSignalCommands.Interact ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                HandleInteractInput();
                return;
            }
        }

        private void BaselineInteractInputSignalSequence()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
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
        /// Zero GC: dispatcher RaycastCommand, TryGetComponent,
        ///          ReferenceEquals, dispatcher action latch — all allocation-free.
        /// </summary>
        public void Tick(float deltaTime)
        {
            ConsumeInteractInputSignals();

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
            if (cache.TryGet(_ray, effectiveReach, resolvedInteractableMask, triggerMode, out QueryResult qResult))
            {
                ApplyInteractionQueryResult(in qResult);
                return;
            }

            QueueInteractionRaycast(in _ray, effectiveReach, resolvedInteractableMask, triggerMode);
        }

        private void QueueInteractionRaycast(
            in Ray ray,
            float effectiveReach,
            int resolvedInteractableMask,
            QueryTriggerInteraction triggerMode)
        {
            if (_raycastPending)
                return;

            RaycastCommand command = default;
            command.from = ray.origin;
            command.direction = ray.direction;
            command.distance = effectiveReach;
            command.queryParameters = new QueryParameters(resolvedInteractableMask, false, triggerMode);

            int requestId = NextRaycastRequestId();
            if (SystemDispatcher.QueueDispatcherRaycast(this, requestId, in command))
            {
                _raycastPending = true;
                _pendingRaycastRequestId = requestId;
                _pendingRaycastRay = ray;
                _pendingRaycastReach = effectiveReach;
                _pendingRaycastMask = resolvedInteractableMask;
                _pendingRaycastTriggerMode = triggerMode;
                return;
            }

            if (_currentHovered != null)
                ClearHover();
        }

        private int NextRaycastRequestId()
        {
            unchecked
            {
                _raycastRequestSequence++;
                if (_raycastRequestSequence == 0)
                    _raycastRequestSequence = 1;
            }

            return _raycastRequestSequence;
        }

        void IDispatcherRaycastReceiver.ConsumeDispatcherRaycastHit(int requestId, in RaycastHit hit)
        {
            if (!_raycastPending || requestId != _pendingRaycastRequestId)
                return;

            _raycastPending = false;
            _pendingRaycastRequestId = 0;

            bool resolved = TryResolveRegisteredRaycastHit(in hit, out RaycastHit resolvedHit);
            QueryResult qResult = new QueryResult
            {
                hasHit = resolved,
                hit = resolved ? resolvedHit : default
            };

            QueryCacheContext cache = _playerLookQueryCache ?? GlobalQueryCacheManager.PlayerLook;
            _playerLookQueryCache = cache;
            cache.Set(_pendingRaycastRay, _pendingRaycastReach, _pendingRaycastMask, _pendingRaycastTriggerMode, qResult);

            if (isActiveAndEnabled)
                ApplyInteractionQueryResult(in qResult);
        }

        private static bool TryResolveRegisteredRaycastHit(in RaycastHit candidate, out RaycastHit registeredHit)
        {
            registeredHit = default;
            Collider candidateCollider = candidate.collider;
            if (candidateCollider == null ||
                !math.isfinite(candidate.distance) ||
                candidate.distance < 0f ||
                !InteractableRegistry.TryResolve(candidateCollider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.Interactable == null)
            {
                return false;
            }

            registeredHit = candidate;
            return true;
        }

        private void ApplyInteractionQueryResult(in QueryResult qResult)
        {
            if (qResult.hasHit)
            {
                Collider hitCollider = qResult.hit.collider;
                if (InteractableRegistry.TryResolve(hitCollider, out InteractableRegistry.TargetInfo targetInfo) &&
                    targetInfo.Interactable != null)
                {
                    IInteractable interactable = targetInfo.Interactable;
                    if (ReferenceEquals(interactable, _currentHovered))
                    {
                        PublishLookTargetSignal(interactable, in qResult.hit, PlayerLookTargetSignalStates.Acquired);
                        return;
                    }

                    ClearHover();
                    SetHover(interactable, targetInfo.PickupSource, in qResult.hit);
                    return;
                }
            }

            if (_currentHovered != null)
                ClearHover();
        }

        private int ResolveInteractableLayerMask()
        {
            int mask = interactableMask.value;
            return HectonLayerMasks.IsEverythingLayerMask(mask) ? _DefaultInteractableLayerMask : mask;
        }

        // ====================================================================
        // HOVER STATE MANAGEMENT
        // ====================================================================

        private void SetHover(IInteractable target, IInventoryPickupSource pickupSource, in RaycastHit hit)
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
            PublishLookTargetSignal(_currentHovered, in hit, PlayerLookTargetSignalStates.Acquired);
        }

        private void ClearHover()
        {
            if (_currentHovered == null)
                return;

            PublishLookTargetSignal(_currentHovered, default, PlayerLookTargetSignalStates.Cleared);
            _currentHovered.OnHoverEnd();
            _currentHovered = null;
            _currentPickupSource = null;

            InteractionEvents.RaiseHoverChanged(null);
        }

        private static void PublishLookTargetSignal(IInteractable target, in RaycastHit hit, byte state)
        {
            PlayerLookTargetSignal signal = default;
            signal.State = state;
            signal.Frame = unchecked((uint)Time.frameCount);

            if (state == PlayerLookTargetSignalStates.Cleared || target == null)
            {
                SignalBus<PlayerLookTargetSignal>.Push(in signal);
                return;
            }

            Vector3 anchor = ResolveLookTargetAnchor(target, in hit);
            signal.RuntimeAnchor.x = anchor.x;
            signal.RuntimeAnchor.y = anchor.y;
            signal.RuntimeAnchor.z = anchor.z;
            signal.TargetAup = AbsoluteUniversePosition.FromRuntimePosition(anchor);
            signal.DistanceMeters = math.isfinite(hit.distance) && hit.distance >= 0f ? hit.distance : 0f;
            signal.SurfaceNormal = ResolveLookTargetNormal(in hit);
            signal.TargetHash = ResolveTargetHash(target);
            signal.ColliderHash = hit.collider != null ? unchecked((uint)EntityId.ToULong(hit.collider.GetEntityId())) : 0u;

            string prompt = target.GetInteractText();
            if (string.IsNullOrEmpty(prompt))
                prompt = "OPEN HATCH";

            signal.PromptHash = ComputePromptHash(prompt);
            PlayerLookTargetPromptCache.Store(signal.PromptHash, prompt);
            SignalBus<PlayerLookTargetSignal>.Push(in signal);
        }

        private static Vector3 ResolveLookTargetAnchor(IInteractable target, in RaycastHit hit)
        {
            if (hit.collider != null
                && math.isfinite(hit.point.x)
                && math.isfinite(hit.point.y)
                && math.isfinite(hit.point.z))
            {
                return hit.point;
            }

            Component component = target as Component;
            return component != null ? component.transform.position : Vector3.zero;
        }

        private static float3 ResolveLookTargetNormal(in RaycastHit hit)
        {
            Vector3 normal = hit.collider != null ? hit.normal : Vector3.up;
            float3 resolved = default;
            resolved.x = normal.x;
            resolved.y = normal.y;
            resolved.z = normal.z;
            if (math.all(math.isfinite(resolved)))
                return resolved;

            resolved.x = 0f;
            resolved.y = 1f;
            resolved.z = 0f;
            return resolved;
        }

        private static uint ResolveTargetHash(IInteractable target)
        {
            Component component = target as Component;
            return component != null ? unchecked((uint)EntityId.ToULong(component.GetEntityId())) : 0u;
        }

        private static uint ComputePromptHash(string prompt)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (string.IsNullOrEmpty(prompt))
                return hash;

            for (int i = 0; i < prompt.Length; i++)
            {
                hash ^= prompt[i];
                hash *= fnvPrime;
            }

            return hash != 0u ? hash : 1u;
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

