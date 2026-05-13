using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Validates shared procedural family contracts across flora, geology, structure, and future verticals.
    /// </summary>
    public static class WorldProceduralFamilyContractValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string ReportFileName = "PROCEDURAL_WORLD_FAMILY_STATUS_REPORT.md";

        /// <summary>
        /// Validates all procedural family profiles under the managed family root.
        /// </summary>
        [MenuItem("Hecton/Validation/Validate Procedural Family Contracts", priority = 243)]
        public static void ValidateContracts()
        {
            List<FamilyRecord> records = LoadFamilyRecords();
            Dictionary<string, int> familyIdCounts = BuildFamilyIdCounts(records);
            int errorCount = 0;
            int warningCount = 0;
            int placeholderOnlyCount = 0;
            int realFinalFamilyCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                FamilyRecord record = records[i];
                WorldPrefabFamilyProfile family = record.Family;
                if (family == null)
                    continue;

                ValidateFamilyIdentity(record, familyIdCounts, ref errorCount);
                ValidatePlacementContract(record, ref errorCount, ref warningCount);

                VariantMetrics metrics = MeasureVariants(family);
                if (metrics.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (metrics.PlaceholderFinalCount > 0)
                    placeholderOnlyCount++;

                ValidateVariants(record, metrics, ref errorCount, ref warningCount);
            }

            if (errorCount <= 0)
            {
                Debug.Log($"[WorldProceduralFamilyContractValidator] PASS families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyCount}, warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[WorldProceduralFamilyContractValidator] COMPLETE errors={errorCount}, warnings={warningCount}, families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyCount}");
        }

        /// <summary>
        /// Generates a shared family readiness report for all procedural verticals.
        /// </summary>
        [MenuItem("Hecton/Validation/Generate Procedural World Family Status Report", priority = 244)]
        public static void GenerateStatusReport()
        {
            List<FamilyRecord> records = LoadFamilyRecords();
            Dictionary<string, int> familyIdCounts = BuildFamilyIdCounts(records);
            List<FamilyStatus> statuses = new List<FamilyStatus>(records.Count);
            Dictionary<string, VerticalSummary> summaries = new Dictionary<string, VerticalSummary>(StringComparer.Ordinal);

            for (int i = 0; i < records.Count; i++)
            {
                FamilyRecord record = records[i];
                WorldPrefabFamilyProfile family = record.Family;
                if (family == null)
                    continue;

                VariantMetrics metrics = MeasureVariants(family);
                FamilyStatus status = BuildStatus(record, metrics, familyIdCounts);
                statuses.Add(status);

                if (!summaries.TryGetValue(status.Vertical, out VerticalSummary summary))
                    summary = new VerticalSummary(status.Vertical);

                summary.TotalFamilies++;
                if (status.RealFinalCount > 0)
                    summary.FamiliesWithRealFinals++;
                if (status.PlaceholderFinalCount > 0)
                    summary.FamiliesWithPlaceholderOnly++;
                if (status.UsesGenerativeGeology)
                    summary.FamiliesUsingGenerativeGeology++;
                if (!status.AllowRuntimeScatter)
                    summary.RuntimeDisabledFamilies++;

                summaries[status.Vertical] = summary;
            }

            statuses.Sort((a, b) =>
            {
                int verticalCompare = string.CompareOrdinal(a.Vertical, b.Vertical);
                return verticalCompare != 0 ? verticalCompare : string.CompareOrdinal(a.FamilyId, b.FamilyId);
            });

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(statuses, summaries), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralFamilyContractValidator] Wrote report to {reportPath}");
        }

        private static List<FamilyRecord> LoadFamilyRecords()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyRecord> records = new List<FamilyRecord>(familyGuids.Length);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null)
                    continue;

                records.Add(new FamilyRecord(assetPath, family));
            }

            return records;
        }

        private static Dictionary<string, int> BuildFamilyIdCounts(IReadOnlyList<FamilyRecord> records)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(records.Count, StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                string familyId = records[i].Family != null ? records[i].Family.familyId : string.Empty;
                if (string.IsNullOrWhiteSpace(familyId))
                    continue;

                if (counts.TryGetValue(familyId, out int count))
                    counts[familyId] = count + 1;
                else
                    counts.Add(familyId, 1);
            }

            return counts;
        }

        private static void ValidateFamilyIdentity(
            FamilyRecord record,
            IReadOnlyDictionary<string, int> familyIdCounts,
            ref int errorCount)
        {
            string familyId = record.Family.familyId;
            if (string.IsNullOrWhiteSpace(familyId))
            {
                Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: familyId is empty.");
                errorCount++;
                return;
            }

            if (!familyIdCounts.TryGetValue(familyId, out int count) || count <= 1)
                return;

            Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: duplicate familyId '{familyId}' detected ({count} assets).");
            errorCount++;
        }

        private static void ValidatePlacementContract(
            FamilyRecord record,
            ref int errorCount,
            ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            if (family.minSpacingMeters < 0.1f)
            {
                Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: minSpacingMeters must be >= 0.1.");
                errorCount++;
            }

            if (family.clusterRadiusMeters < 0f)
            {
                Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: clusterRadiusMeters must be >= 0.");
                errorCount++;
            }

            if (family.clusterCountMin < 1 || family.clusterCountMax < 1)
            {
                Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: cluster counts must be positive.");
                errorCount++;
            }
            else if (family.clusterCountMin > family.clusterCountMax)
            {
                Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: clusterCountMin exceeds clusterCountMax.");
                errorCount++;
            }

            if (family.allowRuntimeScatter && family.variants == null)
            {
                Debug.LogWarning($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: runtime scatter is enabled but variants array is null.");
                warningCount++;
            }
        }

        private static void ValidateVariants(
            FamilyRecord record,
            VariantMetrics metrics,
            ref int errorCount,
            ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            HashSet<string> variantIds = new HashSet<string>(StringComparer.Ordinal);
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null)
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: variant[{i}] is null.");
                    errorCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(variant.variantId))
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: variant[{i}] has empty variantId.");
                    errorCount++;
                }
                else if (!variantIds.Add(variant.variantId))
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: duplicate variantId '{variant.variantId}'.");
                    errorCount++;
                }

                if (variant.weight < 1)
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: variant '{variant.variantId}' has weight < 1.");
                    errorCount++;
                }

                if (variant.uniformScaleRange.x <= 0f || variant.uniformScaleRange.y < variant.uniformScaleRange.x)
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: variant '{variant.variantId}' has invalid uniformScaleRange {variant.uniformScaleRange}.");
                    errorCount++;
                }

                if (variant.finalReady && variant.prefab == null)
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: final-ready variant '{variant.variantId}' is missing prefab.");
                    errorCount++;
                }

                if (!variant.proxyOnly && !variant.finalReady)
                {
                    Debug.LogError($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: variant '{variant.variantId}' is marked non-proxy but not final-ready.");
                    errorCount++;
                }
            }

            if (family.allowRuntimeScatter && variants.Length <= 0 && !family.UsesGenerativeGeology())
            {
                Debug.LogWarning($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: runtime scatter is enabled but family has no variants and no generative geology fallback.");
                warningCount++;
            }

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
            {
                Debug.LogWarning($"[WorldProceduralFamilyContractValidator] {record.AssetPath}: only placeholder finals are linked.");
                warningCount++;
            }
        }

        private static VariantMetrics MeasureVariants(WorldPrefabFamilyProfile family)
        {
            VariantMetrics metrics = new VariantMetrics();
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            metrics.TotalVariants = variants.Length;

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null)
                    continue;

                if (variant.proxyOnly)
                    metrics.ProxyVariantCount++;

                if (!variant.finalReady || variant.proxyOnly)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    metrics.PlaceholderFinalCount++;
                else
                    metrics.RealFinalCount++;
            }

            return metrics;
        }

        private static FamilyStatus BuildStatus(
            FamilyRecord record,
            VariantMetrics metrics,
            IReadOnlyDictionary<string, int> familyIdCounts)
        {
            WorldPrefabFamilyProfile family = record.Family;
            StringBuilder notes = new StringBuilder(96);

            if (string.IsNullOrWhiteSpace(family.familyId))
                AppendNote(notes, "missing-familyId");
            else if (familyIdCounts.TryGetValue(family.familyId, out int familyIdCount) && familyIdCount > 1)
                AppendNote(notes, $"duplicate-familyId:{familyIdCount}");

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
                AppendNote(notes, "placeholder-only");
            else if (metrics.RealFinalCount > 0 && metrics.PlaceholderFinalCount > 0)
                AppendNote(notes, "real-plus-placeholder");
            else if (metrics.RealFinalCount <= 0)
                AppendNote(notes, "no-real-finals");

            if (family.UsesGenerativeGeology())
                AppendNote(notes, "uses-generative-geology");
            if (!family.allowRuntimeScatter)
                AppendNote(notes, "runtime-scatter-disabled");
            if (!family.allowProxyPrimitives)
                AppendNote(notes, "proxy-primitives-disabled");

            return new FamilyStatus(
                ResolveVerticalLabel(family.proceduralDomain),
                family.familyId ?? string.Empty,
                family.familyLabel ?? string.Empty,
                family.proceduralDomain.ToString(),
                family.scatterLayer.ToString(),
                family.ResolveStreamingLayer().ToString(),
                family.allowRuntimeScatter,
                family.UsesGenerativeGeology(),
                metrics.TotalVariants,
                metrics.ProxyVariantCount,
                metrics.RealFinalCount,
                metrics.PlaceholderFinalCount,
                notes.Length > 0 ? notes.ToString() : "ok");
        }

        private static string BuildMarkdown(
            IReadOnlyList<FamilyStatus> statuses,
            IReadOnlyDictionary<string, VerticalSummary> summaries)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# Procedural World Family Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(ProceduralFamilyFolder).AppendLine("`");
            builder.AppendLine("- Purpose: shared readiness view for flora, geology, structure, interior-decor, and colony expansion.");
            builder.AppendLine("- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.");
            builder.AppendLine("- Placeholder finals: final-ready variants generated by `WorldProceduralPlaceholderAuthoring`.");
            builder.AppendLine("- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.");
            builder.AppendLine();
            builder.AppendLine("## Vertical Summary");
            builder.AppendLine();
            builder.AppendLine("| Vertical | Families | Real Final Families | Placeholder-Only Families | Uses Generative Geology | Runtime Disabled |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

            List<string> verticalKeys = new List<string>(summaries.Keys);
            verticalKeys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < verticalKeys.Count; i++)
            {
                VerticalSummary summary = summaries[verticalKeys[i]];
                builder.Append("| ").Append(summary.Vertical)
                    .Append(" | ").Append(summary.TotalFamilies)
                    .Append(" | ").Append(summary.FamiliesWithRealFinals)
                    .Append(" | ").Append(summary.FamiliesWithPlaceholderOnly)
                    .Append(" | ").Append(summary.FamiliesUsingGenerativeGeology)
                    .Append(" | ").Append(summary.RuntimeDisabledFamilies)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Family Detail");
            builder.AppendLine();
            builder.AppendLine("| Family | Vertical | Domain | Scatter | Streaming | Variants | Proxy | Real Finals | Placeholder Finals | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                builder.Append("| ").Append(status.FamilyId)
                    .Append(" | ").Append(status.Vertical)
                    .Append(" | ").Append(status.Domain)
                    .Append(" | ").Append(status.ScatterLayer)
                    .Append(" | ").Append(status.StreamingLayer)
                    .Append(" | ").Append(status.TotalVariants)
                    .Append(" | ").Append(status.ProxyVariantCount)
                    .Append(" | ").Append(status.RealFinalCount)
                    .Append(" | ").Append(status.PlaceholderFinalCount)
                    .Append(" | ").Append(status.Notes)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Readiness Notes");
            builder.AppendLine();
            builder.AppendLine("- `ORGANIC` is currently the strictest pipeline because flora already has shader/material/texture/LOD validators.");
            builder.AppendLine("- `GEOLOGICAL` already has runtime bootstrap and generative fallback, so it is the next category to harden with family-specific validator/report rules.");
            builder.AppendLine("- `STRUCTURAL`, `INTERIOR_DECOR`, and `COLONY_PARTS` must extend existing family/domain ownership instead of creating a second scatter/runtime stack.");
            return builder.ToString();
        }

        private static string ResolveVerticalLabel(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Kelp:
                case WorldPrefabFamilyProfile.ProceduralDomain.Plant:
                case WorldPrefabFamilyProfile.ProceduralDomain.Coral:
                case WorldPrefabFamilyProfile.ProceduralDomain.Egg:
                    return "ORGANIC";

                case WorldPrefabFamilyProfile.ProceduralDomain.Rock:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockCluster:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockShelf:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return "GEOLOGICAL";

                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return "STRUCTURAL";

                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.SafePocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn:
                    return "WORLD_SUPPORT";

                default:
                    return "GENERIC";
            }
        }

        private static void AppendNote(StringBuilder builder, string note)
        {
            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(note);
        }

        private struct FamilyRecord
        {
            public FamilyRecord(string assetPath, WorldPrefabFamilyProfile family)
            {
                AssetPath = assetPath ?? string.Empty;
                Family = family;
            }

            public string AssetPath { get; }
            public WorldPrefabFamilyProfile Family { get; }
        }

        private struct VariantMetrics
        {
            public int TotalVariants;
            public int ProxyVariantCount;
            public int RealFinalCount;
            public int PlaceholderFinalCount;
        }

        private struct FamilyStatus
        {
            public FamilyStatus(
                string vertical,
                string familyId,
                string familyLabel,
                string domain,
                string scatterLayer,
                string streamingLayer,
                bool allowRuntimeScatter,
                bool usesGenerativeGeology,
                int totalVariants,
                int proxyVariantCount,
                int realFinalCount,
                int placeholderFinalCount,
                string notes)
            {
                Vertical = vertical;
                FamilyId = familyId;
                FamilyLabel = familyLabel;
                Domain = domain;
                ScatterLayer = scatterLayer;
                StreamingLayer = streamingLayer;
                AllowRuntimeScatter = allowRuntimeScatter;
                UsesGenerativeGeology = usesGenerativeGeology;
                TotalVariants = totalVariants;
                ProxyVariantCount = proxyVariantCount;
                RealFinalCount = realFinalCount;
                PlaceholderFinalCount = placeholderFinalCount;
                Notes = notes;
            }

            public string Vertical { get; }
            public string FamilyId { get; }
            public string FamilyLabel { get; }
            public string Domain { get; }
            public string ScatterLayer { get; }
            public string StreamingLayer { get; }
            public bool AllowRuntimeScatter { get; }
            public bool UsesGenerativeGeology { get; }
            public int TotalVariants { get; }
            public int ProxyVariantCount { get; }
            public int RealFinalCount { get; }
            public int PlaceholderFinalCount { get; }
            public string Notes { get; }
        }

        private struct VerticalSummary
        {
            public VerticalSummary(string vertical)
            {
                Vertical = vertical ?? string.Empty;
                TotalFamilies = 0;
                FamiliesWithRealFinals = 0;
                FamiliesWithPlaceholderOnly = 0;
                FamiliesUsingGenerativeGeology = 0;
                RuntimeDisabledFamilies = 0;
            }

            public string Vertical;
            public int TotalFamilies;
            public int FamiliesWithRealFinals;
            public int FamiliesWithPlaceholderOnly;
            public int FamiliesUsingGenerativeGeology;
            public int RuntimeDisabledFamilies;
        }
    }
}
