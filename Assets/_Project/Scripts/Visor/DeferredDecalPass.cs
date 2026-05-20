using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
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
    /// Fullscreen deferred decal composite that projects bounded rupture/rust decals from global matrix caches without spawning decal GameObjects.
    /// </summary>
    public sealed class DeferredDecalPass : ScriptableRendererFeature
    {
        private const string DeferredDecalShaderPath = "Assets/_Project/Art/Shaders/Hecton_DeferredDecal.shader";

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Fullscreen deferred decal shader. Must reconstruct world position from depth and project the global crack decal buffer.")]
            public Shader deferredDecalShader = null;

            [Tooltip("Texture2DArray atlas sampled by the deferred decal pass. Null uses procedural scorch fallback.")]
            public Texture2DArray decalAtlas = null;

            [Tooltip("Maximum number of active decals uploaded to the fullscreen decal buffer.")]
            [Range(DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity)] public int maxDecals = DynamicDecalVaultRuntime.MaxCapacity;

            [Tooltip("Base fade time consumed by the Vault-backed decay job.")]
            [Range(0.25f, 60f)] public float baseFadeTimeSeconds = 7.5f;

            [Tooltip("Texture array slice count. CPU writes MaterialHash as an already-resolved slice index.")]
            [Range(1, 16)] public int atlasSlices = DynamicDecalVaultRuntime.AtlasSliceCount;

            [Tooltip("Global additive tint for the projected rust/crack decals.")]
            public Color decalTint = new Color(0.72f, 0.44f, 0.32f, 1f);

            [Tooltip("Global additive intensity applied to the sampled decal atlas.")]
            [Range(0f, 4f)] public float intensity = 1f;
        }

        private sealed class DeferredDecalCompositePass : ScriptableRenderPass, IDisposable
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Deferred Decals");

            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _decalBufferA;
            private GraphicsBuffer _decalBufferB;
            private int _bufferCapacity;
            private int _writeBufferIndex;
            private int _readBufferIndex;
            private int _readCount;
            private bool _hasReadableBuffer;

            public DeferredDecalCompositePass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
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
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                int requiredBufferCapacity = Mathf.Clamp(_settings.maxDecals, DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity);
                if (_hasReadableBuffer && _bufferCapacity != requiredBufferCapacity)
                    EnsureDecalBuffers(requiredBufferCapacity);

                GraphicsBuffer readableBuffer = _hasReadableBuffer ? ResolveBuffer(_readBufferIndex) : null;
                int readableCount = _hasReadableBuffer ? _readCount : 0;
                bool hasUpload = DynamicDecalVaultRuntime.ExecuteVisualSync(
                    cameraData.camera,
                    Time.deltaTime,
                    _settings.maxDecals,
                    _settings.baseFadeTimeSeconds,
                    out DynamicDecalFrameStats stats);
                if (hasUpload)
                    UploadDecalBuffer(in stats);
                else if (stats.ActiveCount <= 0)
                {
                    _hasReadableBuffer = false;
                    _readCount = 0;
                    return;
                }

                if (readableBuffer == null || readableCount <= 0)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonDeferredDecalComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                _material.SetBuffer(ShaderConstants.DecalBufferId, readableBuffer);
                _material.SetInt(ShaderConstants.DecalCountId, readableCount);
                if (_settings.decalAtlas != null)
                    _material.SetTexture(ShaderConstants.DecalAtlasId, _settings.decalAtlas);
                _material.SetVector(
                    ShaderConstants.DecalAtlasParamsId,
                    new Vector4(
                        Mathf.Max(1, _settings.atlasSlices),
                        Mathf.Clamp01(stats.GlobalQualityWeight),
                        Mathf.Max(0f, _settings.intensity),
                        _settings.decalAtlas != null ? 1f : 0f));
                _material.SetColor(ShaderConstants.DecalTintId, _settings.decalTint);
                Vector3 cameraPosition = cameraData.camera != null ? cameraData.camera.transform.position : Vector3.zero;
                _material.SetVector(ShaderConstants.DecalCameraPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, compositeTexture, _material, 0),
                           passName: "Hecton Deferred Decal Composite",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
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
                NativeArray<DecalInstanceDTO> mapped = target.LockBufferForWrite<DecalInstanceDTO>(0, uploadCount);
                try
                {
                    unsafe
                    {
                        DynamicDecalMappedUploadJob uploadJob = new DynamicDecalMappedUploadJob
                        {
                            Source = stats.UploadBuffer,
                            Destination = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                            Count = uploadCount
                        };
                        JobHandle handle = uploadJob.Schedule();
                        H8Memory.RegisterActiveJob(SystemID.Vfx, handle);
                        // [BLOCKING_SYNC_POINT] Unity requires mapped GraphicsBuffer writes to finish before UnlockBufferAfterWrite.
                        DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                    }
                }
                finally
                {
                    target.UnlockBufferAfterWrite<DecalInstanceDTO>(uploadCount);
                }

                float uploadUs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) *
                                         1000000.0d /
                                         System.Diagnostics.Stopwatch.Frequency);
                DynamicDecalVaultRuntime.RecordGpuUploadMicroseconds(uploadUs);
                _readBufferIndex = _writeBufferIndex;
                _readCount = uploadCount;
                _hasReadableBuffer = true;
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
                _decalBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DecalInstanceDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[decal capacity A] - dynamic deferred decal double-buffer upload - owner: SHINOBU_149
                _decalBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DecalInstanceDTO>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[decal capacity B] - dynamic deferred decal double-buffer upload - owner: SHINOBU_149
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
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int DecalBufferId = Shader.PropertyToID("_HectonDeferredDecals");
            internal static readonly int DecalCountId = Shader.PropertyToID("_HectonDeferredDecalCount");
            internal static readonly int DecalAtlasId = Shader.PropertyToID("_HectonDeferredDecalAtlas");
            internal static readonly int DecalAtlasParamsId = Shader.PropertyToID("_HectonDeferredDecalAtlasParams");
            internal static readonly int DecalTintId = Shader.PropertyToID("_HectonDeferredDecalTint");
            internal static readonly int DecalCameraPositionId = Shader.PropertyToID("_HectonDeferredDecalCameraWS");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DeferredDecalCompositePass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.deferredDecalShader == null)
                settings.deferredDecalShader = AssetDatabase.LoadAssetAtPath<Shader>(DeferredDecalShaderPath);
#endif

            _pass ??= new DeferredDecalCompositePass();
            RecreateMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.deferredDecalShader == null || _material == null || _pass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
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
