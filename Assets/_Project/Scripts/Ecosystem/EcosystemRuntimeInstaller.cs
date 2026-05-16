using System;
using System.Reflection;
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
        private const string EcosystemPopulationBalancerTypeName = "Hecton8.AI.Ecosystem.EcosystemPopulationBalancer";

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

            AddComponentIfAvailable(runtimeRoot, EcosystemPopulationBalancerTypeName);
        }

        private static void AddComponentIfAvailable(GameObject runtimeRoot, string typeName)
        {
            Type componentType = ResolveType(typeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                return;

            if (runtimeRoot.GetComponent(componentType) == null)
                runtimeRoot.AddComponent(componentType);
        }

        private static Type ResolveType(string typeName)
        {
            Type type = Type.GetType(typeName, false);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
