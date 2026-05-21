using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Equipment.Auxiliary;
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
    /// Fullscreen depth projection for scanner pulses. The shader reconstructs world position from depth and applies a 2D dither projector.
    /// </summary>
    public sealed class HectonScannerProjectionFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_ScannerDepthProjection.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            public Shader shader = null;
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
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Scanner Projection");
            private FeatureSettings _settings;
            private Material _material;
            private ProjectionRuntimeState _state;
            private Vector4 _appliedOriginRadius;
            private Vector4 _appliedRightDepth;
            private Vector4 _appliedUpAge;
            private Vector4 _appliedForwardIntensity;
            private Color _appliedColor;
            private float _appliedGridScale = -1f;
            private float _appliedDitherCutoff = -1f;
            private float _appliedFlickerSpeed = -1f;
            private bool _materialDirty = true;

            public ProjectionPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, in ProjectionRuntimeState state)
            {
                if (!ReferenceEquals(_material, material))
                    _materialDirty = true;

                _settings = settings;
                _material = material;
                _state = state;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
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
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonScannerDepthProjection";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialIfNeeded(_material, _settings, _state);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                           passName: "Hecton Scanner Projection",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialIfNeeded(Material material, FeatureSettings settings, in ProjectionRuntimeState state)
            {
                float age01 = math.saturate(state.Age01);
                Vector4 originRadius = new Vector4(state.Origin.x, state.Origin.y, state.Origin.z, state.Radius);
                Vector4 rightDepth = new Vector4(state.Right.x, state.Right.y, state.Right.z, math.max(0.001f, settings.projectionDepthMeters));
                Vector4 upAge = new Vector4(state.Up.x, state.Up.y, state.Up.z, age01);
                Vector4 forwardIntensity = new Vector4(state.Forward.x, state.Forward.y, state.Forward.z, state.Intensity);
                float gridScale = math.max(4f, settings.gridScale);
                float ditherCutoff = math.saturate(settings.ditherCutoff);
                float flickerSpeed = math.max(0.1f, settings.flickerSpeed);

                if (_materialDirty || Vector4DistanceSq(_appliedOriginRadius, originRadius) > 0.000001f)
                {
                    material.SetVector(ShaderConstants.OriginRadiusId, originRadius);
                    _appliedOriginRadius = originRadius;
                }

                if (_materialDirty || Vector4DistanceSq(_appliedRightDepth, rightDepth) > 0.000001f)
                {
                    material.SetVector(ShaderConstants.RightDepthId, rightDepth);
                    _appliedRightDepth = rightDepth;
                }

                if (_materialDirty || Vector4DistanceSq(_appliedUpAge, upAge) > 0.000001f)
                {
                    material.SetVector(ShaderConstants.UpAgeId, upAge);
                    _appliedUpAge = upAge;
                }

                if (_materialDirty || Vector4DistanceSq(_appliedForwardIntensity, forwardIntensity) > 0.000001f)
                {
                    material.SetVector(ShaderConstants.ForwardIntensityId, forwardIntensity);
                    _appliedForwardIntensity = forwardIntensity;
                }

                if (_materialDirty || ColorDistanceSq(_appliedColor, settings.projectionColor) > 0.000001f)
                {
                    material.SetColor(ShaderConstants.ColorId, settings.projectionColor);
                    _appliedColor = settings.projectionColor;
                }

                if (_materialDirty || math.abs(_appliedGridScale - gridScale) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.GridScaleId, gridScale);
                    _appliedGridScale = gridScale;
                }

                if (_materialDirty || math.abs(_appliedDitherCutoff - ditherCutoff) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.DitherCutoffId, ditherCutoff);
                    _appliedDitherCutoff = ditherCutoff;
                }

                if (_materialDirty || math.abs(_appliedFlickerSpeed - flickerSpeed) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.FlickerSpeedId, flickerSpeed);
                    _appliedFlickerSpeed = flickerSpeed;
                }

                _materialDirty = false;
            }

            private static float Vector4DistanceSq(Vector4 a, Vector4 b)
            {
                float x = a.x - b.x;
                float y = a.y - b.y;
                float z = a.z - b.z;
                float w = a.w - b.w;
                return x * x + y * y + z * z + w * w;
            }

            private static float ColorDistanceSq(Color a, Color b)
            {
                float r = a.r - b.r;
                float g = a.g - b.g;
                float bl = a.b - b.b;
                float alpha = a.a - b.a;
                return r * r + g * g + bl * bl + alpha * alpha;
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
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ProjectionPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new ProjectionPass();
            Shader shader = settings != null ? settings.shader : null;
            RecreateMaterial(ref _material, shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            if (settings.projectionColor.a <= 0.001f)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            if (!TryResolveLatestAuxiliarySignalState(renderingData.cameraData.camera, out ProjectionRuntimeState state))
                return;

            _pass.Setup(settings, _material, in state);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
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
