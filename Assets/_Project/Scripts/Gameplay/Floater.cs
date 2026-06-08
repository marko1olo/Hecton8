// ============================================================================
// HECTON-8 — Floater.cs
// Small organism/device that can be attached to objects to make them float.
//
// ARCHITECTURE:
//   • Standalone prop — implements IInteractable.
//   • State machine: Idle → Attached.
//   • IFixedTickable for buoyancy force application.
//   • Supports stacking multiple floaters on one object.
//
// ZERO GC:
//   • IFixedTickable.FixedTick() — no Update().
//   • Cached Transform, Rigidbody.
//   • CompareTag for player detection.
//
// USAGE:
//   1. Create floater prefab with collider and visual mesh.
//   2. Player interacts to "pick up" floater.
//   3. Player hits target object to attach.
//   4. Multiple floaters stack for more buoyancy.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton.Localization;
using Hecton8.World;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for floater.
    /// </summary>
    public enum FloaterState
    {
        Idle,      // Floating in water, waiting to be picked up
        Held,      // Held by player, waiting to be attached
        Attached   // Attached to an object, providing buoyancy
    }

    /// <summary>
    /// Small organism/device that can be attached to objects to make them float.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Floater : MonoBehaviour, IInteractable, IInteractableTextProvider, IFixedTickable, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const string DefaultPickupText = "Pick Up Floater";
        private const string DefaultAttachText = "Attach to Object";
        private const int AttachQueryCapacity = 16;
        private const int AttachParentSearchDepth = 8;
        private const float AttachMinimumConeRadius = 0.18f;
        private const float AttachConeRadiusPerMeter = 0.12f;
        private static readonly SpatialTargetKind AttachTargetKinds =
            SpatialTargetKind.Pickup |
            SpatialTargetKind.Resource |
            SpatialTargetKind.Scannable |
            SpatialTargetKind.Module;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUOYANCY
        // ══════════════════════════════════════════════════════════

        [Header("── Buoyancy ────────────────────────────────────")]
        [Tooltip("Upward force applied to attached object.")]
        [SerializeField, Range(1f, 100f)] private float buoyancyForce = 20f;

        [Tooltip("Should force be applied in FixedUpdate?")]
        [SerializeField] private bool applyInFixedUpdate = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — ATTACHMENT
        // ══════════════════════════════════════════════════════════

        [Header("── Attachment ───────────────────────────────────")]
        [Tooltip("Layers that the floater can attach to.")]
        [SerializeField] private LayerMask attachableLayers;

        [Tooltip("Maximum distance for attachment.")]
        [SerializeField, Range(0.5f, 10f)] private float attachDistance = 3f;

        [Tooltip("Offset from attachment point.")]
        [SerializeField] private Vector3 attachOffset = Vector3.zero;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ──────────────────────────────────────")]
        [Tooltip("Renderer to disable when attached.")]
        [SerializeField] private Renderer visualRenderer;

        [Tooltip("Particle system for attach effect.")]
        [SerializeField] private ParticleSystem attachParticles;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when floater is picked up.")]
        [SerializeField] private AudioClip pickupSound;

        [Tooltip("Sound played when floater is attached.")]
        [SerializeField] private AudioClip attachSound;

        [Tooltip("Volume for floater sounds.")]
        [SerializeField, Range(0f, 1f)] private float floaterVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ──────────────────────────────────")]
        [Tooltip("Interaction text when floater is idle.")]
        [SerializeField] private string pickupText = DefaultPickupText;

        [Tooltip("Interaction text when floater is held.")]
        [SerializeField] private string attachText = DefaultAttachText;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when floater is picked up.")]
        [SerializeField] private UnityEvent OnPickedUp;

        [Tooltip("Invoked when floater is attached to an object.")]
        [SerializeField] private UnityEvent<Rigidbody> OnAttached;

        [Tooltip("Invoked when floater is detached.")]
        [SerializeField] private UnityEvent OnDetached;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private Rigidbody _ownRigidbody;
        private FloaterState _state = FloaterState.Idle;
        private Rigidbody _attachedBody;
        private Transform _attachedTransform;
        private Vector3 _localAttachPosition;
        private bool _isRegistered;
        private bool _lateFrameRegistered;
        private Vector3 _pendingRuntimePosition;
        private bool _runtimePositionDirty;
        private bool _pendingVisualEnabled;
        private bool _visualEnabledDirty;
        private bool _pendingPickupAudio;
        private bool _pendingAttachAudio;
        private bool _pendingAttachParticles;
        private Vector3 _pendingPickupAudioPosition;
        private Vector3 _pendingAttachPosition;
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private IPhysicsService _physicsService;
        // COLD ALLOC: SpatialQueryHit[16] - registered owner attach probe buffer - owner: Floater
        private readonly SpatialQueryHit[] _attachQueryHits = new SpatialQueryHit[AttachQueryCapacity];

        // Pre-cached player tag
        private const string PlayerTag = "Player";

        // Pre-cached interaction text
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedPickupTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedAttachTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedPickupTextLength;
        private int _cachedAttachTextLength;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the floater.</summary>
        public FloaterState State => _state;

        /// <summary>Is the floater attached to an object?</summary>
        public bool IsAttached => _state == FloaterState.Attached;

        /// <summary>Buoyancy force provided by this floater.</summary>
        public float BuoyancyForce => buoyancyForce;

        /// <summary>The Rigidbody this floater is attached to.</summary>
        public Rigidbody AttachedBody => _attachedBody;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            TryGetComponent(out _collider);
            TryGetComponent(out _ownRigidbody);

            // Auto-find renderer if not assigned
            if (visualRenderer == null)
            {
                visualRenderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            }

            // Set default layer mask if not assigned
            if (attachableLayers == 0)
            {
                attachableLayers = HectonLayerMasks.DefaultLayerMask;
            }

            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
            _state = FloaterState.Idle;
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            UnregisterFromFixedTick();
            UnregisterFromLateFrame();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterFromFixedTick();
            UnregisterFromLateFrame();
            TryUnregisterHotSwapListener();
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        void IInteractable.OnHoverStart()
        {
            // Could trigger highlight effect here
        }

        void IInteractable.OnHoverEnd()
        {
            // Could disable highlight effect here
        }

        void IInteractable.Interact(Transform interactor)
        {
            switch (_state)
            {
                case FloaterState.Idle:
                    Pickup(interactor);
                    break;

                case FloaterState.Held:
                    // Try to attach to object player is looking at
                    TryAttach(interactor);
                    break;
            }
        }

        string IInteractable.GetInteractText()
        {
            return _state switch
            {
                FloaterState.Idle => ResolveLegacyConfigured(pickupText, DefaultPickupText),
                FloaterState.Held => ResolveLegacyConfigured(attachText, DefaultAttachText),
                _ => null
            };
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            ReadOnlySpan<char> source = _state switch
            {
                FloaterState.Idle => _cachedPickupTextBuffer.AsSpan(0, _cachedPickupTextLength),
                FloaterState.Held => _cachedAttachTextBuffer.AsSpan(0, _cachedAttachTextLength),
                _ => ReadOnlySpan<char>.Empty
            };
            return InteractableTextCopy.TryCopy(source, destination, out length);
        }

        private void RebuildLocalizedTextCache()
        {
            _cachedPickupTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(
                pickupText,
                DefaultPickupText,
                LocalizationKeys.INTERACT_PICK_UP_FLOATER,
                _localizationManager,
                _cachedPickupTextBuffer);
            _cachedAttachTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(
                attachText,
                DefaultAttachText,
                LocalizationKeys.INTERACT_ATTACH_TO_OBJECT,
                _localizationManager,
                _cachedAttachTextBuffer);
        }

        private static string ResolveLegacyConfigured(string configuredText, string defaultText)
        {
            return !string.IsNullOrWhiteSpace(configuredText) &&
                   !string.Equals(configuredText, defaultText, StringComparison.Ordinal)
                ? configuredText
                : defaultText;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager in FixedUpdate.
        /// Applies buoyancy force to attached object.
        /// </summary>
        /// <param name="fixedDeltaTime">Time.fixedDeltaTime.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (_state != FloaterState.Attached) return;
            if (_attachedBody == null)
            {
                Detach();
                return;
            }

            if (applyInFixedUpdate)
            {
                float safeMass = Mathf.Max(_attachedBody.mass, 0.0001f);
                Vector3 buoyancyAcceleration = Vector3.up * (buoyancyForce / safeMass);
                _physicsService?.QueueForce(_attachedBody, buoyancyAcceleration, ForceMode.Acceleration);
            }

            // Update position to follow attached object
            if (_attachedTransform != null)
            {
                ApplyRuntimePosition(_attachedTransform.TransformPoint(_localAttachPosition));
            }
        }

        private void ApplyRuntimePosition(Vector3 runtimePosition)
        {
            _pendingRuntimePosition = runtimePosition;
            _runtimePositionDirty = true;
        }

        public void LateFrameTick()
        {
            if (_runtimePositionDirty)
            {
                _runtimePositionDirty = false;
                if (_transform != null)
                    _transform.position = _pendingRuntimePosition;
            }

            if (_visualEnabledDirty)
            {
                _visualEnabledDirty = false;
                if (visualRenderer != null)
                    visualRenderer.enabled = _pendingVisualEnabled;
            }

            IAudioService audio = ResolveAudioService();
            if (_pendingPickupAudio)
            {
                _pendingPickupAudio = false;
                if (pickupSound != null && audio != null)
                    audio.PlayAtPoint(pickupSound, _pendingPickupAudioPosition, floaterVolume);
            }

            if (_pendingAttachParticles)
            {
                _pendingAttachParticles = false;
                if (attachParticles != null)
                {
                    attachParticles.transform.position = _pendingAttachPosition;
                    attachParticles.Play();
                }
            }

            if (_pendingAttachAudio)
            {
                _pendingAttachAudio = false;
                if (attachSound != null && audio != null)
                    audio.PlayAtPoint(attachSound, _pendingAttachPosition, floaterVolume);
            }

            TryUnregisterLateFrameWhenDormant();
        }

        // ══════════════════════════════════════════════════════════
        //  PICKUP / ATTACH
        // ══════════════════════════════════════════════════════════

        private void Pickup(Transform player)
        {
            _state = FloaterState.Held;

            // Disable physics
            if (_ownRigidbody != null)
            {
                _ownRigidbody.isKinematic = true;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            // Parent to player
            _transform.SetParent(player);
            _transform.localPosition = Vector3.forward * 0.5f;

            QueuePickupAudio(_transform.position);

            // Fire event
            OnPickedUp?.Invoke();
        }

        private void TryAttach(Transform player)
        {
            if (player == null)
                return;

            Vector3 origin = player.position;
            Vector3 direction = player.forward;

            if (TryResolveNearestAttachTarget(
                    origin,
                    direction,
                    attachDistance,
                    out Rigidbody targetBody,
                    out Transform targetTransform,
                    out Vector3 hitPoint))
            {
                AttachTo(targetBody, targetTransform, hitPoint);
            }
        }

        private bool TryResolveNearestAttachTarget(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out Rigidbody targetBody,
            out Transform targetTransform,
            out Vector3 attachPoint)
        {
            targetBody = null;
            targetTransform = null;
            attachPoint = default;

            if (!IsFiniteVector(origin) || !IsFiniteVector(direction) || !float.IsFinite(maxDistance) || maxDistance <= 0f)
                return false;

            float directionSqr = direction.sqrMagnitude;
            if (!float.IsFinite(directionSqr) || directionSqr <= 0.000001f)
                return false;

            Vector3 forward = direction / Mathf.Sqrt(directionSqr);
            float maxDistanceSqr = maxDistance * maxDistance;
            float bestScore = float.MaxValue;
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(origin, maxDistance, AttachTargetKinds, _attachQueryHits);

            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit candidate = _attachQueryHits[i];
                if (candidate.Transform == null ||
                    ReferenceEquals(candidate.Transform, _transform) ||
                    ReferenceEquals(candidate.Owner, this) ||
                    !MatchesLayer(candidate.Layer, attachableLayers.value))
                {
                    continue;
                }

                if (!TryResolveAttachBody(in candidate, out Rigidbody candidateBody, out Transform candidateTransform))
                    continue;

                Vector3 candidatePosition = candidate.Position;
                if (!IsFiniteVector(candidatePosition))
                    continue;

                Vector3 delta = candidatePosition - origin;
                float distanceSqr = delta.sqrMagnitude;
                if (!float.IsFinite(distanceSqr) || distanceSqr <= 0.000001f || distanceSqr > maxDistanceSqr)
                    continue;

                float axial = Vector3.Dot(delta, forward);
                if (!float.IsFinite(axial) || axial <= 0f || axial > maxDistance)
                    continue;

                float lateralSqr = Mathf.Max(0f, distanceSqr - axial * axial);
                float coneRadius = Mathf.Max(AttachMinimumConeRadius, axial * AttachConeRadiusPerMeter);
                if (lateralSqr > coneRadius * coneRadius)
                    continue;

                float score = axial + lateralSqr;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                targetBody = candidateBody;
                targetTransform = candidateTransform;
                attachPoint = origin + forward * axial;
            }

            return targetBody != null && targetTransform != null;
        }

        private bool TryResolveAttachBody(
            in SpatialQueryHit hit,
            out Rigidbody body,
            out Transform targetTransform)
        {
            body = hit.Rigidbody;
            targetTransform = hit.Transform != null ? hit.Transform : body != null ? body.transform : null;
            if (body != null && targetTransform != null)
                return true;

            if (TryResolveBodyFromTransform(hit.Transform, out body, out targetTransform))
                return true;

            Component owner = hit.Owner;
            if (owner != null && owner.TryGetComponent(out body))
            {
                targetTransform = hit.Transform != null ? hit.Transform : owner.transform;
                return targetTransform != null;
            }

            return TryResolveBodyFromTransform(owner != null ? owner.transform : null, out body, out targetTransform);
        }

        private static bool TryResolveBodyFromTransform(
            Transform source,
            out Rigidbody body,
            out Transform targetTransform)
        {
            body = null;
            targetTransform = null;
            Transform current = source;
            for (int depth = 0; depth < AttachParentSearchDepth && current != null; depth++)
            {
                if (current.TryGetComponent(out body))
                {
                    targetTransform = source != null ? source : current;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool MatchesLayer(int layer, int mask)
        {
            return layer >= 0 &&
                   layer < 32 &&
                   (mask & (1 << layer)) != 0;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        /// <summary>
        /// Attaches the floater to a target object.
        /// </summary>
        /// <param name="target">Collider of the target object.</param>
        /// <param name="hitPoint">World position where the floater attaches.</param>
        public void AttachTo(Collider target, Vector3 hitPoint)
        {
            if (target == null)
                return;

            AttachTo(target.attachedRigidbody, target.transform, hitPoint);
        }

        private void AttachTo(Rigidbody targetBody, Transform targetTransform, Vector3 hitPoint)
        {
            if (_state == FloaterState.Attached) return;

            _attachedBody = targetBody;
            _attachedTransform = targetTransform != null ? targetTransform : targetBody != null ? targetBody.transform : null;

            if (_attachedBody == null || _attachedTransform == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[Floater] Cannot attach to object without Rigidbody.", this);
#endif
                return;
            }

            _state = FloaterState.Attached;

            // Calculate local position on target
            _localAttachPosition = _attachedTransform.InverseTransformPoint(hitPoint + attachOffset);

            // Unparent from player
            _transform.SetParent(null);

            // Set position
            _transform.position = hitPoint + attachOffset;

            // Disable own physics
            if (_ownRigidbody != null)
            {
                _ownRigidbody.isKinematic = true;
            }

            QueueAttachPresentation(hitPoint);

            // Register for fixed tick
            RegisterToFixedTick();

            // Fire event
            OnAttached?.Invoke(_attachedBody);
        }

        /// <summary>
        /// Detaches the floater from its current target.
        /// </summary>
        public void Detach()
        {
            if (_state != FloaterState.Attached) return;

            _state = FloaterState.Idle;
            _attachedBody = null;
            _attachedTransform = null;

            // Re-enable physics
            if (_ownRigidbody != null)
            {
                _ownRigidbody.isKinematic = false;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            // Re-enable visual
            if (visualRenderer != null)
            {
                _pendingVisualEnabled = true;
                _visualEnabledDirty = true;
                RegisterToLateFrame();
            }

            // Unregister from fixed tick
            UnregisterFromFixedTick();

            // Fire event
            OnDetached?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the buoyancy force (for runtime configuration).
        /// </summary>
        /// <param name="force">Buoyancy force amount.</param>
        public void SetBuoyancyForce(float force)
        {
            buoyancyForce = Mathf.Max(0f, force);
        }

        // ══════════════════════════════════════════════════════════
        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _localizationManager = GlobalRegistry.LocalizationText;
            _physicsService = GlobalRegistry.Physics;
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

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterFromFixedTick();
                    UnregisterFromLateFrame();
                    if (currentService != null)
                    {
                        if (_state == FloaterState.Attached)
                        {
                            RegisterToFixedTick();
                            RegisterToLateFrame();
                        }
                        else if (HasPendingLateFrameWork())
                        {
                            RegisterToLateFrame();
                        }
                    }
                    break;
            }
        }

        //  TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToFixedTick()
        {
            if (_isRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            _isRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void RegisterToLateFrame()
        {
            if (_lateFrameRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private bool HasPendingLateFrameWork()
        {
            return _runtimePositionDirty ||
                   _visualEnabledDirty ||
                   _pendingPickupAudio ||
                   _pendingAttachAudio ||
                   _pendingAttachParticles;
        }

        private void TryUnregisterLateFrameWhenDormant()
        {
            if (_state == FloaterState.Attached || HasPendingLateFrameWork())
                return;

            UnregisterFromLateFrame();
        }

        private void QueuePickupAudio(Vector3 position)
        {
            _pendingPickupAudioPosition = position;
            _pendingPickupAudio = pickupSound != null;
            if (_pendingPickupAudio)
                RegisterToLateFrame();
        }

        private void QueueAttachPresentation(Vector3 position)
        {
            _pendingAttachPosition = position;
            _pendingAttachParticles = attachParticles != null;
            _pendingAttachAudio = attachSound != null;
            RegisterToLateFrame();
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

            RebuildLocalizedTextCache();
        }
#endif

        private void UnregisterFromFixedTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private void UnregisterFromLateFrame()
        {
            if (!_lateFrameRegistered) return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw state indicator
            Gizmos.color = _state switch
            {
                FloaterState.Idle => new Color(0.3f, 0.8f, 1f, 0.3f),
                FloaterState.Held => new Color(1f, 0.8f, 0.3f, 0.3f),
                FloaterState.Attached => new Color(0.3f, 1f, 0.5f, 0.3f),
                _ => Color.gray
            };
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            // Draw attach distance when held
            if (_state == FloaterState.Held)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.2f);
                Gizmos.DrawRay(transform.position, Vector3.forward * attachDistance);
            }
        }
#endif
    }
}

