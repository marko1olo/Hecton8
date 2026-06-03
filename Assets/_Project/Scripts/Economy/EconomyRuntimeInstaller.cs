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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for economy systems per gameplay scene - owner: EconomyRuntimeInstaller

            if (!runtimeRoot.TryGetComponent<ScrapManager>(out _))
                runtimeRoot.AddComponent<ScrapManager>();

            if (!runtimeRoot.TryGetComponent<ResourceScarcityDirector>(out _))
                runtimeRoot.AddComponent<ResourceScarcityDirector>();

            if (!runtimeRoot.TryGetComponent<TradeMarauderDirector>(out _))
                runtimeRoot.AddComponent<TradeMarauderDirector>();

            if (!runtimeRoot.TryGetComponent<Hecton8.World.EnvironmentalStrainManager>(out _))
                runtimeRoot.AddComponent<Hecton8.World.EnvironmentalStrainManager>();
#else
            _ = runtimeRoot;
#endif
        }
    }
}
