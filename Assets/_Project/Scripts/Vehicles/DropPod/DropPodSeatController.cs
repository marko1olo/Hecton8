namespace Hecton8.Vehicles.DropPod
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Interaction;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;
    using CoreAudioEvent = Hecton8.Core.AudioEvent;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Vehicles/Drop Pod/Seat Controller")]
    public sealed class DropPodSeatController : MonoBehaviour,
        IInteractable,
        IInteractableTextProvider,
        IPhysicalPanelButtonReceiver,
        IFixedTickable,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener
    {
        private const float MaxLateDeltaSeconds = 0.05f;
        private const float MinTransitSeconds = 0.2f;
        private const float MaxTransitSeconds = 8f;
        private const float FallbackTransitSeconds = 2.35f;
        private const int SourceIdSalt = 1602 << 4;
        private const byte HapticPriority = 3;
        private const byte BothMotorMask = 0b0011;
        private const uint TransitInputBlockMask =
            (uint)InputBlockMaskFlags.BlockMovement |
            (uint)InputBlockMaskFlags.BlockLook |
            (uint)InputBlockMaskFlags.BlockTools |
            (uint)InputBlockMaskFlags.BlockDiscrete;
        private static int s_x001DropPodSeatSignalPushDropCount;

        [Header("References")]
        [SerializeField] private DropPodAirlockController airlock;
        [SerializeField] private Transform seatEyeAnchor;
        [SerializeField] private Transform splineControlA;
        [SerializeField] private Transform splineControlB;
        [SerializeField] private Collider activationCollider;

        [Header("Transit")]
        [SerializeField, Range(0.2f, 8f)] private float transitSeconds = 2.35f;
        [SerializeField, Range(0f, 1f)] private float cameraRollBlend = 0.18f;
        [SerializeField] private bool publishLandingAnchorOnComplete;

        [Header("Prompt")]
        [SerializeField] private string readyPrompt = "Strap In";
        [SerializeField] private string blockedPrompt = "Seal Hatch First";
        [SerializeField] private string seatedPrompt = "Pilot Locked";

        [Header("Feedback")]
        [SerializeField] private bool emitAudio = true;
        [SerializeField] private uint beginAudioEventId;
        [SerializeField] private uint completeAudioEventId;
        [SerializeField] private uint blockedAudioEventId;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.44f;
        [SerializeField, Range(0.25f, 2.5f)] private float audioPitch = 0.92f;

        private Transform _cachedTransform;
        private Camera _playerCamera;
        private Transform _cameraTransform;
        private IInputDeterminismService _inputBlockService;
        private IPlayerSeatLockMotorSink _seatLockMotor;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAudioService _audioService;
        private Vector3 _startPosition;
        private Vector3 _controlA;
        private Vector3 _controlB;
        private Vector3 _endPosition;
        private Quaternion _startRotation = Quaternion.identity;
        private Quaternion _endRotation = Quaternion.identity;
        private uint _ownedInputBlockBits;
        private uint _sourceId;
        private float _elapsedSeconds;
        private double _lastLateTickTimeSeconds;
        private Vector3 _pendingFeedbackPosition;
        private uint _pendingFeedbackEventId;
        private byte _pendingFeedbackMotorMask;
        private bool _transiting;
        private bool _seated;
        private bool _inputBlocked;
        private bool _registeredFixed;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _receiverRegistered;
        private bool _hovered;
        private bool _feedbackPending;
        private bool _inputRouteFailClosedPending;
        private bool _cameraRouteFailClosedPending;
        private Collider _registeredCollider;

        private void Awake()
        {
            _cachedTransform = transform;
            _sourceId = ResolveSourceId();
            CacheColdReferences();
            DropPodSignalLaneBootstrap.EnsureConfigured();
        }

        private void OnEnable()
        {
            CacheColdReferences();
            DropPodSignalLaneBootstrap.EnsureConfigured();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            RegisterReceiver();
            PublishSeatAvailabilityStatus(DropPodSignalFlags.VisualOnly);
        }

        private void OnDisable()
        {
            AbortTransitLocal();
            RestoreInputBlock();
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            _hovered = false;
            _feedbackPending = false;
            _inputRouteFailClosedPending = false;
            _cameraRouteFailClosedPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
        }

        private void OnDestroy()
        {
            AbortTransitLocal();
            RestoreInputBlock();
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            UnregisterTicks();
            TryUnregisterHotSwapListener();
            _hovered = false;
            _feedbackPending = false;
            _inputRouteFailClosedPending = false;
            _cameraRouteFailClosedPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
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
            TryBeginTransit(DropPodSignalFlags.PlayerFallback, BothMotorMask);
        }

        public string GetInteractText()
        {
            if (_seated)
                return seatedPrompt;
            return IsSeatAvailable() ? readyPrompt : blockedPrompt;
        }

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            string prompt = _seated ? seatedPrompt : (IsSeatAvailable() ? readyPrompt : blockedPrompt);
            return InteractableTextCopy.TryCopy(prompt, destination, out length);
        }

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            return TryBeginTransit(DropPodSignalFlags.PhysicalHand, ResolveMotorMask(fallbackHandSide));
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_transiting || _seatLockMotor == null || !_seatLockMotor.HasControllableBody)
            {
                UnregisterFixed();
                return;
            }

            _seatLockMotor.MoveSeatLockPosition(_endPosition);
            _seatLockMotor.SetSeatLockLinearVelocity(Vector3.zero);
        }

        public void LateFrameTick()
        {
            if (DispatchPendingInputRouteFailure() || DispatchPendingCameraRouteFailure())
            {
                DispatchPendingFeedback();
                if (!_hovered)
                    UnregisterLate();
                return;
            }

            if (!_transiting)
            {
                DispatchPendingFeedback();
                if (!_hovered)
                    UnregisterLate();
                return;
            }

            DispatchPendingFeedback();
            if (_cameraTransform == null)
            {
                AbortTransitForLostCameraRoute();
                DispatchPendingFeedback();
                if (!_hovered)
                    UnregisterLate();
                return;
            }

            float dt = ResolveLateDeltaSeconds();
            _elapsedSeconds += dt;
            float t = DropPodSplineMath.ResolveTransitT(_elapsedSeconds, ResolveTransitDuration(transitSeconds));
            Vector3 position = DropPodSplineMath.ResolveBezierPosition(_startPosition, _controlA, _controlB, _endPosition, t);
            Quaternion rotation = DropPodSplineMath.ResolveSlerp(_startRotation, _endRotation, t);
            ApplyCameraPose(position, rotation);

            if (t < 0.999f)
                return;

            CompleteTransit();
            DispatchPendingFeedback();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    Transform previousCameraTransform = _cameraTransform;
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _playerCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
                    _cameraTransform = _playerCamera != null ? _playerCamera.transform : null;
                    if (_transiting && _cameraTransform != previousCameraTransform)
                    {
                        _cameraRouteFailClosedPending = true;
                        UnregisterFixed();
                        if (!TryRegisterLate())
                            AbortTransitForLostCameraRoute(false);
                    }
                    break;
                case GlobalRegistryServiceSlot.PlayerMotor:
                    _seatLockMotor = currentService as IPlayerSeatLockMotorSink;
                    RefreshSeatLockMotorRegistration();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Input:
                    RebindInputBlockService(currentService as IInputDeterminismService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterTicks();
                    if (!isActiveAndEnabled)
                        return;
                    if (_transiting)
                    {
                        if (currentService == null || !TryRegisterTicks())
                        {
                            AbortTransitForLostDispatcherRoute();
                            return;
                        }
                    }
                    else if (_hovered || _feedbackPending)
                    {
                        if (currentService == null || !TryRegisterLate())
                            ClearPendingFeedback();
                    }
                    break;
            }
        }

        private bool TryBeginTransit(byte flags, byte startMotorMask)
        {
            if (_transiting || _seated)
                return true;

            if (!IsSeatAvailable())
            {
                PublishCommand(DropPodCommandId.StrapIn, (byte)(flags | DropPodSignalFlags.FailClosed));
                PublishStatus(DropPodStatusId.SeatBlockedAirlockOpen, (byte)(flags | DropPodSignalFlags.FailClosed));
                QueueFeedbackIfLateRouteAvailable(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                return false;
            }

            CacheColdReferences();
            if (_cameraTransform == null || seatEyeAnchor == null)
            {
                PublishStatus(DropPodStatusId.FailClosed, (byte)(flags | DropPodSignalFlags.FailClosed));
                QueueFeedbackIfLateRouteAvailable(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                return false;
            }

            _startPosition = _cameraTransform.position;
            _startRotation = _cameraTransform.rotation;
            _endPosition = seatEyeAnchor.position;
            _endRotation = ResolveSeatRotation();
            _controlA = splineControlA != null ? splineControlA.position : ResolveFallbackControl(_startPosition, _endPosition, 0.34f);
            _controlB = splineControlB != null ? splineControlB.position : ResolveFallbackControl(_startPosition, _endPosition, 0.72f);
            if (!DropPodSplineMath.IsFinite(_startPosition) ||
                !DropPodSplineMath.IsFinite(_controlA) ||
                !DropPodSplineMath.IsFinite(_controlB) ||
                !DropPodSplineMath.IsFinite(_endPosition) ||
                !DropPodSplineMath.IsFinite(_startRotation) ||
                !DropPodSplineMath.IsFinite(_endRotation))
            {
                PublishStatus(DropPodStatusId.FailClosed, (byte)(flags | DropPodSignalFlags.FailClosed));
                QueueFeedbackIfLateRouteAvailable(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                return false;
            }

            if (!TryBlockInput())
            {
                PublishStatus(DropPodStatusId.FailClosed, (byte)(flags | DropPodSignalFlags.FailClosed));
                QueueFeedbackIfLateRouteAvailable(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                return false;
            }

            if (!TryRegisterTicks())
            {
                RestoreInputBlock();
                UnregisterTicks();
                PublishStatus(DropPodStatusId.FailClosed, (byte)(flags | DropPodSignalFlags.FailClosed));
                QueueFeedbackIfLateRouteAvailable(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                return false;
            }

            _elapsedSeconds = 0f;
            _lastLateTickTimeSeconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _transiting = true;
            PublishCommand(DropPodCommandId.SeatTransitStarted, flags);
            PublishStatus(DropPodStatusId.SeatTransitActive, flags);
            QueueFeedback(_startPosition, beginAudioEventId, startMotorMask);
            return true;
        }

        private void CompleteTransit()
        {
            _transiting = false;
            _seated = true;
            ApplyCameraPose(_endPosition, _endRotation);
            RestoreInputBlock();
            PublishCommand(DropPodCommandId.SeatTransitCompleted, DropPodSignalFlags.None);
            PublishStatus(DropPodStatusId.Seated, DropPodSignalFlags.None);
            if (publishLandingAnchorOnComplete)
                PublishLandingAnchor();
            QueueFeedback(_endPosition, completeAudioEventId, BothMotorMask);
            UnregisterFixed();
        }

        private void AbortTransitLocal()
        {
            AbortTransitLocal(true);
        }

        private void AbortTransitLocal(bool restoreCameraPose)
        {
            if (!_transiting)
                return;

            if (restoreCameraPose)
                ApplyCameraPose(_startPosition, _startRotation);

            _elapsedSeconds = 0f;
            _transiting = false;
        }

        private void AbortTransitForLostCameraRoute()
        {
            AbortTransitForLostCameraRoute(true);
        }

        private void AbortTransitForLostCameraRoute(bool queueFeedback)
        {
            _cameraRouteFailClosedPending = false;
            AbortTransitLocal(false);
            RestoreInputBlock();
            PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);
            PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);
            if (queueFeedback)
                QueueFeedback(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
            UnregisterFixed();
        }

        private void AbortTransitForLostDispatcherRoute()
        {
            AbortTransitLocal(false);
            RestoreInputBlock();
            PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);
            PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);
            _feedbackPending = false;
            _inputRouteFailClosedPending = false;
            _cameraRouteFailClosedPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
            UnregisterTicks();
        }

        private void AbortTransitForLostInputRoute()
        {
            _feedbackPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
            _inputRouteFailClosedPending = true;
            _cameraRouteFailClosedPending = false;
            UnregisterFixed();
            if (TryRegisterLate())
                return;

            _inputRouteFailClosedPending = false;
            AbortTransitLocal(false);
            RestoreInputBlock();
            UnregisterTicks();
            PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);
            PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);
        }

        private bool IsSeatAvailable()
        {
            return airlock == null || airlock.IsSealed;
        }

        private void PublishSeatAvailabilityStatus(byte flags)
        {
            if (IsSeatAvailable())
            {
                PublishStatus(DropPodStatusId.SeatTransitArmed, flags);
                return;
            }

            PublishStatus(DropPodStatusId.SeatBlockedAirlockOpen, (byte)(flags | DropPodSignalFlags.FailClosed));
        }

        private Quaternion ResolveSeatRotation()
        {
            Quaternion target = seatEyeAnchor != null ? seatEyeAnchor.rotation : Quaternion.identity;
            if (!DropPodSplineMath.IsFinite(target))
                target = Quaternion.identity;

            float rollBlend = DropPodSplineMath.SanitizeUnit01(cameraRollBlend);
            if (rollBlend <= 0.0001f)
                return target;

            Vector3 forward = target * Vector3.forward;
            if (!DropPodSplineMath.IsFinite(forward) || forward.sqrMagnitude <= 0.000001f)
                return target;

            Quaternion noRoll = Quaternion.LookRotation(forward, Vector3.up);
            return DropPodSplineMath.ResolveSlerp(noRoll, target, rollBlend);
        }

        private void ApplyCameraPose(Vector3 position, Quaternion rotation)
        {
            Transform cameraTransform = _cameraTransform;
            if (cameraTransform == null || !DropPodSplineMath.IsFinite(position) || !DropPodSplineMath.IsFinite(rotation))
                return;

            cameraTransform.SetPositionAndRotation(position, rotation);
        }

        private float ResolveLateDeltaSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            float rawDeltaSeconds = (float)(now - _lastLateTickTimeSeconds);
            _lastLateTickTimeSeconds = now;
            return math.clamp(math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f, 0f, MaxLateDeltaSeconds);
        }

        private Vector3 ResolveFallbackControl(Vector3 start, Vector3 end, float weight)
        {
            Vector3 delta = end - start;
            Vector3 upBias = _cachedTransform != null ? _cachedTransform.up * 0.24f : Vector3.up * 0.24f;
            Vector3 rightBias = _cachedTransform != null ? _cachedTransform.right * 0.18f : Vector3.right * 0.18f;
            return start + delta * math.saturate(weight) + upBias + rightBias;
        }

        private bool TryBlockInput()
        {
            if (_inputBlocked)
                return true;

            if (_inputBlockService == null)
                CacheInputBlockServiceCold();
            if (_inputBlockService == null)
                return false;

            uint currentMask = _inputBlockService.GetInputBlockMask();
            _ownedInputBlockBits = TransitInputBlockMask & ~currentMask;
            uint mask = currentMask | TransitInputBlockMask;
            _inputBlockService.SetInputBlockMask(mask);
            _inputBlocked = true;
            return true;
        }

        private void RestoreInputBlock()
        {
            if (!_inputBlocked)
                return;

            if (_inputBlockService != null)
            {
                uint currentMask = _inputBlockService.GetInputBlockMask();
                uint restoredMask = currentMask & ~_ownedInputBlockBits;
                _inputBlockService.SetInputBlockMask(restoredMask);
            }

            _inputBlocked = false;
            _ownedInputBlockBits = 0u;
        }

        private void RebindInputBlockService(IInputDeterminismService currentService)
        {
            bool shouldKeepBlocked = _transiting;
            RestoreInputBlock();
            _inputBlockService = currentService;
            if (_inputBlockService == null)
                _inputBlockService = InputDispatcher.ActiveRuntimeInstance;

            if (!shouldKeepBlocked)
                return;

            if (TryBlockInput())
                return;

            AbortTransitForLostInputRoute();
        }

        private bool DispatchPendingInputRouteFailure()
        {
            if (!_inputRouteFailClosedPending)
                return false;

            _inputRouteFailClosedPending = false;
            _cameraRouteFailClosedPending = false;
            if (_transiting)
            {
                AbortTransitLocal();
                RestoreInputBlock();
                QueueFeedback(_cachedTransform != null ? _cachedTransform.position : Vector3.zero, blockedAudioEventId, BothMotorMask);
                UnregisterFixed();
            }

            PublishCommand(DropPodCommandId.AbortTransit, DropPodSignalFlags.FailClosed);
            PublishStatus(DropPodStatusId.FailClosed, DropPodSignalFlags.FailClosed);
            return true;
        }

        private bool DispatchPendingCameraRouteFailure()
        {
            if (!_cameraRouteFailClosedPending)
                return false;

            _cameraRouteFailClosedPending = false;
            if (_transiting)
                AbortTransitForLostCameraRoute();
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
            SignalBus<DropPodCommandSignal>.TryPushTracked(in signal, ref s_x001DropPodSeatSignalPushDropCount);
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
            SignalBus<DropPodStatusSignal>.TryPushTracked(in signal, ref s_x001DropPodSeatSignalPushDropCount);
        }

        private void PublishLandingAnchor()
        {
            AbsoluteUniversePosition origin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!origin.IsFinite() || !DropPodSplineMath.IsFinite(_endPosition))
                return;

            DropPodLandedSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.OffsetMeters(in origin, new double3(_endPosition.x, _endPosition.y, _endPosition.z));
            if (!signal.PositionAup.IsFinite())
                return;

            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.SourceHash = _sourceId;
            SignalBus<DropPodLandedSignal>.TryPushTracked(in signal, ref s_x001DropPodSeatSignalPushDropCount);
        }

        private ushort NextSequence(uint frame)
        {
            return DropPodSignalLaneBootstrap.NextSequence(frame);
        }

        private void QueueFeedback(Vector3 position, uint eventId, byte motorMask)
        {
            if (!DropPodSplineMath.IsFinite(position))
                position = Vector3.zero;

            _pendingFeedbackPosition = position;
            _pendingFeedbackEventId = eventId;
            _pendingFeedbackMotorMask = motorMask;
            _feedbackPending = true;
        }

        private void QueueFeedbackIfLateRouteAvailable(Vector3 position, uint eventId, byte motorMask)
        {
            QueueFeedback(position, eventId, motorMask);
            if (!TryRegisterLate())
                ClearPendingFeedback();
        }

        private void ClearPendingFeedback()
        {
            _feedbackPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
        }

        private void DispatchPendingFeedback()
        {
            if (!_feedbackPending)
                return;

            Vector3 position = _pendingFeedbackPosition;
            uint eventId = _pendingFeedbackEventId;
            byte motorMask = _pendingFeedbackMotorMask;
            _feedbackPending = false;
            _pendingFeedbackEventId = 0u;
            _pendingFeedbackMotorMask = 0;
            if (motorMask != 0)
                EnqueueTransitHaptic(motorMask);
            QueueAudio(position, eventId);
        }

        private void EnqueueTransitHaptic(byte motorMask)
        {
            float quality = DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01);
            float low = 0.18f * math.lerp(0.55f, 1.18f, quality);
            float high = 0.54f * math.lerp(0.5f, 1.22f, quality);
            float duration = 0.12f * math.lerp(0.75f, 1.35f, quality);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                math.saturate(low),
                math.saturate(high),
                duration,
                34f,
                HapticPriority,
                motorMask);
        }

        private void QueueAudio(Vector3 position, uint eventId)
        {
            IAudioService audio = ResolveAudioService();
            if (!emitAudio || eventId == 0u || audio == null || !DropPodSplineMath.IsFinite(position))
                return;

            CoreAudioEvent audioEvent = new CoreAudioEvent(
                eventId,
                position,
                DropPodSplineMath.SanitizeRange(audioVolume, 0f, 1f, 0f),
                DropPodSplineMath.SanitizeRange(audioPitch, 0.25f, 2.5f, 1f));
            audio.QueueAudioEvent(in audioEvent);
        }

        private static float ResolveTransitDuration(float seconds)
        {
            return DropPodSplineMath.SanitizeRange(seconds, MinTransitSeconds, MaxTransitSeconds, FallbackTransitSeconds);
        }

        private bool TryRegisterTicks()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

            bool fixedReady = true;
            if (_seatLockMotor != null && _seatLockMotor.HasControllableBody && !_registeredFixed)
            {
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
                fixedReady = _registeredFixed;
            }

            if (fixedReady && TryRegisterLate())
                return true;

            UnregisterTicks();
            return false;
        }

        private void RefreshSeatLockMotorRegistration()
        {
            if (!_transiting || !isActiveAndEnabled)
            {
                if (_seatLockMotor == null || !_seatLockMotor.HasControllableBody)
                    UnregisterFixed();
                return;
            }

            if (_seatLockMotor != null && _seatLockMotor.HasControllableBody)
                TryRegisterTicks();
            else
                UnregisterFixed();
        }

        private bool TryRegisterLate()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;
            if (_registeredLate)
                return true;

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
            if (airlock == null)
                airlock = ComponentReferenceUtility.ResolveOwnedComponent<DropPodAirlockController>(_cachedTransform);

            _playerRuntimeContext = GlobalRegistry.Player;
            _playerCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
            _cameraTransform = _playerCamera != null ? _playerCamera.transform : null;
            _seatLockMotor = GlobalRegistry.PlayerSeatLockMotor;
            CacheAudioService(GlobalRegistry.Audio);
            CacheInputBlockServiceCold();
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
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheInputBlockServiceCold()
        {
            _inputBlockService = GlobalRegistry.Input;
            if (_inputBlockService == null)
                _inputBlockService = InputDispatcher.ActiveRuntimeInstance;
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
