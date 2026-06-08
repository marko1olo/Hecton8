using System;
using UnityEngine;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Bridges the SaveSystem events to the HUD Notification UI.
    /// Ensures Zero-GC and clean separation of concerns.
    /// </summary>
    [DisallowMultipleComponent]
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
        private static readonly int LoadFailedKeyHash = LocHash.Compute(LocalizationKeys.ERROR_LOAD_FAILED_TITLE);
        private static readonly int SlotPrefixKeyHash = LocHash.Compute(LocalizationKeys.SLOT_PREFIX);

        [SerializeField] private HUDNotification notificationSystem;

        private FixedCharBuffer _messageBuffer = new FixedCharBuffer(MessageCharCapacity); // COLD ALLOC: char[160] - save notification HUD staging buffer - owner: HUDSaveNotificationLink
        private ILocalizationTextReadModel _localization;
        private ulong _lastFailureNotificationSignature;
        private uint _lastConsumedFailureSnapshotSequence;
        private bool _hotSwapRegistered;

        private void OnEnable()
        {
            if (notificationSystem == null)
                TryGetComponent(out notificationSystem);

            _localization = GlobalRegistry.LocalizationText;
            TryRegisterHotSwapListener();
            SaveEvents.Register(this);
            LocalizationEvents.RegisterLanguageListener(this);
            LocalizationEvents.RegisterCorruptionVisualStateListener(this);
            TryShowLatestFailureSnapshot();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterCorruptionVisualStateListener(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            SaveEvents.Unregister(this);
            ClearMessageCache();
        }

        private void OnDestroy()
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

            if (IsDuplicateFailureNotification(in payload))
                return;

            string failureMessageOverride = ResolveFailureMessageOverride(in payload);
            if (!TryBuildMessage(in payload, failureMessageOverride))
                return;

            switch (payload.Type)
            {
                case SaveEventType.SaveCompleted:
                    notificationSystem.ShowInfo(in _messageBuffer);
                    return;

                case SaveEventType.SaveFailed:
                case SaveEventType.LoadFailed:
                    notificationSystem.ShowCritical(in _messageBuffer);
                    RememberFailureNotification(in payload);
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

        private void TryShowLatestFailureSnapshot()
        {
            if (notificationSystem == null)
                return;

            if (!SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref _lastConsumedFailureSnapshotSequence,
                    out SaveEventPayload payload,
                    out string failureMessage))
            {
                return;
            }

            if (IsDuplicateFailureNotification(in payload))
                return;

            if (!TryBuildMessage(in payload, failureMessage))
                return;

            notificationSystem.ShowCritical(in _messageBuffer);
            RememberFailureNotification(in payload);
        }

        private bool TryBuildMessage(in SaveEventPayload payload, string failureMessageOverride = null)
        {
            _messageBuffer.Clear();

            if (payload.Type == SaveEventType.SaveCompleted)
                AppendLocalized(ref _messageBuffer, SaveSynchronizedKeyHash, "GAME DATA SYNCHRONIZED - SECURE".AsSpan());
            else if (payload.Type == SaveEventType.SaveFailed)
                AppendLocalized(ref _messageBuffer, SaveFailedKeyHash, "SAVE FAILED".AsSpan());
            else if (payload.Type == SaveEventType.LoadFailed)
                AppendLocalized(ref _messageBuffer, LoadFailedKeyHash, "LOAD FAILED".AsSpan());
            else
                return false;

            AppendSlotLabel(ref _messageBuffer, payload.SlotHash);
            AppendFailureDetail(ref _messageBuffer, in payload, failureMessageOverride);
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

            _localization = currentService as ILocalizationTextReadModel;
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

        private void AppendSlotLabel(ref FixedCharBuffer buffer, uint slotHash)
        {
            if (slotHash == 0u)
                return;

            AppendLiteral(ref buffer, " [".AsSpan());
            AppendLocalized(ref buffer, SlotPrefixKeyHash, "SLOT".AsSpan());
            AppendChar(ref buffer, ' ');
            AppendSlotNumber(ref buffer, slotHash);
            AppendChar(ref buffer, ']');
        }

        private static void AppendSlotNumber(ref FixedCharBuffer buffer, uint slotHash)
        {
            string slotNumber = SaveEvents.ResolveSlotNumber(slotHash);
            for (int index = 0; index < slotNumber.Length; index++)
                AppendChar(ref buffer, slotNumber[index]);
        }

        private static void AppendFailureDetail(
            ref FixedCharBuffer buffer,
            in SaveEventPayload payload,
            string failureMessageOverride)
        {
            if (payload.Type != SaveEventType.SaveFailed &&
                payload.Type != SaveEventType.LoadFailed)
            {
                return;
            }

            string message = failureMessageOverride;
            if (string.IsNullOrEmpty(message) &&
                !SaveEvents.TryResolveMessage(in payload, out message))
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (ResolveRemainingCapacity(in buffer) <= 2)
                return;

            if (!TryAppendLiteral(ref buffer, ": ".AsSpan()))
                return;

            AppendTruncated(ref buffer, message.AsSpan());
        }

        private string ResolveFailureMessageOverride(in SaveEventPayload payload)
        {
            if (!IsFailurePayload(in payload))
                return null;

            if (SaveEvents.TryResolveMessage(in payload, out _))
                return null;

            return SaveEvents.TryConsumeMatchingFailureSnapshotForUi(
                ref _lastConsumedFailureSnapshotSequence,
                in payload,
                out string message)
                ? message
                : null;
        }

        private bool IsDuplicateFailureNotification(in SaveEventPayload payload)
        {
            ulong signature = BuildFailureNotificationSignature(in payload);
            return signature != 0UL && signature == _lastFailureNotificationSignature;
        }

        private void RememberFailureNotification(in SaveEventPayload payload)
        {
            ulong signature = BuildFailureNotificationSignature(in payload);
            if (signature != 0UL)
                _lastFailureNotificationSignature = signature;
        }

        private static ulong BuildFailureNotificationSignature(in SaveEventPayload payload)
        {
            if (!IsFailurePayload(in payload))
            {
                return 0UL;
            }

            ulong typePart = (ulong)(byte)payload.Type << 56;
            ulong slotPart = (ulong)payload.SlotHash << 24;
            return typePart ^ slotPart ^ payload.MessageHash ^ payload.TimestampTicks;
        }

        private static bool IsFailurePayload(in SaveEventPayload payload)
        {
            return payload.Type == SaveEventType.SaveFailed ||
                   payload.Type == SaveEventType.LoadFailed;
        }

        private static bool TryAppendLiteral(ref FixedCharBuffer buffer, ReadOnlySpan<char> text)
        {
            return buffer.Append(text);
        }

        private static void AppendTruncated(ref FixedCharBuffer buffer, ReadOnlySpan<char> text)
        {
            int remaining = ResolveRemainingCapacity(in buffer);
            if (remaining <= 0)
                return;

            if (text.Length <= remaining)
            {
                buffer.Append(text);
                return;
            }

            if (remaining <= 3)
            {
                buffer.Append(text.Slice(0, remaining));
                return;
            }

            buffer.Append(text.Slice(0, remaining - 3));
            buffer.Append("...".AsSpan());
        }

        private static int ResolveRemainingCapacity(in FixedCharBuffer buffer)
        {
            char[] rawBuffer = buffer.Buffer;
            return rawBuffer != null ? Math.Max(0, rawBuffer.Length - buffer.Length) : 0;
        }

        private void AppendLocalized(ref FixedCharBuffer buffer, int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationTextReadModel manager = _localization;
            ReadOnlySpan<char> text = manager != null ? manager.GetRawSpanOrFallback(keyHash, fallback) : fallback;
            buffer.Append(text);
        }

        private static void AppendLiteral(ref FixedCharBuffer buffer, ReadOnlySpan<char> text)
        {
            buffer.Append(text);
        }

        private static void AppendChar(ref FixedCharBuffer buffer, char value)
        {
            Span<char> one = stackalloc char[1];
            one[0] = value;
            buffer.Append(one);
        }

    }
}
