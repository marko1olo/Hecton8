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

        [MenuItem("Hecton/Validation/Generate AI Fauna World Integration Report", priority = 247)]
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

            var biomesWithoutPassive = new List<string>();
            var biomesWithoutThreat = new List<string>();
            var biomesWithLeviathan = new List<string>();
            var skewWarnings = new List<string>();

            if (catalog != null && catalog.Profiles != null)
            {
                for (int i = 0; i < catalog.Profiles.Length; i++)
                {
                    HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                    if (profile == null)
                        continue;

                    if (!datasetByBiomeIndex.TryGetValue(profile.matrixIndex, out FaunaBiomeData dataset) || dataset == null)
                    {
                        biomesWithoutPassive.Add($"{profile.biomeName} - нет датасета");
                        biomesWithoutThreat.Add($"{profile.biomeName} - нет датасета");
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

                    if (leviathanCount > 0)
                        biomesWithLeviathan.Add($"{profile.biomeName} ({leviathanCount})");

                    if (hunterCount > 2)
                        skewWarnings.Add($"{profile.biomeName}: слишком много средних угроз ({hunterCount})");
                    if (leviathanCount > 1)
                        skewWarnings.Add($"{profile.biomeName}: слишком много крупных угроз ({leviathanCount})");
                    if (passiveCount == 0 && threatCount > 0)
                        skewWarnings.Add($"{profile.biomeName}: есть опасность, но нет мирной жизни");
                    if (IsCalmFamily(profile.familyId) && hunterCount > 1)
                        skewWarnings.Add($"{profile.biomeName}: спокойная вода ушла в боевую арену");
                }
            }

            var sb = new StringBuilder(16384);
            sb.AppendLine("# AI Fauna World Integration Report");
            sb.AppendLine();
            sb.AppendLine("## Что есть");
            sb.AppendLine();
            sb.AppendLine($"- Профилей видов: `{archetypeCount}`");
            sb.AppendLine($"- Из них без префаба: `{archetypeWithoutPrefabCount}`");
            sb.AppendLine($"- Наборов фауны по биомам: `{datasetByBiomeIndex.Count}`");
            sb.AppendLine();

            AppendList(sb, "Биомы без мирной жизни", biomesWithoutPassive);
            AppendList(sb, "Биомы без угроз", biomesWithoutThreat);
            AppendList(sb, "Биомы с левиафанами", biomesWithLeviathan);
            AppendList(sb, "Перекосы", skewWarnings);

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
                sb.AppendLine("- Нет.");
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
    }
}
