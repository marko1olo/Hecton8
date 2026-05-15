using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Cold-path runtime installer for player-adjacent narrative systems.
    /// </summary>
    public static class NarrativeRuntimeInstaller
    {
        /// <summary>
        /// Ensures the active player owns the procedural lore frontier director.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            if (!playerObject.TryGetComponent<ProceduralLoreDirector>(out _))
                playerObject.AddComponent<ProceduralLoreDirector>();
        }
    }
}
