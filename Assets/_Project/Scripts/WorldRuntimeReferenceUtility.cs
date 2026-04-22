using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using System;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Shared runtime reference helpers for world directors.
    /// Keeps player resolution aligned with bootstrap runtime state and reduces duplicated
    /// scene-wide fallback searches during runtime startup.
    /// </summary>
    internal static class WorldRuntimeReferenceUtility
    {
        private static Transform _CachedPlayerTransform;
        private static MapMagicBridge _CachedMapMagicBridge;
        private static ScavengePopulator _CachedScavengePopulator;
        private static HectonMapMagicVegetationBridge _CachedVegetationBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _CachedPlayerTransform = null;
            _CachedMapMagicBridge = null;
            _CachedScavengePopulator = null;
            _CachedVegetationBridge = null;
        }

        public static bool TryResolvePlayerTransform(ref Transform target)
        {
            if (target != null)
                return true;

            if (BootstrapState.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer))
            {
                _CachedPlayerTransform = bootstrapPlayer;
                target = bootstrapPlayer;
                return true;
            }

            if (_CachedPlayerTransform != null)
            {
                target = _CachedPlayerTransform;
                return true;
            }

            return false;
        }

        public static bool TryResolveSceneObject(ref Transform target, string relativePath)
        {
            if (target != null)
                return true;

            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer) ||
                bootstrapPlayer == null)
            {
                return false;
            }

            Transform root = bootstrapPlayer.root;
            if (root == null)
                return false;

            target = root.Find(relativePath);
            return target != null;
        }

        public static bool TryResolveSceneObject<T>(ref T target, string relativePath) where T : Component
        {
            if (target != null)
                return true;

            Transform transform = null;
            if (!TryResolveSceneObject(ref transform, relativePath) || transform == null)
                return false;

            target = transform.GetComponent<T>();
            return target != null;
        }

        public static bool TryResolveSceneObject(ref GameObject target, string relativePath)
        {
            if (target != null)
                return true;

            Transform transform = null;
            if (!TryResolveSceneObject(ref transform, relativePath) || transform == null)
                return false;

            target = transform.gameObject;
            return target != null;
        }

        public static bool TryResolveBiomeSamplerCache(ref BiomeSamplerCache target)
        {
            if (target != null)
                return true;

            target = BiomeSamplerCache.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveScatterBudgetController(ref ScatterBudgetController target)
        {
            if (target != null)
                return true;

            target = ScatterBudgetController.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldSliceDirector(ref WorldSliceDirector target)
        {
            if (target != null)
                return true;

            target = WorldSliceDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldZoneDirector(ref WorldZoneDirector target)
        {
            if (target != null)
                return true;

            target = WorldZoneDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldContentDirector(ref WorldContentDirector target)
        {
            if (target != null)
                return true;

            target = WorldContentDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveProximityColliderSystem(ref ProximityColliderSystem target)
        {
            if (target != null)
                return true;

            target = ProximityColliderSystem.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveBiomeMatrixDirector(ref BiomeMatrixDirector target)
        {
            if (target != null)
                return true;

            target = BiomeMatrixDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldProceduralStateRegistry(ref WorldProceduralStateRegistry target)
        {
            if (target != null)
                return true;

            target = WorldProceduralStateRegistry.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldFaunaSpawnRegistry(ref WorldFaunaSpawnRegistry target)
        {
            if (target != null)
                return true;

            target = WorldFaunaSpawnRegistry.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldProceduralFieldSampler(ref WorldProceduralFieldSampler target)
        {
            if (target != null)
                return true;

            target = WorldProceduralFieldSampler.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldProceduralFillDirector(ref WorldProceduralFillDirector target)
        {
            if (target != null)
                return true;

            target = WorldProceduralFillDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldCaveDirector(ref WorldCaveDirector target)
        {
            if (target != null)
                return true;

            target = WorldCaveDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveFaunaDirector(ref FaunaDirector target)
        {
            if (target != null)
                return true;

            target = FaunaDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldGenerativeGeologyService(ref WorldGenerativeGeologyService target)
        {
            if (target != null)
                return true;

            target = WorldGenerativeGeologyService.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldGenerativeGeologyIntegrationDirector(ref WorldGenerativeGeologyIntegrationDirector target)
        {
            if (target != null)
                return true;

            target = WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveWorldGenerativeGeologySeamExecutionDirector(ref WorldGenerativeGeologySeamExecutionDirector target)
        {
            if (target != null)
                return true;

            target = WorldGenerativeGeologySeamExecutionDirector.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveVoxelEngine(ref HectonVoxelEngine target)
        {
            if (target != null)
                return true;

            target = HectonVoxelEngine.ActiveRuntimeInstance;
            return target != null;
        }

        public static bool TryResolveMapMagicBridge(ref MapMagicBridge target)
        {
            if (target != null)
                return true;

            if (_CachedMapMagicBridge != null)
            {
                target = _CachedMapMagicBridge;
                return true;
            }

            target = MapMagicBridge.Instance;
            if (target != null)
                _CachedMapMagicBridge = target;
            return target != null;
        }

        public static bool TryResolveHectonMapMagicVegetationBridge(ref HectonMapMagicVegetationBridge target)
        {
            if (target != null)
                return true;

            if (_CachedVegetationBridge != null)
            {
                target = _CachedVegetationBridge;
                return true;
            }

            target = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (target != null)
                _CachedVegetationBridge = target;
            return target != null;
        }

        public static bool TryResolveScavengePopulator(ref ScavengePopulator target)
        {
            if (target != null)
                return true;

            if (_CachedScavengePopulator != null)
            {
                target = _CachedScavengePopulator;
                return true;
            }

            target = ScavengePopulator.Instance;
            if (target != null)
                _CachedScavengePopulator = target;
            return target != null;
        }
    }
}
