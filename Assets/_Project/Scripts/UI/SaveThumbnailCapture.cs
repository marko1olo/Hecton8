using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Save-event listener that delegates thumbnail capture to SaveThumbnailSystem.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Thumbnail Capture")]
    public sealed class SaveThumbnailCapture : MonoBehaviour, ISaveEventListener
    {
        [Header("=== CAPTURE CAMERA ===")]
        [SerializeField] private Camera captureCamera;

        private void OnEnable()
        {
            SaveEvents.Register(this);
        }

        private void OnDisable()
        {
            SaveEvents.Unregister(this);
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            if (payload.Type != SaveEventType.SaveStarted || payload.SlotName.Length == 0)
                return;

            string slotName = payload.SlotName.ToString();
            SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);
        }
    }
}
