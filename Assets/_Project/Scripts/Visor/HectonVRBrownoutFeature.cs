using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
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
    public sealed class HectonVRBrownoutFeature : ScriptableRendererFeature
    {
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

            public float BrownoutIntensity { get; }
            public float WorldFocusBlur { get; }
            public float NearCollisionIntensity { get; }
            public Vector4 VrComfortSignals { get; }
            public Vector4 VrComfortMotion { get; }
        }

        private sealed class BrownoutPass : ScriptableRenderPass
        {
            private const float MaterialFloatEpsilon = 0.0001f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton VR Brownout");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private Material _lastParameterMaterial;
            private float _lastBrownoutIntensity = float.PositiveInfinity;
            private float _lastWorldFocusBlur = float.PositiveInfinity;
            private float _lastNearCollisionIntensity = float.PositiveInfinity;
            private float _lastWorldBlurTexelRadius = float.PositiveInfinity;
            private float _lastScanlineStrength = float.PositiveInfinity;
            private float _lastDitherStrength = float.PositiveInfinity;
            private Vector4 _lastVrComfortSignals = Vector4.positiveInfinity;
            private Vector4 _lastVrComfortMotion = Vector4.positiveInfinity;
            private bool _materialDirty = true;

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

                UpdateMaterialParameters(_material, _settings, _runtimeState);

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                    passName: "Hecton VR Brownout");

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialParameters(Material material, FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!ReferenceEquals(_lastParameterMaterial, material))
                {
                    ResetMaterialParameterCache();
                    _lastParameterMaterial = material;
                }

                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.BrownoutIntensityId,
                    HectonVRBrownoutFeature.Sanitize01(runtimeState.BrownoutIntensity),
                    ref _lastBrownoutIntensity);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.WorldFocusBlurId,
                    HectonVRBrownoutFeature.Sanitize01(runtimeState.WorldFocusBlur),
                    ref _lastWorldFocusBlur);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.NearCollisionIntensityId,
                    HectonVRBrownoutFeature.Sanitize01(runtimeState.NearCollisionIntensity),
                    ref _lastNearCollisionIntensity);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.WorldBlurTexelRadiusId,
                    HectonVRBrownoutFeature.SanitizeRange(settings.worldBlurTexelRadius, 0f, 3f),
                    ref _lastWorldBlurTexelRadius);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.ScanlineStrengthId,
                    HectonVRBrownoutFeature.Sanitize01(settings.scanlineStrength),
                    ref _lastScanlineStrength);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.DitherStrengthId,
                    HectonVRBrownoutFeature.Sanitize01(settings.ditherStrength),
                    ref _lastDitherStrength);
                SetMaterialVectorIfChanged(
                    material,
                    ShaderConstants.VrComfortSignalsId,
                    SanitizeVrComfortSignals(runtimeState.VrComfortSignals),
                    ref _lastVrComfortSignals);
                SetMaterialVectorIfChanged(
                    material,
                    ShaderConstants.VrComfortMotionId,
                    SanitizeVrComfortMotion(runtimeState.VrComfortMotion),
                    ref _lastVrComfortMotion);
                _materialDirty = false;
            }

            private void ResetMaterialParameterCache()
            {
                _lastBrownoutIntensity = float.PositiveInfinity;
                _lastWorldFocusBlur = float.PositiveInfinity;
                _lastNearCollisionIntensity = float.PositiveInfinity;
                _lastWorldBlurTexelRadius = float.PositiveInfinity;
                _lastScanlineStrength = float.PositiveInfinity;
                _lastDitherStrength = float.PositiveInfinity;
                _lastVrComfortSignals = Vector4.positiveInfinity;
                _lastVrComfortMotion = Vector4.positiveInfinity;
                _materialDirty = true;
            }

            private void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue)
            {
                if (!_materialDirty && math.abs(cachedValue - value) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }

            private void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue)
            {
                if (!_materialDirty &&
                    math.abs(cachedValue.x - value.x) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.y - value.y) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.z - value.z) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.w - value.w) <= MaterialFloatEpsilon)
                {
                    return;
                }

                material.SetVector(shaderId, value);
                cachedValue = value;
            }
        }

        private static class ShaderConstants
        {
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
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private static bool TryBuildRuntimeState(Camera renderCamera, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || !HectonXRRuntimeState.IsXRActive)
                return false;

            Camera playerCamera = null;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                playerCamera = runtimeContext.PlayerCamera;

            if (playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            if (playerCamera != null && !ReferenceEquals(renderCamera, playerCamera))
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
