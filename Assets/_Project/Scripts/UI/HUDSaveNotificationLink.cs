using UnityEngine;
using Hecton8.SaveSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Bridges the SaveSystem events to the HUD Notification UI.
    /// Ensures Zero-GC and clean separation of concerns.
    /// </summary>
    [AddComponentMenu("Hecton8/UI/HUD Save Notification Link")]
    public sealed class HUDSaveNotificationLink : MonoBehaviour
    {
        [SerializeField] private HUDNotification notificationSystem;
        
        private void OnEnable()
        {
            if (notificationSystem == null)
                notificationSystem = GetComponent<HUDNotification>();

            SaveEvents.OnSaveCompleted += HandleSaveCompleted;
            SaveEvents.OnSaveFailed += HandleSaveFailed;
        }

        private void OnDisable()
        {
            SaveEvents.OnSaveCompleted -= HandleSaveCompleted;
            SaveEvents.OnSaveFailed -= HandleSaveFailed;
        }

        private void HandleSaveCompleted(string slotName)
        {
            if (notificationSystem != null)
                notificationSystem.ShowInfo("GAME DATA SYNCHRONIZED — SECURE");
        }

        private void HandleSaveFailed(string slotName, string error)
        {
            if (notificationSystem != null)
                notificationSystem.ShowCritical("SAVE ERROR — CHECK LOGS");
        }
    }
}
