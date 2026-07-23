using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class WorldProceduralProxySceneBuilder
    {
        private const string RuleFolder = "Assets/_Project/Data/World/ProceduralPlacementRules";
        private const string ProxyRootName = "__PROCEDURAL_PROXY_WORLD";
        private const float ProxySnapRaycastElevationMeters = 80f;
        private const float ProxySnapRaycastDistanceMeters = 240f;
        private const float ProxySnapMaxTiltDegrees = 35f;
        private const float ProxySnapMinimumNormalUpDot = 0.2f;
        private const string NativeMemoryOwner = nameof(WorldProceduralProxySceneBuilder);

        [MenuItem("Hecton8/Authoring/Rebuild Procedural Proxy Scene", priority = 179)]
        public static void RebuildProceduralProxyScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralProxySceneBuilder] No active loaded scene.");
                return;
            }

            WorldContentSocket[] sockets = UnityEngine.Object.FindObjectsByType<WorldContentSocket>(FindObjectsInactive.Exclude);
            if (sockets == null || sockets.Length == 0)
            {
                Debug.LogWarning("[WorldProceduralProxySceneBuilder] No WorldContentSocket objects found.");
                return;
            }

            List<WorldProceduralPlacementRule> rules = LoadRules();
            GameObject root = GetOrCreateRoot();
            ClearChildren(root.transform);

            int placedCount = 0;
            for (int i = 0; i < sockets.Length; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null)
                    continue;

                WorldZoneAnchor zone = socket.GetZoneAnchor();
                WorldProceduralPlacementRule rule = ResolveRule(socket, zone, rules);
                WorldPrefabFamilyProfile family = ResolveFamily(socket, zone, rule);
                if (family == null)
                    continue;

                GameObject zoneRoot = GetOrCreateChild(root.transform, zone != null ? zone.ZoneId : "zone.generic");
                GameObject familyRoot = GetOrCreateChild(zoneRoot.transform, family.familyId);
                int instanceCount = ResolveInstanceCount(family, rule, socket);
                for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
                {
                    CreateProxyInstance(familyRoot.transform, family, rule, zone, socket, instanceIndex, instanceCount);
                    placedCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"[WorldProceduralProxySceneBuilder] Rebuilt proxy scene with {placedCount} instances.");
        }

        private static List<WorldProceduralPlacementRule> LoadRules()
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldProceduralPlacementRule", new[] { RuleFolder });
            List<WorldProceduralPlacementRule> rules = new List<WorldProceduralPlacementRule>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WorldProceduralPlacementRule rule = AssetDatabase.LoadAssetAtPath<WorldProceduralPlacementRule>(path);
                if (rule != null)
                    rules.Add(rule);
            }

            return rules;
        }

        private static WorldProceduralPlacementRule ResolveRule(
            WorldContentSocket socket,
            WorldZoneAnchor zone,
            IReadOnlyList<WorldProceduralPlacementRule> rules)
        {
            if (socket == null)
                return null;

            HectonBiomeFamilyProfile biomeFamily = zone != null ? zone.DominantBiomeFamily : null;
            WorldProceduralPlacementRule bestRule = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < rules.Count; i++)
            {
                WorldProceduralPlacementRule rule = rules[i];
                if (rule == null || !rule.Matches(biomeFamily, zone, socket))
                    continue;

                float score = rule.densityScale;
                if (rule.familyProfile != null && socket.Profile != null && !string.IsNullOrWhiteSpace(socket.Profile.futurePrefabFamily))
                {
                    if (string.Equals(rule.familyProfile.familyId, socket.Profile.futurePrefabFamily, StringComparison.Ordinal) ||
                        string.Equals(rule.familyProfile.familyLabel, socket.Profile.futurePrefabFamily, StringComparison.Ordinal))
                    {
                        score += 1.5f;
                    }
                }

                if (bestRule == null || score > bestScore)
                {
                    bestRule = rule;
                    bestScore = score;
                }
            }

            return bestRule;
        }

        private static WorldPrefabFamilyProfile ResolveFamily(
            WorldContentSocket socket,
            WorldZoneAnchor zone,
            WorldProceduralPlacementRule rule)
        {
            if (rule != null && rule.familyProfile != null)
                return rule.familyProfile;

            if (socket != null && socket.Profile != null && !string.IsNullOrWhiteSpace(socket.Profile.futurePrefabFamily))
            {
                WorldPrefabFamilyProfile familyFromProfile = FindFamilyByKey(socket.Profile.futurePrefabFamily);
                if (familyFromProfile != null)
                    return familyFromProfile;
            }

            if (zone != null && zone.Profile != null)
            {
                string familyKey = socket != null ? socket.Kind switch
                {
                    WorldContentSocket.ContentKind.ResourcePickup => zone.Profile.nearInteractiveFamily,
                    WorldContentSocket.ContentKind.ResourceNode => zone.Profile.nearInteractiveFamily,
                    WorldContentSocket.ContentKind.HazardPoint => zone.Profile.midVisualFamily,
                    WorldContentSocket.ContentKind.CombatPoint => zone.Profile.midVisualFamily,
                    WorldContentSocket.ContentKind.Landmark => zone.Profile.farSilhouetteFamily,
                    _ => zone.Profile.midVisualFamily
                } : zone.Profile.midVisualFamily;

                WorldPrefabFamilyProfile familyFromZone = FindFamilyByKey(familyKey);
                if (familyFromZone != null)
                    return familyFromZone;
            }

            return MapSocketKindToDefaultFamily(socket);
        }

        private static WorldPrefabFamilyProfile MapSocketKindToDefaultFamily(WorldContentSocket socket)
        {
            if (socket == null)
                return null;

            string familyId = socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => "family.pocket.resource",
                WorldContentSocket.ContentKind.ResourceNode => "family.pocket.resource",
                WorldContentSocket.ContentKind.FabricationStation => "family.pocket.safe",
                WorldContentSocket.ContentKind.ServiceTarget => "family.service.scar",
                WorldContentSocket.ContentKind.PowerPoint => "family.route.power",
                WorldContentSocket.ContentKind.NavigationMarker => "family.cave.entrance",
                WorldContentSocket.ContentKind.HazardPoint => "family.pocket.hazard",
                WorldContentSocket.ContentKind.CombatPoint => "family.creature.spawn.predator",
                WorldContentSocket.ContentKind.Landmark => "family.landmark.spire",
                _ => "family.rock.cluster.medium"
            };

            return FindFamilyByKey(familyId);
        }

        private static WorldPrefabFamilyProfile FindFamilyByKey(string familyKey)
        {
            if (string.IsNullOrWhiteSpace(familyKey))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { "Assets/_Project/Data/World" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(path);
                if (family == null)
                    continue;

                if (string.Equals(family.familyId, familyKey, StringComparison.Ordinal) ||
                    string.Equals(family.familyLabel, familyKey, StringComparison.Ordinal))
                    return family;
            }

            return null;
        }

        private static int ResolveInstanceCount(WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule, WorldContentSocket socket)
        {
            int minCount = rule != null ? rule.minInstances : family.clusterCountMin;
            int maxCount = rule != null ? Mathf.Max(minCount, rule.maxInstances) : Mathf.Max(family.clusterCountMin, family.clusterCountMax);
            if (socket != null && socket.Kind == WorldContentSocket.ContentKind.Landmark)
                return 1;

            return Mathf.Clamp((minCount + maxCount) / 2, 1, 12);
        }

        private static void CreateProxyInstance(
            Transform parent,
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            int instanceIndex,
            int instanceCount)
        {
            WorldPrefabFamilyProfile.VariantEntry variant = ResolveVariant(family, instanceIndex);
            GameObject prefab = variant != null ? variant.prefab : null;
            GameObject instance;

            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (instance.TryGetComponent(out Collider collider))
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            instance.name = $"{family.familyLabel}_{instanceIndex:00}";
            instance.transform.SetParent(parent, false);
            Vector3 position = ResolvePosition(socket.transform.position, family, instanceIndex, instanceCount);
            Quaternion rotation = Quaternion.Euler(0f, ResolveYaw(socket, instanceIndex), 0f);
            if (TrySnapProxyToScatterSurface(position, out Vector3 snappedPosition, out Vector3 snappedNormal))
            {
                position = snappedPosition;
                rotation = ResolveTerrainAlignedRotation(rotation, snappedNormal);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            float scale = ResolveScaleMultiplier(family, variant, instanceIndex);
            instance.transform.localScale *= scale;

            if (!instance.TryGetComponent(out WorldProceduralProxyInstance metadata))
                metadata = instance.AddComponent<WorldProceduralProxyInstance>();

            metadata.Configure(
                family,
                rule,
                zone,
                socket,
                variant != null ? variant.variantId : $"{family.familyId}.generated",
                variant == null || variant.proxyOnly,
                0,
                instanceIndex);
        }

        private static bool TrySnapProxyToScatterSurface(Vector3 position, out Vector3 point, out Vector3 normal)
        {
            point = position;
            normal = Vector3.up;
            Vector3 origin = position + (Vector3.up * ProxySnapRaycastElevationMeters);
            float distance = ProxySnapRaycastElevationMeters + ProxySnapRaycastDistanceMeters;
            NativeArray<RaycastCommand> commands = AllocateTrackedNativeArray<RaycastCommand>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(commands));
            NativeArray<RaycastHit> hits = AllocateTrackedNativeArray<RaycastHit>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory, nameof(hits));

            try
            {
                commands[0] = new RaycastCommand(
                    origin,
                    Vector3.down,
                    new QueryParameters(HectonLayerMasks.DefaultRaycastLayerMask, false, QueryTriggerInteraction.Ignore),
                    distance);
                // COLD SYNC JOB: editor-only proxy rebuild uses the same batched down-snap primitive as runtime scatter snapping.
                JobHandle handle = RaycastCommand.ScheduleBatch(commands, hits, 1, default);
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                RaycastHit hit = hits[0];
                if (hit.collider == null)
                    return false;

                point = hit.point;
                normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
                return Vector3.Dot(normal, Vector3.up) >= ProxySnapMinimumNormalUpDot;
            }
            finally
            {
                DisposeTrackedNativeArray(ref commands);
                DisposeTrackedNativeArray(ref hits);
            }
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[WorldProceduralProxySceneBuilder] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[WorldProceduralProxySceneBuilder] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static NativeAllocationLifetime ResolveNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }

        private static Quaternion ResolveTerrainAlignedRotation(Quaternion yawRotation, Vector3 normal)
        {
            Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            float angle = Vector3.Angle(Vector3.up, safeNormal);
            if (angle > ProxySnapMaxTiltDegrees)
            {
                float t = ProxySnapMaxTiltDegrees / Mathf.Max(0.001f, angle);
                safeNormal = Vector3.Slerp(Vector3.up, safeNormal, t).normalized;
            }

            return Quaternion.FromToRotation(Vector3.up, safeNormal) * yawRotation;
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolveVariant(WorldPrefabFamilyProfile family, int instanceIndex)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return null;

            int variantIndex = Mathf.Abs(instanceIndex) % family.variants.Length;
            return family.variants[variantIndex];
        }

        private static Vector3 ResolvePosition(Vector3 origin, WorldPrefabFamilyProfile family, int instanceIndex, int instanceCount)
        {
            if (instanceCount <= 1)
                return origin;

            float angle = (360f / instanceCount) * instanceIndex;
            float radius = Mathf.Max(1.5f, family.clusterRadiusMeters * 0.42f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(radius, 0f, 0f);
            return origin + offset;
        }

        private static float ResolveYaw(WorldContentSocket socket, int instanceIndex)
        {
            int seed = (socket != null ? ComputeStableHash(socket.SocketId) : 17) ^ (instanceIndex * 397);
            seed = Mathf.Abs(seed);
            return seed % 360;
        }

        private static float ResolveScaleMultiplier(WorldPrefabFamilyProfile family, WorldPrefabFamilyProfile.VariantEntry variant, int instanceIndex)
        {
            Vector2 range = variant != null ? variant.uniformScaleRange : new Vector2(0.9f, 1.1f);
            if (range.x <= 0f && range.y <= 0f)
                range = new Vector2(0.9f, 1.1f);

            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            if (Mathf.Approximately(min, max))
                return Mathf.Max(0.1f, min);

            float t = ((instanceIndex * 73) % 100) / 99f;
            return Mathf.Lerp(min, max, t);
        }

        private static GameObject GetOrCreateRoot()
        {
            GameObject root = GameObject.Find(ProxyRootName);
            if (root == null)
                root = new GameObject(ProxyRootName);

            return root;
        }

        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                return child.gameObject;

            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        private static int ComputeStableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 17;

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];

                return hash;
            }
        }
    }
}
