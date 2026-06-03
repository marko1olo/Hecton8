namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Collider-driven cockpit toggle that snaps to authored angles and emits a short haptic click.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Hecton8/Interaction/Physical Snap Switch")]
    public sealed class PhysicalSnapSwitch : MonoBehaviour, IUpdatable, ILateFrameTickable, IPhysicalPanelButtonReceiver, IGlobalRegistryHotSwapListener
    {
        private const uint PhysicalSwitchToolId = 0x53574954u;
        private const float RadiansPerDegree = 0.0174532924f;
        private const float HalfPi = 1.57079637f;
        private const float Pi = 3.14159274f;
        private const float TwoPi = 6.28318548f;
        private const float InvTwoPi = 0.159154943f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte CriticalPriority = 3;
        private const float MaximumSwitchDeltaTime = 0.05f;
        private const float MinimumSnapCooldownSeconds = 0.02f;
        private const float MaximumSnapCooldownSeconds = 0.5f;
        private const float MinimumAngleSpanDegrees = 0.0001f;
        private const uint BallastSwitchSourceHash = 0x42535731u;
        private const uint MaintenanceSwitchSourceHash = 0x56534d31u;
        private const float BallastLeverSubmitDeadband01 = 0.001f;
        private const int BallastSubmitRetryFrameInterval = 16;

        private enum SnapAxis : byte
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        [Header("References")]
        [SerializeField] private BoxCollider activationVolume;
        [SerializeField] private Transform leverTransform;

        [Header("Snap")]
        [SerializeField] private SnapAxis snapAxis = SnapAxis.X;
        [SerializeField, Range(-90f, 90f)] private float offAngleDegrees = -28f;
        [SerializeField, Range(-90f, 90f)] private float onAngleDegrees = 28f;
        [SerializeField, Range(4f, 80f)] private float snapSpeed = 36f;
        [SerializeField, Range(0.02f, 0.5f)] private float snapCooldownSeconds = 0.08f;
        [SerializeField] private bool initialOn;

        [Header("Haptics")]
        [SerializeField, Tooltip("Optional layers treated as left-hand finger sources before falling back to the authored hand side.")]
        private LayerMask leftHandSourceLayers;
        [SerializeField, Tooltip("Optional layers treated as right-hand finger sources before falling back to the authored hand side.")]
        private LayerMask rightHandSourceLayers;

        [Header("Diegetic Audio")]
        [SerializeField, Tooltip("Routes switch snaps into the central NativeQueue-backed audio drain when an event id is authored.")]
        private bool emitSnapAudio = true;
        [SerializeField, Tooltip("One-based authored audio event id for mechanical switch clicks. Zero disables audio.")]
        private uint snapAudioEventId;
        [SerializeField, Range(0f, 1f), Tooltip("Linear volume for mechanical switch clicks.")]
        private float snapAudioVolume = 0.32f;
        [SerializeField, Range(0.25f, 2.5f), Tooltip("Pitch for mechanical switch clicks.")]
        private float snapAudioPitch = 1f;

        [Header("Vessel Ballast")]
        [SerializeField, Tooltip("When enabled, this authored cockpit switch writes its lever travel into the submarine ballast telemetry row.")]
        private bool emitBallastLeverCommand;
        [SerializeField, Tooltip("Optional explicit submarine core. If empty, the cold GlobalRegistry submarine service is used.")]
        private SubmarineCoreDirector submarineCore;
        [SerializeField, Range(0f, 90f), Tooltip("Ballast lever angle submitted when the switch is at the off angle.")]
        private float ballastOffLeverAngleDegrees;
        [SerializeField, Range(0f, 90f), Tooltip("Ballast lever angle submitted when the switch is at the on angle.")]
        private float ballastOnLeverAngleDegrees = 90f;

        [Header("Vessel Maintenance")]
        [SerializeField, Tooltip("When enabled, each successful physical snap records a vessel-care action into the unmanaged telemetry row.")]
        private bool recordVesselMaintenanceAction;
        [SerializeField, Range(0, 63), Tooltip("Panel/circuit bit set in VesselTelemetryEntry.HullCleanlinessMask when this switch is serviced.")]
        private int vesselMaintenancePanelBitIndex = 4;

        private Quaternion _baseLocalRotation;
        private Quaternion _offLocalRotation;
        private Quaternion _onLocalRotation;
        private Transform _cachedTransform;
        private float _currentAngle;
        private float _targetAngle;
        private float _snapCooldownRemaining;
        private float _resolvedOffAngleDegrees = -28f;
        private float _resolvedOnAngleDegrees = 28f;
        private float _resolvedSnapSpeed = 36f;
        private float _resolvedSnapCooldownSeconds = 0.08f;
        private float _resolvedSignalRangeDegrees = 56f;
        private bool _isOn;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _tickDormant;
        private bool _receiverRegistered;
        private bool _dispatcherAvailable;
        private int _lastSampleFrame = -1;
        private Collider _registeredActivationVolume;
        private IAudioService _audioService;
        private SubmarineCoreDirector _submarineCore;
        private float _pendingVisualAngle;
        private float _lastSubmittedBallastRatio = -1f;
        private bool _hasPendingVisualAngle;
        private bool _ballastSubmitPending;
        private byte _hasSubmittedBallastRatio;
        private int _nextBallastSubmitRetryFrame;

        public bool IsOn => _isOn;
        public Collider ActivationCollider => activationVolume;

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            if (activationVolume == null || interactionSignals == null || !interactionSignals.IsInitialized || !IsFiniteVector(handPosition))
                return false;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            int resolvedSampleFrame = sampleFrame >= 0 ? sampleFrame : currentFrame;
            if (resolvedSampleFrame > currentFrame || resolvedSampleFrame < _lastSampleFrame)
                return false;

            Vector3 localPoint = activationVolume.transform.InverseTransformPoint(handPosition) - activationVolume.center;
            if (!IsFiniteVector(localPoint))
                return false;

            _lastSampleFrame = resolvedSampleFrame;
            bool desiredOn = ResolveDesiredState(localPoint);
            if (desiredOn == _isOn || _snapCooldownRemaining > 0f)
                return true;

            if (!PublishSwitchSignal(handPosition, handForward, interactionSignals, resolvedSampleFrame, desiredOn))
                return false;

            _isOn = desiredOn;
            _targetAngle = _isOn ? _resolvedOnAngleDegrees : _resolvedOffAngleDegrees;
            _snapCooldownRemaining = _resolvedSnapCooldownSeconds;
            QueueBallastSubmit();
            TryRecordVesselMaintenanceSnap();
            TryRegister();
            EnqueueClickHaptic(handSourceCollider, fallbackHandSide);
            QueueSnapAudio(handPosition);
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

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveReferences();
            RefreshColdRegistryReferences();
            _isOn = initialOn;
            _currentAngle = _isOn ? _resolvedOnAngleDegrees : _resolvedOffAngleDegrees;
            _targetAngle = _currentAngle;
            ApplyAngle(_currentAngle);
            QueueBallastSubmit();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            RegisterCollider();
            QueueBallastSubmit();
            RefreshTickRegistration();
        }

        private void OnDisable()
        {
            Unregister();
            TryUnregisterHotSwapListener();
            UnregisterCollider();
            _snapCooldownRemaining = 0f;
            _lastSampleFrame = -1;
            _ballastSubmitPending = false;
            _nextBallastSubmitRetryFrame = 0;
        }

        private void OnDestroy()
        {
            Unregister();
            TryUnregisterHotSwapListener();
            UnregisterCollider();
        }

        public void Tick(float dt)
        {
            if (_tickDormant)
            {
                if (_ballastSubmitPending)
                    TrySubmitBallastLeverAngleIfChanged();

                TryRetireDormantTickRegistration();
                return;
            }

            float safeDeltaTime = SanitizeDeltaTime(dt);
            if (_snapCooldownRemaining > 0f)
                _snapCooldownRemaining = math.max(0f, _snapCooldownRemaining - safeDeltaTime);

            if (safeDeltaTime <= 0f)
                return;

            if (math.abs(_targetAngle - _currentAngle) < 0.001f)
            {
                _currentAngle = _targetAngle;
                QueueAngle(_currentAngle);
                TryRegisterLateFrameTick();
                TrySubmitBallastLeverAngleIfChanged();
                if (_snapCooldownRemaining <= 0f && !_ballastSubmitPending)
                    _tickDormant = true;
                return;
            }

            float alpha = FastDecayBlend(_resolvedSnapSpeed, safeDeltaTime);
            _currentAngle = math.lerp(_currentAngle, _targetAngle, alpha);
            QueueAngle(_currentAngle);
            TryRegisterLateFrameTick();
            TrySubmitBallastLeverAngleIfChanged();
        }

        public void LateFrameTick()
        {
            if (_hasPendingVisualAngle)
            {
                _hasPendingVisualAngle = false;
                ApplyAngle(_pendingVisualAngle);
            }

            TryRetireDormantTickRegistration();
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float safeSpeed = math.isfinite(speed) ? math.max(0f, speed) : 0f;
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            if (safeSpeed <= 0f || safeDeltaTime <= 0f)
                return 0f;

            float x = math.min(safeSpeed * safeDeltaTime, 3f);
            float x2 = x * x;
            return math.saturate(1f - math.rcp(1f + x + (0.5f * x2)));
        }

        private void ResolveReferences()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (leverTransform == null)
                leverTransform = transform;
            if (activationVolume != null)
                activationVolume.isTrigger = true;
            if (_baseLocalRotation == default && leverTransform != null)
                _baseLocalRotation = leverTransform.localRotation;
            CacheScalarConfig();
            CacheSnapRotations();
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

        private void TryRegister()
        {
            if (!Application.isPlaying || !_dispatcherAvailable)
                return;

            TryRegisterUpdateTick();
            TryRegisterLateFrameTick();
        }

        private void TryRegisterUpdateTick()
        {
            if (_registered || !Application.isPlaying || !_dispatcherAvailable)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (_registered)
                _tickDormant = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrame || !Application.isPlaying || !_dispatcherAvailable)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryRetireDormantTickRegistration()
        {
            if (!_tickDormant)
                return;

            if (_ballastSubmitPending && !TrySubmitBallastLeverAngleIfChanged())
                return;

            if (_hasPendingVisualAngle)
            {
                TryRegisterLateFrameTick();
                return;
            }

            Unregister();
        }

        private void RefreshTickRegistration()
        {
            if (math.abs(_targetAngle - _currentAngle) >= 0.001f || _snapCooldownRemaining > 0f || _ballastSubmitPending)
                TryRegister();
            else
                Unregister();
        }

        private void Unregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            _hasPendingVisualAngle = false;
            _tickDormant = false;
        }

        private void RefreshColdRegistryReferences()
        {
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            _audioService = GlobalRegistry.Audio;
            _submarineCore = ResolveSubmarineCore(GlobalRegistry.Submarine);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineCore = ResolveSubmarineCore(currentService);
                    _nextBallastSubmitRetryFrame = 0;
                    QueueBallastSubmit();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcherAvailable = currentService != null;
                    _registered = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                        RefreshTickRegistration();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private SubmarineCoreDirector ResolveSubmarineCore(object submarineService)
        {
            return submarineCore != null ? submarineCore : submarineService as SubmarineCoreDirector;
        }

        private void TryRecordVesselMaintenanceSnap()
        {
            if (!recordVesselMaintenanceAction)
                return;

            SubmarineCoreDirector core = _submarineCore;
            if (core == null)
            {
                RefreshColdRegistryReferences();
                core = _submarineCore;
                if (core == null)
                    return;
            }

            uint panelBit = (uint)math.clamp(vesselMaintenancePanelBitIndex, 0, 63);
            core.TryRecordVesselMaintenanceAction(panelBit, MaintenanceSwitchSourceHash);
        }

        private bool ResolveDesiredState(Vector3 localPoint)
        {
            switch (snapAxis)
            {
                case SnapAxis.Y:
                    return localPoint.y >= 0f;
                case SnapAxis.Z:
                    return localPoint.z >= 0f;
                default:
                    return localPoint.x >= 0f;
            }
        }

        private void ApplyAngle(float angleDegrees)
        {
            if (leverTransform == null)
                return;

            float offAngle = _resolvedOffAngleDegrees;
            float onAngle = _resolvedOnAngleDegrees;
            float span = onAngle - offAngle;
            float blend = math.abs(span) > MinimumAngleSpanDegrees
                ? math.saturate((angleDegrees - offAngle) * math.rcp(span))
                : (_isOn ? 1f : 0f);
            leverTransform.localRotation = ApproximateNlerpNoSqrt(_offLocalRotation, _onLocalRotation, blend);
        }

        private void QueueAngle(float angleDegrees)
        {
            _pendingVisualAngle = angleDegrees;
            _hasPendingVisualAngle = true;
        }

        private void QueueBallastSubmit()
        {
            if (!emitBallastLeverCommand)
                return;

            _ballastSubmitPending = true;
            _nextBallastSubmitRetryFrame = 0;
            TrySubmitBallastLeverAngleIfChanged();
            if (_ballastSubmitPending)
                TryRegister();
        }

        private bool TrySubmitBallastLeverAngleIfChanged()
        {
            if (!emitBallastLeverCommand)
            {
                _ballastSubmitPending = false;
                _nextBallastSubmitRetryFrame = 0;
                return true;
            }

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (_ballastSubmitPending && currentFrame < _nextBallastSubmitRetryFrame)
                return false;

            SubmarineCoreDirector core = _submarineCore;
            if (core == null)
            {
                _ballastSubmitPending = true;
                _nextBallastSubmitRetryFrame = currentFrame + BallastSubmitRetryFrameInterval;
                return false;
            }

            float travel01 = ResolveCurrentTravel01();
            float safeOffAngle = math.isfinite(ballastOffLeverAngleDegrees)
                ? math.clamp(ballastOffLeverAngleDegrees, 0f, 90f)
                : 0f;
            float safeOnAngle = math.isfinite(ballastOnLeverAngleDegrees)
                ? math.clamp(ballastOnLeverAngleDegrees, 0f, 90f)
                : 90f;
            float leverAngleDegrees = math.lerp(safeOffAngle, safeOnAngle, travel01);
            float leverRatio01 = math.saturate(leverAngleDegrees * (1f / 90f));
            if (!_ballastSubmitPending &&
                _hasSubmittedBallastRatio != 0 &&
                math.abs(leverRatio01 - _lastSubmittedBallastRatio) <= BallastLeverSubmitDeadband01)
            {
                return true;
            }

            if (!core.TrySubmitBallastLeverAngle(leverAngleDegrees, BallastSwitchSourceHash))
            {
                _ballastSubmitPending = true;
                _nextBallastSubmitRetryFrame = currentFrame + BallastSubmitRetryFrameInterval;
                return false;
            }

            _lastSubmittedBallastRatio = leverRatio01;
            _hasSubmittedBallastRatio = 1;
            _ballastSubmitPending = false;
            _nextBallastSubmitRetryFrame = 0;
            return true;
        }

        private float ResolveCurrentTravel01()
        {
            float span = _resolvedOnAngleDegrees - _resolvedOffAngleDegrees;
            if (math.abs(span) <= MinimumAngleSpanDegrees)
                return _isOn ? 1f : 0f;

            return math.saturate((_currentAngle - _resolvedOffAngleDegrees) * math.rcp(span));
        }

        private void CacheSnapRotations()
        {
            Vector3 axis = ResolveAxisVector();
            _offLocalRotation = _baseLocalRotation * ApproximateAxisRotationNoTrig(axis, _resolvedOffAngleDegrees * RadiansPerDegree);
            _onLocalRotation = _baseLocalRotation * ApproximateAxisRotationNoTrig(axis, _resolvedOnAngleDegrees * RadiansPerDegree);
        }

        private Vector3 ResolveAxisVector()
        {
            switch (snapAxis)
            {
                case SnapAxis.Y:
                    return Vector3.up;
                case SnapAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.right;
            }
        }

        private static Quaternion ApproximateAxisRotationNoTrig(Vector3 axis, float radians)
        {
            Vector3 safeAxis = NormalizeVectorApproxNoSqrt(axis, Vector3.right);
            ApproximateSinCosFullNoTrig(radians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                safeAxis.x * sinHalf,
                safeAxis.y * sinHalf,
                safeAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion from, Quaternion to, float t)
        {
            float4 fromValue = new float4(from.x, from.y, from.z, from.w);
            float4 toValue = new float4(to.x, to.y, to.z, to.w);
            toValue = math.select(toValue, -toValue, math.dot(fromValue, toValue) < 0f);
            float4 blended = math.lerp(fromValue, toValue, math.saturate(t));
            float lenSq = math.max(math.dot(blended, blended), 0.000001f);
            blended *= 1.5f - (0.5f * lenSq);
            return new Quaternion(blended.x, blended.y, blended.z, blended.w);
        }

        private static Quaternion NormalizeQuaternionNoSqrt(Quaternion value)
        {
            float4 v = new float4(value.x, value.y, value.z, value.w);
            v *= ApproximateInverseMagnitudeNoSqrt(v);
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        private static Vector3 NormalizeVectorApproxNoSqrt(Vector3 value, Vector3 fallback)
        {
            float lenSq = value.sqrMagnitude;
            if (lenSq <= 0.000001f || !math.all(math.isfinite(new float3(value.x, value.y, value.z))))
                return fallback;

            return value * ApproximateInverseMagnitudeNoSqrt(value);
        }

        private static float ApproximateInverseMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs(new float3(value.x, value.y, value.z));
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            float magnitude = largest + (middle * 0.375f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float4 value)
        {
            float4 absValue = math.abs(value);
            float largest = math.max(math.max(absValue.x, absValue.y), math.max(absValue.z, absValue.w));
            float smallest = math.min(math.min(absValue.x, absValue.y), math.min(absValue.z, absValue.w));
            float middleSum = absValue.x + absValue.y + absValue.z + absValue.w - largest - smallest;
            float magnitude = largest + (middleSum * 0.25f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static float SanitizeDeltaTime(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaximumSwitchDeltaTime) : 0f;
        }

        private void CacheScalarConfig()
        {
            _resolvedOffAngleDegrees = math.isfinite(offAngleDegrees) ? math.clamp(offAngleDegrees, -90f, 90f) : -28f;
            _resolvedOnAngleDegrees = math.isfinite(onAngleDegrees) ? math.clamp(onAngleDegrees, -90f, 90f) : 28f;
            _resolvedSnapSpeed = math.isfinite(snapSpeed) ? math.clamp(snapSpeed, 4f, 80f) : 36f;
            _resolvedSnapCooldownSeconds = math.isfinite(snapCooldownSeconds)
                ? math.clamp(snapCooldownSeconds, MinimumSnapCooldownSeconds, MaximumSnapCooldownSeconds)
                : 0.08f;
            _resolvedSignalRangeDegrees = math.abs(_resolvedOnAngleDegrees - _resolvedOffAngleDegrees);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians * InvTwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private bool PublishSwitchSignal(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            int sampleFrame,
            bool desiredOn)
        {
            if (interactionSignals == null || !interactionSignals.IsInitialized || !IsFiniteVector(handPosition))
                return false;

            if (!TryResolveRuntimeAup(handPosition, out double3 absoluteHitPoint))
                return false;

            Vector3 fallbackForward = _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;
            if (!IsFiniteVector(fallbackForward))
                fallbackForward = Vector3.forward;

            Vector3 safeDirection = NormalizeVectorApproxNoSqrt(handForward, fallbackForward);
            if (!math.all(math.isfinite(absoluteHitPoint)) || !IsFiniteVector(safeDirection))
                return false;

            float3 hitPointAup = new float3((float)absoluteHitPoint.x, (float)absoluteHitPoint.y, (float)absoluteHitPoint.z);
            InteractionPacket packet = new InteractionPacket(
                PhysicalSwitchToolId,
                hitPointAup,
                (float3)safeDirection,
                desiredOn ? 1f : 0.5f,
                _resolvedSignalRangeDegrees,
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)sampleFrame));
            InteractionSignal signal = new InteractionSignal(
                packet,
                0,
                hitPointAup,
                (float3)(-safeDirection),
                desiredOn ? 1f : 0.5f,
                (byte)InteractionEffectType.Drill,
                0,
                absoluteHitPoint,
                InteractionSignal.HitPointAupDoubleValid);

            return interactionSignals.Publish(in signal, activationVolume);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            absoluteAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private void EnqueueClickHaptic(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                0.16f,
                0.42f,
                0.045f,
                92f,
                CriticalPriority,
                ResolveHapticMotorMask(handSourceCollider, fallbackHandSide));
        }

        private void QueueSnapAudio(Vector3 handPosition)
        {
            IAudioService audio = _audioService;
            if (!emitSnapAudio || snapAudioEventId == 0u || audio == null || !audio.IsInitialized)
                return;

            Vector3 sourcePosition = leverTransform != null ? leverTransform.position : handPosition;
            if (!IsFiniteVector(sourcePosition))
                return;

            AudioEvent audioEvent = new AudioEvent(
                snapAudioEventId,
                sourcePosition,
                math.saturate(snapAudioVolume),
                math.clamp(snapAudioPitch, 0.25f, 2.5f));
            audio.QueueAudioEvent(in audioEvent);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (activationVolume == null)
                TryGetComponent(out activationVolume);
            if (activationVolume != null)
                activationVolume.isTrigger = true;

            if (!math.isfinite(offAngleDegrees))
                offAngleDegrees = -28f;
            if (!math.isfinite(onAngleDegrees))
                onAngleDegrees = 28f;
            offAngleDegrees = math.clamp(offAngleDegrees, -90f, 90f);
            onAngleDegrees = math.clamp(onAngleDegrees, -90f, 90f);
            if (!math.isfinite(snapSpeed))
                snapSpeed = 36f;
            if (!math.isfinite(snapCooldownSeconds))
                snapCooldownSeconds = 0.08f;
            snapAudioVolume = math.saturate(snapAudioVolume);
            snapAudioPitch = math.clamp(snapAudioPitch, 0.25f, 2.5f);
            ballastOffLeverAngleDegrees = math.clamp(
                math.isfinite(ballastOffLeverAngleDegrees) ? ballastOffLeverAngleDegrees : 0f,
                0f,
                90f);
            ballastOnLeverAngleDegrees = math.clamp(
                math.isfinite(ballastOnLeverAngleDegrees) ? ballastOnLeverAngleDegrees : 90f,
                0f,
                90f);
            vesselMaintenancePanelBitIndex = math.clamp(vesselMaintenancePanelBitIndex, 0, 63);

            CacheScalarConfig();
            CacheSnapRotations();
        }
#endif
    }
}
