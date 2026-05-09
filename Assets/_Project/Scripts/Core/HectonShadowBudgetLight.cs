using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Authored scene-light bridge for the URP shadow budget guard.
    /// Attach only to first-party forward spotlights that are allowed to compete for the single dynamic shadow slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Hecton8/Rendering/Shadow Budget Light")]
    public sealed class HectonShadowBudgetLight : MonoBehaviour
    {
        [SerializeField] private Light _light;
        private bool _registered;

        private void OnEnable()
        {
            if (_registered)
                return;

            ResolveLight();
            if (_light == null)
                return;

            _registered = HectonUrpShadowBudgetGuard.RegisterAuthoritativeForwardSpotlight(_light);
        }

        private void OnDisable()
        {
            if (!_registered)
                return;

            HectonUrpShadowBudgetGuard.UnregisterDynamicShadowLight(_light);
            _registered = false;
        }

        private void ResolveLight()
        {
            if (_light != null)
                return;

            TryGetComponent(out _light);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveLight();
        }
#endif
    }
}
