using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Rendering
{
    public sealed class HectonBilateralDrsUpscalerFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_BilateralUpscale.compute";
#endif

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        private static Vector2 ResolveProjectionJitterPixels(Camera camera, int fullWidth, int fullHeight)
        {
            if (camera == null)
                return Vector2.zero;

            Matrix4x4 projection = camera.projectionMatrix;
            float jitterX = Mathf.Clamp(projection.m02 * fullWidth * 0.5f, -1f, 1f);
            float jitterY = Mathf.Clamp(projection.m12 * fullHeight * 0.5f, -1f, 1f);
            return new Vector2(jitterX, jitterY);
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Edge-preserving DRS reconstruction compute shader.")]
            public ComputeShader computeShader = null;

            [Tooltip("Injection point after opaque rendering and before post processing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Run even when the active color texture reports full-size dimensions. Used for validation captures only.")]
            public bool forceRunAtFullResolution = false;

            [Tooltip("Minimum source/full dimension ratio below which the pass engages automatically.")]
            [Range(0.25f, 1f)] public float activationScale = 0.995f;
        }

        private struct GraphicsCapabilities
        {
            public bool SupportsComputeShaders;
            public bool Supports2DArrayTextures;
            public GraphicsFormat EdgeMaskLoadStoreFormat;
            public GraphicsFormat EdgeMaskRenderFormat;
            public GraphicsFormat OutputLoadStoreFallbackFormat;
            public GraphicsFormat OutputLoadStoreFormat0;
            public GraphicsFormat OutputLoadStoreFormat1;

            public bool HasEdgeMaskLoadStoreFormat => EdgeMaskLoadStoreFormat != GraphicsFormat.None;
            public bool HasEdgeMaskRenderFormat => EdgeMaskRenderFormat != GraphicsFormat.None;
            public bool HasOutputLoadStoreFallbackFormat => OutputLoadStoreFallbackFormat != GraphicsFormat.None;

            public bool SupportsOutputLoadStoreFormat(GraphicsFormat format)
            {
                return format != GraphicsFormat.None &&
                       (format == OutputLoadStoreFormat0 ||
                        format == OutputLoadStoreFormat1 ||
                        format == OutputLoadStoreFallbackFormat);
            }
        }

        private sealed class BilateralDrsPass : ScriptableRenderPass
        {
            private const string ClearKernelName = "ClearEdgeMask";
            private const string ClearArrayKernelName = "ClearEdgeMaskArray";
            private const string SobelKernelName = "SobelDepthMask";
            private const string SobelArrayKernelName = "SobelDepthMaskArray";
            private const string UpscaleKernelName = "BilateralUpscale";
            private const string UpscaleArrayKernelName = "BilateralUpscaleArray";
            private const string DebugKernelName = "EdgeMaskDebugComposite";
            private const string DebugArrayKernelName = "EdgeMaskDebugCompositeArray";
            private const float MinimumEdgeMaskQualityGate = 0.03125f;

            private sealed class ClearPassData
            {
                internal ComputeShader ComputeShader;
                internal int KernelIndex;
                internal TextureHandle EdgeMask;
                internal int EdgeMaskId;
                internal int DispatchX;
                internal int DispatchY;
                internal int DispatchZ;
            }

            private sealed class RasterClearPassData
            {
            }

            private sealed class SobelPassData
            {
                internal ComputeShader ComputeShader;
                internal int KernelIndex;
                internal uint ThreadGroupSizeX;
                internal uint ThreadGroupSizeY;
                internal TextureHandle Depth;
                internal TextureHandle EdgeMask;
                internal GraphicsBuffer ConstantBuffer;
                internal int DepthId;
                internal int EdgeMaskId;
                internal int DispatchX;
                internal int DispatchY;
                internal int DispatchZ;
            }

            private sealed class UpscalePassData
            {
                internal ComputeShader ComputeShader;
                internal int KernelIndex;
                internal uint ThreadGroupSizeX;
                internal uint ThreadGroupSizeY;
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle EdgeMask;
                internal TextureHandle Destination;
                internal GraphicsBuffer ConstantBuffer;
                internal int SourceId;
                internal int DepthId;
                internal int EdgeMaskId;
                internal int DestinationId;
                internal int DispatchX;
                internal int DispatchY;
                internal int DispatchZ;
            }

            private sealed class DebugPassData
            {
                internal ComputeShader ComputeShader;
                internal int KernelIndex;
                internal TextureHandle EdgeMask;
                internal TextureHandle Destination;
                internal GraphicsBuffer ConstantBuffer;
                internal int EdgeMaskId;
                internal int DestinationId;
                internal int DispatchX;
                internal int DispatchY;
                internal int DispatchZ;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Bilateral DRS Upscaler");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private int _clearKernel = -1;
            private int _clearArrayKernel = -1;
            private int _sobelKernel = -1;
            private int _sobelArrayKernel = -1;
            private int _upscaleKernel = -1;
            private int _upscaleArrayKernel = -1;
            private int _debugKernel = -1;
            private int _debugArrayKernel = -1;
            private uint _clearThreadGroupSizeX;
            private uint _clearThreadGroupSizeY;
            private uint _clearArrayThreadGroupSizeX;
            private uint _clearArrayThreadGroupSizeY;
            private uint _sobelThreadGroupSizeX;
            private uint _sobelThreadGroupSizeY;
            private uint _sobelArrayThreadGroupSizeX;
            private uint _sobelArrayThreadGroupSizeY;
            private uint _upscaleThreadGroupSizeX;
            private uint _upscaleThreadGroupSizeY;
            private uint _upscaleArrayThreadGroupSizeX;
            private uint _upscaleArrayThreadGroupSizeY;
            private uint _debugThreadGroupSizeX;
            private uint _debugThreadGroupSizeY;
            private uint _debugArrayThreadGroupSizeX;
            private uint _debugArrayThreadGroupSizeY;
            private bool _reportedMissingKernels;
            private bool _clearOnly;
            private GraphicsCapabilities _graphicsCapabilities;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

            public BilateralDrsPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                ComputeShader computeShader,
                bool clearOnly,
                in GraphicsCapabilities graphicsCapabilities)
            {
                _settings = settings;
                _clearOnly = clearOnly;
                _graphicsCapabilities = graphicsCapabilities;
                if (!ReferenceEquals(_computeShader, computeShader))
                {
                    _computeShader = computeShader;
                    _clearKernel = -1;
                    _clearArrayKernel = -1;
                    _sobelKernel = -1;
                    _sobelArrayKernel = -1;
                    _upscaleKernel = -1;
                    _upscaleArrayKernel = -1;
                    _debugKernel = -1;
                    _debugArrayKernel = -1;
                    ResetThreadGroups();
                    _reportedMissingKernels = false;
                }

                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(clearOnly ? ScriptableRenderPassInput.None : ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;

                if (_computeShader != null && (_clearKernel < 0 || _clearArrayKernel < 0))
                {
                    if (!TryResolveClearKernel(_computeShader))
                        return;

                    if (!TryResolveThreadGroups(_computeShader, _clearKernel, out _clearThreadGroupSizeX, out _clearThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _clearArrayKernel, out _clearArrayThreadGroupSizeX, out _clearArrayThreadGroupSizeY))
                    {
                        _clearKernel = -1;
                        _clearArrayKernel = -1;
                        _clearThreadGroupSizeX = 0u;
                        _clearThreadGroupSizeY = 0u;
                        _clearArrayThreadGroupSizeX = 0u;
                        _clearArrayThreadGroupSizeY = 0u;
                        return;
                    }
                }

                if (_computeShader != null &&
                    !clearOnly &&
                    (_sobelKernel < 0 || _sobelArrayKernel < 0 ||
                     _upscaleKernel < 0 || _upscaleArrayKernel < 0 ||
                     _debugKernel < 0 || _debugArrayKernel < 0))
                {
                    if (!TryResolveActiveKernels(_computeShader))
                        return;

                    if (!TryResolveThreadGroups(_computeShader, _sobelKernel, out _sobelThreadGroupSizeX, out _sobelThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _sobelArrayKernel, out _sobelArrayThreadGroupSizeX, out _sobelArrayThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _upscaleKernel, out _upscaleThreadGroupSizeX, out _upscaleThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _upscaleArrayKernel, out _upscaleArrayThreadGroupSizeX, out _upscaleArrayThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _debugKernel, out _debugThreadGroupSizeX, out _debugThreadGroupSizeY) ||
                        !TryResolveThreadGroups(_computeShader, _debugArrayKernel, out _debugArrayThreadGroupSizeX, out _debugArrayThreadGroupSizeY))
                    {
                        _sobelKernel = -1;
                        _sobelArrayKernel = -1;
                        _upscaleKernel = -1;
                        _upscaleArrayKernel = -1;
                        _debugKernel = -1;
                        _debugArrayKernel = -1;
                        ClearActiveThreadGroups();
                    }
                }
            }

            private void ResetThreadGroups()
            {
                _clearThreadGroupSizeX = 0u;
                _clearThreadGroupSizeY = 0u;
                _clearArrayThreadGroupSizeX = 0u;
                _clearArrayThreadGroupSizeY = 0u;
                ClearActiveThreadGroups();
            }

            private void ClearActiveThreadGroups()
            {
                _sobelThreadGroupSizeX = 0u;
                _sobelThreadGroupSizeY = 0u;
                _sobelArrayThreadGroupSizeX = 0u;
                _sobelArrayThreadGroupSizeY = 0u;
                _upscaleThreadGroupSizeX = 0u;
                _upscaleThreadGroupSizeY = 0u;
                _upscaleArrayThreadGroupSizeX = 0u;
                _upscaleArrayThreadGroupSizeY = 0u;
                _debugThreadGroupSizeX = 0u;
                _debugThreadGroupSizeY = 0u;
                _debugArrayThreadGroupSizeX = 0u;
                _debugArrayThreadGroupSizeY = 0u;
            }

            private static bool TryResolveThreadGroups(ComputeShader computeShader, int kernel, out uint groupSizeX, out uint groupSizeY)
            {
                groupSizeX = 0u;
                groupSizeY = 0u;
                if (computeShader == null || kernel < 0 || !computeShader.IsSupported(kernel))
                    return false;

                computeShader.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                groupSizeX = x;
                groupSizeY = y;
                return true;
            }

            private bool TryResolveClearKernel(ComputeShader computeShader)
            {
                if (computeShader == null ||
                    !computeShader.HasKernel(ClearKernelName) ||
                    !computeShader.HasKernel(ClearArrayKernelName))
                {
                    _clearKernel = -1;
                    _clearArrayKernel = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!_reportedMissingKernels)
                    {
                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing a clear edge-mask kernel.");
                        _reportedMissingKernels = true;
                    }
#endif
                    return false;
                }

                _clearKernel = computeShader.FindKernel(ClearKernelName);
                _clearArrayKernel = computeShader.FindKernel(ClearArrayKernelName);
                bool resolved = _clearKernel >= 0 && _clearArrayKernel >= 0;
                if (resolved)
                    _reportedMissingKernels = false;
                return resolved;
            }

            private bool TryResolveActiveKernels(ComputeShader computeShader)
            {
                if (computeShader == null ||
                    !computeShader.HasKernel(SobelKernelName) ||
                    !computeShader.HasKernel(SobelArrayKernelName) ||
                    !computeShader.HasKernel(UpscaleKernelName) ||
                    !computeShader.HasKernel(UpscaleArrayKernelName) ||
                    !computeShader.HasKernel(DebugKernelName) ||
                    !computeShader.HasKernel(DebugArrayKernelName))
                {
                    _sobelKernel = -1;
                    _sobelArrayKernel = -1;
                    _upscaleKernel = -1;
                    _upscaleArrayKernel = -1;
                    _debugKernel = -1;
                    _debugArrayKernel = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!_reportedMissingKernels)
                    {
                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing one or more active upscaler kernels.");
                        _reportedMissingKernels = true;
                    }
#endif
                    return false;
                }

                _sobelKernel = computeShader.FindKernel(SobelKernelName);
                _sobelArrayKernel = computeShader.FindKernel(SobelArrayKernelName);
                _upscaleKernel = computeShader.FindKernel(UpscaleKernelName);
                _upscaleArrayKernel = computeShader.FindKernel(UpscaleArrayKernelName);
                _debugKernel = computeShader.FindKernel(DebugKernelName);
                _debugArrayKernel = computeShader.FindKernel(DebugArrayKernelName);
                bool resolved = _sobelKernel >= 0 && _sobelArrayKernel >= 0 &&
                                _upscaleKernel >= 0 && _upscaleArrayKernel >= 0 &&
                                _debugKernel >= 0 && _debugArrayKernel >= 0;
                if (resolved)
                    _reportedMissingKernels = false;
                return resolved;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null)
                {
                    return;
                }

                if (_computeShader == null ||
                    !_graphicsCapabilities.SupportsComputeShaders ||
                    _clearKernel < 0)
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                if (_clearOnly ||
                    _sobelKernel < 0 ||
                    _upscaleKernel < 0 ||
                    _debugKernel < 0)
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc depthDesc = renderGraph.GetTextureDesc(depthTexture);
                if (!TryResolveTextureMode(
                        sourceDesc,
                        depthDesc,
                        _graphicsCapabilities.Supports2DArrayTextures,
                        out bool useTextureArray,
                        out int sliceCount))
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                if (!TryResolveEdgeMaskFormat(out GraphicsFormat edgeMaskFormat) ||
                    !TryResolveOutputColorFormat(sourceDesc.colorFormat, out GraphicsFormat outputColorFormat))
                {
                    TryPublishClearedEdgeMask(renderGraph);
                    return;
                }

                int sourceTextureWidth = Math.Max(1, sourceDesc.width);
                int sourceTextureHeight = Math.Max(1, sourceDesc.height);
                int cameraPixelWidth = cameraData.camera != null ? Math.Max(1, cameraData.camera.pixelWidth) : 1;
                int cameraPixelHeight = cameraData.camera != null ? Math.Max(1, cameraData.camera.pixelHeight) : 1;

                if (!HectonBilateralDrsUpscalerRuntime.TryReadActiveParameters(out UpscalerParamsDTO activeParameters) ||
                    !TryResolveLogicalDimensions(
                        in activeParameters,
                        sourceTextureWidth,
                        sourceTextureHeight,
                        cameraPixelWidth,
                        cameraPixelHeight,
                        out int sourceWidth,
                        out int sourceHeight,
                        out int fullWidth,
                        out int fullHeight))
                {
                    PublishClearedEdgeMask(renderGraph, edgeMaskFormat);
                    return;
                }

                float scaleX = sourceWidth / (float)Math.Max(1, fullWidth);
                float scaleY = sourceHeight / (float)Math.Max(1, fullHeight);
                float effectiveScale = Mathf.Min(scaleX, scaleY);
                if (!_settings.forceRunAtFullResolution && effectiveScale >= Mathf.Clamp(_settings.activationScale, 0.25f, 1f))
                {
                    PublishClearedEdgeMask(renderGraph, edgeMaskFormat);
                    return;
                }

                if (!HectonBilateralDrsUpscalerRuntime.TryGetActiveConstantBufferForDimensions(
                    sourceWidth,
                    sourceHeight,
                    fullWidth,
                    fullHeight,
                    out GraphicsBuffer constantBuffer,
                    out _))
                {
                    PublishClearedEdgeMask(renderGraph, edgeMaskFormat);
                    return;
                }

                float qualityGate = ResolveQualityGate(activeParameters.FilterParams.w);
                float edgeMaskQualityGate = ResolveEdgeMaskQualityGate(qualityGate);
                BufferHandle constantBufferHandle = renderGraph.ImportBuffer(constantBuffer);
                int edgeWidth = ResolveEdgeMaskDimension(fullWidth, edgeMaskQualityGate);
                int edgeHeight = ResolveEdgeMaskDimension(fullHeight, edgeMaskQualityGate);
                int dispatchZ = useTextureArray ? ResolveDispatchDepth(sliceCount) : 1;
                if (dispatchZ <= 0)
                    return;

                int edgeMaskReadId = useTextureArray ? BilateralDrsShaderIds.EdgeMaskArrayReadId : BilateralDrsShaderIds.EdgeMaskReadId;
                int edgeMaskWriteId = useTextureArray ? BilateralDrsShaderIds.EdgeMaskArrayWriteId : BilateralDrsShaderIds.EdgeMaskWriteId;
                int depthId = useTextureArray ? BilateralDrsShaderIds.FullResDepthArrayId : BilateralDrsShaderIds.FullResDepthId;
                int sourceId = useTextureArray ? BilateralDrsShaderIds.LowResColorArrayId : BilateralDrsShaderIds.LowResColorId;
                int destinationId = useTextureArray ? BilateralDrsShaderIds.UpscaledColorArrayId : BilateralDrsShaderIds.UpscaledColorId;
                VRTextureUsage outputVrUsage = useTextureArray ? sourceDesc.vrUsage : VRTextureUsage.None;
                int sobelKernel = useTextureArray ? _sobelArrayKernel : _sobelKernel;
                int debugKernel = useTextureArray ? _debugArrayKernel : _debugKernel;
                int upscaleKernel = useTextureArray ? _upscaleArrayKernel : _upscaleKernel;
                uint sobelGroupSizeX = useTextureArray ? _sobelArrayThreadGroupSizeX : _sobelThreadGroupSizeX;
                uint sobelGroupSizeY = useTextureArray ? _sobelArrayThreadGroupSizeY : _sobelThreadGroupSizeY;
                uint debugGroupSizeX = useTextureArray ? _debugArrayThreadGroupSizeX : _debugThreadGroupSizeX;
                uint debugGroupSizeY = useTextureArray ? _debugArrayThreadGroupSizeY : _debugThreadGroupSizeY;
                uint upscaleGroupSizeX = useTextureArray ? _upscaleArrayThreadGroupSizeX : _upscaleThreadGroupSizeX;
                uint upscaleGroupSizeY = useTextureArray ? _upscaleArrayThreadGroupSizeY : _upscaleThreadGroupSizeY;

                TextureDesc edgeDesc = CreateEdgeMaskDesc(edgeWidth, edgeHeight, edgeMaskFormat, false, useTextureArray, sliceCount, outputVrUsage);
                edgeDesc.name = "_HectonBilateralDrsEdgeMask";
                TextureHandle edgeMask = renderGraph.CreateTexture(edgeDesc);

                TextureDesc outputDesc = new TextureDesc(fullWidth, fullHeight, dynamicResolution: false, xrReady: useTextureArray);
                outputDesc.name = "_HectonBilateralDrsUpscaledColor";
                outputDesc.clearBuffer = false;
                outputDesc.depthBufferBits = DepthBits.None;
                outputDesc.colorFormat = outputColorFormat;
                outputDesc.msaaSamples = MSAASamples.None;
                outputDesc.dimension = useTextureArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
                outputDesc.slices = useTextureArray ? sliceCount : 1;
                outputDesc.vrUsage = outputVrUsage;
                outputDesc.enableRandomWrite = true;
                outputDesc.filterMode = FilterMode.Bilinear;
                outputDesc.wrapMode = TextureWrapMode.Clamp;
                outputDesc.useMipMap = false;
                outputDesc.autoGenerateMips = false;
                outputDesc.useDynamicScale = false;
                outputDesc.useDynamicScaleExplicit = false;
                TextureHandle outputTexture = renderGraph.CreateTexture(outputDesc);

                int dispatchX = CeilByThreadGroup(edgeWidth, sobelGroupSizeX);
                int dispatchY = CeilByThreadGroup(edgeHeight, sobelGroupSizeY);
                if (dispatchX <= 0 || dispatchY <= 0)
                    return;

                using (var builder = renderGraph.AddComputePass("Hecton Bilateral DRS Sobel Edge Mask", out SobelPassData passData, _profilingSampler))
                {
                    passData.ComputeShader = _computeShader;
                    passData.KernelIndex = sobelKernel;
                    passData.ThreadGroupSizeX = sobelGroupSizeX;
                    passData.ThreadGroupSizeY = sobelGroupSizeY;
                    passData.Depth = depthTexture;
                    passData.EdgeMask = edgeMask;
                    passData.ConstantBuffer = constantBuffer;
                    passData.DepthId = depthId;
                    passData.EdgeMaskId = edgeMaskWriteId;
                    passData.DispatchX = dispatchX;
                    passData.DispatchY = dispatchY;
                    passData.DispatchZ = dispatchZ;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(edgeMask, AccessFlags.Write);
                    builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(edgeMask, BilateralDrsShaderIds.EdgeMaskGlobalId);
                    builder.SetRenderFunc(static (SobelPassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.DepthId, data.Depth);
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.EdgeMaskId, data.EdgeMask);
                        cmd.SetComputeConstantBufferParam(data.ComputeShader, BilateralDrsShaderIds.ConstantBufferId, data.ConstantBuffer, 0, BilateralDrsUpscalerConstants.CBufferBytes);
                        cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, data.DispatchX, data.DispatchY, data.DispatchZ);
                    });
                }

                if (HectonBilateralDrsUpscalerRuntime.IsEdgeMaskDebugEnabled())
                {
                    int debugDispatchX = CeilByThreadGroup(fullWidth, debugGroupSizeX);
                    int debugDispatchY = CeilByThreadGroup(fullHeight, debugGroupSizeY);
                    if (debugDispatchX <= 0 || debugDispatchY <= 0)
                        return;

                    using (var builder = renderGraph.AddComputePass("Hecton Bilateral DRS Edge Mask Debug", out DebugPassData passData, _profilingSampler))
                    {
                        passData.ComputeShader = _computeShader;
                        passData.KernelIndex = debugKernel;
                        passData.EdgeMask = edgeMask;
                        passData.Destination = outputTexture;
                        passData.ConstantBuffer = constantBuffer;
                        passData.EdgeMaskId = edgeMaskReadId;
                        passData.DestinationId = destinationId;
                        passData.DispatchX = debugDispatchX;
                        passData.DispatchY = debugDispatchY;
                        passData.DispatchZ = dispatchZ;

                        builder.UseTexture(edgeMask, AccessFlags.Read);
                        builder.UseTexture(outputTexture, AccessFlags.Write);
                        builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                        builder.SetGlobalTextureAfterPass(edgeMask, BilateralDrsShaderIds.EdgeMaskGlobalId);
                        builder.SetRenderFunc(static (DebugPassData data, ComputeGraphContext context) =>
                        {
                            var cmd = context.cmd;
                            cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.EdgeMaskId, data.EdgeMask);
                            cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.DestinationId, data.Destination);
                            cmd.SetComputeConstantBufferParam(data.ComputeShader, BilateralDrsShaderIds.ConstantBufferId, data.ConstantBuffer, 0, BilateralDrsUpscalerConstants.CBufferBytes);
                            cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, data.DispatchX, data.DispatchY, data.DispatchZ);
                        });
                    }

                    UpdateCameraDescriptor(cameraData, fullWidth, fullHeight, outputColorFormat);
                    resourceData.cameraColor = outputTexture;
                    return;
                }

                int upscaleDispatchX = CeilByThreadGroup(fullWidth, upscaleGroupSizeX);
                int upscaleDispatchY = CeilByThreadGroup(fullHeight, upscaleGroupSizeY);
                if (upscaleDispatchX <= 0 || upscaleDispatchY <= 0)
                    return;

                using (var builder = renderGraph.AddComputePass("Hecton Bilateral DRS Upscale", out UpscalePassData passData, _profilingSampler))
                {
                    passData.ComputeShader = _computeShader;
                    passData.KernelIndex = upscaleKernel;
                    passData.ThreadGroupSizeX = upscaleGroupSizeX;
                    passData.ThreadGroupSizeY = upscaleGroupSizeY;
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.EdgeMask = edgeMask;
                    passData.Destination = outputTexture;
                    passData.ConstantBuffer = constantBuffer;
                    passData.SourceId = sourceId;
                    passData.DepthId = depthId;
                    passData.EdgeMaskId = edgeMaskReadId;
                    passData.DestinationId = destinationId;
                    passData.DispatchX = upscaleDispatchX;
                    passData.DispatchY = upscaleDispatchY;
                    passData.DispatchZ = dispatchZ;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(edgeMask, AccessFlags.Read);
                    builder.UseTexture(outputTexture, AccessFlags.Write);
                    builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(edgeMask, BilateralDrsShaderIds.EdgeMaskGlobalId);
                    builder.SetRenderFunc(static (UpscalePassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.SourceId, data.Source);
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.DepthId, data.Depth);
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.EdgeMaskId, data.EdgeMask);
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.DestinationId, data.Destination);
                        cmd.SetComputeConstantBufferParam(data.ComputeShader, BilateralDrsShaderIds.ConstantBufferId, data.ConstantBuffer, 0, BilateralDrsUpscalerConstants.CBufferBytes);
                        cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, data.DispatchX, data.DispatchY, data.DispatchZ);
                    });
                }

                UpdateCameraDescriptor(cameraData, fullWidth, fullHeight, outputColorFormat);
                resourceData.cameraColor = outputTexture;
            }

            private static bool TryResolveTextureMode(
                TextureDesc sourceDesc,
                TextureDesc depthDesc,
                bool supports2DArrayTextures,
                out bool useTextureArray,
                out int sliceCount)
            {
                useTextureArray = false;
                sliceCount = 1;
                if (sourceDesc.msaaSamples != MSAASamples.None ||
                    depthDesc.msaaSamples != MSAASamples.None)
                {
                    return false;
                }

                if (sourceDesc.dimension == TextureDimension.Tex2D &&
                    depthDesc.dimension == TextureDimension.Tex2D &&
                    sourceDesc.slices == 1 &&
                    depthDesc.slices == 1)
                {
                    return true;
                }

                if (sourceDesc.dimension != TextureDimension.Tex2DArray ||
                    depthDesc.dimension != TextureDimension.Tex2DArray)
                {
                    return false;
                }

                if (!supports2DArrayTextures)
                    return false;

                int sourceSlices = sourceDesc.slices;
                int depthSlices = depthDesc.slices;
                if (sourceSlices <= 0 || depthSlices <= 0)
                    return false;

                if (sourceSlices != depthSlices || sourceSlices > 2)
                    return false;

                useTextureArray = true;
                sliceCount = sourceSlices;
                return true;
            }

            private static void UpdateCameraDescriptor(UniversalCameraData cameraData, int width, int height, GraphicsFormat colorFormat)
            {
                if (cameraData == null)
                    return;

                cameraData.cameraTargetDescriptor.width = Math.Max(1, width);
                cameraData.cameraTargetDescriptor.height = Math.Max(1, height);
                if (colorFormat != GraphicsFormat.None)
                    cameraData.cameraTargetDescriptor.graphicsFormat = colorFormat;
            }

            private bool TryPublishClearedEdgeMask(RenderGraph renderGraph)
            {
                if (_graphicsCapabilities.SupportsComputeShaders &&
                    _computeShader != null &&
                    _clearKernel >= 0 &&
                    TryResolveEdgeMaskFormat(out GraphicsFormat edgeMaskFormat))
                {
                    if (PublishClearedEdgeMask(renderGraph, edgeMaskFormat))
                        return true;
                }

                return TryPublishRasterClearedEdgeMask(renderGraph);
            }

            private bool PublishClearedEdgeMask(RenderGraph renderGraph, GraphicsFormat edgeMaskFormat)
            {
                TextureDesc edgeDesc = CreateEdgeMaskDesc(1, 1, edgeMaskFormat, true, false, 1, VRTextureUsage.None);
                edgeDesc.name = "_HectonBilateralDrsEdgeMask_Clear";
                TextureHandle edgeMask = renderGraph.CreateTexture(edgeDesc);
                return RecordClearEdgeMaskPass(renderGraph, edgeMask, true, false, 1);
            }

            private bool RecordClearEdgeMaskPass(
                RenderGraph renderGraph,
                TextureHandle edgeMask,
                bool publishGlobal,
                bool useTextureArray,
                int sliceCount)
            {
                int dispatchX = CeilByThreadGroup(1, useTextureArray ? _clearArrayThreadGroupSizeX : _clearThreadGroupSizeX);
                int dispatchY = CeilByThreadGroup(1, useTextureArray ? _clearArrayThreadGroupSizeY : _clearThreadGroupSizeY);
                int dispatchZ = useTextureArray ? ResolveDispatchDepth(sliceCount) : 1;
                if (dispatchX <= 0 || dispatchY <= 0 || dispatchZ <= 0)
                    return false;

                using (var builder = renderGraph.AddComputePass("Hecton Bilateral DRS Edge Mask Clear", out ClearPassData passData, _profilingSampler))
                {
                    passData.ComputeShader = _computeShader;
                    passData.KernelIndex = useTextureArray ? _clearArrayKernel : _clearKernel;
                    passData.EdgeMask = edgeMask;
                    passData.EdgeMaskId = useTextureArray ? BilateralDrsShaderIds.EdgeMaskArrayWriteId : BilateralDrsShaderIds.EdgeMaskWriteId;
                    passData.DispatchX = dispatchX;
                    passData.DispatchY = dispatchY;
                    passData.DispatchZ = dispatchZ;

                    builder.UseTexture(edgeMask, AccessFlags.Write);
                    if (publishGlobal)
                        builder.SetGlobalTextureAfterPass(edgeMask, BilateralDrsShaderIds.EdgeMaskGlobalId);
                    builder.SetRenderFunc(static (ClearPassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, data.EdgeMaskId, data.EdgeMask);
                        cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, data.DispatchX, data.DispatchY, data.DispatchZ);
                    });
                }

                return true;
            }

            private bool TryPublishRasterClearedEdgeMask(RenderGraph renderGraph)
            {
                if (!TryResolveRasterEdgeMaskFormat(out GraphicsFormat edgeMaskFormat))
                    return false;

                TextureDesc edgeDesc = CreateRasterEdgeMaskDesc(edgeMaskFormat);
                TextureHandle edgeMask = renderGraph.CreateTexture(edgeDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<RasterClearPassData>(
                           "Hecton Bilateral DRS Edge Mask Raster Clear",
                           out RasterClearPassData _,
                           _profilingSampler))
                {
                    builder.SetRenderAttachment(edgeMask, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(edgeMask, BilateralDrsShaderIds.EdgeMaskGlobalId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (RasterClearPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(false, true, Color.black);
                    });
                }

                return true;
            }

            private static TextureDesc CreateRasterEdgeMaskDesc(GraphicsFormat edgeMaskFormat)
            {
                TextureDesc edgeDesc = new TextureDesc(1, 1, dynamicResolution: false, xrReady: false);
                edgeDesc.name = "_HectonBilateralDrsEdgeMask_RasterClear";
                edgeDesc.clearBuffer = true;
                edgeDesc.clearColor = Color.black;
                edgeDesc.depthBufferBits = DepthBits.None;
                edgeDesc.colorFormat = edgeMaskFormat;
                edgeDesc.msaaSamples = MSAASamples.None;
                edgeDesc.dimension = TextureDimension.Tex2D;
                edgeDesc.slices = 1;
                edgeDesc.vrUsage = VRTextureUsage.None;
                edgeDesc.enableRandomWrite = false;
                edgeDesc.filterMode = FilterMode.Point;
                edgeDesc.wrapMode = TextureWrapMode.Clamp;
                edgeDesc.useMipMap = false;
                edgeDesc.autoGenerateMips = false;
                edgeDesc.useDynamicScale = false;
                edgeDesc.useDynamicScaleExplicit = false;
                return edgeDesc;
            }

            private static TextureDesc CreateEdgeMaskDesc(
                int width,
                int height,
                GraphicsFormat edgeMaskFormat,
                bool clearBuffer,
                bool useTextureArray,
                int sliceCount,
                VRTextureUsage vrUsage)
            {
                TextureDesc edgeDesc = new TextureDesc(Math.Max(1, width), Math.Max(1, height), dynamicResolution: false, xrReady: useTextureArray);
                edgeDesc.clearBuffer = clearBuffer;
                edgeDesc.clearColor = Color.black;
                edgeDesc.depthBufferBits = DepthBits.None;
                edgeDesc.colorFormat = edgeMaskFormat;
                edgeDesc.msaaSamples = MSAASamples.None;
                edgeDesc.dimension = useTextureArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
                edgeDesc.slices = useTextureArray ? Math.Max(1, sliceCount) : 1;
                edgeDesc.vrUsage = useTextureArray ? vrUsage : VRTextureUsage.None;
                edgeDesc.enableRandomWrite = true;
                edgeDesc.filterMode = FilterMode.Point;
                edgeDesc.wrapMode = TextureWrapMode.Clamp;
                edgeDesc.useMipMap = false;
                edgeDesc.autoGenerateMips = false;
                edgeDesc.useDynamicScale = false;
                edgeDesc.useDynamicScaleExplicit = false;
                return edgeDesc;
            }

            private static bool TryResolveLogicalDimensions(
                in UpscalerParamsDTO parameters,
                int sourceTextureWidth,
                int sourceTextureHeight,
                int cameraPixelWidth,
                int cameraPixelHeight,
                out int lowWidth,
                out int lowHeight,
                out int fullWidth,
                out int fullHeight)
            {
                lowWidth = 0;
                lowHeight = 0;
                fullWidth = 0;
                fullHeight = 0;
                if (!TryRoundPositiveDimension(parameters.ResolutionParams.x, out lowWidth) ||
                    !TryRoundPositiveDimension(parameters.ResolutionParams.y, out lowHeight) ||
                    !TryRoundPositiveDimension(parameters.ResolutionParams.z, out fullWidth) ||
                    !TryRoundPositiveDimension(parameters.ResolutionParams.w, out fullHeight))
                {
                    return false;
                }

                int maxFullWidth = Math.Max(Math.Max(1, sourceTextureWidth), Math.Max(1, cameraPixelWidth));
                int maxFullHeight = Math.Max(Math.Max(1, sourceTextureHeight), Math.Max(1, cameraPixelHeight));
                return lowWidth <= Math.Max(1, sourceTextureWidth) &&
                       lowHeight <= Math.Max(1, sourceTextureHeight) &&
                       fullWidth <= maxFullWidth &&
                       fullHeight <= maxFullHeight &&
                       lowWidth <= fullWidth &&
                       lowHeight <= fullHeight;
            }

            private static bool TryRoundPositiveDimension(float value, out int dimension)
            {
                dimension = 0;
                if (!IsFinite(value))
                    return false;

                dimension = Math.Max(1, Mathf.RoundToInt(value));
                return dimension > 0;
            }

            private static float ResolveQualityGate(float quality)
            {
                if (!IsFinite(quality))
                    return 0f;

                float denominator = Math.Max(0.0001f, BilateralDrsUpscalerConstants.QualityGateEnd - BilateralDrsUpscalerConstants.QualityGateStart);
                float t = Mathf.Clamp01((quality - BilateralDrsUpscalerConstants.QualityGateStart) / denominator);
                return t * t * (3f - 2f * t);
            }

            private static float ResolveEdgeMaskQualityGate(float qualityGate)
            {
                return Mathf.Lerp(MinimumEdgeMaskQualityGate, 1f, Mathf.Clamp01(qualityGate));
            }

            private static int ResolveEdgeMaskDimension(int fullDimension, float qualityGate)
            {
                float scale = Mathf.Clamp01(qualityGate);
                return Math.Max(1, Mathf.CeilToInt(Math.Max(1, fullDimension) * scale));
            }

            private static int CeilByThreadGroup(int dimension, uint threadGroupSize)
            {
                if (dimension <= 0 || threadGroupSize == 0u)
                    return 0;

                long groups = ((long)dimension + threadGroupSize - 1L) / threadGroupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private static int ResolveDispatchDepth(int value)
            {
                return value > 0 && value <= MaxDispatchGroupsPerDimension ? value : 0;
            }

            private static bool IsFinite(float value)
            {
                return float.IsFinite(value);
            }

            private static GraphicsFormat ResolveColorFormat(GraphicsFormat sourceFormat)
            {
                return sourceFormat == GraphicsFormat.None || GraphicsFormatUtility.IsDepthStencilFormat(sourceFormat)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : sourceFormat;
            }

            private bool TryResolveOutputColorFormat(GraphicsFormat sourceFormat, out GraphicsFormat colorFormat)
            {
                colorFormat = ResolveColorFormat(sourceFormat);
                if (_graphicsCapabilities.SupportsOutputLoadStoreFormat(colorFormat))
                    return true;

                colorFormat = _graphicsCapabilities.OutputLoadStoreFallbackFormat;
                return _graphicsCapabilities.HasOutputLoadStoreFallbackFormat;
            }

            private bool TryResolveEdgeMaskFormat(out GraphicsFormat edgeMaskFormat)
            {
                edgeMaskFormat = _graphicsCapabilities.EdgeMaskLoadStoreFormat;
                return _graphicsCapabilities.HasEdgeMaskLoadStoreFormat;
            }

            private bool TryResolveRasterEdgeMaskFormat(out GraphicsFormat edgeMaskFormat)
            {
                edgeMaskFormat = _graphicsCapabilities.EdgeMaskRenderFormat;
                return _graphicsCapabilities.HasEdgeMaskRenderFormat;
            }

        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();
        private BilateralDrsPass _pass;
        private GraphicsCapabilities _graphicsCapabilities;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif
            CacheGraphicsCapabilitiesCold();
            _pass ??= new BilateralDrsPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!HectonBilateralDrsUpscalerRuntime.TryGetRuntimeInstance(out _) ||
                renderer == null ||
                settings == null ||
                _pass == null)
            {
                return;
            }

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            if (settings.computeShader == null ||
                !_graphicsCapabilities.SupportsComputeShaders)
            {
                _pass.Setup(settings, settings.computeShader, true, in _graphicsCapabilities);
                renderer.EnqueuePass(_pass);
                return;
            }

            Camera renderCamera = renderingData.cameraData.camera;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            if (IsUnsupportedRenderTargetDescriptor(descriptor, _graphicsCapabilities.Supports2DArrayTextures))
            {
                _pass.Setup(settings, settings.computeShader, true, in _graphicsCapabilities);
                renderer.EnqueuePass(_pass);
                return;
            }

            int sourceWidth = Math.Max(1, descriptor.width);
            int sourceHeight = Math.Max(1, descriptor.height);
            int fullWidth = renderCamera != null ? Math.Max(sourceWidth, renderCamera.pixelWidth) : sourceWidth;
            int fullHeight = renderCamera != null ? Math.Max(sourceHeight, renderCamera.pixelHeight) : sourceHeight;
            bool descriptorLooksScaled = sourceWidth < fullWidth || sourceHeight < fullHeight;
            int submittedLowWidth = settings.forceRunAtFullResolution || descriptorLooksScaled ? sourceWidth : 0;
            int submittedLowHeight = settings.forceRunAtFullResolution || descriptorLooksScaled ? sourceHeight : 0;
            Vector2 jitterPixels = ResolveProjectionJitterPixels(renderCamera, fullWidth, fullHeight);
            HectonBilateralDrsUpscalerRuntime.SubmitRenderDimensions(
                submittedLowWidth,
                submittedLowHeight,
                fullWidth,
                fullHeight,
                jitterPixels.x,
                jitterPixels.y);

            _pass.Setup(settings, settings.computeShader, false, in _graphicsCapabilities);
            renderer.EnqueuePass(_pass);
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _graphicsCapabilities = BuildGraphicsCapabilitiesCold();
        }

        private static GraphicsCapabilities BuildGraphicsCapabilitiesCold()
        {
            GraphicsCapabilities capabilities = default;
            capabilities.SupportsComputeShaders = SystemInfo.supportsComputeShaders;
            capabilities.Supports2DArrayTextures = SystemInfo.supports2DArrayTextures;
            capabilities.EdgeMaskLoadStoreFormat = ResolveFirstSupportedFormatCold(
                GraphicsFormat.R8_UNorm,
                GraphicsFormat.R16_SFloat,
                GraphicsFormatUsage.LoadStore);
            capabilities.EdgeMaskRenderFormat = ResolveFirstSupportedFormatCold(
                GraphicsFormat.R8_UNorm,
                GraphicsFormat.R16_SFloat,
                GraphicsFormat.R8G8B8A8_UNorm,
                GraphicsFormatUsage.Render);
            capabilities.OutputLoadStoreFormat0 = ResolveSupportedFormatCold(
                GraphicsFormat.R16G16B16A16_SFloat,
                GraphicsFormatUsage.LoadStore);
            capabilities.OutputLoadStoreFormat1 = ResolveSupportedFormatCold(
                GraphicsFormat.R8G8B8A8_UNorm,
                GraphicsFormatUsage.LoadStore);
            capabilities.OutputLoadStoreFallbackFormat =
                capabilities.OutputLoadStoreFormat0 != GraphicsFormat.None
                    ? capabilities.OutputLoadStoreFormat0
                    : capabilities.OutputLoadStoreFormat1;
            return capabilities;
        }

        private static GraphicsFormat ResolveFirstSupportedFormatCold(
            GraphicsFormat first,
            GraphicsFormat second,
            GraphicsFormatUsage usage)
        {
            GraphicsFormat resolved = ResolveSupportedFormatCold(first, usage);
            return resolved != GraphicsFormat.None
                ? resolved
                : ResolveSupportedFormatCold(second, usage);
        }

        private static GraphicsFormat ResolveFirstSupportedFormatCold(
            GraphicsFormat first,
            GraphicsFormat second,
            GraphicsFormat third,
            GraphicsFormatUsage usage)
        {
            GraphicsFormat resolved = ResolveFirstSupportedFormatCold(first, second, usage);
            return resolved != GraphicsFormat.None
                ? resolved
                : ResolveSupportedFormatCold(third, usage);
        }

        private static GraphicsFormat ResolveSupportedFormatCold(GraphicsFormat format, GraphicsFormatUsage usage)
        {
            return format != GraphicsFormat.None && SystemInfo.IsFormatSupported(format, usage)
                ? format
                : GraphicsFormat.None;
        }

        private static bool IsUnsupportedRenderTargetDescriptor(RenderTextureDescriptor descriptor, bool supports2DArrayTextures)
        {
            bool supportedDimension = descriptor.dimension == TextureDimension.Tex2D ||
                                      (descriptor.dimension == TextureDimension.Tex2DArray &&
                                       supports2DArrayTextures &&
                                       descriptor.volumeDepth > 0 &&
                                       descriptor.volumeDepth <= 2);
            return !supportedDimension ||
                   descriptor.msaaSamples > 1;
        }
    }

    public static class BilateralDrsShaderIds
    {
        public static readonly int ConstantBufferId = Shader.PropertyToID("HectonBilateralUpscalerParams");
        public static readonly int LowResColorId = Shader.PropertyToID("_H8LowResColor");
        public static readonly int LowResColorArrayId = Shader.PropertyToID("_H8LowResColorArray");
        public static readonly int FullResDepthId = Shader.PropertyToID("_H8FullResDepth");
        public static readonly int FullResDepthArrayId = Shader.PropertyToID("_H8FullResDepthArray");
        public static readonly int EdgeMaskReadId = Shader.PropertyToID("_H8EdgeMaskRead");
        public static readonly int EdgeMaskWriteId = Shader.PropertyToID("_H8EdgeMaskWrite");
        public static readonly int EdgeMaskArrayReadId = Shader.PropertyToID("_H8EdgeMaskArrayRead");
        public static readonly int EdgeMaskArrayWriteId = Shader.PropertyToID("_H8EdgeMaskArrayWrite");
        public static readonly int UpscaledColorId = Shader.PropertyToID("_H8UpscaledColor");
        public static readonly int UpscaledColorArrayId = Shader.PropertyToID("_H8UpscaledColorArray");
        public static readonly int EdgeMaskGlobalId = Shader.PropertyToID("_H8BilateralDrsEdgeMask");
    }
}
