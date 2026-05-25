using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
    /// Fullscreen 1-bit CRT-green diagnostic compositor with AUP-space loot emphasis.
    /// </summary>
    public sealed class HectonBiosDiagnosticFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const int LootRefreshCallMask = 0x07;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hidden_Hecton_BiosDiagnostic.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            public Shader shader = null;
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public bool forceEnabled;
            [Range(0f, 1f)] public float forcedIntensity = 1f;
            [Min(1f)] public float lootSearchRadius = 140f;
            [Min(0f)] public float lootRadiusPadding = 0.45f;
            [Range(0f, 1f)] public float ditherStrength = 0.84f;
            [Range(0f, 1f)] public float scanlineStrength = 0.48f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(float intensity, bool hasLoot, Vector4 lootSphereAup)
            {
                Intensity = intensity;
                HasLoot = hasLoot ? (byte)1 : (byte)0;
                LootSphereAup = lootSphereAup;
            }

            public readonly float Intensity;
            public readonly byte HasLoot;
            public readonly Vector4 LootSphereAup;
        }

        private sealed class DiagnosticPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton BIOS Diagnostic");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _state;
            private Vector4 _appliedLootSphereAup;
            private float _appliedIntensity = -1f;
            private float _appliedLootActive = -1f;
            private float _appliedDitherStrength = -1f;
            private float _appliedScanlineStrength = -1f;
            private bool _materialDirty = true;

            public DiagnosticPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, RuntimeState state)
            {
                if (!ReferenceEquals(_material, material))
                    _materialDirty = true;

                _settings = settings;
                _material = material;
                _state = state;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
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
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonBiosDiagnostic";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialIfNeeded(_material, _settings, _state);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                           passName: "Hecton BIOS Diagnostic",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialIfNeeded(Material material, FeatureSettings settings, RuntimeState state)
            {
                float intensity = math.saturate(state.Intensity);
                float lootActive = state.HasLoot != 0 ? 1f : 0f;
                float ditherStrength = math.saturate(settings.ditherStrength);
                float scanlineStrength = math.saturate(settings.scanlineStrength);

                if (_materialDirty || math.abs(_appliedIntensity - intensity) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.IntensityId, intensity);
                    _appliedIntensity = intensity;
                }

                if (_materialDirty || math.abs(_appliedLootActive - lootActive) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.LootActiveId, lootActive);
                    _appliedLootActive = lootActive;
                }

                if (_materialDirty || Vector4DistanceSq(_appliedLootSphereAup, state.LootSphereAup) > 0.000001f)
                {
                    material.SetVector(ShaderConstants.LootSphereId, state.LootSphereAup);
                    _appliedLootSphereAup = state.LootSphereAup;
                }

                if (_materialDirty || math.abs(_appliedDitherStrength - ditherStrength) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.DitherStrengthId, ditherStrength);
                    _appliedDitherStrength = ditherStrength;
                }

                if (_materialDirty || math.abs(_appliedScanlineStrength - scanlineStrength) > 0.0005f)
                {
                    material.SetFloat(ShaderConstants.ScanlineStrengthId, scanlineStrength);
                    _appliedScanlineStrength = scanlineStrength;
                }

                _materialDirty = false;
            }

            private static float Vector4DistanceSq(Vector4 a, Vector4 b)
            {
                float x = a.x - b.x;
                float y = a.y - b.y;
                float z = a.z - b.z;
                float w = a.w - b.w;
                return x * x + y * y + z * z + w * w;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonBiosDiagnosticIntensity");
            internal static readonly int LootActiveId = Shader.PropertyToID("_HectonBiosLootActive");
            internal static readonly int LootSphereId = Shader.PropertyToID("_HectonBiosLootSphere");
            internal static readonly int DitherStrengthId = Shader.PropertyToID("_HectonBiosDitherStrength");
            internal static readonly int ScanlineStrengthId = Shader.PropertyToID("_HectonBiosScanlineStrength");
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
        private HectonPlayerMovement _cachedPlayerMovement;
        private bool _hotSwapRegistered;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new DiagnosticPass();
            Shader shader = settings != null ? settings.shader : null;
            RecreateMaterial(ref _material, shader);
            TryRegisterHotSwapListener();
            CachePlayerContext(Hecton8.Core.GlobalRegistry.Player);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
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

            int frame = Time.frameCount;
            bool refreshLootCache = !_lootCacheInitialized;
            if (!refreshLootCache && _lastLootRefreshFrame != frame)
            {
                _lastLootRefreshFrame = frame;
                refreshLootCache = (++_lootRefreshCounter & LootRefreshCallMask) == 0;
            }

            if (refreshLootCache)
            {
                _lastLootRefreshFrame = frame;
                HectonPlayerMovement playerMovement = ResolvePlayerMovement();
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition observerAup = playerMovement.PredictedAup;
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

            _pass.Setup(settings, _material, new RuntimeState(intensity, _cachedHasLoot, _cachedLootSphereAup));
            renderer.EnqueuePass(_pass);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            _cachedPlayerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            return _cachedPlayerMovement;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
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
            _cachedPlayerMovement = null;
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

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                CoreUtils.Destroy(material);
                material = null;
                return;
            }

            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
    }
}
