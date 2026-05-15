using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Gameplay;
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
    /// Single-pass depth fog deception. It reads depth, dithers fog coverage with deterministic IGN, and composites before transparents.
    /// </summary>
    public sealed class HectonNoirDepthFogFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_NoirDepthFog.shader";
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
            [Tooltip("Hidden fullscreen shader used for depth-based noir fog.")]
            public Shader shader = null;

            [Tooltip("Injection point. Before transparents keeps particles and visor overlays readable.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Shallow fog tint in linear space after Unity converts the serialized color.")]
            public Color shallowFogColor = new Color(0.025f, 0.075f, 0.095f, 1f);

            [Tooltip("Abyss fog tint. Keep nonzero; pure black is forbidden by noir dithering mandate.")]
            public Color abyssFogColor = new Color(0.004f, 0.010f, 0.018f, 1f);

            [Tooltip("Visual fog gain for the depth ramp. This is not physical extinction.")]
            [Range(0.0001f, 0.05f)] public float density = 0.0105f;

            [Tooltip("Fog starts after this eye-space distance.")]
            [Range(0f, 15f)] public float startDistanceMeters = 1.5f;

            [Tooltip("Eye-space distance where the fake fog ramp reaches abyss coverage.")]
            [Range(10f, 180f)] public float maxDepthMeters = 80f;

            [Tooltip("Coverage noise amplitude. Applied to fog alpha only; no clip/discard.")]
            [Range(0f, 1f)] public float ditherStrength = 0.8f;

            [Tooltip("Skips noir depth fog while the player camera is above water or inside the readable surface band.")]
            public bool bypassNearSurface = true;

            [Tooltip("Depth below waterline where surface readability still wins over noir fog.")]
            [Range(0.05f, 4f)] public float nearSurfaceBypassDepthMeters = 0.85f;
        }

        private sealed class NoirDepthFogPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Noir Depth Fog");
            private FeatureSettings _settings;
            private Material _material;
            private Material _lastUploadedMaterial;
            private bool _hasMaterialState;
            private Color _lastShallowFogColor;
            private Color _lastAbyssFogColor;
            private Vector4 _lastParamsA;
            private Vector4 _lastParamsB;

            public NoirDepthFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!Application.isPlaying || _settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonNoirDepthFogComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                UpdateMaterialParameters(_material, _settings);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 0),
                           passName: "Hecton Noir Depth Fog",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialParameters(Material material, FeatureSettings settings)
            {
                Color shallowFogColor = settings.shallowFogColor.linear;
                Color abyssFogColor = settings.abyssFogColor.linear;
                Vector4 paramsA = new Vector4(
                    math.max(settings.density, 0.00001f),
                    math.max(settings.startDistanceMeters, 0f),
                    math.max(settings.maxDepthMeters, 1f),
                    0f);
                Vector4 paramsB = new Vector4(0f, 0f, 0f, math.saturate(settings.ditherStrength));

                if (_lastUploadedMaterial != material)
                {
                    _lastUploadedMaterial = material;
                    _hasMaterialState = false;
                }

                bool materialDirty = !_hasMaterialState;
                SetMaterialColorIfChanged(material, ShaderConstants.ShallowColorId, shallowFogColor, ref _lastShallowFogColor, materialDirty);
                SetMaterialColorIfChanged(material, ShaderConstants.AbyssColorId, abyssFogColor, ref _lastAbyssFogColor, materialDirty);
                SetMaterialVectorIfChanged(material, ShaderConstants.ParamsAId, paramsA, ref _lastParamsA, materialDirty);
                SetMaterialVectorIfChanged(material, ShaderConstants.ParamsBId, paramsB, ref _lastParamsB, materialDirty);

                _hasMaterialState = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void SetMaterialColorIfChanged(Material material, int shaderId, Color value, ref Color cachedValue, bool materialDirty)
            {
                if (!materialDirty && cachedValue == value)
                    return;

                material.SetColor(shaderId, value);
                cachedValue = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue, bool materialDirty)
            {
                if (!materialDirty && cachedValue == value)
                    return;

                material.SetVector(shaderId, value);
                cachedValue = value;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int ShallowColorId = Shader.PropertyToID("_HectonNoirDepthFogShallowColor");
            internal static readonly int AbyssColorId = Shader.PropertyToID("_HectonNoirDepthFogAbyssColor");
            internal static readonly int ParamsAId = Shader.PropertyToID("_HectonNoirDepthFogParamsA");
            internal static readonly int ParamsBId = Shader.PropertyToID("_HectonNoirDepthFogParamsB");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private NoirDepthFogPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/NoirDepthFog");
            RecreateMaterial(ref _material, shader);
            _pass ??= new NoirDepthFogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
                return;

            if (settings == null || _pass == null || _material == null)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            if (settings.bypassNearSurface &&
                ShouldBypassForSurfaceReadability(renderingData.cameraData.camera, settings.nearSurfaceBypassDepthMeters))
            {
                return;
            }

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        private static bool ShouldBypassForSurfaceReadability(Camera renderCamera, float nearSurfaceBypassDepthMeters)
        {
            if (renderCamera == null)
                return false;

            HectonPlayerMovement playerMovement = null;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                playerMovement = runtimeContext.PlayerMovement;

            if (playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            }

            if (playerMovement != null)
            {
                float safeDepth = math.max(0.05f, nearSurfaceBypassDepthMeters);
                return !playerMovement.IsPlayerSubmerged || playerMovement.CurrentDepth <= safeDepth;
            }

            return renderCamera.transform.position.y >= -0.25f;
        }

        protected override void Dispose(bool disposing)
        {
            DisposeMaterial(ref _material);
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
    }
}
