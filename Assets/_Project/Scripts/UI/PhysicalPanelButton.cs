using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Collider-volume diegetic button receiver for physical hand interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/UI/Physical Panel Button")]
    public sealed class PhysicalPanelButton : MonoBehaviour, ITickable, IUpdatable, IInteractionSignalConsumer, IPhysicalPanelButtonReceiver
    {
        private const uint PhysicalPanelToolId = 0x50414E4Cu;
        private const float MinimumDeltaTime = 0.0001f;
        private const int RegistryInitialCapacity = 64;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte MicroHapticPriority = 1;
        private const string LeftHandTag = "LeftHand";
        private const string RightHandTag = "RightHand";

        private static readonly Dictionary<Collider, IPhysicalPanelButtonReceiver> _receiversByCollider = new Dictionary<Collider, IPhysicalPanelButtonReceiver>(RegistryInitialCapacity); // COLD ALLOC: Dictionary<Collider,IPhysicalPanelButtonReceiver>[64] - collider to physical panel button registry - owner: PhysicalPanelButton

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
        private float _nextSignalTime;
        private float _pressed01;
        private bool _registered;
        private bool _pressDispatched;
        private bool _acousticRuntimeAcquired;

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
            receiver = null;
            return collider != null &&
                   _receiversByCollider.TryGetValue(collider, out receiver) &&
                   receiver != null;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            _clickOcclusionMask = AcousticOcclusionUtility.BuildSensoryMask();
            ResolveReferences();
            if (buttonMesh != null)
                _baseLocalPosition = buttonMesh.localPosition;
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterCollider();
            AcquireAcousticRuntime();
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
            ReleaseAcousticRuntime();
            UnregisterCollider();
            _lastHandInsideFrame = -1;
            _pressDispatched = false;
            _pressed01 = 0f;
            if (buttonMesh != null)
                buttonMesh.localPosition = _baseLocalPosition;
        }

        private void OnDestroy()
        {
            ReleaseAcousticRuntime();
            UnregisterCollider();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            bool handInside = _lastHandInsideFrame == Time.frameCount;
            float target = handInside ? 1f : 0f;
            float speed = handInside ? depressSpeed : releaseSpeed;
            float alpha = 1f - math.exp(-speed * math.max(dt, MinimumDeltaTime));
            _pressed01 = math.lerp(_pressed01, target, alpha);

            if (buttonMesh != null)
            {
                Vector3 offset = new Vector3(0f, 0f, -pressDepthMeters * _pressed01);
                buttonMesh.localPosition = _baseLocalPosition + offset;
            }

            if (!handInside && _pressDispatched)
            {
                DispatchPanelEvent(DiegeticPanelInputEventType.Up);
                _pressDispatched = false;
            }
            else if (handInside && _pressDispatched)
            {
                DispatchPanelEvent(DiegeticPanelInputEventType.Hold);
            }
        }

        /// <summary>
        /// Attempts to queue a physical button signal from the player's hand probe.
        /// </summary>
        /// <param name="handPosition">Runtime-space hand position.</param>
        /// <param name="handForward">Runtime-space hand forward vector.</param>
        /// <param name="interactionSignals">Authoritative interaction signal queue.</param>
        /// <returns>True when the hand press was accepted by this button.</returns>
        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            if (activationVolume == null || interactionSignals == null || !interactionSignals.IsInitialized)
                return false;

            _lastHandInsideFrame = Time.frameCount;
            float now = Time.unscaledTime;
            if (now < _nextSignalTime)
                return true;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(handPosition);
            Vector3 safeDirection = (Vector3)math.normalizesafe((float3)handForward, (float3)transform.forward);
            InteractionPacket packet = new InteractionPacket(
                PhysicalPanelToolId,
                (float3)absoluteHitPoint,
                (float3)safeDirection,
                1f,
                pressDepthMeters,
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)Time.frameCount));
            InteractionSignal signal = new InteractionSignal(
                packet,
                0,
                (float3)absoluteHitPoint,
                (float3)(-safeDirection),
                1f,
                (byte)signalEffectType,
                0);

            if (!interactionSignals.Publish(in signal, activationVolume))
                return false;

            _nextSignalTime = now + signalCooldownSeconds;
            EmitPressHaptic(handSourceCollider, fallbackHandSide);
            return true;
        }

        bool IPhysicalPanelButtonReceiver.TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            return TryQueueHandPress(handPosition, handForward, interactionSignals, handSourceCollider, fallbackHandSide);
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
            }
        }

        private void EmitPressHaptic(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            if (!emitPressHaptics)
                return;

            byte motorMask = ResolveHapticMotorMask(handSourceCollider, fallbackHandSide);
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                pressHapticLowFrequency,
                pressHapticHighFrequency,
                pressHapticDurationSeconds,
                pressHapticFrequencyHz,
                MicroHapticPriority,
                motorMask);
        }

        private byte ResolveHapticMotorMask(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            if (handSourceCollider != null)
            {
                GameObject sourceObject = handSourceCollider.gameObject;
                int sourceLayerBit = 1 << sourceObject.layer;
                if ((leftHandSourceLayers.value & sourceLayerBit) != 0 || sourceObject.CompareTag(LeftHandTag))
                    return LeftMotorMask;

                if ((rightHandSourceLayers.value & sourceLayerBit) != 0 || sourceObject.CompareTag(RightHandTag))
                    return RightMotorMask;
            }

            return fallbackHandSide == PhysicalHandSide.Left ? LeftMotorMask : RightMotorMask;
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
                    _panelInteractable = GetComponentInParent<IPanelInteractable>();
            }
        }

        private void RegisterCollider()
        {
            if (activationVolume == null)
                return;

            _receiversByCollider[activationVolume] = this;
            PhysicalHandReceiverRegistry.Register(activationVolume, this);
        }

        private void UnregisterCollider()
        {
            if (activationVolume == null)
                return;

            PhysicalHandReceiverRegistry.Unregister(activationVolume, this);
            if (_receiversByCollider.TryGetValue(activationVolume, out IPhysicalPanelButtonReceiver receiver) &&
                ReferenceEquals(receiver, this))
            {
                _receiversByCollider.Remove(activationVolume);
            }
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
                Timestamp = Time.unscaledTime
            });
        }

        private void PlayDiegeticClick(Vector3 runtimeHitPoint)
        {
            if (pressClickSound == null || GlobalRegistry.Audio == null)
                return;

            Vector3 sourcePosition = clickAudioOrigin != null
                ? clickAudioOrigin.position
                : runtimeHitPoint;
            if (!IsFinite(sourcePosition))
                sourcePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;

            Transform listenerTransform = ResolveListenerTransform();
            Vector3 listenerPosition = listenerTransform != null ? listenerTransform.position : sourcePosition;
            float resolvedVolume = clickVolume;
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

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudio)
            {
                spatialAudio.PlayAtPointWithLowPass(
                    pressClickSound,
                    sourcePosition,
                    resolvedVolume,
                    clickPitch,
                    null,
                    lowPassCutoff);
                return;
            }

            GlobalRegistry.Audio.PlayAtPoint(pressClickSound, sourcePosition, resolvedVolume, clickPitch);
        }

        private static Transform ResolveListenerTransform()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return null;

            if (playerContext.PlayerCamera != null)
                return playerContext.PlayerCamera.transform;

            return playerContext.PlayerTransform;
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

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

            if (pressDepthMeters < 0.001f)
                pressDepthMeters = 0.001f;
            pressHapticLowFrequency = math.clamp(pressHapticLowFrequency, 0f, 0.4f);
            pressHapticHighFrequency = math.clamp(pressHapticHighFrequency, 0f, 0.6f);
            pressHapticDurationSeconds = math.clamp(pressHapticDurationSeconds, 0.01f, 0.12f);
            pressHapticFrequencyHz = math.clamp(pressHapticFrequencyHz, 0f, 80f);
        }
#endif
    }
}
