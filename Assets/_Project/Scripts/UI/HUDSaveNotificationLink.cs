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
                notificationSystem.ShowInfo(BuildCompletedMessage(slotName));
        }

        private void HandleSaveFailed(string slotName, string error)
        {
            if (notificationSystem != null)
                notificationSystem.ShowCritical(BuildFailedMessage(slotName));
        }

        private static string BuildCompletedMessage(string slotName)
        {
            string baseMessage = ResolveLocalized(
                LocalizationKeys.SAVE_NOTIFICATION_SYNCHRONIZED,
                "GAME DATA SYNCHRONIZED - SECURE");
            return AppendSlotLabel(baseMessage, slotName);
        }

        private static string BuildFailedMessage(string slotName)
        {
            string baseMessage = ResolveLocalized(
                LocalizationKeys.ERROR_SAVE_FAILED_TITLE,
                "SAVE FAILED");
            return AppendSlotLabel(baseMessage, slotName);
        }

        private static string AppendSlotLabel(string baseMessage, string slotName)
        {
            string slotLabel = BuildSlotLabel(slotName);
            if (string.IsNullOrEmpty(slotLabel))
                return baseMessage;

            return string.Concat(baseMessage, " [", slotLabel, "]");
        }

        private static string BuildSlotLabel(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return string.Empty;

            LocalizationManager manager = LocalizationManager.Instance;
            string slotPrefix = manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, LocalizationKeys.SLOT_PREFIX, "SLOT")
                : "SLOT";

            return string.Concat(slotPrefix, " ", ExtractSlotNumber(slotName));
        }

        private static string ExtractSlotNumber(string slotName)
        {
            int underscoreIndex = slotName.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotName.Length - 1)
                return slotName.Substring(underscoreIndex + 1);

            return slotName;
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
