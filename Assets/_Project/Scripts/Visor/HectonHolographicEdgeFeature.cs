using System;
using Hecton8.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Draws one procedural holographic edge shell for scan-flagged renderers.
    /// </summary>
    public sealed class HectonHolographicEdgeFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_HolographicEdge.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            public Shader shader = null;
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;
            public Color edgeColor = new Color(0.05f, 0.95f, 1f, 0.82f);
            [Min(0.0001f)] public float shellOffset = 0.024f;
            [Min(0.1f)] public float flickerSpeed = 42f;
            [Range(0f, 0.95f)] public float flickerCutoff = 0.34f;
            [Range(0.5f, 8f)] public float edgePower = 2.6f;
            [Range(0f, 2f)] public float scanlineStrength = 0.55f;
            [Min(1)] public int maxDrawnTargets = HectonScanRenderRegistry.MaxTargets;
            public int requiredFlags = (int)HectonScanRenderFlags.IsScanned;
        }

        private sealed class HolographicEdgePass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle color;
                internal TextureHandle depth;
                internal Material material;
                internal uint requiredFlags;
                internal int maxDrawnTargets;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Holographic Edge");
            private FeatureSettings _settings;
            private Material _material;

            public HolographicEdgePass()
            {
                profilingSampler = _profilingSampler;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                uint requiredFlags = (uint)Mathf.Max(1, _settings.requiredFlags);
                if (!HectonScanRenderRegistry.HasRegisteredFlags(requiredFlags))
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                TextureHandle colorTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!colorTexture.IsValid() || !depthTexture.IsValid())
                    return;

                UpdateMaterial(_material, _settings);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Holographic Edge", out PassData passData, _profilingSampler))
                {
                    passData.color = colorTexture;
                    passData.depth = depthTexture;
                    passData.material = _material;
                    passData.requiredFlags = requiredFlags;
                    passData.maxDrawnTargets = Mathf.Clamp(_settings.maxDrawnTargets, 1, HectonScanRenderRegistry.MaxTargets);

                    builder.UseTexture(colorTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        CoreUtils.SetRenderTarget(cmd, data.color, data.depth, ClearFlag.None);
                        HectonScanRenderRegistry.DrawRenderers(cmd, data.material, data.requiredFlags, data.maxDrawnTargets);
                    });
                }
            }

            private static void UpdateMaterial(Material material, FeatureSettings settings)
            {
                material.SetColor(ShaderConstants.BaseColorId, settings.edgeColor);
                material.SetFloat(ShaderConstants.ShellOffsetId, Mathf.Max(0.0001f, settings.shellOffset));
                material.SetFloat(ShaderConstants.FlickerSpeedId, Mathf.Max(0.1f, settings.flickerSpeed));
                material.SetFloat(ShaderConstants.FlickerCutoffId, Mathf.Clamp01(settings.flickerCutoff));
                material.SetFloat(ShaderConstants.EdgePowerId, Mathf.Max(0.5f, settings.edgePower));
                material.SetFloat(ShaderConstants.ScanlineStrengthId, Mathf.Clamp(settings.scanlineStrength, 0f, 2f));
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            internal static readonly int ShellOffsetId = Shader.PropertyToID("_ShellOffset");
            internal static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
            internal static readonly int FlickerCutoffId = Shader.PropertyToID("_FlickerCutoff");
            internal static readonly int EdgePowerId = Shader.PropertyToID("_EdgePower");
            internal static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private HolographicEdgePass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new HolographicEdgePass();
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        }
    }
}
