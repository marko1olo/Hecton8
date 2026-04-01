using System;
using System.IO;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Handles capturing and loading low-resolution screenshots for save slots.
    /// Integrated with SaveManager to provide visual context for saves.
    /// </summary>
    public static class SaveThumbnailSystem
    {
        private const int Width = 320;
        private const int Height = 180;
        private const string Extension = ".jpg";
        private const int Quality = 75;

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

            RenderTexture rt = new RenderTexture(Width, Height, 24);
            Texture2D screenShot = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            Camera.main.targetTexture = rt;
            Camera.main.Render();

            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            Camera.main.targetTexture = null;
            RenderTexture.active = null;
            
            UnityEngine.Object.Destroy(rt);

            byte[] bytes = screenShot.EncodeToJPG(Quality);
            UnityEngine.Object.Destroy(screenShot);

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
        }

        /// <summary>
        /// Loads a thumbnail for the specified slot.
        /// </summary>
        public static Sprite LoadThumbnail(string slotName)
        {
            string path = GetThumbnailPath(slotName);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            return null;
        }

        public static void DeleteThumbnail(string slotName)
        {
            string path = GetThumbnailPath(slotName);
            if (File.Exists(path))
                File.Delete(path);

            string tempPath = GetTempThumbnailPath(slotName);
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
