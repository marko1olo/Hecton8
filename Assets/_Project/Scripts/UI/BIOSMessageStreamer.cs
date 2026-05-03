using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation submarine BIOS log streamer backed by a 16-line circular char buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Submarine/BIOS Message Streamer")]
    public sealed class BIOSMessageStreamer : MonoBehaviour, IUpdatable, ISubmarineOsEventListener
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
        private int _typingSourceLength;
        private int _typingVisibleLength;
        private float _typingAccumulator;
        private bool _typingActive;
        private bool _registeredUpdatable;

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
        public void Tick(float deltaTime)
        {
            if (!_typingActive)
            {
                TryStartNextEntry();
                return;
            }

            _typingAccumulator += deltaTime * CharactersPerSecond;
            int nextVisibleLength = Mathf.Min(_typingSourceLength, Mathf.FloorToInt(_typingAccumulator));
            if (nextVisibleLength == _typingVisibleLength)
                return;

            _typingVisibleLength = nextVisibleLength;
            RefreshTerminal();
            if (_typingVisibleLength >= _typingSourceLength)
            {
                CommitTypedLine();
                _typingActive = false;
                _typingAccumulator = 0f;
                _typingVisibleLength = 0;
                _typingSourceLength = 0;
                TryStartNextEntry();
            }
        }

        /// <summary>
        /// Allows an external HUD owner to bind the BIOS TMP target at runtime.
        /// </summary>
        public void BindTerminal(TMP_Text label)
        {
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
            _pendingEntries = new PendingEntry[PendingEntryCapacity]; // COLD ALLOC: PendingEntry[12] — BIOS pending priority queue — owner: BIOSMessageStreamer
            _typingBuffer = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — BIOS active typing buffer — owner: BIOSMessageStreamer
            _renderBuffer = new char[(HistoryLineCount * (HistoryLineCapacity + 1)) + HistoryLineCapacity]; // COLD ALLOC: char[1104] — BIOS flattened TMP payload — owner: BIOSMessageStreamer
        }

        private void OnEnable()
        {
            HectonSubmarineOsEvents.Register(this);
            TryRegister();
            RefreshTerminal();
        }

        private void OnDisable()
        {
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
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
        }

        private void HandleLogRequested(in HectonSubmarineOsLogRequest request)
        {
            InsertPendingEntry(request.Code, request.Priority);
            if (!_typingActive)
                TryStartNextEntry();
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

        private void TryStartNextEntry()
        {
            if (_pendingEntryCount <= 0)
                return;

            PendingEntry nextEntry = _pendingEntries[0];
            for (int i = 1; i < _pendingEntryCount; i++)
                _pendingEntries[i - 1] = _pendingEntries[i];

            _pendingEntryCount--;
            Array.Clear(_typingBuffer, 0, _typingBuffer.Length);
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
                    cursor = AppendInt(destination, cursor, Mathf.RoundToInt(snapshot.MaxPressureKPa));
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
            Array.Clear(line, 0, line.Length);
            int safeLength = Mathf.Min(_typingSourceLength, line.Length);
            for (int i = 0; i < safeLength; i++)
                line[i] = _typingBuffer[i];

            _historyLineLengths[writeIndex] = safeLength;
            _historyLineWriteIndex = (_historyLineWriteIndex + 1) % HistoryLineCount;
            if (_historyLineCount < HistoryLineCount)
                _historyLineCount++;
            RefreshTerminal();
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
                cursor = AppendRange(_renderBuffer, cursor, _typingBuffer, 0, _typingVisibleLength);

            int safeLength = Mathf.Clamp(cursor, 0, _renderBuffer.Length);
            terminalLabel.SetCharArray(_renderBuffer, 0, safeLength);
        }

        private static int AppendRange(char[] destination, int cursor, char[] source)
        {
            return AppendRange(destination, cursor, source, 0, source != null ? source.Length : 0);
        }

        private static int AppendRange(char[] destination, int cursor, char[] source, int sourceOffset, int sourceLength)
        {
            if (destination == null || source == null || sourceLength <= 0)
                return cursor;

            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            int safeSourceOffset = Mathf.Clamp(sourceOffset, 0, source.Length);
            int remainingSource = Mathf.Max(0, source.Length - safeSourceOffset);
            int safeLength = Mathf.Min(Mathf.Min(remainingSource, sourceLength), destination.Length - safeCursor);
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = source[safeSourceOffset + i];

            return safeCursor + safeLength;
        }

        private static int AppendChar(char[] destination, int cursor, char value)
        {
            if (destination == null || destination.Length == 0)
                return cursor;

            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            if (safeCursor >= destination.Length)
                return safeCursor;

            destination[safeCursor] = value;
            return safeCursor + 1;
        }

        private static int AppendPercent(char[] destination, int cursor, float normalizedValue)
        {
            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            int percent = Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f);
            Span<char> writableSpan = new Span<char>(destination, safeCursor, destination.Length - safeCursor);
            if (!percent.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }

        private static int AppendInt(char[] destination, int cursor, int value)
        {
            int safeCursor = Mathf.Clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = new Span<char>(destination, safeCursor, destination.Length - safeCursor);
            if (!value.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }
    }
}
