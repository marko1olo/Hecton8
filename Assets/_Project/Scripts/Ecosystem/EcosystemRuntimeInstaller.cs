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
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for ecosystem systems per gameplay scene - owner: EcosystemRuntimeInstaller

            if (runtimeRoot.GetComponent<FaunaGeneticsManager>() == null)
                runtimeRoot.AddComponent<FaunaGeneticsManager>();

            if (runtimeRoot.GetComponent<EcosystemHealthDirector>() == null)
                runtimeRoot.AddComponent<EcosystemHealthDirector>();

            if (runtimeRoot.GetComponent<MigrationDirector>() == null)
                runtimeRoot.AddComponent<MigrationDirector>();
        }
    }
}
