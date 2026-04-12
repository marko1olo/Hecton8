// ============================================================================
// HECTON-8 — LoreContentGenerator.cs
// Editor utility: генерация всех лорных ScriptableObject'ов.
//
// Создаёт:
//   • SuitUpgradeData × 5 (Tier 0-4)
//   • DepthZoneProfile × 7 (по зонам из лора)
//   • QuestData × N (квесты из лора)
//   • AudioLogData × N (дневники колонии)
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.World;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class LoreContentGenerator
    {
        private const string kDataPath = "Assets/_Project/Data/Lore";

        [MenuItem("HECTON-8/Generate Lore Content/All", false, 100)]
        public static void GenerateAll()
        {
            GenerateSuitUpgrades();
            GenerateDepthZones();
            GenerateQuests();
            GenerateAudioLogs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LoreContentGenerator] All content generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Suit Upgrades (5)", false, 101)]
        public static void GenerateSuitUpgrades()
        {
            string path = $"{kDataPath}/SuitUpgrades";
            EnsureFolder(path);

            // Tier 0: Starter
            CreateSuitUpgrade("SuitUpgrade_Tier0_Starter", "tier0_starter", "Базовый комплект", 0,
                deltaMaxOxygen: 0f, deltaSafeDepth: 0f,
                desc: "Стандартный скафандр Deep Reach. До 150м, 4 минуты O2.");

            // Tier 1: First Craft
            CreateSuitUpgrade("SuitUpgrade_Tier1_Reinforced", "tier1_reinforced", "Усиленный корпус", 1,
                deltaMaxOxygen: 240f, deltaSafeDepth: 350f,
                desc: "Усиленный корпус для погружений до 500м. 8 минут O2. Требует титановые пластины.");

            // Tier 2: Deep Operations
            CreateSuitUpgrade("SuitUpgrade_Tier2_DeepOps", "tier2_deep_ops", "Глубоководный модуль", 2,
                deltaMaxOxygen: 420f, deltaSafeDepth: 1000f,
                desc: "Глубоководный модуль для операций до 1500м. 15 минут O2. Рециркуляция.");

            // Tier 3: Abyssal
            CreateSuitUpgrade("SuitUpgrade_Tier3_Abyssal", "tier3_abyssal", "Абиссальный скафандр", 3,
                deltaMaxOxygen: 600f, deltaSafeDepth: 2000f,
                desc: "Абиссальный скафандр для глубин до 3500м. 25 минут O2. Замкнутый цикл.");

            // Tier 4: Hadal
            CreateSuitUpgrade("SuitUpgrade_Tier4_Hadal", "tier4_hadal", "Хадальный экзоскелет", 4,
                deltaMaxOxygen: 1200f, deltaSafeDepth: 1500f,
                desc: "Хадальный экзоскелет для предельных глубин до 5000м. 45 минут O2. Полная изоляция.");

            Debug.Log("[LoreContentGenerator] Suit upgrades generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Depth Zones (7)", false, 102)]
        public static void GenerateDepthZones()
        {
            string path = $"{kDataPath}/DepthZones";
            EnsureFolder(path);

            // Zone 1: THE SPINE
            CreateDepthZone("DepthZone_Spine", "zone_spine", "THE SPINE", 0f, 100f, 0,
                desc: "Мелководные вершины и скалы. Относительно безопасно.",
                biolum: 0.05f, danger: 0.1f);

            // Zone 1.1: Shallow Grave
            CreateDepthZone("DepthZone_ShallowGrave", "zone_shallow_grave", "SHALLOW GRAVE", 0f, 150f, 0,
                desc: "Подводные вершины. Стартовая зона.",
                biolum: 0.08f, danger: 0.15f);

            // Zone 2: DROWNED FACTORIES
            CreateDepthZone("DepthZone_DrownedFactories", "zone_drowned_factories", "THE DROWNED FACTORIES", 100f, 1500f, 1,
                desc: "Затопленные структуры колонии. Механические опасности.",
                biolum: 0.15f, danger: 0.3f);

            // Zone 2.1: Mountain Slopes
            CreateDepthZone("DepthZone_MountainSlopes", "zone_mountain_slopes", "ГОРНЫЕ СКЛОНЫ", 150f, 500f, 1,
                desc: "Склоны подводных гор. Умеренная опасность.",
                biolum: 0.12f, danger: 0.25f);

            // Zone 3: THE DROP
            CreateDepthZone("DepthZone_TheDrop", "zone_the_drop", "THE DROP", 1000f, 5000f, 2,
                desc: "Абиссальный обрыв. Экстремальное давление.",
                biolum: 0.35f, danger: 0.6f);

            // Zone 3.1: Upper Abyss
            CreateDepthZone("DepthZone_UpperAbyss", "zone_upper_abyss", "ВЕРХНЯЯ БЕЗДНА", 1200f, 2500f, 2,
                desc: "Начало бездны. Требуется Tier 2+.",
                biolum: 0.30f, danger: 0.5f);

            // Zone 4: THE WOUND (caves)
            CreateDepthZone("DepthZone_TheWound", "zone_the_wound", "THE WOUND", 0f, 5000f, 3,
                desc: "Пещерная система. Любая глубина. Требуется Tier 3+.",
                biolum: 0.50f, danger: 0.8f, hasCaves: true);

            Debug.Log("[LoreContentGenerator] Depth zones generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Quests", false, 103)]
        public static void GenerateQuests()
        {
            string path = $"{kDataPath}/Quests";
            EnsureFolder(path);

            // Main quest line
            CreateQuest("Quest_Arrival", "quest_arrival", "ПРИБЫТИЕ",
                QuestTriggerType.Manual, "",
                desc: "Добро пожаловать на Гектон-8. Осмотритесь.");

            CreateQuest("Quest_FirstBreath", "quest_first_breath", "ПЕРВЫЙ ВДОХ",
                QuestTriggerType.OnDepthReached, "150",
                desc: "Погрузитесь на 150м. Почувствуйте давление.");

            CreateQuest("Quest_SignalDetected", "quest_atlas_signal_detected", "СИГНАЛ",
                QuestTriggerType.OnSignalDetected, "",
                desc: "Обнаружен неизвестный сигнал. Ритм 11:23.");

            CreateQuest("Quest_SignalDecoded", "quest_atlas_signal_decoded", "РАСШИФРОВКА",
                QuestTriggerType.OnDiscoveryMade, "atlas6_signal_fully_decoded",
                desc: "Сигнал расшифрован. Источник: ядро Атлас-6.");

            CreateQuest("Quest_TheCore", "quest_the_core", "ЯДРО",
                QuestTriggerType.OnDepthReached, "4500",
                desc: "Достигните ядра Атлас-6. Глубина -5000м.");

            Debug.Log("[LoreContentGenerator] Quests generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Audio Logs", false, 104)]
        public static void GenerateAudioLogs()
        {
            string path = $"{kDataPath}/AudioLogs";
            EnsureFolder(path);

            // Chen_M logs
            CreateAudioLog("AudioLog_ChenM_01", "chen_m_log_01", "Дневник Chen_M — Запись 1",
                "Chen_M", AudioLogCategory.Personal,
                "День 847. Системы работают. Жду указаний от капитана. Ничего нового.",
                "847 дней назад");

            CreateAudioLog("AudioLog_ChenM_02", "chen_m_log_02", "Дневник Chen_M — Запись 2",
                "Chen_M", AudioLogCategory.Personal,
                "Слышим движение под модулем. Что-то большое. Докладываю капитану.",
                "847 дней назад");

            CreateAudioLog("AudioLog_ChenM_03", "chen_m_log_03", "Дневник Chen_M — Запись 3",
                "Chen_M", AudioLogCategory.Personal,
                "Попытался взломать систему Атлас-6. Не вышло. Он... растёт. [звук скрежета]",
                "847 дней назад");

            // Captain
            CreateAudioLog("AudioLog_Captain_Last", "captain_last_broadcast", "Последняя трансляция — Капитан",
                "Капитан", AudioLogCategory.Emergency,
                "Атлас... он не отвечает. Но мы видим, как он... растёт. [скрежет] Если кто-то слышит это... не спускайтесь к ядру.",
                "847 дней назад");

            // Biologist
            CreateAudioLog("AudioLog_Biologist_Samples", "biologist_samples", "Образцы — Биолог",
                "Биолог", AudioLogCategory.Technical,
                "Кремниевая флора демонстрирует странное поведение. Образцы адаптируются. Атлас влияет на биомассу.",
                "848 дней назад");

            // Medic
            CreateAudioLog("AudioLog_Medic_Diary", "medic_diary", "Дневник симптомов — Медик",
                "Медик", AudioLogCategory.Technical,
                "Синдром глубины. Галлюцинации на 500м+. Паранойя. Пустые ампулы. Кто-то принимает слишком много.",
                "849 дней назад");

            Debug.Log("[LoreContentGenerator] Audio logs generated.");
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void CreateSuitUpgrade(string fileName, string id, string display, int tier,
            float deltaMaxOxygen, float deltaSafeDepth, string desc)
        {
            string fullPath = $"{kDataPath}/SuitUpgrades/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SuitUpgradeData>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SuitUpgradeData>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }

            asset.upgradeId = id;
            asset.displayName = display;
            asset.tier = tier;
            asset.category = tier <= 1 ? SuitUpgradeCategory.Hull : SuitUpgradeCategory.Oxygen;
            asset.deltaMaxOxygen = deltaMaxOxygen;
            asset.deltaSafeDepth = deltaSafeDepth;
            asset.description = desc;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateDepthZone(string fileName, string id, string display,
            float minDepth, float maxDepth, int reqTier, string desc, float biolum, float danger, bool hasCaves = false)
        {
            string fullPath = $"{kDataPath}/DepthZones/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DepthZoneProfile>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DepthZoneProfile>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }

            asset.zoneId = id;
            asset.displayName = display;
            asset.minDepth = minDepth;
            asset.maxDepth = maxDepth;
            asset.requiredHullTier = reqTier;
            asset.description = desc;
            asset.ambience.biolumIntensity = biolum;
            asset.dangerLevel = danger;
            asset.hasCaves = hasCaves;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateQuest(string fileName, string id, string title,
            QuestTriggerType trigger, string triggerId, string desc)
        {
            string fullPath = $"{kDataPath}/Quests/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<QuestData>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<QuestData>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }

            asset.questId = id;
            asset.displayTitle = title;
            asset.triggerType = trigger;
            asset.triggerId = triggerId;
            asset.description = desc;

            EditorUtility.SetDirty(asset);
        }

        private static void CreateAudioLog(string fileName, string id, string title,
            string author, AudioLogCategory category, string summary, string date)
        {
            string fullPath = $"{kDataPath}/AudioLogs/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<AudioLogData>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AudioLogData>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }

            asset.logId = id;
            asset.displayTitle = title;
            asset.author = author;
            asset.category = category;
            asset.archiveSummary = summary;
            asset.recordDate = date;

            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
