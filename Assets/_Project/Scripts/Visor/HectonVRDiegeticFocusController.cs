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
    public sealed class HectonVRDiegeticFocusController : MonoBehaviour, ITickable
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
        private float _worldBlur;
        private float _hudBlur;
        private float _appliedWorldBlur = -1f;
        private float _appliedHudBlur = -1f;

        private void OnEnable()
        {
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
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

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!TryResolveEyeSelectionPose(out Vector3 rayOriginPosition, out Vector3 rayForward))
            {
                ApplyFocusTargets(0f, 0f, deltaTime);
                if (pdaPanel == null && AreFocusTargetsSettled(0f, 0f))
                    TryUnregisterTick();

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
            ApplyFocusTargets(worldTarget, hudTarget, deltaTime);
            if (pdaPanel == null && AreFocusTargetsSettled(worldTarget, hudTarget))
                TryUnregisterTick();
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

            float3 toPanel = (float3)(panelOrigin - rayOriginPosition);
            float distanceSq = math.lengthsq(toPanel);
            float safeFocusDistance = math.max(0.05f, focusDistanceMeters);
            if (distanceSq > safeFocusDistance * safeFocusDistance)
                return false;

            if (distanceSq <= 0.0001f)
                return true;

            float3 forward = (float3)rayForward;
            float forwardLengthSq = math.lengthsq(forward);
            if (forwardLengthSq <= 0.0001f)
                return false;

            float forwardDot = math.dot(forward, toPanel);
            if (forwardDot <= 0f)
                return false;

            float threshold = math.saturate(focusGateDotThreshold);
            return forwardDot * forwardDot >= distanceSq * forwardLengthSq * threshold * threshold;
        }

        private void ApplyFocusTargets(float worldTarget, float hudTarget, float deltaTime)
        {
            float safeDt = math.max(0f, deltaTime);
            float blend = safeDt > 0f
                ? FastDecayBlend(math.max(0.01f, focusBlendSpeed), safeDt)
                : 1f;
            float alpha = SmoothStep01(blend);

            _worldBlur = math.lerp(_worldBlur, math.saturate(worldTarget), alpha);
            _hudBlur = math.lerp(_hudBlur, math.saturate(hudTarget), alpha);
            ApplyGlobalIfChanged(HectonWorldFocusBlurId, ref _appliedWorldBlur, _worldBlur);
            ApplyGlobalIfChanged(HectonHudFocusBlurId, ref _appliedHudBlur, _hudBlur);
        }

        private bool AreFocusTargetsSettled(float worldTarget, float hudTarget)
        {
            return math.abs(_worldBlur - math.saturate(worldTarget)) <= FocusSleepEpsilon &&
                math.abs(_hudBlur - math.saturate(hudTarget)) <= FocusSleepEpsilon;
        }

        private static void ApplyGlobalIfChanged(int shaderId, ref float appliedValue, float value)
        {
            float clampedValue = math.saturate(value);
            if (math.abs(appliedValue - clampedValue) <= GlobalWriteEpsilon)
                return;

            appliedValue = clampedValue;
            Shader.SetGlobalFloat(shaderId, clampedValue);
        }

        private static float SmoothStep01(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - (2f * t));
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private void TryRegisterTick()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            focusDistanceMeters = math.max(0.05f, focusDistanceMeters);
            focusGateDotThreshold = math.saturate(focusGateDotThreshold);
            focusBlendSpeed = math.max(0.01f, focusBlendSpeed);
            worldBlurWhenPdaFocused = math.saturate(worldBlurWhenPdaFocused);
            hudBlurWhenSceneFocused = math.saturate(hudBlurWhenSceneFocused);
        }
#endif

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
