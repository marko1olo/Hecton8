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
    /// Writes a world-support readiness report for pockets, creature spawns, and large-threat ownership zones.
    /// </summary>
    public static class WorldProceduralSupportStatusReport
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string ReportFileName = "PROCEDURAL_WORLD_SUPPORT_STATUS_REPORT.md";

        [MenuItem("Hecton/Validation/Generate Procedural World Support Status Report", priority = 250)]
        public static void GenerateReport()
        {
            List<FamilyStatus> statuses = LoadStatuses();
            statuses.Sort(static (a, b) => string.CompareOrdinal(a.FamilyId, b.FamilyId));

            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int largeThreatZoneCount = 0;
            int managedMaterialFamilyCount = 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                if (status.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (status.PlaceholderFinalCount > 0)
                    placeholderOnlyFamilyCount++;

                if (status.ContributesLargeThreatZone)
                    largeThreatZoneCount++;

                if (status.HasManagedSupportMaterialStack)
                    managedMaterialFamilyCount++;
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(statuses, realFinalFamilyCount, placeholderOnlyFamilyCount, largeThreatZoneCount, managedMaterialFamilyCount), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralSupportStatusReport] Wrote report to {reportPath}");
        }

        private static List<FamilyStatus> LoadStatuses()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyStatus> statuses = new List<FamilyStatus>(12);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !WorldProceduralSupportContract.IsSupportDomain(family.proceduralDomain))
                    continue;

                statuses.Add(BuildStatus(assetPath, family));
            }

            return statuses;
        }

        private static FamilyStatus BuildStatus(string assetPath, WorldPrefabFamilyProfile family)
        {
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            int realFinalCount = 0;
            int placeholderFinalCount = 0;
            int maxRendererCount = 0;
            int maxLodGroupCount = 0;
            bool missingRequiredLod = false;
            bool hasManagedSupportMaterialStack = true;
            StringBuilder notes = new StringBuilder(96);

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    placeholderFinalCount++;
                    continue;
                }

                realFinalCount++;
                Renderer[] renderers = variant.prefab.GetComponentsInChildren<Renderer>(true);
                LODGroup[] lodGroups = variant.prefab.GetComponentsInChildren<LODGroup>(true);
                maxRendererCount = Mathf.Max(maxRendererCount, renderers != null ? renderers.Length : 0);
                maxLodGroupCount = Mathf.Max(maxLodGroupCount, lodGroups != null ? lodGroups.Length : 0);

                if (WorldProceduralSupportContract.RequiresSupportLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                    missingRequiredLod = true;

                if (!AppendMaterialContractFindings(notes, renderers))
                    hasManagedSupportMaterialStack = false;

                AppendLodContractFindings(notes, lodGroups);
            }

            if (realFinalCount <= 0 && placeholderFinalCount > 0)
                AppendNote(notes, "placeholder-only");
            else if (realFinalCount <= 0)
                AppendNote(notes, "no-real-finals");

            if (missingRequiredLod)
                AppendNote(notes, "required-real-final-missing-lodgroup");

            if (maxRendererCount > WorldProceduralSupportContract.ResolveRendererBudget(family))
                AppendNote(notes, $"renderer-budget-soft-exceeded:{maxRendererCount}>{WorldProceduralSupportContract.ResolveRendererBudget(family)}");

            if (realFinalCount > 0 && !hasManagedSupportMaterialStack)
                AppendNote(notes, "managed-support-material-stack-incomplete");

            return new FamilyStatus(
                assetPath,
                family.familyId ?? string.Empty,
                family.proceduralDomain.ToString(),
                family.ResolveStreamingLayer().ToString(),
                family.contributesLargeThreatZone,
                realFinalCount,
                placeholderFinalCount,
                maxRendererCount,
                maxLodGroupCount,
                realFinalCount > 0 && hasManagedSupportMaterialStack,
                notes.Length > 0 ? notes.ToString() : "ok");
        }

        private static string BuildMarkdown(
            IReadOnlyList<FamilyStatus> statuses,
            int realFinalFamilyCount,
            int placeholderOnlyFamilyCount,
            int largeThreatZoneCount,
            int managedMaterialFamilyCount)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# Procedural World Support Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(ProceduralFamilyFolder).AppendLine("`");
            builder.AppendLine("- Scope: support procedural families only (`ResourcePocket`, `HazardPocket`, `SafePocket`, `CreatureSpawn`).");
            builder.AppendLine("- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.");
            builder.AppendLine("- Large-threat zones are support families with `contributesLargeThreatZone=true`.");
            builder.AppendLine("- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.Append("- Support families: `").Append(statuses.Count).AppendLine("`");
            builder.Append("- Families with real finals: `").Append(realFinalFamilyCount).AppendLine("`");
            builder.Append("- Placeholder-only families: `").Append(placeholderOnlyFamilyCount).AppendLine("`");
            builder.Append("- Large-threat zone families: `").Append(largeThreatZoneCount).AppendLine("`");
            builder.Append("- Families with managed support material stack: `").Append(managedMaterialFamilyCount).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("## Family Table");
            builder.AppendLine();
            builder.AppendLine("| Family | Domain | Streaming | Large Threat Zone | Real Finals | Placeholder Finals | Max Renderers | Max LODGroups | Managed Support Material Stack | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                builder.Append("| ").Append(status.FamilyId)
                    .Append(" | ").Append(status.Domain)
                    .Append(" | ").Append(status.StreamingLayer)
                    .Append(" | ").Append(status.ContributesLargeThreatZone ? "yes" : "no")
                    .Append(" | ").Append(status.RealFinalCount)
                    .Append(" | ").Append(status.PlaceholderFinalCount)
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.MaxLodGroupCount)
                    .Append(" | ").Append(status.HasManagedSupportMaterialStack ? "yes" : "no")
                    .Append(" | ").Append(status.Notes)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Readiness Notes");
            builder.AppendLine();
            builder.Append("- Real-final support baseline: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: true);
            builder.AppendLine();
            builder.Append("- Placeholder-driven support families: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: false);
            builder.AppendLine();
            builder.AppendLine("- Support validator now checks routing, managed support materials, and LOD coverage for large-threat ownership zones.");
            return builder.ToString();
        }

        private static void AppendFamilyNoteList(StringBuilder builder, IReadOnlyList<FamilyStatus> statuses, bool requireRealFinals)
        {
            bool appendedAny = false;
            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                bool matches = requireRealFinals
                    ? status.RealFinalCount > 0
                    : status.RealFinalCount <= 0 && status.PlaceholderFinalCount > 0;

                if (!matches)
                    continue;

                if (appendedAny)
                    builder.Append(", ");

                builder.Append('`').Append(status.FamilyId).Append('`');
                appendedAny = true;
            }

            if (!appendedAny)
                builder.Append("`none`");
        }

        private static bool AppendMaterialContractFindings(StringBuilder notes, Renderer[] renderers)
        {
            if (renderers == null || renderers.Length <= 0)
                return false;

            bool allMaterialsValid = true;
            HashSet<Material> inspectedMaterials = new HashSet<Material>();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length <= 0)
                {
                    AppendNote(notes, $"renderer-without-material:{renderer.name}");
                    allMaterialsValid = false;
                    continue;
                }

                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material material = sharedMaterials[materialIndex];
                    if (material == null)
                    {
                        AppendNote(notes, $"null-material-slot:{renderer.name}:{materialIndex}");
                        allMaterialsValid = false;
                        continue;
                    }

                    if (!inspectedMaterials.Add(material))
                        continue;

                    if (WorldProceduralSupportContract.TryGetMaterialContractFailure(material, out string failureLabel))
                    {
                        AppendNote(notes, $"material-contract-fail:{failureLabel}");
                        allMaterialsValid = false;
                    }
                }
            }

            return allMaterialsValid;
        }

        private static void AppendLodContractFindings(StringBuilder notes, LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length <= 0)
                return;

            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                if (WorldProceduralSupportContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
                    AppendNote(notes, $"lod-contract-fail:{failureLabel}");
            }
        }

        private static void AppendNote(StringBuilder builder, string note)
        {
            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(note);
        }

        private struct FamilyStatus
        {
            public FamilyStatus(
                string assetPath,
                string familyId,
                string domain,
                string streamingLayer,
                bool contributesLargeThreatZone,
                int realFinalCount,
                int placeholderFinalCount,
                int maxRendererCount,
                int maxLodGroupCount,
                bool hasManagedSupportMaterialStack,
                string notes)
            {
                AssetPath = assetPath;
                FamilyId = familyId;
                Domain = domain;
                StreamingLayer = streamingLayer;
                ContributesLargeThreatZone = contributesLargeThreatZone;
                RealFinalCount = realFinalCount;
                PlaceholderFinalCount = placeholderFinalCount;
                MaxRendererCount = maxRendererCount;
                MaxLodGroupCount = maxLodGroupCount;
                HasManagedSupportMaterialStack = hasManagedSupportMaterialStack;
                Notes = notes;
            }

            public string AssetPath { get; }
            public string FamilyId { get; }
            public string Domain { get; }
            public string StreamingLayer { get; }
            public bool ContributesLargeThreatZone { get; }
            public int RealFinalCount { get; }
            public int PlaceholderFinalCount { get; }
            public int MaxRendererCount { get; }
            public int MaxLodGroupCount { get; }
            public bool HasManagedSupportMaterialStack { get; }
            public string Notes { get; }
        }
    }
}
