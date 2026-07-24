// ============================================================================
// HECTON-8 - DeployableBeacon.cs
// Deployable tracking buoy for marking locations.
//
// ARCHITECTURE:
//   - IInteractable for player interaction
//   - IFixedTickable for buoyancy physics (no Update)
//   - MaterialPropertyBlock for beacon light (zero GC)
//   - UnityEvent for HUD integration
//
// FEATURES:
//   - Floats to surface or hovers at fixed depth
//   - Customizable label and color
//   - Rename functionality via UnityEvent
//   - HUD-readable properties
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Deployable tracking beacon for marking locations.
    /// Implements IInteractable for player interaction.
    /// Uses IFixedTickable for buoyancy physics.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Gameplay/Deployable Beacon")]
    public sealed class DeployableBeacon : MonoBehaviour, IInteractable, IInteractableTextProvider, ITickable, IUpdatable, IFixedTickable, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const ulong BeaconIdFnvOffset = 1469598103934665603UL;
        private const ulong BeaconIdFnvPrime = 1099511628211UL;
        private const int BeaconIdHexLength = 8;

        // ==========================================================
        //  INSPECTOR
        // ==========================================================

        [Header("Beacon Settings")]
        [Tooltip("Display label for this beacon.")]
        [SerializeField] private string beaconLabel = "Beacon";

        [Tooltip("Optional localization table key used for authored beacon labels. Runtime rename clears this override.")]
        [SerializeField] private string localizedLabelTableKey;

        [Tooltip("Color of the beacon light.")]
        [SerializeField] private Color beaconColor = new Color(0f, 0.8f, 1f);

        [Tooltip("Unique ID for this beacon.")]
        [SerializeField] private string beaconId;

        [Header("Buoyancy")]
        [Tooltip("Target depth (-Y). 0 = surface, negative = underwater.")]
        [SerializeField, Range(-500f, 0f)] private float targetDepth = 0f;

        [Tooltip("Buoyancy force strength.")]
        [SerializeField, Range(0.1f, 50f)] private float buoyancyForce = 10f;

        [Tooltip("Damping to prevent oscillation.")]
        [SerializeField, Range(0.1f, 10f)] private float damping = 2f;

        [Tooltip("Lock position when at target depth.")]
        [SerializeField] private bool lockAtTarget = false;

        [Header("Status Light")]
        [Tooltip("Renderer for the beacon light.")]
        [SerializeField] private Renderer beaconLight;

        [Tooltip("Material property for light color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Blink interval when active (0 = no blink).")]
        [SerializeField, Range(0f, 2f)] private float blinkInterval = 1f;

        [Tooltip("Light intensity.")]
        [SerializeField, Range(0.5f, 5f)] private float lightIntensity = 2f;

        [Header("Audio")]
        [Tooltip("Sound played when beacon is deployed.")]
        [SerializeField] private AudioClip deploySound;

        [Tooltip("Sound played when beacon is interacted with.")]
        [SerializeField] private AudioClip interactSound;

        [Header("Events")]
        [Tooltip("Fired when player requests to rename the beacon.")]
        [SerializeField] private UnityEvent<DeployableBeacon> OnRenameRequested;

        [Tooltip("Fired when beacon label changes.")]
        [SerializeField] private UnityEvent<string> OnLabelChanged;

        [Tooltip("Fired when beacon color changes.")]
        [SerializeField] private UnityEvent<Color> OnColorChanged;

        [Tooltip("Fired when beacon reaches target depth.")]
        [SerializeField] private UnityEvent OnBeaconStabilized;

        // ==========================================================
        //  PRIVATE STATE
        // ==========================================================

        private Rigidbody _rb;
        private float _blinkTimer;
        private bool _blinkOn = true;
        private bool _isStabilized;
        private bool _registered;
        private bool _registeredFixed;
        private bool _registeredLateFrame;
        private bool _registeredBeacon;
        private bool _hotSwapRegistered;
        private bool _beaconLightDirty;
        private bool _pendingDeployAudio;
        private bool _pendingInteractAudio;
        private AbsoluteUniversePosition _cachedAup;
        private int _emissionPropertyId;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localization;
        private IPhysicsService _physicsService;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Pre-cached interaction text
        private const string DefaultInteractText = "Configure Beacon";
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;

        // ==========================================================
        //  PUBLIC PROPERTIES
        // ==========================================================

        /// <summary>Display label for this beacon.</summary>
        public string BeaconLabel
        {
            get => beaconLabel;
            set
            {
                if (beaconLabel != value)
                {
                    beaconLabel = value;
                    OnLabelChanged?.Invoke(DisplayLabel);
                }
            }
        }

        /// <summary>Localized or user-defined display label shown to the player.</summary>
        public string DisplayLabel => ResolveDisplayLabel();

        /// <summary>Color of the beacon light.</summary>
        public Color BeaconColor
        {
            get => beaconColor;
            set
            {
                beaconColor = value;
                QueueBeaconLightUpdate();
                OnColorChanged?.Invoke(beaconColor);
            }
        }

        /// <summary>Unique ID for this beacon.</summary>
        public string BeaconId => beaconId;

        /// <summary>World position of the beacon.</summary>
        public Vector3 Position => _cachedTransform.position;

        /// <summary>Cached absolute universe position for long-range HUD and scanner logic.</summary>
        public AbsoluteUniversePosition PositionAup => _cachedAup;

        /// <summary>True if beacon has stabilized at target depth.</summary>
        public bool IsStabilized => _isStabilized;

        /// <summary>True if beacon is active and registered.</summary>
        public bool IsActive => enabled && gameObject.activeInHierarchy;

        // ==========================================================
        //  LIFECYCLE
        // ==========================================================

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - per-renderer props - owner: DeployableBeacon

            TryGetComponent(out _rb);

            // Generate deterministic ID if not set.
            if (string.IsNullOrEmpty(beaconId))
            {
                beaconId = CreateDeterministicBeaconId();
            }

            if (beaconLight == null)
                beaconLight = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);

            CacheRegistryServicesCold();
            RefreshCachedAup();
        }

        private string CreateDeterministicBeaconId()
        {
            AbsoluteUniversePosition aup = TryResolveAupFromRuntimeOrigin(_cachedTransform.position, out AbsoluteUniversePosition resolvedAup)
                ? resolvedAup
                : default;
            ulong hash = BeaconIdFnvOffset;
            hash = MixBeaconIdHash(hash, unchecked((ulong)aup.GridX));
            hash = MixBeaconIdHash(hash, unchecked((ulong)aup.GridY));
            hash = MixBeaconIdHash(hash, unchecked((ulong)aup.GridZ));
            hash = MixBeaconIdHash(hash, unchecked((uint)(int)math.round(aup.LocalX * 100f)));
            hash = MixBeaconIdHash(hash, unchecked((uint)(int)math.round(aup.LocalY * 100f)));
            hash = MixBeaconIdHash(hash, unchecked((uint)(int)math.round(aup.LocalZ * 100f)));
            hash = MixBeaconIdHash(hash, EntityId.ToULong(gameObject.GetEntityId()));

            return string.Create(BeaconIdHexLength, hash, (buffer, value) =>
            {
                for (int i = 0; i < BeaconIdHexLength; i++)
                {
                    int nibble = (int)((value >> ((BeaconIdHexLength - 1 - i) * 4)) & 0xFUL);
                    buffer[i] = (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));
                }
            });
        }

        private static ulong MixBeaconIdHash(ulong hash, ulong value)
        {
            hash ^= value;
            return hash * BeaconIdFnvPrime;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegisterTickSystems();

            TryRegisterBeacon();

            RefreshCachedAup();
            RebuildLocalizedTextCache();
            QueueBeaconLightUpdate();

            _pendingDeployAudio = deploySound != null;
            TryRegisterLateFrameIfNeeded();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterBeacon();
            TryUnregisterTickSystems();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            TryUnregisterBeacon();
            TryUnregisterTickSystems();
        }

        // ==========================================================
        //  ITickable - BLINKING LOGIC
        // ==========================================================

        /// <summary>
        /// ITickable implementation. Handles beacon light blinking.
        /// Zero GC: no allocations.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (blinkInterval <= 0f)
            {
                TryUnregisterUpdatableWhenNoBlink();
                return;
            }

            // Handle blinking
            _blinkTimer += deltaTime;
            if (_blinkTimer >= blinkInterval)
            {
                _blinkTimer = 0f;
                _blinkOn = !_blinkOn;
                QueueBeaconLightUpdate();
            }
        }

        public void LateFrameTick()
        {
            if (_beaconLightDirty)
            {
                _beaconLightDirty = false;
                UpdateBeaconLight();
            }

            IAudioService audioService = ResolveAudioService();
            if (_pendingDeployAudio)
            {
                _pendingDeployAudio = false;
                if (deploySound != null && audioService != null)
                    audioService.PlayAtPoint(deploySound, _cachedTransform.position);
            }

            if (_pendingInteractAudio)
            {
                _pendingInteractAudio = false;
                if (interactSound != null && audioService != null)
                    audioService.PlayStatic2D(interactSound, 0.7f);
            }

            TryUnregisterLateFrameWhenDormant();
        }

        // ==========================================================
        //  IFixedTickable - BUOYANCY PHYSICS
        // ==========================================================

        /// <summary>
        /// IFixedTickable implementation. Handles buoyancy physics.
        /// Zero GC: no allocations.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (_rb == null)
                return;

            RefreshCachedAup();

            // Calculate current depth
            float currentDepth = -ResolveRuntimePosition().y;

            // Calculate depth error
            float depthError = targetDepth - currentDepth;

            // Check if stabilized
            bool wasStabilized = _isStabilized;
            float linearSpeedSq = _rb.linearVelocity.sqrMagnitude;
            _isStabilized = math.abs(depthError) < 0.5f && linearSpeedSq < 0.01f;

            if (_isStabilized && !wasStabilized)
            {
                OnBeaconStabilized?.Invoke();

                if (lockAtTarget)
                {
                    _rb.isKinematic = true;
                }
            }

            // Apply buoyancy force
            if (!_rb.isKinematic)
            {
                // Upward force proportional to depth error
                float forceMagnitude = depthError * buoyancyForce;

                // Apply force
                Vector3 force = Vector3.up * forceMagnitude;
                IPhysicsService physicsService = _physicsService;
                if (physicsService == null)
                    return;

                physicsService.QueueForce(_rb, force, ForceMode.Force);

                // Apply damping
                Vector3 dampingForce = -_rb.linearVelocity * damping;
                physicsService.QueueForce(_rb, dampingForce, ForceMode.Force);
            }
        }

        // ==========================================================
        //  IInteractable
        // ==========================================================

        /// <summary>
        /// Called when player's raycast first hits this object.
        /// </summary>
        public void OnHoverStart()
        {
            // Future: highlight effect
        }

        /// <summary>
        /// Called when player's raycast leaves this object.
        /// </summary>
        public void OnHoverEnd()
        {
            // Future: remove highlight
        }

        /// <summary>
        /// Called when player presses interact key while hovering.
        /// </summary>
        public void Interact(Transform interactor)
        {
            _pendingInteractAudio = interactSound != null;
            TryRegisterLateFrameIfNeeded();

            // Fire rename requested event
            OnRenameRequested?.Invoke(this);
        }

        /// <summary>
        /// Returns the UI prompt string. Zero GC: returns cached string.
        /// </summary>
        public string GetInteractText()
        {
            return DefaultInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength), destination, out length);
        }

        // ==========================================================
        //  PUBLIC API
        // ==========================================================

        /// <summary>
        /// Sets the beacon label.
        /// </summary>
        /// <param name="newLabel">New label text.</param>
        public void SetLabel(string newLabel)
        {
            localizedLabelTableKey = string.Empty;
            BeaconLabel = newLabel;
        }

        private void RefreshCachedAup()
        {
            if (TryResolveAupFromRuntimeOrigin(ResolveRuntimePosition(), out AbsoluteUniversePosition beaconAup))
                _cachedAup = beaconAup;
        }

        private Vector3 ResolveRuntimePosition()
        {
            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void TryRegisterBeacon()
        {
            if (_registeredBeacon)
                return;

            BeaconRegistry.Register(this);
            _registeredBeacon = true;
        }

        private void TryUnregisterBeacon()
        {
            if (!_registeredBeacon)
                return;

            BeaconRegistry.Unregister(this);
            _registeredBeacon = false;
        }

        /// <summary>
        /// Sets the localization table key for authored beacon labels.
        /// </summary>
        /// <param name="tableKey">Localization table key to resolve at runtime.</param>
        public void SetLocalizedLabelKey(string tableKey)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(tableKey) ? string.Empty : tableKey.Trim();
            if (string.Equals(localizedLabelTableKey, normalizedKey, System.StringComparison.Ordinal))
                return;

            localizedLabelTableKey = normalizedKey;
            OnLabelChanged?.Invoke(DisplayLabel);
        }

        /// <summary>
        /// Sets the beacon color.
        /// </summary>
        /// <param name="newColor">New beacon color.</param>
        public void SetColor(Color newColor)
        {
            BeaconColor = newColor;
        }

        /// <summary>
        /// Sets the target depth for the beacon.
        /// </summary>
        /// <param name="depth">Target depth (negative = underwater).</param>
        public void SetTargetDepth(float depth)
        {
            targetDepth = math.clamp(depth, -500f, 0f);

            // Re-enable physics if locked
            if (_rb != null && _rb.isKinematic && lockAtTarget)
            {
                _rb.isKinematic = false;
                _isStabilized = false;
            }
        }

        // ==========================================================
        //  VISUALS
        // ==========================================================

        /// <summary>
        /// Updates the beacon light using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateBeaconLight()
        {
            if (beaconLight == null)
                return;

            Color lightColor = _blinkOn || blinkInterval <= 0f
                ? beaconColor * lightIntensity
                : beaconColor * 0.2f;

            beaconLight.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, lightColor);
            beaconLight.SetPropertyBlock(_mpb);
        }

        private void QueueBeaconLightUpdate()
        {
            _beaconLightDirty = true;
            TryRegisterLateFrameIfNeeded();
        }

        // ==========================================================
        //  EDITOR
        // ==========================================================

        private void TryRegisterTickSystems()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered && blinkInterval > 0f)
            {
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredFixed)
            {
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            }

        }

        private void TryRegisterLateFrameIfNeeded()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!HasPendingLateFrameWork())
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private bool HasPendingLateFrameWork()
        {
            return _beaconLightDirty || _pendingDeployAudio || _pendingInteractAudio;
        }

        private void TryUnregisterUpdatableWhenNoBlink()
        {
            if (!_registered || blinkInterval > 0f)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryUnregisterLateFrameWhenDormant()
        {
            if (!_registeredLateFrame || HasPendingLateFrameWork())
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryUnregisterTickSystems()
        {
            if (_registered)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);

            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registered = false;
            _registeredFixed = false;
            _registeredLateFrame = false;
            _beaconLightDirty = false;
            _pendingDeployAudio = false;
            _pendingInteractAudio = false;
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
                    _localization = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    if (!string.IsNullOrWhiteSpace(localizedLabelTableKey))
                        OnLabelChanged?.Invoke(DisplayLabel);
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterTickSystems();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterTickSystems();
                        TryRegisterLateFrameIfNeeded();
                    }
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _localization = GlobalRegistry.LocalizationText;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (buoyancyForce < 0.1f) buoyancyForce = 0.1f;
            if (damping < 0.1f) damping = 0.1f;
            if (lightIntensity < 0.5f) lightIntensity = 0.5f;
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw target depth line
            Vector3 targetPos = new Vector3(transform.position.x, -targetDepth, transform.position.z);
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.3f);

            // Draw beacon info
            if (Application.isPlaying)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 1.5f,
                    $"[{beaconLabel}]\nDepth: {-transform.position.y:F1}m\nTarget: {-targetDepth:F1}m"
                );
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedInteractTextLength = InteractableTextCopy.CopyLocalizedTruncated(
                _localization,
                LocalizationKeys.INTERACT_CONFIGURE_BEACON,
                DefaultInteractText,
                _cachedInteractTextBuffer);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
            if (!string.IsNullOrWhiteSpace(localizedLabelTableKey))
                OnLabelChanged?.Invoke(DisplayLabel);
        }

        private string ResolveDisplayLabel()
        {
            return string.IsNullOrWhiteSpace(beaconLabel) ? "Beacon" : beaconLabel;
        }
    }
}

