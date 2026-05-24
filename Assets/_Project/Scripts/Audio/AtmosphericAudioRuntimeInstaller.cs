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
                playerObject.AddComponent<DeepPsychosisController>();

            if (!playerObject.TryGetComponent(out PlayerStressVFX _))
                playerObject.AddComponent<PlayerStressVFX>();

            // Projected caustics are shader-only on MX350; no player-owned compute projector is installed.
        }

        private static void EnsureProceduralAudioRenderer(GameObject playerObject)
        {
            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;

            AudioListener listener = null;
            if (playerCamera != null)
                playerCamera.TryGetComponent(out listener);

            if (listener == null)
                playerObject.TryGetComponent(out listener);

            if (listener == null)
                return;

            if (!listener.TryGetComponent(out PlayerCriticalProceduralAudioRenderer renderer))
                renderer = listener.gameObject.AddComponent<PlayerCriticalProceduralAudioRenderer>();

            if (!listener.TryGetComponent(out VocalWarningSystem _))
                listener.gameObject.AddComponent<VocalWarningSystem>();

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
