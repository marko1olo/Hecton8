using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Editor
{
    public static class CreatureArchetypeAuthoring
    {
        private const string RootFolder = "Assets/_Project/Data/AI/CreatureArchetypes";
        private const string AmbientFolder = RootFolder + "/Ambient";
        private const string TerritorialFolder = RootFolder + "/Territorial";
        private const string HunterFolder = RootFolder + "/Hunters";
        private const string LeviathanFolder = RootFolder + "/Leviathans";
        private const string RosterDocPath = "C:/hades/Hecton8/AI_CREATURE_ROSTER_ENTERPRISE.md";

        [MenuItem("Hecton/Authoring/Build AI Creature Archetypes", priority = 182)]
        public static void BuildCreatureArchetypes()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/AI");
            EnsureFolder(RootFolder);
            EnsureFolder(AmbientFolder);
            EnsureFolder(TerritorialFolder);
            EnsureFolder(HunterFolder);
            EnsureFolder(LeviathanFolder);
            CreatureProxyPrefabAuthoring.EnsureProxyAssets();

            ArchetypeDefinition[] definitions = GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
                CreateOrUpdateArchetype(definitions[i]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteRosterDocument(definitions);

            Debug.Log("[CreatureArchetypeAuthoring] AI creature archetypes rebuilt.");
        }

        private static ArchetypeDefinition[] GetDefinitions()
        {
            return new[]
            {
                CreateAmbient(
                    "shore_skimmer",
                    "Shore Skimmer",
                    "ÐœÐµÐ»ÐºÐ°Ñ ÑÑ‚Ð°Ð¹Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÐ¿Ð¾ÐºÐ¾Ð¹Ð½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.",
                    22f,
                    4.8f,
                    7.2f,
                    new[] { "fauna.family.littoral_passive", "fauna.family.reef_ambush" },
                    new[] { "biome.family.littoral_karst", "biome.family.fossil_reef" },
                    "Ð”Ð°Ñ‘Ñ‚ Ð¶Ð¸Ð²Ð¾Ð¹ Ñ„Ð¾Ð½ Ñƒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð¸ Ð² ÑÐ¿Ð¾ÐºÐ¾Ð¹Ð½Ñ‹Ñ… Ð°Ñ€ÐºÐ°Ñ…."),
                CreateAmbient(
                    "kelp_raylet",
                    "Kelp Raylet",
                    "ÐœÐ¸Ñ€Ð½Ð°Ñ ÑˆÐ¸Ñ€Ð¾ÐºÐ°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÑ€ÐºÐ¸Ñ… Ð·Ð°Ñ€Ð¾ÑÐ»ÐµÐ¹ Ð¸ Ñ€Ð¸Ñ„Ð¾Ð².",
                    28f,
                    4.2f,
                    6.8f,
                    new[] { "fauna.family.littoral_passive", "fauna.family.crystal_skittish" },
                    new[] { "biome.family.fossil_reef", "biome.family.crystal_growth" },
                    "Ð”Ð°Ñ‘Ñ‚ Ð¼ÑÐ³ÐºÑƒÑŽ ÐºÑ€ÑƒÐ¿Ð½ÑƒÑŽ Ð¶Ð¸Ð·Ð½ÑŒ Ñ‚Ð°Ð¼, Ð³Ð´Ðµ Ð¼Ð¸Ñ€ Ð´Ð¾Ð»Ð¶ÐµÐ½ ÐºÐ°Ð·Ð°Ñ‚ÑŒÑÑ Ð±Ð¾Ð³Ð°Ñ‚Ñ‹Ð¼, Ð° Ð½Ðµ Ð±Ð¾ÐµÐ²Ñ‹Ð¼."),
                CreateAmbient(
                    "silt_drifter",
                    "Silt Drifter",
                    "Ð”Ð¾Ð½Ð½Ñ‹Ð¹ Ð¼Ð¸Ñ€Ð½Ñ‹Ð¹ ÑÐ±Ð¾Ñ€Ñ‰Ð¸Ðº Ð¾ÑÐ°Ð´Ð¾Ñ‡Ð½Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.",
                    30f,
                    3.6f,
                    5.8f,
                    new[] { "fauna.family.sediment_scavengers", "fauna.family.abyssal_sparse" },
                    new[] { "biome.family.sediment_drift", "biome.family.abyssal_silt", "biome.family.granite_escarpment" },
                    "Ð”ÐµÐ»Ð°ÐµÑ‚ Ñ€ÐµÑÑƒÑ€ÑÐ½ÑƒÑŽ Ð²Ð¾Ð´Ñƒ Ð¶Ð¸Ð²Ð¾Ð¹, Ð½Ð¾ Ð½Ðµ Ð°Ð³Ñ€ÐµÑÑÐ¸Ð²Ð½Ð¾Ð¹."),
                CreateAmbient(
                    "wall_glider",
                    "Wall Glider",
                    "ÐœÐ¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÑ‚ÐµÐ½, ÑƒÑÑ‚ÑƒÐ¿Ð¾Ð² Ð¸ Ð³Ñ€ÐµÐ±Ð½ÐµÐ¹.",
                    34f,
                    4.0f,
                    6.4f,
                    new[] { "fauna.family.escarpment_watchers", "fauna.family.ridge_hunters" },
                    new[] { "biome.family.granite_escarpment", "biome.family.rift_spine" },
                    "Ð”ÐµÐ»Ð°ÐµÑ‚ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ñ‹ Ð²Ð´Ð¾Ð»ÑŒ ÑÑ‚ÐµÐ½ Ð¶Ð¸Ð²Ñ‹Ð¼Ð¸ Ð¸ ÑÐ¸Ñ‚Ð°ÐµÐ¼Ñ‹Ð¼Ð¸."),
                CreateAmbient(
                    "brine_siphoner",
                    "Brine Siphoner",
                    "Ð¡Ñ‚Ñ€Ð°Ð½Ð½Ð°Ñ Ð¼Ð¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ Ñ…Ð¸Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð² Ð¸ Ð²ÐµÐ½Ñ‚Ð¾Ð².",
                    36f,
                    3.8f,
                    6.1f,
                    new[] { "fauna.family.chemical_specialists", "fauna.family.thermal_hostile" },
                    new[] { "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine", "biome.family.volcanic_glass" },
                    "ÐÑƒÐ¶ÐµÐ½, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ñ‚Ð¾ÐºÑÐ¸Ñ‡Ð½Ð°Ñ Ð¸ ÑÐµÑ€Ð²Ð¸ÑÐ½Ð°Ñ Ð²Ð¾Ð´Ð° Ð½Ðµ Ð±Ñ‹Ð»Ð° Ð¼Ñ‘Ñ€Ñ‚Ð²Ð¾Ð¹."),
                CreateAmbient(
                    "lantern_sifter",
                    "Lantern Sifter",
                    "Ð ÐµÐ´ÐºÐ°Ñ Ð¼Ð¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¾Ð¹ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.",
                    42f,
                    3.4f,
                    5.6f,
                    new[] { "fauna.family.abyssal_sparse", "fauna.family.hadal_apex" },
                    new[] { "biome.family.abyssal_silt", "biome.family.metallic_hadal", "biome.family.rift_void" },
                    "Ð”Ð°Ñ‘Ñ‚ Ð¾Ñ‰ÑƒÑ‰ÐµÐ½Ð¸Ðµ Ñ€ÐµÐ´ÐºÐ¾Ð¹ Ð¶Ð¸Ð·Ð½Ð¸ Ð´Ð°Ð¶Ðµ Ð² Ð¿Ð¾Ð·Ð´Ð½ÐµÐ¹ Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ðµ."),

                CreateTerritorial(
                    "nursery_shellguard",
                    "Nursery Shellguard",
                    "Защитник кладок и безопасных карманов.",
                    62f,
                    16f,
                    12f,
                    true,
                    true,
                    new[] { "fauna.family.reef_ambush", "fauna.family.littoral_passive" },
                    new[] { "biome.family.fossil_reef", "biome.family.littoral_karst" },
                    "Локальный защитник гнезда. Сначала давит, потом срывается."),
                CreateTerritorial(
                    "archway_sentinel",
                    "Archway Sentinel",
                    "Сторож арок, стен и узких проходов.",
                    78f,
                    18f,
                    14f,
                    false,
                    false,
                    new[] { "fauna.family.escarpment_watchers", "fauna.family.ridge_hunters" },
                    new[] { "biome.family.granite_escarpment", "biome.family.rift_spine" },
                    "Держит маршрут и выталкивает игрока с прохода."),

                CreateHunter(
                    "pocket_ambusher",
                    "Pocket Ambusher",
                    "Короткая засада из опасных карманов.",
                    70f,
                    22f,
                    4.2f,
                    8.2f,
                    false,
                    true,
                    new[] { "fauna.family.reef_ambush", "fauna.family.sediment_scavengers" },
                    new[] { "biome.family.fossil_reef", "biome.family.sediment_drift" },
                    "Сидит в укрытии и наказывает жадный заход в карман."),
                CreateHunter(
                    "needle_hunter",
                    "Needle Hunter",
                    "Быстрый режущий хищник яркой воды.",
                    54f,
                    18f,
                    5.6f,
                    10.2f,
                    false,
                    true,
                    new[] { "fauna.family.crystal_skittish", "fauna.family.reef_ambush" },
                    new[] { "biome.family.crystal_growth", "biome.family.littoral_karst" },
                    "Резко входит и резко выходит. Ломает комфорт скоростью."),
                CreateHunter(
                    "ridge_pack_cutter",
                    "Ridge Pack Cutter",
                    "Стайный хищник гребней и стен.",
                    82f,
                    21f,
                    5.1f,
                    8.9f,
                    true,
                    false,
                    new[] { "fauna.family.ridge_hunters", "fauna.family.escarpment_watchers" },
                    new[] { "biome.family.granite_escarpment", "biome.family.rift_spine" },
                    "Один держит фронт, другие режут с флангов."),
                CreateHunter(
                    "brine_stalker",
                    "Brine Stalker",
                    "Тягучий охотник токсичной и сервисной воды.",
                    96f,
                    24f,
                    4.8f,
                    8.4f,
                    false,
                    true,
                    new[] { "fauna.family.chemical_specialists", "fauna.family.thermal_hostile" },
                    new[] { "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine" },
                    "Любит тяжёлую воду, шрамы сервиса и горячие карманы."),
                CreateHunter(
                    "armor_breaker",
                    "Armor Breaker",
                    "Тяжёлый металлический охотник поздней глубины.",
                    130f,
                    30f,
                    4.2f,
                    7.6f,
                    false,
                    false,
                    new[] { "fauna.family.metal_predators", "fauna.family.hadal_apex" },
                    new[] { "biome.family.metallic_hadal", "biome.family.rift_void" },
                    "Не самый быстрый, но очень опасен на близкой дистанции."),
                CreateHunter(
                    "heat_lurker",
                    "Heat Lurker",
                    "Горячий засадный хищник вулканических губ.",
                    88f,
                    23f,
                    4.7f,
                    8.8f,
                    false,
                    true,
                    new[] { "fauna.family.thermal_hostile", "fauna.family.rift_stalkers" },
                    new[] { "biome.family.volcanic_glass", "biome.family.volcanic_hadal" },
                    "Работает у горячих выбросов и резких узких маршрутов."),
                CreateHunter(
                    "shadow_interceptor",
                    "Shadow Interceptor",
                    "Редкий перехватчик пустоты.",
                    92f,
                    25f,
                    5f,
                    9.4f,
                    false,
                    true,
                    new[] { "fauna.family.abyssal_sparse", "fauna.family.void_apex" },
                    new[] { "biome.family.abyssal_silt", "biome.family.rift_void" },
                    "Строит страх ожиданием и длинным перехватом."),
                CreateHunter(
                    "silt_flatmaw",
                    "Silt Flatmaw",
                    "Осадочный засадник для ресурсной воды.",
                    74f,
                    19f,
                    3.9f,
                    7.1f,
                    false,
                    false,
                    new[] { "fauna.family.sediment_scavengers", "fauna.family.ridge_hunters" },
                    new[] { "biome.family.sediment_drift", "biome.family.granite_escarpment" },
                    "Ждёт добычу у дна и карает жадный сбор ресурсов."),

                CreateLeviathan(
                    "halo_crown",
                    "Halo Crown Leviathan",
                    "Круговой левиафан давления.",
                    LeviathanEncounterType.PresenceCircle,
                    950f,
                    80f,
                    5.8f,
                    10.4f,
                    false,
                    new[] { "fauna.family.hadal_apex", "fauna.family.void_apex" },
                    new[] { "biome.family.rift_void", "biome.family.abyssal_silt" },
                    "Ломает безопасность кругом и поздним входом."),
                CreateLeviathan(
                    "gate_warden",
                    "Gate Warden Leviathan",
                    "Сторож глубокого прохода.",
                    LeviathanEncounterType.SentinelPressure,
                    1100f,
                    95f,
                    5.2f,
                    9.6f,
                    false,
                    new[] { "fauna.family.hadal_apex", "fauna.family.rift_stalkers" },
                    new[] { "biome.family.rift_spine", "biome.family.volcanic_hadal", "biome.family.metallic_hadal" },
                    "Держит маршрут и выдавливает игрока из узкого места."),
                CreateLeviathan(
                    "rift_lancer",
                    "Rift Lancer Leviathan",
                    "Рифтовый левиафан резкого рывка.",
                    LeviathanEncounterType.AmbushBurst,
                    920f,
                    88f,
                    6.2f,
                    11.6f,
                    true,
                    new[] { "fauna.family.rift_stalkers", "fauna.family.void_apex" },
                    new[] { "biome.family.rift_void", "biome.family.rift_spine" },
                    "Пугает ложным заходом и ловит на резком сближении."),
                CreateLeviathan(
                    "black_choir",
                    "Black Choir Leviathan",
                    "Левиафан позднего ужаса.",
                    LeviathanEncounterType.PresenceCircle,
                    1250f,
                    90f,
                    4.8f,
                    9.2f,
                    false,
                    new[] { "fauna.family.void_apex", "fauna.family.hadal_apex" },
                    new[] { "biome.family.rift_void", "biome.family.abyssal_silt" },
                    "Строит страх ожиданием, звуком и поздним контактом."),
                CreateLeviathan(
                    "furnace_maw",
                    "Furnace Maw Leviathan",
                    "Вулканический сторож горячих шахт.",
                    LeviathanEncounterType.SentinelPressure,
                    1080f,
                    98f,
                    5.6f,
                    10.6f,
                    true,
                    new[] { "fauna.family.thermal_hostile", "fauna.family.hadal_apex" },
                    new[] { "biome.family.volcanic_glass", "biome.family.volcanic_hadal" },
                    "Жмёт на маршруте и добавляет ложные проходы перед реальной атакой."),
                CreateLeviathan(
                    "void_ribbon",
                    "Void Ribbon Leviathan",
                    "Быстрый перехватчик пустоты.",
                    LeviathanEncounterType.AmbushBurst,
                    980f,
                    92f,
                    6.4f,
                    12f,
                    true,
                    new[] { "fauna.family.void_apex", "fauna.family.abyssal_sparse" },
                    new[] { "biome.family.abyssal_silt", "biome.family.rift_void" },
                    "Длинный тёмный перехватчик для открытой глубины.")
            };
        }

        private static ArchetypeDefinition CreateAmbientInternal(
            string shortId,
            string displayName,
            string purpose,
            float health,
            float cruiseSpeed,
            float burstSpeed,
            string[] faunaFamilies,
            string[] biomeFamilies,
            string notes)
        {
            return new ArchetypeDefinition
            {
                assetFolder = AmbientFolder,
                assetName = $"Archetype_{ToAssetToken(displayName)}",
                creatureId = $"creature.ambient.{shortId}",
                displayName = displayName,
                gameplayPurpose = purpose,
                roleType = CreatureRoleType.Ambient,
                locomotionType = CreatureLocomotionType.SteeringSolo,
                isAggressive = false,
                canFlee = true,
                maxHealth = health,
                attackDamage = 0f,
                attackCooldown = 2.5f,
                cruiseSpeed = cruiseSpeed,
                burstSpeed = burstSpeed,
                turnSpeed = 4.8f,
                sleepDistance = 160f,
                cullDistance = 220f,
                baseAggroDistance = 0f,
                baseEscapeDistance = 12f,
                baseEscapeSafeDistance = 24f,
                baseDeaggroDistance = 0f,
                noiseDetectionBonus = 0f,
                noiseEscapeBonus = 8f,
                lightDetectionBonus = 0f,
                lightEscapeBonus = 6f,
                stimulusMemoryDuration = 2.6f,
                useHomeTerritory = false,
                homeWanderRadius = 20f,
                homeReturnDistance = 28f,
                territoryProtectRadius = 0f,
                warningDuration = 0f,
                warningStandOffDistance = 0f,
                stalkDuration = 0f,
                stalkDistance = 0f,
                defendNest = false,
                nestProtectRadius = 0f,
                callNearbyAllies = false,
                allyAlertRadius = 0f,
                allyAlertCooldown = 0f,
                allyAlertMaxCount = 0,
                alliesRequireSameArchetype = true,
                usePackHunt = false,
                packSupportRadius = 0f,
                packFlankDistance = 0f,
                packCommitDistance = 0f,
                useLeviathanPresence = false,
                leviathanEncounterType = LeviathanEncounterType.PresenceCircle,
                loomingDuration = 0f,
                loomingDistance = 0f,
                loomingCommitDistance = 0f,
                useFeintRush = false,
                feintDuration = 0f,
                feintTriggerDistance = 0f,
                feintBreakDistance = 0f,
                feintCooldown = 0f,
                useCandiceBehaviorTree = false,
                useAstarPathing = false,
                useGpuBoids = false,
                behaviorTreeHint = "Ð¡Ð¿Ð¾ÐºÐ¾Ð¹Ð½Ð°Ñ Ð¼Ð¸Ñ€Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ. Ð¤Ð¾Ð½, Ð½ÐµÑ€Ð²Ð½Ñ‹Ðµ Ð¾Ñ‚ÑÐºÐ¾ÐºÐ¸, Ð¾Ñ‰ÑƒÑ‰ÐµÐ½Ð¸Ðµ Ð¶Ð¸Ð²Ð¾Ð¹ Ð²Ð¾Ð´Ñ‹.",
                maxAliveGlobal = 18,
                maxAlivePerBiome = 8,
                spawnWeight = 18,
                biomeNotes = notes,
                recommendedFaunaFamilyIds = faunaFamilies,
                recommendedBiomeFamilyIds = biomeFamilies
            };
        }

        private static ArchetypeDefinition CreateAmbient(
            string shortId,
            string displayName,
            string purpose,
            float health,
            float cruiseSpeed,
            float burstSpeed,
            string[] faunaFamilies,
            string[] biomeFamilies,
            string notes)
        {
            return CreateAmbientInternal(shortId, displayName, purpose, health, cruiseSpeed, burstSpeed, faunaFamilies, biomeFamilies, notes);
        }

        private static ArchetypeDefinition CreateTerritorial(
            string shortId,
            string displayName,
            string purpose,
            float health,
            float damage,
            float aggroDistance,
            bool defendNest,
            bool callAllies,
            string[] faunaFamilies,
            string[] biomeFamilies,
            string notes)
        {
            return new ArchetypeDefinition
            {
                assetFolder = TerritorialFolder,
                assetName = $"Archetype_{ToAssetToken(displayName)}",
                creatureId = $"creature.territorial.{shortId}",
                displayName = displayName,
                gameplayPurpose = purpose,
                roleType = CreatureRoleType.Territorial,
                locomotionType = CreatureLocomotionType.SteeringSolo,
                isAggressive = true,
                maxHealth = health,
                attackDamage = damage,
                attackCooldown = 2.2f,
                cruiseSpeed = 3.9f,
                burstSpeed = 6.4f,
                turnSpeed = 3.4f,
                sleepDistance = 170f,
                cullDistance = 230f,
                baseAggroDistance = aggroDistance,
                baseDeaggroDistance = aggroDistance + 12f,
                noiseDetectionBonus = 8f,
                noiseEscapeBonus = 0f,
                lightDetectionBonus = 7f,
                lightEscapeBonus = 0f,
                stimulusMemoryDuration = 4.4f,
                useHomeTerritory = true,
                homeWanderRadius = 14f,
                homeReturnDistance = 22f,
                territoryProtectRadius = aggroDistance + 2f,
                warningDuration = 4.4f,
                warningStandOffDistance = 7f,
                defendNest = defendNest,
                nestProtectRadius = defendNest ? 11f : 0f,
                callNearbyAllies = callAllies,
                allyAlertRadius = callAllies ? 16f : 0f,
                allyAlertCooldown = callAllies ? 3f : 0f,
                allyAlertMaxCount = callAllies ? 2 : 0,
                alliesRequireSameArchetype = true,
                maxAliveGlobal = 12,
                maxAlivePerBiome = 4,
                spawnWeight = 8,
                biomeNotes = notes,
                recommendedFaunaFamilyIds = faunaFamilies,
                recommendedBiomeFamilyIds = biomeFamilies
            };
        }

        private static ArchetypeDefinition CreateHunter(
            string shortId,
            string displayName,
            string purpose,
            float health,
            float damage,
            float cruiseSpeed,
            float burstSpeed,
            bool packHunter,
            bool feint,
            string[] faunaFamilies,
            string[] biomeFamilies,
            string notes)
        {
            return new ArchetypeDefinition
            {
                assetFolder = HunterFolder,
                assetName = $"Archetype_{ToAssetToken(displayName)}",
                creatureId = $"creature.hunter.{shortId}",
                displayName = displayName,
                gameplayPurpose = purpose,
                roleType = CreatureRoleType.Hunter,
                locomotionType = CreatureLocomotionType.SteeringSolo,
                isAggressive = true,
                maxHealth = health,
                attackDamage = damage,
                attackCooldown = 2f,
                cruiseSpeed = cruiseSpeed,
                burstSpeed = burstSpeed,
                turnSpeed = 4.3f,
                sleepDistance = 185f,
                cullDistance = 245f,
                baseAggroDistance = 18f,
                baseDeaggroDistance = 32f,
                noiseDetectionBonus = 11f,
                lightDetectionBonus = 11f,
                stimulusMemoryDuration = 4.8f,
                useHomeTerritory = true,
                homeWanderRadius = 16f,
                homeReturnDistance = 26f,
                territoryProtectRadius = 18f,
                warningDuration = 2.4f,
                warningStandOffDistance = 7f,
                stalkDuration = 4.8f,
                stalkDistance = 10f,
                callNearbyAllies = packHunter,
                allyAlertRadius = packHunter ? 22f : 0f,
                allyAlertCooldown = packHunter ? 3.5f : 0f,
                allyAlertMaxCount = packHunter ? 3 : 0,
                alliesRequireSameArchetype = true,
                usePackHunt = packHunter,
                packSupportRadius = packHunter ? 24f : 0f,
                packFlankDistance = packHunter ? 7f : 0f,
                packCommitDistance = packHunter ? 8f : 0f,
                useFeintRush = feint,
                feintDuration = feint ? 1.8f : 0f,
                feintTriggerDistance = feint ? 14f : 0f,
                feintBreakDistance = feint ? 5.5f : 0f,
                feintCooldown = feint ? 5.2f : 0f,
                maxAliveGlobal = packHunter ? 12 : 9,
                maxAlivePerBiome = packHunter ? 4 : 3,
                spawnWeight = 8,
                biomeNotes = notes,
                recommendedFaunaFamilyIds = faunaFamilies,
                recommendedBiomeFamilyIds = biomeFamilies
            };
        }

        private static ArchetypeDefinition CreateLeviathan(
            string shortId,
            string displayName,
            string purpose,
            LeviathanEncounterType encounterType,
            float health,
            float damage,
            float cruiseSpeed,
            float burstSpeed,
            bool feint,
            string[] faunaFamilies,
            string[] biomeFamilies,
            string notes)
        {
            return new ArchetypeDefinition
            {
                assetFolder = LeviathanFolder,
                assetName = $"Archetype_{ToAssetToken(displayName)}",
                creatureId = $"creature.leviathan.{shortId}",
                displayName = displayName,
                gameplayPurpose = purpose,
                roleType = CreatureRoleType.Leviathan,
                locomotionType = CreatureLocomotionType.CandiceActor,
                isAggressive = true,
                maxHealth = health,
                attackDamage = damage,
                attackCooldown = 4.1f,
                cruiseSpeed = cruiseSpeed,
                burstSpeed = burstSpeed,
                turnSpeed = 2.6f,
                sleepDistance = 330f,
                cullDistance = 450f,
                baseAggroDistance = 34f,
                baseDeaggroDistance = 62f,
                noiseDetectionBonus = 19f,
                lightDetectionBonus = 16f,
                stimulusMemoryDuration = 8.8f,
                useHomeTerritory = true,
                homeWanderRadius = 44f,
                homeReturnDistance = 68f,
                territoryProtectRadius = 60f,
                warningDuration = 5f,
                warningStandOffDistance = 16f,
                useLeviathanPresence = true,
                leviathanEncounterType = encounterType,
                loomingDuration = encounterType == LeviathanEncounterType.PresenceCircle ? 9f : 6.2f,
                loomingDistance = encounterType == LeviathanEncounterType.PresenceCircle ? 26f : 21f,
                loomingCommitDistance = 14f,
                useFeintRush = feint,
                feintDuration = feint ? 2.1f : 0f,
                feintTriggerDistance = feint ? 18f : 0f,
                feintBreakDistance = feint ? 7f : 0f,
                feintCooldown = feint ? 6.6f : 0f,
                useCandiceBehaviorTree = true,
                behaviorTreeHint = "Крупная режиссируемая угроза. Давление, ложные входы и поздний контакт.",
                maxAliveGlobal = 2,
                maxAlivePerBiome = 1,
                spawnWeight = 1,
                biomeNotes = notes,
                recommendedFaunaFamilyIds = faunaFamilies,
                recommendedBiomeFamilyIds = biomeFamilies
            };
        }

        private static void CreateOrUpdateArchetype(ArchetypeDefinition definition)
        {
            string assetPath = $"{definition.assetFolder}/{definition.assetName}.asset";
            CreatureArchetypeData asset = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CreatureArchetypeData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.creatureId = definition.creatureId;
            asset.displayName = definition.displayName;
            asset.gameplayPurpose = definition.gameplayPurpose;
            asset.roleType = definition.roleType;
            asset.locomotionType = definition.locomotionType;
            asset.isAggressive = definition.isAggressive;
            asset.canFlee = definition.canFlee;
            asset.maxHealth = definition.maxHealth;
            asset.attackDamage = definition.attackDamage;
            asset.attackCooldown = definition.attackCooldown;
            asset.cruiseSpeed = definition.cruiseSpeed;
            asset.burstSpeed = definition.burstSpeed;
            asset.turnSpeed = definition.turnSpeed;
            asset.sleepDistance = definition.sleepDistance;
            asset.cullDistance = definition.cullDistance;
            asset.baseAggroDistance = definition.baseAggroDistance;
            asset.baseEscapeDistance = definition.baseEscapeDistance;
            asset.baseEscapeSafeDistance = definition.baseEscapeSafeDistance;
            asset.baseDeaggroDistance = definition.baseDeaggroDistance;
            asset.reactToPlayerNoise = true;
            asset.noiseDetectionBonus = definition.noiseDetectionBonus;
            asset.noiseEscapeBonus = definition.noiseEscapeBonus;
            asset.reactToPlayerLight = true;
            asset.lightDetectionBonus = definition.lightDetectionBonus;
            asset.lightEscapeBonus = definition.lightEscapeBonus;
            asset.stimulusMemoryDuration = definition.stimulusMemoryDuration;
            asset.useHomeTerritory = definition.useHomeTerritory;
            asset.homeWanderRadius = definition.homeWanderRadius;
            asset.homeReturnDistance = definition.homeReturnDistance;
            asset.territoryProtectRadius = definition.territoryProtectRadius;
            asset.warningDuration = definition.warningDuration;
            asset.warningStandOffDistance = definition.warningStandOffDistance;
            asset.stalkDuration = definition.stalkDuration;
            asset.stalkDistance = definition.stalkDistance;
            asset.defendNest = definition.defendNest;
            asset.nestProtectRadius = definition.nestProtectRadius;
            asset.callNearbyAllies = definition.callNearbyAllies;
            asset.allyAlertRadius = definition.allyAlertRadius;
            asset.allyAlertCooldown = definition.allyAlertCooldown;
            asset.allyAlertMaxCount = definition.allyAlertMaxCount;
            asset.alliesRequireSameArchetype = definition.alliesRequireSameArchetype;
            asset.usePackHunt = definition.usePackHunt;
            asset.packSupportRadius = definition.packSupportRadius;
            asset.packFlankDistance = definition.packFlankDistance;
            asset.packCommitDistance = definition.packCommitDistance;
            asset.useLeviathanPresence = definition.useLeviathanPresence;
            asset.leviathanEncounterType = definition.leviathanEncounterType;
            asset.loomingDuration = definition.loomingDuration;
            asset.loomingDistance = definition.loomingDistance;
            asset.loomingCommitDistance = definition.loomingCommitDistance;
            asset.useFeintRush = definition.useFeintRush;
            asset.feintDuration = definition.feintDuration;
            asset.feintTriggerDistance = definition.feintTriggerDistance;
            asset.feintBreakDistance = definition.feintBreakDistance;
            asset.feintCooldown = definition.feintCooldown;
            asset.useCandiceBehaviorTree = definition.useCandiceBehaviorTree;
            asset.useAstarPathing = definition.useAstarPathing;
            asset.useGpuBoids = definition.useGpuBoids;
            asset.behaviorTreeHint = definition.behaviorTreeHint;
            asset.maxAliveGlobal = definition.maxAliveGlobal;
            asset.maxAlivePerBiome = definition.maxAlivePerBiome;
            asset.spawnWeight = definition.spawnWeight;
            asset.biomeNotes = definition.biomeNotes;
            asset.recommendedFaunaFamilyIds = CloneArray(definition.recommendedFaunaFamilyIds);
            asset.recommendedBiomeFamilyIds = CloneArray(definition.recommendedBiomeFamilyIds);
            if (asset.prefab == null || CreatureProxyPrefabAuthoring.IsGeneratedProxy(asset.prefab))
                asset.prefab = CreatureProxyPrefabAuthoring.ResolveDefaultProxyPrefab(definition.roleType, definition.maxHealth, definition.attackDamage);

            EditorUtility.SetDirty(asset);
        }

        private static void WriteRosterDocument(ArchetypeDefinition[] definitions)
        {
            var sb = new StringBuilder(12288);
            sb.AppendLine("# AI Creature Roster Enterprise");
            sb.AppendLine();
            sb.AppendLine("## Что это");
            sb.AppendLine();
            sb.AppendLine("- Это набор реальных профилей видов.");
            sb.AppendLine("- Их можно подвешивать к префабам и потом раскидывать по биомам.");
            sb.AppendLine("- Основной упор здесь: много разных хищников и левиафанов.");
            sb.AppendLine();

            AppendSection(sb, "Мирная жизнь", definitions, CreatureRoleType.Ambient);
            AppendSection(sb, "Территориальные", definitions, CreatureRoleType.Territorial);
            AppendSection(sb, "Хищники", definitions, CreatureRoleType.Hunter);
            AppendSection(sb, "Левиафаны", definitions, CreatureRoleType.Leviathan);

            File.WriteAllText(RosterDocPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendSection(StringBuilder sb, string title, ArchetypeDefinition[] definitions, CreatureRoleType role)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            for (int i = 0; i < definitions.Length; i++)
            {
                ArchetypeDefinition definition = definitions[i];
                if (definition.roleType != role)
                    continue;

                sb.AppendLine($"### {definition.displayName}");
                sb.AppendLine();
                sb.AppendLine($"- `ID`: `{definition.creatureId}`");
                sb.AppendLine($"- `Зачем нужен`: {definition.gameplayPurpose}");
                sb.AppendLine($"- `Суть`: {definition.biomeNotes}");
                sb.AppendLine($"- `Подходит для`: {string.Join(", ", definition.recommendedFaunaFamilyIds)}");
                sb.AppendLine($"- `Биомы`: {string.Join(", ", definition.recommendedBiomeFamilyIds)}");
                sb.AppendLine();
            }
        }

        private static string[] CloneArray(string[] values)
        {
            if (values == null || values.Length == 0)
                return System.Array.Empty<string>();

            string[] clone = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                clone[i] = values[i];
            return clone;
        }

        private static string ToAssetToken(string value)
        {
            return value
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private struct ArchetypeDefinition
        {
            public string assetFolder;
            public string assetName;
            public string creatureId;
            public string displayName;
            public string gameplayPurpose;
            public CreatureRoleType roleType;
            public CreatureLocomotionType locomotionType;
            public bool isAggressive;
            public bool canFlee;
            public float maxHealth;
            public float attackDamage;
            public float attackCooldown;
            public float cruiseSpeed;
            public float burstSpeed;
            public float turnSpeed;
            public float sleepDistance;
            public float cullDistance;
            public float baseAggroDistance;
            public float baseEscapeDistance;
            public float baseEscapeSafeDistance;
            public float baseDeaggroDistance;
            public float noiseDetectionBonus;
            public float noiseEscapeBonus;
            public float lightDetectionBonus;
            public float lightEscapeBonus;
            public float stimulusMemoryDuration;
            public bool useHomeTerritory;
            public float homeWanderRadius;
            public float homeReturnDistance;
            public float territoryProtectRadius;
            public float warningDuration;
            public float warningStandOffDistance;
            public float stalkDuration;
            public float stalkDistance;
            public bool defendNest;
            public float nestProtectRadius;
            public bool callNearbyAllies;
            public float allyAlertRadius;
            public float allyAlertCooldown;
            public int allyAlertMaxCount;
            public bool alliesRequireSameArchetype;
            public bool usePackHunt;
            public float packSupportRadius;
            public float packFlankDistance;
            public float packCommitDistance;
            public bool useLeviathanPresence;
            public LeviathanEncounterType leviathanEncounterType;
            public float loomingDuration;
            public float loomingDistance;
            public float loomingCommitDistance;
            public bool useFeintRush;
            public float feintDuration;
            public float feintTriggerDistance;
            public float feintBreakDistance;
            public float feintCooldown;
            public bool useCandiceBehaviorTree;
            public bool useAstarPathing;
            public bool useGpuBoids;
            public string behaviorTreeHint;
            public int maxAliveGlobal;
            public int maxAlivePerBiome;
            public int spawnWeight;
            public string biomeNotes;
            public string[] recommendedFaunaFamilyIds;
            public string[] recommendedBiomeFamilyIds;
        }
    }
}
