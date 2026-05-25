using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Editor
{
    public static class FaunaArchetypeReportGenerator
    {
        private const string ReportPath = "C:/hades/Hecton8/AI_FAUNA_ARCHETYPE_REPORT.md";

        [MenuItem("Hecton/Validation/Generate AI Fauna Archetype Report")]
        public static void Generate()
        {
            string[] guids = AssetDatabase.FindAssets("t:FaunaBiomeData");
            string[] archetypeGuids = AssetDatabase.FindAssets("t:CreatureArchetypeData");
            var roleCounts = new Dictionary<CreatureRoleType, int>(16);
            var locomotionCounts = new Dictionary<CreatureLocomotionType, int>(8);
            var missing = new List<string>(16);

            int datasetCount = 0;
            int totalEntries = 0;
            int coveredEntries = 0;
            int archetypeAssetCount = 0;
            int archetypeWithoutPrefabCount = 0;

            foreach (string archetypeGuid in archetypeGuids)
            {
                string archetypePath = AssetDatabase.GUIDToAssetPath(archetypeGuid);
                CreatureArchetypeData archetype = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(archetypePath);
                if (archetype == null)
                    continue;

                archetypeAssetCount++;
                if (archetype.prefab == null)
                    archetypeWithoutPrefabCount++;
            }

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                FaunaBiomeData data = AssetDatabase.LoadAssetAtPath<FaunaBiomeData>(assetPath);
                if (data == null)
                    continue;

                datasetCount++;

                List<FaunaEntry> entries = data.possibleCreatures;
                int entryCount = entries != null ? entries.Count : 0;
                for (int i = 0; i < entryCount; i++)
                {
                    totalEntries++;
                    FaunaEntry entry = entries[i];

                    if (entry.archetype == null)
                    {
                        string resolvedName = entry.prefab != null ? entry.prefab.name : "NULL";
                        missing.Add($"- `{data.biomeName}` (`biomeIndex={data.biomeIndex}`) -> `{resolvedName}`");
                        continue;
                    }

                    coveredEntries++;

                    if (!roleCounts.ContainsKey(entry.archetype.roleType))
                        roleCounts[entry.archetype.roleType] = 0;
                    roleCounts[entry.archetype.roleType]++;

                    if (!locomotionCounts.ContainsKey(entry.archetype.locomotionType))
                        locomotionCounts[entry.archetype.locomotionType] = 0;
                    locomotionCounts[entry.archetype.locomotionType]++;
                }
            }

            var sb = new StringBuilder(4096);
            sb.AppendLine("# AI Fauna Archetype Report");
            sb.AppendLine();
            sb.AppendLine("## Svodka");
            sb.AppendLine();
            sb.AppendLine($"- Datasetov biomov: `{datasetCount}`");
            sb.AppendLine($"- Vsego zapisey fauny: `{totalEntries}`");
            sb.AppendLine($"- S profilem vida: `{coveredEntries}`");
            sb.AppendLine($"- Bez profilya vida: `{totalEntries - coveredEntries}`");
            sb.AppendLine($"- Gotovyh profiley vida v proekte: `{archetypeAssetCount}`");
            sb.AppendLine($"- Iz nih bez prefaba: `{archetypeWithoutPrefabCount}`");
            sb.AppendLine();
            sb.AppendLine("## Po rolyam");
            sb.AppendLine();

            foreach (CreatureRoleType role in System.Enum.GetValues(typeof(CreatureRoleType)))
            {
                roleCounts.TryGetValue(role, out int count);
                sb.AppendLine($"- `{role}`: `{count}`");
            }

            sb.AppendLine();
            sb.AppendLine("## Po tipu dvizheniya");
            sb.AppendLine();

            foreach (CreatureLocomotionType locomotion in System.Enum.GetValues(typeof(CreatureLocomotionType)))
            {
                locomotionCounts.TryGetValue(locomotion, out int count);
                sb.AppendLine($"- `{locomotion}`: `{count}`");
            }

            sb.AppendLine();
            sb.AppendLine("## Dyry");
            sb.AppendLine();

            if (missing.Count == 0)
            {
                sb.AppendLine("- Vse zapisi fauny uzhe privyazany k profilyam vida.");
            }
            else
            {
                for (int i = 0; i < missing.Count; i++)
                    sb.AppendLine(missing[i]);
            }

            File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[FaunaArchetypeReportGenerator] Report written: {ReportPath}");
        }
    }
}
