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
    [AddComponentMenu("Hecton8/Vehicles/Drop Pod/Airlock Controller")]
    public sealed class DropPodAirlockController : MonoBehaviour,
        IInteractable,
        IInteractableTextProvider,
        IPhysicalPanelButtonReceiver,
        IFixedTickable,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener
    {
        private const float MaxFixedDeltaSeconds = 0.05f;
        private const float MinMotionSeconds = 0.08f;
        private const float MaxMotionSeconds = 4f;
        private const int SourceIdSalt = 1602;
        private const byte BothMotorMask = 0b0011;
        private const byte HapticPriority = 3;
        private static int s_x001DropPodAirlockSignalPushDropCount;

        [Header("Geometry")]
        [SerializeField] private Transform hatchTransform;
        [SerializeField] private Transform handleAnchor;
        [SerializeField] private Collider activationCollider;
        [SerializeField] private Vector3 openLocalEulerDegrees;
        [SerializeField] private Vector3 sealedLocalEulerDegrees = new Vector3(0f, -86f, 0f);
        [SerializeField, Range(0.08f, 4f)] private float sealSeconds = 1.1f;
        [SerializeField] private bool startSealed;

        [Header("IK")]
        [SerializeField] private MonoBehaviour handIkTargetSinkBehaviour;
        [SerializeField] private PhysicalHandController physicalHandController;
        [SerializeField, Range(0.02f, 0.5f)] private float handTargetHoldSeconds = 0.12f;
        [SerializeField, Range(0f, 1f)] private float handTargetBlend = 0.85f;

        [Header("Prompt")]
        [SerializeField] private string openPrompt = "Seal Hatch";
        [SerializeField] private string sealedPrompt = "Unseal Hatch";

        [Header("Feedback")]
        [SerializeField] private bool emitAudio = true;
        [SerializeField] private uint sealAudioEventId;
        [SerializeField] private uint unlockAudioEventId;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.48f;
        [SerializeField, Range(0.25f, 2.5f)] private float audioPitch = 0.86f;

        private Transform _cachedTransform;
        private IPhysicalHandIkTargetSink _handIkSink;
        private IAudioService _audioService;
        private Quaternion _openLocalRotation = Quaternion.identity;
        private Quaternion _sealedLocalRotation = Quaternion.identity;
        private float _seal01;
        private float _targetSeal01;
        private Vector3 _pendingFeedbackPosition;
        private uint _sourceId;
        private PhysicalHandSide _activeHandSide = PhysicalHandSide.Right;
        private byte _pendingFeedbackMotorMask = BothMotorMask;
        private bool _pendingFeedbackTargetSealed;
        private bool _feedbackPending;
        private bool _sealed;
        private bool _moving;
        private bool _registeredFixed;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _receiverRegistered;
        private bool _hovered;
        private bool _dispatcherAvailable;
        private bool _hatchRotationDirty;
        private Collider _registeredCollider;
        private Quaternion _pendingHatchLocalRotation = Quaternion.identity;

        public bool IsSealed => _sealed;
        public bool IsMoving => _moving;

        private void Awake()
        {
            _cachedTransform = transform;
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            CacheColdReferences();
            CacheRotations();
            _sealed = startSealed;
            _seal01 = _sealed ? 1f : 0f;
            _targetSeal01 = _seal01;
            ApplyHatchRotation(_seal01);
            _sourceId = ResolveSourceId();
            DropPodSignalLaneBootstrap.EnsureConfigured();
        }

        private void OnEnable()
        {
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            CacheColdReferences();
            DropPodSignalLaneBootstrap.EnsureConfigured();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            RegisterReceiver();
            PublishStatus(_sealed ? DropPodStatusId.AirlockSealed : DropPodStatusId.AirlockOpen, DropPodSignalFlags.VisualOnly);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            ClearHandTarget();
            _feedbackPending = false;
            _hovered = false;
            _moving = false;
            SnapToCommittedSealState();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            ClearHandTarget();
            _feedbackPending = false;
            _hovered = false;
            _moving = false;
            SnapToCommittedSealState();
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
            Vector3 samplePosition = interactor != null ? interactor.position : ReadHandlePosition();
            QueueSealToggle(samplePosition, Vector3.forward, PhysicalHandSide.Right, BothMotorMask, DropPodSignalFlags.PlayerFallback);
        }

        public string GetInteractText()
        {
            return _sealed ? sealedPrompt : openPrompt;
        }

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_sealed ? sealedPrompt : openPrompt, destination, out length);
        }

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            return QueueSealToggle(
                handPosition,
                handForward,
                fallbackHandSide,
                ResolveMotorMask(fallbackHandSide),
                DropPodSignalFlags.PhysicalHand);
        }

        void IFixedTickable.FixedTick(float fixedDeltaTime)
        {
            if (!_moving)
            {
                UnregisterFixed();
                return;
            }

            float dt = math.clamp(math.isfinite(fixedDeltaTime) ? fixedDeltaTime : 0f, 0f, MaxFixedDeltaSeconds);
            if (dt <= 0f)
                return;

            float duration = ResolveSealDuration(sealSeconds);
            float direction = _targetSeal01 >= _seal01 ? 1f : -1f;
            float next = _seal01 + direction * dt / duration;
            if (!math.isfinite(next))
            {
                next = _targetSeal01;
                _moving = false;
            }
            else if ((direction > 0f && next >= _targetSeal01) || (direction < 0f && next <= _targetSeal01))
            {
                next = _targetSeal01;
                _moving = false;
            }

            _seal01 = math.saturate(next);
            ApplyHatchRotation(DropPodSplineMath.SmoothStep01(_seal01));
            DispatchHandTarget();

            if (!_moving)
            {
                _sealed = _seal01 >= 0.995f;
                PublishStatus(_sealed ? DropPodStatusId.AirlockSealed : DropPodStatusId.AirlockOpen, DropPodSignalFlags.None);
                ClearHandTarget();
                UnregisterFixed();
            }
        }

        public void LateFrameTick()
        {
            if (_hatchRotationDirty)
                FlushHatchRotation();
            DispatchCompletionFeedback();
            if (!_moving && !_hovered && !_feedbackPending)
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

            _dispatcherAvailable = currentService != null;
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
            else if (_hovered || _feedbackPending)
            {
                if (currentService == null || !TryRegisterLate())
                    ClearLateOnlyStateForLostDispatcherRoute();
            }
        }

        private bool QueueSealToggle(
            Vector3 worldPosition,
            Vector3 handForward,
            PhysicalHandSide handSide,
            byte feedbackMotorMask,
            byte sourceFlags)
        {
            if (!isActiveAndEnabled || !DropPodSplineMath.IsFinite(worldPosition))
                return false;

            bool targetSealed = ResolveNextSealTarget();
            PhysicalHandSide ikHandSide = ResolveIkHandSide(handSide);
            _activeHandSide = ikHandSide;
            _pendingFeedbackPosition = worldPosition;
            _pendingFeedbackMotorMask = feedbackMotorMask;
            _pendingFeedbackTargetSealed = targetSealed;
            _targetSeal01 = targetSealed ? 1f : 0f;
            _moving = math.abs(_targetSeal01 - _seal01) > 0.001f;
            _feedbackPending = _moving;
            if (!_moving)
            {
                PublishCommand(targetSealed ? DropPodCommandId.LockHatch : DropPodCommandId.UnlockHatch, sourceFlags);
                PublishCommittedSealStatus(sourceFlags);
                return true;
            }

            if (!TryRegisterTicks())
            {
                _targetSeal01 = _seal01;
                _moving = false;
                _feedbackPending = false;
                UnregisterTicks();
                PublishStatus(DropPodStatusId.FailClosed, (byte)(sourceFlags | DropPodSignalFlags.FailClosed));
                return false;
            }

            if (!targetSealed)
                _sealed = false;

            PublishCommand(targetSealed ? DropPodCommandId.LockHatch : DropPodCommandId.UnlockHatch, sourceFlags);
            PublishStatus(DropPodStatusId.AirlockMoving, sourceFlags);
            return true;
        }

        private bool ResolveNextSealTarget()
        {
            if (_moving)
                return _targetSeal01 < 0.5f;

            return !_sealed;
        }

        private void ApplyHatchRotation(float t)
        {
            Transform hatch = hatchTransform != null ? hatchTransform : _cachedTransform;
            if (hatch == null)
                return;

            _pendingHatchLocalRotation = DropPodSplineMath.ResolveNlerp(_openLocalRotation, _sealedLocalRotation, t);
            _hatchRotationDirty = true;
            if (!Application.isPlaying || !_registeredFixed)
            {
                FlushHatchRotation();
                return;
            }

            TryRegisterLate();
        }

        private void FlushHatchRotation()
        {
            Transform hatch = hatchTransform != null ? hatchTransform : _cachedTransform;
            if (hatch != null)
                hatch.localRotation = _pendingHatchLocalRotation;
            _hatchRotationDirty = false;
        }

        private void SnapToCommittedSealState()
        {
            _seal01 = _sealed ? 1f : 0f;
            _targetSeal01 = _seal01;
            ApplyHatchRotation(_seal01);
        }

        private void FreezeMotionAtCurrentSealPose()
        {
            _seal01 = DropPodSplineMath.SanitizeUnit01(_seal01);
            _targetSeal01 = _seal01;
            _sealed = _seal01 >= 0.995f;
            ApplyHatchRotation(DropPodSplineMath.SmoothStep01(_seal01));
        }

        private void CancelMotionForLostDispatcherRoute()
        {
            _moving = false;
            _feedbackPending = false;
            FreezeMotionAtCurrentSealPose();
            ClearHandTarget();
            PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);
            UnregisterTicks();
        }

        private void ClearLateOnlyStateForLostDispatcherRoute()
        {
            _feedbackPending = false;
            ClearHandTarget();
            UnregisterTicks();
        }

        private void PublishCommittedSealStatus(byte flags)
        {
            PublishStatus(_sealed ? DropPodStatusId.AirlockSealed : DropPodStatusId.AirlockOpen, flags);
        }

        private void DispatchHandTarget()
        {
            Transform anchor = handleAnchor;
            if (anchor == null)
                return;

            Vector3 position = anchor.position;
            Quaternion rotation = anchor.rotation;
            if (!DropPodSplineMath.IsFinite(position) || !DropPodSplineMath.IsFinite(rotation))
                return;

            PhysicalHandIkTarget target = new PhysicalHandIkTarget(
                unchecked((int)_sourceId),
                _activeHandSide,
                position,
                rotation,
                ResolveHandTargetHoldSeconds(handTargetHoldSeconds),
                ResolveHandTargetBlend(handTargetBlend));

            if (_handIkSink != null)
                _handIkSink.SetTerminalHandTarget(in target);
            else if (physicalHandController != null)
                physicalHandController.SetTerminalHandTarget(in target);
        }

        private void ClearHandTarget()
        {
            int sourceId = unchecked((int)_sourceId);
            if (_handIkSink != null)
                _handIkSink.ClearTerminalHandTarget(sourceId);
            else if (physicalHandController != null)
                physicalHandController.ClearTerminalHandTarget(sourceId);
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
            SignalBus<DropPodCommandSignal>.TryPushTracked(in signal, ref s_x001DropPodAirlockSignalPushDropCount);
        }

        private void PublishStatus(DropPodStatusId statusId, byte flags)
        {
            DropPodStatusSignal signal = default;
            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.StatusId = (uint)statusId;
            signal.SourceId = _sourceId;
            signal.Flags = flags;
            signal.QualityByte = DropPodSignalLaneBootstrap.EncodeQualityByte(SignalBusRegistry.GlobalQualityWeight01);
            signal.Sequence = NextSequence(signal.Frame);
            SignalBus<DropPodStatusSignal>.TryPushTracked(in signal, ref s_x001DropPodAirlockSignalPushDropCount);
        }

        private ushort NextSequence(uint frame)
        {
            return DropPodSignalLaneBootstrap.NextSequence(frame);
        }

        private void EnqueueClickHaptic(byte motorMask)
        {
            float quality = DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01);
            float low = 0.24f * math.lerp(0.55f, 1.15f, quality);
            float high = 0.66f * math.lerp(0.5f, 1.2f, quality);
            float duration = 0.075f * math.lerp(0.75f, 1.3f, quality);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                math.saturate(low),
                math.saturate(high),
                duration,
                58f,
                HapticPriority,
                motorMask);
        }

        private void DispatchCompletionFeedback()
        {
            if (!_feedbackPending || _moving)
                return;

            _feedbackPending = false;
            EnqueueClickHaptic(_pendingFeedbackMotorMask);
            QueueAudio(_pendingFeedbackPosition, _pendingFeedbackTargetSealed);
        }

        private void QueueAudio(Vector3 worldPosition, bool targetSealed)
        {
            IAudioService audio = _audioService;
            uint eventId = targetSealed ? sealAudioEventId : unlockAudioEventId;
            if (!emitAudio || eventId == 0u || audio == null || !audio.IsInitialized || !DropPodSplineMath.IsFinite(worldPosition))
                return;

            CoreAudioEvent audioEvent = new CoreAudioEvent(
                eventId,
                worldPosition,
                DropPodSplineMath.SanitizeRange(audioVolume, 0f, 1f, 0f),
                DropPodSplineMath.SanitizeRange(audioPitch, 0.25f, 2.5f, 1f));
            audio.QueueAudioEvent(in audioEvent);
        }

        private bool TryRegisterTicks()
        {
            if (!Application.isPlaying || !_dispatcherAvailable)
                return false;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            if (_registeredFixed && TryRegisterLate())
                return true;

            UnregisterTicks();
            return false;
        }

        private bool TryRegisterLate()
        {
            if (_registeredLate)
                return true;
            if (!Application.isPlaying || !_dispatcherAvailable)
                return false;

            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            return _registeredLate;
        }

        private void UnregisterTicks()
        {
            UnregisterFixed();
            UnregisterLate();
        }

        private void UnregisterFixed()
        {
            if (!_registeredFixed)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixed = false;
        }

        private void UnregisterLate()
        {
            if (!_registeredLate)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLate = false;
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

        private void RegisterReceiver()
        {
            Collider target = activationCollider;
            if (_receiverRegistered || target == null)
                return;

            _receiverRegistered = PhysicalHandReceiverRegistry.TryRegister(target, this);
            _registeredCollider = _receiverRegistered ? target : null;
        }

        private void UnregisterReceiver()
        {
            if (!_receiverRegistered)
                return;

            PhysicalHandReceiverRegistry.Unregister(_registeredCollider, this);
            _receiverRegistered = false;
            _registeredCollider = null;
        }

        private void CacheColdReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            if (activationCollider == null)
                TryGetComponent(out activationCollider);

            _handIkSink = handIkTargetSinkBehaviour as IPhysicalHandIkTargetSink;
            if (physicalHandController == null)
                physicalHandController = ComponentReferenceUtility.ResolveOwnedComponent<PhysicalHandController>(_cachedTransform);

            _audioService = GlobalRegistry.Audio;
        }

        private void CacheRotations()
        {
            _openLocalRotation = DropPodSplineMath.ResolveLocalEulerNoAlloc(openLocalEulerDegrees);
            _sealedLocalRotation = DropPodSplineMath.ResolveLocalEulerNoAlloc(sealedLocalEulerDegrees);
        }

        private Vector3 ReadHandlePosition()
        {
            if (handleAnchor != null)
                return handleAnchor.position;
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

        private static PhysicalHandSide ResolveIkHandSide(PhysicalHandSide handSide)
        {
            if (handSide == PhysicalHandSide.Left || handSide == PhysicalHandSide.Right)
                return handSide;

            return PhysicalHandSide.Right;
        }

        private static float ResolveSealDuration(float seconds)
        {
            return math.isfinite(seconds) ? math.clamp(seconds, MinMotionSeconds, MaxMotionSeconds) : MinMotionSeconds;
        }

        private static float ResolveHandTargetHoldSeconds(float seconds)
        {
            return math.isfinite(seconds) ? math.clamp(seconds, 0.02f, 0.5f) : 0.02f;
        }

        private static float ResolveHandTargetBlend(float blend)
        {
            return DropPodSplineMath.SanitizeUnit01(blend);
        }
    }
}
