using System;
using System.Runtime.CompilerServices;
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
    /// Single fullscreen VR visor brownout and focus blur pass.
    /// </summary>
    public sealed class HectonVRBrownoutFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const int VRBrownoutGlobalsStrideBytes = 64;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_VRBrownout.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for BIOS green brownout and dynamic focus blur.")]
            public Shader shader = null;

            [Tooltip("Injection point. Before post keeps the pass inside the validated visor stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum world blur UV radius in source texels.")]
            [Range(0.25f, 3f)] public float worldBlurTexelRadius = 1.65f;

            [Tooltip("Scanline contrast applied at full brownout.")]
            [Range(0f, 1f)] public float scanlineStrength = 0.55f;

            [Tooltip("Ordered/noise dither strength applied at full brownout.")]
            [Range(0f, 1f)] public float ditherStrength = 0.85f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(
                float brownoutIntensity,
                float worldFocusBlur,
                float nearCollisionIntensity,
                Vector4 vrComfortSignals,
                Vector4 vrComfortMotion)
            {
                BrownoutIntensity = brownoutIntensity;
                WorldFocusBlur = worldFocusBlur;
                NearCollisionIntensity = nearCollisionIntensity;
                VrComfortSignals = vrComfortSignals;
                VrComfortMotion = vrComfortMotion;
            }

            public readonly float BrownoutIntensity;
            public readonly float WorldFocusBlur;
            public readonly float NearCollisionIntensity;
            public readonly Vector4 VrComfortSignals;
            public readonly Vector4 VrComfortMotion;
        }

        private sealed class BrownoutPass : ScriptableRenderPass
        {
            private const float GlobalsFloatEpsilon = 0.0001f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton VR Brownout");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _brownoutGlobalsBuffer;
            private GraphicsBuffer _brownoutGlobalsBufferA;
            private GraphicsBuffer _brownoutGlobalsBufferB;
            private BrownoutGlobalsDTO _lastBrownoutGlobals;
            private int _brownoutGlobalsWriteIndex;
            private bool _hasBrownoutGlobals;

            public BrownoutPass()
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
                EnsureBrownoutGlobalsBuffer();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _material == null ||
                    (_runtimeState.BrownoutIntensity <= 0.001f &&
                     _runtimeState.WorldFocusBlur <= 0.001f &&
                     _runtimeState.NearCollisionIntensity <= 0.001f &&
                     !HectonVRBrownoutFeature.HasVrComfortWork(_runtimeState.VrComfortSignals, _runtimeState.VrComfortMotion)))
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
                destinationDesc.name = "_HectonVRBrownout";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                if (!UpdateBrownoutGlobals(_settings, _runtimeState))
                    return;

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                    passName: "Hecton VR Brownout");

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _brownoutGlobalsBufferA?.Release();
                _brownoutGlobalsBufferA = null;
                _brownoutGlobalsBufferB?.Release();
                _brownoutGlobalsBufferB = null;
                _brownoutGlobalsBuffer = null;
                _brownoutGlobalsWriteIndex = 0;
                _hasBrownoutGlobals = false;
            }

            private bool EnsureBrownoutGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_brownoutGlobalsBufferA != null && _brownoutGlobalsBufferA.IsValid() &&
                    _brownoutGlobalsBufferB != null && _brownoutGlobalsBufferB.IsValid())
                {
                    if (_brownoutGlobalsBuffer == null)
                        _brownoutGlobalsBuffer = _brownoutGlobalsBufferA;
                    return true;
                }

                _brownoutGlobalsBufferA?.Release();
                _brownoutGlobalsBufferB?.Release();
                _brownoutGlobalsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    VRBrownoutGlobalsStrideBytes);
                _brownoutGlobalsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    VRBrownoutGlobalsStrideBytes);
                _brownoutGlobalsBuffer = _brownoutGlobalsBufferA;
                _brownoutGlobalsWriteIndex = 1;
                _hasBrownoutGlobals = false;
                return _brownoutGlobalsBufferA.IsValid() && _brownoutGlobalsBufferB.IsValid();
            }

            private bool UpdateBrownoutGlobals(FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!EnsureBrownoutGlobalsBuffer())
                    return false;

                BrownoutGlobalsDTO globals = new BrownoutGlobalsDTO(
                    new Vector4(
                        HectonVRBrownoutFeature.Sanitize01(runtimeState.BrownoutIntensity),
                        HectonVRBrownoutFeature.Sanitize01(runtimeState.WorldFocusBlur),
                        HectonVRBrownoutFeature.Sanitize01(runtimeState.NearCollisionIntensity),
                        HectonVRBrownoutFeature.SanitizeRange(settings.worldBlurTexelRadius, 0f, 3f)),
                    new Vector4(
                        HectonVRBrownoutFeature.Sanitize01(settings.scanlineStrength),
                        HectonVRBrownoutFeature.Sanitize01(settings.ditherStrength),
                        0f,
                        0f),
                    SanitizeVrComfortSignals(runtimeState.VrComfortSignals),
                    SanitizeVrComfortMotion(runtimeState.VrComfortMotion));
                if (_hasBrownoutGlobals && BrownoutGlobalsEqual(in _lastBrownoutGlobals, in globals))
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.BrownoutGlobalsBufferId, _brownoutGlobalsBuffer, 0, VRBrownoutGlobalsStrideBytes);
                    return true;
                }

                GraphicsBuffer writeBuffer = _brownoutGlobalsWriteIndex == 0 ? _brownoutGlobalsBufferA : _brownoutGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                NativeArray<BrownoutGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<BrownoutGlobalsDTO>(0, 1);
                mapped[0] = globals;
                writeBuffer.UnlockBufferAfterWrite<BrownoutGlobalsDTO>(1);
                _brownoutGlobalsBuffer = writeBuffer;
                _brownoutGlobalsWriteIndex ^= 1;
                _lastBrownoutGlobals = globals;
                _hasBrownoutGlobals = true;
                Shader.SetGlobalConstantBuffer(ShaderConstants.BrownoutGlobalsBufferId, _brownoutGlobalsBuffer, 0, VRBrownoutGlobalsStrideBytes);
                return true;
            }

            private static bool BrownoutGlobalsEqual(in BrownoutGlobalsDTO left, in BrownoutGlobalsDTO right)
            {
                return Vector4Approximately(left.Params0, right.Params0) &&
                       Vector4Approximately(left.Params1, right.Params1) &&
                       Vector4Approximately(left.VrComfortSignals, right.VrComfortSignals) &&
                       Vector4Approximately(left.VrComfortMotion, right.VrComfortMotion);
            }

            private static bool Vector4Approximately(Vector4 left, Vector4 right)
            {
                return math.abs(left.x - right.x) <= GlobalsFloatEpsilon &&
                       math.abs(left.y - right.y) <= GlobalsFloatEpsilon &&
                       math.abs(left.z - right.z) <= GlobalsFloatEpsilon &&
                       math.abs(left.w - right.w) <= GlobalsFloatEpsilon;
            }

            [StructLayout(LayoutKind.Explicit, Size = VRBrownoutGlobalsStrideBytes)]
            private struct BrownoutGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 Params0;

                [FieldOffset(16)]
                public Vector4 Params1;

                [FieldOffset(32)]
                public Vector4 VrComfortSignals;

                [FieldOffset(48)]
                public Vector4 VrComfortMotion;

                public BrownoutGlobalsDTO(
                    Vector4 params0,
                    Vector4 params1,
                    Vector4 vrComfortSignals,
                    Vector4 vrComfortMotion)
                {
                    Params0 = params0;
                    Params1 = params1;
                    VrComfortSignals = vrComfortSignals;
                    VrComfortMotion = vrComfortMotion;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BrownoutGlobalsBufferId = Shader.PropertyToID("HectonVRBrownoutGlobals");
            internal static readonly int BrownoutIntensityId = Shader.PropertyToID("_HectonVRBrownoutIntensity");
            internal static readonly int WorldFocusBlurId = Shader.PropertyToID("_HectonWorldFocusBlur");
            internal static readonly int NearCollisionIntensityId = Shader.PropertyToID("_HectonVRNearCollisionIntensity");
            internal static readonly int WorldBlurTexelRadiusId = Shader.PropertyToID("_HectonWorldBlurTexelRadius");
            internal static readonly int ScanlineStrengthId = Shader.PropertyToID("_HectonVRBrownoutScanlineStrength");
            internal static readonly int DitherStrengthId = Shader.PropertyToID("_HectonVRBrownoutDitherStrength");
            internal static readonly int VrComfortSignalsId = Shader.PropertyToID("_HectonVrComfortSignals");
            internal static readonly int VrComfortMotionId = Shader.PropertyToID("_HectonVrComfortMotion");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private BrownoutPass _pass;
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

            _pass ??= new BrownoutPass();
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
            TryRegisterHotSwapListener();
            _cachedPlayerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            Camera renderCamera = renderingData.cameraData.camera;
            if (!TryBuildRuntimeState(renderCamera, out RuntimeState runtimeState))
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

        private bool TryBuildRuntimeState(Camera renderCamera, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || !HectonXRRuntimeState.IsXRActive)
                return false;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            float brownoutIntensity = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.BrownoutIntensityId));
            float worldFocusBlur = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.WorldFocusBlurId));
            float nearCollisionIntensity = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.NearCollisionIntensityId));
            Vector4 vrComfortSignals = SanitizeVrComfortSignals(Shader.GetGlobalVector(ShaderConstants.VrComfortSignalsId));
            Vector4 vrComfortMotion = SanitizeVrComfortMotion(Shader.GetGlobalVector(ShaderConstants.VrComfortMotionId));
            if (brownoutIntensity <= 0.001f &&
                worldFocusBlur <= 0.001f &&
                nearCollisionIntensity <= 0.001f &&
                !HasVrComfortWork(vrComfortSignals, vrComfortMotion))
            {
                return false;
            }

            runtimeState = new RuntimeState(
                brownoutIntensity,
                worldFocusBlur,
                nearCollisionIntensity,
                vrComfortSignals,
                vrComfortMotion);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeRange(float value, float minimum, float maximum)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : minimum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 SanitizeVrComfortSignals(Vector4 value)
        {
            return new Vector4(
                Sanitize01(value.x),
                Sanitize01(value.y),
                Sanitize01(value.z),
                Sanitize01(value.w));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 SanitizeVrComfortMotion(Vector4 value)
        {
            return new Vector4(
                math.isfinite(value.x) ? math.clamp(value.x, -1f, 1f) : 0f,
                math.isfinite(value.y) ? math.clamp(value.y, -1f, 1f) : 0f,
                Sanitize01(value.z),
                Sanitize01(value.w));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasVrComfortWork(Vector4 signals, Vector4 motion)
        {
            if (signals.w <= 0.001f)
                return false;

            float strongestSignal = math.max(math.max(signals.x, signals.y), math.max(signals.z, motion.z));
            return strongestSignal > 0.001f;
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
