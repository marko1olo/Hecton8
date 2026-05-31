using Hecton8.SaveSystem;
using System;
using System.Threading;
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
        private CancellationTokenSource _loadCancellationSource;
        private int _loadSequence;

        private void Awake()
        {
            CacheCanvasGroups();
            ShowNoThumbnail();
        }

        private void OnDisable()
        {
            AdvanceLoadSequence();
        }

        private void OnDestroy()
        {
            AdvanceLoadSequence();
        }

        /// <summary>
        /// Requests a thumbnail capture for the provided save slot.
        /// </summary>
        public void CaptureThumbnail(string slotName)
        {
            if (!captureOnSave || !SaveManager.IsSafeSlotName(slotName))
                return;

            SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);
        }

        /// <summary>
        /// Loads and displays the thumbnail associated with the provided save slot.
        /// </summary>
        public void LoadThumbnail(string slotName)
        {
            AdvanceLoadSequence();

            if (!SaveManager.IsSafeSlotName(slotName))
            {
                ShowNoThumbnail();
                return;
            }

            ShowNoThumbnail();
            CancellationTokenSource loadCancellationSource = CreateLoadCancellationSource();
            _ = LoadThumbnailDeferredAsync(slotName, _loadSequence, loadCancellationSource);
        }

        private async Awaitable LoadThumbnailDeferredAsync(string slotName, int sequence, CancellationTokenSource loadCancellationSource)
        {
            try
            {
                CancellationToken cancellationToken = loadCancellationSource.Token;
                Texture2D thumbnailTexture = await SaveThumbnailSystem.LoadThumbnailTextureAsync(slotName, cancellationToken);
                if (sequence != _loadSequence || cancellationToken.IsCancellationRequested || destroyCancellationToken.IsCancellationRequested)
                    return;

                if (thumbnailTexture == null)
                {
                    ShowNoThumbnail();
                    return;
                }

                ShowThumbnail(thumbnailTexture);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_loadCancellationSource, loadCancellationSource))
                    _loadCancellationSource = null;

                loadCancellationSource.Dispose();
            }
        }

        /// <summary>
        /// Clears the currently displayed thumbnail without mutating the global thumbnail cache.
        /// </summary>
        public void ClearThumbnail()
        {
            AdvanceLoadSequence();
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

        private void AdvanceLoadSequence()
        {
            CancelActiveThumbnailLoad();
            unchecked
            {
                _loadSequence++;
                if (_loadSequence == 0)
                    _loadSequence = 1;
            }
        }

        private CancellationTokenSource CreateLoadCancellationSource()
        {
            CancelActiveThumbnailLoad();
            _loadCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); // COLD ALLOC: one cancellable thumbnail request owner.
            return _loadCancellationSource;
        }

        private void CancelActiveThumbnailLoad()
        {
            CancellationTokenSource source = _loadCancellationSource;
            if (source == null)
                return;

            _loadCancellationSource = null;
            if (!source.IsCancellationRequested)
                source.Cancel();
        }
    }
}
