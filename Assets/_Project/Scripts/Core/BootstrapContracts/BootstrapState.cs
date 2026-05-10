// ============================================================================
// HECTON-8 - BootstrapState.cs
// Canonical bootstrap read-model for runtime consumers.
//
// GameBootstrapper remains the owner and publisher. Runtime systems read this
// state to avoid depending on the full bootstrap owner when they only need
// lifecycle/player facts.
// ============================================================================

using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Canonical runtime read-model for bootstrap lifecycle and current player reference.
    /// </summary>
    public static class BootstrapState
    {
        /// <summary>
        /// Bootstrap completed initialization, player activation, and world-ready handoff.
        /// </summary>
        public static bool IsGameReady { get; private set; }

        /// <summary>
        /// A live GameBootstrapper instance currently owns scene startup.
        /// </summary>
        public static bool HasActiveInstance { get; private set; }

        /// <summary>
        /// Last known player GameObject published by bootstrap.
        /// </summary>
        public static GameObject CurrentPlayerObject { get; private set; }

        /// <summary>
        /// Fast-access player transform for runtime systems that must avoid scene search.
        /// </summary>
        public static Transform CurrentPlayerTransform =>
            CurrentPlayerObject != null ? CurrentPlayerObject.transform : null;

        /// <summary>
        /// Publishes whether bootstrap currently owns runtime startup.
        /// </summary>
        /// <param name="hasActiveInstance">True while GameBootstrapper owns scene activation.</param>
        public static void PublishBootstrapPresence(bool hasActiveInstance)
        {
            HasActiveInstance = hasActiveInstance;
        }

        /// <summary>
        /// Publishes the world-ready handoff state.
        /// </summary>
        /// <param name="isGameReady">True after bootstrap completes and gameplay may proceed.</param>
        public static void PublishGameReady(bool isGameReady)
        {
            IsGameReady = isGameReady;
        }

        /// <summary>
        /// Publishes the current player reference for runtime consumers.
        /// </summary>
        /// <param name="playerObject">Resolved player object, or null.</param>
        public static void PublishCurrentPlayerObject(GameObject playerObject)
        {
            CurrentPlayerObject = playerObject;
        }

        /// <summary>
        /// Clears the cached player reference if it still belongs to the specified object.
        /// </summary>
        /// <param name="playerObject">Player object being invalidated.</param>
        public static void ClearCurrentPlayerObject(GameObject playerObject)
        {
            if (ReferenceEquals(CurrentPlayerObject, playerObject))
                CurrentPlayerObject = null;
        }

        /// <summary>
        /// Tries to resolve the current player transform without scene search.
        /// </summary>
        /// <param name="playerTransform">Resolved transform when available.</param>
        /// <returns>True when a current player transform exists.</returns>
        public static bool TryGetCurrentPlayerTransform(out Transform playerTransform)
        {
            playerTransform = CurrentPlayerTransform;
            return playerTransform != null;
        }

        /// <summary>
        /// Resets published bootstrap state during domain reload or play mode exit.
        /// </summary>
        public static void Reset()
        {
            IsGameReady = false;
            HasActiveInstance = false;
            CurrentPlayerObject = null;
        }
    }
}
