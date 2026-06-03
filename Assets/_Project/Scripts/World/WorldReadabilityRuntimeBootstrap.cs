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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying)
                return;

            GameObject runtimeOwner = ResolveManagersRoot();
            if (runtimeOwner == null)
                return;

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
            WorldReadabilityDirector existingDirector = WorldReadabilityDirector.ActiveRuntimeInstance;
            if (existingDirector != null)
            {
                existingDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WorldReadabilityDirector runtimeDirector = runtimeOwner.AddComponent<WorldReadabilityDirector>();
            runtimeDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);

            Hecton8.Core.H8Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned WorldReadabilityDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#else
            return;
#endif
        }

        private static void EnsureRelayDirector(GameObject runtimeOwner)
        {
            EmergencyServiceRelayDirector existingDirector = EmergencyServiceRelayDirector.ActiveRuntimeInstance;
            if (existingDirector != null)
                return;

            if (EmergencyServiceRelay.ActiveCount <= 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            runtimeOwner.AddComponent<EmergencyServiceRelayDirector>();

            Hecton8.Core.H8Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned EmergencyServiceRelayDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#else
            return;
#endif
        }

        private static GameObject ResolveManagersRoot()
        {
            GameObject runtimeOwner = null;
            WorldRuntimeReferenceUtility.TryResolveManagersRoot(ref runtimeOwner);
            if (runtimeOwner != null)
                return runtimeOwner;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // COLD ALLOC: GameObject[1] — runtime fail-safe owner for missing onboarding directors — owner: WorldReadabilityRuntimeBootstrap
            return new GameObject("WorldReadabilityDirector_Root");
#else
            return null;
#endif
        }
    }
}
