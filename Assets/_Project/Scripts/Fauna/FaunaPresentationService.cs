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
        internal void ConfigureSpawnedCreature(
            FaunaBrain ai,
            CreatureArchetypeData archetype,
            int biomeIndex,
            Vector3 runtimePosition,
            in WorldChunkCoordinate chunkCoord)
        {
            if (ai == null)
                return;

            FaunaGeneticsManager.Instance?.ApplyTraits(ai, archetype, biomeIndex, runtimePosition);
            EcosystemHealthDirector.Instance?.ConfigureSpawnedFauna(ai, archetype, chunkCoord);
        }
    }
}
