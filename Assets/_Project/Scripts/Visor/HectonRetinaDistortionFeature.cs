using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Health-critical fullscreen retina distortion pass driven by the player heartbeat cadence.
    /// </summary>
    public sealed class HectonRetinaDistortionFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const int RetinaGlobalsStrideBytes = 32;

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

            internal readonly float ChromaticOffset;
            internal readonly float DistortionOffset;
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

            public readonly float Health01;
            public readonly float Critical01;
            public readonly float HeartbeatBpm;
            public readonly float Narcosis01;
        }

        private sealed class RetinaDistortionPass : ScriptableRenderPass
        {
            private const float GlobalsFloatEpsilon = 0.0001f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Retina Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _retinaGlobalsBuffer;
            private GraphicsBuffer _retinaGlobalsBufferA;
            private GraphicsBuffer _retinaGlobalsBufferB;
            private RetinaGlobalsDTO _lastRetinaGlobals;
            private int _retinaGlobalsWriteIndex;
            private bool _hasRetinaGlobals;
            private bool _lastMx350Tier;
            private bool _keywordStateInitialized;

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
                EnsureRetinaGlobalsBuffer();
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

                if (!UpdateRetinaGlobals(_settings, _runtimeState))
                    return;

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                    passName: "Hecton Retina Distortion");

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _retinaGlobalsBufferA?.Release();
                _retinaGlobalsBufferB?.Release();
                _retinaGlobalsBufferA = null;
                _retinaGlobalsBufferB = null;
                _retinaGlobalsBuffer = null;
                _retinaGlobalsWriteIndex = 0;
                _hasRetinaGlobals = false;
                _keywordStateInitialized = false;
            }

            private bool EnsureRetinaGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_retinaGlobalsBufferA != null && _retinaGlobalsBufferA.IsValid() &&
                    _retinaGlobalsBufferB != null && _retinaGlobalsBufferB.IsValid())
                {
                    if (_retinaGlobalsBuffer == null)
                        _retinaGlobalsBuffer = _retinaGlobalsBufferA;
                    return true;
                }

                _retinaGlobalsBufferA?.Release();
                _retinaGlobalsBufferB?.Release();
                _retinaGlobalsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    RetinaGlobalsStrideBytes);
                _retinaGlobalsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    RetinaGlobalsStrideBytes);
                _retinaGlobalsBuffer = _retinaGlobalsBufferA;
                _retinaGlobalsWriteIndex = 1;
                _hasRetinaGlobals = false;
                return _retinaGlobalsBufferA.IsValid() && _retinaGlobalsBufferB.IsValid();
            }

            private bool UpdateRetinaGlobals(FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!EnsureRetinaGlobalsBuffer())
                    return false;

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

                SetMx350KeywordIfChanged(mx350Tier);

                RetinaGlobalsDTO globals = new RetinaGlobalsDTO(
                    new Vector4(health01, critical01, heartbeatBpm, narcosis01),
                    new Vector4(offsetBudget.ChromaticOffset, offsetBudget.DistortionOffset, vignetteStrength, 0f));
                if (_hasRetinaGlobals && RetinaGlobalsEqual(in _lastRetinaGlobals, in globals))
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.RetinaGlobalsBufferId, _retinaGlobalsBuffer, 0, RetinaGlobalsStrideBytes);
                    return true;
                }

                GraphicsBuffer writeBuffer = (_retinaGlobalsWriteIndex & 1) == 0 ? _retinaGlobalsBufferA : _retinaGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                NativeArray<RetinaGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<RetinaGlobalsDTO>(0, 1);
                mapped[0] = globals;
                writeBuffer.UnlockBufferAfterWrite<RetinaGlobalsDTO>(1);
                _retinaGlobalsBuffer = writeBuffer;
                _retinaGlobalsWriteIndex ^= 1;
                _lastRetinaGlobals = globals;
                _hasRetinaGlobals = true;
                Shader.SetGlobalConstantBuffer(ShaderConstants.RetinaGlobalsBufferId, _retinaGlobalsBuffer, 0, RetinaGlobalsStrideBytes);
                return true;
            }

            private void SetMx350KeywordIfChanged(bool mx350Tier)
            {
                if (_keywordStateInitialized && _lastMx350Tier == mx350Tier)
                    return;

                if (mx350Tier)
                    Shader.EnableKeyword(ShaderConstants.Mx350Keyword);
                else
                    Shader.DisableKeyword(ShaderConstants.Mx350Keyword);

                _lastMx350Tier = mx350Tier;
                _keywordStateInitialized = true;
            }

            private static bool RetinaGlobalsEqual(in RetinaGlobalsDTO left, in RetinaGlobalsDTO right)
            {
                return Vector4Approximately(left.Params0, right.Params0) &&
                       Vector4Approximately(left.Params1, right.Params1);
            }

            private static bool Vector4Approximately(Vector4 left, Vector4 right)
            {
                return math.abs(left.x - right.x) <= GlobalsFloatEpsilon &&
                       math.abs(left.y - right.y) <= GlobalsFloatEpsilon &&
                       math.abs(left.z - right.z) <= GlobalsFloatEpsilon &&
                       math.abs(left.w - right.w) <= GlobalsFloatEpsilon;
            }

            [StructLayout(LayoutKind.Explicit, Size = RetinaGlobalsStrideBytes)]
            private struct RetinaGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 Params0;

                [FieldOffset(16)]
                public Vector4 Params1;

                public RetinaGlobalsDTO(Vector4 params0, Vector4 params1)
                {
                    Params0 = params0;
                    Params1 = params1;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int RetinaGlobalsBufferId = Shader.PropertyToID("HectonRetinaDistortionGlobals");
            internal static readonly int NarcosisId = Shader.PropertyToID("_HectonNarcosisScalar");
            internal const string Mx350Keyword = "_QUALITY_MX350";
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private RetinaDistortionPass _pass;
        private Material _material;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;

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
            TryRegisterHotSwapListener();
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
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
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
            _material = null;
            _cachedPlayerContext = null;
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private bool TryBuildRuntimeState(
            Camera renderCamera,
            FeatureSettings settings,
            out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || settings == null || !UIStateStore.IsInitialized)
                return false;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
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
