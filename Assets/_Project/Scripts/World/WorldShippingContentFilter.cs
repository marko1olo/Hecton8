using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World
{
    internal static class WorldShippingContentFilter
    {
        private const string TrialZonePrefix = "zone.trial.";
        private const string TempHierarchyPrefix = "__TEMP_";
        private const string ToolStagingRootName = "Tool_Staging";
        private const string FabricationTrialRootName = "Fabrication_Trial";
        private const string ToolTrialRangeRootName = "Tool_TrialRange";

        private static readonly string[] _SuppressedHierarchyNames =
        {
            ToolStagingRootName,
            FabricationTrialRootName,
            ToolTrialRangeRootName
        };
        // COLD ALLOC: HashSet<int>[32] — suppressed hierarchy transform ids cached per active runtime scene — owner: WorldShippingContentFilter
        // COLD ALLOC: Dictionary<ulong, HashSet<EntityId>>[4] â€” suppressed hierarchy ids cached per loaded runtime scene â€” owner: WorldShippingContentFilter
        private static readonly Dictionary<ulong, HashSet<EntityId>> _suppressedHierarchyIdsByScene = new Dictionary<ulong, HashSet<EntityId>>(4);
        // COLD ALLOC: HashSet<ulong>[4] â€” scene handles whose suppression caches are fully primed â€” owner: WorldShippingContentFilter
        private static readonly HashSet<ulong> _primedSceneHandles = new HashSet<ulong>();
        // COLD ALLOC: List<GameObject>[64] — scene root buffer for suppression cache priming — owner: WorldShippingContentFilter
        private static readonly List<GameObject> _cacheRootObjects = new List<GameObject>(64);
        // COLD ALLOC: List<Transform>[512] — traversal stack for suppression cache priming — owner: WorldShippingContentFilter
        private static readonly List<Transform> _cacheTraversalStack = new List<Transform>(512);
        // COLD ALLOC: List<ulong>[8] â€” stale scene-handle removal scratch list â€” owner: WorldShippingContentFilter
        private static readonly List<ulong> _staleSceneHandles = new List<ulong>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Dictionary<ulong, HashSet<EntityId>>.Enumerator enumerator = _suppressedHierarchyIdsByScene.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ulong, HashSet<EntityId>> pair = enumerator.Current;
                if (pair.Value != null)
                    pair.Value.Clear();
            }

            _suppressedHierarchyIdsByScene.Clear();
            _primedSceneHandles.Clear();
            _cacheRootObjects.Clear();
            _cacheTraversalStack.Clear();
            _staleSceneHandles.Clear();
        }

        internal static bool IsSuppressedZone(WorldZoneAnchor anchor)
        {
            if (anchor == null)
                return true;

            return anchor.Kind == WorldZoneAnchor.ZoneKind.Trial ||
                   IsSuppressedZoneId(anchor.ZoneId) ||
                   IsSuppressedByHierarchy(anchor.transform);
        }

        internal static bool IsSuppressedSocket(WorldContentSocket socket)
        {
            if (socket == null)
                return true;

            WorldZoneAnchor zoneAnchor = socket.GetZoneAnchor();
            if (zoneAnchor != null && IsSuppressedZone(zoneAnchor))
                return true;

            return IsSuppressedByHierarchy(socket.transform);
        }

        internal static bool IsSuppressedZoneId(string zoneId)
        {
            return !string.IsNullOrWhiteSpace(zoneId) &&
                   zoneId.StartsWith(TrialZonePrefix, System.StringComparison.Ordinal);
        }

        internal static bool IsSuppressedByHierarchy(Transform target)
        {
            if (target == null)
                return false;

            Scene scene = target.gameObject.scene;
            EnsureSuppressionCacheForScene(scene);
            HashSet<EntityId> suppressedHierarchyIds = GetSuppressedHierarchyIds(scene, createIfMissing: false);
            if (suppressedHierarchyIds == null || suppressedHierarchyIds.Count == 0)
                return false;

            Transform current = target;
            while (current != null)
            {
                if (suppressedHierarchyIds.Contains(current.GetEntityId()))
                    return true;

                if (current != target &&
                    current.TryGetComponent(out WorldZoneAnchor zoneAnchor) &&
                    IsSuppressedZoneContract(zoneAnchor))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        internal static int DeactivateSuppressedSceneObjects(
            Scene scene,
            List<GameObject> rootObjects,
            List<Transform> traversalStack)
        {
            if (!scene.IsValid() || !scene.isLoaded || rootObjects == null || traversalStack == null)
                return 0;

            PrimeSuppressionCacheForScene(scene);
            HashSet<EntityId> suppressedHierarchyIds = GetSuppressedHierarchyIds(scene, createIfMissing: false);
            if (suppressedHierarchyIds == null || suppressedHierarchyIds.Count == 0)
            {
                rootObjects.Clear();
                traversalStack.Clear();
                return 0;
            }

            rootObjects.Clear();
            traversalStack.Clear();
            scene.GetRootGameObjects(rootObjects);

            int suppressedCount = 0;
            for (int i = 0; i < rootObjects.Count; i++)
            {
                GameObject root = rootObjects[i];
                if (root == null)
                    continue;

                traversalStack.Add(root.transform);
            }

            while (traversalStack.Count > 0)
            {
                int lastIndex = traversalStack.Count - 1;
                Transform current = traversalStack[lastIndex];
                traversalStack.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (currentObject == null)
                    continue;

                if (suppressedHierarchyIds.Contains(current.GetEntityId()))
                {
                    if (currentObject.activeSelf)
                    {
                        currentObject.SetActive(false);
                        suppressedCount++;
                    }

                    continue;
                }

                for (int i = current.childCount - 1; i >= 0; i--)
                {
                    Transform child = current.GetChild(i);
                    if (child != null)
                        traversalStack.Add(child);
                }
            }

            rootObjects.Clear();
            traversalStack.Clear();
            return suppressedCount;
        }

        private static bool IsSuppressedZoneContract(WorldZoneAnchor zoneAnchor)
        {
            return zoneAnchor != null &&
                   (zoneAnchor.Kind == WorldZoneAnchor.ZoneKind.Trial ||
                    IsSuppressedZoneId(zoneAnchor.ZoneId));
        }

        private static void EnsureSuppressionCacheForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            ulong sceneHandle = scene.handle.GetRawData();
            if (_primedSceneHandles.Contains(sceneHandle))
                return;

            PrimeSuppressionCacheForScene(scene);
        }

        private static HashSet<EntityId> GetSuppressedHierarchyIds(Scene scene, bool createIfMissing)
        {
            return GetSuppressedHierarchyIds(scene.handle.GetRawData(), createIfMissing);
        }

        private static HashSet<EntityId> GetSuppressedHierarchyIds(ulong sceneHandle, bool createIfMissing)
        {
            if (_suppressedHierarchyIdsByScene.TryGetValue(sceneHandle, out HashSet<EntityId> suppressedHierarchyIds))
                return suppressedHierarchyIds;

            if (!createIfMissing)
                return null;

            // COLD ALLOC: HashSet<EntityId>[32] â€” per-scene suppression membership cache â€” owner: WorldShippingContentFilter
            suppressedHierarchyIds = new HashSet<EntityId>(32);
            _suppressedHierarchyIdsByScene.Add(sceneHandle, suppressedHierarchyIds);
            return suppressedHierarchyIds;
        }

        private static void PrimeSuppressionCacheForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            PruneSceneCaches();

            ulong sceneHandle = scene.handle.GetRawData();
            if (_primedSceneHandles.Contains(sceneHandle))
                return;

            HashSet<EntityId> suppressedHierarchyIds = GetSuppressedHierarchyIds(sceneHandle, createIfMissing: true);
            suppressedHierarchyIds.Clear();

            _cacheRootObjects.Clear();
            _cacheTraversalStack.Clear();
            scene.GetRootGameObjects(_cacheRootObjects);

            for (int i = 0; i < _cacheRootObjects.Count; i++)
            {
                GameObject root = _cacheRootObjects[i];
                if (root == null)
                    continue;

                _cacheTraversalStack.Add(root.transform);
            }

            while (_cacheTraversalStack.Count > 0)
            {
                int lastIndex = _cacheTraversalStack.Count - 1;
                Transform current = _cacheTraversalStack[lastIndex];
                _cacheTraversalStack.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                if (IsSuppressedHierarchyName(current.name))
                {
                    suppressedHierarchyIds.Add(current.GetEntityId());
                    continue;
                }

                for (int i = current.childCount - 1; i >= 0; i--)
                {
                    Transform child = current.GetChild(i);
                    if (child != null)
                        _cacheTraversalStack.Add(child);
                }
            }

            _cacheRootObjects.Clear();
            _cacheTraversalStack.Clear();
            _primedSceneHandles.Add(sceneHandle);
        }

        private static void PruneSceneCaches()
        {
            if (_suppressedHierarchyIdsByScene.Count == 0)
                return;

            _staleSceneHandles.Clear();
            Dictionary<ulong, HashSet<EntityId>>.Enumerator enumerator = _suppressedHierarchyIdsByScene.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ulong, HashSet<EntityId>> pair = enumerator.Current;
                if (!IsSceneHandleLoaded(pair.Key))
                    _staleSceneHandles.Add(pair.Key);
            }

            for (int i = 0; i < _staleSceneHandles.Count; i++)
            {
                ulong staleSceneHandle = _staleSceneHandles[i];
                if (_suppressedHierarchyIdsByScene.TryGetValue(staleSceneHandle, out HashSet<EntityId> suppressedHierarchyIds))
                    suppressedHierarchyIds.Clear();

                _suppressedHierarchyIdsByScene.Remove(staleSceneHandle);
                _primedSceneHandles.Remove(staleSceneHandle);
            }

            _staleSceneHandles.Clear();
        }

        private static bool IsSceneHandleLoaded(ulong sceneHandle)
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && scene.handle.GetRawData() == sceneHandle)
                    return true;
            }

            return false;
        }

        private static bool IsSuppressedHierarchyName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            if (objectName.StartsWith(TempHierarchyPrefix, System.StringComparison.Ordinal))
                return true;

            for (int i = 0; i < _SuppressedHierarchyNames.Length; i++)
            {
                if (string.Equals(
                        objectName,
                        _SuppressedHierarchyNames[i],
                        System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
