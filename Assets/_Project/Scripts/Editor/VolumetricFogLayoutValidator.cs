#if UNITY_EDITOR
using Hecton8.VFX;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class VolumetricFogLayoutValidator
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog.compute";
        private const string DearLieShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog_DearLie.shader";
        private const string GridBuildKernelName = "BuildVolumetricFogGrid";
        private const string RaymarchKernelName = "RaymarchVolumetricFog";
        private const string RaymarchXrKernelName = "RaymarchVolumetricFogXR";
        private const string ShaderPragmaPrefix = "#pragma ";
        private const string MultiCompileToken = "multi_compile";
        private const string ShaderFeatureToken = "shader_feature";
        private const string DisableTexture2DArrayToken = "DISABLE_TEXTURE2D_X_ARRAY";
        private const string DearLieProxyPassName = "DearLieProxy";
        private const string BilateralCompositePassName = "BilateralComposite";
        private const string FragmentPragmaPrefix = "#pragma fragment ";
        private const string KernelPragmaPrefix = "#pragma kernel";

        [MenuItem("Hecton8/VFX/Validate Volumetric Fog Layout")]
        public static void ValidateMenu()
        {
            bool layoutValid = ValidateFogConstantsLayout();
            bool shaderValid = ValidateComputeShaderKernels();
            bool dearLieShaderValid = ValidateDearLieShader();
            Debug.Log(layoutValid && shaderValid && dearLieShaderValid
                ? "SHINOBU_233 volumetric fog contract valid: params 64B, lights 32B, telemetry 64B, extinction profiles 64B, grid/raymarch compute kernels present, Dear Lie raster shader present."
                : "SHINOBU_233 volumetric fog contract invalid.");
        }

        public static bool ValidateFogConstantsLayout()
        {
            return VolumetricFogNativeLayout.Validate() &&
                   UnsafeUtility.SizeOf<FogConstantsDTO>() == VolumetricFogConstants.ParamsStrideBytes &&
                   OffsetOf<FogConstantsDTO>(nameof(FogConstantsDTO.FogColorAndDensity)) == 0 &&
                   OffsetOf<FogConstantsDTO>(nameof(FogConstantsDTO.ScatteringParams)) == 16 &&
                   OffsetOf<FogConstantsDTO>(nameof(FogConstantsDTO.FlowAdvection)) == 32 &&
                   OffsetOf<FogConstantsDTO>(nameof(FogConstantsDTO.QualityAndLimits)) == 48 &&
                   UnsafeUtility.SizeOf<PointLightDTO>() == VolumetricFogConstants.PointLightStrideBytes &&
                   OffsetOf<PointLightDTO>(nameof(PointLightDTO.PositionRadius)) == 0 &&
                   OffsetOf<PointLightDTO>(nameof(PointLightDTO.ColorIntensity)) == 16 &&
                   UnsafeUtility.SizeOf<VolumetricFogTelemetryEntry>() == VolumetricFogConstants.TelemetryEntryStrideBytes &&
                   OffsetOf<VolumetricFogTelemetryEntry>(nameof(VolumetricFogTelemetryEntry.DebugValues)) == 48 &&
                   UnsafeUtility.SizeOf<WaterExtinctionProfileDTO>() == VolumetricFogConstants.ExtinctionProfileStrideBytes &&
                   OffsetOf<WaterExtinctionProfileDTO>(nameof(WaterExtinctionProfileDTO.Reserved)) == 48;
        }

        public static bool ValidateComputeShaderKernels()
        {
            ComputeShader computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
            if (computeShader == null)
                return false;

            try
            {
                return computeShader.HasKernel(GridBuildKernelName) &&
                       computeShader.HasKernel(RaymarchKernelName) &&
                       computeShader.HasKernel(RaymarchXrKernelName) &&
                       ValidateComputeShaderPragmas();
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static bool ValidateComputeShaderPragmas()
        {
            if (!File.Exists(ComputeShaderAssetPath))
                return false;

            bool hasGridKernel = false;
            bool hasRaymarchKernel = false;
            bool hasRaymarchXrKernel = false;
            foreach (string line in File.ReadLines(ComputeShaderAssetPath))
            {
                if (LineContainsShaderToken(line, ShaderPragmaPrefix, MultiCompileToken) ||
                    LineContainsShaderToken(line, ShaderPragmaPrefix, ShaderFeatureToken))
                    return false;

                hasGridKernel |= LineContainsKernelPragma(line, GridBuildKernelName, true);
                hasRaymarchKernel |= LineContainsKernelPragma(line, RaymarchKernelName, true);
                hasRaymarchXrKernel |= LineContainsKernelPragma(line, RaymarchXrKernelName, false);
            }

            return hasGridKernel && hasRaymarchKernel && hasRaymarchXrKernel;
        }

        public static bool ValidateDearLieShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(DearLieShaderAssetPath);
            if (shader == null || !File.Exists(DearLieShaderAssetPath))
                return false;

            bool hasDearLieProxy = false;
            bool hasBilateralComposite = false;
            bool hasFragProxy = false;
            bool hasFragComposite = false;
            foreach (string line in File.ReadLines(DearLieShaderAssetPath))
            {
                if (LineContainsShaderToken(line, ShaderPragmaPrefix, MultiCompileToken) ||
                    LineContainsShaderToken(line, ShaderPragmaPrefix, ShaderFeatureToken) ||
                    line.IndexOf(KernelPragmaPrefix, StringComparison.Ordinal) >= 0)
                    return false;

                hasDearLieProxy |= line.IndexOf(DearLieProxyPassName, StringComparison.Ordinal) >= 0;
                hasBilateralComposite |= line.IndexOf(BilateralCompositePassName, StringComparison.Ordinal) >= 0;
                hasFragProxy |= line.IndexOf(FragmentPragmaPrefix + "FragProxy", StringComparison.Ordinal) >= 0;
                hasFragComposite |= line.IndexOf(FragmentPragmaPrefix + "FragComposite", StringComparison.Ordinal) >= 0;
            }

            return hasDearLieProxy && hasBilateralComposite && hasFragProxy && hasFragComposite;
        }

        private static bool LineContainsKernelPragma(string line, string kernelName, bool requiresTexture2DArrayDisable)
        {
            int offset = line.IndexOf(ShaderPragmaPrefix, StringComparison.Ordinal);
            while (offset >= 0)
            {
                int tokenOffset = offset + ShaderPragmaPrefix.Length;
                if (line.IndexOf("kernel ", tokenOffset, StringComparison.Ordinal) == tokenOffset)
                {
                    int kernelOffset = tokenOffset + 7;
                    if (line.IndexOf(kernelName, kernelOffset, StringComparison.Ordinal) == kernelOffset)
                    {
                        int kernelNameEnd = kernelOffset + kernelName.Length;
                        if (kernelNameEnd >= line.Length || char.IsWhiteSpace(line[kernelNameEnd]))
                        {
                            bool hasTexture2DArrayDisable = line.IndexOf(DisableTexture2DArrayToken, kernelOffset, StringComparison.Ordinal) >= 0;
                            return hasTexture2DArrayDisable == requiresTexture2DArrayDisable;
                        }
                    }
                }

                offset = line.IndexOf(ShaderPragmaPrefix, tokenOffset, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool LineContainsShaderToken(string line, string prefix, string token)
        {
            int offset = line.IndexOf(prefix, StringComparison.Ordinal);
            while (offset >= 0)
            {
                int tokenOffset = offset + prefix.Length;
                if (line.IndexOf(token, tokenOffset, StringComparison.Ordinal) == tokenOffset)
                    return true;

                offset = line.IndexOf(prefix, tokenOffset, StringComparison.Ordinal);
            }

            return false;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
#endif
