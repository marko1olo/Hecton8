using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.World;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralFloraFinalVariantValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const float LodThresholdTolerance = 0.0005f;
        private const float RequiredLod0Threshold = 0.6f;
        private const float RequiredLod1Threshold = 0.15f;
        private const float RequiredLod2Threshold = 0.04f;

        [MenuItem("Hecton/Validation/Validate Procedural Flora Final Variants", priority = 240)]
        public static void Validate()
        {
            string rootFolder = WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder;
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                Debug.LogError($"[WorldProceduralFloraFinalVariantValidator] Missing flora final root folder '{rootFolder}'.");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder });
            List<string> issues = new List<string>(64);
            List<string> warnings = new List<string>(32);
            Dictionary<string, CoverageStats> coverageByFamily = InitializeCoverageStats();
            Dictionary<string, string> prefabPathByVariantId = new Dictionary<string, string>(32, System.StringComparer.Ordinal);
            int validatedPrefabs = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
                string familyId = WorldProceduralFloraFinalVariantAuthoring.ResolveFamilyIdFromAsset(prefabPath, prefabName);
                if (!WorldProceduralFloraFinalVariantAuthoring.IsSupportedFloraFamily(familyId))
                {
                    issues.Add($"{prefabPath}: prefab is inside flora baked root but does not resolve to a supported flora family.");
                    continue;
                }

                string variantId = WorldProceduralFloraFinalVariantAuthoring.ResolveVariantIdForPrefab(familyId, prefabName);
                string existingPrefabPath;
                if (prefabPathByVariantId.TryGetValue(variantId, out existingPrefabPath))
                {
                    issues.Add(
                        $"{prefabPath}: resolves to duplicate flora variant identity '{variantId}', already claimed by '{existingPrefabPath}'. Metadata tokens must not be used to create separate logical variants.");
                    continue;
                }

                prefabPathByVariantId.Add(variantId, prefabPath);

                CoverageStats coverage;
                if (!coverageByFamily.TryGetValue(familyId, out coverage))
                {
                    coverage = new CoverageStats();
                    coverageByFamily.Add(familyId, coverage);
                }

                coverage.TotalPrefabs++;
                if (WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(prefabName))
                    coverage.GeneratedPrefabs++;
                else
                    coverage.AuthoredPrefabs++;

                coverageByFamily[familyId] = coverage;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    ValidatePrefab(prefabPath, familyId, prefabRoot, issues, warnings);
                    validatedPrefabs++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AppendCoverageFindings(coverageByFamily, issues, warnings);
            AppendLinkageFindings(coverageByFamily, issues);

            if (issues.Count == 0)
            {
                for (int i = 0; i < warnings.Count; i++)
                    Debug.LogWarning("[WorldProceduralFloraFinalVariantValidator] " + warnings[i]);

                string coverageSummary = BuildCoverageSummary(coverageByFamily);
                Debug.Log($"[WorldProceduralFloraFinalVariantValidator] PASS validatedPrefabs={validatedPrefabs}, warningCount={warnings.Count}, coverage={coverageSummary}, root='{rootFolder}'.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
                Debug.LogWarning("[WorldProceduralFloraFinalVariantValidator] " + issues[i]);

            for (int i = 0; i < warnings.Count; i++)
                Debug.LogWarning("[WorldProceduralFloraFinalVariantValidator] " + warnings[i]);

            string failedCoverageSummary = BuildCoverageSummary(coverageByFamily);
            Debug.LogWarning($"[WorldProceduralFloraFinalVariantValidator] FAIL issues={issues.Count}, warnings={warnings.Count}, validatedPrefabs={validatedPrefabs}, coverage={failedCoverageSummary}.");
        }

        private static Dictionary<string, CoverageStats> InitializeCoverageStats()
        {
            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            Dictionary<string, CoverageStats> coverageByFamily = new Dictionary<string, CoverageStats>(supportedFamilies.Count, System.StringComparer.Ordinal);

            for (int i = 0; i < supportedFamilies.Count; i++)
            {
                string familyId = supportedFamilies[i];
                coverageByFamily[familyId] = new CoverageStats();
            }

            return coverageByFamily;
        }

        private static void AppendCoverageFindings(
            IReadOnlyDictionary<string, CoverageStats> coverageByFamily,
            ICollection<string> issues,
            ICollection<string> warnings)
        {
            foreach (KeyValuePair<string, CoverageStats> pair in coverageByFamily)
            {
                string familyId = pair.Key;
                CoverageStats coverage = pair.Value;

                if (coverage.TotalPrefabs <= 0)
                {
                    issues.Add($"{familyId}: no baked flora final prefabs were found under '{WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder}'.");
                    continue;
                }

                if (coverage.AuthoredPrefabs <= 0)
                    warnings.Add($"{familyId}: only generated starter finals are present ({coverage.GeneratedPrefabs}); authored photoreal finals are still missing.");
            }
        }

        private static void AppendLinkageFindings(
            IDictionary<string, CoverageStats> coverageByFamily,
            ICollection<string> issues)
        {
            Dictionary<string, WorldPrefabFamilyProfile> familiesById = LoadFloraFamilies();
            foreach (KeyValuePair<string, CoverageStats> pair in coverageByFamily)
            {
                string familyId = pair.Key;
                CoverageStats coverage = pair.Value;

                WorldPrefabFamilyProfile family;
                if (!familiesById.TryGetValue(familyId, out family) || family == null)
                {
                    issues.Add($"{familyId}: baked flora family data exists, but the procedural family asset was not found.");
                    continue;
                }

                int linkedRealFinals = CountLinkedRealFinals(family);
                int expectedLinkedFinals = ResolveExpectedLinkedFinals(coverage);
                if (expectedLinkedFinals != linkedRealFinals)
                {
                    issues.Add(
                        $"{familyId}: expected linked real final-ready count {expectedLinkedFinals} does not match actual linked count {linkedRealFinals} in '{family.name}'.");
                }
            }
        }

        private static int ResolveExpectedLinkedFinals(CoverageStats coverage)
        {
            if (coverage.AuthoredPrefabs > 0)
                return coverage.AuthoredPrefabs;

            return coverage.TotalPrefabs;
        }

        private static Dictionary<string, WorldPrefabFamilyProfile> LoadFloraFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { ProceduralFamilyFolder });
            Dictionary<string, WorldPrefabFamilyProfile> familiesById = new Dictionary<string, WorldPrefabFamilyProfile>(16, System.StringComparer.Ordinal);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId) || !WorldProceduralFloraFinalVariantAuthoring.IsSupportedFloraFamily(family.familyId))
                    continue;

                familiesById[family.familyId] = family;
            }

            return familiesById;
        }

        private static int CountLinkedRealFinals(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    continue;

                count++;
            }

            return count;
        }

        private static string BuildCoverageSummary(IReadOnlyDictionary<string, CoverageStats> coverageByFamily)
        {
            List<string> segments = new List<string>(coverageByFamily.Count);
            IReadOnlyList<string> supportedFamilies = WorldProceduralFloraFinalVariantAuthoring.GetSupportedFloraFamiliesInOrder();
            for (int i = 0; i < supportedFamilies.Count; i++)
            {
                string familyId = supportedFamilies[i];
                CoverageStats coverage;
                if (!coverageByFamily.TryGetValue(familyId, out coverage))
                    continue;

                segments.Add($"{familyId}=a{coverage.AuthoredPrefabs}/g{coverage.GeneratedPrefabs}");
            }

            return string.Join(", ", segments);
        }

        private static void ValidatePrefab(
            string prefabPath,
            string familyId,
            GameObject prefabRoot,
            ICollection<string> issues,
            ICollection<string> warnings)
        {
            if (prefabRoot == null)
            {
                issues.Add($"{prefabPath}: prefab root could not be loaded.");
                return;
            }

            WorldProceduralFloraFinalVariantAuthoring.PrefabMetadata metadata =
                WorldProceduralFloraFinalVariantAuthoring.ResolvePrefabMetadata(familyId, prefabRoot.name);
            if (metadata.HasError)
                warnings.Add($"{prefabPath}: {metadata.Error}");

            bool isGeneratedStarter = WorldProceduralFloraFinalVariantAuthoring.IsGeneratedStarterPrefabName(
                System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            WorldProceduralFloraFinalBudgetCatalog.Budget budget = WorldProceduralFloraFinalBudgetCatalog.Resolve(familyId);
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            MeshFilter[] meshFilters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinnedRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Collider[] colliders = prefabRoot.GetComponentsInChildren<Collider>(true);
            Rigidbody[] rigidbodies = prefabRoot.GetComponentsInChildren<Rigidbody>(true);
            Animator[] animators = prefabRoot.GetComponentsInChildren<Animator>(true);
            ParticleSystem[] particles = prefabRoot.GetComponentsInChildren<ParticleSystem>(true);
            AudioSource[] audioSources = prefabRoot.GetComponentsInChildren<AudioSource>(true);
            LODGroup[] lodGroups = prefabRoot.GetComponentsInChildren<LODGroup>(true);
            Renderer[] budgetRenderers = ResolveBudgetRenderers(renderers, lodGroups);
            HashSet<Material> inspectedMaterials = new HashSet<Material>(16);

            if (renderers.Length == 0)
                issues.Add($"{prefabPath}: has no Renderer components.");

            AppendRendererOptimizationWarnings(prefabPath, renderers, warnings);

            if (budgetRenderers.Length > budget.MaxRenderers)
                issues.Add($"{prefabPath}: renderer count {budgetRenderers.Length} exceeds budget {budget.MaxRenderers} for {familyId}.");

            if (colliders.Length > 0)
                issues.Add($"{prefabPath}: final flora visual prefab should not carry Collider components ({colliders.Length} found).");

            if (rigidbodies.Length > 0)
                issues.Add($"{prefabPath}: final flora visual prefab should not carry Rigidbody components ({rigidbodies.Length} found).");

            if (animators.Length > 0)
                issues.Add($"{prefabPath}: final flora visual prefab should not carry Animator components ({animators.Length} found).");

            if (particles.Length > 0)
                issues.Add($"{prefabPath}: final flora visual prefab should not carry ParticleSystem components ({particles.Length} found).");

            if (audioSources.Length > 0)
                issues.Add($"{prefabPath}: final flora visual prefab should not carry AudioSource components ({audioSources.Length} found).");

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    issues.Add($"{prefabPath}: renderer '{renderer.name}' has no shared materials.");
                    continue;
                }

                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material material = sharedMaterials[materialIndex];
                    if (material == null)
                    {
                        issues.Add($"{prefabPath}: renderer '{renderer.name}' has null material slot {materialIndex}.");
                        continue;
                    }

                    if (!inspectedMaterials.Add(material))
                        continue;

                    AppendMaterialValidationFindings(prefabPath, familyId, renderer, material, isGeneratedStarter, warnings, issues);
                }
            }

            int materialSlots = 0;
            for (int i = 0; i < budgetRenderers.Length; i++)
            {
                Renderer renderer = budgetRenderers[i];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                materialSlots += sharedMaterials != null ? sharedMaterials.Length : 0;
            }

            if (materialSlots > budget.MaxMaterialSlots)
                issues.Add($"{prefabPath}: material slot count {materialSlots} exceeds budget {budget.MaxMaterialSlots} for {familyId}.");

            int triangleCount = CountBudgetTriangles(prefabPath, budgetRenderers, issues);

            if (triangleCount > budget.MaxTriangles)
                issues.Add($"{prefabPath}: triangle count {triangleCount} exceeds budget {budget.MaxTriangles} for {familyId}.");

            if (triangleCount < budget.MinRecommendedTriangles)
                warnings.Add($"{prefabPath}: triangle count {triangleCount} is below recommended fidelity floor {budget.MinRecommendedTriangles} for {familyId}; silhouette may read as underbuilt.");

            if (triangleCount >= budget.LodRecommendedTriangleThreshold && lodGroups.Length == 0)
                issues.Add($"{prefabPath}: triangle count {triangleCount} suggests LODGroup, but none is present.");

            int[] lodTriangleCascade = BuildLodTriangleCascade(lodGroups);
            if (lodTriangleCascade.Length > 1 && !IsStrictlyDescending(lodTriangleCascade))
                issues.Add($"{prefabPath}: LOD triangle cascade is not strictly descending ({FormatLodTriangleCascade(lodTriangleCascade)}).");

            AppendLodContractFindings(prefabPath, lodGroups, issues);
        }

        private static void AppendRendererOptimizationWarnings(
            string prefabPath,
            Renderer[] renderers,
            ICollection<string> warnings)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    warnings.Add($"{prefabPath}: renderer '{renderer.name}' keeps shadow casting enabled; flora finals should default to ShadowCastingMode.Off.");

                if (renderer.receiveShadows)
                    warnings.Add($"{prefabPath}: renderer '{renderer.name}' keeps ReceiveShadows enabled; flora finals should default to receiveShadows=false.");

                if (renderer.lightProbeUsage != LightProbeUsage.Off)
                    warnings.Add($"{prefabPath}: renderer '{renderer.name}' keeps LightProbeUsage={renderer.lightProbeUsage}; flora finals should default to LightProbeUsage.Off.");

                if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
                    warnings.Add($"{prefabPath}: renderer '{renderer.name}' keeps ReflectionProbeUsage={renderer.reflectionProbeUsage}; flora finals should default to ReflectionProbeUsage.Off.");

                if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                    warnings.Add($"{prefabPath}: renderer '{renderer.name}' keeps MotionVectorGenerationMode={renderer.motionVectorGenerationMode}; flora finals should default to ForceNoMotion.");
            }
        }

        private static void AppendMaterialValidationFindings(
            string prefabPath,
            string familyId,
            Renderer renderer,
            Material material,
            bool isGeneratedStarter,
            ICollection<string> warnings,
            ICollection<string> issues)
        {
            if (!material.enableInstancing)
                warnings.Add($"{prefabPath}: material '{material.name}' on renderer '{renderer.name}' has instancing disabled.");

            if (familyId.StartsWith("family.kelp.", System.StringComparison.Ordinal))
            {
                AppendFloraStackFindings(prefabPath, familyId, renderer, material, KelpShaderName, "kelp", isGeneratedStarter, warnings, issues);
                return;
            }

            if (familyId.StartsWith("family.coral.", System.StringComparison.Ordinal))
                AppendFloraStackFindings(prefabPath, familyId, renderer, material, CoralShaderName, "coral", isGeneratedStarter, warnings, issues);
        }

        private static void AppendFloraStackFindings(
            string prefabPath,
            string familyId,
            Renderer renderer,
            Material material,
            string expectedShaderName,
            string floraLabel,
            bool isGeneratedStarter,
            ICollection<string> warnings,
            ICollection<string> issues)
        {
            if (!WorldProceduralFloraMaterialAuthoring.IsAcceptedFloraShader(material.shader, familyId))
                issues.Add(
                    $"{prefabPath}: {floraLabel} renderer '{renderer.name}' must use {WorldProceduralFloraMaterialAuthoring.DescribeExpectedShaderVariant(familyId)}, found '{(material.shader != null ? material.shader.name : "<null>")}'.");
            else
            {
                string shaderContractFailure;
                if (WorldProceduralFloraMaterialAuthoring.TryGetShaderContractFailure(material, out shaderContractFailure))
                {
                    issues.Add(
                        $"{prefabPath}: {floraLabel} material '{material.name}' uses stale shader contract ({shaderContractFailure}). Required contract: `_QUALITY_MX350` enabled, `_QUALITY_HIGH` disabled, and positive `{WorldProceduralFloraMaterialAuthoring.NormalScaleProperty}`, `{WorldProceduralFloraMaterialAuthoring.TriplanarScaleProperty}`, `{WorldProceduralFloraMaterialAuthoring.TriplanarSharpnessProperty}`, `{WorldProceduralFloraMaterialAuthoring.CurvatureWetnessStrengthProperty}`, `{WorldProceduralFloraMaterialAuthoring.FresnelStrengthProperty}`, `{WorldProceduralFloraMaterialAuthoring.FresnelPowerProperty}`, `{WorldProceduralFloraMaterialAuthoring.HeightScaleProperty}`.");
                }
            }

            Texture baseTexture = material.GetTexture("_BaseMap");
            Texture detailTexture = material.GetTexture("_DetailMap");
            Texture normalTexture = material.GetTexture("_NormalMap");
            Texture maskTexture = material.GetTexture("_MaskMap");
            if (baseTexture == null)
                issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' is missing _BaseMap.");

            if (detailTexture == null)
                issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' is missing _DetailMap.");

            if (normalTexture == null)
                issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' is missing _NormalMap.");

            if (maskTexture == null)
                issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' is missing _MaskMap.");

            bool usesGeneratedTextureSource =
                WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(baseTexture)
                || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(detailTexture)
                || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(normalTexture)
                || WorldProceduralFloraTextureAuthoring.IsGeneratedProceduralTexture(maskTexture);
            if (usesGeneratedTextureSource)
            {
                if (isGeneratedStarter)
                {
                    warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' still uses procedural editor-generated texture assets. Starter coverage only; not photoreal final proof.");
                }
                else
                {
                    issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' uses procedural editor-generated texture assets. Authored finals must use imported real texture sets.");
                }
            }

            string unexpectedTextureSourceFailure;
            if (WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(baseTexture, familyId, "albedo", out unexpectedTextureSourceFailure)
                || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(detailTexture, familyId, "detail", out unexpectedTextureSourceFailure)
                || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(normalTexture, familyId, "normal", out unexpectedTextureSourceFailure)
                || WorldProceduralFloraTextureAuthoring.TryGetUnexpectedTextureSourceFailure(maskTexture, familyId, "mask", out unexpectedTextureSourceFailure))
            {
                issues.Add(
                    $"{prefabPath}: {floraLabel} material '{material.name}' uses unmanaged texture source ({unexpectedTextureSourceFailure}). Flora finals must use either imported family maps under the managed Imported root or owned generated starter `.asset` textures.");
            }

            string textureStackSourceFailure;
            if (WorldProceduralFloraTextureAuthoring.TryGetTextureStackSourceFailure(baseTexture, detailTexture, normalTexture, maskTexture, out textureStackSourceFailure))
            {
                if (isGeneratedStarter)
                {
                    warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' mixes texture sources ({textureStackSourceFailure}). Generated starter stacks should stay internally consistent.");
                }
                else
                {
                    issues.Add($"{prefabPath}: {floraLabel} material '{material.name}' mixes texture sources ({textureStackSourceFailure}). Authored finals must not combine imported and generated/external maps in one stack.");
                }
            }

            AppendImportedTextureContractFinding(prefabPath, floraLabel, material, familyId, "_BaseMap", "albedo", baseTexture, issues);
            AppendImportedTextureContractFinding(prefabPath, floraLabel, material, familyId, "_DetailMap", "detail", detailTexture, issues);
            AppendImportedTextureContractFinding(prefabPath, floraLabel, material, familyId, "_NormalMap", "normal", normalTexture, issues);
            AppendImportedTextureContractFinding(prefabPath, floraLabel, material, familyId, "_MaskMap", "mask", maskTexture, issues);

            if (material.HasFloat("_ReceiveShadows") && material.GetFloat("_ReceiveShadows") > 0.001f)
                warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' keeps _ReceiveShadows enabled.");

            if (material.HasFloat("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") > 0.001f)
                warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' keeps _EnvironmentReflections enabled.");

            if (material.HasFloat("_SpecularHighlights") && material.GetFloat("_SpecularHighlights") > 0.001f)
                warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' keeps _SpecularHighlights enabled.");

            if (material.HasFloat("_GlossyReflections") && material.GetFloat("_GlossyReflections") > 0.001f)
                warnings.Add($"{prefabPath}: {floraLabel} material '{material.name}' keeps _GlossyReflections enabled.");
        }

        private static void AppendLodContractFindings(
            string prefabPath,
            LODGroup[] lodGroups,
            ICollection<string> issues)
        {
            if (lodGroups == null || lodGroups.Length == 0)
            {
                issues.Add($"{prefabPath}: flora finals must use a 3-visible-LOD `LODGroup` with thresholds {RequiredLod0Threshold:0.##}/{RequiredLod1Threshold:0.##}/{RequiredLod2Threshold:0.##}/0.");
                return;
            }

            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                if (lodGroup.fadeMode != LODFadeMode.CrossFade)
                    issues.Add($"{prefabPath}: LODGroup '{lodGroup.name}' must use LODFadeMode.CrossFade.");

                if (!lodGroup.animateCrossFading)
                    issues.Add($"{prefabPath}: LODGroup '{lodGroup.name}' must enable animateCrossFading for dithered near-field crossfade.");

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null || lods.Length != 3)
                {
                    issues.Add($"{prefabPath}: LODGroup '{lodGroup.name}' must contain exactly 3 visible LOD levels before cull. Found {((lods != null) ? lods.Length : 0)}.");
                    continue;
                }

                if (!MatchesLodTransition(lods[0].screenRelativeTransitionHeight, RequiredLod0Threshold)
                    || !MatchesLodTransition(lods[1].screenRelativeTransitionHeight, RequiredLod1Threshold)
                    || !MatchesLodTransition(lods[2].screenRelativeTransitionHeight, RequiredLod2Threshold))
                {
                    issues.Add(
                        $"{prefabPath}: LODGroup '{lodGroup.name}' uses stale thresholds ({lods[0].screenRelativeTransitionHeight:0.###}/{lods[1].screenRelativeTransitionHeight:0.###}/{lods[2].screenRelativeTransitionHeight:0.###}). Required contract is {RequiredLod0Threshold:0.##}/{RequiredLod1Threshold:0.##}/{RequiredLod2Threshold:0.##}/0.");
                }
            }
        }

        private static bool MatchesLodTransition(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= LodThresholdTolerance;
        }

        private static void AppendImportedTextureContractFinding(
            string prefabPath,
            string floraLabel,
            Material material,
            string familyId,
            string propertyName,
            string mapToken,
            Texture texture,
            ICollection<string> issues)
        {
            if (texture == null || string.IsNullOrEmpty(familyId))
                return;

            string failureLabel;
            if (WorldProceduralFloraTextureAuthoring.TryGetImportedTextureContractFailure(texture, familyId, mapToken, out failureLabel))
            {
                issues.Add(
                    $"{prefabPath}: {floraLabel} material '{material.name}' has imported {propertyName} texture '{texture.name}' with invalid import contract ({failureLabel}). Required: `{mapToken}___{familyId}.png`, Wrap=Repeat, MipMaps=On, Read/Write=Off, and correct type/sRGB/max-size for {mapToken}.");
            }
        }

        private static Renderer[] ResolveBudgetRenderers(Renderer[] allRenderers, LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return allRenderers ?? System.Array.Empty<Renderer>();

            List<Renderer> budgetRenderers = new List<Renderer>(8);
            HashSet<Renderer> seenRenderers = new HashSet<Renderer>(8);

            for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = lodGroups[groupIndex];
                if (lodGroup == null)
                    continue;

                LOD[] lods = lodGroup.GetLODs();
                if (lods == null || lods.Length == 0)
                    continue;

                Renderer[] lodRenderers = lods[0].renderers;
                if (lodRenderers == null)
                    continue;

                for (int rendererIndex = 0; rendererIndex < lodRenderers.Length; rendererIndex++)
                {
                    Renderer renderer = lodRenderers[rendererIndex];
                    if (renderer == null || !seenRenderers.Add(renderer))
                        continue;

                    budgetRenderers.Add(renderer);
                }
            }

            if (budgetRenderers.Count > 0)
                return budgetRenderers.ToArray();

            return allRenderers ?? System.Array.Empty<Renderer>();
        }

        private static int CountBudgetTriangles(string prefabPath, Renderer[] budgetRenderers, ICollection<string> issues)
        {
            int triangleCount = 0;

            for (int i = 0; i < budgetRenderers.Length; i++)
            {
                Renderer renderer = budgetRenderers[i];
                if (renderer == null)
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Mesh sharedMesh = meshFilter.sharedMesh;
                    if (sharedMesh == null)
                    {
                        issues.Add($"{prefabPath}: MeshFilter '{meshFilter.name}' has no sharedMesh.");
                        continue;
                    }

                    triangleCount += CountTriangles(sharedMesh);
                    continue;
                }

                SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedMeshRenderer != null)
                {
                    Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
                    if (sharedMesh == null)
                    {
                        issues.Add($"{prefabPath}: SkinnedMeshRenderer '{skinnedMeshRenderer.name}' has no sharedMesh.");
                        continue;
                    }

                    triangleCount += CountTriangles(sharedMesh);
                }
            }

            return triangleCount;
        }

        private static int[] BuildLodTriangleCascade(LODGroup[] lodGroups)
        {
            if (lodGroups == null || lodGroups.Length == 0)
                return System.Array.Empty<int>();

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
                return System.Array.Empty<int>();

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

        private static bool IsStrictlyDescending(int[] cascade)
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

            System.Text.StringBuilder builder = new System.Text.StringBuilder(24);
            for (int i = 0; i < cascade.Length; i++)
            {
                if (i > 0)
                    builder.Append('/');

                builder.Append(cascade[i]);
            }

            return builder.ToString();
        }

        private static int CountTriangles(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return 0;

            int triangleCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Mesh sharedMesh = meshFilter.sharedMesh;
                    if (sharedMesh != null)
                    {
                        triangleCount += CountTriangles(sharedMesh);
                        continue;
                    }
                }

                SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                    triangleCount += CountTriangles(skinnedMeshRenderer.sharedMesh);
            }

            return triangleCount;
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int triangles = 0;
            int subMeshCount = mesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);

            return triangles;
        }

        private struct CoverageStats
        {
            public int TotalPrefabs;
            public int AuthoredPrefabs;
            public int GeneratedPrefabs;
        }
    }
}
