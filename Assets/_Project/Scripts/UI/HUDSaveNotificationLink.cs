using UnityEngine;
using Hecton.Localization;
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
                notificationSystem.ShowInfo(ResolveLocalized(
                    LocalizationKeys.SAVE_NOTIFICATION_SYNCHRONIZED,
                    "GAME DATA SYNCHRONIZED - SECURE"));
        }

        private void HandleSaveFailed(string slotName, string error)
        {
            if (notificationSystem != null)
                notificationSystem.ShowCritical(ResolveLocalized(
                    LocalizationKeys.SAVE_NOTIFICATION_FAILED,
                    "SAVE ERROR - CHECK LOGS"));
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
