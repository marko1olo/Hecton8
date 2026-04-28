using System;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    [Serializable]
    internal struct EncounterThreatBand
    {
        [Tooltip("Threat class selected when the current director intensity passes this threshold.")]
        public EncounterThreatClass threatClass;
        [Range(0f, 1f)]
        [Tooltip("Minimum normalized intensity required before this threat class becomes eligible.")]
        public float minimumIntensity;
        [Tooltip("If false, this threat class is suppressed when the player is in the critical-health spawn suppression window.")]
        public bool allowDuringCriticalHealth;
    }

    /// <summary>
    /// Authoring template for encounter pacing thresholds and threat eligibility bands.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterProfile", menuName = "Hecton8/AI/Encounter Profile")]
    public sealed class EncounterProfile : ScriptableObject
    {
        [Header("Threat Authoring")]
        [Tooltip("Optional authored token-cost table used by the encounter director.")]
        [SerializeField] private ThreatCostTable threatCostTable;
        [Tooltip("Ordered or unordered threat bands. The highest matching minimum intensity wins at runtime.")]
        [SerializeField] private EncounterThreatBand[] threatBands =
        {
            new EncounterThreatBand { threatClass = EncounterThreatClass.Drone, minimumIntensity = 0f, allowDuringCriticalHealth = true },
            new EncounterThreatBand { threatClass = EncounterThreatClass.Swarm, minimumIntensity = 0.25f, allowDuringCriticalHealth = true },
            new EncounterThreatBand { threatClass = EncounterThreatClass.Stalker, minimumIntensity = 0.55f, allowDuringCriticalHealth = false },
            new EncounterThreatBand { threatClass = EncounterThreatClass.Leviathan, minimumIntensity = 0.85f, allowDuringCriticalHealth = false }
        };

        internal ThreatCostTable ThreatCostTable => threatCostTable;

        internal float ResolveMinimumIntensity(EncounterThreatClass threatClass, float fallback)
        {
            if (TryResolveBand(threatClass, out EncounterThreatBand band))
                return band.minimumIntensity;

            return fallback;
        }

        internal bool ResolveAllowDuringCriticalHealth(EncounterThreatClass threatClass, bool fallback)
        {
            if (TryResolveBand(threatClass, out EncounterThreatBand band))
                return band.allowDuringCriticalHealth;

            return fallback;
        }

        private bool TryResolveBand(EncounterThreatClass threatClass, out EncounterThreatBand band)
        {
            float bestThreshold = float.MinValue;
            bool found = false;
            band = default;

            if (threatBands == null)
                return false;

            for (int i = 0; i < threatBands.Length; i++)
            {
                EncounterThreatBand candidate = threatBands[i];
                if (candidate.threatClass != threatClass || candidate.minimumIntensity < bestThreshold)
                    continue;

                band = candidate;
                bestThreshold = candidate.minimumIntensity;
                found = true;
            }

            return found;
        }

        private void OnValidate()
        {
            if (threatBands == null)
                return;

            for (int i = 0; i < threatBands.Length; i++)
            {
                EncounterThreatBand band = threatBands[i];
                band.minimumIntensity = Mathf.Clamp01(band.minimumIntensity);
                threatBands[i] = band;
            }
        }
    }
}
