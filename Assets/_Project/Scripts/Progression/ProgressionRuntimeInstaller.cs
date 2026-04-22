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

            if (playerObject.GetComponent<AcousticEcholocationTranslator>() == null)
                playerObject.AddComponent<AcousticEcholocationTranslator>();

            if (playerObject.GetComponent<AudioCaptionOverlay>() == null)
                playerObject.AddComponent<AudioCaptionOverlay>();

            if (playerObject.GetComponent<TerminalBootSequence>() == null)
                playerObject.AddComponent<TerminalBootSequence>();

            if (playerObject.GetComponent<PDADeathMemoryDump>() == null)
                playerObject.AddComponent<PDADeathMemoryDump>();

            if (playerObject.GetComponent<ARWaypointOverlay>() == null)
                playerObject.AddComponent<ARWaypointOverlay>();
        }
    }
}
