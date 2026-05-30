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
        private const string WristPdaProjectionRuntimePath = "Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs";
        private const string BrownoutRenderFeaturePath = "Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs";
        private const string VisorUberPostFeaturePath = "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs";
        private const string VisorUberPostNoirFeaturePath = "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs";
        private const string DeferredCausticsFeaturePath = "Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs";
        private const string DeferredCausticsRuntimePath = "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs";
        private const string UberNoirRuntimeBridgePath = "Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs";
        private const string BilateralDrsRuntimePath = "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs";
        private const string VolumetricParticulateFogFeaturePath = "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs";
        private const string DynamicDecalVaultRuntimePath = "Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs";
        private const string DeferredDecalPassPath = "Assets/_Project/Scripts/Visor/DeferredDecalPass.cs";
        private const string DiegeticVisorLensRuntimePath = "Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs";
        private const string DryVolumeFeaturePath = "Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs";
        private const string VoxelSsaoFeaturePath = "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs";
        private const string AtmosphereSootFeaturePath = "Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs";
        private const string FoveatedRenderCommanderPath = "Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs";
        private const string SinglePassOceanFeaturePath = "Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs";
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
            ValidateDeferredCausticsRenderFeature();
            ValidateDeferredDecalPassRenderGraphDependency();
            ValidateAdditionalRenderingDataVaultFinally();
            ValidateAtmosphereSootRenderFeature();
            ValidateFoveatedRenderCommanderDataVault();
            ValidateRuntimeTextureDescCopyTokens();
            ValidateRuntimeRenderGraphStaticCallbacks();
            ValidateOceanWakeRenderGraphZeroGcTokens();
            ValidateDryVolumeRenderGraphTextureDependency();
            ValidateVoxelSsaoContinuousQuality();
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
            AssertContains(guard, "cameraData.requiresColorOption = CameraOverrideOption.Off;", TextureGuardPath, "Quest camera opaque color is forced off");
            AssertContains(guard, "cameraData.requiresColorTexture = false;", TextureGuardPath, "Quest camera opaque color texture is disabled");
            AssertContains(guard, "cameraData.renderPostProcessing = false;", TextureGuardPath, "Quest camera post processing is disabled");
            AssertTokenSequence(
                guard,
                TextureGuardPath,
                "Quest guard disables post/color before non-Quest color force",
                "if (UsesQuestVrMobileSurvivalPolicy)",
                "cameraData.requiresColorOption = CameraOverrideOption.Off;",
                "cameraData.requiresColorTexture = false;",
                "cameraData.renderPostProcessing = false;",
                "return;",
                "if (cameraData.requiresColorOption != CameraOverrideOption.On)");
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
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", FluidAdvectionRenderFeaturePath, "fluid advection rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", FluidAdvectionRenderFeaturePath, "fluid advection AddRenderPasses skips overlay cameras");
            AssertContains(feature, "_pass.Setup(engine);", FluidAdvectionRenderFeaturePath, "fluid advection passes cached owner to render pass");
            AssertNotContains(feature, "_pass.Setup(_cachedFluidEngine);", FluidAdvectionRenderFeaturePath, "fluid advection no stale direct field setup");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "IFluidAdvectionRenderGraphDispatchSource engine = _engine;", FluidAdvectionRenderFeaturePath, "fluid rendergraph base-camera guard must precede owner claim");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "_pass.Setup(engine);", FluidAdvectionRenderFeaturePath, "fluid AddRenderPasses base-camera guard must precede setup");
        }

        private static void ValidateWristPdaScreenProjectorFeature()
        {
            string feature = ReadRequiredText(WristPdaScreenProjectorFeaturePath);
            string runtime = ReadRequiredText(WristPdaProjectionRuntimePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", WristPdaScreenProjectorFeaturePath, "PDA projector checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", WristPdaScreenProjectorFeaturePath, "PDA projector skips non-game camera types before enqueue");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", WristPdaScreenProjectorFeaturePath, "PDA projector rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", WristPdaScreenProjectorFeaturePath, "PDA projector AddRenderPasses skips overlay cameras");
            AssertContains(feature, "TextureHandle atlasTextureHandle = renderGraph.ImportTexture(atlasTexture);", WristPdaScreenProjectorFeaturePath, "PDA atlas is imported into RenderGraph");
            AssertContains(feature, "builder.UseTexture(atlasTextureHandle, AccessFlags.Read);", WristPdaScreenProjectorFeaturePath, "PDA atlas read dependency is declared");
            AssertContains(feature, "context.cmd.SetGlobalTexture(ShaderConstants.AtlasTextureId, data.AtlasTexture);", WristPdaScreenProjectorFeaturePath, "PDA atlas binding uses render command buffer");
            AssertNotContains(feature, "new TextureDesc(sourceDesc)", WristPdaScreenProjectorFeaturePath, "PDA projector hot rendergraph descriptor must use struct copy without new token");
            AssertNotContains(feature, "data.Material.SetTexture(ShaderConstants.AtlasTextureId", WristPdaScreenProjectorFeaturePath, "PDA projector must not mutate material atlas in render func");
            AssertContains(runtime, "private RTHandle _pdaProjectionAtlasHandle;", WristPdaProjectionRuntimePath, "PDA runtime owns cached atlas RTHandle");
            AssertContains(runtime, "bool atlasReady = EnsurePdaProjectionAtlasHandle();", WristPdaProjectionRuntimePath, "PDA runtime prepares atlas handle with graphics buffers");
            AssertContains(runtime, "out RTHandle atlasTexture", WristPdaProjectionRuntimePath, "PDA resource accessor exposes cached RTHandle only");
            AssertContains(runtime, "!vault.TryAcquireWriteLock(in handle, SystemID.UI, out buffer)", WristPdaProjectionRuntimePath, "PDA vault buffer helper acquires UI write lock");
            AssertTokenSequence(
                runtime,
                WristPdaProjectionRuntimePath,
                "PDA vault failed validation releases acquired write lock through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, SystemID.UI);");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "_pass.Setup(settings, _material);", WristPdaScreenProjectorFeaturePath, "PDA projector skip guard must precede pass setup");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "TextureHandle sourceTexture = resourceData.activeColorTexture;", WristPdaScreenProjectorFeaturePath, "PDA projector rendergraph base-camera guard must precede texture use");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "_pass.Setup(settings, _material);", WristPdaScreenProjectorFeaturePath, "PDA projector base-camera guard must precede pass setup");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material);", "renderer.EnqueuePass(_pass);", WristPdaScreenProjectorFeaturePath, "PDA projector setup must precede enqueue");
        }

        private static void ValidateBrownoutRenderFeature()
        {
            string feature = ReadRequiredText(BrownoutRenderFeaturePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", BrownoutRenderFeaturePath, "brownout checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", BrownoutRenderFeaturePath, "brownout skips non-game camera types before enqueue");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", BrownoutRenderFeaturePath, "brownout rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", BrownoutRenderFeaturePath, "brownout AddRenderPasses skips overlay cameras");
            AssertContains(feature, "runtimeState = default;", BrownoutRenderFeaturePath, "brownout runtime state uses allocation-proof field assignments");
            AssertNotContains(feature, "new RuntimeState(", BrownoutRenderFeaturePath, "brownout hot runtime state must not use new token");
            AssertNotContains(feature, "private readonly struct RuntimeState", BrownoutRenderFeaturePath, "brownout runtime state must remain assignable without constructor token");
            AssertNotContains(feature, "new BrownoutGlobalsDTO(", BrownoutRenderFeaturePath, "brownout hot globals must use default field assignments");
            AssertNotContains(feature, "new Vector4(", BrownoutRenderFeaturePath, "brownout hot vector payloads must use default field assignments");
            AssertNotContains(feature, "new TextureDesc(sourceDesc)", BrownoutRenderFeaturePath, "brownout hot rendergraph descriptor must use struct copy without new token");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "TextureHandle sourceTexture = resourceData.activeColorTexture;", BrownoutRenderFeaturePath, "brownout rendergraph base-camera guard must precede texture use");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "Camera renderCamera = renderingData.cameraData.camera;", BrownoutRenderFeaturePath, "brownout skip guard must precede camera state build");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "Camera renderCamera = renderingData.cameraData.camera;", BrownoutRenderFeaturePath, "brownout base-camera guard must precede camera state build");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material, runtimeState);", "renderer.EnqueuePass(_pass);", BrownoutRenderFeaturePath, "brownout setup must precede enqueue");
        }

        private static void ValidateVisorUberPostFeature()
        {
            string feature = ReadRequiredText(VisorUberPostFeaturePath);
            string noirFeature = ReadRequiredText(VisorUberPostNoirFeaturePath);
            AssertContains(feature, "CameraType cameraType = renderingData.cameraData.cameraType;", VisorUberPostFeaturePath, "visor uber post checks camera type before pass setup");
            AssertContains(feature, "cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView", VisorUberPostFeaturePath, "visor uber post skips non-game camera types before enqueue");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", VisorUberPostFeaturePath, "visor uber post rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", VisorUberPostFeaturePath, "visor uber post AddRenderPasses skips overlay cameras");
            AssertContains(noirFeature, "if (cameraData.renderType != CameraRenderType.Base)", VisorUberPostNoirFeaturePath, "unified noir pass rendergraph skips overlay cameras");
            AssertContains(feature, "private bool _depthlessTBDRPlatformClassified;", VisorUberPostFeaturePath, "visor uber post caches depthless TBDR platform classification");
            AssertContains(feature, "RefreshDepthlessTBDRPlatformCandidate();", VisorUberPostFeaturePath, "visor uber post refreshes depthless TBDR platform candidate on cold path");
            AssertContains(feature, "runtimeState = default;", VisorUberPostFeaturePath, "visor uber post runtime state uses allocation-proof field assignments");
            AssertTokenSequence(
                feature,
                VisorUberPostFeaturePath,
                "visor uber post injects camera history read access from UniversalCameraData",
                "UpdateRawColorHistoryRequest(",
                "renderCamera,",
                "renderingData.cameraData.historyManager,",
                "requestRawColorHistory);");
            AssertContains(feature, "crackTextureHandle = renderGraph.ImportTexture(crackHandle);", VisorUberPostFeaturePath, "visor uber post crack texture is imported into RenderGraph");
            AssertContains(feature, "lensDirtTextureHandle = renderGraph.ImportTexture(lensDirtHandle);", VisorUberPostFeaturePath, "visor uber post lens dirt texture is imported into RenderGraph");
            AssertContains(feature, "blueNoiseTextureHandle = renderGraph.ImportTexture(blueNoiseHandle);", VisorUberPostFeaturePath, "visor uber post blue noise texture is imported into RenderGraph");
            AssertContains(feature, "vrComfortMaskTextureHandle = renderGraph.ImportTexture(vrComfortMaskHandle);", VisorUberPostFeaturePath, "visor uber post comfort mask texture is imported into RenderGraph");
            AssertContains(feature, "builder.UseTexture(crackTextureHandle, AccessFlags.Read);", VisorUberPostFeaturePath, "visor uber post declares crack texture RenderGraph read");
            AssertContains(feature, "builder.UseTexture(lensDirtTextureHandle, AccessFlags.Read);", VisorUberPostFeaturePath, "visor uber post declares lens dirt texture RenderGraph read");
            AssertContains(feature, "builder.UseTexture(blueNoiseTextureHandle, AccessFlags.Read);", VisorUberPostFeaturePath, "visor uber post declares blue noise texture RenderGraph read");
            AssertContains(feature, "builder.UseTexture(vrComfortMaskTextureHandle, AccessFlags.Read);", VisorUberPostFeaturePath, "visor uber post declares comfort mask texture RenderGraph read");
            AssertContains(feature, "context.cmd.SetGlobalTexture(ShaderConstants.CrackTextureId, data.CrackTexture);", VisorUberPostFeaturePath, "visor uber post binds crack texture through command buffer");
            AssertNotContains(feature, "new RuntimeState(", VisorUberPostFeaturePath, "visor uber post hot runtime state must not use new token");
            AssertNotContains(feature, "private readonly struct RuntimeState", VisorUberPostFeaturePath, "visor uber post runtime state must remain assignable without constructor token");
            AssertNotContains(feature, "new Vector4(", VisorUberPostFeaturePath, "visor uber post hot vector payloads must use default field assignments");
            AssertNotContains(feature, "new Vector3(", VisorUberPostFeaturePath, "visor uber post hot waterline samples must use default field assignments");
            AssertNotContains(feature, "new TextureDesc(sourceDesc);", VisorUberPostFeaturePath, "visor uber post hot rendergraph descriptors must use struct copy without new token");
            AssertNotContains(noirFeature, "new TextureDesc(sourceDesc);", VisorUberPostNoirFeaturePath, "unified noir hot rendergraph descriptor must use struct copy without new token");
            AssertNotContains(feature, "BindStaticPostTextures();", VisorUberPostFeaturePath, "visor uber post must not hide post textures behind material state mutation");
            AssertNotContains(feature, ".SetTexture(ShaderConstants.CrackTextureId", VisorUberPostFeaturePath, "visor uber post crack texture must not use material hidden dependency");
            AssertNotContains(feature, ".SetTexture(ShaderConstants.LensDirtTextureId", VisorUberPostFeaturePath, "visor uber post lens dirt texture must not use material hidden dependency");
            AssertNotContains(feature, ".SetTexture(ShaderConstants.BlueNoiseTextureId", VisorUberPostFeaturePath, "visor uber post blue noise texture must not use material hidden dependency");
            AssertNotContains(feature, ".SetTexture(ShaderConstants.VrComfortMaskTextureId", VisorUberPostFeaturePath, "visor uber post comfort mask texture must not use material hidden dependency");
            AssertNotContains(feature, "renderCamera.TryGetComponent(out UniversalAdditionalCameraData", VisorUberPostFeaturePath, "visor uber post hot reconstruction route must not resolve camera data with TryGetComponent");
            AssertTokenNotBetween(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "ClearRawColorHistoryRequest();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must not clear game-camera raw history state");
            AssertTokenNotBetween(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "ClearPendingReconstructionInput();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must not clear game-camera reconstruction input");
            AssertTokenNotBetween(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "ClearRawColorHistoryRequest();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post overlay guard must not clear game-camera raw history state");
            AssertTokenNotBetween(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "ClearPendingReconstructionInput();", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post overlay guard must not clear game-camera reconstruction input");
            AssertTokenNotBetween(feature, "private bool ResolveDepthlessTBDRPath()", "SystemInfo.deviceModel", "private void RefreshDepthlessTBDRPlatformCandidate()", VisorUberPostFeaturePath, "visor uber post hot depthless path must not read device model");
            AssertTokenNotBetween(feature, "private bool ResolveDepthlessTBDRPath()", "IndexOf(", "private void RefreshDepthlessTBDRPlatformCandidate()", VisorUberPostFeaturePath, "visor uber post hot depthless path must not string scan");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "TextureHandle sourceTexture = resourceData.activeColorTexture;", VisorUberPostFeaturePath, "visor uber post rendergraph base-camera guard must precede texture use");
            AssertTokenAfter(noirFeature, "if (cameraData.renderType != CameraRenderType.Base)", "TextureHandle sourceTexture = resourceData.activeColorTexture;", VisorUberPostNoirFeaturePath, "unified noir rendergraph base-camera guard must precede texture use");
            AssertTokenAfter(feature, "if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post non-game guard must precede unified pass enqueue");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "if (settings.deepSeaNoirUnifiedPass)", VisorUberPostFeaturePath, "visor uber post overlay guard must precede unified pass enqueue");
            AssertTokenAfter(feature, "crackTextureHandle = renderGraph.ImportTexture(crackHandle);", "builder.UseTexture(crackTextureHandle, AccessFlags.Read);", VisorUberPostFeaturePath, "visor uber post imported crack texture must be declared before render func");
            AssertTokenAfter(feature, "Camera renderCamera = renderingData.cameraData.camera;", "_pass.Setup(", VisorUberPostFeaturePath, "visor uber post state build must precede pass setup");
        }

        private static void ValidateDeferredCausticsRenderFeature()
        {
            string feature = ReadRequiredText(DeferredCausticsFeaturePath);
            string runtime = ReadRequiredText(DeferredCausticsRuntimePath);
            AssertContains(feature, "if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))", DeferredCausticsFeaturePath, "deferred caustics checks camera type before pass setup");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", DeferredCausticsFeaturePath, "deferred caustics rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", DeferredCausticsFeaturePath, "deferred caustics AddRenderPasses skips overlay cameras");
            AssertContains(feature, "AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out _, out _)", DeferredCausticsFeaturePath, "deferred caustics requires active runtime constant buffer before enqueue");
            AssertContains(runtime, "!vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer)", DeferredCausticsRuntimePath, "deferred caustics runtime acquires owned write buffer");
            AssertTokenSequence(
                runtime,
                DeferredCausticsRuntimePath,
                "deferred caustics failed write-buffer validation releases through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, OwnerSystemId);");
            AssertTokenSequence(
                runtime,
                DeferredCausticsRuntimePath,
                "deferred caustics CSV dual lock acquires and releases only successful locks",
                "out NativeArray<byte> csvScratch)",
                "return false;",
                "scratchLocked = true;",
                "out NativeArray<CausticsLightingProfileDTO> profiles)",
                "return false;",
                "profilesLocked = true;",
                "finally",
                "if (profilesLocked)",
                "ReleaseVaultWriteBuffer(in _profilesHandle, BufferID.ShinobuCausticsProfiles);",
                "if (scratchLocked)",
                "ReleaseVaultWriteBuffer(in _csvScratchHandle, BufferID.ShinobuCausticsCsvScratch);");
            AssertTokenNotBetween(
                runtime,
                "out NativeArray<byte> csvScratch)",
                "ReleaseVaultWriteBuffer(in _csvScratchHandle, BufferID.ShinobuCausticsCsvScratch);",
                "scratchLocked = true;",
                DeferredCausticsRuntimePath,
                "deferred caustics CSV failed scratch acquire must not release an unacquired scratch lock");
            AssertNotContains(feature, "new TextureDesc(sourceDesc)", DeferredCausticsFeaturePath, "deferred caustics hot rendergraph descriptor must use struct copy without new token");
            AssertTokenAfter(feature, "if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))", "if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out _, out _))", DeferredCausticsFeaturePath, "deferred caustics camera guard must precede constant-buffer guard");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out _))", DeferredCausticsFeaturePath, "deferred caustics rendergraph base-camera guard must precede constant-buffer claim");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out _, out _))", DeferredCausticsFeaturePath, "deferred caustics AddRenderPasses base-camera guard must precede constant-buffer guard");
            AssertTokenAfter(feature, "if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out _, out _))", "_pass.Setup(settings, _material);", DeferredCausticsFeaturePath, "deferred caustics constant-buffer guard must precede setup");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material);", "renderer.EnqueuePass(_pass);", DeferredCausticsFeaturePath, "deferred caustics setup must precede enqueue");
        }

        private static void ValidateAtmosphereSootRenderFeature()
        {
            string feature = ReadRequiredText(AtmosphereSootFeaturePath);
            AssertContains(feature, "public bool HasPreparedResources()", AtmosphereSootFeaturePath, "atmosphere soot exposes prepared GPU resource check");
            AssertContains(feature, "if (renderingData.cameraData.cameraType != CameraType.Game)", AtmosphereSootFeaturePath, "atmosphere soot skips non-game cameras before setup");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", AtmosphereSootFeaturePath, "atmosphere soot rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", AtmosphereSootFeaturePath, "atmosphere soot AddRenderPasses skips overlay cameras");
            AssertContains(feature, "if (!_pass.HasPreparedResources())", AtmosphereSootFeaturePath, "atmosphere soot skips enqueue when GPU constants are unavailable");
            AssertContains(feature, "HomeostasisBrain.GlobalQualityWeight", AtmosphereSootFeaturePath, "atmosphere soot consumes continuous global quality scalar");
            AssertContains(feature, "ResolveSootQualityCurve01()", AtmosphereSootFeaturePath, "atmosphere soot applies continuous quality curve");
            AssertContains(feature, "math.lerp(0.68f, 1f, qualityCurve01)", AtmosphereSootFeaturePath, "atmosphere soot radius scales continuously");
            AssertNotContains(feature, "new Vector4(", AtmosphereSootFeaturePath, "atmosphere soot hot vector payloads must use default field assignments");
            AssertNotContains(feature, "new TextureDesc(sourceDesc)", AtmosphereSootFeaturePath, "atmosphere soot hot rendergraph descriptor must use struct copy without new token");
            AssertTokenAfter(feature, "if (cameraData.renderType != CameraRenderType.Base)", "TextureHandle sourceTexture = resourceData.activeColorTexture;", AtmosphereSootFeaturePath, "atmosphere soot rendergraph base-camera guard must precede texture use");
            AssertTokenAfter(feature, "if (renderingData.cameraData.cameraType != CameraType.Game)", "if (!_pass.HasPreparedResources())", AtmosphereSootFeaturePath, "atmosphere soot camera guard must precede resource guard");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "if (!_pass.HasPreparedResources())", AtmosphereSootFeaturePath, "atmosphere soot base-camera guard must precede resource guard");
            AssertTokenAfter(feature, "if (!_pass.HasPreparedResources())", "if (!TryBuildRuntimeState(renderCamera, settings, out RuntimeState runtimeState))", AtmosphereSootFeaturePath, "atmosphere soot resource guard must precede runtime state build");
            AssertTokenAfter(feature, "float qualityCurve01 = ResolveSootQualityCurve01();", "runtimeState.Intensity = intensity;", AtmosphereSootFeaturePath, "atmosphere soot quality scaling must precede state publish");
            AssertTokenAfter(feature, "if (!TryBuildRuntimeState(renderCamera, settings, out RuntimeState runtimeState))", "_pass.Setup(settings, _material, runtimeState);", AtmosphereSootFeaturePath, "atmosphere soot runtime state build must precede setup");
            AssertTokenAfter(feature, "_pass.Setup(settings, _material, runtimeState);", "renderer.EnqueuePass(_pass);", AtmosphereSootFeaturePath, "atmosphere soot setup must precede enqueue");
        }

        private static void ValidateAdditionalRenderingDataVaultFinally()
        {
            string noirBridge = ReadRequiredText(UberNoirRuntimeBridgePath);
            string bilateralDrs = ReadRequiredText(BilateralDrsRuntimePath);
            string volumetricFog = ReadRequiredText(VolumetricParticulateFogFeaturePath);
            string dynamicDecal = ReadRequiredText(DynamicDecalVaultRuntimePath);
            string diegeticVisorLens = ReadRequiredText(DiegeticVisorLensRuntimePath);
            AssertTokenSequence(
                noirBridge,
                UberNoirRuntimeBridgePath,
                "uber noir bridge failed telemetry validation releases through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);");
            AssertTokenSequence(
                bilateralDrs,
                BilateralDrsRuntimePath,
                "bilateral drs failed write-buffer validation releases through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, OwnerSystemId);");
            AssertTokenSequence(
                volumetricFog,
                VolumetricParticulateFogFeaturePath,
                "volumetric fog failed write-buffer validation releases through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, OwnerSystemId);");
            AssertTokenSequence(
                dynamicDecal,
                DynamicDecalVaultRuntimePath,
                "dynamic decal failed validation releases only after successful acquire",
                "!_vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))",
                "return false;",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "ReleaseDynamicDecalVaultBuffer(in handle, bufferId);");
            AssertTokenSequence(
                diegeticVisorLens,
                DiegeticVisorLensRuntimePath,
                "diegetic visor lens failed write-buffer validation releases through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in handle, OwnerSystem);");
        }

        private static void ValidateDeferredDecalPassRenderGraphDependency()
        {
            string feature = ReadRequiredText(DeferredDecalPassPath);
            AssertNotContains(feature, "_material.SetTexture(ShaderConstants.DecalAtlasId", DeferredDecalPassPath, "deferred decal atlas must not be hidden material state");
            AssertContains(feature, "private RTHandle _decalAtlasHandle;", DeferredDecalPassPath, "deferred decal caches atlas RTHandle outside render func");
            AssertContains(feature, "_pass.PrepareDecalAtlasHandleCold(settings);", DeferredDecalPassPath, "deferred decal prepares atlas wrapper on cold create path");
            AssertContains(feature, "TextureHandle decalAtlasTexture = TextureHandle.nullHandle;", DeferredDecalPassPath, "deferred decal carries nullable atlas handle through RenderGraph");
            AssertContains(feature, "decalAtlasTexture = renderGraph.ImportTexture(decalAtlasHandle);", DeferredDecalPassPath, "deferred decal imports atlas texture into RenderGraph");
            AssertContains(feature, "builder.UseTexture(decalAtlasTexture, AccessFlags.Read);", DeferredDecalPassPath, "deferred decal declares atlas read before render func");
            AssertContains(feature, "context.cmd.SetGlobalTexture(ShaderConstants.DecalAtlasId, data.DecalAtlas);", DeferredDecalPassPath, "deferred decal binds atlas through command buffer");
            AssertContains(feature, "ReleaseDecalAtlasHandle();", DeferredDecalPassPath, "deferred decal releases cached atlas wrapper");
            AssertTokenNotBetween(
                feature,
                "public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)",
                "RTHandles.Alloc(",
                "using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(",
                DeferredDecalPassPath,
                "deferred decal must not allocate atlas RTHandle inside RecordRenderGraph");
            AssertTokenAfter(
                feature,
                "decalAtlasTexture = renderGraph.ImportTexture(decalAtlasHandle);",
                "builder.UseTexture(decalAtlasTexture, AccessFlags.Read);",
                DeferredDecalPassPath,
                "deferred decal imported atlas must be declared before render func");
        }

        private static void ValidateFoveatedRenderCommanderDataVault()
        {
            string commander = ReadRequiredText(FoveatedRenderCommanderPath);
            AssertContains(commander, "BufferID.FoveatedRenderBlackBox", FoveatedRenderCommanderPath, "foveated telemetry buffer id");
            AssertContains(commander, "!vault.TryAcquireWriteLock(in _telemetryHandle, SystemID.GraphicsScalability, out telemetry)", FoveatedRenderCommanderPath, "foveated telemetry write lock acquire");
            AssertContains(commander, "RefreshQuestRuntimeClass(", FoveatedRenderCommanderPath, "Quest runtime class refresh uses cached state");
            AssertContains(commander, "out bool questFamilyRuntime", FoveatedRenderCommanderPath, "Quest family runtime cached result");
            AssertContains(commander, "s_questFamilyClassRuntime = questFamilyDevice || quest3OrPro || quest2Token;", FoveatedRenderCommanderPath, "Quest family class cached after string token scan");
            AssertContains(commander, "if (questFamilyDevice)", FoveatedRenderCommanderPath, "generic Quest family token completes classification");
            AssertNotContains(commander, "Application.platform == RuntimePlatform.Android && IsQuestFamilyDevice()", FoveatedRenderCommanderPath, "ApplyPolicy must not string-scan Quest family every policy commit");
            AssertNotContains(commander, "bool quest2Runtime = IsQuest2Runtime", FoveatedRenderCommanderPath, "ApplyPolicy must use one cached Quest runtime refresh route");
            AssertTokenAfter(commander, "if (_targetMode != FoveatedRenderMode.GazeTracked)", "Camera renderCamera = GlobalRenderContext.CurrentCamera;", FoveatedRenderCommanderPath, "fixed Quest foveation must not be toggled across UI camera stacks");
            AssertTokenSequence(
                commander,
                FoveatedRenderCommanderPath,
                "fixed foveation UI suppression state clears without hardware apply",
                "if (_targetMode != FoveatedRenderMode.GazeTracked)",
                "if (_uiSuppressionActive)",
                "_uiSuppressionActive = false;",
                "_lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);",
                "return;",
                "Camera renderCamera = GlobalRenderContext.CurrentCamera;");
            AssertTokenSequence(
                commander,
                FoveatedRenderCommanderPath,
                "failed telemetry validation releases acquired write lock through finally",
                "bool releaseOnExit = true;",
                "try",
                "releaseOnExit = false;",
                "return true;",
                "finally",
                "if (releaseOnExit)",
                "vault.ReleaseWriteLock(in _telemetryHandle, SystemID.GraphicsScalability);");
            AssertTokenSequence(
                commander,
                FoveatedRenderCommanderPath,
                "transferred telemetry write lock releases in WriteTelemetry finally",
                "if (!TryAcquireTelemetryWriteBuffer(out NativeArray<FoveatedRenderTelemetryEntry> telemetry))",
                "try",
                "telemetry[_telemetryCursor] = entry;",
                "finally",
                "ReleaseTelemetryWriteBuffer();");
        }

        private static void ValidateRuntimeTextureDescCopyTokens()
        {
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/Visor", "new TextureDesc(sourceDesc)", "visor rendergraph descriptors must use struct copy without new token");
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/Rendering", "new TextureDesc(sourceDesc)", "rendering rendergraph descriptors must use struct copy without new token");
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/UI", "new TextureDesc(sourceDesc)", "UI rendergraph descriptors must use struct copy without new token");
            AssertNoRuntimeSourceRegex("Assets/_Project/Scripts/Visor", @"new TextureDesc\([A-Za-z_][A-Za-z0-9_]*Desc\)", "visor rendergraph descriptor copy constructors must use struct assignment");
            AssertNoRuntimeSourceRegex("Assets/_Project/Scripts/Rendering", @"new TextureDesc\([A-Za-z_][A-Za-z0-9_]*Desc\)", "rendering rendergraph descriptor copy constructors must use struct assignment");
            AssertNoRuntimeSourceRegex("Assets/_Project/Scripts/UI", @"new TextureDesc\([A-Za-z_][A-Za-z0-9_]*Desc\)", "UI rendergraph descriptor copy constructors must use struct assignment");
        }

        private static void ValidateRuntimeRenderGraphStaticCallbacks()
        {
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/Visor", "SetRenderFunc((", "visor RenderGraph callbacks must be static to forbid hidden captures");
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/Rendering", "SetRenderFunc((", "rendering RenderGraph callbacks must be static to forbid hidden captures");
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/UI", "SetRenderFunc((", "UI RenderGraph callbacks must be static to forbid hidden captures");
            AssertNoRuntimeSourceToken("Assets/_Project/Scripts/Graphics/VR", "SetRenderFunc((", "VR RenderGraph callbacks must be static to forbid hidden captures");
        }

        private static void ValidateOceanWakeRenderGraphZeroGcTokens()
        {
            string feature = ReadRequiredText(SinglePassOceanFeaturePath);
            AssertTokenNotBetween(
                feature,
                "builder.SetRenderFunc(static (WakePassData data, ComputeGraphContext context) =>",
                "new Vector4(",
                "});",
                SinglePassOceanFeaturePath,
                "ocean wake rendergraph callback must use default Vector4 fields, not constructor tokens");
        }

        private static void ValidateDryVolumeRenderGraphTextureDependency()
        {
            string feature = ReadRequiredText(DryVolumeFeaturePath);
            AssertNotContains(feature, "_restoreMaterial.SetTexture(ShaderConstants.OceanCameraColorTextureId", DryVolumeFeaturePath, "dry volume restore must not hide ocean camera color as material state");
            AssertNotContains(feature, "IOceanVisualBridge bridge = OceanVisualBridgeRegistry.Active;", DryVolumeFeaturePath, "dry volume must not depend on Crest/global camera-color polling");
            AssertNotContains(feature, "RTHandles.Alloc(oceanCameraColorTexture);", DryVolumeFeaturePath, "dry volume must not allocate texture wrappers from LateFrameTick");
            AssertNotContains(feature, "public void LateFrameTick()", DryVolumeFeaturePath, "dry volume pre-underwater copy must replace late-frame global texture caching");
            AssertNotContains(feature, "GlobalRegistry.TryRegisterLateFrameTickable", DryVolumeFeaturePath, "dry volume render feature must not register a presentation tick just to cache textures");
            AssertContains(feature, "private sealed class PreUnderwaterColorCopyPass", DryVolumeFeaturePath, "dry volume owns a first-party pre-underwater color copy pass");
            AssertContains(feature, "(RenderPassEvent)((int)settings.injectionPoint - 1)", DryVolumeFeaturePath, "dry volume pre-copy runs before the underwater pass");
            AssertContains(feature, "(RenderPassEvent)((int)settings.injectionPoint + 1)", DryVolumeFeaturePath, "dry volume restore runs after the underwater pass");
            AssertContains(feature, "cameraData.cameraType == CameraType.SceneView", DryVolumeFeaturePath, "dry volume rendergraph skips editor scene-view cameras");
            AssertContains(feature, "cameraData.renderType != CameraRenderType.Base", DryVolumeFeaturePath, "dry volume rendergraph skips overlay cameras");
            AssertContains(feature, "cameraType == CameraType.SceneView", DryVolumeFeaturePath, "dry volume AddRenderPasses skips editor scene-view cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", DryVolumeFeaturePath, "dry volume AddRenderPasses skips overlay cameras");
            AssertContains(feature, "bool hasDryVolumes = HectonDryVolumeStencilSource.ActiveSourceCount > 0;", DryVolumeFeaturePath, "dry volume only allocates pre-underwater copy target when dry volumes exist");
            AssertContains(feature, "_preUnderwaterCopyPass.EnsureTarget(renderingData.cameraData.cameraTargetDescriptor);", DryVolumeFeaturePath, "dry volume allocates/resizes its copy target before restore setup");
            AssertTokenAfter(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", "_preUnderwaterCopyPass.EnsureTarget(renderingData.cameraData.cameraTargetDescriptor);", DryVolumeFeaturePath, "dry volume overlay guard must precede copy-target allocation");
            AssertContains(feature, "RenderingUtils.ReAllocateHandleIfNeeded(", DryVolumeFeaturePath, "dry volume pre-copy uses pooled RTHandle resize policy");
            AssertContains(feature, "TextureHandle destinationTexture = renderGraph.ImportTexture(_preUnderwaterColorTexture);", DryVolumeFeaturePath, "dry volume imports pre-underwater copy target into RenderGraph");
            AssertContains(feature, "TextureHandle oceanCameraColorTexture = renderGraph.ImportTexture(_preUnderwaterColorTexture);", DryVolumeFeaturePath, "dry volume imports pre-underwater color into restore RenderGraph");
            AssertContains(feature, "builder.UseTexture(oceanCameraColorTexture, AccessFlags.Read);", DryVolumeFeaturePath, "dry volume declares ocean camera color read");
            AssertContains(feature, "context.cmd.SetGlobalTexture(ShaderConstants.OceanCameraColorTextureId, data.oceanCameraColor);", DryVolumeFeaturePath, "dry volume binds ocean camera color through render command buffer");
            AssertContains(feature, "_preUnderwaterCopyPass?.Release();", DryVolumeFeaturePath, "dry volume releases owned pre-underwater copy target");
        }

        private static void ValidateVoxelSsaoContinuousQuality()
        {
            string feature = ReadRequiredText(VoxelSsaoFeaturePath);
            AssertNotContains(feature, "float renderScale = math.clamp(_settings.renderScale, 0.25f, 1f);", VoxelSsaoFeaturePath, "voxel SSAO render scale must not stay authored-fixed");
            AssertContains(feature, "float globalQualityWeight01 = ResolveGlobalQualityWeight01();", VoxelSsaoFeaturePath, "voxel SSAO reads continuous global quality");
            AssertContains(feature, "float renderScale = ResolveRenderScale(_settings, qualityCurve01);", VoxelSsaoFeaturePath, "voxel SSAO scales render size continuously");
            AssertContains(feature, "passData.paramsA = BuildParamsA(_settings, projectionScale, qualityCurve01);", VoxelSsaoFeaturePath, "voxel SSAO scales visual payload continuously");
            AssertContains(feature, "result.z = math.max(0f, settings.intensity * math.lerp(0.42f, 1f, quality));", VoxelSsaoFeaturePath, "voxel SSAO intensity has survival-to-overkill curve");
            AssertContains(feature, "cameraData.cameraType == CameraType.SceneView", VoxelSsaoFeaturePath, "voxel SSAO skips editor scene-view cameras");
            AssertContains(feature, "if (cameraData.renderType != CameraRenderType.Base)", VoxelSsaoFeaturePath, "voxel SSAO rendergraph skips overlay cameras");
            AssertContains(feature, "if (renderingData.cameraData.renderType != CameraRenderType.Base)", VoxelSsaoFeaturePath, "voxel SSAO AddRenderPasses skips overlay cameras");
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

        private static void AssertNoRuntimeSourceToken(string root, string token, string context)
        {
            if (!Directory.Exists(root))
                throw new FatalArchitectureException("Required source root missing: " + root);

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string normalizedPath = files[index].Replace('\\', '/');
                if (normalizedPath.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;

                string text = File.ReadAllText(files[index]);
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                    throw new FatalArchitectureException("Validation failed in " + normalizedPath + " for " + context + ": token " + token);
            }
        }

        private static void AssertNoRuntimeSourceRegex(string root, string pattern, string context)
        {
            if (!Directory.Exists(root))
                throw new FatalArchitectureException("Required source root missing: " + root);

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string normalizedPath = files[index].Replace('\\', '/');
                if (normalizedPath.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;

                string text = File.ReadAllText(files[index]);
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
                    throw new FatalArchitectureException("Validation failed in " + normalizedPath + " for " + context + ": pattern " + pattern);
            }
        }

        private static void AssertTokenSequence(string text, string path, string context, params string[] tokens)
        {
            int search = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], search, StringComparison.Ordinal);
                if (found < 0)
                    throw new FatalArchitectureException("Validation failed in " + path + " for " + context + ": missing sequence token " + tokens[i]);

                search = found + tokens[i].Length;
            }
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
