using System;
using Hecton8.AI;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    [Serializable]
    internal struct ProceduralFaunaSpeciesDefinition
    {
        [Tooltip("Stable runtime species id mirrored into cognition, spatial-hash and encounter systems.")]
        public int speciesId;
        [Tooltip("Human-readable content key for reports and authoring audits.")]
        public string speciesKey;
        [Tooltip("Optional archetype asset that owns this species entry.")]
        public CreatureArchetypeData archetype;
        [Tooltip("Director-level threat class used when this species is selected for encounter spawning.")]
        public EncounterThreatClass threatClass;
        [Min(0f)]
        [Tooltip("Relative weight used when the encounter director scores this species for despawn under load shedding.")]
        public float despawnPriorityBias;
        [Tooltip("If true, the species is eligible for predator-family encounter pools.")]
        public bool predator;
        [Tooltip("If true, the species is eligible for school/swarm encounter pools.")]
        public bool schooling;
    }

    /// <summary>
    /// Content template that groups fauna species under a procedural family for encounter and director authoring.
    /// </summary>
    [CreateAssetMenu(fileName = "ProceduralFamily_Fauna", menuName = "Hecton8/AI/Procedural Family/Fauna")]
    public sealed class ProceduralFamily_Fauna : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable content identifier for biome and encounter authoring.")]
        private string familyId = "fauna.family.generic";
        [SerializeField, Tooltip("Designer-facing label for this procedural fauna family.")]
        private string displayName = "Generic Procedural Fauna";

        [Header("Species")]
        [SerializeField, Tooltip("Species definitions grouped under this procedural family.")]
        private ProceduralFaunaSpeciesDefinition[] species =
        {
            new ProceduralFaunaSpeciesDefinition
            {
                speciesId = 1001,
                speciesKey = "fauna.swarm.generic",
                threatClass = EncounterThreatClass.Swarm,
                despawnPriorityBias = 1f,
                predator = false,
                schooling = true
            },
            new ProceduralFaunaSpeciesDefinition
            {
                speciesId = 8001,
                speciesKey = "fauna.leviathan.generic",
                threatClass = EncounterThreatClass.Leviathan,
                despawnPriorityBias = 0.15f,
                predator = true,
                schooling = false
            }
        };

        /// <summary>
        /// Stable family id used by content systems.
        /// </summary>
        public string FamilyId => familyId;

        /// <summary>
        /// Designer-facing label.
        /// </summary>
        public string DisplayName => displayName;

        internal bool TryResolveSpecies(int speciesId, out ProceduralFaunaSpeciesDefinition definition)
        {
            if (species != null)
            {
                for (int i = 0; i < species.Length; i++)
                {
                    if (species[i].speciesId != speciesId)
                        continue;

                    definition = species[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(familyId))
                familyId = "fauna.family.generic";

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Generic Procedural Fauna";

            if (species == null)
                return;

            for (int i = 0; i < species.Length; i++)
            {
                ProceduralFaunaSpeciesDefinition definition = species[i];
                definition.speciesId = Mathf.Max(0, definition.speciesId);
                definition.speciesKey = string.IsNullOrWhiteSpace(definition.speciesKey)
                    ? $"fauna.species.{definition.speciesId}"
                    : definition.speciesKey.Trim();
                definition.despawnPriorityBias = Mathf.Max(0f, definition.despawnPriorityBias);
                species[i] = definition;
            }
        }
    }
}
