using System;
using Crest;
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeRequirements()
        {
            ValidateActiveUrpRequirements();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ValidateActiveUrpRequirements();
            EnsureSceneCameraRequirements();
        }

        private static void ValidateActiveUrpRequirements()
        {
            UniversalRenderPipelineAsset urpAsset = ResolveActiveUrpAsset();
            if (urpAsset == null)
                return;

            if (!urpAsset.supportsCameraDepthTexture)
                ReportRuntimeRequirementViolation("Active URP asset has Camera Depth Texture disabled.");

            if (!urpAsset.supportsCameraOpaqueTexture)
                ReportRuntimeRequirementViolation("Active URP asset has Camera Opaque Texture disabled.");

            if (urpAsset.msaaSampleCount != 1)
                ReportRuntimeRequirementViolation($"Active URP asset uses MSAA {urpAsset.msaaSampleCount}. Crest parity path expects MSAA disabled.");

            ReadOnlySpan<ScriptableRendererData> rendererDataList = urpAsset.rendererDataList;
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                if (rendererDataList[rendererIndex] is not UniversalRendererData rendererData)
                    continue;

                if (rendererData.depthPrimingMode != DepthPrimingMode.Disabled)
                    ReportRuntimeRequirementViolation(
                        $"Renderer '{rendererData.name}' has Depth Priming enabled. Crest parity path expects it disabled.");
            }
        }

        private static void EnsureSceneCameraRequirements()
        {
            OceanRenderer oceanRenderer = UnityEngine.Object.FindAnyObjectByType<OceanRenderer>(FindObjectsInactive.Include);
            if (oceanRenderer == null)
                return;

            int oceanLayerMask = 1 << oceanRenderer.Layer;
            UniversalAdditionalCameraData[] cameraDataList =
                UnityEngine.Object.FindObjectsByType<UniversalAdditionalCameraData>(
                    FindObjectsInactive.Include); // COLD ALLOC: UniversalAdditionalCameraData[] - scene camera requirement sweep - owner: HectonUrpTextureRequirementsGuard

            for (int cameraIndex = 0; cameraIndex < cameraDataList.Length; cameraIndex++)
            {
                UniversalAdditionalCameraData cameraData = cameraDataList[cameraIndex];
                if (cameraData == null || cameraData.renderType != CameraRenderType.Base)
                    continue;

                if (!cameraData.TryGetComponent(out Camera camera))
                    continue;

                bool rendersOcean = (camera.cullingMask & oceanLayerMask) != 0;
                bool hasUnderwaterRenderer = cameraData.TryGetComponent<UnderwaterRenderer>(out _);
                if (!rendersOcean && !hasUnderwaterRenderer)
                    continue;

                if (cameraData.requiresDepthOption != CameraOverrideOption.On)
                    cameraData.requiresDepthOption = CameraOverrideOption.On;

                if (cameraData.requiresColorOption != CameraOverrideOption.On)
                    cameraData.requiresColorOption = CameraOverrideOption.On;

                if (!cameraData.requiresDepthTexture)
                    cameraData.requiresDepthTexture = true;

                if (!cameraData.requiresColorTexture)
                    cameraData.requiresColorTexture = true;

                if (!cameraData.renderPostProcessing)
                    cameraData.renderPostProcessing = true;
            }
        }

        private static UniversalRenderPipelineAsset ResolveActiveUrpAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentUrpAsset)
                return currentUrpAsset;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrpAsset)
                return defaultUrpAsset;

            return UniversalRenderPipeline.asset;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportRuntimeRequirementViolation(string message)
        {
            Debug.LogWarning($"[HectonUrpTextureRequirementsGuard] {message}");
        }
    }
}
