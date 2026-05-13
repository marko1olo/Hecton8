using System;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using Hecton8.Core;
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

            [Tooltip("Atlas sampled by the deferred decal pass.")]
            public Texture2D decalAtlas = null;

            [Tooltip("Maximum number of active decals uploaded to the fullscreen decal buffer.")]
            [Range(1, 256)] public int maxDecals = 256;

            [Tooltip("Atlas column count used to decode structural rupture atlas indices.")]
            [Range(1, 8)] public int atlasColumns = 4;

            [Tooltip("Atlas row count used to decode structural rupture atlas indices.")]
            [Range(1, 8)] public int atlasRows = 4;

            [Tooltip("Global additive tint for the projected rust/crack decals.")]
            public Color decalTint = new Color(0.72f, 0.44f, 0.32f, 1f);

            [Tooltip("Global additive intensity applied to the sampled decal atlas.")]
            [Range(0f, 4f)] public float intensity = 1f;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DecalGpuData
        {
            public Vector4 Row0;
            public Vector4 Row1;
            public Vector4 Row2;
            public Vector4 Row3;
            public Vector4 AtlasRect;
            public Vector4 Tint;
        }

        private sealed class DeferredDecalCompositePass : ScriptableRenderPass, IDisposable
        {
            private const int MaxFluidScreenSpaceDecals = 32;

            private sealed class CompositePassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Deferred Decals");
            private readonly DecalGpuData[] _decalUpload = new DecalGpuData[256]; // COLD ALLOC: DecalGpuData[256] - deferred decal upload cache for global crack matrices - owner: DeferredDecalPass
            private readonly Matrix4x4[] _fluidDecalMatrices = new Matrix4x4[MaxFluidScreenSpaceDecals]; // COLD ALLOC: Matrix4x4[32] - screen-space fluid decal matrix scratch - owner: DeferredDecalPass
            private readonly Color[] _fluidDecalColors = new Color[MaxFluidScreenSpaceDecals]; // COLD ALLOC: Color[32] - screen-space fluid decal tint scratch - owner: DeferredDecalPass

            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _decalBuffer;

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
                if (_decalBuffer != null)
                {
                    _decalBuffer.Release();
                    _decalBuffer = null;
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null || _settings.decalAtlas == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                int decalCount = UploadDecalBuffer();
                if (decalCount <= 0)
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

                _material.SetBuffer(ShaderConstants.DecalBufferId, _decalBuffer);
                _material.SetInt(ShaderConstants.DecalCountId, decalCount);
                _material.SetTexture(ShaderConstants.DecalAtlasId, _settings.decalAtlas);
                _material.SetVector(
                    ShaderConstants.DecalAtlasParamsId,
                    new Vector4(
                        Mathf.Max(1, _settings.atlasColumns),
                        Mathf.Max(1, _settings.atlasRows),
                        Mathf.Max(0f, _settings.intensity),
                        0f));
                _material.SetColor(ShaderConstants.DecalTintId, _settings.decalTint);

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Hecton Deferred Decal Composite", out CompositePassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = compositeTexture;
                    passData.material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc((CompositePassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        CoreUtils.SetRenderTarget(cmd, data.destination, ClearFlag.None);
                        Blitter.BlitTexture(cmd, data.source, Vector2.one, data.material, 0);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private int UploadDecalBuffer()
            {
                int safeCapacity = Mathf.Clamp(_settings.maxDecals, 1, _decalUpload.Length);
                EnsureDecalBuffer(safeCapacity);

                var matrices = BaseDegradationSystem.GlobalCrackDecalMatrices;
                var atlasIndices = BaseDegradationSystem.GlobalCrackDecalAtlasIndices;
                int atlasColumns = Mathf.Max(1, _settings.atlasColumns);
                int atlasRows = Mathf.Max(1, _settings.atlasRows);
                int uploadCount = 0;
                int structuralDecalCount = Mathf.Min(safeCapacity, matrices.Count, atlasIndices.Count);

                for (int decalIndex = 0; decalIndex < structuralDecalCount; decalIndex++)
                {
                    Matrix4x4 worldToDecal = matrices[decalIndex].inverse;
                    int atlasIndex = Mathf.Max(0, atlasIndices[decalIndex]);

                    _decalUpload[uploadCount] = new DecalGpuData
                    {
                        Row0 = worldToDecal.GetRow(0),
                        Row1 = worldToDecal.GetRow(1),
                        Row2 = worldToDecal.GetRow(2),
                        Row3 = worldToDecal.GetRow(3),
                        AtlasRect = ResolveAtlasRect(atlasIndex, atlasColumns, atlasRows),
                        Tint = new Vector4(_settings.decalTint.r, _settings.decalTint.g, _settings.decalTint.b, _settings.decalTint.a)
                    };
                    uploadCount++;
                }

                uploadCount = AppendFluidScreenSpaceDecals(uploadCount, safeCapacity, atlasColumns, atlasRows);
                if (uploadCount <= 0)
                    return 0;

                GraphicsBufferUploadUtility.UploadArray(_decalBuffer, _decalUpload, uploadCount);
                return uploadCount;
            }

            private int AppendFluidScreenSpaceDecals(int uploadCount, int safeCapacity, int atlasColumns, int atlasRows)
            {
                if (uploadCount >= safeCapacity || GlobalRegistry.AbyssalFluidDecals == null)
                    return uploadCount;

                int remainingCapacity = Mathf.Min(MaxFluidScreenSpaceDecals, safeCapacity - uploadCount);
                int fluidDecalCount = GlobalRegistry.AbyssalFluidDecals.CopyScreenSpaceDecals(
                    _fluidDecalMatrices,
                    _fluidDecalColors,
                    remainingCapacity);
                if (fluidDecalCount <= 0)
                    return uploadCount;

                Vector4 fullAtlasRect = ResolveAtlasRect(0, atlasColumns, atlasRows);
                for (int decalIndex = 0; decalIndex < fluidDecalCount && uploadCount < safeCapacity; decalIndex++)
                {
                    Matrix4x4 worldToDecal = _fluidDecalMatrices[decalIndex].inverse;
                    Color tint = _fluidDecalColors[decalIndex];
                    _decalUpload[uploadCount] = new DecalGpuData
                    {
                        Row0 = worldToDecal.GetRow(0),
                        Row1 = worldToDecal.GetRow(1),
                        Row2 = worldToDecal.GetRow(2),
                        Row3 = worldToDecal.GetRow(3),
                        AtlasRect = fullAtlasRect,
                        Tint = new Vector4(tint.r, tint.g, tint.b, tint.a)
                    };
                    uploadCount++;
                }

                return uploadCount;
            }

            private static Vector4 ResolveAtlasRect(int atlasIndex, int atlasColumns, int atlasRows)
            {
                float invColumns = 1f / atlasColumns;
                float invRows = 1f / atlasRows;
                int atlasX = atlasIndex % atlasColumns;
                int atlasY = (atlasIndex / atlasColumns) % atlasRows;
                return new Vector4(atlasX * invColumns, atlasY * invRows, invColumns, invRows);
            }

            private void EnsureDecalBuffer(int requiredCapacity)
            {
                if (_decalBuffer != null && _decalBuffer.count == requiredCapacity)
                    return;

                if (_decalBuffer != null)
                {
                    _decalBuffer.Release();
                    _decalBuffer = null;
                }

                _decalBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DecalGpuData>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[maxDecals] - deferred crack/rust decal buffer - owner: DeferredDecalPass
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int DecalBufferId = Shader.PropertyToID("_HectonDeferredDecals");
            internal static readonly int DecalCountId = Shader.PropertyToID("_HectonDeferredDecalCount");
            internal static readonly int DecalAtlasId = Shader.PropertyToID("_HectonDeferredDecalAtlas");
            internal static readonly int DecalAtlasParamsId = Shader.PropertyToID("_HectonDeferredDecalAtlasParams");
            internal static readonly int DecalTintId = Shader.PropertyToID("_HectonDeferredDecalTint");
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
