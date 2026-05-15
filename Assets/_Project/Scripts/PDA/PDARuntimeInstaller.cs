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

            if (!playerObject.TryGetComponent<PlayerExplorationTracker>(out _))
                playerObject.AddComponent<PlayerExplorationTracker>();

            if (!playerObject.TryGetComponent<PDALogbookManager>(out _))
                playerObject.AddComponent<PDALogbookManager>();

            if (!playerObject.TryGetComponent<PDAMarkerRegistry>(out _))
                playerObject.AddComponent<PDAMarkerRegistry>();

            if (!playerObject.TryGetComponent<PDAIntrusionManager>(out _))
                playerObject.AddComponent<PDAIntrusionManager>();
        }
    }
}
