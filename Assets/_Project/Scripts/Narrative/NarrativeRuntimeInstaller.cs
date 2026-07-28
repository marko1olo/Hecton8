using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Cold-path runtime installer for player-adjacent narrative systems.
    /// </summary>
    public static class NarrativeRuntimeInstaller
    {
        /// <summary>
        /// Ensures the active player owns the procedural lore frontier director and the water column
        /// narrative bridge that arms the first-hour quest chain.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            EnsureProceduralLoreDirector(playerObject);
            EnsureWaterColumnEntryNarrativeBridge(playerObject);
        }

        /// <summary>
        /// Installs the procedural lore frontier director once per player owner.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        private static void EnsureProceduralLoreDirector(GameObject playerObject)
        {
            if (playerObject.TryGetComponent(out ProceduralLoreDirector _))
                return;

            if (!ProceduralLoreDirector.IsInstalledOn(playerObject))
                playerObject.AddComponent<ProceduralLoreDirector>();
        }

        /// <summary>
        /// Installs the water column entry bridge once per player owner.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        /// <remarks>
        /// The bridge is the second producer of the <c>first_hour_exit_lifepod</c> discovery that every
        /// entry edge of the mission spine keys off. The first producer,
        /// NarrativeProgressionBridge.TryIssueExitLifePodDiscoveryFromAup, is reachable only from a
        /// BaseAirlockEventType.EnvironmentChanged event, and BaseAirlock is authored into no scene and
        /// no prefab, so nothing could ever complete Quest_Arrival or Quest_FirstHour_ExitLifePod or
        /// trigger Quest_StarterDrill, Quest_CopperSample and Quest_FirstHour_CollectTitanium.
        ///
        /// It goes on the player root, not a runtime holder, because it reads the player movement
        /// snapshot and shares that owner's lifetime. It carries no Destroy(gameObject) path, so it
        /// cannot repeat the shared-root destruction defect.
        /// </remarks>
        private static void EnsureWaterColumnEntryNarrativeBridge(GameObject playerObject)
        {
            if (playerObject.TryGetComponent(out WaterColumnEntryNarrativeBridge _))
                return;

            if (!WaterColumnEntryNarrativeBridge.IsInstalledOn(playerObject))
                playerObject.AddComponent<WaterColumnEntryNarrativeBridge>();
        }
    }
}
