using Hecton8.World;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Cold-path installer for runtime economy systems.
    /// </summary>
    public static class EconomyRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_ECONOMY_RUNTIME";

        /// <summary>
        /// Ensures recycling, scarcity, and environmental strain owners exist in the active gameplay scene.
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for economy systems per gameplay scene - owner: EconomyRuntimeInstaller

            if (runtimeRoot.GetComponent<ScrapManager>() == null)
                runtimeRoot.AddComponent<ScrapManager>();

            if (runtimeRoot.GetComponent<ResourceScarcityDirector>() == null)
                runtimeRoot.AddComponent<ResourceScarcityDirector>();

            if (runtimeRoot.GetComponent<Hecton8.World.EnvironmentalStrainManager>() == null)
                runtimeRoot.AddComponent<Hecton8.World.EnvironmentalStrainManager>();
        }
    }
}
