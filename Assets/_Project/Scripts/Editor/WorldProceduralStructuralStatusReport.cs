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
    /// Writes a structural readiness report for debris, ruins, power routes, and service scars.
    /// </summary>
    public static class WorldProceduralStructuralStatusReport
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string ReportFileName = "PROCEDURAL_STRUCTURAL_STATUS_REPORT.md";

        /// <summary>
        /// Generates the structural status report.
        /// </summary>
        [MenuItem("Hecton/Validation/Generate Procedural Structural Status Report", priority = 248)]
        public static void GenerateReport()
        {
            List<FamilyStatus> statuses = LoadStatuses();
            statuses.Sort((a, b) => string.CompareOrdinal(a.FamilyId, b.FamilyId));

            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int debrisFamilyCount = 0;
            int ruinFamilyCount = 0;
            int serviceFamilyCount = 0;
            int managedMaterialFamilyCount = 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                if (status.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (status.PlaceholderFinalCount > 0)
                    placeholderOnlyFamilyCount++;

                if (status.HasManagedStructuralMaterialStack)
                    managedMaterialFamilyCount++;

                switch (status.Domain)
                {
                    case "Debris":
                        debrisFamilyCount++;
                        break;
                    case "RuinModule":
                        ruinFamilyCount++;
                        break;
                    case "PowerRoute":
                    case "ServiceScar":
                        serviceFamilyCount++;
                        break;
                }
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(statuses, realFinalFamilyCount, placeholderOnlyFamilyCount, debrisFamilyCount, ruinFamilyCount, serviceFamilyCount, managedMaterialFamilyCount), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralStructuralStatusReport] Wrote report to {reportPath}");
        }

        private static List<FamilyStatus> LoadStatuses()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyStatus> statuses = new List<FamilyStatus>(12);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !IsStructuralDomain(family.proceduralDomain))
                    continue;

                statuses.Add(BuildStatus(assetPath, family));
            }

            return statuses;
        }

        private static FamilyStatus BuildStatus(string assetPath, WorldPrefabFamilyProfile family)
        {
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            int proxyVariantCount = 0;
            int realFinalCount = 0;
            int placeholderFinalCount = 0;
            int maxRendererCount = 0;
            int maxMaterialSlots = 0;
            int maxLodGroupCount = 0;
            bool missingLodOnRequiredRealFinal = false;
            bool hasRealFinals = false;
            bool hasManagedStructuralMaterialStack = true;
            StringBuilder notes = new StringBuilder(96);

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null)
                    continue;

                if (variant.proxyOnly)
                    proxyVariantCount++;

                if (!variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    placeholderFinalCount++;
                    continue;
                }

                realFinalCount++;
                hasRealFinals = true;
                Renderer[] renderers = variant.prefab.GetComponentsInChildren<Renderer>(true);
                LODGroup[] lodGroups = variant.prefab.GetComponentsInChildren<LODGroup>(true);
                maxRendererCount = Mathf.Max(maxRendererCount, renderers != null ? renderers.Length : 0);
                maxMaterialSlots = Mathf.Max(maxMaterialSlots, CountMaterialSlots(renderers));
                maxLodGroupCount = Mathf.Max(maxLodGroupCount, lodGroups != null ? lodGroups.Length : 0);
                if (RequiresStructuralLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                    missingLodOnRequiredRealFinal = true;

                if (!AppendMaterialContractFindings(notes, renderers))
                    hasManagedStructuralMaterialStack = false;

                AppendLodContractFindings(notes, lodGroups);
            }

            if (realFinalCount <= 0 && placeholderFinalCount > 0)
                AppendNote(notes, "placeholder-only");
            else if (realFinalCount <= 0)
                AppendNote(notes, "no-real-finals");

            if (missingLodOnRequiredRealFinal)
                AppendNote(notes, "required-real-final-missing-lodgroup");

            if (maxRendererCount > ResolveRendererBudget(family))
                AppendNote(notes, $"renderer-budget-soft-exceeded:{maxRendererCount}>{ResolveRendererBudget(family)}");

            if (hasRealFinals && !hasManagedStructuralMaterialStack)
                AppendNote(notes, "managed-material-stack-incomplete");

            return new FamilyStatus(
                assetPath,
                family.familyId ?? string.Empty,
                family.familyLabel ?? string.Empty,
                family.proceduralDomain.ToString(),
                family.ResolveStreamingLayer().ToString(),
                variants.Length,
                proxyVariantCount,
                realFinalCount,
                placeholderFinalCount,
                maxRendererCount,
                maxMaterialSlots,
                hasRealFinals && hasManagedStructuralMaterialStack,
                maxLodGroupCount,
                missingLodOnRequiredRealFinal,
                notes.Length > 0 ? notes.ToString() : "ok");
        }

        private static string BuildMarkdown(
            IReadOnlyList<FamilyStatus> statuses,
            int realFinalFamilyCount,
            int placeholderOnlyFamilyCount,
            int debrisFamilyCount,
            int ruinFamilyCount,
            int serviceFamilyCount,
            int managedMaterialFamilyCount)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# Procedural Structural Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(ProceduralFamilyFolder).AppendLine("`");
            builder.AppendLine("- Scope: structural procedural families only (`Debris`, `RuinModule`, `PowerRoute`, `ServiceScar`).");
            builder.AppendLine("- Real finals: `finalReady=true` and `proxyOnly=false` and not placeholder.");
            builder.AppendLine("- Placeholder finals: `WorldProceduralPlaceholderAuthoring` output still standing in for missing structure content.");
            builder.AppendLine("- Status remains `PENDING VERIFICATION` until scene/runtime/profiler evidence exists.");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.Append("- Structural families: `").Append(statuses.Count).AppendLine("`");
            builder.Append("- Families with real finals: `").Append(realFinalFamilyCount).AppendLine("`");
            builder.Append("- Placeholder-only families: `").Append(placeholderOnlyFamilyCount).AppendLine("`");
            builder.Append("- Debris families: `").Append(debrisFamilyCount).AppendLine("`");
            builder.Append("- Ruin families: `").Append(ruinFamilyCount).AppendLine("`");
            builder.Append("- Service/power families: `").Append(serviceFamilyCount).AppendLine("`");
            builder.Append("- Families with managed structural material stack: `").Append(managedMaterialFamilyCount).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("## Family Table");
            builder.AppendLine();
            builder.AppendLine("| Family | Domain | Streaming | Variants | Proxy | Real Finals | Placeholder Finals | Max Renderers | Max Material Slots | Managed Material Stack | Max LODGroups | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                builder.Append("| ").Append(status.FamilyId)
                    .Append(" | ").Append(status.Domain)
                    .Append(" | ").Append(status.StreamingLayer)
                    .Append(" | ").Append(status.TotalVariants)
                    .Append(" | ").Append(status.ProxyVariantCount)
                    .Append(" | ").Append(status.RealFinalCount)
                    .Append(" | ").Append(status.PlaceholderFinalCount)
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.MaxMaterialSlots)
                    .Append(" | ").Append(status.HasManagedStructuralMaterialStack ? "yes" : "no")
                    .Append(" | ").Append(status.MaxLodGroupCount)
                    .Append(" | ").Append(status.Notes)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Readiness Notes");
            builder.AppendLine();
            builder.Append("- Real-final structural baseline: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: true);
            builder.AppendLine();
            builder.Append("- Placeholder-driven families: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: false);
            builder.AppendLine();
            builder.AppendLine("- Structural validator now checks managed opaque material stack plus required ruin LOD gates. Dedicated structural texture-source rules are still absent.");
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
                builder.Append(requireRealFinals ? "`none`" : "`none`");
        }

        private static bool IsStructuralDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresStructuralLod(WorldPrefabFamilyProfile family)
        {
            return WorldProceduralStructuralContract.RequiresStructuralLod(family);
        }

        private static int ResolveRendererBudget(WorldPrefabFamilyProfile family)
        {
            return WorldProceduralStructuralContract.ResolveRendererBudget(family);
        }

        private static int CountMaterialSlots(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length <= 0)
                return 0;

            int slotCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterials == null)
                    continue;

                slotCount += renderer.sharedMaterials.Length;
            }

            return slotCount;
        }

        private static bool AppendMaterialContractFindings(StringBuilder notes, Renderer[] renderers)
        {
            if (renderers == null || renderers.Length <= 0)
                return false;

            bool allMaterialsValid = true;
            HashSet<Material> inspectedMaterials = new HashSet<Material>(CountMaterialSlots(renderers));
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

                    if (WorldProceduralStructuralContract.TryGetMaterialContractFailure(material, out string failureLabel))
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

                if (WorldProceduralStructuralContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
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
                string familyLabel,
                string domain,
                string streamingLayer,
                int totalVariants,
                int proxyVariantCount,
                int realFinalCount,
                int placeholderFinalCount,
                int maxRendererCount,
                int maxMaterialSlots,
                bool hasManagedStructuralMaterialStack,
                int maxLodGroupCount,
                bool missingLodOnRequiredRealFinal,
                string notes)
            {
                AssetPath = assetPath;
                FamilyId = familyId;
                FamilyLabel = familyLabel;
                Domain = domain;
                StreamingLayer = streamingLayer;
                TotalVariants = totalVariants;
                ProxyVariantCount = proxyVariantCount;
                RealFinalCount = realFinalCount;
                PlaceholderFinalCount = placeholderFinalCount;
                MaxRendererCount = maxRendererCount;
                MaxMaterialSlots = maxMaterialSlots;
                HasManagedStructuralMaterialStack = hasManagedStructuralMaterialStack;
                MaxLodGroupCount = maxLodGroupCount;
                MissingLodOnRequiredRealFinal = missingLodOnRequiredRealFinal;
                Notes = notes;
            }

            public string AssetPath;
            public string FamilyId;
            public string FamilyLabel;
            public string Domain;
            public string StreamingLayer;
            public int TotalVariants;
            public int ProxyVariantCount;
            public int RealFinalCount;
            public int PlaceholderFinalCount;
            public int MaxRendererCount;
            public int MaxMaterialSlots;
            public bool HasManagedStructuralMaterialStack;
            public int MaxLodGroupCount;
            public bool MissingLodOnRequiredRealFinal;
            public string Notes;
        }
    }
}
