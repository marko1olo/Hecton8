using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

namespace Hecton8.UI
{
    /// <summary>
    /// Collider-volume diegetic button receiver for physical hand interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/UI/Physical Panel Button")]
    public sealed class PhysicalPanelButton : MonoBehaviour, ITickable, IUpdatable, IInteractionSignalConsumer, IPhysicalPanelButtonReceiver, IGlobalRegistryHotSwapListener
    {
        private const uint PhysicalPanelToolId = 0x50414E4Cu;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte MicroHapticPriority = 1;
        private const float HoldDispatchIntervalSeconds = 0.033333335f;
        private const int MaxParentResolveDepth = 32;
        private const float MinimumPressDepthMeters = 0.001f;
        private const float MaximumPressDepthMeters = 0.05f;
        private const float MinimumVisualSpeed = 1f;
        private const float MaximumVisualSpeed = 60f;
        private const float MinimumSignalCooldownSeconds = 0.02f;
        private const float MaximumSignalCooldownSeconds = 1f;
        private const float MinimumPressHapticDurationSeconds = 0.01f;
        private const float MaximumPressHapticDurationSeconds = 0.12f;
        private const float MaximumPressHapticLowFrequency = 0.4f;
        private const float MaximumPressHapticHighFrequency = 0.6f;
        private const float MaximumPressHapticFrequencyHz = 80f;
        private const float MaximumClickVolume = 1f;
        private const float MinimumClickPitch = 0.25f;
        private const float MaximumClickPitch = 2.5f;
        private const float MaximumButtonDeltaSeconds = 0.05f;
        private const float VisualWriteEpsilon = 0.000001f;
        private const float VisualSettleEpsilon = 0.0005f;

        [Header("References")]
        [SerializeField, Tooltip("Trigger volume depressed by the player kinematic hand.")]
        private BoxCollider activationVolume;
        [SerializeField, Tooltip("Optional mesh transform moved along its local Z axis when pressed.")]
        private Transform buttonMesh;
        [SerializeField, Tooltip("Panel receiver that consumes the resolved button event.")]
        private MonoBehaviour panelInteractable;

        [Header("Panel Event")]
        [SerializeField, Tooltip("Stable panel id forwarded to the panel receiver.")]
        private int panelId = 1;
        [SerializeField, Tooltip("Canvas-space event position forwarded when this physical button fires.")]
        private Vector2 canvasHitPoint = new Vector2(256f, 128f);
        [SerializeField, Tooltip("Queued interaction effect family used only to route through the interaction signal queue.")]
        private InteractionEffectType signalEffectType = InteractionEffectType.Drill;

        [Header("Physical Motion")]
        [SerializeField, Range(0.001f, 0.05f), Tooltip("Local Z depression distance in meters.")]
        private float pressDepthMeters = 0.012f;
        [SerializeField, Range(1f, 60f), Tooltip("Visual depression lerp speed.")]
        private float depressSpeed = 22f;
        [SerializeField, Range(1f, 60f), Tooltip("Visual release lerp speed.")]
        private float releaseSpeed = 14f;
        [SerializeField, Range(0.02f, 1f), Tooltip("Minimum seconds between queued button signals.")]
        private float signalCooldownSeconds = 0.18f;

        [Header("Haptics")]
        [SerializeField, Tooltip("Routes physical finger presses into the hand-specific haptic command queue.")]
        private bool emitPressHaptics = true;
        [SerializeField, Tooltip("Optional layers treated as left-hand finger sources before falling back to the controller hand side.")]
        private LayerMask leftHandSourceLayers;
        [SerializeField, Tooltip("Optional layers treated as right-hand finger sources before falling back to the controller hand side.")]
        private LayerMask rightHandSourceLayers;
        [SerializeField, Range(0f, 0.4f), Tooltip("Low-frequency micro pulse amplitude for a button press.")]
        private float pressHapticLowFrequency = 0.06f;
        [SerializeField, Range(0f, 0.6f), Tooltip("High-frequency micro pulse amplitude for a button press.")]
        private float pressHapticHighFrequency = 0.18f;
        [SerializeField, Range(0.01f, 0.12f), Tooltip("Micro pulse duration in seconds.")]
        private float pressHapticDurationSeconds = 0.035f;
        [SerializeField, Range(0f, 80f), Tooltip("Optional sinusoidal carrier for tactile switch texture.")]
        private float pressHapticFrequencyHz = 54f;

