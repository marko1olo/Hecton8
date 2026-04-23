using System;
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
    /// Cheap cave-focused SSAO built from the camera depth prepass and consumed by first-party voxel rock shaders.
    /// </summary>
    public sealed class HectonVoxelSsaoFeature : ScriptableRendererFeature
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VoxelSSAO.compute";

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Compute shader used to resolve voxel-focused SSAO from the camera depth prepass.")]
            public ComputeShader computeShader = null;

            [Tooltip("Optional blue-noise texture used to rotate the low-sample kernel.")]
            public Texture2D blueNoiseTexture = null;

            [Tooltip("When the AO texture is generated in URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPrePasses;

            [Tooltip("Internal render scale of the AO texture.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("World-space AO radius in meters.")]
            [Range(0.25f, 2f)] public float radiusMeters = 1.5f;

            [Tooltip("Occlusion darkening applied to ambient only.")]
            [Range(0f, 2f)] public float intensity = 0.68f;

            [Tooltip("Depth rejection slope used to prevent AO bleeding across hard silhouettes.")]
            [Range(1f, 128f)] public float depthSigma = 28f;

            [Tooltip("Number of blue-noise rotated depth taps.")]
            [Range(4, 6)] public int sampleCount = 4;
        }

        private sealed class VoxelSsaoPass : ScriptableRenderPass, IDisposable
        {
            private sealed class ComputePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle depth;
                internal TextureHandle result;
                internal TextureHandle blueNoiseTexture;
                internal Vector4 inputSize;
                internal Vector4 outputSize;
                internal Matrix4x4 inverseViewProjection;
                internal float projectionScale;
                internal float radiusMeters;
                internal float intensity;
                internal float depthSigma;
                internal int sampleCount;
                internal float hasBlueNoise;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Voxel SSAO");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private RTHandle _aoTexture;
            private RTHandle _blueNoiseTextureHandle;
            private Texture _blueNoiseTextureSource;
            private int _kernelIndex = -1;
            private uint _threadGroupSizeX = 8;
            private uint _threadGroupSizeY = 8;

            public VoxelSsaoPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = false;
            }

            public void Setup(FeatureSettings settings, ComputeShader computeShader)
            {
                _settings = settings;
                _computeShader = computeShader;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.AfterRenderingPrePasses;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                EnsureBlueNoiseTextureHandle(settings != null ? settings.blueNoiseTexture : null);

                if (_computeShader != null && _kernelIndex < 0)
                {
                    _kernelIndex = _computeShader.FindKernel("ResolveVoxelSSAO");
                    _computeShader.GetKernelThreadGroupSizes(_kernelIndex, out _threadGroupSizeX, out _threadGroupSizeY, out _);
                }
            }

            public void Dispose()
            {
                _aoTexture?.Release();
                _blueNoiseTextureHandle?.Release();
                _aoTexture = null;
                _blueNoiseTextureHandle = null;
                _blueNoiseTextureSource = null;
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                _kernelIndex = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);

                if (_settings == null || _computeShader == null || _kernelIndex < 0)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!depthTexture.IsValid())
                    return;

                TextureDesc depthDesc = renderGraph.GetTextureDesc(depthTexture);
                int aoWidth = Mathf.Max(1, Mathf.RoundToInt(depthDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));
                int aoHeight = Mathf.Max(1, Mathf.RoundToInt(depthDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));
                EnsureAoTexture(aoWidth, aoHeight);

                TextureHandle aoTexture = renderGraph.ImportTexture(_aoTexture);
                Camera camera = cameraData.camera;
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                Matrix4x4 inverseViewProjection = (projectionMatrix * camera.worldToCameraMatrix).inverse;
                float projectionScale = Mathf.Abs(projectionMatrix.m11) * 0.5f * depthDesc.height * Mathf.Max(0.01f, _settings.radiusMeters);

                using (var builder = renderGraph.AddComputePass("Hecton Voxel SSAO", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _kernelIndex;
                    passData.threadGroupSizeX = _threadGroupSizeX;
                    passData.threadGroupSizeY = _threadGroupSizeY;
                    passData.depth = depthTexture;
                    passData.result = aoTexture;
                    TextureHandle blueNoiseTexture = default;
                    if (_blueNoiseTextureHandle != null)
                    {
                        blueNoiseTexture = renderGraph.ImportTexture(_blueNoiseTextureHandle);
                        builder.UseTexture(blueNoiseTexture, AccessFlags.Read);
                    }

                    passData.blueNoiseTexture = blueNoiseTexture;
                    passData.inputSize = new Vector4(depthDesc.width, depthDesc.height, 1f / Mathf.Max(1, depthDesc.width), 1f / Mathf.Max(1, depthDesc.height));
                    passData.outputSize = new Vector4(aoWidth, aoHeight, 1f / Mathf.Max(1, aoWidth), 1f / Mathf.Max(1, aoHeight));
                    passData.inverseViewProjection = inverseViewProjection;
                    passData.projectionScale = projectionScale;
                    passData.radiusMeters = Mathf.Max(0.01f, _settings.radiusMeters);
                    passData.intensity = Mathf.Max(0f, _settings.intensity);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = Mathf.Clamp(_settings.sampleCount, 4, 6);
                    passData.hasBlueNoise = _settings.blueNoiseTexture != null ? 1f : 0f;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(aoTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(aoTexture, ShaderConstants.GlobalTextureId);
                    if (blueNoiseTexture.IsValid())
                        builder.SetGlobalTextureAfterPass(blueNoiseTexture, ShaderConstants.BlueNoiseId);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.outputSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.outputSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.ResultId, data.result);
                        if (data.hasBlueNoise > 0f)
                            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.BlueNoiseId, data.blueNoiseTexture);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.InputSizeId, data.inputSize);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.OutputSizeId, data.outputSize);
                        cmd.SetComputeMatrixParam(data.computeShader, ShaderConstants.InverseViewProjectionId, data.inverseViewProjection);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.ProjectionScaleId, data.projectionScale);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.RadiusMetersId, data.radiusMeters);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.IntensityId, data.intensity);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.DepthSigmaId, data.depthSigma);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.HasBlueNoiseId, data.hasBlueNoise);
                        cmd.SetComputeIntParam(data.computeShader, ShaderConstants.SampleCountId, data.sampleCount);
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                        cmd.SetGlobalFloat(ShaderConstants.ActiveId, 1f);
                    });
                }
            }

            private void EnsureAoTexture(int width, int height)
            {
                if (_aoTexture != null &&
                    _aoTexture.rt != null &&
                    _aoTexture.rt.width == width &&
                    _aoTexture.rt.height == height)
                {
                    return;
                }

                _aoTexture?.Release();
                _aoTexture = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R8_UNorm,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonVoxelSSAOTexture");
            }

            private void EnsureBlueNoiseTextureHandle(Texture2D blueNoiseTexture)
            {
                if (_blueNoiseTextureSource == blueNoiseTexture && _blueNoiseTextureHandle != null)
                    return;

                _blueNoiseTextureHandle?.Release();
                _blueNoiseTextureHandle = null;
                _blueNoiseTextureSource = blueNoiseTexture;

                if (blueNoiseTexture == null)
                    return;

                _blueNoiseTextureHandle = RTHandles.Alloc(blueNoiseTexture);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonVoxelSSAODepth");
            internal static readonly int ResultId = Shader.PropertyToID("_HectonVoxelSSAOResult");
            internal static readonly int BlueNoiseId = Shader.PropertyToID("_BlueNoiseTex");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonVoxelSSAOInputSize");
            internal static readonly int OutputSizeId = Shader.PropertyToID("_HectonVoxelSSAOOutputSize");
            internal static readonly int InverseViewProjectionId = Shader.PropertyToID("_HectonVoxelSSAOInverseViewProjection");
            internal static readonly int ProjectionScaleId = Shader.PropertyToID("_HectonVoxelSSAOProjectionScale");
            internal static readonly int RadiusMetersId = Shader.PropertyToID("_HectonVoxelSSAORadiusMeters");
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonVoxelSSAOIntensity");
            internal static readonly int DepthSigmaId = Shader.PropertyToID("_HectonVoxelSSAODepthSigma");
            internal static readonly int HasBlueNoiseId = Shader.PropertyToID("_HectonVoxelSSAOHasBlueNoise");
            internal static readonly int SampleCountId = Shader.PropertyToID("_HectonVoxelSSAOSampleCount");
            internal static readonly int GlobalTextureId = Shader.PropertyToID("_HectonVoxelSSAOTex");
            internal static readonly int ActiveId = Shader.PropertyToID("_HectonVoxelSSAOActive");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VoxelSsaoPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif

            _pass ??= new VoxelSsaoPass();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.computeShader == null || _pass == null)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            _pass.Setup(settings, settings.computeShader);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }
    }
}
