using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.AI.Editor
{
    public static class CreatureRosterReportGenerator
    {
        private const string ReportPath = "C:/hades/Hecton8/AI_CREATURE_ROSTER_REPORT.md";

        [MenuItem("Hecton/Validation/Generate AI Creature Roster Report", priority = 246)]
        public static void Generate()
        {
            string[] guids = AssetDatabase.FindAssets("t:CreatureArchetypeData");
            var all = new List<CreatureArchetypeData>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CreatureArchetypeData asset = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(path);
                if (asset != null)
                    all.Add(asset);
            }

            all.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));

            var sb = new StringBuilder(16384);
            sb.AppendLine("# AI Creature Roster Report");
            sb.AppendLine();
            sb.AppendLine("## Сводка");
            sb.AppendLine();
            sb.AppendLine($"- Всего профилей видов: `{all.Count}`");
            sb.AppendLine($"- Мирной жизни: `{CountByRole(all, CreatureRoleType.Ambient)}`");
            sb.AppendLine($"- Территориальных: `{CountByRole(all, CreatureRoleType.Territorial)}`");
            sb.AppendLine($"- Хищников: `{CountByRole(all, CreatureRoleType.Hunter)}`");
            sb.AppendLine($"- Левиафанов: `{CountByRole(all, CreatureRoleType.Leviathan)}`");
            sb.AppendLine();

            AppendRoleSection(sb, all, CreatureRoleType.Ambient, "Мирная жизнь");
            AppendRoleSection(sb, all, CreatureRoleType.Territorial, "Территориальные");
            AppendRoleSection(sb, all, CreatureRoleType.Hunter, "Хищники");
            AppendRoleSection(sb, all, CreatureRoleType.Leviathan, "Левиафаны");
            AppendFaunaFamilySuggestions(sb, all);

            File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static int CountByRole(List<CreatureArchetypeData> all, CreatureRoleType role)
        {
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].roleType == role)
                    count++;
            }

            return count;
        }

        private static void AppendRoleSection(StringBuilder sb, List<CreatureArchetypeData> all, CreatureRoleType role, string title)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();

            for (int i = 0; i < all.Count; i++)
            {
                CreatureArchetypeData asset = all[i];
                if (asset.roleType != role)
                    continue;

                sb.AppendLine($"### {asset.displayName}");
                sb.AppendLine();
                sb.AppendLine($"- `ID`: `{asset.creatureId}`");
                sb.AppendLine($"- `Задача`: {asset.gameplayPurpose}");
                sb.AppendLine($"- `Движение`: `{asset.locomotionType}`");
                sb.AppendLine($"- `Скорость`: `{asset.cruiseSpeed:0.0} / {asset.burstSpeed:0.0}`");
                sb.AppendLine($"- `Живучесть`: `{asset.maxHealth:0}`");
                sb.AppendLine($"- `Атака`: `{asset.attackDamage:0}`");
                sb.AppendLine($"- `Особое`: {BuildSpecialLine(asset)}");
                sb.AppendLine($"- `Семейства фауны`: {Join(asset.recommendedFaunaFamilyIds)}");
                sb.AppendLine($"- `Биомы`: {Join(asset.recommendedBiomeFamilyIds)}");
                sb.AppendLine();
            }
        }

        private static void AppendFaunaFamilySuggestions(StringBuilder sb, List<CreatureArchetypeData> all)
        {
            string[] faunaFamilyIds =
            {
                "fauna.family.littoral_passive",
                "fauna.family.escarpment_watchers",
                "fauna.family.ridge_hunters",
                "fauna.family.crystal_skittish",
                "fauna.family.reef_ambush",
                "fauna.family.chemical_specialists",
                "fauna.family.rift_stalkers",
                "fauna.family.abyssal_sparse",
                "fauna.family.thermal_hostile",
                "fauna.family.sediment_scavengers",
                "fauna.family.metal_predators",
                "fauna.family.hadal_apex",
                "fauna.family.void_apex"
            };

            sb.AppendLine("## Куда кого сажать");
            sb.AppendLine();

            for (int i = 0; i < faunaFamilyIds.Length; i++)
            {
                string faunaFamilyId = faunaFamilyIds[i];
                sb.AppendLine($"### {faunaFamilyId}");
                sb.AppendLine();

                bool foundAny = false;
                for (int j = 0; j < all.Count; j++)
                {
                    CreatureArchetypeData asset = all[j];
                    if (!Contains(asset.recommendedFaunaFamilyIds, faunaFamilyId))
                        continue;

                    foundAny = true;
                    sb.AppendLine($"- `{asset.displayName}` ({asset.roleType})");
                }

                if (!foundAny)
                    sb.AppendLine("- Пока никто не предложен.");

                sb.AppendLine();
            }
        }

        private static bool Contains(string[] values, string needle)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], needle, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string BuildSpecialLine(CreatureArchetypeData asset)
        {
            var tags = new List<string>(6);

            if (asset.defendNest)
                tags.Add("защита гнезда");
            if (asset.callNearbyAllies)
                tags.Add("зовёт соседей");
            if (asset.usePackHunt)
                tags.Add("стайная охота");
            if (asset.useFeintRush)
                tags.Add("ложный заход");
            if (asset.useLeviathanPresence)
                tags.Add($"сценарий {asset.leviathanEncounterType}");
            if (asset.useCandiceBehaviorTree)
                tags.Add("готов под Candice");

            return tags.Count == 0 ? "без спецрежима" : string.Join(", ", tags);
        }

        private static string Join(string[] values)
        {
            if (values == null || values.Length == 0)
                return "не задано";

            return string.Join(", ", values);
        }
    }
}
