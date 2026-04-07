using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralFloraFinalStatusReport
    {
        private const string ReportFileName = "PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md";
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";

        [MenuItem("Hecton/Validation/Generate Procedural Flora Final Status Report", priority = 241)]
        public static void GenerateReport()
        {
            string rootFolder = WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder;
            Dictionary<string, FamilyStatus> statusByFamily = InitializeStatuses();
            PopulateLinkedFamilyState(statusByFamily);
            PopulatePrefabState(statusByFamily, rootFolder);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string reportPath = Path.Combine(projectRoot, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdown(rootFolder, statusByFamily), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralFloraFinalStatusReport] Wrote report to {reportPath}");
        }

        private static Dictionary<string, FamilyStatus> InitializeStatuses()
        {
            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            Dictionary<string, FamilyStatus> statusByFamily = new Dictionary<string, FamilyStatus>(supportedFamilies.Count, StringComparer.Ordinal);

            for (int i = 0; i < supportedFamilies.Count; i++)
            {
                string familyId = supportedFamilies[i];
                statusByFamily[familyId] = new FamilyStatus(familyId);
            }

            return statusByFamily;
        }

        private static void PopulateLinkedFamilyState(IDictionary<string, FamilyStatus> statusByFamily)
        {
            string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { ProceduralFamilyFolder });
            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                    continue;

                FamilyStatus status;
                if (!statusByFamily.TryGetValue(family.familyId, out status))
                    continue;

                status.FamilyLabel = family.familyLabel;

                WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
                for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++)
                {
                    WorldPrefabFamilyProfile.VariantEntry variant = variants[variantIndex];
                    if (variant == null || !variant.finalReady || variant.proxyOnly)
                        continue;

                    status.LinkedFinalReadyCount++;

                    if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    {
                        status.LinkedPlaceholderCount++;
                        continue;
                    }

                    status.LinkedRealFinalCount++;

                    string prefabName = variant.prefab != null ? variant.prefab.name : string.Empty;
                    if (WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(prefabName))
                        status.LinkedGeneratedCount++;
                    else
                        status.LinkedAuthoredCount++;
                }
            }
        }

        private static void PopulatePrefabState(IDictionary<string, FamilyStatus> statusByFamily, string rootFolder)
        {
            if (!AssetDatabase.IsValidFolder(rootFolder))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                string familyId = WorldProceduralFloraFinalVariantAuthoring.ResolveFamilyIdFromAsset(prefabPath, prefabName);
                if (!WorldProceduralFloraFinalVariantAuthoring.IsSupportedFloraFamily(familyId))
                    continue;

                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                bool isGenerated = WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(prefabName);
                if (isGenerated)
                    status.GeneratedPrefabCount++;
                else
                    status.AuthoredPrefabCount++;

                PrefabStatus prefabStatus = InspectPrefab(prefabPath, prefabName, isGenerated);
                status.Prefabs.Add(prefabStatus);
                if (prefabStatus.HasLodGroup)
                    status.PrefabsWithLodCount++;

                if (prefabStatus.MaterialStateOk)
                    status.MaterialReadyPrefabCount++;

                if (prefabStatus.HasValidLodCascade)
                    status.PrefabsWithValidLodCascadeCount++;

                if (prefabStatus.MeetsFidelityFloor)
                    status.PrefabsMeetingFidelityFloorCount++;

                if (prefabStatus.BudgetTriangleCount > status.MaxBudgetTriangles)
                    status.MaxBudgetTriangles = prefabStatus.BudgetTriangleCount;

                if (prefabStatus.RendererCount > status.MaxRendererCount)
                    status.MaxRendererCount = prefabStatus.RendererCount;
            }

            foreach (KeyValuePair<string, FamilyStatus> pair in statusByFamily)
            {
                FamilyStatus status = pair.Value;
                WorldProceduralFloraFinalBudgetCatalog.Budget budget = WorldProceduralFloraFinalBudgetCatalog.Resolve(status.FamilyId);
                status.Prefabs.Sort(ComparePrefabStatus);
                status.TriangleBudgetLimit = budget.MaxTriangles;
                status.TriangleFidelityFloor = budget.MinRecommendedTriangles;
                status.RendererBudgetLimit = budget.MaxRenderers;
                status.ExpectedLinkedRealFinalCount = status.AuthoredPrefabCount > 0
                    ? status.AuthoredPrefabCount
                    : status.GeneratedPrefabCount + status.AuthoredPrefabCount;
            }
        }

        private static int ComparePrefabStatus(PrefabStatus left, PrefabStatus right)
        {
            int generatedComparison = left.IsGenerated.CompareTo(right.IsGenerated);
            if (generatedComparison != 0)
                return generatedComparison;

            return string.CompareOrdinal(left.Name, right.Name);
        }

        private static PrefabStatus InspectPrefab(string prefabPath, string prefabName, bool isGenerated)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                string familyId = WorldProceduralFloraFinalVariantAuthoring.ResolveFamilyIdFromAsset(prefabPath, prefabName);
                WorldProceduralFloraFinalVariantAuthoring.PrefabMetadata metadata =
                    WorldProceduralFloraFinalVariantAuthoring.ResolvePrefabMetadata(familyId, prefabName);
                Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                LODGroup[] lodGroups = prefabRoot.GetComponentsInChildren<LODGroup>(true);
                Renderer[] budgetRenderers = ResolveBudgetRenderers(renderers, lodGroups);

                return new PrefabStatus(
                    prefabName,
                    prefabPath,
                    isGenerated,
                    WorldProceduralFloraFinalVariantAuthoring.ResolveVariantIdForPrefab(familyId, prefabName),
                    renderers != null ? renderers.Length : 0,
                    lodGroups != null ? lodGroups.Length : 0,
                    CountLodLevels(lodGroups),
                    CountTriangles(budgetRenderers),
                    lodGroups != null && lodGroups.Length > 0,
                    metadata.Weight,
                    metadata.UniformScaleRange,
                    metadata.HasCustomWeight,
                    metadata.HasCustomScaleRange,
                    BuildLodTriangleCascade(lodGroups),
                    WorldProceduralFloraFinalBudgetCatalog.Resolve(familyId).MaxTriangles,
                    WorldProceduralFloraFinalBudgetCatalog.Resolve(familyId).MinRecommendedTriangles,
                    EvaluateMaterialState(familyId, renderers),
                    EvaluateRendererState(renderers),
                    metadata.HasError,
                    metadata.Error);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Renderer[] ResolveBudgetRenderers(Renderer[] allRenderers, LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return allRenderers ?? Array.Empty<Renderer>();

            List<Renderer> budgetRenderers = new List<Renderer>(8);
            HashSet<Renderer> seen = new HashSet<Renderer>();
            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null || lods.Length == 0 || lods[0].renderers == null)
                    continue;

                Renderer[] lod0Renderers = lods[0].renderers;
                for (int rendererIndex = 0; rendererIndex < lod0Renderers.Length; rendererIndex++)
                {
                    Renderer renderer = lod0Renderers[rendererIndex];
                    if (renderer == null || !seen.Add(renderer))
                        continue;

                    budgetRenderers.Add(renderer);
                }
            }

            return budgetRenderers.Count > 0 ? budgetRenderers.ToArray() : allRenderers ?? Array.Empty<Renderer>();
        }

        private static int CountTriangles(Renderer[] renderers)
        {
            int triangleCount = 0;
            if (renderers == null)
                return 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    triangleCount += CountTriangles(meshFilter.sharedMesh);
                    continue;
                }

                SkinnedMeshRenderer skinnedMesh = renderer as SkinnedMeshRenderer;
                if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
                    triangleCount += CountTriangles(skinnedMesh.sharedMesh);
            }

            return triangleCount;
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int triangles = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);

            return triangles;
        }

        private static int CountLodLevels(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return 0;

            int maxLodLevels = 0;
            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length > maxLodLevels)
                    maxLodLevels = lods.Length;
            }

            return maxLodLevels;
        }

        private static int[] BuildLodTriangleCascade(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return Array.Empty<int>();

            int maxLevels = 0;
            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length > maxLevels)
                    maxLevels = lods.Length;
            }

            if (maxLevels <= 0)
                return Array.Empty<int>();

            int[] cascade = new int[maxLevels];
            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null)
                    continue;

                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                    cascade[lodIndex] += CountTriangles(lods[lodIndex].renderers);
            }

            return cascade;
        }

        private static bool HasStrictLodCascade(int[] cascade)
        {
            if (cascade == null || cascade.Length <= 1)
                return true;

            for (int i = 1; i < cascade.Length; i++)
            {
                if (cascade[i] >= cascade[i - 1])
                    return false;
            }

            return true;
        }

        private static string FormatLodTriangleCascade(int[] cascade)
        {
            if (cascade == null || cascade.Length == 0)
                return "none";

            StringBuilder builder = new StringBuilder(24);
            for (int i = 0; i < cascade.Length; i++)
            {
                if (i > 0)
                    builder.Append('/');

                builder.Append(cascade[i]);
            }

            return builder.ToString();
        }

        private static MaterialState EvaluateMaterialState(string familyId, Renderer[] renderers)
        {
            string expectedShaderName = ResolveExpectedShaderName(familyId);
            bool instancingOk = true;
            bool shaderOk = true;
            bool textureStackOk = true;
            bool anyMaterial = false;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        instancingOk = false;
                        shaderOk = false;
                        textureStackOk = false;
                        continue;
                    }

                    for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                    {
                        Material material = sharedMaterials[materialIndex];
                        if (material == null)
                        {
                            instancingOk = false;
                            shaderOk = false;
                            textureStackOk = false;
                            continue;
                        }

                        anyMaterial = true;
                        if (!material.enableInstancing)
                            instancingOk = false;

                        if (string.IsNullOrEmpty(expectedShaderName))
                            continue;

                        if (material.shader == null || material.shader.name != expectedShaderName)
                            shaderOk = false;

                        if (material.GetTexture("_BaseMap") == null
                            || material.GetTexture("_DetailMap") == null
                            || material.GetTexture("_NormalMap") == null
                            || material.GetTexture("_MaskMap") == null)
                        {
                            textureStackOk = false;
                        }
                    }
                }
            }

            if (!anyMaterial)
                return new MaterialState(false, false, false, "missing-materials");

            if (string.IsNullOrEmpty(expectedShaderName))
                return new MaterialState(instancingOk, true, true, instancingOk ? "ok" : "instancing-off");

            if (instancingOk && shaderOk && textureStackOk)
                return new MaterialState(true, true, true, "ok");

            if (!shaderOk)
                return new MaterialState(instancingOk, false, textureStackOk, "shader-mismatch");

            if (!textureStackOk)
                return new MaterialState(instancingOk, true, false, "texture-stack-missing");

            return new MaterialState(false, true, true, "instancing-off");
        }

        private static string ResolveExpectedShaderName(string familyId)
        {
            if (familyId.StartsWith("family.kelp.", StringComparison.Ordinal))
                return KelpShaderName;

            if (familyId.StartsWith("family.coral.", StringComparison.Ordinal))
                return CoralShaderName;

            return string.Empty;
        }

        private static RendererState EvaluateRendererState(Renderer[] renderers)
        {
            bool defaultsOk = true;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off
                        || renderer.receiveShadows
                        || renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off
                        || renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off
                        || renderer.motionVectorGenerationMode != UnityEngine.MotionVectorGenerationMode.ForceNoMotion)
                    {
                        defaultsOk = false;
                        break;
                    }
                }
            }

            return new RendererState(defaultsOk, defaultsOk ? "ok" : "renderer-defaults-dirty");
        }

        private static string BuildMarkdown(string rootFolder, IReadOnlyDictionary<string, FamilyStatus> statusByFamily)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Flora Final Status Report");
            builder.AppendLine();
            builder.Append("- Root: `").Append(rootFolder).AppendLine("`");
            builder.Append("- Generated: `GEN_` prefabs are starter finals only.").AppendLine();
            builder.Append("- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.").AppendLine();
            builder.Append("- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.").AppendLine();
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Ready | LOD Cascade | Fidelity Floor |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            for (int familyIndex = 0; familyIndex < supportedFamilies.Count; familyIndex++)
            {
                string familyId = supportedFamilies[familyIndex];
                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                builder.Append("| ")
                    .Append(status.FamilyId)
                    .Append(" | a").Append(status.AuthoredPrefabCount).Append("/g").Append(status.GeneratedPrefabCount)
                    .Append(" | ").Append(status.ExpectedLinkedRealFinalCount)
                    .Append(" | ").Append(status.LinkedRealFinalCount)
                    .Append(" (authored ").Append(status.LinkedAuthoredCount).Append(", gen ").Append(status.LinkedGeneratedCount).Append(')')
                    .Append(" | ").Append(status.LinkedPlaceholderCount)
                    .Append(" | ").Append(status.MaxBudgetTriangles)
                    .Append(" | ").Append(status.TriangleBudgetLimit - status.MaxBudgetTriangles)
                    .Append(" | ").Append(status.MaxRendererCount)
                    .Append(" | ").Append(status.PrefabsWithLodCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.MaterialReadyPrefabCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.PrefabsWithValidLodCascadeCount).Append('/').Append(status.Prefabs.Count)
                    .Append(" | ").Append(status.PrefabsMeetingFidelityFloorCount).Append('/').Append(status.Prefabs.Count)
                    .AppendLine(" |");
            }

            for (int familyIndex = 0; familyIndex < supportedFamilies.Count; familyIndex++)
            {
                string familyId = supportedFamilies[familyIndex];
                FamilyStatus status;
                if (!statusByFamily.TryGetValue(familyId, out status))
                    continue;

                builder.AppendLine();
                builder.Append("## ").Append(status.FamilyId);
                if (!string.IsNullOrWhiteSpace(status.FamilyLabel))
                    builder.Append(" - ").Append(status.FamilyLabel);
                builder.AppendLine();
                builder.AppendLine();
                builder.Append("- Coverage: `a").Append(status.AuthoredPrefabCount).Append("/g").Append(status.GeneratedPrefabCount).AppendLine("`");
                builder.Append("- Expected linked real finals: `").Append(status.ExpectedLinkedRealFinalCount).Append("`").AppendLine();
                builder.Append("- Linked final-ready: `").Append(status.LinkedFinalReadyCount).Append("`").AppendLine();
                builder.Append("- Linked real finals: `").Append(status.LinkedRealFinalCount).Append("`").AppendLine();
                builder.Append("- Linked placeholders: `").Append(status.LinkedPlaceholderCount).Append("`").AppendLine();
                builder.Append("- Max budget triangles: `").Append(status.MaxBudgetTriangles).Append("`").AppendLine();
                builder.Append("- Triangle budget limit: `").Append(status.TriangleBudgetLimit).Append("`").AppendLine();
                builder.Append("- Triangle headroom: `").Append(status.TriangleBudgetLimit - status.MaxBudgetTriangles).Append("`").AppendLine();
                builder.Append("- Minimum recommended triangles: `").Append(status.TriangleFidelityFloor).Append("`").AppendLine();
                builder.Append("- Max renderer count: `").Append(status.MaxRendererCount).Append("`").AppendLine();
                builder.Append("- Renderer budget limit: `").Append(status.RendererBudgetLimit).Append("`").AppendLine();
                builder.Append("- Material-ready prefabs: `").Append(status.MaterialReadyPrefabCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
                builder.Append("- Strict LOD cascade prefabs: `").Append(status.PrefabsWithValidLodCascadeCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();
                builder.Append("- Prefabs meeting fidelity floor: `").Append(status.PrefabsMeetingFidelityFloorCount).Append('/').Append(status.Prefabs.Count).Append("`").AppendLine();

                if (status.Prefabs.Count == 0)
                {
                    builder.AppendLine("- No baked prefabs found.");
                    continue;
                }

                builder.AppendLine("- Prefabs:");
                for (int prefabIndex = 0; prefabIndex < status.Prefabs.Count; prefabIndex++)
                {
                    PrefabStatus prefab = status.Prefabs[prefabIndex];
                    builder.Append("  - `").Append(prefab.Name).Append("`");
                    builder.Append(prefab.IsGenerated ? " generated" : " authored");
                    builder.Append(" | variantId=`").Append(prefab.VariantId).Append('`');
                    builder.Append(" | renderers=").Append(prefab.RendererCount);
                    builder.Append(" | lodGroups=").Append(prefab.LodGroupCount);
                    builder.Append(" | lodLevels=").Append(prefab.LodLevelCount);
                    builder.Append(" | budgetTriangles=").Append(prefab.BudgetTriangleCount);
                    builder.Append(" | weight=").Append(prefab.Weight);
                    if (prefab.HasCustomWeight)
                        builder.Append('*');
                    builder.Append(" | scale=").Append(FormatScaleRange(prefab.ScaleRange));
                    if (prefab.HasCustomScaleRange)
                        builder.Append('*');
                    builder.Append(" | lodTriangles=").Append(FormatLodTriangleCascade(prefab.LodTriangleCascade));
                    builder.Append(" | material=").Append(prefab.MaterialStateLabel);
                    builder.Append(" | renderState=").Append(prefab.RendererStateLabel);
                    builder.Append(" | fidelity=").Append(prefab.FidelityLabel);
                    builder.Append(" | path=`").Append(prefab.Path).AppendLine("`");
                    if (prefab.HasMetadataError)
                        builder.Append("    - metadataError=`").Append(prefab.MetadataError).AppendLine("`");
                }
            }

            return builder.ToString();
        }

        private static string FormatScaleRange(Vector2 scaleRange)
        {
            int minPercent = Mathf.RoundToInt(scaleRange.x * 100f);
            int maxPercent = Mathf.RoundToInt(scaleRange.y * 100f);
            return minPercent.ToString() + "-" + maxPercent.ToString();
        }

        private sealed class FamilyStatus
        {
            public FamilyStatus(string familyId)
            {
                FamilyId = familyId;
                FamilyLabel = string.Empty;
                Prefabs = new List<PrefabStatus>(4);
            }

            public string FamilyId { get; }
            public string FamilyLabel { get; set; }
            public int AuthoredPrefabCount { get; set; }
            public int GeneratedPrefabCount { get; set; }
            public int LinkedFinalReadyCount { get; set; }
            public int LinkedRealFinalCount { get; set; }
            public int LinkedPlaceholderCount { get; set; }
            public int LinkedGeneratedCount { get; set; }
            public int LinkedAuthoredCount { get; set; }
            public int ExpectedLinkedRealFinalCount { get; set; }
            public int PrefabsWithLodCount { get; set; }
            public int MaxBudgetTriangles { get; set; }
            public int MaxRendererCount { get; set; }
            public int TriangleBudgetLimit { get; set; }
            public int TriangleFidelityFloor { get; set; }
            public int RendererBudgetLimit { get; set; }
            public int MaterialReadyPrefabCount { get; set; }
            public int PrefabsWithValidLodCascadeCount { get; set; }
            public int PrefabsMeetingFidelityFloorCount { get; set; }
            public List<PrefabStatus> Prefabs { get; }
        }

        private readonly struct PrefabStatus
        {
            public PrefabStatus(
                string name,
                string path,
                bool isGenerated,
                string variantId,
                int rendererCount,
                int lodGroupCount,
                int lodLevelCount,
                int budgetTriangleCount,
                bool hasLodGroup,
                int weight,
                Vector2 scaleRange,
                bool hasCustomWeight,
                bool hasCustomScaleRange,
                int[] lodTriangleCascade,
                int triangleBudgetLimit,
                int triangleFidelityFloor,
                MaterialState materialState,
                RendererState rendererState,
                bool hasMetadataError,
                string metadataError)
            {
                Name = name;
                Path = path;
                IsGenerated = isGenerated;
                VariantId = variantId ?? string.Empty;
                RendererCount = rendererCount;
                LodGroupCount = lodGroupCount;
                LodLevelCount = lodLevelCount;
                BudgetTriangleCount = budgetTriangleCount;
                HasLodGroup = hasLodGroup;
                Weight = weight;
                ScaleRange = scaleRange;
                HasCustomWeight = hasCustomWeight;
                HasCustomScaleRange = hasCustomScaleRange;
                LodTriangleCascade = lodTriangleCascade ?? Array.Empty<int>();
                TriangleBudgetLimit = triangleBudgetLimit;
                TriangleFidelityFloor = triangleFidelityFloor;
                MeetsFidelityFloor = budgetTriangleCount >= triangleFidelityFloor;
                HasValidLodCascade = HasStrictLodCascade(LodTriangleCascade);
                MaterialStateOk = materialState.IsOk;
                MaterialStateLabel = materialState.Label ?? string.Empty;
                RendererStateOk = rendererState.IsOk;
                RendererStateLabel = rendererState.Label ?? string.Empty;
                FidelityLabel = MeetsFidelityFloor ? "ok" : "underbuilt";
                HasMetadataError = hasMetadataError;
                MetadataError = metadataError ?? string.Empty;
            }

            public string Name { get; }
            public string Path { get; }
            public bool IsGenerated { get; }
            public string VariantId { get; }
            public int RendererCount { get; }
            public int LodGroupCount { get; }
            public int LodLevelCount { get; }
            public int BudgetTriangleCount { get; }
            public bool HasLodGroup { get; }
            public int Weight { get; }
            public Vector2 ScaleRange { get; }
            public bool HasCustomWeight { get; }
            public bool HasCustomScaleRange { get; }
            public int[] LodTriangleCascade { get; }
            public int TriangleBudgetLimit { get; }
            public int TriangleFidelityFloor { get; }
            public bool MeetsFidelityFloor { get; }
            public bool HasValidLodCascade { get; }
            public bool MaterialStateOk { get; }
            public string MaterialStateLabel { get; }
            public bool RendererStateOk { get; }
            public string RendererStateLabel { get; }
            public string FidelityLabel { get; }
            public bool HasMetadataError { get; }
            public string MetadataError { get; }
        }

        private readonly struct MaterialState
        {
            public MaterialState(bool instancingOk, bool shaderOk, bool textureStackOk, string label)
            {
                InstancingOk = instancingOk;
                ShaderOk = shaderOk;
                TextureStackOk = textureStackOk;
                Label = label ?? string.Empty;
            }

            public bool InstancingOk { get; }
            public bool ShaderOk { get; }
            public bool TextureStackOk { get; }
            public string Label { get; }
            public bool IsOk => InstancingOk && ShaderOk && TextureStackOk;
        }

        private readonly struct RendererState
        {
            public RendererState(bool isOk, string label)
            {
                IsOk = isOk;
                Label = label ?? string.Empty;
            }

            public bool IsOk { get; }
            public string Label { get; }
        }
    }
}
