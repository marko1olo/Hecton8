using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Enforces the production scene route so runtime scenes cannot execute
    /// without a live bootstrap owner.
    /// </summary>
    public static class BootstrapRouteEnforcer
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private static bool _bootstrapRecoveryTriggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _bootstrapRecoveryTriggered = false;
        }

        /// <summary>
        /// Ensures the current runtime scene was reached through bootstrap.
        /// Returns false after scheduling a bootstrap recovery load.
        /// </summary>
        public static bool EnsureBootstrapRuntimeRoute(string currentSceneName, string ownerName)
        {
            if (!Application.isPlaying)
                return true;

            if (GameBootstrapper.AreAllSystemsReady())
                return true;

            if (_bootstrapRecoveryTriggered)
                return false;

            _bootstrapRecoveryTriggered = true;

            Hecton8.Core.H8Debug.LogError(
                $"[{ownerName}] Scene '{currentSceneName}' entered without an active bootstrap. " +
                $"Reloading {BootstrapSceneName} to restore the required route.");

            GameStartContextHolder.Reset();
            SceneManager.LoadScene(BootstrapSceneName);
            return false;
        }
    }
}
