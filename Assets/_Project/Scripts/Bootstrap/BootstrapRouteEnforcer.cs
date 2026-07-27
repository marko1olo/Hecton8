using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// How a runtime scene owner reached the scene it is waking up in.
    /// </summary>
    public enum BootstrapRouteStatus : byte
    {
        /// <summary>
        /// Every mandatory core service is registered. The owner may initialize immediately.
        /// </summary>
        Ready = 0,

        /// <summary>
        /// Bootstrap started this play session and has not finished its ordered phases yet.
        /// The scene was reached through the production route, only early. Owners must keep
        /// initializing and pick services up as they land.
        /// </summary>
        Initializing = 1,

        /// <summary>
        /// The scene was entered with no bootstrap at all and a recovery load is scheduled.
        /// </summary>
        Recovering = 2,

        /// <summary>
        /// The scene was entered with no bootstrap and the recovery load was refused.
        /// A later evaluation retries.
        /// </summary>
        Failed = 3,
    }

    /// <summary>
    /// Enforces the production scene route so runtime scenes cannot execute
    /// without a live bootstrap owner.
    /// </summary>
    public static class BootstrapRouteEnforcer
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";

        // Latched only once SceneManager actually accepts a recovery load. Latching before the
        // attempt left a session with no bootstrap and no pending recovery whenever the load
        // was refused, because nothing ever cleared the flag to allow a second try.
        private static bool _bootstrapRecoveryScheduled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _bootstrapRecoveryScheduled = false;
        }

        /// <summary>
        /// Classifies how the current runtime scene was reached, scheduling a bootstrap
        /// recovery load only for scenes that genuinely bypassed bootstrap.
        /// </summary>
        /// <param name="currentSceneName">Scene the calling owner lives in.</param>
        /// <param name="ownerName">Calling owner, used for route diagnostics.</param>
        /// <returns>The route status the caller must react to.</returns>
        public static BootstrapRouteStatus EvaluateBootstrapRuntimeRoute(
            string currentSceneName,
            string ownerName)
        {
            if (!Application.isPlaying)
                return BootstrapRouteStatus.Ready;

            if (GameBootstrapper.AreAllSystemsReady())
                return BootstrapRouteStatus.Ready;

            // A boot that already began owns this route and is still walking its ordered phases,
            // so an owner woken inside that window arrived legally and only has to wait. Treating
            // it as an illegal entry is what broke the production route: the recovery below loads
            // 00_BOOTSTRAP as LoadSceneMode.Single, which tears down the in-flight bootstrapper
            // along with the very services it was registering.
            if (BootstrapStatus.BootStarted || BootstrapState.HasActiveInstance)
                return BootstrapRouteStatus.Initializing;

            if (_bootstrapRecoveryScheduled)
                return BootstrapRouteStatus.Recovering;

            Hecton8.Core.H8Debug.LogError(
                $"[{ownerName}] Scene '{currentSceneName}' entered without an active bootstrap. " +
                $"Reloading {BootstrapSceneName} to restore the required route.");

            GameStartContextHolder.Reset();
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                BootstrapSceneName,
                LoadSceneMode.Single);
            if (operation == null)
            {
                // SceneManager refuses a Single load while another load is still activating.
                // Leave the latch clear so the next evaluation retries the recovery.
                Hecton8.Core.H8Debug.LogError(
                    $"[{ownerName}] Failed to schedule async bootstrap recovery load.");
                return BootstrapRouteStatus.Failed;
            }

            _bootstrapRecoveryScheduled = true;
            return BootstrapRouteStatus.Recovering;
        }

        /// <summary>
        /// Ensures the current runtime scene was reached through bootstrap.
        /// Returns true only when every mandatory core service is already live.
        /// Callers that can tolerate services arriving mid-boot should use
        /// <see cref="EvaluateBootstrapRuntimeRoute"/> so they are not disabled during
        /// a legal, still-initializing boot.
        /// </summary>
        /// <param name="currentSceneName">Scene the calling owner lives in.</param>
        /// <param name="ownerName">Calling owner, used for route diagnostics.</param>
        /// <returns>True when core services are ready.</returns>
        public static bool EnsureBootstrapRuntimeRoute(string currentSceneName, string ownerName)
        {
            return EvaluateBootstrapRuntimeRoute(currentSceneName, ownerName) ==
                   BootstrapRouteStatus.Ready;
        }
    }
}
