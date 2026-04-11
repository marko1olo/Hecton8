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
    /// Writes a focused readiness report for egg-cluster and giant-plant families.
    /// </summary>
    public static class WorldProceduralOrganicMiscStatusReport
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string ReportFileName = "PROCEDURAL_ORGANIC_MISC_STATUS_REPORT.md";

        [MenuItem("Hecton/Validation/Generate Procedural Organic Misc Status Report", priority = 242)]
        public static void GenerateReport()
        {
            List<FamilyStatus> statuses = LoadStatuses();
            statuses.Sort(static (a, b) => string.CompareOrdinal(a.FamilyId, b.FamilyId));

            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int managedMaterialFamilyCount = 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                if (status.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (status.PlaceholderFinalCount > 0)
                    placeholderOnlyFamilyCount++;

                if (status.HasManagedOrganicMaterialStack)
                    managedMaterialFamilyCount++;
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(statuses, realFinalFamilyCount, placeholderOnlyFamilyCount, managedMaterialFamilyCount), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralOrganicMiscStatusReport] Wrote report to {reportPath}");
        }

        private static List<FamilyStatus> LoadStatuses()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyStatus> statuses = new List<FamilyStatus>(4);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !WorldProceduralOrganicMiscContract.IsOrganicMiscFamily(family.familyId))
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
            bool hasManagedOrganicMaterialStack = true;
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

                if (WorldProceduralOrganicMiscContract.RequiresOrganicLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                    missingRequiredLod = true;

                if (!AppendMaterialContractFindings(notes, renderers))
                    hasManagedOrganicMaterialStack = false;

                AppendLodContractFindings(notes, lodGroups);
            }

            if (realFinalCount <= 0 && placeholderFinalCount > 0)
                AppendNote(notes, "placeholder-only");
            else if (realFinalCount <= 0)
                AppendNote(notes, "no-real-finals");

            if (missingRequiredLod)
                AppendNote(notes, "required-real-final-missing-lodgroup");

            if (maxRendererCount > WorldProceduralOrganicMiscContract.ResolveRendererBudget(family))
                AppendNote(notes, $"renderer-budget-soft-exceeded:{maxRendererCount}>{WorldProceduralOrganicMiscContract.ResolveRendererBudget(family)}");

            if (realFinalCount > 0 && !hasManagedOrganicMaterialStack)
                AppendNote(notes, "managed-organic-material-stack-incomplete");

            return new FamilyStatus(
                assetPath,
                family.familyId ?? string.Empty,
                family.proceduralDomain.ToString(),
                family.ResolveStreamingLayer().ToString(),
                realFinalCount,
                placeholderFinalCount,
                maxRendererCount,
                maxLodGroupCount,
                realFinalCount > 0 && hasManagedOrganicMaterialStack,
                notes.Length > 0 ? notes.ToString() : "ok");
        }

        private static string BuildMarkdown(
            IReadOnlyList<FamilyStatus> statuses,
            int realFinalFamilyCount,
            int placeholderOnlyFamilyCount,
            int managedMaterialFamilyCount)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Organic Misc Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(ProceduralFamilyFolder).AppendLine("`");
            builder.AppendLine("- Scope: organic procedural families outside the main kelp/coral baked pipeline (`Egg`, `Plant`).");
            builder.AppendLine("- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.");
            builder.AppendLine("- Managed materials must live under `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc`.");
            builder.AppendLine("- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.Append("- Organic misc families: `").Append(statuses.Count).AppendLine("`");
            builder.Append("- Families with real finals: `").Append(realFinalFamilyCount).AppendLine("`");
            builder.Append("- Placeholder-only families: `").Append(placeholderOnlyFamilyCount).AppendLine("`");
            builder.Append("- Families with managed organic material stack: `").Append(managedMaterialFamilyCount).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("## Family Table");
            builder.AppendLine();
            builder.AppendLine("| Family | Domain | Streaming | Real Finals | Placeholder Finals | Max Renderers | Max LODGroups | Managed Organic Material Stack | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                builder.Append("| ").Append(status.FamilyId)
                    .Append(" | ").Append(status.Domain)
                    .Append(" | ").Append(status.StreamingLayer)
                    .Append(" | ").Append(status.RealFinalCount)
                    .Append(" | ").Append(status.PlaceholderFinalCount)
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.MaxLodGroupCount)
                    .Append(" | ").Append(status.HasManagedOrganicMaterialStack ? "yes" : "no")
                    .Append(" | ").Append(status.Notes)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Readiness Notes");
            builder.AppendLine();
            builder.Append("- Real-final organic misc baseline: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: true);
            builder.AppendLine();
            builder.Append("- Placeholder-driven organic misc families: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: false);
            builder.AppendLine();
            builder.AppendLine("- This path currently enforces mesh/material/LOD discipline only. Authored texture and custom flora shader coverage are still separate decisions.");
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

                    if (WorldProceduralOrganicMiscContract.TryGetMaterialContractFailure(material, out string failureLabel))
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

                if (WorldProceduralOrganicMiscContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
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
                int realFinalCount,
                int placeholderFinalCount,
                int maxRendererCount,
                int maxLodGroupCount,
                bool hasManagedOrganicMaterialStack,
                string notes)
            {
                AssetPath = assetPath;
                FamilyId = familyId;
                Domain = domain;
                StreamingLayer = streamingLayer;
                RealFinalCount = realFinalCount;
                PlaceholderFinalCount = placeholderFinalCount;
                MaxRendererCount = maxRendererCount;
                MaxLodGroupCount = maxLodGroupCount;
                HasManagedOrganicMaterialStack = hasManagedOrganicMaterialStack;
                Notes = notes;
            }

            public string AssetPath { get; }
            public string FamilyId { get; }
            public string Domain { get; }
            public string StreamingLayer { get; }
            public int RealFinalCount { get; }
            public int PlaceholderFinalCount { get; }
            public int MaxRendererCount { get; }
            public int MaxLodGroupCount { get; }
            public bool HasManagedOrganicMaterialStack { get; }
            public string Notes { get; }
        }
    }
}
