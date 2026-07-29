// ============================================================================
// HECTON-8 â€” HUDNotification.cs
// ÐšÑ€Ð°Ñ‚ÐºÐ¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ðµ ÑƒÐ²ÐµÐ´Ð¾Ð¼Ð»ÐµÐ½Ð¸Ñ Ð½Ð° HUD (Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€ÑŒ Ð¿Ð¾Ð»Ð¾Ð½, Ð¸ Ñ‚.Ð´.)
// Sibling Ðº HUD_V4_CanvasRoot Ð½Ð° Suit_HUD_Canvas.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton.Localization;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Notification")]
    public sealed class HUDNotification : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, INotificationEventListener, IInventoryEventListener, IGlobalRegistryHotSwapListener
    {
        private enum NotificationSeverity
        {
            Info = 0,
            Warning = 1,
            Critical = 2
        }

        private const int MaxNotificationQueueCapacity = 8;
        private const int FixedBufferMessageCacheSize = MaxNotificationQueueCapacity + 1;
        private const int FixedBufferMessageCharCapacity = 512;
        private const int MaxHudNotificationSignalsPerLateFrame = MaxNotificationQueueCapacity;
        private const string HudSignalFallbackPrefix = "HUD SIGNAL 0x";
        private const string InventoryFullMessagePrefix = "INVENTORY FULL // CANNOT STORE ";
        private const string FallbackInventoryItemName = "ITEM";
        private const uint HudNotificationSignalUnresolvedWarningHash = 0x484E5355u; // HNSU
        private const uint HudNotificationSignalContextHash = 0x484E5347u; // HNSG
        private const double UnresolvedSignalTelemetryCooldownSeconds = 5.0;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID QueueBufferId = BufferID.HudNotificationQueue;
        private static readonly uint HudNotificationRegistrationMissWarningHash = unchecked((uint)LocHash.Compute("HUDNotification.MessageRegistrationMiss"));
        private static readonly uint HudNotificationQueueDropWarningHash = unchecked((uint)LocHash.Compute("HUDNotification.QueueDrop"));
        private static readonly uint HudNotificationQueueWriteMissWarningHash = unchecked((uint)LocHash.Compute("HUDNotification.QueueWriteMiss"));
        private static readonly uint HudNotificationSignalMissWarningHash = unchecked((uint)LocHash.Compute("HUDNotification.SignalMessageMiss"));
        private static readonly uint HudNotificationContextHash = unchecked((uint)LocHash.Compute("HUDNotification"));

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct NotificationRequest
        {
            [FieldOffset(0)]
            public uint MessageHash;
            [FieldOffset(4)]
            public byte Severity;
            [FieldOffset(5)]
            private byte _pad0;
            [FieldOffset(6)]
            private ushort _pad1;
        }

        private struct FixedBufferMessageCacheEntry
        {
            public uint MessageHash;
            public int Length;
            public byte IsValid;
        }

        [Header("â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeSpeed = 4f;
        [SerializeField] private int maxQueuedNotifications = 6;
        [SerializeField] private float repeatSuppressWindow = 0.85f;
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color WarningBg = new Color(0.12f, 0.06f, 0.02f, 0.7f);
        private static readonly Color WarningText = new Color(1f, 0.74f, 0.22f, 0.95f);
        private static readonly Color CriticalBg = new Color(0.18f, 0.03f, 0.03f, 0.78f);
        private static readonly Color CriticalText = new Color(1f, 0.52f, 0.42f, 0.98f);
        private static readonly Color InfoBg = new Color(0.02f, 0.08f, 0.1f, 0.7f);
        private static readonly Color InfoText = new Color(0.46f, 0.98f, 0.94f, 0.9f);
        // COLD ALLOC: FixedBufferMessageCacheEntry[9] - active plus queued fixed-buffer HUD messages - owner: HUDNotification
        private readonly FixedBufferMessageCacheEntry[] _fixedBufferMessageCache =
            new FixedBufferMessageCacheEntry[FixedBufferMessageCacheSize];

        // COLD ALLOC: char[4608] - fixed-buffer HUD message cache backing store - owner: HUDNotification
        private readonly char[] _fixedBufferMessageCharacters =
            new char[FixedBufferMessageCacheSize * FixedBufferMessageCharCapacity];

        // COLD ALLOC: char[512] - hash-only HUD signal localization decode buffer - owner: HUDNotification
        private readonly char[] _hudSignalDecodeCharacters = new char[FixedBufferMessageCharCapacity];

        private FixedCharBuffer _inventoryFullMessageBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - inventory-full notification staging buffer - owner: HUDNotification

        private RectTransform _notifRoot;
        private Image _notifBg;
        private TextMeshProUGUI _notifText;
        private CanvasGroup _canvasGroup;
        private float _timer;
        private float _currentAlpha;
        private bool _built;
        private bool _isShowing;
        private VaultGenerationHandle<NotificationRequest> _queueHandle;
        private IDataVault _dataVault;
        private IDataVault _queueWriteVault;
        private int _queueCount;
        private int _queueCapacity;
        private uint _currentMessageHash;
        private NotificationSeverity _currentSeverity;
        private uint _lastEnqueuedMessageHash;
        private NotificationSeverity _lastEnqueuedSeverity;
        private float _lastEnqueueTime = -999f;
        private int _fixedBufferMessageCacheCursor;
        private bool _registeredToTickManager;
        private bool _registeredToLateFrame;
        private bool _registeredHotSwapListener;
        private bool _tickDormant = true;
        private bool _presentationDirty;
        private bool _visualStyleDirty;
        private bool _textDirty;
        private int _unresolvedSignalCount;
        private double _nextUnresolvedSignalTelemetryTime;
        private ILocalizationStressPresentationReadModel _localizationStressPresentation;
        private int _lastStressCorruptionBucket = int.MinValue;
        private int _messageRegistrationMissCount;
        private int _queueDropCount;
        private int _queueWriteMissCount;
        private int _hudSignalMessageMissCount;
        private int _lastMessageRegistrationMissTelemetryFrame = -1;
        private int _lastQueueDropTelemetryFrame = -1;
        private int _lastQueueWriteMissTelemetryFrame = -1;
        private int _lastHudSignalMessageMissTelemetryFrame = -1;
        private static HUDNotification _activeRuntime;

        public int MessageRegistrationMissCount => _messageRegistrationMissCount;
        public int QueueDropCount => _queueDropCount;
        public int QueueWriteMissCount => _queueWriteMissCount;
        public int HudSignalMessageMissCount => _hudSignalMessageMissCount;

        public static bool TryGetActive(out HUDNotification notification)
        {
            return TryUseRegisteredNotification(_activeRuntime, out notification);
        }

        private static bool TryUseRegisteredNotification(HUDNotification candidate, out HUDNotification notification)
        {
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                notification = candidate;
                return true;
            }

            notification = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ApplyPreviewSafeState();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRuntime = null;
        }

        private void OnEnable()
        {
            _activeRuntime = this;
            if (font == null) font = TMP_Settings.defaultFontAsset;

            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            InventoryEvents.Register(this);
            NotificationEvents.Register(this);

            EnsureBuilt();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_activeRuntime, this))
                _activeRuntime = null;

            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            InventoryEvents.Unregister(this);
            NotificationEvents.Unregister(this);
            _queueCount = 0;
            _currentMessageHash = 0u;
            _unresolvedSignalCount = 0;
            _nextUnresolvedSignalTelemetryTime = 0d;
            ClearFixedBufferMessageCache();
            ClearNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_activeRuntime, this))
                _activeRuntime = null;

            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            InventoryEvents.Unregister(this);
            NotificationEvents.Unregister(this);
            ReleaseQueue(_dataVault);
            ClearNotificationDiagnostics();
        }

        public void Tick(float deltaTime)
        {
            // The signal lane is drained ONCE per frame, in LateFrameTick, and deliberately not here.
            //
            // Both drains used to run: this one through DrainHudNotificationSignals (GetFrameSnapshot, which
            // does NOT advance SignalBus's _legacyReadCursor) and then DrainHudNotificationSignalLane in
            // LateFrameTick (TryConsumeFrame, which reads frameSnapshot[_legacyReadCursor++] out of the SAME
            // buffer). Tick runs first, so every signal was enqueued twice per frame — and the repeat
            // suppressor could not absorb the duplicate, because the two paths enqueue under DIFFERENT hashes.
            // This one passed `signal.MessageHash`, the localisation KEY hash; the lane decodes that key through
            // LocRegistry and re-registers the rendered TEXT, so Enqueue(ReadOnlySpan<char>) derives a hash of
            // the characters instead. The suppressor at :533 compares `messageHash == _lastEnqueuedMessageHash`,
            // which therefore never matched. The player saw every notification twice.
            //
            // Nobody could observe that, and the reason is worth recording rather than glossing: this component
            // had ZERO instances anywhere in the project until GameBootstrapper.EnsureHudNotificationRegistered
            // was added, so both drains ran on nothing at all. Building the surface is what made the duplicate
            // real, which makes closing it part of that change rather than a follow-up to it.
            //
            // The lane drain is the survivor on three counts: it CONSUMES, so the cursor advances and the frame
            // does not leak into the next reader; it is BUDGETED at MaxHudNotificationSignalsPerLateFrame rather
            // than iterating an unbounded snapshot; and it resolves display text through LocRegistry instead of
            // handing a raw key hash to the queue. It carries its own miss reporting through
            // ReportHudSignalMessageMiss, so no diagnostic coverage is lost by dropping this call.
            if (_tickDormant) return;
            if (_notifRoot == null) return;

            if (_timer > 0f)
            {
                _timer -= deltaTime;
                _currentAlpha = math.lerp(_currentAlpha, 1f, ResolveDecayBlend(fadeSpeed, deltaTime));
            }
            else
            {
                _currentAlpha = math.lerp(_currentAlpha, 0f, ResolveDecayBlend(fadeSpeed, deltaTime));

                if (_currentAlpha < 0.01f)
                {
                    _currentAlpha = 0f;
                    _isShowing = false;

                    if (_queueCount > 0)
                    {
                        NotificationRequest next = PopQueueFront();
                        ShowImmediate(next.MessageHash, (NotificationSeverity)next.Severity);
                    }
                    else
                    {
                        _tickDormant = true;
                    }
                }
            }

            if (_isShowing)
                _textDirty = true;

            _presentationDirty = true;
        }

        private void DrainHudNotificationSignals()
        {
            ReadOnlySpan<HUDNotificationSignal> signals = SignalBus<HUDNotificationSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                HUDNotificationSignal signal = signals[i];
                if (!CanResolveDisplayMessage(signal.MessageHash))
                {
                    ReportUnresolvedHudNotificationSignal(in signal);
                    continue;
                }

                Enqueue(signal.MessageHash, ResolveSignalSeverity(signal.Severity));
            }
        }

        private bool CanResolveDisplayMessage(uint messageHash)
        {
            if (messageHash == 0u)
                return false;

            if (HasFixedBufferMessage(messageHash))
                return true;

            return NotificationEvents.TryResolveMessageSpan(messageHash, out ReadOnlySpan<char> message) &&
                   message.Length > 0;
        }

        private bool HasFixedBufferMessage(uint messageHash)
        {
            if (messageHash == 0u)
                return false;

            for (int i = 0; i < _fixedBufferMessageCache.Length; i++)
            {
                FixedBufferMessageCacheEntry entry = _fixedBufferMessageCache[i];
                if (entry.IsValid != 0 && entry.MessageHash == messageHash && entry.Length > 0)
                    return true;
            }

            return false;
        }

        private static NotificationSeverity ResolveSignalSeverity(byte severity)
        {
            if (severity >= (byte)NotificationSeverity.Critical)
                return NotificationSeverity.Critical;

            return severity == (byte)NotificationSeverity.Warning
                ? NotificationSeverity.Warning
                : NotificationSeverity.Info;
        }

        private void ReportUnresolvedHudNotificationSignal(in HUDNotificationSignal signal)
        {
            if (_unresolvedSignalCount < int.MaxValue)
                _unresolvedSignalCount++;

            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextUnresolvedSignalTelemetryTime)
                return;

            _nextUnresolvedSignalTelemetryTime = now + UnresolvedSignalTelemetryCooldownSeconds;
            uint contextHash = signal.SourceId != 0u ? signal.SourceId : HudNotificationSignalContextHash;
            GlobalTelemetryBus.PublishPerformanceWarning(
                HudNotificationSignalUnresolvedWarningHash,
                contextHash,
                _unresolvedSignalCount);
        }

        public void LateFrameTick()
        {
            DrainHudNotificationSignalLane();

            if (!_presentationDirty && !_visualStyleDirty && !_textDirty)
                return;

            _presentationDirty = false;

            if (_visualStyleDirty)
            {
                _visualStyleDirty = false;
                ApplySeverityVisuals(_currentSeverity);
            }

            if (_isShowing && _textDirty)
            {
                _textDirty = false;
                RefreshStressCorruptionIfNeeded();
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;
        }

        private void RegisterToTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToTickManager)
                _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredToLateFrame)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            UnregisterDispatcherTicks();
            _presentationDirty = false;
            _visualStyleDirty = false;
            _textDirty = false;
        }

        private void UnregisterDispatcherTicks()
        {
            if (_registeredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredToLateFrame = false;
            }

            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _localizationStressPresentation = GlobalRegistry.LocalizationStressPresentation;
            _dataVault = GlobalRegistry.DataVault;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationStressPresentation = currentService as ILocalizationStressPresentationReadModel;
                    _lastStressCorruptionBucket = int.MinValue;
                    if (_isShowing)
                    {
                        _textDirty = true;
                        _presentationDirty = true;
                    }
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherTicks();
                    if (currentService != null)
                        RegisterToTickManager();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseQueue(previousService is IDataVault previousVault ? previousVault : _dataVault);
                    _dataVault = currentService is IDataVault currentVault ? currentVault : null;
                    _queueCount = 0;
                    EnsureQueue();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void ShowWarning(ReadOnlySpan<char> message)
        {
            Enqueue(message, NotificationSeverity.Warning);
        }

        /// <summary>
        /// Queues a warning notification from a caller-owned fixed character buffer.
        /// </summary>
        public void ShowWarning(in FixedCharBuffer messageBuffer)
        {
            Enqueue(in messageBuffer, NotificationSeverity.Warning);
        }

        public void ShowCritical(ReadOnlySpan<char> message)
        {
            Enqueue(message, NotificationSeverity.Critical);
        }

        /// <summary>
        /// Queues a critical notification from a caller-owned fixed character buffer.
        /// </summary>
        public void ShowCritical(in FixedCharBuffer messageBuffer)
        {
            Enqueue(in messageBuffer, NotificationSeverity.Critical);
        }

        public void ShowInfo(ReadOnlySpan<char> message)
        {
            Enqueue(message, NotificationSeverity.Info);
        }

        /// <summary>
        /// Queues an informational notification from a caller-owned fixed character buffer.
        /// </summary>
        public void ShowInfo(in FixedCharBuffer messageBuffer)
        {
            Enqueue(in messageBuffer, NotificationSeverity.Info);
        }

        private void Enqueue(ReadOnlySpan<char> message, NotificationSeverity severity)
        {
            if (message.IsEmpty)
                return;

            EnsureBuilt();

            uint messageHash = NotificationEvents.RegisterMessage(message);
            if (messageHash == 0u)
            {
                ReportMessageRegistrationMiss(severity);
                return;
            }

            Enqueue(messageHash, severity);
        }

        private void Enqueue(in FixedCharBuffer messageBuffer, NotificationSeverity severity)
        {
            if (messageBuffer.Length <= 0)
                return;

            EnsureBuilt();

            uint messageHash = RegisterFixedBufferMessage(in messageBuffer);
            if (messageHash == 0u)
            {
                ReportMessageRegistrationMiss(severity);
                return;
            }

            Enqueue(messageHash, severity);
        }

        private void Enqueue(uint messageHash, NotificationSeverity severity)
        {
            EnsureBuilt();
            if (messageHash == 0u)
            {
                ReportMessageRegistrationMiss(severity);
                return;
            }

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;

            if (messageHash == _currentMessageHash && severity == _currentSeverity && _timer > 0f)
            {
                _timer = displayDuration;
                return;
            }

            if (messageHash == _lastEnqueuedMessageHash &&
                severity == _lastEnqueuedSeverity &&
                now - _lastEnqueueTime < repeatSuppressWindow)
            {
                return;
            }

            _lastEnqueuedMessageHash = messageHash;
            _lastEnqueuedSeverity = severity;
            _lastEnqueueTime = now;

            if (_timer <= 0f && _queueCount == 0 && !_isShowing && _currentAlpha <= 0.01f)
            {
                ShowImmediate(messageHash, severity);
                return;
            }

            if (severity == NotificationSeverity.Critical && _currentSeverity != NotificationSeverity.Critical)
            {
                if (_currentMessageHash != 0u && _queueCount < ResolveQueueCapacity())
                {
                    if (!InsertQueueFront(new NotificationRequest
                    {
                        MessageHash = _currentMessageHash,
                        Severity = (byte)_currentSeverity
                    }))
                    {
                        ReportQueueWriteMiss(_currentSeverity, _currentMessageHash);
                    }
                }

                ShowImmediate(messageHash, severity);
                return;
            }

            if (_queueCount >= ResolveQueueCapacity())
            {
                ReportQueueDrop(severity, messageHash);
                if (severity <= NotificationSeverity.Info)
                    return;

                RemoveQueueFront();
            }

            if (!PushQueueBack(new NotificationRequest
            {
                MessageHash = messageHash,
                Severity = (byte)severity
            }))
            {
                ReportQueueWriteMiss(severity, messageHash);
            }
        }

        private void ShowImmediate(uint messageHash, NotificationSeverity severity)
        {
            _tickDormant = false;
            RegisterToTickManager();
            ApplyVisuals(messageHash, severity);
            _timer = displayDuration;
            _currentAlpha = 0f;
            _isShowing = true;
        }

        private void ApplyVisuals(uint messageHash, NotificationSeverity severity)
        {
            _currentMessageHash = messageHash;
            _currentSeverity = severity;
            _visualStyleDirty = true;
            _textDirty = true;
            _presentationDirty = true;
            _lastStressCorruptionBucket = int.MinValue;
        }

        private void ApplySeverityVisuals(NotificationSeverity severity)
        {
            if (_notifBg == null || _notifText == null)
                return;

            switch (severity)
            {
                case NotificationSeverity.Critical:
                    _notifBg.color = CriticalBg;
                    _notifText.color = CriticalText;
                    break;
                case NotificationSeverity.Warning:
                    _notifBg.color = WarningBg;
                    _notifText.color = WarningText;
                    break;
                default:
                    _notifBg.color = InfoBg;
                    _notifText.color = InfoText;
                    break;
            }
        }

        public void OnNotificationEvent(in NotificationEventPayload payload)
        {
            Enqueue(payload.MessageHash, (NotificationSeverity)payload.Severity);
        }

        /// <inheritdoc />
        public void OnInventoryEvent(in InventoryEventPayload payload)
        {
            if ((InventoryEventType)payload.EventType != InventoryEventType.InventoryFull)
                return;

            InventoryEvents.TryResolveItem(in payload, out ItemData item);
            OnInventoryFull(item);
        }

        private void OnInventoryFull(ItemData item)
        {
            string itemName = item != null ? item.itemName : null;
            if (string.IsNullOrWhiteSpace(itemName))
                itemName = FallbackInventoryItemName;

            _inventoryFullMessageBuffer.Clear();
            AppendText(ref _inventoryFullMessageBuffer, InventoryFullMessagePrefix);
            AppendUpperInvariant(ref _inventoryFullMessageBuffer, itemName);
            ShowWarning(in _inventoryFullMessageBuffer);
        }

        private void DrainHudNotificationSignalLane()
        {
            int budget = MaxHudNotificationSignalsPerLateFrame;
            while (budget-- > 0 && SignalBus<HUDNotificationSignal>.TryConsumeFrame(out HUDNotificationSignal signal))
                PushHudNotificationSignal(in signal);
        }

        private void PushHudNotificationSignal(in HUDNotificationSignal signal)
        {
            if (!TryWriteHudSignalMessage(in signal, out int length))
                return;

            Enqueue(_hudSignalDecodeCharacters.AsSpan(0, length), ResolveSignalSeverity(signal.Severity));
        }

        private bool TryWriteHudSignalMessage(in HUDNotificationSignal signal, out int length)
        {
            length = 0;
            if (signal.MessageHash == 0u)
            {
                ReportHudSignalMessageMiss(in signal);
                return false;
            }

            bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                signal.MessageHash,
                _hudSignalDecodeCharacters.AsSpan(),
                out length,
                stripRichText: true);
            if (found && length > 0)
                return true;

            ReportHudSignalMessageMiss(in signal);
            return TryWriteHudSignalFallback(signal.MessageHash, _hudSignalDecodeCharacters.AsSpan(), out length);
        }

        private static bool TryWriteHudSignalFallback(uint messageHash, Span<char> target, out int length)
        {
            length = 0;
            if (target.Length < HudSignalFallbackPrefix.Length + 8)
                return false;

            HudSignalFallbackPrefix.AsSpan().CopyTo(target);
            length = HudSignalFallbackPrefix.Length;
            for (int shift = 28; shift >= 0; shift -= 4)
                target[length++] = ToUpperHexNibble((messageHash >> shift) & 0xFu);

            return true;
        }

        private static char ToUpperHexNibble(uint value)
        {
            value &= 0xFu;
            return value < 10u
                ? (char)('0' + value)
                : (char)('A' + (value - 10u));
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            ILocalizationStressPresentationReadModel manager = _localizationStressPresentation;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            ApplyNotificationText(_currentMessageHash);
        }

        private void ApplyNotificationText(uint messageHash)
        {
            if (_notifText == null || messageHash == 0u)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                if (!TryWriteDisplayMessage(messageHash, lease.Buffer, out int length))
                    return;

                _notifText.SetCharArray(lease.Buffer, 0, length);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private bool TryWriteDisplayMessage(uint messageHash, char[] target, out int length)
        {
            length = 0;
            if (TryWriteFixedBufferMessage(messageHash, target, out length))
                return true;

            if (messageHash == 0u ||
                !NotificationEvents.TryResolveMessageSpan(messageHash, out ReadOnlySpan<char> message) ||
                message.Length <= 0)
                return false;

            return TryWriteDisplaySpan(message, target, out length);
        }

        private bool TryWriteFixedBufferMessage(uint messageHash, char[] target, out int length)
        {
            length = 0;
            if (messageHash == 0u || target == null || target.Length == 0)
                return false;

            for (int i = 0; i < _fixedBufferMessageCache.Length; i++)
            {
                FixedBufferMessageCacheEntry entry = _fixedBufferMessageCache[i];
                if (entry.IsValid == 0 || entry.MessageHash != messageHash || entry.Length <= 0)
                    continue;

                int sourceOffset = i * FixedBufferMessageCharCapacity;
                ReadOnlySpan<char> source = _fixedBufferMessageCharacters.AsSpan(sourceOffset, entry.Length);
                return TryWriteDisplaySpan(source, target, out length);
            }

            return false;
        }

        private bool TryWriteDisplaySpan(ReadOnlySpan<char> message, char[] target, out int length)
        {
            length = 0;
            if (message.Length <= 0 || target == null || target.Length == 0)
                return false;

            ILocalizationStressPresentationReadModel manager = _localizationStressPresentation;
            if (manager != null)
                return manager.TryApplyHullStressCorruptionIfNeeded(message, target, out length) && length > 0;

            length = math.min(message.Length, target.Length);
            if (length <= 0)
                return false;

            message.Slice(0, length).CopyTo(target.AsSpan());
            return true;
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }

        private static bool AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            Span<char> scratch = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                scratch[0] = c == '_' ? ' ' : ToAsciiUpperInvariant(c);
                if (!buffer.Append(scratch))
                    return false;
            }

            return true;
        }

        private static char ToAsciiUpperInvariant(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private uint RegisterFixedBufferMessage(in FixedCharBuffer messageBuffer)
        {
            ReadOnlySpan<char> source = messageBuffer.AsSpan();
            if (source.Length <= 0)
                return 0u;

            uint messageHash = unchecked((uint)LocHash.Compute(source));
            if (messageHash == 0u)
                return 0u;

            int storedLength = math.min(source.Length, FixedBufferMessageCharCapacity);
            for (int i = 0; i < _fixedBufferMessageCache.Length; i++)
            {
                if (IsFixedBufferMessageMatch(i, messageHash, source, storedLength))
                    return messageHash;
            }

            int cacheIndex = _fixedBufferMessageCacheCursor;
            _fixedBufferMessageCacheCursor = (_fixedBufferMessageCacheCursor + 1) % _fixedBufferMessageCache.Length;
            int targetOffset = cacheIndex * FixedBufferMessageCharCapacity;
            source.Slice(0, storedLength).CopyTo(_fixedBufferMessageCharacters.AsSpan(targetOffset, storedLength));
            _fixedBufferMessageCache[cacheIndex] = new FixedBufferMessageCacheEntry
            {
                MessageHash = messageHash,
                Length = storedLength,
                IsValid = 1
            };

            return messageHash;
        }

        private bool IsFixedBufferMessageMatch(int cacheIndex, uint messageHash, ReadOnlySpan<char> source, int storedLength)
        {
            if ((uint)cacheIndex >= (uint)_fixedBufferMessageCache.Length)
                return false;

            FixedBufferMessageCacheEntry entry = _fixedBufferMessageCache[cacheIndex];
            if (entry.IsValid == 0 || entry.MessageHash != messageHash || entry.Length != storedLength)
                return false;

            int sourceOffset = cacheIndex * FixedBufferMessageCharCapacity;
            ReadOnlySpan<char> cached = _fixedBufferMessageCharacters.AsSpan(sourceOffset, storedLength);
            return cached.SequenceEqual(source.Slice(0, storedLength));
        }

        private void ClearFixedBufferMessageCache()
        {
            for (int i = 0; i < _fixedBufferMessageCache.Length; i++)
                _fixedBufferMessageCache[i] = default;

            _fixedBufferMessageCacheCursor = 0;
        }

        private void ClearNotificationDiagnostics()
        {
            _messageRegistrationMissCount = 0;
            _queueDropCount = 0;
            _queueWriteMissCount = 0;
            _hudSignalMessageMissCount = 0;
            _lastMessageRegistrationMissTelemetryFrame = -1;
            _lastQueueDropTelemetryFrame = -1;
            _lastQueueWriteMissTelemetryFrame = -1;
            _lastHudSignalMessageMissTelemetryFrame = -1;
        }

        private int ResolveQueueCapacity()
        {
            int backingCapacity = _queueCapacity > 0 ? _queueCapacity : MaxNotificationQueueCapacity;
            return math.clamp(maxQueuedNotifications, 1, backingCapacity);
        }

        private void EnsureQueue()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsHudQueueHandle(in _queueHandle) &&
                vault.TryReadOnlyHandle(in _queueHandle, out NativeArray<NotificationRequest>.ReadOnly queue) &&
                queue.IsCreated &&
                queue.Length >= MaxNotificationQueueCapacity)
            {
                _queueCapacity = queue.Length;
                return;
            }

            if (IsHudQueueHandle(in _queueHandle))
                vault.ReleaseBuffer(in _queueHandle);

            _queueHandle = vault.EnsureGenerationHandle<NotificationRequest>(
                QueueBufferId,
                MaxNotificationQueueCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);

            _queueCapacity = IsHudQueueHandle(in _queueHandle) &&
                vault.TryReadOnlyHandle(in _queueHandle, out queue) &&
                queue.IsCreated
                    ? queue.Length
                    : 0;
            if (_queueCapacity <= 0)
                _queueCount = 0;
            else if (_queueCount > _queueCapacity)
                _queueCount = _queueCapacity;
        }

        private bool PushQueueBack(in NotificationRequest request)
        {
            if (!TryAcquireQueueWrite(out NativeArray<NotificationRequest> queue))
                return false;

            int capacity = ResolveQueueCapacity();
            try
            {
                if (queue.Length < capacity)
                    capacity = queue.Length;
                if (_queueCount >= capacity)
                    return false;

                queue[_queueCount] = request;
                _queueCount++;
                return true;
            }
            finally
            {
                ReleaseQueueWrite();
            }
        }

        private bool InsertQueueFront(in NotificationRequest request)
        {
            if (!TryAcquireQueueWrite(out NativeArray<NotificationRequest> queue))
                return false;

            int capacity = ResolveQueueCapacity();
            try
            {
                if (queue.Length < capacity)
                    capacity = queue.Length;
                if (capacity <= 0)
                {
                    _queueCount = 0;
                    return false;
                }

                if (_queueCount >= capacity)
                    _queueCount = capacity - 1;

                for (int i = _queueCount; i > 0; i--)
                    queue[i] = queue[i - 1];

                queue[0] = request;
                _queueCount++;
                return true;
            }
            finally
            {
                ReleaseQueueWrite();
            }
        }

        private NotificationRequest PopQueueFront()
        {
            if (!TryAcquireQueueWrite(out NativeArray<NotificationRequest> queue))
            {
                _queueCount = 0;
                return default;
            }

            try
            {
                NotificationRequest request = _queueCount > 0 ? queue[0] : default;
                RemoveQueueFrontLocked(queue);
                return request;
            }
            finally
            {
                ReleaseQueueWrite();
            }
        }

        private void RemoveQueueFront()
        {
            if (!TryAcquireQueueWrite(out NativeArray<NotificationRequest> queue))
            {
                _queueCount = 0;
                return;
            }

            try
            {
                RemoveQueueFrontLocked(queue);
            }
            finally
            {
                ReleaseQueueWrite();
            }
        }

        private void RemoveQueueFrontLocked(NativeArray<NotificationRequest> queue)
        {
            if (!queue.IsCreated || _queueCount <= 0)
                return;

            int count = math.min(_queueCount, queue.Length);
            for (int i = 1; i < count; i++)
                queue[i - 1] = queue[i];

            _queueCount--;
            if ((uint)_queueCount < (uint)queue.Length)
                queue[_queueCount] = default;
        }

        private bool TryAcquireQueueWrite(out NativeArray<NotificationRequest> queue)
        {
            queue = default;
            if (_queueWriteVault != null)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !IsHudQueueHandle(in _queueHandle))
            {
                EnsureQueue();
                vault = _dataVault;
            }

            if (vault == null ||
                !IsHudQueueHandle(in _queueHandle) ||
                !vault.TryAcquireWriteLock(in _queueHandle, VaultOwnerSystemId, out queue))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!queue.IsCreated)
                    return false;

                _queueWriteVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in _queueHandle, VaultOwnerSystemId);
            }
        }

        private void ReleaseQueueWrite()
        {
            IDataVault vault = _queueWriteVault;
            _queueWriteVault = null;
            if (vault != null && IsHudQueueHandle(in _queueHandle))
                vault.ReleaseWriteLock(in _queueHandle, VaultOwnerSystemId);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private void ReleaseQueue(IDataVault vault)
        {
            ReleaseQueueWrite();
            if (vault != null && IsHudQueueHandle(in _queueHandle))
                vault.ReleaseBuffer(in _queueHandle);

            _queueHandle = default;
            _queueWriteVault = null;
            _queueCapacity = 0;
            _queueCount = 0;
        }

        private void ReportMessageRegistrationMiss(NotificationSeverity severity)
        {
            _messageRegistrationMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastMessageRegistrationMissTelemetryFrame == frame)
                return;

            _lastMessageRegistrationMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                HudNotificationRegistrationMissWarningHash,
                HudNotificationContextHash ^ ((uint)severity << 24),
                math.max(1, _messageRegistrationMissCount));
        }

        private void ReportQueueDrop(NotificationSeverity severity, uint messageHash)
        {
            _queueDropCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastQueueDropTelemetryFrame == frame)
                return;

            _lastQueueDropTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                HudNotificationQueueDropWarningHash,
                HudNotificationContextHash ^ messageHash ^ ((uint)severity << 24),
                math.max(1, _queueDropCount));
        }

        private void ReportQueueWriteMiss(NotificationSeverity severity, uint messageHash)
        {
            _queueWriteMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastQueueWriteMissTelemetryFrame == frame)
                return;

            _lastQueueWriteMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                HudNotificationQueueWriteMissWarningHash,
                HudNotificationContextHash ^ messageHash ^ ((uint)severity << 24),
                math.max(1, _queueWriteMissCount));
        }

        private void ReportHudSignalMessageMiss(in HUDNotificationSignal signal)
        {
            _hudSignalMessageMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastHudSignalMessageMissTelemetryFrame == frame)
                return;

            _lastHudSignalMessageMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                HudNotificationSignalMissWarningHash,
                HudNotificationContextHash ^ signal.MessageHash ^ signal.ContextHash ^ ((uint)signal.Severity << 24),
                math.max(1, _hudSignalMessageMissCount));
        }

        private static bool IsHudQueueHandle(in VaultGenerationHandle<NotificationRequest> handle)
        {
            return handle.BufferID == unchecked((uint)(int)QueueBufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            EnsureQueue();

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            self.anchorMin = new Vector2(0.5f, 1f);
            self.anchorMax = new Vector2(0.5f, 1f);
            self.pivot = new Vector2(0.5f, 1f);
            self.anchoredPosition = new Vector2(0f, -110f);
            self.sizeDelta = new Vector2(420f, 36f);

            _notifRoot = self;
            if (!gameObject.TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _currentAlpha = 0f;
            _isShowing = false;

            if (!gameObject.TryGetComponent(out _notifBg))
                _notifBg = gameObject.AddComponent<Image>();
            _notifBg.color = WarningBg;
            _notifBg.raycastTarget = false;

            GameObject txtGo = new GameObject("NotifText", typeof(RectTransform));
            txtGo.TryGetComponent(out RectTransform txtR);
            txtR.SetParent(self, false);
            txtR.anchorMin = Vector2.zero;
            txtR.anchorMax = Vector2.one;
            txtR.offsetMin = new Vector2(12f, 0f);
            txtR.offsetMax = new Vector2(-12f, 0f);
            txtGo.layer = gameObject.layer;

            _notifText = txtGo.AddComponent<TextMeshProUGUI>();
            _notifText.font = font;
            _notifText.fontSize = 13f;
            _notifText.fontStyle = FontStyles.Bold;
            _notifText.alignment = TextAlignmentOptions.Center;
            _notifText.textWrappingMode = TextWrappingModes.NoWrap;
            _notifText.raycastTarget = false;
            _notifText.color = WarningText;
            _built = true;
        }

        private void ApplyPreviewSafeState()
        {
            if (TryGetComponent(out CanvasGroup cg))
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            if (TryGetComponent(out Image image))
            {
                Color c = image.color;
                c.a = 0f;
                image.color = c;
                image.raycastTarget = false;
            }
        }
    }
}
