using System;
using System.IO;
using System.Text;
using Hecton8.Rendering.WaterOptics;
using Hecton8.Visor;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    public static class WaterOpticsLayoutValidator
    {
        private const string UberNoirIncludePath = "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl";
        private const string UberNoirShaderPath = "Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader";
        private const string WaterIncludePath = "Assets/_Project/Art/Shaders/Hecton_WaterExtinction.hlsl";
        private const string FogComputePath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog.compute";
        private const string DearLiePath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog_DearLie.shader";
        private const string TraumaShaderPath = "Assets/_Project/Art/Shaders/Hecton_VisorTrauma.shader";
        private const string DeferredDecalShaderPath = "Assets/_Project/Art/Shaders/Hecton_DeferredDecal.shader";
        private const string VisorUberPostShaderPath = "Assets/_Project/Art/Shaders/HectonVisorUberPost.shader";
        private const string GlobalWaterOpticsCBufferToken = "CBUFFER_START(_GlobalWaterOptics)";
        private const string GlobalWaterOpticsCBufferEndToken = "CBUFFER_END";
        private const string AbsorptionLaneToken = "float4 _H8WaterOpticsAbsorptionCoefficientsRGB";
        private const string ScatteringLaneToken = "float4 _H8WaterOpticsScatteringCoefficientsRGB";
        private const string DirectionalLightLaneToken = "float4 _H8WaterOpticsDirectionalLightColorAndIntensity";
        private const string QualityDepthLaneToken = "float4 _H8WaterOpticsQualityAndDepthLimits";
        private const string ForbiddenNewVolumeProfileToken = "new Volume" + "Profile(";
        private const string ForbiddenGraphicsBlitToken = "Graphics." + "Blit(";
        private const string ForbiddenCommandBufferToken = "CommandBufferPool." + "Get(";
        private const string ForbiddenGlobalRegistryGetToken = "GlobalRegistry." + "Get<";
        private const string ForbiddenForceCompleteToken = "force" + "Complete: true";
        private const string ForbiddenBlockingCompletionToken = "Wait" + "ForCompletion";
        private const string ForbiddenDispatcherFenceToken = "Dispatcher" + "JobFence";

        private static readonly string[] TargetSourcePaths =
        {
            "Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs",
            "Assets/_Project/Scripts/Rendering/WaterOptics/HectonWaterOpticsTelemetryFeature.cs",
            "Assets/_Project/Scripts/Rendering/WaterOptics/Editor/PostProcess_Fog_Scanner.cs",
            "Assets/_Project/Scripts/Rendering/WaterOptics/Editor/WaterOpticsLayoutValidator.cs",
            "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs",
            "Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs",
            "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs",
            "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs",
            "Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs",
            "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs",
            "Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs",
            "Assets/_Project/Scripts/Visor/DeferredDecalPass.cs",
            "Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs",
            "Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs"
        };

        private static readonly string[] RendererAssetPaths =
        {
            "Assets/_Project/Data/PC_Renderer.asset",
            "Assets/_Project/Data/PC_High_Renderer.asset",
            "Assets/_Project/Data/Mobile_Renderer.asset",
            "Assets/_Project/Data/Quest_VR_Renderer.asset"
        };

        private const string VisorUberPostFeatureClassName = "Hecton8.Visor.HectonVisorUberPostFeature";

        private static readonly string[] LegacyStandalonePostFeatureClassNames =
        {
            "Hecton8.Visor.HectonRetinaDistortionFeature",
            "Hecton8.Visor.HectonVisorFluidDistortionFeature"
        };

        private static readonly string[] OpticalStackFeatureClassNames =
        {
            "Hecton8.Visor.HectonVolumetricParticulateFogFeature",
            "Hecton8.Visor.HectonNoirDepthFogFeature",
            VisorUberPostFeatureClassName,
            "Hecton8.Visor.DeferredDecalPass",
            "Hecton8.Visor.HectonHalfResParticlesFeature",
            "Hecton8.Rendering.HectonDeferredCausticsFeature"
        };

        [MenuItem("Hecton8/Rendering/Water Optics/Validate Layout")]
        public static void ValidateMenu()
        {
            bool ok = ValidateWaterOpticsLayout(out string report);
            if (ok)
            {
                Debug.Log(report);
                return;
            }

            Debug.LogError(report);
        }

        public static bool ValidateWaterOpticsLayout(out string report)
        {
            bool layoutOk = WaterOpticsRuntime.ValidateLayouts(
                out int opticsSize,
                out int profileSize,
                out int tuningSize,
                out int telemetrySize);
            bool dumpHeaderOk = WaterOpticsRuntime.ValidateDumpHeaderLayout(out int dumpHeaderSize);

            bool waterIncludeAbiOk = HasGlobalWaterOpticsCBufferLayout(WaterIncludePath);
            bool fogAbiOk = HasGlobalWaterOpticsCBufferLayout(FogComputePath);
            bool dearLieAbiOk = HasGlobalWaterOpticsCBufferLayout(DearLiePath);

            bool waterIncludeOk = waterIncludeAbiOk &&
                                  FileContains(WaterIncludePath, "H8WaterOpticsApplyBeerLambert") &&
                                  FileContains(WaterIncludePath, "H8WaterOpticsTransmittanceCompressed");
            bool uberNoirIncludeOk = FileContains(UberNoirIncludePath, "H8WaterOpticsApplyBeerLambert");
            bool uberNoirShaderOk = FileContains(UberNoirShaderPath, "Shader \"Hecton8/Rendering/UberNoir\"") &&
                                    FileContains(UberNoirShaderPath, "#include \"Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl\"");
            bool uberNoirOk = uberNoirIncludeOk && uberNoirShaderOk;
            bool fogOk = fogAbiOk &&
                         FileContains(FogComputePath, "ApplyWaterOpticsToScattering");
            bool dearLieOk = FileContains(DearLiePath, "WaterOpticsDearLieTint") &&
                             FileContains(DearLiePath, "WaterOpticsWaterlineWeight") &&
                             dearLieAbiOk;
            bool traumaLayoutOk = DynamicDecalVaultRuntime.ValidateTraumaDecalLayout();
            bool traumaCapacityOk = DynamicDecalVaultRuntime.MaxCapacity == 128 &&
                                    DynamicDecalVaultRuntime.LowCapacity >= 8 &&
                                    DynamicDecalVaultRuntime.LowCapacity <= DynamicDecalVaultRuntime.MaxCapacity;
            bool noirLayoutOk = HectonVisorUberPostFeature.ValidateNoirPostProcessLayoutForEditor();
            bool noForbiddenOwnerTokens = ValidateForbiddenOwnerTokens(
                out int newVolumeProfileCount,
                out int graphicsBlitCount,
                out int commandBufferCount,
                out int globalRegistryGetCount,
                out int forceCompleteCount,
                out int blockingCompletionCount,
                out int dispatcherFenceCount);
            bool traumaShaderOk = HasBoundedTraumaShaderRoute(TraumaShaderPath);
            bool deferredDecalShaderOk = HasBoundedTraumaShaderRoute(DeferredDecalShaderPath);
            bool visorUberWaterlineOk = FileContains(VisorUberPostShaderPath, "_InternalWaterlineY") &&
                                        FileContains(VisorUberPostShaderPath, "ResolveInternalWaterMask") &&
                                        FileContainsAny(VisorUberPostShaderPath, "clamp(", "saturate(");
            int activeVisorUberPostSlots = CountActiveRendererFeatureSlots(VisorUberPostFeatureClassName);
            int activeLegacyStandalonePostSlots = CountActiveRendererFeatureSlots(LegacyStandalonePostFeatureClassNames);
            int activeOpticalStackSlots = CountActiveRendererFeatureSlots(OpticalStackFeatureClassNames);
            bool rendererTopologyOk = activeVisorUberPostSlots == RendererAssetPaths.Length &&
                                      activeLegacyStandalonePostSlots == 0;

            var builder = new StringBuilder(768);
            builder.AppendLine("Water Optics Layout Validation");
            builder.Append("WaterOpticsDTO=").Append(opticsSize).AppendLine(" bytes; expected 64, offsets 0/16/32/48.");
            builder.Append("WaterOpticsProfileDTO=").Append(profileSize).AppendLine(" bytes; expected 64.");
            builder.Append("WaterOpticsTuningDTO=").Append(tuningSize).AppendLine(" bytes; expected 64.");
            builder.Append("WaterOpticsTelemetryEntry=").Append(telemetrySize).AppendLine(" bytes; expected 64.");
            builder.Append("WaterOpticsDumpHeader=").Append(dumpHeaderSize).AppendLine(" bytes; expected 32.");
            builder.Append("Shader ABI include order=").AppendLine(waterIncludeAbiOk ? "PASS" : "FAIL");
            builder.Append("Shader ABI fog order=").AppendLine(fogAbiOk ? "PASS" : "FAIL");
            builder.Append("Shader ABI Dear Lie order=").AppendLine(dearLieAbiOk ? "PASS" : "FAIL");
            builder.Append("Shader include route=").AppendLine(waterIncludeOk ? "PASS" : "FAIL");
            builder.Append("UberNoir include graft=").AppendLine(uberNoirIncludeOk ? "PASS" : "FAIL");
            builder.Append("UberNoir shader route=").AppendLine(uberNoirShaderOk ? "PASS" : "FAIL");
            builder.Append("Volumetric fog graft=").AppendLine(fogOk ? "PASS" : "FAIL");
            builder.Append("Dear Lie waterline fake=").AppendLine(dearLieOk ? "PASS" : "FAIL");
            builder.Append("Trauma decal DTO layout=").AppendLine(traumaLayoutOk ? "PASS" : "FAIL");
            builder.Append("Trauma decal capacity lanes=").Append(traumaCapacityOk ? "PASS" : "FAIL")
                .Append("; low=").Append(DynamicDecalVaultRuntime.LowCapacity)
                .Append(", max=").Append(DynamicDecalVaultRuntime.MaxCapacity)
                .AppendLine(", shader bound=128.");
            builder.Append("Noir post DTO layout=").AppendLine(noirLayoutOk ? "PASS" : "FAIL");
            builder.Append("Forbidden owner tokens=").Append(noForbiddenOwnerTokens ? "PASS" : "FAIL")
                .Append("; newVolumeProfile=").Append(newVolumeProfileCount)
                .Append(", graphicsBlit=").Append(graphicsBlitCount)
                .Append(", commandBufferPoolGet=").Append(commandBufferCount)
                .Append(", globalRegistryGetGeneric=").Append(globalRegistryGetCount)
                .Append(", forceComplete=").Append(forceCompleteCount)
                .Append(", waitForCompletion=").Append(blockingCompletionCount)
                .Append(", dispatcherFence=").Append(dispatcherFenceCount)
                .AppendLine(".");
            builder.Append("Trauma shader global buffer/bounded loop/clamp=").AppendLine(traumaShaderOk ? "PASS" : "FAIL");
            builder.Append("Deferred decal trauma shader global buffer/bounded loop/clamp=").AppendLine(deferredDecalShaderOk ? "PASS" : "FAIL");
            builder.Append("Visor uber waterline clamp route=").AppendLine(visorUberWaterlineOk ? "PASS" : "FAIL");
            builder.Append("Renderer post owner topology=").Append(rendererTopologyOk ? "PASS" : "FAIL")
                .Append("; activeUberPost=").Append(activeVisorUberPostSlots)
                .Append(", activeLegacyStandalonePost=").Append(activeLegacyStandalonePostSlots)
                .Append(", opticalStackSlots=").Append(activeOpticalStackSlots)
                .AppendLine(".");
            report = builder.ToString();
            return layoutOk &&
                   dumpHeaderOk &&
                   waterIncludeOk &&
                   uberNoirOk &&
                   fogOk &&
                   dearLieOk &&
                   traumaLayoutOk &&
                   traumaCapacityOk &&
                   noirLayoutOk &&
                   noForbiddenOwnerTokens &&
                   traumaShaderOk &&
                   deferredDecalShaderOk &&
                   visorUberWaterlineOk &&
                   rendererTopologyOk;
        }

        private static bool HasGlobalWaterOpticsCBufferLayout(string path)
        {
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            int cbufferStart = text.IndexOf(GlobalWaterOpticsCBufferToken, StringComparison.Ordinal);
            if (cbufferStart < 0)
                return false;

            int cbufferEnd = text.IndexOf(GlobalWaterOpticsCBufferEndToken, cbufferStart, StringComparison.Ordinal);
            if (cbufferEnd <= cbufferStart)
                return false;

            int absorptionIndex = text.IndexOf(AbsorptionLaneToken, cbufferStart, StringComparison.Ordinal);
            int scatteringIndex = text.IndexOf(ScatteringLaneToken, cbufferStart, StringComparison.Ordinal);
            int directionalLightIndex = text.IndexOf(DirectionalLightLaneToken, cbufferStart, StringComparison.Ordinal);
            int qualityDepthIndex = text.IndexOf(QualityDepthLaneToken, cbufferStart, StringComparison.Ordinal);
            return absorptionIndex > cbufferStart &&
                   scatteringIndex > absorptionIndex &&
                   directionalLightIndex > scatteringIndex &&
                   qualityDepthIndex > directionalLightIndex &&
                   qualityDepthIndex < cbufferEnd;
        }

        private static bool FileContains(string path, string token)
        {
            if (!File.Exists(path))
                return false;

            foreach (string line in File.ReadLines(path))
            {
                if (line.Contains(token))
                    return true;
            }

            return false;
        }

        private static bool FileContainsAny(string path, string firstToken, string secondToken)
        {
            if (!File.Exists(path))
                return false;

            foreach (string line in File.ReadLines(path))
            {
                if (line.IndexOf(firstToken, StringComparison.Ordinal) >= 0 ||
                    line.IndexOf(secondToken, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBoundedTraumaShaderRoute(string path)
        {
            return FileContains(path, "StructuredBuffer<TraumaDecalData> _GlobalVisorTrauma") &&
                   FileContains(path, "for (int traumaIndex = 0; traumaIndex < 128; traumaIndex++)") &&
                   FileContains(path, "clamp(input.screenUV + refractOffset");
        }

        private static bool ValidateForbiddenOwnerTokens(
            out int newVolumeProfileCount,
            out int graphicsBlitCount,
            out int commandBufferCount,
            out int globalRegistryGetCount,
            out int forceCompleteCount,
            out int blockingCompletionCount,
            out int dispatcherFenceCount)
        {
            newVolumeProfileCount = 0;
            graphicsBlitCount = 0;
            commandBufferCount = 0;
            globalRegistryGetCount = 0;
            forceCompleteCount = 0;
            blockingCompletionCount = 0;
            dispatcherFenceCount = 0;

            for (int i = 0; i < TargetSourcePaths.Length; i++)
            {
                string path = TargetSourcePaths[i];
                if (!File.Exists(path))
                    continue;

                foreach (string line in File.ReadLines(path))
                {
                    newVolumeProfileCount += CountToken(line, ForbiddenNewVolumeProfileToken);
                    graphicsBlitCount += CountToken(line, ForbiddenGraphicsBlitToken);
                    commandBufferCount += CountToken(line, ForbiddenCommandBufferToken);
                    globalRegistryGetCount += CountToken(line, ForbiddenGlobalRegistryGetToken);
                    forceCompleteCount += CountToken(line, ForbiddenForceCompleteToken);
                    blockingCompletionCount += CountToken(line, ForbiddenBlockingCompletionToken);
                    dispatcherFenceCount += CountToken(line, ForbiddenDispatcherFenceToken);
                }
            }

            return newVolumeProfileCount == 0 &&
                   graphicsBlitCount == 0 &&
                   commandBufferCount == 0 &&
                   globalRegistryGetCount == 0 &&
                   forceCompleteCount == 0 &&
                   blockingCompletionCount == 0 &&
                   dispatcherFenceCount == 0;
        }

        private static int CountActiveRendererFeatureSlots(string featureClassName)
        {
            int total = 0;
            for (int i = 0; i < RendererAssetPaths.Length; i++)
                total += CountActiveRendererFeatureSlots(RendererAssetPaths[i], featureClassName);
            return total;
        }

        private static int CountActiveRendererFeatureSlots(string[] featureClassNames)
        {
            int total = 0;
            for (int i = 0; i < RendererAssetPaths.Length; i++)
            {
                for (int featureIndex = 0; featureIndex < featureClassNames.Length; featureIndex++)
                    total += CountActiveRendererFeatureSlots(RendererAssetPaths[i], featureClassNames[featureIndex]);
            }

            return total;
        }

        private static int CountActiveRendererFeatureSlots(string rendererAssetPath, string featureClassName)
        {
            if (!File.Exists(rendererAssetPath))
                return 0;

            string text = File.ReadAllText(rendererAssetPath);
            return FeatureActiveInAsset(text, featureClassName) ? 1 : 0;
        }

        private static bool FeatureActiveInAsset(string text, string className)
        {
            string token = "m_EditorClassIdentifier: Hecton8.Core::" + className;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                int searchLength = Math.Min(640, text.Length - found);
                if (text.IndexOf("m_Active: 1", found, searchLength, StringComparison.Ordinal) >= 0)
                    return true;

                index = found + token.Length;
            }

            return false;
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + token.Length;
            }

            return count;
        }
    }
}
