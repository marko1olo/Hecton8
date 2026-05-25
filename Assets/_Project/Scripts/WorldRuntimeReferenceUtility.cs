using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World
{
    /// <summary>
    /// Shared runtime reference helpers for world directors.
    /// Keeps player resolution aligned with bootstrap runtime state and reduces duplicated
    /// scene-wide fallback searches during runtime startup.
    /// </summary>
    public static class WorldRuntimeReferenceUtility
    {
        private static Transform _CachedPlayerTransform;
        private static MapMagicBridge _CachedMapMagicBridge;
        private static ScavengePopulator _CachedScavengePopulator;
        private static HectonMapMagicVegetationBridge _CachedVegetationBridge;
        // COLD ALLOC: List<GameObject>[32] — loaded-scene root traversal buffer for deterministic scene-path resolution — owner: WorldRuntimeReferenceUtility
        private static readonly List<GameObject> _SceneRootBuffer = new List<GameObject>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _CachedPlayerTransform = null;
            _CachedMapMagicBridge = null;
            _CachedScavengePopulator = null;
            _CachedVegetationBridge = null;
            _SceneRootBuffer.Clear();
        }

        public static bool TryResolvePlayerTransform(ref Transform target)
        {
            if (target != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform registryPlayer = playerContext != null ? playerContext.PlayerTransform : null;
            if (registryPlayer != null)
            {
                _CachedPlayerTransform = registryPlayer;
                target = registryPlayer;
                return true;
            }

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

            Transform bootstrapPlayer = null;
            if (!TryResolvePlayerTransform(ref bootstrapPlayer) || bootstrapPlayer == null)
            {
                return false;
            }

            Transform root = bootstrapPlayer.root;
            if (root == null)
                return false;

            target = root.Find(relativePath);
            return target != null;
        }

        public static bool TryResolveScenePath(ref Transform target, string scenePath)
        {
            if (target != null)
                return true;

            if (string.IsNullOrWhiteSpace(scenePath))
                return false;

            target = ResolveScenePath(scenePath);
            return target != null;
        }

        public static bool TryResolveSceneObject<T>(ref T target, string relativePath) where T : Component
        {
            if (target != null)
                return true;

            Transform transform = null;
            if (!TryResolveSceneObject(ref transform, relativePath) || transform == null)
                return false;

            transform.TryGetComponent(out target);
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

        public static bool TryResolveScenePath(ref GameObject target, string scenePath)
        {
            if (target != null)
                return true;

            Transform transform = null;
            if (!TryResolveScenePath(ref transform, scenePath) || transform == null)
                return false;

            target = transform.gameObject;
            return true;
        }

        public static bool TryResolveManagersRoot(ref GameObject target)
        {
            if (target != null)
                return true;

            if (TryResolveScenePath(ref target, "[MANAGERS]"))
                return true;

            return TryResolveScenePath(ref target, "--- SYSTEMS ---");
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

            target = GlobalRegistry.MapMagic;
            if (target != null)
                _CachedMapMagicBridge = target;
            return target != null;
        }

        public static bool TryResolveHectonMapMagicVegetationBridge(ref HectonMapMagicVegetationBridge target)
        {
            if (target != null)
                return true;

            HectonMapMagicVegetationBridge registered = GlobalRegistry.MapMagicVegetation;
            if (registered != null)
            {
                _CachedVegetationBridge = registered;
                target = registered;
                return true;
            }

            if (_CachedVegetationBridge != null && _CachedVegetationBridge.isActiveAndEnabled)
            {
                target = _CachedVegetationBridge;
                return true;
            }

            _CachedVegetationBridge = null;
            return false;
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

            target = GlobalRegistry.ScavengePopulator;
            if (target != null)
                _CachedScavengePopulator = target;
            return target != null;
        }

        private static Transform ResolveScenePath(string scenePath)
        {
            int segmentStart = 0;
            string rootSegment = ReadNextScenePathSegment(scenePath, ref segmentStart);
            if (string.IsNullOrEmpty(rootSegment))
                return null;

            Transform current = FindLoadedSceneRoot(rootSegment);
            while (current != null && segmentStart < scenePath.Length)
            {
                string segment = ReadNextScenePathSegment(scenePath, ref segmentStart);
                if (string.IsNullOrEmpty(segment))
                    continue;

                current = current.Find(segment);
            }

            return current;
        }

        private static Transform FindLoadedSceneRoot(string rootSegment)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                _SceneRootBuffer.Clear();
                scene.GetRootGameObjects(_SceneRootBuffer);
                for (int rootIndex = 0; rootIndex < _SceneRootBuffer.Count; rootIndex++)
                {
                    GameObject root = _SceneRootBuffer[rootIndex];
                    if (root != null && string.Equals(root.name, rootSegment, StringComparison.Ordinal))
                        return root.transform;
                }
            }

            return null;
        }

        private static string ReadNextScenePathSegment(string scenePath, ref int segmentStart)
        {
            while (segmentStart < scenePath.Length && scenePath[segmentStart] == '/')
                segmentStart++;

            if (segmentStart >= scenePath.Length)
                return null;

            int separatorIndex = scenePath.IndexOf('/', segmentStart);
            if (separatorIndex < 0)
            {
                string finalSegment = scenePath.Substring(segmentStart);
                segmentStart = scenePath.Length;
                return finalSegment;
            }

            string segment = scenePath.Substring(segmentStart, separatorIndex - segmentStart);
            segmentStart = separatorIndex + 1;
            return segment;
        }
    }
}
