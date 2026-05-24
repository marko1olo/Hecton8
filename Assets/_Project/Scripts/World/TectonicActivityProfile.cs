using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "TectonicActivityProfile", menuName = "Hecton8/World/Tectonic Activity Profile")]
    public sealed class TectonicActivityProfile : ScriptableObject
    {
        [System.Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SeismicEventSettings
        {
            [Range(0.1f, 4f)] public float collapseChanceMultiplier;
            [Range(1, 8)] public int stampCountMin;
            [Range(1, 8)] public int stampCountMax;
            [Min(1f)] public float stampScatterRadius;
            [Min(2f)] public float ceilingSearchDepth;
            [Min(0.5f)] public float craterRadiusMin;
            [Min(0.5f)] public float craterRadiusMax;
            [Min(4f)] public float impulseRadius;
            [Min(0.5f)] public float impulseMagnitude;

            public SeismicEventSettings Sanitize()
            {
                SeismicEventSettings sanitized = this;
                sanitized.collapseChanceMultiplier = Mathf.Clamp(sanitized.collapseChanceMultiplier, 0.1f, 4f);
                sanitized.stampCountMin = Mathf.Clamp(sanitized.stampCountMin, 1, 8);
                sanitized.stampCountMax = Mathf.Clamp(sanitized.stampCountMax, sanitized.stampCountMin, 8);
                sanitized.stampScatterRadius = Mathf.Max(1f, sanitized.stampScatterRadius);
                sanitized.ceilingSearchDepth = Mathf.Max(2f, sanitized.ceilingSearchDepth);
                sanitized.craterRadiusMin = Mathf.Max(0.5f, sanitized.craterRadiusMin);
                sanitized.craterRadiusMax = Mathf.Max(sanitized.craterRadiusMin, sanitized.craterRadiusMax);
                sanitized.impulseRadius = Mathf.Max(4f, sanitized.impulseRadius);
                sanitized.impulseMagnitude = Mathf.Max(0.5f, sanitized.impulseMagnitude);
                return sanitized;
            }
        }

        [System.Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct BiomeSeismicRule
        {
            public string familyId;
            public string geologyProfileId;
            public SeismicEventSettings settings;

            public bool Matches(string targetFamilyId, string targetGeologyProfileId)
            {
                bool familyMatches = string.IsNullOrWhiteSpace(familyId) ||
                                     string.Equals(familyId, targetFamilyId, System.StringComparison.OrdinalIgnoreCase);
                bool profileMatches = string.IsNullOrWhiteSpace(geologyProfileId) ||
                                      string.Equals(geologyProfileId, targetGeologyProfileId, System.StringComparison.OrdinalIgnoreCase);
                return familyMatches && profileMatches;
            }
        }

        [Header("Defaults")]
        [SerializeField] private SeismicEventSettings defaultSettings = new SeismicEventSettings
        {
            collapseChanceMultiplier = 1f,
            stampCountMin = 2,
            stampCountMax = 4,
            stampScatterRadius = 18f,
            ceilingSearchDepth = 18f,
            craterRadiusMin = 2.5f,
            craterRadiusMax = 6f,
            impulseRadius = 100f,
            impulseMagnitude = 14f
        };

        [Header("Biome Overrides")]
        [SerializeField] private BiomeSeismicRule[] biomeRules = System.Array.Empty<BiomeSeismicRule>();

        public SeismicEventSettings ResolveSeismicSettings(string familyId, string geologyProfileId)
        {
            if (biomeRules != null)
            {
                for (int i = 0; i < biomeRules.Length; i++)
                {
                    if (!biomeRules[i].Matches(familyId, geologyProfileId))
                        continue;

                    return biomeRules[i].settings.Sanitize();
                }
            }

            return defaultSettings.Sanitize();
        }
    }
}
