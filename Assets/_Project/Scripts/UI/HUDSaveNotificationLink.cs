using System;
using UnityEngine;
using Hecton.Localization;
using Hecton8.Core;
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
        ILocalizationCorruptionVisualStateListener,
        IGlobalRegistryHotSwapListener
    {
        private const int MessageCharCapacity = 160;
        private static readonly int SaveSynchronizedKeyHash = LocHash.Compute(LocalizationKeys.SAVE_NOTIFICATION_SYNCHRONIZED);
        private static readonly int SaveFailedKeyHash = LocHash.Compute(LocalizationKeys.ERROR_SAVE_FAILED_TITLE);
        private static readonly int SlotPrefixKeyHash = LocHash.Compute(LocalizationKeys.SLOT_PREFIX);

        [SerializeField] private HUDNotification notificationSystem;

        private FixedCharBuffer _messageBuffer = new FixedCharBuffer(MessageCharCapacity); // COLD ALLOC: char[160] - save notification HUD staging buffer - owner: HUDSaveNotificationLink
        private LocalizationManager _localization;
        private bool _hotSwapRegistered;
        
        private void OnEnable()
        {
            if (notificationSystem == null)
                TryGetComponent(out notificationSystem);

            _localization = GlobalRegistry.Localization;
            TryRegisterHotSwapListener();
            SaveEvents.Register(this);
            LocalizationEvents.RegisterLanguageListener(this);
            LocalizationEvents.RegisterCorruptionVisualStateListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
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

            if (!TryBuildMessage(in payload))
                return;

            switch (payload.Type)
            {
                case SaveEventType.SaveCompleted:
                    notificationSystem.ShowInfo(in _messageBuffer);
                    return;

                case SaveEventType.SaveFailed:
                    notificationSystem.ShowCritical(in _messageBuffer);
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

        private bool TryBuildMessage(in SaveEventPayload payload)
        {
            _messageBuffer.Clear();

            if (payload.Type == SaveEventType.SaveCompleted)
                AppendLocalized(ref _messageBuffer, SaveSynchronizedKeyHash, "GAME DATA SYNCHRONIZED - SECURE".AsSpan());
            else if (payload.Type == SaveEventType.SaveFailed)
                AppendLocalized(ref _messageBuffer, SaveFailedKeyHash, "SAVE FAILED".AsSpan());
            else
                return false;

            AppendSlotLabel(ref _messageBuffer, in payload.SlotName);
            return _messageBuffer.Length > 0;
        }

        private void ClearMessageCache()
        {
            _messageBuffer.Clear();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
                return;

            _localization = currentService as LocalizationManager;
            ClearMessageCache();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void AppendSlotLabel(ref FixedCharBuffer buffer, in FixedString64Bytes slotName)
        {
            if (slotName.Length <= 0)
                return;

            AppendLiteral(ref buffer, " [".AsSpan());
            AppendLocalized(ref buffer, SlotPrefixKeyHash, "SLOT".AsSpan());
            AppendChar(ref buffer, ' ');
            AppendSlotNumber(ref buffer, in slotName);
            AppendChar(ref buffer, ']');
        }

        private static void AppendSlotNumber(ref FixedCharBuffer buffer, in FixedString64Bytes slotName)
        {
            int startIndex = 0;
            for (int index = slotName.Length - 1; index >= 0; index--)
            {
                if (slotName[index] == (byte)'_')
                {
                    startIndex = index + 1;
                    break;
                }
            }

            for (int index = startIndex; index < slotName.Length; index++)
                AppendAsciiByte(ref buffer, slotName[index]);
        }

        private void AppendLocalized(ref FixedCharBuffer buffer, int keyHash, ReadOnlySpan<char> fallback)
        {
            LocalizationManager manager = _localization;
            ReadOnlySpan<char> text = manager != null ? manager.GetRawSpanOrFallback(keyHash, fallback) : fallback;
            buffer.Append(text);
        }

        private static void AppendLiteral(ref FixedCharBuffer buffer, ReadOnlySpan<char> text)
        {
            buffer.Append(text);
        }

        private static void AppendAsciiByte(ref FixedCharBuffer buffer, byte value)
        {
            AppendChar(ref buffer, value >= 32 && value < 127 ? (char)value : '?');
        }

        private static void AppendChar(ref FixedCharBuffer buffer, char value)
        {
            Span<char> one = stackalloc char[1];
            one[0] = value;
            buffer.Append(one);
        }

    }
}
