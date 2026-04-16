using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Ensures the world readability layer exists in runtime even if the scene authoring
    /// omitted the component. This is a fail-safe for onboarding/world-legibility, not a
    /// replacement for proper authored scene setup.
    /// </summary>
    internal static class WorldReadabilityRuntimeBootstrap
    {
        private const string ManagersRootName = "[MANAGERS]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying)
                return;

            WorldZoneDirector runtimeWorldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;
            BiomeMatrixDirector runtimeBiomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (runtimeWorldZoneDirector == null || runtimeBiomeMatrixDirector == null)
                return;

            WorldReadabilityDirector existingDirector = Object.FindAnyObjectByType<WorldReadabilityDirector>(FindObjectsInactive.Include);
            if (existingDirector != null)
            {
                existingDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
                return;
            }

            GameObject runtimeOwner = GameObject.Find(ManagersRootName);
            if (runtimeOwner == null)
            {
                // COLD ALLOC: GameObject[1] — runtime fail-safe for missing world readability director — owner: WorldReadabilityRuntimeBootstrap
                runtimeOwner = new GameObject("WorldReadabilityDirector_Root");
            }

            WorldReadabilityDirector runtimeDirector = runtimeOwner.AddComponent<WorldReadabilityDirector>();
            runtimeDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned WorldReadabilityDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }
    }
}
