using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen visor trauma composite that projects bounded cracks, blood, burns, and torn glass without spawning decal GameObjects.
    /// </summary>
    public sealed class DeferredDecalPass : ScriptableRendererFeature, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Authored fullscreen visor trauma material. Must reconstruct world position from depth and project the global visor trauma buffer.")]
            [FormerlySerializedAs("deferredDecalShader")]
            public Material material = null;

            [Tooltip("Texture2DArray atlas sampled by the visor trauma pass. Null uses procedural crack/blood fallback.")]
            public Texture2DArray decalAtlas = null;

            [Tooltip("Maximum number of active visor traumas uploaded to the fullscreen buffer.")]
            [Range(DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity)] public int maxDecals = DynamicDecalVaultRuntime.MaxCapacity;

            [Tooltip("Base fade time consumed by the Vault-backed decay job.")]
            [Range(0.25f, 60f)] public float baseFadeTimeSeconds = 7.5f;

            [Tooltip("Texture array slice count. DecalTypeHash stores type bits 0..3 and atlas slice bits 4..7.")]
            [Range(1, 16)] public int atlasSlices = DynamicDecalVaultRuntime.AtlasSliceCount;

            [Tooltip("Global additive tint for projected visor traumas.")]
            public Color decalTint = new Color(0.72f, 0.44f, 0.32f, 1f);

            [Tooltip("Global additive intensity applied to the sampled decal atlas.")]
            [Range(0f, 4f)] public float intensity = 1f;
        }

        private sealed class DeferredDecalCompositePass : ScriptableRenderPass, IDisposable
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Trauma");

            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _decalBufferA;
            private GraphicsBuffer _decalBufferB;
            private int _bufferCapacity;
            private int _writeBufferIndex;
            private int _readBufferIndex;
            private int _readCount;
            private bool _hasReadableBuffer;
            private int _stagedBufferIndex;
            private int _stagedCount;
            private bool _hasStagedBuffer;
            private DynamicDecalFrameStats _lastFrameStats;
            private bool _hasLastFrameStats;
            private Texture _boundDecalAtlas;
            private RTHandle _decalAtlasHandle;

            private sealed class PassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public TextureHandle DecalAtlas;
                public BufferHandle DecalBuffer;
                public Material Material;
                public Vector4 DecalAtlasParams;
                public Vector4 DecalRefractionParams;
                public Vector4 DecalTint;
                public Vector4 CameraPosition;
                public int DecalCount;
            }

            public DeferredDecalCompositePass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public bool PrepareFrame(Camera camera, float deltaTime)
            {
                if (_settings == null || _material == null)
                    return false;

                PromoteStagedUpload();

                int requiredBufferCapacity = Mathf.Clamp(_settings.maxDecals, DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity);
                if ((_hasReadableBuffer || _hasStagedBuffer) && _bufferCapacity != requiredBufferCapacity)
                    EnsureDecalBuffers(requiredBufferCapacity);

                bool hasUpload = DynamicDecalVaultRuntime.ExecuteVisualSync(
                    camera,
                    deltaTime,
                    _settings.maxDecals,
                    _settings.baseFadeTimeSeconds,
                    out DynamicDecalFrameStats stats);
                _lastFrameStats = stats;
                _hasLastFrameStats = true;

                if (hasUpload)
                    UploadDecalBuffer(in stats);
                else if (stats.ActiveCount <= 0)
                {
                    _hasReadableBuffer = false;
                    _readCount = 0;
                    _hasStagedBuffer = false;
                    _stagedCount = 0;
                }

                return _hasReadableBuffer;
            }

            public void PublishStagedUpload()
            {
                PromoteStagedUpload();
            }

            public bool HasReadableFrame
            {
                get
                {
                    return _hasReadableBuffer && _readCount > 0 && ResolvePublishedBuffer() != null;
                }
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                ReleaseDecalAtlasHandle();
                ReleaseBuffers();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base)
                    return;

                GraphicsBuffer readableBuffer = ResolvePublishedBuffer();
                int readableCount = _readCount;
                if (readableBuffer == null || readableCount <= 0)
                    return;
                DynamicDecalFrameStats stats = _hasLastFrameStats ? _lastFrameStats : default;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonVisorTraumaComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);
                Vector3 cameraPosition = cameraData.camera != null ? cameraData.camera.transform.position : Vector3.zero;
                BufferHandle decalBufferHandle = renderGraph.ImportBuffer(readableBuffer);
                RTHandle decalAtlasHandle = GetDecalAtlasHandle(_settings.decalAtlas);
                bool hasDecalAtlas = decalAtlasHandle != null;
                TextureHandle decalAtlasTexture = TextureHandle.nullHandle;
                if (hasDecalAtlas)
                {
                    decalAtlasTexture = renderGraph.ImportTexture(decalAtlasHandle);
                    hasDecalAtlas = decalAtlasTexture.IsValid();
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor Trauma Composite",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.DecalAtlas = decalAtlasTexture;
                    passData.DecalBuffer = decalBufferHandle;
                    passData.Material = _material;
                    passData.DecalCount = readableCount;
                    passData.DecalAtlasParams = MakeVector4(
                        Mathf.Max(1, _settings.atlasSlices),
                        Mathf.Clamp01(stats.GlobalQualityWeight),
                        Mathf.Max(0f, _settings.intensity),
                        hasDecalAtlas ? 1f : 0f);
                    passData.DecalRefractionParams = MakeVector4(
                        Mathf.Max(0f, stats.NormalRefractionIntensity),
                        readableCount,
                        Mathf.Clamp01(stats.ThermalPressure01),
                        0f);
                    passData.DecalTint = MakeVector4(_settings.decalTint.r, _settings.decalTint.g, _settings.decalTint.b, _settings.decalTint.a);
                    passData.CameraPosition = MakeVector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f);

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (hasDecalAtlas)
                        builder.UseTexture(decalAtlasTexture, AccessFlags.Read);
                    builder.UseBuffer(decalBufferHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer decalBuffer = data.DecalBuffer;
                        if (decalBuffer == null || data.Material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        if (data.DecalAtlasParams.w > 0.5f)
                            context.cmd.SetGlobalTexture(ShaderConstants.DecalAtlasId, data.DecalAtlas);
                        context.cmd.SetGlobalBuffer(ShaderConstants.DecalBufferId, decalBuffer);
                        context.cmd.SetGlobalInt(ShaderConstants.DecalCountId, data.DecalCount);
                        context.cmd.SetGlobalVector(ShaderConstants.DecalAtlasParamsId, data.DecalAtlasParams);
                        context.cmd.SetGlobalVector(ShaderConstants.DecalRefractionParamsId, data.DecalRefractionParams);
                        context.cmd.SetGlobalVector(ShaderConstants.DecalTintId, data.DecalTint);
                        context.cmd.SetGlobalVector(ShaderConstants.DecalCameraPositionId, data.CameraPosition);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void UploadDecalBuffer(in DynamicDecalFrameStats stats)
            {
                int requestedUploadCount = Mathf.Clamp(stats.UploadCount, 0, DynamicDecalVaultRuntime.MaxCapacity);
                if (requestedUploadCount <= 0 ||
                    !DynamicDecalVaultRuntime.TryResolveUploadBuffer(in stats, out NativeArray<TraumaDecalDTO>.ReadOnly uploadBuffer))
                {
                    return;
                }

                EnsureDecalBuffers(Mathf.Clamp(_settings.maxDecals, DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity));
                GraphicsBuffer target = ResolveBuffer(_writeBufferIndex);
                if (target == null)
                    return;

                int uploadCount = Mathf.Min(requestedUploadCount, Mathf.Min(target.count, uploadBuffer.Length));
                if (uploadCount <= 0)
                    return;

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                NativeArray<TraumaDecalDTO> mapped = target.LockBufferForWrite<TraumaDecalDTO>(0, uploadCount);
                try
                {
                    unsafe
                    {
                        DynamicDecalVaultRuntime.CopyDecalsToMappedUploadBuffer(
                            uploadBuffer,
                            (TraumaDecalDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                            uploadCount);
                    }
                }
                finally
                {
                    target.UnlockBufferAfterWrite<TraumaDecalDTO>(uploadCount);
                }

                float uploadUs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) *
                                         1000000.0d /
                                         System.Diagnostics.Stopwatch.Frequency);
                DynamicDecalVaultRuntime.RecordGpuUploadMicroseconds(uploadUs);
                _stagedBufferIndex = _writeBufferIndex;
                _stagedCount = uploadCount;
                _hasStagedBuffer = true;
                _writeBufferIndex ^= 1;
            }

            private void EnsureDecalBuffers(int requiredCapacity)
            {
                if (_decalBufferA != null &&
                    _decalBufferB != null &&
                    _bufferCapacity == requiredCapacity &&
                    _decalBufferA.count == requiredCapacity &&
                    _decalBufferB.count == requiredCapacity)
                {
                    return;
                }

                ReleaseBuffers();
                _bufferCapacity = requiredCapacity;
                _writeBufferIndex = 0;
                _readBufferIndex = 0;
                _readCount = 0;
                _hasReadableBuffer = false;
                _stagedBufferIndex = 0;
                _stagedCount = 0;
                _hasStagedBuffer = false;
                _decalBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<TraumaDecalDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[visor trauma capacity A] - screen-space wound double-buffer upload - owner: DeferredDecalPass
                _decalBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<TraumaDecalDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[visor trauma capacity B] - screen-space wound double-buffer upload - owner: DeferredDecalPass
            }

            private void PromoteStagedUpload()
            {
                if (!_hasStagedBuffer)
                    return;

                _readBufferIndex = _stagedBufferIndex;
                _readCount = _stagedCount;
                _hasReadableBuffer = _readCount > 0;
                _hasStagedBuffer = false;
                _stagedCount = 0;
            }

            private GraphicsBuffer ResolvePublishedBuffer()
            {
                return _hasReadableBuffer && _readCount > 0 ? ResolveBuffer(_readBufferIndex) : null;
            }

            private GraphicsBuffer ResolveBuffer(int index)
            {
                return (index & 1) == 0 ? _decalBufferA : _decalBufferB;
            }

            private static Vector4 MakeVector4(float x, float y, float z, float w)
            {
                Vector4 result = default;
                result.x = x;
                result.y = y;
                result.z = z;
                result.w = w;
                return result;
            }

            public void PrepareDecalAtlasHandleCold(FeatureSettings settings)
            {
                Texture atlas = settings != null ? settings.decalAtlas : null;
                EnsureDecalAtlasHandle(atlas);
            }

            private RTHandle GetDecalAtlasHandle(Texture atlas)
            {
                return atlas != null && ReferenceEquals(_boundDecalAtlas, atlas) ? _decalAtlasHandle : null;
            }

            private void EnsureDecalAtlasHandle(Texture atlas)
            {
                if (atlas == null)
                {
                    ReleaseDecalAtlasHandle();
                    return;
                }

                if (ReferenceEquals(_boundDecalAtlas, atlas) && _decalAtlasHandle != null)
                    return;

                ReleaseDecalAtlasHandle();
                _boundDecalAtlas = atlas;
                _decalAtlasHandle = RTHandles.Alloc(atlas);
            }

            private void ReleaseDecalAtlasHandle()
            {
                if (_decalAtlasHandle != null)
                {
                    RTHandles.Release(_decalAtlasHandle);
                    _decalAtlasHandle = null;
                }

                _boundDecalAtlas = null;
            }

            private void ReleaseBuffers()
            {
                if (_decalBufferA != null)
                {
                    _decalBufferA.Release();
                    _decalBufferA = null;
                }

                if (_decalBufferB != null)
                {
                    _decalBufferB.Release();
                    _decalBufferB = null;
                }

                _bufferCapacity = 0;
                _readCount = 0;
                _hasReadableBuffer = false;
                _stagedCount = 0;
                _hasStagedBuffer = false;
                _hasLastFrameStats = false;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int DecalBufferId = Shader.PropertyToID("_GlobalVisorTrauma");
            internal static readonly int DecalCountId = Shader.PropertyToID("_GlobalVisorTraumaCount");
            internal static readonly int DecalAtlasId = Shader.PropertyToID("_GlobalVisorTraumaAtlas");
            internal static readonly int DecalAtlasParamsId = Shader.PropertyToID("_GlobalVisorTraumaParams");
            internal static readonly int DecalRefractionParamsId = Shader.PropertyToID("_GlobalVisorTraumaRefractionParams");
            internal static readonly int DecalTintId = Shader.PropertyToID("_GlobalVisorTraumaTint");
            internal static readonly int DecalCameraPositionId = Shader.PropertyToID("_GlobalVisorTraumaCameraWS");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DeferredDecalCompositePass _pass;
        private Camera _pendingVisualSyncCamera;
        private float _pendingVisualSyncDeltaTime;
        private bool _hasPendingVisualSyncCamera;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;

        public override void Create()
        {
            _pass ??= new DeferredDecalCompositePass();
            TryRegisterHotSwapListener();
            if (Application.isPlaying)
                DynamicDecalVaultRuntime.TryInitializeColdStorage();
            _pass.PrepareDecalAtlasHandleCold(settings);
            TryRegisterLateFrame();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.material == null || _pass == null)
                return;

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base)
                return;

            if (!DynamicDecalVaultRuntime.IsColdStorageReady())
                return;

            _pass.Setup(settings, settings.material);
            _pass.PublishStagedUpload();
            StageVisualSyncContext(cameraData.camera);
            if (!_pass.HasReadableFrame)
                return;

            renderer.EnqueuePass(_pass);
        }

        public void LateFrameTick()
        {
            if (!_hasPendingVisualSyncCamera || _pass == null || settings == null || settings.material == null)
                return;

            if (!DynamicDecalVaultRuntime.IsColdStorageReady())
                return;

            _pass.Setup(settings, settings.material);
            _pass.PrepareFrame(_pendingVisualSyncCamera, _pendingVisualSyncDeltaTime);
            _hasPendingVisualSyncCamera = false;
        }

        protected override void Dispose(bool disposing)
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            _pass?.Dispose();
        }

        private void StageVisualSyncContext(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game)
                return;

            _pendingVisualSyncCamera = camera;
            _pendingVisualSyncDeltaTime = Mathf.Max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            _hasPendingVisualSyncCamera = true;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DynamicDecalVaultRuntime.ResetColdStorageForRebind();
                DynamicDecalVaultRuntime.TryInitializeColdStorage();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                DynamicDecalVaultRuntime.RefreshColdPlayerContext();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterLateFrame();
                if (currentService != null && isActive)
                    TryRegisterLateFrame();
            }
        }
    }
}
