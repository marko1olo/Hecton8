using Hecton8.Ecosystem;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Presentation-side fauna spawn wiring. Simulation code must not depend on genetics, ecology, renderers, or animators.
    /// </summary>
    internal sealed class FaunaPresentationService
    {
        private FaunaGeneticsManager _faunaGenetics;
        private EcosystemHealthDirector _ecosystemHealth;

        internal void Bind(
            FaunaGeneticsManager faunaGenetics,
            EcosystemHealthDirector ecosystemHealth)
        {
            _faunaGenetics = faunaGenetics;
            _ecosystemHealth = ecosystemHealth;
        }

        internal void ConfigureSpawnedCreature(
            FaunaBrain ai,
            CreatureArchetypeData archetype,
            int biomeIndex,
            Vector3 runtimePosition,
            in WorldChunkCoordinate chunkCoord)
        {
            if (ai == null)
                return;

            _faunaGenetics?.ApplyTraits(ai, archetype, biomeIndex, runtimePosition);
            _ecosystemHealth?.ConfigureSpawnedFauna(ai, archetype, chunkCoord);
        }
    }
}
