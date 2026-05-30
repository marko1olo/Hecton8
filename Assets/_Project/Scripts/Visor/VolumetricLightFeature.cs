using System;
using Hecton8.Core;
using Hecton8.VFX;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Half-resolution compute-driven god ray solve with bilateral depth-aware composite.
    /// </summary>
    public sealed class VolumetricLightFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute";
        private const string ProxyShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricLightProxy.shader";
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
            private const int MinimumQualityRaymarchSteps = 4;
            private const int MaximumQualityRaymarchSteps = 12;

            [Tooltip("Compute shader that owns both the half-res raymarch and full-res bilateral composite.")]
            public ComputeShader computeShader = null;

            [Tooltip("Fullscreen Dear Lie fallback used when compute volumetrics are not allowed on this device.")]
            public Shader proxyShader = null;

            [Tooltip("Optional shared VFX profile that defines god ray step budgets by continuous quality weight.")]
            public VFXEmissionProfile emissionProfile = null;

            [Tooltip("Fallback continuous quality weight used only when global quality is not finite.")]
            [FormerlySerializedAs("hardwareTier")]
            [Range(0f, 1f)] public float qualityFallbackWeight = 0.5f;

            [Tooltip("Where the volumetric pass is injected into URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Internal render scale for the raymarch target. Low memory pressure should stay half-res or lower.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Fallback maximum-quality step count used when no emission profile is assigned.")]
            [Range(1, MaximumQualityRaymarchSteps)] public int fallbackSteps = MaximumQualityRaymarchSteps;

            [Tooltip("Screen-space shadow raymarch steps. Continuous quality clamps the active limit.")]
            [Range(1, MaximumQualityRaymarchSteps)] public int volumetricShadowSteps = MinimumQualityRaymarchSteps;

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
                    return Mathf.Clamp(emissionProfile.GetVolumetricGodRaySteps(qualityWeight), MinimumQualityRaymarchSteps, maxStepCount);

                float fallback = Mathf.Lerp(MinimumQualityRaymarchSteps, fallbackSteps, SmoothStep01(qualityWeight * 1.7f));
                fallback = Mathf.Lerp(fallback, MaximumQualityRaymarchSteps, SmoothStep01((qualityWeight - 0.58f) * 2.15f));
                return Mathf.Clamp(Mathf.RoundToInt(fallback), MinimumQualityRaymarchSteps, maxStepCount);
            }

            internal int ResolveVolumetricShadowSteps()
            {
                return Mathf.Clamp(volumetricShadowSteps, 1, ResolveContinuousStepLimit(ResolveEffectiveQualityWeight01()));
            }

            internal float ResolveRenderScale()
            {
                float authoredScale = Mathf.Clamp(renderScale, 0.25f, 1f);
                float qualityWeight = ResolveEffectiveQualityWeight01();
                float qualityCurve = SmoothStep01(qualityWeight);
                float lowScale = Mathf.Min(authoredScale, 0.35f);
                return Mathf.Clamp(Mathf.Lerp(lowScale, authoredScale, qualityCurve), 0.25f, 1f);
            }

            internal float ResolveQualityWeight01()
            {
                return ResolveEffectiveQualityWeight01();
            }

            private int ResolveContinuousStepLimit(float qualityWeight)
            {
                float t = SmoothStep01(Mathf.Clamp01(qualityWeight));
                return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(MinimumQualityRaymarchSteps, MaximumQualityRaymarchSteps, t)), MinimumQualityRaymarchSteps, MaximumQualityRaymarchSteps);
            }

            private float ResolveEffectiveQualityWeight01()
            {
                float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
                if (float.IsNaN(qualityWeight) || float.IsInfinity(qualityWeight))
                    qualityWeight = Mathf.Clamp01(qualityFallbackWeight);
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
                internal int dispatchX;
                internal int dispatchY;
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
                internal int dispatchX;
                internal int dispatchY;
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

            private sealed class ProxyPassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material material;
                internal Vector4 fullSize;
                internal Vector4 mainLightDirection;
                internal Vector4 mainLightColor;
                internal Vector4 flashlightPosition;
                internal Vector4 flashlightDirection;
                internal Vector4 flashlightColor;
                internal Vector4 flashlightConeData;
                internal Vector4 scatteringParams;
                internal Vector4 marchParams;
                internal Vector4 shadowParams;
                internal Vector4 proxyParams;
                internal float flashlightActive;
                internal float flashlightVolumetricOpacity;
                internal float freezeFrameDither;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Volumetric Light");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private Material _proxyMaterial;
            private bool _forceProxyOnly;
            private int _raymarchKernel = -1;
            private int _compositeKernel = -1;
            private uint _raymarchThreadGroupSizeX;
            private uint _raymarchThreadGroupSizeY;
            private uint _compositeThreadGroupSizeX;
            private uint _compositeThreadGroupSizeY;
            private Vector4 _mainLightDirection;
            private Vector4 _mainLightColor;
            private Vector4 _flashlightPosition;
            private Vector4 _flashlightDirection;
            private Vector4 _flashlightColor;
            private Vector4 _flashlightConeData;
            private float _flashlightActive;
            private float _flashlightVolumetricOpacity;
            private Vector4 _hudFogPerturbation;
            private float _fogScatteringCoeff;
            private float _freezeFrameDither;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

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
                float flashlightVolumetricOpacity,
                in Vector4 hudFogPerturbation,
                float fogScatteringCoeff,
                float freezeFrameDither,
                Material proxyMaterial,
                bool forceProxyOnly)
            {
                _settings = settings;
                _proxyMaterial = proxyMaterial;
                _forceProxyOnly = forceProxyOnly;
                if (!ReferenceEquals(_computeShader, computeShader))
                {
                    _computeShader = computeShader;
                    ResetComputeKernelState();
                }

                _mainLightDirection = mainLightDirection;
                _mainLightColor = mainLightColor;
                _flashlightPosition = flashlightPosition;
                _flashlightDirection = flashlightDirection;
                _flashlightColor = flashlightColor;
                _flashlightConeData = flashlightConeData;
                _flashlightActive = Mathf.Clamp01(flashlightActive);
                _flashlightVolumetricOpacity = Mathf.Clamp01(flashlightVolumetricOpacity);
                _hudFogPerturbation = hudFogPerturbation;
                _fogScatteringCoeff = Mathf.Max(0f, fogScatteringCoeff);
                _freezeFrameDither = freezeFrameDither;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (!_forceProxyOnly && _computeShader != null && (_raymarchKernel < 0 || _compositeKernel < 0))
                {
                    if (!TryResolveKernel(_computeShader, "RaymarchVolumetricLight", out _raymarchKernel, out _raymarchThreadGroupSizeX, out _raymarchThreadGroupSizeY) ||
                        !TryResolveKernel(_computeShader, "CompositeVolumetricLight", out _compositeKernel, out _compositeThreadGroupSizeX, out _compositeThreadGroupSizeY))
                    {
                        _raymarchKernel = -1;
                        _compositeKernel = -1;
                        _raymarchThreadGroupSizeX = 0u;
                        _raymarchThreadGroupSizeY = 0u;
                        _compositeThreadGroupSizeX = 0u;
                        _compositeThreadGroupSizeY = 0u;
                    }
                }
            }

            public void Dispose()
            {
                ResetComputeKernelState();
            }

            private void ResetComputeKernelState()
            {
                _raymarchKernel = -1;
                _compositeKernel = -1;
                _raymarchThreadGroupSizeX = 0u;
                _raymarchThreadGroupSizeY = 0u;
                _compositeThreadGroupSizeX = 0u;
                _compositeThreadGroupSizeY = 0u;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
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
                bool hasComputeKernels = !_forceProxyOnly &&
                                         _computeShader != null &&
                                         _raymarchKernel >= 0 &&
                                         _compositeKernel >= 0;
                if (!hasComputeKernels)
                {
                    if (_proxyMaterial == null)
                        return;

                    RecordProxyComposite(renderGraph, resourceData, sourceTexture, depthTexture, sourceDesc);
                    return;
                }

                int fullWidth = Mathf.Max(1, sourceDesc.width);
                int fullHeight = Mathf.Max(1, sourceDesc.height);
                float renderScale = _settings.ResolveRenderScale();
                int halfWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * renderScale)));
                int halfHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * renderScale)));
                int raymarchDispatchX = ResolveDispatchGroups(halfWidth, _raymarchThreadGroupSizeX);
                int raymarchDispatchY = ResolveDispatchGroups(halfHeight, _raymarchThreadGroupSizeY);
                int compositeDispatchX = ResolveDispatchGroups(fullWidth, _compositeThreadGroupSizeX);
                int compositeDispatchY = ResolveDispatchGroups(fullHeight, _compositeThreadGroupSizeY);
                if (raymarchDispatchX <= 0 || raymarchDispatchY <= 0 || compositeDispatchX <= 0 || compositeDispatchY <= 0)
                    return;

                TextureDesc halfDesc = CreateGraphTextureDesc(
                    sourceDesc,
                    halfWidth,
                    halfHeight,
                    "_HectonVolumetricLightHalf",
                    GraphicsFormat.R16G16B16A16_SFloat,
                    true,
                    FilterMode.Bilinear);
                TextureDesc compositeDesc = CreateGraphTextureDesc(
                    sourceDesc,
                    fullWidth,
                    fullHeight,
                    "_HectonVolumetricLightComposite",
                    GraphicsFormat.R16G16B16A16_SFloat,
                    true,
                    FilterMode.Bilinear);
                TextureHandle halfTexture = renderGraph.CreateTexture(halfDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);
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
                    passData.dispatchX = raymarchDispatchX;
                    passData.dispatchY = raymarchDispatchY;
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
                    passData.hudFogPerturbation = _hudFogPerturbation;
                    passData.marchParams = marchParams;
                    passData.shadowParams = shadowParams;
                    passData.fogScatteringCoeff = _fogScatteringCoeff;
                    passData.viewProjection = viewProjection;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (RaymarchPassData data, ComputeGraphContext context) =>
                    {
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
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Volumetric Light Composite", out CompositePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _compositeKernel;
                    passData.dispatchX = compositeDispatchX;
                    passData.dispatchY = compositeDispatchY;
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
                    passData.freezeFrameDither = _freezeFrameDither;
                    passData.compositeParams = compositeParams;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (CompositePassData data, ComputeGraphContext context) =>
                    {
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
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void RecordProxyComposite(
                RenderGraph renderGraph,
                UniversalResourceData resourceData,
                TextureHandle sourceTexture,
                TextureHandle depthTexture,
                in TextureDesc sourceDesc)
            {
                int fullWidth = Mathf.Max(1, sourceDesc.width);
                int fullHeight = Mathf.Max(1, sourceDesc.height);
                TextureDesc proxyDesc = CreateGraphTextureDesc(
                    sourceDesc,
                    fullWidth,
                    fullHeight,
                    "_HectonVolumetricLightProxyComposite",
                    sourceDesc.colorFormat,
                    false,
                    FilterMode.Bilinear);
                TextureHandle proxyTexture = renderGraph.CreateTexture(proxyDesc);
                Vector4 fullSize = new Vector4(fullWidth, fullHeight, 1f / fullWidth, 1f / fullHeight);
                Vector4 scatteringParams = new Vector4(
                    Mathf.Max(0f, _settings.density),
                    Mathf.Max(0f, _settings.scatterCoefficient),
                    Mathf.Clamp(_settings.anisotropy, -0.95f, 0.95f),
                    Mathf.Max(0f, _settings.intensity));
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
                Vector4 proxyParams = new Vector4(
                    _settings.ResolveQualityWeight01(),
                    _settings.ResolveRenderScale(),
                    Mathf.Max(0.01f, _settings.bilateralDepthScale),
                    _fogScatteringCoeff);

                using (var builder = renderGraph.AddRasterRenderPass<ProxyPassData>(
                           "Hecton Volumetric Light Proxy Composite",
                           out ProxyPassData passData,
                           _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = proxyTexture;
                    passData.material = _proxyMaterial;
                    passData.fullSize = fullSize;
                    passData.mainLightDirection = _mainLightDirection;
                    passData.mainLightColor = _mainLightColor;
                    passData.flashlightPosition = _flashlightPosition;
                    passData.flashlightDirection = _flashlightDirection;
                    passData.flashlightColor = _flashlightColor;
                    passData.flashlightConeData = _flashlightConeData;
                    passData.scatteringParams = scatteringParams;
                    passData.marchParams = marchParams;
                    passData.shadowParams = shadowParams;
                    passData.proxyParams = proxyParams;
                    passData.flashlightActive = _flashlightActive;
                    passData.flashlightVolumetricOpacity = _flashlightVolumetricOpacity;
                    passData.freezeFrameDither = _freezeFrameDither;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(proxyTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (ProxyPassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        context.cmd.SetGlobalTexture(ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetGlobalVector(ShaderConstants.FullSizeId, data.fullSize);
                        context.cmd.SetGlobalVector(ShaderConstants.MainLightDirectionId, data.mainLightDirection);
                        context.cmd.SetGlobalVector(ShaderConstants.MainLightColorId, data.mainLightColor);
                        context.cmd.SetGlobalVector(ShaderConstants.FlashlightPositionId, data.flashlightPosition);
                        context.cmd.SetGlobalVector(ShaderConstants.FlashlightDirectionId, data.flashlightDirection);
                        context.cmd.SetGlobalVector(ShaderConstants.FlashlightColorId, data.flashlightColor);
                        context.cmd.SetGlobalVector(ShaderConstants.FlashlightConeDataId, data.flashlightConeData);
                        context.cmd.SetGlobalVector(ShaderConstants.ScatteringParamsId, data.scatteringParams);
                        context.cmd.SetGlobalVector(ShaderConstants.MarchParamsId, data.marchParams);
                        context.cmd.SetGlobalVector(ShaderConstants.ShadowParamsId, data.shadowParams);
                        context.cmd.SetGlobalVector(ShaderConstants.ProxyParamsId, data.proxyParams);
                        context.cmd.SetGlobalFloat(ShaderConstants.FlashlightActiveId, data.flashlightActive);
                        context.cmd.SetGlobalFloat(ShaderConstants.FlashlightVolumetricOpacityId, data.flashlightVolumetricOpacity);
                        context.cmd.SetGlobalFloat(ShaderConstants.FreezeFrameDitherId, data.freezeFrameDither);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, 0);
                    });
                }

                resourceData.cameraColor = proxyTexture;
            }

            private static bool TryResolveKernel(ComputeShader computeShader, string kernelName, out int kernelIndex, out uint groupSizeX, out uint groupSizeY)
            {
                kernelIndex = -1;
                groupSizeX = 0u;
                groupSizeY = 0u;
                if (computeShader == null || !computeShader.HasKernel(kernelName))
                    return false;

                int resolvedKernel = computeShader.FindKernel(kernelName);
                if (resolvedKernel < 0 || !computeShader.IsSupported(resolvedKernel))
                    return false;

                computeShader.GetKernelThreadGroupSizes(resolvedKernel, out uint x, out uint y, out uint z);
                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                kernelIndex = resolvedKernel;
                groupSizeX = x;
                groupSizeY = y;
                return true;
            }

            private static int ResolveDispatchGroups(int value, uint groupSize)
            {
                if (value <= 0 || groupSize == 0u)
                    return 0;

                long groups = ((long)value + groupSize - 1L) / groupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }

            private static TextureDesc CreateGraphTextureDesc(
                in TextureDesc sourceDesc,
                int width,
                int height,
                string name,
                GraphicsFormat colorFormat,
                bool enableRandomWrite,
                FilterMode filterMode)
            {
                TextureDesc desc = new TextureDesc(Mathf.Max(1, width), Mathf.Max(1, height), false, false);
                desc.name = name;
                desc.width = Mathf.Max(1, width);
                desc.height = Mathf.Max(1, height);
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.colorFormat = colorFormat != GraphicsFormat.None ? colorFormat : sourceDesc.colorFormat;
                desc.clearBuffer = false;
                desc.dimension = TextureDimension.Tex2D;
                desc.slices = 1;
                desc.useDynamicScale = false;
                desc.useDynamicScaleExplicit = false;
                desc.enableRandomWrite = enableRandomWrite;
                desc.filterMode = filterMode;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                return desc;
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
            internal static readonly int ProxyParamsId = Shader.PropertyToID("_HectonVolumetricProxyParams");
            internal static readonly int ViewProjectionId = Shader.PropertyToID("_HectonVolumetricViewProjection");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
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
        private Material _proxyMaterial;
        private int _nextPerformanceWarningFrame;
        private bool _supportsComputeShaders;
        private bool _hotSwapRegistered;
        private bool _lateFrameRegistered;
        private Vector4 _cachedFlashlightPosition;
        private Vector4 _cachedFlashlightDirection;
        private Vector4 _cachedFlashlightColor;
        private Vector4 _cachedFlashlightConeData;
        private Vector4 _cachedHudFogPerturbation;
        private float _cachedFlashlightActive;
        private float _cachedFogScatteringCoeff;
        private float _cachedFreezeFrameDither;

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            CachePresentationGlobalsLate();
        }

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
            if (settings != null && settings.proxyShader == null)
                settings.proxyShader = AssetDatabase.LoadAssetAtPath<Shader>(ProxyShaderAssetPath);
#endif

            _pass ??= new VolumetricLightPass();
            CacheGraphicsCapabilitiesCold();
            Shader proxyShader = settings != null ? settings.proxyShader : null;
            if (proxyShader == null)
                RuntimeShaderReferenceCatalog.TryGetVolumetricLightProxyShader(out proxyShader);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (proxyShader == null)
                proxyShader = Shader.Find("Hidden/Hecton8/VolumetricLightProxy");
#endif
            RecreateMaterial(ref _proxyMaterial, proxyShader);
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            CachePresentationGlobalsLate();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _pass == null)
            {
                return;
            }

            bool allowComputeVolumetrics = settings.computeShader != null &&
                                           _supportsComputeShaders;
            bool forceProxyOnly = !allowComputeVolumetrics;
            if (forceProxyOnly && _proxyMaterial == null)
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

            Vector4 flashlightPosition = _cachedFlashlightPosition;
            Vector4 flashlightDirection = _cachedFlashlightDirection;
            Vector4 flashlightColor = _cachedFlashlightColor;
            Vector4 flashlightConeData = _cachedFlashlightConeData;
            float flashlightActive = _cachedFlashlightActive;
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
                flashlightVolumetricOpacity,
                _cachedHudFogPerturbation,
                _cachedFogScatteringCoeff,
                _cachedFreezeFrameDither,
                _proxyMaterial,
                forceProxyOnly);
            renderer.EnqueuePass(_pass);
            PublishVolumetricSetupWarningIfNeeded(setupStartTimestamp);
        }

        private void PublishVolumetricSetupWarningIfNeeded(long setupStartTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - setupStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / System.Diagnostics.Stopwatch.Frequency;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (elapsedMilliseconds <= VolumetricSetupBudgetWarningMilliseconds || frame < _nextPerformanceWarningFrame)
                return;

            _nextPerformanceWarningFrame = frame + VolumetricPerformanceWarningCooldownFrames;
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
            CoreUtils.Destroy(_proxyMaterial);
            _proxyMaterial = null;
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterLateFrameTickable();
            if (currentService != null)
                TryRegisterLateFrameTickable();
        }

        public void LateFrameTick()
        {
            CachePresentationGlobalsLate();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShaders = SystemInfo.supportsComputeShaders;
        }

        private void CachePresentationGlobalsLate()
        {
            _cachedFlashlightPosition = Shader.GetGlobalVector(ShaderConstants.FlashlightPositionId);
            _cachedFlashlightDirection = Shader.GetGlobalVector(ShaderConstants.FlashlightDirectionId);
            _cachedFlashlightColor = Shader.GetGlobalVector(ShaderConstants.FlashlightColorId);
            _cachedFlashlightConeData = Shader.GetGlobalVector(ShaderConstants.FlashlightConeDataId);
            _cachedHudFogPerturbation = Shader.GetGlobalVector(ShaderConstants.HudFogPerturbationId);
            _cachedFlashlightActive = Shader.GetGlobalFloat(ShaderConstants.FlashlightActiveId);
            _cachedFogScatteringCoeff = Mathf.Max(0f, Shader.GetGlobalFloat(ShaderConstants.FogScatteringCoeffId));
            _cachedFreezeFrameDither = Shader.GetGlobalFloat(ShaderConstants.FreezeFrameDitherId);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_lateFrameRegistered)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                CoreUtils.Destroy(material);
                material = null;
                return;
            }

            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
    }
}
