using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Semantic thermal plume wrapper over <see cref="CurrentVolume"/>.
    /// CurrentVolume owns water transport. Heat is published as thermodynamics field data, not a PhysX hazard volume.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurrentVolume))]
    [AddComponentMenu("Hecton/Physics/Thermal Updraft Volume")]
    public sealed class ThermalUpdraftVolume : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        [Header("Flow")]
        [Tooltip("Upward force stamped into the backing CurrentVolume.")]
        [SerializeField, Min(0f)] private float updraftStrength = 6.8f;

        [Tooltip("Lateral swirl factor layered into the plume.")]
        [SerializeField, Range(-1f, 1f)] private float swirlBias = 0.12f;

        [Header("Heat")]
        [Tooltip("Heat intensity injected into the thermodynamics field.")]
        [SerializeField, Min(0f)] private float heatIntensity = 18f;

        [Tooltip("Multiplies the CurrentVolume radius when publishing the survival heat hazard.")]
        [SerializeField, Min(0.1f)] private float hazardRadiusScale = 1.1f;

        private CurrentVolume _currentVolume;
        private bool _registeredToTick;
        private int _hazardSourceId;

        private void Awake()
        {
            TryGetComponent(out _currentVolume);
            _hazardSourceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            SanitizeAuthoringValues();
            ApplyPreset();
        }

        private void OnEnable()
        {
            SanitizeAuthoringValues();
            ApplyPreset();
            UpdateHazardRegistration();
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegisterToTick();
        }

        private void OnDisable()
        {
            TryUnregisterFromTick();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            HectonHazardManager.Unregister(_hazardSourceId);
        }

        private void OnDestroy()
        {
            TryUnregisterFromTick();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            HectonHazardManager.Unregister(_hazardSourceId);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterFromTick();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterToTick();
            }
        }

        public void SlowTick()
        {
            UpdateHazardRegistration();
        }

        private void ApplyPreset()
        {
            if (_currentVolume == null)
                return;

            Vector3 plumeDirection = Mathf.Abs(swirlBias) > 0.0001f
                ? new Vector3(swirlBias, 1f, swirlBias * 0.35f)
                : Vector3.up;

            _currentVolume.ApplySemanticFlowPreset(
                Mathf.Abs(swirlBias) > 0.0001f
                    ? CurrentVolume.FlowPattern.Directional
                    : CurrentVolume.FlowPattern.Updraft,
                plumeDirection,
                updraftStrength,
                1f,
                swirlBias);
        }

        private void SanitizeAuthoringValues()
        {
            updraftStrength = float.IsFinite(updraftStrength) ? Mathf.Max(0f, updraftStrength) : 0f;
            swirlBias = float.IsFinite(swirlBias) ? Mathf.Clamp(swirlBias, -1f, 1f) : 0f;
            heatIntensity = float.IsFinite(heatIntensity) ? Mathf.Max(0f, heatIntensity) : 0f;
            hazardRadiusScale = float.IsFinite(hazardRadiusScale) ? Mathf.Max(0.1f, hazardRadiusScale) : 0.1f;
        }

        private void UpdateHazardRegistration()
        {
            if (!isActiveAndEnabled)
                return;

            Vector3 position = transform.position;
            if (!IsFiniteVector3(position) ||
                !float.IsFinite(heatIntensity) ||
                heatIntensity <= 0f)
            {
                HectonHazardManager.Unregister(_hazardSourceId);
                return;
            }

            float influenceRadius = _currentVolume != null
                ? _currentVolume.GetApproximateInfluenceRadius()
                : 1f;
            float safeInfluenceRadius = FiniteAtLeast(influenceRadius, 1f, 1f);
            float radius = safeInfluenceRadius * FiniteAtLeast(hazardRadiusScale, 1.1f, 0.1f);
            if (!float.IsFinite(radius) || radius <= 0f)
            {
                HectonHazardManager.Unregister(_hazardSourceId);
                return;
            }

            IThermodynamicsService thermodynamics = GlobalRegistry.ThermodynamicsService;
            bool injected = thermodynamics != null &&
                            thermodynamics.IsInitialized &&
                            thermodynamics.TryInjectTransientHeatSource(
                                position,
                                radius,
                                heatIntensity,
                                unchecked((uint)_hazardSourceId));
            if (injected)
                return;

            HectonHazardManager.Unregister(_hazardSourceId);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static float FiniteAtLeast(float value, float fallback, float minimum)
        {
            float safeFallback = float.IsFinite(fallback) ? fallback : minimum;
            float safeValue = float.IsFinite(value) ? value : safeFallback;
            return Mathf.Max(minimum, safeValue);
        }

        private void TryRegisterToTick()
        {
            if (_registeredToTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTick()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_currentVolume == null)
                TryGetComponent(out _currentVolume);

            SanitizeAuthoringValues();
            ApplyPreset();
        }
#endif
    }
}
