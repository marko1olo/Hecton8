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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen 1-bit CRT-green diagnostic compositor with AUP-space loot emphasis.
    /// </summary>
    public sealed class HectonBiosDiagnosticFeature : ScriptableRendererFeature
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
                HasLoot = hasLoot;
                LootSphereAup = lootSphereAup;
            }

            public float Intensity { get; }
            public bool HasLoot { get; }
            public Vector4 LootSphereAup { get; }
        }

        private sealed class DiagnosticPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton BIOS Diagnostic");
            private FeatureSettings _settings;
            private Material _material;
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

                UpdateMaterial(_material, _settings, _state);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton BIOS Diagnostic", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = destinationTexture;
                    passData.material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(destinationTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(
                            cmd,
                            data.source,
                            data.destination,
                            RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store,
                            data.material,
                            0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private static void UpdateMaterial(Material material, FeatureSettings settings, RuntimeState state)
            {
                material.SetFloat(ShaderConstants.IntensityId, math.saturate(state.Intensity));
                material.SetFloat(ShaderConstants.LootActiveId, state.HasLoot ? 1f : 0f);
                material.SetVector(ShaderConstants.LootSphereId, state.LootSphereAup);
                material.SetFloat(ShaderConstants.DitherStrengthId, math.saturate(settings.ditherStrength));
                material.SetFloat(ShaderConstants.ScanlineStrengthId, math.saturate(settings.scanlineStrength));
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
        private int _lootRefreshCounter;
        private bool _lootCacheInitialized;
        private bool _cachedHasLoot;
        private HectonPlayerMovement _cachedPlayerMovement;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new DiagnosticPass();
            Shader shader = settings != null ? settings.shader : null;
            RecreateMaterial(ref _material, shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            float intensity = settings.forceEnabled
                ? math.saturate(settings.forcedIntensity)
                : HectonBiosDiagnosticState.Intensity;
            if (intensity <= 0.001f)
                return;

            Camera camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            if (!_lootCacheInitialized || (++_lootRefreshCounter & LootRefreshCallMask) == 0)
            {
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
            if (_cachedPlayerMovement != null)
                return _cachedPlayerMovement;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _cachedPlayerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            return _cachedPlayerMovement;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
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
