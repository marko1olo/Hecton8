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
            // The install below is deliberately unguarded. It previously sat inside
            // "#if UNITY_EDITOR || DEVELOPMENT_BUILD ... #else _ = runtimeRoot; #endif", so a player
            // build discarded the resolved root and shipped with none of these four owners while this
            // method still returned cleanly. That is a silent null-object production fallback with no
            // degraded telemetry and no named disabled capability, which
            // .agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt Section8 forbids outright.
            //
            // Nothing inside the four installs is an editor concern. FaunaGeneticsManager (:19),
            // EcosystemHealthDirector (:16), MigrationDirector (:25) and EcosystemPopulationBalancer
            // (:21) are all declared outside any preprocessor directive, so every one of them compiles
            // into a player build. The only editor-gated code anywhere in that set is the designer JSON
            // coefficient override in EcosystemPopulationBalancer.cs:355-362 and :372-434, and it
            // already ships a working player fallback: EcosystemPopulationCoefficient.CreateDefault()
            // plus TelemetryFallbackCoefficientsFlag. That fallback is the disproof of the guard, not a
            // reason for it.
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            if (!runtimeRoot.TryGetComponent<FaunaGeneticsManager>(out _))
                runtimeRoot.AddComponent<FaunaGeneticsManager>();

            if (!runtimeRoot.TryGetComponent<EcosystemHealthDirector>(out _))
                runtimeRoot.AddComponent<EcosystemHealthDirector>();

            if (!runtimeRoot.TryGetComponent<MigrationDirector>(out _))
                runtimeRoot.AddComponent<MigrationDirector>();

            if (!runtimeRoot.TryGetComponent<EcosystemPopulationBalancer>(out _))
                runtimeRoot.AddComponent<EcosystemPopulationBalancer>();

            ShinobuEcosystemBalancer.EnsureRuntimeService();
        }

        /// <summary>
        /// Resolves the ecosystem runtime root across loaded scenes, creating it when no scene owns one,
        /// and guarantees it is live so the installs below actually run Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the ecosystem owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for ecosystem systems per gameplay scene - owner: EcosystemRuntimeInstaller

            // A resolved root can come back hidden or deactivated from an earlier scene state.
            // AddComponent on an inactive GameObject never runs Awake/OnEnable, so the owners would
            // exist and never reach the dispatcher - the same player-visible outcome as the guard this
            // change removes. WorldRuntimeReferenceUtility.TryResolveScenePath resolves scene roots, so
            // activeSelf is the whole hierarchy state here. Same handling as
            // PrologueOrbitSceneBootstrap.cs:186-193, which owns the identical __HECTON_* root shape.
            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            return runtimeRoot;
        }
    }
}
