using UnityEngine;
using Hecton8.UI;

namespace Hecton8.Progression
{
    /// <summary>
    /// Cold-path runtime installer for player-owned progression systems.
    /// </summary>
    public static class ProgressionRuntimeInstaller
    {
        /// <summary>
        /// Ensures the active player owns the contextual advisory and achievement registries.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            if (playerObject.GetComponent<PDAContextualAdvisorySystem>() == null)
                playerObject.AddComponent<PDAContextualAdvisorySystem>();

            if (playerObject.GetComponent<PlayerAchievementRegistry>() == null)
                playerObject.AddComponent<PlayerAchievementRegistry>();

            if (playerObject.GetComponent<HectonOSBootManager>() == null)
                playerObject.AddComponent<HectonOSBootManager>();

            if (playerObject.GetComponent<SonarHoloCompass>() == null)
                playerObject.AddComponent<SonarHoloCompass>();
        }
    }
}
