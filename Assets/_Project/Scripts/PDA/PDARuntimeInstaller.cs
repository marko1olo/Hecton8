using UnityEngine;
using Hecton8.UI;

namespace Hecton8.PDA
{
    /// <summary>
    /// Cold-path runtime installer for PDA systems that must exist on the active player rig.
    /// </summary>
    public static class PDARuntimeInstaller
    {
        /// <summary>
        /// Ensures the active player owns the PDA exploration, logbook, and marker components.
        /// </summary>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            if (playerObject.GetComponent<PlayerExplorationTracker>() == null)
                playerObject.AddComponent<PlayerExplorationTracker>();

            if (playerObject.GetComponent<PDALogbookManager>() == null)
                playerObject.AddComponent<PDALogbookManager>();

            if (playerObject.GetComponent<PDAMarkerRegistry>() == null)
                playerObject.AddComponent<PDAMarkerRegistry>();

            if (playerObject.GetComponent<PDAIntrusionManager>() == null)
                playerObject.AddComponent<PDAIntrusionManager>();
        }
    }
}
