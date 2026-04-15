using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace Hecton8.UI
{
    /// <summary>
    /// Save slot thumbnail capture and display (Subnautica-style).
    /// Captures screenshot on save, displays on load menu.
    /// Thumbnails stored as PNG files alongside save data.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Slot Thumbnail")]
    public sealed class SaveSlotThumbnail : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int ThumbnailWidth = 320;
        private const int ThumbnailHeight = 180;
        private const string ThumbnailExtension = ".png";

        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== REFERENCES ===")]
        [SerializeField] private RawImage thumbnailImage;
        [SerializeField] private GameObject noThumbnailPlaceholder;

        [Header("=== SETTINGS ===")]
        [SerializeField] private Camera captureCamera;
        [SerializeField] private bool captureOnSave = true;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Texture2D _thumbnailTexture;
        private RenderTexture _captureRT;
        private CanvasGroup _thumbnailCanvasGroup;
        private CanvasGroup _placeholderCanvasGroup;
        private Camera _fallbackCaptureCamera;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CacheCanvasGroups();
            ShowNoThumbnail();
        }

        private void OnDestroy()
        {
            ReleaseThumbnailTexture();
            ReleaseCaptureRT();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — CAPTURE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Capture screenshot and save as thumbnail for slot.
        /// Call this when saving game.
        /// </summary>
        public void CaptureThumbnail(string slotName)
        {
            if (!captureOnSave || string.IsNullOrEmpty(slotName))
                return;

            Camera cam = ResolveCaptureCamera();
            if (cam == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveSlotThumbnail] No camera available for thumbnail capture.");
#endif
                return;
            }

            // Create RenderTexture for capture
            if (_captureRT == null || _captureRT.width != ThumbnailWidth || _captureRT.height != ThumbnailHeight)
            {
                ReleaseCaptureRT();
                _captureRT = new RenderTexture(ThumbnailWidth, ThumbnailHeight, 24, RenderTextureFormat.ARGB32);
                _captureRT.antiAliasing = 1;
            }

            // Capture camera view to RenderTexture
            RenderTexture previousRT = cam.targetTexture;
            cam.targetTexture = _captureRT;
            cam.Render();
            cam.targetTexture = previousRT;

            // Read pixels from RenderTexture
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = _captureRT;

            Texture2D screenshot = new Texture2D(ThumbnailWidth, ThumbnailHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, ThumbnailWidth, ThumbnailHeight), 0, 0);
            screenshot.Apply();

            RenderTexture.active = previousActive;

            // Encode to PNG and save
            byte[] pngData = screenshot.EncodeToPNG();
            string thumbnailPath = GetThumbnailPath(slotName);

            try
            {
                string directory = Path.GetDirectoryName(thumbnailPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(thumbnailPath, pngData);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SaveSlotThumbnail] Captured thumbnail for {slotName}: {thumbnailPath}");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveSlotThumbnail] Failed to save thumbnail for {slotName}: {ex.Message}");
            }

            // Cleanup temp texture
            if (Application.isPlaying)
                Destroy(screenshot);
            else
                DestroyImmediate(screenshot);
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — DISPLAY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Load and display thumbnail for slot.
        /// Call this when populating save slot UI.
        /// </summary>
        public void LoadThumbnail(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                ShowNoThumbnail();
                return;
            }

            string thumbnailPath = GetThumbnailPath(slotName);
            if (!File.Exists(thumbnailPath))
            {
                ShowNoThumbnail();
                return;
            }

            try
            {
                byte[] pngData = File.ReadAllBytes(thumbnailPath);
                ReleaseThumbnailTexture();

                _thumbnailTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (_thumbnailTexture.LoadImage(pngData))
                {
                    ShowThumbnail(_thumbnailTexture);
                }
                else
                {
                    ShowNoThumbnail();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveSlotThumbnail] Failed to load thumbnail for {slotName}: {ex.Message}");
                ShowNoThumbnail();
            }
        }

        /// <summary>
        /// Clear displayed thumbnail.
        /// </summary>
        public void ClearThumbnail()
        {
            ReleaseThumbnailTexture();
            ShowNoThumbnail();
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ShowThumbnail(Texture2D texture)
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = texture;
                thumbnailImage.enabled = true;
                SetCanvasVisible(_thumbnailCanvasGroup, true);
            }

            SetCanvasVisible(_placeholderCanvasGroup, false);
        }

        private void ShowNoThumbnail()
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.texture = null;
                thumbnailImage.enabled = false;
                SetCanvasVisible(_thumbnailCanvasGroup, false);
            }

            SetCanvasVisible(_placeholderCanvasGroup, true);
        }

        private void CacheCanvasGroups()
        {
            if (thumbnailImage != null && !thumbnailImage.TryGetComponent(out _thumbnailCanvasGroup))
                _thumbnailCanvasGroup = thumbnailImage.gameObject.AddComponent<CanvasGroup>();

            if (noThumbnailPlaceholder != null && !noThumbnailPlaceholder.TryGetComponent(out _placeholderCanvasGroup))
                _placeholderCanvasGroup = noThumbnailPlaceholder.AddComponent<CanvasGroup>();
        }

        private Camera ResolveCaptureCamera()
        {
            if (captureCamera != null)
                return captureCamera;

            if (_fallbackCaptureCamera != null)
                return _fallbackCaptureCamera;

            _fallbackCaptureCamera = Camera.main;
            return _fallbackCaptureCamera;
        }

        private static void SetCanvasVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void ReleaseThumbnailTexture()
        {
            if (_thumbnailTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_thumbnailTexture);
            else
                DestroyImmediate(_thumbnailTexture);

            _thumbnailTexture = null;
        }

        private void ReleaseCaptureRT()
        {
            if (_captureRT == null)
                return;

            _captureRT.Release();

            if (Application.isPlaying)
                Destroy(_captureRT);
            else
                DestroyImmediate(_captureRT);

            _captureRT = null;
        }

        private static string GetThumbnailPath(string slotName)
        {
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            return Path.Combine(saveDirectory, slotName + ThumbnailExtension);
        }
    }
}
