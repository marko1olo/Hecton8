using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation submarine BIOS log streamer backed by a 16-line circular char buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Submarine/BIOS Message Streamer")]
    public sealed class BIOSMessageStreamer : MonoBehaviour, ILateFrameTickable, ISubmarineOsEventListener, IGlobalRegistryHotSwapListener
    {
        private const int HistoryLineCount = 16;
        private const int HistoryLineCapacity = 64;
        private const int PendingEntryCapacity = 12;
        private const float CharactersPerSecond = 96f;
        private static readonly char[] s_okPrefix = "[OK] ".ToCharArray();
        private static readonly char[] s_warnPrefix = "[WARN] ".ToCharArray();
        private static readonly char[] s_failPrefix = "[FAIL] ".ToCharArray();
        private static readonly char[] s_reactorStable = "REACTOR STABLE".ToCharArray();
        private static readonly char[] s_lowBusPower = "LOW BUS POWER ".ToCharArray();
        private static readonly char[] s_powerBusStable = "POWER BUS STABLE".ToCharArray();
        private static readonly char[] s_oxygen = "OXYGEN ".ToCharArray();
        private static readonly char[] s_lifeSupportStable = "LIFE SUPPORT STABLE".ToCharArray();
        private static readonly char[] s_hullPressure = "HULL PRESS ".ToCharArray();
        private static readonly char[] s_hullPressureNormal = "HULL PRESS NORMAL".ToCharArray();
        private static readonly char[] s_kpa = "KPA".ToCharArray();
        private static readonly char[] s_multiSystemFailure = "MULTI SYSTEM FAILURE".ToCharArray();
        private static readonly char[] s_fatalHullImplosion = "FATAL HULL IMPLOSION".ToCharArray();
        private static readonly char[] s_emergencyNominal = "EMERGENCY LEVEL NOMINAL".ToCharArray();
        private static readonly char[] s_emergencyCaution = "EMERGENCY LEVEL CAUTION".ToCharArray();
        private static readonly char[] s_emergencyDanger = "EMERGENCY LEVEL DANGER".ToCharArray();
        private static readonly char[] s_emergencyEvacuate = "EMERGENCY LEVEL EVACUATE".ToCharArray();
        private static readonly char[] s_stationKeepingArmed = "STATION KEEPING ARMED".ToCharArray();
        private static readonly char[] s_stationKeepingReleased = "STATION KEEPING RELEASED".ToCharArray();
        private static readonly char[] s_hostileDroneDetected = "HOSTILE DRONE DETECTED".ToCharArray();
        private struct PendingEntry
        {
            public HectonSubmarineOsLogCode Code;
            public byte Priority;
        }

        [Header("Terminal")]
        [Tooltip("Optional TMP label used for the diegetic BIOS terminal text stream.")]
        [SerializeField] private TMP_Text terminalLabel;

        private char[][] _historyLines;
        private int[] _historyLineLengths;
        private PendingEntry[] _pendingEntries;
        private char[] _typingBuffer;
        private char[] _renderBuffer;
        private HectonSubmarineOsSnapshot _snapshot;
        private int _historyLineWriteIndex;
        private int _historyLineCount;
        private int _pendingEntryCount;
        private int _pendingEntryHead;
        private int _pendingEntryTail;
        private int _typingSourceLength;
        private int _typingVisibleLength;
        private int _typingRenderBaseLength;
        private float _typingAccumulator;
        private bool _typingActive;
        private bool _registeredUpdatable;
        private bool _hotSwapListenerRegistered;

        /// <summary>
        /// Ensures one streamer exists on the supplied host.
        /// </summary>
        public static void EnsureRuntimeInstalled(GameObject host)
        {
            if (host == null)
                return;

            if (!host.TryGetComponent(out BIOSMessageStreamer _))
                host.AddComponent<BIOSMessageStreamer>(); // COLD ALLOC: BIOSMessageStreamer[1] — submarine BIOS terminal log owner — owner: BIOSMessageStreamer
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (!_typingActive)
            {
                TryStartNextEntry();
                RefreshTickRegistration();
                return;
            }

            _typingAccumulator += deltaTime * CharactersPerSecond;
            int nextVisibleLength = math.min(_typingSourceLength, (int)math.floor(_typingAccumulator));
            if (nextVisibleLength == _typingVisibleLength)
                return;

            _typingVisibleLength = nextVisibleLength;
            ApplyTypingVisibleCharacters();
            if (_typingVisibleLength >= _typingSourceLength)
            {
                CommitTypedLine();
                _typingActive = false;
                _typingAccumulator = 0f;
                _typingVisibleLength = 0;
                _typingSourceLength = 0;
                _typingRenderBaseLength = 0;
                RefreshTerminal();
                TryStartNextEntry();
                RefreshTickRegistration();
            }
        }

        /// <summary>
        /// Allows an external HUD owner to bind the BIOS TMP target at runtime.
        /// </summary>
        public void BindTerminal(TMP_Text label)
        {
            if (ReferenceEquals(terminalLabel, label))
            {
                RefreshTerminal();
                return;
            }

            ResetTerminalVisibility();
            terminalLabel = label;
            RefreshTerminal();
        }

        private void Awake()
        {
            if (terminalLabel == null)
                TryGetComponent(out terminalLabel);

            _historyLines = new char[HistoryLineCount][]; // COLD ALLOC: char[][16] — BIOS line ring top-level slots — owner: BIOSMessageStreamer
            for (int i = 0; i < HistoryLineCount; i++)
                _historyLines[i] = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — BIOS line char storage — owner: BIOSMessageStreamer

            _historyLineLengths = new int[HistoryLineCount]; // COLD ALLOC: int[16] — BIOS line length ring — owner: BIOSMessageStreamer
            _pendingEntries = new PendingEntry[PendingEntryCapacity]; // COLD ALLOC: PendingEntry[12] — BIOS pending FIFO ring — owner: BIOSMessageStreamer
            _typingBuffer = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — BIOS active typing buffer — owner: BIOSMessageStreamer
            _renderBuffer = new char[(HistoryLineCount * (HistoryLineCapacity + 1)) + HistoryLineCapacity]; // COLD ALLOC: char[1104] — BIOS flattened TMP payload — owner: BIOSMessageStreamer
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            HectonSubmarineOsEvents.Register(this);
            RefreshTickRegistration();
            RefreshTerminal();
        }

        private void OnDisable()
        {
            ResetTerminalVisibility();
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            ResetTerminalVisibility();
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void TryRegister()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void RefreshTickRegistration()
        {
            if (_typingActive || _pendingEntryCount > 0)
            {
                TryRegister();
                return;
            }

            TryUnregister();
        }

        private void TryUnregister()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredUpdatable = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (currentService == null)
            {
                _registeredUpdatable = false;
                return;
            }

            if (isActiveAndEnabled)
            {
                TryUnregister();
                RefreshTickRegistration();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void HandleSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        private void HandleLogRequested(in HectonSubmarineOsLogRequest request)
        {
            InsertPendingEntry(request.Code, request.Priority);
            if (!_typingActive)
                TryStartNextEntry();

            RefreshTickRegistration();
        }

        public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
        {
            switch ((SubmarineOsEventType)payload.EventType)
            {
                case SubmarineOsEventType.SnapshotUpdated:
                    if (HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot))
                        HandleSnapshotUpdated(in snapshot);
                    return;

                case SubmarineOsEventType.LogRequested:
                    if (HectonSubmarineOsEvents.TryBuildLogRequest(in payload, out HectonSubmarineOsLogRequest request))
                        HandleLogRequested(in request);
                    return;
            }
        }

        private void InsertPendingEntry(HectonSubmarineOsLogCode code, byte priority)
        {
            if (_pendingEntryCount >= PendingEntryCapacity)
            {
                _pendingEntries[_pendingEntryHead] = default;
                _pendingEntryHead = (_pendingEntryHead + 1) % PendingEntryCapacity;
                _pendingEntryCount--;
            }

            _pendingEntries[_pendingEntryTail] = new PendingEntry
            {
                Code = code,
                Priority = priority
            };
            _pendingEntryTail = (_pendingEntryTail + 1) % PendingEntryCapacity;
            _pendingEntryCount++;
        }

        private void TryStartNextEntry()
        {
            if (_pendingEntryCount <= 0)
                return;

            PendingEntry nextEntry = _pendingEntries[_pendingEntryHead];
            _pendingEntries[_pendingEntryHead] = default;
            _pendingEntryHead = (_pendingEntryHead + 1) % PendingEntryCapacity;
            _pendingEntryCount--;
            if (_pendingEntryCount == 0)
                _pendingEntryTail = _pendingEntryHead;

            _typingSourceLength = BuildMessage(nextEntry.Code, _typingBuffer, _snapshot);
            if (_typingSourceLength <= 0)
                return;

            _typingActive = true;
            _typingAccumulator = 0f;
            _typingVisibleLength = 0;
            RefreshTerminal();
        }

        private int BuildMessage(HectonSubmarineOsLogCode code, char[] destination, HectonSubmarineOsSnapshot snapshot)
        {
            int cursor = 0;
            switch (code)
            {
                case HectonSubmarineOsLogCode.LowPowerModeEngaged:
                    cursor = AppendRange(destination, cursor, s_warnPrefix);
                    cursor = AppendRange(destination, cursor, s_lowBusPower);
                    cursor = AppendPercent(destination, cursor, snapshot.PowerNormalized);
                    cursor = AppendChar(destination, cursor, '%');
                    return cursor;

                case HectonSubmarineOsLogCode.LowPowerModeCleared:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_powerBusStable);

                case HectonSubmarineOsLogCode.LifeSupportCritical:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    cursor = AppendRange(destination, cursor, s_oxygen);
                    cursor = AppendPercent(destination, cursor, snapshot.OxygenNormalized);
                    cursor = AppendChar(destination, cursor, '%');
                    return cursor;

                case HectonSubmarineOsLogCode.LifeSupportStabilized:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_lifeSupportStable);

                case HectonSubmarineOsLogCode.HullPressureHigh:
                    cursor = AppendRange(destination, cursor, s_warnPrefix);
                    cursor = AppendRange(destination, cursor, s_hullPressure);
                    cursor = AppendInt(destination, cursor, (int)math.round(snapshot.MaxPressureKPa));
                    cursor = AppendRange(destination, cursor, s_kpa);
                    return cursor;

                case HectonSubmarineOsLogCode.HullPressureStabilized:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_hullPressureNormal);

                case HectonSubmarineOsLogCode.MultiSystemFailure:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    return AppendRange(destination, cursor, s_multiSystemFailure);

                case HectonSubmarineOsLogCode.FatalImplosion:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    return AppendRange(destination, cursor, s_fatalHullImplosion);

                case HectonSubmarineOsLogCode.EmergencyLevelCaution:
                    cursor = AppendRange(destination, cursor, s_warnPrefix);
                    return AppendRange(destination, cursor, s_emergencyCaution);

                case HectonSubmarineOsLogCode.EmergencyLevelDanger:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    return AppendRange(destination, cursor, s_emergencyDanger);

                case HectonSubmarineOsLogCode.EmergencyLevelEvacuate:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    return AppendRange(destination, cursor, s_emergencyEvacuate);

                case HectonSubmarineOsLogCode.StationKeepingArmed:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_stationKeepingArmed);

                case HectonSubmarineOsLogCode.StationKeepingReleased:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_stationKeepingReleased);

                case HectonSubmarineOsLogCode.HostileDroneDetected:
                    cursor = AppendRange(destination, cursor, s_failPrefix);
                    return AppendRange(destination, cursor, s_hostileDroneDetected);

                case HectonSubmarineOsLogCode.EmergencyLevelNominal:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_emergencyNominal);

                default:
                    cursor = AppendRange(destination, cursor, s_okPrefix);
                    return AppendRange(destination, cursor, s_reactorStable);
            }
        }

        private void CommitTypedLine()
        {
            int writeIndex = _historyLineWriteIndex;
            char[] line = _historyLines[writeIndex];
            int safeLength = math.min(_typingSourceLength, line.Length);
            for (int i = 0; i < safeLength; i++)
                line[i] = _typingBuffer[i];

            _historyLineLengths[writeIndex] = safeLength;
            _historyLineWriteIndex = (_historyLineWriteIndex + 1) % HistoryLineCount;
            if (_historyLineCount < HistoryLineCount)
                _historyLineCount++;
        }

        private void RefreshTerminal()
        {
            if (terminalLabel == null)
                return;

            int cursor = 0;
            int oldestIndex = _historyLineCount >= HistoryLineCount
                ? _historyLineWriteIndex
                : 0;

            for (int i = 0; i < _historyLineCount; i++)
            {
                int historyIndex = _historyLineCount >= HistoryLineCount
                    ? (oldestIndex + i) % HistoryLineCount
                    : i;
                int lineLength = _historyLineLengths[historyIndex];
                cursor = AppendRange(_renderBuffer, cursor, _historyLines[historyIndex], 0, lineLength);
                if (cursor < _renderBuffer.Length)
                    _renderBuffer[cursor++] = '\n';
            }

            if (_typingActive)
            {
                _typingRenderBaseLength = cursor;
                cursor = AppendRange(_renderBuffer, cursor, _typingBuffer, 0, _typingSourceLength);
            }

            int safeLength = math.clamp(cursor, 0, _renderBuffer.Length);
            terminalLabel.SetCharArray(_renderBuffer, 0, safeLength);
            ApplyTypingVisibleCharacters(safeLength);
        }

        private void ApplyTypingVisibleCharacters()
        {
            ApplyTypingVisibleCharacters(_renderBuffer != null ? _renderBuffer.Length : 0);
        }

        private void ApplyTypingVisibleCharacters(int renderedLength)
        {
            if (terminalLabel == null)
                return;

            if (!_typingActive)
            {
                if (terminalLabel.maxVisibleCharacters != int.MaxValue)
                    terminalLabel.maxVisibleCharacters = int.MaxValue;
                return;
            }

            int safeRenderedLength = renderedLength > 0 ? renderedLength : _typingRenderBaseLength + _typingSourceLength;
            int visibleCharacters = math.clamp(_typingRenderBaseLength + _typingVisibleLength, 0, safeRenderedLength);
            if (terminalLabel.maxVisibleCharacters != visibleCharacters)
                terminalLabel.maxVisibleCharacters = visibleCharacters;
        }

        private void ResetTerminalVisibility()
        {
            if (terminalLabel != null && terminalLabel.maxVisibleCharacters != int.MaxValue)
                terminalLabel.maxVisibleCharacters = int.MaxValue;
        }

        private static int AppendRange(char[] destination, int cursor, char[] source)
        {
            return AppendRange(destination, cursor, source, 0, source != null ? source.Length : 0);
        }

        private static int AppendRange(char[] destination, int cursor, char[] source, int sourceOffset, int sourceLength)
        {
            if (destination == null || source == null || sourceLength <= 0)
                return cursor;

            int safeCursor = math.clamp(cursor, 0, destination.Length);
            int safeSourceOffset = math.clamp(sourceOffset, 0, source.Length);
            int remainingSource = math.max(0, source.Length - safeSourceOffset);
            int safeLength = math.min(math.min(remainingSource, sourceLength), destination.Length - safeCursor);
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = source[safeSourceOffset + i];

            return safeCursor + safeLength;
        }

        private static int AppendChar(char[] destination, int cursor, char value)
        {
            if (destination == null || destination.Length == 0)
                return cursor;

            int safeCursor = math.clamp(cursor, 0, destination.Length);
            if (safeCursor >= destination.Length)
                return safeCursor;

            destination[safeCursor] = value;
            return safeCursor + 1;
        }

        private static int AppendPercent(char[] destination, int cursor, float normalizedValue)
        {
            int safeCursor = math.clamp(cursor, 0, destination.Length);
            int percent = (int)math.round(math.saturate(normalizedValue) * 100f);
            Span<char> writableSpan = destination.AsSpan(safeCursor, destination.Length - safeCursor);
            if (!percent.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }

        private static int AppendInt(char[] destination, int cursor, int value)
        {
            int safeCursor = math.clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = destination.AsSpan(safeCursor, destination.Length - safeCursor);
            if (!value.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }
    }
}
