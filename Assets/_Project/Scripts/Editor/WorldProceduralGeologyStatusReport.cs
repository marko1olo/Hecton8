using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Writes a geology-specific readiness report for rock and landmark families.
    /// </summary>
    public static class WorldProceduralGeologyStatusReport
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string ReportFileName = "PROCEDURAL_GEOLOGY_STATUS_REPORT.md";
        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(32);
        private static readonly List<LODGroup> s_LodGroupScratch = new List<LODGroup>(8);

        /// <summary>
        /// Generates a focused geology status report.
        /// </summary>
        [MenuItem("Hecton8/Validation/Generate Procedural Geology Status Report", priority = 246)]
        public static void GenerateReport()
        {
            List<FamilyStatus> statuses = LoadStatuses();
            statuses.Sort((a, b) => string.CompareOrdinal(a.FamilyId, b.FamilyId));

            int explicitProfileCount = 0;
            int emergencyFallbackCount = 0;
            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int lodReadyFamilyCount = 0;

            for (int i = 0; i < statuses.Count; i++)
            {
                FamilyStatus status = statuses[i];
                if (status.HasExplicitProfile)
                    explicitProfileCount++;
                else if (status.UsesGenerativeGeology)
                    emergencyFallbackCount++;

                if (status.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (status.PlaceholderFinalCount > 0)
                    placeholderOnlyFamilyCount++;

                if (status.RealFinalCount > 0 && !status.MissingLodOnLargeRealFinal)
                    lodReadyFamilyCount++;
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(statuses, explicitProfileCount, emergencyFallbackCount, realFinalFamilyCount, placeholderOnlyFamilyCount, lodReadyFamilyCount), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralGeologyStatusReport] Wrote report to {reportPath}");
        }

        private static List<FamilyStatus> LoadStatuses()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyStatus> statuses = new List<FamilyStatus>(8);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !IsGeologicalDomain(family.proceduralDomain))
                    continue;

                statuses.Add(BuildStatus(assetPath, family));
            }

            return statuses;
        }

        private static FamilyStatus BuildStatus(string assetPath, WorldPrefabFamilyProfile family)
        {
            WorldGenerativeGeologyProfile profile = family.generativeGeologyProfile;
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            int proxyVariantCount = 0;
            int realFinalCount = 0;
            int placeholderFinalCount = 0;
            int rendererCountMax = 0;
            int lodGroupCountMax = 0;
            bool missingLodOnLargeRealFinal = false;

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
                GameObject prefab = variant.prefab;
                s_RendererScratch.Clear();
                prefab.GetComponentsInChildren(true, s_RendererScratch);
                s_LodGroupScratch.Clear();
                prefab.GetComponentsInChildren(true, s_LodGroupScratch);
                rendererCountMax = Mathf.Max(rendererCountMax, s_RendererScratch.Count);
                lodGroupCountMax = Mathf.Max(lodGroupCountMax, s_LodGroupScratch.Count);
                if (RequiresLargeFormLod(family) && s_LodGroupScratch.Count <= 0)
                    missingLodOnLargeRealFinal = true;
            }

            StringBuilder notes = new StringBuilder(96);
            if (profile != null)
            {
                AppendNote(notes, $"profile:{profile.profileId}");
                AppendNote(notes, $"mode:{profile.generatorMode}");
                AppendNote(notes, $"shape:{profile.shapeArchetype}");
            }
            else if (family.UsesGenerativeGeology())
            {
                AppendNote(notes, "emergency-fallback-profile");
            }

            if (realFinalCount <= 0 && placeholderFinalCount > 0)
                AppendNote(notes, "placeholder-only");
            else if (realFinalCount <= 0)
                AppendNote(notes, "no-real-finals");

            if (missingLodOnLargeRealFinal)
                AppendNote(notes, "large-real-final-missing-lodgroup");

            return new FamilyStatus(
                assetPath,
                family.familyId ?? string.Empty,
                family.familyLabel ?? string.Empty,
                family.proceduralDomain.ToString(),
                family.ResolveStreamingLayer().ToString(),
                family.UsesGenerativeGeology(),
                profile != null,
                profile != null ? profile.IsEnabled : false,
                profile != null ? profile.lodCount : 0,
                profile != null ? profile.lodScreenHeights : Vector3.zero,
                variants.Length,
                proxyVariantCount,
                realFinalCount,
                placeholderFinalCount,
                rendererCountMax,
                lodGroupCountMax,
                missingLodOnLargeRealFinal,
                notes.Length > 0 ? notes.ToString() : "ok");
        }

        private static string BuildMarkdown(
            IReadOnlyList<FamilyStatus> statuses,
            int explicitProfileCount,
            int emergencyFallbackCount,
            int realFinalFamilyCount,
            int placeholderOnlyFamilyCount,
            int lodReadyFamilyCount)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# Procedural Geology Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(ProceduralFamilyFolder).AppendLine("`");
            builder.AppendLine("- Scope: geological procedural families only (`Rock`, `RockCluster`, `RockArch`, `RockShelf`, `CaveEntrance`, `Landmark`).");
            builder.AppendLine("- Explicit profile: `WorldPrefabFamilyProfile.generativeGeologyProfile` assigned.");
            builder.AppendLine("- Emergency fallback: geological behavior inferred from domain without explicit geology profile.");
            builder.AppendLine("- Status remains `PENDING VERIFICATION` until runtime/seam/profiler evidence exists.");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.Append("- Geological families: `").Append(statuses.Count).AppendLine("`");
            builder.Append("- Families with real finals: `").Append(realFinalFamilyCount).AppendLine("`");
            builder.Append("- Placeholder-only families: `").Append(placeholderOnlyFamilyCount).AppendLine("`");
            builder.Append("- Explicit geology profiles: `").Append(explicitProfileCount).AppendLine("`");
            builder.Append("- Emergency fallback families: `").Append(emergencyFallbackCount).AppendLine("`");
            builder.Append("- Real-final families without missing large-form LODGroup: `").Append(lodReadyFamilyCount).AppendLine("`");
            builder.AppendLine();
            builder.AppendLine("## Family Table");
            builder.AppendLine();
            builder.AppendLine("| Family | Domain | Streaming | Variants | Proxy | Real Finals | Placeholder Finals | Explicit Profile | Profile Enabled | Profile LOD | Max Renderers | Max LODGroups | Notes |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

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
                    .Append(" | ").Append(status.HasExplicitProfile ? "yes" : "no")
                    .Append(" | ").Append(status.ProfileEnabled ? "yes" : "no")
                    .Append(" | ").Append(status.ProfileLodCount > 0 ? $"{status.ProfileLodCount} [{FormatVector(status.ProfileLodHeights)}]" : "-")
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.MaxLodGroupCount)
                    .Append(" | ").Append(status.Notes)
                    .AppendLine(" |");
            }

            builder.AppendLine();
            builder.AppendLine("## Readiness Notes");
            builder.AppendLine();
            builder.Append("- Real-final geology baseline: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: true);
            builder.AppendLine();
            builder.Append("- Placeholder-driven geology families: ");
            AppendFamilyNoteList(builder, statuses, requireRealFinals: false);
            builder.AppendLine();
            builder.AppendLine("- Large geological silhouettes should converge on explicit geology profiles plus real-final prefabs with LODGroup support.");
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

        private static bool IsGeologicalDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Rock:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockCluster:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockShelf:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresLargeFormLod(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return true;

                default:
                    return family.budgetClass == WorldPrefabFamilyProfile.BudgetClass.Heavy
                        || family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark;
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.##}/{value.y:0.##}/{value.z:0.##}";
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
                bool usesGenerativeGeology,
                bool hasExplicitProfile,
                bool profileEnabled,
                int profileLodCount,
                Vector3 profileLodHeights,
                int totalVariants,
                int proxyVariantCount,
                int realFinalCount,
                int placeholderFinalCount,
                int maxRendererCount,
                int maxLodGroupCount,
                bool missingLodOnLargeRealFinal,
                string notes)
            {
                AssetPath = assetPath;
                FamilyId = familyId;
                FamilyLabel = familyLabel;
                Domain = domain;
                StreamingLayer = streamingLayer;
                UsesGenerativeGeology = usesGenerativeGeology;
                HasExplicitProfile = hasExplicitProfile;
                ProfileEnabled = profileEnabled;
                ProfileLodCount = profileLodCount;
                ProfileLodHeights = profileLodHeights;
                TotalVariants = totalVariants;
                ProxyVariantCount = proxyVariantCount;
                RealFinalCount = realFinalCount;
                PlaceholderFinalCount = placeholderFinalCount;
                MaxRendererCount = maxRendererCount;
                MaxLodGroupCount = maxLodGroupCount;
                MissingLodOnLargeRealFinal = missingLodOnLargeRealFinal;
                Notes = notes;
            }

            public string AssetPath;
            public string FamilyId;
            public string FamilyLabel;
            public string Domain;
            public string StreamingLayer;
            public bool UsesGenerativeGeology;
            public bool HasExplicitProfile;
            public bool ProfileEnabled;
            public int ProfileLodCount;
            public Vector3 ProfileLodHeights;
            public int TotalVariants;
            public int ProxyVariantCount;
            public int RealFinalCount;
            public int PlaceholderFinalCount;
            public int MaxRendererCount;
            public int MaxLodGroupCount;
            public bool MissingLodOnLargeRealFinal;
            public string Notes;
        }
    }
}
