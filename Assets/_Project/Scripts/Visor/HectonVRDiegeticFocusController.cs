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

        private static readonly int HectonWorldFocusBlurId = Shader.PropertyToID("_HectonWorldFocusBlur");
        private static readonly int HectonHudFocusBlurId = Shader.PropertyToID("_HectonHudFocusBlur");

        [Header("Focus Ray")]
        [SerializeField] private Transform eyeSelectionOrigin = null;
        [SerializeField] private Camera fallbackEyeCamera = null;
        [SerializeField] private DiegeticPanelController pdaPanel = null;

        [Header("Focus Response")]
        [SerializeField, Min(0.05f)] private float focusDistanceMeters = 3f;
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
            Transform rayOrigin = ResolveEyeSelectionOrigin();
            if (rayOrigin == null)
            {
                ApplyFocusTargets(0f, 0f, deltaTime);
                return;
            }

            bool pdaFocused = pdaPanel != null &&
                              pdaPanel.TryProjectRayToCanvas(
                                  rayOrigin.position,
                                  rayOrigin.forward,
                                  focusDistanceMeters,
                                  out float2 _,
                                  out Vector3 _);

            float worldTarget = pdaFocused ? worldBlurWhenPdaFocused : 0f;
            float hudTarget = pdaFocused ? 0f : hudBlurWhenSceneFocused;
            ApplyFocusTargets(worldTarget, hudTarget, deltaTime);
        }

        internal void OverrideFocusTargets(Transform selectionOrigin, DiegeticPanelController panel)
        {
            eyeSelectionOrigin = selectionOrigin;
            pdaPanel = panel;
        }

        private Transform ResolveEyeSelectionOrigin()
        {
            if (eyeSelectionOrigin != null)
                return eyeSelectionOrigin;

            Camera camera = fallbackEyeCamera;
            if (camera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                camera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            return camera != null ? camera.transform : null;
        }

        private void ApplyFocusTargets(float worldTarget, float hudTarget, float deltaTime)
        {
            float safeDt = math.max(0f, deltaTime);
            float linearAlpha = safeDt > 0f
                ? 1f - math.exp(-math.max(0.01f, focusBlendSpeed) * safeDt)
                : 1f;
            float alpha = linearAlpha * linearAlpha * (3f - (2f * linearAlpha));

            _worldBlur = math.lerp(_worldBlur, math.saturate(worldTarget), alpha);
            _hudBlur = math.lerp(_hudBlur, math.saturate(hudTarget), alpha);
            ApplyGlobalIfChanged(HectonWorldFocusBlurId, ref _appliedWorldBlur, _worldBlur);
            ApplyGlobalIfChanged(HectonHudFocusBlurId, ref _appliedHudBlur, _hudBlur);
        }

        private static void ApplyGlobalIfChanged(int shaderId, ref float appliedValue, float value)
        {
            float clampedValue = math.saturate(value);
            if (math.abs(appliedValue - clampedValue) <= GlobalWriteEpsilon)
                return;

            appliedValue = clampedValue;
            Shader.SetGlobalFloat(shaderId, clampedValue);
        }

        private void TryRegisterTick()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = GlobalRegistry.Updatables.Contains(this);
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
            focusBlendSpeed = math.max(0.01f, focusBlendSpeed);
            worldBlurWhenPdaFocused = math.saturate(worldBlurWhenPdaFocused);
            hudBlurWhenSceneFocused = math.saturate(hudBlurWhenSceneFocused);
        }
#endif
    }
}