        [Header("Diegetic Audio")]
        [SerializeField, Tooltip("One-based authored NativeQueue audio event id for mechanical clicks. Zero falls back to the optional AudioClip path.")]
        private uint pressAudioEventId;
        [SerializeField, Tooltip("Optional short mechanical click routed through the world-space audio pool.")]
        private AudioClip pressClickSound;
        [SerializeField, Range(0f, 1f), Tooltip("Linear volume for the physical panel click.")]
        private float clickVolume = 0.42f;
        [SerializeField, Range(0.25f, 2.5f), Tooltip("Pitch applied to the physical panel click.")]
        private float clickPitch = 1f;
        [SerializeField, Tooltip("Optional source transform for the diegetic click. Defaults to this button transform.")]
        private Transform clickAudioOrigin;

        private IPanelInteractable _panelInteractable;
        private Transform _cachedTransform;
        private Vector3 _baseLocalPosition;
        private int _clickOcclusionMask;
        private int _lastHandInsideFrame = -1;
        private float _buttonClock;
        private float _signalCooldownRemaining;
        private float _holdEventRemaining;
        private float _pressed01;
        private float _resolvedPressDepthMeters = 0.012f;
        private float _resolvedDepressSpeed = 22f;
        private float _resolvedReleaseSpeed = 14f;
        private float _resolvedSignalCooldownSeconds = 0.18f;
        private float _resolvedPressHapticLowFrequency = 0.06f;
        private float _resolvedPressHapticHighFrequency = 0.18f;
        private float _resolvedPressHapticDurationSeconds = 0.035f;
        private float _resolvedPressHapticFrequencyHz = 54f;
        private float _resolvedClickVolume = 0.42f;
        private float _resolvedClickPitch = 1f;
        private IAudioService _cachedAudioService;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _registered;
        private bool _receiverRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _pressDispatched;
        private bool _acousticRuntimeAcquired;
        private Collider _registeredActivationVolume;

        /// <summary>Collider volume used by the physical hand overlap probe.</summary>
        public Collider ActivationCollider => activationVolume;

        /// <summary>
        /// Resolves a registered physical panel button receiver without a component lookup.
        /// </summary>
        /// <param name="collider">Collider returned by the hand overlap probe.</param>
        /// <param name="receiver">Resolved button receiver.</param>
        /// <returns>True when the collider maps to an enabled physical panel button.</returns>
        public static bool TryResolve(Collider collider, out IPhysicalPanelButtonReceiver receiver)
        {
            return PhysicalHandReceiverRegistry.TryResolve(collider, out receiver);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            _clickOcclusionMask = AcousticOcclusionUtility.BuildSensoryMask();
            CacheRegistryServicesCold();
            CacheScalarConfig();
            ResolveReferences();
            if (buttonMesh != null)
                _baseLocalPosition = buttonMesh.localPosition;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            CacheScalarConfig();
            ResolveReferences();
            RegisterCollider();
            TryRegisterHotSwapListener();
            AcquireAcousticRuntime();
            RefreshTickRegistration(false);
        }

        private void OnDisable()
        {
            if (_pressDispatched)
                DispatchPanelEvent(DiegeticPanelInputEventType.Up);

            Unregister();
            TryUnregisterHotSwapListener();
            ReleaseAcousticRuntime();
            UnregisterCollider();
            _lastHandInsideFrame = -1;
            _pressDispatched = false;
            _buttonClock = 0f;
            _signalCooldownRemaining = 0f;
            _holdEventRemaining = 0f;
            _pressed01 = 0f;
            if (buttonMesh != null)
                buttonMesh.localPosition = _baseLocalPosition;
        }

