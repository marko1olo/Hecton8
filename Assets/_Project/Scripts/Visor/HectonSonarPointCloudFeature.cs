using System;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Persists active-sonar contact hits into a screen-space point-cloud history so abyss silhouettes survive after the pulse passes.
    /// </summary>
    public sealed class HectonSonarPointCloudFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable, ISlowTickable
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [UnityEngine.Serialization.FormerlySerializedAs("shader")]
            [Tooltip("Authored fullscreen sonar point-cloud material. Uses pass 0 for screen history, pass 1 for world history, and pass 2 for composite.")]
            public Material material = null;

            [Tooltip("Legacy fullscreen sonar history/composite path. Active sonar geo illumination is now shader-global geometry emission.")]
            public bool enableFullscreenSonarHistory = false;

            [Tooltip("Where the sonar point-cloud overlay is injected into URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Internal history resolution. Lower values reduce MX350 fill cost.")]
            [Range(0.25f, 1f)] public float renderScale = 0.65f;

            [Tooltip("How long burned sonar points persist on screen in seconds.")]
            [Range(0.5f, 15f)] public float persistenceSeconds = 12f;

            [Tooltip("Density of neon sonar points written into the persistent history.")]
            [Range(0.05f, 4f)] public float pointDensity = 1.15f;

            [Tooltip("Brightness multiplier for newly written sonar points.")]
            [Range(0f, 4f)] public float pointBoost = 1.35f;

            [Tooltip("Resolution of the world-space sonar memory treadmill map.")]
            [Range(256, 2048)] public int worldMemoryResolution = 1024;

            [Tooltip("World-space coverage of the treadmill sonar memory map in meters.")]
            [Range(64f, 1024f)] public float worldMemoryWorldSize = 320f;

            [Tooltip("How long the world-space sonar memory map persists in seconds.")]
            [Range(0.5f, 15f)] public float worldPersistenceSeconds = 12f;

            [Tooltip("World-space point radius used when stamping sonar contacts into the treadmill memory map.")]
            [Range(0.25f, 8f)] public float worldPointRadius = 1.8f;

            [Tooltip("Pixel stride used to quantize treadmill recentering and avoid shimmer.")]
            [Range(0.1f, 4f)] public float worldCenterSnapPixelStride = 1f;
        }

        private sealed class SonarPointCloudPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;

            private sealed class SonarFullscreenPassData
            {
                internal Material material;
                internal MaterialPropertyBlock properties;
                internal TextureHandle source;
                internal TextureHandle history;
                internal TextureHandle worldHistory;
                internal Vector4 worldMemoryRect;
                internal Vector4 worldScrollUvOffset;
                internal Vector4 worldOriginOffset;
                internal float persistenceSeconds;
                internal float pointDensity;
                internal float pointBoost;
                internal float hasHistory;
                internal float worldPersistenceSeconds;
                internal float worldPointRadius;
                internal float hasWorldHistory;
                internal int shaderPassIndex;
                internal bool bindHistory;
                internal bool bindWorldHistory;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Sonar Point Cloud");
            private FeatureSettings _settings;
            private Material _material;
            private MaterialPropertyBlock _drawProperties; // COLD ALLOC: per-feature sonar fullscreen draw payload - owner: HectonSonarPointCloudFeature
            private RTHandle _historyRead;
            private RTHandle _historyWrite;
            private RTHandle _worldHistoryRead;
            private RTHandle _worldHistoryWrite;
            private bool _historyValid;
            private bool _worldHistoryValid;
            private float _screenHistoryRetainUntilTime;
            private float _worldHistoryRetainUntilTime;
            private Vector4 _worldMemoryRect;
            private Vector2 _worldCenterXZ;
            private Vector2 _worldScrollUvOffset;
            private float _worldMemoryWorldSize;
            private float _sonarRevealExpireTime;
            private int _requestedHistoryWidth;
            private int _requestedHistoryHeight;
            private int _requestedWorldResolution;
            private bool _resourceRequestPending;
            private HectonFloatingOrigin _floatingOrigin;

            public SonarPointCloudPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material material,
                HectonFloatingOrigin floatingOrigin,
                float sonarRevealExpireTime)
            {
                _settings = settings;
                _material = material;
                _floatingOrigin = floatingOrigin;
                _sonarRevealExpireTime = sonarRevealExpireTime;
                EnsureDrawPropertiesCold();
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                _historyRead?.Release();
                _historyWrite?.Release();
                _worldHistoryRead?.Release();
                _worldHistoryWrite?.Release();
                _historyRead = null;
                _historyWrite = null;
                _worldHistoryRead = null;
                _worldHistoryWrite = null;
                _historyValid = false;
                _worldHistoryValid = false;
                _screenHistoryRetainUntilTime = 0f;
                _worldHistoryRetainUntilTime = 0f;
                _worldMemoryRect = Vector4.zero;
                _worldCenterXZ = Vector2.zero;
                _worldScrollUvOffset = Vector2.zero;
                _worldMemoryWorldSize = 0f;
                _sonarRevealExpireTime = 0f;
                _requestedHistoryWidth = 0;
                _requestedHistoryHeight = 0;
                _requestedWorldResolution = 0;
                _resourceRequestPending = false;
                _floatingOrigin = null;
                _material = null;
                if (_drawProperties != null)
                    _drawProperties.Clear();
            }

            private void EnsureDrawPropertiesCold()
            {
                if (_drawProperties != null)
                    return;

                _drawProperties = new MaterialPropertyBlock(); // COLD ALLOC: per-feature sonar fullscreen draw payload - owner: HectonSonarPointCloudFeature
            }

            public bool HasHistory =>
                (_historyValid && (float)SystemDispatcher.CurrentUnscaledTimeSeconds <= _screenHistoryRetainUntilTime) ||
                (_worldHistoryValid && (float)SystemDispatcher.CurrentUnscaledTimeSeconds <= _worldHistoryRetainUntilTime);

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || !_settings.enableFullscreenSonarHistory || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                float renderScale = math.clamp(_settings.renderScale, 0.25f, 1f);
                int historyWidth = QuantizeDimension(math.max(1, (int)math.round(sourceDesc.width * renderScale)));
                int historyHeight = QuantizeDimension(math.max(1, (int)math.round(sourceDesc.height * renderScale)));
                int worldResolution = math.clamp(_settings.worldMemoryResolution, 256, 2048);
                if (!HasResourcesFor(historyWidth, historyHeight, worldResolution))
                    return;

                TextureHandle historyReadTexture = renderGraph.ImportTexture(_historyRead);
                TextureHandle historyWriteTexture = renderGraph.ImportTexture(_historyWrite);
                TextureHandle worldHistoryReadTexture = renderGraph.ImportTexture(_worldHistoryRead);
                TextureHandle worldHistoryWriteTexture = renderGraph.ImportTexture(_worldHistoryWrite);

                HectonFloatingOrigin floatingOrigin = _floatingOrigin;
                Vector3 floatingOriginOffset = floatingOrigin != null ? floatingOrigin.TotalOffset : Vector3.zero;
                Vector3 absoluteCameraPosition = cameraData.camera.transform.position + floatingOriginOffset;
                RefreshWorldMemoryRect(new Vector2(absoluteCameraPosition.x, absoluteCameraPosition.z), false);

                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonSonarPointCloudComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
                bool hasActiveSonarReveal = _sonarRevealExpireTime > currentTime;
                bool screenHistoryAlive = _historyValid && currentTime <= _screenHistoryRetainUntilTime;
                bool worldHistoryAlive = _worldHistoryValid && currentTime <= _worldHistoryRetainUntilTime;
                if (hasActiveSonarReveal)
                {
                    _screenHistoryRetainUntilTime = math.max(_screenHistoryRetainUntilTime, currentTime + math.max(0.05f, _settings.persistenceSeconds));
                    _worldHistoryRetainUntilTime = math.max(_worldHistoryRetainUntilTime, currentTime + math.max(0.05f, _settings.worldPersistenceSeconds));
                }

                if (_drawProperties == null)
                    return;

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Sonar History Write",
                    sourceTexture,
                    historyReadTexture,
                    default,
                    historyWriteTexture,
                    _material,
                    _drawProperties,
                    _settings,
                    screenHistoryAlive,
                    worldHistoryAlive,
                    _worldMemoryRect,
                    _worldScrollUvOffset,
                    floatingOriginOffset,
                    0,
                    true,
                    false);
                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Sonar World History Write",
                    sourceTexture,
                    default,
                    worldHistoryReadTexture,
                    worldHistoryWriteTexture,
                    _material,
                    _drawProperties,
                    _settings,
                    screenHistoryAlive,
                    worldHistoryAlive,
                    _worldMemoryRect,
                    _worldScrollUvOffset,
                    floatingOriginOffset,
                    1,
                    false,
                    true);
                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Sonar Point Cloud Composite",
                    sourceTexture,
                    historyWriteTexture,
                    worldHistoryWriteTexture,
                    compositeTexture,
                    _material,
                    _drawProperties,
                    _settings,
                    screenHistoryAlive,
                    worldHistoryAlive,
                    _worldMemoryRect,
                    _worldScrollUvOffset,
                    floatingOriginOffset,
                    2,
                    true,
                    true);

                resourceData.cameraColor = compositeTexture;
                SwapHistoryTargets();
                SwapWorldMemoryTargets();
            }

            public void QueueResourceRequest(int historyWidth, int historyHeight, int worldResolution)
            {
                int safeHistoryWidth = QuantizeDimension(math.max(1, historyWidth));
                int safeHistoryHeight = QuantizeDimension(math.max(1, historyHeight));
                int safeWorldResolution = math.clamp(worldResolution, 256, 2048);
                if (HasHistoryTextures(safeHistoryWidth, safeHistoryHeight) &&
                    HasWorldMemoryTextures(safeWorldResolution))
                {
                    _resourceRequestPending = false;
                    return;
                }

                if (_requestedHistoryWidth == safeHistoryWidth &&
                    _requestedHistoryHeight == safeHistoryHeight &&
                    _requestedWorldResolution == safeWorldResolution &&
                    _resourceRequestPending)
                {
                    return;
                }

                _requestedHistoryWidth = safeHistoryWidth;
                _requestedHistoryHeight = safeHistoryHeight;
                _requestedWorldResolution = safeWorldResolution;
                _resourceRequestPending = true;
            }

            public void PrepareQueuedResources()
            {
                if (!_resourceRequestPending ||
                    _requestedHistoryWidth <= 0 ||
                    _requestedHistoryHeight <= 0 ||
                    _requestedWorldResolution <= 0)
                {
                    return;
                }

                EnsureHistoryTextures(_requestedHistoryWidth, _requestedHistoryHeight);
                EnsureWorldMemoryTextures(_requestedWorldResolution);
                _resourceRequestPending = false;
            }

            public bool HasResourcesFor(int historyWidth, int historyHeight, int worldResolution)
            {
                return HasHistoryTextures(QuantizeDimension(math.max(1, historyWidth)), QuantizeDimension(math.max(1, historyHeight))) &&
                       HasWorldMemoryTextures(math.clamp(worldResolution, 256, 2048));
            }

            private void RecordFullscreenPass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle source,
                TextureHandle history,
                TextureHandle worldHistory,
                TextureHandle destination,
                Material material,
                MaterialPropertyBlock properties,
                FeatureSettings settings,
                bool historyValid,
                bool worldHistoryValid,
                Vector4 worldMemoryRect,
                Vector2 worldScrollUvOffset,
                Vector3 floatingOriginOffset,
                int shaderPassIndex,
                bool bindHistory,
                bool bindWorldHistory)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<SonarFullscreenPassData>(
                           passName,
                           out SonarFullscreenPassData passData,
                           _profilingSampler))
                {
                    passData.material = material;
                    passData.properties = properties;
                    passData.source = source;
                    passData.history = history;
                    passData.worldHistory = worldHistory;
                    passData.worldMemoryRect = worldMemoryRect;
                    passData.worldScrollUvOffset = new Vector4(worldScrollUvOffset.x, worldScrollUvOffset.y, 0f, 0f);
                    passData.worldOriginOffset = new Vector4(floatingOriginOffset.x, floatingOriginOffset.y, floatingOriginOffset.z, 0f);
                    passData.persistenceSeconds = math.max(0.05f, settings.persistenceSeconds);
                    passData.pointDensity = math.max(0.05f, settings.pointDensity);
                    passData.pointBoost = math.max(0f, settings.pointBoost);
                    passData.hasHistory = historyValid ? 1f : 0f;
                    passData.worldPersistenceSeconds = math.max(0.05f, settings.worldPersistenceSeconds);
                    passData.worldPointRadius = math.max(0.05f, settings.worldPointRadius);
                    passData.hasWorldHistory = worldHistoryValid ? 1f : 0f;
                    passData.shaderPassIndex = shaderPassIndex;
                    passData.bindHistory = bindHistory;
                    passData.bindWorldHistory = bindWorldHistory;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (bindHistory)
                        builder.UseTexture(history, AccessFlags.Read);
                    if (bindWorldHistory)
                        builder.UseTexture(worldHistory, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (SonarFullscreenPassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        if (data.bindHistory)
                        {
                            context.cmd.SetGlobalTexture(ShaderConstants.HistoryTextureId, data.history);
                            context.cmd.SetGlobalTexture(ShaderConstants.PointCloudTextureId, data.history);
                        }

                        if (data.bindWorldHistory)
                        {
                            context.cmd.SetGlobalTexture(ShaderConstants.WorldHistoryTextureId, data.worldHistory);
                            context.cmd.SetGlobalTexture(ShaderConstants.WorldPointCloudTextureId, data.worldHistory);
                        }

                        UpdateMaterialParameters(data.properties, data);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, data.properties, data.shaderPassIndex);
                    });
                }
            }

            private void EnsureHistoryTextures(int width, int height)
            {
                if (HasHistoryTextures(width, height))
                {
                    return;
                }

                _historyRead?.Release();
                _historyWrite?.Release();
                _historyRead = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonSonarPointCloudHistoryRead");
                _historyWrite = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonSonarPointCloudHistoryWrite");
                _historyValid = false;
                _screenHistoryRetainUntilTime = 0f;
            }

            private bool HasHistoryTextures(int width, int height)
            {
                return _historyRead != null &&
                       _historyWrite != null &&
                       _historyRead.rt != null &&
                       _historyWrite.rt != null &&
                       _historyRead.rt.width == width &&
                       _historyRead.rt.height == height &&
                       _historyWrite.rt.width == width &&
                       _historyWrite.rt.height == height;
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = math.max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }

            private void EnsureWorldMemoryTextures(int resolution)
            {
                if (HasWorldMemoryTextures(resolution))
                {
                    return;
                }

                _worldHistoryRead?.Release();
                _worldHistoryWrite?.Release();
                _worldHistoryRead = RTHandles.Alloc(
                    resolution,
                    resolution,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonSonarWorldMemoryRead");
                _worldHistoryWrite = RTHandles.Alloc(
                    resolution,
                    resolution,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonSonarWorldMemoryWrite");
                _worldHistoryValid = false;
                _worldHistoryRetainUntilTime = 0f;
                _worldScrollUvOffset = Vector2.zero;
                _worldMemoryWorldSize = 0f;
            }

            private bool HasWorldMemoryTextures(int resolution)
            {
                return _worldHistoryRead != null &&
                       _worldHistoryWrite != null &&
                       _worldHistoryRead.rt != null &&
                       _worldHistoryWrite.rt != null &&
                       _worldHistoryRead.rt.width == resolution &&
                       _worldHistoryRead.rt.height == resolution &&
                       _worldHistoryWrite.rt.width == resolution &&
                       _worldHistoryWrite.rt.height == resolution;
            }

            private void RefreshWorldMemoryRect(Vector2 absoluteCenterXZ, bool forceClear)
            {
                if (_worldHistoryRead == null || _worldHistoryWrite == null)
                    return;

                _worldScrollUvOffset = Vector2.zero;
                float desiredWorldSize = math.max(64f, _settings.worldMemoryWorldSize);
                float snapStride = ResolveWorldMemorySnapStride(desiredWorldSize);
                Vector2 desiredCenterXZ = QuantizeWorldMemoryCenter(absoluteCenterXZ, snapStride);

                bool mustClear = forceClear || _worldMemoryWorldSize <= 0f || math.abs(desiredWorldSize - _worldMemoryWorldSize) > 0.001f;
                Vector2 centerDelta = desiredCenterXZ - _worldCenterXZ;
                float centerDeltaSq = centerDelta.x * centerDelta.x + centerDelta.y * centerDelta.y;
                if (!mustClear && centerDeltaSq <= 0.000001f)
                    return;

                _worldCenterXZ = desiredCenterXZ;
                _worldMemoryWorldSize = desiredWorldSize;
                float halfSize = desiredWorldSize * 0.5f;
                _worldMemoryRect = new Vector4(
                    desiredCenterXZ.x - halfSize,
                    desiredCenterXZ.y - halfSize,
                    1f / math.max(desiredWorldSize, 0.001f),
                    1f / math.max(desiredWorldSize, 0.001f));

                if (mustClear)
                {
                    _worldHistoryValid = false;
                    _worldScrollUvOffset = Vector2.zero;
                    return;
                }

                _worldScrollUvOffset = new Vector2(
                    centerDelta.x / math.max(desiredWorldSize, 0.001f),
                    centerDelta.y / math.max(desiredWorldSize, 0.001f));
            }

            private float ResolveWorldMemorySnapStride(float worldSize)
            {
                int resolution = _worldHistoryRead != null && _worldHistoryRead.rt != null ? _worldHistoryRead.rt.width : math.max(1, _settings.worldMemoryResolution);
                float pixelWorldSize = worldSize / math.max(resolution, 1);
                return pixelWorldSize * math.max(0.1f, _settings.worldCenterSnapPixelStride);
            }

            private static Vector2 QuantizeWorldMemoryCenter(Vector2 centerXZ, float stride)
            {
                if (stride <= 0.0001f)
                    return centerXZ;

                return new Vector2(
                    math.round(centerXZ.x / stride) * stride,
                    math.round(centerXZ.y / stride) * stride);
            }

            private void SwapHistoryTargets()
            {
                RTHandle temp = _historyRead;
                _historyRead = _historyWrite;
                _historyWrite = temp;
                _historyValid = true;
            }

            private void SwapWorldMemoryTargets()
            {
                RTHandle temp = _worldHistoryRead;
                _worldHistoryRead = _worldHistoryWrite;
                _worldHistoryWrite = temp;
                _worldHistoryValid = true;
            }

            private static void UpdateMaterialParameters(MaterialPropertyBlock properties, SonarFullscreenPassData data)
            {
                properties.Clear();
                properties.SetFloat(ShaderConstants.PersistenceSecondsId, data.persistenceSeconds);
                properties.SetFloat(ShaderConstants.PointDensityId, data.pointDensity);
                properties.SetFloat(ShaderConstants.PointBoostId, data.pointBoost);
                properties.SetFloat(ShaderConstants.HasHistoryId, data.hasHistory);
                properties.SetFloat(ShaderConstants.WorldPersistenceSecondsId, data.worldPersistenceSeconds);
                properties.SetFloat(ShaderConstants.WorldPointRadiusId, data.worldPointRadius);
                properties.SetFloat(ShaderConstants.HasWorldHistoryId, data.hasWorldHistory);
                properties.SetVector(ShaderConstants.WorldMemoryRectId, data.worldMemoryRect);
                properties.SetVector(ShaderConstants.WorldScrollUvOffsetId, data.worldScrollUvOffset);
                properties.SetVector(ShaderConstants.WorldOriginOffsetId, data.worldOriginOffset);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SonarRevealExpireTimeId = Shader.PropertyToID("_SonarRevealExpireTime");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int HistoryTextureId = Shader.PropertyToID("_HectonSonarHistoryTex");
            internal static readonly int PointCloudTextureId = Shader.PropertyToID("_HectonSonarPointCloudRT");
            internal static readonly int PersistenceSecondsId = Shader.PropertyToID("_PersistenceSeconds");
            internal static readonly int PointDensityId = Shader.PropertyToID("_PointDensity");
            internal static readonly int PointBoostId = Shader.PropertyToID("_PointBoost");
            internal static readonly int HasHistoryId = Shader.PropertyToID("_HasHistory");
            internal static readonly int WorldHistoryTextureId = Shader.PropertyToID("_HectonSonarWorldHistoryTex");
            internal static readonly int WorldPointCloudTextureId = Shader.PropertyToID("_HectonSonarWorldPointCloudRT");
            internal static readonly int WorldPersistenceSecondsId = Shader.PropertyToID("_WorldPersistenceSeconds");
            internal static readonly int WorldPointRadiusId = Shader.PropertyToID("_WorldPointRadius");
            internal static readonly int HasWorldHistoryId = Shader.PropertyToID("_HasWorldHistory");
            internal static readonly int WorldMemoryRectId = Shader.PropertyToID("_HectonSonarWorldMemoryRect");
            internal static readonly int WorldScrollUvOffsetId = Shader.PropertyToID("_HectonSonarWorldScrollUvOffset");
            internal static readonly int WorldOriginOffsetId = Shader.PropertyToID("_HectonSonarWorldOriginOffset");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private SonarPointCloudPass _pass;
        private HectonFloatingOrigin _cachedFloatingOrigin;
        private float _cachedSonarRevealExpireTime;
        private bool _hotSwapRegistered;
        private bool _lateFrameRegistered;
        private bool _slowTickRegistered;

        private void OnEnable()
        {
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            CachePresentationGlobalsLate();
        }

        /// <inheritdoc />
        public override void Create()
        {
            if (_pass == null)
                _pass = new SonarPointCloudPass();

            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            _cachedFloatingOrigin = GlobalRegistry.FloatingOrigin;
            CachePresentationGlobalsLate();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || settings.material == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            if (!TryResolveResourceRequest(renderingData, settings, out int historyWidth, out int historyHeight, out int worldResolution))
                return;

            _pass.QueueResourceRequest(historyWidth, historyHeight, worldResolution);
            if (!_pass.HasResourcesFor(historyWidth, historyHeight, worldResolution))
                return;

            if (!_pass.HasHistory && _cachedSonarRevealExpireTime <= 0f)
                return;

            _pass.Setup(settings, settings.material, _cachedFloatingOrigin, _cachedSonarRevealExpireTime);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _cachedFloatingOrigin = null;
            _cachedSonarRevealExpireTime = 0f;
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FloatingOriginRuntime)
            {
                _cachedFloatingOrigin = currentService as HectonFloatingOrigin;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTickable();
                TryUnregisterLateFrameTickable();
                if (currentService != null)
                {
                    TryRegisterSlowTickable();
                    TryRegisterLateFrameTickable();
                }
            }
        }

        public void SlowTick()
        {
            _pass?.PrepareQueuedResources();
        }

        public void LateFrameTick()
        {
            CachePresentationGlobalsLate();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        private void CachePresentationGlobalsLate()
        {
            _cachedSonarRevealExpireTime = Shader.GetGlobalFloat(ShaderConstants.SonarRevealExpireTimeId);
        }

        private static bool TryResolveResourceRequest(
            RenderingData renderingData,
            FeatureSettings settings,
            out int historyWidth,
            out int historyHeight,
            out int worldResolution)
        {
            historyWidth = 0;
            historyHeight = 0;
            worldResolution = 0;
            if (settings == null)
                return false;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            int sourceWidth = math.max(1, descriptor.width);
            int sourceHeight = math.max(1, descriptor.height);
            float renderScale = math.clamp(settings.renderScale, 0.25f, 1f);
            historyWidth = math.max(1, (int)math.round(sourceWidth * renderScale));
            historyHeight = math.max(1, (int)math.round(sourceHeight * renderScale));
            worldResolution = math.clamp(settings.worldMemoryResolution, 256, 2048);
            return true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_slowTickRegistered)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_lateFrameRegistered)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRegistered = false;
        }

    }
}
