using Hecton8.World;
using UnityEngine;

namespace Hecton8.World.Biomes
{
    /// <summary>
    /// Runtime fail-safe for the biome-boundary SDF producer. Authored scene placement is preferred.
    /// </summary>
    internal static class BiomeBoundarySdfRuntimeBootstrap
    {
        private const string FallbackRootName = "BiomeBoundarySdfRuntime_Root";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying || BiomeBoundarySdfRuntime.ActiveRuntimeInstance != null)
                return;

            GameObject runtimeOwner = ResolveRuntimeOwner();
            if (!runtimeOwner.TryGetComponent(out BiomeBoundarySdfRuntime runtime))
                runtime = runtimeOwner.AddComponent<BiomeBoundarySdfRuntime>();

            runtime.enabled = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[BiomeBoundarySdfRuntimeBootstrap] Spawned BiomeBoundarySdfRuntime at runtime because the active scene had none. " +
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
