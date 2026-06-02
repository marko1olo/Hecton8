namespace Hecton8.Vehicles.DropPod
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Interaction;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;
    using CoreAudioEvent = Hecton8.Core.AudioEvent;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Vehicles/Drop Pod/Dashboard Toggle Switch")]
    public sealed class DropPodDashboardToggleSwitch : MonoBehaviour,
        IInteractable,
        IInteractableTextProvider,
        IPhysicalPanelButtonReceiver,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener
    {
        private const float MaximumDeltaTime = 0.05f;
        private const float MinimumMotionSeconds = 0.05f;
        private const float MaximumMotionSeconds = 0.8f;
        private const int SourceIdSalt = 1602 << 8;
        private const byte HapticPriority = 3;
        private const byte BothMotorMask = 0b0011;
        private static int s_x001DropPodDashboardToggleSignalPushDropCount;

        [Header("Switch")]
        [SerializeField] private Transform switchTransform;
        [SerializeField] private Collider activationCollider;
        [SerializeField] private Vector3 offLocalEulerDegrees;
        [SerializeField] private Vector3 onLocalEulerDegrees = new Vector3(-18f, 0f, 0f);
        [SerializeField, Range(0.05f, 0.8f)] private float motionSeconds = 0.12f;
        [SerializeField] private bool initialOn;
        [SerializeField] private DropPodCommandId commandOn = DropPodCommandId.DashboardToggle;
        [SerializeField] private DropPodCommandId commandOff = DropPodCommandId.ToggleAuxPower;

        [Header("Prompt")]
        [SerializeField] private string offPrompt = "Engage Switch";
        [SerializeField] private string onPrompt = "Disengage Switch";

        [Header("Feedback")]
        [SerializeField] private bool emitAudio = true;
        [SerializeField] private uint clickAudioEventId;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.26f;
        [SerializeField, Range(0.25f, 2.5f)] private float audioPitch = 1.04f;

        private Transform _cachedTransform;
        private IAudioService _audioService;
        private Quaternion _offRotation = Quaternion.identity;
        private Quaternion _onRotation = Quaternion.identity;
        private Vector3 _pendingFeedbackPosition;
        private float _pendingVisual01;
        private float _position01;
        private float _target01;
        private uint _sourceId;
        private byte _pendingFeedbackMotorMask = BothMotorMask;
        private bool _isOn;
        private bool _moving;
        private bool _hovered;
        private bool _feedbackPending;
        private bool _visualDirty;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _receiverRegistered;
        private Collider _registeredCollider;

        private void Awake()
        {
            _cachedTransform = transform;
            _sourceId = ResolveSourceId();
            CacheColdReferences();
            CacheRotations();
            _isOn = initialOn;
            _position01 = _isOn ? 1f : 0f;
            _target01 = _position01;
            ApplyVisual(_position01);
            DropPodSignalLaneBootstrap.EnsureConfigured();
        }

        private void OnEnable()
        {
            CacheColdReferences();
            DropPodSignalLaneBootstrap.EnsureConfigured();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            RegisterReceiver();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            _hovered = false;
            _moving = false;
            _feedbackPending = false;
            SnapVisualToCommittedState();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            _hovered = false;
            _moving = false;
            _feedbackPending = false;
            SnapVisualToCommittedState();
        }

        public void OnHoverStart()
        {
            _hovered = true;
        }

        public void OnHoverEnd()
        {
            _hovered = false;
        }

        public void Interact(Transform interactor)
        {
            Vector3 position = interactor != null ? interactor.position : ReadSwitchPosition();
            Toggle(position, BothMotorMask, DropPodSignalFlags.PlayerFallback);
        }

        public string GetInteractText()
        {
            return _isOn ? onPrompt : offPrompt;
        }

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_isOn ? onPrompt : offPrompt, destination, out length);
        }

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            return Toggle(handPosition, ResolveMotorMask(fallbackHandSide), DropPodSignalFlags.PhysicalHand);
        }

        public void LateFrameTick()
        {
            if (_moving)
                AdvanceVisualMotion(SystemDispatcher.CurrentFrameDeltaTime);

            FlushVisual();
            DispatchCompletionFeedback();
            if (!_hovered && !_moving && !_feedbackPending && !_visualDirty)
                UnregisterLate();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                _audioService = currentService as IAudioService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterTicks();
            if (!isActiveAndEnabled)
                return;
            if (_moving)
            {
                if (currentService == null || !TryRegisterTicks())
                {
                    CancelMotionForLostDispatcherRoute();
                    return;
                }
            }
            else if (_hovered || _feedbackPending || _visualDirty)
            {
                if (currentService == null || !TryRegisterTicks())
                    ClearLateOnlyStateForLostDispatcherRoute();
            }
        }

        private bool Toggle(Vector3 worldPosition, byte feedbackMotorMask, byte flags)
        {
            if (!isActiveAndEnabled || !DropPodSplineMath.IsFinite(worldPosition))
                return false;
            if (!TryRegisterTicks())
                return false;

            _isOn = !_isOn;
            _target01 = _isOn ? 1f : 0f;
            _moving = math.abs(_target01 - _position01) > 0.001f;
            _pendingFeedbackPosition = worldPosition;
            _pendingFeedbackMotorMask = feedbackMotorMask;
            _feedbackPending = true;
            PublishCommand(_isOn ? commandOn : commandOff, flags);
            return true;
        }

        private void PublishCommand(DropPodCommandId commandId, byte flags)
        {
            DropPodCommandSignal signal = default;
            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.CommandId = (uint)commandId;
            signal.SourceId = _sourceId;
            signal.Flags = flags;
            signal.QualityByte = DropPodSignalLaneBootstrap.EncodeQualityByte(SignalBusRegistry.GlobalQualityWeight01);
            signal.Sequence = NextSequence(signal.Frame);
            SignalBus<DropPodCommandSignal>.TryPushTracked(in signal, ref s_x001DropPodDashboardToggleSignalPushDropCount);
        }

        private void AdvanceVisualMotion(float deltaTime)
        {
            float dt = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0f, 0f, MaximumDeltaTime);
            float duration = ResolveMotionDuration(motionSeconds);
            float direction = _target01 >= _position01 ? 1f : -1f;
            float next = _position01 + direction * dt / duration;
            if (!math.isfinite(next))
            {
                next = _target01;
                _moving = false;
            }
            else if ((direction > 0f && next >= _target01) || (direction < 0f && next <= _target01))
            {
                next = _target01;
                _moving = false;
            }

            _position01 = math.saturate(next);
            QueueVisual(DropPodSplineMath.SmoothStep01(_position01));
        }

        private static float ResolveMotionDuration(float seconds)
        {
            return math.isfinite(seconds) ? math.clamp(seconds, MinimumMotionSeconds, MaximumMotionSeconds) : MinimumMotionSeconds;
        }

        private void QueueVisual(float visual01)
        {
            _pendingVisual01 = DropPodSplineMath.SanitizeUnit01(visual01);
            _visualDirty = true;
        }

        private void FlushVisual()
        {
            if (!_visualDirty)
                return;

            _visualDirty = false;
            ApplyVisual(_pendingVisual01);
        }

        private ushort NextSequence(uint frame)
        {
            return DropPodSignalLaneBootstrap.NextSequence(frame);
        }

        private void ApplyVisual(float t)
        {
            Transform target = switchTransform != null ? switchTransform : _cachedTransform;
            if (target == null)
                return;

            target.localRotation = DropPodSplineMath.ResolveNlerp(_offRotation, _onRotation, t);
        }

        private void SnapVisualToCommittedState()
        {
            _position01 = _isOn ? 1f : 0f;
            _target01 = _position01;
            _pendingVisual01 = DropPodSplineMath.SmoothStep01(_position01);
            _visualDirty = false;
            ApplyVisual(_pendingVisual01);
        }

        private void CancelMotionForLostDispatcherRoute()
        {
            _moving = false;
            _feedbackPending = false;
            SnapVisualToCommittedState();
            UnregisterTicks();
        }

        private void ClearLateOnlyStateForLostDispatcherRoute()
        {
            _feedbackPending = false;
            SnapVisualToCommittedState();
            UnregisterTicks();
        }

        private void EnqueueClickHaptic(byte motorMask)
        {
            float quality = DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01);
            float low = 0.12f * math.lerp(0.5f, 1.1f, quality);
            float high = 0.35f * math.lerp(0.45f, 1.18f, quality);
            float duration = 0.04f * math.lerp(0.7f, 1.25f, quality);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                math.saturate(low),
                math.saturate(high),
                duration,
                88f,
                HapticPriority,
                motorMask);
        }

        private void DispatchCompletionFeedback()
        {
            if (!_feedbackPending || _moving)
                return;

            _feedbackPending = false;
            EnqueueClickHaptic(_pendingFeedbackMotorMask);
            QueueAudio(_pendingFeedbackPosition);
        }

        private void QueueAudio(Vector3 worldPosition)
        {
            IAudioService audio = _audioService;
            if (!emitAudio || clickAudioEventId == 0u || audio == null || !audio.IsInitialized || !DropPodSplineMath.IsFinite(worldPosition))
                return;

            CoreAudioEvent audioEvent = new CoreAudioEvent(
                clickAudioEventId,
                worldPosition,
                DropPodSplineMath.SanitizeRange(audioVolume, 0f, 1f, 0f),
                DropPodSplineMath.SanitizeRange(audioPitch, 0.25f, 2.5f, 1f));
            audio.QueueAudioEvent(in audioEvent);
        }

        private bool TryRegisterTicks()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            return _registeredLate;
        }

        private void UnregisterTicks()
        {
            UnregisterLate();
        }

        private void UnregisterLate()
        {
            if (!_registeredLate)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLate = false;
        }

        private void RegisterReceiver()
        {
            if (_receiverRegistered || activationCollider == null)
                return;

            _registeredCollider = activationCollider;
            _receiverRegistered = PhysicalHandReceiverRegistry.TryRegister(_registeredCollider, this);
            if (!_receiverRegistered)
                _registeredCollider = null;
        }

        private void UnregisterReceiver()
        {
            if (!_receiverRegistered)
                return;

            PhysicalHandReceiverRegistry.Unregister(_registeredCollider, this);
            _receiverRegistered = false;
            _registeredCollider = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheColdReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            if (activationCollider == null)
                TryGetComponent(out activationCollider);
            _audioService = GlobalRegistry.Audio;
        }

        private void CacheRotations()
        {
            _offRotation = DropPodSplineMath.ResolveLocalEulerNoAlloc(offLocalEulerDegrees);
            _onRotation = DropPodSplineMath.ResolveLocalEulerNoAlloc(onLocalEulerDegrees);
        }

        private Vector3 ReadSwitchPosition()
        {
            if (switchTransform != null)
                return switchTransform.position;
            if (activationCollider != null)
                return activationCollider.bounds.center;
            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private uint ResolveSourceId()
        {
            uint id = gameObject != null ? unchecked((uint)UnityEngine.EntityId.ToULong(gameObject.GetEntityId())) : 0u;
            return id ^ SourceIdSalt;
        }

        private static byte ResolveMotorMask(PhysicalHandSide handSide)
        {
            if (handSide == PhysicalHandSide.Left)
                return 0b0001;
            if (handSide == PhysicalHandSide.Right)
                return 0b0010;
            return BothMotorMask;
        }
    }
}
