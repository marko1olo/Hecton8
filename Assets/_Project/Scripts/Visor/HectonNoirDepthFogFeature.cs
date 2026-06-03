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
using UnityEngine.Serialization;

namespace Hecton8.Visor
{
    /// <summary>
    /// Single-pass depth fog deception. It reads depth, dithers fog coverage with deterministic IGN, and composites before transparents.
    /// </summary>
    public sealed class HectonNoirDepthFogFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const int DepthFogGlobalsStrideBytes = 64;

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        private static float ResolveFiniteSaturated(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float Smooth01(float value)
        {
            float t = ResolveFiniteSaturated(value);
            return t * t * (3f - 2f * t);
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Authored fullscreen material used for depth-based noir fog.")]
            [FormerlySerializedAs("shader")]
            public Material material = null;

            [Tooltip("Injection point. Before transparents keeps particles and visor overlays readable.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Shallow fog tint in linear space after Unity converts the serialized color.")]
            public Color shallowFogColor = new Color(0.025f, 0.075f, 0.095f, 1f);

            [Tooltip("Abyss fog tint. Keep nonzero; pure black is forbidden by noir dithering mandate.")]
            public Color abyssFogColor = new Color(0.004f, 0.010f, 0.018f, 1f);

            [Tooltip("Visual fog gain for the depth ramp. This is not physical extinction.")]
            [Range(0.0001f, 0.05f)] public float density = 0.0105f;

            [Tooltip("Fog starts after this eye-space distance.")]
            [Range(0f, 15f)] public float startDistanceMeters = 1.5f;

            [Tooltip("Eye-space distance where the fake fog ramp reaches abyss coverage.")]
            [Range(10f, 180f)] public float maxDepthMeters = 80f;

            [Tooltip("Coverage noise amplitude. Applied to fog alpha only; no clip/discard.")]
            [Range(0f, 1f)] public float ditherStrength = 0.8f;

            [Tooltip("Skips noir depth fog while the player camera is above water or inside the readable surface band.")]
            public bool bypassNearSurface = true;

            [Tooltip("Depth below waterline where surface readability still wins over noir fog.")]
            [Range(0.05f, 4f)] public float nearSurfaceBypassDepthMeters = 0.85f;
        }

        private sealed class NoirDepthFogPass : ScriptableRenderPass
        {
            private sealed class DepthFogPassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public BufferHandle ConstantsBuffer;
                public Material Material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Noir Depth Fog");
            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _depthFogGlobalsBuffer;
            private GraphicsBuffer _depthFogGlobalsBufferA;
            private GraphicsBuffer _depthFogGlobalsBufferB;
            private DepthFogGlobalsDTO _lastDepthFogGlobals;
            private int _depthFogGlobalsWriteIndex;
            private float _surfaceFogWeight01 = 1f;
            private float _qualityWeight01 = 1f;
            private bool _hasDepthFogGlobals;
            private bool _supportsSetConstantBuffer;

            public NoirDepthFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, float surfaceFogWeight01, float qualityWeight01)
            {
                _settings = settings;
                _material = material;
                _surfaceFogWeight01 = ResolveFiniteSaturated(surfaceFogWeight01);
                _qualityWeight01 = ResolveFiniteSaturated(qualityWeight01);
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public bool PrepareResources()
            {
                return EnsureDepthFogGlobalsBuffer();
            }

            public void SetGraphicsCapabilitiesCold(bool supportsSetConstantBuffer)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
                if (!_supportsSetConstantBuffer)
                    Dispose();
            }

            public void Dispose()
            {
                _depthFogGlobalsBufferA?.Release();
                _depthFogGlobalsBufferB?.Release();
                _depthFogGlobalsBufferA = null;
                _depthFogGlobalsBufferB = null;
                _depthFogGlobalsBuffer = null;
                _depthFogGlobalsWriteIndex = 0;
                _lastDepthFogGlobals = default;
                _hasDepthFogGlobals = false;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner() || _settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                if (!UpdateDepthFogGlobals(_settings))
                    return;
                if (_depthFogGlobalsBuffer == null || !_depthFogGlobalsBuffer.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonNoirDepthFogComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle globalsBuffer = renderGraph.ImportBuffer(_depthFogGlobalsBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DepthFogPassData>(
                           "Hecton Noir Depth Fog",
                           out DepthFogPassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.ConstantsBuffer = globalsBuffer;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseBuffer(globalsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (DepthFogPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.DepthFogGlobalsBufferId,
                            0,
                            DepthFogGlobalsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private bool EnsureDepthFogGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_depthFogGlobalsBufferA != null && _depthFogGlobalsBufferA.IsValid() &&
                    _depthFogGlobalsBufferB != null && _depthFogGlobalsBufferB.IsValid())
                {
                    if (_depthFogGlobalsBuffer == null)
                        _depthFogGlobalsBuffer = _depthFogGlobalsBufferA;
                    return true;
                }

                _depthFogGlobalsBufferA?.Release();
                _depthFogGlobalsBufferB?.Release();
                _depthFogGlobalsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    DepthFogGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[64B] - noir depth fog global CBuffer A - owner: NoirDepthFogPass
                _depthFogGlobalsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    DepthFogGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[64B] - noir depth fog global CBuffer B - owner: NoirDepthFogPass
                _depthFogGlobalsBuffer = _depthFogGlobalsBufferA;
                _depthFogGlobalsWriteIndex = 1;
                _hasDepthFogGlobals = false;

                if (_depthFogGlobalsBufferA == null || !_depthFogGlobalsBufferA.IsValid() ||
                    _depthFogGlobalsBufferB == null || !_depthFogGlobalsBufferB.IsValid())
                {
                    Dispose();
                    return false;
                }

                return true;
            }

            private bool UpdateDepthFogGlobals(FeatureSettings settings)
            {
                if (!HasDepthFogGlobalsBuffer())
                    return false;

                DepthFogGlobalsDTO globals = DepthFogGlobalsDTO.FromSettings(
                    settings,
                    _qualityWeight01,
                    _surfaceFogWeight01);
                if (_hasDepthFogGlobals && DepthFogGlobalsEqual(in _lastDepthFogGlobals, in globals))
                {
                    return _depthFogGlobalsBuffer != null && _depthFogGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = (_depthFogGlobalsWriteIndex & 1) == 0 ? _depthFogGlobalsBufferA : _depthFogGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                try
                {
                    NativeArray<DepthFogGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<DepthFogGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<DepthFogGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkDepthFogGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkDepthFogGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkDepthFogGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkDepthFogGlobalsUnavailable();
                    return false;
                }

                _depthFogGlobalsBuffer = writeBuffer;
                _depthFogGlobalsWriteIndex ^= 1;
                _lastDepthFogGlobals = globals;
                _hasDepthFogGlobals = true;
                return _depthFogGlobalsBuffer != null && _depthFogGlobalsBuffer.IsValid();
            }

            private bool HasDepthFogGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_depthFogGlobalsBufferA == null || !_depthFogGlobalsBufferA.IsValid() ||
                    _depthFogGlobalsBufferB == null || !_depthFogGlobalsBufferB.IsValid())
                {
                    return false;
                }

                if (_depthFogGlobalsBuffer == null || !_depthFogGlobalsBuffer.IsValid())
                    _depthFogGlobalsBuffer = _depthFogGlobalsBufferA;
                return true;
            }

            private void MarkDepthFogGlobalsUnavailable()
            {
                _depthFogGlobalsBuffer = null;
                _hasDepthFogGlobals = false;
            }

            private static bool DepthFogGlobalsEqual(
                in DepthFogGlobalsDTO left,
                in DepthFogGlobalsDTO right)
            {
                return left.ShallowColor == right.ShallowColor &&
                       left.AbyssColor == right.AbyssColor &&
                       left.ParamsA == right.ParamsA &&
                       left.ParamsB == right.ParamsB;
            }

            [StructLayout(LayoutKind.Explicit, Size = DepthFogGlobalsStrideBytes)]
            private struct DepthFogGlobalsDTO
            {
                [FieldOffset(0)]
                internal Vector4 ShallowColor;

                [FieldOffset(16)]
                internal Vector4 AbyssColor;

                [FieldOffset(32)]
                internal Vector4 ParamsA;

                [FieldOffset(48)]
                internal Vector4 ParamsB;

                internal static DepthFogGlobalsDTO FromSettings(
                    FeatureSettings settings,
                    float qualityWeight01,
                    float surfaceFogWeight01)
                {
                    Color shallowFogColor = settings.shallowFogColor.linear;
                    Color abyssFogColor = settings.abyssFogColor.linear;
                    float quality = ResolveFiniteSaturated(qualityWeight01);
                    float qualityCurve = Smooth01(quality);
                    float surfaceFogWeight = ResolveFiniteSaturated(surfaceFogWeight01);
                    float visualDensity = math.max(settings.density, 0.00001f) * math.lerp(0.82f, 1.12f, qualityCurve);

                    DepthFogGlobalsDTO dto;
                    dto.ShallowColor = new Vector4(shallowFogColor.r, shallowFogColor.g, shallowFogColor.b, shallowFogColor.a);
                    dto.AbyssColor = new Vector4(abyssFogColor.r, abyssFogColor.g, abyssFogColor.b, abyssFogColor.a);
                    dto.ParamsA = new Vector4(
                        math.max(visualDensity, 0.00001f),
                        math.max(settings.startDistanceMeters, 0f),
                        math.max(settings.maxDepthMeters, 1f),
                        0f);
                    dto.ParamsB = new Vector4(quality, surfaceFogWeight, 0f, math.saturate(settings.ditherStrength));
                    return dto;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int DepthFogGlobalsBufferId = Shader.PropertyToID("HectonNoirDepthFogGlobals");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private NoirDepthFogPass _pass;
        private Material _material;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;
        private bool _supportsSetConstantBuffer;

        public override void Create()
        {
            HectonDrsRenderFeatureGate.PrimeCold();

            _material = settings != null ? settings.material : null;
            _pass ??= new NoirDepthFogPass();
            CacheGraphicsCapabilitiesCold();
            if (_material == null)
            {
                _pass.Dispose();
                return;
            }

            if (!Application.isPlaying)
                _pass.Dispose();
            TryRegisterHotSwapListener();
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
                return;

            if (settings == null || _pass == null)
                return;

            _material = settings.material;
            if (_material == null)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            float surfaceFogWeight01 = ResolveSurfaceFogWeight01(
                renderingData.cameraData.camera,
                settings.nearSurfaceBypassDepthMeters,
                settings.bypassNearSurface);
            if (surfaceFogWeight01 <= 0.0001f)
                return;

            if (!_pass.PrepareResources())
                return;

            _pass.Setup(settings, _material, surfaceFogWeight01, ResolveGlobalQualityWeight01());
            renderer.EnqueuePass(_pass);
        }

        private float ResolveSurfaceFogWeight01(Camera renderCamera, float nearSurfaceBypassDepthMeters, bool attenuateNearSurface)
        {
            if (!attenuateNearSurface)
                return 1f;

            if (renderCamera == null)
                return 1f;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            if (playerMovement != null)
            {
                float safeDepth = math.max(0.05f, nearSurfaceBypassDepthMeters);
                if (!playerMovement.IsPlayerSubmerged)
                    return 0f;

                return Smooth01(playerMovement.CurrentDepth / safeDepth);
            }

            float fallbackDepth = math.max(0f, -renderCamera.transform.position.y);
            return Smooth01(fallbackDepth / math.max(0.05f, nearSurfaceBypassDepthMeters));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            return ResolveFiniteSaturated(HomeostasisBrain.GlobalQualityWeight);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
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

    }
}
