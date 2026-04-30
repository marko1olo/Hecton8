using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Bootstrap;
using Hecton8.Core;
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
        private const string Extension = ".png";
        private const string LegacyExtension = ".jpg";
        private const int MaxCachedSprites = 12;
        private const uint PngChunkIhdr = 0x49484452u;
        private const uint PngChunkIdat = 0x49444154u;
        private const uint PngChunkIend = 0x49454E44u;

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

            ClearCacheEntry(slotName);
            _requestSequence++;
            _pendingRequest = new CaptureRequest
            {
                SlotName = slotName,
                Camera = captureCamera,
                SequenceId = _requestSequence
            };
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

        private static void HandleReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (!_hasInflightRequest)
                return;

            CaptureRequest inflightRequest = _inflightRequest;
            _inflightRequest = default;
            _hasInflightRequest = false;

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

            // COLD ALLOC: NativeArray<byte>[Width * Height * 4] - persistent GPU readback staging buffer for background PNG write - owner: SaveThumbnailSystem
            NativeArray<byte> persistentRgba = new NativeArray<byte>(expectedLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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

                unsafe
                {
                    void* dataPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rgbaBytes);
                    using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    WritePngRgba32(stream, (byte*)dataPtr, rgbaBytes.Length, width, height);
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
                    rgbaBytes.Dispose();
            }
        }

        private static unsafe void WritePngRgba32(Stream stream, byte* rgbaBytes, int rgbaLength, int width, int height)
        {
            if (stream == null || rgbaBytes == null || width <= 0 || height <= 0)
                return;

            int stride = width * 4;
            int expectedLength = stride * height;
            if (rgbaLength < expectedLength)
                return;

            Span<byte> signature = stackalloc byte[8];
            signature[0] = 137;
            signature[1] = 80;
            signature[2] = 78;
            signature[3] = 71;
            signature[4] = 13;
            signature[5] = 10;
            signature[6] = 26;
            signature[7] = 10;
            stream.Write(signature);

            // COLD ALLOC: byte[13] - PNG IHDR payload generated off main thread - owner: SaveThumbnailSystem
            byte[] ihdr = new byte[13];
            WriteUInt32BigEndian(ihdr, 0, (uint)width);
            WriteUInt32BigEndian(ihdr, 4, (uint)height);
            ihdr[8] = 8;
            ihdr[9] = 6;
            ihdr[10] = 0;
            ihdr[11] = 0;
            ihdr[12] = 0;
            WritePngChunk(stream, PngChunkIhdr, ihdr, ihdr.Length);

            int filteredLength = height * (stride + 1);
            // COLD ALLOC: byte[height * (stride + 1)] - PNG scanline filter buffer generated off main thread - owner: SaveThumbnailSystem
            byte[] filtered = new byte[filteredLength];
            fixed (byte* filteredPtr = filtered)
            {
                for (int y = 0; y < height; y++)
                {
                    int destinationOffset = y * (stride + 1);
                    filteredPtr[destinationOffset] = 0;
                    Buffer.MemoryCopy(rgbaBytes + (y * stride), filteredPtr + destinationOffset + 1, stride, stride);
                }
            }

            byte[] idat = BuildUncompressedZlibPayload(filtered);
            WritePngChunk(stream, PngChunkIdat, idat, idat.Length);
            WritePngChunk(stream, PngChunkIend, null, 0);
        }

        private static byte[] BuildUncompressedZlibPayload(byte[] filtered)
        {
            int filteredLength = filtered != null ? filtered.Length : 0;
            int blockCount = Math.Max(1, (filteredLength + ushort.MaxValue - 1) / ushort.MaxValue);
            // COLD ALLOC: byte[zlib payload] - uncompressed PNG IDAT payload generated off main thread - owner: SaveThumbnailSystem
            byte[] payload = new byte[2 + (blockCount * 5) + filteredLength + 4];
            int cursor = 0;
            payload[cursor++] = 0x78;
            payload[cursor++] = 0x01;

            int sourceOffset = 0;
            int remaining = filteredLength;
            for (int block = 0; block < blockCount; block++)
            {
                int blockLength = Math.Min(ushort.MaxValue, remaining);
                bool isFinalBlock = block == blockCount - 1;
                payload[cursor++] = isFinalBlock ? (byte)1 : (byte)0;
                payload[cursor++] = (byte)(blockLength & 0xFF);
                payload[cursor++] = (byte)((blockLength >> 8) & 0xFF);
                int invertedLength = (~blockLength) & 0xFFFF;
                payload[cursor++] = (byte)(invertedLength & 0xFF);
                payload[cursor++] = (byte)((invertedLength >> 8) & 0xFF);
                if (blockLength > 0)
                    Buffer.BlockCopy(filtered, sourceOffset, payload, cursor, blockLength);

                cursor += blockLength;
                sourceOffset += blockLength;
                remaining -= blockLength;
            }

            uint adler = ComputeAdler32(filtered, filteredLength);
            payload[cursor++] = (byte)((adler >> 24) & 0xFF);
            payload[cursor++] = (byte)((adler >> 16) & 0xFF);
            payload[cursor++] = (byte)((adler >> 8) & 0xFF);
            payload[cursor] = (byte)(adler & 0xFF);
            return payload;
        }

        private static uint ComputeAdler32(byte[] data, int length)
        {
            const uint ModAdler = 65521u;
            uint a = 1u;
            uint b = 0u;
            for (int i = 0; i < length; i++)
            {
                a = (a + data[i]) % ModAdler;
                b = (b + a) % ModAdler;
            }

            return (b << 16) | a;
        }

        private static void WritePngChunk(Stream stream, uint chunkType, byte[] data, int length)
        {
            WriteUInt32BigEndian(stream, (uint)Math.Max(0, length));
            WriteUInt32BigEndian(stream, chunkType);
            if (data != null && length > 0)
                stream.Write(data, 0, length);

            WriteUInt32BigEndian(stream, ComputeChunkCrc(chunkType, data, length));
        }

        private static uint ComputeChunkCrc(uint chunkType, byte[] data, int length)
        {
            uint crc = 0xFFFFFFFFu;
            crc = UpdateCrc(crc, (byte)((chunkType >> 24) & 0xFF));
            crc = UpdateCrc(crc, (byte)((chunkType >> 16) & 0xFF));
            crc = UpdateCrc(crc, (byte)((chunkType >> 8) & 0xFF));
            crc = UpdateCrc(crc, (byte)(chunkType & 0xFF));
            if (data != null)
            {
                for (int i = 0; i < length; i++)
                    crc = UpdateCrc(crc, data[i]);
            }

            return ~crc;
        }

        private static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1u) != 0u ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;

            return crc;
        }

        private static void WriteUInt32BigEndian(Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            WriteUInt32BigEndian(buffer, 0, value);
            stream.Write(buffer);
        }

        private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteUInt32BigEndian(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
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
