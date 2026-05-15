namespace Hecton8.Interaction
{
    using Hecton8.Core;
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
    public sealed class PhysicalSnapSwitch : MonoBehaviour, IUpdatable, IPhysicalPanelButtonReceiver
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

        private Quaternion _baseLocalRotation;
        private Quaternion _offLocalRotation;
        private Quaternion _onLocalRotation;
        private Transform _cachedTransform;
        private float _currentAngle;
        private float _targetAngle;
        private float _snapCooldownRemaining;
        private bool _isOn;
        private bool _registered;
        private bool _receiverRegistered;
        private int _lastSampleFrame = -1;
        private Collider _registeredActivationVolume;

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
            if (activationVolume == null || !IsFiniteVector(handPosition))
                return false;

            int currentFrame = Time.frameCount;
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

            _isOn = desiredOn;
            _targetAngle = _isOn ? ResolveSafeOnAngleDegrees() : ResolveSafeOffAngleDegrees();
            _snapCooldownRemaining = ResolveSafeSnapCooldownSeconds();
            TryRegister();
            PublishSwitchSignal(handPosition, handForward, interactionSignals, resolvedSampleFrame);
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
            _isOn = initialOn;
            _currentAngle = _isOn ? ResolveSafeOnAngleDegrees() : ResolveSafeOffAngleDegrees();
            _targetAngle = _currentAngle;
            if (leverTransform != null)
                _baseLocalRotation = leverTransform.localRotation;
            CacheSnapRotations();
            ApplyAngle(_currentAngle);
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterCollider();
            RefreshTickRegistration();
        }

        private void OnDisable()
        {
            Unregister();
            UnregisterCollider();
            _snapCooldownRemaining = 0f;
            _lastSampleFrame = -1;
        }

        private void OnDestroy()
        {
            UnregisterCollider();
        }

        public void Tick(float dt)
        {
            float safeDeltaTime = SanitizeDeltaTime(dt);
            if (_snapCooldownRemaining > 0f)
                _snapCooldownRemaining = math.max(0f, _snapCooldownRemaining - safeDeltaTime);

            if (safeDeltaTime <= 0f)
                return;

            if (math.abs(_targetAngle - _currentAngle) < 0.001f)
            {
                _currentAngle = _targetAngle;
                ApplyAngle(_currentAngle);
                if (_snapCooldownRemaining <= 0f)
                    Unregister();
                return;
            }

            float alpha = FastDecayBlend(ResolveSafeSnapSpeed(), safeDeltaTime);
            _currentAngle = math.lerp(_currentAngle, _targetAngle, alpha);
            ApplyAngle(_currentAngle);
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
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void RefreshTickRegistration()
        {
            if (math.abs(_targetAngle - _currentAngle) >= 0.001f || _snapCooldownRemaining > 0f)
                TryRegister();
            else
                Unregister();
        }

        private void Unregister()
        {
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
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

            float offAngle = ResolveSafeOffAngleDegrees();
            float onAngle = ResolveSafeOnAngleDegrees();
            float span = onAngle - offAngle;
            float blend = math.abs(span) > MinimumAngleSpanDegrees
                ? math.saturate((angleDegrees - offAngle) * math.rcp(span))
                : (_isOn ? 1f : 0f);
            leverTransform.localRotation = ApproximateNlerpNoSqrt(_offLocalRotation, _onLocalRotation, blend);
        }

        private void CacheSnapRotations()
        {
            Vector3 axis = ResolveAxisVector();
            _offLocalRotation = _baseLocalRotation * ApproximateAxisRotationNoTrig(axis, ResolveSafeOffAngleDegrees() * RadiansPerDegree);
            _onLocalRotation = _baseLocalRotation * ApproximateAxisRotationNoTrig(axis, ResolveSafeOnAngleDegrees() * RadiansPerDegree);
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

        private float ResolveSafeOffAngleDegrees()
        {
            return math.isfinite(offAngleDegrees) ? math.clamp(offAngleDegrees, -90f, 90f) : -28f;
        }

        private float ResolveSafeOnAngleDegrees()
        {
            return math.isfinite(onAngleDegrees) ? math.clamp(onAngleDegrees, -90f, 90f) : 28f;
        }

        private float ResolveSafeSnapSpeed()
        {
            return math.isfinite(snapSpeed) ? math.clamp(snapSpeed, 4f, 80f) : 36f;
        }

        private float ResolveSafeSnapCooldownSeconds()
        {
            return math.isfinite(snapCooldownSeconds)
                ? math.clamp(snapCooldownSeconds, MinimumSnapCooldownSeconds, MaximumSnapCooldownSeconds)
                : 0.08f;
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

        private void PublishSwitchSignal(Vector3 handPosition, Vector3 handForward, IInteractionSignalService interactionSignals, int sampleFrame)
        {
            if (interactionSignals == null || !interactionSignals.IsInitialized || !IsFiniteVector(handPosition))
                return;

            double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(handPosition);
            Vector3 fallbackForward = _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;
            if (!IsFiniteVector(fallbackForward))
                fallbackForward = Vector3.forward;

            Vector3 safeDirection = NormalizeVectorApproxNoSqrt(handForward, fallbackForward);
            if (!math.all(math.isfinite(absoluteHitPoint)) || !IsFiniteVector(safeDirection))
                return;

            float signalRange = math.abs(ResolveSafeOnAngleDegrees() - ResolveSafeOffAngleDegrees());
            InteractionPacket packet = new InteractionPacket(
                PhysicalSwitchToolId,
                new float3((float)absoluteHitPoint.x, (float)absoluteHitPoint.y, (float)absoluteHitPoint.z),
                (float3)safeDirection,
                _isOn ? 1f : 0.5f,
                signalRange,
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)sampleFrame));
            InteractionSignal signal = new InteractionSignal(
                packet,
                0,
                new float3((float)absoluteHitPoint.x, (float)absoluteHitPoint.y, (float)absoluteHitPoint.z),
                (float3)(-safeDirection),
                _isOn ? 1f : 0.5f,
                (byte)InteractionEffectType.Drill,
                0);

            interactionSignals.Publish(in signal, activationVolume);
        }

        private void EnqueueClickHaptic(Collider handSourceCollider, PhysicalHandSide fallbackHandSide)
        {
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                0.16f,
                0.42f,
                0.045f,
                92f,
                CriticalPriority,
                ResolveHapticMotorMask(handSourceCollider, fallbackHandSide));
        }

        private void QueueSnapAudio(Vector3 handPosition)
        {
            IAudioService audio = GlobalRegistry.Audio;
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

            CacheSnapRotations();
        }
#endif
    }
}
