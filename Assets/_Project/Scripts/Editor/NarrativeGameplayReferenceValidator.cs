using System.Collections.Generic;
using System.Text;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Validates authored mission and quest string references that are expected to resolve through runtime content catalogs.
    /// </summary>
    public static class NarrativeGameplayReferenceValidator
    {
        private const string ProjectDataRoot = "Assets/_Project/Data";
        private const string ItemCatalogRoot = "Assets/_Project/Data/Items";
        private const string ModuleCatalogRoot = "Assets/_Project/Data/Construction";
        private const string LoreRegistryRoot = "Assets/_Project/Data/Lore/Registries";
        private const string DepthZoneRoot = "Assets/_Project/Data/Lore/DepthZones";

        [MenuItem("Hecton/Validation/Validate Narrative Gameplay References", priority = 243)]
        public static void Validate()
        {
            int errorCount = 0;
            int warningCount = 0;

            ItemCatalog itemCatalog = LoadSingleAsset<ItemCatalog>("ItemCatalog", ItemCatalogRoot, ref errorCount);
            ModuleCatalog moduleCatalog = LoadSingleAsset<ModuleCatalog>("ModuleCatalog", ModuleCatalogRoot, ref errorCount);
            ColonistLoreRegistry loreRegistry = LoadSingleAsset<ColonistLoreRegistry>("ColonistLoreRegistry", LoreRegistryRoot, ref errorCount);

            ValidateQuestAssets(itemCatalog, ref errorCount, ref warningCount);
            ValidateMissionAssets(itemCatalog, moduleCatalog, ref errorCount, ref warningCount);
            HashSet<string> loreDiscoveryIds = ValidateColonistLoreRegistry(loreRegistry, ref errorCount);
            ValidateDepthZoneAssets(loreDiscoveryIds, ref errorCount, ref warningCount);
            ValidateActiveSceneRelayRoutes(ref errorCount, ref warningCount);

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[NarrativeGameplayReferenceValidation] PASS no issues found.");
                return;
            }

            Debug.LogWarning($"[NarrativeGameplayReferenceValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static void ValidateQuestAssets(
            ItemCatalog itemCatalog,
            ref int errorCount,
            ref int warningCount)
        {
            string[] questGuids = AssetDatabase.FindAssets("t:QuestData", new[] { ProjectDataRoot });
            HashSet<string> questIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < questGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(questGuids[i]);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest == null)
                    continue;

                if (string.IsNullOrWhiteSpace(quest.questId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Quest missing questId: {path}", quest);
                    errorCount++;
                }
                else if (!questIds.Add(quest.questId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Duplicate questId '{quest.questId}': {path}", quest);
                    errorCount++;
                }

                ValidateQuestReference(path, quest, true, quest.triggerType, quest.triggerId, itemCatalog, ref errorCount, ref warningCount);
                ValidateQuestReference(path, quest, false, quest.completionType, quest.completionId, itemCatalog, ref errorCount, ref warningCount);
            }
        }

        private static void ValidateQuestReference(
            string path,
            QuestData quest,
            bool isTrigger,
            object questPhaseType,
            string referenceId,
            ItemCatalog itemCatalog,
            ref int errorCount,
            ref int warningCount)
        {
            string phaseLabel = isTrigger ? "trigger" : "completion";

            if (questPhaseType is QuestTriggerType triggerType)
            {
                if (triggerType == QuestTriggerType.Manual)
                {
                    if (!string.IsNullOrWhiteSpace(referenceId))
                    {
                        Debug.LogWarning(
                            $"[NarrativeGameplayReferenceValidation] Quest '{quest.questId}' has manual {phaseLabel} with unused id '{referenceId}': {path}",
                            quest);
                        warningCount++;
                    }

                    return;
                }

                if (triggerType == QuestTriggerType.OnItemCollected)
                {
                    ValidateItemReference(path, quest, quest.questId, $"{phaseLabel}Id", referenceId, itemCatalog, ref errorCount);
                }

                return;
            }

            QuestCompletionType completionType = (QuestCompletionType)questPhaseType;
            if (completionType == QuestCompletionType.Manual)
            {
                if (!string.IsNullOrWhiteSpace(referenceId))
                {
                    Debug.LogWarning(
                        $"[NarrativeGameplayReferenceValidation] Quest '{quest.questId}' has manual {phaseLabel} with unused id '{referenceId}': {path}",
                        quest);
                    warningCount++;
                }

                return;
            }

            if (completionType == QuestCompletionType.OnItemCollected)
                ValidateItemReference(path, quest, quest.questId, $"{phaseLabel}Id", referenceId, itemCatalog, ref errorCount);
        }

        private static void ValidateMissionAssets(
            ItemCatalog itemCatalog,
            ModuleCatalog moduleCatalog,
            ref int errorCount,
            ref int warningCount)
        {
            string[] missionGuids = AssetDatabase.FindAssets("t:MissionData", new[] { ProjectDataRoot });
            HashSet<string> missionIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < missionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(missionGuids[i]);
                MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(path);
                if (mission == null)
                    continue;

                if (string.IsNullOrWhiteSpace(mission.missionId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Mission missing missionId: {path}", mission);
                    errorCount++;
                }
                else if (!missionIds.Add(mission.missionId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Duplicate missionId '{mission.missionId}': {path}", mission);
                    errorCount++;
                }

                ValidateMissionObjectives(path, mission, itemCatalog, moduleCatalog, ref errorCount, ref warningCount);
                ValidateMissionRewards(path, mission, itemCatalog, ref errorCount);
            }
        }

        private static void ValidateMissionObjectives(
            string path,
            MissionData mission,
            ItemCatalog itemCatalog,
            ModuleCatalog moduleCatalog,
            ref int errorCount,
            ref int warningCount)
        {
            if (mission.objectives == null)
                return;

            HashSet<string> objectiveIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < mission.objectives.Count; i++)
            {
                ObjectiveData objective = mission.objectives[i];
                if (objective == null)
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' has null objective at index {i}: {path}", mission);
                    errorCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(objective.objectiveId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' has objective with empty objectiveId at index {i}: {path}", mission);
                    errorCount++;
                }
                else if (!objectiveIds.Add(objective.objectiveId))
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' has duplicate objectiveId '{objective.objectiveId}': {path}",
                        mission);
                    errorCount++;
                }

                switch (objective.type)
                {
                    case ObjectiveData.ObjectiveType.CollectItem:
                        ValidateItemReference(path, mission, mission.missionId, $"objective[{i}].targetId", objective.targetId, itemCatalog, ref errorCount);
                        break;
                    case ObjectiveData.ObjectiveType.BuildModule:
                        ValidateModuleReference(path, mission, mission.missionId, $"objective[{i}].targetId", objective.targetId, moduleCatalog, ref errorCount);
                        break;
                    case ObjectiveData.ObjectiveType.Manual:
                        if (!string.IsNullOrWhiteSpace(objective.targetId))
                        {
                            Debug.LogWarning(
                                $"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' manual objective '{objective.objectiveId}' has unused targetId '{objective.targetId}': {path}",
                                mission);
                            warningCount++;
                        }

                        break;
                }
            }
        }

        private static void ValidateMissionRewards(
            string path,
            MissionData mission,
            ItemCatalog itemCatalog,
            ref int errorCount)
        {
            if (mission.rewards == null)
                return;

            for (int i = 0; i < mission.rewards.Count; i++)
            {
                RewardData reward = mission.rewards[i];
                if (reward == null)
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' has null reward at index {i}: {path}", mission);
                    errorCount++;
                    continue;
                }

                if (reward.type != RewardData.RewardType.Item)
                    continue;

                ValidateItemReference(path, mission, mission.missionId, $"reward[{i}].itemId", reward.itemId, itemCatalog, ref errorCount);
                if (reward.count <= 0)
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Mission '{mission.missionId}' has item reward '{reward.itemId}' with non-positive count {reward.count}: {path}",
                        mission);
                    errorCount++;
                }
            }
        }

        private static HashSet<string> ValidateColonistLoreRegistry(
            ColonistLoreRegistry loreRegistry,
            ref int errorCount)
        {
            HashSet<string> loreDiscoveryIds = new HashSet<string>(System.StringComparer.Ordinal);
            if (loreRegistry == null)
                return loreDiscoveryIds;

            LoreEntry[] entries = loreRegistry.entries;
            if (entries == null)
                return loreDiscoveryIds;

            string registryPath = AssetDatabase.GetAssetPath(loreRegistry);
            for (int i = 0; i < entries.Length; i++)
            {
                string discoveryId = entries[i].discoveryId;
                if (string.IsNullOrWhiteSpace(discoveryId))
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] ColonistLoreRegistry entry[{i}] has empty discoveryId: {registryPath}",
                        loreRegistry);
                    errorCount++;
                    continue;
                }

                if (!loreDiscoveryIds.Add(discoveryId))
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] ColonistLoreRegistry has duplicate discoveryId '{discoveryId}' at entry[{i}]: {registryPath}",
                        loreRegistry);
                    errorCount++;
                }
            }

            return loreDiscoveryIds;
        }

        private static void ValidateDepthZoneAssets(
            HashSet<string> loreDiscoveryIds,
            ref int errorCount,
            ref int warningCount)
        {
            string[] zoneGuids = AssetDatabase.FindAssets("t:DepthZoneProfile", new[] { DepthZoneRoot });
            HashSet<string> zoneIds = new HashSet<string>(System.StringComparer.Ordinal);
            HashSet<string> zoneDiscoveryIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < zoneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(zoneGuids[i]);
                DepthZoneProfile zone = AssetDatabase.LoadAssetAtPath<DepthZoneProfile>(path);
                if (zone == null)
                    continue;

                if (string.IsNullOrWhiteSpace(zone.zoneId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Depth zone missing zoneId: {path}", zone);
                    errorCount++;
                }
                else if (!zoneIds.Add(zone.zoneId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Duplicate depth zone zoneId '{zone.zoneId}': {path}", zone);
                    errorCount++;
                }

                if (string.IsNullOrWhiteSpace(zone.discoveryId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Depth zone '{zone.zoneId}' missing discoveryId: {path}", zone);
                    errorCount++;
                }
                else if (!zoneDiscoveryIds.Add(zone.discoveryId))
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Duplicate depth zone discoveryId '{zone.discoveryId}': {path}",
                        zone);
                    errorCount++;
                }
                else if (loreDiscoveryIds != null &&
                         loreDiscoveryIds.Count > 0 &&
                         !loreDiscoveryIds.Contains(zone.discoveryId))
                {
                    Debug.LogWarning(
                        $"[NarrativeGameplayReferenceValidation] Depth zone '{zone.zoneId}' discoveryId '{zone.discoveryId}' does not resolve in ColonistLoreRegistry: {path}",
                        zone);
                    warningCount++;
                }

                if (zone.maxDepth <= zone.minDepth)
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Depth zone '{zone.zoneId}' has invalid depth range min={zone.minDepth} max={zone.maxDepth}: {path}",
                        zone);
                    errorCount++;
                }
            }
        }

        private static void ValidateActiveSceneRelayRoutes(ref int errorCount, ref int warningCount)
        {
            EmergencyServiceRelay[] relays =
                Object.FindObjectsByType<EmergencyServiceRelay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (relays == null || relays.Length <= 0)
                return;

            HashSet<string> relayIds = new HashSet<string>(System.StringComparer.Ordinal);
            Dictionary<string, HashSet<int>> relayOrdersByChain =
                new Dictionary<string, HashSet<int>>(System.StringComparer.Ordinal);

            for (int i = 0; i < relays.Length; i++)
            {
                EmergencyServiceRelay relay = relays[i];
                if (relay == null || EditorUtility.IsPersistent(relay))
                    continue;

                string relayPath = GetSceneObjectPath(relay.transform);
                if (string.IsNullOrWhiteSpace(relay.RelayId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Relay missing relayId: {relayPath}", relay);
                    errorCount++;
                }
                else if (!relayIds.Add(relay.RelayId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Duplicate relayId '{relay.RelayId}': {relayPath}", relay);
                    errorCount++;
                }

                if (string.IsNullOrWhiteSpace(relay.ChainId))
                {
                    Debug.LogError($"[NarrativeGameplayReferenceValidation] Relay '{relay.RelayId}' missing chainId: {relayPath}", relay);
                    errorCount++;
                }
                else
                {
                    if (!relayOrdersByChain.TryGetValue(relay.ChainId, out HashSet<int> chainOrders))
                    {
                        chainOrders = new HashSet<int>();
                        relayOrdersByChain.Add(relay.ChainId, chainOrders);
                    }

                    if (!chainOrders.Add(relay.RelayOrder))
                    {
                        Debug.LogError(
                            $"[NarrativeGameplayReferenceValidation] Relay chain '{relay.ChainId}' has duplicate relayOrder {relay.RelayOrder}: {relayPath}",
                            relay);
                        errorCount++;
                    }
                }

                EmergencyServiceRelay nextRelay = relay.NextRelay;
                if (nextRelay == null)
                    continue;

                if (nextRelay == relay)
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Relay '{relay.RelayId}' points nextRelay to itself: {relayPath}",
                        relay);
                    errorCount++;
                    continue;
                }

                if (!string.Equals(relay.ChainId, nextRelay.ChainId, System.StringComparison.Ordinal))
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Relay '{relay.RelayId}' nextRelay crosses chain boundary to '{nextRelay.RelayId}': {relayPath}",
                        relay);
                    errorCount++;
                }

                if (nextRelay.RelayOrder <= relay.RelayOrder)
                {
                    Debug.LogError(
                        $"[NarrativeGameplayReferenceValidation] Relay '{relay.RelayId}' nextRelay '{nextRelay.RelayId}' does not advance relayOrder: {relayPath}",
                        relay);
                    errorCount++;
                }

                if (EditorUtility.IsPersistent(nextRelay))
                {
                    Debug.LogWarning(
                        $"[NarrativeGameplayReferenceValidation] Relay '{relay.RelayId}' nextRelay references a persistent asset/prefab object. Verify scene instance wiring: {relayPath}",
                        relay);
                    warningCount++;
                }
            }
        }

        private static void ValidateItemReference(
            string path,
            Object context,
            string ownerId,
            string fieldLabel,
            string itemId,
            ItemCatalog itemCatalog,
            ref int errorCount)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] '{ownerId}' has empty {fieldLabel}: {path}", context);
                errorCount++;
                return;
            }

            if (itemCatalog == null)
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] ItemCatalog missing while validating '{ownerId}' {fieldLabel}: {path}", context);
                errorCount++;
                return;
            }

            if (itemCatalog.FindById(itemId) == null)
            {
                Debug.LogError(
                    $"[NarrativeGameplayReferenceValidation] '{ownerId}' references unknown item id '{itemId}' in {fieldLabel}: {path}",
                    context);
                errorCount++;
            }
        }

        private static void ValidateModuleReference(
            string path,
            Object context,
            string ownerId,
            string fieldLabel,
            string moduleId,
            ModuleCatalog moduleCatalog,
            ref int errorCount)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] '{ownerId}' has empty {fieldLabel}: {path}", context);
                errorCount++;
                return;
            }

            if (moduleCatalog == null)
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] ModuleCatalog missing while validating '{ownerId}' {fieldLabel}: {path}", context);
                errorCount++;
                return;
            }

            if (moduleCatalog.FindDataById(moduleId) == null)
            {
                Debug.LogError(
                    $"[NarrativeGameplayReferenceValidation] '{ownerId}' references unknown module id '{moduleId}' in {fieldLabel}: {path}",
                    context);
                errorCount++;
            }
        }

        private static T LoadSingleAsset<T>(string label, string root, ref int errorCount) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root });
            if (guids.Length <= 0)
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] Missing {label} under '{root}'.");
                errorCount++;
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"[NarrativeGameplayReferenceValidation] Failed to load {label} at '{path}'.");
                errorCount++;
                return null;
            }

            if (guids.Length > 1)
            {
                Debug.LogWarning($"[NarrativeGameplayReferenceValidation] Multiple {label} assets found under '{root}'. Using '{path}'.");
            }

            return asset;
        }

        private static string GetSceneObjectPath(Transform target)
        {
            if (target == null)
                return "<null>";

            StringBuilder builder = new StringBuilder(128);
            BuildSceneObjectPath(target, builder);
            return builder.ToString();
        }

        private static void BuildSceneObjectPath(Transform target, StringBuilder builder)
        {
            if (target.parent != null)
            {
                BuildSceneObjectPath(target.parent, builder);
                builder.Append('/');
            }

            builder.Append(target.name);
        }
    }
}
