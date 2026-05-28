using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
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
        public sealed class HectonRetinaDistortionFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
        {
            private const int RetinaGlobalsStrideBytes = 32;
            private const int RetinaSurvivalGraphicsMemoryMb = 1536;
            private const int RetinaVisualOverkillGraphicsMemoryMb = 4096;

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

        private struct RuntimeState
        {
            public float Health01;
            public float Critical01;
            public float HeartbeatBpm;
            public float Narcosis01;
        }

        private sealed class RetinaDistortionPass : ScriptableRenderPass
        {
            private const float GlobalsFloatEpsilon = 0.0001f;

            private sealed class RetinaPassData
            {
                public TextureHandle Source;
                public BufferHandle ConstantsBuffer;
                public Material Material;
            }

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

            public bool PrepareResources()
            {
                return EnsureRetinaGlobalsBuffer();
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

                if (!UpdateRetinaGlobals(_settings, _runtimeState))
                    return;
                if (_retinaGlobalsBuffer == null || !_retinaGlobalsBuffer.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonRetinaDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle globalsBuffer = renderGraph.ImportBuffer(_retinaGlobalsBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<RetinaPassData>(
                           "Hecton Retina Distortion",
                           out RetinaPassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.ConstantsBuffer = globalsBuffer;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseBuffer(globalsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (RetinaPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.RetinaGlobalsBufferId,
                            0,
                            RetinaGlobalsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

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
                if (!HasRetinaGlobalsBuffer())
                    return false;

                float critical01 = math.saturate(runtimeState.Critical01);
                float narcosis01 = math.saturate(runtimeState.Narcosis01);
                float drive01 = math.max(critical01, narcosis01);
                float health01 = math.saturate(runtimeState.Health01);
                float heartbeatBpm = math.max(1f, runtimeState.HeartbeatBpm);
                float retinaQualityWeight = ResolveRetinaRuntimeQualityWeight(SystemInfo.graphicsMemorySize);
                RetinaOffsetBudget offsetBudget = ResolveRetinaOffsetBudget(
                    math.max(0f, settings.maxChromaticOffset),
                    math.max(0f, settings.maxDistortionOffset),
                    drive01);
                float vignetteStrength = math.saturate(settings.maxVignetteStrength) * math.max(critical01, narcosis01 * 0.62f);

                RetinaGlobalsDTO globals = new RetinaGlobalsDTO(
                    new Vector4(health01, critical01, heartbeatBpm, narcosis01),
                    new Vector4(offsetBudget.ChromaticOffset, offsetBudget.DistortionOffset, vignetteStrength, retinaQualityWeight));
                if (_hasRetinaGlobals && RetinaGlobalsEqual(in _lastRetinaGlobals, in globals))
                {
                    return _retinaGlobalsBuffer != null && _retinaGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = (_retinaGlobalsWriteIndex & 1) == 0 ? _retinaGlobalsBufferA : _retinaGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                try
                {
                    NativeArray<RetinaGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<RetinaGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<RetinaGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkRetinaGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkRetinaGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkRetinaGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkRetinaGlobalsUnavailable();
                    return false;
                }
                _retinaGlobalsBuffer = writeBuffer;
                _retinaGlobalsWriteIndex ^= 1;
                _lastRetinaGlobals = globals;
                _hasRetinaGlobals = true;
                return _retinaGlobalsBuffer != null && _retinaGlobalsBuffer.IsValid();
            }

            private bool HasRetinaGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                    return false;

                if (_retinaGlobalsBufferA == null || !_retinaGlobalsBufferA.IsValid() ||
                    _retinaGlobalsBufferB == null || !_retinaGlobalsBufferB.IsValid())
                {
                    return false;
                }

                if (_retinaGlobalsBuffer == null || !_retinaGlobalsBuffer.IsValid())
                    _retinaGlobalsBuffer = _retinaGlobalsBufferA;
                return true;
            }

            private void MarkRetinaGlobalsUnavailable()
            {
                _retinaGlobalsBuffer = null;
                _hasRetinaGlobals = false;
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
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int NarcosisId = Shader.PropertyToID("_HectonNarcosisScalar");
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
            _pass.PrepareResources();
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
            runtimeState = default;
            runtimeState.Health01 = health01;
            runtimeState.Critical01 = drive01;
            runtimeState.HeartbeatBpm = math.lerp(baseBpm, criticalBpm, drive01);
            runtimeState.Narcosis01 = narcosis01;
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
            float critical01)
        {
            float clampedCritical = math.saturate(critical01);
            return new RetinaOffsetBudget(
                math.max(0f, maxChromaticOffset) * clampedCritical,
                math.max(0f, maxDistortionOffset) * clampedCritical);
        }

        internal static float ResolveRetinaVisualQualityWeight(int graphicsMemoryMb)
        {
            if (graphicsMemoryMb <= 0)
                return 1f;

            float range = math.max(1f, RetinaVisualOverkillGraphicsMemoryMb - RetinaSurvivalGraphicsMemoryMb);
            float t = math.saturate((graphicsMemoryMb - RetinaSurvivalGraphicsMemoryMb) / range);
            return t * t * (3f - 2f * t);
        }

        private static float ResolveRetinaRuntimeQualityWeight(int graphicsMemoryMb)
        {
            float hardwareWeight = ResolveRetinaVisualQualityWeight(graphicsMemoryMb);
            float globalWeight = HomeostasisBrain.GlobalQualityWeight;
            globalWeight = math.isfinite(globalWeight) ? math.saturate(globalWeight) : 1f;
            return hardwareWeight * globalWeight;
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
