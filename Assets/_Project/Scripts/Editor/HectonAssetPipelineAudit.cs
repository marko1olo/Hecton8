#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Editor;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Runs the HECTON-8 asset pipeline enforcement pass and emits the tech-art markdown log.
    /// </summary>
    internal static class HectonAssetPipelineAudit
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Run Full Audit And Emit Log";
        private const string ReportAssetPath = "Assets/DOCS/AGENT_06_TECHART_LOG.md";

        [MenuItem(MenuPath, priority = 195)]
        private static void RunFromMenu()
        {
            string reportPath = RunFullAuditAndEmitLog();
            UnityEngine.Debug.Log($"[HectonAssetPipelineAudit] Wrote tech-art log to '{reportPath}'.");
        }

        internal static string RunFullAuditAndEmitLog()
        {
            HectonPrefabIntegrityScanner.ScanResult prefabResult;
            HectonMaterialChannelPackValidator.AuditResult materialResult;
            HectonBakeryUvAudit.AuditResult bakeryResult;
            HectonLodGroupAudit.AuditResult lodResult;
            HectonAssetQuarantineUtility.QuarantineResult quarantinePreview;
            var reimportedFbx = HectonFBXPostprocessor.ReimportFbxAssets(HectonFBXPostprocessor.ManagedFbxRoots);

            prefabResult = HectonPrefabIntegrityScanner.ScanAndRepair();
            materialResult = HectonMaterialChannelPackValidator.RunAudit();
            bakeryResult = HectonBakeryUvAudit.RunAudit();
            lodResult = HectonLodGroupAudit.RunAudit();
            quarantinePreview = HectonAssetQuarantineUtility.PreviewQuarantine(materialResult.QuarantineCandidatePaths, lodResult.QuarantineCandidatePaths);

            EnsureFolder("Assets/DOCS");
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), ReportAssetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(absolutePath, BuildReport(reimportedFbx, prefabResult, materialResult, bakeryResult, lodResult, quarantinePreview), Encoding.UTF8);
            AssetDatabase.Refresh();
            return ReportAssetPath;
        }

        private static string BuildReport(
            System.Collections.Generic.List<string> reimportedFbx,
            HectonPrefabIntegrityScanner.ScanResult prefabResult,
            HectonMaterialChannelPackValidator.AuditResult materialResult,
            HectonBakeryUvAudit.AuditResult bakeryResult,
            HectonLodGroupAudit.AuditResult lodResult,
            HectonAssetQuarantineUtility.QuarantineResult quarantinePreview)
        {
            StringBuilder builder = new StringBuilder(32768);
            builder.AppendLine("# AGENT 06 TechArt Log");
            builder.AppendLine();
            builder.AppendLine("Generated: `" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("Status: `PENDING VERIFICATION`");
            builder.AppendLine();
            builder.AppendLine("Mandates followed:");
            builder.AppendLine("- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`");
            builder.AppendLine("- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`");
            builder.AppendLine("- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`");
            builder.AppendLine("- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`");
            builder.AppendLine("- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`");
            builder.AppendLine("- `PROJECT_LTS_Compatibility_Layer.txt`");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"- FBX reimported under enforced policy: `{reimportedFbx.Count}`");
            builder.AppendLine($"- Prefab assets scanned: `{prefabResult.ScannedPrefabAssetCount}`");
            builder.AppendLine($"- Scenes scanned: `{prefabResult.ScannedSceneCount}`");
            builder.AppendLine($"- Broken variant assets replaced: `{prefabResult.ReplacedPrefabAssets.Count}`");
            builder.AppendLine($"- Missing prefab instances unpacked: `{prefabResult.UnpackedInstances.Count}`");
            builder.AppendLine($"- Missing prefab instances replaced: `{prefabResult.ReplacedInstances.Count}`");
            builder.AppendLine($"- Channel-pack VRAM violations: `{materialResult.VramViolations.Count}`");
            builder.AppendLine($"- Packed-mask integrity violations: `{materialResult.PackedMaskViolations.Count}`");
            builder.AppendLine($"- Bakery UV auto-fixes: `{bakeryResult.AutoFixedModels.Count}`");
            builder.AppendLine($"- Bakery UV manual review items: `{bakeryResult.ManualReviewModels.Count}`");
            builder.AppendLine($"- LODGroup violations (>2k tris without LOD): `{lodResult.Violations.Count}`");
            builder.AppendLine($"- Broken asset quarantine candidates: `{quarantinePreview.CandidateCount}`");
            builder.AppendLine();
            builder.AppendLine("## FBX Reimported");
            builder.AppendLine();
            AppendList(builder, reimportedFbx, "No FBX assets were reimported.");
            builder.AppendLine();
            builder.AppendLine("## Broken Prefabs");
            builder.AppendLine();
            AppendList(builder, prefabResult.BrokenVariantAssets, "No broken variant assets detected.");
            builder.AppendLine();
            builder.AppendLine("## Prefab Repairs");
            builder.AppendLine();
            AppendList(builder, prefabResult.ReplacedPrefabAssets, "No prefab assets required replacement.");
            AppendList(builder, prefabResult.UnpackedInstances, "No missing prefab instances were unpacked.");
            AppendList(builder, prefabResult.ReplacedInstances, "No missing prefab instances were replaced with the error cube.");
            AppendList(builder, prefabResult.SkippedSceneRepairs, "No scene repairs were skipped.");
            builder.AppendLine();
            builder.AppendLine("## Broken References");
            builder.AppendLine();
            AppendList(builder, prefabResult.BrokenReferences, "No additional broken prefab/scene references detected.");
            builder.AppendLine();
            builder.AppendLine("## Bakery UV Audit");
            builder.AppendLine();
            AppendList(builder, bakeryResult.AutoFixedModels, "No importer-side Bakery UV fixes were applied.");
            AppendList(builder, bakeryResult.ManualReviewModels, "No Bakery UV manual review items detected.");
            builder.AppendLine();
            builder.AppendLine("## Channel Pack Violations");
            builder.AppendLine();
            AppendList(builder, materialResult.VramViolations, "No channel-pack VRAM violations detected in Assets/_Project/Art/Materials.");
            AppendList(builder, materialResult.PackedMaskViolations, "No packed-mask integrity violations detected in the targeted material set.");
            builder.AppendLine();
            builder.AppendLine("## LOD Group Violations");
            builder.AppendLine();
            AppendList(builder, lodResult.Violations, "No >2k triangle assets were found without an LODGroup in the scanned roots.");
            AppendList(builder, lodResult.BrokenAssets, "No broken prefab/model assets were detected by the LOD audit.");
            builder.AppendLine();
            builder.AppendLine("## Shader Variant Policy");
            builder.AppendLine();
            builder.AppendLine("- " + HectonProjectAuditor.BuildShaderVariantPolicySummary());
            builder.AppendLine();
            builder.AppendLine("## Quarantine Preview");
            builder.AppendLine();
            AppendList(builder, quarantinePreview.MovedAssets, "No assets were moved into _Isolated during the preview quarantine pass.");
            AppendList(builder, quarantinePreview.SkippedAssets, "No quarantine move failures were recorded.");
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            builder.AppendLine("- Import spam verification still requires a fresh Unity log/console pass after the enforced FBX reimport.");
            builder.AppendLine("- `Hecton8/Environment/Hecton_DryZoneLit` remains a legacy split-texture shader and is reported as non-compliant until its shader contract is migrated to packed masks.");
            builder.AppendLine("- Bakery items listed under manual review still require 3D-modeling follow-up if Unity auto-unwrap could not resolve them.");
            builder.AppendLine("- Quarantine is limited to fundamentally broken assets that fail to load or cannot be analysed; missing LODGroup alone is reported but not isolated.");
            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, System.Collections.Generic.List<string> entries, string emptyMessage)
        {
            if (entries == null || entries.Count == 0)
            {
                builder.AppendLine("- " + emptyMessage);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
                builder.AppendLine("- " + entries[i]);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int separatorIndex = assetPath.LastIndexOf('/');
            if (separatorIndex <= 0)
                return;

            string parentPath = assetPath.Substring(0, separatorIndex);
            string folderName = assetPath.Substring(separatorIndex + 1);
            EnsureFolder(parentPath);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
#endif
