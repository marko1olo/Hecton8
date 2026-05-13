using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Mathematical radiation source. Exposure is sampled by RadiationHazardGrid, not by trigger colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard")]
    public sealed class RadiationHazard : MonoBehaviour
    {
        [Header("Radiation Settings")]
        [SerializeField] private float radiationBuildupRate = 0.5f;
        [SerializeField] private float maxRadiationLevel = 100f;
        [SerializeField, Min(0.5f)] private float radiationRadiusMeters = 18f;

        private int _sourceId;
        private Transform _cachedTransform;

        private void Awake()
        {
            _sourceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            _cachedTransform = transform;
        }

        private void OnEnable()
        {
            RegisterSource();
        }

        private void OnDisable()
        {
            RadiationHazardGrid.UnregisterSource(_sourceId);
        }

        private void OnDestroy()
        {
            RadiationHazardGrid.UnregisterSource(_sourceId);
        }

        private void RegisterSource()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            float intensity = Mathf.Max(0f, radiationBuildupRate) * Mathf.Max(0f, maxRadiationLevel);
            RadiationHazardGrid.RegisterSource(_sourceId, _cachedTransform.position, intensity, radiationRadiusMeters);
        }
    }
}
