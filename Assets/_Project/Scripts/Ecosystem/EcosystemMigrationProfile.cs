using System;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    [CreateAssetMenu(fileName = "EcosystemMigrationProfile_", menuName = "Hecton8/Ecosystem/Migration Profile")]
    public sealed class EcosystemMigrationProfile : ScriptableObject
    {
        [Serializable]
        public struct TemperatureRoute
        {
            [Tooltip("Diagnostic label for this temperature band.")]
            public string label;
            [Tooltip("Minimum inclusive water temperature for this migration band.")]
            public float minTemperatureCelsius;
            [Tooltip("Maximum inclusive water temperature for this migration band.")]
            public float maxTemperatureCelsius;
            [Tooltip("Planar migration distance applied when this band is active.")]
            [Min(1f)] public float migrationDistanceMeters;
            [Tooltip("Preferred planar migration heading in XZ space. Zero means deterministic hash heading.")]
            public Vector2 preferredPlanarDirection;
            [Tooltip("Additional downward offset applied to the resolved migration target.")]
            public float depthBiasMeters;
            [Tooltip("How strongly local water current bends the authored route heading.")]
            [Range(0f, 1f)] public float currentAlignmentWeight;
        }

        [Header("Temperature Routes")]
        [Tooltip("Authored migration bands keyed by sampled water temperature.")]
        [SerializeField] private TemperatureRoute[] temperatureRoutes;

        public bool TryResolveRoute(float temperatureCelsius, out TemperatureRoute route)
        {
            if (temperatureRoutes != null)
            {
                for (int i = 0; i < temperatureRoutes.Length; i++)
                {
                    TemperatureRoute candidate = temperatureRoutes[i];
                    if (temperatureCelsius < candidate.minTemperatureCelsius ||
                        temperatureCelsius > candidate.maxTemperatureCelsius)
                    {
                        continue;
                    }

                    route = candidate;
                    return true;
                }
            }

            route = default;
            return false;
        }
    }
}
