using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Environment;
using UnityEditor;

namespace Hecton8.AI.Editor
{
    public static class FaunaWorldIntegrationReportGenerator
    {
        private const string BiomeCatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string ReportPath = "C:/hades/Hecton8/AI_FAUNA_WORLD_INTEGRATION_REPORT.md";

        [MenuItem("Hecton8/Validation/Generate AI Fauna World Integration Report", priority = 247)]
        public static void Generate()
        {
            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeCatalogPath);
            string[] archetypeGuids = AssetDatabase.FindAssets("t:CreatureArchetypeData");
            string[] datasetGuids = AssetDatabase.FindAssets("t:FaunaBiomeData");

            var datasetByBiomeIndex = new Dictionary<int, FaunaBiomeData>(datasetGuids.Length);
            int archetypeCount = 0;
            int archetypeWithoutPrefabCount = 0;

            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                CreatureArchetypeData archetype = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(path);
                if (archetype == null)
                    continue;

                archetypeCount++;
                if (archetype.prefab == null)
                    archetypeWithoutPrefabCount++;
            }

            for (int i = 0; i < datasetGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(datasetGuids[i]);
                FaunaBiomeData dataset = AssetDatabase.LoadAssetAtPath<FaunaBiomeData>(path);
                if (dataset != null)
                    datasetByBiomeIndex[dataset.biomeIndex] = dataset;
            }

            var biomesWithoutPassive = new List<string>(16);
            var biomesWithoutThreat = new List<string>(16);
            var biomesWithLargeThreatZone = new List<string>(16);
            var biomesWithLeviathan = new List<string>(16);
            var heavyHunterMacroZones = new List<string>(16);
            var reserveBiomesWithLeviathan = new List<string>(8);
            var surfaceBiomesWithLeviathan = new List<string>(8);
            var skewWarnings = new List<string>(16);
            var reefBiomeSummaries = new List<string>(16);
            var reefBiomeWarnings = new List<string>(16);

            if (catalog != null && catalog.Profiles != null)
            {
                for (int i = 0; i < catalog.Profiles.Length; i++)
                {
                    HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                    if (profile == null)
                        continue;

                    if (!datasetByBiomeIndex.TryGetValue(profile.matrixIndex, out FaunaBiomeData dataset) || dataset == null)
                    {
                        biomesWithoutPassive.Add($"{profile.biomeName} - no dataset");
                        biomesWithoutThreat.Add($"{profile.biomeName} - no dataset");
                        continue;
                    }

                    int passiveCount = 0;
                    int threatCount = 0;
                    int hunterCount = 0;
                    int leviathanCount = 0;

                    List<FaunaEntry> entries = dataset.possibleCreatures;
                    int entryCount = entries != null ? entries.Count : 0;
                    for (int j = 0; j < entryCount; j++)
                    {
                        CreatureArchetypeData archetype = entries[j].archetype;
                        if (archetype == null)
                            continue;

                        switch (archetype.roleType)
                        {
                            case CreatureRoleType.Ambient:
                                passiveCount++;
                                break;
                            case CreatureRoleType.Territorial:
                                threatCount++;
                                break;
                            case CreatureRoleType.Hunter:
                                threatCount++;
                                hunterCount++;
                                break;
                            case CreatureRoleType.Leviathan:
                                threatCount++;
                                leviathanCount++;
                                break;
                        }
                    }

                    if (passiveCount == 0)
                        biomesWithoutPassive.Add(profile.biomeName);

                    if (threatCount == 0)
                        biomesWithoutThreat.Add(profile.biomeName);

                    if (dataset.HasLargeThreatZone())
                    {
                        string zoneLine =
                            $"{profile.biomeName} - {dataset.largeThreatArchetype.displayName} / {DescribeEncounterType(dataset.largeThreatEncounterType)} / zone {dataset.largeThreatZoneRadius:0}m";
                        biomesWithLargeThreatZone.Add(zoneLine);

                        if (dataset.preferHeavyHunterInsteadOfLeviathan)
                            heavyHunterMacroZones.Add(profile.biomeName);
                    }

                    if (leviathanCount > 0)
                    {
                        string leviathanLine = dataset.largeThreatArchetype != null && dataset.largeThreatArchetype.roleType == CreatureRoleType.Leviathan
                            ? $"{profile.biomeName} ({leviathanCount}) - {dataset.largeThreatArchetype.displayName} / {DescribeEncounterType(dataset.largeThreatEncounterType)}"
                            : $"{profile.biomeName} ({leviathanCount})";

                        biomesWithLeviathan.Add(leviathanLine);

                        if (IsGenericReserveBiomeName(profile.biomeName))
                            reserveBiomesWithLeviathan.Add(profile.biomeName);
                        if (profile.maxDepthMeters <= 3000f)
                            surfaceBiomesWithLeviathan.Add(profile.biomeName);
                    }

                    if (hunterCount > 2)
                        skewWarnings.Add($"{profile.biomeName}: too many mid threats ({hunterCount})");
                    if (leviathanCount > 1)
                        skewWarnings.Add($"{profile.biomeName}: too many large threats ({leviathanCount})");
                    if (passiveCount == 0 && threatCount > 0)
                        skewWarnings.Add($"{profile.biomeName}: danger exists but passive life is missing");
                    if (IsCalmFamily(profile.familyId) && hunterCount > 1)
                        skewWarnings.Add($"{profile.biomeName}: calm water drifted into combat arena");
                    if (leviathanCount > 0 && !dataset.HasLargeThreatZone())
                        skewWarnings.Add($"{profile.biomeName}: leviathan exists but large threat zone is missing");
                    if (dataset.preferHeavyHunterInsteadOfLeviathan && leviathanCount > 0)
                        skewWarnings.Add($"{profile.biomeName}: heavy hunter and leviathan are both present");

                    if (IsReefLifeBiome(profile))
                    {
                        string faunaFamilyLabel = profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null
                            ? profile.familyProfile.faunaFamilyProfile.familyLabel
                            : "None";
                        string faunaFamilyId = profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null
                            ? profile.familyProfile.faunaFamilyProfile.familyId
                            : "none";
                        string entrySummary = DescribeEntries(entries);

                        reefBiomeSummaries.Add(
                            $"{profile.biomeName} - family `{profile.familyId}` / fauna `{faunaFamilyLabel}` (`{faunaFamilyId}`) / passive `{passiveCount}` / threat `{threatCount}` / hunter `{hunterCount}` / leviathan `{leviathanCount}` / entries `{entrySummary}`");

                        if (passiveCount < 2)
                            reefBiomeWarnings.Add($"{profile.biomeName}: reef/littoral flora biome is too thin on passive life ({passiveCount}).");
                        if (threatCount < 1)
                            reefBiomeWarnings.Add($"{profile.biomeName}: reef/littoral flora biome has no threat pressure.");
                        if (entries == null || entries.Count < 3)
                            reefBiomeWarnings.Add($"{profile.biomeName}: reef/littoral flora biome has too few fauna entries ({(entries != null ? entries.Count : 0)}).");
                    }
                }
            }

