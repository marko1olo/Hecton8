using UnityEngine;
using Hecton8.SaveSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Wrapper component to listen to SaveEvents and trigger thumbnail capture.
    /// Attach to a GameObject in 02_HECTON_WORLD scene (e.g., [SaveManager] or dedicated [ThumbnailCapture]).
    /// Zero-GC: event subscription, cached SaveSlotThumbnail reference.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Thumbnail Capture")]
    public sealed class SaveThumbnailCapture : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== THUMBNAIL COMPONENT ===")]
        [SerializeField] private SaveSlotThumbnail thumbnailComponent;

        [Header("=== CAPTURE CAMERA ===")]
        [SerializeField] private Camera captureCamera;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // Create thumbnail component if not assigned
            if (thumbnailComponent == null)
            {
                GameObject thumbnailObj = new GameObject("ThumbnailCapture");
                thumbnailObj.transform.SetParent(transform, false);
                thumbnailComponent = thumbnailObj.AddComponent<SaveSlotThumbnail>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SaveThumbnailCapture] Created SaveSlotThumbnail component dynamically.");
#endif
            }
        }

        private void OnEnable()
        {
            SaveEvents.OnSaveStarted += OnSaveStarted;
        }

        private void OnDisable()
        {
            SaveEvents.OnSaveStarted -= OnSaveStarted;
        }

        // ══════════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void OnSaveStarted(string slotName)
        {
            if (thumbnailComponent == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveThumbnailCapture] thumbnailComponent is null. Cannot capture thumbnail.");
#endif
                return;
            }

            if (string.IsNullOrEmpty(slotName))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveThumbnailCapture] slotName is empty. Cannot capture thumbnail.");
#endif
                return;
            }

            // Capture thumbnail
            thumbnailComponent.CaptureThumbnail(slotName);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SaveThumbnailCapture] Captured thumbnail for slot: {slotName}");
#endif
        }
    }
}
