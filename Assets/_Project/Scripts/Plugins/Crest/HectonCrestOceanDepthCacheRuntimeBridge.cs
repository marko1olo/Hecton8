using System;
using System.IO;
using Crest;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path typed bridge for the Crest depth-cache runtime API.
    /// </summary>
    internal static unsafe class HectonCrestOceanDepthCacheRuntimeBridge
    {
        private const float HectonMinimumCameraHeightAboveSeaLevel = 8f;
        private static readonly bool HectonRuntimeDepthCacheCameraDisabled = true;

        internal static void HectonConfigureRealtimeCapture(
            this OceanDepthCache depthCache,
            int layerMask,
            int resolution,
            float cameraMaxTerrainHeight,
            bool relativeToSeaLevel)
        {
            if (depthCache == null)
                return;

            if (HectonRuntimeDepthCacheCameraDisabled)
                return;

            depthCache.HectonApplyRuntimeSettings(
                layerMask,
                resolution,
                Mathf.Max(HectonMinimumCameraHeightAboveSeaLevel, cameraMaxTerrainHeight),
                relativeToSeaLevel);
        }

        internal static Camera HectonEnsureCaptureCamera(this OceanDepthCache depthCache, bool updateComponents)
        {
            if (depthCache == null)
                return null;

            if (HectonRuntimeDepthCacheCameraDisabled)
                return null;

            return depthCache.HectonGetOrCreateCaptureCamera(updateComponents);
        }

        internal static void HectonAlignCaptureCamera(
            this OceanDepthCache depthCache,
            Camera captureCamera,
            Vector3 runtimeCacheCenter,
            float cameraMaxTerrainHeight,
            float cameraFarPlane,
            float coverageSize,
            int layerMask)
        {
            if (depthCache == null || captureCamera == null)
                return;

            if (HectonRuntimeDepthCacheCameraDisabled)
                return;

            float resolvedCameraHeight = Mathf.Max(HectonMinimumCameraHeightAboveSeaLevel, cameraMaxTerrainHeight);
            Transform cameraTransform = captureCamera.transform;
            cameraTransform.position = runtimeCacheCenter + Vector3.up * resolvedCameraHeight;
            cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            captureCamera.orthographic = true;
            captureCamera.orthographicSize = Mathf.Max(coverageSize * 0.5f, 1f);
            captureCamera.nearClipPlane = 0.05f;
            captureCamera.farClipPlane = Mathf.Max(cameraFarPlane, resolvedCameraHeight + 8f);
            captureCamera.cullingMask = layerMask;
        }

        internal static bool HectonSaveDepthCacheTexturePng(this OceanDepthCache depthCache, string absolutePath)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (depthCache == null || string.IsNullOrWhiteSpace(absolutePath))
                return false;

            if (HectonRuntimeDepthCacheCameraDisabled)
                return false;

            RenderTexture cacheTexture = depthCache.CacheTexture;
            if (cacheTexture == null)
                return false;

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int cacheWidth = cacheTexture.width;
            int cacheHeight = cacheTexture.height;
            UnityEngine.Rendering.AsyncGPUReadback.Request(cacheTexture, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError)
                    return;

                Texture2D readbackTexture = new Texture2D(cacheWidth, cacheHeight, TextureFormat.RGBA32, false, true)
                {
                    name = "__HectonDepthCacheDebugReadback",
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - one-shot async depth-cache forensic PNG dump - owner: HectonCrestOceanDepthCacheRuntimeBridge
                readbackTexture.SetPixelData(request.GetData<Color32>(), 0);
                readbackTexture.Apply(false, false);
                byte[] pngBytes = readbackTexture.EncodeToPNG();
                if (pngBytes != null && pngBytes.Length > 0)
                    File.WriteAllBytes(absolutePath, pngBytes);

                UnityEngine.Object.DestroyImmediate(readbackTexture);
            });
            return true;
#else
            return false;
#endif
        }
    }
}
