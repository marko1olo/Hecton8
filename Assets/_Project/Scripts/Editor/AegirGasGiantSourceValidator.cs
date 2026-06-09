using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class AegirGasGiantSourceValidator
    {
        public const string ProductionGasGiantPrefabPath = "Assets/_Project/Prefabs/GasGiant_Aegir.prefab";
        public const string LegacyPrologueGasGiantPrefabPath = "Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab";
        public const string OrbitScenePath = "Assets/_Project/Scenes/01_ORBIT.unity";
        public const string GasGiantMaterialPath = "Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat";
        public const string SkyMaterialPath = "Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat";
        public const string CanonicalBandTexturePath = "Assets/_Project/Art/TEXTURES/clouds0_diff.png";
        public const string CanonicalDetailTexturePath = "Assets/_Project/Art/TEXTURES/Sky/oblakajip.png";
        public const string CanonicalStormTexturePath = "Assets/_Project/Art/TEXTURES/Aegir_storms.png";
        public const string CanonicalProductionMeshPath = "Assets/_Project/Art/Models/gasgiant.asset";
        public const string CelestialEnginePath = "Assets/_Project/Scripts/HectonCelestialEngine.cs";
        public const string OrbitalRelativityDirectorPath = "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs";
        public const string AegirSkyShaderPath = "Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader";
        public const string ProofContactSheetBuilderPath = "Tools/BuildAegirGasGiantProofContactSheet.py";
        public const string UnityBuiltInPrimitiveMeshGuid = "0000000000000000e000000000000000";

        private const string GasGiantShaderName = "HECTON/Celestial/H8_AegirGasGiantImpostor_1428";
        private const string SkyShaderName = "HECTON/Sky/Hecton_AegirSky";
        private const string ProductionGasGiantPrefabGuid = "9bafceacd557491409f6134514063ff4";

        [MenuItem("Hecton8/Validation/Aegir Gas Giant Source Contract")]
        public static void ValidateFromMenu()
        {
            AegirGasGiantSourceValidationReport report = ValidateSources();
            LogReport(report, "[AegirGasGiantSourceValidator]");
        }

        [MenuItem("Hecton8/Validation/Repair Aegir Gas Giant Source Contract")]
        public static void RepairFromMenu()
        {
            int changeCount = RepairAllSources();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AegirGasGiantSourceValidationReport report = ValidateSources();
            LogReport(report, "[AegirGasGiantSourceValidator][Repair]");
            Debug.Log("[AegirGasGiantSourceValidator][Repair] AppliedChanges=" + changeCount);
        }

        public static AegirGasGiantSourceValidationReport ValidateSources()
        {
            AegirGasGiantSourceValidationReport report = new AegirGasGiantSourceValidationReport();
            ValidateMaterialBindings(report);
            ValidateTextureImport(CanonicalBandTexturePath, true, report);
            ValidateTextureImport(CanonicalDetailTexturePath, true, report);
            ValidateTextureImport(CanonicalStormTexturePath, true, report);
            ValidatePrefabSource(ProductionGasGiantPrefabPath, true, report);
            ValidatePrefabSource(LegacyPrologueGasGiantPrefabPath, false, report);
            ValidateOrbitSceneOverrides(report);
            ValidateRuntimeSourceContracts(report);
            return report;
        }

        public static bool ValidateSources(out AegirGasGiantSourceValidationReport report)
        {
            report = ValidateSources();
            return report.FailureCount == 0;
        }

        public static int RepairAllSources()
        {
            int changeCount = 0;
            changeCount += RepairMaterialBindings();
            changeCount += RepairPrefabSource(ProductionGasGiantPrefabPath);
            changeCount += RepairPrefabSource(LegacyPrologueGasGiantPrefabPath);
            changeCount += RepairOrbitSceneFromMenu();
            return changeCount;
        }

        private static void LogReport(AegirGasGiantSourceValidationReport report, string prefix)
        {
            for (int i = 0; i < report.Findings.Count; i++)
            {
                AegirGasGiantSourceValidationFinding finding = report.Findings[i];
                string line = prefix + " " + finding.Severity + " | " + finding.Category + " | " +
                              finding.AssetPath + " | " + finding.ComponentPath + " | " + finding.Message;

                if (finding.Severity == AegirGasGiantSourceFindingSeverity.Fail)
                    Debug.LogError(line);
                else if (finding.Severity == AegirGasGiantSourceFindingSeverity.Warning)
                    Debug.LogWarning(line);
                else
                    Debug.Log(line);
            }

            if (report.FailureCount > 0)
            {
                Debug.LogError(
                    prefix + " FAILED. CheckedMaterials=" + report.CheckedMaterialCount +
                    ", CheckedTextures=" + report.CheckedTextureCount +
                    ", CheckedPrefabs=" + report.CheckedPrefabCount +
                    ", CheckedScenes=" + report.CheckedSceneCount +
                    ", CheckedSources=" + report.CheckedSourceCount +
                    ", Failures=" + report.FailureCount +
                    ", Warnings=" + report.WarningCount +
                    ". Aegir must remain a believable gas giant source route, not a flat storm-mask decal or built-in sphere override.");
            }
            else
            {
                Debug.Log(
                    prefix + " PASS. CheckedMaterials=" + report.CheckedMaterialCount +
                    ", CheckedTextures=" + report.CheckedTextureCount +
                    ", CheckedPrefabs=" + report.CheckedPrefabCount +
                    ", CheckedScenes=" + report.CheckedSceneCount +
                    ", CheckedSources=" + report.CheckedSourceCount +
                    ", Warnings=" + report.WarningCount +
                    ". Static binding pass only; visual proof still requires surface, horizon, underwater-up, phase, fog/cloud, and quality-tier screenshots.");
            }
        }

        private static void ValidateMaterialBindings(AegirGasGiantSourceValidationReport report)
        {
            Material gasMaterial = AssetDatabase.LoadAssetAtPath<Material>(GasGiantMaterialPath);
            if (gasMaterial == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingMaterial,
                    GasGiantMaterialPath,
                    string.Empty,
                    "Gas giant impostor material is missing.");
                return;
            }

            report.CheckedMaterialCount++;
            ValidateShader(gasMaterial, GasGiantShaderName, GasGiantMaterialPath, report);
            ValidateMaterialTexture(gasMaterial, "_MainTex", CanonicalBandTexturePath, GasGiantMaterialPath, report);
            ValidateMaterialTexture(gasMaterial, "_DetailTex", CanonicalDetailTexturePath, GasGiantMaterialPath, report);
            ValidateMaterialTexture(gasMaterial, "_StormTex", CanonicalStormTexturePath, GasGiantMaterialPath, report);
            ValidateMaterialFloat(gasMaterial, "_StormEmission", 1f, GasGiantMaterialPath, report);

            Material skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (skyMaterial == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingMaterial,
                    SkyMaterialPath,
                    string.Empty,
                    "Aegir sky projection material is missing.");
                return;
            }

            report.CheckedMaterialCount++;
            ValidateShader(skyMaterial, SkyShaderName, SkyMaterialPath, report);
            ValidateMaterialTexture(skyMaterial, "_AegirBandTex", CanonicalBandTexturePath, SkyMaterialPath, report);

            Texture skyBand = skyMaterial.GetTexture("_AegirBandTex");
            string skyBandPath = skyBand != null ? AssetDatabase.GetAssetPath(skyBand) : string.Empty;
            if (skyBandPath.Equals(CanonicalStormTexturePath, StringComparison.OrdinalIgnoreCase))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureBinding,
                    SkyMaterialPath,
                    "_AegirBandTex",
                    "Sky projection is bound to the storm mask. This produces a flat dark decal instead of readable gas giant bands.");
            }
        }

        private static void ValidateShader(
            Material material,
            string expectedShaderName,
            string materialPath,
            AegirGasGiantSourceValidationReport report)
        {
            string actualShaderName = material.shader != null ? material.shader.name : string.Empty;
            if (!actualShaderName.Equals(expectedShaderName, StringComparison.Ordinal))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadMaterialShader,
                    materialPath,
                    material.name,
                    "Expected shader '" + expectedShaderName + "', found '" + actualShaderName + "'.");
            }
        }

        private static void ValidateMaterialTexture(
            Material material,
            string propertyName,
            string expectedTexturePath,
            string materialPath,
            AegirGasGiantSourceValidationReport report)
        {
            if (!material.HasProperty(propertyName))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureBinding,
                    materialPath,
                    propertyName,
                    "Material lacks required texture property.");
                return;
            }

            Texture expected = AssetDatabase.LoadAssetAtPath<Texture>(expectedTexturePath);
            if (expected == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingTexture,
                    expectedTexturePath,
                    propertyName,
                    "Required texture source is missing.");
                return;
            }

            Texture actual = material.GetTexture(propertyName);
            if (actual == expected)
                return;

            report.AddFail(
                AegirGasGiantSourceFindingCategory.BadTextureBinding,
                materialPath,
                propertyName,
                "Expected '" + expectedTexturePath + "', found '" + (actual != null ? AssetDatabase.GetAssetPath(actual) : "<null>") + "'.");
        }

        private static void ValidateMaterialFloat(
            Material material,
            string propertyName,
            float expectedValue,
            string materialPath,
            AegirGasGiantSourceValidationReport report)
        {
            if (!material.HasProperty(propertyName))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRuntimeSourceContract,
                    materialPath,
                    propertyName,
                    "Material lacks required runtime scalar property.");
                return;
            }

            float actual = material.GetFloat(propertyName);
            if (Math.Abs(actual - expectedValue) <= 0.0001f)
                return;

            report.AddFail(
                AegirGasGiantSourceFindingCategory.BadRuntimeSourceContract,
                materialPath,
                propertyName,
                "Expected neutral runtime scalar " + expectedValue + ", found " + actual + ".");
        }

        private static void ValidateTextureImport(
            string texturePath,
            bool requireStreamingMips,
            AegirGasGiantSourceValidationReport report)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingTexture,
                    texturePath,
                    string.Empty,
                    "Texture asset is missing.");
                return;
            }

            report.CheckedTextureCount++;
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    string.Empty,
                    "Texture importer is not available.");
                return;
            }

            if (!importer.mipmapEnabled)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    "mipmapEnabled",
                    "Sky-visible gas giant textures require mipmaps for horizon and underwater distortion stability.");
            }

            if (requireStreamingMips && !importer.streamingMipmaps)
            {
                report.AddWarning(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    "streamingMipmaps",
                    "Streaming mipmaps are disabled. This is not a correctness failure, but it can make always-visible sky assets compete with water/terrain VRAM.");
            }

            if (importer.isReadable)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    "isReadable",
                    "Texture read/write is enabled. Runtime gas giant render path does not need CPU-readable texture memory.");
            }

            if (importer.maxTextureSize < 2048)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    "maxTextureSize",
                    "Gas giant bands must keep at least 2048 max size for believable horizon/sky projection readability.");
            }

            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                report.AddWarning(
                    AegirGasGiantSourceFindingCategory.BadTextureImport,
                    texturePath,
                    "textureCompression",
                    "Texture import is uncompressed. This can be acceptable during authoring, but should be reviewed for VRAM tier fallback.");
            }
        }

        private static void ValidatePrefabSource(
            string prefabPath,
            bool production,
            AegirGasGiantSourceValidationReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingPrefab,
                    prefabPath,
                    string.Empty,
                    "Gas giant source prefab is missing.");
                return;
            }

            report.CheckedPrefabCount++;
            MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadPrefabBinding,
                    prefabPath,
                    prefab.name,
                    "Gas giant prefab has no MeshFilter.");
                return;
            }

            ValidateMesh(meshFilter.sharedMesh, prefabPath, BuildTransformPath(prefab.transform, meshFilter.transform), report);

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (renderer == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    prefabPath,
                    BuildTransformPath(prefab.transform, meshFilter.transform),
                    "Gas giant prefab has no Renderer.");
                return;
            }

            ValidateRendererState(renderer, prefabPath, BuildTransformPath(prefab.transform, renderer.transform), report);
            ValidateRendererMaterial(renderer, production, prefabPath, BuildTransformPath(prefab.transform, renderer.transform), report);
        }

        private static void ValidateMesh(
            Mesh mesh,
            string assetPath,
            string componentPath,
            AegirGasGiantSourceValidationReport report)
        {
            if (mesh == null)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadPrefabBinding,
                    assetPath,
                    componentPath,
                    "MeshFilter.sharedMesh is missing.");
                return;
            }

            if (IsUnityBuiltInPrimitiveMesh(mesh, out long localFileId, out string meshName))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BuiltInPrimitiveMesh,
                    assetPath,
                    componentPath,
                    "MeshFilter uses Unity built-in primitive mesh '" + meshName + "' localFileID=" + localFileId + ". Use authored gas giant mesh/projection source.");
                return;
            }

            report.AddInfo(
                AegirGasGiantSourceFindingCategory.BadPrefabBinding,
                assetPath,
                componentPath,
                "Mesh source is non-built-in: '" + AssetDatabase.GetAssetPath(mesh) + "'.");
        }

        private static void ValidateRendererState(
            Renderer renderer,
            string assetPath,
            string componentPath,
            AegirGasGiantSourceValidationReport report)
        {
            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    assetPath,
                    componentPath,
                    "Gas giant renderer casts shadows. Sky-scale Aegir must not enter expensive mesh shadow paths.");
            }

            if (renderer.receiveShadows)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    assetPath,
                    componentPath,
                    "Gas giant renderer receives shadows. Phase/limb lighting belongs to material globals, not scene shadow sampling.");
            }

            if (renderer.lightProbeUsage != LightProbeUsage.Off || renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    assetPath,
                    componentPath,
                    "Gas giant renderer uses light/reflection probes. Source must be driven by celestial lighting contract.");
            }

            if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                report.AddWarning(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    assetPath,
                    componentPath,
                    "Motion vectors are not forced off. Check this before enabling temporal effects around the sky body.");
            }

            if (renderer.allowOcclusionWhenDynamic)
            {
                report.AddWarning(
                    AegirGasGiantSourceFindingCategory.BadRendererState,
                    assetPath,
                    componentPath,
                    "Dynamic occlusion is enabled. A sky-scale celestial source should not flicker from stale occlusion handles.");
            }
        }

        private static void ValidateRendererMaterial(
            Renderer renderer,
            bool production,
            string assetPath,
            string componentPath,
            AegirGasGiantSourceValidationReport report)
        {
            Material expected = AssetDatabase.LoadAssetAtPath<Material>(GasGiantMaterialPath);
            if (expected == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            bool found = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == expected)
                {
                    found = true;
                    break;
                }
            }

            if (found)
                return;

            AegirGasGiantSourceFindingSeverity severity = production
                ? AegirGasGiantSourceFindingSeverity.Fail
                : AegirGasGiantSourceFindingSeverity.Warning;

            report.Add(
                severity,
                AegirGasGiantSourceFindingCategory.BadMaterialBinding,
                assetPath,
                componentPath,
                "Renderer does not reference canonical Aegir gas giant material '" + GasGiantMaterialPath + "'.");
        }

        private static void ValidateOrbitSceneOverrides(AegirGasGiantSourceValidationReport report)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), OrbitScenePath);
            if (!File.Exists(fullPath))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.MissingScene,
                    OrbitScenePath,
                    string.Empty,
                    "Orbit scene is missing.");
                return;
            }

            report.CheckedSceneCount++;
            string source = File.ReadAllText(fullPath).Replace("\r\n", "\n");
            string sourcePrefabToken = "m_SourcePrefab: {fileID: 100100000, guid: " + ProductionGasGiantPrefabGuid;
            int sourcePrefabCount = CountOccurrences(source, sourcePrefabToken);
            if (sourcePrefabCount <= 0)
            {
                report.AddWarning(
                    AegirGasGiantSourceFindingCategory.SceneOverrideRisk,
                    OrbitScenePath,
                    "GasGiant_Aegir",
                    "Orbit scene does not reference the production Aegir prefab GUID. This validator cannot prove the scene binding route.");
                return;
            }

            if (sourcePrefabCount > 1)
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.SceneOverrideRisk,
                    OrbitScenePath,
                    "GasGiant_Aegir",
                    "Orbit scene references " + sourcePrefabCount + " production Aegir prefab instances. Keep one celestial renderer/source owner.");
            }

            if (ContainsPrefabOverride(source, "m_Mesh", "guid: " + UnityBuiltInPrimitiveMeshGuid))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.SceneOverrideRisk,
                    OrbitScenePath,
                    "GasGiant_Aegir/m_Mesh",
                    "Orbit scene overrides production prefab mesh back to Unity built-in Sphere. Run Hecton8/Validation/Repair Aegir Gas Giant Source Contract.");
            }

            if (ContainsPrefabOverride(source, "m_CastShadows", "value: 1"))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.SceneOverrideRisk,
                    OrbitScenePath,
                    "GasGiant_Aegir/m_CastShadows",
                    "Orbit scene overrides Aegir renderer to cast shadows.");
            }

            if (ContainsPrefabOverride(source, "m_LightProbeUsage", "value: 1"))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.SceneOverrideRisk,
                    OrbitScenePath,
                    "GasGiant_Aegir/m_LightProbeUsage",
                    "Orbit scene overrides Aegir renderer to use light probes.");
            }
        }

        private static void ValidateRuntimeSourceContracts(AegirGasGiantSourceValidationReport report)
        {
            ValidateSourceTokens(
                CelestialEnginePath,
                report,
                "HectonCelestialEngine",
                "ResolveAegirSkyProjectionStormEmission()",
                "_AegirStormEmissionInvalidWarningHash",
                "AegirStormEmissionWarningCooldownFrames",
                "ReportAegirStormEmissionInvalidIfNeeded",
                "Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission());",
                "Shader.SetGlobalFloat(_ID_H8AegirStormEmission, 1f);",
                "block.SetFloat(_ID_StormEmission, ResolveAegirSkyProjectionStormEmission());",
                "RestoreCelestialTextureDefaults();",
                "ClearAegirMaterialRuntimeCache();",
                "aegirRenderer.SetPropertyBlock(null);",
                "_aegirSharedMaterial = null;",
                "PublishOceanCelestialProjectionGlobals(aegirDirection)",
                "_ID_HectonEclipseWaterShadowParams",
                "_ID_HectonEclipseWaterShadowDirection",
                "_ID_HectonRingCausticsParams",
                "_ID_HectonRingCausticsDirection",
                "ResolveAupOceanShadowCenterRuntimeXZ",
                "TryResolvePlayerAup");
            ValidateSourceTokens(
                OrbitalRelativityDirectorPath,
                report,
                "OrbitalRelativityDirector",
                "_aegirStormEmissionId",
                "Shader.SetGlobalFloat(_aegirStormEmissionId, 1f);");
            ValidateSourceTokens(
                AegirSkyShaderPath,
                report,
                "Hecton_AegirSky",
                "float _H8AegirStormEmission;",
                "float AegirStormEmission()",
                "clamp(_H8AegirStormEmission, 0.0, 4.0)",
                "stormBand * cloudTexture * 0.15 * stormEmission",
                "bands += float3(0.095, 0.052, 0.022) * stormSignal * stormEmission");
            ValidateSourceTokens(
                ProofContactSheetBuilderPath,
                report,
                "BuildAegirGasGiantProofContactSheet",
                "storm_emission",
                "stormEmissionMultiplier",
                "weather-driven storm emission");
        }

        private static void ValidateSourceTokens(
            string assetPath,
            AegirGasGiantSourceValidationReport report,
            string componentPath,
            params string[] requiredTokens)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(fullPath))
            {
                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRuntimeSourceContract,
                    assetPath,
                    componentPath,
                    "Runtime source file is missing.");
                return;
            }

            report.CheckedSourceCount++;
            string source = File.ReadAllText(fullPath).Replace("\r\n", "\n");
            for (int i = 0; i < requiredTokens.Length; i++)
            {
                string token = requiredTokens[i];
                if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                    continue;

                report.AddFail(
                    AegirGasGiantSourceFindingCategory.BadRuntimeSourceContract,
                    assetPath,
                    componentPath,
                    "Runtime source contract token is missing: " + token);
            }
        }

        private static bool ContainsPrefabOverride(string source, string propertyPath, string expectedFollowingToken)
        {
            int searchFrom = 0;
            string targetToken = "guid: " + ProductionGasGiantPrefabGuid;
            while (searchFrom >= 0 && searchFrom < source.Length)
            {
                int target = source.IndexOf(targetToken, searchFrom, StringComparison.Ordinal);
                if (target < 0)
                    return false;

                int property = source.IndexOf("propertyPath: " + propertyPath, target, StringComparison.Ordinal);
                if (property < 0)
                    return false;

                int nextTarget = source.IndexOf(targetToken, target + targetToken.Length, StringComparison.Ordinal);
                int end = nextTarget >= 0 ? nextTarget : Math.Min(source.Length, property + 240);
                string block = source.Substring(target, Math.Min(end - target, 360));
                if (block.IndexOf("propertyPath: " + propertyPath, StringComparison.Ordinal) >= 0 &&
                    block.IndexOf(expectedFollowingToken, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                searchFrom = target + targetToken.Length;
            }

            return false;
        }

        private static int CountOccurrences(string source, string token)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while (index >= 0 && index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static int RepairMaterialBindings()
        {
            int changeCount = 0;
            changeCount += EnsureMaterialTexture(GasGiantMaterialPath, "_MainTex", CanonicalBandTexturePath);
            changeCount += EnsureMaterialTexture(GasGiantMaterialPath, "_DetailTex", CanonicalDetailTexturePath);
            changeCount += EnsureMaterialTexture(GasGiantMaterialPath, "_StormTex", CanonicalStormTexturePath);
            changeCount += EnsureMaterialFloat(GasGiantMaterialPath, "_StormEmission", 1f);
            changeCount += EnsureMaterialTexture(SkyMaterialPath, "_AegirBandTex", CanonicalBandTexturePath);
            return changeCount;
        }

        private static int EnsureMaterialTexture(string materialPath, string propertyName, string texturePath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (material == null || texture == null || !material.HasProperty(propertyName))
                return 0;

            if (material.GetTexture(propertyName) == texture)
                return 0;

            material.SetTexture(propertyName, texture);
            EditorUtility.SetDirty(material);
            return 1;
        }

        private static int EnsureMaterialFloat(string materialPath, string propertyName, float expectedValue)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !material.HasProperty(propertyName))
                return 0;

            if (Math.Abs(material.GetFloat(propertyName) - expectedValue) <= 0.0001f)
                return 0;

            material.SetFloat(propertyName, expectedValue);
            EditorUtility.SetDirty(material);
            return 1;
        }

        private static int RepairPrefabSource(string prefabPath)
        {
            Mesh canonicalMesh = ResolveCanonicalGasGiantMesh();
            Material canonicalMaterial = AssetDatabase.LoadAssetAtPath<Material>(GasGiantMaterialPath);
            if (canonicalMesh == null || canonicalMaterial == null)
                return 0;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return 0;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
                return 0;

            int changeCount = 0;
            try
            {
                MeshFilter meshFilter = prefabRoot.GetComponentInChildren<MeshFilter>(true);
                if (meshFilter != null && meshFilter.sharedMesh != canonicalMesh)
                {
                    meshFilter.sharedMesh = canonicalMesh;
                    changeCount++;
                }

                Renderer renderer = meshFilter != null ? meshFilter.GetComponent<Renderer>() : prefabRoot.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                    changeCount += ApplyRendererSourceState(renderer, canonicalMaterial);

                if (changeCount > 0)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return changeCount;
        }

        private static int RepairOrbitSceneFromMenu()
        {
            Mesh canonicalMesh = ResolveCanonicalGasGiantMesh();
            Material canonicalMaterial = AssetDatabase.LoadAssetAtPath<Material>(GasGiantMaterialPath);
            if (canonicalMesh == null || canonicalMaterial == null)
            {
                Debug.LogError("[AegirGasGiantSourceValidator][Repair] Missing canonical mesh or material; cannot repair scene.");
                return 0;
            }

            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), OrbitScenePath)))
                return 0;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[AegirGasGiantSourceValidator][Repair] Scene repair cancelled because current modified scenes were not saved.");
                return 0;
            }

            Scene scene = EditorSceneManager.OpenScene(OrbitScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                return 0;

            int changeCount = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    Transform transform = transforms[j];
                    if (!transform.name.Equals("GasGiant_Aegir", StringComparison.Ordinal))
                        continue;

                    MeshFilter meshFilter = transform.GetComponentInChildren<MeshFilter>(true);
                    if (meshFilter != null && meshFilter.sharedMesh != canonicalMesh)
                    {
                        meshFilter.sharedMesh = canonicalMesh;
                        changeCount++;
                    }

                    Renderer renderer = meshFilter != null ? meshFilter.GetComponent<Renderer>() : transform.GetComponentInChildren<Renderer>(true);
                    if (renderer != null)
                        changeCount += ApplyRendererSourceState(renderer, canonicalMaterial);

                    changeCount += RevertAegirScenePrefabOverrides(meshFilter, renderer);
                }
            }

            if (changeCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return changeCount;
        }

        private static int ApplyRendererSourceState(Renderer renderer, Material material)
        {
            int changeCount = 0;
            if (renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
                changeCount++;
            }

            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                changeCount++;
            }

            if (renderer.receiveShadows)
            {
                renderer.receiveShadows = false;
                changeCount++;
            }

            if (renderer.lightProbeUsage != LightProbeUsage.Off)
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                changeCount++;
            }

            if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
            {
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                changeCount++;
            }

            if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                changeCount++;
            }

            if (renderer.allowOcclusionWhenDynamic)
            {
                renderer.allowOcclusionWhenDynamic = false;
                changeCount++;
            }

            return changeCount;
        }

        private static int RevertAegirScenePrefabOverrides(MeshFilter meshFilter, Renderer renderer)
        {
            int changeCount = 0;
            changeCount += RevertPrefabPropertyOverride(meshFilter, "m_Mesh");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_CastShadows");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_ReceiveShadows");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_LightProbeUsage");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_ReflectionProbeUsage");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_MotionVectors");
            changeCount += RevertPrefabPropertyOverride(renderer, "m_DynamicOccludee");
            return changeCount;
        }

        private static int RevertPrefabPropertyOverride(UnityEngine.Object target, string propertyPath)
        {
            if (target == null || string.IsNullOrEmpty(propertyPath) || !PrefabUtility.IsPartOfPrefabInstance(target))
                return 0;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.prefabOverride)
                return 0;

            PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            return 1;
        }

        private static Mesh ResolveCanonicalGasGiantMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalProductionMeshPath);
            if (mesh != null && !IsUnityBuiltInPrimitiveMesh(mesh, out _, out _))
                return mesh;

            GameObject productionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionGasGiantPrefabPath);
            if (productionPrefab == null)
                return null;

            MeshFilter meshFilter = productionPrefab.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return null;

            return IsUnityBuiltInPrimitiveMesh(meshFilter.sharedMesh, out _, out _) ? null : meshFilter.sharedMesh;
        }

        private static bool IsUnityBuiltInPrimitiveMesh(Mesh mesh, out long localFileId, out string meshName)
        {
            localFileId = 0L;
            meshName = mesh != null ? mesh.name : "<null>";
            if (mesh == null)
                return false;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out localFileId) &&
                guid.Equals(UnityBuiltInPrimitiveMeshGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return mesh.name == "Cube" ||
                   mesh.name == "Sphere" ||
                   mesh.name == "Capsule" ||
                   mesh.name == "Cylinder" ||
                   mesh.name == "Plane" ||
                   mesh.name == "Quad";
        }

        private static string BuildTransformPath(Transform root, Transform leaf)
        {
            if (leaf == null)
                return string.Empty;

            Stack<string> names = new Stack<string>();
            Transform current = leaf;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }

    public sealed class AegirGasGiantSourceValidationReport
    {
        private readonly List<AegirGasGiantSourceValidationFinding> _findings =
            new List<AegirGasGiantSourceValidationFinding>(24);

        public IReadOnlyList<AegirGasGiantSourceValidationFinding> Findings => _findings;
        public int CheckedMaterialCount { get; internal set; }
        public int CheckedTextureCount { get; internal set; }
        public int CheckedPrefabCount { get; internal set; }
        public int CheckedSceneCount { get; internal set; }
        public int CheckedSourceCount { get; internal set; }
        public int FailureCount { get; private set; }
        public int WarningCount { get; private set; }

        public void AddFail(AegirGasGiantSourceFindingCategory category, string assetPath, string componentPath, string message)
        {
            Add(AegirGasGiantSourceFindingSeverity.Fail, category, assetPath, componentPath, message);
        }

        public void AddWarning(AegirGasGiantSourceFindingCategory category, string assetPath, string componentPath, string message)
        {
            Add(AegirGasGiantSourceFindingSeverity.Warning, category, assetPath, componentPath, message);
        }

        public void AddInfo(AegirGasGiantSourceFindingCategory category, string assetPath, string componentPath, string message)
        {
            Add(AegirGasGiantSourceFindingSeverity.Info, category, assetPath, componentPath, message);
        }

        public void Add(
            AegirGasGiantSourceFindingSeverity severity,
            AegirGasGiantSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            if (severity == AegirGasGiantSourceFindingSeverity.Fail)
                FailureCount++;
            else if (severity == AegirGasGiantSourceFindingSeverity.Warning)
                WarningCount++;

            _findings.Add(new AegirGasGiantSourceValidationFinding(
                severity,
                category,
                assetPath ?? string.Empty,
                componentPath ?? string.Empty,
                message ?? string.Empty));
        }
    }

    public readonly struct AegirGasGiantSourceValidationFinding
    {
        public readonly AegirGasGiantSourceFindingSeverity Severity;
        public readonly AegirGasGiantSourceFindingCategory Category;
        public readonly string AssetPath;
        public readonly string ComponentPath;
        public readonly string Message;

        public AegirGasGiantSourceValidationFinding(
            AegirGasGiantSourceFindingSeverity severity,
            AegirGasGiantSourceFindingCategory category,
            string assetPath,
            string componentPath,
            string message)
        {
            Severity = severity;
            Category = category;
            AssetPath = assetPath;
            ComponentPath = componentPath;
            Message = message;
        }
    }

    public enum AegirGasGiantSourceFindingSeverity
    {
        Info = 0,
        Warning = 1,
        Fail = 2
    }

    public enum AegirGasGiantSourceFindingCategory
    {
        MissingMaterial = 0,
        MissingTexture = 1,
        MissingPrefab = 2,
        MissingScene = 3,
        BadMaterialShader = 4,
        BadTextureBinding = 5,
        BadTextureImport = 6,
        BadPrefabBinding = 7,
        BadMaterialBinding = 8,
        BuiltInPrimitiveMesh = 9,
        BadRendererState = 10,
        SceneOverrideRisk = 11,
        BadRuntimeSourceContract = 12
    }
}
