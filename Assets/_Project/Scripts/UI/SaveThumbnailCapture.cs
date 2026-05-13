using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Optional manual thumbnail trigger for legacy UI wiring. SaveManager owns save-request captures.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Thumbnail Capture")]
    public sealed class SaveThumbnailCapture : MonoBehaviour
    {
        [Header("=== CAPTURE CAMERA ===")]
        [SerializeField] private Camera captureCamera;

        public void CaptureThumbnail(string slotName)
        {
            if (!SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName))
                return;

            SaveThumbnailSystem.CaptureThumbnail(safeSlotName, captureCamera);
        }
    }
}
