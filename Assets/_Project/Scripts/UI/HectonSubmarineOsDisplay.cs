using Hecton8.Core;
using Hecton8.Gameplay;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation diegetic HUD overlay for submarine OS logs, metrics, and subsystem bit icons.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton Submarine OS Display")]
    public sealed class HectonSubmarineOsDisplay : MonoBehaviour, IUpdatable
    {
        private const int HistoryLineCount = 6;
        private const int HistoryLineCapacity = 64;
        private const int PendingEntryCapacity = 12;
        private const int MetricBufferLength = 64;
        private const int StatusBufferLength = 48;
        private const int RenderBufferLength = (HistoryLineCount * (HistoryLineCapacity + 1)) + 2;
        private const float CharactersPerSecond = 42f;
        private const float RootWidth = 520f;
        private const float RootHeight = 188f;
        private const float IconWidth = 72f;
        private const float IconHeight = 28f;
        private const string RootName = "HectonSubmarineOsDisplay";
        private static readonly Color s_panelColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color s_onlineColor = new Color(0.92f, 0.96f, 0.96f, 0.98f);
        private static readonly Color s_offlineColor = new Color(0.92f, 0.96f, 0.96f, 0.22f);
        private static readonly char[] s_emptyChars = System.Array.Empty<char>();
        private static readonly char[] s_statusNominal = "LVL 0 // NOMINAL".ToCharArray();
        private static readonly char[] s_statusCaution = "LVL 1 // CAUTION".ToCharArray();
        private static readonly char[] s_statusDanger = "LVL 2 // DANGER".ToCharArray();
        private static readonly char[] s_statusEvacuate = "LVL 3 // EVACUATE".ToCharArray();
        private static readonly char[] s_iconEngines = "ENG".ToCharArray();
        private static readonly char[] s_iconLifeSupport = "AIR".ToCharArray();
        private static readonly char[] s_iconLights = "LGT".ToCharArray();
        private static readonly char[] s_iconSonar = "SNR".ToCharArray();
        private static readonly char[] s_logReactorStable = "[OK] REACTOR STABLE".ToCharArray();
        private static readonly char[] s_logLowPowerEngaged = "[WARN] LOW POWER MODE ENGAGED".ToCharArray();
        private static readonly char[] s_logLowPowerCleared = "[OK] LOW POWER MODE CLEARED".ToCharArray();
        private static readonly char[] s_logLifeSupportCritical = "[CRIT] LIFE SUPPORT CRITICAL".ToCharArray();
        private static readonly char[] s_logLifeSupportStabilized = "[OK] LIFE SUPPORT STABILIZED".ToCharArray();
        private static readonly char[] s_logHullPressureHigh = "[WARN] HULL PRESSURE HIGH".ToCharArray();
        private static readonly char[] s_logHullPressureStabilized = "[OK] HULL PRESSURE STABLE".ToCharArray();
        private static readonly char[] s_logMultiFailure = "[CRIT] MULTIPLE SYSTEM FAILURES".ToCharArray();
        private static readonly char[] s_logFatalImplosion = "[CRIT] FATAL IMPLOSION EVENT".ToCharArray();
        private static readonly char[] s_logLevelNominal = "[OK] EMERGENCY LEVEL NOMINAL".ToCharArray();
        private static readonly char[] s_logLevelCaution = "[WARN] EMERGENCY LEVEL CAUTION".ToCharArray();
        private static readonly char[] s_logLevelDanger = "[CRIT] EMERGENCY LEVEL DANGER".ToCharArray();
        private static readonly char[] s_logLevelEvacuate = "[CRIT] EMERGENCY LEVEL EVACUATE".ToCharArray();
        private static readonly char[] s_logStationKeepingArmed = "[OK] STATION KEEPING ARMED".ToCharArray();
        private static readonly char[] s_logStationKeepingReleased = "[OK] STATION KEEPING RELEASED".ToCharArray();

        private struct PendingEntry
        {
            public HectonSubmarineOsLogCode Code;
            public byte Priority;
        }

        private static HectonSubmarineOsDisplay s_instance;

        private readonly PendingEntry[] _pendingEntries = new PendingEntry[PendingEntryCapacity]; // COLD ALLOC: PendingEntry[12] — submarine OS log typing queue — owner: HectonSubmarineOsDisplay
        private readonly int[] _historyLineLengths = new int[HistoryLineCount]; // COLD ALLOC: int[6] — committed log line lengths — owner: HectonSubmarineOsDisplay
        private readonly char[] _historyLineStorage = new char[HistoryLineCount * HistoryLineCapacity]; // COLD ALLOC: char[384] — committed submarine OS log storage — owner: HectonSubmarineOsDisplay
        private readonly char[] _typingBuffer = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — active typed line staging buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _metricBuffer = new char[MetricBufferLength]; // COLD ALLOC: char[64] — metrics render buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _statusBuffer = new char[StatusBufferLength]; // COLD ALLOC: char[48] — status render buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _renderBuffer = new char[RenderBufferLength]; // COLD ALLOC: char[392] — multiline log render buffer — owner: HectonSubmarineOsDisplay

        private RectTransform _root;
        private TMP_Text _statusLabel;
        private TMP_Text _metricLabel;
        private TMP_Text _logLabel;
        private Image[] _subsystemIconImages;
        private TMP_Text[] _subsystemIconLabels;
        private int _pendingEntryCount;
        private int _historyLineWriteIndex;
        private int _historyLineCount;
        private int _typingVisibleLength;
        private int _typingSourceLength;
        private float _typingAccumulator;
        private HectonSubmarineOsLogCode _typingCode;
        private bool _typingActive;
        private bool _registeredUpdatable;
        private HectonSubmarineOsSnapshot _snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_instance = null;
        }

        internal static HectonSubmarineOsDisplay EnsureRuntimeInstance()
        {
            if (s_instance != null)
                return s_instance;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return null;

            if (!targetCanvas.gameObject.TryGetComponent(out HectonSubmarineOsDisplay display))
                display = targetCanvas.gameObject.AddComponent<HectonSubmarineOsDisplay>(); // COLD ALLOC: HectonSubmarineOsDisplay[1] — HUD-owned submarine OS overlay — owner: HectonSubmarineOsDisplay

            s_instance = display;
            return display;
        }

        private void OnEnable()
        {
            s_instance = this;
            EnsureUiBuilt();
            HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSnapshotUpdated;
            HectonSubmarineOsEvents.OnSnapshotUpdated += HandleSnapshotUpdated;
            HectonSubmarineOsEvents.OnLogRequested -= HandleLogRequested;
            HectonSubmarineOsEvents.OnLogRequested += HandleLogRequested;
            TryRegister();
            RefreshStatusLabels();
            RefreshMetricsLabel();
            RefreshLogLabel();
        }

        private void OnDisable()
        {
            HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSnapshotUpdated;
            HectonSubmarineOsEvents.OnLogRequested -= HandleLogRequested;
            TryUnregister();
            if (ReferenceEquals(s_instance, this))
                s_instance = null;
        }

        private void OnDestroy()
        {
            HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSnapshotUpdated;
            HectonSubmarineOsEvents.OnLogRequested -= HandleLogRequested;
            TryUnregister();
            if (ReferenceEquals(s_instance, this))
                s_instance = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            EnsureUiBuilt();
            if (!_typingActive)
            {
                TryStartNextTypedEntry();
                return;
            }

            _typingAccumulator += deltaTime * CharactersPerSecond;
            int nextVisibleLength = Mathf.Min(_typingSourceLength, Mathf.FloorToInt(_typingAccumulator));
            if (nextVisibleLength == _typingVisibleLength)
                return;

            _typingVisibleLength = nextVisibleLength;
            RefreshLogLabel();
            if (_typingVisibleLength >= _typingSourceLength)
            {
                CommitTypedLine();
                _typingActive = false;
                _typingAccumulator = 0f;
                _typingVisibleLength = 0;
                _typingSourceLength = 0;
                TryStartNextTypedEntry();
            }
        }

        private void TryRegister()
        {
            if (_registeredUpdatable)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredUpdatable = true;
        }

        private void TryUnregister()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredUpdatable = false;
        }

        private void HandleSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            _snapshot = snapshot;
            RefreshStatusLabels();
            RefreshMetricsLabel();
        }

        private void HandleLogRequested(in HectonSubmarineOsLogRequest request)
        {
            InsertPendingEntry(request.Code, request.Priority);
            if (!_typingActive)
                TryStartNextTypedEntry();
        }

        private void InsertPendingEntry(HectonSubmarineOsLogCode code, byte priority)
        {
            if (_pendingEntryCount >= PendingEntryCapacity)
            {
                _pendingEntryCount = PendingEntryCapacity - 1;
                for (int i = 0; i < _pendingEntryCount; i++)
                    _pendingEntries[i] = _pendingEntries[i + 1];
            }

            int insertIndex = _pendingEntryCount;
            while (insertIndex > 0 && _pendingEntries[insertIndex - 1].Priority < priority)
            {
                _pendingEntries[insertIndex] = _pendingEntries[insertIndex - 1];
                insertIndex--;
            }

            _pendingEntries[insertIndex] = new PendingEntry
            {
                Code = code,
                Priority = priority
            };
            _pendingEntryCount++;
        }

        private void TryStartNextTypedEntry()
        {
            if (_pendingEntryCount <= 0)
                return;

            PendingEntry nextEntry = _pendingEntries[0];
            for (int i = 1; i < _pendingEntryCount; i++)
                _pendingEntries[i - 1] = _pendingEntries[i];

            _pendingEntryCount--;
            if (!TryResolveLogChars(nextEntry.Code, out char[] source, out int sourceLength))
                return;

            int safeLength = Mathf.Min(sourceLength, _typingBuffer.Length);
            for (int i = 0; i < safeLength; i++)
                _typingBuffer[i] = source[i];

            _typingCode = nextEntry.Code;
            _typingActive = safeLength > 0;
            _typingAccumulator = 0f;
            _typingVisibleLength = 0;
            _typingSourceLength = safeLength;
            RefreshLogLabel();
        }

        private void CommitTypedLine()
        {
            int writeIndex = _historyLineWriteIndex;
            int baseOffset = writeIndex * HistoryLineCapacity;
            int safeLength = Mathf.Min(_typingSourceLength, HistoryLineCapacity);
            for (int i = 0; i < safeLength; i++)
                _historyLineStorage[baseOffset + i] = _typingBuffer[i];

            _historyLineLengths[writeIndex] = safeLength;
            _historyLineWriteIndex = (_historyLineWriteIndex + 1) % HistoryLineCount;
            if (_historyLineCount < HistoryLineCount)
                _historyLineCount++;
            RefreshLogLabel();
        }

        private void RefreshStatusLabels()
        {
            if (_statusLabel == null)
                return;

            char[] source = ResolveStatusChars(_snapshot.EmergencyLevel);
            int safeLength = Mathf.Min(source.Length, _statusBuffer.Length);
            for (int i = 0; i < safeLength; i++)
                _statusBuffer[i] = source[i];

            _statusLabel.SetCharArray(_statusBuffer, 0, safeLength);
            RefreshSubsystemIcons();
        }

        private void RefreshMetricsLabel()
        {
            if (_metricLabel == null)
                return;

            int cursor = 0;
            cursor = AppendLiteral(_metricBuffer, cursor, "PWR ");
            cursor = AppendPercent(_metricBuffer, cursor, _snapshot.PowerNormalized);
            cursor = AppendLiteral(_metricBuffer, cursor, "  O2 ");
            cursor = AppendPercent(_metricBuffer, cursor, _snapshot.OxygenNormalized);
            cursor = AppendLiteral(_metricBuffer, cursor, "  P ");
            cursor = AppendInt(_metricBuffer, cursor, Mathf.RoundToInt(_snapshot.MaxPressureKPa));
            cursor = AppendLiteral(_metricBuffer, cursor, "kPa");
            _metricLabel.SetCharArray(_metricBuffer, 0, Mathf.Max(0, cursor));
        }

        private void RefreshLogLabel()
        {
            if (_logLabel == null)
                return;

            int cursor = 0;
            int oldestIndex = _historyLineCount >= HistoryLineCount
                ? _historyLineWriteIndex
                : 0;

            for (int i = 0; i < _historyLineCount; i++)
            {
                int historyIndex = (_historyLineCount >= HistoryLineCount
                    ? (oldestIndex + i) % HistoryLineCount
                    : i);
                int lineLength = _historyLineLengths[historyIndex];
                int historyOffset = historyIndex * HistoryLineCapacity;
                cursor = AppendRange(_renderBuffer, cursor, _historyLineStorage, historyOffset, lineLength);
                if (cursor < _renderBuffer.Length)
                    _renderBuffer[cursor++] = '\n';
            }

            if (_typingActive)
                cursor = AppendRange(_renderBuffer, cursor, _typingBuffer, 0, _typingVisibleLength);

            int safeLength = Mathf.Clamp(cursor, 0, _renderBuffer.Length);
            _logLabel.SetCharArray(_renderBuffer, 0, safeLength);
        }

        private void RefreshSubsystemIcons()
        {
            if (_subsystemIconImages == null || _subsystemIconLabels == null)
                return;

            ApplyIconState(0, (_snapshot.SubsystemStatus & SubsystemStatus.Engines) != 0);
            ApplyIconState(1, (_snapshot.SubsystemStatus & SubsystemStatus.LifeSupport) != 0);
            ApplyIconState(2, (_snapshot.SubsystemStatus & SubsystemStatus.Lights) != 0);
            ApplyIconState(3, (_snapshot.SubsystemStatus & SubsystemStatus.Sonar) != 0);
        }

        private void ApplyIconState(int index, bool active)
        {
            if ((uint)index >= (uint)_subsystemIconImages.Length)
                return;

            Color color = active ? s_onlineColor : s_offlineColor;
            _subsystemIconImages[index].color = color;
            if (_subsystemIconLabels[index] != null)
                _subsystemIconLabels[index].color = color;
        }

        private void EnsureUiBuilt()
        {
            if (_root != null)
                return;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] — submarine OS overlay root — owner: HectonSubmarineOsDisplay
            rootObject.transform.SetParent(targetCanvas.transform, false);
            _root = rootObject.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = new Vector2(34f, -128f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            Image panelImage = rootObject.GetComponent<Image>();
            panelImage.color = s_panelColor;
            panelImage.raycastTarget = false;

            _statusLabel = CreateText("Status", _root, new Vector2(14f, -12f), new Vector2(280f, 24f), 19f);
            _metricLabel = CreateText("Metrics", _root, new Vector2(14f, -38f), new Vector2(320f, 20f), 16f);
            _logLabel = CreateText("Log", _root, new Vector2(14f, -70f), new Vector2(356f, 108f), 15f);
            _logLabel.alignment = TextAlignmentOptions.TopLeft;
            _logLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _logLabel.overflowMode = TextOverflowModes.Overflow;
            _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _metricLabel.textWrappingMode = TextWrappingModes.NoWrap;

            _subsystemIconImages = new Image[4]; // COLD ALLOC: Image[4] — subsystem monochrome icon image refs — owner: HectonSubmarineOsDisplay
            _subsystemIconLabels = new TMP_Text[4]; // COLD ALLOC: TMP_Text[4] — subsystem icon labels — owner: HectonSubmarineOsDisplay
            CreateIconSlot(0, s_iconEngines, new Vector2(-156f, -16f));
            CreateIconSlot(1, s_iconLifeSupport, new Vector2(-78f, -16f));
            CreateIconSlot(2, s_iconLights, new Vector2(0f, -16f));
            CreateIconSlot(3, s_iconSonar, new Vector2(78f, -16f));

            RefreshStatusLabels();
            RefreshMetricsLabel();
            RefreshLogLabel();
        }

        private void CreateIconSlot(int index, char[] labelChars, Vector2 anchoredPosition)
        {
            GameObject iconObject = new GameObject("SubsystemIcon", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] — subsystem icon root — owner: HectonSubmarineOsDisplay
            iconObject.transform.SetParent(_root, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(1f, 1f);
            iconRect.anchoredPosition = anchoredPosition;
            iconRect.sizeDelta = new Vector2(IconWidth, IconHeight);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.color = s_offlineColor;
            iconImage.raycastTarget = false;
            _subsystemIconImages[index] = iconImage;

            TMP_Text label = CreateText("Label", iconRect, new Vector2(0f, 0f), new Vector2(IconWidth, IconHeight), 14f);
            label.alignment = TextAlignmentOptions.Center;
            label.SetCharArray(labelChars, 0, labelChars.Length);
            _subsystemIconLabels[index] = label;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(HectonTextNode)); // COLD ALLOC: GameObject[1] — runtime TMP owner for submarine OS display — owner: HectonSubmarineOsDisplay
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = s_onlineColor;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            TMP_TextRegistry.EnsureRegistered(text);
            return text;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null
                ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>()
                : null;
        }

        private static char[] ResolveStatusChars(SubmarineEmergencyLevel emergencyLevel)
        {
            switch (emergencyLevel)
            {
                case SubmarineEmergencyLevel.Caution:
                    return s_statusCaution;
                case SubmarineEmergencyLevel.Danger:
                    return s_statusDanger;
                case SubmarineEmergencyLevel.Evacuate:
                    return s_statusEvacuate;
                default:
                    return s_statusNominal;
            }
        }

        private static bool TryResolveLogChars(HectonSubmarineOsLogCode code, out char[] chars, out int length)
        {
            chars = ResolveLogChars(code);
            length = chars != null ? chars.Length : 0;
            return chars != null && length > 0;
        }

        private static char[] ResolveLogChars(HectonSubmarineOsLogCode code)
        {
            switch (code)
            {
                case HectonSubmarineOsLogCode.LowPowerModeEngaged:
                    return s_logLowPowerEngaged;
                case HectonSubmarineOsLogCode.LowPowerModeCleared:
                    return s_logLowPowerCleared;
                case HectonSubmarineOsLogCode.LifeSupportCritical:
                    return s_logLifeSupportCritical;
                case HectonSubmarineOsLogCode.LifeSupportStabilized:
                    return s_logLifeSupportStabilized;
                case HectonSubmarineOsLogCode.HullPressureHigh:
                    return s_logHullPressureHigh;
                case HectonSubmarineOsLogCode.HullPressureStabilized:
                    return s_logHullPressureStabilized;
                case HectonSubmarineOsLogCode.MultiSystemFailure:
                    return s_logMultiFailure;
                case HectonSubmarineOsLogCode.FatalImplosion:
                    return s_logFatalImplosion;
                case HectonSubmarineOsLogCode.EmergencyLevelNominal:
                    return s_logLevelNominal;
                case HectonSubmarineOsLogCode.EmergencyLevelCaution:
                    return s_logLevelCaution;
                case HectonSubmarineOsLogCode.EmergencyLevelDanger:
                    return s_logLevelDanger;
                case HectonSubmarineOsLogCode.EmergencyLevelEvacuate:
                    return s_logLevelEvacuate;
                case HectonSubmarineOsLogCode.StationKeepingArmed:
                    return s_logStationKeepingArmed;
                case HectonSubmarineOsLogCode.StationKeepingReleased:
                    return s_logStationKeepingReleased;
                default:
                    return s_logReactorStable;
            }
        }

        private static int AppendLiteral(char[] destination, int cursor, string literal)
        {
            if (destination == null || string.IsNullOrEmpty(literal))
                return cursor;

            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            int remaining = destination.Length - safeCursor;
            int safeLength = Mathf.Min(remaining, literal.Length);
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = literal[i];

            return safeCursor + safeLength;
        }

        private static int AppendPercent(char[] destination, int cursor, float normalizedValue)
        {
            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            int percent = Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f);
            Span<char> writableSpan = new Span<char>(destination, safeCursor, destination.Length - safeCursor);
            if (!percent.TryFormat(writableSpan, out int written))
                return safeCursor;

            safeCursor += written;
            if (safeCursor < destination.Length)
                destination[safeCursor++] = '%';

            return safeCursor;
        }

        private static int AppendInt(char[] destination, int cursor, int value)
        {
            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = new Span<char>(destination, safeCursor, destination.Length - safeCursor);
            if (!value.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }

        private static int AppendRange(char[] destination, int cursor, char[] source, int sourceStart, int length)
        {
            if (destination == null || source == null || length <= 0)
                return cursor;

            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            int safeStart = Mathf.Clamp(sourceStart, 0, source.Length);
            int safeLength = Mathf.Clamp(length, 0, Mathf.Min(source.Length - safeStart, destination.Length - safeCursor));
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = source[safeStart + i];

            return safeCursor + safeLength;
        }
    }
}
