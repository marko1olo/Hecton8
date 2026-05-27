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
            private const bool HasRuntimeConsumer = false;
            internal static bool HasRuntimeConsumerAvailable => HasRuntimeConsumer;

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
            private ComputeShader _resolvedComputeShader;
            private int _kernelIndex = -1;
            private uint _threadGroupSizeX;
            private uint _threadGroupSizeY;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

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

                if (!ReferenceEquals(_resolvedComputeShader, _computeShader))
                    ClearKernelState();

                if (_computeShader != null && _kernelIndex < 0)
                {
                    if (!TryResolveKernel(_computeShader, "ResolveVoxelSSAO", out _kernelIndex, out _threadGroupSizeX, out _threadGroupSizeY))
                    {
                        ClearKernelState();
                    }
                    else
                    {
                        _resolvedComputeShader = _computeShader;
                    }
                }
            }

            public void Dispose()
            {
                ClearKernelState();
            }

            private void ClearKernelState()
            {
                _resolvedComputeShader = null;
                _kernelIndex = -1;
                _threadGroupSizeX = 0u;
                _threadGroupSizeY = 0u;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HasRuntimeConsumer)
                    return;

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
                int dispatchX = ResolveDispatchGroups(aoWidth, _threadGroupSizeX);
                int dispatchY = ResolveDispatchGroups(aoHeight, _threadGroupSizeY);
                if (dispatchX <= 0 || dispatchY <= 0)
                    return;

                TextureDesc aoDesc = new TextureDesc(depthDesc);
                aoDesc.name = "_HectonVoxelSSAOTexture";
                aoDesc.width = aoWidth;
                aoDesc.height = aoHeight;
                aoDesc.depthBufferBits = DepthBits.None;
                aoDesc.msaaSamples = MSAASamples.None;
                aoDesc.colorFormat = GraphicsFormat.R8_UNorm;
                aoDesc.clearBuffer = true;
                aoDesc.clearColor = Color.white;
                aoDesc.filterMode = FilterMode.Bilinear;
                aoDesc.enableRandomWrite = true;
                aoDesc.useMipMap = false;
                aoDesc.autoGenerateMips = false;

                TextureHandle aoTexture = renderGraph.CreateTexture(aoDesc);
                float projectionScale = math.abs(cameraData.camera.projectionMatrix.m11) * 0.5f * depthDesc.height * math.max(0.01f, _settings.radiusMeters);

                using (var builder = renderGraph.AddComputePass("Hecton Voxel SSAO", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _kernelIndex;
                    passData.depth = depthTexture;
                    passData.result = aoTexture;
                    passData.dispatchX = dispatchX;
                    passData.dispatchY = dispatchY;
                    passData.paramsA = BuildParamsA(_settings, projectionScale);

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(aoTexture, AccessFlags.Write);

                    builder.SetRenderFunc((ComputePassData data, ComputeGraphContext context) =>
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
            private static int ResolveDispatchGroups(int dimension, uint threadGroupSize)
            {
                if (dimension <= 0 || threadGroupSize == 0u)
                    return 0;

                long groups = ((long)dimension + threadGroupSize - 1L) / threadGroupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private static bool TryResolveKernel(ComputeShader computeShader, string kernelName, out int kernelIndex, out uint groupSizeX, out uint groupSizeY)
            {
                kernelIndex = -1;
                groupSizeX = 0u;
                groupSizeY = 0u;
                if (computeShader == null || !computeShader.HasKernel(kernelName))
                    return false;

                int resolvedKernel = computeShader.FindKernel(kernelName);
                if (resolvedKernel < 0 || !computeShader.IsSupported(resolvedKernel))
                    return false;

                computeShader.GetKernelThreadGroupSizes(resolvedKernel, out uint x, out uint y, out uint z);
                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                kernelIndex = resolvedKernel;
                groupSizeX = x;
                groupSizeY = y;
                return true;
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
            if (!VoxelSsaoPass.HasRuntimeConsumerAvailable)
                return;

            if (settings == null ||
                settings.computeShader == null ||
                _pass == null ||
                !SystemInfo.supportsComputeShaders)
            {
                return;
            }

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
