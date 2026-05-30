using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Enforces URP depth/opaque/MSAA requirements needed by water and post-process consumers.
    /// </summary>
    internal static class HectonUrpTextureRequirementsGuard
    {
        private enum TextureRequirementPolicy : byte
        {
            FullOceanCompatibility = 0,
            QuestVrMobileSurvival = 1
        }

        private const string QuestVrUrpAssetName = "URP_Quest_VR";
        private const int CameraDataCacheCapacity = 32;

        private static TextureRequirementPolicy s_textureRequirementPolicy;
        private static readonly int[] s_cameraInstanceIdCache = new int[CameraDataCacheCapacity];
        private static readonly UniversalAdditionalCameraData[] s_cameraDataCache = new UniversalAdditionalCameraData[CameraDataCacheCapacity];
        private static readonly List<GameObject> s_sceneRootScratch = new List<GameObject>(64); // COLD ALLOC: List<GameObject>[64] - scene camera prewarm roots - owner: HectonUrpTextureRequirementsGuard
        private static readonly List<Camera> s_cameraScratch = new List<Camera>(32); // COLD ALLOC: List<Camera>[32] - scene camera prewarm cameras - owner: HectonUrpTextureRequirementsGuard
        private static int s_cameraDataCacheCount;
        private static int s_cameraDataCacheCursor;

        internal static bool UsesQuestVrMobileSurvivalPolicy =>
            s_textureRequirementPolicy == TextureRequirementPolicy.QuestVrMobileSurvival;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeRequirements()
        {
            ResetCameraDataCache();
            ValidateActiveUrpRequirements();
            PrewarmLoadedSceneCameraData();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetCameraDataCache();
            ValidateActiveUrpRequirements();
            PrewarmSceneCameraData(scene);
        }

        private static void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            EnsureCameraRequirements(camera);
        }

        private static void ValidateActiveUrpRequirements()
        {
            UniversalRenderPipelineAsset urpAsset = ResolveActiveUrpAsset();
            if (urpAsset == null)
            {
                s_textureRequirementPolicy = TextureRequirementPolicy.FullOceanCompatibility;
                return;
            }

            s_textureRequirementPolicy = ResolveTextureRequirementPolicy(urpAsset);
            bool questVrMobileSurvival = UsesQuestVrMobileSurvivalPolicy;

            if (!urpAsset.supportsCameraDepthTexture)
                ReportRuntimeRequirementViolation("Active URP asset has Camera Depth Texture disabled.");

            if (!questVrMobileSurvival && !urpAsset.supportsCameraOpaqueTexture)
                ReportRuntimeRequirementViolation("Active URP asset has Camera Opaque Texture disabled.");

            if (!questVrMobileSurvival && urpAsset.msaaSampleCount != 1)
                ReportRuntimeRequirementViolation($"Active URP asset uses MSAA {urpAsset.msaaSampleCount}. Ocean parity path expects MSAA disabled.");

            ReadOnlySpan<ScriptableRendererData> rendererDataList = urpAsset.rendererDataList;
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                if (rendererDataList[rendererIndex] is not UniversalRendererData rendererData)
                    continue;

                if (rendererData.depthPrimingMode != DepthPrimingMode.Disabled)
                    ReportRuntimeRequirementViolation(
                        $"Renderer '{rendererData.name}' has Depth Priming enabled. Ocean parity path expects it disabled.");
            }
        }

        private static void EnsureCameraRequirements(Camera camera)
        {
            if (camera == null)
                return;

            if (!TryResolveCameraData(camera, out UniversalAdditionalCameraData cameraData, out bool cacheHit))
            {
                if (cacheHit || !TryCacheCameraDataCold(camera, out cameraData))
                    return;
            }

            if (cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            if (cameraData.requiresDepthOption != CameraOverrideOption.On)
                cameraData.requiresDepthOption = CameraOverrideOption.On;

            if (!cameraData.requiresDepthTexture)
                cameraData.requiresDepthTexture = true;

            if (UsesQuestVrMobileSurvivalPolicy)
            {
                if (cameraData.requiresColorOption != CameraOverrideOption.Off)
                    cameraData.requiresColorOption = CameraOverrideOption.Off;

                if (cameraData.requiresColorTexture)
                    cameraData.requiresColorTexture = false;

                if (cameraData.renderPostProcessing)
                    cameraData.renderPostProcessing = false;

                return;
            }

            if (cameraData.requiresColorOption != CameraOverrideOption.On)
                cameraData.requiresColorOption = CameraOverrideOption.On;

            if (!cameraData.requiresColorTexture)
                cameraData.requiresColorTexture = true;

            if (!cameraData.renderPostProcessing)
                cameraData.renderPostProcessing = true;
        }

        private static bool TryResolveCameraData(
            Camera camera,
            out UniversalAdditionalCameraData cameraData,
            out bool cacheHit)
        {
            cacheHit = false;
            int instanceId = unchecked((int)UnityEngine.EntityId.ToULong(camera.GetEntityId()));
            for (int index = 0; index < s_cameraDataCacheCount; index++)
            {
                if (s_cameraInstanceIdCache[index] != instanceId)
                    continue;

                cacheHit = true;
                cameraData = s_cameraDataCache[index];
                return cameraData != null;
            }

            cameraData = null;
            return false;
        }

        private static void PrewarmLoadedSceneCameraData()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
                PrewarmSceneCameraData(SceneManager.GetSceneAt(sceneIndex));
        }

        private static void PrewarmSceneCameraData(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            s_sceneRootScratch.Clear();
            scene.GetRootGameObjects(s_sceneRootScratch);
            for (int rootIndex = 0; rootIndex < s_sceneRootScratch.Count; rootIndex++)
            {
                GameObject root = s_sceneRootScratch[rootIndex];
                if (root == null)
                    continue;

                s_cameraScratch.Clear();
                root.GetComponentsInChildren(true, s_cameraScratch);
                for (int cameraIndex = 0; cameraIndex < s_cameraScratch.Count; cameraIndex++)
                    TryCacheCameraDataCold(s_cameraScratch[cameraIndex], out _);
            }

            s_cameraScratch.Clear();
            s_sceneRootScratch.Clear();
        }

        private static bool TryCacheCameraDataCold(
            Camera camera,
            out UniversalAdditionalCameraData cameraData)
        {
            cameraData = null;
            if (camera == null)
                return false;

            int instanceId = unchecked((int)UnityEngine.EntityId.ToULong(camera.GetEntityId()));
            if (!camera.TryGetComponent(out cameraData) || cameraData == null)
            {
                StoreCameraDataCacheEntry(instanceId, null);
                return false;
            }

            StoreCameraDataCacheEntry(instanceId, cameraData);
            return true;
        }

        private static void StoreCameraDataCacheEntry(int instanceId, UniversalAdditionalCameraData cameraData)
        {
            for (int index = 0; index < s_cameraDataCacheCount; index++)
            {
                if (s_cameraInstanceIdCache[index] != instanceId)
                    continue;

                s_cameraDataCache[index] = cameraData;
                return;
            }

            if (s_cameraDataCacheCount < CameraDataCacheCapacity)
            {
                s_cameraInstanceIdCache[s_cameraDataCacheCount] = instanceId;
                s_cameraDataCache[s_cameraDataCacheCount] = cameraData;
                s_cameraDataCacheCount++;
                return;
            }

            s_cameraInstanceIdCache[s_cameraDataCacheCursor] = instanceId;
            s_cameraDataCache[s_cameraDataCacheCursor] = cameraData;
            s_cameraDataCacheCursor++;
            if (s_cameraDataCacheCursor >= CameraDataCacheCapacity)
                s_cameraDataCacheCursor = 0;
        }

        private static void ResetCameraDataCache()
        {
            for (int index = 0; index < s_cameraDataCacheCount; index++)
            {
                s_cameraInstanceIdCache[index] = 0;
                s_cameraDataCache[index] = null;
            }

            s_cameraDataCacheCount = 0;
            s_cameraDataCacheCursor = 0;
        }

        private static UniversalRenderPipelineAsset ResolveActiveUrpAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentUrpAsset)
                return currentUrpAsset;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrpAsset)
                return defaultUrpAsset;

            return UniversalRenderPipeline.asset;
        }

        private static TextureRequirementPolicy ResolveTextureRequirementPolicy(UniversalRenderPipelineAsset urpAsset)
        {
            if (urpAsset == null)
                return TextureRequirementPolicy.FullOceanCompatibility;

            string assetName = urpAsset.name;
            return !string.IsNullOrEmpty(assetName) &&
                   assetName.IndexOf(QuestVrUrpAssetName, StringComparison.OrdinalIgnoreCase) >= 0
                ? TextureRequirementPolicy.QuestVrMobileSurvival
                : TextureRequirementPolicy.FullOceanCompatibility;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportRuntimeRequirementViolation(string message)
        {
            Hecton8.Core.H8Debug.LogWarning($"[HectonUrpTextureRequirementsGuard] {message}");
        }
    }
}
