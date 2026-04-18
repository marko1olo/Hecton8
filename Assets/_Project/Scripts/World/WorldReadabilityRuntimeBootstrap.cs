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
        private const string SystemsRootName = "--- SYSTEMS ---";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying)
                return;

            GameObject runtimeOwner = ResolveManagersRoot();
            EnsureRelayDirector(runtimeOwner);

            WorldZoneDirector runtimeWorldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;
            BiomeMatrixDirector runtimeBiomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (runtimeWorldZoneDirector == null || runtimeBiomeMatrixDirector == null)
                return;

            EnsureReadabilityDirector(runtimeOwner, runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
        }

        private static void EnsureReadabilityDirector(
            GameObject runtimeOwner,
            WorldZoneDirector runtimeWorldZoneDirector,
            BiomeMatrixDirector runtimeBiomeMatrixDirector)
        {
            WorldReadabilityDirector existingDirector = Object.FindAnyObjectByType<WorldReadabilityDirector>(FindObjectsInactive.Include);
            if (existingDirector != null)
            {
                existingDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
                return;
            }

            WorldReadabilityDirector runtimeDirector = runtimeOwner.AddComponent<WorldReadabilityDirector>();
            runtimeDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned WorldReadabilityDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }

        private static void EnsureRelayDirector(GameObject runtimeOwner)
        {
            EmergencyServiceRelayDirector existingDirector =
                Object.FindAnyObjectByType<EmergencyServiceRelayDirector>(FindObjectsInactive.Include);
            if (existingDirector != null)
                return;

            if (EmergencyServiceRelay.ActiveCount <= 0)
                return;

            runtimeOwner.AddComponent<EmergencyServiceRelayDirector>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned EmergencyServiceRelayDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }

        private static GameObject ResolveManagersRoot()
        {
            GameObject runtimeOwner = GameObject.Find(ManagersRootName);
            if (runtimeOwner != null)
                return runtimeOwner;

            runtimeOwner = GameObject.Find(SystemsRootName);
            if (runtimeOwner != null)
                return runtimeOwner;

            // COLD ALLOC: GameObject[1] — runtime fail-safe owner for missing onboarding directors — owner: WorldReadabilityRuntimeBootstrap
            return new GameObject("WorldReadabilityDirector_Root");
        }
    }
}
