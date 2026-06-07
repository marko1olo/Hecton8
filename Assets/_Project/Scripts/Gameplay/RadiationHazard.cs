using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
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

            if (!Application.isPlaying || _sourceId == 0)
                return;

            Vector3 position = _cachedTransform.position;
            float intensity = radiationBuildupRate * maxRadiationLevel;
            if (!TryResolveValidRadiationSource(
                    position,
                    intensity,
                    radiationRadiusMeters,
                    out AbsoluteUniversePosition sourceAup,
                    out float safeIntensity,
                    out float safeRadius))
            {
                RadiationHazardGrid.UnregisterSource(_sourceId);
                return;
            }

            RadiationHazardGrid.RegisterSource(_sourceId, in sourceAup, safeIntensity, safeRadius);
        }

        private static bool TryResolveValidRadiationSource(
            Vector3 runtimePosition,
            float intensity,
            float radiusMeters,
            out AbsoluteUniversePosition sourceAup,
            out float safeIntensity,
            out float safeRadius)
        {
            sourceAup = default;
            safeIntensity = 0f;
            safeRadius = 0f;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z) ||
                !math.isfinite(intensity) ||
                intensity <= 0f ||
                !math.isfinite(radiusMeters) ||
                radiusMeters <= 0f)
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            sourceAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!sourceAup.IsFinite())
                return false;

            safeIntensity = intensity;
            safeRadius = math.max(0.5f, radiusMeters);
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(radiationBuildupRate) || radiationBuildupRate < 0f)
                radiationBuildupRate = 0f;
            if (!math.isfinite(maxRadiationLevel) || maxRadiationLevel < 0f)
                maxRadiationLevel = 0f;
            if (!math.isfinite(radiationRadiusMeters) || radiationRadiusMeters < 0.5f)
                radiationRadiusMeters = 0.5f;
        }
#endif
    }
}
