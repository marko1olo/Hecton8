using System;
using Hecton8.Core;
using Hecton8.VFX;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Half-resolution compute-driven god ray solve with bilateral depth-aware composite.
    /// </summary>
    public sealed class VolumetricLightFeature : ScriptableRendererFeature
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute";
        private const float FlashlightVolumetricFullDistanceMeters = 20f;
        private const float FlashlightVolumetricCullDistanceMeters = 30f;
        private const float FlashlightVolumetricFullDistanceMetersSq = FlashlightVolumetricFullDistanceMeters * FlashlightVolumetricFullDistanceMeters;
        private const float FlashlightVolumetricCullDistanceMetersSq = FlashlightVolumetricCullDistanceMeters * FlashlightVolumetricCullDistanceMeters;
        private const double VolumetricSetupBudgetWarningMilliseconds = 0.2d;
        private const int VolumetricPerformanceWarningCooldownFrames = 30;
        private const uint TelemetryWarningVolumetricSetupOverBudgetHash = 0xD4A51F82u;
        private const uint TelemetryContextVolumetricLightFeatureHash = 0x671C92E5u;

        [Serializable]
        private sealed class FeatureSettings
        {
            private const int LowTierRaymarchSteps = 4;
            private const int HighTierRaymarchSteps = 12;

            [Tooltip("Compute shader that owns both the half-res raymarch and full-res bilateral composite.")]
            public ComputeShader computeShader = null;

            [Tooltip("Optional shared VFX profile that defines god ray step budgets by hardware tier.")]
            public VFXEmissionProfile emissionProfile = null;

            [Tooltip("Hardware tier used to resolve god ray step budgets from the emission profile.")]
            public VFXEmissionProfile.HardwareTier hardwareTier = VFXEmissionProfile.HardwareTier.Medium;

            [Tooltip("Where the volumetric pass is injected into URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Internal render scale for the raymarch target. MX350 path must stay half-res or lower.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Fallback medium-tier step count used when no emission profile is assigned.")]
            [Range(1, HighTierRaymarchSteps)] public int fallbackSteps = HighTierRaymarchSteps;

            [Tooltip("Screen-space shadow raymarch steps. MX350 path is strictly below eight.")]
            [Range(1, HighTierRaymarchSteps)] public int volumetricShadowSteps = LowTierRaymarchSteps;

            [Tooltip("Maximum world-space distance for the secondary shadow raymarch toward the light.")]
            [Range(1f, 24f)] public float volumetricShadowDistance = 8f;

            [Tooltip("World-space bias used when testing screen depth along the light shaft.")]
            [Range(0.01f, 0.5f)] public float volumetricShadowBias = 0.08f;

            [Tooltip("Shadow density applied to occluders found by the secondary light-shaft raymarch.")]
            [Range(0f, 4f)] public float volumetricShadowStrength = 1.15f;

            [Tooltip("Base participating media density used by the god ray solve.")]
            [Range(0f, 4f)] public float density = 1.05f;

            [Tooltip("Scattering coefficient applied to the volumetric density sample.")]
            [Range(0f, 4f)] public float scatterCoefficient = 0.85f;

            [Tooltip("Henyey-Greenstein anisotropy applied to main-light forward scattering.")]
            [Range(-0.95f, 0.95f)] public float anisotropy = 0.68f;

            [Tooltip("Maximum world-space march distance in meters.")]
            [Range(4f, 96f)] public float maxRayDistance = 48f;

            [Tooltip("Blue-noise style jitter strength applied to the raymarch start position.")]
            [Range(0f, 1f)] public float jitterStrength = 0.8f;

            [Tooltip("Early-out threshold for accumulated transmittance.")]
            [Range(0f, 1f)] public float minimumTransmittance = 0.03f;

            [Tooltip("Depth-aware rejection scale used during the 3x3 bilateral upsample.")]
            [Range(0.1f, 128f)] public float bilateralDepthScale = 24f;

            [Tooltip("Final additive intensity for the resolved volumetric light.")]
            [Range(0f, 4f)] public float intensity = 1.0f;

            internal int ResolveRaymarchSteps()
            {
                float qualityWeight = ResolveEffectiveQualityWeight01();
                int maxStepCount = ResolveContinuousStepLimit(qualityWeight);

                if (emissionProfile != null)
                    return Mathf.Clamp(emissionProfile.GetVolumetricGodRaySteps(qualityWeight), LowTierRaymarchSteps, maxStepCount);

                float fallback = Mathf.Lerp(LowTierRaymarchSteps, fallbackSteps, SmoothStep01(qualityWeight * 1.7f));
                fallback = Mathf.Lerp(fallback, HighTierRaymarchSteps, SmoothStep01((qualityWeight - 0.58f) * 2.15f));
                return Mathf.Clamp(Mathf.RoundToInt(fallback), LowTierRaymarchSteps, maxStepCount);
            }

            internal int ResolveVolumetricShadowSteps()
            {
                return Mathf.Clamp(volumetricShadowSteps, 1, ResolveContinuousStepLimit(ResolveEffectiveQualityWeight01()));
            }

            private int ResolveContinuousStepLimit(float qualityWeight)
            {
                float t = SmoothStep01(Mathf.Clamp01(qualityWeight));
                return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(LowTierRaymarchSteps, HighTierRaymarchSteps, t)), LowTierRaymarchSteps, HighTierRaymarchSteps);
            }

            private float ResolveEffectiveQualityWeight01()
            {
                float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
                if (float.IsNaN(qualityWeight) || float.IsInfinity(qualityWeight))
                    qualityWeight = Mathf.Clamp01((int)hardwareTier * 0.5f);
                if (Shader.IsKeywordEnabled("_MATH_LOD_LOW"))
                    qualityWeight = Mathf.Min(qualityWeight, 0f);
                if (Shader.IsKeywordEnabled("_MATH_LOD_HIGH"))
                    qualityWeight = Mathf.Max(qualityWeight, 1f);
                return Mathf.Clamp01(qualityWeight);
            }

            private static float SmoothStep01(float value)
            {
                float t = Mathf.Clamp01(value);
                return t * t * (3f - 2f * t);
            }
        }

        private sealed class VolumetricLightPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;

            private sealed class RaymarchPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle depth;
                internal TextureHandle result;
                internal Vector4 fullSize;
                internal Vector4 halfSize;
                internal Matrix4x4 inverseViewProjection;
                internal Vector4 mainLightDirection;
                internal Vector4 mainLightColor;
                internal Vector4 flashlightPosition;
                internal Vector4 flashlightDirection;
                internal Vector4 flashlightColor;
                internal Vector4 flashlightConeData;
                internal float flashlightActive;
                internal float flashlightVolumetricOpacity;
                internal Vector4 scatteringParams;
                internal Vector4 hudFogPerturbation;
                internal Vector4 marchParams;
                internal Vector4 shadowParams;
                internal float fogScatteringCoeff;
                internal Matrix4x4 viewProjection;
            }

            private sealed class CompositePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle halfInput;
                internal TextureHandle destination;
                internal Vector4 fullSize;
                internal Vector4 halfSize;
                internal Matrix4x4 inverseViewProjection;
                internal Vector4 mainLightDirection;
                internal Vector4 flashlightColor;
                internal float flashlightActive;
                internal float flashlightVolumetricOpacity;
                internal float freezeFrameDither;
                internal Vector4 compositeParams;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Volumetric Light");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private RTHandle _halfTexture;
            private RTHandle _compositeTexture;
            private int _raymarchKernel = -1;
            private int _compositeKernel = -1;
            private uint _raymarchThreadGroupSizeX = 8;
            private uint _raymarchThreadGroupSizeY = 8;
            private uint _compositeThreadGroupSizeX = 8;
            private uint _compositeThreadGroupSizeY = 8;
            private Vector4 _mainLightDirection;
            private Vector4 _mainLightColor;
            private Vector4 _flashlightPosition;
            private Vector4 _flashlightDirection;
            private Vector4 _flashlightColor;
            private Vector4 _flashlightConeData;
            private float _flashlightActive;
            private float _flashlightVolumetricOpacity;

            public VolumetricLightPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                ComputeShader computeShader,
                in Vector4 mainLightDirection,
                in Vector4 mainLightColor,
                in Vector4 flashlightPosition,
                in Vector4 flashlightDirection,
                in Vector4 flashlightColor,
                in Vector4 flashlightConeData,
                float flashlightActive,
                float flashlightVolumetricOpacity)
            {
                _settings = settings;
                _computeShader = computeShader;
                _mainLightDirection = mainLightDirection;
                _mainLightColor = mainLightColor;
                _flashlightPosition = flashlightPosition;
                _flashlightDirection = flashlightDirection;
                _flashlightColor = flashlightColor;
                _flashlightConeData = flashlightConeData;
                _flashlightActive = Mathf.Clamp01(flashlightActive);
                _flashlightVolumetricOpacity = Mathf.Clamp01(flashlightVolumetricOpacity);
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (_computeShader != null && (_raymarchKernel < 0 || _compositeKernel < 0))
                {
                    _raymarchKernel = _computeShader.FindKernel("RaymarchVolumetricLight");
                    _compositeKernel = _computeShader.FindKernel("CompositeVolumetricLight");
                    _computeShader.GetKernelThreadGroupSizes(_raymarchKernel, out _raymarchThreadGroupSizeX, out _raymarchThreadGroupSizeY, out _);
                    _computeShader.GetKernelThreadGroupSizes(_compositeKernel, out _compositeThreadGroupSizeX, out _compositeThreadGroupSizeY, out _);
                }
            }

            public void Dispose()
            {
                _halfTexture?.Release();
                _compositeTexture?.Release();
                _halfTexture = null;
                _compositeTexture = null;
                _raymarchKernel = -1;
                _compositeKernel = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _computeShader == null ||
                    _raymarchKernel < 0 ||
                    _compositeKernel < 0 ||
                    (_mainLightDirection.w <= 0.5f && _flashlightActive <= 0.5f))
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                int fullWidth = QuantizeDimension(Mathf.Max(1, sourceDesc.width));
                int fullHeight = QuantizeDimension(Mathf.Max(1, sourceDesc.height));
                int halfWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f))));
                int halfHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f))));
                EnsureRenderTargets(halfWidth, halfHeight, fullWidth, fullHeight);

                TextureHandle halfTexture = renderGraph.ImportTexture(_halfTexture);
                TextureHandle compositeTexture = renderGraph.ImportTexture(_compositeTexture);
                Camera camera = cameraData.camera;
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                Matrix4x4 viewProjection = projectionMatrix * camera.worldToCameraMatrix;
                Matrix4x4 inverseViewProjection = viewProjection.inverse;
                Vector4 fullSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                Vector4 halfSize = new Vector4(halfWidth, halfHeight, 1f / Mathf.Max(1, halfWidth), 1f / Mathf.Max(1, halfHeight));
                Vector4 scatteringParams = new Vector4(
                    Mathf.Max(0f, _settings.density),
                    Mathf.Max(0f, _settings.scatterCoefficient),
                    Mathf.Clamp(_settings.anisotropy, -0.95f, 0.95f),
                    Mathf.Max(0f, _settings.intensity));
                Vector4 hudFogPerturbation = Shader.GetGlobalVector(ShaderConstants.HudFogPerturbationId);
                float fogScatteringCoeff = Shader.GetGlobalFloat(ShaderConstants.FogScatteringCoeffId);
                Vector4 marchParams = new Vector4(
                    Mathf.Max(0.1f, _settings.maxRayDistance),
                    _settings.ResolveRaymarchSteps(),
                    Mathf.Clamp01(_settings.jitterStrength),
                    Mathf.Clamp01(_settings.minimumTransmittance));
                Vector4 shadowParams = new Vector4(
                    _settings.ResolveVolumetricShadowSteps(),
                    Mathf.Max(0.1f, _settings.volumetricShadowDistance),
                    Mathf.Max(0.001f, _settings.volumetricShadowBias),
                    Mathf.Max(0f, _settings.volumetricShadowStrength));
                Vector4 compositeParams = new Vector4(Mathf.Max(0.01f, _settings.bilateralDepthScale), 0f, 0f, 0f);

                using (var builder = renderGraph.AddComputePass("Hecton Volumetric Light Raymarch", out RaymarchPassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _raymarchKernel;
                    passData.threadGroupSizeX = _raymarchThreadGroupSizeX;
                    passData.threadGroupSizeY = _raymarchThreadGroupSizeY;
                    passData.depth = depthTexture;
                    passData.result = halfTexture;
                    passData.fullSize = fullSize;
                    passData.halfSize = halfSize;
                    passData.inverseViewProjection = inverseViewProjection;
                    passData.mainLightDirection = _mainLightDirection;
                    passData.mainLightColor = _mainLightColor;
                    passData.flashlightPosition = _flashlightPosition;
                    passData.flashlightDirection = _flashlightDirection;
                    passData.flashlightColor = _flashlightColor;
                    passData.flashlightConeData = _flashlightConeData;
                    passData.flashlightActive = _flashlightActive;
                    passData.flashlightVolumetricOpacity = _flashlightVolumetricOpacity;
                    passData.scatteringParams = scatteringParams;
                    passData.hudFogPerturbation = hudFogPerturbation;
                    passData.marchParams = marchParams;
                    passData.shadowParams = shadowParams;
                    passData.fogScatteringCoeff = Mathf.Max(0f, fogScatteringCoeff);
                    passData.viewProjection = viewProjection;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Write);

                    builder.SetRenderFunc((RaymarchPassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.halfSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.halfSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.HalfResultId, data.result);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FullSizeId, data.fullSize);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.HalfSizeId, data.halfSize);
                        context.cmd.SetComputeMatrixParam(data.computeShader, ShaderConstants.InverseViewProjectionId, data.inverseViewProjection);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.MainLightDirectionId, data.mainLightDirection);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.MainLightColorId, data.mainLightColor);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FlashlightPositionId, data.flashlightPosition);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FlashlightDirectionId, data.flashlightDirection);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FlashlightColorId, data.flashlightColor);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FlashlightConeDataId, data.flashlightConeData);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FlashlightActiveId, data.flashlightActive);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FlashlightVolumetricOpacityId, data.flashlightVolumetricOpacity);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.ScatteringParamsId, data.scatteringParams);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.HudFogPerturbationId, data.hudFogPerturbation);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.MarchParamsId, data.marchParams);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.ShadowParamsId, data.shadowParams);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FogScatteringCoeffId, data.fogScatteringCoeff);
                        context.cmd.SetComputeMatrixParam(data.computeShader, ShaderConstants.ViewProjectionId, data.viewProjection);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Volumetric Light Composite", out CompositePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _compositeKernel;
                    passData.threadGroupSizeX = _compositeThreadGroupSizeX;
                    passData.threadGroupSizeY = _compositeThreadGroupSizeY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.halfInput = halfTexture;
                    passData.destination = compositeTexture;
                    passData.fullSize = fullSize;
                    passData.halfSize = halfSize;
                    passData.inverseViewProjection = inverseViewProjection;
                    passData.mainLightDirection = _mainLightDirection;
                    passData.flashlightColor = _flashlightColor;
                    passData.flashlightActive = _flashlightActive;
                    passData.flashlightVolumetricOpacity = _flashlightVolumetricOpacity;
                    passData.freezeFrameDither = Shader.GetGlobalFloat(ShaderConstants.FreezeFrameDitherId);
                    passData.compositeParams = compositeParams;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc((CompositePassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.fullSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.fullSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.HalfInputId, data.halfInput);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.CompositeResultId, data.destination);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FullSizeId, data.fullSize);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.HalfSizeId, data.halfSize);
                        context.cmd.SetComputeMatrixParam(data.computeShader, ShaderConstants.InverseViewProjectionId, data.inverseViewProjection);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.MainLightDirectionId, data.mainLightDirection);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.FlashlightColorId, data.flashlightColor);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FlashlightActiveId, data.flashlightActive);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FlashlightVolumetricOpacityId, data.flashlightVolumetricOpacity);
                        context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.FreezeFrameDitherId, data.freezeFrameDither);
                        context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.CompositeParamsId, data.compositeParams);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void EnsureRenderTargets(int halfWidth, int halfHeight, int fullWidth, int fullHeight)
            {
                if ((_halfTexture == null || _halfTexture.rt == null || _halfTexture.rt.width != halfWidth || _halfTexture.rt.height != halfHeight) ||
                    (_compositeTexture == null || _compositeTexture.rt == null || _compositeTexture.rt.width != fullWidth || _compositeTexture.rt.height != fullHeight))
                {
                    _halfTexture?.Release();
                    _compositeTexture?.Release();

                    // COLD ALLOC: RTHandle[1] — persistent half-resolution volumetric lighting buffer — owner: VolumetricLightFeature
                    _halfTexture = RTHandles.Alloc(
                        halfWidth,
                        halfHeight,
                        1,
                        DepthBits.None,
                        GraphicsFormat.R16G16B16A16_SFloat,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        TextureDimension.Tex2D,
                        true,
                        name: "_HectonVolumetricLightHalf");

                    // COLD ALLOC: RTHandle[1] — persistent full-resolution volumetric composite buffer — owner: VolumetricLightFeature
                    _compositeTexture = RTHandles.Alloc(
                        fullWidth,
                        fullHeight,
                        1,
                        DepthBits.None,
                        GraphicsFormat.R16G16B16A16_SFloat,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        TextureDimension.Tex2D,
                        true,
                        name: "_HectonVolumetricLightComposite");
                }
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonVolumetricSourceColor");
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonVolumetricSourceDepth");
            internal static readonly int HalfInputId = Shader.PropertyToID("_HectonVolumetricHalfInput");
            internal static readonly int HalfResultId = Shader.PropertyToID("_HectonVolumetricHalfResult");
            internal static readonly int CompositeResultId = Shader.PropertyToID("_HectonVolumetricCompositeResult");
            internal static readonly int FullSizeId = Shader.PropertyToID("_HectonVolumetricFullSize");
            internal static readonly int HalfSizeId = Shader.PropertyToID("_HectonVolumetricHalfSize");
            internal static readonly int InverseViewProjectionId = Shader.PropertyToID("_HectonVolumetricInverseViewProjection");
            internal static readonly int MainLightDirectionId = Shader.PropertyToID("_HectonVolumetricMainLightDirection");
            internal static readonly int MainLightColorId = Shader.PropertyToID("_HectonVolumetricMainLightColor");
            internal static readonly int ScatteringParamsId = Shader.PropertyToID("_HectonVolumetricScatteringParams");
            internal static readonly int HudFogPerturbationId = Shader.PropertyToID("_HectonHudFogPerturbation");
            internal static readonly int FogScatteringCoeffId = Shader.PropertyToID("_FogScatteringCoeff");
            internal static readonly int MarchParamsId = Shader.PropertyToID("_HectonVolumetricMarchParams");
            internal static readonly int ShadowParamsId = Shader.PropertyToID("_HectonVolumetricShadowParams");
            internal static readonly int CompositeParamsId = Shader.PropertyToID("_HectonVolumetricCompositeParams");
            internal static readonly int ViewProjectionId = Shader.PropertyToID("_HectonVolumetricViewProjection");
            internal static readonly int FlashlightActiveId = Shader.PropertyToID("_HectonFlashlightActive");
            internal static readonly int FlashlightPositionId = Shader.PropertyToID("_HectonFlashlightPositionWS");
            internal static readonly int FlashlightDirectionId = Shader.PropertyToID("_HectonFlashlightDirectionWS");
            internal static readonly int FlashlightColorId = Shader.PropertyToID("_HectonFlashlightColor");
            internal static readonly int FlashlightConeDataId = Shader.PropertyToID("_HectonFlashlightConeData");
            internal static readonly int FlashlightVolumetricOpacityId = Shader.PropertyToID("_HectonFlashlightVolumetricOpacity");
            internal static readonly int FreezeFrameDitherId = Shader.PropertyToID("_HectonFreezeFrameDither");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VolumetricLightPass _pass;
        private int _nextPerformanceWarningFrame;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif

            _pass ??= new VolumetricLightPass();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.computeShader == null || _pass == null || !SystemInfo.supportsComputeShaders)
                return;

            long setupStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            Vector4 mainLightDirection = Vector4.zero;
            Vector4 mainLightColor = Vector4.zero;

            int mainLightIndex = renderingData.lightData.mainLightIndex;
            if (mainLightIndex >= 0 && mainLightIndex < renderingData.lightData.visibleLights.Length)
            {
                VisibleLight visibleLight = renderingData.lightData.visibleLights[mainLightIndex];
                if (visibleLight.lightType == LightType.Directional)
                {
                    Vector3 directionWS = -visibleLight.localToWorldMatrix.GetColumn(2);
                    Color color = visibleLight.finalColor;
                    mainLightDirection = new Vector4(directionWS.x, directionWS.y, directionWS.z, 1f);
                    mainLightColor = new Vector4(color.r, color.g, color.b, 1f);
                }
            }

            Vector4 flashlightPosition = Shader.GetGlobalVector(ShaderConstants.FlashlightPositionId);
            Vector4 flashlightDirection = Shader.GetGlobalVector(ShaderConstants.FlashlightDirectionId);
            Vector4 flashlightColor = Shader.GetGlobalVector(ShaderConstants.FlashlightColorId);
            Vector4 flashlightConeData = Shader.GetGlobalVector(ShaderConstants.FlashlightConeDataId);
            float flashlightActive = Shader.GetGlobalFloat(ShaderConstants.FlashlightActiveId);
            float flashlightHasCone = flashlightActive > 0.5f && flashlightColor.w > 0.001f && flashlightPosition.w > 0.1f
                ? 1f
                : 0f;
            float flashlightVolumetricOpacity = ResolveFlashlightVolumetricOpacity(renderingData.cameraData.camera, in flashlightPosition, flashlightHasCone);
            if (flashlightVolumetricOpacity <= 0.001f)
                flashlightHasCone = 0f;

            if (mainLightDirection.w <= 0.5f && flashlightHasCone <= 0.5f)
                return;

            _pass.Setup(
                settings,
                settings.computeShader,
                mainLightDirection,
                mainLightColor,
                flashlightPosition,
                flashlightDirection,
                flashlightColor,
                flashlightConeData,
                flashlightHasCone,
                flashlightVolumetricOpacity);
            renderer.EnqueuePass(_pass);
            PublishVolumetricSetupWarningIfNeeded(setupStartTimestamp);
        }

        private void PublishVolumetricSetupWarningIfNeeded(long setupStartTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - setupStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds <= VolumetricSetupBudgetWarningMilliseconds || Time.frameCount < _nextPerformanceWarningFrame)
                return;

            _nextPerformanceWarningFrame = Time.frameCount + VolumetricPerformanceWarningCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                TelemetryWarningVolumetricSetupOverBudgetHash,
                TelemetryContextVolumetricLightFeatureHash,
                (float)elapsedMilliseconds);
        }

        private static float ResolveFlashlightVolumetricOpacity(Camera camera, in Vector4 flashlightPosition, float flashlightHasCone)
        {
            if (flashlightHasCone <= 0.5f)
                return 0f;

            if (camera == null)
                return 1f;

            Vector3 cameraPosition = camera.transform.position;
            float dx = flashlightPosition.x - cameraPosition.x;
            float dy = flashlightPosition.y - cameraPosition.y;
            float dz = flashlightPosition.z - cameraPosition.z;
            float distanceSq = dx * dx + dy * dy + dz * dz;
            if (float.IsNaN(distanceSq) || float.IsInfinity(distanceSq) || distanceSq >= FlashlightVolumetricCullDistanceMetersSq)
                return 0f;

            if (distanceSq <= FlashlightVolumetricFullDistanceMetersSq)
                return 1f;

            float fade = Mathf.Clamp01((distanceSq - FlashlightVolumetricFullDistanceMetersSq) /
                (FlashlightVolumetricCullDistanceMetersSq - FlashlightVolumetricFullDistanceMetersSq));
            fade = fade * fade * (3f - 2f * fade);
            return 1f - fade;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }
    }
}
