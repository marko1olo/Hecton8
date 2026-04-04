using System;
using System.IO;
using System.Collections.Generic;
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

        // Pooled resources to avoid dynamic allocations during Save
        private static RenderTexture _pooledRenderTexture;
        private static Texture2D _pooledTexture2D;
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pooledRenderTexture = null;
            _pooledTexture2D = null;
            _spriteCache.Clear();
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
            if (Camera.main == null) return;

            if (_pooledRenderTexture == null)
            {
                _pooledRenderTexture = new RenderTexture(Width, Height, 24);
                _pooledRenderTexture.name = "SaveThumbnail_PooledRT";
            }

            if (_pooledTexture2D == null)
            {
                _pooledTexture2D = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                _pooledTexture2D.name = "SaveThumbnail_PooledTex";
            }

            Camera.main.targetTexture = _pooledRenderTexture;
            Camera.main.Render();

            RenderTexture.active = _pooledRenderTexture;
            _pooledTexture2D.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            Camera.main.targetTexture = null;
            RenderTexture.active = null;

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

        /// <summary>
        /// Loads a thumbnail for the specified slot. Uses cache safely to avoid allocations.
        /// </summary>
        public static Sprite LoadThumbnail(string slotName)
        {
            if (_spriteCache.TryGetValue(slotName, out Sprite cached))
            {
                if (cached != null && cached.texture != null)
                    return cached;
                    
                _spriteCache.Remove(slotName);
            }

            string path = GetThumbnailPath(slotName);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _spriteCache[slotName] = s;
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
                _spriteCache.Remove(slotName);
            }
        }

        /// <summary>
        /// Purges cached runtime thumbnails to free Memory.
        /// Call when UI windows are closed.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _spriteCache)
            {
                if (kvp.Value != null)
                {
                    if (kvp.Value.texture != null)
                        UnityEngine.Object.Destroy(kvp.Value.texture);
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            _spriteCache.Clear();
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
    }
}
