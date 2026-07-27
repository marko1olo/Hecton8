// ============================================================================
// HECTON-8 Ã¢â‚¬â€ HectonUnderwaterVisuals.cs  v5.1
// Ãâ€¢Ãâ€ÃËœÃÂÃÅ¾Ãâ€ºÃËœÃÂ§ÃÂÃÂ«Ãâ„¢ Ãâ€ÃËœÃÂ Ãâ€¢ÃÅ¡ÃÂ¢ÃÅ¾ÃÂ  ÃÂ¡ÃÂ Ãâ€¢Ãâ€ÃÂ«: Ã‘â€šÃ‘Æ’ÃÂ¼ÃÂ°ÃÂ½, Ã‘ÂÃÂ²ÃÂµÃ‘â€š, Ã‘â€ ÃÂ²ÃÂµÃ‘â€šÃÂ°, ÃÂºÃÂ°ÃÂ¼ÃÂµÃ‘â‚¬ÃÂ°, Ã‘â‚¬ÃÂ°Ã‘ÂÃ‘ÂÃÂµÃÂ¸ÃÂ²ÃÂ°ÃÂ½ÃÂ¸ÃÂµ Ã‘ÂÃÂ¾ÃÂ»ÃÂ½Ã‘â€ ÃÂ°.
//
// Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
// v5.1 CHANGES Ã¢â‚¬â€ RACE CONDITION Patch:
// Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
//
//   [FIX] EXECUTION ORDER:
//     DefaultExecutionOrder(-4000) ensures this script's OnEnable
//     fires AFTER AtmosphereManager(-6000) but BEFORE CelestialEngine(-3000).
//     Registration order in GameTickManager = tick order.
//
//     CHAIN (every frame, deterministic):
//       1. AtmosphereManager.Tick() Ã¢â€ â€™ fresh ProfileSunIntensity, ComputedHorizonFade
//       2. UnderwaterVisuals.Tick() Ã¢â€ â€™ sunLight.intensity = profile Ãƒâ€” horizon Ãƒâ€” depth
//       3. CelestialEngine.Tick()   Ã¢â€ â€™ sunLight.intensity *= eclipseVisibility
//
//   [FIX] ResolveHorizonFade():
//     Now reads AtmosphereManager.ComputedHorizonFade DIRECTLY
//     instead of recalculating from SunElevation with a potentially
//     different fadeAngle. ONE SOURCE OF TRUTH for horizon fade.
//     Removes the subtle desync where UnderwaterVisuals and
//     AtmosphereManager used different smoothstep curves.
//
//   [FIX] Surface light update:
//     Above water, sunLight.intensity is still written every frame
//     (profile Ãƒâ€” horizon) so CelestialEngine can multiply by eclipse.
//     Guard changed: only skip if BOTH baseSun AND horizon are zero
//     (prevents writing 0 when AtmosphereManager hasn't initialized).
//
// Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
// v5.0 PRESERVED:
//   Ã¢Å“â€œ Global depth curve (AnimationCurve)
//   Ã¢Å“â€œ Fog density derived from lightFactor
//   Ã¢Å“â€œ Sun scattering / color fade
//   Ã¢Å“â€œ Biome color interpolation
//   Ã¢Å“â€œ Camera background = fog color
//   Ã¢Å“â€œ EnforceFogState render callback
//   Ã¢Å“â€œ Zero GC in Tick
//   Ã¢Å“â€œ [ExecuteAlways] for Editor preview
//
// ÃÅ¡ÃÅ¾ÃÅ¾ÃÂ Ãâ€ÃËœÃÂÃÂÃÂ¦ÃËœÃÂ¯ Ãâ€”ÃÂÃÅ¸ÃËœÃÂ¡ÃËœ sunLight.intensity (v5.1):
//   AtmosphereManager(-6000) Ã¢â€ â€™ ProfileSunIntensity, ComputedHorizonFade (data)
//   UnderwaterVisuals(-4000) Ã¢â€ â€™ sunLight.intensity = profile Ãƒâ€” horizon Ãƒâ€” lightCurve
//   CelestialEngine(-3000)   Ã¢â€ â€™ sunLight.intensity *= (1 - eclipseOcclusion)
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Gameplay;
using Hecton8.VFX;
using Hecton8.World;
using NASAPunk.Visor;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)]
    public sealed class HectonUnderwaterVisuals : MonoBehaviour,
        Hecton8.Core.IColdTickable,
        Hecton8.Core.ISlowTickable,
        Hecton8.Core.ILateFrameTickable,
        Hecton8.Core.IRenderable,
        Hecton8.World.ISoundscapeEventListener,
        IBiomeMatrixEventListener,
        Hecton8.Core.IMapMagicBiomeEventListener,
        Hecton8.Core.IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        private const string HudFogLuminanceComputeAssetPath = "Assets/_Project/Art/Shaders/HectonHudFogLuminance.compute";
        private const string PhotophobiaFieldComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_PhotophobiaField.compute";
#endif

        internal static HectonUnderwaterVisuals ActiveRuntimeInstance { get; private set; }
        internal static Material RuntimeSkyMaterialReference { get; private set; }

        private const float RuntimeCameraResolveRetryInterval = 1f;
        private const float EditorCameraResolveRetryInterval = 0.25f;
        private const float VisualEnterUnderwaterDepth = 0.08f;
        private const float VisualExitUnderwaterDepth = 0.03f;
        private const float VisualForcedUnderwaterDepth = 0.18f;
        private const float VisualCameraDepthOverrideThreshold = 0.15f;
        private const float MaxSurfaceFogBlendDepth = 3f;
        private const float MaxSceneViewUnderwaterFogDensityScale = 0.24f;
        private const float UnderwaterFogDensityFloorNearSurface = 0.012f;
        private const float UnderwaterFogDensityFloorAtDepth = 0.0048f;
        private const float UnderwaterFogDensityFloorDepth = 8f;
        private const float DaylightReadableDepth = 620f;
        private const float DaylightReadableLightFloor = 0.36f;
        private const float DaylightReadableExtinctionReduction = 0.52f;
        private const float FogBlackoutStartDepthDay = 900f;
        private const float FogBlackoutStartDepthNight = 260f;
        private const float FogBlackBlendIntensity = 0.24f;
        private const float UnderwaterFarHazeStartDepth = 1.5f;
        private const float UnderwaterFarHazeFullDepth = 14f;
        private const float UnderwaterFarHazeDensityBoost = 0.00075f;
        private const float UnderwaterBaselineDistanceHaze = 0.00045f;
        private const float HudFogPerturbationMaxDensityBoost = 0.0012f;
        private const float HudFogPerturbationResponse = 8f;
        private const float UnderwaterMediumFogColorBlend = 0.54f;
        private const float UnderwaterDepthColumnHazeFullDepth = 36f;
        private const float UnderwaterDepthColumnHazeDensityBoost = 0.00055f;
        private const float UnderwaterBiomeFogInfluenceShallow = 0.18f;
        private const float UnderwaterBiomeFogInfluenceDeep = 0.34f;
        private const float UnderwaterBiomeFogInfluenceDepth = 90f;
        private const float HudFogLuminanceReadbackIntervalSeconds = 0.1f;
        private const uint KccVelocityUnderwaterVisualMaxAgeFrames = 12u;
        private const float HudFogVolumetricScatterBoost = 0.14f;
        private const float SuitCriticalHealthThreshold01 = 0.2f;
        private const int FlashlightPhotophobiaFieldResolution = 128;
        private const int PortableMaxComputeThreadsPerGroup = 256;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const float FlashlightPhotophobiaRecoveryGraceSeconds = 0.25f;
        private const float GpuBubbleTrailMinSpeed = 1.4f;
        private const float GpuBubbleTrailFullSpeed = 5.2f;
        private const float GpuBubbleExhaleImpulseDecayRate = 2.8f;
        private const float WeatherFlowResponseSeconds = 2.4f;
        private const float StormFogColorDriftRate = 0.45f;
        private const float StormFogColorInfluence = 0.46f;
        private const float CalmFlowVelocityMultiplier = 1f;
        private const float StormFlowVelocityMultiplier = 3f;
        private const float CalmTurbulenceFrequency = 0.26f;
        private const float StormTurbulenceFrequency = 0.8f;
        private const float UnderwaterSunlitTintDepth = 32f;
        private const float UnderwaterSunlitTintStrength = 0.38f;
        private const float UnderwaterSunlitAmbientDepth = 42f;
        private const float UnderwaterSunlitAmbientStrength = 0.24f;
        private const float GameplayReadableBeerLambertExtinctionBias = 0.72f;
        private const float UnderwaterShallowColumnColorDepth = 56f;
        private const float UnderwaterShallowColumnColorStrength = 0.12f;
        private const float UnderwaterDaylightSeaTintDepth = 96f;
        private const float UnderwaterDaylightSeaTintStrength = 0.34f;
        private const float UnderwaterDaylightAmbientTintStrength = 0.18f;
        private const float UnderwaterClearWaterMotesStrength = 0.2f;
        private const float SuspendedMotesQualityRefreshEpsilon = 0.01f;
        private const string UnderwaterSuspendedMotesChildName = "Underwater_SuspendedMotes";
        private const string UnderwaterExhaleBubblesChildName = "Underwater_ExhaleBubbles";
        private const string UnderwaterShallowSunBeamChildName = "Underwater_ShallowSunBeam";
        private static readonly Color UnderwaterDaylightSeaTintShallow = new Color(0.118f, 0.402f, 0.424f, 1f);
        private static readonly Color UnderwaterDaylightSeaTintMid = new Color(0.026f, 0.156f, 0.238f, 1f);
        private static readonly Color StormFogDriftDeepBlue = new Color(0.015f, 0.055f, 0.105f, 1f);
        private static readonly Color StormFogDriftGreenGray = new Color(0.035f, 0.075f, 0.062f, 1f);
        private static readonly int _SargassumCanopyShadowParamsId = Shader.PropertyToID("_SargassumCanopyShadowParams");
        private static readonly int _SargassumCanopyLightingParamsId = Shader.PropertyToID("_SargassumCanopyLightingParams");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        internal static bool TryPublishHudAverageLuminance(float luminance01)
        {
            if (ActiveRuntimeInstance == null)
                return false;

            ActiveRuntimeInstance._hudFogTargetLuminance01 = math.saturate(luminance01);
            return true;
        }

        private float _hudFogTargetLuminance01;
        private float _hudFogSmoothedLuminance01;
        private float _hudFogDownsampledLuminance01;
        private float _nextHudFogLuminanceReadbackTime;
        private RenderTexture _hudFogLuminanceTexture;
        private HudFogLuminanceReadbackOwner _hudFogLuminanceReadback;
        private int _hudFogLuminanceKernel = -1;
        private int _hudFogLuminanceThreadGroupSizeX;
        private int _hudFogLuminanceThreadGroupSizeY;
        private bool _hudFogLuminanceReady;
        private bool _hudFogReadbackPending;
        private bool _hudFogLuminanceReleasePending;
        private bool _hudFogLuminanceReadbackRepairRequested = true;
        private Action<AsyncGPUReadbackRequest> _hudFogLuminanceReadbackCompleted;
        private RenderTexture _photophobiaFieldTextureA;
        private RenderTexture _photophobiaFieldTextureB;
        private int _photophobiaFieldKernel = -1;
        private int _photophobiaFieldThreadGroupSizeX;
        private int _photophobiaFieldThreadGroupSizeY;
        private bool _photophobiaFieldReady;
        private bool _photophobiaFieldWriteToA = true;
        private bool _photophobiaFieldDirty;
        private float _photophobiaRecoverUntilUnscaledTime;
        private Vector4 _photophobiaFieldOriginScale;
        private float _gpuBubbleExhaleImpulse01;

        private struct HudFogLuminanceReadbackOwner
        {
            public NativeArray<float> Data;
        }
        private bool _supportsComputeShadersCold;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ REFERENCES
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â REFERENCES Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private LensFlareComponentSRP sunFlare;
        [SerializeField] private ComputeShader hudFogLuminanceCompute;
        [Tooltip("Optional HUD luminance GPU readback for diagnostics/high-end tuning. Off by default; HUD owners can publish luminance directly.")]
        [SerializeField] private bool enableHudFogLuminanceGpuReadback;
        [SerializeField] private ComputeShader photophobiaFieldCompute;

#if UNITY_EDITOR
        /// <summary>
        /// Resolves both compute kernels at AUTHOR time and persists the result.
        ///
        /// Both fields already had a lazy resolve from the same asset paths, but those live inside
        /// #if UNITY_EDITOR and assign the field in memory only, so the serialized value stayed null. The
        /// HUD fog luminance and photophobia field therefore worked in the editor and were dead in every
        /// player build, where the repairing #if block does not exist. Marking the object dirty here means
        /// the reference is SERIALIZED at author time, so it ships - and so a brand new scene works without
        /// anyone remembering to drag the assets into the Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            bool resolvedAny = false;

            if (hudFogLuminanceCompute == null)
            {
                hudFogLuminanceCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(HudFogLuminanceComputeAssetPath);
                resolvedAny |= hudFogLuminanceCompute != null;
            }

            if (photophobiaFieldCompute == null)
            {
                photophobiaFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(PhotophobiaFieldComputeAssetPath);
                resolvedAny |= photophobiaFieldCompute != null;
            }

            if (resolvedAny)
                EditorUtility.SetDirty(this);
        }
