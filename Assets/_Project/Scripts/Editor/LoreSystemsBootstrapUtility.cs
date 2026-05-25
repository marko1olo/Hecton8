// ============================================================================
// HECTON-8 - LoreSystemsBootstrapUtility.cs
// Editor utility for bootstrapping lore systems in scene.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Bootstrap;
using Hecton8.Quest;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class LoreSystemsBootstrapUtility
    {
        private const string kMenuPath = "Tools/Hecton8/Lore Systems/Bootstrap All";
        private const string kValidatePath = "Tools/Hecton8/Lore Systems/Validate";

        [MenuItem(kMenuPath, false, 100)]
        public static void BootstrapAllLoreSystems()
        {
            GameObject loreSystemsGo = GameObject.Find("--- SYSTEMS ---/LoreSystems");
            
            if (loreSystemsGo == null)
            {
                GameObject systemsRoot = GameObject.Find("--- SYSTEMS ---");
                if (systemsRoot == null)
                {
                    Debug.LogError("[LoreBootstrap] --- SYSTEMS --- root not found. Aborting.");
                    return;
                }

                loreSystemsGo = new GameObject("LoreSystems");
                loreSystemsGo.transform.SetParent(systemsRoot.transform, false);
                Undo.RegisterCreatedObjectUndo(loreSystemsGo, "Create LoreSystems");
            }

            if (!loreSystemsGo.TryGetComponent(out HectonLoreSystemsRoot root))
            {
                root = Undo.AddComponent<HectonLoreSystemsRoot>(loreSystemsGo);
            }

            Undo.RecordObject(root, "Setup Lore Systems");
            root.SetupAllSystems();
            EditorUtility.SetDirty(root);

            int found = root.GetFoundSystemCount();
            string missing = root.GetMissingSystemsSummary();

            if (found == HectonLoreSystemsRoot.ExpectedSystemCount)
            {
                Debug.Log($"[LoreBootstrap] SUCCESS: All {found} lore systems bootstrapped.");
            }
            else
            {
                Debug.LogWarning($"[LoreBootstrap] PARTIAL: {found}/{HectonLoreSystemsRoot.ExpectedSystemCount} systems. Missing: {missing}");
            }
        }

        [MenuItem(kValidatePath, false, 101)]
        public static void ValidateLoreSystems()
        {
            HectonLoreSystemsRoot root = Object.FindAnyObjectByType<HectonLoreSystemsRoot>();
            
            if (root == null)
            {
                Debug.LogError("[LoreBootstrap] HectonLoreSystemsRoot not found in scene. Run Bootstrap first.");
                return;
            }

            root.ValidateSystems();
            int found = root.GetFoundSystemCount();
            string missing = root.GetMissingSystemsSummary();

            Debug.Log($"[LoreBootstrap] Validation: {found}/{HectonLoreSystemsRoot.ExpectedSystemCount} systems. Missing: {missing}");
        }

        [MenuItem("Tools/Hecton8/Lore Systems/Populate All Registries", false, 150)]
        public static void PopulateAllRegistries()
        {
            PopulateQuestRegistry();
            PopulateSuitUpgradeRegistry();
            Debug.Log("[LoreBootstrap] All registries populated.");
        }

        private static void PopulateQuestRegistry()
        {
            var questManager = Object.FindAnyObjectByType<Hecton8.Quest.QuestManager>();
            if (questManager == null)
            {
                Debug.LogWarning("[LoreBootstrap] QuestManager not found.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:QuestData", new[] { "Assets/_Project/Data/Lore/Quests" });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[LoreBootstrap] No QuestData assets found.");
                return;
            }

            var quests = new System.Collections.Generic.List<Hecton8.Quest.QuestData>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var quest = AssetDatabase.LoadAssetAtPath<Hecton8.Quest.QuestData>(path);
                if (quest != null) quests.Add(quest);
            }

            if (quests.Count == 0) return;

            var so = new SerializedObject(questManager);
            var prop = so.FindProperty("allQuests");
            if (prop != null)
            {
                prop.arraySize = quests.Count;
                for (int i = 0; i < quests.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(questManager);
                Debug.Log($"[LoreBootstrap] QuestManager: {quests.Count} quests assigned.");
            }
        }

        private static void PopulateSuitUpgradeRegistry()
        {
            Hecton8.Gameplay.SuitUpgradeManager manager = Object.FindAnyObjectByType<Hecton8.Gameplay.SuitUpgradeManager>();
            if (manager == null)
            {
                Debug.LogWarning("[LoreBootstrap] SuitUpgradeManager not found.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:SuitUpgradeData", new[] { "Assets/_Project/Data/Lore/SuitUpgrades" });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[LoreBootstrap] No SuitUpgradeData assets found.");
                return;
            }

            // COLD ALLOC: List[16] - editor-only registry population scratch list - owner: LoreSystemsBootstrapUtility
            System.Collections.Generic.List<Hecton8.Gameplay.SuitUpgradeData> upgrades =
                new System.Collections.Generic.List<Hecton8.Gameplay.SuitUpgradeData>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                Hecton8.Gameplay.SuitUpgradeData upgrade =
                    AssetDatabase.LoadAssetAtPath<Hecton8.Gameplay.SuitUpgradeData>(assetPath);
                if (upgrade != null)
                    upgrades.Add(upgrade);
            }

            if (upgrades.Count == 0)
                return;

            SerializedObject serializedObject = new SerializedObject(manager);
            SerializedProperty property = serializedObject.FindProperty("allUpgrades");
            if (property == null)
            {
                Debug.LogError("[LoreBootstrap] SuitUpgradeManager field 'allUpgrades' not found.");
                return;
            }

            property.arraySize = upgrades.Count;
            for (int i = 0; i < upgrades.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            Debug.Log($"[LoreBootstrap] SuitUpgradeManager: {upgrades.Count} upgrades assigned.");
        }

        [MenuItem("Tools/Hecton8/Lore Systems/Create Quest Data Assets", false, 200)]
        public static void CreateQuestDataAssets()
        {
            const string folder = "Assets/_Project/Data/Lore/Quests";
            
            CreateQuestAsset(
                folder,
                "Quest_FirstBreath",
                "First Breath",
                "Take your first breath under water.",
                Hecton8.Quest.QuestTriggerType.OnDepthReached,
                string.Empty,
                10f,
                Hecton8.Quest.QuestCompletionType.OnDepthReached,
                string.Empty,
                50f,
                true);

            CreateQuestAsset(
                folder,
                "Quest_Arrival",
                "Arrival",
                "Survey the landing zone.",
                Hecton8.Quest.QuestTriggerType.Manual,
                string.Empty,
                0f,
                Hecton8.Quest.QuestCompletionType.OnDiscoveryMade,
                string.Empty,
                0f,
                false);

            CreateQuestAsset(
                folder,
                "Quest_SignalDetected",
                "Signal Detected",
                "Locate the source of the anomalous signal.",
                Hecton8.Quest.QuestTriggerType.OnDiscoveryMade,
                "atlas6_signal_identified",
                0f,
                Hecton8.Quest.QuestCompletionType.OnSignalDecoded,
                "atlas6_signal_fully_decoded",
                0f,
                false);

            AssetDatabase.SaveAssets();
            Debug.Log("[LoreBootstrap] Quest assets created or verified.");
        }

        private static void CreateQuestAsset(
            string folder, 
            string fileName, 
            string title, 
            string description,
            Hecton8.Quest.QuestTriggerType triggerType,
            string triggerId,
            float triggerValue,
            Hecton8.Quest.QuestCompletionType completionType,
            string completionId,
            float completionValue,
            bool autoActivate)
        {
            string path = $"{folder}/{fileName}.asset";
            
            Hecton8.Quest.QuestData existing = AssetDatabase.LoadAssetAtPath<Hecton8.Quest.QuestData>(path);
            if (existing != null)
                return;

            Hecton8.Quest.QuestData quest = ScriptableObject.CreateInstance<Hecton8.Quest.QuestData>();
            quest.questId = fileName.ToLowerInvariant();
            quest.displayTitle = title;
            quest.description = description;
            quest.triggerType = triggerType;
            quest.triggerId = triggerId;
            quest.triggerValue = triggerValue;
            quest.completionType = completionType;
            quest.completionId = completionId;
            quest.completionValue = completionValue;
            quest.autoActivateOnStart = autoActivate;

            AssetDatabase.CreateAsset(quest, path);
        }
    }
}
#endif
