using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Enforces runtime shadow atlas and dynamic shadow-caster budgets for first-party lights.
    /// </summary>
    public static class HectonUrpShadowBudgetGuard
    {
        private const float SurvivalShadowDistanceMeters = 24f;
        private const float VisualOverkillShadowDistanceMeters = 40f;
        private const float SurvivalDynamicShadowCullDistanceMeters = 12f;
        private const float VisualOverkillDynamicShadowCullDistanceMeters = 28f;
        private const int MaxTrackedDynamicShadowLights = 16;
        private const int SurvivalShadowAtlasResolution = 1024;
        private const int VisualOverkillShadowAtlasResolution = 2048;
        private const int SurvivalDynamicShadowCasterBudget = 1;
        private const int VisualOverkillDynamicShadowCasterBudget = 3;
        private const int ShadowQualityQuantizationMilli = 25;
        private const float CascadeNearSplitMeters = 8f;
        private const float CascadeMediumSplitMeters = 25f;
        // COLD ALLOC: Light[16] — registered dynamic shadow-light slots for runtime budget enforcement — owner: HectonUrpShadowBudgetGuard
        private static readonly Light[] _trackedDynamicShadowLights = new Light[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: Transform[16] - cached dynamic shadow-light transforms for render hot-path distance checks - owner: HectonUrpShadowBudgetGuard
        private static readonly Transform[] _trackedDynamicShadowTransforms = new Transform[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: LightShadows[16] — original shadow modes for registered dynamic shadow-light slots — owner: HectonUrpShadowBudgetGuard
        private static readonly LightShadows[] _trackedDynamicShadowModes = new LightShadows[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: bool[16] - cached eligibility for continuous-quality dynamic shadow casters - owner: HectonUrpShadowBudgetGuard
        private static readonly bool[] _trackedDynamicShadowEligibility = new bool[MaxTrackedDynamicShadowLights];

        private static UniversalRenderPipelineAsset _lastResolvedAsset;
        private static int _lastQualityWeightMilli = -1;
        private static int _lastDynamicShadowBudgetFrame = -1;
        private static float _shadowDistanceMeters = SurvivalShadowDistanceMeters;
        private static float _dynamicShadowCullDistanceMeters = SurvivalDynamicShadowCullDistanceMeters;
        private static int _dynamicShadowCasterBudget = SurvivalDynamicShadowCasterBudget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            _lastResolvedAsset = null;
            _lastQualityWeightMilli = -1;
            _lastDynamicShadowBudgetFrame = -1;
            _shadowDistanceMeters = SurvivalShadowDistanceMeters;
            _dynamicShadowCullDistanceMeters = SurvivalDynamicShadowCullDistanceMeters;
            _dynamicShadowCasterBudget = SurvivalDynamicShadowCasterBudget;
            for (int i = 0; i < MaxTrackedDynamicShadowLights; i++)
                ClearTrackedDynamicShadowLightSlot(i);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureRuntimeShadowBudget();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        public static void RegisterDynamicShadowLight(Light light)
        {
            RegisterDynamicShadowLightInternal(light, requireForwardName: true);
        }

        public static bool RegisterAuthoritativeForwardSpotlight(Light light)
        {
            return RegisterDynamicShadowLightInternal(light, requireForwardName: false);
        }

        private static bool RegisterDynamicShadowLightInternal(Light light, bool requireForwardName)
        {
            if (light == null)
                return false;

            if (!IsAllowedForwardSpotlightCold(light, requireForwardName))
            {
                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;
                return false;
            }

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                if (_trackedDynamicShadowLights[i] == light)
                    return true;
            }

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                if (_trackedDynamicShadowLights[i] != null)
                    continue;

                _trackedDynamicShadowLights[i] = light;
                _trackedDynamicShadowTransforms[i] = light.transform;
                _trackedDynamicShadowModes[i] = light.shadows;
                _trackedDynamicShadowEligibility[i] = true;
                return true;
            }

            return false;
        }

        public static void UnregisterDynamicShadowLight(Light light)
        {
            if (light == null)
                return;

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                if (_trackedDynamicShadowLights[i] != light)
                    continue;

                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;
                ClearTrackedDynamicShadowLightSlot(i);
                return;
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!EnsureRuntimeShadowBudget())
                EnforceSceneShadowDictatorshipCold();
        }

        private static void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDynamicShadowBudgetFrame == frame)
                return;

            _lastDynamicShadowBudgetFrame = frame;
            EnsureRuntimeShadowBudget();
            ApplyDynamicShadowCasterBudget(camera.transform.position);
        }

        private static bool EnsureRuntimeShadowBudget()
        {
            UniversalRenderPipelineAsset urpAsset = ResolveActiveUrpAsset();
            if (urpAsset == null)
                return false;

            float shadowQuality01 = ResolveShadowQuality01();
            int qualityWeightMilli = ResolveQuantizedQualityMilli(shadowQuality01);
            shadowQuality01 = qualityWeightMilli * 0.001f;
            if (urpAsset == _lastResolvedAsset &&
                qualityWeightMilli == _lastQualityWeightMilli)
            {
                return false;
            }

            int atlasResolution = ResolveShadowAtlasResolution(shadowQuality01);
            int cascadeCount = ResolveCascadeCount(shadowQuality01);
            float shadowDistance = ResolveShadowDistance(shadowQuality01);
            _shadowDistanceMeters = shadowDistance;
            _dynamicShadowCullDistanceMeters = ResolveDynamicShadowCullDistance(shadowQuality01);
            _dynamicShadowCasterBudget = ResolveDynamicShadowCasterBudget(shadowQuality01);

            if (urpAsset.mainLightShadowmapResolution != atlasResolution)
                urpAsset.mainLightShadowmapResolution = atlasResolution;

            if (urpAsset.additionalLightsShadowmapResolution != atlasResolution)
                urpAsset.additionalLightsShadowmapResolution = atlasResolution;

            if (!Mathf.Approximately(urpAsset.shadowDistance, shadowDistance))
                urpAsset.shadowDistance = shadowDistance;

            if (urpAsset.shadowCascadeCount != cascadeCount)
                urpAsset.shadowCascadeCount = cascadeCount;

            float cascade2Split = ResolveCascadeNearSplit(shadowDistance);
            if (!Mathf.Approximately(urpAsset.cascade2Split, cascade2Split))
                urpAsset.cascade2Split = cascade2Split;

            Vector2 cascade3Split = ResolveCascadeMediumSplits(shadowDistance);
            if (urpAsset.cascade3Split != cascade3Split)
                urpAsset.cascade3Split = cascade3Split;

            _lastResolvedAsset = urpAsset;
            _lastQualityWeightMilli = qualityWeightMilli;
            if (HasLoadedRuntimeScene())
            {
                EnforceSceneShadowDictatorshipCold();
                return true;
            }

            return false;
        }

        private static void ApplyDynamicShadowCasterBudget(Vector3 viewerPosition)
        {
            int nearestIndexA = -1;
            int nearestIndexB = -1;
            int nearestIndexC = -1;
            float nearestDistanceSqA = float.MaxValue;
            float nearestDistanceSqB = float.MaxValue;
            float nearestDistanceSqC = float.MaxValue;
            int shadowCasterBudget = Mathf.Clamp(
                _dynamicShadowCasterBudget,
                SurvivalDynamicShadowCasterBudget,
                VisualOverkillDynamicShadowCasterBudget);
            float maxDistanceSq = _dynamicShadowCullDistanceMeters * _dynamicShadowCullDistanceMeters;

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                Light light = _trackedDynamicShadowLights[i];
                Transform lightTransform = _trackedDynamicShadowTransforms[i];
                if (light == null || lightTransform == null)
                {
                    ClearTrackedDynamicShadowLightSlot(i);
                    continue;
                }

                if (!_trackedDynamicShadowEligibility[i] ||
                    !light.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 lightVisualPosition = lightTransform.position;
                Vector3 visualDeltaToViewer = lightVisualPosition - viewerPosition;
                float distanceSq = visualDeltaToViewer.sqrMagnitude;
                if (distanceSq > maxDistanceSq)
                    continue;

                if (distanceSq < nearestDistanceSqA)
                {
                    nearestDistanceSqC = nearestDistanceSqB;
                    nearestIndexC = nearestIndexB;
                    nearestDistanceSqB = nearestDistanceSqA;
                    nearestIndexB = nearestIndexA;
                    nearestDistanceSqA = distanceSq;
                    nearestIndexA = i;
                    continue;
                }

                if (shadowCasterBudget > 1 && distanceSq < nearestDistanceSqB)
                {
                    nearestDistanceSqC = nearestDistanceSqB;
                    nearestIndexC = nearestIndexB;
                    nearestDistanceSqB = distanceSq;
                    nearestIndexB = i;
                    continue;
                }

                if (shadowCasterBudget > 2 && distanceSq < nearestDistanceSqC)
                {
                    nearestDistanceSqC = distanceSq;
                    nearestIndexC = i;
                }
            }

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                Light light = _trackedDynamicShadowLights[i];
                if (light == null)
                {
                    ClearTrackedDynamicShadowLightSlot(i);
                    continue;
                }

                bool shouldCastShadow =
                    _trackedDynamicShadowEligibility[i] &&
                    (i == nearestIndexA ||
                     (shadowCasterBudget > 1 && i == nearestIndexB) ||
                     (shadowCasterBudget > 2 && i == nearestIndexC));
                LightShadows targetShadowMode = shouldCastShadow ? ResolveAllowedShadowMode(_trackedDynamicShadowModes[i]) : LightShadows.None;
                if (light.shadows != targetShadowMode)
                    light.shadows = targetShadowMode;
            }
        }

        private static void ClearTrackedDynamicShadowLightSlot(int index)
        {
            if ((uint)index >= MaxTrackedDynamicShadowLights)
                return;

            _trackedDynamicShadowLights[index] = null;
            _trackedDynamicShadowTransforms[index] = null;
            _trackedDynamicShadowModes[index] = LightShadows.None;
            _trackedDynamicShadowEligibility[index] = false;
        }

        private static void EnforceSceneShadowDictatorshipCold()
        {
            if (!HasLoadedRuntimeScene())
                return;

            TryEnforceTrackedShadowDictatorship();
        }

        private static bool TryEnforceTrackedShadowDictatorship()
        {
            Light bestForwardSpot = null;
            int bestForwardSpotIndex = -1;
            float bestForwardSpotScore = float.MinValue;

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                Light light = _trackedDynamicShadowLights[i];
                if (light == null)
                {
                    ClearTrackedDynamicShadowLightSlot(i);
                    continue;
                }

                if (!_trackedDynamicShadowEligibility[i] ||
                    !light.isActiveAndEnabled)
                    continue;

                float spotScore = light.intensity * Mathf.Max(0.1f, light.range);
                if (spotScore <= bestForwardSpotScore)
                    continue;

                bestForwardSpotScore = spotScore;
                bestForwardSpot = light;
                bestForwardSpotIndex = i;
            }

            if (bestForwardSpot == null)
                return false;

            for (int i = 0; i < _trackedDynamicShadowLights.Length; i++)
            {
                Light light = _trackedDynamicShadowLights[i];
                if (light == null)
                    continue;

                LightShadows targetShadowMode = i == bestForwardSpotIndex
                    ? ResolveAllowedShadowMode(_trackedDynamicShadowModes[i])
                    : LightShadows.None;

                if (light.shadows != targetShadowMode)
                    light.shadows = targetShadowMode;
            }

            return true;
        }

        private static bool IsAllowedForwardSpotlightCold(Light light)
        {
            return IsAllowedForwardSpotlightCold(light, requireForwardName: true);
        }

        private static bool IsAllowedForwardSpotlightCold(Light light, bool requireForwardName)
        {
            if (light == null || light.type != LightType.Spot)
                return false;

            if (!requireForwardName)
                return true;

            Transform cursor = light.transform;
            while (cursor != null)
            {
                string nodeName = cursor.name;
                if (nodeName.IndexOf("Submarine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nodeName.IndexOf("Forward", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nodeName.IndexOf("MainSpot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nodeName.IndexOf("Headlight", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static LightShadows ResolveAllowedShadowMode(LightShadows shadowMode)
        {
            return shadowMode == LightShadows.None ? LightShadows.Soft : shadowMode;
        }

        private static float ResolveShadowQuality01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (float.IsNaN(qualityWeight) || float.IsInfinity(qualityWeight))
                qualityWeight = 1f;

            float platformWeight = PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight;
            if (!float.IsNaN(platformWeight) && !float.IsInfinity(platformWeight))
                qualityWeight = Mathf.Min(qualityWeight, platformWeight);

            return Mathf.Clamp01(qualityWeight);
        }

        private static int ResolveShadowAtlasResolution(float shadowQuality01)
        {
            float scaledResolution = Mathf.Lerp(
                SurvivalShadowAtlasResolution,
                VisualOverkillShadowAtlasResolution,
                Mathf.Clamp01(shadowQuality01));
            return scaledResolution < 1536f
                ? SurvivalShadowAtlasResolution
                : VisualOverkillShadowAtlasResolution;
        }

        private static int ResolveQuantizedQualityMilli(float shadowQuality01)
        {
            int qualityMilli = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(shadowQuality01) * 1000f), 0, 1000);
            int quantized = (qualityMilli / ShadowQualityQuantizationMilli) * ShadowQualityQuantizationMilli;
            if (qualityMilli > quantized)
                quantized += ShadowQualityQuantizationMilli;
            return Mathf.Clamp(quantized, 0, 1000);
        }

        private static int ResolveCascadeCount(float shadowQuality01)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(2f, 3f, Mathf.Clamp01(shadowQuality01))),
                2,
                3);
        }

        private static float ResolveShadowDistance(float shadowQuality01)
        {
            return Mathf.Lerp(
                SurvivalShadowDistanceMeters,
                VisualOverkillShadowDistanceMeters,
                Mathf.Clamp01(shadowQuality01));
        }

        private static float ResolveDynamicShadowCullDistance(float shadowQuality01)
        {
            return Mathf.Lerp(
                SurvivalDynamicShadowCullDistanceMeters,
                VisualOverkillDynamicShadowCullDistanceMeters,
                Mathf.Clamp01(shadowQuality01));
        }

        private static int ResolveDynamicShadowCasterBudget(float shadowQuality01)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    SurvivalDynamicShadowCasterBudget,
                    VisualOverkillDynamicShadowCasterBudget,
                    Mathf.Clamp01(shadowQuality01))),
                SurvivalDynamicShadowCasterBudget,
                VisualOverkillDynamicShadowCasterBudget);
        }

        private static float ResolveCascadeNearSplit(float shadowDistance)
        {
            float safeDistance = Mathf.Max(1f, shadowDistance);
            return Mathf.Clamp(CascadeNearSplitMeters / safeDistance, 0.05f, 0.45f);
        }

        private static Vector2 ResolveCascadeMediumSplits(float shadowDistance)
        {
            float nearSplit = ResolveCascadeNearSplit(shadowDistance);
            float mediumSplit = Mathf.Clamp(
                CascadeMediumSplitMeters / Mathf.Max(1f, shadowDistance),
                nearSplit + 0.05f,
                0.95f);
            return new Vector2(nearSplit, mediumSplit);
        }

        private static UniversalRenderPipelineAsset ResolveActiveUrpAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentUrpAsset)
                return currentUrpAsset;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrpAsset)
                return defaultUrpAsset;

            return UniversalRenderPipeline.asset;
        }

        private static bool HasLoadedRuntimeScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.isLoaded;
        }
    }
}
