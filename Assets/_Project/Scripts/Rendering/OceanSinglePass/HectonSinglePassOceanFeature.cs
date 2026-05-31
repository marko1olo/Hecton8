using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Rendering.OceanSinglePass
{
    public static class H8OceanSinglePassShaderIds
    {
        public static readonly int ConstantBufferId = Shader.PropertyToID("HectonOceanVisualOverrides");
        public static readonly int SourceDepthId = Shader.PropertyToID("_H8OceanSourceDepth");
        public static readonly int DepthFoamMaskId = Shader.PropertyToID("_H8OceanDepthFoamMask");
        public static readonly int WakeTextureId = Shader.PropertyToID("_H8OceanWakeDisplacement");
        public static readonly int WakeTextureWriteId = Shader.PropertyToID("_H8OceanWakeDisplacementWrite");
        public static readonly int WakeParamsId = Shader.PropertyToID("_H8OceanWakeParams");
        public static readonly int WakeScrollOffsetId = Shader.PropertyToID("_H8OceanWakeScrollOffset");
        public static readonly int WakeEventsId = Shader.PropertyToID("_H8PropwashEvents");
        public static readonly int WakeEventCountId = Shader.PropertyToID("_H8PropwashEventCount");
        public static readonly int WakeResolutionId = Shader.PropertyToID("_H8WakeResolution");
        public static readonly int ShorelineFoamBufferId = Shader.PropertyToID("_GlobalShorelineFoam");
        public static readonly int ShorelineFoamCountId = Shader.PropertyToID("_GlobalShorelineFoamCount");
        public static readonly int ShorelineFoamRuntimeId = Shader.PropertyToID("_GlobalShorelineFoamRuntime");
    }

    public sealed class HectonSinglePassOceanFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string DepthShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_OceanDepthFoam.shader";
        private const string WakeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_WakeDisplacement.compute";
#endif

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader that derives ocean depth/shoreline scalar from the primary camera depth texture.")]
            public Shader depthFoamShader = null;
            [Tooltip("Compute shader that accumulates PropwashEventDTO wake ripples into a single RenderGraph texture.")]
            public ComputeShader wakeCompute = null;
            [Tooltip("URP injection point before water transparents sample the generated masks.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
        }

        private sealed class SinglePassOceanPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _depthSampler = new ProfilingSampler("Hecton Ocean Depth Mask");
            private readonly ProfilingSampler _wakeSampler = new ProfilingSampler("Hecton Ocean Wake Compute");
            private FeatureSettings _settings;
            private Material _depthMaterial;
            private int _clearWakeKernel = -1;
            private int _accumulateWakeKernel = -1;
            private uint _clearThreadGroupSizeX;
            private uint _clearThreadGroupSizeY;
            private uint _accumulateThreadGroupSizeX;
            private uint _accumulateThreadGroupSizeY;
            private ComputeShader _resolvedWakeCompute;
            private bool _supportsComputeShadersCold;
            private bool _resolvedSupportsComputeShaders;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

            public SinglePassOceanPass()
            {
                profilingSampler = _wakeSampler;
                requiresIntermediateTexture = false;
            }

            public void Setup(FeatureSettings settings, Material depthMaterial, bool supportsComputeShadersCold)
            {
                _settings = settings;
                _depthMaterial = depthMaterial;
                _supportsComputeShadersCold = supportsComputeShadersCold;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);

                ComputeShader wakeCompute = settings != null ? settings.wakeCompute : null;
                if (!ReferenceEquals(_resolvedWakeCompute, wakeCompute) ||
                    _resolvedSupportsComputeShaders != supportsComputeShadersCold)
                {
                    ResolveKernels(wakeCompute, supportsComputeShadersCold);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null)
                    return;

                if (!OceanSinglePassRuntime.TryEnterRenderGraphRuntimeGate())
                    return;

                if (!OceanSinglePassRuntime.TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out _))
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!depthTexture.IsValid())
                    return;

                TextureDesc depthDesc = renderGraph.GetTextureDesc(depthTexture);
                bool useTextureArray = false;
                int sliceCount = 1;
                BufferHandle constantBufferHandle = renderGraph.ImportBuffer(constantBuffer);
                bool hasShorelineFoam = ShorelineFoamGraftRuntime.TryGetActiveBuffer(
                    out GraphicsBuffer shorelineFoamBuffer,
                    out int shorelineFoamCount,
                    out Vector4 shorelineFoamRuntime);
                BufferHandle shorelineFoamHandle = default;
                if (hasShorelineFoam)
                    shorelineFoamHandle = renderGraph.ImportBuffer(shorelineFoamBuffer);
                TextureHandle depthMask = CreateDepthMask(renderGraph, depthDesc, useTextureArray, sliceCount);
                RecordDepthMaskPass(
                    renderGraph,
                    depthTexture,
                    depthMask,
                    constantBufferHandle,
                    constantBuffer,
                    shorelineFoamHandle,
                    shorelineFoamBuffer,
                    hasShorelineFoam ? shorelineFoamCount : 0,
                    shorelineFoamRuntime);

                if (_supportsComputeShadersCold &&
                    _settings.wakeCompute != null &&
                    _clearWakeKernel >= 0 &&
                    _accumulateWakeKernel >= 0)
                {
                    RecordWakeComputePass(renderGraph, constantBufferHandle, constantBuffer, useTextureArray, sliceCount);
                }
                else
                {
                    PublishClearedWakeTexture(renderGraph);
                }
            }

            private TextureHandle CreateDepthMask(RenderGraph renderGraph, TextureDesc depthDesc, bool useTextureArray, int sliceCount)
            {
                int width = Math.Max(1, depthDesc.width >> 1);
                int height = Math.Max(1, depthDesc.height >> 1);
                TextureDesc maskDesc = new TextureDesc(width, height, dynamicResolution: false, xrReady: useTextureArray);
                maskDesc.name = "_H8OceanDepthFoamMask";
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.black;
                maskDesc.depthBufferBits = DepthBits.None;
                maskDesc.colorFormat = GraphicsFormat.R16_SFloat;
                maskDesc.msaaSamples = MSAASamples.None;
                maskDesc.dimension = useTextureArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
                maskDesc.slices = useTextureArray ? sliceCount : 1;
                maskDesc.enableRandomWrite = false;
                maskDesc.filterMode = FilterMode.Bilinear;
                maskDesc.wrapMode = TextureWrapMode.Clamp;
                maskDesc.useMipMap = false;
                maskDesc.autoGenerateMips = false;
                maskDesc.useDynamicScale = false;
                maskDesc.useDynamicScaleExplicit = false;
                return renderGraph.CreateTexture(maskDesc);
            }

            private void RecordDepthMaskPass(
                RenderGraph renderGraph,
                TextureHandle depthTexture,
                TextureHandle depthMask,
                BufferHandle constantBufferHandle,
                GraphicsBuffer constantBuffer,
                BufferHandle shorelineFoamHandle,
                GraphicsBuffer shorelineFoamBuffer,
                int shorelineFoamCount,
                Vector4 shorelineFoamRuntime)
            {
                if (_depthMaterial == null)
                    return;

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DepthPassData>(
                           "Hecton Ocean Single-Camera Depth",
                           out DepthPassData passData,
                           _depthSampler))
                {
                    passData.Depth = depthTexture;
                    passData.Material = _depthMaterial;
                    passData.ConstantBuffer = constantBuffer;
                    passData.ShorelineFoamBuffer = shorelineFoamBuffer;
                    passData.ShorelineFoamCount = math.clamp(shorelineFoamCount, 0, ShorelineFoamConstants.ShaderLoopMax);
                    passData.ShorelineFoamRuntime = shorelineFoamRuntime;
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                    if (shorelineFoamBuffer != null && shorelineFoamBuffer.IsValid() && passData.ShorelineFoamCount > 0)
                        builder.UseBuffer(shorelineFoamHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(depthMask, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depthMask, H8OceanSinglePassShaderIds.DepthFoamMaskId);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (DepthPassData data, RasterGraphContext context) =>
                    {
                        long startTicks = Stopwatch.GetTimestamp();
                        context.cmd.BeginSample("H8 Ocean Depth Dear Lie");
                        context.cmd.SetGlobalTexture(H8OceanSinglePassShaderIds.SourceDepthId, data.Depth);
                        context.cmd.SetGlobalConstantBuffer(
                            data.ConstantBuffer,
                            H8OceanSinglePassShaderIds.ConstantBufferId,
                            0,
                            OceanSinglePassConstants.CBufferBytes);
                        context.cmd.SetGlobalInt(H8OceanSinglePassShaderIds.ShorelineFoamCountId, data.ShorelineFoamCount);
                        context.cmd.SetGlobalVector(H8OceanSinglePassShaderIds.ShorelineFoamRuntimeId, data.ShorelineFoamRuntime);
                        if (data.ShorelineFoamCount > 0 && data.ShorelineFoamBuffer != null && data.ShorelineFoamBuffer.IsValid())
                            context.cmd.SetGlobalBuffer(H8OceanSinglePassShaderIds.ShorelineFoamBufferId, data.ShorelineFoamBuffer);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                        context.cmd.EndSample("H8 Ocean Depth Dear Lie");
                        OceanSinglePassRuntime.ReportRenderGraphTelemetry(
                            TicksToMicroseconds(Stopwatch.GetTimestamp() - startTicks),
                            -1f,
                            -1f,
                            1u);
                    });
                }
            }

            private void RecordWakeComputePass(
                RenderGraph renderGraph,
                BufferHandle constantBufferHandle,
                GraphicsBuffer constantBuffer,
                bool useTextureArray,
                int sliceCount)
            {
                OceanSinglePassRuntime.TryGetWakeState(out int resolution, out float scale, out float4 scrollOffset);
                int dispatchZ = useTextureArray ? ResolveDispatchDepth(sliceCount) : 1;
                int clearDispatchX = CeilByThreadGroup(resolution, _clearThreadGroupSizeX);
                int clearDispatchY = CeilByThreadGroup(resolution, _clearThreadGroupSizeY);
                int accumulateDispatchX = CeilByThreadGroup(resolution, _accumulateThreadGroupSizeX);
                int accumulateDispatchY = CeilByThreadGroup(resolution, _accumulateThreadGroupSizeY);
                if (dispatchZ <= 0 ||
                    clearDispatchX <= 0 ||
                    clearDispatchY <= 0 ||
                    accumulateDispatchX <= 0 ||
                    accumulateDispatchY <= 0)
                {
                    PublishClearedWakeTexture(renderGraph);
                    return;
                }

                TextureHandle wakeTexture = CreateWakeTexture(renderGraph, resolution, useTextureArray, sliceCount);
                bool hasEvents = OceanSinglePassRuntime.TryGetWakeEventBuffer(out GraphicsBuffer eventBuffer, out int eventCount);
                BufferHandle eventBufferHandle = default;
                if (hasEvents)
                    eventBufferHandle = renderGraph.ImportBuffer(eventBuffer);

                using (var builder = renderGraph.AddComputePass("Hecton Ocean Wake Compute", out WakePassData passData, _wakeSampler))
                {
                    passData.Compute = _settings.wakeCompute;
                    passData.ClearKernel = _clearWakeKernel;
                    passData.AccumulateKernel = _accumulateWakeKernel;
                    passData.WakeTexture = wakeTexture;
                    passData.ConstantBuffer = constantBuffer;
                    passData.EventBuffer = eventBuffer;
                    passData.EventCount = hasEvents ? eventCount : 0;
                    passData.Resolution = resolution;
                    passData.ResolutionScale = scale;
                    passData.ScrollOffset = scrollOffset;
                    passData.ClearDispatchX = clearDispatchX;
                    passData.ClearDispatchY = clearDispatchY;
                    passData.AccumulateDispatchX = accumulateDispatchX;
                    passData.AccumulateDispatchY = accumulateDispatchY;
                    passData.DispatchZ = dispatchZ;

                    builder.UseTexture(wakeTexture, AccessFlags.Write);
                    builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                    if (hasEvents)
                        builder.UseBuffer(eventBufferHandle, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(wakeTexture, H8OceanSinglePassShaderIds.WakeTextureId);
                    builder.SetRenderFunc(static (WakePassData data, ComputeGraphContext context) =>
                    {
                        long startTicks = Stopwatch.GetTimestamp();
                        var cmd = context.cmd;
                        Vector4 wakeParams = default;
                        wakeParams.x = data.ResolutionScale;
                        Vector4 wakeScrollOffset = default;
                        wakeScrollOffset.x = data.ScrollOffset.x;
                        wakeScrollOffset.y = data.ScrollOffset.y;
                        wakeScrollOffset.z = data.ScrollOffset.z;
                        wakeScrollOffset.w = data.ScrollOffset.w;
                        cmd.BeginSample("H8 Ocean Wake Dear Lie");
                        cmd.SetComputeTextureParam(data.Compute, data.ClearKernel, H8OceanSinglePassShaderIds.WakeTextureWriteId, data.WakeTexture);
                        cmd.SetComputeIntParam(data.Compute, H8OceanSinglePassShaderIds.WakeResolutionId, data.Resolution);
                        cmd.SetComputeVectorParam(data.Compute, H8OceanSinglePassShaderIds.WakeParamsId, wakeParams);
                        cmd.SetComputeVectorParam(data.Compute, H8OceanSinglePassShaderIds.WakeScrollOffsetId, wakeScrollOffset);
                        cmd.SetComputeConstantBufferParam(data.Compute, H8OceanSinglePassShaderIds.ConstantBufferId, data.ConstantBuffer, 0, OceanSinglePassConstants.CBufferBytes);
                        cmd.DispatchCompute(data.Compute, data.ClearKernel, data.ClearDispatchX, data.ClearDispatchY, data.DispatchZ);

                        if (data.EventCount > 0 && data.EventBuffer != null)
                        {
                            cmd.SetComputeTextureParam(data.Compute, data.AccumulateKernel, H8OceanSinglePassShaderIds.WakeTextureWriteId, data.WakeTexture);
                            cmd.SetComputeBufferParam(data.Compute, data.AccumulateKernel, H8OceanSinglePassShaderIds.WakeEventsId, data.EventBuffer);
                            cmd.SetComputeIntParam(data.Compute, H8OceanSinglePassShaderIds.WakeEventCountId, data.EventCount);
                            cmd.SetComputeIntParam(data.Compute, H8OceanSinglePassShaderIds.WakeResolutionId, data.Resolution);
                            wakeParams.y = data.EventCount;
                            wakeParams.z = OceanSinglePassConstants.WakeTextureWorldSizeMeters;
                            cmd.SetComputeVectorParam(data.Compute, H8OceanSinglePassShaderIds.WakeParamsId, wakeParams);
                            cmd.SetComputeVectorParam(data.Compute, H8OceanSinglePassShaderIds.WakeScrollOffsetId, wakeScrollOffset);
                            cmd.SetComputeConstantBufferParam(data.Compute, H8OceanSinglePassShaderIds.ConstantBufferId, data.ConstantBuffer, 0, OceanSinglePassConstants.CBufferBytes);
                            cmd.DispatchCompute(data.Compute, data.AccumulateKernel, data.AccumulateDispatchX, data.AccumulateDispatchY, data.DispatchZ);
                        }

                        cmd.EndSample("H8 Ocean Wake Dear Lie");
                        long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                        float elapsedMicroseconds = TicksToMicroseconds(elapsedTicks);
                        OceanSinglePassRuntime.ReportRenderGraphTelemetry(
                            -1f,
                            elapsedMicroseconds,
                            elapsedMicroseconds,
                            2u);
                    });
                }
            }

            private TextureHandle CreateWakeTexture(RenderGraph renderGraph, int resolution, bool useTextureArray, int sliceCount)
            {
                int safeResolution = Math.Max(1, resolution);
                TextureDesc wakeDesc = new TextureDesc(safeResolution, safeResolution, dynamicResolution: false, xrReady: useTextureArray);
                wakeDesc.name = "_H8OceanWakeDisplacement";
                wakeDesc.clearBuffer = true;
                wakeDesc.clearColor = Color.clear;
                wakeDesc.depthBufferBits = DepthBits.None;
                wakeDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                wakeDesc.msaaSamples = MSAASamples.None;
                wakeDesc.dimension = useTextureArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
                wakeDesc.slices = useTextureArray ? sliceCount : 1;
                wakeDesc.enableRandomWrite = true;
                wakeDesc.filterMode = FilterMode.Bilinear;
                wakeDesc.wrapMode = TextureWrapMode.Repeat;
                wakeDesc.useMipMap = false;
                wakeDesc.autoGenerateMips = false;
                wakeDesc.useDynamicScale = false;
                wakeDesc.useDynamicScaleExplicit = false;
                return renderGraph.CreateTexture(wakeDesc);
            }

            private void PublishClearedWakeTexture(RenderGraph renderGraph)
            {
                TextureDesc wakeDesc = new TextureDesc(1, 1, dynamicResolution: false, xrReady: false);
                wakeDesc.name = "_H8OceanWakeDisplacement_Clear";
                wakeDesc.clearBuffer = true;
                wakeDesc.clearColor = Color.clear;
                wakeDesc.depthBufferBits = DepthBits.None;
                wakeDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                wakeDesc.msaaSamples = MSAASamples.None;
                wakeDesc.enableRandomWrite = false;
                wakeDesc.filterMode = FilterMode.Bilinear;
                wakeDesc.wrapMode = TextureWrapMode.Repeat;
                TextureHandle wakeTexture = renderGraph.CreateTexture(wakeDesc);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ClearPassData>(
                           "Hecton Ocean Wake Clear",
                           out ClearPassData _,
                           _wakeSampler))
                {
                    builder.SetRenderAttachment(wakeTexture, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(wakeTexture, H8OceanSinglePassShaderIds.WakeTextureId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (ClearPassData data, RasterGraphContext context) =>
                    {
                        // The transient texture descriptor owns the clear. Avoid command-buffer target
                        // clears here so the Crest validator only flags real camera/depth mutation.
                    });
                }
            }

            private void ResolveKernels(ComputeShader wakeCompute, bool supportsComputeShaders)
            {
                _resolvedWakeCompute = wakeCompute;
                _resolvedSupportsComputeShaders = supportsComputeShaders;

                if (!supportsComputeShaders || wakeCompute == null)
                {
                    _clearWakeKernel = -1;
                    _accumulateWakeKernel = -1;
                    _clearThreadGroupSizeX = 0u;
                    _clearThreadGroupSizeY = 0u;
                    _accumulateThreadGroupSizeX = 0u;
                    _accumulateThreadGroupSizeY = 0u;
                    return;
                }

                _clearWakeKernel = ResolveKernel(wakeCompute, "ClearWake");
                _accumulateWakeKernel = ResolveKernel(wakeCompute, "AccumulateWake");
                if (!TryResolveThreadGroupSizes(wakeCompute, _clearWakeKernel, out _clearThreadGroupSizeX, out _clearThreadGroupSizeY) ||
                    !TryResolveThreadGroupSizes(wakeCompute, _accumulateWakeKernel, out _accumulateThreadGroupSizeX, out _accumulateThreadGroupSizeY))
                {
                    _clearWakeKernel = -1;
                    _accumulateWakeKernel = -1;
                    _clearThreadGroupSizeX = 0u;
                    _clearThreadGroupSizeY = 0u;
                    _accumulateThreadGroupSizeX = 0u;
                    _accumulateThreadGroupSizeY = 0u;
                }
            }

            private static int ResolveKernel(ComputeShader compute, string name)
            {
                if (compute == null)
                    return -1;

                try
                {
                    if (!compute.HasKernel(name))
                        return -1;

                    int kernel = compute.FindKernel(name);
                    if (kernel < 0)
                        return -1;

                    return compute.IsSupported(kernel) ? kernel : -1;
                }
                catch (System.ObjectDisposedException)
                {
                    return -1;
                }
                catch (System.InvalidOperationException)
                {
                    return -1;
                }
                catch (System.ArgumentException)
                {
                    return -1;
                }
                catch (MissingReferenceException)
                {
                    return -1;
                }
                catch (UnityException)
                {
                    return -1;
                }
            }

            private static bool TryResolveThreadGroupSizes(ComputeShader compute, int kernel, out uint x, out uint y)
            {
                x = 0u;
                y = 0u;
                if (compute == null || kernel < 0)
                    return false;

                uint groupX;
                uint groupY;
                uint groupZ;
                try
                {
                    if (!compute.IsSupported(kernel))
                        return false;

                    compute.GetKernelThreadGroupSizes(kernel, out groupX, out groupY, out groupZ);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (MissingReferenceException)
                {
                    return false;
                }
                catch (UnityException)
                {
                    return false;
                }

                ulong threadProduct = (ulong)groupX * groupY * groupZ;
                if (groupX == 0u || groupY == 0u || groupZ != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                x = groupX;
                y = groupY;
                return true;
            }

            private static int CeilByThreadGroup(int value, uint groupSize)
            {
                if (value <= 0 || groupSize == 0u)
                    return 0;

                long groups = ((long)value + groupSize - 1L) / groupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private static int ResolveDispatchDepth(int value)
            {
                return value > 0 && value <= MaxDispatchGroupsPerDimension ? value : 0;
            }

            private static float TicksToMicroseconds(long ticks)
            {
                return (float)(ticks * (1000000.0 / Stopwatch.Frequency));
            }

            private sealed class DepthPassData
            {
                internal TextureHandle Depth;
                internal Material Material;
                internal GraphicsBuffer ConstantBuffer;
                internal GraphicsBuffer ShorelineFoamBuffer;
                internal int ShorelineFoamCount;
                internal Vector4 ShorelineFoamRuntime;
            }

            private sealed class WakePassData
            {
                internal ComputeShader Compute;
                internal int ClearKernel;
                internal int AccumulateKernel;
                internal TextureHandle WakeTexture;
                internal GraphicsBuffer ConstantBuffer;
                internal GraphicsBuffer EventBuffer;
                internal int EventCount;
                internal int Resolution;
                internal float ResolutionScale;
                internal float4 ScrollOffset;
                internal int ClearDispatchX;
                internal int ClearDispatchY;
                internal int AccumulateDispatchX;
                internal int AccumulateDispatchY;
                internal int DispatchZ;
            }

            private sealed class ClearPassData
            {
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();
        private SinglePassOceanPass _pass;
        private Material _depthMaterial;
        private bool _supportsComputeShadersCold;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.depthFoamShader == null)
                settings.depthFoamShader = AssetDatabase.LoadAssetAtPath<Shader>(DepthShaderAssetPath);
            if (settings != null && settings.wakeCompute == null)
                settings.wakeCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeComputeAssetPath);
#endif

            Shader shader = settings != null ? settings.depthFoamShader : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shader == null)
                shader = Shader.Find("Hidden/Hecton8/OceanDepthFoam");
#endif
            RecreateMaterial(ref _depthMaterial, shader);
            CacheGraphicsCapabilitiesCold();
            _pass ??= new SinglePassOceanPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!OceanSinglePassRuntime.HasRendererFeatureRuntimeGate() ||
                settings == null ||
                _pass == null ||
                IsUnsupportedCameraType(renderingData.cameraData.cameraType))
            {
                return;
            }

            _pass.Setup(settings, _depthMaterial, _supportsComputeShadersCold);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeMaterial(ref _depthMaterial);
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                DisposeMaterial(ref material);
                return;
            }

            if (material != null && material.shader == shader)
                return;

            DisposeMaterial(ref material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static void DisposeMaterial(ref Material material)
        {
            if (material == null)
                return;

            CoreUtils.Destroy(material);
            material = null;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
        }
    }
}