#endif

        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private DepthZoneDirector depthZoneDirector;
        [SerializeField] private LandingImpactVFX transitionCameraVfx;
        [SerializeField] private VisorHUDController transitionVisorController;
        [Tooltip("Near-camera suspended particulate system parented under the runtime main camera.")]
        [SerializeField] private ParticleSystem underwaterSuspendedMotes;
        [Tooltip("Optional GPU marine snow renderer. If operational, it supersedes the legacy particle-system motes path.")]
        [SerializeField] private HectonMarineSnowRenderer underwaterMarineSnow;
        [Tooltip("Burst-only exhale bubble system parented under the runtime main camera.")]
        [SerializeField] private ParticleSystem underwaterExhaleBubbles;
        [Tooltip("Attached light used only as a screen-space shaft source. Keep cullingMask = 0 to avoid lighting the world.")]
        [SerializeField] private Light shallowSunBeamLight;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SARGASSUM CANOPY Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [Tooltip("Allows underwater visuals to react to global sargassum density when the player dives under floating mats.")]
        [SerializeField] private bool enableSargassumCanopyLighting = true;
        [Tooltip("World-space radius used by the local canopy shadow blob pushed into first-party underwater terrain shaders.")]
        [SerializeField, UnityEngine.Range(4f, 48f)] private float sargassumCanopyShadowRadius = 24f;
        [Tooltip("How strongly floating sargassum density suppresses underwater light when the player is under a canopy.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyLightOcclusionStrength = 0.62f;
        [Tooltip("Extra fog density added under dense canopy coverage.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyFogBoost = 0.28f;
        [Tooltip("Ambient-light suppression under a dense canopy.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyAmbientOcclusionStrength = 0.48f;
        [Tooltip("How strongly the canopy suppresses shallow sun beams unless a local Voronoi window is open.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamOcclusionStrength = 0.72f;
        [Tooltip("How strongly Voronoi canopy windows re-open shallow god rays.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamWindowBoost = 0.94f;
        [Tooltip("How strongly the shallow god ray shifts laterally toward the current canopy-window anchor so the beam tracks drifting sargassum openings.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamAnchorTracking = 0.42f;
        [Tooltip("Maximum local beam offset applied when tracking a drifting canopy window.")]
        [SerializeField, UnityEngine.Range(0f, 6f)] private float sargassumCanopyBeamAnchorMaxOffset = 2.4f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â ATMOSPHERE MANAGER Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â OCEAN MATERIAL Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SKY MATERIAL Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private Material skyMaterial;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â BIOME PALETTE Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private HectonOceanPalette biomePalette;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â VERTICAL RUNTIME Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ GLOBAL DEPTH CURVE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â GLOBAL LIGHT CURVE Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [Tooltip("Ãâ€œÃâ€ºÃÂÃâ€™ÃÂÃÂÃÂ¯ ÃÅ¡ÃÂ ÃËœÃâ€™ÃÂÃÂ¯ Ãâ€”ÃÂÃÂ¢Ãâ€¢ÃÅ“ÃÂÃâ€¢ÃÂÃËœÃÂ¯.\n" +
                 "X = ÃÂ³ÃÂ»Ã‘Æ’ÃÂ±ÃÂ¸ÃÂ½ÃÂ° (ÃÂ¼), Y = ÃÂ¼ÃÂ½ÃÂ¾ÃÂ¶ÃÂ¸Ã‘â€šÃÂµÃÂ»Ã‘Å’ Ã‘ÂÃÂ²ÃÂµÃ‘â€šÃÂ° [0..1].")]
        [SerializeField] private AnimationCurve globalLightCurve = new AnimationCurve(
            new Keyframe(0f,    1.0f,  0f, 0f),
            new Keyframe(300f,  0.8f,  0f, 0f),
            new Keyframe(700f,  0.1f,  0f, 0f),
            new Keyframe(1000f, 0.0f,  0f, 0f)
        );

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â FOG DENSITY RANGE Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [Header("Beer-Lambert Depth Attenuation")]
        [Tooltip("Uses ocean adapter depth-fog coefficients as Beer-Lambert extinction instead of the legacy authored depth curve.")]
        [SerializeField] private bool useBeerLambertDepthAttenuation = true;
        [Tooltip("Keeps the upper water column readable before full extinction ramps in.")]
        [SerializeField, UnityEngine.Range(0f, 80f)] private float beerLambertSurfaceClarityDepth = 35f;
        [Tooltip("Global multiplier on extinction derived from ocean adapter _DepthFogDensity.")]
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float beerLambertExtinctionScale = 1f;
        [Tooltip("Treat deep water as effectively black once transmittance falls below this threshold.")]
        [SerializeField, UnityEngine.Range(0f, 0.05f)] private float beerLambertBlackoutThreshold = 0.0025f;
        [Tooltip("Depth gate for the deep-black clamp so the upper column stays readable.")]
        [SerializeField, UnityEngine.Range(100f, 1000f)] private float beerLambertBlackoutDepth = 450f;

        [Header("Fog Density Range")]
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float minFogDensity = 0.002f;

        [UnityEngine.Range(0.01f, 0.5f)]
        [SerializeField] private float maxFogDensity = 0.08f;

        [Header("Editor Scene View Preview")]
        [Tooltip("Scales Unity fog density for Scene View underwater preview so the editor does not stack full fog on top of ocean underwater rendering.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sceneViewUnderwaterFogDensityScale = 0.35f;

        [Header("Horizon Weld")]
        [Tooltip("Blends underwater fog back toward the sky-owned horizon color near the surface to avoid a hard seam.")]
        [SerializeField, UnityEngine.Range(0.5f, 40f)] private float surfaceFogBlendDepth = 16f;

        [Header("Surface Ocean Horizon Merge")]
        [Tooltip("How much the distant/grazing ocean is pulled toward the fog-owned horizon veil. Raise this when the waterline still reads as a hard cut.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonFogBlend = 0.78f;
        [Tooltip("How much the ocean base color is lifted toward the same atmospheric veil. Lower than grazing on purpose so near water keeps its body color.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanBaseFogBlend = 0.24f;
        [Tooltip("How much the sun-facing horizon water is allowed to inherit the sky sun-scatter tint instead of staying neutral fog.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanSunScatterBlend = 0.2f;
        [Tooltip("Extra luminance lift for grazing water near the horizon veil. Raise this when the horizon is still a dark strip under a bright sky.")]
        [SerializeField, UnityEngine.Range(0f, 2f)] private float surfaceOceanHorizonLuminanceLift = 0.7f;
        [Tooltip("How strongly distant ocean merge prefers sky/haze tint over neutral fog. Raise this when the far water keeps collapsing into gray instead of inheriting the horizon air color.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonSkyBias = 0.72f;
        [Tooltip("Preserves sky/haze color in the distant ocean merge after fog neutralization. Raise this when the horizon line softens but the far water still looks dead and gray.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonColorPreserve = 0.28f;
        [Tooltip("Controls how strongly the ocean adapter procedural sky base is glued to the fog/haze state instead of the authored fallback color.")]
        [FormerlySerializedAs("crestSkyBaseFogLink")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float oceanSkyBaseFogLink = 0.88f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  INSPECTOR Ã¢â‚¬â€ CONFIGURATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â WATER LEVEL Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        private const float DefaultWaterLevelFallback = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [SerializeField] private float waterLevelFallback = DefaultWaterLevelFallback;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â DEEP CELESTIAL CULL Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [Tooltip("Suppresses SpaceCamera celestial rendering below this depth to cut deep-water render overhead without touching shallow water.")]
        [SerializeField] private float deepCelestialCullDepth = 1000f;
        [Tooltip("Keeps SpaceCamera celestial rendering suppressed until the player climbs clearly out of the deep-water threshold instead of thrashing at one boundary.")]
        [SerializeField, UnityEngine.Range(0f, 300f)] private float deepCelestialCullDepthHysteresis = 120f;
        [Tooltip("Allows weak hardware to suppress the extra celestial camera earlier once dynamic resolution has already fallen and the player is no longer in shallow water.")]
        [SerializeField] private bool enableAdaptiveSpaceCameraCull = true;
        [Tooltip("Do not suppress the celestial camera from perf pressure in shallow water. This preserves near-surface sky readability.")]
        [SerializeField, UnityEngine.Range(0f, 1000f)] private float adaptiveSpaceCameraCullMinDepth = 350f;
        [Tooltip("Render-scale threshold that triggers earlier SpaceCamera suppression on weak hardware.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveSpaceCameraCullRenderScale = 0.76f;
        [Tooltip("Render-scale threshold required before the SpaceCamera is restored after adaptive suppression.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveSpaceCameraRestoreRenderScale = 0.9f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SUN VISUAL Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.005f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SUN SCATTERING Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private float baseSunSize = 0.0065f;
        [SerializeField] private float underwaterSunSizeMax = 0.15f;
        [SerializeField] private float baseSunEdgeSoftness = 0.0035f;
        [SerializeField] private float underwaterSunSoftnessMax = 0.5f;
        [SerializeField, UnityEngine.Range(0.5f, 20f)] private float sunStateBrightenSpeed = 4.5f;
        [SerializeField, UnityEngine.Range(0.5f, 20f)] private float sunStateDarkenSpeed = 8f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â TRANSITION Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [UnityEngine.Range(0.05f, 2.0f)]
        [SerializeField] private float biomeTransitionSpeed = 0.2f;
        [SerializeField] private float slowTickInterval = 0.5f;
        [Tooltip("AUP transition band used by biome fog blending when Matrix events do not provide authored biome edge points.")]
        [SerializeField, UnityEngine.Range(4f, 160f)] private float biomeFogTransitionLengthMeters = 48f;

        [Header("ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â SUBMERGE IMPULSE ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â")]
        [SerializeField, UnityEngine.Range(0f, 0.6f)] private float submergeDarkenStrength = 0.2f;
        [SerializeField, UnityEngine.Range(0f, 2f)] private float submergeFogBoost = 0.45f;
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float submergeImpulseDuration = 0.32f;
        [SerializeField, UnityEngine.Range(0.1f, 2f)] private float submergeImpulseDepthWindow = 0.9f;

        [Header("â”€â”€ Thermocline Transition â”€â”€â”€")]
        [Tooltip("Optional subtle one-shot used when the player punches through a sharp water-layer boundary.")]
        [SerializeField] private AudioClip thermoclineTransitionClip;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineMinTriggerIntensity = 0.18f;
        [SerializeField, UnityEngine.Range(0.25f, 20f)] private float thermoclineTemperatureDeltaForFullEffect = 6f;
        [SerializeField, UnityEngine.Range(0.02f, 0.5f)] private float thermoclineFogDeltaForFullEffect = 0.11f;
        [SerializeField, UnityEngine.Range(0.05f, 1.5f)] private float thermoclineColorDeltaForFullEffect = 0.32f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineAudioVolume = 0.26f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineVisorDistortionHoldDuration = 0.08f;
        [SerializeField, UnityEngine.Range(0.25f, 8f)] private float thermoclineVisorDistortionRecoverySpeed = 5.8f;
        [SerializeField, UnityEngine.Range(0.1f, 2f)] private float thermoclineMinRepeatInterval = 0.45f;

        [Header("ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â SHALLOW CAUSTICS ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â")]
        [SerializeField] private bool enableShallowCaustics = true;
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float causticsStrengthScale = 1f;
        [SerializeField, UnityEngine.Range(0.05f, 2f)] private float causticsFadeInDepth = 0.3f;
        [SerializeField, UnityEngine.Range(1f, 40f)] private float causticsFadeOutDepth = 18f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float causticsMinLightFactor = 0.18f;
        [Header("Noir Final Resolve")]
        [Tooltip("Non-linear blackout exponent applied to underwater fog. Pure #000 is forbidden because it destroys silhouette and color separation.")]
        [SerializeField, UnityEngine.Range(1f, 4f)] private float noirFogPower = 1.18f;
        [Tooltip("Blue-noise dither strength applied in the final underwater resolve pass.")]
        [SerializeField, UnityEngine.Range(0f, 2f)] private float underwaterFinalDitherStrength = 0.75f;
        [Tooltip("Absolute abyssal luminance floor. Pure black is forbidden because the frame must keep readable separation in the deep.")]
        [SerializeField] private Color abyssalBlackFloor = new Color(0.028f, 0.042f, 0.060f, 1f);
        [Tooltip("Meters between the water surface and the noir floor band used to normalize vertical fog density.")]
        [SerializeField, UnityEngine.Range(8f, 600f)] private float noirVerticalFogSpan = 180f;
        [Tooltip("Extra density injected at the abyssal floor. 1.5 = 2.5x denser than the surface layer.")]
        [SerializeField, UnityEngine.Range(0f, 4f)] private float abyssalDensityBoost = 0.42f;
        [Tooltip("How aggressively voxel-cave occlusion absorbs procedural caustics.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float biomeAbsorption = 0.9f;
        [Header("Noir Value Caustics")]
        [Tooltip("Absolute-universe scale of the primary procedural caustics layer.")]
        [SerializeField, UnityEngine.Range(0.02f, 1f)] private float noirCausticsLayerAScale = 0.18f;
        [Tooltip("Primary caustics world-scroll speed on X.")]
        [SerializeField, UnityEngine.Range(-0.5f, 0.5f)] private float noirCausticsLayerAScrollX = 0.07f;
        [Tooltip("Primary caustics world-scroll speed on Z.")]
        [SerializeField, UnityEngine.Range(-0.5f, 0.5f)] private float noirCausticsLayerAScrollZ = 0.05f;
        [Tooltip("Primary layer contribution before ocean shallow-depth gating.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float noirCausticsLayerAStrength = 0.55f;
        [Tooltip("Absolute-universe scale of the secondary procedural caustics layer.")]
        [SerializeField, UnityEngine.Range(0.02f, 1.5f)] private float noirCausticsLayerBScale = 0.41f;
        [Tooltip("Secondary caustics world-scroll speed on X.")]
        [SerializeField, UnityEngine.Range(-0.5f, 0.5f)] private float noirCausticsLayerBScrollX = -0.04f;
        [Tooltip("Secondary caustics world-scroll speed on Z.")]
        [SerializeField, UnityEngine.Range(-0.5f, 0.5f)] private float noirCausticsLayerBScrollZ = 0.08f;
        [Tooltip("Secondary layer contribution before ocean shallow-depth gating.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float noirCausticsLayerBStrength = 0.35f;
        [Tooltip("Higher values tighten the procedural caustics into thinner noir streaks.")]
        [SerializeField, UnityEngine.Range(1f, 8f)] private float noirCausticsSharpness = 3.4f;
        [Tooltip("Cross-layer distortion amount used to break up repeated noise lobes.")]
        [SerializeField, UnityEngine.Range(0f, 2f)] private float noirCausticsDistortion = 0.38f;
        [Tooltip("Depth at which the procedural caustics start fading out.")]
        [SerializeField, UnityEngine.Range(0.5f, 32f)] private float noirCausticsDepthFadeStart = 12f;
        [Tooltip("Fade span after the start depth. Larger values keep caustics alive deeper.")]
        [SerializeField, UnityEngine.Range(0.5f, 32f)] private float noirCausticsDepthFadeRange = 8f;

        [Header("Flashlight Photophobia Field")]
        [Tooltip("Compute-driven temporal dimming field sampled by first-party bioluminescent flora shaders.")]
        [SerializeField] private bool enableFlashlightPhotophobiaField = true;
        [Tooltip("World-space width in meters covered by the local photophobia texture.")]
        [SerializeField, UnityEngine.Range(32f, 192f)] private float flashlightPhotophobiaFieldExtent = 96f;
        [Tooltip("Seconds for flora emission to recover after leaving the flashlight cone.")]
        [SerializeField, UnityEngine.Range(0.25f, 4f)] private float flashlightPhotophobiaRecoverySeconds = 1f;
        [Tooltip("How hard the flashlight suppresses bioluminescent flora emission inside the cone.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float flashlightPhotophobiaStrength = 1f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬ UNDERWATER MOTES Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Enables camera-local suspended particulate while underwater.")]
        [SerializeField] private bool enableSuspendedMotes = true;
        [Tooltip("Base emission rate at clean shallow water.")]
        [SerializeField, UnityEngine.Range(0f, 64f)] private float suspendedMotesMinEmission = 6f;
        [Tooltip("Emission ceiling at deeper or dirtier water.")]
        [SerializeField, UnityEngine.Range(0f, 128f)] private float suspendedMotesMaxEmission = 24f;
        [Tooltip("Depth at which the particle field reaches full density.")]
        [SerializeField, UnityEngine.Range(0.25f, 40f)] private float suspendedMotesFullEmissionDepth = 10f;
        [Tooltip("Extra emission injected during the first moment of submerge.")]
        [SerializeField, UnityEngine.Range(0f, 32f)] private float suspendedMotesSubmergeBoost = 10f;
        [Tooltip("How strongly biome turbidity raises particulate density.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float suspendedMotesTurbidityWeight = 0.65f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬ BOTTOM SILT Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Boosts near-camera particulate when the player moves close to the seafloor.")]
        [SerializeField] private bool enableBottomSiltBoost = true;
        [Tooltip("Maximum interval between bottom probes while underwater.")]
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float bottomSiltProbeInterval = 0.18f;
        [Tooltip("No extra disturbed silt above this seafloor distance.")]
        [SerializeField, UnityEngine.Range(0.25f, 12f)] private float bottomSiltActivationDistance = 3.5f;
        [Tooltip("Full disturbed-silt response when this close to the seafloor.")]
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float bottomSiltFullDistance = 1.2f;
        [Tooltip("Minimum player speed before disturbed silt appears.")]
        [SerializeField, UnityEngine.Range(0f, 6f)] private float bottomSiltMinSpeed = 0.85f;
        [Tooltip("Player speed at which disturbed silt reaches full intensity.")]
        [SerializeField, UnityEngine.Range(0.25f, 12f)] private float bottomSiltFullSpeed = 3.4f;
        [Tooltip("Maximum extra emission injected into the existing suspended motes field near the seabed.")]
        [SerializeField, UnityEngine.Range(0f, 48f)] private float bottomSiltEmissionBoost = 14f;
        [Tooltip("How quickly one-shot external bottom-silt bursts decay back into the normal seabed response.")]
        [SerializeField, UnityEngine.Range(0.5f, 12f)] private float bottomSiltBurstRecoverySpeed = 4.5f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬ EXHALE BUBBLES Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Emits a short bubble burst on each underwater exhale event from the player movement owner.")]
        [SerializeField] private bool enableExhaleBubbles = true;
        [Tooltip("Minimum burst count in clean shallow water.")]
        [SerializeField, UnityEngine.Range(0, 32)] private int exhaleBubbleMinBurstCount = 7;
        [Tooltip("Burst count ceiling in deeper or murkier water.")]
        [SerializeField, UnityEngine.Range(1, 48)] private int exhaleBubbleMaxBurstCount = 14;
        [Tooltip("Depth at which exhale bubbles reach their full burst density.")]
        [SerializeField, UnityEngine.Range(0.5f, 40f)] private float exhaleBubbleFullDepth = 14f;
        [Tooltip("How strongly turbidity contributes to burst density.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float exhaleBubbleTurbidityWeight = 0.35f;
        [Tooltip("Protects against duplicate exhale events landing in the same short window.")]
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float exhaleBubbleMinInterval = 0.28f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬ SHALLOW SUN BEAM Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Enables the near-camera shallow-water sunshaft proxy.")]
        [SerializeField] private bool enableShallowSunBeam = true;
        [Tooltip("Beam fades in over this first shallow depth range.")]
        [SerializeField, UnityEngine.Range(0.05f, 4f)] private float shallowSunBeamFadeInDepth = 0.75f;
        [Tooltip("Beam is fully faded out by this depth.")]
        [SerializeField, UnityEngine.Range(2f, 40f)] private float shallowSunBeamFadeOutDepth = 16f;
        [Tooltip("Minimum underwater light factor required before the shaft appears.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float shallowSunBeamMinLightFactor = 0.32f;
        [Tooltip("Maximum light intensity pushed into the proxy beam light.")]
        [SerializeField, UnityEngine.Range(0f, 4f)] private float shallowSunBeamMaxLightIntensity = 0.55f;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬ ECOLOGY RESPONSE Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("How strongly fauna mood can thicken underwater suspended particulates.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologySuspendedMotesWeight = 0.28f;
        [Tooltip("How strongly fauna mood can increase exhale bubble burst density.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologyBubbleWeight = 0.18f;
        [Tooltip("How strongly calm/lively fauna space keeps shallow beams readable before deeper fade takes over.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologySunBeamWeight = 0.16f;
        [Header("Adaptive Budget Response")]
        [Tooltip("Scales underwater near-camera dressing from DynamicResolutionScaler render scale so weak devices shed expensive dressing before the frame collapses.")]
        [SerializeField] private bool enableAdaptiveBudgetResponse = true;
        [Tooltip("Render scale at which adaptive dressing reaches its minimum authored budget response.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveBudgetFloorRenderScale = 0.7f;
        [Tooltip("Minimum suspended motes density allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveMotesBudgetFloor = 0.55f;
        [Tooltip("Minimum exhale bubble density allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveBubbleBudgetFloor = 0.6f;
        [Tooltip("Minimum shallow sun-beam intensity allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveBeamBudgetFloor = 0.7f;
        [Tooltip("Minimum shallow caustics intensity allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveCausticsBudgetFloor = 0.72f;
        [Tooltip("Maximum bottom-silt probe interval multiplier at the adaptive budget floor. Higher values reduce probe cadence on weak frames.")]
        [SerializeField, UnityEngine.Range(1f, 4f)] private float adaptiveBottomSiltProbeIntervalMultiplier = 1.8f;

        [Header("Soundscape Tier Response")]
        // Depth-band response stays inside the underwater visual owner instead of a fake global audio owner.
        [Tooltip("Applies authored soundscape depth tiers to underwater fog, ambient, beam, and caustics so each depth band reads like a different water mass.")]
        [SerializeField] private bool enableSoundscapeTierResponse = true;
        [Tooltip("Ambient tint injected only in thermal tier so the abyss stops reading as flat blue-black.")]
        [SerializeField] private Color thermalTierTintColor = new Color(0.22f, 0.1f, 0.03f, 1f);
        [Tooltip("Thermal tier tint blend amount applied to fog and ambient colors.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermalTierTintBlend = 0.28f;
        [Tooltip("Fog density multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float twilightTierFogScale = 1.08f;
        [Tooltip("Fog density multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float darknessTierFogScale = 1.18f;
        [Tooltip("Fog density multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float abyssTierFogScale = 1.32f;
        [Tooltip("Fog density multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float deepAbyssTierFogScale = 1.48f;
        [Tooltip("Fog density multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float thermalTierFogScale = 1.3f;
        [Tooltip("Ambient intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float twilightTierAmbientScale = 0.94f;
        [Tooltip("Ambient intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float darknessTierAmbientScale = 0.82f;
        [Tooltip("Ambient intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float abyssTierAmbientScale = 0.72f;
        [Tooltip("Ambient intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float deepAbyssTierAmbientScale = 0.62f;
        [Tooltip("Ambient intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float thermalTierAmbientScale = 0.78f;
        [Tooltip("Shallow sun beam intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float twilightTierBeamScale = 0.88f;
        [Tooltip("Shallow sun beam intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float darknessTierBeamScale = 0.6f;
        [Tooltip("Shallow sun beam intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float abyssTierBeamScale = 0.32f;
        [Tooltip("Shallow sun beam intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float deepAbyssTierBeamScale = 0.14f;
        [Tooltip("Shallow sun beam intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float thermalTierBeamScale = 0.16f;
        [Tooltip("Caustics intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float twilightTierCausticsScale = 0.92f;
        [Tooltip("Caustics intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float darknessTierCausticsScale = 0.58f;
        [Tooltip("Caustics intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float abyssTierCausticsScale = 0.28f;
        [Tooltip("Caustics intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float deepAbyssTierCausticsScale = 0.1f;
        [Tooltip("Caustics intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float thermalTierCausticsScale = 0.14f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SURFACE DEFAULTS Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceFogColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private float surfaceFogDensity = 0.001f;
        [SerializeField] private bool enableSurfaceFog = false;
        [ColorUsage(false)]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â UNDERWATER AMBIENT Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [ColorUsage(false)]
        [SerializeField] private Color underwaterAmbientColor = new Color(0.02f, 0.04f, 0.06f, 1f);

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  DIAGNOSTICS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â DIAGNOSTICS Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
#pragma warning disable CS0414
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugLightFactor;
        [SerializeField] private float _debugFogDensity;
        [SerializeField] private float _debugTurbidity;
        [SerializeField] private float _debugAtmoSunIntensity;
        [SerializeField] private float _debugHorizonFade;
        [SerializeField] private float _debugFinalSunIntensity;
        [SerializeField] private int   _debugTargetBiome;
        [SerializeField] private float _debugTransitionProgress;
        [SerializeField] private bool  _debugIsUnderwater;
        [SerializeField] private float _debugCausticsStrength;
        [SerializeField] private float _debugSuspendedMotesEmission;
        [SerializeField] private float _debugBottomDistance;
        [SerializeField] private float _debugBottomSiltBoost;
        [SerializeField] private int   _debugExhaleBubbleBurstCount;
        [SerializeField] private float _debugShallowSunBeamIntensity;
        [SerializeField] private string _debugFaunaMood = "None";
        [SerializeField] private string _debugFaunaAmbience = "None";
        [SerializeField] private float _debugEcologyMotesMultiplier = 1f;
        [SerializeField] private float _debugEcologyBubbleMultiplier = 1f;
        [SerializeField] private float _debugEcologyBeamMultiplier = 1f;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveMotesScale = 1f;
        [SerializeField] private float _debugAdaptiveBubbleScale = 1f;
        [SerializeField] private float _debugAdaptiveBeamScale = 1f;
        [SerializeField] private float _debugAdaptiveCausticsScale = 1f;
        [SerializeField] private float _debugAdaptiveBottomProbeScale = 1f;
        [SerializeField] private string _debugSoundscapeTier = "Shallow";
        [SerializeField] private float _debugSoundscapeFogScale = 1f;
        [SerializeField] private float _debugSoundscapeAmbientScale = 1f;
        [SerializeField] private float _debugSoundscapeBeamScale = 1f;
        [SerializeField] private float _debugSoundscapeCausticsScale = 1f;
        [SerializeField] private bool  _debugPhysicsEngineFound;
        [SerializeField] private bool  _debugAtmoManagerFound;
        [SerializeField] private bool  _debugPlayerMovementFound;
        [SerializeField] private string _debugPlayerMovementSource = "Unresolved";
        [SerializeField] private bool  _debugSunVisualActive;
        [SerializeField] private float _debugSunScatter;
        [SerializeField] private bool  _debugEditorDriven;
#pragma warning restore CS0414

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SHADER PROPERTY IDs
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private static readonly int _ID_ScatterColourBase =
            Shader.PropertyToID("_ScatterColourBase");
        private static readonly int _ID_ScatterColourShallow =
            Shader.PropertyToID("_ScatterColourShallow");
        private static readonly int _ID_DepthFogDensity =
            Shader.PropertyToID("_DepthFogDensity");
        private static readonly int _ID_Diffuse =
            Shader.PropertyToID("_Diffuse");
        private static readonly int _ID_DiffuseGrazing =
            Shader.PropertyToID("_DiffuseGrazing");
        private static readonly int _ID_DiffuseShadow =
            Shader.PropertyToID("_DiffuseShadow");
        private static readonly int _ID_SubSurfaceColour =
            Shader.PropertyToID("_SubSurfaceColour");
        private static readonly int _ID_SubSurfaceShallowCol =
            Shader.PropertyToID("_SubSurfaceShallowCol");
        private static readonly int _ID_SubSurfaceShallowColShadow =
            Shader.PropertyToID("_SubSurfaceShallowColShadow");
        private static readonly int _ID_SubSurfaceBase =
            Shader.PropertyToID("_SubSurfaceBase");
        private static readonly int _ID_SubSurfaceSun =
            Shader.PropertyToID("_SubSurfaceSun");
        private static readonly int _ID_SubSurfaceSunFallOff =
            Shader.PropertyToID("_SubSurfaceSunFallOff");
        private static readonly int _ID_SubSurfaceDepthMax =
            Shader.PropertyToID("_SubSurfaceDepthMax");
        private static readonly int _ID_SubSurfaceDepthPower =
            Shader.PropertyToID("_SubSurfaceDepthPower");
        private static readonly int _ID_SubSurfaceScattering =
            Shader.PropertyToID("_SubSurfaceScattering");
        private static readonly int _ID_SubSurfaceShallowColour =
            Shader.PropertyToID("_SubSurfaceShallowColour");
        private static readonly int _ID_Underwater =
            Shader.PropertyToID("_Underwater");
        private static readonly int _ID_CullMode =
            Shader.PropertyToID("_CullMode");
        private static readonly int _ID_Caustics =
            Shader.PropertyToID("_Caustics");
        private static readonly int _ID_CausticsStrength =
            Shader.PropertyToID("_CausticsStrength");
        private static readonly int _HectonNoirResolveSettingsId =
            Shader.PropertyToID("_HectonNoirResolveSettings");
        private static readonly int _HectonNoirAbyssFloorId =
            Shader.PropertyToID("_HectonNoirAbyssFloor");
        private static readonly int _HectonNoirFogStratificationId =
            Shader.PropertyToID("_HectonNoirFogStratification");
        private static readonly int _HectonNoirDitherParamsId =
            Shader.PropertyToID("_HectonNoirDitherParams");
        private static readonly int _HectonNoirCausticsLayerAId =
            Shader.PropertyToID("_HectonNoirCausticsLayerA");
        private static readonly int _HectonNoirCausticsLayerBId =
            Shader.PropertyToID("_HectonNoirCausticsLayerB");
        private static readonly int _HectonNoirCausticsShapeId =
            Shader.PropertyToID("_HectonNoirCausticsShape");
        private static readonly int _HectonNoirCaveAttenuationId =
            Shader.PropertyToID("_HectonNoirCaveAttenuation");
        private static readonly int _HectonHudFogPerturbationId =
            Shader.PropertyToID("_HectonHudFogPerturbation");
        private static readonly int _FogScatteringCoeffId =
            Shader.PropertyToID("_FogScatteringCoeff");
        private static readonly int _HectonHudFogSourceId =
            Shader.PropertyToID("_HectonHudFogSource");
        private static readonly int _HectonHudFogLuminanceOutputId =
            Shader.PropertyToID("_HectonHudFogLuminanceOutput");
        private static readonly int _HectonHudFogLuminanceParamsId =
            Shader.PropertyToID("_HectonHudFogLuminanceParams");
        private static readonly int _HectonFlowSynchronyParamsId =
            Shader.PropertyToID("_HectonFlowSynchronyParams");
        private static readonly int _HectonSuitHealthGlitchId =
            Shader.PropertyToID("_HectonSuitHealthGlitch");
        private static readonly int _HectonPhotophobiaFieldTexId =
            Shader.PropertyToID("_HectonPhotophobiaFieldTex");
        private static readonly int _HectonPhotophobiaFieldOriginScaleId =
            Shader.PropertyToID("_HectonPhotophobiaFieldOriginScale");
        private static readonly int _HectonPhotophobiaFieldStateId =
            Shader.PropertyToID("_HectonPhotophobiaFieldState");
        private static readonly int _HectonPhotophobiaSourceTexId =
            Shader.PropertyToID("_HectonPhotophobiaSourceTex");
        private static readonly int _HectonPhotophobiaTargetTexId =
            Shader.PropertyToID("_HectonPhotophobiaTargetTex");
        private static readonly int _HectonPhotophobiaParamsId =
            Shader.PropertyToID("_HectonPhotophobiaParams");
        private static readonly int _HectonPhotophobiaCone0Id =
            Shader.PropertyToID("_HectonPhotophobiaCone0");
        private static readonly int _HectonPhotophobiaCone1Id =
            Shader.PropertyToID("_HectonPhotophobiaCone1");
        private static readonly int _HectonPhotophobiaCone2Id =
            Shader.PropertyToID("_HectonPhotophobiaCone2");
        private static readonly int _HectonFlashlightPositionWSId =
            Shader.PropertyToID("_HectonFlashlightPositionWS");
        private static readonly int _HectonFlashlightDirectionWSId =
            Shader.PropertyToID("_HectonFlashlightDirectionWS");
        private static readonly int _HectonFlashlightColorId =
            Shader.PropertyToID("_HectonFlashlightColor");
        private static readonly int _HectonFlashlightConeDataId =
            Shader.PropertyToID("_HectonFlashlightConeData");
        private static readonly int _HectonFlashlightActiveId =
            Shader.PropertyToID("_HectonFlashlightActive");
        private static readonly int _HectonWaterSurfaceEmissionId =
            Shader.PropertyToID("_HectonWaterSurfaceEmission");
        private static readonly int _HectonUnderwaterSurfaceColorId =
            Shader.PropertyToID("_HectonUnderwaterSurfaceColor");
        private static readonly int _ID_SunSize =
            Shader.PropertyToID("_SunSize");
        private static readonly int _ID_SunEdgeSoftness =
            Shader.PropertyToID("_SunEdgeSoftness");
        private static readonly int _ID_SunDiscColor =
            Shader.PropertyToID("_SunDiscColor");
        private static readonly int _ID_SunScatterColor =
            Shader.PropertyToID("_SunScatterColor");
        private static readonly int _ID_SkyColorZenith =
            Shader.PropertyToID("_SkyColorZenith");
        private static readonly int _ID_SkyColorHorizon =
            Shader.PropertyToID("_SkyColorHorizon");
        private static readonly int _ID_SkyBase =
            Shader.PropertyToID("_SkyBase");
        private static readonly int _ID_SkyTowardsSun =
            Shader.PropertyToID("_SkyTowardsSun");
        private static readonly int _ID_SkyAwayFromSun =
            Shader.PropertyToID("_SkyAwayFromSun");
        private static readonly int _ID_SkyDirectionality =
            Shader.PropertyToID("_SkyDirectionality");
        private static readonly int _ID_ProceduralSky =
            Shader.PropertyToID("_ProceduralSky");

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  CONSTANTS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private static readonly Color MIN_AMBIENT = new Color(0.01f, 0.02f, 0.03f, 1f);
        private static readonly Color SurfaceReadableSkyAmbientFloor = new Color(0.300f, 0.380f, 0.420f, 1f);
        private static readonly Color SurfaceReadableEquatorAmbientFloor = new Color(0.280f, 0.360f, 0.400f, 1f);
        private static readonly Color SurfaceReadableGroundAmbientFloor = new Color(0.220f, 0.280f, 0.300f, 1f);
        private const string ProceduralSkyKeyword = "_PROCEDURALSKY_ON";
        private const string UnderwaterKeyword = "_UNDERWATER_ON";
        private const float SurfaceScatterLuminanceFloor = 0.50f;
        private const float SurfaceOceanBaseFloorMin = 0.50f;
        private const float SurfaceOceanBaseFloorLightSpan = 0.24f;
        private const float SurfaceOceanBaseHorizonLiftScale = 0.38f;
        private const float SurfaceOceanBaseLuminanceFloor = 0.52f;
        private const float SurfaceOceanShallowLuminanceFloor = 0.58f;
        private const float SurfaceOceanShadowLuminanceFloor = 0.46f;
        private const float SurfaceOceanShallowShadowLuminanceFloor = 0.52f;
        private static readonly Color SurfaceOceanDaylightReadableTint = new Color(0.500f, 0.660f, 0.760f, 1f);
        private const float SurfaceOceanBaseDaylightBlueBias = 0.10f;
        private const float SurfaceOceanShallowDaylightBlueBias = 0.08f;
        private const float SurfaceOceanShadowDaylightBlueBias = 0.06f;
        private const float SurfaceOceanShallowShadowDaylightBlueBias = 0.05f;
        private const float SurfaceOceanLuminanceFloorBlend = 0.86f;
        private const float SurfaceOceanShadowLuminanceFloorBlend = 0.72f;
        private const float SurfaceOceanDiffuseShadowBlackBlend = 0.16f;
        private const float SurfaceOceanShallowShadowBaseBlend = 0.14f;
        private const float UnderwaterScatterLuminanceFloor = 0.14f;
        private const float SharedOceanUnderwaterScatterLuminanceFloor = 0.64f;
        private const float SurfaceReadableSunIntensityFloor = 1.05f;
        private const float SurfaceReadableAmbientIntensityFloor = 1.34f;
        private const float SurfaceReadableFogDensityCeiling = 0.001f;
        private const float SurfaceReadableOceanDepthFogCeiling = 0.05f;
        private const float SurfaceFogReadableLuminanceFloor = 0.58f;
        private const float SurfaceHorizonReadableLuminanceFloor = 0.62f;
        private const float SurfaceSkyReadableLuminanceFloor = 0.56f;
        private const float SurfaceFogDaylightBlueBias = 0.24f;
        private const float SurfaceHorizonDaylightBlueBias = 0.18f;
        private const float SurfaceSkyDaylightBlueBias = 0.10f;
        private const float OceanSkyDirectionality = 0.78f;
        private const float GIRelaySurfaceEmissionEpsilon = 0.0005f;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  RUNTIME STATE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private IFluidSurfaceCurrentReadModel _physicsEngine;
        private IFluidBubbleBurstSink _fluidBubbleBurstSink;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private IHectonOceanKinematics _oceanKinematicsProvider;
        private bool _physicsEngineCached;
        private bool _physicsEngineLookupAttempted;
        private static IOceanVisualBridge s_oceanVisualBridge;

        private HectonAtmosphereManager _cachedAtmoManager;
        private bool _atmoManagerCached;
        private bool _atmoManagerLookupAttempted;
        private IAudioService _audioRuntime;
        private DynamicResolutionScaler _dynamicResolutionRuntime;
        private IWeatherService _weatherRuntime;
        private ISurfaceWeatherReadModel _surfaceWeatherRuntime;
        private IGIRelaySystem _giRelayRuntime;
        private SargassumGlobalDragManager _sargassumDragRuntime;
        private SoundscapeSystem _soundscapeRuntime;
        private MapMagicBridge _mapMagicRuntime;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerMovement _subscribedPlayerMovement;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private HectonBiomeProfile _matrixRuntimeVisualProfile;
        private HectonBiomeMatrixProfile _activeMatrixFogProfile;
        private HectonBiomeProfile _biomeFogFromProfile;
        private HectonBiomeProfile _biomeFogToProfile;
        private byte _biomeFogFromId;
        private byte _biomeFogToId;
        private float _biomeFogFallbackBlend01 = 1f;
        private AbsoluteUniversePositionBlit128 _biomeFogTransitionFromAup;
        private AbsoluteUniversePositionBlit128 _biomeFogTransitionToAup;
        private bool _biomeFogTransitionActive;
        private WorldProceduralFaunaMood _currentFaunaMood;
        private string _currentFaunaAmbienceSummary;
        private float _ecologySuspendedMotesMultiplier = 1f;
        private float _ecologyBubbleMultiplier = 1f;
        private float _ecologySunBeamMultiplier = 1f;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveMotesScale = 1f;
        private float _adaptiveBubbleScale = 1f;
        private float _adaptiveBeamScale = 1f;
        private float _adaptiveCausticsScale = 1f;
        private float _adaptiveBottomSiltProbeIntervalScale = 1f;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _soundscapeFogDensityScale = 1f;
        private float _soundscapeAmbientScale = 1f;
        private float _soundscapeBeamScale = 1f;
        private float _soundscapeCausticsScale = 1f;
        private float _soundscapeThermalTintBlend = 0f;

        private int _targetBiomeIndex;

        private Color   _currentScatterBase;
        private Color   _currentScatterShallow;
        private Vector3 _currentDepthFogDensity;
        private Color   _currentFogColor;
        private float   _currentTurbidity;
        private float   _currentBiomeFogDensityScale = 1f;
        private Color   _currentAmbientColor;

        private Color   _targetScatterBase;
        private Color   _targetScatterShallow;
        private Vector3 _targetDepthFogDensity;
        private Color   _targetFogColor;
        private float   _targetTurbidity;
        private float   _targetBiomeFogDensityScale = 1f;
        private float   _targetBiomeAbsorption = 0.9f;
        private Color   _targetAmbientColor;

        private float _transitionProgress;
        private float _cachedFogDensity;
        private Color _cachedUnderwaterFogColor = new Color(0.06f, 0.18f, 0.28f, 1f);

        private float _baseFlareIntensity;
        private bool  _baseValuesCaptured;

        private Color _baseSunDiscColor;
        private Color _baseSunScatterColor;
        private bool  _baseSkyColorsCaptured;
        private bool  _surfaceWeatherOverrideActive;
        private Color _surfaceWeatherFogColor;
        private float _surfaceWeatherFogDensity;
        private Color _surfaceWeatherAmbientColor;
        private float _surfaceWeatherSunMultiplier = 1f;
        private Color _giRelaySurfaceEmissionColor;
        private bool _giRelaySurfaceEmissionActive;

        private bool _registeredRenderable;
        private bool _registeredColdTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwapListener;
        private bool _runtimeVisualCallbacksActive;
        private bool _renderSettingsGuardAcquired;
        private bool _pendingVisualTickDirty;
        private bool _pendingOceanMaterialBindingDirty;
        private float _pendingVisualTickDeltaTime;
        private bool _wasUnderwater;
        private DepthZoneProfile _lastDepthZoneProfile;
        private float _submergeImpulseTimer;
        private float _nextThermoclineAllowedTime = float.NegativeInfinity;
        private float _cachedVisualDepth;
        private float _cachedLightFactor;
        private float _cachedCausticsStrength;
        private float _weatherStormFlowBlend;
        private float _sharedFlowSynchronyPhaseTime;
        private Color _stormFogDriftColor = StormFogDriftDeepBlue;
        private float _smoothedSunLightFactor = -1f;
        private float _smoothedSunIntensity = -1f;
        private float _cachedSuspendedMotesEmission = -1f;
        private int _cachedSuspendedMotesParticleCap;
        private float _cachedSuspendedMotesQualityWeight = -1f;
        private float _cachedBottomDistance = float.PositiveInfinity;
        private float _cachedBottomSiltBoost;
        private float _externalBottomSiltBurstBoost;
        private float _cachedShallowSunBeamLightIntensity = -1f;
        private bool _cachedVisualIsUnderwater;
        private bool _underwaterSuspendedMotesPlaying;
        private bool _shallowSunBeamActive;
        private bool _editorGameplayMainCameraSuppressed;
        private bool _editorGameplaySpaceCameraSuppressed;
        private bool _sunVisualWasDisabled;
        private bool _editorOceanPassSuppressed;
        private bool _spaceCameraSuppressed;
        private bool _spaceCameraMaskCaptured;
        private bool _runtimeServiceResolveRequested = true;
        private bool _runtimeVisualOwnerResolveRequested = true;
        private float _nextBottomSiltProbeTime = float.NegativeInfinity;
        private float _nextExhaleBubbleAllowedTime = float.NegativeInfinity;
        private float _nextRuntimePlayerCameraResolveTime = float.NegativeInfinity;
        private float _nextRuntimeMainCameraResolveTime = float.NegativeInfinity;
        private float _nextRuntimeSpaceCameraResolveTime = float.NegativeInfinity;
        private float _nextSecondaryUnderwaterPassPurgeTime = float.NegativeInfinity;
        private float _nextRuntimeReferenceWarningTime = float.NegativeInfinity;
        private byte _runtimeReferenceWarningMask;
        private const byte RuntimeReferenceWarningPlayerCamera = 1 << 0;
        private const byte RuntimeReferenceWarningMainCamera = 1 << 1;
        private const byte RuntimeReferenceWarningSunVisual = 1 << 2;
        private float _nextEditorCameraResolveTime = float.NegativeInfinity;
        private const int RuntimeCameraBufferSize = 8;
        private static readonly Camera[] _runtimeCameraBuffer = new Camera[RuntimeCameraBufferSize]; // COLD ALLOC: Camera[8] Ã¢â‚¬â€ reusable runtime main-camera resolve buffer to avoid hierarchy array allocations Ã¢â‚¬â€ owner: HectonUnderwaterVisuals
        private Camera _gameplayMainCamera;
        private Camera _spaceCamera;
        private Camera _underwaterMarineSnowSearchCamera;
        private Camera _underwaterSuspendedMotesSearchCamera;
        private Camera _underwaterExhaleBubblesSearchCamera;
        private Camera _transitionCameraVfxSearchCamera;
        private Camera _secondaryUnderwaterPassPurgeMainCamera;
        private Camera _secondaryUnderwaterPassPurgeSpaceCamera;
        private Camera _capturedCompositionMainCamera;
        private Camera _capturedCompositionSpaceCamera;
        private Camera _shallowSunBeamSearchCamera;
        private Camera _playerCameraComponent;
        private Transform _underwaterSuspendedMotesSearchTransform;
        private Transform _underwaterExhaleBubblesSearchTransform;
        private Transform _transitionVisorSearchRoot;
        private Transform _transitionVisorSearchTransform;
        private Transform _shallowSunBeamTransform;
        private Transform _shallowSunBeamLightSearchTransform;
        private Transform _cachedPlayerCameraTransform;
        private Camera _cachedPlayerCameraComponent;
        private Light _sunVisualSearchLight;
        private Vector3 _shallowSunBeamBaseLocalPosition;
        private bool _underwaterSuspendedMotesSearchCompleted;
        private bool _underwaterExhaleBubblesSearchCompleted;
        private bool _transitionCameraVfxSearchCompleted;
        private bool _transitionVisorSearchCompleted;
        private bool _shallowSunBeamLightSearchCompleted;
        private bool _sunVisualSearchCompleted;
        private Component _mainCameraUnderwaterPass;
        private Component _spaceCameraUnderwaterPass;
        private Component _secondaryUnderwaterPassPurgeMainPass;
        private Component _secondaryUnderwaterPassPurgeSpacePass;
        private Camera _cachedMainCameraDataCamera;
        private Camera _cachedSpaceCameraDataCamera;
        private UniversalAdditionalCameraData _cachedMainCameraData;
        private UniversalAdditionalCameraData _cachedSpaceCameraData;
        private bool _cachedMainCameraDataMissing;
        private bool _cachedSpaceCameraDataMissing;
        private bool _cameraCompositionDefaultsCaptured;
        private bool _runtimeCameraStackFallbackActive;
        private int _spaceCameraOriginalCullingMask;
        private int _mainCameraOriginalCullingMask;
        private float _mainCameraOriginalDepth;
        private float _spaceCameraOriginalDepth;
        private CameraClearFlags _mainCameraOriginalClearFlags;
        private CameraRenderType _mainCameraOriginalRenderType;
        private CameraRenderType _spaceCameraOriginalRenderType;
        private const int CelestialLayerIndex = 15;
        private const int _CelestialLayerMask = 1 << CelestialLayerIndex;
#if UNITY_EDITOR
        private Component _editorOceanUnderwaterPass;
        private Component _editorSceneViewUnderwaterPass;
        private bool _editorOceanUnderwaterPassWasEnabled;
        private Camera _editorGameplaySpaceCamera;
        private bool _editorGameplayMainCameraWasEnabled;
        private bool _editorGameplaySpaceCameraWasEnabled;
#endif

        private float _editorSlowTickAccum;
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  LIFECYCLE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void Awake()
        {
            _runtimeVisualCallbacksActive = Application.isPlaying;
            _hudFogLuminanceReadbackCompleted = HandleHudFogLuminanceReadbackCompleted;
            CacheGraphicsCapabilitiesCold();
            CacheRuntimeSkyMaterialReference();
            ForceMandatedSkyboxOwnership();
        }

        private void OnEnable()
        {
            _runtimeVisualCallbacksActive = Application.isPlaying;
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (!_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Acquire(this);
                _renderSettingsGuardAcquired = true;
            }

            if (_runtimeVisualCallbacksActive)
            {
                ActiveRuntimeInstance = this;
                CacheGraphicsCapabilitiesCold();
                CacheRuntimeDependencies();
                TryRegisterHotSwapListener();
                _debugEditorDriven = false;
                if (mainCamera != null && !IsRuntimeMainCamera(mainCamera))
                    mainCamera = null;
            }

            ResolvePlayerCamera();
            ResolveMainCamera();
            ResolveTransitionVisorController();
            ResolveUnderwaterParticles();
            ResolveUnderwaterMarineSnow();
            ResolveUnderwaterExhaleBubbles();
            ResolveShallowSunBeam();
            ResolveSpaceCamera();
            ValidateReferences();
            CachePhysicsEngine();
            CacheAtmosphereManager();
            CaptureBaseValues();
            CacheRuntimeSkyMaterialReference();
            ForceMandatedSkyboxOwnership();
            CaptureSkyBaseColors();
            InitializeCurrentValues();
            EnsureOceanUnderwaterPassOwnership();
            ApplyOceanMaterialBindings();
            ApplyNoirResolveGlobals();
            ApplyInitialSurfaceDefaultsIfAboveWater();

            if (_runtimeVisualCallbacksActive)
            {
                _debugEditorDriven = false;
                TryRegisterRenderDispatcher();
                EnsureRuntimeVisualOwners();
                EnsureGameplayCameraStackEnabled();
                if (enableHudFogLuminanceGpuReadback)
                    EnsureHudFogLuminanceResources(allowAllocate: true);
                EnsurePhotophobiaFieldResources(allowAllocate: true);
                MapMagicBiomeEvents.Register(this);
                BiomeMatrixEvents.Register(this);
                SoundscapeEvents.Register(this);
                ResolveBiomeMatrixDirector();
                ApplyCurrentMatrixVisualOverride();
                TryRegisterTickManagers();
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorUpdate;
                EditorApplication.update += EditorUpdate;
            }
#endif

            _wasUnderwater = false;
            _lastDepthZoneProfile = null;
            _nextThermoclineAllowedTime = float.NegativeInfinity;
            _sunVisualWasDisabled = false;
        }

        private void ApplyInitialSurfaceDefaultsIfAboveWater()
        {
            float cameraDepth = ResolveActiveVisualCameraDepth();
            if (ResolveUnderwaterVisualStateForCameraDepth(cameraDepth, cameraDepth))
                return;

            _cachedLightFactor = 1f;
            _cachedCausticsStrength = 0f;
            _cachedVisualDepth = 0f;
            _cachedVisualIsUnderwater = false;
            ApplySurfaceDefaults();
            ApplySurfaceReadableRenderSettingsFloor();
        }

        private void EnsureGameplayCameraStackEnabled()
        {
            if (mainCamera != null && !mainCamera.enabled)
                mainCamera.enabled = true;

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera != null && !spaceCamera.enabled)
                spaceCamera.enabled = true;

            EnsureCameraTextureRequirementsCached(
                mainCamera,
                ref _cachedMainCameraDataCamera,
                ref _cachedMainCameraData,
                ref _cachedMainCameraDataMissing);
            EnsureCameraTextureRequirementsCached(
                spaceCamera,
                ref _cachedSpaceCameraDataCamera,
                ref _cachedSpaceCameraData,
                ref _cachedSpaceCameraDataMissing);
            ApplyGameplayCameraCompositionMode();
            EnsureOceanUnderwaterPassOwnership();
        }

        private void ApplyCachedCameraAndOceanPresentation()
        {
            bool hasMainCameraData = TryReadCameraDataCached(
                mainCamera,
                ref _cachedMainCameraDataCamera,
                ref _cachedMainCameraData,
                ref _cachedMainCameraDataMissing,
                out UniversalAdditionalCameraData mainCameraData);

            if (!IsCameraReferenceValid(mainCamera) ||
                _mainCameraUnderwaterPass == null ||
                !hasMainCameraData)
            {
                _runtimeVisualOwnerResolveRequested = true;
                return;
            }

            if (!mainCamera.enabled)
                mainCamera.enabled = true;

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            UniversalAdditionalCameraData spaceCameraData = null;
            if (spaceCamera != null)
            {
                if (!spaceCamera.enabled)
                    spaceCamera.enabled = true;

                if (!TryReadCameraDataCached(
                        spaceCamera,
                        ref _cachedSpaceCameraDataCamera,
                        ref _cachedSpaceCameraData,
                        ref _cachedSpaceCameraDataMissing,
                        out spaceCameraData))
                {
                    _runtimeVisualOwnerResolveRequested = true;
                }
            }

            EnsureCameraTextureRequirements(mainCameraData, mainCamera);
            if (spaceCamera != null && spaceCameraData != null)
                EnsureCameraTextureRequirements(spaceCameraData, spaceCamera);

            ApplyCachedGameplayCameraCompositionMode(spaceCamera, mainCameraData, spaceCameraData);

            if (!IsUnderwaterPassEnabled(_mainCameraUnderwaterPass))
                SetUnderwaterPassEnabled(_mainCameraUnderwaterPass, true);

            SetCopyOceanMaterialParamsEachFrame(_mainCameraUnderwaterPass, true);

            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge != null && !bridge.IsOceanCameraOwnedBy(mainCamera))
                bridge.AssignOceanCamera(mainCamera);
        }

        private void ApplyCachedGameplayCameraCompositionMode(
            Camera spaceCamera,
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData)
        {
            if (spaceCamera == null || mainCameraData == null || spaceCameraData == null)
                return;

            CaptureGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);

            if (SupportsGameplayCameraStacking(mainCameraData, spaceCameraData))
            {
                RestoreGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);
                return;
            }

            ApplyGameplayCameraCompositionFallback(mainCameraData, spaceCameraData, spaceCamera);
        }

        private void ApplyGameplayCameraCompositionMode()
        {
            if (!_runtimeVisualCallbacksActive || mainCamera == null)
                return;

            ResolveSpaceCamera();
            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera == null)
                return;

            if (!TryResolveCameraDataCold(
                    mainCamera,
                    ref _cachedMainCameraDataCamera,
                    ref _cachedMainCameraData,
                    ref _cachedMainCameraDataMissing,
                    out UniversalAdditionalCameraData mainCameraData) ||
                !TryResolveCameraDataCold(
                    spaceCamera,
                    ref _cachedSpaceCameraDataCamera,
                    ref _cachedSpaceCameraData,
                    ref _cachedSpaceCameraDataMissing,
                    out UniversalAdditionalCameraData spaceCameraData))
            {
                return;
            }

            EnsureCameraTextureRequirements(mainCameraData, mainCamera);
            EnsureCameraTextureRequirements(spaceCameraData, spaceCamera);
            CaptureGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);

            if (SupportsGameplayCameraStacking(mainCameraData, spaceCameraData))
            {
                RestoreGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);
                return;
            }

            ApplyGameplayCameraCompositionFallback(mainCameraData, spaceCameraData, spaceCamera);
        }

        private void CaptureGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (_cameraCompositionDefaultsCaptured &&
                ReferenceEquals(_capturedCompositionMainCamera, mainCamera) &&
                ReferenceEquals(_capturedCompositionSpaceCamera, spaceCamera))
            {
                return;
            }

            _capturedCompositionMainCamera = mainCamera;
            _capturedCompositionSpaceCamera = spaceCamera;
            _mainCameraOriginalDepth = mainCamera.depth;
            _spaceCameraOriginalDepth = spaceCamera.depth;
            _mainCameraOriginalClearFlags = mainCamera.clearFlags;
            _mainCameraOriginalRenderType = mainCameraData.renderType;
            _spaceCameraOriginalRenderType = spaceCameraData.renderType;
            _mainCameraOriginalCullingMask = mainCamera.cullingMask;
            _cameraCompositionDefaultsCaptured = true;
        }

        private static bool SupportsGameplayCameraStacking(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData)
        {
            ScriptableRenderer mainRenderer = mainCameraData.scriptableRenderer;
            ScriptableRenderer spaceRenderer = spaceCameraData.scriptableRenderer;

            return mainRenderer != null &&
                   spaceRenderer != null &&
                   mainRenderer.SupportsCameraStackingType(CameraRenderType.Overlay) &&
                   spaceRenderer.SupportsCameraStackingType(CameraRenderType.Base);
        }

        private void EnsureCameraTextureRequirementsCached(
            Camera camera,
            ref Camera cachedCamera,
            ref UniversalAdditionalCameraData cachedData,
            ref bool cachedMissing)
        {
            if (!TryResolveCameraDataCold(
                    camera,
                    ref cachedCamera,
                    ref cachedData,
                    ref cachedMissing,
                    out UniversalAdditionalCameraData cameraData))
            {
                return;
            }

            EnsureCameraTextureRequirements(cameraData, camera);
        }

        private static bool TryReadCameraDataCached(
            Camera camera,
            ref Camera cachedCamera,
            ref UniversalAdditionalCameraData cachedData,
            ref bool cachedMissing,
            out UniversalAdditionalCameraData cameraData)
        {
            cameraData = null;
            if (!IsCameraReferenceValid(camera))
                return false;

            if (ReferenceEquals(cachedCamera, camera))
            {
                if (IsCameraDataReferenceValid(cachedData))
                {
                    cameraData = cachedData;
                    return true;
                }

                if (cachedMissing)
                    return false;
            }
            else
            {
                return false;
            }

            return false;
        }

        private static bool TryResolveCameraDataCold(
            Camera camera,
            ref Camera cachedCamera,
            ref UniversalAdditionalCameraData cachedData,
            ref bool cachedMissing,
            out UniversalAdditionalCameraData cameraData)
        {
            if (TryReadCameraDataCached(
                    camera,
                    ref cachedCamera,
                    ref cachedData,
                    ref cachedMissing,
                    out cameraData))
            {
                return true;
            }

            if (!IsCameraReferenceValid(camera))
            {
                cameraData = null;
                cachedCamera = null;
                cachedData = null;
                cachedMissing = false;
                return false;
            }

            if (!ReferenceEquals(cachedCamera, camera))
            {
                cachedCamera = camera;
                cachedData = null;
                cachedMissing = false;
            }
            else if (cachedMissing)
            {
                return false;
            }

            if (!camera.TryGetComponent(out cameraData) || cameraData == null)
            {
                cachedMissing = true;
                return false;
            }

            cachedData = cameraData;
            cachedMissing = false;
            return true;
        }

        private static bool IsCameraDataReferenceValid(UniversalAdditionalCameraData cameraData)
        {
            if (ReferenceEquals(cameraData, null))
                return false;

            try
            {
                return cameraData != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnassignedReferenceException)
            {
                return false;
            }
        }

        private void EnsureCameraTextureRequirements(UniversalAdditionalCameraData cameraData, Camera camera)
        {
            if (cameraData == null)
                return;

            if (cameraData.requiresDepthOption != CameraOverrideOption.On)
                cameraData.requiresDepthOption = CameraOverrideOption.On;

            if (!cameraData.requiresDepthTexture)
                cameraData.requiresDepthTexture = true;

            if (HectonUrpTextureRequirementsGuard.UsesQuestVrMobileSurvivalPolicy)
                return;

            if (cameraData.requiresColorOption != CameraOverrideOption.On)
                cameraData.requiresColorOption = CameraOverrideOption.On;

            if (!cameraData.requiresColorTexture)
                cameraData.requiresColorTexture = true;

            bool shouldEnablePostProcessing = camera != null && HasUnderwaterPass(camera);
            if (!shouldEnablePostProcessing &&
                camera != null &&
                camera.CompareTag("MainCamera"))
            {
                shouldEnablePostProcessing = true;
            }

            if (shouldEnablePostProcessing && !cameraData.renderPostProcessing)
                cameraData.renderPostProcessing = true;
        }

        private void CacheOceanVisualBridgeCold()
        {
            s_oceanVisualBridge = OceanVisualBridgeRegistry.Active;
        }

        private void RefreshOceanVisualBridgeOnColdCadence()
        {
            IOceanVisualBridge activeBridge = OceanVisualBridgeRegistry.Active;
            if (ReferenceEquals(s_oceanVisualBridge, activeBridge))
                return;

            s_oceanVisualBridge = activeBridge;
            _runtimeVisualOwnerResolveRequested = true;
            _pendingOceanMaterialBindingDirty = true;
        }

        private static IOceanVisualBridge ResolveOceanVisualBridge()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return OceanVisualBridgeRegistry.Active;
#endif
            return s_oceanVisualBridge;
        }

        private bool HasUnderwaterPass(Camera camera)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null && bridge.HasUnderwaterPass(camera);
        }

        private Component TryGetUnderwaterPass(Camera camera)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null ? bridge.TryGetUnderwaterPass(camera) : null;
        }

        private Component EnsureUnderwaterPass(Camera camera)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null ? bridge.EnsureUnderwaterPass(camera) : null;
        }

        private bool IsUnderwaterPassEnabled(Component renderer)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null && bridge.IsUnderwaterPassEnabled(renderer);
        }

        private bool IsUnderwaterPassActive(Component renderer)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null && bridge.IsUnderwaterPassActive(renderer);
        }

        private void SetUnderwaterPassEnabled(Component renderer, bool enabled)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge == null)
                return;

            bridge.SetUnderwaterPassEnabled(renderer, enabled);
        }

        private void SetCopyOceanMaterialParamsEachFrame(Component renderer, bool enabled)
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge == null)
                return;

            bridge.SetCopyOceanMaterialParamsEachFrame(renderer, enabled);
        }

        private Material ResolveOceanMaterial()
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null ? bridge.OceanMaterial : null;
        }

        private bool HasUnderwaterPassInstance()
        {
            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            return bridge != null && bridge.HasUnderwaterInstance;
        }

        private void ApplyGameplayCameraCompositionFallback(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (spaceCameraData.renderType != CameraRenderType.Base)
                spaceCameraData.renderType = CameraRenderType.Base;

            if (mainCameraData.renderType != CameraRenderType.Base)
                mainCameraData.renderType = CameraRenderType.Base;

            float fallbackSpaceDepth = _cameraCompositionDefaultsCaptured ? _spaceCameraOriginalDepth : spaceCamera.depth;
            float fallbackMainDepth = math.max(
                _cameraCompositionDefaultsCaptured ? _mainCameraOriginalDepth : mainCamera.depth,
                fallbackSpaceDepth + 1f);

            if (!Mathf.Approximately(spaceCamera.depth, fallbackSpaceDepth))
                spaceCamera.depth = fallbackSpaceDepth;

            if (!Mathf.Approximately(mainCamera.depth, fallbackMainDepth))
                mainCamera.depth = fallbackMainDepth;

            EnsureFallbackMainCameraCelestialVisibility(mainCamera);
            _runtimeCameraStackFallbackActive = true;
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Depth);
        }

        private void RestoreGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (!_cameraCompositionDefaultsCaptured)
                return;

            if (spaceCameraData.renderType != _spaceCameraOriginalRenderType)
                spaceCameraData.renderType = _spaceCameraOriginalRenderType;

            if (mainCameraData.renderType != _mainCameraOriginalRenderType)
                mainCameraData.renderType = _mainCameraOriginalRenderType;

            if (!Mathf.Approximately(spaceCamera.depth, _spaceCameraOriginalDepth))
                spaceCamera.depth = _spaceCameraOriginalDepth;

            if (!Mathf.Approximately(mainCamera.depth, _mainCameraOriginalDepth))
                mainCamera.depth = _mainCameraOriginalDepth;

            if (_runtimeCameraStackFallbackActive || mainCamera.clearFlags != _mainCameraOriginalClearFlags)
                mainCamera.clearFlags = _mainCameraOriginalClearFlags;

            if (_cameraCompositionDefaultsCaptured &&
                mainCamera.cullingMask != _mainCameraOriginalCullingMask)
            {
                mainCamera.cullingMask = _mainCameraOriginalCullingMask;
            }

            if (spaceCameraData.renderType == CameraRenderType.Base &&
                mainCameraData.renderType == CameraRenderType.Overlay)
            {
                var stack = spaceCameraData.cameraStack;
                if (stack != null && !stack.Contains(mainCamera))
                    stack.Add(mainCamera);
            }

            _runtimeCameraStackFallbackActive = false;
        }

        private static void EnsureFallbackMainCameraCelestialVisibility(Camera mainCamera)
        {
            if (mainCamera == null || _CelestialLayerMask == 0)
                return;

            int fallbackMask = mainCamera.cullingMask | _CelestialLayerMask;
            if (mainCamera.cullingMask != fallbackMask)
                mainCamera.cullingMask = fallbackMask;
        }

        private void ApplyRuntimeMainCameraClearFlags(CameraClearFlags desiredFlags)
        {
            if (mainCamera == null)
                return;

            CameraClearFlags appliedFlags = _runtimeCameraStackFallbackActive
                ? CameraClearFlags.Depth
                : desiredFlags;

            if (mainCamera.clearFlags != appliedFlags)
                mainCamera.clearFlags = appliedFlags;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            _runtimeVisualCallbacksActive = true;
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
            _debugEditorDriven = false;
            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            TryRegisterRenderDispatcher();
            EnsureRuntimeVisualOwners();
            EnsureGameplayCameraStackEnabled();

            if (!_registeredColdTick || !_registeredSlowTick || !_registeredLateFrameTick)
                TryRegisterTickManagers();

            if (!_physicsEngineLookupAttempted)
                CachePhysicsEngine();

            if (!_atmoManagerLookupAttempted)
                CacheAtmosphereManager();

            if (biomeMatrixDirector == null)
                ResolveBiomeMatrixDirector();

            ApplyCurrentMatrixVisualOverride();
            EnsureRuntimeVisualOwners();
            EnsureOceanUnderwaterPassOwnership();
            ApplyOceanMaterialBindings();
        }

        private void OnDisable()
        {
            bool runtimeCallbacksActive = _runtimeVisualCallbacksActive || Application.isPlaying;
            _runtimeVisualCallbacksActive = false;
            if (runtimeCallbacksActive)
            {
                TryUnregisterHotSwapListener();
                if (ActiveRuntimeInstance == this)
                {
                    ActiveRuntimeInstance = null;
                    if (ReferenceEquals(RuntimeSkyMaterialReference, skyMaterial))
                        RuntimeSkyMaterialReference = null;
                }
                if (GlobalRegistry.UnderwaterVisuals == this)
                    GlobalRegistry.UnregisterUnderwaterVisualsRuntime(this);

                UnregisterRenderDispatcher();
                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
                SoundscapeEvents.Unregister(this);

                UnregisterTickManagers();
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorUpdate;
                DisableEditorSceneViewUnderwaterPass();
            }
#endif

#if UNITY_EDITOR
            ResumeEditorWaterRendering();
            DisableEditorSceneViewUnderwaterPass();
#endif
            _lastDepthZoneProfile = null;
            _nextThermoclineAllowedTime = float.NegativeInfinity;
            _cachedBottomDistance = float.PositiveInfinity;
            _cachedBottomSiltBoost = 0f;
            _cachedSuspendedMotesParticleCap = 0;
            _cachedSuspendedMotesQualityWeight = -1f;
            _nextBottomSiltProbeTime = float.NegativeInfinity;
            _nextExhaleBubbleAllowedTime = float.NegativeInfinity;
            _nextRuntimeSpaceCameraResolveTime = float.NegativeInfinity;
            _nextSecondaryUnderwaterPassPurgeTime = float.NegativeInfinity;
            _cachedMainCameraDataCamera = null;
            _cachedSpaceCameraDataCamera = null;
            _cachedMainCameraData = null;
            _cachedSpaceCameraData = null;
            _cachedMainCameraDataMissing = false;
            _cachedSpaceCameraDataMissing = false;
            _underwaterSuspendedMotesSearchCamera = null;
            _underwaterExhaleBubblesSearchCamera = null;
            _transitionCameraVfxSearchCamera = null;
            _secondaryUnderwaterPassPurgeMainCamera = null;
            _secondaryUnderwaterPassPurgeSpaceCamera = null;
            _secondaryUnderwaterPassPurgeMainPass = null;
            _secondaryUnderwaterPassPurgeSpacePass = null;
            _shallowSunBeamSearchCamera = null;
            _underwaterSuspendedMotesSearchTransform = null;
            _underwaterExhaleBubblesSearchTransform = null;
            _transitionVisorSearchRoot = null;
            _transitionVisorSearchTransform = null;
            _shallowSunBeamLightSearchTransform = null;
            _sunVisualSearchLight = null;
            _sargassumDragRuntime = null;
            _underwaterSuspendedMotesSearchCompleted = false;
            _underwaterExhaleBubblesSearchCompleted = false;
            _transitionCameraVfxSearchCompleted = false;
            _transitionVisorSearchCompleted = false;
            _shallowSunBeamLightSearchCompleted = false;
            _sunVisualSearchCompleted = false;
            UnsubscribePlayerMovement(_subscribedPlayerMovement);
            DisableUnderwaterSuspendedMotes(true);
            DisableUnderwaterExhaleBubbles(true);
            DisableShallowSunBeam(true);
            RestoreBaseValues();
            RestoreSunVisual();
            RestoreSpaceCameraDefaults();
            RestoreCameraDefaults();
            RestoreSkyMaterialDefaults();
            ReleaseRuntimeSkyboxMaterial();
            ReleaseHudFogLuminanceResources();
            ReleasePhotophobiaFieldResources();
            Shader.SetGlobalVector(_SargassumCanopyShadowParamsId, Vector4.zero);
            Shader.SetGlobalVector(_SargassumCanopyLightingParamsId, new Vector4(0f, 0f, 1f, 0f));
            ResetNoirResolveGlobals();

            if (_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Release(this);
                _renderSettingsGuardAcquired = false;
            }

            ReleaseRuntimeSkyboxMaterial();

        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  EDITOR UPDATE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void OnDestroy()
        {
            bool runtimeCallbacksActive = _runtimeVisualCallbacksActive || Application.isPlaying;
            _runtimeVisualCallbacksActive = false;
            if (runtimeCallbacksActive)
            {
                TryUnregisterHotSwapListener();
                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
                UnregisterTickManagers();
                if (GlobalRegistry.UnderwaterVisuals == this)
                    GlobalRegistry.UnregisterUnderwaterVisualsRuntime(this);
            }

            if (ReferenceEquals(RuntimeSkyMaterialReference, skyMaterial))
                RuntimeSkyMaterialReference = null;

#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            ResumeEditorWaterRendering();
            DisableEditorSceneViewUnderwaterPass();
#endif

            if (_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Release(this);
                _renderSettingsGuardAcquired = false;
            }

            ReleaseHudFogLuminanceResources();
            ReleasePhotophobiaFieldResources();
            _sargassumDragRuntime = null;
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null) return;

            if (Application.isPlaying)
            {
                ResumeEditorWaterRendering();
                return;
            }

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                SuspendEditorWaterRendering();
                DisableEditorSceneViewUnderwaterPass();
                _editorSlowTickAccum = 0f;
                return;
            }

            if (!IsEditorPreviewActive())
            {
                SuspendEditorWaterRendering();
                DisableEditorSceneViewUnderwaterPass();
                _editorSlowTickAccum = 0f;
                return;
            }

            ResolveEditorCamera();

            if (!ShouldRunEditorPreviewTick())
            {
                SuspendEditorWaterRendering();
                DisableEditorSceneViewUnderwaterPass();
                _editorSlowTickAccum = 0f;
                return;
            }

            if (ShouldSuppressEditorGameplayOceanPass())
            {
                SuspendEditorWaterRendering();
            }
            else
            {
                ResumeEditorWaterRendering();
            }

            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;

            QueueUnderwaterVisualTick(dt);

            _editorSlowTickAccum += dt;
            if (_editorSlowTickAccum >= slowTickInterval)
            {
                _editorSlowTickAccum -= slowTickInterval;
                SlowTick();
            }

            _debugEditorDriven = true;
        }

        private static bool IsEditorPreviewActive()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private bool ShouldRunEditorPreviewTick()
        {
            float cameraDepth = ResolveActiveVisualCameraDepth();
            if (cameraDepth <= VisualExitUnderwaterDepth)
                return true;

            return ResolveUnderwaterVisualStateForCameraDepth(cameraDepth, cameraDepth);
        }

        private void ResolveEditorCamera()
        {
            if (Application.isPlaying) return;

            var sv = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sv != null ? sv.camera : null;
            ResolveGameplayMainCameraForEditor();

            Camera authoredGameplayCamera = IsRuntimeMainCamera(_gameplayMainCamera)
                ? _gameplayMainCamera
                : null;
            if (authoredGameplayCamera == null && IsRuntimeMainCamera(mainCamera))
            {
                authoredGameplayCamera = mainCamera;
            }
            else if (authoredGameplayCamera == null && playerCamera != null)
            {
                playerCamera.TryGetComponent(out Camera playerOwnedCamera);
                if (IsRuntimeMainCamera(playerOwnedCamera))
                    authoredGameplayCamera = playerOwnedCamera;
            }

            if (authoredGameplayCamera != null)
            {
                if (!ReferenceEquals(mainCamera, authoredGameplayCamera))
                    mainCamera = authoredGameplayCamera;
                if (!ReferenceEquals(playerCamera, authoredGameplayCamera.transform))
                    playerCamera = authoredGameplayCamera.transform;

                _nextEditorCameraResolveTime = float.NegativeInfinity;
                return;
            }

            if (sceneViewCamera != null)
            {
                if (mainCamera != sceneViewCamera)
                    mainCamera = sceneViewCamera;
                if (!ReferenceEquals(playerCamera, sceneViewCamera.transform))
                    playerCamera = sceneViewCamera.transform;
                _nextEditorCameraResolveTime = float.NegativeInfinity;
                return;
            }

            if (ResolvePresentationClockSeconds() < _nextEditorCameraResolveTime)
                return;

            _nextEditorCameraResolveTime = ResolvePresentationClockSeconds() + EditorCameraResolveRetryInterval;

            if (mainCamera != null)
            {
                if (playerCamera == null)
                    playerCamera = mainCamera.transform;
                return;
            }

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            mainCamera = _gameplayMainCamera;
            playerCamera = _gameplayMainCamera.transform;
        }

        private bool ShouldSuppressEditorGameplayOceanPass()
        {
            if (Application.isPlaying)
                return false;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return false;

            if (!ReferenceEquals(mainCamera, _gameplayMainCamera))
                return false;

            float cameraDepth = math.max(
                0f,
                ResolveWaterLevel() - _gameplayMainCamera.transform.position.y);
            return cameraDepth < VisualForcedUnderwaterDepth;
        }

        private void SuspendEditorWaterRendering()
        {
            _debugEditorDriven = false;

            if (_editorOceanPassSuppressed)
                return;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            _editorOceanUnderwaterPass = TryGetUnderwaterPass(_gameplayMainCamera);

            if (_editorOceanUnderwaterPass != null)
            {
                _editorOceanUnderwaterPassWasEnabled =
                    IsUnderwaterPassEnabled(_editorOceanUnderwaterPass);
                if (_editorOceanUnderwaterPassWasEnabled)
                    SetUnderwaterPassEnabled(_editorOceanUnderwaterPass, false);

                _editorOceanPassSuppressed = _editorOceanUnderwaterPassWasEnabled;
            }

            // Do not suppress gameplay cameras in edit mode. The Game view must
            // remain usable for live preview and tooling.
            _editorGameplayMainCameraWasEnabled = false;
            _editorGameplayMainCameraSuppressed = false;
            _editorGameplaySpaceCameraWasEnabled = false;
            _editorGameplaySpaceCameraSuppressed = false;
        }

        private void ResumeEditorWaterRendering()
        {
            if (_editorGameplaySpaceCameraSuppressed &&
                IsCameraReferenceValid(_editorGameplaySpaceCamera) &&
                _editorGameplaySpaceCameraWasEnabled &&
                !_editorGameplaySpaceCamera.enabled)
            {
                _editorGameplaySpaceCamera.enabled = true;
            }

            _editorGameplaySpaceCameraSuppressed = false;
            _editorGameplaySpaceCameraWasEnabled = false;

            if (_editorGameplayMainCameraSuppressed &&
                _gameplayMainCamera != null &&
                _editorGameplayMainCameraWasEnabled &&
                !_gameplayMainCamera.enabled)
            {
                _gameplayMainCamera.enabled = true;
            }

            _editorGameplayMainCameraSuppressed = false;
            _editorGameplayMainCameraWasEnabled = false;

            if (!_editorOceanPassSuppressed)
                return;

            if (_editorOceanUnderwaterPass != null &&
                _editorOceanUnderwaterPassWasEnabled &&
                !IsUnderwaterPassEnabled(_editorOceanUnderwaterPass))
            {
                SetUnderwaterPassEnabled(_editorOceanUnderwaterPass, true);
            }

            _editorOceanPassSuppressed = false;
            _editorOceanUnderwaterPassWasEnabled = false;
        }
#endif

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  VISUAL_SYNC queue - per-frame
        //
        //  v5.1: By the time this runs, AtmosphereManager has already
        //  computed fresh ProfileSunIntensity and ComputedHorizonFade.
        //  We read those values and combine with depth factor.
        //  CelestialEngine will run AFTER us and multiply by eclipse.
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void QueueUnderwaterVisualTick(float deltaTime)
        {
            _pendingVisualTickDeltaTime += math.max(0f, deltaTime);
            _pendingVisualTickDirty = true;
        }

        private void RunUnderwaterVisualTick(float deltaTime)
        {
            RequestRuntimeVisualOwnerResolveIfMissing();
            EnsureGameplayCameraStackInitializedOnTick();
            ConsumePlayerExhaleSignals();
            DecayExternalBottomSiltBurst(deltaTime);
            UpdateFlowSynchronyState(deltaTime);

            if (playerCamera == null)
            {
                _runtimeVisualOwnerResolveRequested = true;
                return;
            }

            float depth = ResolveCurrentDepth();
            bool isUnderwater = ResolveUnderwaterVisualState(depth);

            ApplySpaceCameraDepthState(depth, isUnderwater);

            UpdateDepthDiagnostics(depth, isUnderwater);
            UpdateSubmergeImpulse(deltaTime);
            RefreshAdaptiveBudgetResponse();
            _cachedVisualDepth = depth;
            _cachedVisualIsUnderwater = isUnderwater;
            UpdateTransportCockpitOverlay();
            TryHandleThermoclineTransition(isUnderwater);

            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
            //  ABOVE WATER
            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

            if (!isUnderwater)
            {
                if (_wasUnderwater)
                {
                    TriggerSurfaceBreakImpulse();
                    _cachedLightFactor = 1f;
                    _cachedCausticsStrength = 0f;
                    ApplySurfaceDefaults();
                    _wasUnderwater = false;
                }

                UpdateUnderwaterSuspendedMotes(depth, 1f, 0f, false);
                DisableUnderwaterExhaleBubbles(true);
                UpdateShallowSunBeam(depth, 1f, false, Vector3.zero, 1f, 0f);
                ApplySargassumCanopyShaderGlobals(default);

                // Ã¢â€â‚¬Ã¢â€â‚¬ v5.1: Write sunLight.intensity EVERY FRAME above water Ã¢â€â‚¬Ã¢â€â‚¬
                // This is the "base" value that CelestialEngine will multiply
                // by eclipse visibility in its Tick() (which runs after ours).
                //
                // profile Ãƒâ€” horizon gives the correct sunset/sunrise dimming.
                // CelestialEngine then applies: intensity *= (1 - eclipseOcclusion)
                //
                // Guard: skip only if AtmosphereManager hasn't computed yet
                // (both values would be at their defaults = 1.0, which is fine).
                if (sunLight != null)
                {
                    float targetSunIntensity;
                    if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState))
                    {
                        targetSunIntensity = ResolveReadableSurfaceSunIntensity(surfaceState.DirectionalLightIntensity);
                        UpdateSurfaceLightDiagnostics(
                            surfaceState.DirectionalLightIntensity,
                            1f,
                            targetSunIntensity);
                    }
                    else
                    {
                        float baseSun = ResolveProfileSunIntensity();
                        float horizon = ResolveHorizonFade();
                        targetSunIntensity = ResolveReadableSurfaceSunIntensity(baseSun * horizon * ResolveSurfaceSunMultiplier());

                        UpdateSurfaceLightDiagnostics(
                            baseSun,
                            horizon,
                            targetSunIntensity);
                    }

                    float smoothedLightFactor = SmoothSunState(targetSunIntensity, 1f, deltaTime);
                    _cachedLightFactor = smoothedLightFactor;
                    ApplySunVisualState(smoothedLightFactor);
                    ApplySunScattering(smoothedLightFactor);
                    ApplySunColorFade(smoothedLightFactor);
                }

                ApplySurfaceReadableRenderSettingsFloor();
                ApplyNoirResolveGlobals();

                return;
            }

            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
            //  ENTERING WATER
            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

            if (!_wasUnderwater)
            {
                RenderSettings.fog     = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                TriggerSubmergeImpulse();
                _wasUnderwater = true;
            }

            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
            //  UNDERWATER Ã¢â‚¬â€ DEPTH-DRIVEN
            // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

            SargassumGlobalDragManager.SargassumFieldSample sargassumCanopySample = ResolveSargassumCanopySample();
            float canopyOcclusion01 = sargassumCanopySample.Occlusion01;
            float canopyWindow01 = sargassumCanopySample.Window01;
            float lightFactor = ResolveDepthLightFactor(depth);
            float submergeImpulse = EvaluateSubmergeImpulse(depth);
            lightFactor *= 1f - (submergeDarkenStrength * submergeImpulse);
            lightFactor *= 1f - (canopyOcclusion01 * sargassumCanopyLightOcclusionStrength);
            _cachedLightFactor = lightFactor;
            _cachedCausticsStrength = ResolveCausticsStrength(depth, lightFactor, isUnderwater);
            UpdateUnderwaterSuspendedMotes(depth, lightFactor, submergeImpulse, true);
            UpdateShallowSunBeam(depth, lightFactor, true, sargassumCanopySample.AnchorWS, canopyWindow01, canopyOcclusion01);
            ApplySargassumCanopyShaderGlobals(sargassumCanopySample);

            // Ã¢â€â‚¬Ã¢â€â‚¬ Sun intensity = profile Ãƒâ€” horizon Ãƒâ€” depthCurve Ã¢â€â‚¬Ã¢â€â‚¬
            float baseSunIntensity = ResolveProfileSunIntensity();
            float horizonFade = ResolveHorizonFade();
            float finalSunIntensity = baseSunIntensity * horizonFade * lightFactor;

            float smoothedUnderwaterLightFactor = SmoothSunState(finalSunIntensity, lightFactor, deltaTime);
            float appliedSunIntensity = _smoothedSunIntensity;
            ApplyRuntimeSkyboxOwnership();
            ApplySunVisualState(smoothedUnderwaterLightFactor);
            ApplySunScattering(smoothedUnderwaterLightFactor);
            ApplySunColorFade(smoothedUnderwaterLightFactor);
            ApplyUnderwaterFog(lightFactor, depth, submergeImpulse, canopyOcclusion01);
            ApplyUnderwaterAmbient(canopyOcclusion01);
            ApplyUnderwaterCamera();

            UpdateLightDiagnostics(lightFactor, baseSunIntensity, horizonFade, appliedSunIntensity);
            ApplyNoirResolveGlobals();
        }

        public void LateFrameTick()
        {
            if (!_pendingVisualTickDirty)
                QueueUnderwaterVisualTick(SystemDispatcher.CurrentFrameDeltaTime);

            if (_pendingVisualTickDirty)
            {
                float deltaTime = _pendingVisualTickDeltaTime;
                _pendingVisualTickDeltaTime = 0f;
                _pendingVisualTickDirty = false;
                RunUnderwaterVisualTick(deltaTime);
            }

            ApplyCachedCameraAndOceanPresentation();

            if (_pendingOceanMaterialBindingDirty)
            {
                _pendingOceanMaterialBindingDirty = false;
                ApplyOceanMaterialBindings();
            }
        }

        /// <summary>
        /// Applies the current per-camera underwater fog state through the registry-owned render dispatcher.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the render dispatcher.</param>
        public void Render(float deltaTime)
        {
            Camera currentCamera = GlobalRenderContext.CurrentCamera;
            if (currentCamera == null)
                return;

            EnforceFogState(currentCamera);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  ISlowTickable.SlowTick Ã¢â‚¬â€ 2Hz
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public void ColdTick()
        {
            if (_runtimeVisualCallbacksActive)
            {
                ResolveRuntimeServiceCachesOnColdCadence();
                RefreshOceanVisualBridgeOnColdCadence();
                ResolveRuntimeVisualOwnersOnColdCadence();
                if (enableHudFogLuminanceGpuReadback)
                    EnsureHudFogLuminanceResources(allowAllocate: true);
                if (enableFlashlightPhotophobiaField)
                    EnsurePhotophobiaFieldResources(allowAllocate: true);
            }
        }

        public void SlowTick()
        {
            if (_runtimeVisualCallbacksActive)
                WarnIfRuntimeReferencesStillMissing();

            if (_runtimeVisualCallbacksActive && enableHudFogLuminanceGpuReadback)
                FlushHudFogLuminanceReadbackRepairSlow();

            if (playerCamera == null) return;

            RefreshTargetsFromCurrentProfile();
            RefreshSoundscapeTierResponse(false);

            float lerpT = math.saturate(biomeTransitionSpeed * slowTickInterval);
            InterpolateBiomeParameters(lerpT);
            ApplyBiomeFogBlend(lerpT);

            _pendingOceanMaterialBindingDirty = true;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  ATMOSPHERE MANAGER INTEGRATION
        //
        //  v5.1: ResolveHorizonFade now reads the PRECOMPUTED value
        //  from AtmosphereManager instead of recalculating.
        //  This ensures ONE source of truth for the horizon curve.
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private float ResolveProfileSunIntensity()
        {
            if (!_atmoManagerLookupAttempted)
                RequestRuntimeServiceCacheCold();

            HectonAtmosphereManager cachedAtmoManager = _cachedAtmoManager;
            if (cachedAtmoManager != null)
            {
                float profileSunIntensity = cachedAtmoManager.ProfileSunIntensity;
                if (profileSunIntensity > 0.0001f)
                    return profileSunIntensity;

                float currentSunIntensity = cachedAtmoManager.CurrentSunIntensity;
                if (currentSunIntensity > 0.0001f)
                    return currentSunIntensity;
            }

            // Fallback: no atmosphere manager = sun at full intensity
            if (sunLight != null && sunLight.intensity > 0.0001f)
                return sunLight.intensity;

            return 1f;
        }

        /// <summary>
        /// v5.1 Patch: Reads AtmosphereManager.ComputedHorizonFade directly.
        ///
        /// OLD (v5.0): Recalculated from SunElevation with its own fadeAngle.
        ///   Problem: different fadeAngle Ã¢â€ â€™ different curve Ã¢â€ â€™ desync with
        ///   what AtmosphereManager considers "sunset".
        ///
        /// NEW (v5.1): Single source of truth.
        ///   AtmosphereManager computes horizonFade from its own _sunHorizonFadeAngle.
        ///   We just read the result.
        ///   If no AtmosphereManager: return 1.0 (sun always visible).
        /// </summary>
        private float ResolveHorizonFade()
        {
            if (!_atmoManagerLookupAttempted)
                RequestRuntimeServiceCacheCold();

            HectonAtmosphereManager cachedAtmoManager = _cachedAtmoManager;
            if (cachedAtmoManager != null)
                return cachedAtmoManager.ComputedHorizonFade;

            return 1f;
        }

        private float ResolveDepthLightFactor(float depth)
        {
            if (depth <= 0f)
                return 1f;

            if (!useBeerLambertDepthAttenuation)
                return math.saturate(globalLightCurve.Evaluate(depth));

            float effectiveDepth = math.max(0f, depth - beerLambertSurfaceClarityDepth);
            if (effectiveDepth <= 0f)
                return 1f;

            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float extinction = ResolveBeerLambertExtinction();
            if (daylightVisibility > 0.001f)
            {
                float readableBandFade = 1f - math.saturate(
                    (depth - DaylightReadableDepth) /
                    math.max(1f, beerLambertBlackoutDepth - DaylightReadableDepth));
                extinction *= LerpClamped(
                    1f,
                    DaylightReadableExtinctionReduction,
                    daylightVisibility * readableBandFade);
            }

            float transmittance = ApproximateExpNegPositive(extinction * effectiveDepth);
            if (daylightVisibility > 0.001f)
            {
                float readabilityDepthT = math.saturate(
                    (depth - beerLambertSurfaceClarityDepth) /
                    math.max(1f, DaylightReadableDepth - beerLambertSurfaceClarityDepth));
                float readabilityBlackoutFade = 1f - math.saturate(
                    (depth - DaylightReadableDepth) /
                    math.max(1f, beerLambertBlackoutDepth - DaylightReadableDepth));
                float readabilityFloor = LerpClamped(
                    DaylightReadableLightFloor,
                    DaylightReadableLightFloor * 0.72f,
                    readabilityDepthT);
                readabilityFloor *= daylightVisibility * readabilityBlackoutFade;
                transmittance = math.max(transmittance, readabilityFloor);
            }

            if (depth >= beerLambertBlackoutDepth &&
                transmittance <= beerLambertBlackoutThreshold)
            {
                return 0f;
            }

            return math.saturate(transmittance);
        }

        private float ResolveBeerLambertExtinction()
        {
            Vector3 depthFogDensity = _currentDepthFogDensity;
            float luminance =
                (depthFogDensity.x * 0.2126f) +
                (depthFogDensity.y * 0.7152f) +
                (depthFogDensity.z * 0.0722f);

            float extinction = luminance * math.max(0.1f, beerLambertExtinctionScale) * GameplayReadableBeerLambertExtinctionBias;
            extinction *= math.max(0.5f, _currentTurbidity);

            return math.max(0.0001f, extinction);
        }

        private void CacheAtmosphereManager()
        {
            _cachedAtmoManager = atmosphereManager;

            if (_cachedAtmoManager == null)
                _cachedAtmoManager = Hecton8.Core.GlobalRegistry.Atmosphere;

            _atmoManagerLookupAttempted = _runtimeVisualCallbacksActive;
            _atmoManagerCached = _cachedAtmoManager != null;

#if UNITY_EDITOR
            _debugAtmoManagerFound = _atmoManagerCached;
#endif
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SUN INTENSITY
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void CacheRuntimeDependencies()
        {
            if (!_runtimeVisualCallbacksActive)
                return;

            CacheGraphicsCapabilitiesCold();
            CacheAudioService(GlobalRegistry.Audio);
            _dynamicResolutionRuntime = GlobalRegistry.DynamicResolution;
            _weatherRuntime = GlobalRegistry.Weather;
            _surfaceWeatherRuntime = GlobalRegistry.SurfaceWeatherReadModel;
            _giRelayRuntime = GlobalRegistry.GIRelay;
            WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);
            _soundscapeRuntime = GlobalRegistry.Soundscape;
            _mapMagicRuntime = null;
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);
            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;

            if (depthZoneDirector == null)
                depthZoneDirector = GlobalRegistry.DepthZone;

            CachePhysicsEngine();
            CacheOceanKinematicsRuntimeCold();
            CacheAtmosphereManager();
            CacheOceanVisualBridgeCold();
            _runtimeServiceResolveRequested = false;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
        }

        private void RequestRuntimeServiceCacheCold()
        {
            if (_runtimeVisualCallbacksActive)
                _runtimeServiceResolveRequested = true;
        }

        private void ResolveRuntimeServiceCachesOnColdCadence()
        {
            if (!_runtimeServiceResolveRequested)
                return;

            _runtimeServiceResolveRequested = false;
            CachePhysicsEngine();
            CacheOceanKinematicsRuntimeCold();
            CacheAtmosphereManager();
            CacheOceanVisualBridgeCold();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !_runtimeVisualCallbacksActive)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;

                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionRuntime = currentService as DynamicResolutionScaler;
                    RefreshAdaptiveBudgetResponse();
                    break;

                case GlobalRegistryServiceSlot.Weather:
                    _weatherRuntime = currentService as IWeatherService;
                    break;

                case GlobalRegistryServiceSlot.SurfaceWeatherRuntime:
                    _surfaceWeatherRuntime = currentService as ISurfaceWeatherReadModel;
                    break;

                case GlobalRegistryServiceSlot.GIRelayRuntime:
                    _giRelayRuntime = currentService as IGIRelaySystem;
                    break;

                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _sargassumDragRuntime = currentService as SargassumGlobalDragManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDragRuntime);
                    break;

                case GlobalRegistryServiceSlot.SoundscapeRuntime:
                    _soundscapeRuntime = currentService as SoundscapeSystem;
                    RefreshSoundscapeTierResponse(true);
                    break;

                case GlobalRegistryServiceSlot.DepthZoneRuntime:
                    depthZoneDirector = currentService as DepthZoneDirector;
                    _lastDepthZoneProfile = null;
                    break;

                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_mapMagicRuntime, previousService))
                        _mapMagicRuntime = null;
                    _mapMagicRuntime = currentService as MapMagicBridge;
                    WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _mapMagicRuntime);
                    _nextBottomSiltProbeTime = float.NegativeInfinity;
                    break;

                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    UnsubscribePlayerMovement(_subscribedPlayerMovement);
                    _playerMovement = null;
                    _playerTransportCoordinator = null;
                    _nextRuntimePlayerCameraResolveTime = float.NegativeInfinity;
                    ResolvePlayerCamera();
                    break;

                case GlobalRegistryServiceSlot.FluidRuntime:
                    _physicsEngine = currentService as IFluidSurfaceCurrentReadModel;
                    _fluidBubbleBurstSink = currentService as IFluidBubbleBurstSink;
                    _physicsEngineLookupAttempted = true;
                    _physicsEngineCached = _physicsEngine != null;
#if UNITY_EDITOR
                    _debugPhysicsEngineFound = _physicsEngineCached;
#endif
                    break;

                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    _oceanKinematicsProvider = _oceanKinematicsService != null
                        ? _oceanKinematicsService.ActiveProvider
                        : null;
                    break;

                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _cachedAtmoManager = atmosphereManager != null
                        ? atmosphereManager
                        : currentService as HectonAtmosphereManager;
                    _atmoManagerLookupAttempted = true;
                    _atmoManagerCached = _cachedAtmoManager != null;
#if UNITY_EDITOR
                    _debugAtmoManagerFound = _atmoManagerCached;
#endif
                    break;
            }
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioRuntime = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioRuntime;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioRuntime = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private float SmoothSunState(float targetIntensity, float targetLightFactor, float deltaTime)
        {
            if (_smoothedSunIntensity < 0f)
                _smoothedSunIntensity = targetIntensity;

            if (_smoothedSunLightFactor < 0f)
                _smoothedSunLightFactor = targetLightFactor;

            float brightenT = ResolveDecayBlend(sunStateBrightenSpeed, deltaTime);
            float darkenT = ResolveDecayBlend(sunStateDarkenSpeed, deltaTime);

            float intensityT = targetIntensity >= _smoothedSunIntensity ? brightenT : darkenT;
            float lightFactorT = targetLightFactor >= _smoothedSunLightFactor ? brightenT : darkenT;

            _smoothedSunIntensity = math.lerp(_smoothedSunIntensity, targetIntensity, intensityT);
            _smoothedSunLightFactor = math.lerp(_smoothedSunLightFactor, targetLightFactor, lightFactorT);

            ApplySunIntensityImmediate(_smoothedSunIntensity, _smoothedSunLightFactor);
            return _smoothedSunLightFactor;
        }

        private float ApplySunIntensityImmediate(float finalIntensity, float lightFactor)
        {
            if (sunLight != null)
                sunLight.intensity = finalIntensity;

            DisableLegacySunFlare();

            return finalIntensity;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SUN VISUAL DISC
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplySunVisualState(float lightFactor)
        {
            if (sunVisualTransform == null) return;

            if (_cachedAtmoManager != null)
            {
                HideSunVisualAboveWater();
                return;
            }

            float disableAt = sunVisualDisableThreshold;
            float enableAt  = sunVisualDisableThreshold * 2f;

            if (!_sunVisualWasDisabled)
            {
                if (lightFactor < disableAt)
                {
                    sunVisualTransform.gameObject.SetActive(false);
                    _sunVisualWasDisabled = true;
                }
            }
            else
            {
                if (lightFactor > enableAt)
                {
                    sunVisualTransform.gameObject.SetActive(true);
                    _sunVisualWasDisabled = false;
                }
            }

#if UNITY_EDITOR
            _debugSunVisualActive = !_sunVisualWasDisabled;
#endif
        }

        private void RestoreSunVisual()
        {
            if (_cachedAtmoManager != null)
            {
                HideSunVisualAboveWater();
                return;
            }

            if (sunVisualTransform != null && _sunVisualWasDisabled)
            {
                sunVisualTransform.gameObject.SetActive(true);
                _sunVisualWasDisabled = false;
            }
        }

        private void HideSunVisualAboveWater()
        {
            if (sunVisualTransform == null) return;

            if (sunVisualTransform.gameObject.activeSelf)
                sunVisualTransform.gameObject.SetActive(false);

            _sunVisualWasDisabled = true;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SUN SCATTERING
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private Material ResolveActiveSkyMaterial()
        {
            return skyMaterial;
        }

        private void ApplyRuntimeSkyboxOwnership()
        {
            if (!_runtimeVisualCallbacksActive || skyMaterial == null)
                return;

            if (AtmosphereDirector.IsSkybox(skyMaterial))
                return;

            AtmosphereDirector.SetSkybox(skyMaterial);
        }

        private void ReleaseRuntimeSkyboxMaterial()
        {
            if (skyMaterial != null && AtmosphereDirector.Skybox == null)
                AtmosphereDirector.SetSkybox(skyMaterial);
        }

        private void CacheRuntimeSkyMaterialReference()
        {
            if (skyMaterial != null)
                RuntimeSkyMaterialReference = skyMaterial;
        }

        private void ForceMandatedSkyboxOwnership()
        {
            if (skyMaterial == null)
                return;

            if (!ReferenceEquals(RuntimeSkyMaterialReference, skyMaterial))
                RuntimeSkyMaterialReference = skyMaterial;

            if (!AtmosphereDirector.IsSkybox(skyMaterial))
                AtmosphereDirector.SetSkybox(skyMaterial);
        }

        internal static bool TryGetRuntimeSkyMaterialReference(out Material skyMaterialReference)
        {
            skyMaterialReference = RuntimeSkyMaterialReference;
            return skyMaterialReference != null;
        }

        private void ApplySunScattering(float lightFactor)
        {
            Material activeSkyMaterial = ResolveActiveSkyMaterial();
            if (activeSkyMaterial == null) return;

            float scatterT = math.saturate(1f - lightFactor);

            float sunSize = LerpClamped(baseSunSize, underwaterSunSizeMax, scatterT);
            float sunSoftness = LerpClamped(baseSunEdgeSoftness, underwaterSunSoftnessMax, scatterT);

            activeSkyMaterial.SetFloat(_ID_SunSize, sunSize);
            activeSkyMaterial.SetFloat(_ID_SunEdgeSoftness, sunSoftness);

#if UNITY_EDITOR
            _debugSunScatter = scatterT;
#endif
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SUN DISC / SCATTER COLOR FADE
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void CaptureSkyBaseColors()
        {
            if (_baseSkyColorsCaptured) return;
            Material activeSkyMaterial = ResolveActiveSkyMaterial();
            if (activeSkyMaterial == null) return;

            if (activeSkyMaterial.HasColor(_ID_SunDiscColor))
                _baseSunDiscColor = activeSkyMaterial.GetColor(_ID_SunDiscColor);
            else
                _baseSunDiscColor = Color.white;

            if (activeSkyMaterial.HasColor(_ID_SunScatterColor))
                _baseSunScatterColor = activeSkyMaterial.GetColor(_ID_SunScatterColor);
            else
                _baseSunScatterColor = Color.white;

            _baseSkyColorsCaptured = true;
        }

        private void ApplySunColorFade(float lightFactor)
        {
            Material activeSkyMaterial = ResolveActiveSkyMaterial();
            if (activeSkyMaterial == null) return;
            if (!_baseSkyColorsCaptured) return;

            float colorFactor = lightFactor * lightFactor;

            Color fadedDisc;
            fadedDisc.r = _baseSunDiscColor.r * colorFactor;
            fadedDisc.g = _baseSunDiscColor.g * colorFactor;
            fadedDisc.b = _baseSunDiscColor.b * colorFactor;
            fadedDisc.a = _baseSunDiscColor.a;

            Color fadedScatter;
            fadedScatter.r = _baseSunScatterColor.r * colorFactor;
            fadedScatter.g = _baseSunScatterColor.g * colorFactor;
            fadedScatter.b = _baseSunScatterColor.b * colorFactor;
            fadedScatter.a = _baseSunScatterColor.a;

            activeSkyMaterial.SetColor(_ID_SunDiscColor, fadedDisc);
            activeSkyMaterial.SetColor(_ID_SunScatterColor, fadedScatter);
        }

        private void RestoreSkyMaterialDefaults()
        {
            Material activeSkyMaterial = ResolveActiveSkyMaterial();
            if (activeSkyMaterial == null) return;

            activeSkyMaterial.SetFloat(_ID_SunSize, baseSunSize);
            activeSkyMaterial.SetFloat(_ID_SunEdgeSoftness, baseSunEdgeSoftness);

            if (_baseSkyColorsCaptured)
            {
                activeSkyMaterial.SetColor(_ID_SunDiscColor, _baseSunDiscColor);
                activeSkyMaterial.SetColor(_ID_SunScatterColor, _baseSunScatterColor);
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  FOG
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public void ApplyGIRelaySurfaceEmission(Color surfaceEmission)
        {
            if (!IsFiniteColor(surfaceEmission))
                return;

            surfaceEmission.a = 1f;
            Shader.SetGlobalColor(_HectonWaterSurfaceEmissionId, surfaceEmission);
            Shader.SetGlobalColor(_HectonUnderwaterSurfaceColorId, surfaceEmission);

            bool surfaceEmissionChanged = !_giRelaySurfaceEmissionActive ||
                HasColorDelta(_giRelaySurfaceEmissionColor, surfaceEmission);
            if (surfaceEmissionChanged)
            {
                _giRelaySurfaceEmissionColor = surfaceEmission;
                _giRelaySurfaceEmissionActive = true;
                ApplyOceanMaterialBindings();
                return;
            }

            ApplyGIRelaySurfaceEmissionToMaterial(oceanUnderwaterMaterial, surfaceEmission);
        }

        private bool IsGIRelayAmbientAuthorityActive()
        {
            IGIRelaySystem giRelay = _giRelayRuntime;
            return giRelay != null && giRelay.IsAmbientProbeAuthorityActive;
        }

        private static bool IsFiniteColor(Color color)
        {
            return math.isfinite(color.r) &&
                   math.isfinite(color.g) &&
                   math.isfinite(color.b) &&
                   math.isfinite(color.a);
        }

        private static bool HasColorDelta(Color lhs, Color rhs)
        {
            float dr = lhs.r - rhs.r;
            float dg = lhs.g - rhs.g;
            float db = lhs.b - rhs.b;
            return (dr * dr) + (dg * dg) + (db * db) > GIRelaySurfaceEmissionEpsilon;
        }

        private static void ApplyGIRelaySurfaceEmissionToMaterial(Material targetMaterial, Color surfaceEmission)
        {
            if (targetMaterial == null)
                return;

            Color shallow = MaxColorRgb(
                ReadMaterialColorOrDefault(targetMaterial, _ID_SubSurfaceShallowCol, surfaceEmission),
                surfaceEmission);
            Color diffuseGrazing = MaxColorRgb(
                ReadMaterialColorOrDefault(targetMaterial, _ID_DiffuseGrazing, surfaceEmission),
                ScaleColorRgb(surfaceEmission, 0.82f));
            Color subsurface = MaxColorRgb(
                ReadMaterialColorOrDefault(targetMaterial, _ID_SubSurfaceColour, surfaceEmission),
                ScaleColorRgb(surfaceEmission, 0.9f));

            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseGrazing, diffuseGrazing);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceColour, subsurface);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowCol, shallow);
        }

        private Color ResolveSurfaceFogColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
            {
                Color stateFogColor = state.FogColor;
                stateFogColor.a = 1f;
                return ResolveSurfaceReadableDaylightColor(
                    LiftColorToMinimumLuminance(
                        stateFogColor,
                        SurfaceFogReadableLuminanceFloor,
                        0.66f),
                    SurfaceFogDaylightBlueBias);
            }

            Color skyHorizonColor = Shader.GetGlobalColor(_ID_SkyColorHorizon);
            if (skyHorizonColor.maxColorComponent > 0.0001f)
            {
                skyHorizonColor.a = 1f;
                return ResolveSurfaceReadableDaylightColor(
                    LiftColorToMinimumLuminance(
                        skyHorizonColor,
                        SurfaceFogReadableLuminanceFloor,
                        0.66f),
                    SurfaceFogDaylightBlueBias);
            }

            Color fallbackFog = _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogColor
                : surfaceFogColor;
            fallbackFog.a = 1f;
            return ResolveSurfaceReadableDaylightColor(
                LiftColorToMinimumLuminance(
                    fallbackFog,
                    SurfaceFogReadableLuminanceFloor,
                    0.66f),
                SurfaceFogDaylightBlueBias);
        }

        private float ResolveSurfaceFogDensity()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
                return ResolveReadableSurfaceFogDensity(state.FogDensity);

            float density = _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogDensity
                : surfaceFogDensity;
            return ResolveReadableSurfaceFogDensity(density);
        }

        private Color ResolveSurfaceAmbientColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
            {
                Color surfaceAmbient = Color.Lerp(state.AmbientEquatorColor, state.AmbientSkyColor, 0.35f);
                surfaceAmbient.a = 1f;
                return surfaceAmbient;
            }

            if (_surfaceWeatherOverrideActive)
                return _surfaceWeatherAmbientColor;

            Color skyAmbient = RenderSettings.ambientSkyColor;
            if (skyAmbient.maxColorComponent <= 0.0001f)
                skyAmbient = ResolveSurfaceSkyZenithColor();

            if (skyAmbient.maxColorComponent <= 0.0001f)
                return surfaceAmbientColor;

            Color blendedAmbient = Color.Lerp(surfaceAmbientColor, skyAmbient, 0.72f);
            blendedAmbient.a = 1f;
            return blendedAmbient;
        }

        private float ResolveSurfaceSunMultiplier()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherSunMultiplier
                : 1f;
        }

        private static float ResolveReadableSurfaceSunIntensity(float intensity)
        {
            return math.max(math.isfinite(intensity) ? intensity : 0f, SurfaceReadableSunIntensityFloor);
        }

        private static float ResolveReadableSurfaceAmbientIntensity(float intensity)
        {
            return math.max(math.isfinite(intensity) ? intensity : 0f, SurfaceReadableAmbientIntensityFloor);
        }

        private static float ResolveReadableSurfaceFogDensity(float density)
        {
            if (!math.isfinite(density))
                return SurfaceReadableFogDensityCeiling;

            return math.min(math.max(0f, density), SurfaceReadableFogDensityCeiling);
        }

        private static Color ResolveReadableSurfaceAmbientColor(Color source, Color floor)
        {
            source.r = math.max(source.r, floor.r);
            source.g = math.max(source.g, floor.g);
            source.b = math.max(source.b, floor.b);
            source.a = 1f;
            return source;
        }

        private void ApplySurfaceReadableRenderSettingsFloor()
        {
            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveReadableSurfaceFogDensity(RenderSettings.fogDensity);
            }

            if (IsGIRelayAmbientAuthorityActive())
                return;

            if (RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientSkyColor = ResolveReadableSurfaceAmbientColor(
                    RenderSettings.ambientSkyColor,
                    SurfaceReadableSkyAmbientFloor);
                RenderSettings.ambientEquatorColor = ResolveReadableSurfaceAmbientColor(
                    RenderSettings.ambientEquatorColor,
                    SurfaceReadableEquatorAmbientFloor);
                RenderSettings.ambientGroundColor = ResolveReadableSurfaceAmbientColor(
                    RenderSettings.ambientGroundColor,
                    SurfaceReadableGroundAmbientFloor);
            }
            else
            {
                RenderSettings.ambientLight = ResolveReadableSurfaceAmbientColor(
                    RenderSettings.ambientLight,
                    SurfaceReadableSkyAmbientFloor);
            }

            RenderSettings.ambientIntensity = ResolveReadableSurfaceAmbientIntensity(RenderSettings.ambientIntensity);
        }

        private void ApplyUnderwaterFog(float lightFactor, float currentDepth, float submergeImpulse, float canopyOcclusion01)
        {
            Color fogColor = ResolveUnderwaterFogColor(lightFactor, currentDepth);
            if (canopyOcclusion01 > 0.0001f)
            {
                float canopyColorBlend = canopyOcclusion01 * 0.55f;
                fogColor = Color.Lerp(fogColor, fogColor * 0.54f, canopyColorBlend);
                fogColor.a = 1f;
            }
            float effectiveSurfaceFogBlendDepth = math.min(surfaceFogBlendDepth, MaxSurfaceFogBlendDepth);
            float surfaceBlend = 1f - math.saturate(
                currentDepth / math.max(0.01f, effectiveSurfaceFogBlendDepth));
            surfaceBlend *= surfaceBlend;
            if (surfaceBlend > 0f)
            {
                fogColor = Color.Lerp(fogColor, ResolveSurfaceFogColor(), surfaceBlend);
                fogColor.a = 1f;
            }

            _cachedUnderwaterFogColor = fogColor;
            RenderSettings.fogColor = fogColor;

            float baseDensity = LerpClamped(maxFogDensity, minFogDensity, lightFactor);

            // TASK-198: Delegate pure math logic to extracted static class
            string biomeName = "OpenOcean";
            if (_activeMatrixFogProfile != null)
            {
                biomeName = _activeMatrixFogProfile.biomeName;
            }
            float rawDensity = Hecton8.PureLogic.Systems.UnderwaterFogDensityCalculator.Compute(biomeName, currentDepth, baseDensity, _currentTurbidity);

            float targetDensity = rawDensity;

            // Note: In an ideal full-extraction we'd move all modifiers into the static method,
            // but the prompt specifies extracting the base formula. We'll leave the local modifiers intact
            // and pass the rest to the compute function or simply replace the base `targetDensity`.
            targetDensity *= _currentBiomeFogDensityScale;
            targetDensity *= _soundscapeFogDensityScale;
            targetDensity *= 1f + (submergeFogBoost * submergeImpulse);
            targetDensity *= 1f + (canopyOcclusion01 * sargassumCanopyFogBoost);
            float shallowDensityFloor = LerpClamped(
                UnderwaterFogDensityFloorNearSurface,
                UnderwaterFogDensityFloorAtDepth,
                math.saturate(currentDepth / UnderwaterFogDensityFloorDepth));
            targetDensity = math.max(targetDensity, shallowDensityFloor);
            targetDensity += UnderwaterBaselineDistanceHaze * math.max(0.85f, _currentTurbidity);
            float farHazeBlend = math.saturate(
                (currentDepth - UnderwaterFarHazeStartDepth) /
                math.max(0.01f, UnderwaterFarHazeFullDepth - UnderwaterFarHazeStartDepth));
            targetDensity += UnderwaterFarHazeDensityBoost * _currentTurbidity * farHazeBlend;
            float depthColumnHazeBlend = math.saturate(currentDepth / UnderwaterDepthColumnHazeFullDepth);
            targetDensity += UnderwaterDepthColumnHazeDensityBoost *
                LerpClamped(0.75f, 1f, ResolveSurfaceDaylightVisibility()) *
                _currentTurbidity *
                depthColumnHazeBlend;

            float smoothSubmerge = math.saturate(currentDepth / 0.5f);
            float surfDensity = enableSurfaceFog ? surfaceFogDensity : 0.0001f;
            if (_surfaceWeatherOverrideActive)
                surfDensity = ResolveSurfaceFogDensity();

            _cachedFogDensity = LerpClamped(surfDensity, targetDensity, smoothSubmerge);
            RenderSettings.fogDensity = _cachedFogDensity;

#if UNITY_EDITOR
            _debugFogDensity = _cachedFogDensity;
#endif
        }

        private void EnforceFogState(Camera cam)
        {
            if (cam == null || cam.cameraType == CameraType.Preview)
                return;

            if (_runtimeVisualCallbacksActive)
                _debugEditorDriven = false;

            bool renderUnderwater =
                !(cam.cameraType == CameraType.SceneView && !_runtimeVisualCallbacksActive) &&
                ShouldRenderUnderwaterFogForCamera(cam);

            if (renderUnderwater)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = ResolvePerCameraUnderwaterFogDensity(cam);
            }
            else if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out _))
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveSurfaceFogDensity();
            }
            else
            {
                if (enableSurfaceFog)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = ResolveSurfaceFogColor();
                    RenderSettings.fogDensity = ResolveSurfaceFogDensity();
                }
                else
                {
                    RenderSettings.fog = false;
                }
            }
        }

        private float ResolvePerCameraUnderwaterFogDensity(Camera cam)
        {
            if (cam == null)
                return _cachedFogDensity;

            if (cam.cameraType != CameraType.SceneView || _runtimeVisualCallbacksActive)
                return _cachedFogDensity;

            float surfaceDensity = enableSurfaceFog ? surfaceFogDensity : 0.0001f;
            if (_surfaceWeatherOverrideActive)
                surfaceDensity = ResolveSurfaceFogDensity();

            float sceneViewDensityScale = math.min(sceneViewUnderwaterFogDensityScale, MaxSceneViewUnderwaterFogDensityScale);
            return LerpClamped(surfaceDensity, _cachedFogDensity, sceneViewDensityScale);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  AMBIENT / CAMERA
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplyUnderwaterAmbient(float canopyOcclusion01)
        {
            Color effectiveAmbient = ResolveUnderwaterAmbientColor();
            if (canopyOcclusion01 > 0.0001f)
            {
                float canopyDarken = 1f - (canopyOcclusion01 * sargassumCanopyAmbientOcclusionStrength);
                effectiveAmbient.r *= canopyDarken;
                effectiveAmbient.g *= canopyDarken;
                effectiveAmbient.b *= canopyDarken;
            }
            Color ambient;
            ambient.r = math.max(effectiveAmbient.r, MIN_AMBIENT.r);
            ambient.g = math.max(effectiveAmbient.g, MIN_AMBIENT.g);
            ambient.b = math.max(effectiveAmbient.b, MIN_AMBIENT.b);
            ambient.a = 1f;

            if (IsGIRelayAmbientAuthorityActive())
                return;

            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState) &&
                surfaceState.IsValid != 0)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;

                float depthBlend = math.saturate(_cachedVisualDepth / UnderwaterDaylightSeaTintDepth);
                Color skyAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientSkyColor, ambient, LerpClamped(0.18f, 0.34f, depthBlend)),
                    ScaleColorRgb(ambient, 0.78f));
                Color equatorAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientEquatorColor, ambient, LerpClamped(0.26f, 0.5f, depthBlend)),
                    ambient);
                Color groundAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientGroundColor, ambient, LerpClamped(0.34f, 0.62f, depthBlend)),
                    ScaleColorRgb(ambient, 0.88f));

                skyAmbient.a = 1f;
                equatorAmbient.a = 1f;
                groundAmbient.a = 1f;

                RenderSettings.ambientSkyColor = skyAmbient;
                RenderSettings.ambientEquatorColor = equatorAmbient;
                RenderSettings.ambientGroundColor = groundAmbient;
                RenderSettings.ambientIntensity = Mathf.Max(surfaceState.AmbientIntensity, 0.55f);
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;
            mainCamera.backgroundColor = _cachedUnderwaterFogColor;
            CameraClearFlags underwaterClearFlags =
                _mainCameraUnderwaterPass != null &&
                IsUnderwaterPassActive(_mainCameraUnderwaterPass)
                    ? CameraClearFlags.Skybox
                    : CameraClearFlags.SolidColor;
            ApplyRuntimeMainCameraClearFlags(underwaterClearFlags);
        }

        private SargassumGlobalDragManager.SargassumFieldSample ResolveSargassumCanopySample()
        {
            SargassumGlobalDragManager.SargassumFieldSample sample = default;
            sample.Window01 = 1f;
            if (!enableSargassumCanopyLighting)
                return sample;

            SargassumGlobalDragManager dragManager = _sargassumDragRuntime;
            if (dragManager == null)
                return sample;

            Transform sampleTransform = playerCamera != null ? playerCamera : mainCamera != null ? mainCamera.transform : null;
            if (sampleTransform == null)
                return sample;

            dragManager.SampleDetailedInfluence(sampleTransform.position, 1.15f, 0f, out sample);
            return sample;
        }

        private void ApplySargassumCanopyShaderGlobals(SargassumGlobalDragManager.SargassumFieldSample sample)
        {
            if (!enableSargassumCanopyLighting || sample.HasInfluence == 0)
            {
                Shader.SetGlobalVector(_SargassumCanopyShadowParamsId, Vector4.zero);
                Shader.SetGlobalVector(_SargassumCanopyLightingParamsId, new Vector4(0f, 0f, 1f, 0f));
                return;
            }

            float inverseRadius = 1f / math.max(0.01f, sargassumCanopyShadowRadius);
            Shader.SetGlobalVector(
                _SargassumCanopyShadowParamsId,
                new Vector4(
                    sample.AnchorWS.x,
                    sample.AnchorWS.z,
                    inverseRadius,
                    sample.Occlusion01));
            Shader.SetGlobalVector(
                _SargassumCanopyLightingParamsId,
                new Vector4(
                    sample.Density01,
                    sample.Occlusion01,
                    sample.Window01,
                    1f));
        }

        private Color ResolveUnderwaterAmbientColor()
        {
            Color ambientColor = _currentAmbientColor;
            ambientColor.r *= _soundscapeAmbientScale;
            ambientColor.g *= _soundscapeAmbientScale;
            ambientColor.b *= _soundscapeAmbientScale;

            float currentDepth = ResolveCurrentDepth();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float sunlitAmbientBlend =
                daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterSunlitAmbientDepth));
            if (sunlitAmbientBlend > 0.0001f)
            {
                Color skylitWaterAmbient = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.18f);
                skylitWaterAmbient.a = 1f;
                ambientColor = Color.Lerp(
                    ambientColor,
                    skylitWaterAmbient,
                    sunlitAmbientBlend * UnderwaterSunlitAmbientStrength);
            }

            float daylightSeaAmbientBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterDaylightSeaTintDepth));
            if (daylightSeaAmbientBlend > 0.0001f)
            {
                float shallowSeaBlend = 1f - math.saturate(currentDepth / UnderwaterSunlitAmbientDepth);
                Color daylightSeaAmbient = Color.Lerp(
                    UnderwaterDaylightSeaTintMid,
                    UnderwaterDaylightSeaTintShallow,
                    shallowSeaBlend);
                daylightSeaAmbient = MaxColorRgb(
                    daylightSeaAmbient,
                    ScaleColorRgb(_currentScatterShallow, 0.72f));
                ambientColor = Color.Lerp(
                    ambientColor,
                    daylightSeaAmbient,
                    daylightSeaAmbientBlend * UnderwaterDaylightAmbientTintStrength);
            }

            if (_soundscapeThermalTintBlend > 0.001f)
                ambientColor = Color.Lerp(ambientColor, thermalTierTintColor, _soundscapeThermalTintBlend);

            ambientColor.a = 1f;
            return ambientColor;
        }

        private Color ResolveUnderwaterFogColor(float lightFactor, float currentDepth)
        {
            Color fogColor = ResolveBaseUnderwaterFogColor();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float shallowColumnBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterShallowColumnColorDepth));
            if (shallowColumnBlend > 0.0001f)
            {
                Color shallowColumnColor = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.18f);
                shallowColumnColor.a = 1f;
                fogColor = Color.Lerp(
                    fogColor,
                    shallowColumnColor,
                    shallowColumnBlend * UnderwaterShallowColumnColorStrength);
            }

            float fogBlackoutStartDepth = LerpClamped(
                FogBlackoutStartDepthNight,
                FogBlackoutStartDepthDay,
                daylightVisibility);

            float blackoutRange = math.max(
                1f,
                beerLambertBlackoutDepth - fogBlackoutStartDepth);
            float depthBlackBlend = math.saturate(
                (currentDepth - fogBlackoutStartDepth) / blackoutRange);
            depthBlackBlend *= depthBlackBlend;

            float extinctionBlend =
                math.saturate(1f - lightFactor) *
                math.saturate(
                    (currentDepth - beerLambertSurfaceClarityDepth) /
                    math.max(1f, beerLambertBlackoutDepth - beerLambertSurfaceClarityDepth));
            extinctionBlend *= LerpClamped(1f, 0.45f, daylightVisibility);

            float deepBlackBlend = math.max(depthBlackBlend, extinctionBlend);
            if (deepBlackBlend <= 0.0001f)
            {
                if (_weatherStormFlowBlend > 0.0001f)
                    fogColor = Color.Lerp(fogColor, _stormFogDriftColor, _weatherStormFlowBlend * StormFogColorInfluence);
                fogColor.a = 1f;
                return fogColor;
            }

            Color abyssColor = new Color(0.004f, 0.008f, 0.016f, 1f);
            fogColor = Color.Lerp(fogColor, abyssColor, deepBlackBlend * FogBlackBlendIntensity);
            if (_weatherStormFlowBlend > 0.0001f)
                fogColor = Color.Lerp(fogColor, _stormFogDriftColor, _weatherStormFlowBlend * StormFogColorInfluence);
            fogColor.a = 1f;
            return fogColor;
        }

        private float ResolveSurfaceDaylightVisibility()
        {
            float directSunFactor = Mathf.Clamp01(ResolveProfileSunIntensity() * ResolveHorizonFade());
            Color zenithColor = ResolveSurfaceSkyZenithColor();
            Color horizonColor = ResolveSurfaceFogColor();
            Color daylightColor = Color.Lerp(zenithColor, horizonColor, 0.25f);
            float skyFactor = ResolvePerceivedLuminance(daylightColor);
            return Mathf.Clamp01(Mathf.Max(directSunFactor, skyFactor * 0.82f));
        }

        private Color ResolveBaseUnderwaterFogColor()
        {
            float currentDepth = ResolveCurrentDepth();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            Color waterMediumColor = Color.Lerp(_currentScatterBase, _currentScatterShallow, 0.62f);
            waterMediumColor.a = 1f;
            Color fogColor = Color.Lerp(_currentFogColor, waterMediumColor, UnderwaterMediumFogColorBlend);

            float biomeInfluence = LerpClamped(
                UnderwaterBiomeFogInfluenceShallow,
                UnderwaterBiomeFogInfluenceDeep,
                math.saturate(currentDepth / UnderwaterBiomeFogInfluenceDepth));
            fogColor = Color.Lerp(waterMediumColor, fogColor, biomeInfluence);

            float daylightSeaTintBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterDaylightSeaTintDepth));
            if (daylightSeaTintBlend > 0.0001f)
            {
                float shallowSeaBlend = 1f - math.saturate(currentDepth / UnderwaterSunlitTintDepth);
                Color daylightSeaTint = Color.Lerp(
                    UnderwaterDaylightSeaTintMid,
                    UnderwaterDaylightSeaTintShallow,
                    shallowSeaBlend);
                daylightSeaTint = MaxColorRgb(
                    daylightSeaTint,
                    ScaleColorRgb(_currentScatterShallow, 0.84f));
                fogColor = Color.Lerp(
                    fogColor,
                    daylightSeaTint,
                    daylightSeaTintBlend * UnderwaterDaylightSeaTintStrength);
            }

            float sunlitShallowBlend =
                daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterSunlitTintDepth));
            if (sunlitShallowBlend > 0.0001f)
            {
                Color sunlitWaterColor = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.16f);
                sunlitWaterColor.a = 1f;
                fogColor = Color.Lerp(
                    fogColor,
                    sunlitWaterColor,
                    sunlitShallowBlend * UnderwaterSunlitTintStrength);
            }

            if (_soundscapeThermalTintBlend > 0.001f)
                fogColor = Color.Lerp(fogColor, thermalTierTintColor, _soundscapeThermalTintBlend);

            fogColor.a = 1f;
            return fogColor;
        }

        private bool ShouldRenderUnderwaterFogForCamera(Camera camera)
        {
            if (camera != null && ReferenceEquals(camera, _spaceCamera))
                return false;

            float cameraDepth = ResolveVisualDepthForCamera(camera);
            if (cameraDepth <= VisualExitUnderwaterDepth)
                return false;

            if (_runtimeVisualCallbacksActive &&
                !_wasUnderwater &&
                cameraDepth < VisualForcedUnderwaterDepth)
            {
                return false;
            }

            return ResolveUnderwaterVisualStateForCameraDepth(cameraDepth, cameraDepth);
        }

        private void EnsureGameplayCameraStackInitializedOnTick()
        {
            if (!_runtimeVisualCallbacksActive)
                return;

            if (mainCamera == null ||
                !IsRuntimeMainCamera(mainCamera) ||
                !mainCamera.enabled ||
                !mainCamera.gameObject.activeInHierarchy)
            {
                return;
            }

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            bool missingStackSetup =
                _mainCameraUnderwaterPass == null ||
                !IsUnderwaterPassEnabled(_mainCameraUnderwaterPass) ||
                (spaceCamera != null && !spaceCamera.enabled);

            if (!missingStackSetup)
                return;

            _runtimeVisualOwnerResolveRequested = true;
        }

        private Color ResolveSurfaceSkyZenithColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid != 0)
            {
                Color stateZenith = state.SkyZenithColor;
                stateZenith.a = 1f;
                return ResolveSurfaceReadableDaylightColor(
                    LiftColorToMinimumLuminance(
                        stateZenith,
                        SurfaceSkyReadableLuminanceFloor,
                        0.54f),
                    SurfaceSkyDaylightBlueBias);
            }

            Color zenithColor = Shader.GetGlobalColor(_ID_SkyColorZenith);
            if (zenithColor.maxColorComponent <= 0.0001f)
                zenithColor = RenderSettings.ambientSkyColor;

            if (zenithColor.maxColorComponent <= 0.0001f)
                zenithColor = surfaceFogColor;

            zenithColor.a = 1f;
            return ResolveSurfaceReadableDaylightColor(
                LiftColorToMinimumLuminance(
                    zenithColor,
                    SurfaceSkyReadableLuminanceFloor,
                    0.54f),
                SurfaceSkyDaylightBlueBias);
        }

        private Color ResolveSurfaceHorizonVeilColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid != 0)
            {
                Color veilColor = Color.Lerp(
                    state.FogColor,
                    state.HorizonHazeColor,
                    Mathf.Clamp01(0.35f + (state.HorizonHazeIntensity * 0.85f)));
                veilColor = Color.Lerp(
                    veilColor,
                    state.AmbientEquatorColor,
                    0.18f);
                veilColor.a = 1f;
                return ResolveSurfaceReadableDaylightColor(
                    LiftColorToMinimumLuminance(
                        veilColor,
                        SurfaceHorizonReadableLuminanceFloor,
                        0.62f),
                    SurfaceHorizonDaylightBlueBias);
            }

            return ResolveSurfaceFogColor();
        }

        private Color ResolveSurfaceOceanHorizonMergeColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid != 0)
            {
                Color skyDrivenColor = Color.Lerp(
                    state.HorizonHazeColor,
                    state.SkyHorizonColor,
                    surfaceOceanHorizonSkyBias);
                Color ambientLiftColor = Color.Lerp(
                    state.AmbientEquatorColor,
                    state.AmbientSkyColor,
                    0.24f);
                Color fogAnchoredColor = Color.Lerp(
                    state.FogColor,
                    skyDrivenColor,
                    surfaceOceanHorizonSkyBias);
                Color mergeColor = Color.Lerp(fogAnchoredColor, ambientLiftColor, 0.16f);
                mergeColor = Color.Lerp(mergeColor, skyDrivenColor, surfaceOceanHorizonColorPreserve);

                float targetLuminance = Mathf.Max(
                    ResolvePerceivedLuminance(skyDrivenColor),
                    ResolvePerceivedLuminance(state.HorizonHazeColor),
                    ResolvePerceivedLuminance(ambientLiftColor) * 0.92f);
                mergeColor = LiftColorTowardsLuminance(
                    mergeColor,
                    targetLuminance,
                    0.18f + (surfaceOceanHorizonColorPreserve * 0.2f));
                mergeColor.a = 1f;
                return ResolveSurfaceReadableDaylightColor(
                    LiftColorToMinimumLuminance(
                        mergeColor,
                        SurfaceHorizonReadableLuminanceFloor,
                        0.62f),
                    SurfaceHorizonDaylightBlueBias);
            }

            return ResolveSurfaceHorizonVeilColor();
        }

        private Color ResolveOceanSkyTowardsSunColor()
        {
            Material activeSkyMaterial = ResolveActiveSkyMaterial();
            Color sunScatterColor = activeSkyMaterial != null && activeSkyMaterial.HasProperty(_ID_SunScatterColor)
                ? activeSkyMaterial.GetColor(_ID_SunScatterColor)
                : _baseSunScatterColor;

            if (sunScatterColor.maxColorComponent <= 0.0001f)
                sunScatterColor = ResolveSurfaceFogColor();

            sunScatterColor.a = 1f;
            return sunScatterColor;
        }

        private float ResolveSurfaceMaterialLightFactor()
        {
            float directSunFactor = Mathf.Clamp01(ResolveProfileSunIntensity() * ResolveHorizonFade());
            float skyFactor = ResolvePerceivedLuminance(ResolveSurfaceFogColor());
            return Mathf.Clamp01(Mathf.Max(directSunFactor, skyFactor));
        }

        private void ApplyOceanSkyBinding(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            if (targetMaterial.HasProperty(_ID_ProceduralSky))
                targetMaterial.SetFloat(_ID_ProceduralSky, 1f);

            targetMaterial.EnableKeyword(ProceduralSkyKeyword);

            Color horizonVeilColor = ResolveSurfaceHorizonVeilColor();
            Color oceanHorizonMergeColor = ResolveSurfaceOceanHorizonMergeColor();
            Color skyBase = ResolveSafeSkyBindingColor(
                Color.Lerp(ResolveSurfaceFogColor(), oceanHorizonMergeColor, oceanSkyBaseFogLink),
                targetMaterial,
                _ID_SkyBase,
                horizonVeilColor);
            Color skyAwayFromSun = ResolveSafeSkyBindingColor(
                ResolveSurfaceSkyZenithColor(),
                targetMaterial,
                _ID_SkyAwayFromSun,
                skyBase);
            Color skyTowardsSun = ResolveSafeSkyBindingColor(
                ResolveOceanSkyTowardsSunColor(),
                targetMaterial,
                _ID_SkyTowardsSun,
                Color.Lerp(skyBase, skyAwayFromSun, 0.35f));

            SetMaterialColorIfPresent(targetMaterial, _ID_SkyBase, skyBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_SkyAwayFromSun, skyAwayFromSun);
            SetMaterialColorIfPresent(targetMaterial, _ID_SkyTowardsSun, skyTowardsSun);
            SetMaterialFloatIfPresent(targetMaterial, _ID_SkyDirectionality, OceanSkyDirectionality);
        }

        private static float ResolvePerceivedLuminance(Color color)
        {
            float luminance =
                (color.r * 0.2126f) +
                (color.g * 0.7152f) +
                (color.b * 0.0722f);
            return Mathf.Clamp01(luminance);
        }

        private static Color LiftColorTowardsLuminance(Color color, float targetLuminance, float blend)
        {
            float currentLuminance = ResolvePerceivedLuminance(color);
            float clampedBlend = Mathf.Clamp01(blend);

            if (currentLuminance <= 0.0001f)
            {
                Color fallbackLift = Color.Lerp(
                    color,
                    new Color(targetLuminance, targetLuminance, targetLuminance, 1f),
                    clampedBlend);
                fallbackLift.a = 1f;
                return fallbackLift;
            }

            float targetScale = Mathf.Max(0f, targetLuminance / currentLuminance);
            Color lifted = ScaleColorRgb(color, LerpClamped(1f, targetScale, clampedBlend));
            lifted.a = 1f;
            return lifted;
        }

        private static Color LiftColorToMinimumLuminance(Color color, float minimumLuminance, float blend)
        {
            return ResolvePerceivedLuminance(color) >= minimumLuminance
                ? color
                : LiftColorTowardsLuminance(color, minimumLuminance, blend);
        }

        private static Color ResolveSurfaceReadableDaylightColor(Color color, float blend)
        {
            Color daylight = Color.Lerp(
                color,
                SurfaceOceanDaylightReadableTint,
                Mathf.Clamp01(blend));
            daylight.a = 1f;
            return daylight;
        }

        private bool IsOceanUnderwaterRequiredForMaterial(Material targetMaterial)
        {
            if (targetMaterial == null)
                return false;

            Material oceanMaterial = ResolveOceanMaterial();

            if (!ReferenceEquals(targetMaterial, oceanMaterial))
                return false;

            if (!_cachedVisualIsUnderwater)
                return false;

            if (_mainCameraUnderwaterPass != null)
                return true;

            if (HasUnderwaterPassInstance())
                return true;

            return false;
        }

        private bool IsOceanUnderwaterSupportRequiredForMaterial(Material targetMaterial)
        {
            if (targetMaterial == null)
                return false;

            Material oceanMaterial = ResolveOceanMaterial();

            if (!ReferenceEquals(targetMaterial, oceanMaterial))
                return false;

            if (_mainCameraUnderwaterPass != null)
                return true;

            return HasUnderwaterPassInstance();
        }

        private static Color ScaleColorRgb(Color color, float multiplier)
        {
            Color scaled = color;
            scaled.r *= multiplier;
            scaled.g *= multiplier;
            scaled.b *= multiplier;
            scaled.a = 1f;
            return scaled;
        }

        private static Color MaxColorRgb(Color left, Color right)
        {
            return new Color(
                Mathf.Max(left.r, right.r),
                Mathf.Max(left.g, right.g),
                Mathf.Max(left.b, right.b),
                1f);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  SURFACE DEFAULTS
        //
        //  v5.1: ApplySurfaceDefaults writes sunLight.intensity
        //  as profile Ãƒâ€” horizon. CelestialEngine will multiply
        //  by eclipse visibility afterward (runs later in tick order).
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplySurfaceDefaults()
        {
            bool hasSurfaceAtmosphereState = HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState);
            bool giRelayAmbientAuthority = IsGIRelayAmbientAuthorityActive();

            // Ã¢â€â‚¬Ã¢â€â‚¬ Sun intensity: base for CelestialEngine to multiply Ã¢â€â‚¬Ã¢â€â‚¬
            if (sunLight != null)
            {
                if (hasSurfaceAtmosphereState)
                {
                    sunLight.intensity = ResolveReadableSurfaceSunIntensity(surfaceState.DirectionalLightIntensity);
                }
                else
                {
                    float baseSun = ResolveProfileSunIntensity();
                    float horizon = ResolveHorizonFade();
                    sunLight.intensity = ResolveReadableSurfaceSunIntensity(baseSun * horizon * ResolveSurfaceSunMultiplier());
                }
            }

            DisableLegacySunFlare();

            HideSunVisualAboveWater();

            if (hasSurfaceAtmosphereState)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveReadableSurfaceFogDensity(surfaceState.FogDensity);
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = ResolveReadableSurfaceAmbientColor(surfaceState.AmbientSkyColor, SurfaceReadableSkyAmbientFloor);
                    RenderSettings.ambientEquatorColor = ResolveReadableSurfaceAmbientColor(surfaceState.AmbientEquatorColor, SurfaceReadableEquatorAmbientFloor);
                    RenderSettings.ambientGroundColor = ResolveReadableSurfaceAmbientColor(surfaceState.AmbientGroundColor, SurfaceReadableGroundAmbientFloor);
                    RenderSettings.ambientIntensity = ResolveReadableSurfaceAmbientIntensity(surfaceState.AmbientIntensity);
                }
            }
            else if (enableSurfaceFog)
            {
                RenderSettings.fog        = true;
                RenderSettings.fogMode    = FogMode.ExponentialSquared;
                RenderSettings.fogColor   = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveReadableSurfaceFogDensity(ResolveSurfaceFogDensity());
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode  = AmbientMode.Flat;
                    RenderSettings.ambientLight = ResolveReadableSurfaceAmbientColor(ResolveSurfaceAmbientColor(), SurfaceReadableSkyAmbientFloor);
                }
            }
            else
            {
                RenderSettings.fog = false;
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode  = AmbientMode.Flat;
                    RenderSettings.ambientLight = ResolveReadableSurfaceAmbientColor(ResolveSurfaceAmbientColor(), SurfaceReadableSkyAmbientFloor);
                }
            }

            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Skybox);

            if (biomePalette != null)
            {
                HectonBiomeProfile surfProfile = biomePalette.SurfaceProfile;
                if (surfProfile != null)
                {
                    _currentScatterBase     = ResolveProfileScatterBase(surfProfile);
                    _currentScatterShallow  = ResolveProfileScatterShallow(surfProfile);
                    _currentDepthFogDensity = ResolveProfileDepthFogDensity(surfProfile);
                    _cachedVisualDepth = 0f;
                    _cachedVisualIsUnderwater = false;
                    _cachedCausticsStrength = 0f;
                    ApplyOceanMaterialBindings();
                }
            }
        }

        private void RestoreCameraDefaults()
        {
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Skybox);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  BIOME INTERPOLATION
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void RefreshTargetsFromCurrentProfile()
        {
            HectonBiomeProfile matrixOverride = ResolveActiveMatrixRuntimeVisualProfile();
            if (matrixOverride != null)
            {
                _targetScatterBase     = ResolveProfileScatterBase(matrixOverride);
                _targetScatterShallow  = ResolveProfileScatterShallow(matrixOverride);
                _targetDepthFogDensity = ResolveProfileDepthFogDensity(matrixOverride);
                _targetFogColor        = ResolveProfileFogColor(matrixOverride);
                _targetTurbidity       = ResolveProfileTurbidity(matrixOverride);
                _targetAmbientColor    = underwaterAmbientColor;
                return;
            }

            if (biomePalette == null) return;

            HectonBiomeProfile currentProf = biomePalette.GetProfile(_targetBiomeIndex);
            if (currentProf == null)
            {
                currentProf = biomePalette.GetProfile(0);
                if (currentProf == null) return;
            }

            _targetScatterBase     = ResolveProfileScatterBase(currentProf);
            _targetScatterShallow  = ResolveProfileScatterShallow(currentProf);
            _targetDepthFogDensity = ResolveProfileDepthFogDensity(currentProf);
            _targetFogColor        = ResolveProfileFogColor(currentProf);
            _targetTurbidity       = ResolveProfileTurbidity(currentProf);
            _targetAmbientColor    = underwaterAmbientColor;
        }

        private void InterpolateBiomeParameters(float lerpT)
        {
            _currentScatterBase = Color.Lerp(
                _currentScatterBase, _targetScatterBase, lerpT);
            _currentScatterShallow = Color.Lerp(
                _currentScatterShallow, _targetScatterShallow, lerpT);
            _currentDepthFogDensity = Vector3.Lerp(
                _currentDepthFogDensity, _targetDepthFogDensity, lerpT);
            _currentFogColor = Color.Lerp(
                _currentFogColor, _targetFogColor, lerpT);
            _currentTurbidity = LerpClamped(
                _currentTurbidity, _targetTurbidity, lerpT);
            _currentBiomeFogDensityScale = LerpClamped(
                _currentBiomeFogDensityScale, _targetBiomeFogDensityScale, lerpT);
            biomeAbsorption = LerpClamped(
                biomeAbsorption, _targetBiomeAbsorption, lerpT);
            _currentAmbientColor = Color.Lerp(
                _currentAmbientColor, _targetAmbientColor, lerpT);

            float dist = ColorDistanceManhattan(
                _currentScatterBase, _targetScatterBase);
            _transitionProgress = 1f - math.saturate(dist * 10f);

#if UNITY_EDITOR
            _debugTransitionProgress = _transitionProgress;
            _debugTurbidity          = _currentTurbidity;
#endif
        }

        private void CaptureBiomeFogTransition(
            HectonBiomeMatrixProfile previousProfile,
            HectonBiomeMatrixProfile nextProfile)
        {
            HectonBiomeProfile fromProfile = ResolveMatrixRuntimeVisualProfile(previousProfile);
            if (fromProfile == null)
                fromProfile = _matrixRuntimeVisualProfile != null
                    ? _matrixRuntimeVisualProfile
                    : ResolvePaletteProfile(_targetBiomeIndex);

            HectonBiomeProfile toProfile = ResolveMatrixRuntimeVisualProfile(nextProfile);
            if (toProfile == null)
                toProfile = ResolvePaletteProfile(_targetBiomeIndex);

            _biomeFogFromProfile = fromProfile;
            _biomeFogToProfile = toProfile;
            _biomeFogFromId = ResolveMatrixVisualFamilyByte(previousProfile, _targetBiomeIndex);
            _biomeFogToId = ResolveMatrixVisualFamilyByte(nextProfile, _targetBiomeIndex);

            bool sameFogFamily = _biomeFogFromId == _biomeFogToId;
            _biomeFogFallbackBlend01 = ReferenceEquals(fromProfile, toProfile) || sameFogFamily ? 1f : 0f;
            _biomeFogTransitionActive = fromProfile != null && toProfile != null && !sameFogFamily;

            CaptureBiomeFogTransitionAnchors();
        }

        private void CaptureBiomeFogTransitionAnchors()
        {
            Transform cameraTransform = playerCamera;
            Vector3 center = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            forward = ResolveSafeDirection(forward, Vector3.forward);

            float halfLength = Mathf.Max(4f, biomeFogTransitionLengthMeters) * 0.5f;
            _biomeFogTransitionFromAup = BuildAupFromRuntimePosition(center - forward * halfLength);
            _biomeFogTransitionToAup = BuildAupFromRuntimePosition(center + forward * halfLength);
        }

        private void ApplyBiomeFogBlend(float lerpT)
        {
            if (!_runtimeVisualCallbacksActive ||
                !_biomeFogTransitionActive ||
                _biomeFogFromProfile == null ||
                _biomeFogToProfile == null)
            {
                return;
            }

            Transform cameraTransform = playerCamera;
            Vector3 playerPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            _biomeFogFallbackBlend01 = math.saturate(_biomeFogFallbackBlend01 + math.max(0.001f, lerpT));

            BiomeTransitionSample sample = default;
            sample.FromBiomeId = _biomeFogFromId;
            sample.ToBiomeId = _biomeFogToId;
            sample.Blend255 = (byte)math.clamp((int)(_biomeFogFallbackBlend01 * 255f + 0.5f), 0, 255);
            sample.Flags = 0;

            BiomeTransitionFogSource from = BuildBiomeFogSource(_biomeFogFromId, _biomeFogFromProfile);
            BiomeTransitionFogSource to = BuildBiomeFogSource(_biomeFogToId, _biomeFogToProfile);
            AbsoluteUniversePositionBlit128 playerAup = BuildAupFromRuntimePosition(playerPosition);
            float blend = ResolveBiomeFogAupBlend(
                in _biomeFogTransitionFromAup,
                in _biomeFogTransitionToAup,
                in playerAup,
                sample.Blend255 * (1f / 255f),
                Mathf.Max(4f, biomeFogTransitionLengthMeters));
            float smoothBlend = BiomeTransitionMath.Smooth01(blend);
            sample.Blend255 = (byte)math.round(math.saturate(smoothBlend) * 255f);

            BiomeTransitionFogResult result = default;
            result.Sample = sample;
            result.FogColor = math.lerp(from.FogColor, to.FogColor, smoothBlend);
            result.Density = math.lerp(from.Density, to.Density, smoothBlend);
            result.Turbidity = math.lerp(from.Turbidity, to.Turbidity, smoothBlend);
            result.Absorption = math.lerp(from.Absorption, to.Absorption, smoothBlend);
            result.FogAttenuationDistance = math.max(
                0.001f,
                math.lerp(from.FogAttenuationDistance, to.FogAttenuationDistance, smoothBlend));
            result.NormalizedWeightSum = 1f;
            CommitBiomeFogBlendResult(in result);
        }

        private static float ResolveBiomeFogAupBlend(
            in AbsoluteUniversePositionBlit128 fromAup,
            in AbsoluteUniversePositionBlit128 toAup,
            in AbsoluteUniversePositionBlit128 playerAup,
            float fallbackBlend,
            float transitionLengthMeters)
        {
            double3 from = BiomeTransitionMath.ToAbsoluteDouble3(in fromAup);
            double3 to = BiomeTransitionMath.ToAbsoluteDouble3(in toAup);
            double3 player = BiomeTransitionMath.ToAbsoluteDouble3(in playerAup);
            float3 segment = (float3)(to - from);
            float3 playerFrom = (float3)(player - from);
            float lengthSq = math.lengthsq(segment);
            if (lengthSq <= BiomeTransitionConstants.NaNEpsilon)
                return math.saturate(fallbackBlend);

            float projected = math.dot(playerFrom, segment) * math.rcp(math.max(lengthSq, BiomeTransitionConstants.NaNEpsilon));
            float segmentBlend = math.saturate(projected);
            float transitionLength = math.max(0.001f, transitionLengthMeters);
            float transitionLengthSq = transitionLength * transitionLength;
            float halfWindow = math.saturate(transitionLengthSq * math.rcp(math.max(lengthSq, BiomeTransitionConstants.NaNEpsilon))) * 0.5f;
            float lower = math.max(0f, 0.5f - halfWindow);
            float upper = math.min(1f, 0.5f + halfWindow);
            float remapped = math.saturate((segmentBlend - lower) * math.rcp(math.max(0.001f, upper - lower)));
            return math.max(remapped, math.saturate(fallbackBlend));
        }

        private void CommitBiomeFogBlendResult(in BiomeTransitionFogResult result)
        {
            _currentFogColor = ToColor(result.FogColor);
            _currentTurbidity = Mathf.Clamp(result.Turbidity, 0.5f, 2f);
            _currentBiomeFogDensityScale = Mathf.Clamp(result.Density, 0.5f, 2f);
            biomeAbsorption = Mathf.Clamp01(result.Absorption);
            _transitionProgress = math.max(_transitionProgress, result.Sample.Blend255 * (1f / 255f));

            if (result.Sample.Blend255 >= 254)
            {
                _biomeFogTransitionActive = false;
                _biomeFogFallbackBlend01 = 1f;
            }
        }

        private BiomeTransitionFogSource BuildBiomeFogSource(byte visualFamilyId, HectonBiomeProfile profile)
        {
            BiomeTransitionFogSource source = default;
            source.FogColor = ToFloat4(ResolveVisualFamilyFogColor(visualFamilyId));
            source.Density = ResolveProfileFogDensityScale(profile);
            source.Turbidity = ResolveProfileTurbidity(profile);
            source.Absorption = ResolveProfileAbsorption(profile);
            return source;
        }

        private HectonBiomeProfile ResolveMatrixRuntimeVisualProfile(HectonBiomeMatrixProfile profile)
        {
            return profile != null ? profile.runtimeVisualProfile : null;
        }

        private HectonBiomeProfile ResolvePaletteProfile(int biomeIndex)
        {
            if (biomePalette == null)
                return null;

            HectonBiomeProfile profile = biomePalette.GetProfile(biomeIndex);
            if (profile != null)
                return profile;

            return biomePalette.Count > 0 ? biomePalette.GetProfile(0) : null;
        }

        private static byte ResolveMatrixVisualFamilyByte(HectonBiomeMatrixProfile profile, int fallbackBiomeIndex)
        {
            int biomeId = profile != null ? profile.matrixIndex : fallbackBiomeIndex;
            return HectonBiomeVisualFamilyUtility.MapToVisualFamily(biomeId);
        }

        private static Color ResolveVisualFamilyFogColor(byte visualFamilyId)
        {
            switch ((VisualFamily)(visualFamilyId & 0x7))
            {
                case VisualFamily.Sand:
                    return new Color(0.015f, 0.32f, 0.34f, 1f);
                case VisualFamily.Rock:
                    return new Color(0.025f, 0.04f, 0.05f, 1f);
                case VisualFamily.Vegetation:
                    return new Color(0.0f, 0.24f, 0.18f, 1f);
                case VisualFamily.Coral:
                    return new Color(0.08f, 0.18f, 0.20f, 1f);
                case VisualFamily.Ruin:
                    return new Color(0.035f, 0.045f, 0.05f, 1f);
                case VisualFamily.Volcanic:
                    return new Color(0.18f, 0.045f, 0.02f, 1f);
                case VisualFamily.Void:
                    return new Color(0.003f, 0.002f, 0.007f, 1f);
                default:
                    return new Color(0.002f, 0.003f, 0.004f, 1f);
            }
        }

        private static AbsoluteUniversePositionBlit128 BuildAupFromRuntimePosition(Vector3 runtimePosition)
        {
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return default;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return default;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return resolvedAup.IsFinite() ? resolvedAup.ToAlignedBlit() : default;
        }

        private static float4 ToFloat4(Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        private static Color ToColor(float4 color)
        {
            return new Color(color.x, color.y, color.z, color.w);
        }

        private void ApplyOceanMaterialBindings()
        {
            ApplyOceanMaterialBindings(oceanUnderwaterMaterial, true);

            Material oceanMaterial = ResolveOceanMaterial();
            if (oceanMaterial != null && !ReferenceEquals(oceanMaterial, oceanUnderwaterMaterial))
                ApplyOceanMaterialBindings(oceanMaterial, false);
        }

        private void ApplyOceanMaterialBindings(Material targetMaterial, bool underwaterMaterial)
        {
            if (targetMaterial == null)
                return;

            bool crestUnderwaterRequired = underwaterMaterial || IsOceanUnderwaterRequiredForMaterial(targetMaterial);
            bool crestUnderwaterSupportRequired =
                underwaterMaterial || IsOceanUnderwaterSupportRequiredForMaterial(targetMaterial);
            bool sharedOceanFeedsUnderwater = crestUnderwaterRequired && !underwaterMaterial;
            Material scatterSourceMaterial = sharedOceanFeedsUnderwater && oceanUnderwaterMaterial != null
                ? oceanUnderwaterMaterial
                : targetMaterial;

            float materialLightFactor = underwaterMaterial
                ? Mathf.Clamp01(_cachedLightFactor)
                : ResolveSurfaceMaterialLightFactor();
            float scatterLuminanceFloor;
            if (underwaterMaterial)
            {
                scatterLuminanceFloor = UnderwaterScatterLuminanceFloor;
            }
            else if (sharedOceanFeedsUnderwater)
            {
                scatterLuminanceFloor = SharedOceanUnderwaterScatterLuminanceFloor;
            }
            else
            {
                scatterLuminanceFloor = SurfaceScatterLuminanceFloor;
            }
            float scatterIntensity = LerpClamped(scatterLuminanceFloor, 1f, materialLightFactor);

            Color horizonVeilColor = ResolveSurfaceHorizonVeilColor();
            Color oceanHorizonMergeColor = ResolveSurfaceOceanHorizonMergeColor();
            Color zenithSkyColor = ResolveSurfaceSkyZenithColor();
            Color sunSkyColor = ResolveOceanSkyTowardsSunColor();

            Color sourceScatterBase = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_Diffuse,
                new Color(0f, 0.03f, 0.07f, 1f));
            Color sourceScatterShallow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceShallowCol,
                new Color(0f, 0.15f, 0.12f, 1f));

            Color scatterBase = ResolveSafeOceanColor(
                _currentScatterBase,
                sourceScatterBase);
            Color scatterShallow = ResolveSafeOceanColor(
                _currentScatterShallow,
                sourceScatterShallow);

            ResolveOceanScatterColors(
                underwaterMaterial,
                sharedOceanFeedsUnderwater,
                materialLightFactor,
                scatterIntensity,
                scatterLuminanceFloor,
                sourceScatterBase,
                sourceScatterShallow,
                zenithSkyColor,
                oceanHorizonMergeColor,
                sunSkyColor,
                horizonVeilColor,
                ref scatterBase,
                ref scatterShallow);

            ResolveOceanShadowColors(
                scatterSourceMaterial,
                underwaterMaterial,
                sharedOceanFeedsUnderwater,
                scatterBase,
                scatterShallow,
                out Color diffuseShadow,
                out Color shallowShadow);

            ResolveOceanSubsurfaceProperties(
                scatterSourceMaterial,
                underwaterMaterial,
                sharedOceanFeedsUnderwater,
                out float subSurfaceBaseIntensity,
                out float subSurfaceSunIntensity,
                out float subSurfaceSunFalloff);

            ResolveOceanDepthFogDensity(
                targetMaterial,
                underwaterMaterial,
                out Vector3 depthFogDensity);

            ApplyOceanSkyBinding(targetMaterial);

            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourBase, scatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_Diffuse, scatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseGrazing, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseShadow, diffuseShadow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceColour, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourShallow, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowCol, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowColShadow, shallowShadow);
            SetMaterialVectorIfPresent(
                targetMaterial,
                _ID_DepthFogDensity,
                new Vector4(
                    depthFogDensity.x,
                    depthFogDensity.y,
                    depthFogDensity.z,
                    0f));
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_Caustics,
                _cachedCausticsStrength > 0.001f ? 1f : 0f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_CausticsStrength,
                _cachedCausticsStrength);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceScattering,
                1f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceBase,
                subSurfaceBaseIntensity);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceSun,
                subSurfaceSunIntensity);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceSunFallOff,
                subSurfaceSunFalloff);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceShallowColour,
                1f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_Underwater,
                crestUnderwaterSupportRequired ? 1f : 0f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_CullMode,
                crestUnderwaterSupportRequired
                    ? (float)UnityEngine.Rendering.CullMode.Off
                    : (float)UnityEngine.Rendering.CullMode.Back);

            if (crestUnderwaterSupportRequired)
                targetMaterial.EnableKeyword(UnderwaterKeyword);
            else
                targetMaterial.DisableKeyword(UnderwaterKeyword);

            ApplyOceanUnderwaterGlobals(
                _runtimeVisualCallbacksActive,
                targetMaterial,
                depthFogDensity,
                scatterBase,
                scatterShallow,
                diffuseShadow,
                subSurfaceSunIntensity,
                subSurfaceBaseIntensity,
                subSurfaceSunFalloff);

            if (_giRelaySurfaceEmissionActive)
                ApplyGIRelaySurfaceEmissionToMaterial(targetMaterial, _giRelaySurfaceEmissionColor);
        }

        private void ResolveOceanScatterColors(
            bool underwaterMaterial,
            bool sharedOceanFeedsUnderwater,
            float materialLightFactor,
            float scatterIntensity,
            float scatterLuminanceFloor,
            Color sourceScatterBase,
            Color sourceScatterShallow,
            Color zenithSkyColor,
            Color oceanHorizonMergeColor,
            Color sunSkyColor,
            Color horizonVeilColor,
            ref Color scatterBase,
            ref Color scatterShallow)
        {
            if (sharedOceanFeedsUnderwater)
            {
                Color underwaterFogColor = ResolveBaseUnderwaterFogColor();
                Color sharedBaseSeed = Color.Lerp(sourceScatterBase, underwaterFogColor, 0.72f);
                Color sharedShallowSeed = Color.Lerp(
                    sourceScatterShallow,
                    Color.Lerp(underwaterFogColor, oceanHorizonMergeColor, 0.58f),
                    0.78f);
                sharedBaseSeed.a = 1f;
                sharedShallowSeed.a = 1f;
                scatterBase = MaxColorRgb(scatterBase, sharedBaseSeed);
                scatterShallow = MaxColorRgb(scatterShallow, sharedShallowSeed);
            }

            scatterBase = ScaleColorRgb(scatterBase, scatterIntensity);
            scatterShallow = ScaleColorRgb(scatterShallow, LerpClamped(scatterLuminanceFloor * 1.15f, 1f, materialLightFactor));

            if (underwaterMaterial && !_cachedVisualIsUnderwater)
            {
                Color surfaceReadableBase = ScaleColorRgb(
                    Color.Lerp(ResolveBaseUnderwaterFogColor(), zenithSkyColor, 0.42f),
                    0.72f + (materialLightFactor * 0.22f));
                Color surfaceReadableShallow = ScaleColorRgb(
                    Color.Lerp(oceanHorizonMergeColor, sunSkyColor, 0.22f),
                    0.82f + (materialLightFactor * 0.28f));
                scatterBase = MaxColorRgb(scatterBase, surfaceReadableBase);
                scatterShallow = MaxColorRgb(scatterShallow, surfaceReadableShallow);
            }

            if (!underwaterMaterial)
            {
                Color surfaceBaseFloor;
                Color surfaceShallowFloor;

                if (sharedOceanFeedsUnderwater)
                {
                    Color underwaterFogColor = ResolveBaseUnderwaterFogColor();
                    surfaceBaseFloor = ScaleColorRgb(
                        Color.Lerp(underwaterFogColor, zenithSkyColor, 0.22f),
                        0.64f + (materialLightFactor * 0.18f));
                    surfaceShallowFloor = ScaleColorRgb(
                        Color.Lerp(underwaterFogColor, oceanHorizonMergeColor, 0.58f),
                        0.74f + (materialLightFactor * 0.20f));
                }
                else
                {
                    Color surfaceBaseHorizonTarget = Color.Lerp(
                        horizonVeilColor,
                        oceanHorizonMergeColor,
                        0.28f);
                    Color surfaceBaseSeed = Color.Lerp(
                        zenithSkyColor,
                        surfaceBaseHorizonTarget,
                        surfaceOceanBaseFogBlend);
                    float surfaceBaseFloorMultiplier =
                        (SurfaceOceanBaseFloorMin + (materialLightFactor * SurfaceOceanBaseFloorLightSpan)) *
                        (1f + (surfaceOceanHorizonLuminanceLift * SurfaceOceanBaseHorizonLiftScale));
                    surfaceBaseFloor = ScaleColorRgb(
                        surfaceBaseSeed,
                        surfaceBaseFloorMultiplier);

                    Color surfaceShallowSeed = Color.Lerp(
                        oceanHorizonMergeColor,
                        sunSkyColor,
                        surfaceOceanSunScatterBlend);
                    surfaceShallowSeed = Color.Lerp(
                        surfaceShallowSeed,
                        Color.Lerp(oceanHorizonMergeColor, horizonVeilColor, 0.18f),
                        surfaceOceanHorizonFogBlend);
                    surfaceShallowFloor = ScaleColorRgb(
                        surfaceShallowSeed,
                        (0.46f + (materialLightFactor * 0.24f)) *
                        (1f + surfaceOceanHorizonLuminanceLift));
                }

                scatterBase = MaxColorRgb(scatterBase, surfaceBaseFloor);
                scatterShallow = MaxColorRgb(scatterShallow, surfaceShallowFloor);
            }

            if (!underwaterMaterial && !sharedOceanFeedsUnderwater)
            {
                scatterBase = LiftColorToMinimumLuminance(
                    scatterBase,
                    SurfaceOceanBaseLuminanceFloor,
                    SurfaceOceanLuminanceFloorBlend);
                scatterShallow = LiftColorToMinimumLuminance(
                    scatterShallow,
                    SurfaceOceanShallowLuminanceFloor,
                    SurfaceOceanLuminanceFloorBlend);
                scatterBase = ResolveSurfaceReadableDaylightColor(
                    scatterBase,
                    SurfaceOceanBaseDaylightBlueBias);
                scatterShallow = ResolveSurfaceReadableDaylightColor(
                    scatterShallow,
                    SurfaceOceanShallowDaylightBlueBias);
            }
        }

        private void ResolveOceanShadowColors(
            Material scatterSourceMaterial,
            bool underwaterMaterial,
            bool sharedOceanFeedsUnderwater,
            Color scatterBase,
            Color scatterShallow,
            out Color diffuseShadow,
            out Color shallowShadow)
        {
            Color diffuseShadowFallback = sharedOceanFeedsUnderwater
                ? Color.Lerp(scatterBase, Color.black, 0.22f)
                : Color.Lerp(scatterBase, Color.black, 0.45f);
            Color shallowShadowFallback = sharedOceanFeedsUnderwater
                ? Color.Lerp(scatterShallow, scatterBase, 0.18f)
                : Color.Lerp(scatterShallow, scatterBase, 0.35f);
            diffuseShadow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_DiffuseShadow,
                diffuseShadowFallback);
            shallowShadow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceShallowColShadow,
                shallowShadowFallback);
            if (underwaterMaterial && !_cachedVisualIsUnderwater)
            {
                diffuseShadow = MaxColorRgb(diffuseShadow, Color.Lerp(scatterBase, Color.black, 0.12f));
                shallowShadow = MaxColorRgb(shallowShadow, Color.Lerp(scatterShallow, scatterBase, 0.12f));
            }
            if (sharedOceanFeedsUnderwater)
            {
                diffuseShadow = MaxColorRgb(diffuseShadow, diffuseShadowFallback);
                shallowShadow = MaxColorRgb(shallowShadow, shallowShadowFallback);
            }
            else if (!underwaterMaterial)
            {
                diffuseShadow = MaxColorRgb(
                    diffuseShadow,
                    Color.Lerp(scatterBase, Color.black, SurfaceOceanDiffuseShadowBlackBlend));
                shallowShadow = MaxColorRgb(
                    shallowShadow,
                    Color.Lerp(scatterShallow, scatterBase, SurfaceOceanShallowShadowBaseBlend));
                diffuseShadow = LiftColorToMinimumLuminance(
                    diffuseShadow,
                    SurfaceOceanShadowLuminanceFloor,
                    SurfaceOceanShadowLuminanceFloorBlend);
                shallowShadow = LiftColorToMinimumLuminance(
                    shallowShadow,
                    SurfaceOceanShallowShadowLuminanceFloor,
                    SurfaceOceanShadowLuminanceFloorBlend);
                diffuseShadow = ResolveSurfaceReadableDaylightColor(
                    diffuseShadow,
                    SurfaceOceanShadowDaylightBlueBias);
                shallowShadow = ResolveSurfaceReadableDaylightColor(
                    shallowShadow,
                    SurfaceOceanShallowShadowDaylightBlueBias);
            }
        }

        private void ResolveOceanSubsurfaceProperties(
            Material scatterSourceMaterial,
            bool underwaterMaterial,
            bool sharedOceanFeedsUnderwater,
            out float subSurfaceBaseIntensity,
            out float subSurfaceSunIntensity,
            out float subSurfaceSunFalloff)
        {
            subSurfaceBaseIntensity = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceBase,
                sharedOceanFeedsUnderwater ? 0f : 0.33f);
            subSurfaceSunIntensity = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceSun,
                sharedOceanFeedsUnderwater ? 0.22f : 1.13f);
            subSurfaceSunFalloff = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceSunFallOff,
                sharedOceanFeedsUnderwater ? 5.26f : 7.11f);

            if (!underwaterMaterial && !sharedOceanFeedsUnderwater)
            {
                float horizonSubsurfaceBias = Mathf.Clamp01(
                    (surfaceOceanHorizonFogBlend * 0.65f) +
                    (surfaceOceanSunScatterBlend * 0.35f));
                subSurfaceBaseIntensity = Mathf.Max(
                    subSurfaceBaseIntensity,
                    LerpClamped(0.38f, 0.72f, horizonSubsurfaceBias));
                subSurfaceSunIntensity = Mathf.Max(
                    subSurfaceSunIntensity,
                    LerpClamped(1.25f, 1.95f, horizonSubsurfaceBias));
                subSurfaceSunFalloff = Mathf.Min(
                    subSurfaceSunFalloff,
                    LerpClamped(6.2f, 4.8f, horizonSubsurfaceBias));
            }
            else if (underwaterMaterial && !_cachedVisualIsUnderwater)
            {
                subSurfaceBaseIntensity = Mathf.Max(subSurfaceBaseIntensity, 0.38f);
                subSurfaceSunIntensity = Mathf.Max(subSurfaceSunIntensity, 1.18f);
                subSurfaceSunFalloff = Mathf.Min(subSurfaceSunFalloff, 5.4f);
            }
        }

        private void ResolveOceanDepthFogDensity(
            Material targetMaterial,
            bool underwaterMaterial,
            out Vector3 depthFogDensity)
        {
            depthFogDensity = ResolveSafeDepthFogDensity(targetMaterial);
            Vector3 authoredDepthFogDensity = ResolveAuthoredDepthFogDensity(
                targetMaterial,
                ResolveFallbackDepthFogDensity(targetMaterial));
            float authoredDensityFloorScale =
                underwaterMaterial || _cachedVisualIsUnderwater
                    ? 1f
                    : 0.68f;
            depthFogDensity = new Vector3(
                Mathf.Max(depthFogDensity.x, authoredDepthFogDensity.x * authoredDensityFloorScale),
                Mathf.Max(depthFogDensity.y, authoredDepthFogDensity.y * authoredDensityFloorScale),
                Mathf.Max(depthFogDensity.z, authoredDepthFogDensity.z * authoredDensityFloorScale));
            if (!_cachedVisualIsUnderwater)
            {
                depthFogDensity = new Vector3(
                    Mathf.Min(depthFogDensity.x, SurfaceReadableOceanDepthFogCeiling),
                    Mathf.Min(depthFogDensity.y, SurfaceReadableOceanDepthFogCeiling),
                    Mathf.Min(depthFogDensity.z, SurfaceReadableOceanDepthFogCeiling));
            }
        }


        private void ApplyOceanUnderwaterGlobals(
            bool runtimeCallbacksActive,
            Material targetMaterial,
            Vector3 depthFogDensity,
            Color diffuse,
            Color diffuseGrazing,
            Color diffuseShadow,
            float subSurfaceSun,
            float subSurfaceBase,
            float subSurfaceSunFalloff)
        {
            if (!runtimeCallbacksActive || targetMaterial == null)
                return;

            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge == null)
                return;

            bridge.ApplyUnderwaterGlobals(
                targetMaterial,
                depthFogDensity,
                diffuse,
                diffuseGrazing,
                diffuseShadow,
                subSurfaceSun,
                subSurfaceBase,
                subSurfaceSunFalloff);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  BIOME EVENT
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void HandleBiomeChanged(int biomeIndex)
        {
            _targetBiomeIndex = biomeIndex;
            if (_matrixRuntimeVisualProfile != null)
            {
#if UNITY_EDITOR
                _debugTargetBiome = _targetBiomeIndex;
#endif
                return;
            }

            ApplyBiomePaletteTarget(biomeIndex);
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HectonBiomeMatrixProfile previousFogProfile = _activeMatrixFogProfile;
            if (previousFogProfile == null)
            {
                _activeMatrixFogProfile = profile;
                _biomeFogTransitionActive = false;
                _biomeFogFallbackBlend01 = 1f;
            }
            else if (!ReferenceEquals(previousFogProfile, profile))
            {
                CaptureBiomeFogTransition(previousFogProfile, profile);
                _activeMatrixFogProfile = profile;
            }

            HectonBiomeProfile nextOverride = profile != null ? profile.runtimeVisualProfile : null;
            ApplyEcologyContext(profile);
            if (_matrixRuntimeVisualProfile == nextOverride)
                return;

            _matrixRuntimeVisualProfile = nextOverride;
            if (_matrixRuntimeVisualProfile != null)
            {
                SetTargetFromProfile(_matrixRuntimeVisualProfile);
                return;
            }

            ApplyBiomePaletteTarget(_targetBiomeIndex);
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HandleMatrixBiomeChanged(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        void Hecton8.Core.IMapMagicBiomeEventListener.OnMapMagicBiomeChanged(int biomeId)
        {
            HandleBiomeChanged(biomeId);
        }

        private void HandleSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            ApplySoundscapeTierResponse(newTier);
        }

        void Hecton8.World.ISoundscapeEventListener.OnSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            HandleSoundscapeTierChanged(oldTier, newTier);
        }
        private void RefreshSoundscapeTierResponse(bool force)
        {
            SoundscapeSystem soundscape = _soundscapeRuntime;
            SoundscapeTier tier = soundscape != null
                ? soundscape.CurrentTier
                : SoundscapeTier.Shallow;

            ApplySoundscapeTierResponse(tier);
        }

        private void ApplySoundscapeTierResponse(SoundscapeTier tier)
        {
            _currentSoundscapeTier = tier;
            _soundscapeFogDensityScale = 1f;
            _soundscapeAmbientScale = 1f;
            _soundscapeBeamScale = 1f;
            _soundscapeCausticsScale = 1f;
            _soundscapeThermalTintBlend = 0f;

            if (!enableSoundscapeTierResponse)
            {
                UpdateSoundscapeDiagnostics();
                return;
            }

            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    _soundscapeFogDensityScale = twilightTierFogScale;
                    _soundscapeAmbientScale = twilightTierAmbientScale;
                    _soundscapeBeamScale = twilightTierBeamScale;
                    _soundscapeCausticsScale = twilightTierCausticsScale;
                    break;

                case SoundscapeTier.Darkness:
                    _soundscapeFogDensityScale = darknessTierFogScale;
                    _soundscapeAmbientScale = darknessTierAmbientScale;
                    _soundscapeBeamScale = darknessTierBeamScale;
                    _soundscapeCausticsScale = darknessTierCausticsScale;
                    break;

                case SoundscapeTier.Abyss:
                    _soundscapeFogDensityScale = abyssTierFogScale;
                    _soundscapeAmbientScale = abyssTierAmbientScale;
                    _soundscapeBeamScale = abyssTierBeamScale;
                    _soundscapeCausticsScale = abyssTierCausticsScale;
                    break;

                case SoundscapeTier.DeepAbyss:
                    _soundscapeFogDensityScale = deepAbyssTierFogScale;
                    _soundscapeAmbientScale = deepAbyssTierAmbientScale;
                    _soundscapeBeamScale = deepAbyssTierBeamScale;
                    _soundscapeCausticsScale = deepAbyssTierCausticsScale;
                    break;

                case SoundscapeTier.Thermal:
                    _soundscapeFogDensityScale = thermalTierFogScale;
                    _soundscapeAmbientScale = thermalTierAmbientScale;
                    _soundscapeBeamScale = thermalTierBeamScale;
                    _soundscapeCausticsScale = thermalTierCausticsScale;
                    _soundscapeThermalTintBlend = thermalTierTintBlend;
                    break;

                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    break;
            }

            UpdateSoundscapeDiagnostics();
        }

        private void ApplyBiomePaletteTarget(int biomeIndex)
        {
            if (biomePalette == null) return;

            HectonBiomeProfile profile = biomePalette.GetProfile(biomeIndex);
            if (profile == null)
            {
                profile = biomePalette.GetProfile(0);
                _targetBiomeIndex = 0;
            }

            if (profile == null) return;

            SetTargetFromProfile(profile);

#if UNITY_EDITOR
            _debugTargetBiome = _targetBiomeIndex;
#endif
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  PUBLIC API
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public float CurrentDepth
        {
            get
            {
                return ResolveCurrentDepth();
            }
        }

        public float CurrentLightFactor
        {
            get
            {
                float d = CurrentDepth;
                if (d <= 0f) return 1f;
                return ResolveDepthLightFactor(d);
            }
        }

        internal void SetSurfaceWeatherOverride(
            Color fogColor,
            float fogDensity,
            Color ambientColor,
            float sunMultiplier)
        {
            _surfaceWeatherOverrideActive = true;
            _surfaceWeatherFogColor = fogColor;
            _surfaceWeatherFogDensity = Mathf.Max(0f, fogDensity);
            _surfaceWeatherAmbientColor = ambientColor;
            _surfaceWeatherSunMultiplier = Mathf.Max(0f, sunMultiplier);
        }

        internal void ClearSurfaceWeatherOverride()
        {
            _surfaceWeatherOverrideActive = false;
            _surfaceWeatherSunMultiplier = 1f;
        }

        public bool IsUnderwater
        {
            get
            {
                if (Application.isPlaying)
                    return _wasUnderwater;

                return ResolveCurrentDepth() > 0f;
            }
        }

        internal bool TryGetOwnedSkyboxMaterial(out Material ownedSkyboxMaterial)
        {
            ownedSkyboxMaterial = Application.isPlaying && _wasUnderwater
                ? skyMaterial
                : null;
            return ownedSkyboxMaterial != null;
        }

        public float CurrentTurbidity => _currentTurbidity;
        public int TargetBiomeIndex => _targetBiomeIndex;
        public float TransitionProgress => _transitionProgress;
        internal float DebugAdaptiveMotesScale => _debugAdaptiveMotesScale;
        internal float DebugAdaptiveBubbleScale => _debugAdaptiveBubbleScale;
        internal float DebugAdaptiveBeamScale => _debugAdaptiveBeamScale;
        internal float DebugSuspendedMotesEmission => _debugSuspendedMotesEmission;
        internal int DebugExhaleBubbleBurstCount => _debugExhaleBubbleBurstCount;

        public void SetTargetBiome(int biomeIndex) => HandleBiomeChanged(biomeIndex);
        public void SetPlayerCamera(Transform camera) => playerCamera = camera;
        public void SetWaterLevelFallback(float y) => waterLevelFallback = SanitizeVisualWaterLevel(y, DefaultWaterLevelFallback);

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  PRIVATE Ã¢â‚¬â€ INIT
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ResolvePlayerCamera()
        {
            if (Application.isPlaying)
            {
                if (_playerMovement == null && playerCamera != null)
                    TryCachePlayerMovementFromTransformHierarchy(playerCamera, "PlayerCameraHierarchy");

                if (_playerMovement == null && mainCamera != null)
                    TryCachePlayerMovementFromTransformHierarchy(mainCamera.transform, "MainCameraHierarchy");

                if (_playerMovement == null &&
                    GameBootstrapper.TryGetCurrentPlayerTransform(out Transform cachedPlayerTransform))
                {
                    TryCachePlayerMovementFromTransformHierarchy(cachedPlayerTransform, "GameBootstrapper");
                    CachePlayerMovement(cachedPlayerTransform);
                }

                if (playerCamera != null) return;

                if (ResolvePresentationClockSeconds() < _nextRuntimePlayerCameraResolveTime)
                    return;

                _nextRuntimePlayerCameraResolveTime = ResolvePresentationClockSeconds() + RuntimeCameraResolveRetryInterval;
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                {
                    CachePlayerMovement(playerTransform);

                    Camera playerOwnedCamera = ResolveRuntimeMainCamera(playerTransform);
                    if (playerOwnedCamera != null)
                    {
                        playerCamera = playerOwnedCamera.transform;
                        mainCamera = playerOwnedCamera;
                        return;
                    }

                    playerCamera = playerTransform;
                    return;
                }

            }
#if UNITY_EDITOR
            else
            {
                if (playerCamera != null) return;
                ResolveEditorCamera();
            }
#endif
        }

        private void ResolveMainCamera()
        {
            if (mainCamera != null &&
                (!Application.isPlaying || IsRuntimeMainCamera(mainCamera)))
            {
                return;
            }

            mainCamera = null;
            if (Application.isPlaying)
            {
                if (playerCamera != null)
                {
                    playerCamera.TryGetComponent(out Camera playerOwnedCamera);
                    if (IsRuntimeMainCamera(playerOwnedCamera))
                    {
                        mainCamera = playerOwnedCamera;
                        return;
                    }
                }

                if (ResolvePresentationClockSeconds() < _nextRuntimeMainCameraResolveTime)
                    return;

                _nextRuntimeMainCameraResolveTime = ResolvePresentationClockSeconds() + RuntimeCameraResolveRetryInterval;
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                {
                    Camera playerOwnedCamera = ResolveRuntimeMainCamera(playerTransform);
                    if (playerOwnedCamera != null)
                    {
                        mainCamera = playerOwnedCamera;
                        playerCamera = playerOwnedCamera.transform;
                        return;
                    }
                }

                mainCamera = ResolveRuntimeMainCamera();
                if (mainCamera != null)
                {
                    playerCamera = mainCamera.transform;
                    return;
                }

                if (TryGetComponent(out Camera localCamera) && IsRuntimeMainCamera(localCamera))
                {
                    mainCamera = localCamera;
                    return;
                }

                Camera parentCamera = ResolveNearestParentCamera(transform);
                if (IsRuntimeMainCamera(parentCamera))
                    mainCamera = parentCamera;

            }
#if UNITY_EDITOR
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) mainCamera = sv.camera;
            }
#endif

            if (Application.isPlaying)
                EnsureOceanUnderwaterPassOwnership();
        }

        private void ResolveGameplayMainCameraForEditor()
        {
            if (Application.isPlaying)
                return;

            if (_gameplayMainCamera != null)
                return;

            if (playerCamera != null)
            {
                playerCamera.TryGetComponent(out Camera playerOwnedCamera);
                if (playerOwnedCamera != null && HasUnderwaterPass(playerOwnedCamera))
                {
                    _gameplayMainCamera = playerOwnedCamera;
                    return;
                }
            }

            if (mainCamera != null && HasUnderwaterPass(mainCamera))
            {
                _gameplayMainCamera = mainCamera;
                return;
            }

            Camera runtimeMainCamera = ResolveRuntimeMainCamera();
            if (runtimeMainCamera != null)
            {
                _gameplayMainCamera = runtimeMainCamera;
                return;
            }

            Transform root = transform.root;
            if (root == null)
                return;

            Transform mainCameraTransform = root.Find("Main Camera");
            if (mainCameraTransform == null && root.parent != null)
                mainCameraTransform = root.parent.Find("Main Camera");

            if (mainCameraTransform != null)
                mainCameraTransform.TryGetComponent(out _gameplayMainCamera);
        }

#if UNITY_EDITOR
        // Edit-mode only, and guarded to match its own state: _editorGameplaySpaceCamera is declared
        // inside #if UNITY_EDITOR. The body is also unreachable in a player build by construction -
        // Application.isPlaying is constant true there - so there is no player behaviour to preserve.
        // The player/runtime space-camera route is ResolveSpaceCamera() below, which owns _spaceCamera
        // and its own retry interval; it is a separate resolver, not a fallback for this one.
        private void ResolveGameplaySpaceCameraForEditor()
        {
            if (Application.isPlaying)
                return;

            if (ResolveValidCameraReference(ref _editorGameplaySpaceCamera) != null)
                return;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            Transform spaceCameraTransform = _gameplayMainCamera.transform.Find("SpaceCamera");
            if (spaceCameraTransform == null)
                return;

            spaceCameraTransform.TryGetComponent(out _editorGameplaySpaceCamera);
            ResolveValidCameraReference(ref _editorGameplaySpaceCamera);
        }
#endif

        private void ResolveSpaceCamera()
        {
            if (IsCameraReferenceValid(_spaceCamera))
                return;

            _spaceCamera = null;

            if (Application.isPlaying)
            {
                float now = ResolvePresentationClockSeconds();
                if (now < _nextRuntimeSpaceCameraResolveTime)
                    return;

                _nextRuntimeSpaceCameraResolveTime = now + RuntimeCameraResolveRetryInterval;
            }

            Transform spaceCameraTransform = null;
            if (playerCamera != null)
                spaceCameraTransform = playerCamera.Find("SpaceCamera");

            if (spaceCameraTransform == null)
            {
                if (mainCamera == null)
                    return;

                spaceCameraTransform = mainCamera.transform.Find("SpaceCamera");
                if (spaceCameraTransform == null && mainCamera.transform.parent != null)
                    spaceCameraTransform = mainCamera.transform.parent.Find("SpaceCamera");
            }

            if (spaceCameraTransform == null)
                return;

            spaceCameraTransform.TryGetComponent(out Camera resolvedSpaceCamera);
            if (!IsCameraReferenceValid(resolvedSpaceCamera))
            {
                _spaceCamera = null;
                return;
            }

            _spaceCamera = resolvedSpaceCamera;
            if (_spaceCameraMaskCaptured)
                return;

            _spaceCameraOriginalCullingMask = resolvedSpaceCamera.cullingMask;
            _spaceCameraMaskCaptured = true;

            if (Application.isPlaying)
                EnsureOceanUnderwaterPassOwnership();
        }

        private void EnsureOceanUnderwaterPassOwnership()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnsureEditorOceanUnderwaterPassOwnership();
                return;
            }
#endif

            if (mainCamera == null)
                return;

            _mainCameraUnderwaterPass = EnsureUnderwaterPass(mainCamera);
            if (_mainCameraUnderwaterPass == null)
                return;
            if (!IsUnderwaterPassEnabled(_mainCameraUnderwaterPass))
                SetUnderwaterPassEnabled(_mainCameraUnderwaterPass, true);

            SetCopyOceanMaterialParamsEachFrame(_mainCameraUnderwaterPass, true);
            EnsureCameraTextureRequirementsCached(
                mainCamera,
                ref _cachedMainCameraDataCamera,
                ref _cachedMainCameraData,
                ref _cachedMainCameraDataMissing);

            ResolveSpaceCamera();
            PurgeSecondaryUnderwaterPassesIfNeeded();
            EnsureOceanCameraOwnership();
        }

#if UNITY_EDITOR
        private void EnsureEditorOceanUnderwaterPassOwnership()
        {
            EnsureEditorGameplayCameraUnderwaterPass();
            DisableEditorSceneViewUnderwaterPass();
        }

        private void EnsureEditorGameplayCameraUnderwaterPass()
        {
            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            float cameraDepth = math.max(0f, ResolveWaterLevel() - _gameplayMainCamera.transform.position.y);
            bool requiresUnderwaterPass =
                ResolveUnderwaterVisualStateForCameraDepth(cameraDepth, cameraDepth);
            if (!requiresUnderwaterPass)
            {
                _editorOceanUnderwaterPass = TryGetUnderwaterPass(_gameplayMainCamera);

                if (IsUnderwaterPassEnabled(_editorOceanUnderwaterPass))
                    SetUnderwaterPassEnabled(_editorOceanUnderwaterPass, false);

                if (ReferenceEquals(mainCamera, _gameplayMainCamera))
                    _mainCameraUnderwaterPass = _editorOceanUnderwaterPass;

                return;
            }

            _editorOceanUnderwaterPass = EnsureUnderwaterPass(_gameplayMainCamera);
            if (_editorOceanUnderwaterPass == null)
                return;

            Component template = ResolveEditorUnderwaterPassTemplate();
            if (template != null &&
                !ReferenceEquals(template, _editorOceanUnderwaterPass))
            {
                CopyUnderwaterPassSettings(template, _editorOceanUnderwaterPass);
            }

            EnsureCameraTextureRequirementsCached(
                _gameplayMainCamera,
                ref _cachedMainCameraDataCamera,
                ref _cachedMainCameraData,
                ref _cachedMainCameraDataMissing);
            SetCopyOceanMaterialParamsEachFrame(_editorOceanUnderwaterPass, true);
            if (!IsUnderwaterPassEnabled(_editorOceanUnderwaterPass))
            {
                SetUnderwaterPassEnabled(_editorOceanUnderwaterPass, true);
            }

            if (ReferenceEquals(mainCamera, _gameplayMainCamera))
                _mainCameraUnderwaterPass = _editorOceanUnderwaterPass;
        }

        private void DisableEditorSceneViewUnderwaterPass()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
            if (sceneViewCamera != null)
            {
                Component sceneViewUnderwaterPass = TryGetUnderwaterPass(sceneViewCamera);
                _editorSceneViewUnderwaterPass = sceneViewUnderwaterPass;
                if (IsUnderwaterPassEnabled(sceneViewUnderwaterPass))
                    SetUnderwaterPassEnabled(sceneViewUnderwaterPass, false);
            }

            if (_editorSceneViewUnderwaterPass != null &&
                IsUnderwaterPassEnabled(_editorSceneViewUnderwaterPass))
            {
                SetUnderwaterPassEnabled(_editorSceneViewUnderwaterPass, false);
            }

            if (ReferenceEquals(_mainCameraUnderwaterPass, _editorSceneViewUnderwaterPass))
                _mainCameraUnderwaterPass = _editorOceanUnderwaterPass;
        }

        private Component ResolveEditorUnderwaterPassTemplate()
        {
            if (_editorOceanUnderwaterPass != null)
                return _editorOceanUnderwaterPass;

            if (_gameplayMainCamera != null)
            {
                Component gameplayRenderer = TryGetUnderwaterPass(_gameplayMainCamera);
                _editorOceanUnderwaterPass = gameplayRenderer;
                if (gameplayRenderer != null)
                    return gameplayRenderer;
            }

            return _mainCameraUnderwaterPass;
        }
#endif

        private void EnsureOceanCameraOwnership()
        {
            if (!Application.isPlaying)
                return;

            if (mainCamera == null || !IsRuntimeMainCamera(mainCamera))
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge == null)
                return;

            if (bridge.IsOceanCameraOwnedBy(mainCamera))
                return;

            EnsureCameraTextureRequirementsCached(
                mainCamera,
                ref _cachedMainCameraDataCamera,
                ref _cachedMainCameraData,
                ref _cachedMainCameraDataMissing);
            bridge.AssignOceanCamera(mainCamera);
        }

        private void EnsureRuntimeVisualOwners()
        {
            if (!Application.isPlaying)
                return;

            if (playerCamera == null || _playerMovement == null)
                ResolvePlayerCamera();

            if (mainCamera == null || !IsRuntimeMainCamera(mainCamera))
                ResolveMainCamera();

            EnsurePrimarySunReference();

            if (sunVisualTransform == null)
                ResolveSunVisualTransform();

            if (transitionCameraVfx == null)
                ResolveTransitionCameraVfx();

            if (transitionVisorController == null)
                ResolveTransitionVisorController();

            if (underwaterSuspendedMotes == null)
                ResolveUnderwaterParticles();

            if (underwaterExhaleBubbles == null)
                ResolveUnderwaterExhaleBubbles();

            if (shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                ResolveShallowSunBeam();

            if (!IsCameraReferenceValid(_spaceCamera))
                ResolveSpaceCamera();

            EnsureOceanCameraOwnership();

            if (_mainCameraUnderwaterPass == null)
                EnsureOceanUnderwaterPassOwnership();
        }

        private void ResolveRuntimeVisualOwnersOnColdCadence()
        {
            if (!_runtimeVisualOwnerResolveRequested)
                return;

            _runtimeVisualOwnerResolveRequested = false;
            EnsureRuntimeVisualOwners();
        }

        private void RequestRuntimeVisualOwnerResolveIfMissing()
        {
            if (!_runtimeVisualCallbacksActive)
                return;

            if (playerCamera == null ||
                _playerMovement == null ||
                mainCamera == null ||
                !IsRuntimeMainCamera(mainCamera) ||
                !IsPrimarySunLightValid(sunLight) ||
                sunVisualTransform == null ||
                transitionCameraVfx == null ||
                transitionVisorController == null ||
                underwaterSuspendedMotes == null ||
                underwaterMarineSnow == null ||
                underwaterExhaleBubbles == null ||
                shallowSunBeamLight == null ||
                _shallowSunBeamTransform == null ||
                !IsCameraReferenceValid(_spaceCamera) ||
                _mainCameraUnderwaterPass == null)
            {
                _runtimeVisualOwnerResolveRequested = true;
            }
        }

        private static bool IsRuntimeMainCamera(Camera camera)
        {
            return camera != null &&
                   camera.cameraType != CameraType.SceneView &&
                   camera.CompareTag("MainCamera");
        }

        private static Camera ResolveNearestParentCamera(Transform start)
        {
            Transform cursor = start;
            while (cursor != null)
            {
                if (cursor.TryGetComponent(out Camera camera))
                    return camera;

                cursor = cursor.parent;
            }

            return null;
        }

        private static Camera ResolveRuntimeMainCamera()
        {
            int totalFound = Camera.GetAllCameras(_runtimeCameraBuffer);
            int safeCount = math.min(totalFound, _runtimeCameraBuffer.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (IsRuntimeMainCamera(candidate) &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Camera ResolveRuntimeMainCamera(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            int totalFound = Camera.GetAllCameras(_runtimeCameraBuffer);
            int safeCount = math.min(totalFound, _runtimeCameraBuffer.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (!IsRuntimeMainCamera(candidate) ||
                    !candidate.enabled ||
                    !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Transform candidateTransform = candidate.transform;
                if (ReferenceEquals(candidateTransform, playerTransform) ||
                    candidateTransform.IsChildOf(playerTransform))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void PurgeSecondaryUnderwaterPassesIfNeeded()
        {
            bool ownerChanged =
                !ReferenceEquals(_secondaryUnderwaterPassPurgeMainCamera, mainCamera) ||
                !ReferenceEquals(_secondaryUnderwaterPassPurgeSpaceCamera, _spaceCamera) ||
                !ReferenceEquals(_secondaryUnderwaterPassPurgeMainPass, _mainCameraUnderwaterPass) ||
                !ReferenceEquals(_secondaryUnderwaterPassPurgeSpacePass, _spaceCameraUnderwaterPass);

            float now = ResolvePresentationClockSeconds();
            if (!ownerChanged && now < _nextSecondaryUnderwaterPassPurgeTime)
                return;

            PurgeSecondaryUnderwaterPasses();
            _secondaryUnderwaterPassPurgeMainCamera = mainCamera;
            _secondaryUnderwaterPassPurgeSpaceCamera = _spaceCamera;
            _secondaryUnderwaterPassPurgeMainPass = _mainCameraUnderwaterPass;
            _secondaryUnderwaterPassPurgeSpacePass = _spaceCameraUnderwaterPass;
            _nextSecondaryUnderwaterPassPurgeTime = now + RuntimeCameraResolveRetryInterval;
        }

        private void PurgeSecondaryUnderwaterPasses()
        {
            int totalFound = Camera.GetAllCameras(_runtimeCameraBuffer);
            int safeCount = math.min(totalFound, _runtimeCameraBuffer.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (candidate == null)
                    continue;

                Component candidateRenderer = TryGetUnderwaterPass(candidate);
                if (candidateRenderer == null)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, mainCamera))
                {
                    _mainCameraUnderwaterPass = candidateRenderer;
                    continue;
                }

                if (ReferenceEquals(candidate, _spaceCamera))
                {
                    _spaceCameraUnderwaterPass = candidateRenderer;
                    continue;
                }

                Destroy(candidateRenderer);
            }
        }

        private void CopyUnderwaterPassSettings(
            Component source,
            Component target)
        {
            if (source == null || target == null || ReferenceEquals(source, target))
                return;

            IOceanVisualBridge bridge = ResolveOceanVisualBridge();
            if (bridge == null)
                return;

            bridge.CopyUnderwaterPassSettings(source, target);
        }

        private static void SetMaterialColorIfPresent(Material targetMaterial, int propertyId, Color value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetColor(propertyId, value);
        }

        private static void SetMaterialVectorIfPresent(Material targetMaterial, int propertyId, Vector4 value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetVector(propertyId, value);
        }

        private static void SetMaterialFloatIfPresent(Material targetMaterial, int propertyId, float value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetFloat(propertyId, value);
        }

        private static float ReadMaterialFloatOrDefault(Material material, int propertyId, float fallback)
        {
            return material != null && material.HasProperty(propertyId)
                ? material.GetFloat(propertyId)
                : fallback;
        }

        private static Color ReadMaterialColorOrDefault(Material material, int propertyId, Color fallback)
        {
            return material != null && material.HasProperty(propertyId)
                ? material.GetColor(propertyId)
                : fallback;
        }

        private Color ResolveSafeSkyBindingColor(
            Color preferred,
            Material targetMaterial,
            int propertyId,
            Color fallback)
        {
            Color resolved = preferred;
            if (IsNearlyBlack(resolved))
                resolved = ReadMaterialColorOrDefault(targetMaterial, propertyId, fallback);

            if (IsNearlyBlack(resolved))
                resolved = fallback;

            resolved.a = 1f;
            return resolved;
        }

        private static Vector3 ReadMaterialVector3OrDefault(Material material, int propertyId, Vector3 fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            Vector4 value = material.GetVector(propertyId);
            return new Vector3(value.x, value.y, value.z);
        }

        private Vector3 ResolveAuthoredDepthFogDensity(Material targetMaterial, Vector3 fallback)
        {
            Vector3 authoredDensity = ReadMaterialVector3OrDefault(targetMaterial, _ID_DepthFogDensity, fallback);
            return new Vector3(
                Mathf.Clamp(authoredDensity.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(authoredDensity.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(authoredDensity.z, minFogDensity, maxFogDensity));
        }

        private static bool IsNearlyBlack(Color color)
        {
            return color.r <= 0.0001f &&
                   color.g <= 0.0001f &&
                   color.b <= 0.0001f;
        }

        private Color ResolveSafeOceanColor(Color preferred, Color fallback)
        {
            Color resolved = IsNearlyBlack(preferred) ? fallback : preferred;
            if (IsNearlyBlack(resolved))
                resolved = fallback;

            resolved.a = 1f;
            return resolved;
        }

        private Vector3 ResolveSafeDepthFogDensity(Material targetMaterial)
        {
            Vector3 fallback = ResolveFallbackDepthFogDensity(targetMaterial);
            Vector3 source = _currentDepthFogDensity;

            return new Vector3(
                Mathf.Clamp(source.x > 0f ? source.x : fallback.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.y > 0f ? source.y : fallback.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.z > 0f ? source.z : fallback.z, minFogDensity, maxFogDensity));
        }

        private Vector3 ResolveFallbackDepthFogDensity(Material preferredMaterial)
        {
            Vector3 fallback = new Vector3(0.0125f, 0.009f, 0.01f);
            fallback = ReadMaterialVector3OrDefault(preferredMaterial, _ID_DepthFogDensity, fallback);
            fallback = ReadMaterialVector3OrDefault(oceanUnderwaterMaterial, _ID_DepthFogDensity, fallback);

            Material oceanMaterial = ResolveOceanMaterial();
            fallback = ReadMaterialVector3OrDefault(oceanMaterial, _ID_DepthFogDensity, fallback);

            return new Vector3(
                Mathf.Clamp(fallback.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(fallback.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(fallback.z, minFogDensity, maxFogDensity));
        }

        private Color ResolveFallbackFogColor()
        {
            Color fallback = new Color(0.0567818f, 0.28103185f, 0.41509432f, 1f);
            fallback = ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_ScatterColourShallow, fallback);
            return ResolveSafeOceanColor(fallback, new Color(0f, 0.15f, 0.12f, 1f));
        }

        private void TryHandleThermoclineTransition(bool isUnderwater)
        {
            if (!_runtimeVisualCallbacksActive)
                return;

            DepthZoneProfile currentZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;
            if (!isUnderwater || !_wasUnderwater)
            {
                _lastDepthZoneProfile = currentZone;
                return;
            }

            if (currentZone == _lastDepthZoneProfile)
                return;

            DepthZoneProfile previousZone = _lastDepthZoneProfile;
            _lastDepthZoneProfile = currentZone;

            if (previousZone == null || currentZone == null)
                return;

            if (ResolvePresentationClockSeconds() < _nextThermoclineAllowedTime)
                return;

            float intensity = ResolveThermoclineTransitionIntensity(previousZone, currentZone);
            if (intensity < thermoclineMinTriggerIntensity)
                return;

            if (transitionCameraVfx == null || transitionVisorController == null)
                _runtimeVisualOwnerResolveRequested = true;

            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerThermoclineImpulse(intensity);

            if (transitionVisorController != null)
            {
                transitionVisorController.TriggerEnvironmentalDistortion(
                    intensity,
                    thermoclineVisorDistortionHoldDuration,
                    thermoclineVisorDistortionRecoverySpeed);
            }

            IAudioService audioRuntime = ResolveAudioService();
            if (thermoclineTransitionClip != null && audioRuntime != null)
                audioRuntime.PlayStatic2D(thermoclineTransitionClip, thermoclineAudioVolume * intensity);

            _nextThermoclineAllowedTime = ResolvePresentationClockSeconds() + thermoclineMinRepeatInterval;
        }

        private float ResolveThermoclineTransitionIntensity(DepthZoneProfile previousZone, DepthZoneProfile currentZone)
        {
            DepthZoneAmbience previousAmbience = previousZone.ambience;
            DepthZoneAmbience currentAmbience = currentZone.ambience;

            float temperatureDelta = Mathf.Abs(previousAmbience.waterTemperature - currentAmbience.waterTemperature);
            float fogDelta = Mathf.Abs(previousAmbience.fogDensity - currentAmbience.fogDensity);
            float colorDeltaR = currentAmbience.waterColor.r - previousAmbience.waterColor.r;
            float colorDeltaG = currentAmbience.waterColor.g - previousAmbience.waterColor.g;
            float colorDeltaB = currentAmbience.waterColor.b - previousAmbience.waterColor.b;
            float colorDeltaSq = (colorDeltaR * colorDeltaR) + (colorDeltaG * colorDeltaG) + (colorDeltaB * colorDeltaB);

            float normalizedTemperature = temperatureDelta / Mathf.Max(0.01f, thermoclineTemperatureDeltaForFullEffect);
            float normalizedFog = fogDelta / Mathf.Max(0.01f, thermoclineFogDeltaForFullEffect);
            float normalizedColor = colorDeltaSq / math.max(0.0001f, thermoclineColorDeltaForFullEffect * thermoclineColorDeltaForFullEffect);
            float structuralBonus = previousZone.isThermal != currentZone.isThermal ? 0.24f : 0f;
            structuralBonus += Mathf.Abs(previousZone.dangerLevel - currentZone.dangerLevel) * 0.12f;

            return Mathf.Clamp01(Mathf.Max(normalizedTemperature, Mathf.Max(normalizedFog, normalizedColor)) + structuralBonus);
        }

        private void TriggerSubmergeImpulse()
        {
            if (transitionCameraVfx == null || transitionVisorController == null)
                _runtimeVisualOwnerResolveRequested = true;

            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerSubmergeImpulse();

            if (transitionVisorController != null)
                transitionVisorController.TriggerSubmergeRunoff();

            _submergeImpulseTimer = submergeImpulseDuration;
        }

        private void TriggerSurfaceBreakImpulse()
        {
            if (transitionCameraVfx == null || transitionVisorController == null)
                _runtimeVisualOwnerResolveRequested = true;

            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerSurfaceBreakImpulse();

            if (transitionVisorController != null)
                transitionVisorController.TriggerSurfaceBreakRunoff();
        }

        private void ResolveTransitionCameraVfx()
        {
            if (transitionCameraVfx != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            if (_transitionCameraVfxSearchCompleted &&
                ReferenceEquals(_transitionCameraVfxSearchCamera, mainCamera))
            {
                return;
            }

            _transitionCameraVfxSearchCamera = mainCamera;
            _transitionCameraVfxSearchCompleted = true;
            mainCamera.TryGetComponent(out transitionCameraVfx);
        }

        private void ResolveTransitionVisorController()
        {
            if (transitionVisorController != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            Transform playerRoot = mainCamera.transform.parent;
            if (playerRoot == null)
                return;

            if (!ReferenceEquals(_transitionVisorSearchRoot, playerRoot))
            {
                _transitionVisorSearchRoot = playerRoot;
                _transitionVisorSearchTransform = playerRoot.Find("Suit_Visor");
                _transitionVisorSearchCompleted = _transitionVisorSearchTransform == null;
            }

            if (_transitionVisorSearchTransform == null || _transitionVisorSearchCompleted)
                return;

            _transitionVisorSearchCompleted = true;
            _transitionVisorSearchTransform.TryGetComponent(out transitionVisorController);
        }

        private void ResolveUnderwaterParticles()
        {
            if (underwaterSuspendedMotes != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            if (!ReferenceEquals(_underwaterSuspendedMotesSearchCamera, mainCamera))
            {
                _underwaterSuspendedMotesSearchCamera = mainCamera;
                _underwaterSuspendedMotesSearchTransform = mainCamera.transform.Find(UnderwaterSuspendedMotesChildName);
                _underwaterSuspendedMotesSearchCompleted = _underwaterSuspendedMotesSearchTransform == null;
            }

            if (_underwaterSuspendedMotesSearchTransform == null || _underwaterSuspendedMotesSearchCompleted)
                return;

            _underwaterSuspendedMotesSearchCompleted = true;
            _underwaterSuspendedMotesSearchTransform.TryGetComponent(out underwaterSuspendedMotes);
        }

        private void ResolveUnderwaterMarineSnow()
        {
            if (underwaterMarineSnow != null)
            {
                if (mainCamera != null)
                    underwaterMarineSnow.BindTargetCamera(mainCamera);
                return;
            }

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            if (ReferenceEquals(_underwaterMarineSnowSearchCamera, mainCamera))
                return;

            mainCamera.TryGetComponent(out underwaterMarineSnow);
            _underwaterMarineSnowSearchCamera = mainCamera;
            if (underwaterMarineSnow != null)
                underwaterMarineSnow.BindTargetCamera(mainCamera);
        }

        private void ResolveUnderwaterExhaleBubbles()
        {
            if (underwaterExhaleBubbles != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            if (!ReferenceEquals(_underwaterExhaleBubblesSearchCamera, mainCamera))
            {
                _underwaterExhaleBubblesSearchCamera = mainCamera;
                _underwaterExhaleBubblesSearchTransform = mainCamera.transform.Find(UnderwaterExhaleBubblesChildName);
                _underwaterExhaleBubblesSearchCompleted = _underwaterExhaleBubblesSearchTransform == null;
            }

            if (_underwaterExhaleBubblesSearchTransform == null || _underwaterExhaleBubblesSearchCompleted)
                return;

            _underwaterExhaleBubblesSearchCompleted = true;
            _underwaterExhaleBubblesSearchTransform.TryGetComponent(out underwaterExhaleBubbles);
        }

        private void ResolveShallowSunBeam()
        {
            if (shallowSunBeamLight != null &&
                _shallowSunBeamTransform != null)
            {
                return;
            }

            if (shallowSunBeamLight != null &&
                _shallowSunBeamTransform == null)
            {
                CacheShallowSunBeamTransform(shallowSunBeamLight.transform);
                return;
            }

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            if (_shallowSunBeamTransform == null)
            {
                if (ReferenceEquals(_shallowSunBeamSearchCamera, mainCamera))
                    return;

                _shallowSunBeamSearchCamera = mainCamera;
                Transform beamTransform = mainCamera.transform.Find(UnderwaterShallowSunBeamChildName);
                if (beamTransform == null)
                    return;

                CacheShallowSunBeamTransform(beamTransform);
            }

            if (shallowSunBeamLight != null)
                return;

            if (_shallowSunBeamLightSearchCompleted &&
                ReferenceEquals(_shallowSunBeamLightSearchTransform, _shallowSunBeamTransform))
            {
                return;
            }

            _shallowSunBeamLightSearchTransform = _shallowSunBeamTransform;
            _shallowSunBeamLightSearchCompleted = true;
            _shallowSunBeamTransform.TryGetComponent(out shallowSunBeamLight);
        }

        private void CacheShallowSunBeamTransform(Transform beamTransform)
        {
            if (beamTransform == null)
                return;

            _shallowSunBeamTransform = beamTransform;
            _shallowSunBeamBaseLocalPosition = beamTransform.localPosition;
            _shallowSunBeamLightSearchTransform = null;
            _shallowSunBeamLightSearchCompleted = false;
        }

        private void ResolveSunVisualTransform()
        {
            EnsurePrimarySunReference();

            if (sunVisualTransform != null)
                return;

            if (sunLight == null)
                sunLight = RenderSettings.sun;

            if (sunLight == null)
                return;

            if (_sunVisualSearchCompleted &&
                ReferenceEquals(_sunVisualSearchLight, sunLight))
            {
                return;
            }

            _sunVisualSearchLight = sunLight;
            _sunVisualSearchCompleted = true;
            Transform resolvedSunVisual = sunLight.transform.Find("Sun_Body");
            if (resolvedSunVisual != null)
                sunVisualTransform = resolvedSunVisual;
        }

        private void EnsurePrimarySunReference()
        {
            Light renderSun = RenderSettings.sun;
            if (IsPrimarySunLightValid(renderSun) && !ReferenceEquals(sunLight, renderSun))
                sunLight = renderSun;

            if (!IsPrimarySunLightValid(sunLight))
                sunLight = IsPrimarySunLightValid(renderSun) ? renderSun : null;

            if (sunVisualTransform == null)
                return;

            Light visualLight = sunVisualTransform.GetComponent<Light>();
            if (visualLight != null && !ReferenceEquals(visualLight, sunLight))
                sunVisualTransform = null;
        }

        private static bool IsPrimarySunLightValid(Light candidate)
        {
            return candidate != null &&
                   candidate.type == LightType.Directional &&
                   candidate.enabled &&
                   candidate.gameObject.activeInHierarchy;
        }

        private void UpdateSubmergeImpulse(float deltaTime)
        {
            if (_submergeImpulseTimer <= 0f)
                return;

            _submergeImpulseTimer -= deltaTime;
            if (_submergeImpulseTimer < 0f)
                _submergeImpulseTimer = 0f;
        }

        private float EvaluateSubmergeImpulse(float depth)
        {
            if (_submergeImpulseTimer <= 0f || submergeImpulseDuration <= 0.0001f)
                return 0f;

            float timeFade = _submergeImpulseTimer / submergeImpulseDuration;
            float depthFade = 1f - math.saturate(depth / math.max(0.01f, submergeImpulseDepthWindow));
            return timeFade * depthFade;
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!enableAdaptiveBudgetResponse)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            DynamicResolutionScaler scaler = _dynamicResolutionRuntime;
            if (scaler == null || !scaler.Enabled)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            float renderScale = math.saturate(scaler.CurrentRenderScale);
            float normalized = math.saturate(
                (renderScale - adaptiveBudgetFloorRenderScale) /
                math.max(0.0001f, 1f - adaptiveBudgetFloorRenderScale));

            ApplyAdaptiveBudgetResponse(renderScale, normalized);
        }

        private void ApplyAdaptiveBudgetResponse(float renderScale, float normalized)
        {
            _adaptiveBudgetNormalized = normalized;
            _adaptiveMotesScale = math.lerp(adaptiveMotesBudgetFloor, 1f, normalized);
            _adaptiveBubbleScale = math.lerp(adaptiveBubbleBudgetFloor, 1f, normalized);
            _adaptiveBeamScale = math.lerp(adaptiveBeamBudgetFloor, 1f, normalized);
            _adaptiveCausticsScale = math.lerp(adaptiveCausticsBudgetFloor, 1f, normalized);
            _adaptiveBottomSiltProbeIntervalScale = math.lerp(adaptiveBottomSiltProbeIntervalMultiplier, 1f, normalized);

#if UNITY_EDITOR
            _debugAdaptiveRenderScale = renderScale;
            _debugAdaptiveBudgetNormalized = normalized;
            _debugAdaptiveMotesScale = _adaptiveMotesScale;
            _debugAdaptiveBubbleScale = _adaptiveBubbleScale;
            _debugAdaptiveBeamScale = _adaptiveBeamScale;
            _debugAdaptiveCausticsScale = _adaptiveCausticsScale;
            _debugAdaptiveBottomProbeScale = _adaptiveBottomSiltProbeIntervalScale;
#endif
        }

        private void UpdateUnderwaterSuspendedMotes(
            float depth,
            float lightFactor,
            float submergeImpulse,
            bool isUnderwater)
        {
            if (underwaterSuspendedMotes == null || underwaterMarineSnow == null)
                _runtimeVisualOwnerResolveRequested = true;

            float targetEmission = 0f;
            bool shouldPlay = false;

            if (enableSuspendedMotes && isUnderwater)
            {
                float transportExposureScale = ResolveTransportHelmetExposureScale();
                float depthFactor = math.saturate(
                    depth / math.max(0.01f, suspendedMotesFullEmissionDepth));
                float turbidityFactor = math.saturate(
                    (_currentTurbidity - 0.5f) * suspendedMotesTurbidityWeight);
                float darknessFactor = 1f - lightFactor;
                float daylightVisibility = ResolveSurfaceDaylightVisibility();
                float clearWaterPresence = daylightVisibility * (1f - turbidityFactor);
                float densityFactor = math.saturate(
                    depthFactor * 0.45f +
                    turbidityFactor * 0.35f +
                    darknessFactor * 0.2f +
                    clearWaterPresence * UnderwaterClearWaterMotesStrength);

                targetEmission = math.lerp(
                    suspendedMotesMinEmission,
                    suspendedMotesMaxEmission,
                    densityFactor);
                targetEmission *= transportExposureScale;
                targetEmission *= _ecologySuspendedMotesMultiplier;
                targetEmission *= _adaptiveMotesScale;
                targetEmission += ResolveBottomSiltEmissionBoost(isUnderwater);
                targetEmission += submergeImpulse * suspendedMotesSubmergeBoost * transportExposureScale;
                targetEmission *= FrameTimeWatchdog.ParticleEmissionScale;
                shouldPlay = targetEmission > 0.01f;
            }
            else
            {
                ResolveBottomSiltEmissionBoost(false);
            }

            if (underwaterMarineSnow != null)
            {
                float densityCeiling = math.max(
                    0.01f,
                    suspendedMotesMaxEmission +
                    bottomSiltEmissionBoost +
                    suspendedMotesSubmergeBoost);
                float densityScale = math.saturate(targetEmission / densityCeiling);
                float playerSpeedSq = ResolvePlayerSpeedSquaredMetersPerSecond();
                float bubbleTrail01 = shouldPlay
                    ? ResolveSquaredSpeedFactor(playerSpeedSq, GpuBubbleTrailMinSpeed, GpuBubbleTrailFullSpeed)
                    : 0f;
                float bubbleDeltaTime = _runtimeVisualCallbacksActive ? math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime) : 0.0166667f;
                if (_gpuBubbleExhaleImpulse01 > 0f)
                {
                    _gpuBubbleExhaleImpulse01 = math.max(
                        0f,
                        _gpuBubbleExhaleImpulse01 - GpuBubbleExhaleImpulseDecayRate * bubbleDeltaTime);
                }

                underwaterMarineSnow.SetUnderwaterState(
                    shouldPlay,
                    densityScale,
                    depth,
                    lightFactor,
                    submergeImpulse);
                underwaterMarineSnow.SetBubbleTrailState(
                    bubbleTrail01 * _adaptiveBubbleScale * FrameTimeWatchdog.ParticleEmissionScale,
                    _cachedVisualIsUnderwater ? _gpuBubbleExhaleImpulse01 * _adaptiveBubbleScale * FrameTimeWatchdog.ParticleEmissionScale : 0f);

                if (underwaterMarineSnow.IsOperational)
                {
                    DisableUnderwaterSuspendedMotes(true);
#if UNITY_EDITOR
                    _debugSuspendedMotesEmission = targetEmission;
#endif
                    return;
                }
            }

            if (underwaterSuspendedMotes == null)
                return;

            RefreshSuspendedMotesQualityCap();

            if (math.abs(targetEmission - _cachedSuspendedMotesEmission) > 0.05f)
            {
                ParticleSystem.EmissionModule emission = underwaterSuspendedMotes.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(targetEmission);
                _cachedSuspendedMotesEmission = targetEmission;
            }

            if (shouldPlay)
            {
                if (!_underwaterSuspendedMotesPlaying)
                {
                    underwaterSuspendedMotes.Play(true);
                    _underwaterSuspendedMotesPlaying = true;
                }
            }
            else
            {
                DisableUnderwaterSuspendedMotes(true);
            }

#if UNITY_EDITOR
            _debugSuspendedMotesEmission = targetEmission;
#endif
        }

        private void RefreshSuspendedMotesQualityCap()
        {
            if (underwaterSuspendedMotes == null)
                return;

            float qualityWeight = ResolveVisualQualityWeight01();
            if (_cachedSuspendedMotesParticleCap > 0 &&
                math.abs(qualityWeight - _cachedSuspendedMotesQualityWeight) < SuspendedMotesQualityRefreshEpsilon)
            {
                return;
            }

            int particleCap = VfxComputeParticleBudgetCatalog.ResolvePoolCapacity(
                qualityWeight,
                0,
                VFXEmissionProfile.FluidType.Snow);
            particleCap = math.clamp(
                math.max(1, particleCap),
                1,
                VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount);

            if (particleCap != _cachedSuspendedMotesParticleCap)
            {
                ParticleSystem.MainModule main = underwaterSuspendedMotes.main;
                main.maxParticles = particleCap;
                _cachedSuspendedMotesParticleCap = particleCap;
            }

            _cachedSuspendedMotesQualityWeight = qualityWeight;
        }

        private static float ResolveVisualQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(0f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private void UpdateHudFogLuminanceDownsample()
        {
            if (!enableHudFogLuminanceGpuReadback)
            {
                _hudFogDownsampledLuminance01 = 0f;
                return;
            }

            Texture sourceTexture = VisorHUDController.ActiveHudRenderTexture;
            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
                return;

            if (_hudFogLuminanceReleasePending || !HasHudFogLuminanceResourcesReady() || _hudFogReadbackPending)
                return;

            if (!HasHudFogLuminanceReadbackData())
            {
                QueueHudFogLuminanceReadbackRepair();
                return;
            }

            float now = ResolvePresentationClockSeconds();
            if (now < _nextHudFogLuminanceReadbackTime)
                return;

            int groupsX = ResolveDispatchGroups(1, _hudFogLuminanceThreadGroupSizeX);
            int groupsY = ResolveDispatchGroups(1, _hudFogLuminanceThreadGroupSizeY);
            if (groupsX <= 0 || groupsY <= 0)
                return;

            if (_hudFogLuminanceReadbackCompleted == null)
                _hudFogLuminanceReadbackCompleted = HandleHudFogLuminanceReadbackCompleted;

            _nextHudFogLuminanceReadbackTime = now + HudFogLuminanceReadbackIntervalSeconds;
            hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogSourceId, sourceTexture);
            hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogLuminanceOutputId, _hudFogLuminanceTexture);
            hudFogLuminanceCompute.SetVector(
                _HectonHudFogLuminanceParamsId,
                new Vector4(
                    sourceTexture.width,
                    sourceTexture.height,
                    1f / math.max(1f, sourceTexture.width),
                    1f / math.max(1f, sourceTexture.height)));
            hudFogLuminanceCompute.Dispatch(_hudFogLuminanceKernel, groupsX, groupsY, 1);

            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(
                ref _hudFogLuminanceReadback.Data,
                _hudFogLuminanceTexture,
                0,
                _hudFogLuminanceReadbackCompleted);
            _hudFogReadbackPending = !request.hasError;
        }

        private void EnsureHudFogLuminanceResources(bool allowAllocate = true)
        {
            if (_hudFogLuminanceReleasePending)
                return;

            if (_hudFogLuminanceReady && _hudFogLuminanceTexture != null)
                return;

            if (!_supportsComputeShadersCold)
            {
                _hudFogLuminanceReady = false;
                return;
            }

#if UNITY_EDITOR
            if (hudFogLuminanceCompute == null)
                hudFogLuminanceCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(HudFogLuminanceComputeAssetPath);
#endif

            if (hudFogLuminanceCompute == null)
            {
                _hudFogLuminanceReady = false;
                return;
            }

            if (_hudFogLuminanceKernel < 0)
            {
                if (!TryResolveComputeKernel(hudFogLuminanceCompute, "ResolveHudFogLuminance", out _hudFogLuminanceKernel))
                {
                    _hudFogLuminanceReady = false;
                    return;
                }
            }

            if (!TryResolveKernelThreadGroupSize2D(
                    hudFogLuminanceCompute,
                    _hudFogLuminanceKernel,
                    out _hudFogLuminanceThreadGroupSizeX,
                    out _hudFogLuminanceThreadGroupSizeY))
            {
                _hudFogLuminanceReady = false;
                _hudFogLuminanceThreadGroupSizeX = 0;
                _hudFogLuminanceThreadGroupSizeY = 0;
                return;
            }

            if (_hudFogLuminanceTexture == null)
            {
                if (!allowAllocate)
                {
                    _hudFogLuminanceReady = false;
                    return;
                }

                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(1, 1)
                {
                    dimension = TextureDimension.Tex2D,
                    graphicsFormat = GraphicsFormat.R32_SFloat,
                    depthBufferBits = 0,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = true,
                    sRGB = false
                };

                _hudFogLuminanceTexture = new RenderTexture(descriptor)
                {
                    name = "__HectonHudFogLuminance",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: RenderTexture[1x1 R32F] - throttled HUD luminance reduction target - owner: HectonUnderwaterVisuals
                _hudFogLuminanceTexture.Create();
            }

            _hudFogLuminanceReady = true;
        }

        private bool HasHudFogLuminanceResourcesReady()
        {
            return _hudFogLuminanceReady &&
                   _hudFogLuminanceTexture != null &&
                   _hudFogLuminanceKernel >= 0 &&
                   _hudFogLuminanceThreadGroupSizeX > 0 &&
                   _hudFogLuminanceThreadGroupSizeY > 0;
        }

        private bool HasHudFogLuminanceReadbackData()
        {
            return _hudFogLuminanceReadback.Data.IsCreated &&
                   _hudFogLuminanceReadback.Data.Length >= 1;
        }

        private void QueueHudFogLuminanceReadbackRepair()
        {
            _hudFogLuminanceReadbackRepairRequested = true;
        }

        private void FlushHudFogLuminanceReadbackRepairSlow()
        {
            if (!_hudFogLuminanceReadbackRepairRequested && HasHudFogLuminanceReadbackData())
                return;

            _hudFogLuminanceReadbackRepairRequested = false;
            EnsureHudFogLuminanceResources(allowAllocate: true);

            if (!HasHudFogLuminanceResourcesReady())
            {
                _hudFogLuminanceReadbackRepairRequested = true;
                return;
            }

            EnsureHudFogLuminanceReadbackData();
        }

        private void ReleaseHudFogLuminanceResources()
        {
            _hudFogLuminanceReady = false;
            _hudFogLuminanceKernel = -1;
            _hudFogLuminanceThreadGroupSizeX = 0;
            _hudFogLuminanceThreadGroupSizeY = 0;
            _hudFogLuminanceReadbackRepairRequested = true;

            if (_hudFogReadbackPending)
            {
                _hudFogLuminanceReleasePending = true;
                return;
            }

            ReleaseHudFogLuminanceResourcesImmediate();
        }

        private void ReleaseHudFogLuminanceResourcesImmediate()
        {
            _hudFogReadbackPending = false;
            _hudFogLuminanceReleasePending = false;
            DisposeHudFogLuminanceReadbackData();

            if (_hudFogLuminanceTexture == null)
                return;

            _hudFogLuminanceTexture.Release();
            Destroy(_hudFogLuminanceTexture);
            _hudFogLuminanceTexture = null;
        }

        private void HandleHudFogLuminanceReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            _hudFogReadbackPending = false;
            if (!request.hasError)
            {
                NativeArray<float> luminance = _hudFogLuminanceReadback.Data;
                if (luminance.IsCreated && luminance.Length > 0)
                {
                    float resolved = luminance[0];
                    _hudFogDownsampledLuminance01 = math.isfinite(resolved) ? math.saturate(resolved) : 0f;
                }
            }

            if (_hudFogLuminanceReleasePending)
                ReleaseHudFogLuminanceResourcesImmediate();
        }

        private void EnsureHudFogLuminanceReadbackData()
        {
            if (_hudFogLuminanceReadback.Data.IsCreated && _hudFogLuminanceReadback.Data.Length >= 1)
                return;

            DisposeHudFogLuminanceReadbackData();
            _hudFogLuminanceReadback.Data = H8Memory.Allocate<float>(
                1,
                SystemID.Vfx,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - async HUD fog luminance readback target - owner: HectonUnderwaterVisuals
            if (!_hudFogLuminanceReadback.Data.IsCreated)
                throw new InvalidOperationException($"{nameof(HectonUnderwaterVisuals)} native allocation failed for _hudFogLuminanceReadbackData.");

            _hudFogLuminanceReadbackRepairRequested = false;
        }

        private void DisposeHudFogLuminanceReadbackData()
        {
            if (_hudFogLuminanceReadback.Data.IsCreated)
            {
                H8Memory.Release(ref _hudFogLuminanceReadback.Data, SystemID.Vfx);
            }
        }

        private void UpdateFlashlightPhotophobiaField(float deltaTime)
        {
            if (!_runtimeVisualCallbacksActive || !enableFlashlightPhotophobiaField || !_cachedVisualIsUnderwater)
            {
                Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
                return;
            }

            float now = ResolvePresentationClockSeconds();
            Vector4 lightPosition = Shader.GetGlobalVector(_HectonFlashlightPositionWSId);
            Vector4 lightDirection = Shader.GetGlobalVector(_HectonFlashlightDirectionWSId);
            Vector4 lightColor = Shader.GetGlobalVector(_HectonFlashlightColorId);
            Vector4 coneData = Shader.GetGlobalVector(_HectonFlashlightConeDataId);
            float active = Shader.GetGlobalFloat(_HectonFlashlightActiveId);
            float rangeMeters = math.max(lightPosition.w, 0.1f);
            float lightEnergy = math.saturate(lightColor.w * flashlightPhotophobiaStrength);

            float3 direction = new float3(lightDirection.x, lightDirection.y, lightDirection.z);
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) < 0.0001f)
            {
                direction = new float3(0f, 0f, 1f);
            }
            else
            {
                float3 absDirection = math.abs(direction);
                if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
                    direction = new float3(direction.x < 0f ? -1f : 1f, 0f, 0f);
                else if (absDirection.y >= absDirection.z)
                    direction = new float3(0f, direction.y < 0f ? -1f : 1f, 0f);
                else
                    direction = new float3(0f, 0f, direction.z < 0f ? -1f : 1f);
            }

            bool hasActiveCone =
                active > 0.5f &&
                lightEnergy > 0.0001f &&
                math.isfinite(rangeMeters) &&
                rangeMeters > 0.1f;

            if (hasActiveCone)
            {
                float fieldExtentMeters = math.max(32f, math.max(flashlightPhotophobiaFieldExtent, rangeMeters * 1.2f));
                float3 lightPositionWs = new float3(lightPosition.x, lightPosition.y, lightPosition.z);
                if (!math.all(math.isfinite(lightPositionWs)))
                    lightPositionWs = float3.zero;

                float3 fieldOriginWs = lightPositionWs + direction * (rangeMeters * 0.5f);
                _photophobiaFieldOriginScale = new Vector4(
                    fieldOriginWs.x,
                    fieldOriginWs.y,
                    fieldOriginWs.z,
                    1f / fieldExtentMeters);
                _photophobiaRecoverUntilUnscaledTime =
                    now + math.max(0.25f, flashlightPhotophobiaRecoverySeconds) + FlashlightPhotophobiaRecoveryGraceSeconds;
            }

            bool shouldRun = hasActiveCone || now <= _photophobiaRecoverUntilUnscaledTime;
            if (!shouldRun)
            {
                if (_photophobiaFieldDirty)
                {
                    ClearPhotophobiaFieldTextures();
                    _photophobiaFieldDirty = false;
                }

                Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
                return;
            }

            if (!HasPhotophobiaFieldResourcesReady())
            {
                Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
                return;
            }

            RenderTexture source = _photophobiaFieldWriteToA ? _photophobiaFieldTextureB : _photophobiaFieldTextureA;
            RenderTexture target = _photophobiaFieldWriteToA ? _photophobiaFieldTextureA : _photophobiaFieldTextureB;
            float safeDeltaTime = math.clamp(deltaTime, 0f, 0.05f);
            float transitionSeconds = math.max(0.25f, flashlightPhotophobiaRecoverySeconds);
            float transitionRate = 1f / transitionSeconds;
            float innerCos = lightDirection.w;
            float outerCos = coneData.x;
            float invRange = math.rcp(math.max(rangeMeters, 0.1f));

            photophobiaFieldCompute.SetTexture(_photophobiaFieldKernel, _HectonPhotophobiaSourceTexId, source);
            photophobiaFieldCompute.SetTexture(_photophobiaFieldKernel, _HectonPhotophobiaTargetTexId, target);
            photophobiaFieldCompute.SetVector(
                _HectonPhotophobiaParamsId,
                new Vector4(
                    safeDeltaTime,
                    transitionRate,
                    transitionRate,
                    hasActiveCone ? 1f : 0f));
            photophobiaFieldCompute.SetVector(_HectonPhotophobiaFieldOriginScaleId, _photophobiaFieldOriginScale);
            photophobiaFieldCompute.SetVector(
                _HectonPhotophobiaCone0Id,
                new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, rangeMeters));
            photophobiaFieldCompute.SetVector(
                _HectonPhotophobiaCone1Id,
                new Vector4(direction.x, direction.y, direction.z, outerCos));
            photophobiaFieldCompute.SetVector(
                _HectonPhotophobiaCone2Id,
                new Vector4(innerCos, invRange, lightEnergy, now));

            int groupsX = ResolveDispatchGroups(FlashlightPhotophobiaFieldResolution, _photophobiaFieldThreadGroupSizeX);
            int groupsY = ResolveDispatchGroups(FlashlightPhotophobiaFieldResolution, _photophobiaFieldThreadGroupSizeY);
            if (groupsX <= 0 || groupsY <= 0)
            {
                Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
                return;
            }

            photophobiaFieldCompute.Dispatch(_photophobiaFieldKernel, groupsX, groupsY, 1);
            _photophobiaFieldWriteToA = !_photophobiaFieldWriteToA;
            _photophobiaFieldDirty = true;

            Shader.SetGlobalTexture(_HectonPhotophobiaFieldTexId, target);
            Shader.SetGlobalVector(_HectonPhotophobiaFieldOriginScaleId, _photophobiaFieldOriginScale);
            Shader.SetGlobalVector(
                _HectonPhotophobiaFieldStateId,
                new Vector4(1f, lightEnergy, transitionSeconds, hasActiveCone ? 1f : 0f));
        }

        private void EnsurePhotophobiaFieldResources(bool allowAllocate = true)
        {
            if (_photophobiaFieldReady && _photophobiaFieldTextureA != null && _photophobiaFieldTextureB != null)
                return;

            if (!_supportsComputeShadersCold)
            {
                _photophobiaFieldReady = false;
                return;
            }

#if UNITY_EDITOR
            if (photophobiaFieldCompute == null)
                photophobiaFieldCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(PhotophobiaFieldComputeAssetPath);
#endif

            if (photophobiaFieldCompute == null)
            {
                _photophobiaFieldReady = false;
                return;
            }

            if (_photophobiaFieldTextureA == null || _photophobiaFieldTextureB == null)
            {
                if (!allowAllocate)
                {
                    _photophobiaFieldReady = false;
                    return;
                }

                ReleasePhotophobiaFieldResources();
                _photophobiaFieldTextureA = CreatePhotophobiaFieldTexture("__HectonPhotophobiaFieldA");
                _photophobiaFieldTextureB = CreatePhotophobiaFieldTexture("__HectonPhotophobiaFieldB");
                ClearPhotophobiaFieldTextures();
                _photophobiaFieldWriteToA = true;
            }

            if (!TryResolveComputeKernel(photophobiaFieldCompute, "UpdatePhotophobiaField", out _photophobiaFieldKernel) ||
                !TryResolveKernelThreadGroupSize2D(
                    photophobiaFieldCompute,
                    _photophobiaFieldKernel,
                    out _photophobiaFieldThreadGroupSizeX,
                    out _photophobiaFieldThreadGroupSizeY))
            {
                _photophobiaFieldReady = false;
                _photophobiaFieldThreadGroupSizeX = 0;
                _photophobiaFieldThreadGroupSizeY = 0;
                return;
            }

            _photophobiaFieldReady = true;
        }

        private bool HasPhotophobiaFieldResourcesReady()
        {
            return _photophobiaFieldReady &&
                   _photophobiaFieldTextureA != null &&
                   _photophobiaFieldTextureB != null &&
                   _photophobiaFieldKernel >= 0 &&
                   _photophobiaFieldThreadGroupSizeX > 0 &&
                   _photophobiaFieldThreadGroupSizeY > 0;
        }

        private bool TryResolveComputeKernel(ComputeShader compute, string kernelName, out int kernelIndex)
        {
            kernelIndex = -1;
            if (compute == null || !_supportsComputeShadersCold)
                return false;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return false;

                kernelIndex = compute.FindKernel(kernelName);
                return kernelIndex >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (UnityException)
            {
                kernelIndex = -1;
                return false;
            }
        }

        private static bool TryResolveKernelThreadGroupSize2D(
            ComputeShader compute,
            int kernelIndex,
            out int groupSizeX,
            out int groupSizeY)
        {
            groupSizeX = 0;
            groupSizeY = 0;
            if (!TryQueryKernelThreadGroups(compute, kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ))
                return false;
            if (sizeY == 0u || sizeZ != 1u)
                return false;

            groupSizeX = (int)sizeX;
            groupSizeY = (int)sizeY;
            return true;
        }

        private static bool TryValidateKernelThreadProduct(ComputeShader compute, int kernelIndex)
        {
            return TryQueryKernelThreadGroups(compute, kernelIndex, out _, out _, out _);
        }

        private static bool TryQueryKernelThreadGroups(ComputeShader compute, int kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ)
        {
            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            if (compute == null || kernelIndex < 0)
                return false;

            try
            {
                if (!compute.IsSupported(kernelIndex))
                    return false;

                compute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
            if (sizeX == 0u || sizeY == 0u || sizeZ == 0u)
                return false;

            ulong maxThreads = (ulong)PortableMaxComputeThreadsPerGroup;
            ulong xyThreads = (ulong)sizeX * sizeY;
            if (xyThreads == 0UL ||
                xyThreads > maxThreads ||
                sizeZ > maxThreads / xyThreads)
            {
                return false;
            }

            return xyThreads * sizeZ <= maxThreads;
        }

        private static int ResolveDispatchGroups(int value, int threadGroupSize)
        {
            if (value <= 0 || threadGroupSize <= 0)
                return 0;

            long groups = ((long)value + threadGroupSize - 1L) / threadGroupSize;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private static RenderTexture CreatePhotophobiaFieldTexture(string name)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                FlashlightPhotophobiaFieldResolution,
                FlashlightPhotophobiaFieldResolution)
            {
                dimension = TextureDimension.Tex2D,
                graphicsFormat = GraphicsFormat.R8_UNorm,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[128x128 R8] - ping-pong flora photophobia field - owner: HectonUnderwaterVisuals
            texture.Create();
            return texture;
        }

        private void ClearPhotophobiaFieldTextures()
        {
            ClearPhotophobiaFieldTexture(_photophobiaFieldTextureA);
            ClearPhotophobiaFieldTexture(_photophobiaFieldTextureB);
        }

        private static void ClearPhotophobiaFieldTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            RenderTexture previous = RenderTexture.active;
            UnityEngine.Graphics.SetRenderTarget(texture);
            GL.Clear(false, true, Color.white);
            UnityEngine.Graphics.SetRenderTarget(previous);
        }

        private void ReleasePhotophobiaFieldResources()
        {
            _photophobiaFieldReady = false;
            _photophobiaFieldKernel = -1;
            _photophobiaFieldThreadGroupSizeX = 0;
            _photophobiaFieldThreadGroupSizeY = 0;
            _photophobiaFieldWriteToA = true;
            _photophobiaFieldDirty = false;
            _photophobiaRecoverUntilUnscaledTime = 0f;
            _photophobiaFieldOriginScale = Vector4.zero;

            if (_photophobiaFieldTextureA != null)
            {
                _photophobiaFieldTextureA.Release();
                Destroy(_photophobiaFieldTextureA);
                _photophobiaFieldTextureA = null;
            }

            if (_photophobiaFieldTextureB != null)
            {
                _photophobiaFieldTextureB.Release();
                Destroy(_photophobiaFieldTextureB);
                _photophobiaFieldTextureB = null;
            }

            Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
        }

        private void ApplyNoirResolveGlobals()
        {
            bool runtimeCallbacksActive = _runtimeVisualCallbacksActive;
            float causticsGate = enableShallowCaustics && _cachedVisualIsUnderwater
                ? _cachedCausticsStrength
                : 0f;
            Color abyssFloor = abyssalBlackFloor;
            abyssFloor.a = 1f;
            float waterLevel = ResolveWaterLevel();
            float verticalFogSpan = math.max(8f, noirVerticalFogSpan);
            int frameIndex = runtimeCallbacksActive ? SystemDispatcher.CurrentFrameIndex : 0;
            float velocityMultiplier = math.lerp(
                CalmFlowVelocityMultiplier,
                StormFlowVelocityMultiplier,
                _weatherStormFlowBlend);
            float turbulenceFrequency = math.lerp(
                CalmTurbulenceFrequency,
                StormTurbulenceFrequency,
                _weatherStormFlowBlend);
            if (runtimeCallbacksActive)
                UpdateHudFogLuminanceDownsample();
            UpdateSuitHealthGlitchGlobal(runtimeCallbacksActive);

            float hudFogTargetLuminance01 = math.max(_hudFogTargetLuminance01, _hudFogDownsampledLuminance01);
            float hudDeltaTime = runtimeCallbacksActive ? math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime) : 0.0166667f;
            UpdateFlashlightPhotophobiaField(hudDeltaTime);
            float hudAlpha = ResolveDecayBlend(HudFogPerturbationResponse, hudDeltaTime);
            _hudFogSmoothedLuminance01 = math.lerp(
                _hudFogSmoothedLuminance01,
                hudFogTargetLuminance01,
                hudAlpha);
            float hudFogDensityBoost = _cachedVisualIsUnderwater
                ? _hudFogSmoothedLuminance01 * HudFogPerturbationMaxDensityBoost
                : 0f;
            float fogScatteringCoeff = math.max(0.0001f, _cachedFogDensity + hudFogDensityBoost);
            float hudVolumetricScatterBoost = _cachedVisualIsUnderwater
                ? _hudFogSmoothedLuminance01 * HudFogVolumetricScatterBoost
                : 0f;

            Shader.SetGlobalVector(
                _HectonNoirResolveSettingsId,
                new Vector4(
                    math.max(1f, noirFogPower),
                    math.max(0f, underwaterFinalDitherStrength),
                    _cachedVisualIsUnderwater ? 1f : 0f,
                    0f));
            Shader.SetGlobalColor(_HectonNoirAbyssFloorId, abyssFloor);
            Shader.SetGlobalVector(
                _HectonNoirFogStratificationId,
                new Vector4(
                    waterLevel,
                    1f / verticalFogSpan,
                    math.max(0f, abyssalDensityBoost),
                    fogScatteringCoeff));
            HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionRuntime(
                new Vector4(
                    waterLevel,
                    math.max(0f, _currentTurbidity),
                    _cachedVisualIsUnderwater ? 1f : 0f,
                    _cachedVisualIsUnderwater ? 1f : 0f));
            Shader.SetGlobalFloat(_FogScatteringCoeffId, fogScatteringCoeff);
            Shader.SetGlobalVector(
                _HectonHudFogPerturbationId,
                new Vector4(
                    _hudFogSmoothedLuminance01,
                    hudFogDensityBoost,
                    hudVolumetricScatterBoost,
                    fogScatteringCoeff));
            Shader.SetGlobalVector(
                _HectonNoirDitherParamsId,
                new Vector4(
                    math.frac(frameIndex * 0.75487766f),
                    math.frac(frameIndex * 0.56984029f),
                    64f,
                    0f));
            Shader.SetGlobalVector(
                _HectonNoirCausticsLayerAId,
                new Vector4(
                    math.max(0.02f, noirCausticsLayerAScale),
                    noirCausticsLayerAScrollX,
                    noirCausticsLayerAScrollZ,
                    causticsGate * math.saturate(noirCausticsLayerAStrength)));
            Shader.SetGlobalVector(
                _HectonNoirCausticsLayerBId,
                new Vector4(
                    math.max(0.02f, noirCausticsLayerBScale),
                    noirCausticsLayerBScrollX,
                    noirCausticsLayerBScrollZ,
                    causticsGate * math.saturate(noirCausticsLayerBStrength)));
            Shader.SetGlobalVector(
                _HectonNoirCausticsShapeId,
                new Vector4(
                    math.max(1f, noirCausticsSharpness),
                    math.max(0f, noirCausticsDistortion),
                    math.max(0.25f, noirCausticsDepthFadeStart),
                    math.max(0.25f, noirCausticsDepthFadeRange)));
            Shader.SetGlobalVector(
                _HectonNoirCaveAttenuationId,
                new Vector4(
                    math.saturate(biomeAbsorption),
                    0f,
                    0f,
                    0f));
            Shader.SetGlobalVector(
                _HectonFlowSynchronyParamsId,
                new Vector4(
                    velocityMultiplier,
                    turbulenceFrequency,
                    _sharedFlowSynchronyPhaseTime,
                    _weatherStormFlowBlend));
        }

        private static void UpdateSuitHealthGlitchGlobal(bool runtimeCallbacksActive)
        {
            float health01 = 1f;
            if (runtimeCallbacksActive &&
                UIStateStore.IsInitialized &&
                UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot))
            {
                health01 = math.saturate(healthSlot.Value);
            }

            float critical01 = health01 < SuitCriticalHealthThreshold01
                ? math.saturate((SuitCriticalHealthThreshold01 - health01) / SuitCriticalHealthThreshold01)
                : 0f;
            float easedCritical01 = critical01 * critical01 * (3f - 2f * critical01);
            Shader.SetGlobalVector(
                _HectonSuitHealthGlitchId,
                new Vector4(
                    easedCritical01,
                    health01,
                    easedCritical01 * 0.042f,
                    easedCritical01 * 0.013f));
        }

        private void ResetNoirResolveGlobals()
        {
            float waterLevel = ResolveWaterLevel();
            Shader.SetGlobalVector(_HectonNoirResolveSettingsId, new Vector4(1.18f, 0.75f, 0f, 0f));
            Shader.SetGlobalColor(_HectonNoirAbyssFloorId, new Color(0.028f, 0.042f, 0.060f, 1f));
            Shader.SetGlobalVector(_HectonNoirFogStratificationId, new Vector4(waterLevel, 1f / 180f, 0.42f, 0.0001f));
            HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionRuntime(new Vector4(waterLevel, 1f, 1f, 0f));
            Shader.SetGlobalVector(_HectonNoirDitherParamsId, new Vector4(0f, 0f, 64f, 0f));
            Shader.SetGlobalVector(_HectonNoirCausticsLayerAId, Vector4.zero);
            Shader.SetGlobalVector(_HectonNoirCausticsLayerBId, Vector4.zero);
            Shader.SetGlobalVector(_HectonNoirCausticsShapeId, new Vector4(3.4f, 0.38f, 12f, 8f));
            Shader.SetGlobalVector(_HectonNoirCaveAttenuationId, new Vector4(0.9f, 0f, 0f, 0f));
            Shader.SetGlobalVector(_HectonFlowSynchronyParamsId, new Vector4(1f, 0.26f, 0f, 0f));
            Shader.SetGlobalVector(_HectonHudFogPerturbationId, Vector4.zero);
            Shader.SetGlobalVector(_HectonSuitHealthGlitchId, Vector4.zero);
            Shader.SetGlobalVector(_HectonPhotophobiaFieldOriginScaleId, Vector4.zero);
            Shader.SetGlobalVector(_HectonPhotophobiaFieldStateId, Vector4.zero);
            Shader.SetGlobalFloat(_FogScatteringCoeffId, 0f);
        }

        private void UpdateFlowSynchronyState(float deltaTime)
        {
            float targetStormBlend = ResolveStormFlowBlend();
            UpdateStormFogColorDrift(deltaTime, targetStormBlend);
            if (deltaTime > 0f)
            {
                float normalizedStep = deltaTime / WeatherFlowResponseSeconds;
                _weatherStormFlowBlend = Mathf.MoveTowards(_weatherStormFlowBlend, targetStormBlend, normalizedStep);
                _sharedFlowSynchronyPhaseTime += deltaTime;
                if (_sharedFlowSynchronyPhaseTime >= 4096f)
                    _sharedFlowSynchronyPhaseTime -= 4096f;
            }
            else
            {
                _weatherStormFlowBlend = targetStormBlend;
            }
        }

        private void UpdateStormFogColorDrift(float deltaTime, float targetStormBlend)
        {
            Color targetFogColor = targetStormBlend > 0.001f
                ? StormFogDriftGreenGray
                : StormFogDriftDeepBlue;
            float maxDelta = deltaTime > 0f
                ? deltaTime * StormFogColorDriftRate
                : 1f;
            _stormFogDriftColor.r = Mathf.MoveTowards(_stormFogDriftColor.r, targetFogColor.r, maxDelta);
            _stormFogDriftColor.g = Mathf.MoveTowards(_stormFogDriftColor.g, targetFogColor.g, maxDelta);
            _stormFogDriftColor.b = Mathf.MoveTowards(_stormFogDriftColor.b, targetFogColor.b, maxDelta);
            _stormFogDriftColor.a = 1f;
        }

        private float ResolveStormFlowBlend()
        {
            IWeatherService weatherService = _weatherRuntime;
            if (weatherService != null &&
                (weatherService.CurrentWeatherState & WeatherState.Storm) != 0)
            {
                return 1f;
            }

            ISurfaceWeatherReadModel surfaceWeather = _surfaceWeatherRuntime;
            if (surfaceWeather == null || surfaceWeather.IsSurfaceSuppressed)
                return 0f;

            return surfaceWeather.CurrentWeatherKindCode == SurfaceWeatherKindCodes.ElectricalStorm ? 1f : 0f;
        }

        private void DisableUnderwaterSuspendedMotes(bool clearParticles)
        {
            if (underwaterSuspendedMotes == null)
                return;

            if (_cachedSuspendedMotesEmission != 0f)
            {
                ParticleSystem.EmissionModule emission = underwaterSuspendedMotes.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
                _cachedSuspendedMotesEmission = 0f;
            }

            if (_underwaterSuspendedMotesPlaying)
            {
                underwaterSuspendedMotes.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
                _underwaterSuspendedMotesPlaying = false;
            }

            _cachedBottomSiltBoost = 0f;

#if UNITY_EDITOR
            _debugSuspendedMotesEmission = 0f;
            _debugBottomDistance = 0f;
            _debugBottomSiltBoost = 0f;
#endif
        }

        private void DisableUnderwaterExhaleBubbles(bool clearParticles)
        {
            if (underwaterExhaleBubbles == null)
                return;

            if (underwaterExhaleBubbles.isPlaying || underwaterExhaleBubbles.particleCount > 0)
            {
                underwaterExhaleBubbles.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

#if UNITY_EDITOR
            _debugExhaleBubbleBurstCount = 0;
#endif
        }

        private void ConsumePlayerExhaleSignals()
        {
            ReadOnlySpan<PlayerExhaleSignal> signals = SignalBus<PlayerExhaleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                HandlePlayerExhale();
        }

        private void HandlePlayerExhale()
        {
            if (!enableExhaleBubbles || !_cachedVisualIsUnderwater)
                return;

            if (ResolveTransportHelmetExposureScale() <= 0.001f)
                return;

            if (ResolvePresentationClockSeconds() < _nextExhaleBubbleAllowedTime)
                return;

            if (underwaterMarineSnow == null)
                _runtimeVisualOwnerResolveRequested = true;

            int burstCount = ResolveExhaleBubbleBurstCount();
            if (burstCount <= 0)
                return;

            _nextExhaleBubbleAllowedTime = ResolvePresentationClockSeconds() + exhaleBubbleMinInterval;

            if (underwaterMarineSnow != null && underwaterMarineSnow.IsOperational)
            {
                _gpuBubbleExhaleImpulse01 = 1f;
                if (!_physicsEngineLookupAttempted)
                    RequestRuntimeServiceCacheCold();
                IFluidBubbleBurstSink fluidEngine = _fluidBubbleBurstSink;
                Transform bubbleOrigin = playerCamera != null ? playerCamera : transform;
                if (fluidEngine != null && bubbleOrigin != null)
                    fluidEngine.TryQueueAdvectedBubbleBurst(bubbleOrigin.position, burstCount, 1f);
#if UNITY_EDITOR
                _debugExhaleBubbleBurstCount = burstCount;
#endif
                return;
            }

            if (underwaterExhaleBubbles == null)
            {
                _runtimeVisualOwnerResolveRequested = true;
                return;
            }

            underwaterExhaleBubbles.Play(true);
            underwaterExhaleBubbles.Emit(burstCount);

#if UNITY_EDITOR
            _debugExhaleBubbleBurstCount = burstCount;
#endif
        }

        private int ResolveExhaleBubbleBurstCount()
        {
            int minBurst = math.max(0, exhaleBubbleMinBurstCount);
            int maxBurst = math.max(minBurst, exhaleBubbleMaxBurstCount);
            if (maxBurst <= 0)
                return 0;

            float depthFactor = math.saturate(
                _cachedVisualDepth / math.max(0.01f, exhaleBubbleFullDepth));
            float turbidityFactor = math.saturate((_currentTurbidity - 0.5f) * exhaleBubbleTurbidityWeight);
            float burstFactor = math.saturate(depthFactor * 0.65f + turbidityFactor * 0.35f);
            float transportExposureScale = ResolveTransportHelmetExposureScale();
            float burstValue = math.lerp(minBurst, maxBurst, burstFactor) *
                _ecologyBubbleMultiplier *
                _adaptiveBubbleScale *
                transportExposureScale;
            int burstCount = (int)(burstValue + 0.5f);
            return math.max(0, burstCount);
        }

        private float ResolvePlayerSpeedSquaredMetersPerSecond()
        {
            if (CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityUnderwaterVisualMaxAgeFrames, out float3 velocity))
                return math.lengthsq(velocity);

            return 0f;
        }

        private static float ResolveSquaredSpeedFactor(float speedSq, float minSpeed, float fullSpeed)
        {
            float minSpeedSq = math.max(0f, minSpeed * minSpeed);
            float fullSpeedSq = math.max(minSpeedSq + 0.0001f, fullSpeed * fullSpeed);
            return math.saturate((speedSq - minSpeedSq) * math.rcp(math.max(0.0001f, fullSpeedSq - minSpeedSq)));
        }

        private float ResolveBottomSiltEmissionBoost(bool isUnderwater)
        {
            if (!enableBottomSiltBoost || !isUnderwater)
            {
                _cachedBottomSiltBoost = 0f;
                _cachedBottomDistance = float.PositiveInfinity;
#if UNITY_EDITOR
                _debugBottomDistance = 0f;
                _debugBottomSiltBoost = 0f;
#endif
                return 0f;
            }

            if (playerCamera == null)
                _runtimeVisualOwnerResolveRequested = true;

            if (playerCamera == null)
            {
                _cachedBottomSiltBoost = 0f;
                _cachedBottomDistance = float.PositiveInfinity;
#if UNITY_EDITOR
                _debugBottomDistance = 0f;
                _debugBottomSiltBoost = 0f;
#endif
                return 0f;
            }

            RefreshBottomSiltProbe(playerCamera.position);

            float playerSpeedSq = CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityUnderwaterVisualMaxAgeFrames, out float3 kccVelocity)
                ? math.lengthsq(kccVelocity)
                : 0f;
            float distanceFactor = 1f - math.saturate(
                (_cachedBottomDistance - bottomSiltFullDistance) /
                math.max(0.01f, bottomSiltActivationDistance - bottomSiltFullDistance));
            float speedFactor = ResolveSquaredSpeedFactor(playerSpeedSq, bottomSiltMinSpeed, bottomSiltFullSpeed);

            float boost = bottomSiltEmissionBoost * distanceFactor * speedFactor * _adaptiveMotesScale + _externalBottomSiltBurstBoost;
            _cachedBottomSiltBoost = boost;

#if UNITY_EDITOR
            _debugBottomDistance = float.IsPositiveInfinity(_cachedBottomDistance) ? 0f : _cachedBottomDistance;
            _debugBottomSiltBoost = boost;
#endif
            return boost;
        }

        private void RefreshBottomSiltProbe(Vector3 probePosition)
        {
            if (ResolvePresentationClockSeconds() < _nextBottomSiltProbeTime)
                return;

            _nextBottomSiltProbeTime = ResolvePresentationClockSeconds() + math.max(0.05f, bottomSiltProbeInterval * _adaptiveBottomSiltProbeIntervalScale);
            _cachedBottomDistance = ResolveBottomSiltDistance(probePosition);
        }

        private float ResolveBottomSiltDistance(Vector3 probePosition)
        {
            MapMagicBridge bridge = _mapMagicRuntime;
            if (bridge != null && bridge.TryGetHeight(probePosition.x, probePosition.z, out float seafloorHeight))
                return math.max(0f, probePosition.y - seafloorHeight);

            return ResolveFakeBottomSiltDistance(probePosition);
        }

        private float ResolveFakeBottomSiltDistance(Vector3 probePosition)
        {
            float depth = math.max(0f, ResolveWaterLevel() - probePosition.y);
            float2 phase = new float2(probePosition.x * 0.017f, probePosition.z * 0.013f);
            float ridge = FastTriangleSignedRadians(phase.x + phase.y * 1.37f) * 0.5f +
                          FastTriangleSignedRadians(phase.x * -1.91f + phase.y * 0.73f + 2.17f) * 0.25f;
            float distance = math.lerp(18f, 140f, math.saturate(depth / 220f)) + ridge * 12f;
            return math.max(4f, distance);
        }

        private static float FastTriangleSignedRadians(float radians)
        {
            float phase = radians * 0.159154943f;
            int whole = (int)phase;
            phase -= whole;
            if (phase < 0f)
                phase += 1f;
            else if (phase >= 1f)
                phase -= 1f;

            return 1f - (4f * math.abs(phase - 0.5f));
        }

        internal void TriggerExternalBottomSiltBurst(float intensity01)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0f)
                return;

            float requestedBoost = bottomSiltEmissionBoost * clampedIntensity;
            if (requestedBoost > _externalBottomSiltBurstBoost)
                _externalBottomSiltBurstBoost = requestedBoost;
        }

        private void DecayExternalBottomSiltBurst(float deltaTime)
        {
            if (_externalBottomSiltBurstBoost <= 0f || deltaTime <= 0f)
                return;

            _externalBottomSiltBurstBoost = math.max(
                0f,
                _externalBottomSiltBurstBoost - bottomSiltBurstRecoverySpeed * bottomSiltEmissionBoost * deltaTime);
        }

        private void UpdateShallowSunBeam(float depth, float lightFactor, bool isUnderwater, Vector3 canopyAnchorWS, float canopyWindow01, float canopyOcclusion01)
        {
            if (shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                _runtimeVisualOwnerResolveRequested = true;

            if (shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                return;

            float targetIntensity = 0f;
            bool shouldActivate = false;

            if (enableShallowSunBeam && isUnderwater)
            {
                float fadeIn = math.saturate(depth / math.max(0.01f, shallowSunBeamFadeInDepth));
                float fadeOut = 1f - math.saturate(
                    (depth - shallowSunBeamFadeInDepth) /
                    math.max(0.01f, shallowSunBeamFadeOutDepth - shallowSunBeamFadeInDepth));
                float lightFade = math.saturate(
                    (lightFactor - shallowSunBeamMinLightFactor) /
                    math.max(0.0001f, 1f - shallowSunBeamMinLightFactor));
                float beamFactor = fadeIn * fadeOut * lightFade * ResolveHorizonFade();
                if (enableSargassumCanopyLighting)
                {
                    float canopyWindowFactor = LerpClamped(
                        1f - sargassumCanopyBeamOcclusionStrength,
                        1f,
                        canopyWindow01 * sargassumCanopyBeamWindowBoost);
                    beamFactor *= canopyWindowFactor;
                    beamFactor *= 1f - (canopyOcclusion01 * sargassumCanopyBeamOcclusionStrength);
                }
                targetIntensity = shallowSunBeamMaxLightIntensity * beamFactor * _ecologySunBeamMultiplier * _adaptiveBeamScale * _soundscapeBeamScale;
                shouldActivate = targetIntensity > 0.001f;
            }

            if (shouldActivate)
            {
                GameObject beamObject = _shallowSunBeamTransform.gameObject;
                if (!_shallowSunBeamActive && !beamObject.activeSelf)
                    beamObject.SetActive(true);

                if (sunLight != null)
                {
                    Vector3 beamDirection = sunLight.transform.forward;
                    if (beamDirection.sqrMagnitude > 0.0001f)
                    {
                        beamDirection = ResolveSafeDirection(beamDirection, Vector3.forward);
                        Vector3 beamUp = math.abs(Vector3.Dot(beamDirection, Vector3.up)) > 0.98f
                            ? (mainCamera != null ? mainCamera.transform.right : Vector3.right)
                            : Vector3.up;
                        _shallowSunBeamTransform.rotation = Quaternion.LookRotation(beamDirection, beamUp);
                    }
                }

                Vector3 beamLocalPosition = _shallowSunBeamBaseLocalPosition;
                if (enableSargassumCanopyLighting && mainCamera != null && canopyOcclusion01 > 0.0001f)
                {
                    Vector3 canopyDeltaWS = canopyAnchorWS - mainCamera.transform.position;
                    canopyDeltaWS.y = 0f;
                    float maxOffset = math.max(0.01f, sargassumCanopyBeamAnchorMaxOffset);
                    canopyDeltaWS = Vector3.ClampMagnitude(canopyDeltaWS, maxOffset);
                    Vector3 canopyDeltaLS = mainCamera.transform.InverseTransformVector(canopyDeltaWS);
                    canopyDeltaLS.z = 0f;
                    beamLocalPosition += canopyDeltaLS * (sargassumCanopyBeamAnchorTracking * canopyWindow01);
                }

                if ((_shallowSunBeamTransform.localPosition - beamLocalPosition).sqrMagnitude > 0.000001f)
                    _shallowSunBeamTransform.localPosition = beamLocalPosition;

                if (math.abs(targetIntensity - _cachedShallowSunBeamLightIntensity) > 0.01f)
                {
                    shallowSunBeamLight.intensity = targetIntensity;
                    _cachedShallowSunBeamLightIntensity = targetIntensity;
                }

                _shallowSunBeamActive = true;
            }
            else
            {
                DisableShallowSunBeam(true);
            }

#if UNITY_EDITOR
            _debugShallowSunBeamIntensity = targetIntensity;
#endif
        }

        private void DisableShallowSunBeam(bool setInactive)
        {
            if (shallowSunBeamLight != null && _cachedShallowSunBeamLightIntensity != 0f)
            {
                shallowSunBeamLight.intensity = 0f;
                _cachedShallowSunBeamLightIntensity = 0f;
            }

            if (_shallowSunBeamTransform != null && _shallowSunBeamActive && setInactive)
                _shallowSunBeamTransform.gameObject.SetActive(false);

            _shallowSunBeamActive = false;

#if UNITY_EDITOR
            _debugShallowSunBeamIntensity = 0f;
#endif
        }

        private float ResolveCausticsStrength(float depth, float lightFactor, bool isUnderwater)
        {
            if (!enableShallowCaustics || !isUnderwater)
            {
#if UNITY_EDITOR
                _debugCausticsStrength = 0f;
#endif
                return 0f;
            }

            float fadeIn = math.saturate(depth / math.max(0.01f, causticsFadeInDepth));
            float fadeOut = 1f - math.saturate(
                (depth - causticsFadeInDepth) /
                math.max(0.01f, causticsFadeOutDepth - causticsFadeInDepth));
            float lightFade = math.saturate(
                (lightFactor - causticsMinLightFactor) /
                math.max(0.0001f, 1f - causticsMinLightFactor));

            float strength = causticsStrengthScale * fadeIn * fadeOut * lightFade * _adaptiveCausticsScale * _soundscapeCausticsScale;

#if UNITY_EDITOR
            _debugCausticsStrength = strength;
#endif
            return strength;
        }

        private void ApplyEcologyContext(HectonBiomeMatrixProfile profile)
        {
            _currentFaunaMood = profile != null ? profile.faunaMood : WorldProceduralFaunaMood.None;

            HectonFaunaFamilyProfile faunaFamily = profile != null &&
                                                  profile.familyProfile != null
                ? profile.familyProfile.faunaFamilyProfile
                : null;

            _currentFaunaAmbienceSummary = faunaFamily != null ? faunaFamily.ambienceSummary : null;

            switch (_currentFaunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    _ecologySuspendedMotesMultiplier = 0.92f;
                    _ecologyBubbleMultiplier = 0.94f;
                    _ecologySunBeamMultiplier = 1f + ecologySunBeamWeight;
                    break;

                case WorldProceduralFaunaMood.Lively:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight;
                    _ecologySunBeamMultiplier = 1f + ecologySunBeamWeight * 0.4f;
                    break;

                case WorldProceduralFaunaMood.Mixed:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight * 0.55f;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight * 0.45f;
                    _ecologySunBeamMultiplier = 1f;
                    break;

                case WorldProceduralFaunaMood.Hostile:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight * 0.75f;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight * 0.7f;
                    _ecologySunBeamMultiplier = 1f - ecologySunBeamWeight;
                    break;

                default:
                    _ecologySuspendedMotesMultiplier = 1f;
                    _ecologyBubbleMultiplier = 1f;
                    _ecologySunBeamMultiplier = 1f;
                    break;
            }

#if UNITY_EDITOR
            _debugFaunaMood = ResolveFaunaMoodDebugName(_currentFaunaMood);
            _debugFaunaAmbience = string.IsNullOrWhiteSpace(_currentFaunaAmbienceSummary)
                ? "None"
                : _currentFaunaAmbienceSummary;
            _debugEcologyMotesMultiplier = _ecologySuspendedMotesMultiplier;
            _debugEcologyBubbleMultiplier = _ecologyBubbleMultiplier;
            _debugEcologyBeamMultiplier = _ecologySunBeamMultiplier;
#endif
        }

        private void ValidateReferences()
        {
            if (Application.isPlaying)
            {
                ResolvePlayerCamera();
                ResolveMainCamera();
                EnsurePrimarySunReference();
                ResolveSunVisualTransform();
            }

            if (biomePalette == null)
                Hecton8.Core.H8Debug.LogWarning("[HectonUnderwaterVisuals] biomePalette not assigned.", this);
            if (oceanUnderwaterMaterial == null)
                Hecton8.Core.H8Debug.LogWarning("[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.", this);
            if (skyMaterial == null)
                Hecton8.Core.H8Debug.LogWarning("[HectonUnderwaterVisuals] skyMaterial not assigned.", this);
            if (globalLightCurve == null || globalLightCurve.length == 0)
                Hecton8.Core.H8Debug.LogError("[HectonUnderwaterVisuals] globalLightCurve is empty!", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnIfRuntimeReferencesStillMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_runtimeVisualCallbacksActive)
                return;

            if (ResolvePresentationClockSeconds() < _nextRuntimeReferenceWarningTime)
                return;

            _nextRuntimeReferenceWarningTime = ResolvePresentationClockSeconds() + 5f;

            WarnIfRuntimeReferenceMissing(
                playerCamera == null,
                RuntimeReferenceWarningPlayerCamera,
                "[HectonUnderwaterVisuals] playerCamera still unresolved after runtime retry.");

            WarnIfRuntimeReferenceMissing(
                mainCamera == null,
                RuntimeReferenceWarningMainCamera,
                "[HectonUnderwaterVisuals] mainCamera still unresolved after runtime retry.");

            WarnIfRuntimeReferenceMissing(
                sunVisualTransform == null,
                RuntimeReferenceWarningSunVisual,
                "[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnIfRuntimeReferenceMissing(bool missing, byte warningMask, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!missing)
            {
                _runtimeReferenceWarningMask = (byte)(_runtimeReferenceWarningMask & ~warningMask);
                return;
            }

            if ((_runtimeReferenceWarningMask & warningMask) != 0)
                return;

            _runtimeReferenceWarningMask |= warningMask;
            Hecton8.Core.H8Debug.LogWarning(message, this);
#endif
        }

        private void ResolveBiomeMatrixDirector()
        {
            if (biomeMatrixDirector != null && biomeMatrixDirector.isActiveAndEnabled)
                return;

            biomeMatrixDirector = null;
            if (Application.isPlaying)
            {
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
                return;
            }

#if UNITY_EDITOR
            biomeMatrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
#endif
        }

        private void ApplyCurrentMatrixVisualOverride()
        {
            if (biomeMatrixDirector == null)
                return;

            HandleMatrixBiomeChanged(biomeMatrixDirector.CurrentProfile);
        }

        private HectonBiomeProfile ResolveActiveMatrixRuntimeVisualProfile()
        {
            if (_matrixRuntimeVisualProfile != null)
                return _matrixRuntimeVisualProfile;

            if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentProfile == null)
                return null;

            return biomeMatrixDirector.CurrentProfile.runtimeVisualProfile;
        }

        private void CaptureBaseValues()
        {
            if (_baseValuesCaptured) return;
            if (sunFlare != null)
                _baseFlareIntensity = sunFlare.intensity;
            _baseValuesCaptured = true;
            DisableLegacySunFlare();
        }

        private void RestoreBaseValues()
        {
            if (!_baseValuesCaptured) return;
            DisableLegacySunFlare();
        }

        private void DisableLegacySunFlare()
        {
            if (sunFlare == null)
                return;

            sunFlare.intensity = 0f;
            if (sunFlare.enabled)
                sunFlare.enabled = false;
        }

        private void ApplySpaceCameraDepthState(float depth, bool isUnderwater)
        {
            if (!_runtimeVisualCallbacksActive)
                return;

            Camera validatedMainCamera = ResolveValidCameraReference(ref mainCamera);
            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera == null)
                _runtimeVisualOwnerResolveRequested = true;

            bool canFallbackToMainCameraMask = _runtimeCameraStackFallbackActive && validatedMainCamera != null;
            if (spaceCamera == null && !canFallbackToMainCameraMask)
                return;

            bool shouldSuppress = ShouldSuppressSpaceCamera(depth, isUnderwater);
            if (_spaceCameraSuppressed == shouldSuppress)
                return;

            if (!_spaceCameraMaskCaptured && spaceCamera != null)
            {
                _spaceCameraOriginalCullingMask = spaceCamera.cullingMask;
                _spaceCameraMaskCaptured = true;
            }

            if (_runtimeCameraStackFallbackActive && validatedMainCamera != null)
            {
                int visibleMask = _mainCameraOriginalCullingMask | _CelestialLayerMask;
                int hiddenMask = visibleMask & ~_CelestialLayerMask;
                int targetMask = shouldSuppress ? hiddenMask : visibleMask;
                if (validatedMainCamera.cullingMask != targetMask)
                    validatedMainCamera.cullingMask = targetMask;
            }

            if (spaceCamera != null)
                spaceCamera.cullingMask = shouldSuppress ? 0 : _spaceCameraOriginalCullingMask;

            _spaceCameraSuppressed = shouldSuppress;
        }

        private bool ShouldSuppressSpaceCamera(float depth, bool isUnderwater)
        {
            if (!isUnderwater)
                return false;

            float renderScale = 1f;
            DynamicResolutionScaler scaler = _dynamicResolutionRuntime;
            if (scaler != null)
                renderScale = math.saturate(scaler.CurrentRenderScale);

            float depthReleaseThreshold = math.max(0f, deepCelestialCullDepth - deepCelestialCullDepthHysteresis);
            float adaptiveDepthReleaseThreshold = math.max(0f, adaptiveSpaceCameraCullMinDepth - deepCelestialCullDepthHysteresis);

            if (_spaceCameraSuppressed)
            {
                bool keepDepthSuppressed = depth >= depthReleaseThreshold;
                bool keepPerfSuppressed =
                    enableAdaptiveSpaceCameraCull &&
                    depth >= adaptiveDepthReleaseThreshold &&
                    renderScale <= adaptiveSpaceCameraRestoreRenderScale;
                return keepDepthSuppressed || keepPerfSuppressed;
            }

            bool suppressByDepth = depth >= deepCelestialCullDepth;
            bool suppressByPerf =
                enableAdaptiveSpaceCameraCull &&
                depth >= adaptiveSpaceCameraCullMinDepth &&
                renderScale <= adaptiveSpaceCameraCullRenderScale;
            return suppressByDepth || suppressByPerf;
        }

        private void RestoreSpaceCameraDefaults()
        {
            if (mainCamera != null && _cameraCompositionDefaultsCaptured)
                mainCamera.cullingMask = _mainCameraOriginalCullingMask;

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera == null || !_spaceCameraMaskCaptured)
            {
                _spaceCameraSuppressed = false;
                return;
            }

            spaceCamera.cullingMask = _spaceCameraOriginalCullingMask;
            _spaceCameraSuppressed = false;
        }

        private static bool IsCameraReferenceValid(Camera camera)
        {
            if (ReferenceEquals(camera, null))
                return false;

            try
            {
                return camera != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnassignedReferenceException)
            {
                return false;
            }
        }

        private static Camera ResolveValidCameraReference(ref Camera camera)
        {
            if (IsCameraReferenceValid(camera))
                return camera;

            camera = null;
            return null;
        }

        private float ResolveWaterLevel()
        {
            if (_playerMovement != null &&
                TryResolveRuntimeVisualWaterLevel(_playerMovement.CurrentWaterSurfaceY, out float playerWaterSurfaceY))
            {
                return playerWaterSurfaceY;
            }

            IHectonOceanKinematics oceanKinematics = ReadCachedOceanKinematicsProvider();
            if (oceanKinematics != null &&
                TryResolveRuntimeVisualWaterLevel(oceanKinematics.SeaLevel, out float oceanSeaLevel))
            {
                return oceanSeaLevel;
            }

            if (atmosphereManager != null)
            {
                float atmosphereSeaLevel = atmosphereManager.SeaLevelY;
                if (TryResolveRuntimeVisualWaterLevel(atmosphereSeaLevel, out float resolvedAtmosphereSeaLevel))
                    return resolvedAtmosphereSeaLevel;
            }

            float terrainWaterLevel = SanitizeVisualWaterLevel(waterLevelFallback, DefaultWaterLevelFallback);
            bool hasTerrainWaterLevel = false;
            MapMagicBridge terrainRuntime = _mapMagicRuntime;
            if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainRuntime) &&
                TryResolveVisualWaterLevel(terrainRuntime.WaterSurfaceLevel, out float mapMagicWaterLevel))
            {
                _mapMagicRuntime = terrainRuntime;
                terrainWaterLevel = mapMagicWaterLevel;
                hasTerrainWaterLevel = true;
            }

            if (!_physicsEngineLookupAttempted)
                RequestRuntimeServiceCacheCold();
            if (_physicsEngine != null)
            {
                float fluidWaterLevel = _physicsEngine.WaterLevel;
                if (TryResolveRuntimeVisualWaterLevel(fluidWaterLevel, out float resolvedFluidWaterLevel) &&
                    (!hasTerrainWaterLevel || math.abs(resolvedFluidWaterLevel - terrainWaterLevel) <= 128f))
                {
                    return SanitizeVisualWaterLevel(resolvedFluidWaterLevel, terrainWaterLevel);
                }
            }

            if (hasTerrainWaterLevel)
                return SanitizeVisualWaterLevel(terrainWaterLevel, terrainWaterLevel);

            return SanitizeVisualWaterLevel(waterLevelFallback, waterLevelFallback);
        }

        private static bool TryResolveRuntimeVisualWaterLevel(float waterLevel, out float resolvedWaterLevel)
        {
            if (math.isfinite(waterLevel) &&
                math.abs(waterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                resolvedWaterLevel = waterLevel;
                return true;
            }

            resolvedWaterLevel = DefaultWaterLevelFallback;
            return false;
        }

        private float SanitizeVisualWaterLevel(float waterLevel, float fallbackWaterLevel)
        {
            float safeFallback = TryResolveVisualWaterLevel(fallbackWaterLevel, out float resolvedFallbackWaterLevel)
                ? resolvedFallbackWaterLevel
                : DefaultWaterLevelFallback;
            if (!TryResolveVisualWaterLevel(waterLevel, out float resolvedWaterLevel))
                return safeFallback;

            Camera camera = mainCamera;
            if (camera == null && playerCamera != null)
            {
                if (!ReferenceEquals(_cachedPlayerCameraTransform, playerCamera))
                {
                    _cachedPlayerCameraTransform = playerCamera;
                    playerCamera.TryGetComponent(out _cachedPlayerCameraComponent);
                }
                camera = _cachedPlayerCameraComponent;
            }

            if (camera != null && math.isfinite(camera.transform.position.y) &&
                math.abs(resolvedWaterLevel - camera.transform.position.y) > 1000f)
            {
                return safeFallback;
            }

            return resolvedWaterLevel;
        }

        private static bool TryResolveVisualWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevelFallback;
            return false;
        }

        private float ResolveCurrentDepth()
        {
#if UNITY_EDITOR
            if (!_runtimeVisualCallbacksActive)
                return ResolveActiveVisualCameraDepth();
#endif

            float cameraDepth = ResolveActiveVisualCameraDepth();

            if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                float movementDepth = math.max(0f, movementState.DepthMeters);
                if (cameraDepth > movementDepth + VisualCameraDepthOverrideThreshold)
                    return cameraDepth;

                return movementDepth;
            }

            if (HasPlayerRuntimeContext())
                return 0f;

            if (_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth))
            {
                float movementDepth = math.max(0f, _playerMovement.CurrentDepth);
                if (cameraDepth > movementDepth + VisualCameraDepthOverrideThreshold)
                    return cameraDepth;

                return movementDepth;
            }

            return cameraDepth;
        }

        private bool ResolveUnderwaterVisualState(float depth)
        {
            return ResolveUnderwaterVisualStateForCameraDepth(depth, ResolveActiveVisualCameraDepth());
        }

        private bool ResolveUnderwaterVisualStateForCameraDepth(float depth, float cameraDepth)
        {
            if (cameraDepth <= 0f)
                cameraDepth = depth;
            float visualDepth = math.max(depth, cameraDepth);

            if (visualDepth <= VisualExitUnderwaterDepth)
                return false;

            bool depthDrivenUnderwater = SurfaceStateUtility.ResolveUnderwaterFromDepth(
                visualDepth,
                _wasUnderwater,
                VisualEnterUnderwaterDepth,
                VisualExitUnderwaterDepth);

#if UNITY_EDITOR
            if (!_runtimeVisualCallbacksActive)
            {
                return depthDrivenUnderwater;
            }
#endif

            if (TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                if (visualDepth >= VisualForcedUnderwaterDepth)
                    return true;

                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u)
                    return true;

                return depthDrivenUnderwater;
            }

            if (HasPlayerRuntimeContext())
                return false;

            if (_playerMovement != null)
            {
                if (visualDepth >= VisualForcedUnderwaterDepth)
                    return true;

                switch (_playerMovement.CurrentLocomotionMode)
                {
                    case PlayerLocomotionMode.UnderwaterSwim:
                        return true;

                    case PlayerLocomotionMode.ExosuitLocomotion:
                        return depth > 0.01f || _playerMovement.IsPlayerSubmerged || depthDrivenUnderwater;

                    case PlayerLocomotionMode.SurfaceSwim:
                        return _playerMovement.IsPlayerSubmerged || depthDrivenUnderwater;

                    default:
                        return depthDrivenUnderwater;
                }
            }

            return depthDrivenUnderwater;
        }

        private bool TryResolvePlayerMovementRuntimeState(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;

            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out movementState) &&
                IsUsablePlayerMovementRuntimeState(in movementState))
            {
                return true;
            }

            playerContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out movementState) ||
                !IsUsablePlayerMovementRuntimeState(in movementState))
            {
                movementState = default;
                return false;
            }

            return true;
        }

        private static bool IsUsablePlayerMovementRuntimeState(in PlayerMovementRuntimeState movementState)
        {
            return (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                   math.isfinite(movementState.DepthMeters) &&
                   math.all(math.isfinite(movementState.WorldPosition));
        }

        private bool HasPlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            return playerContext != null ||
                   PlayerRuntimeContextService.ActiveRuntimeContext != null;
        }

        private float ResolveActiveVisualCameraDepth()
        {
#if UNITY_EDITOR
            if (!_runtimeVisualCallbacksActive)
            {
                Camera editorPreviewCamera = null;
                if (mainCamera != null && mainCamera.enabled && mainCamera.gameObject.activeInHierarchy)
                {
                    editorPreviewCamera = mainCamera;
                }
                else if (playerCamera != null)
                {
                    Camera cachedMainCamera = mainCamera;
                    if (cachedMainCamera != null &&
                        ReferenceEquals(cachedMainCamera.transform, playerCamera) &&
                        cachedMainCamera.enabled &&
                        cachedMainCamera.gameObject.activeInHierarchy)
                    {
                        editorPreviewCamera = cachedMainCamera;
                    }
                }

                if (editorPreviewCamera != null)
                    return math.max(0f, ResolveWaterLevel() - editorPreviewCamera.transform.position.y);

                SceneView sceneView = SceneView.lastActiveSceneView;
                Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
                if (sceneViewCamera != null)
                    return math.max(0f, ResolveWaterLevel() - sceneViewCamera.transform.position.y);
            }
#endif

            if (playerCamera != null)
                return math.max(0f, ResolveWaterLevel() - playerCamera.position.y);

            return 0f;
        }

        private float ResolveVisualDepthForCamera(Camera camera)
        {
            if (camera == null)
                return ResolveActiveVisualCameraDepth();

            return math.max(0f, ResolveWaterLevel() - camera.transform.position.y);
        }

        private void CachePlayerMovement(Transform playerTransform)
        {
            HectonPlayerMovement nextPlayerMovement = null;
            PlayerTransportCoordinator nextPlayerTransportCoordinator = null;

            if (playerTransform != null)
            {
                IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
                if (runtimeContext != null && ReferenceEquals(runtimeContext.PlayerTransform, playerTransform))
                {
                    nextPlayerMovement = runtimeContext.PlayerMovement;
                    nextPlayerTransportCoordinator = runtimeContext.PlayerTransportCoordinator;
                }
                else
                {
                    IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                    if (playerContext != null && ReferenceEquals(playerContext.PlayerTransform, playerTransform))
                    {
                        nextPlayerMovement = playerContext.PlayerMovement;
                        nextPlayerTransportCoordinator = playerContext.PlayerTransportCoordinator;
                    }
                }
            }

            if (!ReferenceEquals(_subscribedPlayerMovement, nextPlayerMovement))
            {
                UnsubscribePlayerMovement(_subscribedPlayerMovement);
                SubscribePlayerMovement(nextPlayerMovement);
            }

            _playerMovement = nextPlayerMovement;
            _playerTransportCoordinator = nextPlayerTransportCoordinator;
            _debugPlayerMovementFound = _playerMovement != null;
            if (_playerMovement == null)
                _debugPlayerMovementSource = "Unresolved";
        }

        private float ResolveTransportHelmetExposureScale()
        {
            if (_playerTransportCoordinator == null)
                return 1f;

            PlayerTransportFeelContract transportFeelContract = _playerTransportCoordinator.ResolveTransportFeelContract();
            if (transportFeelContract == null)
                return 1f;

            switch (transportFeelContract.OccupancyMode)
            {
                case PlayerTransportOccupancyMode.EnclosedCabin:
                    return 0f;

                case PlayerTransportOccupancyMode.Exosuit:
                    return math.min(0.5f, math.saturate(transportFeelContract.SwimPresentationScale));

                default:
                    return 1f;
            }
        }

        private void UpdateTransportCockpitOverlay()
        {
            if (transitionCameraVfx == null)
                return;

            if (_playerTransportCoordinator == null)
            {
                transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                return;
            }

            PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
            if (transportPreset == null)
            {
                transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                return;
            }

            switch (_playerTransportCoordinator.ResolveTransportOccupancyMode())
            {
                case PlayerTransportOccupancyMode.Exosuit:
                    transitionCameraVfx.SetTransportCockpitOverlay(
                        transportPreset.CockpitVignetteIntensity > 0f ? transportPreset.CockpitVignetteIntensity : 0.12f,
                        transportPreset.CockpitVignetteRoundness > 0f ? transportPreset.CockpitVignetteRoundness : 0.38f,
                        transportPreset.CockpitVignetteSmoothness > 0f ? transportPreset.CockpitVignetteSmoothness : 0.52f,
                        transportPreset.CockpitChromaticAberration);
                    return;

                case PlayerTransportOccupancyMode.EnclosedCabin:
                    transitionCameraVfx.SetTransportCockpitOverlay(
                        transportPreset.CockpitVignetteIntensity > 0f ? transportPreset.CockpitVignetteIntensity : 0.24f,
                        transportPreset.CockpitVignetteRoundness > 0f ? transportPreset.CockpitVignetteRoundness : 0.96f,
                        transportPreset.CockpitVignetteSmoothness > 0f ? transportPreset.CockpitVignetteSmoothness : 0.44f,
                        transportPreset.CockpitChromaticAberration > 0f ? transportPreset.CockpitChromaticAberration : 0.04f);
                    return;

                default:
                    transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                    return;
            }
        }

        private bool TryCachePlayerMovementFromTransformHierarchy(Transform anchor, string sourceLabel)
        {
            if (anchor == null)
                return false;

            Transform playerRoot = null;
            HectonPlayerMovement movement = null;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext != null)
            {
                playerRoot = runtimeContext.PlayerTransform;
                movement = runtimeContext.PlayerMovement;
            }
            else
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                {
                    playerRoot = playerContext.PlayerTransform;
                    movement = playerContext.PlayerMovement;
                }
            }

            if (playerRoot == null || movement == null)
                return false;

            Transform current = anchor;
            while (current != null)
            {
                if (ReferenceEquals(current, playerRoot) || ReferenceEquals(current, movement.transform))
                {
                    CachePlayerMovement(playerRoot);
                    _debugPlayerMovementSource = sourceLabel;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void SubscribePlayerMovement(HectonPlayerMovement movement)
        {
            if (movement == null)
                return;

            _subscribedPlayerMovement = movement;
        }

        private void UnsubscribePlayerMovement(HectonPlayerMovement movement)
        {
            if (movement == null)
                return;

            if (ReferenceEquals(_subscribedPlayerMovement, movement))
                _subscribedPlayerMovement = null;
        }

        private void CachePhysicsEngine()
        {
            if (!Application.isPlaying)
            {
                _physicsEngine = null;
                _fluidBubbleBurstSink = null;
                _physicsEngineCached = false;
                _physicsEngineLookupAttempted = false;
#if UNITY_EDITOR
                _debugPhysicsEngineFound = false;
#endif
                return;
            }
            _physicsEngine = GlobalRegistry.FluidSurfaceCurrent;
            _fluidBubbleBurstSink = GlobalRegistry.FluidBubbleBurstSink;
            _physicsEngineLookupAttempted = true;
            _physicsEngineCached = _physicsEngine != null;
#if UNITY_EDITOR
            _debugPhysicsEngineFound = _physicsEngineCached;
#endif
        }

        private void CacheOceanKinematicsRuntimeCold()
        {
            if (!Application.isPlaying)
            {
                _oceanKinematicsService = null;
                _oceanKinematicsProvider = null;
                return;
            }

            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _oceanKinematicsProvider = _oceanKinematicsService != null
                ? _oceanKinematicsService.ActiveProvider
                : null;
        }

        private IHectonOceanKinematics ReadCachedOceanKinematicsProvider()
        {
            IHectonOceanKinematics provider = _oceanKinematicsProvider;
            if (provider != null)
                return provider;

            IHectonOceanKinematicsService service = _oceanKinematicsService;
            provider = service != null ? service.ActiveProvider : null;
            _oceanKinematicsProvider = provider;
            return provider;
        }

        private void InitializeCurrentValues()
        {
            HectonBiomeProfile initial = null;

            if (biomePalette != null)
            {
                initial = biomePalette.SurfaceProfile;
                if (initial == null && biomePalette.Count > 0)
                    initial = biomePalette.GetProfile(0);
            }

            if (initial != null)
            {
                SetCurrentFromProfile(initial);
                SetTargetFromProfile(initial);
            }
            else
            {
                _currentScatterBase     = ResolveSafeOceanColor(
                    ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_Diffuse, new Color(0f, 0.03f, 0.07f, 1f)),
                    new Color(0f, 0.03f, 0.07f, 1f));
                _currentScatterShallow  = ResolveSafeOceanColor(
                    ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_SubSurfaceShallowCol, new Color(0f, 0.15f, 0.12f, 1f)),
                    new Color(0f, 0.15f, 0.12f, 1f));
                _currentDepthFogDensity = ResolveFallbackDepthFogDensity(oceanUnderwaterMaterial);
                _currentFogColor        = ResolveFallbackFogColor();
                _currentTurbidity       = 1.0f;
                _currentBiomeFogDensityScale = 1f;
                biomeAbsorption         = 0.9f;
                _currentAmbientColor    = underwaterAmbientColor;

                _targetScatterBase     = _currentScatterBase;
                _targetScatterShallow  = _currentScatterShallow;
                _targetDepthFogDensity = _currentDepthFogDensity;
                _targetFogColor        = _currentFogColor;
                _targetTurbidity       = 1.0f;
                _targetBiomeFogDensityScale = 1f;
                _targetBiomeAbsorption = biomeAbsorption;
                _targetAmbientColor    = _currentAmbientColor;
            }

            _transitionProgress = 1f;
            _targetBiomeIndex = 0;
        }

        private void SetCurrentFromProfile(HectonBiomeProfile p)
        {
            _currentScatterBase     = ResolveProfileScatterBase(p);
            _currentScatterShallow  = ResolveProfileScatterShallow(p);
            _currentDepthFogDensity = ResolveProfileDepthFogDensity(p);
            _currentFogColor        = ResolveProfileFogColor(p);
            _currentTurbidity       = ResolveProfileTurbidity(p);
            _currentBiomeFogDensityScale = ResolveProfileFogDensityScale(p);
            biomeAbsorption         = ResolveProfileAbsorption(p);
            _currentAmbientColor    = underwaterAmbientColor;
        }

        private void SetTargetFromProfile(HectonBiomeProfile p)
        {
            _targetScatterBase     = ResolveProfileScatterBase(p);
            _targetScatterShallow  = ResolveProfileScatterShallow(p);
            _targetDepthFogDensity = ResolveProfileDepthFogDensity(p);
            _targetFogColor        = ResolveProfileFogColor(p);
            _targetTurbidity       = ResolveProfileTurbidity(p);
            _targetBiomeFogDensityScale = ResolveProfileFogDensityScale(p);
            _targetBiomeAbsorption = ResolveProfileAbsorption(p);
            _targetAmbientColor    = underwaterAmbientColor;
            _transitionProgress    = 0f;
        }

        private Color ResolveProfileScatterBase(HectonBiomeProfile profile)
        {
            Color fallback = ReadMaterialColorOrDefault(
                oceanUnderwaterMaterial,
                _ID_Diffuse,
                new Color(0f, 0.03f, 0.07f, 1f));
            return ResolveSafeOceanColor(
                profile != null ? profile.scatterColorBase : fallback,
                fallback);
        }

        private Color ResolveProfileScatterShallow(HectonBiomeProfile profile)
        {
            Color fallback = ReadMaterialColorOrDefault(
                oceanUnderwaterMaterial,
                _ID_SubSurfaceShallowCol,
                new Color(0f, 0.15f, 0.12f, 1f));
            return ResolveSafeOceanColor(
                profile != null ? profile.scatterColorShallow : fallback,
                fallback);
        }

        private Vector3 ResolveProfileDepthFogDensity(HectonBiomeProfile profile)
        {
            Vector3 fallback = ResolveFallbackDepthFogDensity(oceanUnderwaterMaterial);
            Vector3 source = profile != null ? profile.depthFogDensity : fallback;
            return new Vector3(
                Mathf.Clamp(source.x > 0f ? source.x : fallback.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.y > 0f ? source.y : fallback.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.z > 0f ? source.z : fallback.z, minFogDensity, maxFogDensity));
        }

        private Color ResolveProfileFogColor(HectonBiomeProfile profile)
        {
            Color fallback = ResolveFallbackFogColor();
            return ResolveSafeOceanColor(
                profile != null ? profile.fogColor : fallback,
                fallback);
        }

        private static float ResolveProfileTurbidity(HectonBiomeProfile profile)
        {
            return profile != null && profile.turbidityMultiplier > 0f
                ? profile.turbidityMultiplier
                : 1f;
        }

        private float ResolveProfileFogDensityScale(HectonBiomeProfile profile)
        {
            Vector3 fallback = ResolveFallbackDepthFogDensity(oceanUnderwaterMaterial);
            Vector3 source = profile != null ? ResolveProfileDepthFogDensity(profile) : fallback;
            float fallbackAverage = Mathf.Max(0.0001f, (fallback.x + fallback.y + fallback.z) * (1f / 3f));
            float sourceAverage = Mathf.Max(0.0001f, (source.x + source.y + source.z) * (1f / 3f));
            return Mathf.Clamp(sourceAverage / fallbackAverage, 0.5f, 2f);
        }

        private static float ResolveProfileAbsorption(HectonBiomeProfile profile)
        {
            return profile != null
                ? Mathf.Clamp01(profile.absorption)
                : 0.9f;
        }

        private void TryRegisterTickManagers()
        {
            if (!_runtimeVisualCallbacksActive || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredColdTick)
            {
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            }
            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }
            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void UnregisterTickManagers()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterRenderDispatcher()
        {
            if (!_runtimeVisualCallbacksActive || _registeredRenderable)
                return;

            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRenderDispatcher()
        {
            if (!_registeredRenderable)
                return;

            GlobalRegistry.Renderables.Unregister(this);
            _registeredRenderable = false;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  UTILITY
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private static float ColorDistanceManhattan(Color a, Color b)
        {
            return math.abs(a.r - b.r) +
                   math.abs(a.g - b.g) +
                   math.abs(a.b - b.b);
        }

        private static float ResolvePresentationClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + ((to - from) * math.saturate(t));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float lengthSq = direction.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return math.abs(lengthSq - 1f) <= 0.0625f ? direction : direction * math.rsqrt(lengthSq);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDepthDiagnostics(float depth, bool underwater)
        {
            _debugDepth = depth;
            _debugIsUnderwater = underwater;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateLightDiagnostics(
            float lightFactor, float atmoIntensity, float horizonFade, float finalIntensity)
        {
            _debugLightFactor = lightFactor;
            _debugAtmoSunIntensity = atmoIntensity;
            _debugHorizonFade = horizonFade;
            _debugFinalSunIntensity = finalIntensity;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSurfaceLightDiagnostics(
            float baseSun, float horizon, float finalIntensity)
        {
            _debugAtmoSunIntensity = baseSun;
            _debugHorizonFade = horizon;
            _debugFinalSunIntensity = finalIntensity;
            _debugLightFactor = 1f;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSoundscapeDiagnostics()
        {
            _debugSoundscapeTier = ResolveSoundscapeTierDebugName(_currentSoundscapeTier);
            _debugSoundscapeFogScale = _soundscapeFogDensityScale;
            _debugSoundscapeAmbientScale = _soundscapeAmbientScale;
            _debugSoundscapeBeamScale = _soundscapeBeamScale;
            _debugSoundscapeCausticsScale = _soundscapeCausticsScale;
        }

        private static string ResolveFaunaMoodDebugName(WorldProceduralFaunaMood mood)
        {
            switch (mood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return "Calm";
                case WorldProceduralFaunaMood.Lively:
                    return "Lively";
                case WorldProceduralFaunaMood.Mixed:
                    return "Mixed";
                case WorldProceduralFaunaMood.Hostile:
                    return "Hostile";
                case WorldProceduralFaunaMood.None:
                default:
                    return "None";
            }
        }

        private static string ResolveSoundscapeTierDebugName(SoundscapeTier tier)
        {
            switch (tier)
            {
                case SoundscapeTier.Surface:
                    return "Surface";
                case SoundscapeTier.Twilight:
                    return "Twilight";
                case SoundscapeTier.Darkness:
                    return "Darkness";
                case SoundscapeTier.Abyss:
                    return "Abyss";
                case SoundscapeTier.DeepAbyss:
                    return "DeepAbyss";
                case SoundscapeTier.Thermal:
                    return "Thermal";
                case SoundscapeTier.Shallow:
                default:
                    return "Shallow";
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        //  GIZMOS
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform cam = playerCamera;
            if (cam == null)
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null)
                    cam = sv.camera.transform;
            }
            if (cam == null) return;

            float waterLevel = SanitizeVisualWaterLevel(waterLevelFallback, DefaultWaterLevelFallback);
            Vector3 camPos = cam.position;
            float depth = Mathf.Max(0f, waterLevel - camPos.y);

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
            Gizmos.DrawCube(
                new Vector3(camPos.x, waterLevel, camPos.z),
                new Vector3(80f, 0.05f, 80f));

            if (depth > 0f)
            {
                float lf = ResolveDepthLightFactor(depth);

                Gizmos.color = Color.Lerp(Color.black, Color.cyan, lf);
                Gizmos.DrawLine(
                    new Vector3(camPos.x, waterLevel, camPos.z), camPos);

                float darknessDepth = FindCurveDarknessDepth();
                if (darknessDepth > 0f)
                {
                    float darknessY = waterLevel - darknessDepth;
                    Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
                    Gizmos.DrawCube(
                        new Vector3(camPos.x, darknessY, camPos.z),
                        new Vector3(40f, 0.05f, 40f));
                }

                Gizmos.color = Color.Lerp(Color.black, new Color(1f, 0.95f, 0.8f), lf);
                Gizmos.DrawWireSphere(camPos, 2.5f);

                float scatter = 1f - lf;
                UnityEditor.Handles.Label(
                    camPos + Vector3.up * 3f,
                    $"Depth: {depth:F0}m  Light: {lf:P0}  Scatter: {scatter:P0}  Turbidity: {_currentTurbidity:F2}");
            }
            else
            {
                UnityEditor.Handles.Label(
                    camPos + Vector3.up * 3f,
                    "Above water");
            }
        }

        private float FindCurveDarknessDepth()
        {
            if (globalLightCurve == null || globalLightCurve.length < 2)
                return 0f;

            float maxTime = useBeerLambertDepthAttenuation
                ? Mathf.Max(
                    beerLambertBlackoutDepth,
                    globalLightCurve[globalLightCurve.length - 1].time)
                : globalLightCurve[globalLightCurve.length - 1].time;
            const float threshold = 0.005f;
            const int samples = 100;
            float step = maxTime / samples;

            for (int i = 1; i <= samples; i++)
            {
                float t = i * step;
                float v = ResolveDepthLightFactor(t);
                if (v <= threshold)
                    return t;
            }

            return maxTime;
        }
#endif
    
        #region JulesLink_CausticIntensityDepthCalculator
        private static void JulesLink_CausticIntensityDepthCalculator() { _ = typeof(Hecton8.PureLogic.Systems.CausticIntensityDepthCalculator); }
        #endregion
}

}