        private void OnDestroy()
        {
            Unregister();
            TryUnregisterHotSwapListener();
            ReleaseAcousticRuntime();
            UnregisterCollider();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            float safeDeltaTime = SanitizeButtonDeltaSeconds(dt);
            _buttonClock += safeDeltaTime;
            if (_signalCooldownRemaining > 0f)
                _signalCooldownRemaining = math.max(0f, _signalCooldownRemaining - safeDeltaTime);

            bool handInside = _lastHandInsideFrame == Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            float target = handInside ? 1f : 0f;
            float speed = handInside ? _resolvedDepressSpeed : _resolvedReleaseSpeed;
            float alpha = FastDecayBlend(speed, safeDeltaTime);
            float previousPressed = _pressed01;
            bool pressedWasInvalid = !math.isfinite(previousPressed);
            float currentPressed = pressedWasInvalid ? 0f : previousPressed;
            _pressed01 = math.lerp(currentPressed, target, alpha);

            if (buttonMesh != null && (pressedWasInvalid || math.abs(_pressed01 - currentPressed) > VisualWriteEpsilon))
            {
                Vector3 offset = new Vector3(0f, 0f, -_resolvedPressDepthMeters * _pressed01);
                buttonMesh.localPosition = _baseLocalPosition + offset;
            }

            if (!handInside && _pressDispatched)
            {
                DispatchPanelEvent(DiegeticPanelInputEventType.Up);
                _pressDispatched = false;
            }
            else if (handInside && _pressDispatched && safeDeltaTime > 0f)
            {
                _holdEventRemaining -= safeDeltaTime;
                if (_holdEventRemaining <= 0f)
                {
                    DispatchPanelEvent(DiegeticPanelInputEventType.Hold);
                    _holdEventRemaining = HoldDispatchIntervalSeconds;
                }
            }

            RefreshTickRegistration(handInside);
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float safeSpeed = math.isfinite(speed) ? math.max(0f, speed) : 0f;
            float safeDeltaTime = SanitizeButtonDeltaSeconds(deltaTime);
            if (safeSpeed <= 0f || safeDeltaTime <= 0f)
                return 0f;

            float x = safeSpeed * safeDeltaTime;
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        /// <summary>
        /// Attempts to queue a physical button signal from the player's hand probe.
        /// </summary>
        /// <param name="handPosition">Runtime-space hand position.</param>
        /// <param name="handForward">Runtime-space hand forward vector.</param>
        /// <param name="interactionSignals">Authoritative interaction signal queue.</param>
        /// <param name="handSourceCollider">Collider that produced the hand contact sample.</param>
        /// <param name="fallbackHandSide">Hand side supplied by the physical hand bridge.</param>
        /// <param name="sampleFrame">Frame stamp captured once by the physical hand probe.</param>
        /// <returns>True when the hand press was accepted by this button.</returns>
        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            if (activationVolume == null || interactionSignals == null || !interactionSignals.IsInitialized)
                return false;

            if (!IsFinite(handPosition))
                return false;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            int frame = sampleFrame >= 0 ? sampleFrame : currentFrame;
            if (frame > currentFrame || frame < _lastHandInsideFrame)
                return false;

            if (_pressDispatched)
            {
                _lastHandInsideFrame = frame;
                TryRegister();
                return true;
            }

            if (_signalCooldownRemaining > 0f)
            {
                _lastHandInsideFrame = frame;
                TryRegister();
                return true;
            }

            if (!TryResolveRuntimeAup(handPosition, out double3 absoluteHitPoint))
                return false;

            Vector3 fallbackForward = _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;
            if (!IsFinite(fallbackForward))
                fallbackForward = Vector3.forward;

            Vector3 safeDirection = ResolveApproxPressDirection(handForward, fallbackForward);
            float3 hitPointAup = new float3((float)absoluteHitPoint.x, (float)absoluteHitPoint.y, (float)absoluteHitPoint.z);
            InteractionPacket packet = new InteractionPacket(
                PhysicalPanelToolId,
                hitPointAup,
                (float3)safeDirection,
                1f,
                _resolvedPressDepthMeters,
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)frame));
            InteractionSignal signal = new InteractionSignal(
                packet,
                0,
                hitPointAup,
                (float3)(-safeDirection),
                1f,
                (byte)signalEffectType,
                0,
                absoluteHitPoint,
                InteractionSignal.HitPointAupDoubleValid);

