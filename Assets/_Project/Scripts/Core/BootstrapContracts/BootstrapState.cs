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
            CurrentPlayerObject = IsProductionPlayerAuthorityObject(playerObject) ? playerObject : null;
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

        /// <summary>
        /// Validates that the object owns production player authority without referencing gameplay assemblies.
        /// </summary>
        /// <param name="playerObject">Candidate player object.</param>
        /// <returns>True when the object has the required production player authority components.</returns>
        public static bool IsProductionPlayerAuthorityObject(GameObject playerObject)
        {
            if (playerObject == null)
                return false;

            // Three constant-cost component probes on the candidate itself, evaluated BEFORE the
            // hierarchy walk below. The predicate is a plain conjunction, so the accepted set is
            // unchanged by this ordering - but a candidate that fails a cheap probe no longer pays
            // for a recursive subtree walk to learn it. GameBootstrapper.cs:8009 runs this over
            // multiple candidates, so every rejected one used to pay the full walk first.
            if (!playerObject.TryGetComponent(out IBootstrapProductionPlayerMovementAuthority movement) ||
                movement == null ||
                !playerObject.TryGetComponent(out IBootstrapProductionPlayerInteractionAuthority interaction) ||
                interaction == null ||
                !playerObject.TryGetComponent(out Rigidbody body) ||
                body == null)
            {
                return false;
            }

            return !IsLegacyWorldShellOwned(playerObject);
        }

        /// <summary>
        /// Names the FIRST condition of <see cref="IsProductionPlayerAuthorityObject"/> that a candidate
        /// fails, or <c>"NONE"</c> when it passes.
        ///
        /// This exists because the boolean above collapses five independent requirements into one bit,
        /// and a caller that rejects the player then has nothing to report but "invalid". A headless
        /// route probe hit exactly that wall: boot walked thirteen activation steps, reached
        /// Step 8.9 Scene Gate Verification, and died with the generic
        /// <c>PLAYER_INSTANTIATION_PENDING</c> - which named the gate, not the missing component. Five
        /// candidate causes and no way to tell them apart is the silent-degeneracy shape this project's
        /// rules single out: a system that can collapse quietly must fail loudly instead.
        ///
        /// Returns interned literals only - no allocation, no interpolation - so it is safe to call from
        /// the boot path and from a diagnostic poll.
        /// </summary>
        /// <param name="playerObject">Candidate player authority object.</param>
        /// <returns>A stable reason token, or <c>"NONE"</c>.</returns>
        public static string DescribeProductionPlayerAuthorityFailure(GameObject playerObject)
        {
            if (playerObject == null)
                return "PLAYER_NULL";

            if (IsLegacyWorldShellOwned(playerObject))
                return "PLAYER_OWNED_BY_LEGACY_WORLD_SHELL";

            if (!playerObject.TryGetComponent(out IBootstrapProductionPlayerMovementAuthority movement) ||
                movement == null)
            {
                return "PLAYER_MISSING_MOVEMENT_AUTHORITY";
            }

            if (!playerObject.TryGetComponent(out IBootstrapProductionPlayerInteractionAuthority interaction) ||
                interaction == null)
            {
                return "PLAYER_MISSING_INTERACTION_AUTHORITY";
            }

            if (!playerObject.TryGetComponent(out Rigidbody body) || body == null)
                return "PLAYER_MISSING_RIGIDBODY";

            return "NONE";
        }

        /// <summary>
        /// Rejects a candidate that is, contains, or descends from a legacy world-shell owner.
        ///
        /// CURRENTLY INERT, DELIBERATELY KEPT. <see cref="IBootstrapLegacyWorldShellOwner"/> has ZERO
        /// implementors in the project right now, so this method cannot return <c>true</c>. It is not
        /// abandoned code, and it must not be deleted casually - two live facts pin it:
        ///
        /// 1. <c>Tools/ValidatePlayerRouteStaticEvidence.py:129-145</c>
        ///    (<c>has_production_player_authority_guard</c>) requires the literal tokens
        ///    <c>IsLegacyWorldShellOwned</c> and <c>IBootstrapLegacyWorldShellOwner</c> to be present in
        ///    THIS file. Removing either flips that guard false and re-raises the
        ///    <c>bootstrap-publish-player-without-production-validation</c> blocker that was retired as a
        ///    false positive.
        /// 2. The marker interface exists specifically so this contracts-assembly file never names a
        ///    concrete gameplay/world class. The former implementor lived at
        ///    <c>Assets/_Project/Scripts/World/HectonWorldShellController1428.cs</c> (still the
        ///    validator's <c>DEFAULT_WORLD_SHELL</c> at <c>:34</c>) and that file no longer exists - the
        ///    implementor was deleted, the rejection contract was not.
        ///
        /// So this is a live rejection rule with no current subject, not dead code. Cost is bounded by
        /// the call ordering in <see cref="IsProductionPlayerAuthorityObject"/>: only a candidate that
        /// has already passed all three authority component probes reaches the walk.
        /// </summary>
        /// <param name="candidate">Candidate player authority object.</param>
        /// <returns>True when the candidate or any ancestor/descendant owns a legacy world shell.</returns>
        private static bool IsLegacyWorldShellOwned(GameObject candidate)
        {
            if (candidate == null)
                return false;

            if (candidate.TryGetComponent(out IBootstrapLegacyWorldShellOwner shell) &&
                shell != null)
            {
                return true;
            }

            Transform current = candidate.transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent(out shell) && shell != null)
                    return true;

                current = current.parent;
            }

            return ContainsLegacyWorldShellOwnerInChildren(candidate.transform);
        }

        private static bool ContainsLegacyWorldShellOwnerInChildren(Transform root)
        {
            if (root == null)
                return false;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (child.TryGetComponent(out IBootstrapLegacyWorldShellOwner shell) &&
                    shell != null)
                {
                    return true;
                }

                if (ContainsLegacyWorldShellOwnerInChildren(child))
                    return true;
            }

            return false;
        }
    }

    public interface IBootstrapProductionPlayerMovementAuthority
    {
    }

    public interface IBootstrapProductionPlayerInteractionAuthority
    {
    }

    /// <summary>
    /// Marker for a legacy in-scene world-shell owner. A player candidate that carries this, or that
    /// sits under or above one, is NOT the production player.
    ///
    /// ZERO IMPLEMENTORS TODAY. The only implementor,
    /// <c>Assets/_Project/Scripts/World/HectonWorldShellController1428.cs</c>, has been deleted, so
    /// <c>IsLegacyWorldShellOwned</c> is currently inert. The marker is retained on purpose: it is the
    /// contracts-assembly boundary that lets BootstrapState reject a scene-local shell player without
    /// referencing a gameplay/world type, and <c>Tools/ValidatePlayerRouteStaticEvidence.py:133</c>
    /// grep-asserts this exact name inside BootstrapState.cs. Do not delete it without also updating
    /// that validator and its test in the same change.
    /// </summary>
    public interface IBootstrapLegacyWorldShellOwner
    {
    }
}
