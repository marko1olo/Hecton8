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

            if (!playerObject.TryGetComponent<PDAContextualAdvisorySystem>(out _))
                playerObject.AddComponent<PDAContextualAdvisorySystem>();

            if (!playerObject.TryGetComponent<PlayerAchievementRegistry>(out _))
                playerObject.AddComponent<PlayerAchievementRegistry>();

            if (!playerObject.TryGetComponent<NarrativeProgressionBridge>(out _))
                playerObject.AddComponent<NarrativeProgressionBridge>();

            if (!playerObject.TryGetComponent<HectonOSBootManager>(out _))
                playerObject.AddComponent<HectonOSBootManager>();

            if (!playerObject.TryGetComponent<SonarHoloCompass>(out _))
                playerObject.AddComponent<SonarHoloCompass>();

            if (!playerObject.TryGetComponent<Hecton8.UI.FakeRadarBlipController>(out _))
                playerObject.AddComponent<Hecton8.UI.FakeRadarBlipController>();

            if (!playerObject.TryGetComponent<Hecton8.UI.ShaderCompassRibbon>(out _))
                playerObject.AddComponent<Hecton8.UI.ShaderCompassRibbon>();

            if (!playerObject.TryGetComponent<AcousticEcholocationTranslator>(out _))
                playerObject.AddComponent<AcousticEcholocationTranslator>();

            if (!playerObject.TryGetComponent<AudioCaptionOverlay>(out _))
                playerObject.AddComponent<AudioCaptionOverlay>();

            if (!playerObject.TryGetComponent<TerminalBootSequence>(out _))
                playerObject.AddComponent<TerminalBootSequence>();

            if (!playerObject.TryGetComponent<PDADeathMemoryDump>(out _))
                playerObject.AddComponent<PDADeathMemoryDump>();

            if (!playerObject.TryGetComponent<ARWaypointOverlay>(out _))
                playerObject.AddComponent<ARWaypointOverlay>();
        }
    }
}
