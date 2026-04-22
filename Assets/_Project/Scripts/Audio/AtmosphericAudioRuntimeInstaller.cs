using Hecton8.Visor;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Cold-path installer for player-owned atmospheric audio and immersion systems.
    /// </summary>
    public static class AtmosphericAudioRuntimeInstaller
    {
        /// <summary>
        /// Ensures the active player owns the atmospheric polish systems.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            if (playerObject.GetComponent<DeepPsychosisController>() == null)
                playerObject.AddComponent<DeepPsychosisController>();

            if (playerObject.GetComponent<PlayerStressVFX>() == null)
                playerObject.AddComponent<PlayerStressVFX>();

            if (playerObject.GetComponent<CausticsProjectorManager>() == null)
                playerObject.AddComponent<CausticsProjectorManager>();
        }
    }
}
