using System;
using Hecton8.Core;
using Hecton8.Gameplay;
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
    /// Unified fullscreen visor post pass for damage chroma, heat haze, pressure warp, crack reveal, dirt, stress, hypoxia, and blood edge tint.
    /// </summary>
    public sealed class HectonVisorUberPostFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/HectonVisorUberPost.shader";
#endif

        private const float MaterialFloatEpsilon = 0.0001f;
        private const float DefaultHypoxiaSafeOxygen01 = 0.22f;
        private const float TemperatureActivityThreshold = 0.001f;
        private const uint BleedingStatusBit = 1u;

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for the unified visor post pass.")]
            public Shader shader = null;

            [Tooltip("Packed crack normal/alpha texture. RG is normal XY; alpha is reveal threshold.")]
            public Texture2D crackTexture = null;

            [Tooltip("Lens dirt texture multiplied by a blue-noise dither mask.")]
            public Texture2D lensDirtTexture = null;

            [Tooltip("Blue-noise texture used to dither lens dirt.")]
            public Texture2D blueNoiseTexture = null;

            [Tooltip("Injection point for the unified visor post effect.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Single-sample chromatic damage strength.")]
            [Range(0f, 1f)] public float chromaticStrength = 0.34f;

            [Tooltip("Hypoxia desaturation strength.")]
            [Range(0f, 1f)] public float hypoxiaDesaturationStrength = 0.72f;

            [Tooltip("Pressure barrel warp strength.")]
            [Range(0f, 0.18f)] public float pressureWarpStrength = 0.035f;

            [Tooltip("Crack darken/normal strength gate.")]
            [Range(0f, 1f)] public float crackStrength = 0.82f;

            [Tooltip("Pressure delta to effect scalar. Pressure starts at 1 atm.")]
            [Min(0f)] public float pressureInvRange = 0.045f;

            [Tooltip("Temperature scalar used for heat haze activity.")]
            [Min(0f)] public float temperatureScale = 0.018f;

            [Tooltip("Crack normal UV displacement strength.")]
            [Range(0f, 0.01f)] public float crackUvStrength = 0.0024f;

            [Tooltip("Lens dirt and blood edge strength.")]
            [Range(0f, 1f)] public float lensDirtAndBloodStrength = 0.26f;

            [Tooltip("Heat haze sine frequency.")]
            [Min(1f)] public float heatHazeFrequency = 38f;

            [Tooltip("Heat haze sine speed.")]
            [Min(0f)] public float heatHazeSpeed = 0.62f;

            [Tooltip("Heat haze UV displacement amplitude. Forced to zero on low tier.")]
            [Range(0f, 0.006f)] public float heatHazeAmplitude = 0.0017f;

            [Tooltip("Damage/stress vignette strength.")]
            [Range(0f, 1f)] public float damageVignetteStrength = 0.24f;

            [Tooltip("Below or equal to this VRAM amount, heat haze is disabled.")]
            [Min(256)] public int lowTierVideoMemoryMb = 2048;

            [Tooltip("Oxygen value below which hypoxia ramps when no stronger global signal is published.")]
            [Range(0.01f, 1f)] public float hypoxiaSafeOxygen01 = DefaultHypoxiaSafeOxygen01;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(
                float healthFraction,
                float localTemperature,
                float ambientPressure,
                float playerStress01,
                float hypoxia01,
                uint statusMask,
                float wetLens01,
                float hullStress01,
                uint aupShiftFrame,
                bool lowTier)
            {
                HealthFraction = healthFraction;
                LocalTemperature = localTemperature;
                AmbientPressure = ambientPressure;
                PlayerStress01 = playerStress01;
                Hypoxia01 = hypoxia01;
                Bleeding01 = (statusMask & BleedingStatusBit) != 0u ? 1f : 0f;
                WetLens01 = wetLens01;
                HullStress01 = hullStress01;
                AupShiftFrame = aupShiftFrame;
                LowTier = lowTier;
            }

            public float HealthFraction { get; }
            public float LocalTemperature { get; }
            public float AmbientPressure { get; }
            public float PlayerStress01 { get; }
            public float Hypoxia01 { get; }
            public float Bleeding01 { get; }
            public float WetLens01 { get; }
            public float HullStress01 { get; }
            public uint AupShiftFrame { get; }
            public bool LowTier { get; }
        }

        private sealed class VisorUberPostPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Uber Post"); // COLD ALLOC: ProfilingSampler[1] - RenderGraph marker reused for every frame - owner: VisorUberPostPass
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private Material _lastParameterMaterial;
            private Texture _lastCrackTexture;
            private Texture _lastLensDirtTexture;
            private Texture _lastBlueNoiseTexture;
            private Vector4 _lastStrengths0 = Vector4.positiveInfinity;
            private Vector4 _lastStrengths1 = Vector4.positiveInfinity;
            private Vector4 _lastWaveParams = Vector4.positiveInfinity;
            private Vector4 _lastTextureFlags = Vector4.positiveInfinity;
            private float _lastHealthFraction = float.PositiveInfinity;
            private float _lastLocalTemperature = float.PositiveInfinity;
            private float _lastAmbientPressure = float.PositiveInfinity;
            private float _lastPlayerStress01 = float.PositiveInfinity;
            private float _lastHypoxia01 = float.PositiveInfinity;
            private float _lastBleeding01 = float.PositiveInfinity;
            private float _lastWetLens01 = float.PositiveInfinity;
            private float _lastHullStress01 = float.PositiveInfinity;
            private float _lastAupShiftFrame = float.PositiveInfinity;
            private float _lastLowTier = float.PositiveInfinity;
            private bool _materialDirty = true;

            public VisorUberPostPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, RuntimeState runtimeState)
            {
                _settings = settings;
                _material = material;
                _runtimeState = runtimeState;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView)
                {
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonVisorUberPost";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                UpdateMaterialParameters(_material, _settings, _runtimeState);

#pragma warning disable 0618
                using (var builder = renderGraph.AddRenderPass<PassData>("Hecton Visor Uber Post", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.destination = destinationTexture;
                    passData.material = _material;

                    builder.ReadTexture(sourceTexture);
                    builder.UseColorBuffer(destinationTexture, 0);
                    builder.UseDepthBuffer(depthTexture, DepthAccess.Read);

                    builder.SetRenderFunc(static (PassData data, RenderGraphContext context) =>
                    {
                        Blitter.BlitCameraTexture(
                            context.cmd,
                            data.source,
                            data.destination,
                            RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store,
                            data.material,
                            0);
                    });
                }
#pragma warning restore 0618

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialParameters(Material material, FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!ReferenceEquals(_lastParameterMaterial, material))
                {
                    ResetMaterialParameterCache();
                    _lastParameterMaterial = material;
                }

                bool lowTier = runtimeState.LowTier;
                SetMaterialFloatIfChanged(material, ShaderConstants.HealthFractionId, Sanitize01(runtimeState.HealthFraction), ref _lastHealthFraction);
                SetMaterialFloatIfChanged(material, ShaderConstants.LocalTemperatureId, SanitizeFinite(runtimeState.LocalTemperature, 0f), ref _lastLocalTemperature);
                SetMaterialFloatIfChanged(material, ShaderConstants.AmbientPressureId, math.max(1f, SanitizeFinite(runtimeState.AmbientPressure, 1f)), ref _lastAmbientPressure);
                SetMaterialFloatIfChanged(material, ShaderConstants.PlayerStressId, Sanitize01(runtimeState.PlayerStress01), ref _lastPlayerStress01);
                SetMaterialFloatIfChanged(material, ShaderConstants.HypoxiaId, Sanitize01(runtimeState.Hypoxia01), ref _lastHypoxia01);
                SetMaterialFloatIfChanged(material, ShaderConstants.BleedingId, runtimeState.Bleeding01, ref _lastBleeding01);
                SetMaterialFloatIfChanged(material, ShaderConstants.WetLensId, Sanitize01(runtimeState.WetLens01), ref _lastWetLens01);
                SetMaterialFloatIfChanged(material, ShaderConstants.HullStressId, Sanitize01(runtimeState.HullStress01), ref _lastHullStress01);
                SetMaterialFloatIfChanged(material, ShaderConstants.AupShiftFrameId, runtimeState.AupShiftFrame, ref _lastAupShiftFrame);
                SetMaterialFloatIfChanged(material, ShaderConstants.LowTierId, lowTier ? 1f : 0f, ref _lastLowTier);

                Vector4 strengths0 = new Vector4(
                    math.saturate(settings.chromaticStrength),
                    math.saturate(settings.hypoxiaDesaturationStrength),
                    math.clamp(settings.pressureWarpStrength, 0f, 0.18f),
                    math.saturate(settings.crackStrength));
                Vector4 strengths1 = new Vector4(
                    math.max(0f, settings.pressureInvRange),
                    math.max(0f, settings.temperatureScale),
                    math.clamp(settings.crackUvStrength, 0f, 0.01f),
                    math.saturate(settings.lensDirtAndBloodStrength));
                Vector4 waveParams = new Vector4(
                    math.max(1f, settings.heatHazeFrequency),
                    math.max(0f, settings.heatHazeSpeed),
                    lowTier ? 0f : math.clamp(settings.heatHazeAmplitude, 0f, 0.006f),
                    math.saturate(settings.damageVignetteStrength));
                Vector4 textureFlags = new Vector4(
                    settings.crackTexture != null ? 1f : 0f,
                    settings.lensDirtTexture != null ? 1f : 0f,
                    settings.blueNoiseTexture != null ? 1f : 0f,
                    0f);
                SetMaterialVectorIfChanged(material, ShaderConstants.Strengths0Id, strengths0, ref _lastStrengths0);
                SetMaterialVectorIfChanged(material, ShaderConstants.Strengths1Id, strengths1, ref _lastStrengths1);
                SetMaterialVectorIfChanged(material, ShaderConstants.WaveParamsId, waveParams, ref _lastWaveParams);
                SetMaterialVectorIfChanged(material, ShaderConstants.TextureFlagsId, textureFlags, ref _lastTextureFlags);
                SetMaterialTextureIfChanged(material, ShaderConstants.CrackTextureId, settings.crackTexture != null ? settings.crackTexture : Texture2D.blackTexture, ref _lastCrackTexture);
                SetMaterialTextureIfChanged(material, ShaderConstants.LensDirtTextureId, settings.lensDirtTexture != null ? settings.lensDirtTexture : Texture2D.whiteTexture, ref _lastLensDirtTexture);
                SetMaterialTextureIfChanged(material, ShaderConstants.BlueNoiseTextureId, settings.blueNoiseTexture != null ? settings.blueNoiseTexture : Texture2D.grayTexture, ref _lastBlueNoiseTexture);
                _materialDirty = false;
            }

            private void ResetMaterialParameterCache()
            {
                _lastCrackTexture = null;
                _lastLensDirtTexture = null;
                _lastBlueNoiseTexture = null;
                _lastStrengths0 = Vector4.positiveInfinity;
                _lastStrengths1 = Vector4.positiveInfinity;
                _lastWaveParams = Vector4.positiveInfinity;
                _lastTextureFlags = Vector4.positiveInfinity;
                _lastHealthFraction = float.PositiveInfinity;
                _lastLocalTemperature = float.PositiveInfinity;
                _lastAmbientPressure = float.PositiveInfinity;
                _lastPlayerStress01 = float.PositiveInfinity;
                _lastHypoxia01 = float.PositiveInfinity;
                _lastBleeding01 = float.PositiveInfinity;
                _lastWetLens01 = float.PositiveInfinity;
                _lastHullStress01 = float.PositiveInfinity;
                _lastAupShiftFrame = float.PositiveInfinity;
                _lastLowTier = float.PositiveInfinity;
                _materialDirty = true;
            }

            private void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue)
            {
                if (!_materialDirty && math.abs(cachedValue - value) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }

            private void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue)
            {
                if (!_materialDirty &&
                    math.abs(cachedValue.x - value.x) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.y - value.y) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.z - value.z) <= MaterialFloatEpsilon &&
                    math.abs(cachedValue.w - value.w) <= MaterialFloatEpsilon)
                {
                    return;
                }

                material.SetVector(shaderId, value);
                cachedValue = value;
            }

            private void SetMaterialTextureIfChanged(Material material, int shaderId, Texture texture, ref Texture cachedTexture)
            {
                if (!_materialDirty && ReferenceEquals(cachedTexture, texture))
                    return;

                material.SetTexture(shaderId, texture);
                cachedTexture = texture;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HealthFractionId = Shader.PropertyToID("_HectonUberHealthFraction");
            internal static readonly int LocalTemperatureId = Shader.PropertyToID("_HectonUberLocalTemperature");
            internal static readonly int AmbientPressureId = Shader.PropertyToID("_HectonUberAmbientPressure");
            internal static readonly int PlayerStressId = Shader.PropertyToID("_HectonUberPlayerStress01");
            internal static readonly int HypoxiaId = Shader.PropertyToID("_HectonUberHypoxia01");
            internal static readonly int BleedingId = Shader.PropertyToID("_HectonUberBleeding01");
            internal static readonly int WetLensId = Shader.PropertyToID("_HectonUberWetLens01");
            internal static readonly int HullStressId = Shader.PropertyToID("_HectonUberHullStress01");
            internal static readonly int AupShiftFrameId = Shader.PropertyToID("_HectonUberAupShiftFrame");
            internal static readonly int LowTierId = Shader.PropertyToID("_HectonUberLowTier");
            internal static readonly int Strengths0Id = Shader.PropertyToID("_HectonUberStrengths0");
            internal static readonly int Strengths1Id = Shader.PropertyToID("_HectonUberStrengths1");
            internal static readonly int WaveParamsId = Shader.PropertyToID("_HectonUberWaveParams");
            internal static readonly int TextureFlagsId = Shader.PropertyToID("_HectonUberTextureFlags");
            internal static readonly int CrackTextureId = Shader.PropertyToID("_HectonVisorCrackTex");
            internal static readonly int LensDirtTextureId = Shader.PropertyToID("_HectonLensDirtTex");
            internal static readonly int BlueNoiseTextureId = Shader.PropertyToID("_HectonBlueNoiseTex");
            internal static readonly int PlayerStressGlobalId = Shader.PropertyToID("_PlayerStress01");
            internal static readonly int HypoxiaSignalGlobalId = Shader.PropertyToID("_HypoxiaSignal");
            internal static readonly int LocalTemperatureGlobalId = Shader.PropertyToID("_LocalTemperature");
            internal static readonly int AmbientPressureGlobalId = Shader.PropertyToID("_AmbientPressure");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings(); // COLD ALLOC: FeatureSettings[1] - serialized renderer feature settings - owner: HectonVisorUberPostFeature

        private VisorUberPostPass _pass;
        private Material _material;
        private int _cachedLowTierThresholdMb = int.MinValue;
        private int _cachedGraphicsMemoryMb;
        private bool _cachedLowTier;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            // COLD ALLOC: VisorUberPostPass[1] - reused ScriptableRenderPass instance - owner: HectonVisorUberPostFeature
            _pass ??= new VisorUberPostPass();
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            Camera renderCamera = renderingData.cameraData.camera;
            bool lowTier = ResolveLowTier(settings);
            if (!TryBuildRuntimeState(renderCamera, settings, lowTier, out RuntimeState runtimeState))
                return;

            _pass.Setup(settings, _material, runtimeState);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private static bool TryBuildRuntimeState(Camera renderCamera, FeatureSettings settings, bool lowTier, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || settings == null)
                return false;

            if (!TryResolvePlayerContext(out Camera playerCamera, out HectonPlayerMovement playerMovement, out uint contextStatusMask) ||
                playerCamera == null ||
                !ReferenceEquals(renderCamera, playerCamera))
            {
                return false;
            }

            float healthFraction = 1f;
            float oxygen01 = 1f;
            float ambientPressure = math.max(1f, SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.AmbientPressureGlobalId), 1f));
            if (UIStateStore.IsInitialized)
            {
                if (UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot))
                    healthFraction = Sanitize01(healthSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.Oxygen01, out UIValueSlot oxygenSlot))
                    oxygen01 = Sanitize01(oxygenSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.PressureAtm, out UIValueSlot pressureSlot))
                    ambientPressure = math.max(1f, SanitizeFinite(pressureSlot.Value, ambientPressure));
            }

            float wetLens = playerMovement != null ? Sanitize01(playerMovement.CurrentWetLensIntensity01) : 0f;
            float hullStress = playerMovement != null ? Sanitize01(playerMovement.CurrentHullStress01) : 0f;
            float localTemperature = SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.LocalTemperatureGlobalId), 0f);
            float globalStress = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.PlayerStressGlobalId));
            float playerStress = math.saturate(math.max(globalStress, math.max(hullStress, 1f - healthFraction)));
            float hypoxia = math.max(
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.HypoxiaSignalGlobalId)),
                ResolveHypoxiaFromOxygen(oxygen01, settings.hypoxiaSafeOxygen01));
            uint statusMask = contextStatusMask;

            bool hasActiveSignal =
                healthFraction < 0.999f ||
                wetLens > 0.001f ||
                hullStress > 0.001f ||
                playerStress > 0.001f ||
                hypoxia > 0.001f ||
                statusMask != 0u ||
                ambientPressure > 1.001f ||
                math.abs(localTemperature) > TemperatureActivityThreshold ||
                settings.lensDirtTexture != null;
            if (!hasActiveSignal)
                return false;

            runtimeState = new RuntimeState(
                healthFraction,
                localTemperature,
                ambientPressure,
                playerStress,
                hypoxia,
                statusMask,
                wetLens,
                hullStress,
                HectonFloatingOrigin.CurrentShiftSequence,
                lowTier);
            return true;
        }

        private static bool TryResolvePlayerContext(out Camera playerCamera, out HectonPlayerMovement playerMovement, out uint statusMask)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                playerCamera = runtimeContext.PlayerCamera;
                playerMovement = runtimeContext.PlayerMovement;
                statusMask = runtimeContext.SurvivalState.StatusMask;
                if (statusMask == 0u && runtimeContext.SurvivalSystem != null)
                    statusMask = runtimeContext.SurvivalSystem.StatusMask;
                return true;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                playerCamera = playerContext.PlayerCamera;
                playerMovement = playerContext.PlayerMovement;
                HectonSurvivalSystem survivalSystem = playerContext.SurvivalSystem;
                statusMask = survivalSystem != null ? survivalSystem.StatusMask : 0u;
                return true;
            }

            playerCamera = null;
            playerMovement = null;
            statusMask = 0u;
            return false;
        }

        private bool ResolveLowTier(FeatureSettings currentSettings)
        {
            int thresholdMb = currentSettings != null ? math.max(256, currentSettings.lowTierVideoMemoryMb) : 2048;
            if (_cachedLowTierThresholdMb == thresholdMb)
                return _cachedLowTier;

            _cachedGraphicsMemoryMb = SystemInfo.graphicsMemorySize;
            _cachedLowTierThresholdMb = thresholdMb;
            _cachedLowTier = _cachedGraphicsMemoryMb > 0 && _cachedGraphicsMemoryMb <= thresholdMb;
            return _cachedLowTier;
        }

        private static float ResolveHypoxiaFromOxygen(float oxygen01, float safeThreshold)
        {
            float safe = math.clamp(safeThreshold, 0.01f, 1f);
            float oxygen = Sanitize01(oxygen01);
            return oxygen < safe ? math.saturate(1f - oxygen * math.rcp(safe)) : 0f;
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
            // COLD ALLOC: Material[1] - engine-owned fullscreen post material recreated only when shader changes - owner: HectonVisorUberPostFeature
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }
}
