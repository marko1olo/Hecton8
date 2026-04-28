using Hecton8.Visor;
using Hecton8.Core;
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

            EnsureProceduralAudioRenderer(playerObject);

            if (playerObject.GetComponent<DeepPsychosisController>() == null)
                playerObject.AddComponent<DeepPsychosisController>();

            if (playerObject.GetComponent<PlayerStressVFX>() == null)
                playerObject.AddComponent<PlayerStressVFX>();

            if (playerObject.GetComponent<CausticsProjectorManager>() == null)
                playerObject.AddComponent<CausticsProjectorManager>();
        }

        private static void EnsureProceduralAudioRenderer(GameObject playerObject)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;

            AudioListener listener = null;
            if (playerCamera != null)
                playerCamera.TryGetComponent(out listener);

            if (listener == null)
                playerObject.TryGetComponent(out listener);

            if (listener == null)
                return;

            PlayerCriticalProceduralAudioRenderer renderer =
                listener.GetComponent<PlayerCriticalProceduralAudioRenderer>();
            if (renderer == null)
                renderer = listener.gameObject.AddComponent<PlayerCriticalProceduralAudioRenderer>();

            renderer.BindToPlayer(playerObject);

            PlayerThrusterAudio legacyThrusterAudio = playerContext != null ? playerContext.ThrusterAudio : null;
            if (legacyThrusterAudio != null)
            {
                AudioSource legacySource = legacyThrusterAudio.GetComponent<AudioSource>();
                if (legacySource != null && legacySource.isPlaying)
                    legacySource.Stop();

                legacyThrusterAudio.enabled = false;
            }
        }
    }
}
