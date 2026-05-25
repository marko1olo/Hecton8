using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World
{
    /// <summary>
    /// Enforces shipping scene cleanup for every runtime scene load, independent of bootstrap route.
    /// </summary>
    internal static class WorldShippingSceneRuntimeGuard
    {
        private static bool _subscribed;

        // COLD ALLOC: List<GameObject>[64] — reusable scene root buffer for shipping cleanup — owner: WorldShippingSceneRuntimeGuard
        private static readonly List<GameObject> _rootObjects = new List<GameObject>(64);
        // COLD ALLOC: List<Transform>[512] — reusable traversal stack for shipping cleanup — owner: WorldShippingSceneRuntimeGuard
        private static readonly List<Transform> _traversalStack = new List<Transform>(512);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _subscribed = false;
            _rootObjects.Clear();
            _traversalStack.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_subscribed)
            {
                CleanupScene(SceneManager.GetActiveScene());
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _subscribed = true;

            CleanupScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupLoadedScene(scene);
        }

        internal static int CleanupLoadedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            int suppressedCount = WorldShippingContentFilter.DeactivateSuppressedSceneObjects(
                scene,
                _rootObjects,
                _traversalStack);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (suppressedCount > 0)
            {
                Hecton8.Core.H8Debug.Log(
                    "[WorldShippingSceneRuntimeGuard] Deactivated " +
                    suppressedCount.ToString(CultureInfo.InvariantCulture) +
                    " suppressed objects in scene '" +
                    scene.name +
                    "'.");
            }
#endif

            return suppressedCount;
        }

        private static void CleanupScene(Scene scene)
        {
            CleanupLoadedScene(scene);
        }
    }
}
