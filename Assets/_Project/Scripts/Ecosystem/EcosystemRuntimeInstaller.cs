using Hecton8.AI.Ecosystem;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Cold-path installer for scene-level ecosystem systems.
    /// </summary>
    public static class EcosystemRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_ECOSYSTEM_RUNTIME";

        /// <summary>
        /// Ensures genetics, infection, and migration ecosystem owners exist in the active gameplay scene.
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for ecosystem systems per gameplay scene - owner: EcosystemRuntimeInstaller

            if (!runtimeRoot.TryGetComponent<FaunaGeneticsManager>(out _))
                runtimeRoot.AddComponent<FaunaGeneticsManager>();

            if (!runtimeRoot.TryGetComponent<EcosystemHealthDirector>(out _))
                runtimeRoot.AddComponent<EcosystemHealthDirector>();

            if (!runtimeRoot.TryGetComponent<MigrationDirector>(out _))
                runtimeRoot.AddComponent<MigrationDirector>();

            if (!runtimeRoot.TryGetComponent<EcosystemPopulationBalancer>(out _))
                runtimeRoot.AddComponent<EcosystemPopulationBalancer>();
#else
            _ = runtimeRoot;
#endif

            ShinobuEcosystemBalancer.EnsureRuntimeService();
        }
    }
}
