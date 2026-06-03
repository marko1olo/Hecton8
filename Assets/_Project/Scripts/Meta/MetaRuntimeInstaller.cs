using Hecton8.World;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: one runtime root for meta systems per gameplay scene - owner: MetaRuntimeInstaller

            if (!runtimeRoot.TryGetComponent<GlobalProfileManager>(out _))
                runtimeRoot.AddComponent<GlobalProfileManager>();

            if (!runtimeRoot.TryGetComponent<DynamicDifficultyDirector>(out _))
                runtimeRoot.AddComponent<DynamicDifficultyDirector>();

            if (!runtimeRoot.TryGetComponent<RunModifierController>(out _))
                runtimeRoot.AddComponent<RunModifierController>();

            if (!runtimeRoot.TryGetComponent<MetaBuffInjector>(out _))
                runtimeRoot.AddComponent<MetaBuffInjector>();

            EnsureMetaCampaignService(runtimeRoot);
#else
            _ = runtimeRoot;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void EnsureMetaCampaignService(GameObject runtimeRoot)
        {
            Type serviceType = Type.GetType("Hecton8.Narrative.Campaign.MetaCampaignService, Hecton8.Narrative.Campaign");
            if (serviceType == null)
                return;

            if (!runtimeRoot.TryGetComponent(serviceType, out _))
                runtimeRoot.AddComponent(serviceType);
        }
#endif
    }
}
