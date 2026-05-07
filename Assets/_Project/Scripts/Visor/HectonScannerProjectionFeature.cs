using System;
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
    /// Fullscreen depth projection for scanner pulses. The shader reconstructs world position from depth and applies a 2D dither projector.
    /// </summary>
    public sealed class HectonScannerProjectionFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_ScannerDepthProjection.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            public Shader shader = null;
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Color projectionColor = new Color(0.08f, 0.95f, 1f, 0.72f);
            [Min(4f)] public float gridScale = 36f;
            [Range(0f, 1f)] public float ditherCutoff = 0.42f;
            [Min(0.1f)] public float flickerSpeed = 38f;
            [Min(0.001f)] public float projectionDepthMeters = 38f;
        }

        private sealed class ProjectionPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Scanner Projection");
            private FeatureSettings _settings;
            private Material _material;
            private HectonScannerProjectionState.RuntimeState _state;

            public ProjectionPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, in HectonScannerProjectionState.RuntimeState state)
            {
                _settings = settings;
                _material = material;
                _state = state;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonScannerDepthProjection";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterial(_material, _settings, _state);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Scanner Projection", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = destinationTexture;
                    passData.material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
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

            private static void UpdateMaterial(Material material, FeatureSettings settings, in HectonScannerProjectionState.RuntimeState state)
            {
                float now = Time.time;
                float age01 = math.saturate((now - state.StartTime) / math.max(0.001f, state.Duration));
                material.SetVector(ShaderConstants.OriginRadiusId, new Vector4(state.Origin.x, state.Origin.y, state.Origin.z, state.Radius));
                material.SetVector(ShaderConstants.RightDepthId, new Vector4(state.Right.x, state.Right.y, state.Right.z, math.max(0.001f, settings.projectionDepthMeters)));
                material.SetVector(ShaderConstants.UpAgeId, new Vector4(state.Up.x, state.Up.y, state.Up.z, age01));
                material.SetVector(ShaderConstants.ForwardIntensityId, new Vector4(state.Forward.x, state.Forward.y, state.Forward.z, state.Intensity));
                material.SetColor(ShaderConstants.ColorId, settings.projectionColor);
                material.SetFloat(ShaderConstants.GridScaleId, math.max(4f, settings.gridScale));
                material.SetFloat(ShaderConstants.DitherCutoffId, math.saturate(settings.ditherCutoff));
                material.SetFloat(ShaderConstants.FlickerSpeedId, math.max(0.1f, settings.flickerSpeed));
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int OriginRadiusId = Shader.PropertyToID("_HectonScannerProjectionOriginRadius");
            internal static readonly int RightDepthId = Shader.PropertyToID("_HectonScannerProjectionRightDepth");
            internal static readonly int UpAgeId = Shader.PropertyToID("_HectonScannerProjectionUpAge");
            internal static readonly int ForwardIntensityId = Shader.PropertyToID("_HectonScannerProjectionForwardIntensity");
            internal static readonly int ColorId = Shader.PropertyToID("_HectonScannerProjectionColor");
            internal static readonly int GridScaleId = Shader.PropertyToID("_HectonScannerProjectionGridScale");
            internal static readonly int DitherCutoffId = Shader.PropertyToID("_HectonScannerProjectionDitherCutoff");
            internal static readonly int FlickerSpeedId = Shader.PropertyToID("_HectonScannerProjectionFlickerSpeed");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ProjectionPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new ProjectionPass();
            Shader shader = settings != null ? settings.shader : null;
            RecreateMaterial(ref _material, shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            if (!HectonScannerProjectionState.TryGetState(Time.time, out HectonScannerProjectionState.RuntimeState state))
                return;

            _pass.Setup(settings, _material, in state);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
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
