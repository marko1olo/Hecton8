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
using Hecton8.Physics;
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
    public sealed class Floater : MonoBehaviour, IInteractable, IFixedTickable
    {
        private const string DefaultPickupText = "Pick Up Floater";
        private const string DefaultAttachText = "Attach to Object";

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

        // Pre-cached player tag
        private const string PlayerTag = "Player";

        // Pre-cached interaction text
        private string _cachedPickupText;
        private string _cachedAttachText;

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
            _collider = GetComponent<Collider>();
            _ownRigidbody = GetComponent<Rigidbody>();

            // Auto-find renderer if not assigned
            if (visualRenderer == null)
            {
                visualRenderer = GetComponentInChildren<Renderer>();
            }

            // Set default layer mask if not assigned
            if (attachableLayers == 0)
            {
                attachableLayers = LayerMask.GetMask("Default", "PhysicsObject");
            }

            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RebuildLocalizedTextCache();
            _state = FloaterState.Idle;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            UnregisterFromFixedTick();
        }

        private void OnDestroy()
        {
            UnregisterFromFixedTick();
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
                FloaterState.Idle => _cachedPickupText,
                FloaterState.Held => _cachedAttachText,
                _ => null
            };
        }

        private void RebuildLocalizedTextCache()
        {
            _cachedPickupText = ResolveConfiguredText(
                pickupText,
                DefaultPickupText,
                LocalizationKeys.INTERACT_PICK_UP_FLOATER);
            _cachedAttachText = ResolveConfiguredText(
                attachText,
                DefaultAttachText,
                LocalizationKeys.INTERACT_ATTACH_TO_OBJECT);
        }

        private string ResolveConfiguredText(string configuredText, string defaultText, string localizationKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredText) &&
                !string.Equals(configuredText, defaultText, System.StringComparison.Ordinal))
            {
                return configuredText;
            }

            return ResolveLocalized(localizationKey, defaultText);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
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
                Vector3 force = Vector3.up * buoyancyForce;
                PhysicsForceRouter.QueueForce(_attachedBody, force, ForceMode.Force);
            }

            // Update position to follow attached object
            if (_attachedTransform != null)
            {
                _transform.position = _attachedTransform.TransformPoint(_localAttachPosition);
            }
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

            // Play pickup sound
            if (pickupSound != null && SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(pickupSound, _transform.position, floaterVolume);
            }

            // Fire event
            OnPickedUp?.Invoke();
        }

        private void TryAttach(Transform player)
        {
            // Raycast from player camera
            Vector3 origin = player.position;
            Vector3 direction = player.forward;

            if (UnityEngine.Physics.Raycast(origin, direction, out RaycastHit hit, attachDistance, attachableLayers))
            {
                AttachTo(hit.collider, hit.point);
            }
        }

        /// <summary>
        /// Attaches the floater to a target object.
        /// </summary>
        /// <param name="target">Collider of the target object.</param>
        /// <param name="hitPoint">World position where the floater attaches.</param>
        public void AttachTo(Collider target, Vector3 hitPoint)
        {
            if (_state == FloaterState.Attached) return;

            // Get target Rigidbody
            _attachedBody = target.attachedRigidbody;
            _attachedTransform = target.transform;

            if (_attachedBody == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Floater] Cannot attach to object without Rigidbody.", this);
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

            // Disable visual (optional)
            // if (visualRenderer != null) visualRenderer.enabled = false;

            // Play attach particles
            if (attachParticles != null)
            {
                attachParticles.transform.position = hitPoint;
                attachParticles.Play();
            }

            // Play attach sound
            if (attachSound != null && SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(attachSound, hitPoint, floaterVolume);
            }

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
                visualRenderer.enabled = true;
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
        //  TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToFixedTick()
        {
            if (_isRegistered) return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Register(this);
                _isRegistered = true;
            }
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

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
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
