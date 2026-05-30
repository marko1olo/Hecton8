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
    /// Localized fullscreen soot overlay driven by fake room-atmosphere status bits.
    /// </summary>
    public sealed class HectonAtmosphereSootFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const float MinimumSootRadius = 0.001f;
        private const float ActiveSootIntensityEpsilon = 0.001f;
        private const float DefaultMaximumSootRadius = 0.82f;
        private const float DefaultSootCenter01 = 0.5f;
        private const int SootGlobalsStrideBytes = 32;

        private static readonly Vector4 DefaultSootCenter = CreateDefaultSootCenter();
        private static Vector4 s_runtimeSootParams;
        private static Vector4 s_runtimeSootCenter = DefaultSootCenter;
        private static bool s_runtimeSootActive;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_AtmosphereSootOverlay.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for fake room fire smoke soot.")]
            public Shader shader = null;

            [Tooltip("Injection point. Before post keeps soot inside the existing visor/camera stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Hard clamp for the screen-space soot radius.")]
            [Range(0.05f, 1f)] public float maximumRadius = 0.82f;
        }

        private struct RuntimeState
        {
            public float Intensity;
            public float Radius;
            public float DitherStrength;
            public float DarkenStrength;
            public Vector2 Center;
            public float Aspect;
        }

        private sealed class SootPass : ScriptableRenderPass
        {
            private sealed class SootPassData
            {
                public TextureHandle Source;
                public BufferHandle ConstantsBuffer;
                public Material Material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Atmosphere Soot");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _sootGlobalsBuffer;
            private GraphicsBuffer _sootGlobalsBufferA;
            private GraphicsBuffer _sootGlobalsBufferB;
            private SootGlobalsDTO _lastSootGlobals;
            private int _sootGlobalsWriteIndex;
            private bool _hasSootGlobals;
            private bool _supportsSetConstantBuffer;

            public SootPass()
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
                return EnsureSootGlobalsBuffer();
            }

            public void SetGraphicsCapabilitiesCold(bool supportsSetConstantBuffer)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
                if (!_supportsSetConstantBuffer)
                    Dispose();
            }

            public bool HasPreparedResources()
            {
                return HasSootGlobalsBuffer();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null || _runtimeState.Intensity <= ActiveSootIntensityEpsilon)
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
                if (cameraData.renderType != CameraRenderType.Base)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid())
                    return;

                if (!UpdateSootGlobals(_settings, _runtimeState))
                    return;
                if (_sootGlobalsBuffer == null || !_sootGlobalsBuffer.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonAtmosphereSootOverlay";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle globalsBuffer = renderGraph.ImportBuffer(_sootGlobalsBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<SootPassData>(
                           "Hecton Atmosphere Soot",
                           out SootPassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.ConstantsBuffer = globalsBuffer;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseBuffer(globalsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (SootPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.SootGlobalsBufferId,
                            0,
                            SootGlobalsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _sootGlobalsBufferA?.Release();
                _sootGlobalsBufferB?.Release();
                _sootGlobalsBufferA = null;
                _sootGlobalsBufferB = null;
                _sootGlobalsBuffer = null;
                _sootGlobalsWriteIndex = 0;
                _hasSootGlobals = false;
            }

            private bool EnsureSootGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_sootGlobalsBufferA != null && _sootGlobalsBufferA.IsValid() &&
                    _sootGlobalsBufferB != null && _sootGlobalsBufferB.IsValid())
                {
                    if (_sootGlobalsBuffer == null)
                        _sootGlobalsBuffer = _sootGlobalsBufferA;
                    return true;
                }

                _sootGlobalsBufferA?.Release();
                _sootGlobalsBufferB?.Release();
                _sootGlobalsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SootGlobalsStrideBytes);
                _sootGlobalsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SootGlobalsStrideBytes);
                _sootGlobalsBuffer = _sootGlobalsBufferA;
                _sootGlobalsWriteIndex = 1;
                _hasSootGlobals = false;
                return _sootGlobalsBufferA.IsValid() && _sootGlobalsBufferB.IsValid();
            }

            private bool UpdateSootGlobals(FeatureSettings settings, RuntimeState runtimeState)
            {
                Vector4 sootParams = default;
                sootParams.x = math.saturate(runtimeState.Intensity);
                sootParams.y = math.clamp(runtimeState.Radius, MinimumSootRadius, ResolveMaximumRadius(settings));
                sootParams.z = math.saturate(runtimeState.DitherStrength);
                sootParams.w = math.saturate(runtimeState.DarkenStrength);

                float centerX = math.saturate(runtimeState.Center.x);
                float aspect = math.max(1f, runtimeState.Aspect);
                Vector4 sootCenter = default;
                sootCenter.x = centerX;
                sootCenter.y = math.saturate(runtimeState.Center.y);
                sootCenter.z = aspect;
                sootCenter.w = centerX * aspect;

                if (!HasSootGlobalsBuffer())
                    return false;

                SootGlobalsDTO globals = new SootGlobalsDTO(sootParams, sootCenter);
                if (_hasSootGlobals && SootGlobalsEqual(in _lastSootGlobals, in globals))
                {
                    return _sootGlobalsBuffer != null && _sootGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = (_sootGlobalsWriteIndex & 1) == 0 ? _sootGlobalsBufferA : _sootGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                try
                {
                    NativeArray<SootGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<SootGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<SootGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkSootGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkSootGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkSootGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkSootGlobalsUnavailable();
                    return false;
                }
                _sootGlobalsBuffer = writeBuffer;
                _sootGlobalsWriteIndex ^= 1;
                _lastSootGlobals = globals;
                _hasSootGlobals = true;
                return _sootGlobalsBuffer != null && _sootGlobalsBuffer.IsValid();
            }

            private bool HasSootGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_sootGlobalsBufferA == null || !_sootGlobalsBufferA.IsValid() ||
                    _sootGlobalsBufferB == null || !_sootGlobalsBufferB.IsValid())
                {
                    return false;
                }

                if (_sootGlobalsBuffer == null || !_sootGlobalsBuffer.IsValid())
                    _sootGlobalsBuffer = _sootGlobalsBufferA;
                return true;
            }

            private void MarkSootGlobalsUnavailable()
            {
                _sootGlobalsBuffer = null;
                _hasSootGlobals = false;
            }

            private static bool SootGlobalsEqual(in SootGlobalsDTO left, in SootGlobalsDTO right)
            {
                return Vector4Equals(left.SootParams, right.SootParams) &&
                       Vector4Equals(left.SootCenter, right.SootCenter);
            }

            [StructLayout(LayoutKind.Explicit, Size = SootGlobalsStrideBytes)]
            private struct SootGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 SootParams;

                [FieldOffset(16)]
                public Vector4 SootCenter;

                public SootGlobalsDTO(Vector4 sootParams, Vector4 sootCenter)
                {
                    SootParams = sootParams;
                    SootCenter = sootCenter;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SootGlobalsBufferId = Shader.PropertyToID("HectonAtmosphereSootGlobals");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings(); // COLD ALLOC: FeatureSettings[1] - serialized soot overlay renderer settings - owner: HectonAtmosphereSootFeature

        private SootPass _pass;
        private Material _material;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;
        private bool _supportsSetConstantBuffer;

        public static void PublishRuntimeState(bool active, in Vector4 sootParams, in Vector4 sootCenter)
        {
            if (!active || !math.isfinite(sootParams.x))
            {
                ClearRuntimeState();
                return;
            }

            float intensity = math.saturate(sootParams.x);
            if (intensity <= ActiveSootIntensityEpsilon)
            {
                ClearRuntimeState();
                return;
            }

            float ditherStrength = math.isfinite(sootParams.z) ? math.saturate(sootParams.z) : 0f;
            float darkenStrength = math.isfinite(sootParams.w) ? math.saturate(sootParams.w) : 0f;
            if (ditherStrength <= 0f && darkenStrength <= 0f)
            {
                ClearRuntimeState();
                return;
            }

            Vector4 sanitizedParams = default;
            sanitizedParams.x = intensity;
            sanitizedParams.y = math.isfinite(sootParams.y) ? math.max(MinimumSootRadius, sootParams.y) : MinimumSootRadius;
            sanitizedParams.z = ditherStrength;
            sanitizedParams.w = darkenStrength;
            s_runtimeSootParams = sanitizedParams;

            Vector4 sanitizedCenter = default;
            sanitizedCenter.x = math.isfinite(sootCenter.x) ? math.saturate(sootCenter.x) : DefaultSootCenter01;
            sanitizedCenter.y = math.isfinite(sootCenter.y) ? math.saturate(sootCenter.y) : DefaultSootCenter01;
            s_runtimeSootCenter = sanitizedCenter;
            s_runtimeSootActive = true;
        }

        private static Vector4 CreateDefaultSootCenter()
        {
            Vector4 result = default;
            result.x = DefaultSootCenter01;
            result.y = DefaultSootCenter01;
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearRuntimeState();
        }

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new SootPass(); // COLD ALLOC: SootPass[1] - reusable soot overlay render pass - owner: HectonAtmosphereSootFeature
            CacheGraphicsCapabilitiesCold();
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

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;
            if (renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            if (!_pass.HasPreparedResources())
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

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _pass?.SetGraphicsCapabilitiesCold(_supportsSetConstantBuffer);
        }

        private bool TryBuildRuntimeState(Camera renderCamera, FeatureSettings settings, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || settings == null)
                return false;

            if (!s_runtimeSootActive)
                return false;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            Vector4 sootParams = s_runtimeSootParams;
            float intensity = sootParams.x;
            if (intensity <= ActiveSootIntensityEpsilon)
                return false;

            float maximumRadius = ResolveMaximumRadius(settings);
            float radius = math.clamp(sootParams.y, MinimumSootRadius, maximumRadius);
            float ditherStrength = sootParams.z;
            float darkenStrength = sootParams.w;
            if (ditherStrength <= 0f && darkenStrength <= 0f)
                return false;

            float qualityCurve01 = ResolveSootQualityCurve01();
            radius = math.clamp(radius * math.lerp(0.68f, 1f, qualityCurve01), MinimumSootRadius, maximumRadius);
            ditherStrength = math.saturate(ditherStrength * math.lerp(0.55f, 1f, qualityCurve01));
            darkenStrength = math.saturate(darkenStrength * math.lerp(0.75f, 1.08f, qualityCurve01));
            if (ditherStrength <= 0f && darkenStrength <= 0f)
                return false;

            Vector4 sootCenter = s_runtimeSootCenter;
            float aspect = math.max(1f, renderCamera.pixelWidth / math.max(1f, (float)renderCamera.pixelHeight));
            runtimeState.Intensity = intensity;
            runtimeState.Radius = radius;
            runtimeState.DitherStrength = ditherStrength;
            runtimeState.DarkenStrength = darkenStrength;
            runtimeState.Center.x = sootCenter.x;
            runtimeState.Center.y = sootCenter.y;
            runtimeState.Aspect = aspect;
            return true;
        }

        private static float ResolveSootQualityCurve01()
        {
            float quality01 = ResolveGlobalQualityWeight01();
            return quality01 * quality01 * (3f - 2f * quality01);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.select(math.saturate(quality), 1f, !math.isfinite(quality));
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

        private static void ClearRuntimeState()
        {
            s_runtimeSootParams = Vector4.zero;
            s_runtimeSootCenter = DefaultSootCenter;
            s_runtimeSootActive = false;
        }

        private static float ResolveMaximumRadius(FeatureSettings settings)
        {
            float maximumRadius = settings != null ? settings.maximumRadius : DefaultMaximumSootRadius;
            return math.isfinite(maximumRadius)
                ? math.clamp(maximumRadius, MinimumSootRadius, 1f)
                : DefaultMaximumSootRadius;
        }

        private static bool Vector4Equals(Vector4 left, Vector4 right)
        {
            return left.x == right.x &&
                   left.y == right.y &&
                   left.z == right.z &&
                   left.w == right.w;
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
            material = CoreUtils.CreateEngineMaterial(shader); // COLD ALLOC: Material[1] - hidden soot overlay renderer material - owner: HectonAtmosphereSootFeature
        }
    }
}
