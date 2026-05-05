using System;
using Hecton8.Core;
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
    /// Health-critical fullscreen retina distortion pass driven by the player heartbeat cadence.
    /// </summary>
    public sealed class HectonRetinaDistortionFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_RetinaDistortion.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for health-critical retina distortion.")]
            public Shader shader = null;

            [Tooltip("Injection point for health-critical retina distortion. Before post-processing keeps the effect inside the noir stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Health threshold below which the retina distortion pass becomes active.")]
            [Range(0.01f, 0.35f)] public float healthThreshold01 = 0.15f;

            [Tooltip("Resting heartbeat cadence used when the health gate first opens.")]
            [Range(30f, 90f)] public float baseHeartbeatBpm = 54f;

            [Tooltip("Maximum heartbeat cadence reached at terminal health.")]
            [Range(90f, 180f)] public float criticalHeartbeatBpm = 124f;

            [Tooltip("Maximum radial chromatic split in normalized screen UV units.")]
            [Range(0f, 0.008f)] public float maxChromaticOffset = 0.0038f;

            [Tooltip("Maximum radial screen-space distortion in normalized screen UV units.")]
            [Range(0f, 0.03f)] public float maxDistortionOffset = 0.014f;

            [Tooltip("Maximum edge darkening applied at terminal health.")]
            [Range(0f, 0.6f)] public float maxVignetteStrength = 0.28f;
        }

        internal readonly struct RetinaOffsetBudget
        {
            internal RetinaOffsetBudget(float chromaticOffset, float distortionOffset)
            {
                ChromaticOffset = chromaticOffset;
                DistortionOffset = distortionOffset;
            }

            internal float ChromaticOffset { get; }
            internal float DistortionOffset { get; }
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(float health01, float critical01, float heartbeatBpm)
            {
                Health01 = health01;
                Critical01 = critical01;
                HeartbeatBpm = heartbeatBpm;
            }

            public float Health01 { get; }
            public float Critical01 { get; }
            public float HeartbeatBpm { get; }
        }

        private sealed class RetinaDistortionPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Retina Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;

            public RetinaDistortionPass()
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
                if (_settings == null || _material == null || _runtimeState.Critical01 <= 0.001f)
                    return;

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
                destinationDesc.name = "_HectonRetinaDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialParameters(_material, _settings, _runtimeState);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Retina Distortion", out PassData passData, _profilingSampler))
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
                float critical01 = Mathf.Clamp01(runtimeState.Critical01);
                material.SetFloat(ShaderConstants.HealthId, Mathf.Clamp01(runtimeState.Health01));
                material.SetFloat(ShaderConstants.CriticalId, critical01);
                material.SetFloat(ShaderConstants.HeartbeatBpmId, Mathf.Max(1f, runtimeState.HeartbeatBpm));
                RetinaOffsetBudget offsetBudget = ResolveRetinaOffsetBudget(
                    Mathf.Max(0f, settings.maxChromaticOffset),
                    Mathf.Max(0f, settings.maxDistortionOffset),
                    critical01,
                    SystemInfo.graphicsMemorySize);
                bool mx350Tier = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048;
                if (mx350Tier)
                    material.EnableKeyword(ShaderConstants.Mx350Keyword);
                else
                    material.DisableKeyword(ShaderConstants.Mx350Keyword);
                material.SetFloat(ShaderConstants.ChromaticOffsetId, offsetBudget.ChromaticOffset);
                material.SetFloat(ShaderConstants.DistortionOffsetId, offsetBudget.DistortionOffset);
                material.SetFloat(ShaderConstants.VignetteStrengthId, Mathf.Clamp01(settings.maxVignetteStrength) * critical01);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HealthId = Shader.PropertyToID("_HectonRetinaHealth01");
            internal static readonly int CriticalId = Shader.PropertyToID("_HectonRetinaCritical01");
            internal static readonly int HeartbeatBpmId = Shader.PropertyToID("_HectonRetinaHeartbeatBpm");
            internal static readonly int ChromaticOffsetId = Shader.PropertyToID("_HectonRetinaChromaticOffset");
            internal static readonly int DistortionOffsetId = Shader.PropertyToID("_HectonRetinaDistortionOffset");
            internal static readonly int VignetteStrengthId = Shader.PropertyToID("_HectonRetinaVignetteStrength");
            internal const string Mx350Keyword = "_QUALITY_MX350";
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private RetinaDistortionPass _pass;
        private Material _material;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new RetinaDistortionPass();
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
            if (renderCamera == null || settings == null || !UIStateStore.IsInitialized)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            if (!UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot))
                return false;

            float threshold = Mathf.Clamp(settings.healthThreshold01, 0.01f, 0.35f);
            float health01 = Mathf.Clamp01(healthSlot.Value);
            if (health01 >= threshold)
                return false;

            float critical01 = Mathf.Clamp01((threshold - health01) / threshold);
            float drive01 = critical01 * critical01 * (3f - 2f * critical01);
            float baseBpm = Mathf.Max(1f, settings.baseHeartbeatBpm);
            float criticalBpm = Mathf.Max(baseBpm, settings.criticalHeartbeatBpm);
            runtimeState = new RuntimeState(health01, drive01, Mathf.Lerp(baseBpm, criticalBpm, drive01));
            return true;
        }

        internal static RetinaOffsetBudget ResolveRetinaOffsetBudget(
            float maxChromaticOffset,
            float maxDistortionOffset,
            float critical01,
            int graphicsMemoryMb)
        {
            float clampedCritical = Mathf.Clamp01(critical01);
            bool mx350Tier = graphicsMemoryMb > 0 && graphicsMemoryMb <= 2048;
            if (mx350Tier)
            {
                return new RetinaOffsetBudget(
                    Mathf.Max(0f, maxChromaticOffset) * clampedCritical,
                    0f);
            }

            return new RetinaOffsetBudget(
                0f,
                Mathf.Max(0f, maxDistortionOffset) * clampedCritical);
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
