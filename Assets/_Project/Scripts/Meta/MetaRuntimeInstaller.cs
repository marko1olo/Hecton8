using System;
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

        /// <summary>
        /// Ensures global profile and dynamic difficulty owners exist in the active gameplay scene.
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for meta systems per gameplay scene - owner: MetaRuntimeInstaller

            if (runtimeRoot.GetComponent<GlobalProfileManager>() == null)
                runtimeRoot.AddComponent<GlobalProfileManager>();

            if (runtimeRoot.GetComponent<DynamicDifficultyDirector>() == null)
                runtimeRoot.AddComponent<DynamicDifficultyDirector>();

            if (runtimeRoot.GetComponent<RunModifierController>() == null)
                runtimeRoot.AddComponent<RunModifierController>();

            if (runtimeRoot.GetComponent<MetaBuffInjector>() == null)
                runtimeRoot.AddComponent<MetaBuffInjector>();

            EnsureMetaCampaignService(runtimeRoot);
        }

        private static void EnsureMetaCampaignService(GameObject runtimeRoot)
        {
            Type serviceType = Type.GetType("Hecton8.Narrative.Campaign.MetaCampaignService, Hecton8.Narrative.Campaign");
            if (serviceType == null)
                return;

            if (runtimeRoot.GetComponent(serviceType) == null)
                runtimeRoot.AddComponent(serviceType);
        }
    }
}
