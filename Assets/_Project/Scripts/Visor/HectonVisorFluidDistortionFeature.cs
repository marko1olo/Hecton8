using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
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
    /// Fullscreen visor droplet and leak distortion driven by the active player wet-lens and hull-stress signals.
    /// </summary>
    public sealed class HectonVisorFluidDistortionFeature : ScriptableRendererFeature
    {
        private const float ThermalDistortionCullSpeedMetersPerSecond = 15f;
        private const float ThermalDistortionCullSpeedMetersPerSecondSq = ThermalDistortionCullSpeedMetersPerSecond * ThermalDistortionCullSpeedMetersPerSecond;
        private const float HullStressVisorContributionStart01 = 0.65f;
        private const float HullStressVisorContributionInvRange = 1f / (1f - HullStressVisorContributionStart01);
        private const float VisorSpeedSquaredToShader01 = 0.0016f;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader";
        private const string BlueNoiseAssetPath = "Assets/_Project/Art/TEXTURES/Utility/TX_BlueNoise_256_R8.png";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for procedural visor droplets and hull-stress leaks.")]
            public Shader shader = null;

            [Tooltip("Injection point for the visor distortion. Before post-processing keeps the effect inside the validated noir stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum UV refraction applied by the procedural fluid mask.")]
            [Range(0f, 0.04f)] public float distortionStrength = 0.012f;

            [Tooltip("Base vertical runoff speed for droplets sliding down the visor.")]
            [Range(0.1f, 4f)] public float runoffSpeed = 1.2f;

            [Tooltip("Base droplet tiling density across the visor.")]
            [Range(2f, 24f)] public float dropletScale = 8f;

            [Tooltip("How strongly camera-relative sideways velocity shears the runoff field.")]
            [Range(0f, 1f)] public float lateralStreakStrength = 0.42f;

            [Tooltip("How strongly forward speed elongates and densifies the streak field.")]
            [Range(0f, 1f)] public float forwardStretchStrength = 0.28f;

            [Tooltip("How strongly high speed pushes droplets radially toward the visor edges.")]
            [Range(0f, 1f)] public float edgeStreakStrength = 0.46f;

            [Tooltip("How much hull stress contributes even when the player is otherwise dry.")]
            [Range(0f, 1f)] public float hullStressContribution = 0.8f;

            [Tooltip("Viewport edge fade used to keep the center readable while droplets accumulate on the visor rim.")]
            [Range(0.1f, 4f)] public float edgeFadeExponent = 1.35f;

            [Tooltip("Single low-resolution blue-noise mask used to distribute visor dust and condensation breakup.")]
            public Texture2D blueNoiseTexture = null;

            [Tooltip("Dust visibility added by ambient light on the visor layer.")]
            [Range(0f, 1f)] public float dustStrength = 0.28f;

            [Tooltip("How aggressively ambient light exposes visor dust.")]
            [Range(0f, 4f)] public float ambientDustResponse = 1.45f;

            [Tooltip("Pixel size of the repeating blue-noise source. Kept explicit so the shader can tile without a second lookup.")]
            [Range(16f, 512f)] public float blueNoiseTilePixels = 256f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(float wetness, float hullStress, Vector3 localVelocity, float ambientLight01, float effectIntensity, float rainIntensity, float thermalMotionCull01)
            {
                Wetness = wetness;
                HullStress = hullStress;
                LocalVelocity = localVelocity;
                AmbientLight01 = ambientLight01;
                EffectIntensity = effectIntensity;
                RainIntensity = rainIntensity;
                ThermalMotionCull01 = thermalMotionCull01;
            }

            public float Wetness { get; }
            public float HullStress { get; }
            public Vector3 LocalVelocity { get; }
            public float AmbientLight01 { get; }
            public float EffectIntensity { get; }
            public float RainIntensity { get; }
            public float ThermalMotionCull01 { get; }
        }

        private sealed class VisorFluidPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Fluid Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;

            public VisorFluidPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, RuntimeState runtimeState)
            {
                _settings = settings;
                _material = material;
                _runtimeState = runtimeState;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _material == null ||
                    (_runtimeState.EffectIntensity <= 0.001f && _runtimeState.RainIntensity <= 0.001f))
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView)
                {
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonVisorFluidDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialParameters(_material, _settings, _runtimeState);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Visor Fluid Distortion", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.destination = destinationTexture;
                    passData.material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(destinationTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(
                            cmd,
                            data.source,
                            data.destination,
                            RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store,
                            data.material,
                            0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private static void UpdateMaterialParameters(Material material, FeatureSettings settings, RuntimeState runtimeState)
            {
                Vector3 localVelocity = runtimeState.LocalVelocity;
                float lateralVelocity = Mathf.Clamp(localVelocity.x * 0.08f, -1f, 1f);
                float forwardVelocity = Mathf.Clamp(localVelocity.z * 0.05f, -1f, 1f);
                float verticalVelocity = Mathf.Clamp(localVelocity.y * 0.08f, -1f, 1f);

                material.SetFloat(ShaderConstants.IntensityId, runtimeState.EffectIntensity);
                material.SetFloat(ShaderConstants.RainIntensityId, runtimeState.RainIntensity);
                material.SetFloat(ShaderConstants.WetnessId, runtimeState.Wetness);
                material.SetFloat(ShaderConstants.HullStressId, runtimeState.HullStress);
                material.SetFloat(ShaderConstants.DistortionStrengthId, Mathf.Max(0f, settings.distortionStrength));
                material.SetFloat(ShaderConstants.RunoffSpeedId, Mathf.Max(0.1f, settings.runoffSpeed));
                material.SetFloat(ShaderConstants.DropletScaleId, Mathf.Max(2f, settings.dropletScale));
                material.SetFloat(ShaderConstants.LateralStreakStrengthId, Mathf.Clamp01(settings.lateralStreakStrength));
                material.SetFloat(ShaderConstants.ForwardStretchStrengthId, Mathf.Clamp01(settings.forwardStretchStrength));
                material.SetFloat(ShaderConstants.EdgeStreakStrengthId, Mathf.Clamp01(settings.edgeStreakStrength));
                material.SetFloat(ShaderConstants.EdgeFadeExponentId, Mathf.Max(0.1f, settings.edgeFadeExponent));
                float speed01 = math.saturate(localVelocity.sqrMagnitude * VisorSpeedSquaredToShader01);
                material.SetFloat(ShaderConstants.SpeedId, speed01);
                material.SetVector(ShaderConstants.LocalVelocityId, new Vector4(lateralVelocity, verticalVelocity, forwardVelocity, 0f));
                material.SetFloat(ShaderConstants.ThermalMotionCullId, runtimeState.ThermalMotionCull01);
                material.SetFloat(ShaderConstants.AmbientLightId, runtimeState.AmbientLight01);
                material.SetFloat(ShaderConstants.DustStrengthId, Mathf.Clamp01(settings.dustStrength));
                material.SetFloat(ShaderConstants.AmbientDustResponseId, Mathf.Max(0f, settings.ambientDustResponse));
                material.SetFloat(ShaderConstants.BlueNoiseTilePixelsId, Mathf.Max(16f, settings.blueNoiseTilePixels));
                material.SetFloat(ShaderConstants.HasBlueNoiseId, settings.blueNoiseTexture != null ? 1f : 0f);
                if (settings.blueNoiseTexture != null)
                    material.SetTexture(ShaderConstants.BlueNoiseTexId, settings.blueNoiseTexture);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonVisorFluidIntensity");
            internal static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
            internal static readonly int WetnessId = Shader.PropertyToID("_HectonVisorFluidWetness");
            internal static readonly int HullStressId = Shader.PropertyToID("_HectonVisorFluidHullStress");
            internal static readonly int DistortionStrengthId = Shader.PropertyToID("_HectonVisorFluidDistortionStrength");
            internal static readonly int RunoffSpeedId = Shader.PropertyToID("_HectonVisorFluidRunoffSpeed");
            internal static readonly int DropletScaleId = Shader.PropertyToID("_HectonVisorFluidDropletScale");
            internal static readonly int LateralStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidLateralStreakStrength");
            internal static readonly int ForwardStretchStrengthId = Shader.PropertyToID("_HectonVisorFluidForwardStretchStrength");
            internal static readonly int EdgeStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidEdgeStreakStrength");
            internal static readonly int EdgeFadeExponentId = Shader.PropertyToID("_HectonVisorFluidEdgeFadeExponent");
            internal static readonly int SpeedId = Shader.PropertyToID("_HectonVisorFluidSpeed");
            internal static readonly int LocalVelocityId = Shader.PropertyToID("_HectonVisorFluidLocalVelocity");
            internal static readonly int ThermalMotionCullId = Shader.PropertyToID("_HectonThermalDistortionMotionCull");
            internal static readonly int AmbientLightId = Shader.PropertyToID("_HectonVisorFluidAmbientLight");
            internal static readonly int DustStrengthId = Shader.PropertyToID("_HectonVisorFluidDustStrength");
            internal static readonly int AmbientDustResponseId = Shader.PropertyToID("_HectonVisorFluidAmbientDustResponse");
            internal static readonly int BlueNoiseTilePixelsId = Shader.PropertyToID("_HectonVisorFluidBlueNoiseTilePixels");
            internal static readonly int HasBlueNoiseId = Shader.PropertyToID("_HectonVisorFluidHasBlueNoise");
            internal static readonly int BlueNoiseTexId = Shader.PropertyToID("_HectonVisorFluidBlueNoiseTex");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VisorFluidPass _pass;
        private Material _material;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            if (settings != null && settings.blueNoiseTexture == null)
                settings.blueNoiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BlueNoiseAssetPath);
#endif

            _pass ??= new VisorFluidPass();
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            Camera renderCamera = renderingData.cameraData.camera;
            if (!TryBuildRuntimeState(renderCamera, settings, out RuntimeState runtimeState))
                return;

            _pass.Setup(settings, _material, runtimeState);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private static bool TryBuildRuntimeState(
            Camera renderCamera,
            FeatureSettings settings,
            out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || settings == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return false;

            Camera playerCamera = playerContext.PlayerCamera;
            HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            float wetness = playerMovement != null ? Mathf.Clamp01(playerMovement.CurrentWetLensIntensity01) : 0f;
            float hullStress = playerMovement != null ? Mathf.Clamp01(playerMovement.CurrentHullStress01) : 0f;
            float ambientLight01 = ResolveAmbientLight01();
            float hullContribution = math.saturate(
                math.saturate((hullStress - HullStressVisorContributionStart01) * HullStressVisorContributionInvRange) *
                math.saturate(settings.hullStressContribution));
            float dustContribution = math.saturate(ambientLight01 * math.saturate(settings.dustStrength) * math.max(0f, settings.ambientDustResponse));
            float effectIntensity = math.saturate(math.max(math.max(wetness, hullContribution), dustContribution));
            float rainIntensity = Mathf.Clamp01(Shader.GetGlobalFloat(ShaderConstants.RainIntensityId));
            if (effectIntensity <= 0.001f && rainIntensity <= 0.001f)
                return false;

            Vector3 localVelocity = playerMovement != null
                ? ResolveCameraLocalVelocity(playerCamera.transform, playerMovement.InterpolatedLinearVelocity)
                : Vector3.zero;
            float thermalMotionCull01 = localVelocity.sqrMagnitude > ThermalDistortionCullSpeedMetersPerSecondSq ? 1f : 0f;
            runtimeState = new RuntimeState(wetness, hullStress, localVelocity, ambientLight01, effectIntensity, rainIntensity, thermalMotionCull01);
            return true;
        }

        private static Vector3 ResolveCameraLocalVelocity(Transform cameraTransform, Vector3 worldVelocity)
        {
            if (cameraTransform == null)
                return Vector3.zero;

            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;
            Vector3 cameraForward = cameraTransform.forward;
            return new Vector3(
                Vector3.Dot(worldVelocity, cameraRight),
                Vector3.Dot(worldVelocity, cameraUp),
                Vector3.Dot(worldVelocity, cameraForward));
        }

        private static float ResolveAmbientLight01()
        {
            Color ambientColor = RenderSettings.ambientLight.linear;
            float colorIntensity = Mathf.Max(ambientColor.r, Mathf.Max(ambientColor.g, ambientColor.b));
            return Mathf.Clamp01(Mathf.Max(RenderSettings.ambientIntensity, colorIntensity));
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
