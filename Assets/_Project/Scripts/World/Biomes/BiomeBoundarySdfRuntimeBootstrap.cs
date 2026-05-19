using Hecton8.World;
using UnityEngine;

namespace Hecton8.World.Biomes
{
    /// <summary>
    /// Runtime fail-safe for the biome transition producers. Authored scene placement is preferred.
    /// </summary>
    internal static class BiomeBoundarySdfRuntimeBootstrap
    {
        private const string FallbackRootName = "BiomeTransitionRuntime_Root";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying ||
                (BiomeTransitionManagerRuntime.ActiveRuntimeInstance != null &&
                 BiomeBoundarySdfRuntime.ActiveRuntimeInstance != null))
            {
                return;
            }

            GameObject runtimeOwner = ResolveRuntimeOwner();
            BiomeTransitionManagerRuntime transitionRuntime = null;
            BiomeBoundarySdfRuntime sdfRuntime = null;
            if (BiomeTransitionManagerRuntime.ActiveRuntimeInstance == null &&
                !runtimeOwner.TryGetComponent(out transitionRuntime))
            {
                transitionRuntime = runtimeOwner.AddComponent<BiomeTransitionManagerRuntime>();
            }

            if (BiomeBoundarySdfRuntime.ActiveRuntimeInstance == null &&
                !runtimeOwner.TryGetComponent(out sdfRuntime))
            {
                sdfRuntime = runtimeOwner.AddComponent<BiomeBoundarySdfRuntime>();
            }

            if (transitionRuntime != null)
                transitionRuntime.enabled = true;
            if (sdfRuntime != null)
                sdfRuntime.enabled = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
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

            // COLD ALLOC: GameObject[1] - runtime fail-safe owner for missing biome-boundary SDF producer - owner: BiomeBoundarySdfRuntimeBootstrap
            return new GameObject(FallbackRootName);
        }
    }
}
