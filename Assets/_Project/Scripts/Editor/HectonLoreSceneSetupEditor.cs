// ============================================================================
// HECTON-8 - HectonLoreSceneSetupEditor.cs
// Editor helper for quick lore bootstrap setup.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class HectonLoreSceneSetupEditor
    {
        [MenuItem("Hecton8/Lore/Setup Lore Systems in Scene")]
        public static void SetupLoreSystemsInScene()
        {
            // -executeMethod / CI: never open DisplayDialog in batchmode.
            bool batch = Application.isBatchMode;

            HectonLoreSystemsRoot existing = Object.FindAnyObjectByType<HectonLoreSystemsRoot>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.ValidateSystems();
                Debug.Log("[HectonLoreSceneSetupEditor] RESULT: PASS — HectonLoreSystemsRoot already exists in the scene.");
                if (!batch)
                {
                    EditorUtility.DisplayDialog(
                        "Lore Systems",
                        "HectonLoreSystemsRoot already exists in the scene.\nUse the inspector actions to validate or reconcile it.",
                        "OK");
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing.gameObject);
                }
                return;
            }

            GameObject go = new GameObject("LoreSystems");
            if (!batch)
                Undo.RegisterCreatedObjectUndo(go, "Create LoreSystems");

            HectonLoreSystemsRoot root = go.AddComponent<HectonLoreSystemsRoot>();
            root.SetupAllSystems();

            EditorSceneManager.MarkSceneDirty(go.scene);
            if (!batch)
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                EditorUtility.DisplayDialog(
                    "Lore Systems",
                    "LoreSystems created in the scene.\n\n" +
                    "Next steps:\n" +
                    "1. Create ScriptableObject assets under Data/Lore/\n" +
                    "2. Wire references in the inspector for each system\n" +
                    "3. Place AudioLogPickup and NarrativeDiscovery objects in the world\n\n" +
                    "See: Assets/_Project/Scripts/LORE_SYSTEMS_GUIDE.md",
                    "OK");
            }

            Debug.Log("[HectonLoreSceneSetupEditor] RESULT: PASS — LoreSystems created. See LORE_SYSTEMS_GUIDE.md for setup instructions.");
        }

        [MenuItem("Hecton8/Lore/Validate Lore Systems in Scene")]
        public static void ValidateLoreSystemsInScene()
        {
            // -executeMethod / CI: never open DisplayDialog in batchmode.
            bool batch = Application.isBatchMode;

            HectonLoreSystemsRoot existing = Object.FindAnyObjectByType<HectonLoreSystemsRoot>(FindObjectsInactive.Include);
            if (existing == null)
            {
                // Soft FAIL stays exit 0 under -quit; DisplayDialog only interactive.
                Debug.LogError("[HectonLoreSceneSetupEditor] RESULT: FAIL — No HectonLoreSystemsRoot exists in the active scene.");
                if (!batch)
                {
                    EditorUtility.DisplayDialog(
                        "Lore Systems",
                        "No HectonLoreSystemsRoot exists in the active scene.",
                        "OK");
                }
                return;
            }

            existing.ValidateSystems();
            Debug.Log("[HectonLoreSceneSetupEditor] RESULT: PASS — HectonLoreSystemsRoot validated.");
            if (!batch)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
            }
        }

        [MenuItem("Hecton8/Lore/Create Lore Data Folder Structure")]
        public static void CreateLoreDataFolders()
        {
            bool batch = Application.isBatchMode;

            string[] folders =
            {
                "Assets/_Project/Data/Lore",
                "Assets/_Project/Data/Lore/AudioLogs",
                "Assets/_Project/Data/Lore/Quests",
                "Assets/_Project/Data/Lore/DepthZones",
                "Assets/_Project/Data/Lore/SuitUpgrades",
                "Assets/_Project/Data/Lore/Registries"
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                    string name = System.IO.Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }

            AssetDatabase.Refresh();

            Debug.Log("[HectonLoreSceneSetupEditor] RESULT: PASS — Lore data folders ensured.");
            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Lore Data Folders",
                    "Folders created:\n" +
                    "• Data/Lore/AudioLogs - AudioLogData assets\n" +
                    "• Data/Lore/Quests - QuestData assets\n" +
                    "• Data/Lore/DepthZones - DepthZoneProfile assets\n" +
                    "• Data/Lore/SuitUpgrades - SuitUpgradeData assets\n" +
                    "• Data/Lore/Registries - registry ScriptableObjects",
                    "OK");
            }
        }

        [MenuItem("Hecton8/Lore/Open Lore Guide")]
        public static void OpenLoreGuide()
        {
            bool batch = Application.isBatchMode;
            string path = "Assets/_Project/Scripts/LORE_SYSTEMS_GUIDE.md";
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
            {
                if (!batch)
                    AssetDatabase.OpenAsset(asset);
                Debug.Log("[HectonLoreSceneSetupEditor] RESULT: PASS — Lore guide found: " + path);
            }
            else
            {
                Debug.LogError("[HectonLoreSceneSetupEditor] RESULT: FAIL — File not found: " + path);
                if (!batch)
                    EditorUtility.DisplayDialog("Not Found", $"File not found: {path}", "OK");
            }
        }

        [MenuItem("Hecton8/Lore/Create Default SO Assets")]
        public static void CreateDefaultSOAssets()
        {
            bool batch = Application.isBatchMode;
            int created = 0;

            created += CreateSOIfMissing<Hecton8.Narrative.ColonistLoreRegistry>(
                "Assets/_Project/Data/Lore/Registries/ColonistLoreRegistry.asset");

            created += CreateSOIfMissing<Hecton8.Narrative.FaunaLoreRegistry>(
                "Assets/_Project/Data/Lore/Registries/FaunaLoreRegistry.asset");

            created += CreateSOIfMissing<Hecton8.Narrative.DeepReachCorporationData>(
                "Assets/_Project/Data/Lore/Registries/DeepReachCorporationData.asset");

            created += CreateDepthZoneProfile("DepthZone_TheSpine",
                "THE SPINE / SHALLOW GRAVE", 0f, 100f, 0, 0.1f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheSpine.asset");

            created += CreateDepthZoneProfile("DepthZone_DrownedFactories",
                "THE DROWNED FACTORIES", 100f, 1500f, 1, 0.4f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_DrownedFactories.asset");

            created += CreateDepthZoneProfile("DepthZone_TheDropUpper",
                "THE DROP - UPPER", 1000f, 2500f, 2, 0.6f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheDropUpper.asset");

            created += CreateDepthZoneProfile("DepthZone_TheDropDeep",
                "THE DROP - DEEP ABYSS", 2500f, 4000f, 3, 0.8f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheDropDeep.asset");

            created += CreateDepthZoneProfile("DepthZone_ThermalFields",
                "THERMAL FIELDS - THE RIFT", 4000f, 5500f, 4, 1.0f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_ThermalFields.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier1",
                "HULL TIER 1 - to -500m", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 1,
                350f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier1.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Oxygen_Tier1",
                "OXYGEN TIER 1 - 8 min", Hecton8.Gameplay.SuitUpgradeCategory.Oxygen, 1,
                0f, 140f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Oxygen_Tier1.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier2",
                "HULL TIER 2 - to -1500m", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 2,
                1000f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier2.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier3",
                "HULL TIER 3 - to -3500m", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 3,
                3000f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier3.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier4",
                "HULL TIER 4 - to -5000m", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 4,
                4850f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier4.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[HectonLoreSceneSetupEditor] RESULT: PASS — Created {created} assets in Assets/_Project/Data/Lore/");
            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Lore Assets Created",
                    $"Created {created} assets in Assets/_Project/Data/Lore/\n\nAssign them in the relevant system inspectors.",
                    "OK");
            }
        }

        private static int CreateSOIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return 0;
            }

            T so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return 1;
        }

        private static int CreateDepthZoneProfile(string id, string displayName,
            float minDepth, float maxDepth, int requiredHullTier, float dangerLevel,
            string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Hecton8.World.DepthZoneProfile>(path) != null)
            {
                return 0;
            }

            Hecton8.World.DepthZoneProfile so = ScriptableObject.CreateInstance<Hecton8.World.DepthZoneProfile>();
            so.zoneId = id;
            so.displayName = displayName;
            so.minDepth = minDepth;
            so.maxDepth = maxDepth;
            so.requiredHullTier = requiredHullTier;
            so.dangerLevel = dangerLevel;
            so.discoveryId = "zone_" + id.ToLower().Replace(" ", "_");
            AssetDatabase.CreateAsset(so, path);
            return 1;
        }

        private static int CreateSuitUpgrade(string upgradeId, string displayName,
            Hecton8.Gameplay.SuitUpgradeCategory category, int tier,
            float deltaSafeDepth, float deltaMaxOxygen, float deltaMaxEnergy,
            float deltaMaxIntegrity, float deltaMinTemp, float deltaMaxTemp, float deltaRad,
            string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Hecton8.Gameplay.SuitUpgradeData>(path) != null)
            {
                return 0;
            }

            Hecton8.Gameplay.SuitUpgradeData so = ScriptableObject.CreateInstance<Hecton8.Gameplay.SuitUpgradeData>();
            so.upgradeId = upgradeId;
            so.displayName = displayName;
            so.category = category;
            so.tier = tier;
            so.deltaSafeDepth = deltaSafeDepth;
            so.deltaMaxOxygen = deltaMaxOxygen;
            so.deltaMaxEnergy = deltaMaxEnergy;
            so.deltaMaxIntegrity = deltaMaxIntegrity;
            so.deltaMinSafeTemp = deltaMinTemp;
            so.deltaMaxSafeTemp = deltaMaxTemp;
            so.deltaRadiationThreshold = deltaRad;
            AssetDatabase.CreateAsset(so, path);
            return 1;
        }
    }
}
#endif
