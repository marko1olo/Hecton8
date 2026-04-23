using System;
using System.IO;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Handles capturing and loading low-resolution screenshots for save slots.
    /// Integrated with SaveManager to provide visual context for saves.
    /// [Zero-GC Optimized: Pools resources to prevent CPU allocations]
    /// </summary>
    public static class SaveThumbnailSystem
    {
        private const int Width = 320;
        private const int Height = 180;
        private const string Extension = ".jpg";
        private const int Quality = 75;
        private const int MaxCachedSprites = 12;

        // Pooled resources to avoid dynamic allocations during Save
        private static RenderTexture _pooledRenderTexture;
        private static Texture2D _pooledTexture2D;
        private static Camera _cachedCaptureCamera;
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(MaxCachedSprites, StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _spriteCacheOrder = new List<string>(MaxCachedSprites);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleasePooledResources();
            ClearCache();
            _cachedCaptureCamera = null;
        }

        public static string GetThumbnailPath(string slotName)
        {
            return Path.Combine(Application.persistentDataPath, slotName + Extension);
        }

        public static string GetTempThumbnailPath(string slotName)
        {
            return GetThumbnailPath(slotName) + ".tmp";
        }

        /// <summary>
        /// Captures the current view and saves it as a thumbnail for the given slot.
        /// Must be called from the Main Thread.
        /// </summary>
        public static void CaptureThumbnail(string slotName)
        {
            if (!TryResolveCaptureCamera(out Camera captureCamera))
                return;

            if (_pooledRenderTexture == null)
            {
                _pooledRenderTexture = new RenderTexture(Width, Height, 24);
                _pooledRenderTexture.name = "SaveThumbnail_PooledRT";
                _pooledRenderTexture.useMipMap = false;
                _pooledRenderTexture.autoGenerateMips = false;
                _pooledRenderTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_pooledTexture2D == null)
            {
                _pooledTexture2D = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                _pooledTexture2D.name = "SaveThumbnail_PooledTex";
                _pooledTexture2D.hideFlags = HideFlags.HideAndDontSave;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = captureCamera.targetTexture;

            try
            {
                captureCamera.targetTexture = _pooledRenderTexture;
                captureCamera.Render();

                RenderTexture.active = _pooledRenderTexture;
                _pooledTexture2D.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                _pooledTexture2D.Apply(false, false);
            }
            finally
            {
                captureCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }

            byte[] bytes = _pooledTexture2D.EncodeToJPG(Quality);

            string path = GetThumbnailPath(slotName);
            string tempPath = GetTempThumbnailPath(slotName);
            
            // Background write with temp-file swap to avoid half-written thumbnails.
            File.WriteAllBytesAsync(tempPath, bytes).ContinueWith(t => 
            {
                if (t.IsFaulted)
                {
                    Debug.LogError($"[SaveThumbnailSystem] Failed to save thumbnail: {t.Exception}");
                    return;
                }

                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    File.Move(tempPath, path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveThumbnailSystem] Failed to promote thumbnail for '{slotName}': {ex.Message}");
                }
            });
            
            // Invalidate the cache for this slot so UI triggers a reload if necessary
            ClearCacheEntry(slotName);
        }

        private static bool TryResolveCaptureCamera(out Camera captureCamera)
        {
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
                _cachedCaptureCamera = playerTransform.GetComponentInChildren<Camera>(true);
            }

            captureCamera = _cachedCaptureCamera;
            return captureCamera != null;
        }

        /// <summary>
        /// Loads a thumbnail for the specified slot. Uses cache safely to avoid allocations.
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

            string path = GetThumbnailPath(slotName);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.hideFlags = HideFlags.HideAndDontSave;
            if (tex.LoadImage(bytes, true))
            {
                Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                s.hideFlags = HideFlags.HideAndDontSave;
                AddCacheEntry(slotName, s);
                return s;
            }

            UnityEngine.Object.Destroy(tex);
            return null;
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

        /// <summary>
        /// Purges cached runtime thumbnails to free Memory.
        /// Call when UI windows are closed.
        /// </summary>
        public static void ClearCache()
        {
            Dictionary<string, Sprite>.Enumerator enumerator = _spriteCache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, Sprite> kvp = enumerator.Current;
                if (kvp.Value != null)
                {
                    if (kvp.Value.texture != null)
                        UnityEngine.Object.Destroy(kvp.Value.texture);
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            _spriteCache.Clear();
            _spriteCacheOrder.Clear();
        }

        private static void ReleasePooledResources()
        {
            if (_pooledRenderTexture != null)
            {
                _pooledRenderTexture.Release();
                UnityEngine.Object.Destroy(_pooledRenderTexture);
                _pooledRenderTexture = null;
            }

            if (_pooledTexture2D != null)
            {
                UnityEngine.Object.Destroy(_pooledTexture2D);
                _pooledTexture2D = null;
            }
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
