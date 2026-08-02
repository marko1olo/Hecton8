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

            WorldZoneDirector runtimeWorldZoneDirector = null;
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref runtimeWorldZoneDirector);
            BiomeMatrixDirector runtimeBiomeMatrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref runtimeBiomeMatrixDirector);
            IDepthZoneReadModel runtimeDepthZoneReadModel = GlobalRegistry.DepthZoneReadModel;
            if (runtimeWorldZoneDirector == null &&
                runtimeBiomeMatrixDirector == null &&
                runtimeDepthZoneReadModel == null)
            {
                return;
            }

            EnsureReadabilityDirector(runtimeOwner, runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
        }

        private static void EnsureReadabilityDirector(
            GameObject runtimeOwner,
            WorldZoneDirector runtimeWorldZoneDirector,
            BiomeMatrixDirector runtimeBiomeMatrixDirector)
        {
            WorldReadabilityDirector existingDirector = null;
            WorldRuntimeReferenceUtility.TryResolveWorldReadabilityDirector(ref existingDirector);
            if (existingDirector != null)
            {
                existingDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);
                return;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            WorldReadabilityDirector runtimeDirector = runtimeOwner.AddComponent<WorldReadabilityDirector>();
            runtimeDirector.ApplyRuntimeDependencies(runtimeWorldZoneDirector, runtimeBiomeMatrixDirector);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned WorldReadabilityDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }

        private static void EnsureRelayDirector(GameObject runtimeOwner)
        {
            EmergencyServiceRelayDirector existingDirector = null;
            WorldRuntimeReferenceUtility.TryResolveEmergencyServiceRelayDirector(ref existingDirector);
            if (existingDirector != null)
                return;

            if (EmergencyServiceRelay.ActiveCount <= 0)
                return;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            runtimeOwner.AddComponent<EmergencyServiceRelayDirector>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(
                "[WorldReadabilityRuntimeBootstrap] Spawned EmergencyServiceRelayDirector at runtime because the active scene had none. " +
                "Owner='" + runtimeOwner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif
        }

        private static GameObject ResolveManagersRoot()
        {
            GameObject runtimeOwner = null;
            WorldRuntimeReferenceUtility.TryResolveManagersRoot(ref runtimeOwner);
            if (runtimeOwner != null)
                return runtimeOwner;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            // COLD ALLOC: GameObject[1] - runtime fail-safe owner for missing onboarding directors - owner: WorldReadabilityRuntimeBootstrap
            return new GameObject("WorldReadabilityDirector_Root");
        }
    }
}