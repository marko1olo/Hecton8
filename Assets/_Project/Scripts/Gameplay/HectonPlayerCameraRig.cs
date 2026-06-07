using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Sole owner that applies camera transform and FOV state for the player rig.
    /// Locomotion publishes desired state; the rig interpolates and applies it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hecton Player Camera Rig")]
    public sealed class HectonPlayerCameraRig : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const float MinimumBlendSharpness = 0.01f;
        private const float MinimumBlendDeltaTime = 0.0001f;
        private const float MinimumCameraFov = 1f;
        private const float MaximumCameraFov = 179f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;
        private const float MaximumLateFrameKccOffsetMeters = 0.75f;

        [Header("References")]
        [SerializeField, Tooltip("Camera transform driven by the rig.")]
        private Transform cameraTransform;

        [SerializeField, Tooltip("Optional explicit camera component. Falls back to the driven transform.")]
        private Camera cameraComponent;

        [SerializeField, Tooltip("Tracking-space root reparented under active AUP anchors for VR cockpit motion.")]
        private Transform trackingSpaceRoot;

        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _hasPendingState;
        private HectonCameraState _pendingState;
        private bool _hasLastAppliedTrackingState;
        private int _originShiftTrackingLockFrame = -1;
        private Transform _defaultTrackingSpaceParent;
        private Transform _pendingAupAnchor;
        private Transform _appliedAupAnchor;
        private Vector3 _lastAppliedLocalPosition;
        private Quaternion _lastAppliedWorldRotation = Quaternion.identity;

        /// <summary>
        /// Wires the runtime camera references used by this rig.
        /// </summary>
        public void Bind(Transform targetCameraTransform, Camera targetCameraComponent)
        {
            cameraTransform = targetCameraTransform;
            if (targetCameraComponent != null)
            {
                cameraComponent = targetCameraComponent;
            }
            else if (targetCameraTransform != null)
            {
                targetCameraTransform.TryGetComponent(out cameraComponent);
            }
            else
            {
                cameraComponent = null;
            }

            if (trackingSpaceRoot == null && targetCameraTransform != null)
                trackingSpaceRoot = targetCameraTransform.parent;
            if (trackingSpaceRoot != null && _defaultTrackingSpaceParent == null)
                _defaultTrackingSpaceParent = trackingSpaceRoot.parent;
        }

        /// <summary>
        /// Pushes the latest locomotion-owned camera target for application on the rig tick.
        /// </summary>
        public void SetLocomotionState(HectonCameraState state)
        {
            _pendingState = state;
            _hasPendingState = true;
        }

        /// <summary>
        /// Assigns the parent frame for VR tracking space. Null restores the original scene parent.
        /// </summary>
        public void SetAupAnchor(Transform aupAnchor)
        {
            _pendingAupAnchor = aupAnchor;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearPendingState();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_hasPendingState || cameraTransform == null)
                return;

            ApplyCameraState(_pendingState);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (cameraTransform != null)
            {
                _lastAppliedLocalPosition = cameraTransform.localPosition;
                _lastAppliedWorldRotation = cameraTransform.rotation;
                _hasLastAppliedTrackingState = true;
            }

            _originShiftTrackingLockFrame = SystemDispatcher.CurrentFrameIndex;
        }

        private void ApplyCameraState(in HectonCameraState state)
        {
            ApplyPendingAupAnchor();
            Quaternion targetRotation = SanitizeQuaternion(state.TargetRotation, _lastAppliedWorldRotation);
            Vector3 targetLocalPosition = SanitizeVector3(state.TargetLocalPosition, _lastAppliedLocalPosition);
            bool applyTransformDirectly = HectonCameraState.RequiresDirectTransform(state.Flags);
            if (!applyTransformDirectly)
                targetLocalPosition += ResolveLateFrameKccLocalOffset(in state);
            float safeDeltaTime = math.isfinite(state.DeltaTime) ? math.max(0f, state.DeltaTime) : 0f;
            float targetFieldOfView = SanitizeFieldOfView(
                state.TargetFieldOfView,
                cameraComponent != null ? cameraComponent.fieldOfView : 60f);
            bool lockTrackingForAup = _hasLastAppliedTrackingState && SystemDispatcher.CurrentFrameIndex == _originShiftTrackingLockFrame;
            if (lockTrackingForAup)
            {
                cameraTransform.rotation = SanitizeQuaternion(_lastAppliedWorldRotation, targetRotation);
                cameraTransform.localPosition = SanitizeVector3(_lastAppliedLocalPosition, targetLocalPosition);
                _originShiftTrackingLockFrame = -1;
                _lastAppliedLocalPosition = cameraTransform.localPosition;
                _lastAppliedWorldRotation = cameraTransform.rotation;
                return;
            }
            else if (_originShiftTrackingLockFrame >= 0 && SystemDispatcher.CurrentFrameIndex > _originShiftTrackingLockFrame)
            {
                _originShiftTrackingLockFrame = -1;
            }

            if (applyTransformDirectly)
            {
                cameraTransform.rotation = targetRotation;
                cameraTransform.localPosition = targetLocalPosition;
            }
            else
            {
                float rotationT = ResolvePresentationBlendT(state.RotationSharpness, safeDeltaTime);
                Quaternion currentRotation = SanitizeQuaternion(cameraTransform.rotation, targetRotation);
                cameraTransform.rotation = ApproximateNlerpNoSqrt(currentRotation, targetRotation, rotationT);

                float positionT = ResolvePresentationBlendT(state.PositionSharpness, safeDeltaTime);
                Vector3 currentLocalPosition = SanitizeVector3(cameraTransform.localPosition, targetLocalPosition);
                cameraTransform.localPosition = currentLocalPosition + ((targetLocalPosition - currentLocalPosition) * positionT);
            }

            if (cameraComponent != null)
            {
                float fovT = ResolvePresentationBlendT(state.FieldOfViewSharpness, safeDeltaTime);
                float currentFieldOfView = SanitizeFieldOfView(cameraComponent.fieldOfView, targetFieldOfView);
                cameraComponent.fieldOfView = math.lerp(currentFieldOfView, targetFieldOfView, fovT);
            }

            _lastAppliedLocalPosition = cameraTransform.localPosition;
            _lastAppliedWorldRotation = cameraTransform.rotation;
            _hasLastAppliedTrackingState = true;
        }

        private Vector3 ResolveLateFrameKccLocalOffset(in HectonCameraState state)
        {
            float alpha = ResolveFixedInterpolationAlpha();
            Vector3 currentFixedPosition = SanitizeVector3(state.CurrentFixedPosition, Vector3.zero);
            Vector3 previousFixedPosition = SanitizeVector3(state.PreviousFixedPosition, currentFixedPosition);
            Vector3 interpolatedFixedPosition = previousFixedPosition + ((currentFixedPosition - previousFixedPosition) * alpha);
            Vector3 worldOffset = interpolatedFixedPosition - currentFixedPosition;
            worldOffset.x = math.clamp(worldOffset.x, -MaximumLateFrameKccOffsetMeters, MaximumLateFrameKccOffsetMeters);
            worldOffset.y = math.clamp(worldOffset.y, -MaximumLateFrameKccOffsetMeters, MaximumLateFrameKccOffsetMeters);
            worldOffset.z = math.clamp(worldOffset.z, -MaximumLateFrameKccOffsetMeters, MaximumLateFrameKccOffsetMeters);

            Transform parent = cameraTransform != null ? cameraTransform.parent : null;
            return parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;
        }

        private static float ResolveFixedInterpolationAlpha()
        {
            float alpha = HectonFloatingOrigin.CurrentFixedInterpolationAlpha;
            return math.isfinite(alpha) ? math.saturate(alpha) : 0f;
        }

        private static float ResolvePresentationBlendT(float sharpness, float deltaTime)
        {
            float x = math.max(MinimumBlendSharpness, sharpness) * math.max(MinimumBlendDeltaTime, deltaTime);
            return math.saturate(x / (1f + 0.5f * x));
        }

        private static Vector3 SanitizeVector3(Vector3 value, Vector3 fallback)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z)
                ? value
                : fallback;
        }

        private static Quaternion SanitizeQuaternion(Quaternion value, Quaternion fallback)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.dot(q, q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return IsFiniteQuaternion(fallback) ? fallback : Quaternion.identity;

            if (math.abs(lengthSq - 1f) > QuaternionUnitLengthSqEpsilon)
                q *= ApproximateInverseLengthNoSqrt(lengthSq);

            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static float SanitizeFieldOfView(float value, float fallback)
        {
            float resolved = math.isfinite(value) ? value : fallback;
            return math.clamp(resolved, MinimumCameraFov, MaximumCameraFov);
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion fromRotation, Quaternion toRotation, float blend01)
        {
            float4 from = new float4(fromRotation.x, fromRotation.y, fromRotation.z, fromRotation.w);
            float4 to = new float4(toRotation.x, toRotation.y, toRotation.z, toRotation.w);
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, math.saturate(blend01));
            blended *= ApproximateInverseLengthNoSqrt(math.dot(blended, blended));
            return new Quaternion(blended.x, blended.y, blended.z, blended.w);
        }

        private static float ApproximateInverseLengthNoSqrt(float lengthSq)
        {
            return math.rcp(0.5f + (0.5f * math.max(lengthSq, 0.000001f)));
        }

        private void ApplyPendingAupAnchor()
        {
            if (trackingSpaceRoot == null)
                return;

            Transform targetParent = _pendingAupAnchor != null ? _pendingAupAnchor : _defaultTrackingSpaceParent;
            if (trackingSpaceRoot.parent != targetParent)
                trackingSpaceRoot.SetParent(targetParent, true);

            _appliedAupAnchor = _pendingAupAnchor;
            if (_appliedAupAnchor == null)
                return;

            trackingSpaceRoot.localPosition = Vector3.zero;
            trackingSpaceRoot.localRotation = Quaternion.identity;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void TryUnregister()
        {
            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        private void ClearPendingState()
        {
            _hasPendingState = false;
            _pendingState = default;
            _pendingAupAnchor = null;
            _appliedAupAnchor = null;
            _hasLastAppliedTrackingState = false;
            _originShiftTrackingLockFrame = -1;
            _lastAppliedLocalPosition = Vector3.zero;
            _lastAppliedWorldRotation = Quaternion.identity;
        }
    }
}
