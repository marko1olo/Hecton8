using System;
using System.IO;
using System.Text;
using Hecton8.Rendering.WaterOptics;
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
        private const string GlobalWaterOpticsCBufferToken = "CBUFFER_START(_GlobalWaterOptics)";
        private const string GlobalWaterOpticsCBufferEndToken = "CBUFFER_END";
        private const string AbsorptionLaneToken = "float4 _H8WaterOpticsAbsorptionCoefficientsRGB";
        private const string ScatteringLaneToken = "float4 _H8WaterOpticsScatteringCoefficientsRGB";
        private const string DirectionalLightLaneToken = "float4 _H8WaterOpticsDirectionalLightColorAndIntensity";
        private const string QualityDepthLaneToken = "float4 _H8WaterOpticsQualityAndDepthLimits";

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

            var builder = new StringBuilder(512);
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
            report = builder.ToString();
            return layoutOk && dumpHeaderOk && waterIncludeOk && uberNoirOk && fogOk && dearLieOk;
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
    }
}
