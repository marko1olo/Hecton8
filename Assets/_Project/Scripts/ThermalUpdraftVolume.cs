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
    public sealed class ThermalUpdraftVolume : MonoBehaviour, ISlowTickable
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
            ApplyPreset();
        }

        private void OnEnable()
        {
            ApplyPreset();
            UpdateHazardRegistration();
            TryRegisterToTick();
        }

        private void OnDisable()
        {
            TryUnregisterFromTick();
            HectonHazardManager.Unregister(_hazardSourceId);
        }

        private void OnDestroy()
        {
            TryUnregisterFromTick();
            HectonHazardManager.Unregister(_hazardSourceId);
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

        private void UpdateHazardRegistration()
        {
            if (!isActiveAndEnabled)
                return;

            float radius = _currentVolume != null
                ? _currentVolume.GetApproximateInfluenceRadius() * Mathf.Max(0.1f, hazardRadiusScale)
                : Mathf.Max(1f, hazardRadiusScale);

            IThermodynamicsService thermodynamics = GlobalRegistry.ThermodynamicsService;
            if (thermodynamics != null && thermodynamics.IsInitialized)
                thermodynamics.TryInjectTransientHeatSource(transform.position, radius, heatIntensity, unchecked((uint)_hazardSourceId));
            else
                HectonHazardManager.Unregister(_hazardSourceId);
        }

        private void TryRegisterToTick()
        {
            if (_registeredToTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = GlobalRegistry.SlowTickables.Contains(this);
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

            updraftStrength = Mathf.Max(0f, updraftStrength);
            swirlBias = Mathf.Clamp(swirlBias, -1f, 1f);
            heatIntensity = Mathf.Max(0f, heatIntensity);
            hazardRadiusScale = Mathf.Max(0.1f, hazardRadiusScale);
            ApplyPreset();
        }
#endif
    }
}
