using UnityEngine;
using Hecton.Localization;
using Hecton8.SaveSystem;
using Unity.Collections;

namespace Hecton8.UI
{
    /// <summary>
    /// Bridges the SaveSystem events to the HUD Notification UI.
    /// Ensures Zero-GC and clean separation of concerns.
    /// </summary>
    [AddComponentMenu("Hecton8/UI/HUD Save Notification Link")]
    public sealed class HUDSaveNotificationLink :
        MonoBehaviour,
        ISaveEventListener,
        ILocalizationLanguageChangedListener,
        ILocalizationCorruptionVisualStateListener
    {
        private const int MessageCacheCapacity = 8;

        private struct SaveNotificationCacheEntry
        {
            public FixedString64Bytes SlotName;
            public uint LanguageHash;
            public SaveEventType EventType;
            public string Message;
            public bool IsValid;
        }

        [SerializeField] private HUDNotification notificationSystem;

        // COLD ALLOC: SaveNotificationCacheEntry[8] — bounded save HUD message cache — owner: HUDSaveNotificationLink
        private readonly SaveNotificationCacheEntry[] _messageCache = new SaveNotificationCacheEntry[MessageCacheCapacity];
        private int _messageCacheCursor;
        
        private void OnEnable()
        {
            if (notificationSystem == null)
                notificationSystem = GetComponent<HUDNotification>();

            SaveEvents.Register(this);
            LocalizationEvents.RegisterLanguageListener(this);
            LocalizationEvents.RegisterCorruptionVisualStateListener(this);
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterCorruptionVisualStateListener(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            SaveEvents.Unregister(this);
            ClearMessageCache();
        }

        /// <summary>
        /// Routes deferred save-system payloads into the HUD notification surface.
        /// </summary>
        public void OnSaveEvent(in SaveEventPayload payload)
        {
            if (notificationSystem == null)
                return;

            switch (payload.Type)
            {
                case SaveEventType.SaveCompleted:
                    notificationSystem.ShowInfo(ResolveCachedMessage(in payload));
                    return;

                case SaveEventType.SaveFailed:
                    notificationSystem.ShowCritical(ResolveCachedMessage(in payload));
                    return;
            }
        }

        /// <summary>
        /// Invalidates cached localized save notification copy after language swaps.
        /// </summary>
        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            ClearMessageCache();
        }

        /// <summary>
        /// Invalidates cached localized save notification copy after visual glyph/corruption state changes.
        /// </summary>
        public void OnLocalizationCorruptionVisualStateChanged(in LocalizationEventPayload payload)
        {
            ClearMessageCache();
        }

        private string ResolveCachedMessage(in SaveEventPayload payload)
        {
            uint languageHash = GetCurrentLanguageHash();
            for (int i = 0; i < _messageCache.Length; i++)
            {
                SaveNotificationCacheEntry entry = _messageCache[i];
                if (!entry.IsValid ||
                    entry.EventType != payload.Type ||
                    entry.LanguageHash != languageHash ||
                    !entry.SlotName.Equals(payload.SlotName))
                {
                    continue;
                }

                return entry.Message;
            }

            string slotName = payload.SlotName.ToString();
            string message = payload.Type == SaveEventType.SaveCompleted
                ? BuildCompletedMessage(slotName)
                : BuildFailedMessage(slotName);

            int cacheIndex = _messageCacheCursor;
            _messageCacheCursor = (_messageCacheCursor + 1) % _messageCache.Length;
            _messageCache[cacheIndex] = new SaveNotificationCacheEntry
            {
                SlotName = payload.SlotName,
                LanguageHash = languageHash,
                EventType = payload.Type,
                Message = message,
                IsValid = true
            };

            return message;
        }

        private void ClearMessageCache()
        {
            for (int i = 0; i < _messageCache.Length; i++)
                _messageCache[i] = default;

            _messageCacheCursor = 0;
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

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            string slotPrefix = ResolveCurrentLanguage(manager, LocalizationKeys.SLOT_PREFIX, "SLOT");

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
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return ResolveCurrentLanguage(manager, key, fallback);
        }

        private static string ResolveCurrentLanguage(LocalizationManager manager, string key, string fallback)
        {
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static uint GetCurrentLanguageHash()
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? unchecked((uint)manager.CurrentLanguage)
                : 0u;
        }
    }
}
