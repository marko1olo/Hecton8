using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Hecton8.Bootstrap;

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
        private const string TempThumbnailExtension = ".png.tmp";

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
        private Texture2D _captureTexture;
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
            ReleaseCaptureTexture();
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

            Texture2D screenshot = GetOrCreateCaptureTexture();
            if (screenshot == null)
            {
                RenderTexture.active = previousActive;
                return;
            }

            screenshot.ReadPixels(new Rect(0, 0, ThumbnailWidth, ThumbnailHeight), 0, 0);
            screenshot.Apply(false, false);

            RenderTexture.active = previousActive;

            // Encode to PNG and save
            byte[] pngData = screenshot.EncodeToPNG();
            string thumbnailPath = GetThumbnailPath(slotName);
            string tempThumbnailPath = GetTempThumbnailPath(slotName);

            try
            {
                string directory = Path.GetDirectoryName(thumbnailPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(tempThumbnailPath))
                    File.Delete(tempThumbnailPath);

                File.WriteAllBytes(tempThumbnailPath, pngData);

                if (File.Exists(thumbnailPath))
                    File.Replace(tempThumbnailPath, thumbnailPath, null, true);
                else
                    File.Move(tempThumbnailPath, thumbnailPath);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SaveSlotThumbnail] Captured thumbnail for {slotName}: {thumbnailPath}");
#endif
            }
            catch (System.Exception ex)
            {
                TryDeleteTempThumbnail(tempThumbnailPath);
                Debug.LogError($"[SaveSlotThumbnail] Failed to save thumbnail for {slotName}: {ex.Message}");
            }

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
                Texture2D thumbnailTexture = GetOrCreateThumbnailTexture();
                if (thumbnailTexture != null && thumbnailTexture.LoadImage(pngData, true))
                {
                    ShowThumbnail(thumbnailTexture);
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

        /// <summary>
        /// Injects an explicit capture camera from the local save-thumbnail owner.
        /// </summary>
        internal void SetCaptureCamera(Camera camera)
        {
            captureCamera = camera;
            if (camera != null)
                _fallbackCaptureCamera = camera;
        }

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

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    _fallbackCaptureCamera = playerOwnedCamera;
                    return _fallbackCaptureCamera;
                }

                Camera playerChildCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
                if (playerChildCamera != null)
                {
                    _fallbackCaptureCamera = playerChildCamera;
                    return _fallbackCaptureCamera;
                }
            }

            if (TryGetComponent(out Camera localCamera))
            {
                _fallbackCaptureCamera = localCamera;
                return _fallbackCaptureCamera;
            }

            Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
            if (childCamera != null)
            {
                _fallbackCaptureCamera = childCamera;
                return _fallbackCaptureCamera;
            }

            Camera parentCamera = GetComponentInParent<Camera>();
            if (parentCamera != null)
            {
                _fallbackCaptureCamera = parentCamera;
                return _fallbackCaptureCamera;
            }

            _fallbackCaptureCamera = null;
            return null;
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

        private Texture2D GetOrCreateThumbnailTexture()
        {
            if (_thumbnailTexture != null)
                return _thumbnailTexture;

            // COLD ALLOC: Texture2D[1] — reusable save-slot thumbnail display texture — owner: SaveSlotThumbnail
            _thumbnailTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            return _thumbnailTexture;
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

        private Texture2D GetOrCreateCaptureTexture()
        {
            if (_captureTexture != null &&
                _captureTexture.width == ThumbnailWidth &&
                _captureTexture.height == ThumbnailHeight)
            {
                return _captureTexture;
            }

            ReleaseCaptureTexture();
            // COLD ALLOC: Texture2D[1] — reusable save-slot thumbnail capture buffer — owner: SaveSlotThumbnail
            _captureTexture = new Texture2D(ThumbnailWidth, ThumbnailHeight, TextureFormat.RGB24, false);
            return _captureTexture;
        }

        private void ReleaseCaptureTexture()
        {
            if (_captureTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_captureTexture);
            else
                DestroyImmediate(_captureTexture);

            _captureTexture = null;
        }

        private static string GetThumbnailPath(string slotName)
        {
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            return Path.Combine(saveDirectory, slotName + ThumbnailExtension);
        }

        private static string GetTempThumbnailPath(string slotName)
        {
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            return Path.Combine(saveDirectory, slotName + TempThumbnailExtension);
        }

        private static void TryDeleteTempThumbnail(string tempThumbnailPath)
        {
            if (string.IsNullOrEmpty(tempThumbnailPath) || !File.Exists(tempThumbnailPath))
                return;

            try
            {
                File.Delete(tempThumbnailPath);
            }
            catch
            {
                // Ignore cleanup failure. The authoritative thumbnail path remains unchanged.
            }
        }
    }
}
