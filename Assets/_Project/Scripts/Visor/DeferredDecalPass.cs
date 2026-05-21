using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen visor wound composite that projects bounded cracks, blood, burns, and torn glass without spawning decal GameObjects.
    /// </summary>
    public sealed class DeferredDecalPass : ScriptableRendererFeature, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string DeferredDecalShaderPath = "Assets/_Project/Art/Shaders/Hecton_VisorWounds.shader";

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Fullscreen visor wound shader. Must reconstruct world position from depth and project the global visor wound buffer.")]
            public Shader deferredDecalShader = null;

            [Tooltip("Texture2DArray atlas sampled by the visor wound pass. Null uses procedural crack/blood fallback.")]
            public Texture2DArray decalAtlas = null;

            [Tooltip("Maximum number of active visor wounds uploaded to the fullscreen buffer.")]
            [Range(DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity)] public int maxDecals = DynamicDecalVaultRuntime.MaxCapacity;

            [Tooltip("Base fade time consumed by the Vault-backed decay job.")]
            [Range(0.25f, 60f)] public float baseFadeTimeSeconds = 7.5f;

            [Tooltip("Texture array slice count. DecalTypeHash stores type bits 0..3 and atlas slice bits 4..7.")]
            [Range(1, 16)] public int atlasSlices = DynamicDecalVaultRuntime.AtlasSliceCount;

            [Tooltip("Global additive tint for projected visor wounds.")]
            public Color decalTint = new Color(0.72f, 0.44f, 0.32f, 1f);

            [Tooltip("Global additive intensity applied to the sampled decal atlas.")]
            [Range(0f, 4f)] public float intensity = 1f;
        }

        private sealed class DeferredDecalCompositePass : ScriptableRenderPass, IDisposable
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Wounds");

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
            private Material _boundAtlasMaterial;
            private Texture2DArray _boundDecalAtlas;

            private sealed class PassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
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

            public bool DrainPendingVisualSync()
            {
                PromoteStagedUpload();
                if (!DynamicDecalVaultRuntime.TryDrainPendingVisualSync(out DynamicDecalFrameStats stats))
                    return _hasReadableBuffer;

                _lastFrameStats = stats;
                _hasLastFrameStats = true;
                if (stats.UploadCount > 0)
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

            public void ForceCompletePendingVisualSync()
            {
                DynamicDecalVaultRuntime.ForceCompletePendingVisualSync(out _);
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
                if (_settings.decalAtlas != null &&
                    (_boundAtlasMaterial != _material || _boundDecalAtlas != _settings.decalAtlas))
                {
                    _material.SetTexture(ShaderConstants.DecalAtlasId, _settings.decalAtlas);
                    _boundAtlasMaterial = _material;
                    _boundDecalAtlas = _settings.decalAtlas;
                }
            }

            public void Dispose()
            {
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
                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonVisorWoundComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);
                Vector3 cameraPosition = cameraData.camera != null ? cameraData.camera.transform.position : Vector3.zero;
                BufferHandle decalBufferHandle = renderGraph.ImportBuffer(readableBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor Wound Composite",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.DecalBuffer = decalBufferHandle;
                    passData.Material = _material;
                    passData.DecalCount = readableCount;
                    passData.DecalAtlasParams = new Vector4(
                        Mathf.Max(1, _settings.atlasSlices),
                        Mathf.Clamp01(stats.GlobalQualityWeight),
                        Mathf.Max(0f, _settings.intensity),
                        _settings.decalAtlas != null ? 1f : 0f);
                    passData.DecalRefractionParams = new Vector4(
                        Mathf.Max(0f, stats.NormalRefractionIntensity),
                        readableCount,
                        Mathf.Clamp01(stats.ThermalPressure01),
                        0f);
                    passData.DecalTint = new Vector4(_settings.decalTint.r, _settings.decalTint.g, _settings.decalTint.b, _settings.decalTint.a);
                    passData.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f);

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
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
                if (requestedUploadCount <= 0 || !stats.UploadBuffer.IsCreated)
                    return;

                EnsureDecalBuffers(Mathf.Clamp(_settings.maxDecals, DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity));
                GraphicsBuffer target = ResolveBuffer(_writeBufferIndex);
                if (target == null)
                    return;

                int uploadCount = Mathf.Min(requestedUploadCount, Mathf.Min(target.count, stats.UploadBuffer.Length));
                if (uploadCount <= 0)
                    return;

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                NativeArray<VisorDecalDTO> mapped = target.LockBufferForWrite<VisorDecalDTO>(0, uploadCount);
                try
                {
                    unsafe
                    {
                        DynamicDecalVaultRuntime.CopyDecalsToMappedUploadBuffer(
                            stats.UploadBuffer,
                            (VisorDecalDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                            uploadCount);
                    }
                }
                finally
                {
                    target.UnlockBufferAfterWrite<VisorDecalDTO>(uploadCount);
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
                _decalBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<VisorDecalDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[visor wound capacity A] - screen-space wound double-buffer upload - owner: SHINOBU_275
                _decalBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<VisorDecalDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[visor wound capacity B] - screen-space wound double-buffer upload - owner: SHINOBU_275
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
            internal static readonly int DecalBufferId = Shader.PropertyToID("_GlobalVisorWounds");
            internal static readonly int DecalCountId = Shader.PropertyToID("_GlobalVisorWoundCount");
            internal static readonly int DecalAtlasId = Shader.PropertyToID("_GlobalVisorWoundAtlas");
            internal static readonly int DecalAtlasParamsId = Shader.PropertyToID("_GlobalVisorWoundParams");
            internal static readonly int DecalRefractionParamsId = Shader.PropertyToID("_GlobalVisorWoundRefractionParams");
            internal static readonly int DecalTintId = Shader.PropertyToID("_GlobalVisorWoundTint");
            internal static readonly int DecalCameraPositionId = Shader.PropertyToID("_GlobalVisorWoundCameraWS");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DeferredDecalCompositePass _pass;
        private Material _material;
        private Camera _pendingVisualSyncCamera;
        private float _pendingVisualSyncDeltaTime;
        private bool _hasPendingVisualSyncCamera;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.deferredDecalShader == null)
                settings.deferredDecalShader = AssetDatabase.LoadAssetAtPath<Shader>(DeferredDecalShaderPath);
#endif

            _pass ??= new DeferredDecalCompositePass();
            TryRegisterHotSwapListener();
            if (Application.isPlaying)
                DynamicDecalVaultRuntime.TryInitializeColdStorage();
            RecreateMaterial();
            TryRegisterLateFrame();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.deferredDecalShader == null || _material == null || _pass == null)
                return;

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base)
                return;

            if (!DynamicDecalVaultRuntime.IsColdStorageReady())
                return;

            _pass.Setup(settings, _material);
            _pass.PublishStagedUpload();
            StageVisualSyncContext(cameraData.camera);
            if (!_pass.HasReadableFrame)
                return;

            renderer.EnqueuePass(_pass);
        }

        public void LateFrameTick()
        {
            _pass?.DrainPendingVisualSync();

            if (!_hasPendingVisualSyncCamera || _pass == null || settings == null || _material == null)
                return;

            if (!DynamicDecalVaultRuntime.IsColdStorageReady())
                return;

            _pass.Setup(settings, _material);
            _pass.PrepareFrame(_pendingVisualSyncCamera, _pendingVisualSyncDeltaTime);
            _hasPendingVisualSyncCamera = false;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.ForceCompletePendingVisualSync();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            _pass?.Dispose();
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
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
                TryRegisterLateFrame();
        }

        private void RecreateMaterial()
        {
            if (settings == null || settings.deferredDecalShader == null)
            {
                if (_material != null)
                {
                    CoreUtils.Destroy(_material);
                    _material = null;
                }

                return;
            }

            if (_material == null || _material.shader != settings.deferredDecalShader)
            {
                if (_material != null)
                    CoreUtils.Destroy(_material);

                _material = CoreUtils.CreateEngineMaterial(settings.deferredDecalShader);
            }
        }
    }
}
