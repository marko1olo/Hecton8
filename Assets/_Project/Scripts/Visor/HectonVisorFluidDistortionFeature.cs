using System;
using Hecton8.Core;
using Hecton8.Gameplay;
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
    /// Fullscreen visor droplet and leak distortion driven by the active player wet-lens and hull-stress signals.
    /// </summary>
    public sealed class HectonVisorFluidDistortionFeature : ScriptableRendererFeature
    {
        private const float ThermalDistortionCullSpeedMetersPerSecond = 15f;
        private const float ThermalDistortionCullSpeedMetersPerSecondSq = ThermalDistortionCullSpeedMetersPerSecond * ThermalDistortionCullSpeedMetersPerSecond;
        private const float HullStressVisorContributionStart01 = 0.65f;
        private const float HullStressVisorContributionInvRange = 1f / (1f - HullStressVisorContributionStart01);
        private const float VisorSpeedSquaredToShader01 = 0.0016f;
        private const float QuaternionMinimumLengthSq = 0.000001f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for procedural visor droplets and hull-stress leaks.")]
            public Shader shader = null;

            [Tooltip("Injection point for the visor distortion. Before post-processing keeps the effect inside the validated noir stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum UV refraction applied by the procedural fluid mask.")]
            [Range(0f, 0.04f)] public float distortionStrength = 0.012f;

            [Tooltip("Base vertical runoff speed for droplets sliding down the visor.")]
            [Range(0.1f, 4f)] public float runoffSpeed = 1.2f;

            [Tooltip("Base droplet tiling density across the visor.")]
            [Range(2f, 24f)] public float dropletScale = 8f;

            [Tooltip("How strongly camera-relative sideways velocity shears the runoff field.")]
            [Range(0f, 1f)] public float lateralStreakStrength = 0.42f;

            [Tooltip("How strongly forward speed elongates and densifies the streak field.")]
            [Range(0f, 1f)] public float forwardStretchStrength = 0.28f;

            [Tooltip("How strongly high speed pushes droplets radially toward the visor edges.")]
            [Range(0f, 1f)] public float edgeStreakStrength = 0.46f;

            [Tooltip("How much hull stress contributes even when the player is otherwise dry.")]
            [Range(0f, 1f)] public float hullStressContribution = 0.8f;

            [Tooltip("Viewport edge fade used to keep the center readable while droplets accumulate on the visor rim.")]
            [Range(0.1f, 4f)] public float edgeFadeExponent = 1.35f;

            [Tooltip("Dust visibility added by ambient light on the visor layer.")]
            [Range(0f, 1f)] public float dustStrength = 0.28f;

            [Tooltip("How aggressively ambient light exposes visor dust.")]
            [Range(0f, 4f)] public float ambientDustResponse = 1.45f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(float wetness, float hullStress, Vector3 localVelocity, float ambientLight01, float effectIntensity, float rainIntensity, float thermalMotionCull01)
            {
                Wetness = wetness;
                HullStress = hullStress;
                LocalVelocity = localVelocity;
                AmbientLight01 = ambientLight01;
                EffectIntensity = effectIntensity;
                RainIntensity = rainIntensity;
                ThermalMotionCull01 = thermalMotionCull01;
            }

            public float Wetness { get; }
            public float HullStress { get; }
            public Vector3 LocalVelocity { get; }
            public float AmbientLight01 { get; }
            public float EffectIntensity { get; }
            public float RainIntensity { get; }
            public float ThermalMotionCull01 { get; }
        }

        private sealed class VisorFluidPass : ScriptableRenderPass
        {
            private const float MaterialFloatEpsilon = 0.0001f;

            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Fluid Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private Material _lastParameterMaterial;
            private Vector4 _lastLocalVelocityShader = Vector4.positiveInfinity;
            private float _lastIntensity = float.PositiveInfinity;
            private float _lastRainIntensity = float.PositiveInfinity;
            private float _lastWetness = float.PositiveInfinity;
            private float _lastHullStress = float.PositiveInfinity;
            private float _lastDistortionStrength = float.PositiveInfinity;
            private float _lastRunoffSpeed = float.PositiveInfinity;
            private float _lastDropletScale = float.PositiveInfinity;
            private float _lastLateralStreakStrength = float.PositiveInfinity;
            private float _lastForwardStretchStrength = float.PositiveInfinity;
            private float _lastEdgeStreakStrength = float.PositiveInfinity;
            private float _lastEdgeFadeExponent = float.PositiveInfinity;
            private float _lastSpeed01 = float.PositiveInfinity;
            private float _lastThermalMotionCull = float.PositiveInfinity;
            private float _lastAmbientLight = float.PositiveInfinity;
            private float _lastDustStrength = float.PositiveInfinity;
            private float _lastAmbientDustResponse = float.PositiveInfinity;

            public VisorFluidPass()
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
                    (_runtimeState.EffectIntensity <= 0.001f && _runtimeState.RainIntensity <= 0.001f))
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
                destinationDesc.name = "_HectonVisorFluidDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialParameters(_material, _settings, _runtimeState);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Visor Fluid Distortion", out PassData passData, _profilingSampler))
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

                float effectIntensity = Sanitize01(runtimeState.EffectIntensity);
                float rainIntensity = Sanitize01(runtimeState.RainIntensity);
                float wetness = Sanitize01(runtimeState.Wetness);
                float hullStress = Sanitize01(runtimeState.HullStress);
                float ambientLight01 = Sanitize01(runtimeState.AmbientLight01);
                float thermalMotionCull01 = Sanitize01(runtimeState.ThermalMotionCull01);
                Vector3 localVelocity = SanitizeVector(runtimeState.LocalVelocity);
                float lateralVelocity = math.clamp(localVelocity.x * 0.08f, -1f, 1f);
                float forwardVelocity = math.clamp(localVelocity.z * 0.05f, -1f, 1f);
                float verticalVelocity = math.clamp(localVelocity.y * 0.08f, -1f, 1f);
                float speed01 = math.saturate(
                    (localVelocity.x * localVelocity.x +
                     localVelocity.y * localVelocity.y +
                     localVelocity.z * localVelocity.z) * VisorSpeedSquaredToShader01);
                Vector4 localVelocityShader = new Vector4(lateralVelocity, verticalVelocity, forwardVelocity, 0f);
                SetMaterialFloatIfChanged(material, ShaderConstants.IntensityId, effectIntensity, ref _lastIntensity);
                SetMaterialFloatIfChanged(material, ShaderConstants.RainIntensityId, rainIntensity, ref _lastRainIntensity);
                SetMaterialFloatIfChanged(material, ShaderConstants.WetnessId, wetness, ref _lastWetness);
                SetMaterialFloatIfChanged(material, ShaderConstants.HullStressId, hullStress, ref _lastHullStress);
                SetMaterialFloatIfChanged(material, ShaderConstants.DistortionStrengthId, SanitizeNonNegative(settings.distortionStrength), ref _lastDistortionStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.RunoffSpeedId, SanitizeAtLeast(settings.runoffSpeed, 0.1f), ref _lastRunoffSpeed);
                SetMaterialFloatIfChanged(material, ShaderConstants.DropletScaleId, SanitizeAtLeast(settings.dropletScale, 2f), ref _lastDropletScale);
                SetMaterialFloatIfChanged(material, ShaderConstants.LateralStreakStrengthId, Sanitize01(settings.lateralStreakStrength), ref _lastLateralStreakStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.ForwardStretchStrengthId, Sanitize01(settings.forwardStretchStrength), ref _lastForwardStretchStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.EdgeStreakStrengthId, Sanitize01(settings.edgeStreakStrength), ref _lastEdgeStreakStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.EdgeFadeExponentId, SanitizeAtLeast(settings.edgeFadeExponent, 0.1f), ref _lastEdgeFadeExponent);
                SetMaterialFloatIfChanged(material, ShaderConstants.SpeedId, speed01, ref _lastSpeed01);
                SetMaterialVectorIfChanged(material, ShaderConstants.LocalVelocityId, localVelocityShader, ref _lastLocalVelocityShader);
                SetMaterialFloatIfChanged(material, ShaderConstants.ThermalMotionCullId, thermalMotionCull01, ref _lastThermalMotionCull);
                SetMaterialFloatIfChanged(material, ShaderConstants.AmbientLightId, ambientLight01, ref _lastAmbientLight);
                SetMaterialFloatIfChanged(material, ShaderConstants.DustStrengthId, Sanitize01(settings.dustStrength), ref _lastDustStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.AmbientDustResponseId, SanitizeNonNegative(settings.ambientDustResponse), ref _lastAmbientDustResponse);
            }

            private void ResetMaterialParameterCache()
            {
                _lastLocalVelocityShader = Vector4.positiveInfinity;
                _lastIntensity = float.PositiveInfinity;
                _lastRainIntensity = float.PositiveInfinity;
                _lastWetness = float.PositiveInfinity;
                _lastHullStress = float.PositiveInfinity;
                _lastDistortionStrength = float.PositiveInfinity;
                _lastRunoffSpeed = float.PositiveInfinity;
                _lastDropletScale = float.PositiveInfinity;
                _lastLateralStreakStrength = float.PositiveInfinity;
                _lastForwardStretchStrength = float.PositiveInfinity;
                _lastEdgeStreakStrength = float.PositiveInfinity;
                _lastEdgeFadeExponent = float.PositiveInfinity;
                _lastSpeed01 = float.PositiveInfinity;
                _lastThermalMotionCull = float.PositiveInfinity;
                _lastAmbientLight = float.PositiveInfinity;
                _lastDustStrength = float.PositiveInfinity;
                _lastAmbientDustResponse = float.PositiveInfinity;
            }

            private static void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue)
            {
                if (math.abs(value - cachedValue) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }

            private static void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue)
            {
                if (math.abs(value.x - cachedValue.x) <= MaterialFloatEpsilon &&
                    math.abs(value.y - cachedValue.y) <= MaterialFloatEpsilon &&
                    math.abs(value.z - cachedValue.z) <= MaterialFloatEpsilon &&
                    math.abs(value.w - cachedValue.w) <= MaterialFloatEpsilon)
                {
                    return;
                }

                material.SetVector(shaderId, value);
                cachedValue = value;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonVisorFluidIntensity");
            internal static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
            internal static readonly int WetnessId = Shader.PropertyToID("_HectonVisorFluidWetness");
            internal static readonly int HullStressId = Shader.PropertyToID("_HectonVisorFluidHullStress");
            internal static readonly int DistortionStrengthId = Shader.PropertyToID("_HectonVisorFluidDistortionStrength");
            internal static readonly int RunoffSpeedId = Shader.PropertyToID("_HectonVisorFluidRunoffSpeed");
            internal static readonly int DropletScaleId = Shader.PropertyToID("_HectonVisorFluidDropletScale");
            internal static readonly int LateralStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidLateralStreakStrength");
            internal static readonly int ForwardStretchStrengthId = Shader.PropertyToID("_HectonVisorFluidForwardStretchStrength");
            internal static readonly int EdgeStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidEdgeStreakStrength");
            internal static readonly int EdgeFadeExponentId = Shader.PropertyToID("_HectonVisorFluidEdgeFadeExponent");
            internal static readonly int SpeedId = Shader.PropertyToID("_HectonVisorFluidSpeed");
            internal static readonly int LocalVelocityId = Shader.PropertyToID("_HectonVisorFluidLocalVelocity");
            internal static readonly int ThermalMotionCullId = Shader.PropertyToID("_HectonThermalDistortionMotionCull");
            internal static readonly int AmbientLightId = Shader.PropertyToID("_HectonVisorFluidAmbientLight");
            internal static readonly int DustStrengthId = Shader.PropertyToID("_HectonVisorFluidDustStrength");
            internal static readonly int AmbientDustResponseId = Shader.PropertyToID("_HectonVisorFluidAmbientDustResponse");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VisorFluidPass _pass;
        private Material _material;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new VisorFluidPass();
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
            if (renderCamera == null || settings == null)
                return false;

            Camera playerCamera;
            HectonPlayerMovement playerMovement;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                playerCamera = runtimeContext.PlayerCamera;
                playerMovement = runtimeContext.PlayerMovement;
            }
            else
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext == null)
                    return false;

                playerCamera = playerContext.PlayerCamera;
                playerMovement = playerContext.PlayerMovement;
            }

            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            Transform playerCameraTransform = playerCamera.transform;
            float wetness = playerMovement != null ? Sanitize01(playerMovement.CurrentWetLensIntensity01) : 0f;
            float hullStress = playerMovement != null ? Sanitize01(playerMovement.CurrentHullStress01) : 0f;
            float dustStrength = Sanitize01(settings.dustStrength);
            float ambientDustResponse = SanitizeNonNegative(settings.ambientDustResponse);
            float ambientLight01 = 0f;
            float dustContribution = 0f;
            if (dustStrength > 0.001f && ambientDustResponse > 0.001f)
            {
                ambientLight01 = Sanitize01(ResolveAmbientLight01());
                dustContribution = math.saturate(ambientLight01 * dustStrength * ambientDustResponse);
            }

            float hullContribution = math.saturate(
                math.saturate((hullStress - HullStressVisorContributionStart01) * HullStressVisorContributionInvRange) *
                Sanitize01(settings.hullStressContribution));
            float effectIntensity = math.saturate(math.max(math.max(wetness, hullContribution), dustContribution));
            float rainIntensity = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.RainIntensityId));
            if (effectIntensity <= 0.001f && rainIntensity <= 0.001f)
                return false;

            Vector3 localVelocity = Vector3.zero;
            float fluidVelocityActivity = math.max(wetness, hullStress);
            if (playerMovement != null && fluidVelocityActivity > 0.001f)
                localVelocity = ResolveCameraLocalVelocity(playerCameraTransform, playerMovement.InterpolatedLinearVelocity);

            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            float thermalMotionCull01 = localVelocitySq > ThermalDistortionCullSpeedMetersPerSecondSq ? 1f : 0f;
            runtimeState = new RuntimeState(wetness, hullStress, localVelocity, ambientLight01, effectIntensity, rainIntensity, thermalMotionCull01);
            return true;
        }

        private static Vector3 ResolveCameraLocalVelocity(Transform cameraTransform, Vector3 worldVelocity)
        {
            if (cameraTransform == null)
                return Vector3.zero;

            worldVelocity = SanitizeVector(worldVelocity);
            Quaternion cameraRotation = cameraTransform.rotation;
            if (!TrySanitizeQuaternion(cameraRotation, out cameraRotation))
                return Vector3.zero;

            Vector3 cameraRight = cameraRotation * Vector3.right;
            Vector3 cameraUp = cameraRotation * Vector3.up;
            Vector3 cameraForward = cameraRotation * Vector3.forward;
            return new Vector3(
                Vector3.Dot(worldVelocity, cameraRight),
                Vector3.Dot(worldVelocity, cameraUp),
                Vector3.Dot(worldVelocity, cameraForward));
        }

        private static bool TrySanitizeQuaternion(Quaternion value, out Quaternion sanitized)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= QuaternionMinimumLengthSq)
            {
                sanitized = Quaternion.identity;
                return false;
            }

            if (math.abs(lengthSq - 1f) > QuaternionUnitLengthSqEpsilon)
                q *= math.rcp(math.max(ApproximateMagnitude(q), 0.000001f));

            sanitized = new Quaternion(q.x, q.y, q.z, q.w);
            return true;
        }

        private static float ApproximateMagnitude(float4 value)
        {
            float4 absValue = math.abs(value);
            float maxA = math.max(absValue.x, absValue.y);
            float maxB = math.max(absValue.z, absValue.w);
            float maxAxis = math.max(maxA, maxB);
            float minA = math.min(absValue.x, absValue.y);
            float minB = math.min(absValue.z, absValue.w);
            float minAxis = math.min(minA, minB);
            float midSum = absValue.x + absValue.y + absValue.z + absValue.w - maxAxis - minAxis;
            return maxAxis + (midSum * 0.25f) + (minAxis * 0.125f);
        }

        private static float ResolveAmbientLight01()
        {
            Color ambientColor = RenderSettings.ambientLight.linear;
            float colorIntensity = math.max(ambientColor.r, math.max(ambientColor.g, ambientColor.b));
            return math.saturate(math.max(RenderSettings.ambientIntensity, colorIntensity));
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizeAtLeast(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z)
                ? value
                : Vector3.zero;
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
