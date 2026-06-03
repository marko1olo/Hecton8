using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Equipment.Auxiliary;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen depth projection for scanner pulses. The shader reconstructs world position from depth and applies a 2D dither projector.
    /// </summary>
    public sealed class HectonScannerProjectionFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [UnityEngine.Serialization.FormerlySerializedAs("shader")]
            public Material material = null;
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Color projectionColor = new Color(0.08f, 0.95f, 1f, 0.72f);
            [Min(4f)] public float gridScale = 36f;
            [Range(0f, 1f)] public float ditherCutoff = 0.42f;
            [Min(0.1f)] public float flickerSpeed = 38f;
            [Min(0.001f)] public float projectionDepthMeters = 38f;
        }

        private readonly struct ProjectionRuntimeState
        {
            public readonly float3 Origin;
            public readonly float3 Right;
            public readonly float3 Up;
            public readonly float3 Forward;
            public readonly float Radius;
            public readonly float Age01;
            public readonly float Intensity;

            public ProjectionRuntimeState(
                float3 origin,
                float3 right,
                float3 up,
                float3 forward,
                float radius,
                float age01,
                float intensity)
            {
                Origin = origin;
                Right = right;
                Up = up;
                Forward = forward;
                Radius = radius;
                Age01 = age01;
                Intensity = intensity;
            }
        }

        private sealed class ProjectionPass : ScriptableRenderPass
        {
            private sealed class ProjectionPassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public Material Material;
                public MaterialPropertyBlock Properties;
                public Vector4 OriginRadius;
                public Vector4 RightDepth;
                public Vector4 UpAge;
                public Vector4 ForwardIntensity;
                public Color ProjectionColor;
                public float GridScale;
                public float DitherCutoff;
                public float FlickerSpeed;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Scanner Projection");
            private FeatureSettings _settings;
            private Material _material;
            private MaterialPropertyBlock _drawProperties;
            private ProjectionRuntimeState _state;

            public ProjectionPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, in ProjectionRuntimeState state)
            {
                _settings = settings;
                _material = material;
                _state = state;
                EnsureDrawPropertiesCold();
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                _drawProperties?.Clear();
                _drawProperties = null;
                _material = null;
                _settings = null;
                _state = default;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null || _drawProperties == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonScannerDepthProjection";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                float age01 = math.saturate(_state.Age01);
                Vector4 originRadius = new Vector4(_state.Origin.x, _state.Origin.y, _state.Origin.z, _state.Radius);
                Vector4 rightDepth = new Vector4(_state.Right.x, _state.Right.y, _state.Right.z, math.max(0.001f, _settings.projectionDepthMeters));
                Vector4 upAge = new Vector4(_state.Up.x, _state.Up.y, _state.Up.z, age01);
                Vector4 forwardIntensity = new Vector4(_state.Forward.x, _state.Forward.y, _state.Forward.z, _state.Intensity);
                float gridScale = math.max(4f, _settings.gridScale);
                float ditherCutoff = math.saturate(_settings.ditherCutoff);
                float flickerSpeed = math.max(0.1f, _settings.flickerSpeed);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ProjectionPassData>(
                           "Hecton Scanner Projection",
                           out ProjectionPassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Material = _material;
                    passData.Properties = _drawProperties;
                    passData.OriginRadius = originRadius;
                    passData.RightDepth = rightDepth;
                    passData.UpAge = upAge;
                    passData.ForwardIntensity = forwardIntensity;
                    passData.ProjectionColor = _settings.projectionColor;
                    passData.GridScale = gridScale;
                    passData.DitherCutoff = ditherCutoff;
                    passData.FlickerSpeed = flickerSpeed;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (ProjectionPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null || data.Properties == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        UpdateDrawProperties(data.Properties, data);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, data.Properties, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private static void UpdateDrawProperties(MaterialPropertyBlock properties, ProjectionPassData data)
            {
                properties.Clear();
                properties.SetVector(ShaderConstants.OriginRadiusId, data.OriginRadius);
                properties.SetVector(ShaderConstants.RightDepthId, data.RightDepth);
                properties.SetVector(ShaderConstants.UpAgeId, data.UpAge);
                properties.SetVector(ShaderConstants.ForwardIntensityId, data.ForwardIntensity);
                properties.SetColor(ShaderConstants.ColorId, data.ProjectionColor);
                properties.SetFloat(ShaderConstants.GridScaleId, data.GridScale);
                properties.SetFloat(ShaderConstants.DitherCutoffId, data.DitherCutoff);
                properties.SetFloat(ShaderConstants.FlickerSpeedId, data.FlickerSpeed);
            }

            private void EnsureDrawPropertiesCold()
            {
                if (_drawProperties != null)
                    return;

                _drawProperties = new MaterialPropertyBlock(); // COLD ALLOC: scanner projection per-pass payload - owner: HECTON_SCANNER_PROJECTION
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int OriginRadiusId = Shader.PropertyToID("_HectonScannerProjectionOriginRadius");
            internal static readonly int RightDepthId = Shader.PropertyToID("_HectonScannerProjectionRightDepth");
            internal static readonly int UpAgeId = Shader.PropertyToID("_HectonScannerProjectionUpAge");
            internal static readonly int ForwardIntensityId = Shader.PropertyToID("_HectonScannerProjectionForwardIntensity");
            internal static readonly int ColorId = Shader.PropertyToID("_HectonScannerProjectionColor");
            internal static readonly int GridScaleId = Shader.PropertyToID("_HectonScannerProjectionGridScale");
            internal static readonly int DitherCutoffId = Shader.PropertyToID("_HectonScannerProjectionDitherCutoff");
            internal static readonly int FlickerSpeedId = Shader.PropertyToID("_HectonScannerProjectionFlickerSpeed");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ProjectionPass _pass;

        public override void Create()
        {
            _pass ??= new ProjectionPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || settings.material == null)
                return;

            if (settings.projectionColor.a <= 0.001f)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            if (!TryResolveLatestAuxiliarySignalState(renderingData.cameraData.camera, out ProjectionRuntimeState state))
                return;

            _pass.Setup(settings, settings.material, in state);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        private static bool TryResolveLatestAuxiliarySignalState(
            Camera camera,
            out ProjectionRuntimeState state)
        {
            state = default;
            ReadOnlySpan<AuxiliarySonarRequestSignal> signals = SignalBus<AuxiliarySonarRequestSignal>.GetSignals();
            for (int i = signals.Length - 1; i >= 0; i--)
            {
                AuxiliarySonarRequestSignal signal = signals[i];
                if ((signal.Flags & AuxiliaryEquipmentFlags.SensorPing) == 0u ||
                    !math.all(math.isfinite(signal.AUP_Position)) ||
                    !math.isfinite(signal.CurrentRadius) ||
                    !math.isfinite(signal.ExpansionRate) ||
                    !math.isfinite(signal.MaxRadius) ||
                    !math.isfinite(signal.Intensity))
                {
                    continue;
                }

                float3 origin = DowncastLocalAupForShader(signal.AUP_Position);
                if (!math.all(math.isfinite(origin)))
                    continue;

                float3 forward = camera != null
                    ? NormalizeVectorRsqrt((float3)camera.transform.forward, new float3(0f, 0f, 1f))
                    : new float3(0f, 0f, 1f);
                float3 upSeed = camera != null
                    ? NormalizeVectorRsqrt((float3)camera.transform.up, new float3(0f, 1f, 0f))
                    : new float3(0f, 1f, 0f);
                if (math.abs(math.dot(upSeed, forward)) > 0.94f)
                    upSeed = math.abs(forward.y) < 0.94f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);

                float3 right = NormalizeVectorRsqrt(math.cross(upSeed, forward), new float3(1f, 0f, 0f));
                float3 up = NormalizeVectorRsqrt(math.cross(forward, right), new float3(0f, 1f, 0f));
                float maxRadius = math.max(0.1f, signal.MaxRadius);
                float radius = math.clamp(signal.CurrentRadius, 0.1f, maxRadius);
                float age01 = math.saturate(radius * math.rcp(maxRadius));
                state = new ProjectionRuntimeState(
                    origin,
                    right,
                    up,
                    forward,
                    radius,
                    age01,
                    math.saturate(signal.Intensity));
                return true;
            }

            return false;
        }

        private static float3 DowncastLocalAupForShader(double3 aup)
        {
            double3 local = aup - HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(local)))
                return new float3(float.NaN, float.NaN, float.NaN);

            local = math.clamp(local, new double3(-1000000.0), new double3(1000000.0));
            return new float3((float)local.x, (float)local.y, (float)local.z);
        }

        private static float3 NormalizeVectorRsqrt(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            float3 normalized = value * math.rsqrt(lengthSq);
            return math.all(math.isfinite(normalized)) ? normalized : fallback;
        }
    }
}
