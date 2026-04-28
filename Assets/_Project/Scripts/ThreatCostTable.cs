using System;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    [Serializable]
    internal struct ThreatCostDefinition
    {
        [Tooltip("Encounter threat class resolved by the Burst encounter director.")]
        public EncounterThreatClass threatClass;
        [Min(0f)]
        [Tooltip("Token cost spent when the director spawns this threat class.")]
        public float tokenCost;
        [Min(0)]
        [Tooltip("Maximum simultaneous live instances allowed for this threat class.")]
        public int maxSimultaneous;
        [Min(0f)]
        [Tooltip("Class-level multiplier applied when the director scores despawn candidates under load shedding.")]
        public float despawnPriorityBias;
    }

    /// <summary>
    /// Authored token-cost table consumed by the encounter director authoring snapshot.
    /// </summary>
    [CreateAssetMenu(fileName = "ThreatCostTable", menuName = "Hecton8/AI/Threat Cost Table")]
    public sealed class ThreatCostTable : ScriptableObject
    {
        [Header("Threat Costs")]
        [Tooltip("Per-threat token costs and simultaneous caps. Missing entries fall back to director defaults.")]
        [SerializeField] private ThreatCostDefinition[] entries =
        {
            new ThreatCostDefinition { threatClass = EncounterThreatClass.Drone, tokenCost = 10f, maxSimultaneous = 8, despawnPriorityBias = 1.25f },
            new ThreatCostDefinition { threatClass = EncounterThreatClass.Swarm, tokenCost = 20f, maxSimultaneous = 4, despawnPriorityBias = 1f },
            new ThreatCostDefinition { threatClass = EncounterThreatClass.Stalker, tokenCost = 35f, maxSimultaneous = 2, despawnPriorityBias = 0.55f },
            new ThreatCostDefinition { threatClass = EncounterThreatClass.Leviathan, tokenCost = 80f, maxSimultaneous = 1, despawnPriorityBias = 0.15f }
        };

        internal bool TryResolveDefinition(EncounterThreatClass threatClass, out ThreatCostDefinition definition)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].threatClass != threatClass)
                        continue;

                    definition = entries[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private void OnValidate()
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                ThreatCostDefinition definition = entries[i];
                definition.tokenCost = Mathf.Max(0f, definition.tokenCost);
                definition.maxSimultaneous = Mathf.Max(0, definition.maxSimultaneous);
                definition.despawnPriorityBias = Mathf.Max(0f, definition.despawnPriorityBias);
                entries[i] = definition;
            }
        }
    }
}
