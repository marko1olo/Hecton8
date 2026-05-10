// ============================================================================
// HECTON-8 — LoreContentGenerator.cs
// Editor utility: generatsiya vseh lornyh ScriptableObject'ov.
//
// Sozdaet:
//   • SuitUpgradeData × 5 (Tier 0-4)
//   • DepthZoneProfile × 7 (po zonam iz lora)
//   • QuestData × N (kvesty iz lora)
//   • AudioLogData × N (dnevniki kolonii)
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
            CreateSuitUpgrade("SuitUpgrade_Tier0_Starter", "tier0_starter", "Bazovyy komplekt", 0,
                deltaMaxOxygen: 0f, deltaSafeDepth: 0f,
                desc: "Standartnyy skafandr Deep Reach. Do 150m, 4 minuty O2.");

            // Tier 1: First Craft
            CreateSuitUpgrade("SuitUpgrade_Tier1_Reinforced", "tier1_reinforced", "Usilennyy korpus", 1,
                deltaMaxOxygen: 240f, deltaSafeDepth: 350f,
                desc: "Usilennyy korpus dlya pogruzheniy do 500m. 8 minut O2. Trebuet titanovye plastiny.");

            // Tier 2: Deep Operations
            CreateSuitUpgrade("SuitUpgrade_Tier2_DeepOps", "tier2_deep_ops", "Glubokovodnyy modul", 2,
                deltaMaxOxygen: 420f, deltaSafeDepth: 1000f,
                desc: "Glubokovodnyy modul dlya operatsiy do 1500m. 15 minut O2. Retsirkulyatsiya.");

            // Tier 3: Abyssal
            CreateSuitUpgrade("SuitUpgrade_Tier3_Abyssal", "tier3_abyssal", "Abissalnyy skafandr", 3,
                deltaMaxOxygen: 600f, deltaSafeDepth: 2000f,
                desc: "Abissalnyy skafandr dlya glubin do 3500m. 25 minut O2. Zamknutyy tsikl.");

            // Tier 4: Hadal
            CreateSuitUpgrade("SuitUpgrade_Tier4_Hadal", "tier4_hadal", "Hadalnyy ekzoskelet", 4,
                deltaMaxOxygen: 1200f, deltaSafeDepth: 1500f,
                desc: "Hadalnyy ekzoskelet dlya predelnyh glubin do 5000m. 45 minut O2. Polnaya izolyatsiya.");

            Debug.Log("[LoreContentGenerator] Suit upgrades generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Depth Zones (7)", false, 102)]
        public static void GenerateDepthZones()
        {
            string path = $"{kDataPath}/DepthZones";
            EnsureFolder(path);

            // Zone 1: THE SPINE
            CreateDepthZone("DepthZone_Spine", "zone_spine", "THE SPINE", 0f, 100f, 0,
                desc: "Melkovodnye vershiny i skaly. Otnositelno bezopasno.",
                biolum: 0.05f, danger: 0.1f);

            // Zone 1.1: Shallow Grave
            CreateDepthZone("DepthZone_ShallowGrave", "zone_shallow_grave", "SHALLOW GRAVE", 0f, 150f, 0,
                desc: "Podvodnye vershiny. Startovaya zona.",
                biolum: 0.08f, danger: 0.15f);

            // Zone 2: DROWNED FACTORIES
            CreateDepthZone("DepthZone_DrownedFactories", "zone_drowned_factories", "THE DROWNED FACTORIES", 100f, 1500f, 1,
                desc: "Zatoplennye struktury kolonii. Mehanicheskie opasnosti.",
                biolum: 0.15f, danger: 0.3f);

            // Zone 2.1: Mountain Slopes
            CreateDepthZone("DepthZone_MountainSlopes", "zone_mountain_slopes", "GORNYE SKLONY", 150f, 500f, 1,
                desc: "Sklony podvodnyh gor. Umerennaya opasnost.",
                biolum: 0.12f, danger: 0.25f);

            // Zone 3: THE DROP
            CreateDepthZone("DepthZone_TheDrop", "zone_the_drop", "THE DROP", 1000f, 5000f, 2,
                desc: "Abissalnyy obryv. Ekstremalnoe davlenie.",
                biolum: 0.35f, danger: 0.6f);

            // Zone 3.1: Upper Abyss
            CreateDepthZone("DepthZone_UpperAbyss", "zone_upper_abyss", "VERHNYaYa BEZDNA", 1200f, 2500f, 2,
                desc: "Nachalo bezdny. Trebuetsya Tier 2+.",
                biolum: 0.30f, danger: 0.5f);

            // Zone 4: THE WOUND (caves)
            CreateDepthZone("DepthZone_TheWound", "zone_the_wound", "THE WOUND", 0f, 5000f, 3,
                desc: "Peschernaya sistema. Lyubaya glubina. Trebuetsya Tier 3+.",
                biolum: 0.50f, danger: 0.8f, hasCaves: true);

            Debug.Log("[LoreContentGenerator] Depth zones generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Quests", false, 103)]
        public static void GenerateQuests()
        {
            string path = $"{kDataPath}/Quests";
            EnsureFolder(path);

            // Main quest line
            CreateQuest("Quest_Arrival", "quest_arrival", "PRIBYTIE",
                QuestTriggerType.Manual, "",
                desc: "Dobro pozhalovat na Gekton-8. Osmotrites.");

            CreateQuest("Quest_FirstBreath", "quest_first_breath", "PERVYY VDOH",
                QuestTriggerType.OnDepthReached, "150",
                desc: "Pogruzites na 150m. Pochuvstvuyte davlenie.");

            CreateQuest("Quest_SignalDetected", "quest_atlas_signal_detected", "SIGNAL",
                QuestTriggerType.OnDiscoveryMade, "atlas6_signal_identified",
                desc: "Obnaruzhen neizvestnyy signal. Ritm 11:23.");

            CreateQuest("Quest_SignalDecoded", "quest_atlas_signal_decoded", "RASShIFROVKA",
                QuestTriggerType.OnDiscoveryMade, "atlas6_signal_fully_decoded",
                desc: "Signal rasshifrovan. Istochnik: yadro Atlas-6.");

            CreateQuest("Quest_TheCore", "quest_the_core", "YaDRO",
                QuestTriggerType.OnDepthReached, "4500",
                desc: "Dostignite yadra Atlas-6. Glubina -5000m.");

            Debug.Log("[LoreContentGenerator] Quests generated.");
        }

        [MenuItem("HECTON-8/Generate Lore Content/Audio Logs", false, 104)]
        public static void GenerateAudioLogs()
        {
            string path = $"{kDataPath}/AudioLogs";
            EnsureFolder(path);

            // Chen_M logs
            CreateAudioLog("AudioLog_ChenM_01", "chen_m_log_01", "Dnevnik Chen_M — Zapis 1",
                "Chen_M", AudioLogCategory.Personal,
                "Den 847. Sistemy rabotayut. Zhdu ukazaniy ot kapitana. Nichego novogo.",
                "847 dney nazad");

            CreateAudioLog("AudioLog_ChenM_02", "chen_m_log_02", "Dnevnik Chen_M — Zapis 2",
                "Chen_M", AudioLogCategory.Personal,
                "Slyshim dvizhenie pod modulem. Chto-to bolshoe. Dokladyvayu kapitanu.",
                "847 dney nazad");

            CreateAudioLog("AudioLog_ChenM_03", "chen_m_log_03", "Dnevnik Chen_M — Zapis 3",
                "Chen_M", AudioLogCategory.Personal,
                "Popytalsya vzlomat sistemu Atlas-6. Ne vyshlo. On... rastet. [zvuk skrezheta]",
                "847 dney nazad");

            // Captain
            CreateAudioLog("AudioLog_Captain_Last", "captain_last_broadcast", "Poslednyaya translyatsiya — Kapitan",
                "Kapitan", AudioLogCategory.Emergency,
                "Atlas... on ne otvechaet. No my vidim, kak on... rastet. [skrezhet] Esli kto-to slyshit eto... ne spuskaytes k yadru.",
                "847 dney nazad");

            // Biologist
            CreateAudioLog("AudioLog_Biologist_Samples", "biologist_samples", "Obraztsy — Biolog",
                "Biolog", AudioLogCategory.Technical,
                "Kremnievaya flora demonstriruet strannoe povedenie. Obraztsy adaptiruyutsya. Atlas vliyaet na biomassu.",
                "848 dney nazad");

            // Medic
            CreateAudioLog("AudioLog_Medic_Diary", "medic_diary", "Dnevnik simptomov — Medik",
                "Medik", AudioLogCategory.Technical,
                "Sindrom glubiny. Gallyutsinatsii na 500m+. Paranoyya. Pustye ampuly. Kto-to prinimaet slishkom mnogo.",
                "849 dney nazad");

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
