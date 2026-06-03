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

            if (!playerObject.TryGetComponent(out DeepPsychosisController _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored DeepPsychosisController on player. Runtime component creation is disabled.", playerObject);
#endif
            }

            if (!playerObject.TryGetComponent(out PlayerStressVFX _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored PlayerStressVFX on player. Runtime component creation is disabled.", playerObject);
#endif
            }

            // Projected caustics are shader-only on MX350; no player-owned compute projector is installed.
        }

        private static void EnsureProceduralAudioRenderer(GameObject playerObject)
        {
            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;

            AudioListener listener = null;
            if (playerCamera != null)
                playerCamera.TryGetComponent(out listener);

            if (listener == null)
                playerObject.TryGetComponent(out listener);

            if (listener == null)
                return;

            if (!listener.TryGetComponent(out PlayerCriticalProceduralAudioRenderer renderer))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored PlayerCriticalProceduralAudioRenderer on active listener. Runtime component creation is disabled.", listener);
#endif
                return;
            }

            if (!listener.TryGetComponent(out VocalWarningSystem _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored VocalWarningSystem on active listener. Runtime component creation is disabled.", listener);
#endif
            }

            renderer.BindToPlayer(playerObject);

            PlayerThrusterAudio legacyThrusterAudio = playerContext != null ? playerContext.ThrusterAudio : null;
            if (legacyThrusterAudio != null)
            {
                if (legacyThrusterAudio.TryGetComponent(out AudioSource legacySource) && legacySource.isPlaying)
                    legacySource.Stop();

                legacyThrusterAudio.enabled = false;
            }
        }
    }
}
