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
    /// Single-pass depth fog deception. It reads depth, dithers fog coverage with deterministic IGN, and composites before transparents.
    /// </summary>
    public sealed class HectonNoirDepthFogFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_NoirDepthFog.shader";
#endif
        private const int DepthFogGlobalsStrideBytes = 64;

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for depth-based noir fog.")]
            public Shader shader = null;

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
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Noir Depth Fog");
            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _depthFogGlobalsBuffer;
            private GraphicsBuffer _depthFogGlobalsBufferA;
            private GraphicsBuffer _depthFogGlobalsBufferB;
            private DepthFogGlobalsDTO _lastDepthFogGlobals;
            private int _depthFogGlobalsWriteIndex;
            private bool _hasDepthFogGlobals;

            public NoirDepthFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
                EnsureDepthFogGlobalsBuffer();
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
                if (!Application.isPlaying || _settings == null || _material == null)
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

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonNoirDepthFogComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                if (!UpdateDepthFogGlobals(_settings))
                    return;

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                           passName: "Hecton Noir Depth Fog",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = destinationTexture;
            }

            private bool EnsureDepthFogGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
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
                if (!EnsureDepthFogGlobalsBuffer())
                    return false;

                DepthFogGlobalsDTO globals = DepthFogGlobalsDTO.FromSettings(settings);
                if (_hasDepthFogGlobals && DepthFogGlobalsEqual(in _lastDepthFogGlobals, in globals))
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.DepthFogGlobalsBufferId, _depthFogGlobalsBuffer, 0, DepthFogGlobalsStrideBytes);
                    return true;
                }

                GraphicsBuffer writeBuffer = (_depthFogGlobalsWriteIndex & 1) == 0 ? _depthFogGlobalsBufferA : _depthFogGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                NativeArray<DepthFogGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<DepthFogGlobalsDTO>(0, 1);
                try
                {
                    mapped[0] = globals;
                }
                finally
                {
                    writeBuffer.UnlockBufferAfterWrite<DepthFogGlobalsDTO>(1);
                }

                _depthFogGlobalsBuffer = writeBuffer;
                _depthFogGlobalsWriteIndex ^= 1;
                Shader.SetGlobalConstantBuffer(ShaderConstants.DepthFogGlobalsBufferId, _depthFogGlobalsBuffer, 0, DepthFogGlobalsStrideBytes);
                _lastDepthFogGlobals = globals;
                _hasDepthFogGlobals = true;
                return true;
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

                internal static DepthFogGlobalsDTO FromSettings(FeatureSettings settings)
                {
                    Color shallowFogColor = settings.shallowFogColor.linear;
                    Color abyssFogColor = settings.abyssFogColor.linear;

                    DepthFogGlobalsDTO dto;
                    dto.ShallowColor = new Vector4(shallowFogColor.r, shallowFogColor.g, shallowFogColor.b, shallowFogColor.a);
                    dto.AbyssColor = new Vector4(abyssFogColor.r, abyssFogColor.g, abyssFogColor.b, abyssFogColor.a);
                    dto.ParamsA = new Vector4(
                        math.max(settings.density, 0.00001f),
                        math.max(settings.startDistanceMeters, 0f),
                        math.max(settings.maxDepthMeters, 1f),
                        0f);
                    dto.ParamsB = new Vector4(0f, 0f, 0f, math.saturate(settings.ditherStrength));
                    return dto;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int DepthFogGlobalsBufferId = Shader.PropertyToID("HectonNoirDepthFogGlobals");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private NoirDepthFogPass _pass;
        private Material _material;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/NoirDepthFog");
            RecreateMaterial(ref _material, shader);
            _pass ??= new NoirDepthFogPass();
            TryRegisterHotSwapListener();
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
                return;

            if (settings == null || _pass == null || _material == null)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            if (settings.bypassNearSurface &&
                ShouldBypassForSurfaceReadability(renderingData.cameraData.camera, settings.nearSurfaceBypassDepthMeters))
            {
                return;
            }

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        private bool ShouldBypassForSurfaceReadability(Camera renderCamera, float nearSurfaceBypassDepthMeters)
        {
            if (renderCamera == null)
                return false;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            if (playerMovement != null)
            {
                float safeDepth = math.max(0.05f, nearSurfaceBypassDepthMeters);
                return !playerMovement.IsPlayerSubmerged || playerMovement.CurrentDepth <= safeDepth;
            }

            return renderCamera.transform.position.y >= -0.25f;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            DisposeMaterial(ref _material);
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

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                DisposeMaterial(ref material);
                return;
            }

            if (material != null && material.shader == shader)
                return;

            DisposeMaterial(ref material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static void DisposeMaterial(ref Material material)
        {
            if (material == null)
                return;

            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
