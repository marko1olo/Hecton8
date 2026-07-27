// ============================================================================
// HECTON-8 — PlayerInteraction.cs
// Core player interaction component. Attach to the Player prefab root.
// Runs throttled spatial target probes, manages hover state, dispatches interactions.
//
// PERFORMANCE NOTES:
//   - Spatial target probes throttled to 0.05s (20 checks/sec) - smooth hover, low CPU.
//   - LayerMask MUST be set to 'Interactable' layer — no full-scene sweeps.
//   - Zero GC allocations in Tick loop.
//   - Uses the fixed InteractableRegistry spatial cache with QueryTriggerInteraction.Ignore.
//   - Component caching via TryGetComponent (no GetComponent alloc on hit).
//   - ReferenceEquals for hover comparison — no boxing, no vtable dispatch.
//
// ARCHITECTURE:
//   - Integrated with GameTickManager via ITickable — native Update() is PROHIBITED.
//   - Spatial target probe tick (throttled) -> updates _currentHovered from the registry cache.
//   - Input poll (every tick) → reads _currentHovered, fires Interact().
//   - These two paths are fully decoupled: input is never gated by the target probe timer.
//   - UI State Guard: interaction input is blocked when any menu is open,
//     but spatial target probes continue so the hover prompt is refreshed immediately on close.
//
// AUDIO FEEDBACK:
//   - Hover transition → SpatialAudioManager.PlayStatic2D(hoverSound, 0.3f)
//   - Interact confirm → SpatialAudioManager.PlayStatic2D(interactSound, 0.6f)
//   - All clips are optional — null clips are silently skipped.
//
// REVISION NOTES:
//   - targetProbeInterval reduced to 0.05f for zero-latency hover feel.
//   - interactableMask defaults to Nothing — MUST be configured in Inspector.
//   - Debug.DrawRay persists for targetProbeInterval duration (continuous line).
//   - Hover events fire on every transition: clear→new, old→new, any→null.
//   - Migrated from Update() to ITickable.Tick(float) via GameTickManager.
//   - Added UI State Guard: interaction blocked while HectonFabricatorUI.IsMenuOpen.
//   - [FIX] OnEnable/OnDisable now null-safe against missing GameTickManager.
//   - [FIX] All singleton access guarded with null-checks.
//   - [FIX] Registration tracking flag prevents double-register/unregister.
//   - [REFACTOR] Deferred registration: OnEnable → attempt, Start → fallback,
//     Debug.LogError only if Instance still null at Start.
//   - [REFACTOR v3] Unity.Mathematics for target-probe origin offset calculation.
//     float3 arithmetic replaces Vector3 operator+ (same perf, consistent style).
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Inventory;
    using Hecton8.UI;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Serialization;
    using Hecton8.Audio;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player Interaction")]
    public sealed class PlayerInteraction : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IBootstrapProductionPlayerInteractionAuthority
    {
        private static int s_x001PlayerInteractionSignalPushDropCount;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private const string DefaultLookTargetPrompt = "INTERACT";
        private const string PlayerActionMapName = "Player";
        private const string InteractActionName = "Interact";
        private static readonly char[] s_promptScratch = new char[PlayerLookTargetPromptCache.MaxCharsPerPrompt];

        // ====================================================================
        // SERIALIZED CONFIGURATION
        // ====================================================================

        [Header("Target Probe Settings")]

        [SerializeField,
         Tooltip("Maximum interaction reach in meters.")]
        private float reachDistance = 3.5f;

        [FormerlySerializedAs("raycastInterval")]
        [SerializeField,
         Tooltip("Seconds between registered spatial target probes. 0.05 = 20 checks/sec for smooth hover.")]
        private float targetProbeInterval = 0.05f;

        [SerializeField,
         Tooltip("REQUIRED: Set to 'Interactable' layer. Never leave as Everything.")]
        private LayerMask interactableMask = 0;

        [SerializeField,
         Tooltip("Small offset to push ray origin forward, avoiding the player's own collider.")]
        private float rayOriginOffset = 0.1f;

        [Header("Audio Feedback")]

        [SerializeField,
         Tooltip("Quiet metallic click played when hovering over a new interactable.")]
        private AudioClip hoverSound;

        [SerializeField,
         Tooltip("Firm confirmation sound played on successful interaction.")]
        private AudioClip interactSound;

        [Header("References")]

        [SerializeField,
         Tooltip("Assign the player camera. If null, resolves from the local player hierarchy.")]
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
        private InteractableRegistry.TargetInfo _currentTargetInfo;
        private float         _targetProbeTimer;
        private Transform     _cameraTransform;
        private Hecton8.Interaction.PhysicalInteractionHandler _physicalInteractionHandler;
        private IAudioService _audioService;
        private IPlayerInventoryService _playerInventoryService;
        private IInputBindingService _subscribedInputBindingService;
        private Ray           _ray;
        private static readonly int _DefaultInteractableLayerMask = HectonLayerMasks.InteractableLayerMask;
        private static string _activeInteractKey = "E";
        private AudioClip _pendingStaticAudio0;
        private AudioClip _pendingStaticAudio1;
        private float _pendingStaticAudioVolume0;
        private float _pendingStaticAudioVolume1;
        private int _pendingStaticAudioCount;

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register (OnEnable +
        /// Start both succeeding) and orphan unregister.
        /// </summary>
        private bool          _registeredToTickManager;
        private bool          _registeredToLateFrameTick;
        private bool          _hotSwapListenerRegistered;
        private uint          _lastPlayerInputSignalSequence;
        private Action<string, string, int, string> _rebindCompletedAction;
        private Action<string, string, int> _rebindCanceledAction;
        private Action _bindingOverridesChangedAction;

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
                    Hecton8.Core.H8Debug.LogError("[PlayerInteraction] No player camera assigned or found in the local player hierarchy.", this);
#endif
                    enabled = false;
                    return;
                }
            }

            _cameraTransform = playerCamera.transform;
            _targetProbeTimer = 0f;
            _registeredToTickManager = false;
            _registeredToLateFrameTick = false;
            _hotSwapListenerRegistered = false;
            if (!TryGetComponent(out _physicalInteractionHandler))
            {
                _physicalInteractionHandler = gameObject.AddComponent<PhysicalInteractionHandler>(); // COLD ALLOC: PhysicalInteractionHandler[1] - player-owned physical pickup/heavy-carry route - owner: PlayerInteraction
            }

            RefreshActiveInteractKeyCache();
            if (Application.isPlaying)
                InteractionEvents.PrewarmCold();

            // ────────────────────────────────────────────────────
            // Layer mask validation — catch misconfiguration early.
            // ────────────────────────────────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (interactableMask.value == 0)
            {
                Hecton8.Core.H8Debug.LogWarning("[PlayerInteraction] interactableMask is set to Nothing. Probing with the Interactable route default instead.", this);
            }
            else if (HectonLayerMasks.IsEverythingLayerMask(interactableMask.value))
            {
                Hecton8.Core.H8Debug.LogWarning("[PlayerInteraction] interactableMask is set to Everything. Probing with the Interactable route default instead.", this);
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
            EnsureCachedBindingDelegates();
            RefreshCachedRegistryServices();
            SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);
            if (Application.isPlaying)
            {
                InteractableRegistry.EnsureSceneRegistryCold();
                BaselineInteractInputSignalSequence();
            }
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
            EnsureCachedBindingDelegates();
            RefreshCachedRegistryServices();
            SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);
            if (Application.isPlaying)
            {
                InteractableRegistry.EnsureSceneRegistryCold();
                BaselineInteractInputSignalSequence();
            }
            RefreshActiveInteractKeyCache();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredToTickManager = false;
            }

            TryUnregisterLateFrameTickable();
            UnsubscribeInputBindingService();
            TryUnregisterHotSwapListener();
            ClearQueuedStaticAudio();
            _audioService = null;
            _playerInventoryService = null;

            // Clean up hover state if disabled mid-hover.
            ClearHover();
        }

        private void OnDestroy()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredToTickManager = false;
            }

            TryUnregisterLateFrameTickable();
            UnsubscribeInputBindingService();
            TryUnregisterHotSwapListener();
            ClearQueuedStaticAudio();
            _audioService = null;
            _playerInventoryService = null;
            ClearHover();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    return;

                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    return;

                case GlobalRegistryServiceSlot.Input:
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    if (!isActiveAndEnabled)
                        return;

                    BaselineInteractInputSignalSequence();
                    RefreshActiveInteractKeyCache();
                    return;

                case GlobalRegistryServiceSlot.InputBinding:
                    UnsubscribeInputBindingService();
                    if (!isActiveAndEnabled)
                        return;

                    SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);
                    RefreshActiveInteractKeyCache();
                    return;

                case GlobalRegistryServiceSlot.Dispatcher:
                    if (_registeredToTickManager)
                    {
                        GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                        _registeredToTickManager = false;
                    }

                    TryUnregisterLateFrameTickable();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
                        if (_pendingStaticAudioCount > 0)
                            TryRegisterLateFrameTickable();
                    }
                    return;
            }
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

        private void EnsureCachedBindingDelegates()
        {
            _rebindCompletedAction ??= HandleRebindCompleted; // COLD ALLOC: Action<string,string,int,string>[1] - cached input binding listener - owner: PlayerInteraction
            _rebindCanceledAction ??= HandleRebindCanceled; // COLD ALLOC: Action<string,string,int>[1] - cached input binding listener - owner: PlayerInteraction
            _bindingOverridesChangedAction ??= HandleBindingOverridesChanged; // COLD ALLOC: Action[1] - cached input binding listener - owner: PlayerInteraction
        }

        private void SubscribeInputBindingServiceIfAvailable(IInputBindingService bindingService)
        {
            if (_subscribedInputBindingService != null || bindingService == null)
                return;

            EnsureCachedBindingDelegates();
            _subscribedInputBindingService = bindingService;
            _subscribedInputBindingService.OnRebindCompleted += _rebindCompletedAction;
            _subscribedInputBindingService.OnRebindCanceled += _rebindCanceledAction;
            _subscribedInputBindingService.OnOverridesLoaded += _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesSaved += _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesCleared += _bindingOverridesChangedAction;
        }

        private void UnsubscribeInputBindingService()
        {
            if (_subscribedInputBindingService == null)
                return;

            _subscribedInputBindingService.OnRebindCompleted -= _rebindCompletedAction;
            _subscribedInputBindingService.OnRebindCanceled -= _rebindCanceledAction;
            _subscribedInputBindingService.OnOverridesLoaded -= _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesSaved -= _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesCleared -= _bindingOverridesChangedAction;
            _subscribedInputBindingService = null;
        }

        private static void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RefreshActiveInteractKeyCache();
        }

        private static void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RefreshActiveInteractKeyCache();
        }

        private static void HandleBindingOverridesChanged()
        {
            RefreshActiveInteractKeyCache();
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

        private void RefreshCachedRegistryServices()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _playerInventoryService = GlobalRegistry.PlayerInventory;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void QueueStaticAudio(AudioClip clip, float volume)
        {
            if (clip == null || ResolveAudioService() == null)
                return;

            switch (_pendingStaticAudioCount)
            {
                case 0:
                    _pendingStaticAudio0 = clip;
                    _pendingStaticAudioVolume0 = math.saturate(volume);
                    _pendingStaticAudioCount = 1;
                    break;
                case 1:
                    _pendingStaticAudio1 = clip;
                    _pendingStaticAudioVolume1 = math.saturate(volume);
                    _pendingStaticAudioCount = 2;
                    break;
                default:
                    return;
            }

            TryRegisterLateFrameTickable();
        }

        private void FlushQueuedStaticAudio()
        {
            int count = _pendingStaticAudioCount;
            if (count <= 0)
            {
                TryUnregisterLateFrameTickable();
                return;
            }

            AudioClip clip0 = _pendingStaticAudio0;
            AudioClip clip1 = _pendingStaticAudio1;
            float volume0 = _pendingStaticAudioVolume0;
            float volume1 = _pendingStaticAudioVolume1;
            ClearQueuedStaticAudio();

            IAudioService audioService = ResolveAudioService();
            if (audioService != null)
            {
                if (count > 0 && clip0 != null)
                    audioService.PlayStatic2D(clip0, volume0);
                if (count > 1 && clip1 != null)
                    audioService.PlayStatic2D(clip1, volume1);
            }

            TryUnregisterLateFrameTickable();
        }

        private void ClearQueuedStaticAudio()
        {
            _pendingStaticAudio0 = null;
            _pendingStaticAudio1 = null;
            _pendingStaticAudioVolume0 = 0f;
            _pendingStaticAudioVolume1 = 0f;
            _pendingStaticAudioCount = 0;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredToLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredToLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredToLateFrameTick = false;
        }

        private static void RefreshActiveInteractKeyCache()
        {
            INativeInputManagerRuntime inputManager = GlobalRegistry.NativeInputRuntime;
            if (inputManager == null)
            {
                _activeInteractKey = "E";
                return;
            }

            string display = inputManager.GetBindingDisplayString(InteractActionName, PlayerActionMapName);
            _activeInteractKey = string.IsNullOrEmpty(display) ? "E" : display;
        }

        // ====================================================================
        // ITickable IMPLEMENTATION — Replaces native Update().
        // ====================================================================

        /// <summary>
        /// Main tick loop. Called by GameTickManager every frame.
        ///
                /// Phase 1: Throttled spatial target probe (20Hz) - target acquisition.
        ///          NOT blocked by UI state — hover prompt must be
        ///          visible the instant a menu closes.
        ///
        /// Phase 2: Input poll (every tick) — action execution.
        ///          Blocked by UI state (HectonFabricatorUI.IsMenuOpen).
        ///
        /// Zero GC: fixed spatial registry scan, TryGetComponent,
        ///          ReferenceEquals, dispatcher action latch — all allocation-free.
        /// </summary>
        public void Tick(float deltaTime)
        {
            ConsumeInteractInputSignals();

            // ════════════════════════════════════════════════════
            // PHASE 1: THROTTLED RAYCAST — Target acquisition.
            // ════════════════════════════════════════════════════
            _targetProbeTimer += deltaTime;

            if (_targetProbeTimer >= targetProbeInterval)
            {
                _targetProbeTimer = 0f;
                ResolveHoveredTarget();
            }
        }

        public void LateFrameTick()
        {
            FlushQueuedStaticAudio();
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        // ====================================================================
        // CORE RAYCAST — Zero GC. Struct-only. Layer-targeted.
        // ====================================================================

        private void ResolveHoveredTarget()
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
                targetProbeInterval,
                false);
#endif

            // USE GLOBAL CACHE — Zero Redundancy
            int resolvedInteractableMask = ResolveInteractableLayerMask();
            const QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Ignore;
            if (InteractableRegistry.TryResolveSpatialTarget(
                    in _ray,
                    effectiveReach,
                    resolvedInteractableMask,
                    triggerMode,
                    out InteractableRegistry.SpatialHit hit))
            {
                ApplyInteractionSpatialHit(in hit);
                return;
            }

            if (_currentHovered != null)
                ClearHover();
        }

        private void ApplyInteractionSpatialHit(in InteractableRegistry.SpatialHit hit)
        {
            if (hit.HasHit && hit.TargetInfo.Interactable != null)
            {
                IInteractable interactable = hit.TargetInfo.Interactable;
                if (ReferenceEquals(interactable, _currentHovered))
                {
                    _currentPickupSource = hit.TargetInfo.PickupSource;
                    _currentTargetInfo = hit.TargetInfo;
                    PublishLookTargetSignal(interactable, in hit, PlayerLookTargetSignalStates.Acquired);
                    return;
                }

                ClearHover();
                SetHover(interactable, hit.TargetInfo.PickupSource, in hit);
                return;
            }

            if (_currentHovered != null)
                ClearHover();
        }

        private int ResolveInteractableLayerMask()
        {
            // Nothing (0) is the serialized default of this field and rejects every
            // registered collider in InteractableRegistry.LayerIncluded, which kills the
            // whole aim -> query hop with no exception and no player-build log. Resolve
            // both Nothing and Everything to the declared route default.
            return InteractionProbeLayerMask.Resolve(interactableMask.value, _DefaultInteractableLayerMask);
        }

        // ====================================================================
        // HOVER STATE MANAGEMENT
        // ====================================================================

        private void SetHover(IInteractable target, IInventoryPickupSource pickupSource, in InteractableRegistry.SpatialHit hit)
        {
            _currentHovered = target;
            _currentPickupSource = pickupSource;
            _currentTargetInfo = hit.TargetInfo;
            _currentHovered.OnHoverStart();

            // Audio: subtle metallic click on hover acquisition.
            QueueStaticAudio(hoverSound, 0.3f);

            InteractionEvents.TryRaiseHoverChanged(_currentHovered);
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
            _currentTargetInfo = default;

            InteractionEvents.TryRaiseHoverChanged(null);
        }

        private static void PublishLookTargetSignal(IInteractable target, in InteractableRegistry.SpatialHit hit, byte state)
        {
            PlayerLookTargetSignal signal = default;
            signal.State = state;
            signal.Frame = SystemDispatcher.CurrentFrameId;

            if (state == PlayerLookTargetSignalStates.Cleared || target == null)
            {
                SignalBus<PlayerLookTargetSignal>.TryPushTracked(in signal, ref s_x001PlayerInteractionSignalPushDropCount);
                return;
            }

            Vector3 anchor = ResolveLookTargetAnchor(target, in hit);
            signal.RuntimeAnchor.x = anchor.x;
            signal.RuntimeAnchor.y = anchor.y;
            signal.RuntimeAnchor.z = anchor.z;
            if (!TryResolveAupFromRuntimeOrigin(anchor, out signal.TargetAup))
                return;

            signal.DistanceMeters = math.isfinite(hit.Distance) && hit.Distance >= 0f ? hit.Distance : 0f;
            signal.SurfaceNormal = ResolveLookTargetNormal(in hit);
            signal.TargetHash = ResolveTargetHash(target);
            signal.ColliderHash = hit.Collider != null ? unchecked((uint)EntityId.ToULong(hit.Collider.GetEntityId())) : 0u;

            ReadOnlySpan<char> promptSpan = ResolvePromptSpan(target);
            signal.PromptHash = ComputePromptHash(promptSpan);
            PlayerLookTargetPromptCache.Store(signal.PromptHash, promptSpan);
            SignalBus<PlayerLookTargetSignal>.TryPushTracked(in signal, ref s_x001PlayerInteractionSignalPushDropCount);
        }

        private static ReadOnlySpan<char> ResolvePromptSpan(IInteractable target)
        {
            if (target is IInteractableTextProvider textProvider &&
                textProvider.TryCopyInteractText(s_promptScratch, out int length) &&
                length > 0)
            {
                return s_promptScratch.AsSpan(0, math.min(length, s_promptScratch.Length));
            }

            return DefaultLookTargetPrompt.AsSpan();
        }

        private static Vector3 ResolveLookTargetAnchor(IInteractable target, in InteractableRegistry.SpatialHit hit)
        {
            if (hit.Collider != null
                && math.isfinite(hit.Point.x)
                && math.isfinite(hit.Point.y)
                && math.isfinite(hit.Point.z))
            {
                return hit.Point;
            }

            Component component = target as Component;
            return component != null ? component.transform.position : Vector3.zero;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static float3 ResolveLookTargetNormal(in InteractableRegistry.SpatialHit hit)
        {
            Vector3 normal = hit.Collider != null ? hit.Normal : Vector3.up;
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

        private static uint ComputePromptHash(ReadOnlySpan<char> prompt)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (prompt.IsEmpty)
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
            IInteractable target = _currentHovered;
            if (_physicalInteractionHandler != null &&
                _physicalInteractionHandler.TryHandleInteraction(target, transform, in _currentTargetInfo))
            {
                QueueDefaultInteractionFeedback(target);
                InteractionEvents.TryRaiseInteractionStarted(
                    target, transform);
                return;
            }

            PlayerInventory inventory = _playerInventoryService != null ? _playerInventoryService.Inventory : null;
            if (_currentPickupSource != null &&
                inventory != null &&
                _currentPickupSource.TryHandleInventoryPickup(inventory, transform))
            {
                QueueDefaultInteractionFeedback(target);
                InteractionEvents.TryRaiseInteractionStarted(
                    target, transform);
                return;
            }

            target.Interact(transform);
            QueueDefaultInteractionFeedback(target);
            TryRaiseDefaultInteractionStarted(target, transform);
        }

        private static bool TryRaiseDefaultInteractionStarted(IInteractable target, Transform interactor)
        {
            if (IsInteractionStartedEventOwner(target))
                return false;

            return InteractionEvents.TryRaiseInteractionStarted(target, interactor);
        }

        private void QueueDefaultInteractionFeedback(IInteractable target)
        {
            if (IsInteractionStartedEventOwner(target))
                return;

            QueueStaticAudio(interactSound, 0.6f);
        }

        private static bool IsInteractionStartedEventOwner(IInteractable target)
        {
            return target is IInteractionStartedEventOwner;
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

