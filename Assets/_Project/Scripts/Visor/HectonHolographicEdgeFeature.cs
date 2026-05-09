using System;
using Hecton8.Gameplay;
using Unity.Mathematics;
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
            private uint _requiredFlags;
            private int _maxDrawnTargets;

            public HolographicEdgePass()
            {
                profilingSampler = _profilingSampler;
            }

            public void Setup(FeatureSettings settings, Material material, uint requiredFlags, int maxDrawnTargets)
            {
                _settings = settings;
                _material = material;
                _requiredFlags = requiredFlags;
                _maxDrawnTargets = maxDrawnTargets;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
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

                TextureHandle colorTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!colorTexture.IsValid() || !depthTexture.IsValid())
                    return;

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Holographic Edge", out PassData passData, _profilingSampler))
                {
                    passData.color = colorTexture;
                    passData.depth = depthTexture;
                    passData.material = _material;
                    passData.requiredFlags = _requiredFlags;
                    passData.maxDrawnTargets = _maxDrawnTargets;

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
        private Color _appliedEdgeColor;
        private float _appliedShellOffset;
        private float _appliedFlickerSpeed;
        private float _appliedFlickerCutoff;
        private float _appliedEdgePower;
        private float _appliedScanlineStrength;
        private uint _targetProbeFlags;
        private int _targetProbeFrame = -1;
        private bool _targetProbeResult;
        private bool _edgeMaterialDirty = true;

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
            _edgeMaterialDirty = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            if (settings.edgeColor.a <= 0.001f || settings.maxDrawnTargets <= 0)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            uint requiredFlags = (uint)math.max(1, settings.requiredFlags);
            int maxDrawnTargets = math.clamp(settings.maxDrawnTargets, 1, HectonScanRenderRegistry.MaxTargets);
            if (!HasTargetsForCurrentFrame(requiredFlags))
                return;

            UpdateEdgeMaterialIfNeeded();
            _pass.Setup(settings, _material, requiredFlags, maxDrawnTargets);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private void UpdateEdgeMaterialIfNeeded()
        {
            float shellOffset = math.max(0.0001f, settings.shellOffset);
            float flickerSpeed = math.max(0.1f, settings.flickerSpeed);
            float flickerCutoff = math.saturate(settings.flickerCutoff);
            float edgePower = math.max(0.5f, settings.edgePower);
            float scanlineStrength = math.clamp(settings.scanlineStrength, 0f, 2f);
            if (!_edgeMaterialDirty &&
                ColorDistanceSq(_appliedEdgeColor, settings.edgeColor) <= 0.000001f &&
                math.abs(_appliedShellOffset - shellOffset) <= 0.0005f &&
                math.abs(_appliedFlickerSpeed - flickerSpeed) <= 0.0005f &&
                math.abs(_appliedFlickerCutoff - flickerCutoff) <= 0.0005f &&
                math.abs(_appliedEdgePower - edgePower) <= 0.0005f &&
                math.abs(_appliedScanlineStrength - scanlineStrength) <= 0.0005f)
            {
                return;
            }

            _material.SetColor(ShaderConstants.BaseColorId, settings.edgeColor);
            _material.SetFloat(ShaderConstants.ShellOffsetId, shellOffset);
            _material.SetFloat(ShaderConstants.FlickerSpeedId, flickerSpeed);
            _material.SetFloat(ShaderConstants.FlickerCutoffId, flickerCutoff);
            _material.SetFloat(ShaderConstants.EdgePowerId, edgePower);
            _material.SetFloat(ShaderConstants.ScanlineStrengthId, scanlineStrength);
            _appliedEdgeColor = settings.edgeColor;
            _appliedShellOffset = shellOffset;
            _appliedFlickerSpeed = flickerSpeed;
            _appliedFlickerCutoff = flickerCutoff;
            _appliedEdgePower = edgePower;
            _appliedScanlineStrength = scanlineStrength;
            _edgeMaterialDirty = false;
        }

        private bool HasTargetsForCurrentFrame(uint requiredFlags)
        {
            int frame = Time.frameCount;
            if (_targetProbeFrame == frame && _targetProbeFlags == requiredFlags)
                return _targetProbeResult;

            _targetProbeFrame = frame;
            _targetProbeFlags = requiredFlags;
            _targetProbeResult = HectonScanRenderRegistry.HasAnyTargetWithFlags(requiredFlags);
            return _targetProbeResult;
        }

        private static float ColorDistanceSq(Color a, Color b)
        {
            float r = a.r - b.r;
            float g = a.g - b.g;
            float bl = a.b - b.b;
            float alpha = a.a - b.a;
            return r * r + g * g + bl * bl + alpha * alpha;
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
