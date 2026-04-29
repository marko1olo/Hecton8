using System;
using System.IO;
using Crest;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path typed bridge for the Crest depth-cache runtime API.
    /// </summary>
    internal static class HectonCrestOceanDepthCacheRuntimeBridge
    {
        private const float HectonMinimumCameraHeightAboveSeaLevel = 8f;

        internal static void HectonConfigureRealtimeCapture(
            this OceanDepthCache depthCache,
            int layerMask,
            int resolution,
            float cameraMaxTerrainHeight,
            bool relativeToSeaLevel)
        {
            if (depthCache == null)
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
            if (depthCache == null || string.IsNullOrWhiteSpace(absolutePath))
                return false;

            RenderTexture cacheTexture = depthCache.CacheTexture;
            if (cacheTexture == null)
                return false;

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            RenderTexture previousActive = RenderTexture.active;
            Texture2D readbackTexture = null;
            try
            {
                RenderTexture.active = cacheTexture;
                readbackTexture = new Texture2D(cacheTexture.width, cacheTexture.height, TextureFormat.RGBA32, false, true)
                {
                    name = "__HectonDepthCacheDebugReadback",
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] — one-shot depth-cache forensic readback for PNG dump — owner: HectonCrestOceanDepthCacheRuntimeBridge
                readbackTexture.ReadPixels(new Rect(0f, 0f, cacheTexture.width, cacheTexture.height), 0, 0, false);
                readbackTexture.Apply(false, false);
                File.WriteAllBytes(absolutePath, readbackTexture.EncodeToPNG());
                return true;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (readbackTexture != null)
                    UnityEngine.Object.DestroyImmediate(readbackTexture);
            }
        }
    }
}
