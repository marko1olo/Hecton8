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
            public RuntimeState(float health01, float critical01, float heartbeatBpm, float narcosis01)
            {
                Health01 = health01;
                Critical01 = critical01;
                HeartbeatBpm = heartbeatBpm;
                Narcosis01 = narcosis01;
            }

            public float Health01 { get; }
            public float Critical01 { get; }
            public float HeartbeatBpm { get; }
            public float Narcosis01 { get; }
        }

        private sealed class RetinaDistortionPass : ScriptableRenderPass
        {
            private const float MaterialFloatEpsilon = 0.0001f;

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
            private Material _lastParameterMaterial;
            private float _lastHealth01 = float.PositiveInfinity;
            private float _lastCritical01 = float.PositiveInfinity;
            private float _lastHeartbeatBpm = float.PositiveInfinity;
            private float _lastNarcosis01 = float.PositiveInfinity;
            private float _lastChromaticOffset = float.PositiveInfinity;
            private float _lastDistortionOffset = float.PositiveInfinity;
            private float _lastVignetteStrength = float.PositiveInfinity;
            private bool _lastMx350Tier;
            private bool _keywordStateInitialized;
            private bool _materialDirty = true;

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
                if (_settings == null ||
                    _material == null ||
                    math.max(_runtimeState.Critical01, _runtimeState.Narcosis01) <= 0.001f)
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

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
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

            private void UpdateMaterialParameters(Material material, FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!ReferenceEquals(_lastParameterMaterial, material))
                {
                    ResetMaterialParameterCache();
                    _lastParameterMaterial = material;
                }

                float critical01 = math.saturate(runtimeState.Critical01);
                float narcosis01 = math.saturate(runtimeState.Narcosis01);
                float drive01 = math.max(critical01, narcosis01);
                float health01 = math.saturate(runtimeState.Health01);
                float heartbeatBpm = math.max(1f, runtimeState.HeartbeatBpm);
                RetinaOffsetBudget offsetBudget = ResolveRetinaOffsetBudget(
                    math.max(0f, settings.maxChromaticOffset),
                    math.max(0f, settings.maxDistortionOffset),
                    drive01,
                    SystemInfo.graphicsMemorySize);
                bool mx350Tier = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048;
                float vignetteStrength = math.saturate(settings.maxVignetteStrength) * math.max(critical01, narcosis01 * 0.62f);

                SetMx350KeywordIfChanged(material, mx350Tier);
                SetMaterialFloatIfChanged(material, ShaderConstants.HealthId, health01, ref _lastHealth01);
                SetMaterialFloatIfChanged(material, ShaderConstants.CriticalId, critical01, ref _lastCritical01);
                SetMaterialFloatIfChanged(material, ShaderConstants.HeartbeatBpmId, heartbeatBpm, ref _lastHeartbeatBpm);
                SetMaterialFloatIfChanged(material, ShaderConstants.NarcosisId, narcosis01, ref _lastNarcosis01);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.ChromaticOffsetId,
                    offsetBudget.ChromaticOffset,
                    ref _lastChromaticOffset);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.DistortionOffsetId,
                    offsetBudget.DistortionOffset,
                    ref _lastDistortionOffset);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.VignetteStrengthId,
                    vignetteStrength,
                    ref _lastVignetteStrength);
                _materialDirty = false;
            }

            private void ResetMaterialParameterCache()
            {
                _lastHealth01 = float.PositiveInfinity;
                _lastCritical01 = float.PositiveInfinity;
                _lastHeartbeatBpm = float.PositiveInfinity;
                _lastNarcosis01 = float.PositiveInfinity;
                _lastChromaticOffset = float.PositiveInfinity;
                _lastDistortionOffset = float.PositiveInfinity;
                _lastVignetteStrength = float.PositiveInfinity;
                _keywordStateInitialized = false;
                _materialDirty = true;
            }

            private void SetMx350KeywordIfChanged(Material material, bool mx350Tier)
            {
                if (!_materialDirty && _keywordStateInitialized && _lastMx350Tier == mx350Tier)
                    return;

                if (mx350Tier)
                    material.EnableKeyword(ShaderConstants.Mx350Keyword);
                else
                    material.DisableKeyword(ShaderConstants.Mx350Keyword);

                _lastMx350Tier = mx350Tier;
                _keywordStateInitialized = true;
            }

            private void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue)
            {
                if (!_materialDirty && math.abs(cachedValue - value) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HealthId = Shader.PropertyToID("_HectonRetinaHealth01");
            internal static readonly int CriticalId = Shader.PropertyToID("_HectonRetinaCritical01");
            internal static readonly int HeartbeatBpmId = Shader.PropertyToID("_HectonRetinaHeartbeatBpm");
            internal static readonly int NarcosisId = Shader.PropertyToID("_HectonNarcosisScalar");
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

            float narcosis01 = math.saturate(Shader.GetGlobalFloat(ShaderConstants.NarcosisId));
            float threshold = math.clamp(settings.healthThreshold01, 0.01f, 0.35f);
            bool hasHealth = UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot);
            float health01 = hasHealth ? math.saturate(healthSlot.Value) : 1f;
            float critical01 = hasHealth && health01 < threshold
                ? math.saturate((threshold - health01) * math.rcp(threshold))
                : 0f;
            if (math.max(critical01, narcosis01) <= 0.001f)
                return false;

            float drive01 = critical01 * critical01 * (3f - 2f * critical01);
            float baseBpm = math.max(1f, settings.baseHeartbeatBpm);
            float criticalBpm = math.max(baseBpm, settings.criticalHeartbeatBpm);
            runtimeState = new RuntimeState(health01, drive01, math.lerp(baseBpm, criticalBpm, drive01), narcosis01);
            return true;
        }

        internal static RetinaOffsetBudget ResolveRetinaOffsetBudget(
            float maxChromaticOffset,
            float maxDistortionOffset,
            float critical01,
            int graphicsMemoryMb)
        {
            float clampedCritical = math.saturate(critical01);
            bool mx350Tier = graphicsMemoryMb > 0 && graphicsMemoryMb <= 2048;
            if (mx350Tier)
            {
                return new RetinaOffsetBudget(
                    math.max(0f, maxChromaticOffset) * clampedCritical,
                    0f);
            }

            return new RetinaOffsetBudget(
                0f,
                math.max(0f, maxDistortionOffset) * clampedCritical);
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
