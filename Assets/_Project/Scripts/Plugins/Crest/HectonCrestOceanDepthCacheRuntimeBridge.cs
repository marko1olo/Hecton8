using System;
using System.IO;
using Crest;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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

                NativeArray<byte> pngBytes = default;
                try
                {
                    NativeArray<Color32> readbackPixels = request.GetData<Color32>();
                    pngBytes = ImageConversion.EncodeNativeArrayToPNG(
                        readbackPixels,
                        GraphicsFormat.R8G8B8A8_UNorm,
                        (uint)cacheWidth,
                        (uint)cacheHeight,
                        0u);

                    if (pngBytes.IsCreated && pngBytes.Length > 0)
                    {
                        byte* pngPointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pngBytes);
                        using FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.SequentialScan);
                        stream.Write(new ReadOnlySpan<byte>(pngPointer, pngBytes.Length));
                    }
                }
                finally
                {
                    if (pngBytes.IsCreated)
                        pngBytes.Dispose();
                }
            });
            return true;
#else
            return false;
#endif
        }
    }
}
