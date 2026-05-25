using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Drives visor/world focus globals from a physical PDA surface projection.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Visor/VR Diegetic Focus Controller")]
    public sealed class HectonVRDiegeticFocusController : MonoBehaviour, ITickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float GlobalWriteEpsilon = 0.001f;
        private const float FocusSleepEpsilon = 0.002f;

        private static readonly int HectonWorldFocusBlurId = Shader.PropertyToID("_HectonWorldFocusBlur");
        private static readonly int HectonHudFocusBlurId = Shader.PropertyToID("_HectonHudFocusBlur");

        [Header("Focus Ray")]
        [SerializeField] private Transform eyeSelectionOrigin = null;
        [SerializeField] private Camera fallbackEyeCamera = null;
        [SerializeField] private DiegeticPanelController pdaPanel = null;

        [Header("Focus Response")]
        [SerializeField, Min(0.05f)] private float focusDistanceMeters = 3f;
        [SerializeField, Range(0f, 1f)] private float focusGateDotThreshold = 0.35f;
        [SerializeField, Min(0.01f)] private float focusBlendSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float worldBlurWhenPdaFocused = 1f;
        [SerializeField, Range(0f, 1f)] private float hudBlurWhenSceneFocused = 0.45f;
        [SerializeField] private bool clearGlobalsOnDisable = true;

        private bool _registeredToTick;
        private bool _registeredToLateFrame;
        private bool _hotSwapRegistered;
        private float _worldBlur;
        private float _hudBlur;
        private float _appliedWorldBlur = -1f;
        private float _appliedHudBlur = -1f;
        private float _pendingWorldTarget;
        private float _pendingHudTarget;
        private float _pendingFocusDeltaTime;
        private bool _focusVisualDirty;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
            if (clearGlobalsOnDisable)
            {
                _worldBlur = 0f;
                _hudBlur = 0f;
                _appliedWorldBlur = -1f;
                _appliedHudBlur = -1f;
                Shader.SetGlobalFloat(HectonWorldFocusBlurId, 0f);
                Shader.SetGlobalFloat(HectonHudFocusBlurId, 0f);
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            _registeredToTick = false;
            _registeredToLateFrame = false;
            if (currentService != null && isActiveAndEnabled)
                TryRegisterTick();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!TryResolveEyeSelectionPose(out Vector3 rayOriginPosition, out Vector3 rayForward))
            {
                QueueFocusTargets(0f, 0f, deltaTime);
                return;
            }

            bool pdaFocused = pdaPanel != null &&
                              IsPanelInsideCheapFocusGate(rayOriginPosition, rayForward) &&
                              pdaPanel.TryProjectRayToCanvas(
                                  rayOriginPosition,
                                  rayForward,
                                  focusDistanceMeters,
                                  out float2 _,
                                  out Vector3 _);

            float worldTarget = pdaFocused ? worldBlurWhenPdaFocused : 0f;
            float hudTarget = pdaFocused ? 0f : hudBlurWhenSceneFocused;
            QueueFocusTargets(worldTarget, hudTarget, deltaTime);
        }

        public void LateFrameTick()
        {
            if (!_focusVisualDirty)
                return;

            _focusVisualDirty = false;
            float deltaTime = _pendingFocusDeltaTime;
            _pendingFocusDeltaTime = 0f;
            ApplyFocusTargets(_pendingWorldTarget, _pendingHudTarget, deltaTime);
        }

        internal void OverrideFocusTargets(Transform selectionOrigin, DiegeticPanelController panel)
        {
            eyeSelectionOrigin = selectionOrigin;
            pdaPanel = panel;
            TryRegisterTick();
        }

        private bool TryResolveEyeSelectionPose(out Vector3 position, out Vector3 forward)
        {
            Transform rayOrigin = ResolveEyeSelectionOrigin();
            if (rayOrigin == null)
            {
                position = Vector3.zero;
                forward = Vector3.forward;
                return false;
            }

            rayOrigin.GetPositionAndRotation(out position, out Quaternion rotation);
            if (!IsFinite(position) || !IsFinite(rotation))
            {
                position = Vector3.zero;
                forward = Vector3.forward;
                return false;
            }

            forward = rotation * Vector3.forward;
            return IsFinite(forward);
        }

        private Transform ResolveEyeSelectionOrigin()
        {
            if (eyeSelectionOrigin != null)
                return eyeSelectionOrigin;

            Camera camera = fallbackEyeCamera;
            if (camera == null)
            {
                if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                    camera = runtimeContext.PlayerCamera;

                if (camera == null)
                {
                    IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                    camera = playerContext != null ? playerContext.PlayerCamera : null;
                }
            }

            return camera != null ? camera.transform : null;
        }

        private bool IsPanelInsideCheapFocusGate(Vector3 rayOriginPosition, Vector3 rayForward)
        {
            if (pdaPanel == null || !pdaPanel.TryGetFocusGateData(out Vector3 panelOrigin, out _))
                return false;

            if (!IsFinite(rayOriginPosition) || !IsFinite(rayForward) || !IsFinite(panelOrigin))
                return false;

            float3 toPanel = (float3)(panelOrigin - rayOriginPosition);
            float distanceSq = math.lengthsq(toPanel);
            if (!math.isfinite(distanceSq))
                return false;

            float safeFocusDistance = SanitizeMinimum(focusDistanceMeters, 0.05f);
            float safeFocusDistanceSq = safeFocusDistance * safeFocusDistance;
            if (distanceSq > safeFocusDistanceSq)
                return false;

            if (distanceSq <= 0.0001f)
                return true;

            float3 forward = (float3)rayForward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.isfinite(forwardLengthSq) || forwardLengthSq <= 0.0001f)
                return false;

            float forwardDot = math.dot(forward, toPanel);
            if (!math.isfinite(forwardDot) || forwardDot <= 0f)
                return false;

            float threshold = Sanitize01(focusGateDotThreshold);
            return forwardDot * forwardDot >= distanceSq * forwardLengthSq * threshold * threshold;
        }

        private void ApplyFocusTargets(float worldTarget, float hudTarget, float deltaTime)
        {
            bool deltaTimeFinite = math.isfinite(deltaTime);
            float safeDt = deltaTimeFinite ? math.max(0f, deltaTime) : 0f;
            float blend = safeDt > 0f
                ? FastDecayBlend(SanitizeMinimum(focusBlendSpeed, 0.01f), safeDt)
                : (deltaTimeFinite ? 1f : 0f);
            float alpha = SmoothStep01(blend);

            _worldBlur = math.lerp(Sanitize01(_worldBlur), Sanitize01(worldTarget), alpha);
            _hudBlur = math.lerp(Sanitize01(_hudBlur), Sanitize01(hudTarget), alpha);
            ApplyGlobalIfChanged(HectonWorldFocusBlurId, ref _appliedWorldBlur, _worldBlur);
            ApplyGlobalIfChanged(HectonHudFocusBlurId, ref _appliedHudBlur, _hudBlur);
        }

        private void QueueFocusTargets(float worldTarget, float hudTarget, float deltaTime)
        {
            _pendingWorldTarget = Sanitize01(worldTarget);
            _pendingHudTarget = Sanitize01(hudTarget);
            _pendingFocusDeltaTime += math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f);
            _focusVisualDirty = true;
        }

        private bool AreFocusTargetsSettled(float worldTarget, float hudTarget)
        {
            return math.abs(Sanitize01(_worldBlur) - Sanitize01(worldTarget)) <= FocusSleepEpsilon &&
                math.abs(Sanitize01(_hudBlur) - Sanitize01(hudTarget)) <= FocusSleepEpsilon;
        }

        private static void ApplyGlobalIfChanged(int shaderId, ref float appliedValue, float value)
        {
            float clampedValue = Sanitize01(value);
            if (!math.isfinite(appliedValue))
                appliedValue = -1f;

            if (math.abs(appliedValue - clampedValue) <= GlobalWriteEpsilon)
                return;

            appliedValue = clampedValue;
            Shader.SetGlobalFloat(shaderId, clampedValue);
        }

        private static float SmoothStep01(float t)
        {
            t = Sanitize01(t);
            return t * t * (3f - (2f * t));
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = SanitizeNonNegative(speed) * SanitizeNonNegative(deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private void TryRegisterTick()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToTick)
                _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredToLateFrame)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTick()
        {
            if (_registeredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredToLateFrame = false;
            }

            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTick = false;
            }
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
            focusDistanceMeters = SanitizeMinimum(focusDistanceMeters, 0.05f);
            focusGateDotThreshold = Sanitize01(focusGateDotThreshold);
            focusBlendSpeed = SanitizeMinimum(focusBlendSpeed, 0.01f);
            worldBlurWhenPdaFocused = Sanitize01(worldBlurWhenPdaFocused);
            hudBlurWhenSceneFocused = Sanitize01(hudBlurWhenSceneFocused);
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeMinimum(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }
    }
}