            if (reserveBiomesWithLeviathan.Count > 0)
                skewWarnings.Add($"Leviathans are still sitting in generic reserve biomes: {reserveBiomesWithLeviathan.Count}");
            if (surfaceBiomesWithLeviathan.Count > 4)
                skewWarnings.Add($"Too many shallow or mid-depth leviathan biomes: {surfaceBiomesWithLeviathan.Count}");

            var sb = new StringBuilder(16384);
            sb.AppendLine("# AI Fauna World Integration Report");
            sb.AppendLine();
            sb.AppendLine("## What Exists");
            sb.AppendLine();
            sb.AppendLine($"- Creature archetypes: `{archetypeCount}`");
            sb.AppendLine($"- Archetypes without prefab: `{archetypeWithoutPrefabCount}`");
            sb.AppendLine($"- Fauna datasets by biome: `{datasetByBiomeIndex.Count}`");
            sb.AppendLine();

            AppendList(sb, "Biomes Without Passive Life", biomesWithoutPassive);
            AppendList(sb, "Biomes Without Threats", biomesWithoutThreat);
            AppendList(sb, "Large Water Areas With Major Threats", biomesWithLargeThreatZone);
            AppendList(sb, "Biomes With Leviathans", biomesWithLeviathan);
            AppendList(sb, "Biomes Using Heavy Hunters Instead Of Leviathans", heavyHunterMacroZones);
            AppendList(sb, "Reserve Biomes With Leviathans", reserveBiomesWithLeviathan);
            AppendList(sb, "Shallow And Mid-Depth Biomes With Leviathans", surfaceBiomesWithLeviathan);
            AppendList(sb, "Reef And Littoral Flora Biomes", reefBiomeSummaries);
            AppendList(sb, "Reef And Littoral Flora Warnings", reefBiomeWarnings);
            AppendList(sb, "Skew Warnings", skewWarnings);

            File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[FaunaWorldIntegrationReport] Report written: {ReportPath}");
        }

        private static void AppendList(StringBuilder sb, string title, List<string> values)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();

            if (values.Count == 0)
            {
                sb.AppendLine("- None.");
                sb.AppendLine();
                return;
            }

            for (int i = 0; i < values.Count; i++)
                sb.AppendLine($"- {values[i]}");
            sb.AppendLine();
        }

        private static bool IsCalmFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.littoral_karst", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.fossil_reef", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.crystal_growth", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReefLifeBiome(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (IsCalmFamily(profile.familyId))
                return true;

            string biomeName = profile.biomeName != null ? profile.biomeName.ToLowerInvariant() : string.Empty;
            return biomeName.Contains("reef") ||
                   biomeName.Contains("coral") ||
                   biomeName.Contains("sea-stack") ||
                   biomeName.Contains("archipelago") ||
                   biomeName.Contains("fossil");
        }

        private static string DescribeEntries(List<FaunaEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return "none";

            var sb = new StringBuilder(256);
            int written = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                CreatureArchetypeData archetype = entries[i].archetype;
                if (archetype == null)
                    continue;

                if (written > 0)
                    sb.Append(" | ");

                sb.Append(archetype.displayName)
                    .Append(" [")
                    .Append(archetype.roleType)
                    .Append(']');

                written++;
            }

            return written > 0 ? sb.ToString() : "none";
        }

        private static bool IsGenericReserveBiomeName(string biomeName)
        {
            return !string.IsNullOrWhiteSpace(biomeName) &&
                   biomeName.StartsWith("Tier ", System.StringComparison.OrdinalIgnoreCase) &&
                   biomeName.Contains("Reserve", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeEncounterType(LeviathanEncounterType encounterType)
        {
            switch (encounterType)
            {
                case LeviathanEncounterType.AmbushBurst:
                    return "ambush burst";
                case LeviathanEncounterType.SentinelPressure:
                    return "sentinel pressure";
                default:
                    return "presence circle";
            }
        }
    }
}
