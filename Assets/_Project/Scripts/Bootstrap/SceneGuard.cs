// ============================================================================
// HECTON-8 — SceneGuard.cs
// Protect non-bootstrap scenes from executing without a live bootstrap owner.
//
// Route classification is NOT owned here. BootstrapRouteEnforcer is the single
// owner of "did this scene reach me through 00_BOOTSTRAP", of the recovery load,
// and of the start-context reset that goes with it. This component only reacts
// to the status it is handed, exactly as MainMenuController does.
//
// This file used to carry its own copy of that decision: a raw
// AreAllSystemsReady() test followed by GameStartContextHolder.Reset() and a
// LoadSceneMode.Single reload of 00_BOOTSTRAP. Because it runs at
// DefaultExecutionOrder(-29000) it fired before almost everything else in its
// scene, so a boot that had merely not finished its ordered phases yet was
// treated as an illegal entry: the Single reload tore down the in-flight
// bootstrapper along with the services it was registering, and the Reset() wiped
// the pending target scene the main menu had just written.
//
// ============================================================================

using UnityEngine;
using Hecton8.Bootstrap;
using Hecton8.World;

namespace Hecton8.Guardian
{
    /// <summary>
    /// Scene guard. Confirms the scene it lives in was reached through bootstrap
    /// and stands down while the route enforcer recovers a session that was not.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29000)] // Posle BootstrapController, no do ostalnogo
    public sealed class SceneGuard : MonoBehaviour
    {
        [SerializeField] private bool _enforceBootstrap = true;

        private void Awake()
        {
            WorldShippingSceneRuntimeGuard.CleanupLoadedScene(gameObject.scene);

            if (!_enforceBootstrap)
                return;

            BootstrapRouteStatus routeStatus = BootstrapRouteEnforcer.EvaluateBootstrapRuntimeRoute(
                gameObject.scene.name,
                nameof(SceneGuard));

            // Ready: bootstrap finished, nothing to guard against.
            // Initializing: bootstrap started and owns this route, it is only mid-phase.
            // Either way this guard must not touch the scene or the start context.
            if (routeStatus != BootstrapRouteStatus.Recovering &&
                routeStatus != BootstrapRouteStatus.Failed)
            {
                return;
            }

            // No bootstrap ran at all. The enforcer owns the recovery load and has
            // already reset the start context; this scene is being torn down, so the
            // guard stops rather than acting on state that is about to disappear.
            enabled = false;
        }
    }
}
