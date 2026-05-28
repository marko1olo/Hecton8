using System;
using System.IO;
using Hecton8.Visor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    internal static class QuestVrOptimizationValidator1406
    {
        private const string QuestUrpPath = "Assets/_Project/Data/URP_Quest_VR.asset";
        private const string QuestRendererPath = "Assets/_Project/Data/Quest_VR_Renderer.asset";
        private const string QualityPath = "ProjectSettings/QualitySettings.asset";
        private const string OpenXrPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        private const string ConfiguratorPath = "Assets/_Project/Scripts/Editor/Build/QuestVulkanRenderPipelineConfigurator.cs";
        private const string XrReadinessValidatorPath = "Assets/_Project/Scripts/Editor/Build/XrPlatformReadinessValidator.cs";
        private const string FluidAdvectionRenderFeaturePath = "Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs";
        private const string WristPdaScreenProjectorFeaturePath = "Assets/_Project/Scripts/UI/WristPdaScreenProjectorFeature.cs";
        private const string BrownoutRenderFeaturePath = "Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs";
        private const string VisorUberPostFeaturePath = "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs";
        private const string TextureGuardPath = "Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs";
        private const string UnderwaterVisualsPath = "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs";
        private const string QuestUrpGuid = "d9c4cd6a763fec04a913c6a149663003";

        [MenuItem("Hecton8/Validation/Quest VR Optimization 1406")]
        private static void ValidateFromMenu()
        {
            ValidateAll();
            Debug.Log("Quest VR optimization static validation 1406 passed.");
        }

        internal static void ValidateAll()
        {
            ValidateAssetDatabaseLoads();
            ValidateQuestUrpYaml();
            ValidateQualityBinding();
            ValidateOpenXrYaml();
            ValidateRendererStripping();
            ValidatePrebuildConfigurator();
            ValidateXrReadinessValidator();
            ValidateFluidAdvectionRenderFeature();
            ValidateWristPdaScreenProjectorFeature();
            ValidateBrownoutRenderFeature();
            ValidateVisorUberPostFeature();
            ValidateCameraTexturePolicy();
            ValidateFoveationMock();
        }

        private static void ValidateAssetDatabaseLoads()
        {
            AssertObjectLoaded(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(QuestUrpPath), QuestUrpPath);
            AssertObjectLoaded(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(QuestRendererPath), QuestRendererPath);
            AssertObjectLoaded(AssetDatabase.LoadAllAssetsAtPath(OpenXrPath), OpenXrPath);
        }

        private static void ValidateQuestUrpYaml()
        {
            string urp = ReadRequiredText(QuestUrpPath);
            AssertContains(urp, "--- !u!114 &11400000", QuestUrpPath, "root fileID");
            AssertContains(urp, "m_Name: URP_Quest_VR", QuestUrpPath, "profile name");
            AssertContains(urp, "m_MSAA: 2", QuestUrpPath, "VR MSAA cap");
            AssertContains(urp, "m_RenderScale: 0.85", QuestUrpPath, "Quest render scale cap");
            AssertContains(urp, "m_RequireOpaqueTexture: 0", QuestUrpPath, "opaque texture disabled");
            AssertContains(urp, "m_SupportsHDR: 0", QuestUrpPath, "HDR disabled");
            AssertContains(urp, "m_AdditionalLightsPerObjectLimit: 1", QuestUrpPath, "additional light cap");
            AssertContains(urp, "m_ShadowDistance: 18", QuestUrpPath, "shadow distance");
            AssertContains(urp, "m_ShadowCascadeCount: 1", QuestUrpPath, "single shadow cascade");
            AssertContains(urp, "m_SoftShadowQuality: 0", QuestUrpPath, "soft shadow quality stripped");
            AssertContains(urp, "m_PrefilteringModeScreenSpaceOcclusion: 0", QuestUrpPath, "SSAO prefilter stripped");
            AssertContains(urp, "m_VolumeProfile: {fileID: 0}", QuestUrpPath, "Quest global volume disabled");
        }

        private static void ValidateQualityBinding()
        {
            string quality = ReadRequiredText(QualityPath);
            AssertContains(quality, "name: Quest (VR)", QualityPath, "Quest quality tier");
            AssertContains(quality, "customRenderPipeline: {fileID: 11400000, guid: " + QuestUrpGuid + ", type: 2}", QualityPath, "Quest URP GUID");
            AssertContains(quality, "Android: 3", QualityPath, "Android default quality index");
        }

        private static void ValidateOpenXrYaml()
        {
            string openXr = ReadRequiredText(OpenXrPath);
            string foveation = ExtractNamedBlock(openXr, "FoveatedRenderingFeature Android", OpenXrPath);
            AssertContains(foveation, "m_enabled: 1", OpenXrPath, "Android foveation feature enabled");
            AssertContains(foveation, "enableSubsampledLayout: 1", OpenXrPath, "subsampled foveation layout enabled");

            string androidSettings = ExtractNamedBlock(openXr, "Android", OpenXrPath);
            AssertContains(androidSettings, "m_renderMode: 1", OpenXrPath, "Android single-pass render mode");
            AssertContains(androidSettings, "m_optimizeBufferDiscards: 1", OpenXrPath, "Android buffer discard optimization");
            AssertContains(androidSettings, "m_symmetricProjection: 1", OpenXrPath, "Android symmetric projection required for multiview regions");
            AssertContains(androidSettings, "m_optimizeMultiviewRenderRegions: 1", OpenXrPath, "Android multiview region optimization");
            AssertContains(androidSettings, "m_multiviewRenderRegionsOptimizationMode: 1", OpenXrPath, "Android multiview region final-pass mode");
            AssertContains(androidSettings, "m_foveatedRenderingApi: 1", OpenXrPath, "OpenXR foveation API");

            string metaQuest = ExtractNamedBlock(openXr, "MetaQuestFeature Android", OpenXrPath);
            AssertContains(metaQuest, "m_enabled: 1", OpenXrPath, "Meta Quest feature enabled");
            AssertContains(metaQuest, "m_symmetricProjection: 1", OpenXrPath, "Meta symmetric projection required for multiview regions");
            AssertContains(metaQuest, "optimizeMultiviewRenderRegions: 1", OpenXrPath, "Meta multiview region optimization");
            AssertContains(metaQuest, "m_multiviewRenderRegionsOptimizationMode: 1", OpenXrPath, "Meta multiview region final-pass mode");
        }

        private static void ValidateRendererStripping()
        {
            string renderer = ReadRequiredText(QuestRendererPath);
            AssertContains(ExtractNamedBlock(renderer, "HectonAbyssalSsdoFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "SSDO inactive");
            AssertContains(ExtractNamedBlock(renderer, "HectonNoirDepthFogFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "depth fog inactive");
            AssertContains(ExtractNamedBlock(renderer, "HectonScooterVolumetricShaftsFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "volumetric shafts inactive");
            AssertContains(ExtractNamedBlock(renderer, "HectonRetinaDistortionFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "retina distortion inactive");
            AssertContains(ExtractNamedBlock(renderer, "HectonVisorFluidDistortionFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "visor fluid distortion inactive");
            AssertContains(ExtractNamedBlock(renderer, "ShapesRenderFeature", QuestRendererPath), "m_Active: 0", QuestRendererPath, "Shapes immediate-mode renderer inactive");
        }

        private static void ValidatePrebuildConfigurator()
        {
            string configurator = ReadRequiredText(ConfiguratorPath);
            AssertContains(configurator, "public int callbackOrder => -4700;", ConfiguratorPath, "prebuild runs before graphics/XR validators");
            AssertContains(configurator, "UniversalRenderPipelineAsset urpAsset = EnsureQuestAssets(logSummary: true);", ConfiguratorPath, "prebuild owns Quest URP asset");
            AssertContains(configurator, "int questIndex = EnsureQuestQualityRow(urpAsset);", ConfiguratorPath, "prebuild ensures Quest quality row");
            AssertContains(configurator, "IsolateAndroidQualityLevel(questIndex);", ConfiguratorPath, "prebuild isolates Android quality route");
            AssertContains(configurator, "PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);", ConfiguratorPath, "prebuild disables automatic Android graphics API");
            AssertContains(configurator, "PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });", ConfiguratorPath, "prebuild forces Android Vulkan");
            AssertContains(configurator, "SetBool(serialized, \"m_RequireDepthTexture\", true);", ConfiguratorPath, "prebuild depth texture preserved");
            AssertContains(configurator, "SetBool(serialized, \"m_RequireOpaqueTexture\", false);", ConfiguratorPath, "prebuild opaque texture stripped");
            AssertContains(configurator, "SetInt(serialized, \"m_MSAA\", 2);", ConfiguratorPath, "prebuild MSAA cap");
            AssertContains(configurator, "SetFloat(serialized, \"m_RenderScale\", 0.85f);", ConfiguratorPath, "prebuild render scale cap");
            AssertContains(configurator, "SetInt(serialized, \"m_AdditionalLightsPerObjectLimit\", 1);", ConfiguratorPath, "prebuild additional light cap");
            AssertContains(configurator, "SetFloat(serialized, \"m_ShadowDistance\", 18f);", ConfiguratorPath, "prebuild shadow distance");
            AssertContains(configurator, "SetInt(serialized, \"m_ShadowCascadeCount\", 1);", ConfiguratorPath, "prebuild shadow cascade cap");
            AssertContains(configurator, "SetObject(serialized, \"m_VolumeProfile\", null);", ConfiguratorPath, "prebuild global volume strip");
            AssertContains(configurator, "Contains(name, \"RetinaDistortion\")", ConfiguratorPath, "prebuild retina distortion strip");
            AssertContains(configurator, "Contains(name, \"VisorFluidDistortion\")", ConfiguratorPath, "prebuild visor fluid distortion strip");
            AssertContains(configurator, "Contains(name, \"ShapesRenderFeature\")", ConfiguratorPath, "prebuild Shapes immediate-mode strip");
            AssertNotContains(configurator, "SetInt(serialized, \"m_MSAA\", 4);", ConfiguratorPath, "prebuild must not restore 4x MSAA");
            AssertNotContains(configurator, "SetFloat(serialized, \"m_RenderScale\", 1f);", ConfiguratorPath, "prebuild must not restore full render scale");
        }

        private static void ValidateXrReadinessValidator()
        {
            string validator = ReadRequiredText(XrReadinessValidatorPath);
            AssertContains(validator, "ConfigureAndroidQuestOpenXrRenderSettings(openXrSettings);", XrReadinessValidatorPath, "XR CI route configures Quest render settings");
            AssertContains(validator, "EnableOpenXrFeature(foveatedRenderingFeature);", XrReadinessValidatorPath, "XR CI route enables foveated rendering feature");
            AssertContains(validator, "SetFoveatedSubsampledLayoutEnabled(foveatedRenderingFeature, true);", XrReadinessValidatorPath, "XR CI route enables subsampled layout");
            AssertContains(validator, "OpenXRSettings.RenderMode.SinglePassInstanced", XrReadinessValidatorPath, "XR validator enforces single-pass instanced");
            AssertContains(validator, "openXrSettings.symmetricProjection", XrReadinessValidatorPath, "XR validator enforces symmetric projection");
            AssertContains(validator, "OpenXRSettings.MultiviewRenderRegionsOptimizationMode.FinalPass", XrReadinessValidatorPath, "XR validator enforces final-pass multiview regions");
            AssertContains(validator, "OpenXRSettings.BackendFovationApi.SRPFoveation", XrReadinessValidatorPath, "XR validator enforces SRP foveation API");
            AssertContains(validator, "OpenXR Android Foveated Rendering subsampled layout is disabled.", XrReadinessValidatorPath, "XR validator hard-fails subsampled layout drift");
        }

        private static void ValidateCameraTexturePolicy()
        {
            string guard = ReadRequiredText(TextureGuardPath);
            AssertContains(guard, "QuestVrMobileSurvival", TextureGuardPath, "Quest mobile policy present");
            AssertContains(guard, "if (UsesQuestVrMobileSurvivalPolicy)", TextureGuardPath, "Quest camera color/post guard branch");
            AssertTokenAfter(guard, "if (UsesQuestVrMobileSurvivalPolicy)", "cameraData.requiresColorOption", TextureGuardPath, "Quest guard must return before opaque color force");
            AssertContains(guard, "StoreCameraDataCacheEntry(instanceId, null);", TextureGuardPath, "Quest guard negative camera-data cache");
            AssertContains(guard, "s_cameraDataCacheCursor", TextureGuardPath, "Quest guard bounded ring camera-data cache");
            AssertContains(guard, "ReportRuntimeRequirementViolation(\"Active URP asset has Camera Opaque Texture disabled.\");", TextureGuardPath, "non-Quest opaque violation preserved");

            string underwater = ReadRequiredText(UnderwaterVisualsPath);
            AssertContains(underwater, "HectonUrpTextureRequirementsGuard.UsesQuestVrMobileSurvivalPolicy", UnderwaterVisualsPath, "underwater camera color force bypass");
            AssertTokenAfter(underwater, "HectonUrpTextureRequirementsGuard.UsesQuestVrMobileSurvivalPolicy", "cameraData.requiresColorOption", UnderwaterVisualsPath, "underwater Quest path must return before opaque color force");
            AssertNotContains(underwater, "private static void EnsureCameraTextureRequirements(Camera camera)", UnderwaterVisualsPath, "underwater direct camera lookup overload removed");
            AssertNotContains(underwater, "cameraData.TryGetComponent(out camera);", UnderwaterVisualsPath, "underwater no hidden cameraData lookup fallback");
            AssertNotContains(underwater, "EnsureCameraTextureRequirements(_gameplayMainCamera);", UnderwaterVisualsPath, "editor gameplay camera uses cached texture requirements");
            AssertContains(underwater, "ApplyBiomeFogBlend(lerpT);", UnderwaterVisualsPath, "biome fog applies local visual fake");
            AssertNotContains(underwater, "BiomeTransitionFogBlendJob job", UnderwaterVisualsPath, "one-sample biome fog job removed");
            AssertNotContains(underwater, "BufferID.UnderwaterBiomeFog", UnderwaterVisualsPath, "biome fog no longer uses DataVault buffers");
            AssertNotContains(underwater, "_biomeFogVault", UnderwaterVisualsPath, "biome fog DataVault alias removed");
        }

        private static void ValidateFluidAdvectionRenderFeature()
        {
            string feature = ReadRequiredText(FluidAdvectionRenderFeaturePath);
            AssertContains(feature, "IFluidAdvectionRenderGraphDispatchSource engine = _cachedFluidEngine;", FluidAdvectionRenderFeaturePath, "fluid advection uses cached rendergraph owner");
            AssertContains(feature, "if (engine == null)", FluidAdvectionRenderFeaturePath, "fluid advection skips pass enqueue without owner");
            AssertContains(feature, "_pass.Setup(engine);", FluidAdvectionRenderFeaturePath, "fluid advection passes cached owner to render pass");
            AssertNotContains(feature, "_pass.Setup(_cachedFluidEngine);", FluidAdvectionRenderFeaturePath, "fluid advection no stale direct field setup");
        }

        private static void ValidateWristPdaScreenProjectorFeature()
        {
            string feature = ReadRequiredText(WristPdaScreenProjectorFeaturePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", WristPdaScreenProjectorFeaturePath, "PDA projector checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", WristPdaScreenProjectorFeaturePath, "PDA projector skips non-game camera types before enqueue");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "_pass.Setup(settings, _material);", WristPdaScreenProjectorFeaturePath, "PDA projector skip guard must precede pass setup");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material);", "renderer.EnqueuePass(_pass);", WristPdaScreenProjectorFeaturePath, "PDA projector setup must precede enqueue");
        }

        private static void ValidateBrownoutRenderFeature()
        {
            string feature = ReadRequiredText(BrownoutRenderFeaturePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", BrownoutRenderFeaturePath, "brownout checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", BrownoutRenderFeaturePath, "brownout skips non-game camera types before enqueue");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "Camera renderCamera = renderingData.cameraData.camera;", BrownoutRenderFeaturePath, "brownout skip guard must precede camera state build");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material, runtimeState);", "renderer.EnqueuePass(_pass);", BrownoutRenderFeaturePath, "brownout setup must precede enqueue");
        }

        private static void ValidateVisorUberPostFeature()
        {
            string feature = ReadRequiredText(VisorUberPostFeaturePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", VisorUberPostFeaturePath, "visor uber post checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", VisorUberPostFeaturePath, "visor uber post skips non-game camera types before enqueue");
            AssertTokenNotBetween(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "ClearRawColorHistoryRequest();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must not clear game-camera raw history state");
            AssertTokenNotBetween(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "ClearPendingReconstructionInput();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must not clear game-camera reconstruction input");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must precede unified pass enqueue");
            AssertTokenAfter(feature, "Camera renderCamera = renderingData.cameraData.camera;", "_pass.Setup(", VisorUberPostFeaturePath, "visor uber post state build must precede pass setup");
        }

        private static void ValidateFoveationMock()
        {
            if (!QuestFoveationDriver.WouldAbortCleanlyForUnsupported(false, FoveatedRenderingCaps.None))
                throw new FatalArchitectureException("Unsupported XR foveation path did not fail closed.");

            byte unsupported = QuestFoveationDriver.ResolveMockTargetLevelCode(false, FoveatedRenderingCaps.None, 0f, 1f, false);
            if (unsupported != 0)
                throw new FatalArchitectureException("Unsupported foveation target did not resolve to Disabled.");

            float unsupportedLevel = QuestFoveationDriver.ResolveMockTargetLevel01(false, FoveatedRenderingCaps.None, 0f, 1f, false);
            if (unsupportedLevel != 0f)
                throw new FatalArchitectureException("Unsupported foveation level did not resolve to zero.");

            byte survival = QuestFoveationDriver.ResolveMockTargetLevelCode(true, FoveatedRenderingCaps.NonUniformRaster, 0f, 1f, false);
            if (survival != 3)
                throw new FatalArchitectureException("Survival foveation did not resolve to high fixed foveation.");

            float visual = QuestFoveationDriver.ResolveMockTargetLevel01(true, FoveatedRenderingCaps.NonUniformRaster, 1f, 0f, false);
            if (visual < 0.35f)
                throw new FatalArchitectureException("Visual-overkill foveation dropped below low fixed foveation floor.");
        }

        private static string ReadRequiredText(string path)
        {
            if (!File.Exists(path))
                throw new FatalArchitectureException("Required file missing: " + path);

            return File.ReadAllText(path);
        }

        private static string ExtractNamedBlock(string text, string assetName, string path)
        {
            string marker = "m_Name: " + assetName;
            int search = 0;
            while (search < text.Length)
            {
                int start = text.IndexOf(marker, search, StringComparison.Ordinal);
                if (start < 0)
                    break;

                int lineStart = start;
                while (lineStart > 0 && text[lineStart - 1] != '\n')
                    lineStart--;

                int lineEnd = text.IndexOf('\n', start);
                if (lineEnd < 0)
                    lineEnd = text.Length;

                int contentStart = lineStart;
                while (contentStart < lineEnd && (text[contentStart] == ' ' || text[contentStart] == '\t'))
                    contentStart++;

                int contentEnd = lineEnd;
                while (contentEnd > contentStart &&
                       (text[contentEnd - 1] == '\r' || text[contentEnd - 1] == ' ' || text[contentEnd - 1] == '\t'))
                {
                    contentEnd--;
                }

                int markerLength = contentEnd - contentStart;
                if (markerLength == marker.Length &&
                    string.CompareOrdinal(text, contentStart, marker, 0, marker.Length) == 0)
                {
                    int next = text.IndexOf("\n--- !u!", start + marker.Length, StringComparison.Ordinal);
                    if (next < 0)
                        next = text.Length;

                    return text.Substring(lineStart, next - lineStart);
                }

                search = start + marker.Length;
            }

            throw new FatalArchitectureException("Missing exact m_Name block in " + path + ": " + assetName);
        }

        private static void AssertContains(string text, string token, string path, string context)
        {
            if (text.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new FatalArchitectureException("Validation failed in " + path + " for " + context + ": missing " + token);
        }

        private static void AssertNotContains(string text, string token, string path, string context)
        {
            if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new FatalArchitectureException("Validation failed in " + path + " for " + context + ": forbidden " + token);
        }

        private static void AssertTokenAfter(string text, string firstToken, string secondToken, string path, string context)
        {
            int first = text.IndexOf(firstToken, StringComparison.Ordinal);
            int second = text.IndexOf(secondToken, StringComparison.Ordinal);
            if (first < 0 || second < 0 || second <= first)
                throw new FatalArchitectureException("Validation failed in " + path + " for " + context);
        }

        private static void AssertTokenNotBetween(string text, string startToken, string forbiddenToken, string endToken, string path, string context)
        {
            int start = text.IndexOf(startToken, StringComparison.Ordinal);
            int end = start < 0 ? -1 : text.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
            int forbidden = start < 0 ? -1 : text.IndexOf(forbiddenToken, start + startToken.Length, StringComparison.Ordinal);
            if (start < 0 || end < 0 || (forbidden >= 0 && forbidden < end))
                throw new FatalArchitectureException("Validation failed in " + path + " for " + context);
        }

        private static void AssertObjectLoaded(UnityEngine.Object value, string path)
        {
            if (value == null)
                throw new FatalArchitectureException("AssetDatabase failed to load " + path);
        }

        private static void AssertObjectLoaded(UnityEngine.Object[] values, string path)
        {
            if (values == null || values.Length == 0)
                throw new FatalArchitectureException("AssetDatabase failed to load any objects from " + path);
        }

        private sealed class FatalArchitectureException : Exception
        {
            public FatalArchitectureException(string message)
                : base(message)
            {
            }
        }
    }
}
