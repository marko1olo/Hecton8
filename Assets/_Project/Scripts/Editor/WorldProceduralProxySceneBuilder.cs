using System;
using System.Collections.Generic;
using Hecton8.Environment;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralProxySceneBuilder
    {
        private const string RuleFolder = "Assets/_Project/Data/World/ProceduralPlacementRules";
        private const string ProxyRootName = "__PROCEDURAL_PROXY_WORLD";

        [MenuItem("Hecton/Authoring/Rebuild Procedural Proxy Scene", priority = 179)]
        public static void RebuildProceduralProxyScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralProxySceneBuilder] No active loaded scene.");
                return;
            }

            WorldContentSocket[] sockets = UnityEngine.Object.FindObjectsByType<WorldContentSocket>();
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

                WorldZoneAnchor zone = socket.GetComponentInParent<WorldZoneAnchor>();
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
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            instance.name = $"{family.familyLabel}_{instanceIndex:00}";
            instance.transform.SetParent(parent, false);
            instance.transform.position = ResolvePosition(socket.transform.position, family, instanceIndex, instanceCount);
            instance.transform.rotation = Quaternion.Euler(0f, ResolveYaw(socket, instanceIndex), 0f);
            float scale = ResolveScaleMultiplier(family, variant, instanceIndex);
            instance.transform.localScale *= scale;

            WorldProceduralProxyInstance metadata = instance.GetComponent<WorldProceduralProxyInstance>();
            if (metadata == null)
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
