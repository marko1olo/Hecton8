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
    /// Localized fullscreen soot overlay driven by fake room-atmosphere status bits.
    /// </summary>
    public sealed class HectonAtmosphereSootFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const float MinimumSootRadius = 0.001f;
        private const float ActiveSootIntensityEpsilon = 0.001f;
        private const float DefaultMaximumSootRadius = 0.82f;
        private const float DefaultSootCenter01 = 0.5f;
        private const int SootGlobalsStrideBytes = 32;

        private static readonly Vector4 DefaultSootCenter = new Vector4(DefaultSootCenter01, DefaultSootCenter01, 0f, 0f);
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

        private readonly struct RuntimeState
        {
            public RuntimeState(float intensity, float radius, float ditherStrength, float darkenStrength, Vector2 center, float aspect)
            {
                Intensity = intensity;
                Radius = radius;
                DitherStrength = ditherStrength;
                DarkenStrength = darkenStrength;
                Center = center;
                Aspect = aspect;
            }

            public readonly float Intensity;
            public readonly float Radius;
            public readonly float DitherStrength;
            public readonly float DarkenStrength;
            public readonly Vector2 Center;
            public readonly float Aspect;
        }

        private sealed class SootPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Atmosphere Soot");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _sootGlobalsBuffer;
            private SootGlobalsDTO _lastSootGlobals;
            private bool _hasSootGlobals;

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
                EnsureSootGlobalsBuffer();
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

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonAtmosphereSootOverlay";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                if (!UpdateSootGlobals(_settings, _runtimeState))
                    return;

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                    passName: "Hecton Atmosphere Soot");

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _sootGlobalsBuffer?.Release();
                _sootGlobalsBuffer = null;
                _hasSootGlobals = false;
            }

            private bool EnsureSootGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_sootGlobalsBuffer != null && _sootGlobalsBuffer.IsValid())
                    return true;

                _sootGlobalsBuffer?.Release();
                _sootGlobalsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    SootGlobalsStrideBytes);
                _hasSootGlobals = false;
                return _sootGlobalsBuffer.IsValid();
            }

            private bool UpdateSootGlobals(FeatureSettings settings, RuntimeState runtimeState)
            {
                Vector4 sootParams = new Vector4(
                    math.saturate(runtimeState.Intensity),
                    math.clamp(runtimeState.Radius, MinimumSootRadius, ResolveMaximumRadius(settings)),
                    math.saturate(runtimeState.DitherStrength),
                    math.saturate(runtimeState.DarkenStrength));
                Vector4 sootCenter = new Vector4(
                    math.saturate(runtimeState.Center.x),
                    math.saturate(runtimeState.Center.y),
                    math.max(1f, runtimeState.Aspect),
                    math.saturate(runtimeState.Center.x) * math.max(1f, runtimeState.Aspect));

                if (!EnsureSootGlobalsBuffer())
                    return false;

                SootGlobalsDTO globals = new SootGlobalsDTO(sootParams, sootCenter);
                if (_hasSootGlobals && SootGlobalsEqual(in _lastSootGlobals, in globals))
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.SootGlobalsBufferId, _sootGlobalsBuffer, 0, SootGlobalsStrideBytes);
                    return true;
                }

                NativeArray<SootGlobalsDTO> mapped = _sootGlobalsBuffer.LockBufferForWrite<SootGlobalsDTO>(0, 1);
                mapped[0] = globals;
                _sootGlobalsBuffer.UnlockBufferAfterWrite<SootGlobalsDTO>(1);
                _lastSootGlobals = globals;
                _hasSootGlobals = true;
                Shader.SetGlobalConstantBuffer(ShaderConstants.SootGlobalsBufferId, _sootGlobalsBuffer, 0, SootGlobalsStrideBytes);
                return true;
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
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings(); // COLD ALLOC: FeatureSettings[1] - serialized soot overlay renderer settings - owner: HectonAtmosphereSootFeature

        private SootPass _pass;
        private Material _material;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;

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

            s_runtimeSootParams = new Vector4(
                intensity,
                math.isfinite(sootParams.y) ? math.max(MinimumSootRadius, sootParams.y) : MinimumSootRadius,
                ditherStrength,
                darkenStrength);
            s_runtimeSootCenter = new Vector4(
                math.isfinite(sootCenter.x) ? math.saturate(sootCenter.x) : DefaultSootCenter01,
                math.isfinite(sootCenter.y) ? math.saturate(sootCenter.y) : DefaultSootCenter01,
                0f,
                0f);
            s_runtimeSootActive = true;
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
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
            TryRegisterHotSwapListener();
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game)
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

            Vector4 sootCenter = s_runtimeSootCenter;
            float aspect = math.max(1f, renderCamera.pixelWidth / math.max(1f, (float)renderCamera.pixelHeight));
            runtimeState = new RuntimeState(
                intensity,
                radius,
                ditherStrength,
                darkenStrength,
                new Vector2(sootCenter.x, sootCenter.y),
                aspect);
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
