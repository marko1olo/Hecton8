using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Displays cached save thumbnails and delegates capture requests to SaveThumbnailSystem.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Slot Thumbnail")]
    public sealed class SaveSlotThumbnail : MonoBehaviour
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private RawImage thumbnailImage;
        [SerializeField] private GameObject noThumbnailPlaceholder;

        [Header("=== SETTINGS ===")]
        [SerializeField] private Camera captureCamera;
        [SerializeField] private bool captureOnSave = true;

        private CanvasGroup _thumbnailCanvasGroup;
        private CanvasGroup _placeholderCanvasGroup;

        private void Awake()
        {
            CacheCanvasGroups();
            ShowNoThumbnail();
        }

        /// <summary>
        /// Requests a thumbnail capture for the provided save slot.
        /// </summary>
        public void CaptureThumbnail(string slotName)
        {
            if (!captureOnSave || string.IsNullOrEmpty(slotName))
                return;

            SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);
        }

        /// <summary>
        /// Loads and displays the thumbnail associated with the provided save slot.
        /// </summary>
        public void LoadThumbnail(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                ShowNoThumbnail();
                return;
            }

            Sprite thumbnailSprite = SaveThumbnailSystem.LoadThumbnail(slotName);
            if (thumbnailSprite == null || thumbnailSprite.texture == null)
            {
                ShowNoThumbnail();
                return;
            }

            ShowThumbnail(thumbnailSprite.texture);
        }

        /// <summary>
        /// Clears the currently displayed thumbnail without mutating the global thumbnail cache.
        /// </summary>
        public void ClearThumbnail()
        {
            ShowNoThumbnail();
        }

        /// <summary>
        /// Injects an explicit capture camera from the local save-thumbnail owner.
        /// </summary>
        internal void SetCaptureCamera(Camera camera)
        {
            captureCamera = camera;
        }

        private void ShowThumbnail(Texture texture)
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

        private static void SetCanvasVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
