#if UNITY_EDITOR
using Hecton8.Quest;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Applies the QUEST_STATE_GRAPH_VALIDATOR repair set through AssetDatabase.
    /// </summary>
    public static class QuestGraphRepairUtility
    {
        private const string QuestFolder = "Assets/_Project/Data/Lore/Quests/";

        [MenuItem("Tools/Hecton8/Quest/Apply State Graph Validator Repairs", false, 250)]
        public static void ApplyQuestStateGraphValidatorRepairs()
        {
            int changed = 0;
            changed += ApplyQuestRepair(
                "Quest_Arrival.asset",
                QuestTriggerType.Manual,
                string.Empty,
                0f,
                QuestCompletionType.OnDiscoveryMade,
                "first_hour_exit_lifepod",
                0f,
                true);

            changed += ApplyQuestRepair(
                "Quest_BiomeSpine.asset",
                QuestTriggerType.OnBiomeEntered,
                string.Empty,
                1f,
                QuestCompletionType.OnBiomeEntered,
                string.Empty,
                1f,
                false);

            changed += ApplyQuestRepair(
                "Quest_CopperSample.asset",
                QuestTriggerType.OnDiscoveryMade,
                "first_hour_exit_lifepod",
                0f,
                QuestCompletionType.OnItemCollected,
                "Data_Copper",
                1f,
                false);

            changed += ApplyQuestRepair(
                "Quest_CoreReached.asset",
                QuestTriggerType.OnDepthReached,
                string.Empty,
                4800f,
                QuestCompletionType.OnDepthReached,
                string.Empty,
                4800f,
                false);

            changed += ApplyQuestRepair(
                "Quest_RadShield.asset",
                QuestTriggerType.OnDiscoveryMade,
                "radiation_critical_advisory",
                0f,
                QuestCompletionType.OnItemCollected,
                "Item_Equip_RadiationVeil",
                1f,
                false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QuestGraphRepairUtility] Applied QUEST_STATE_GRAPH_VALIDATOR repairs. Changed assets: " + changed);
        }

        private static int ApplyQuestRepair(
            string assetName,
            QuestTriggerType triggerType,
            string triggerId,
            float triggerValue,
            QuestCompletionType completionType,
            string completionId,
            float completionValue,
            bool autoActivateOnStart)
        {
            string path = QuestFolder + assetName;
            QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (quest == null)
            {
                Debug.LogError("[QuestGraphRepairUtility] Missing QuestData asset: " + path);
                return 0;
            }

            bool changed = false;
            changed |= Assign(ref quest.triggerType, triggerType);
            changed |= Assign(ref quest.triggerId, triggerId);
            changed |= Assign(ref quest.triggerValue, triggerValue);
            changed |= Assign(ref quest.completionType, completionType);
            changed |= Assign(ref quest.completionId, completionId);
            changed |= Assign(ref quest.completionValue, completionValue);
            changed |= Assign(ref quest.autoActivateOnStart, autoActivateOnStart);

            if (!changed)
                return 0;

            EditorUtility.SetDirty(quest);
            return 1;
        }

        private static bool Assign<T>(ref T current, T next)
        {
            if (Equals(current, next))
                return false;

            current = next;
            return true;
        }
    }
}
#endif
