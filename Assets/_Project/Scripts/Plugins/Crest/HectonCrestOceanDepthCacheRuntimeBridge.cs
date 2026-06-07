using System;
using System.IO;
using Crest;
using Hecton8.Core;
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
        private const string NativeMemoryOwner = nameof(HectonCrestOceanDepthCacheRuntimeBridge);
        private const string DepthCacheReadbackPixelsLabel = "depthCacheReadbackPixels";
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
            NativeArray<Color32> readbackPixels = new NativeArray<Color32>(
                cacheWidth * cacheHeight,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            ReadbackDisposalState readbackDisposalState;
            try
            {
                int readbackSentinelId = NativeMemorySentinel.RegisterNativeArray(
                    readbackPixels,
                    NativeMemoryOwner,
                    DepthCacheReadbackPixelsLabel,
                    NativeAllocationLifetime.Session);
                if (readbackSentinelId <= 0)
                    throw new InvalidOperationException("Native memory sentinel registration failed for Crest depth-cache readback pixels.");

                readbackDisposalState = new ReadbackDisposalState(
                    readbackSentinelId);
            }
            catch
            {
                if (readbackPixels.IsCreated)
                    readbackPixels.Dispose();

                throw;
            }

            UnityEngine.Rendering.AsyncGPUReadbackRequest readbackRequest;
            try
            {
                readbackRequest = UnityEngine.Rendering.AsyncGPUReadback.RequestIntoNativeArray(
                    ref readbackPixels,
                    cacheTexture,
                    0,
                    TextureFormat.RGBA32,
                    request =>
                {
                    NativeArray<byte> pngBytes = default;
                    try
                    {
                        if (request.hasError)
                            return;

                        pngBytes = ImageConversion.EncodeNativeArrayToPNG(
                            readbackPixels,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            (uint)cacheWidth,
                            (uint)cacheHeight,
                            0u);

                        if (pngBytes.IsCreated && pngBytes.Length > 0)
                        {
                            byte* pngPointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pngBytes);
                            WriteDepthCachePngAtomic(absolutePath, pngPointer, pngBytes.Length);
                        }
                    }
                    finally
                    {
                        if (pngBytes.IsCreated)
                            pngBytes.Dispose();

                        DisposeRegisteredReadbackPixels(readbackPixels, readbackDisposalState);
                    }
                });
            }
            catch
            {
                DisposeRegisteredReadbackPixels(readbackPixels, readbackDisposalState);
                throw;
            }

            if (!readbackRequest.hasError)
                return true;

            DisposeRegisteredReadbackPixels(readbackPixels, readbackDisposalState);

            return false;
#else
            return false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void WriteDepthCachePngAtomic(string absolutePath, byte* pngPointer, int byteLength)
        {
            string tempPath = absolutePath + ".tmp";
            TryDeleteFileCold(tempPath);

            try
            {
                using (FileStream stream = new FileStream(
                           tempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.Read,
                           65536,
                           FileOptions.SequentialScan | FileOptions.WriteThrough))
                {
                    stream.Write(new ReadOnlySpan<byte>(pngPointer, byteLength));
                    stream.Flush(true);
                }

                PromoteTempFileAtomic(tempPath, absolutePath);
            }
            catch
            {
                TryDeleteFileCold(tempPath);
                throw;
            }
        }

        private static void PromoteTempFileAtomic(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Replace(tempPath, destinationPath, null, true);
            else
                File.Move(tempPath, destinationPath);
        }

        private static void TryDeleteFileCold(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
#endif

        private static void DisposeRegisteredReadbackPixels(
            NativeArray<Color32> readbackPixels,
            ReadbackDisposalState readbackDisposalState)
        {
            if (readbackDisposalState == null ||
                System.Threading.Interlocked.Exchange(ref readbackDisposalState.Disposed, 1) != 0)
            {
                return;
            }

            bool unregistered = false;
            try
            {
                if (readbackDisposalState.SentinelId > 0)
                {
                    NativeMemorySentinel.Unregister(readbackDisposalState.SentinelId);
                    readbackDisposalState.SentinelId = 0;
                    unregistered = true;
                }
                else
                {
                    NativeMemorySentinel.UnregisterNativeArray(readbackPixels);
                }

                if (readbackPixels.IsCreated)
                    readbackPixels.Dispose();
            }
            catch
            {
                System.Threading.Volatile.Write(ref readbackDisposalState.Disposed, 0);
                if (unregistered && readbackPixels.IsCreated)
                {
                    readbackDisposalState.SentinelId = NativeMemorySentinel.RegisterNativeArray(
                        readbackPixels,
                        NativeMemoryOwner,
                        DepthCacheReadbackPixelsLabel,
                        NativeAllocationLifetime.Session);
                    if (readbackDisposalState.SentinelId <= 0)
                        throw new InvalidOperationException("Native memory sentinel restore failed for Crest depth-cache readback pixels.");
                }

                throw;
            }
        }

        private sealed class ReadbackDisposalState
        {
            internal int SentinelId;
            internal int Disposed;

            internal ReadbackDisposalState(int sentinelId)
            {
                SentinelId = sentinelId;
            }
        }
    }
}
