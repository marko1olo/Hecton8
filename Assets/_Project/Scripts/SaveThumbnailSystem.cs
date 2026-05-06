using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Optimization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Handles save-slot thumbnail capture requests and cached thumbnail loading.
    /// Capture is routed through URP and AsyncGPUReadback via SaveThumbnailCaptureFeature.
    /// </summary>
    public static class SaveThumbnailSystem
    {
        private const int Width = 320;
        private const int Height = 180;
        private const string Extension = ".jpg";
        private const string LegacyExtension = ".png";
        private const int JpegQuality = 82;
        private const int MaxCachedSprites = 12;
        private const float MinPoseCaptureDistanceMeters = 5f;
        private const float MinPoseCaptureAngleDegrees = 5f;
        private const float MinPoseCaptureDistanceSq = MinPoseCaptureDistanceMeters * MinPoseCaptureDistanceMeters;
        private const string NativeMemoryOwner = nameof(SaveThumbnailSystem);
        private static readonly float MinPoseCaptureQuaternionDot =
            Mathf.Cos(MinPoseCaptureAngleDegrees * Mathf.Deg2Rad * 0.5f);

        private struct CaptureRequest
        {
            public string SlotName;
            public Camera Camera;
            public int SequenceId;
        }

        internal readonly struct RenderRequest
        {
            public RenderRequest(Camera camera, int sequenceId)
            {
                Camera = camera;
                SequenceId = sequenceId;
            }

            public Camera Camera { get; }
            public int SequenceId { get; }
        }

        private static readonly Dictionary<string, Sprite> _spriteCache =
            new Dictionary<string, Sprite>(MaxCachedSprites, StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _spriteCacheOrder = new List<string>(MaxCachedSprites);
        private static readonly Action<AsyncGPUReadbackRequest> s_readbackCompleted = HandleReadbackCompleted;

        private static Camera _cachedCaptureCamera;
        private static CaptureRequest _pendingRequest;
        private static CaptureRequest _inflightRequest;
        private static bool _hasPendingRequest;
        private static bool _hasInflightRequest;
        private static bool _hasLastCapturePose;
        private static Vector3 _lastCapturePosition;
        private static Quaternion _lastCaptureRotation;
        private static int _requestSequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearCache();
            _cachedCaptureCamera = null;
            _pendingRequest = default;
            _inflightRequest = default;
            _hasPendingRequest = false;
            _hasInflightRequest = false;
            _hasLastCapturePose = false;
            _lastCapturePosition = default;
            _lastCaptureRotation = default;
            _requestSequence = 0;
        }

        internal static int CaptureWidth => Width;
        internal static int CaptureHeight => Height;
        internal static Action<AsyncGPUReadbackRequest> ReadbackCompletedCallback => s_readbackCompleted;

        public static string GetThumbnailPath(string slotName)
        {
            return Path.Combine(Application.persistentDataPath, slotName + Extension);
        }

        public static string GetTempThumbnailPath(string slotName)
        {
            return GetThumbnailPath(slotName) + ".tmp";
        }

        /// <summary>
        /// Requests a thumbnail capture from the active player camera or an explicit override camera.
        /// The actual readback is executed by SaveThumbnailCaptureFeature during the next camera render.
        /// </summary>
        public static void CaptureThumbnail(string slotName, Camera overrideCamera = null)
        {
            if (string.IsNullOrEmpty(slotName) || !TryResolveCaptureCamera(overrideCamera, out Camera captureCamera))
                return;

            if ((_hasPendingRequest && string.Equals(_pendingRequest.SlotName, slotName, StringComparison.OrdinalIgnoreCase)) ||
                (_hasInflightRequest && string.Equals(_inflightRequest.SlotName, slotName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (!HasCapturePoseChanged(captureCamera))
                return;

            ClearCacheEntry(slotName);
            _requestSequence++;
            _pendingRequest = new CaptureRequest
            {
                SlotName = slotName,
                Camera = captureCamera,
                SequenceId = _requestSequence
            };
            Transform captureTransform = captureCamera.transform;
            _lastCapturePosition = captureTransform.position;
            _lastCaptureRotation = captureTransform.rotation;
            _hasLastCapturePose = true;
            _hasPendingRequest = true;
        }

        internal static bool TryAcquireRenderRequest(Camera renderCamera, out RenderRequest request)
        {
            if (_hasPendingRequest &&
                renderCamera != null &&
                ReferenceEquals(renderCamera, _pendingRequest.Camera))
            {
                request = new RenderRequest(renderCamera, _pendingRequest.SequenceId);
                return true;
            }

            request = default;
            return false;
        }

        internal static bool TrySubmitGpuReadback(int sequenceId)
        {
            if (!_hasPendingRequest || _hasInflightRequest || _pendingRequest.SequenceId != sequenceId)
                return false;

            _inflightRequest = _pendingRequest;
            _hasInflightRequest = true;
            _hasPendingRequest = false;
            return true;
        }

        /// <summary>
        /// Loads a thumbnail for the specified slot. Uses cache safely to avoid repeated texture churn.
        /// </summary>
        public static Sprite LoadThumbnail(string slotName)
        {
            AssetLoadDispatcher.ForceEvaluateUiMipBiasGate();

            if (_spriteCache.TryGetValue(slotName, out Sprite cached))
            {
                if (cached != null && cached.texture != null)
                {
                    MarkCacheEntryAsMostRecent(_spriteCacheOrder, slotName);
                    return cached;
                }

                RemoveCacheEntry(slotName);
            }

            string path = ResolveExistingThumbnailPath(slotName);
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.hideFlags = HideFlags.HideAndDontSave;
            if (texture.LoadImage(bytes, true))
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
                AddCacheEntry(slotName, sprite);
                return sprite;
            }

            UnityEngine.Object.Destroy(texture);
            return null;
        }

        public static void DeleteThumbnail(string slotName)
        {
            ClearCacheEntry(slotName);

            string path = GetThumbnailPath(slotName);
            if (File.Exists(path))
                File.Delete(path);

            string tempPath = GetTempThumbnailPath(slotName);
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            string legacyPath = GetLegacyThumbnailPath(slotName);
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }

        /// <summary>
        /// Purges cached runtime thumbnails to free memory.
        /// </summary>
        public static void ClearCache()
        {
            Dictionary<string, Sprite>.Enumerator enumerator = _spriteCache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, Sprite> kvp = enumerator.Current;
                if (kvp.Value == null)
                    continue;

                if (kvp.Value.texture != null)
                    UnityEngine.Object.Destroy(kvp.Value.texture);
                UnityEngine.Object.Destroy(kvp.Value);
            }

            _spriteCache.Clear();
            _spriteCacheOrder.Clear();
        }

        private static bool TryResolveCaptureCamera(Camera overrideCamera, out Camera captureCamera)
        {
            if (overrideCamera != null &&
                overrideCamera.isActiveAndEnabled &&
                overrideCamera.gameObject.activeInHierarchy)
            {
                _cachedCaptureCamera = overrideCamera;
                captureCamera = overrideCamera;
                return true;
            }

            if (_cachedCaptureCamera != null &&
                _cachedCaptureCamera.isActiveAndEnabled &&
                _cachedCaptureCamera.gameObject.activeInHierarchy)
            {
                captureCamera = _cachedCaptureCamera;
                return true;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                _cachedCaptureCamera = GlobalRegistry.Player != null && GlobalRegistry.Player.PlayerCamera != null
                    ? GlobalRegistry.Player.PlayerCamera
                    : playerTransform.GetComponent<Camera>();
            }

            captureCamera = _cachedCaptureCamera;
            return captureCamera != null;
        }

        private static bool HasCapturePoseChanged(Camera captureCamera)
        {
            if (!_hasLastCapturePose || captureCamera == null)
                return true;

            Transform captureTransform = captureCamera.transform;
            Vector3 delta = captureTransform.position - _lastCapturePosition;
            if (delta.sqrMagnitude > MinPoseCaptureDistanceSq)
                return true;

            float rotationDot = Mathf.Abs(Quaternion.Dot(_lastCaptureRotation, captureTransform.rotation));
            return rotationDot < MinPoseCaptureQuaternionDot;
        }

        private static void HandleReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (!_hasInflightRequest)
                return;

            CaptureRequest inflightRequest = _inflightRequest;
            _inflightRequest = default;
            _hasInflightRequest = false;

            if (!Application.isPlaying || inflightRequest.Camera == null)
                return;

            if (request.hasError)
            {
                Debug.LogError($"[SaveThumbnailSystem] AsyncGPUReadback failed for '{inflightRequest.SlotName}'.");
                return;
            }

            NativeArray<byte> readbackData = request.GetData<byte>();
            int expectedLength = Width * Height * 4;
            if (!readbackData.IsCreated || readbackData.Length < expectedLength)
            {
                Debug.LogError($"[SaveThumbnailSystem] AsyncGPUReadback returned invalid thumbnail data for '{inflightRequest.SlotName}'.");
                return;
            }

            // COLD ALLOC: NativeArray<byte>[Width * Height * 4] - persistent GPU readback staging buffer for background JPG write - owner: SaveThumbnailSystem
            NativeArray<byte> persistentRgba = new NativeArray<byte>(expectedLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(
                persistentRgba,
                NativeMemoryOwner,
                "thumbnailReadbackRgba",
                NativeAllocationLifetime.TransientArena);
            NativeArray<byte>.Copy(readbackData, persistentRgba, expectedLength);
            _ = PersistThumbnailAsync(inflightRequest.SlotName, persistentRgba, Width, Height);
        }

        private static async Awaitable PersistThumbnailAsync(string slotName, NativeArray<byte> rgbaBytes, int width, int height)
        {
            string path = GetThumbnailPath(slotName);
            string tempPath = GetTempThumbnailPath(slotName);

            try
            {
                await Awaitable.BackgroundThreadAsync();

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                NativeArray<byte> encodedJpg = ImageConversion.EncodeNativeArrayToJPG(
                    rgbaBytes,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    (uint)width,
                    (uint)height,
                    0u,
                    JpegQuality);

                if (!encodedJpg.IsCreated || encodedJpg.Length <= 0)
                    throw new IOException("JPG encoder returned no thumbnail bytes.");

                NativeMemorySentinel.RegisterNativeArray(
                    encodedJpg,
                    NativeMemoryOwner,
                    "thumbnailEncodedJpg",
                    NativeAllocationLifetime.TransientArena);

                try
                {
                    unsafe
                    {
                        void* dataPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(encodedJpg);
                        using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        stream.Write(new ReadOnlySpan<byte>(dataPtr, encodedJpg.Length));
                    }
                }
                finally
                {
                    if (encodedJpg.IsCreated)
                    {
                        NativeMemorySentinel.UnregisterNativeArray(encodedJpg);
                        encodedJpg.Dispose();
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }

                await Awaitable.MainThreadAsync();
                Debug.LogError($"[SaveThumbnailSystem] Failed to persist thumbnail for '{slotName}': {ex.Message}");
            }
            finally
            {
                if (rgbaBytes.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(rgbaBytes);
                    rgbaBytes.Dispose();
                }
            }
        }

        private static string ResolveExistingThumbnailPath(string slotName)
        {
            string primaryPath = GetThumbnailPath(slotName);
            if (File.Exists(primaryPath))
                return primaryPath;

            return GetLegacyThumbnailPath(slotName);
        }

        private static string GetLegacyThumbnailPath(string slotName)
        {
            return Path.Combine(Application.persistentDataPath, slotName + LegacyExtension);
        }

        private static void ClearCacheEntry(string slotName)
        {
            if (_spriteCache.TryGetValue(slotName, out Sprite cached))
            {
                if (cached != null)
                {
                    if (cached.texture != null)
                        UnityEngine.Object.Destroy(cached.texture);
                    UnityEngine.Object.Destroy(cached);
                }

                RemoveCacheEntry(slotName);
            }
        }

        private static void AddCacheEntry(string slotName, Sprite sprite)
        {
            if (_spriteCache.TryGetValue(slotName, out Sprite existing))
            {
                if (existing != null && existing != sprite)
                {
                    if (existing.texture != null)
                        UnityEngine.Object.Destroy(existing.texture);
                    UnityEngine.Object.Destroy(existing);
                }
            }

            _spriteCache[slotName] = sprite;
            MarkCacheEntryAsMostRecent(_spriteCacheOrder, slotName);
            TrimCacheToLimit();
        }

        private static void RemoveCacheEntry(string slotName)
        {
            _spriteCache.Remove(slotName);

            for (int i = 0; i < _spriteCacheOrder.Count; i++)
            {
                if (string.Equals(_spriteCacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                {
                    _spriteCacheOrder.RemoveAt(i);
                    return;
                }
            }
        }

        private static void TrimCacheToLimit()
        {
            while (_spriteCacheOrder.Count > MaxCachedSprites)
            {
                string oldestSlotName = _spriteCacheOrder[0];
                _spriteCacheOrder.RemoveAt(0);

                if (!_spriteCache.TryGetValue(oldestSlotName, out Sprite cached))
                    continue;

                _spriteCache.Remove(oldestSlotName);
                if (cached == null)
                    continue;

                if (cached.texture != null)
                    UnityEngine.Object.Destroy(cached.texture);
                UnityEngine.Object.Destroy(cached);
            }
        }

        private static void MarkCacheEntryAsMostRecent(List<string> cacheOrder, string slotName)
        {
            for (int i = 0; i < cacheOrder.Count; i++)
            {
                if (!string.Equals(cacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                    continue;

                cacheOrder.RemoveAt(i);
                break;
            }

            cacheOrder.Add(slotName);
        }
    }
}
