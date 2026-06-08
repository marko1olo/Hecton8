using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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

        private static bool IsLiveBehaviour(Behaviour behaviour)
        {
            return behaviour != null && behaviour.isActiveAndEnabled;
        }

        private static bool IsLiveTransform(Transform transform)
        {
            return transform != null &&
                   transform.gameObject != null &&
                   transform.gameObject.activeInHierarchy;
        }

        private static bool TryResolveLiveActiveRuntime<T>(ref T target, T active) where T : Behaviour
        {
            if (!IsLiveBehaviour(active))
            {
                target = null;
                return false;
            }

            if (IsLiveBehaviour(target) && ReferenceEquals(target, active))
                return true;

            target = active;
            return true;
        }

        private static bool TryResolveLiveCachedActiveRuntime<T>(ref T target, ref T cache, T active) where T : Behaviour
        {
            if (!TryResolveLiveActiveRuntime(ref target, active))
            {
                cache = null;
                return false;
            }

            cache = target;
            return true;
        }

        public static bool TryResolvePlayerTransform(ref Transform target)
        {
            if (!TryResolveCurrentPlayerTransform(out Transform active))
            {
                target = null;
                _CachedPlayerTransform = null;
                return false;
            }

            if (ReferenceEquals(target, active))
                return true;

            _CachedPlayerTransform = active;
            target = active;
            return true;
        }

        private static bool TryResolveCurrentPlayerTransform(out Transform active)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform registryPlayer = playerContext != null ? playerContext.PlayerTransform : null;
            if (IsLiveTransform(registryPlayer))
            {
                active = registryPlayer;
                return true;
            }

            if (BootstrapState.TryGetCurrentPlayerTransform(out Transform bootstrapPlayer) &&
                IsLiveTransform(bootstrapPlayer))
            {
                active = bootstrapPlayer;
                return true;
            }

            active = null;
            return false;
        }

        public static void InvalidatePlayerTransformCache(Transform instance)
        {
            if (instance == null || ReferenceEquals(_CachedPlayerTransform, instance))
                _CachedPlayerTransform = null;
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
            return TryResolveLiveActiveRuntime(ref target, BiomeSamplerCache.ActiveRuntimeInstance);
        }

        public static bool TryResolveScatterBudgetController(ref ScatterBudgetController target)
        {
            return TryResolveLiveActiveRuntime(ref target, ScatterBudgetController.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldSliceDirector(ref WorldSliceDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldSliceDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldZoneDirector(ref WorldZoneDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldZoneDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldContentDirector(ref WorldContentDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldContentDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveProximityColliderSystem(ref ProximityColliderSystem target)
        {
            return TryResolveLiveActiveRuntime(ref target, ProximityColliderSystem.ActiveRuntimeInstance);
        }

        public static bool TryResolveBiomeMatrixDirector(ref BiomeMatrixDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, BiomeMatrixDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldProceduralStateRegistry(ref WorldProceduralStateRegistry target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldProceduralStateRegistry.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldProceduralScatterDirector(ref WorldProceduralScatterDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldProceduralScatterDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveEcosystemDirector(ref EcosystemDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, EcosystemDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveResourceDistributionDirector(ref ResourceDistributionDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, ResourceDistributionDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveDestructibleOrganicManager(ref DestructibleOrganicManager target)
        {
            return TryResolveLiveActiveRuntime(ref target, DestructibleOrganicManager.ActiveRuntimeInstance);
        }

        public static bool TryResolveAbyssalThermalManager(ref AbyssalThermalManager target)
        {
            return TryResolveLiveActiveRuntime(ref target, AbyssalThermalManager.ActiveRuntimeInstance);
        }

        public static bool TryResolveFloraInteractionManager(ref FloraInteractionManager target)
        {
            return TryResolveLiveActiveRuntime(ref target, FloraInteractionManager.ActiveRuntimeInstance);
        }

        public static bool TryResolveHectonUnderwaterVisuals(ref HectonUnderwaterVisuals target)
        {
            return TryResolveLiveActiveRuntime(ref target, HectonUnderwaterVisuals.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldReadabilityDirector(ref WorldReadabilityDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldReadabilityDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveEmergencyServiceRelayDirector(ref EmergencyServiceRelayDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, EmergencyServiceRelayDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldFaunaSpawnRegistry(ref WorldFaunaSpawnRegistry target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldFaunaSpawnRegistry.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldProceduralFieldSampler(ref WorldProceduralFieldSampler target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldProceduralFieldSampler.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldProceduralFillDirector(ref WorldProceduralFillDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldProceduralFillDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldCaveDirector(ref WorldCaveDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldCaveDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveFaunaDirector(ref FaunaDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, FaunaDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldGenerativeGeologyService(ref WorldGenerativeGeologyService target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologyService.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldGenerativeGeologyIntegrationDirector(ref WorldGenerativeGeologyIntegrationDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveWorldGenerativeGeologySeamExecutionDirector(ref WorldGenerativeGeologySeamExecutionDirector target)
        {
            return TryResolveLiveActiveRuntime(ref target, WorldGenerativeGeologySeamExecutionDirector.ActiveRuntimeInstance);
        }

        public static bool TryResolveVoxelEngine(ref HectonVoxelEngine target)
        {
            return TryResolveLiveActiveRuntime(ref target, HectonVoxelEngine.ActiveRuntimeInstance);
        }

        public static bool TryResolveSargassumGlobalDragManager(ref SargassumGlobalDragManager target)
        {
            return TryResolveLiveActiveRuntime(ref target, SargassumGlobalDragManager.Instance);
        }

        public static bool TryResolveSargassumCutManager(ref SargassumCutManager target)
        {
            return TryResolveLiveActiveRuntime(ref target, SargassumCutManager.Instance);
        }

        public static bool TryResolveSargassumMicroFaunaBoids(ref SargassumMicroFaunaBoids target)
        {
            return TryResolveLiveActiveRuntime(ref target, SargassumMicroFaunaBoids.Instance);
        }

        public static bool TryResolveSargassumDragReadModel(ref ISargassumDragReadModel target)
        {
            SargassumGlobalDragManager active = SargassumGlobalDragManager.Instance;
            if (!IsLiveBehaviour(active))
            {
                target = null;
                return false;
            }

            if (target is Behaviour targetBehaviour && IsLiveBehaviour(targetBehaviour))
            {
                if (ReferenceEquals(targetBehaviour, active))
                    return true;
            }

            target = active;
            return true;
        }

        public static bool TryResolveSargassumCutWriteService(ref ISargassumCutWriteService target)
        {
            SargassumCutManager active = SargassumCutManager.Instance;
            if (!IsLiveBehaviour(active))
            {
                target = null;
                return false;
            }

            if (target is Behaviour targetBehaviour && IsLiveBehaviour(targetBehaviour))
            {
                if (ReferenceEquals(targetBehaviour, active))
                    return true;
            }

            target = active;
            return true;
        }

        public static bool TryResolveMicroFaunaPresentationPulseSink(ref IMicroFaunaPresentationPulseSink target)
        {
            SargassumMicroFaunaBoids active = SargassumMicroFaunaBoids.Instance;
            if (!IsLiveBehaviour(active))
            {
                target = null;
                return false;
            }

            if (target is Behaviour targetBehaviour && IsLiveBehaviour(targetBehaviour))
            {
                if (ReferenceEquals(targetBehaviour, active))
                    return true;
            }

            target = active;
            return true;
        }

        public static bool TryResolveWorldResourceSpawnerReadModel(
            ref IWorldResourceSpawnerReadModel target,
            ref IWorldResourceSpawnerReadDependencySink dependencySink)
        {
            IWorldResourceSpawnerReadModel active = GlobalRegistry.WorldResourceSpawner;
            if (active == null)
            {
                target = null;
                dependencySink = null;
                return false;
            }

            if (!ReferenceEquals(target, active))
                target = active;

            dependencySink = active as IWorldResourceSpawnerReadDependencySink;
            return true;
        }

        public static bool TryResolveMapMagicBridge(ref MapMagicBridge target)
        {
            return TryResolveLiveCachedActiveRuntime(ref target, ref _CachedMapMagicBridge, MapMagicBridge.ActiveRuntimeInstance);
        }

        public static void InvalidateMapMagicBridgeCache(MapMagicBridge instance)
        {
            if (instance == null || ReferenceEquals(_CachedMapMagicBridge, instance))
                _CachedMapMagicBridge = null;
        }

        public static bool TryResolveHectonMapMagicVegetationBridge(ref HectonMapMagicVegetationBridge target)
        {
            return TryResolveLiveCachedActiveRuntime(ref target, ref _CachedVegetationBridge, HectonMapMagicVegetationBridge.ActiveRuntimeInstance);
        }

        public static void InvalidateHectonMapMagicVegetationBridgeCache(HectonMapMagicVegetationBridge instance)
        {
            if (instance == null || ReferenceEquals(_CachedVegetationBridge, instance))
                _CachedVegetationBridge = null;
        }

        public static bool TryResolveScavengePopulator(ref ScavengePopulator target)
        {
            if (target != null && target.IsRuntimeOwnerUsable)
                return true;

            target = null;

            ScavengePopulator registered = GlobalRegistry.ScavengePopulator;
            if (ReferenceEquals(_CachedScavengePopulator, registered) &&
                _CachedScavengePopulator != null &&
                _CachedScavengePopulator.IsRuntimeOwnerUsable)
            {
                target = _CachedScavengePopulator;
                return true;
            }

            if (registered != null && registered.IsRuntimeOwnerUsable)
            {
                _CachedScavengePopulator = registered;
                target = registered;
                return true;
            }

            _CachedScavengePopulator = null;
            return false;
        }

        public static void InvalidateScavengePopulatorCache(ScavengePopulator instance)
        {
            if (instance == null || ReferenceEquals(_CachedScavengePopulator, instance))
                _CachedScavengePopulator = null;
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
