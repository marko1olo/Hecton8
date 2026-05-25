using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/VR Valve Wheel Handle")]
    public sealed class VRValveWheelHandle : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float RadiansPerDegree = 0.0174532924f;
        private const float HalfPi = 1.57079637f;
        private const float Pi = 3.14159274f;
        private const float TwoPi = 6.28318548f;
        private const float DegreesPerRadian = 57.29578f;
        private const float DefaultValveSampleDeltaSeconds = 0.02f;
        private const float MaxValveDeltaSeconds = 0.05f;
        private const float MaxMomentumDegreesPerSecond = 1080f;
        private const float MaxDegreesToOpen = 1440f;
        private const float MaxAngularDragPerSecond = 60f;
        private const float MaxMinimumMomentumDegreesPerSecond = 180f;

        [Header("Valve")]
        [SerializeField] private Transform wheelVisual;
        [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
        [SerializeField, Min(1f)] private float degreesToOpen = 360f;
        [SerializeField, Range(0f, 1f)] private float initialOpen01;
        [SerializeField, Range(1f, 180f)] private float maxAcceptedSampleDeltaDegrees = 80f;
        [SerializeField, Min(0f)] private float angularDragPerSecond = 7.5f;
        [SerializeField, Min(0f)] private float minimumMomentumDegreesPerSecond = 2f;

        private Quaternion _closedLocalRotation;
        private Transform _cachedTransform;
        private Transform _resolvedVisual;
        private Vector3 _resolvedLocalAxis = Vector3.forward;
        private Vector3 _cachedPivotWorldPosition;
        private Vector3 _cachedWorldAxis = Vector3.forward;
        private Vector3 _previousPlaneVector;
        private float _accumulatedDegrees;
        private float _angularVelocityDegreesPerSecond;
        private float _isOpen01;
        private float _resolvedDegreesToOpen = 360f;
        private float _resolvedMaxAcceptedSampleDeltaDegrees = 80f;
        private float _resolvedAngularDragPerSecond = 7.5f;
        private float _resolvedMinimumMomentumDegreesPerSecond = 2f;
        private bool _grabbed;
        private bool _hasPreviousVector;
        private bool _grabPoseCached;
        private bool _registeredMomentumTick;
        private bool _momentumTickDormant;
        private bool _registeredHotSwap;

        public float IsOpen01 => _isOpen01;
        public bool IsGrabbed => _grabbed;
        public float AngularVelocityDegreesPerSecond => _angularVelocityDegreesPerSecond;

        private void Awake()
        {
            EnsureReferences();
            RefreshCachedLocalAxis();
            CacheScalarConfig();
            _closedLocalRotation = IsFiniteQuaternion(_resolvedVisual.localRotation)
                ? _resolvedVisual.localRotation
                : Quaternion.identity;
            SetOpen01Direct(initialOpen01);
        }

        private void OnEnable()
        {
            EnsureReferences();
            RefreshCachedLocalAxis();
            CacheScalarConfig();
            SanitizeRuntimeOpenState();
            TryRegisterHotSwapListener();
            ApplyWheelVisual();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool shouldRestoreTick = (_registeredMomentumTick && !_momentumTickDormant) || ShouldRunMomentumTick();
            TryUnregisterMomentumTick();
            if (shouldRestoreTick && currentService != null && isActiveAndEnabled)
                TryRegisterMomentumTick();
        }

        public void BeginGrab(Vector3 controllerWorldPosition)
        {
            if (!IsFiniteVector(controllerWorldPosition))
                return;

            _grabbed = true;
            _angularVelocityDegreesPerSecond = 0f;
            TryUnregisterMomentumTick();
            CacheScalarConfig();
            CacheGrabPose();
            _hasPreviousVector = TryProjectControllerVector(controllerWorldPosition, out _previousPlaneVector);
        }

        public bool SampleControllerPose(Vector3 controllerWorldPosition)
        {
            return SampleControllerPose(controllerWorldPosition, DefaultValveSampleDeltaSeconds);
        }

        public bool SampleControllerPose(Vector3 controllerWorldPosition, float sampleDeltaSeconds)
        {
            if (!IsFiniteVector(controllerWorldPosition))
                return false;

            if (!_grabbed)
                BeginGrab(controllerWorldPosition);

            if (!TryProjectControllerVector(controllerWorldPosition, out Vector3 currentPlaneVector))
                return false;

            if (!_hasPreviousVector)
            {
                _previousPlaneVector = currentPlaneVector;
                _hasPreviousVector = true;
                return false;
            }

            Vector3 axisWorld = _cachedWorldAxis;
            float deltaDegrees = ResolveSignedDeltaDegrees(_previousPlaneVector, currentPlaneVector, axisWorld);
            _previousPlaneVector = currentPlaneVector;

            if (!math.isfinite(deltaDegrees) || math.abs(deltaDegrees) > _resolvedMaxAcceptedSampleDeltaDegrees)
                return false;

            float sampleDeltaTime = SanitizeDeltaSeconds(sampleDeltaSeconds, 0.0001f);
            float rawAngularVelocity = deltaDegrees / sampleDeltaTime;
            _angularVelocityDegreesPerSecond = math.clamp(
                rawAngularVelocity,
                -MaxMomentumDegreesPerSecond,
                MaxMomentumDegreesPerSecond);
            ApplyControllerAngularDeltaDegrees(deltaDegrees);
            return true;
        }

        public void EndGrab()
        {
            _grabbed = false;
            _hasPreviousVector = false;
            _grabPoseCached = false;
            if (math.abs(_angularVelocityDegreesPerSecond) > _resolvedMinimumMomentumDegreesPerSecond)
                TryRegisterMomentumTick();
        }

        public void ApplyControllerAngularDeltaDegrees(float deltaDegrees)
        {
            if (!math.isfinite(deltaDegrees))
                return;

            float currentDegrees = math.isfinite(_accumulatedDegrees) ? _accumulatedDegrees : 0f;
            _accumulatedDegrees = math.clamp(currentDegrees + deltaDegrees, 0f, _resolvedDegreesToOpen);
            _isOpen01 = math.saturate(_accumulatedDegrees / _resolvedDegreesToOpen);
            ApplyWheelVisual();
        }

        public void Tick(float deltaTime)
        {
            if (_momentumTickDormant)
                return;

            if (_grabbed)
                return;

            float safeDeltaTime = SanitizeDeltaSeconds(deltaTime, 0f);
            if (!math.isfinite(_angularVelocityDegreesPerSecond))
                _angularVelocityDegreesPerSecond = 0f;

            float speed = math.abs(_angularVelocityDegreesPerSecond);
            if (speed <= _resolvedMinimumMomentumDegreesPerSecond || safeDeltaTime <= 0f)
            {
                _angularVelocityDegreesPerSecond = 0f;
                _momentumTickDormant = true;
                return;
            }

            float before = _accumulatedDegrees;
            ApplyControllerAngularDeltaDegrees(_angularVelocityDegreesPerSecond * safeDeltaTime);
            if (math.abs(_accumulatedDegrees - before) <= 0.0001f &&
                (_accumulatedDegrees <= 0f || _accumulatedDegrees >= _resolvedDegreesToOpen))
            {
                _angularVelocityDegreesPerSecond = 0f;
                _momentumTickDormant = true;
                return;
            }

            float dragScalar = math.max(0f, 1f - _resolvedAngularDragPerSecond * safeDeltaTime);
            _angularVelocityDegreesPerSecond *= dragScalar;
        }

        public void SetOpen01Direct(float open01)
        {
            _isOpen01 = math.isfinite(open01) ? math.saturate(open01) : 0f;
            _accumulatedDegrees = _isOpen01 * _resolvedDegreesToOpen;
            _angularVelocityDegreesPerSecond = 0f;
            if (!_grabbed)
                TryUnregisterMomentumTick();
            ApplyWheelVisual();
        }

        private bool TryProjectControllerVector(Vector3 controllerWorldPosition, out Vector3 planeVector)
        {
            if (!_grabPoseCached)
                CacheGrabPose();

            Vector3 axisWorld = _cachedWorldAxis;
            Vector3 toController = controllerWorldPosition - _cachedPivotWorldPosition;
            float axisDot = toController.x * axisWorld.x + toController.y * axisWorld.y + toController.z * axisWorld.z;
            planeVector = toController - axisWorld * axisDot;
            float lengthSq = planeVector.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(planeVector.x, planeVector.y, planeVector.z))))
            {
                planeVector = Vector3.zero;
                return false;
            }

            planeVector *= ApproximateInverseMagnitudeNoSqrt(planeVector);
            return true;
        }

        private static float ResolveSignedDeltaDegrees(Vector3 previous, Vector3 current, Vector3 axisWorld)
        {
            float3 prev = new float3(previous.x, previous.y, previous.z);
            float3 curr = new float3(current.x, current.y, current.z);
            float3 axis = new float3(axisWorld.x, axisWorld.y, axisWorld.z);
            float sin = math.dot(axis, math.cross(prev, curr));
            float cos = math.clamp(math.dot(prev, curr), -1f, 1f);
            return ApproximateAtan2NoTrig(sin, cos) * DegreesPerRadian;
        }

        private void ApplyWheelVisual()
        {
            EnsureReferences();
            _resolvedVisual.localRotation = _closedLocalRotation * ApproximateAxisRotationNoTrig(_resolvedLocalAxis, _accumulatedDegrees * RadiansPerDegree);
        }

        private void EnsureReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            Transform expectedVisual = wheelVisual != null ? wheelVisual : _cachedTransform;
            if (_resolvedVisual == null || !ReferenceEquals(_resolvedVisual, expectedVisual))
                _resolvedVisual = expectedVisual;
        }

        private void RefreshCachedLocalAxis()
        {
            _resolvedLocalAxis = ResolveLocalAxis();
            _grabPoseCached = false;
        }

        private Vector3 ResolveLocalAxis()
        {
            float lengthSq = localRotationAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(localRotationAxis.x, localRotationAxis.y, localRotationAxis.z))))
                return Vector3.forward;

            return localRotationAxis * ApproximateInverseMagnitudeNoSqrt(localRotationAxis);
        }

        private void CacheGrabPose()
        {
            EnsureReferences();
            _cachedPivotWorldPosition = _cachedTransform.position;
            if (!IsFiniteVector(_cachedPivotWorldPosition))
                _cachedPivotWorldPosition = Vector3.zero;

            _cachedWorldAxis = NormalizeVectorApproxNoSqrt(
                _cachedTransform.TransformDirection(_resolvedLocalAxis),
                Vector3.forward);
            _grabPoseCached = true;
        }

        private static Quaternion ApproximateAxisRotationNoTrig(Vector3 normalizedAxis, float radians)
        {
            ApproximateSinCosFullNoTrig(radians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                normalizedAxis.x * sinHalf,
                normalizedAxis.y * sinHalf,
                normalizedAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternionNoSqrt(rotation);
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

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
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

        private static float ApproximateAtan2NoTrig(float y, float x)
        {
            if (!math.isfinite(y) || !math.isfinite(x))
                return 0f;

            float absY = math.abs(y) + 0.0000001f;
            float angle;
            if (x >= 0f)
            {
                float ratio = (x - absY) / math.max(x + absY, 0.0000001f);
                angle = (Pi * 0.25f) - ((Pi * 0.25f) * ratio);
            }
            else
            {
                float ratio = (x + absY) / math.max(absY - x, 0.0000001f);
                angle = (Pi * 0.75f) - ((Pi * 0.25f) * ratio);
            }

            return y < 0f ? -angle : angle;
        }

        private static float SanitizeDeltaSeconds(float value, float minimum)
        {
            float resolved = math.isfinite(value) ? value : minimum;
            return math.clamp(resolved, minimum, MaxValveDeltaSeconds);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > 0.000001f;
        }

        private void CacheScalarConfig()
        {
            _resolvedDegreesToOpen = math.isfinite(degreesToOpen)
                ? math.clamp(degreesToOpen, 1f, MaxDegreesToOpen)
                : 360f;
            _resolvedMaxAcceptedSampleDeltaDegrees = math.isfinite(maxAcceptedSampleDeltaDegrees)
                ? math.clamp(maxAcceptedSampleDeltaDegrees, 1f, 180f)
                : 80f;
            _resolvedAngularDragPerSecond = math.isfinite(angularDragPerSecond)
                ? math.clamp(angularDragPerSecond, 0f, MaxAngularDragPerSecond)
                : 7.5f;
            _resolvedMinimumMomentumDegreesPerSecond = math.isfinite(minimumMomentumDegreesPerSecond)
                ? math.clamp(minimumMomentumDegreesPerSecond, 0f, MaxMinimumMomentumDegreesPerSecond)
                : 2f;
        }

        private void SanitizeRuntimeOpenState()
        {
            float safeInitialOpen01 = math.isfinite(initialOpen01) ? math.saturate(initialOpen01) : 0f;
            if (!math.isfinite(_accumulatedDegrees))
                _accumulatedDegrees = safeInitialOpen01 * _resolvedDegreesToOpen;

            _accumulatedDegrees = math.clamp(_accumulatedDegrees, 0f, _resolvedDegreesToOpen);
            _isOpen01 = math.saturate(_accumulatedDegrees / _resolvedDegreesToOpen);
            if (!math.isfinite(_angularVelocityDegreesPerSecond))
                _angularVelocityDegreesPerSecond = 0f;

            _angularVelocityDegreesPerSecond = math.clamp(
                _angularVelocityDegreesPerSecond,
                -MaxMomentumDegreesPerSecond,
                MaxMomentumDegreesPerSecond);
        }

        private void TryRegisterMomentumTick()
        {
            if (_registeredMomentumTick || !Application.isPlaying)
                return;

            _registeredMomentumTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (_registeredMomentumTick)
                _momentumTickDormant = false;
        }

        private void TryUnregisterMomentumTick()
        {
            if (!_registeredMomentumTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredMomentumTick = false;
            _momentumTickDormant = false;
        }

        private bool ShouldRunMomentumTick()
        {
            return !_grabbed
                && !_momentumTickDormant
                && math.abs(_angularVelocityDegreesPerSecond) > _resolvedMinimumMomentumDegreesPerSecond;
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

        private void OnDisable()
        {
            _grabbed = false;
            _hasPreviousVector = false;
            _grabPoseCached = false;
            _previousPlaneVector = Vector3.zero;
            _angularVelocityDegreesPerSecond = 0f;
            TryUnregisterMomentumTick();
            TryUnregisterHotSwapListener();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(degreesToOpen) || degreesToOpen < 1f)
                degreesToOpen = 1f;
            degreesToOpen = math.min(degreesToOpen, MaxDegreesToOpen);
            if (!math.isfinite(maxAcceptedSampleDeltaDegrees) || maxAcceptedSampleDeltaDegrees < 1f)
                maxAcceptedSampleDeltaDegrees = 1f;
            maxAcceptedSampleDeltaDegrees = math.min(maxAcceptedSampleDeltaDegrees, 180f);
            if (!math.isfinite(angularDragPerSecond) || angularDragPerSecond < 0f)
                angularDragPerSecond = 0f;
            angularDragPerSecond = math.min(angularDragPerSecond, MaxAngularDragPerSecond);
            if (!math.isfinite(minimumMomentumDegreesPerSecond) || minimumMomentumDegreesPerSecond < 0f)
                minimumMomentumDegreesPerSecond = 0f;
            minimumMomentumDegreesPerSecond = math.min(minimumMomentumDegreesPerSecond, MaxMinimumMomentumDegreesPerSecond);
            CacheScalarConfig();
            EnsureReferences();
            RefreshCachedLocalAxis();
        }
#endif
    }
}
