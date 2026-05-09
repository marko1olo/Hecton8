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
        private const float MaxShadowDistanceMeters = 40f;
        private const float DynamicShadowCullDistanceMeters = 20f;
        private const int MaxTrackedDynamicShadowLights = 16;
        private const int LowTierShadowAtlasResolution = 1024;
        private const int HighTierShadowAtlasResolution = 2048;
        private const float CascadeNearSplitNormalized = 8f / MaxShadowDistanceMeters;
        private static readonly Vector2 CascadeMediumSplitsNormalized = new Vector2(8f / MaxShadowDistanceMeters, 25f / MaxShadowDistanceMeters);
        // COLD ALLOC: Light[16] — registered dynamic shadow-light slots for runtime budget enforcement — owner: HectonUrpShadowBudgetGuard
        private static readonly Light[] _trackedDynamicShadowLights = new Light[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: Transform[16] - cached dynamic shadow-light transforms for render hot-path distance checks - owner: HectonUrpShadowBudgetGuard
        private static readonly Transform[] _trackedDynamicShadowTransforms = new Transform[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: LightShadows[16] — original shadow modes for registered dynamic shadow-light slots — owner: HectonUrpShadowBudgetGuard
        private static readonly LightShadows[] _trackedDynamicShadowModes = new LightShadows[MaxTrackedDynamicShadowLights];
        // COLD ALLOC: bool[16] - cached eligibility for the single allowed forward spotlight shadow caster - owner: HectonUrpShadowBudgetGuard
        private static readonly bool[] _trackedDynamicShadowEligibility = new bool[MaxTrackedDynamicShadowLights];

        private static UniversalRenderPipelineAsset _lastResolvedAsset;
        private static int _lastQualityLevel = -1;
        private static int _lastDynamicShadowBudgetFrame = -1;

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

            int frame = Time.frameCount;
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

            int qualityLevel = QualitySettings.GetQualityLevel();
            if (urpAsset == _lastResolvedAsset && qualityLevel == _lastQualityLevel)
                return false;

            bool lowTier = qualityLevel <= 1;
            int atlasResolution = lowTier ? LowTierShadowAtlasResolution : HighTierShadowAtlasResolution;
            int cascadeCount = lowTier ? 2 : 3;

            if (urpAsset.mainLightShadowmapResolution != atlasResolution)
                urpAsset.mainLightShadowmapResolution = atlasResolution;

            if (urpAsset.additionalLightsShadowmapResolution != atlasResolution)
                urpAsset.additionalLightsShadowmapResolution = atlasResolution;

            if (urpAsset.shadowDistance != MaxShadowDistanceMeters)
                urpAsset.shadowDistance = MaxShadowDistanceMeters;

            if (urpAsset.shadowCascadeCount != cascadeCount)
                urpAsset.shadowCascadeCount = cascadeCount;

            if (!Mathf.Approximately(urpAsset.cascade2Split, CascadeNearSplitNormalized))
                urpAsset.cascade2Split = CascadeNearSplitNormalized;

            if (urpAsset.cascade3Split != CascadeMediumSplitsNormalized)
                urpAsset.cascade3Split = CascadeMediumSplitsNormalized;

            _lastResolvedAsset = urpAsset;
            _lastQualityLevel = qualityLevel;
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
            float nearestDistanceSqA = float.MaxValue;
            float maxDistanceSq = DynamicShadowCullDistanceMeters * DynamicShadowCullDistanceMeters;

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

                float distanceSq = (lightTransform.position - viewerPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq)
                    continue;

                if (distanceSq < nearestDistanceSqA)
                {
                    nearestDistanceSqA = distanceSq;
                    nearestIndexA = i;
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

                bool shouldCastShadow = i == nearestIndexA && _trackedDynamicShadowEligibility[i];
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

            if (TryEnforceTrackedShadowDictatorship())
                return;

            Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Light bestForwardSpot = null;
            float bestForwardSpotScore = float.MinValue;

            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light light = sceneLights[i];
                if (light == null)
                    continue;

                if (!IsAllowedForwardSpotlightCold(light))
                    continue;

                float spotScore = light.intensity * Mathf.Max(0.1f, light.range);
                if (spotScore > bestForwardSpotScore)
                {
                    bestForwardSpotScore = spotScore;
                    bestForwardSpot = light;
                }
            }

            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light light = sceneLights[i];
                if (light == null)
                    continue;

                bool allowedForwardSpot = light == bestForwardSpot && IsAllowedForwardSpotlightCold(light);
                if (allowedForwardSpot)
                {
                    if (light.shadows == LightShadows.None)
                        light.shadows = LightShadows.Soft;
                    continue;
                }

                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;
            }

            if (bestForwardSpot != null)
                RegisterDynamicShadowLight(bestForwardSpot);
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
