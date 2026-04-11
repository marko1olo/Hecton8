// ============================================================================
// HECTON-8 — HectonLoreSceneSetupEditor.cs
// Editor утилита: быстрая настройка лорных систем в сцене.
//
// Меню: Hecton8 → Lore → Setup Lore Systems in Scene
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class HectonLoreSceneSetupEditor
    {
        [MenuItem("Hecton8/Lore/Setup Lore Systems in Scene")]
        public static void SetupLoreSystemsInScene()
        {
            // Ищем существующий LoreSystems объект
            HectonLoreSystemsRoot existing =
                Object.FindAnyObjectByType<HectonLoreSystemsRoot>();

            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Lore Systems",
                    "HectonLoreSystemsRoot уже существует в сцене.\n" +
                    "Используйте кнопку [Setup All Systems] в инспекторе.",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Создаём новый
            GameObject go = new GameObject("LoreSystems");
            HectonLoreSystemsRoot root = go.AddComponent<HectonLoreSystemsRoot>();
            root.SetupAllSystems();

            Undo.RegisterCreatedObjectUndo(go, "Create LoreSystems");
            Selection.activeGameObject = go;

            EditorUtility.DisplayDialog(
                "Lore Systems",
                "LoreSystems создан в сцене.\n\n" +
                "Следующие шаги:\n" +
                "1. Создать ScriptableObject ассеты (Data/Lore/)\n" +
                "2. Назначить ссылки в инспекторе каждой системы\n" +
                "3. Разместить AudioLogPickup и NarrativeDiscovery в мире\n\n" +
                "Подробнее: Assets/_Project/Scripts/LORE_SYSTEMS_GUIDE.md",
                "OK");

            Debug.Log("[LoreSetup] LoreSystems created. See LORE_SYSTEMS_GUIDE.md for setup instructions.");
        }

        [MenuItem("Hecton8/Lore/Create Lore Data Folder Structure")]
        public static void CreateLoreDataFolders()
        {
            string[] folders = {
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

            EditorUtility.DisplayDialog(
                "Lore Data Folders",
                "Папки созданы:\n" +
                "• Data/Lore/AudioLogs — AudioLogData ассеты\n" +
                "• Data/Lore/Quests — QuestData ассеты\n" +
                "• Data/Lore/DepthZones — DepthZoneProfile ассеты\n" +
                "• Data/Lore/SuitUpgrades — SuitUpgradeData ассеты\n" +
                "• Data/Lore/Registries — Registry SO ассеты",
                "OK");
        }

        [MenuItem("Hecton8/Lore/Open Lore Guide")]
        public static void OpenLoreGuide()
        {
            string path = "Assets/_Project/Scripts/LORE_SYSTEMS_GUIDE.md";
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
            else
                EditorUtility.DisplayDialog("Not Found",
                    $"Файл не найден: {path}", "OK");
        }

        [MenuItem("Hecton8/Lore/Create Default SO Assets")]
        public static void CreateDefaultSOAssets()
        {
            int created = 0;

            created += CreateSOIfMissing<Hecton8.Narrative.ColonistLoreRegistry>(
                "Assets/_Project/Data/Lore/Registries/ColonistLoreRegistry.asset");

            created += CreateSOIfMissing<Hecton8.Narrative.FaunaLoreRegistry>(
                "Assets/_Project/Data/Lore/Registries/FaunaLoreRegistry.asset");

            created += CreateSOIfMissing<Hecton8.Narrative.DeepReachCorporationData>(
                "Assets/_Project/Data/Lore/Registries/DeepReachCorporationData.asset");

            // DepthZone profiles — 7 зон из лора
            created += CreateDepthZoneProfile("DepthZone_TheSpine",
                "THE SPINE / SHALLOW GRAVE", 0f, 100f, 0, 0.1f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheSpine.asset");

            created += CreateDepthZoneProfile("DepthZone_DrownedFactories",
                "THE DROWNED FACTORIES", 100f, 1500f, 1, 0.4f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_DrownedFactories.asset");

            created += CreateDepthZoneProfile("DepthZone_TheDropUpper",
                "THE DROP — UPPER", 1000f, 2500f, 2, 0.6f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheDropUpper.asset");

            created += CreateDepthZoneProfile("DepthZone_TheDropDeep",
                "THE DROP — DEEP ABYSS", 2500f, 4000f, 3, 0.8f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_TheDropDeep.asset");

            created += CreateDepthZoneProfile("DepthZone_ThermalFields",
                "THERMAL FIELDS — THE RIFT", 4000f, 5500f, 4, 1.0f,
                "Assets/_Project/Data/Lore/DepthZones/DepthZone_ThermalFields.asset");

            // SuitUpgrade profiles — Tier 0-4
            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier1",
                "КОРПУС ТИР 1 — до -500м", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 1,
                350f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier1.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Oxygen_Tier1",
                "КИСЛОРОД ТИР 1 — 8 мин", Hecton8.Gameplay.SuitUpgradeCategory.Oxygen, 1,
                0f, 140f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Oxygen_Tier1.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier2",
                "КОРПУС ТИР 2 — до -1500м", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 2,
                1000f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier2.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier3",
                "КОРПУС ТИР 3 — до -3500м", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 3,
                3000f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier3.asset");

            created += CreateSuitUpgrade("SuitUpgrade_Hull_Tier4",
                "КОРПУС ТИР 4 — до -5000м", Hecton8.Gameplay.SuitUpgradeCategory.Hull, 4,
                4850f, 0f, 0f, 0f, 0f, 0f, 0f,
                "Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier4.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Lore Assets Created",
                $"Создано {created} ассетов в Assets/_Project/Data/Lore/\n\n" +
                "Назначьте их в инспекторе соответствующих систем.",
                "OK");
        }

        private static int CreateSOIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return 0;

            T so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return 1;
        }

        private static int CreateDepthZoneProfile(string id, string displayName,
            float minDepth, float maxDepth, int requiredHullTier, float dangerLevel,
            string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Hecton8.World.DepthZoneProfile>(path) != null)
                return 0;

            var so = ScriptableObject.CreateInstance<Hecton8.World.DepthZoneProfile>();
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
                return 0;

            var so = ScriptableObject.CreateInstance<Hecton8.Gameplay.SuitUpgradeData>();
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