            if (!interactionSignals.Publish(in signal, activationVolume))
                return false;

            _lastHandInsideFrame = frame;
            TryRegister();
            _signalCooldownRemaining = _resolvedSignalCooldownSeconds;
            EmitPressHaptic(handSourceCollider, fallbackHandSide);
            return true;
        }

        bool IPhysicalPanelButtonReceiver.TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame)
        {
            return TryQueueHandPress(handPosition, handForward, interactionSignals, handSourceCollider, fallbackHandSide, sampleFrame);
        }

        private static Vector3 ResolveApproxPressDirection(Vector3 handForward, Vector3 fallbackForward)
        {
            float3 direction = (float3)handForward;
            if (!math.all(math.isfinite(direction)))
                direction = (float3)fallbackForward;

            float lengthSq = math.lengthsq(direction);
            if (lengthSq <= 0.0001f)
            {
                direction = (float3)fallbackForward;
                if (!math.all(math.isfinite(direction)))
                    return Vector3.forward;

                lengthSq = math.lengthsq(direction);
                if (lengthSq <= 0.0001f)
                    return Vector3.forward;
            }

            float3 absDirection = math.abs(direction);
            float maxAxis = math.max(absDirection.x, math.max(absDirection.y, absDirection.z));
            float minAxis = math.min(absDirection.x, math.min(absDirection.y, absDirection.z));
            float midAxis = absDirection.x + absDirection.y + absDirection.z - maxAxis - minAxis;
            float approxLength = math.max(0.0001f, maxAxis + (midAxis * 0.375f) + (minAxis * 0.1875f));
            return (Vector3)(direction * math.rcp(approxLength));
        }

        /// <inheritdoc />
        public void ApplyInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (signal.PowerDelivered <= 0f)
                return;

            if (!_pressDispatched)
            {
                DispatchPanelEvent(DiegeticPanelInputEventType.Down);
                PlayDiegeticClick(runtimeHitPoint);
                _pressDispatched = true;
                _holdEventRemaining = HoldDispatchIntervalSeconds;
                TryRegister();
            }
        }

        private void EmitPressHaptic(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            if (!emitPressHaptics)
                return;

            byte motorMask = ResolveHapticMotorMask(handSourceCollider, fallbackHandSide);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                _resolvedPressHapticLowFrequency,
                _resolvedPressHapticHighFrequency,
                _resolvedPressHapticDurationSeconds,
                _resolvedPressHapticFrequencyHz,
                MicroHapticPriority,
                motorMask);
        }

        private byte ResolveHapticMotorMask(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            if (handSourceCollider != null)
            {
                int sourceLayerBit = 1 << handSourceCollider.gameObject.layer;
                if ((leftHandSourceLayers.value & sourceLayerBit) != 0)
                    return LeftMotorMask;

                if ((rightHandSourceLayers.value & sourceLayerBit) != 0)
                    return RightMotorMask;
            }

            if (fallbackHandSide == PhysicalHandSide.Left)
                return LeftMotorMask;

            if (fallbackHandSide == PhysicalHandSide.Right)
                return RightMotorMask;

            return BothMotorMask;
        }

        private void ResolveReferences()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);

            if (activationVolume != null)
                activationVolume.isTrigger = true;

            if (_panelInteractable == null)
            {
                if (panelInteractable is IPanelInteractable explicitReceiver)
                    _panelInteractable = explicitReceiver;
                else
                    TryResolveParentPanelInteractable(transform, out _panelInteractable);
            }
        }

        private void RegisterCollider()
        {
            Collider registeredVolume = _registeredActivationVolume;
            if (_receiverRegistered || registeredVolume != null)
            {
                if (_receiverRegistered && ReferenceEquals(registeredVolume, activationVolume))
                    return;

                UnregisterCollider();
            }

            if (activationVolume == null || !Application.isPlaying)
                return;

            if (!PhysicalHandReceiverRegistry.TryRegister(activationVolume, this))
                return;

            _registeredActivationVolume = activationVolume;
            _receiverRegistered = true;
        }

        private void UnregisterCollider()
        {
            Collider registeredVolume = _registeredActivationVolume;
            if (!_receiverRegistered && registeredVolume == null)
                return;

            if (registeredVolume != null)
                PhysicalHandReceiverRegistry.Unregister(registeredVolume, this);

            _registeredActivationVolume = null;
            _receiverRegistered = false;
        }

        private void DispatchPanelEvent(DiegeticPanelInputEventType eventType)
        {
            if (_panelInteractable == null)
                return;

            _panelInteractable.ReceiveCanvasInput(new DiegeticPanelInputEvent
            {
                PanelId = panelId,
                CanvasHitPoint = new float2(canvasHitPoint.x, canvasHitPoint.y),
                EventType = eventType,
                Timestamp = _buttonClock
            });
        }

        private void PlayDiegeticClick(Vector3 runtimeHitPoint)
        {
            IAudioService audio = _cachedAudioService;
            if (audio == null)
                return;

            Vector3 sourcePosition = clickAudioOrigin != null
                ? clickAudioOrigin.position
                : runtimeHitPoint;
            if (!IsFinite(sourcePosition))
                sourcePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;

            if (pressAudioEventId != 0u && audio.IsInitialized)
            {
                CoreAudioEvent audioEvent = new CoreAudioEvent(
                    pressAudioEventId,
                    sourcePosition,
                    _resolvedClickVolume,
                    _resolvedClickPitch);
                audio.QueueAudioEvent(in audioEvent);
                return;
            }

            if (pressClickSound == null)
                return;

            Transform listenerTransform = ResolveListenerTransform();
            Vector3 listenerPosition = listenerTransform != null ? listenerTransform.position : sourcePosition;
            float resolvedVolume = _resolvedClickVolume;
            float lowPassCutoff = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (_clickOcclusionMask != 0)
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(
                    sourcePosition,
                    listenerPosition,
                    _clickOcclusionMask,
                    _cachedTransform,
                    listenerTransform);
                if (AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                        sourcePosition,
                        listenerPosition,
                        _clickOcclusionMask,
                        _cachedTransform,
                        listenerTransform,
                        out AcousticOcclusionResult result))
                {
                    resolvedVolume *= math.saturate(result.Transmission01);
                    lowPassCutoff = result.LowPassCutoffHz;
                }
            }

            if (audio is ISpatialAudioLowPassPlayback spatialAudio)
            {
                spatialAudio.PlayAtPointWithLowPass(
                    pressClickSound,
                    sourcePosition,
                    resolvedVolume,
                    _resolvedClickPitch,
                    null,
                    lowPassCutoff);
                return;
            }

            audio.PlayAtPoint(pressClickSound, sourcePosition, resolvedVolume, _resolvedClickPitch);
        }

        private Transform ResolveListenerTransform()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
                return null;

            if (playerContext.PlayerCamera != null)
                return playerContext.PlayerCamera.transform;

            return playerContext.PlayerTransform;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _cachedAudioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedAudioService = GlobalRegistry.Audio;
            _cachedPlayerContext = GlobalRegistry.Player;
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

        private void AcquireAcousticRuntime()
        {
            if (_acousticRuntimeAcquired)
                return;

            AcousticOcclusionUtility.AcquireRuntime();
            _acousticRuntimeAcquired = true;
        }

        private void ReleaseAcousticRuntime()
        {
            if (!_acousticRuntimeAcquired)
                return;

            AcousticOcclusionUtility.ReleaseRuntime();
            _acousticRuntimeAcquired = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite((float3)value));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(positionAup));
        }

        private static float SanitizeButtonDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaximumButtonDeltaSeconds) : 0f;
        }

        private void CacheScalarConfig()
        {
            _resolvedPressDepthMeters = ClampFiniteRange(
                pressDepthMeters,
                MinimumPressDepthMeters,
                MaximumPressDepthMeters,
                0.012f);
            _resolvedDepressSpeed = ClampFiniteRange(
                depressSpeed,
                MinimumVisualSpeed,
                MaximumVisualSpeed,
                22f);
            _resolvedReleaseSpeed = ClampFiniteRange(
                releaseSpeed,
                MinimumVisualSpeed,
                MaximumVisualSpeed,
                14f);
            _resolvedSignalCooldownSeconds = ClampFiniteRange(
                signalCooldownSeconds,
                MinimumSignalCooldownSeconds,
                MaximumSignalCooldownSeconds,
                0.18f);
            _resolvedPressHapticLowFrequency = ClampFiniteRange(
                pressHapticLowFrequency,
                0f,
                MaximumPressHapticLowFrequency,
                0.06f);
            _resolvedPressHapticHighFrequency = ClampFiniteRange(
                pressHapticHighFrequency,
                0f,
                MaximumPressHapticHighFrequency,
                0.18f);
            _resolvedPressHapticDurationSeconds = ClampFiniteRange(
                pressHapticDurationSeconds,
                MinimumPressHapticDurationSeconds,
                MaximumPressHapticDurationSeconds,
                0.035f);
            _resolvedPressHapticFrequencyHz = ClampFiniteRange(
                pressHapticFrequencyHz,
                0f,
                MaximumPressHapticFrequencyHz,
                54f);
            _resolvedClickVolume = ClampFiniteRange(clickVolume, 0f, MaximumClickVolume, 0.42f);
            _resolvedClickPitch = ClampFiniteRange(clickPitch, MinimumClickPitch, MaximumClickPitch, 1f);
        }

        private static float ClampFiniteRange(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        private static bool TryResolveParentPanelInteractable(Transform start, out IPanelInteractable component)
        {
            component = null;
            Transform current = start;
            int depth = 0;
            while (current != null && depth++ < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void RefreshTickRegistration(bool handInside)
        {
            if (handInside || _pressDispatched || _pressed01 > VisualSettleEpsilon)
            {
                TryRegister();
                return;
            }

            if (_pressed01 != 0f)
            {
                _pressed01 = 0f;
                if (buttonMesh != null)
                    buttonMesh.localPosition = _baseLocalPosition;
            }

            _signalCooldownRemaining = 0f;
            _holdEventRemaining = 0f;
            Unregister();
        }

        private void Unregister()
        {
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);

            if (activationVolume != null)
                activationVolume.isTrigger = true;

            pressDepthMeters = ClampFiniteRange(
                pressDepthMeters,
                MinimumPressDepthMeters,
                MaximumPressDepthMeters,
                0.012f);
            depressSpeed = ClampFiniteRange(depressSpeed, MinimumVisualSpeed, MaximumVisualSpeed, 22f);
            releaseSpeed = ClampFiniteRange(releaseSpeed, MinimumVisualSpeed, MaximumVisualSpeed, 14f);
            signalCooldownSeconds = ClampFiniteRange(
                signalCooldownSeconds,
                MinimumSignalCooldownSeconds,
                MaximumSignalCooldownSeconds,
                0.18f);
            pressHapticLowFrequency = ClampFiniteRange(pressHapticLowFrequency, 0f, MaximumPressHapticLowFrequency, 0.06f);
            pressHapticHighFrequency = ClampFiniteRange(pressHapticHighFrequency, 0f, MaximumPressHapticHighFrequency, 0.18f);
            pressHapticDurationSeconds = ClampFiniteRange(
                pressHapticDurationSeconds,
                MinimumPressHapticDurationSeconds,
                MaximumPressHapticDurationSeconds,
                0.035f);
            pressHapticFrequencyHz = ClampFiniteRange(pressHapticFrequencyHz, 0f, MaximumPressHapticFrequencyHz, 54f);
            clickVolume = ClampFiniteRange(clickVolume, 0f, MaximumClickVolume, 0.42f);
            clickPitch = ClampFiniteRange(clickPitch, MinimumClickPitch, MaximumClickPitch, 1f);
            CacheScalarConfig();
        }
#endif
    }
}
