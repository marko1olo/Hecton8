using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Screen-space volumetric shafts for scooter headlights with a lightweight abyssal floor projection pass.
    /// </summary>
    public sealed class HectonScooterVolumetricShaftsFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden multi-pass shader used for the shaft raymarch, bilateral blur, and composite.")]
            public Shader shader;

            [Tooltip("Optional blue-noise texture used to jitter raymarch steps. Leave null to fall back to procedural noise.")]
            public Texture2D blueNoiseTexture;

            [Tooltip("Where the volumetric shaft pass is injected into URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Internal render scale for the shaft target. Lower values save MX350 fill-rate.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Raymarch step count for the scooter shaft volume.")]
            [Range(6, 32)] public int raymarchSteps = 12;

            [Tooltip("Maximum volumetric march distance in meters.")]
            [Range(8f, 120f)] public float maxRayDistance = 56f;

            [Tooltip("Forward-scattering anisotropy for the headlight shafts.")]
            [Range(0f, 0.95f)] public float scatteringAnisotropy = 0.68f;

            [Tooltip("Base water density used for light accumulation.")]
            [Range(0f, 4f)] public float density = 1.05f;

            [Tooltip("Amount of blue-noise jitter applied to the raymarch start position.")]
            [Range(0f, 1f)] public float blueNoiseJitter = 0.85f;

            [Tooltip("Edge-preserving bilateral depth falloff used during blur.")]
            [Range(0.1f, 128f)] public float bilateralDepthSigma = 24f;

            [Tooltip("Overall shaft brightness multiplier.")]
            [Range(0f, 6f)] public float shaftIntensity = 1.3f;

            [Tooltip("World-space scale of the abyssal biolum floor projection.")]
            [Range(0.01f, 2f)] public float biolumPatternScale = 0.14f;

            [Tooltip("How much floor biolum energy is projected back onto opaque seabed geometry.")]
            [Range(0f, 3f)] public float biolumProjectionStrength = 0.62f;

            [Tooltip("Strength of suspended silt inside the scooter headlight cone.")]
            [Range(0f, 4f)] public float siltStrength = 1.15f;

            [Tooltip("World-space scale of the silt noise field.")]
            [Range(0.02f, 1f)] public float siltNoiseScale = 0.14f;

            [Tooltip("How much denser the silt becomes near the hit surface or seabed.")]
            [Range(0f, 4f)] public float siltFloorBoost = 1.35f;

            [Tooltip("Temporal drift speed of the suspended silt field.")]
            [Range(0f, 2f)] public float siltDriftSpeed = 0.18f;
        }

        private sealed class ShaftsPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle shafts;
                internal TextureHandle blur;
                internal TextureHandle destination;
                internal Material raymarchMaterial;
                internal Material blurHorizontalMaterial;
                internal Material blurVerticalMaterial;
                internal Material compositeMaterial;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Scooter Volumetric Shafts");
            private FeatureSettings _settings;
            private Material _raymarchMaterial;
            private Material _blurHorizontalMaterial;
            private Material _blurVerticalMaterial;
            private Material _compositeMaterial;

            public ShaftsPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material raymarchMaterial,
                Material blurHorizontalMaterial,
                Material blurVerticalMaterial,
                Material compositeMaterial)
            {
                _settings = settings;
                _raymarchMaterial = raymarchMaterial;
                _blurHorizontalMaterial = blurHorizontalMaterial;
                _blurVerticalMaterial = blurVerticalMaterial;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _raymarchMaterial == null ||
                    _blurHorizontalMaterial == null ||
                    _blurVerticalMaterial == null ||
                    _compositeMaterial == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                int shaftWidth = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));
                int shaftHeight = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));

                TextureDesc shaftDesc = new TextureDesc(sourceDesc);
                shaftDesc.name = "_HectonScooterVolumetricShafts";
                shaftDesc.width = shaftWidth;
                shaftDesc.height = shaftHeight;
                shaftDesc.depthBufferBits = DepthBits.None;
                shaftDesc.msaaSamples = MSAASamples.None;
                shaftDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                shaftDesc.clearBuffer = true;
                shaftDesc.clearColor = Color.black;
                shaftDesc.filterMode = FilterMode.Bilinear;
                shaftDesc.useMipMap = false;
                shaftDesc.autoGenerateMips = false;

                TextureDesc blurDesc = new TextureDesc(shaftDesc);
                blurDesc.name = "_HectonScooterVolumetricShaftsBlur";

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonScooterVolumetricShaftsComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;

                TextureHandle shaftsTexture = renderGraph.CreateTexture(shaftDesc);
                TextureHandle blurTexture = renderGraph.CreateTexture(blurDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                UpdateMaterialParameters(_raymarchMaterial, _settings, 0f);
                UpdateMaterialParameters(_blurHorizontalMaterial, _settings, 1f);
                UpdateMaterialParameters(_blurVerticalMaterial, _settings, 2f);
                UpdateMaterialParameters(_compositeMaterial, _settings, 3f);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Scooter Volumetric Shafts", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.shafts = shaftsTexture;
                    passData.blur = blurTexture;
                    passData.destination = compositeTexture;
                    passData.raymarchMaterial = _raymarchMaterial;
                    passData.blurHorizontalMaterial = _blurHorizontalMaterial;
                    passData.blurVerticalMaterial = _blurVerticalMaterial;
                    passData.compositeMaterial = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(shaftsTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(blurTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.shafts, LoadAction, StoreAction, data.raymarchMaterial, 0);
                        Blitter.BlitCameraTexture(cmd, data.shafts, data.blur, LoadAction, StoreAction, data.blurHorizontalMaterial, 1);
                        Blitter.BlitCameraTexture(cmd, data.blur, data.shafts, LoadAction, StoreAction, data.blurVerticalMaterial, 2);
                        cmd.SetGlobalTexture(ShaderConstants.ShaftTextureId, data.shafts);
                        cmd.SetGlobalTexture(ShaderConstants.HeadlightVolumetricsTextureId, data.shafts);
                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.compositeMaterial, 3);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static void UpdateMaterialParameters(Material material, FeatureSettings settings, float passMode)
            {
                material.SetFloat(ShaderConstants.PassModeId, passMode);
                material.SetFloat(ShaderConstants.RenderScaleId, Mathf.Clamp(settings.renderScale, 0.25f, 1f));
                material.SetFloat(ShaderConstants.RaymarchStepsId, Mathf.Clamp(settings.raymarchSteps, 6, 32));
                material.SetFloat(ShaderConstants.MaxRayDistanceId, Mathf.Max(1f, settings.maxRayDistance));
                material.SetFloat(ShaderConstants.ScatteringAnisotropyId, Mathf.Clamp(settings.scatteringAnisotropy, 0f, 0.95f));
                material.SetFloat(ShaderConstants.DensityId, Mathf.Max(0f, settings.density));
                material.SetFloat(ShaderConstants.BlueNoiseJitterId, Mathf.Clamp01(settings.blueNoiseJitter));
                material.SetFloat(ShaderConstants.BilateralDepthSigmaId, Mathf.Max(0.01f, settings.bilateralDepthSigma));
                material.SetFloat(ShaderConstants.ShaftIntensityId, Mathf.Max(0f, settings.shaftIntensity));
                material.SetFloat(ShaderConstants.BiolumPatternScaleId, Mathf.Max(0.001f, settings.biolumPatternScale));
                material.SetFloat(ShaderConstants.BiolumProjectionStrengthId, Mathf.Max(0f, settings.biolumProjectionStrength));
                material.SetFloat(ShaderConstants.SiltStrengthId, Mathf.Max(0f, settings.siltStrength));
                material.SetFloat(ShaderConstants.SiltNoiseScaleId, Mathf.Max(0.001f, settings.siltNoiseScale));
                material.SetFloat(ShaderConstants.SiltFloorBoostId, Mathf.Max(0f, settings.siltFloorBoost));
                material.SetFloat(ShaderConstants.SiltDriftSpeedId, Mathf.Max(0f, settings.siltDriftSpeed));
                material.SetFloat(ShaderConstants.HasBlueNoiseTextureId, settings.blueNoiseTexture != null ? 1f : 0f);
                material.SetTexture(ShaderConstants.BlueNoiseTextureId, settings.blueNoiseTexture);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
            internal static readonly int FloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
            internal static readonly int PassModeId = Shader.PropertyToID("_HectonShaftPassMode");
            internal static readonly int RenderScaleId = Shader.PropertyToID("_HectonShaftRenderScale");
            internal static readonly int RaymarchStepsId = Shader.PropertyToID("_HectonShaftRaymarchSteps");
            internal static readonly int MaxRayDistanceId = Shader.PropertyToID("_HectonShaftMaxRayDistance");
            internal static readonly int ScatteringAnisotropyId = Shader.PropertyToID("_HectonShaftScatteringAnisotropy");
            internal static readonly int DensityId = Shader.PropertyToID("_HectonShaftDensity");
            internal static readonly int BlueNoiseJitterId = Shader.PropertyToID("_HectonShaftBlueNoiseJitter");
            internal static readonly int BilateralDepthSigmaId = Shader.PropertyToID("_HectonShaftBilateralDepthSigma");
            internal static readonly int ShaftIntensityId = Shader.PropertyToID("_HectonShaftIntensity");
            internal static readonly int BiolumPatternScaleId = Shader.PropertyToID("_HectonBiolumPatternScale");
            internal static readonly int BiolumProjectionStrengthId = Shader.PropertyToID("_HectonBiolumProjectionStrength");
            internal static readonly int SiltStrengthId = Shader.PropertyToID("_HectonSiltStrength");
            internal static readonly int SiltNoiseScaleId = Shader.PropertyToID("_HectonSiltNoiseScale");
            internal static readonly int SiltFloorBoostId = Shader.PropertyToID("_HectonSiltFloorBoost");
            internal static readonly int SiltDriftSpeedId = Shader.PropertyToID("_HectonSiltDriftSpeed");
            internal static readonly int BlueNoiseTextureId = Shader.PropertyToID("_BlueNoiseTex");
            internal static readonly int HasBlueNoiseTextureId = Shader.PropertyToID("_HectonHasBlueNoiseTex");
            internal static readonly int ShaftTextureId = Shader.PropertyToID("_HectonShaftsTexture");
            internal static readonly int HeadlightVolumetricsTextureId = Shader.PropertyToID("_HectonHeadlightVolumetrics");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ShaftsPass _pass;
        private Material _raymarchMaterial;
        private Material _blurHorizontalMaterial;
        private Material _blurVerticalMaterial;
        private Material _compositeMaterial;

        /// <inheritdoc />
        public override void Create()
        {
            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/ScooterVolumetricShafts");

            if (_pass == null)
                _pass = new ShaftsPass();

            RecreateMaterial(ref _raymarchMaterial, shader);
            RecreateMaterial(ref _blurHorizontalMaterial, shader);
            RecreateMaterial(ref _blurVerticalMaterial, shader);
            RecreateMaterial(ref _compositeMaterial, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _pass == null ||
                _raymarchMaterial == null ||
                _blurHorizontalMaterial == null ||
                _blurVerticalMaterial == null ||
                _compositeMaterial == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            if (Shader.GetGlobalInt(ShaderConstants.HeadlightCountId) <= 0 &&
                Shader.GetGlobalFloat(ShaderConstants.FloorBiolumStrengthId) <= 0.0001f)
            {
                return;
            }

            _pass.Setup(settings, _raymarchMaterial, _blurHorizontalMaterial, _blurVerticalMaterial, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_raymarchMaterial);
            CoreUtils.Destroy(_blurHorizontalMaterial);
            CoreUtils.Destroy(_blurVerticalMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _raymarchMaterial = null;
            _blurHorizontalMaterial = null;
            _blurVerticalMaterial = null;
            _compositeMaterial = null;
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
