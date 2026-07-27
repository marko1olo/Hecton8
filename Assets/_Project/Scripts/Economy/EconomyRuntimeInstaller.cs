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
            // Same defect as EcosystemRuntimeInstaller carried: this body sat inside
            // "#if UNITY_EDITOR || DEVELOPMENT_BUILD ... #else _ = runtimeRoot; #endif", so a player
            // build got none of these owners and this method still returned cleanly - a silent
            // null-object production fallback, forbidden by
            // .agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt Section8.
            //
            // Nothing here is an editor concern. ScrapManager (:18), ResourceScarcityDirector (:27),
            // TradeMarauderDirector (TradeMarauderRuntime.cs:1879) and EnvironmentalStrainManager (:16)
            // are all declared outside any preprocessor directive. TradeMarauderRuntime's guards at
            // :355-567, :640-642 and :2250-2272 are internal and do not enclose the class declaration.
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            if (!runtimeRoot.TryGetComponent<ScrapManager>(out _))
                runtimeRoot.AddComponent<ScrapManager>();

            if (!runtimeRoot.TryGetComponent<ResourceScarcityDirector>(out _))
                runtimeRoot.AddComponent<ResourceScarcityDirector>();

            if (!runtimeRoot.TryGetComponent<TradeMarauderDirector>(out _))
                runtimeRoot.AddComponent<TradeMarauderDirector>();

            if (!runtimeRoot.TryGetComponent<Hecton8.World.EnvironmentalStrainManager>(out _))
                runtimeRoot.AddComponent<Hecton8.World.EnvironmentalStrainManager>();
        }

        /// <summary>
        /// Resolves the economy runtime root across loaded scenes, creating it when no scene owns one,
        /// and guarantees it is live so the installs above actually run Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the economy owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for economy systems per gameplay scene - owner: EconomyRuntimeInstaller

            // A resolved root can come back hidden or deactivated from an earlier scene state.
            // AddComponent on an inactive GameObject never runs Awake/OnEnable, so the owners would
            // exist and never reach the dispatcher. TryResolveScenePath resolves scene roots, so
            // activeSelf is the whole hierarchy state here. Same handling as
            // PrologueOrbitSceneBootstrap.cs:186-193.
            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            return runtimeRoot;
        }
    }
}
