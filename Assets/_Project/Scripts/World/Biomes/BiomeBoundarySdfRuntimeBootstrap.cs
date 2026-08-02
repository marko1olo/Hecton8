using Hecton8.World;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World.Biomes
{
    /// <summary>
    /// Runtime fail-safe for the biome transition producers. Authored scene placement is preferred.
    /// </summary>
    internal static class BiomeBoundarySdfRuntimeBootstrap
    {
        private const string FallbackRootName = "BiomeTransitionRuntime_Root";
        private const string WorldSceneName = "02_HECTON_WORLD";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying ||
                !IsWorldScene(SceneManager.GetActiveScene()) ||
                (BiomeTransitionManagerRuntime.ActiveRuntimeInstance != null &&
                 BiomeBoundarySdfRuntime.ActiveRuntimeInstance != null))
            {
                return;
            }

            GameObject runtimeOwner = ResolveRuntimeOwner();
            if (runtimeOwner == null)
                return;

            BiomeTransitionManagerRuntime transitionRuntime = null;
            BiomeBoundarySdfRuntime sdfRuntime = null;
            if (BiomeTransitionManagerRuntime.ActiveRuntimeInstance == null &&
                !runtimeOwner.TryGetComponent(out transitionRuntime))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                transitionRuntime = runtimeOwner.AddComponent<BiomeTransitionManagerRuntime>();
            }

            if (BiomeBoundarySdfRuntime.ActiveRuntimeInstance == null &&
                !runtimeOwner.TryGetComponent(out sdfRuntime))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                sdfRuntime = runtimeOwner.AddComponent<BiomeBoundarySdfRuntime>();
            }

            if (transitionRuntime != null)
                transitionRuntime.enabled = true;
            if (sdfRuntime != null)
                sdfRuntime.enabled = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(
                "[BiomeBoundarySdfRuntimeBootstrap] Spawned biome transition runtime host because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }

        private static GameObject ResolveRuntimeOwner()
        {
            GameObject runtimeOwner = null;
            WorldRuntimeReferenceUtility.TryResolveManagersRoot(ref runtimeOwner);
            if (runtimeOwner != null)
                return runtimeOwner;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            // COLD ALLOC: GameObject[1] - runtime fail-safe owner for missing biome-boundary SDF producer - owner: BiomeBoundarySdfRuntimeBootstrap
            return new GameObject(FallbackRootName);
        }

        private static bool IsWorldScene(Scene scene)
        {
            return scene.IsValid() &&
                string.Equals(scene.name, WorldSceneName, StringComparison.Ordinal);
        }
    }
}