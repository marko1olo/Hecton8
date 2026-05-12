using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
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

            [Tooltip("When the AO texture is generated in URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPrePasses;

            [Tooltip("Internal render scale of the AO texture.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Depth-only AO radius in eye-space meters.")]
            [Range(0.25f, 2f)] public float radiusMeters = 1.5f;

            [Tooltip("Occlusion darkening applied to ambient only.")]
            [Range(0f, 2f)] public float intensity = 0.68f;

            [Tooltip("Depth rejection slope used to prevent AO bleeding across hard silhouettes.")]
            [Range(1f, 128f)] public float depthSigma = 28f;
        }

        private sealed class VoxelSsaoPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;

            private sealed class ComputePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal TextureHandle depth;
                internal TextureHandle result;
                internal int dispatchX;
                internal int dispatchY;
                internal Vector4 paramsA;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Voxel SSAO");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private RTHandle _aoTexture;
            private int _kernelIndex = -1;
            private uint _threadGroupSizeX = 8;
            private uint _threadGroupSizeY = 8;
            private float _threadGroupInvX = 0.125f;
            private float _threadGroupInvY = 0.125f;

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

                if (_computeShader != null && _kernelIndex < 0)
                {
                    _kernelIndex = _computeShader.FindKernel("ResolveVoxelSSAO");
                    _computeShader.GetKernelThreadGroupSizes(_kernelIndex, out _threadGroupSizeX, out _threadGroupSizeY, out _);
                    _threadGroupInvX = math.rcp(math.max(1f, (float)_threadGroupSizeX));
                    _threadGroupInvY = math.rcp(math.max(1f, (float)_threadGroupSizeY));
                }
            }

            public void Dispose()
            {
                _aoTexture?.Release();
                _aoTexture = null;
                _kernelIndex = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!FrameTimeWatchdog.IsVoxelAmbientOcclusionEnabled)
                    return;

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
                float renderScale = math.clamp(_settings.renderScale, 0.25f, 1f);
                int aoWidth = QuantizeDimension(math.max(1, (int)math.round(depthDesc.width * renderScale)));
                int aoHeight = QuantizeDimension(math.max(1, (int)math.round(depthDesc.height * renderScale)));
                EnsureAoTexture(aoWidth, aoHeight);

                TextureHandle aoTexture = renderGraph.ImportTexture(_aoTexture);
                float projectionScale = math.abs(cameraData.camera.projectionMatrix.m11) * 0.5f * depthDesc.height * math.max(0.01f, _settings.radiusMeters);

                using (var builder = renderGraph.AddComputePass("Hecton Voxel SSAO", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _kernelIndex;
                    passData.depth = depthTexture;
                    passData.result = aoTexture;
                    passData.dispatchX = CeilByThreadGroup(aoWidth, _threadGroupInvX);
                    passData.dispatchY = CeilByThreadGroup(aoHeight, _threadGroupInvY);
                    passData.paramsA = BuildParamsA(_settings, projectionScale);

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(aoTexture, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(aoTexture, ShaderConstants.GlobalTextureId);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.ResultId, data.result);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.ParamsAId, data.paramsA);
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                    });
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CeilByThreadGroup(int dimension, float invThreadGroupSize)
            {
                return math.max(1, (int)math.ceil(math.max(1, dimension) * invThreadGroupSize));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector4 BuildParamsA(FeatureSettings settings, float projectionScale)
            {
                return new Vector4(
                    math.max(0.01f, projectionScale),
                    math.max(0.01f, settings.radiusMeters),
                    math.max(0f, settings.intensity),
                    math.max(0.01f, settings.depthSigma));
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

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = math.max(1, dimension);
                return (safeDimension + RenderTextureBucketSize - 1) & ~(RenderTextureBucketSize - 1);
            }

        }

        private static class ShaderConstants
        {
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonVoxelSSAODepth");
            internal static readonly int ResultId = Shader.PropertyToID("_HectonVoxelSSAOResult");
            internal static readonly int ParamsAId = Shader.PropertyToID("_HectonVoxelSSAOParamsA");
            internal static readonly int GlobalTextureId = Shader.PropertyToID("_HectonVoxelSSAOTex");
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
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

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
