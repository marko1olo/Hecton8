using Hecton8.World;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Cold-path installer for scene-level meta progression systems.
    /// </summary>
    public static class MetaRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_META_RUNTIME";

        private const string MetaCampaignServiceTypeName =
            "Hecton8.Narrative.Campaign.MetaCampaignService, Hecton8.Narrative.Campaign";

        private const string MetaCampaignServiceMissingWarning =
            "[MetaRuntimeInstaller] Hecton8.Narrative.Campaign.MetaCampaignService did not resolve by name. Meta campaign progression, its save identity, and its tick routes are absent this run.";

        /// <summary>
        /// Ensures global profile and dynamic difficulty owners exist in the active gameplay scene.
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            // Same defect as EcosystemRuntimeInstaller carried: this body sat inside
            // "#if UNITY_EDITOR || DEVELOPMENT_BUILD ... #else _ = runtimeRoot; #endif", so a player
            // build got none of these owners and this method still returned cleanly - a silent
            // null-object production fallback, forbidden by
            // .agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt Section8.
            //
            // Nothing here is an editor concern. GlobalProfileManager (:23), DynamicDifficultyDirector
            // (:18), RunModifierController (:15) and MetaBuffInjector (:18) are all declared outside any
            // preprocessor directive. GlobalProfileManager's own guards at :1085, :1149 and :1204 are
            // narrow, internal, and already closed around their own statements.
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            if (!runtimeRoot.TryGetComponent<GlobalProfileManager>(out _))
                runtimeRoot.AddComponent<GlobalProfileManager>();

            if (!runtimeRoot.TryGetComponent<DynamicDifficultyDirector>(out _))
                runtimeRoot.AddComponent<DynamicDifficultyDirector>();

            if (!runtimeRoot.TryGetComponent<RunModifierController>(out _))
                runtimeRoot.AddComponent<RunModifierController>();

            if (!runtimeRoot.TryGetComponent<MetaBuffInjector>(out _))
                runtimeRoot.AddComponent<MetaBuffInjector>();

            EnsureMetaCampaignService(runtimeRoot);
        }

        /// <summary>
        /// Resolves the meta runtime root across loaded scenes, creating it when no scene owns one, and
        /// guarantees it is live so the installs above actually run Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the meta owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for meta systems per gameplay scene - owner: MetaRuntimeInstaller

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

        /// <summary>
        /// Installs the campaign service owner, which lives in an assembly this one cannot reference.
        /// </summary>
        /// <param name="runtimeRoot">Active meta runtime root.</param>
        private static void EnsureMetaCampaignService(GameObject runtimeRoot)
        {
            // Hecton8.Narrative.Campaign.asmdef ships on every platform (includePlatforms is empty) but
            // sets autoReferenced:false, so no direct reference exists and the type is resolved by name
            // exactly once on the cold boot route. This is not a hot path and not a self-heal: the
            // bootstrapper owns the call order (GameBootstrapper.cs:7813).
            System.Type serviceType = System.Type.GetType(MetaCampaignServiceTypeName);
            if (serviceType == null)
            {
                // Assets/link.xml now roots this assembly (preserve="nothing" on the assembly plus an
                // explicit MetaCampaignService type child), which is what makes it survive IL2CPP
                // managed stripping: nothing references Hecton8.Narrative.Campaign, no scene or prefab
                // holds one of its script GUIDs, and autoReferenced is false, so without that entry it
                // is not a linker root at all and a type reached only by name cannot come back.
                // Assets/_Project/Scripts/Global/Generated/link.xml still does not preserve it and must
                // not be hand-edited - CompileWallXRayWindow.cs regenerates it. If the Assets/link.xml
                // entry is ever removed, this branch is the only thing that will report the loss.
                // Losing it silently is what Section8 rejects; say so in every configuration
                // that can still print.
                Hecton8.Core.H8Debug.LogWarning(MetaCampaignServiceMissingWarning, runtimeRoot);
                return;
            }

            if (!runtimeRoot.TryGetComponent(serviceType, out _))
                runtimeRoot.AddComponent(serviceType);
        }
    }
}
