using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen 1-bit CRT-green diagnostic compositor with AUP-space loot emphasis.
    /// </summary>
    public sealed class HectonBiosDiagnosticFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const int LootRefreshCallMask = 0x07;

        [Serializable]
        private sealed class FeatureSettings
        {
            [FormerlySerializedAs("shader")]
            public Material material = null;
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public bool forceEnabled;
            [Range(0f, 1f)] public float forcedIntensity = 1f;
            [Min(1f)] public float lootSearchRadius = 140f;
            [Min(0f)] public float lootRadiusPadding = 0.45f;
            [Range(0f, 1f)] public float ditherStrength = 0.84f;
            [Range(0f, 1f)] public float scanlineStrength = 0.48f;
        }

        private struct RuntimeState
        {
            public float Intensity;
            public byte HasLoot;
            public Vector4 LootSphereAup;
        }

        private sealed class DiagnosticPass : ScriptableRenderPass
        {
            private sealed class DiagnosticPassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public Material Material;
                public MaterialPropertyBlock Properties;
                public Vector4 LootSphereAup;
                public float Intensity;
                public float LootActive;
                public float DitherStrength;
                public float ScanlineStrength;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton BIOS Diagnostic");
            private FeatureSettings _settings;
            private Material _material;
            private MaterialPropertyBlock _drawProperties;
            private RuntimeState _state;

            public DiagnosticPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, RuntimeState state)
            {
                _settings = settings;
                _material = material;
                _state = state;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                _drawProperties?.Clear();
                _drawProperties = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null || _state.Intensity <= 0.001f)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonBiosDiagnostic";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                EnsureDrawProperties();

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DiagnosticPassData>(
                           "Hecton BIOS Diagnostic",
                           out DiagnosticPassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Material = _material;
                    passData.Properties = _drawProperties;
                    passData.LootSphereAup = _state.LootSphereAup;
                    passData.Intensity = math.saturate(_state.Intensity);
                    passData.LootActive = _state.HasLoot != 0 ? 1f : 0f;
                    passData.DitherStrength = math.saturate(_settings.ditherStrength);
                    passData.ScanlineStrength = math.saturate(_settings.scanlineStrength);

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (DiagnosticPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null || data.Properties == null)
                            return;

                        UpdateDrawProperties(data.Properties, data);
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, data.Properties, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private static void UpdateDrawProperties(MaterialPropertyBlock properties, DiagnosticPassData data)
            {
                properties.Clear();
                properties.SetFloat(ShaderConstants.IntensityId, data.Intensity);
                properties.SetFloat(ShaderConstants.LootActiveId, data.LootActive);
                properties.SetVector(ShaderConstants.LootSphereId, data.LootSphereAup);
                properties.SetFloat(ShaderConstants.DitherStrengthId, data.DitherStrength);
                properties.SetFloat(ShaderConstants.ScanlineStrengthId, data.ScanlineStrength);
            }

            private void EnsureDrawProperties()
            {
                _drawProperties ??= new MaterialPropertyBlock(); // COLD ALLOC: BIOS diagnostic per-pass payload - owner: HECTON_BIOS_DIAGNOSTIC
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonBiosDiagnosticIntensity");
            internal static readonly int LootActiveId = Shader.PropertyToID("_HectonBiosLootActive");
            internal static readonly int LootSphereId = Shader.PropertyToID("_HectonBiosLootSphere");
            internal static readonly int DitherStrengthId = Shader.PropertyToID("_HectonBiosDitherStrength");
            internal static readonly int ScanlineStrengthId = Shader.PropertyToID("_HectonBiosScanlineStrength");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DiagnosticPass _pass;
        private Material _material;
        private Vector4 _cachedLootSphereAup;
        private int _lastLootRefreshFrame = -1;
        private int _lootRefreshCounter;
        private bool _lootCacheInitialized;
        private bool _cachedHasLoot;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;

        public override void Create()
        {
            _pass ??= new DiagnosticPass();
            _material = settings != null ? settings.material : null;
            TryRegisterHotSwapListener();
            CachePlayerContext(Hecton8.Core.GlobalRegistry.Player);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings != null)
                _material = settings.material;

            if (settings == null || _pass == null || _material == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            float intensity = settings.forceEnabled
                ? math.saturate(settings.forcedIntensity)
                : HectonBiosDiagnosticState.Intensity;
            if (intensity <= 0.001f)
                return;

            Camera camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            bool refreshLootCache = !_lootCacheInitialized;
            if (!refreshLootCache && _lastLootRefreshFrame != frame)
            {
                _lastLootRefreshFrame = frame;
                refreshLootCache = (++_lootRefreshCounter & LootRefreshCallMask) == 0;
            }

            if (refreshLootCache)
            {
                _lastLootRefreshFrame = frame;
                if (TryResolvePlayerObserverAup(out AbsoluteUniversePosition observerAup))
                {
                    _cachedHasLoot = HectonScanRenderRegistry.TryFindNearestLootSphereAup(
                        in observerAup,
                        settings.lootSearchRadius,
                        settings.lootRadiusPadding,
                        out _cachedLootSphereAup);
                }
                else
                {
                    _cachedHasLoot = false;
                    _cachedLootSphereAup = default;
                }

                _lootCacheInitialized = true;
            }

            RuntimeState runtimeState = default;
            runtimeState.Intensity = intensity;
            runtimeState.HasLoot = _cachedHasLoot ? (byte)1 : (byte)0;
            runtimeState.LootSphereAup = _cachedLootSphereAup;
            _pass.Setup(settings, _material, runtimeState);
            renderer.EnqueuePass(_pass);
        }

        private bool TryResolvePlayerObserverAup(out AbsoluteUniversePosition observerAup)
        {
            observerAup = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                return false;
            }

            observerAup = movementState.PredictedAup;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _material = null;
            CachePlayerContext(null);
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                CachePlayerContext(currentService as IPlayerRuntimeContext);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private void CachePlayerContext(IPlayerRuntimeContext playerContext)
        {
            if (ReferenceEquals(_cachedPlayerContext, playerContext))
                return;

            _cachedPlayerContext = playerContext;
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

    }
}
