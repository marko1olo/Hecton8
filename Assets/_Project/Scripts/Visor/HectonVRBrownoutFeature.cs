using System;
using Hecton8.Core;
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
    /// Single fullscreen VR visor brownout and focus blur pass.
    /// </summary>
    public sealed class HectonVRBrownoutFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_VRBrownout.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for BIOS green brownout and dynamic focus blur.")]
            public Shader shader = null;

            [Tooltip("Injection point. Before post keeps the pass inside the validated visor stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum world blur UV radius in source texels.")]
            [Range(0.25f, 3f)] public float worldBlurTexelRadius = 1.65f;

            [Tooltip("Scanline contrast applied at full brownout.")]
            [Range(0f, 1f)] public float scanlineStrength = 0.55f;

            [Tooltip("Ordered/noise dither strength applied at full brownout.")]
            [Range(0f, 1f)] public float ditherStrength = 0.85f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(float brownoutIntensity, float worldFocusBlur, float nearCollisionIntensity)
            {
                BrownoutIntensity = brownoutIntensity;
                WorldFocusBlur = worldFocusBlur;
                NearCollisionIntensity = nearCollisionIntensity;
            }

            public float BrownoutIntensity { get; }
            public float WorldFocusBlur { get; }
            public float NearCollisionIntensity { get; }
        }

        private sealed class BrownoutPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton VR Brownout");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;

            public BrownoutPass()
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
                    (_runtimeState.BrownoutIntensity <= 0.001f &&
                     _runtimeState.WorldFocusBlur <= 0.001f &&
                     _runtimeState.NearCollisionIntensity <= 0.001f))
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
                destinationDesc.name = "_HectonVRBrownout";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialParameters(_material, _settings, _runtimeState);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton VR Brownout", out PassData passData, _profilingSampler))
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
                material.SetFloat(ShaderConstants.BrownoutIntensityId, math.saturate(runtimeState.BrownoutIntensity));
                material.SetFloat(ShaderConstants.WorldFocusBlurId, math.saturate(runtimeState.WorldFocusBlur));
                material.SetFloat(ShaderConstants.NearCollisionIntensityId, math.saturate(runtimeState.NearCollisionIntensity));
                material.SetFloat(ShaderConstants.WorldBlurTexelRadiusId, math.max(0f, settings.worldBlurTexelRadius));
                material.SetFloat(ShaderConstants.ScanlineStrengthId, math.saturate(settings.scanlineStrength));
                material.SetFloat(ShaderConstants.DitherStrengthId, math.saturate(settings.ditherStrength));
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BrownoutIntensityId = Shader.PropertyToID("_HectonVRBrownoutIntensity");
            internal static readonly int WorldFocusBlurId = Shader.PropertyToID("_HectonWorldFocusBlur");
            internal static readonly int NearCollisionIntensityId = Shader.PropertyToID("_HectonVRNearCollisionIntensity");
            internal static readonly int WorldBlurTexelRadiusId = Shader.PropertyToID("_HectonWorldBlurTexelRadius");
            internal static readonly int ScanlineStrengthId = Shader.PropertyToID("_HectonVRBrownoutScanlineStrength");
            internal static readonly int DitherStrengthId = Shader.PropertyToID("_HectonVRBrownoutDitherStrength");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private BrownoutPass _pass;
        private Material _material;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new BrownoutPass();
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
            if (!TryBuildRuntimeState(renderCamera, out RuntimeState runtimeState))
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

        private static bool TryBuildRuntimeState(Camera renderCamera, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera != null && !ReferenceEquals(renderCamera, playerCamera))
                return false;

            float brownoutIntensity = math.saturate(Shader.GetGlobalFloat(ShaderConstants.BrownoutIntensityId));
            float worldFocusBlur = math.saturate(Shader.GetGlobalFloat(ShaderConstants.WorldFocusBlurId));
            float nearCollisionIntensity = math.saturate(Shader.GetGlobalFloat(ShaderConstants.NearCollisionIntensityId));
            if (brownoutIntensity <= 0.001f && worldFocusBlur <= 0.001f && nearCollisionIntensity <= 0.001f)
                return false;

            runtimeState = new RuntimeState(brownoutIntensity, worldFocusBlur, nearCollisionIntensity);
            return true;
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
