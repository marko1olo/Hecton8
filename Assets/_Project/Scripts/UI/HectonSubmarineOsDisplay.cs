using Hecton8.Core;
using Hecton8.Gameplay;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation diegetic HUD overlay for submarine OS logs, metrics, and subsystem bit icons.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton Submarine OS Display")]
    public sealed class HectonSubmarineOsDisplay : MonoBehaviour, ILateFrameTickable, ISubmarineOsEventListener, IGlobalRegistryHotSwapListener
    {
        private const int HistoryLineCount = 16;
        private const int HistoryLineCapacity = 64;
        private const int PendingEntryCapacity = 12;
        private const int MetricBufferLength = 112;
        private const int StatusBufferLength = 48;
        private const int RenderBufferLength = (HistoryLineCount * (HistoryLineCapacity + 1)) + HistoryLineCapacity;
        private const float CharactersPerSecond = 42f;
        private const float RootWidth = 520f;
        private const float RootHeight = 372f;
        private const float IconWidth = 72f;
        private const float IconHeight = 28f;
        private const float HeatBarWidth = 142f;
        private const float HeatBarHeight = 5f;
        private const int InvalidCachedMetric = int.MinValue;
        private const byte InvalidCachedStatus = byte.MaxValue;
        private const string RootName = "HectonSubmarineOsDisplay";
        private static readonly Color s_panelColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color s_onlineColor = new Color(0.92f, 0.96f, 0.96f, 0.98f);
        private static readonly Color s_offlineColor = new Color(0.92f, 0.96f, 0.96f, 0.22f);
        private static readonly Color s_heatBarBackColor = new Color(0.10f, 0.18f, 0.18f, 1f);
        private static readonly Color s_heatBarFillColor = new Color(0.92f, 0.96f, 0.62f, 1f);
        private static readonly Color s_heatBarHotColor = new Color(1f, 0.22f, 0.12f, 1f);
        private static ReadOnlySpan<char> StatusNominal => "LVL 0 // NOMINAL".AsSpan();
        private static ReadOnlySpan<char> StatusCaution => "LVL 1 // CAUTION".AsSpan();
        private static ReadOnlySpan<char> StatusDanger => "LVL 2 // DANGER".AsSpan();
        private static ReadOnlySpan<char> StatusEvacuate => "LVL 3 // EVACUATE".AsSpan();
        private static ReadOnlySpan<char> LogPrefixOk => "[OK] ".AsSpan();
        private static ReadOnlySpan<char> LogPrefixWarn => "[WARN] ".AsSpan();
        private static ReadOnlySpan<char> LogPrefixCrit => "[CRIT] ".AsSpan();
        private static ReadOnlySpan<char> IconEngines => "ENG".AsSpan();
        private static ReadOnlySpan<char> IconLifeSupport => "AIR".AsSpan();
        private static ReadOnlySpan<char> IconLights => "LGT".AsSpan();
        private static ReadOnlySpan<char> IconSonar => "SNR".AsSpan();
        private static ReadOnlySpan<char> ActiveDrones512 => "Active Drones: 512".AsSpan();
        private static ReadOnlySpan<char> LogReactorStable => "[OK] REACTOR STABLE".AsSpan();
        private static ReadOnlySpan<char> LogLowPowerEngaged => "[WARN] LOW POWER MODE ENGAGED".AsSpan();
        private static ReadOnlySpan<char> LogLowPowerCleared => "[OK] LOW POWER MODE CLEARED".AsSpan();
        private static ReadOnlySpan<char> LogLifeSupportCritical => "[CRIT] LIFE SUPPORT CRITICAL".AsSpan();
        private static ReadOnlySpan<char> LogLifeSupportStabilized => "[OK] LIFE SUPPORT STABILIZED".AsSpan();
        private static ReadOnlySpan<char> LogHullPressureHigh => "[WARN] HULL PRESSURE HIGH".AsSpan();
        private static ReadOnlySpan<char> LogHullPressureStabilized => "[OK] HULL PRESSURE STABLE".AsSpan();
        private static ReadOnlySpan<char> LogMultiFailure => "[CRIT] MULTIPLE SYSTEM FAILURES".AsSpan();
        private static ReadOnlySpan<char> LogFatalImplosion => "[CRIT] FATAL IMPLOSION EVENT".AsSpan();
        private static ReadOnlySpan<char> LogLevelNominal => "[OK] EMERGENCY LEVEL NOMINAL".AsSpan();
        private static ReadOnlySpan<char> LogLevelCaution => "[WARN] EMERGENCY LEVEL CAUTION".AsSpan();
        private static ReadOnlySpan<char> LogLevelDanger => "[CRIT] EMERGENCY LEVEL DANGER".AsSpan();
        private static ReadOnlySpan<char> LogLevelEvacuate => "[CRIT] EMERGENCY LEVEL EVACUATE".AsSpan();
        private static ReadOnlySpan<char> LogStationKeepingArmed => "[OK] STATION KEEPING ARMED".AsSpan();
        private static ReadOnlySpan<char> LogStationKeepingReleased => "[OK] STATION KEEPING RELEASED".AsSpan();
        private static ReadOnlySpan<char> LogHostileDroneDetected => "[CRIT] HOSTILE DRONE DETECTED".AsSpan();
        private static ReadOnlySpan<char> LogEngineTelemetryMasked => "[WARN] ENGINE TELEMETRY MASKED".AsSpan();
        private static ReadOnlySpan<char> LogEngineTelemetryRestored => "[OK] ENGINE TELEMETRY RESTORED".AsSpan();
        private static ReadOnlySpan<char> LogBusPower => "BUS POWER ".AsSpan();
        private static ReadOnlySpan<char> LogOxygen => "OXYGEN ".AsSpan();
        private static ReadOnlySpan<char> LogHullPressure => "HULL PRESSURE ".AsSpan();
        private static ReadOnlySpan<char> LogKpaSuffix => "KPA".AsSpan();

        private struct PendingEntry
        {
            public HectonSubmarineOsLogCode Code;
            public byte Priority;
        }


        private readonly PendingEntry[] _pendingEntries = new PendingEntry[PendingEntryCapacity]; // COLD ALLOC: PendingEntry[12] — submarine OS log typing queue — owner: HectonSubmarineOsDisplay
        private readonly int[] _historyLineLengths = new int[HistoryLineCount]; // COLD ALLOC: int[16] — committed log line lengths — owner: HectonSubmarineOsDisplay
        private readonly char[][] _historyLineStorage = new char[HistoryLineCount][]; // COLD ALLOC: char[][16] — committed submarine OS log ring — owner: HectonSubmarineOsDisplay
        private readonly char[] _typingBuffer = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — active typed line staging buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _metricBuffer = new char[MetricBufferLength]; // COLD ALLOC: char[112] — metrics render buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _statusBuffer = new char[StatusBufferLength]; // COLD ALLOC: char[48] — status render buffer — owner: HectonSubmarineOsDisplay
        private readonly char[] _renderBuffer = new char[RenderBufferLength]; // COLD ALLOC: char[1104] — multiline log render buffer — owner: HectonSubmarineOsDisplay

        [Header("Authored UI")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _panelImage;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _metricLabel;
        [SerializeField] private TMP_Text _droneFleetLabel;
        [SerializeField] private TMP_Text _logLabel;
        [SerializeField] private RectTransform _engineHeatBarRoot;
        [SerializeField] private Image _engineHeatBarBackImage;
        [SerializeField] private RectTransform _engineHeatBarFill;
        [SerializeField] private Image _engineHeatBarImage;
        [SerializeField] private Image[] _subsystemIconImages = new Image[4]; // COLD ALLOC: Image[4] - fixed authored subsystem icon bindings - owner: HectonSubmarineOsDisplay
        [SerializeField] private TMP_Text[] _subsystemIconLabels = new TMP_Text[4]; // COLD ALLOC: TMP_Text[4] - fixed authored subsystem label bindings - owner: HectonSubmarineOsDisplay
        private int _pendingEntryCount;
        private int _pendingEntryHead;
        private int _pendingEntryTail;
        private int _historyLineWriteIndex;
        private int _historyLineCount;
        private int _typingVisibleLength;
        private int _typingSourceLength;
        private int _typingRenderBaseLength;
        private float _typingAccumulator;
        private int _renderedPowerPercent = InvalidCachedMetric;
        private int _renderedOxygenPercent = InvalidCachedMetric;
        private int _renderedCarbonDioxidePercent = InvalidCachedMetric;
        private int _renderedPressureKPa = InvalidCachedMetric;
        private int _renderedNativeCopyMegabytes = InvalidCachedMetric;
        private int _renderedSpeedTenthsKnots = InvalidCachedMetric;
        private int _renderedEngineHeatPercent = InvalidCachedMetric;
        private int _renderedSonarContactCount = InvalidCachedMetric;
        private int _renderedNearestSonarMeters = InvalidCachedMetric;

        private bool _uiBound;
        private bool _typingActive;
        private bool _registeredUpdatable;
        private bool _hotSwapListenerRegistered;
        private SubsystemStatus _renderedSubsystemStatus = (SubsystemStatus)InvalidCachedStatus;
        private SubmarineEmergencyLevel _renderedEmergencyLevel = (SubmarineEmergencyLevel)InvalidCachedStatus;
        private HectonSubmarineOsSnapshot _snapshot;

        internal static HectonSubmarineOsDisplay EnsureRuntimeInstance()
        {
            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return null;

            if (targetCanvas.gameObject.TryGetComponent(out HectonSubmarineOsDisplay display))
                return display;

            Transform authoredRoot = targetCanvas.transform.Find(RootName);
            return authoredRoot != null && authoredRoot.TryGetComponent(out display)
                ? display
                : null;
        }

        private void Awake()
        {
            for (int i = 0; i < HistoryLineCount; i++)
            {
                if (_historyLineStorage[i] == null)
                    _historyLineStorage[i] = new char[HistoryLineCapacity]; // COLD ALLOC: char[64] — committed submarine OS log line — owner: HectonSubmarineOsDisplay
            }
        }


        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            EnsureUiBuilt();
            HectonSubmarineOsEvents.Unregister(this);
            HectonSubmarineOsEvents.Register(this);
            RefreshStatusLabels();
            RefreshMetricsLabel();
            RefreshDroneFleetLabel();
            RefreshLogLabel();
            TryRegister();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            EnsureUiBuilt();
            TryRegister();
        }

        private void OnDisable()
        {
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            HectonSubmarineOsEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        /// <inheritdoc />
        public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
        {
            if (HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot))
            {
                HandleSnapshotUpdated(in snapshot);
                return;
            }

            if (HectonSubmarineOsEvents.TryBuildLogRequest(in payload, out HectonSubmarineOsLogRequest request))
                HandleLogRequested(in request);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_uiBound || _root == null)
                return;

            if (!_typingActive)
            {
                if (!TryStartNextTypedEntry())
                    return;
            }

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
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
                RefreshLogLabel();
                TryStartNextTypedEntry();
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }
        }

        private void TryRegister()
        {
            if (_registeredUpdatable || !Application.isPlaying || !_uiBound)
                return;

            _registeredUpdatable = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
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

        private void TryUnregister()
        {
            if (!_registeredUpdatable)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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
            TryRegister();
            if (!_typingActive)
                TryStartNextTypedEntry();
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

        private bool TryStartNextTypedEntry()
        {
            while (_pendingEntryCount > 0)
            {
                PendingEntry nextEntry = _pendingEntries[_pendingEntryHead];
                _pendingEntries[_pendingEntryHead] = default;
                _pendingEntryHead = (_pendingEntryHead + 1) % PendingEntryCapacity;
                _pendingEntryCount--;
                if (_pendingEntryCount == 0)
                    _pendingEntryTail = _pendingEntryHead;

                int safeLength = BuildLogLine(nextEntry.Code, _typingBuffer);
                if (safeLength <= 0)
                    continue;

                _typingActive = true;
                _typingAccumulator = 0f;
                _typingVisibleLength = 0;
                _typingSourceLength = safeLength;
                RefreshLogLabel();
                return true;
            }

            return false;
        }

        private void CommitTypedLine()
        {
            int writeIndex = _historyLineWriteIndex;
            char[] historyLine = _historyLineStorage[writeIndex];
            int safeLength = math.min(_typingSourceLength, HistoryLineCapacity);
            CopyCharsUnsafe(_typingBuffer, 0, historyLine, 0, safeLength);

            _historyLineLengths[writeIndex] = safeLength;
            _historyLineWriteIndex = (_historyLineWriteIndex + 1) % HistoryLineCount;
            if (_historyLineCount < HistoryLineCount)
                _historyLineCount++;
        }

        private void RefreshStatusLabels()
        {
            if (_statusLabel == null)
                return;

            SubmarineEmergencyLevel emergencyLevel = _snapshot.EmergencyLevel;
            SubsystemStatus subsystemStatus = _snapshot.SubsystemStatus;
            if (emergencyLevel == _renderedEmergencyLevel && subsystemStatus == _renderedSubsystemStatus)
                return;

            if (emergencyLevel != _renderedEmergencyLevel)
            {
                ReadOnlySpan<char> source = ResolveStatusChars(emergencyLevel);
                int safeLength = CopySpan(_statusBuffer, 0, source);

                _statusLabel.SetCharArray(_statusBuffer, 0, safeLength);
                _renderedEmergencyLevel = emergencyLevel;
            }

            if (subsystemStatus != _renderedSubsystemStatus)
            {
                RefreshSubsystemIcons(subsystemStatus);
                _renderedSubsystemStatus = subsystemStatus;
            }
        }

        private void RefreshMetricsLabel()
        {
            if (_metricLabel == null)
                return;

            int powerPercent = ToPercent(_snapshot.PowerNormalized);
            int oxygenPercent = ToPercent(_snapshot.OxygenNormalized);
            int carbonDioxidePercent = ToPercent(_snapshot.CarbonDioxideNormalized);
            int pressureKPa = (int)math.round(_snapshot.MaxPressureKPa);
            int speedTenthsKnots = (int)math.round(math.max(0f, _snapshot.SpeedKnots) * 10f);
            int engineHeatPercent = ToPercent(_snapshot.EngineHeat01);
            int sonarContactCount = math.max(0, _snapshot.SonarContactCount);
            int nearestSonarMeters = math.max(0, _snapshot.NearestSonarContactMeters);
            long nativeCopyMegabytesRaw = GlobalTelemetryBus.NativeCopyMegabyteCount;
            int nativeCopyMegabytes = nativeCopyMegabytesRaw > int.MaxValue ? int.MaxValue : (int)nativeCopyMegabytesRaw;
            if (powerPercent == _renderedPowerPercent &&
                oxygenPercent == _renderedOxygenPercent &&
                carbonDioxidePercent == _renderedCarbonDioxidePercent &&
                pressureKPa == _renderedPressureKPa &&
                speedTenthsKnots == _renderedSpeedTenthsKnots &&
                engineHeatPercent == _renderedEngineHeatPercent &&
                sonarContactCount == _renderedSonarContactCount &&
                nearestSonarMeters == _renderedNearestSonarMeters &&
                nativeCopyMegabytes == _renderedNativeCopyMegabytes)
            {
                return;
            }

            int cursor = 0;
            cursor = AppendLiteral(_metricBuffer, cursor, "PWR ");
            cursor = AppendPercentValue(_metricBuffer, cursor, powerPercent);
            cursor = AppendLiteral(_metricBuffer, cursor, "  O2 ");
            cursor = AppendPercentValue(_metricBuffer, cursor, oxygenPercent);
            cursor = AppendLiteral(_metricBuffer, cursor, "  CO2 ");
            cursor = AppendPercentValue(_metricBuffer, cursor, carbonDioxidePercent);
            cursor = AppendLiteral(_metricBuffer, cursor, "  P ");
            cursor = AppendInt(_metricBuffer, cursor, pressureKPa);
            cursor = AppendLiteral(_metricBuffer, cursor, "kPa");
            cursor = AppendLiteral(_metricBuffer, cursor, "  SPD ");
            cursor = AppendFixedTenths(_metricBuffer, cursor, speedTenthsKnots);
            cursor = AppendLiteral(_metricBuffer, cursor, "kt");
            cursor = AppendLiteral(_metricBuffer, cursor, "  SNR ");
            cursor = AppendInt(_metricBuffer, cursor, sonarContactCount);
            cursor = AppendLiteral(_metricBuffer, cursor, "/");
            if (nearestSonarMeters > 0)
                cursor = AppendInt(_metricBuffer, cursor, nearestSonarMeters);
            else
                cursor = AppendLiteral(_metricBuffer, cursor, "--");
            cursor = AppendLiteral(_metricBuffer, cursor, "m");
            cursor = AppendLiteral(_metricBuffer, cursor, "  MEM ");
            cursor = AppendInt(_metricBuffer, cursor, nativeCopyMegabytes);
            cursor = AppendLiteral(_metricBuffer, cursor, "MB");
            _metricLabel.SetCharArray(_metricBuffer, 0, math.max(0, cursor));
            _renderedPowerPercent = powerPercent;
            _renderedOxygenPercent = oxygenPercent;
            _renderedCarbonDioxidePercent = carbonDioxidePercent;
            _renderedPressureKPa = pressureKPa;
            _renderedSpeedTenthsKnots = speedTenthsKnots;
            _renderedEngineHeatPercent = engineHeatPercent;
            _renderedSonarContactCount = sonarContactCount;
            _renderedNearestSonarMeters = nearestSonarMeters;
            _renderedNativeCopyMegabytes = nativeCopyMegabytes;
            RefreshEngineHeatBar(engineHeatPercent);
        }

        private void RefreshEngineHeatBar(int engineHeatPercent)
        {
            if (_engineHeatBarFill == null || _engineHeatBarImage == null)
                return;

            float fill01 = math.saturate(engineHeatPercent * 0.01f);
            _engineHeatBarFill.sizeDelta = new Vector2(math.round(HeatBarWidth * fill01), HeatBarHeight);
            _engineHeatBarImage.color = engineHeatPercent >= 75 ? s_heatBarHotColor : s_heatBarFillColor;
        }

        private void RefreshDroneFleetLabel()
        {
            if (_droneFleetLabel == null)
                return;

            ReadOnlySpan<char> label = ActiveDrones512;
            int safeLength = CopySpan(_statusBuffer, 0, label);
            _droneFleetLabel.SetCharArray(_statusBuffer, 0, safeLength);
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
                cursor = AppendRange(_renderBuffer, cursor, _historyLineStorage[historyIndex], 0, lineLength);
                if (cursor < _renderBuffer.Length)
                    _renderBuffer[cursor++] = '\n';
            }

            if (_typingActive)
            {
                _typingRenderBaseLength = cursor;
                cursor = AppendRange(_renderBuffer, cursor, _typingBuffer, 0, _typingSourceLength);
            }

            int safeLength = math.clamp(cursor, 0, _renderBuffer.Length);
            _logLabel.SetCharArray(_renderBuffer, 0, safeLength);
            ApplyTypingVisibleCharacters(safeLength);
        }

        private void ApplyTypingVisibleCharacters()
        {
            ApplyTypingVisibleCharacters(_renderBuffer.Length);
        }

        private void ApplyTypingVisibleCharacters(int renderedLength)
        {
            if (_logLabel == null)
                return;

            if (!_typingActive)
            {
                if (_logLabel.maxVisibleCharacters != int.MaxValue)
                    _logLabel.maxVisibleCharacters = int.MaxValue;
                return;
            }

            int safeRenderedLength = renderedLength > 0 ? renderedLength : _typingRenderBaseLength + _typingSourceLength;
            int visibleCharacters = math.clamp(_typingRenderBaseLength + _typingVisibleLength, 0, safeRenderedLength);
            if (_logLabel.maxVisibleCharacters != visibleCharacters)
                _logLabel.maxVisibleCharacters = visibleCharacters;
        }

        private void RefreshSubsystemIcons(SubsystemStatus subsystemStatus)
        {
            if (!HasBoundIconSlots())
                return;

            ApplyIconState(0, (subsystemStatus & SubsystemStatus.Engines) != 0);
            ApplyIconState(1, (subsystemStatus & SubsystemStatus.LifeSupport) != 0);
            ApplyIconState(2, (subsystemStatus & SubsystemStatus.Lights) != 0);
            ApplyIconState(3, (subsystemStatus & SubsystemStatus.Sonar) != 0);
        }

        private void ApplyIconState(int index, bool active)
        {
            if ((uint)index >= (uint)_subsystemIconImages.Length)
                return;

            Color color = active ? s_onlineColor : s_offlineColor;
            Image image = _subsystemIconImages[index];
            if (image != null)
                image.color = color;

            if ((uint)index >= (uint)_subsystemIconLabels.Length)
                return;

            TMP_Text label = _subsystemIconLabels[index];
            if (label != null)
                label.color = color;
        }

        private bool EnsureUiBuilt()
        {
            if (_uiBound && HasCompleteUiBindings())
                return true;

            _uiBound = false;
            _root = ResolveAuthoredRoot(_root);
            if (_root == null)
                return false;

            if (_panelImage == null && !_root.TryGetComponent(out _panelImage))
                return false;

            _panelImage.color = s_panelColor;
            _panelImage.raycastTarget = false;

            _statusLabel = ResolveChildText(_root, _statusLabel, "Status");
            _metricLabel = ResolveChildText(_root, _metricLabel, "Metrics");
            _droneFleetLabel = ResolveChildText(_root, _droneFleetLabel, "DroneFleet");
            _logLabel = ResolveChildText(_root, _logLabel, "Log");
            if (_statusLabel == null || _metricLabel == null || _droneFleetLabel == null || _logLabel == null)
                return false;

            _engineHeatBarRoot = ResolveChildRect(_root, _engineHeatBarRoot, "EngineHeatBar");
            if (_engineHeatBarRoot == null)
                return false;

            if (_engineHeatBarBackImage == null && !_engineHeatBarRoot.TryGetComponent(out _engineHeatBarBackImage))
                return false;

            _engineHeatBarBackImage.color = s_heatBarBackColor;
            _engineHeatBarBackImage.raycastTarget = false;

            _engineHeatBarFill = ResolveChildRect(_engineHeatBarRoot, _engineHeatBarFill, "EngineHeatFill");
            if (_engineHeatBarFill == null)
                return false;

            if (_engineHeatBarImage == null && !_engineHeatBarFill.TryGetComponent(out _engineHeatBarImage))
                return false;

            _engineHeatBarImage.color = s_heatBarFillColor;
            _engineHeatBarImage.raycastTarget = false;

            if (!TryBindSubsystemIcons())
                return false;

            ConfigureText(_statusLabel, 19f, TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
            ConfigureText(_metricLabel, 13f, TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
            ConfigureText(_droneFleetLabel, 16f, TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
            ConfigureText(_logLabel, 15f, TextAlignmentOptions.TopLeft, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);

            SetIconLabel(0, IconEngines);
            SetIconLabel(1, IconLifeSupport);
            SetIconLabel(2, IconLights);
            SetIconLabel(3, IconSonar);

            _uiBound = true;
            InvalidateSnapshotRenderCaches();
            RefreshStatusLabels();
            RefreshMetricsLabel();
            RefreshDroneFleetLabel();
            RefreshLogLabel();
            return true;
        }









        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static ReadOnlySpan<char> ResolveStatusChars(SubmarineEmergencyLevel emergencyLevel)
        {
            switch (emergencyLevel)
            {
                case SubmarineEmergencyLevel.Caution:
                    return StatusCaution;
                case SubmarineEmergencyLevel.Danger:
                    return StatusDanger;
                case SubmarineEmergencyLevel.Evacuate:
                    return StatusEvacuate;
                default:
                    return StatusNominal;
            }
        }

        private static bool TryResolveLogChars(HectonSubmarineOsLogCode code, out ReadOnlySpan<char> chars, out int length)
        {
            chars = ResolveLogChars(code);
            length = chars.Length;
            return length > 0;
        }

        private void InvalidateSnapshotRenderCaches()
        {
            _renderedEmergencyLevel = (SubmarineEmergencyLevel)InvalidCachedStatus;
            _renderedSubsystemStatus = (SubsystemStatus)InvalidCachedStatus;
            _renderedPowerPercent = InvalidCachedMetric;
            _renderedOxygenPercent = InvalidCachedMetric;
            _renderedCarbonDioxidePercent = InvalidCachedMetric;
            _renderedPressureKPa = InvalidCachedMetric;
            _renderedSpeedTenthsKnots = InvalidCachedMetric;
            _renderedEngineHeatPercent = InvalidCachedMetric;
            _renderedSonarContactCount = InvalidCachedMetric;
            _renderedNearestSonarMeters = InvalidCachedMetric;
            _renderedNativeCopyMegabytes = InvalidCachedMetric;
        }

        private int BuildLogLine(HectonSubmarineOsLogCode code, char[] destination)
        {
            int cursor = 0;
            switch (code)
            {
                case HectonSubmarineOsLogCode.LowPowerModeEngaged:
                    cursor = AppendSpan(destination, cursor, LogPrefixWarn);
                    cursor = AppendSpan(destination, cursor, LogBusPower);
                    return AppendPercent(destination, cursor, _snapshot.PowerNormalized);

                case HectonSubmarineOsLogCode.LifeSupportCritical:
                    cursor = AppendSpan(destination, cursor, LogPrefixCrit);
                    cursor = AppendSpan(destination, cursor, LogOxygen);
                    return AppendPercent(destination, cursor, _snapshot.OxygenNormalized);

                case HectonSubmarineOsLogCode.HullPressureHigh:
                    cursor = AppendSpan(destination, cursor, LogPrefixWarn);
                    cursor = AppendSpan(destination, cursor, LogHullPressure);
                    cursor = AppendInt(destination, cursor, (int)math.round(_snapshot.MaxPressureKPa));
                    return AppendSpan(destination, cursor, LogKpaSuffix);

                default:
                    if (!TryResolveLogChars(code, out ReadOnlySpan<char> chars, out int length))
                        return 0;

                    return AppendSpan(destination, cursor, chars.Slice(0, length));
            }
        }

        private static ReadOnlySpan<char> ResolveLogChars(HectonSubmarineOsLogCode code)
        {
            switch (code)
            {
                case HectonSubmarineOsLogCode.ReactorStable:
                    return LogReactorStable;
                case HectonSubmarineOsLogCode.LowPowerModeEngaged:
                    return LogLowPowerEngaged;
                case HectonSubmarineOsLogCode.LowPowerModeCleared:
                    return LogLowPowerCleared;
                case HectonSubmarineOsLogCode.LifeSupportCritical:
                    return LogLifeSupportCritical;
                case HectonSubmarineOsLogCode.LifeSupportStabilized:
                    return LogLifeSupportStabilized;
                case HectonSubmarineOsLogCode.HullPressureHigh:
                    return LogHullPressureHigh;
                case HectonSubmarineOsLogCode.HullPressureStabilized:
                    return LogHullPressureStabilized;
                case HectonSubmarineOsLogCode.MultiSystemFailure:
                    return LogMultiFailure;
                case HectonSubmarineOsLogCode.FatalImplosion:
                    return LogFatalImplosion;
                case HectonSubmarineOsLogCode.EmergencyLevelNominal:
                    return LogLevelNominal;
                case HectonSubmarineOsLogCode.EmergencyLevelCaution:
                    return LogLevelCaution;
                case HectonSubmarineOsLogCode.EmergencyLevelDanger:
                    return LogLevelDanger;
                case HectonSubmarineOsLogCode.EmergencyLevelEvacuate:
                    return LogLevelEvacuate;
                case HectonSubmarineOsLogCode.StationKeepingArmed:
                    return LogStationKeepingArmed;
                case HectonSubmarineOsLogCode.StationKeepingReleased:
                    return LogStationKeepingReleased;
                case HectonSubmarineOsLogCode.HostileDroneDetected:
                    return LogHostileDroneDetected;
                case HectonSubmarineOsLogCode.EngineTelemetryMasked:
                    return LogEngineTelemetryMasked;
                case HectonSubmarineOsLogCode.EngineTelemetryRestored:
                    return LogEngineTelemetryRestored;
                default:
                    return ReadOnlySpan<char>.Empty;
            }
        }

        private static int AppendLiteral(char[] destination, int cursor, string literal)
        {
            if (destination == null || string.IsNullOrEmpty(literal))
                return cursor;

            int safeCursor = math.clamp(cursor, 0, destination.Length);
            int remaining = destination.Length - safeCursor;
            int safeLength = math.min(remaining, literal.Length);
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = literal[i];

            return safeCursor + safeLength;
        }

        private static int AppendSpan(char[] destination, int cursor, ReadOnlySpan<char> source)
        {
            if (destination == null || source.Length <= 0)
                return cursor;

            int safeCursor = math.clamp(cursor, 0, destination.Length);
            int safeLength = math.min(source.Length, destination.Length - safeCursor);
            for (int i = 0; i < safeLength; i++)
                destination[safeCursor + i] = source[i];

            return safeCursor + safeLength;
        }

        private static int CopySpan(char[] destination, int cursor, ReadOnlySpan<char> source)
        {
            return AppendSpan(destination, cursor, source);
        }

        private static int AppendPercent(char[] destination, int cursor, float normalizedValue)
        {
            return AppendPercentValue(destination, cursor, ToPercent(normalizedValue));
        }

        private static int AppendPercentValue(char[] destination, int cursor, int percent)
        {
            int safeCursor = math.clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = destination.AsSpan(safeCursor, destination.Length - safeCursor);
            if (!percent.TryFormat(writableSpan, out int written))
                return safeCursor;

            safeCursor += written;
            if (safeCursor < destination.Length)
                destination[safeCursor++] = '%';

            return safeCursor;
        }

        private static int AppendInt(char[] destination, int cursor, int value)
        {
            int safeCursor = math.clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = destination.AsSpan(safeCursor, destination.Length - safeCursor);
            if (!value.TryFormat(writableSpan, out int written))
                return safeCursor;

            return safeCursor + written;
        }

        private static int AppendFixedTenths(char[] destination, int cursor, int valueTenths)
        {
            int whole = valueTenths / 10;
            int tenths = math.abs(valueTenths % 10);
            int safeCursor = AppendInt(destination, cursor, whole);
            if (safeCursor < destination.Length)
                destination[safeCursor++] = '.';
            if (safeCursor < destination.Length)
                destination[safeCursor++] = (char)('0' + tenths);

            return safeCursor;
        }

        private static int AppendRange(char[] destination, int cursor, char[] source, int sourceStart, int length)
        {
            if (destination == null || source == null || length <= 0)
                return cursor;

            int safeCursor = math.clamp(cursor, 0, destination.Length);
            int safeStart = math.clamp(sourceStart, 0, source.Length);
            int safeLength = math.clamp(length, 0, math.min(source.Length - safeStart, destination.Length - safeCursor));
            CopyCharsUnsafe(source, safeStart, destination, safeCursor, safeLength);

            return safeCursor + safeLength;
        }

        private static int ToPercent(float normalizedValue)
        {
            return (int)math.round(math.saturate(normalizedValue) * 100f);
        }

        private static unsafe void CopyCharsUnsafe(char[] source, int sourceStart, char[] destination, int destinationStart, int length)
        {
            if (source == null || destination == null || length <= 0)
                return;

            fixed (char* sourcePtr = source)
            fixed (char* destinationPtr = destination)
            {
                long destinationBytes = (long)(destination.Length - destinationStart) * sizeof(char);
                long sourceBytes = (long)length * sizeof(char);
                UnsafeMemoryCopyGuard.SafeCopy(
                    destinationPtr + destinationStart,
                    destinationBytes,
                    sourcePtr + sourceStart,
                    sourceBytes);
            }
        }


        private RectTransform ResolveAuthoredRoot(RectTransform configuredRoot)
        {
            if (configuredRoot != null)
                return configuredRoot;

            RectTransform localRoot = transform as RectTransform;
            if (localRoot != null)
                return localRoot;

            Canvas targetCanvas = ResolveTargetCanvas();
            Transform child = targetCanvas != null ? targetCanvas.transform.Find(RootName) : transform.Find(RootName);
            return child as RectTransform;
        }

        private static RectTransform ResolveChildRect(Transform root, RectTransform configuredRect, string childName)
        {
            if (configuredRect != null)
                return configuredRect;

            Transform child = root != null ? root.Find(childName) : null;
            return child as RectTransform;
        }

        private static TMP_Text ResolveChildText(Transform root, TMP_Text configuredText, string childName)
        {
            if (configuredText != null)
                return configuredText;

            Transform child = root != null ? root.Find(childName) : null;
            if (child == null)
                return null;

            return child.TryGetComponent(out TMP_Text text) ? text : null;
        }

        private bool TryBindSubsystemIcons()
        {
            if (_subsystemIconImages == null || _subsystemIconLabels == null)
                return false;

            if (_subsystemIconImages.Length < 4 || _subsystemIconLabels.Length < 4)
                return false;

            if (!HasBoundIconSlots())
            {
                int slotIndex = 0;
                int childCount = _root.childCount;
                for (int i = 0; i < childCount && slotIndex < 4; i++)
                {
                    Transform child = _root.GetChild(i);
                    if (child == null || child.name != "SubsystemIcon")
                        continue;

                    if (_subsystemIconImages[slotIndex] == null && child.TryGetComponent(out Image image))
                        _subsystemIconImages[slotIndex] = image;

                    if (_subsystemIconLabels[slotIndex] == null)
                        _subsystemIconLabels[slotIndex] = ResolveChildText(child, null, "Label");

                    slotIndex++;
                }
            }

            if (!HasBoundIconSlots())
                return false;

            for (int i = 0; i < 4; i++)
            {
                _subsystemIconImages[i].raycastTarget = false;
                ConfigureText(_subsystemIconLabels[i], 14f, TextAlignmentOptions.Center, TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
            }

            return true;
        }

        private bool HasCompleteUiBindings()
        {
            return _root != null &&
                _panelImage != null &&
                _statusLabel != null &&
                _metricLabel != null &&
                _droneFleetLabel != null &&
                _logLabel != null &&
                _engineHeatBarRoot != null &&
                _engineHeatBarBackImage != null &&
                _engineHeatBarFill != null &&
                _engineHeatBarImage != null &&
                HasBoundIconSlots();
        }

        private bool HasBoundIconSlots()
        {
            if (_subsystemIconImages == null ||
                _subsystemIconLabels == null ||
                _subsystemIconImages.Length < 4 ||
                _subsystemIconLabels.Length < 4)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (_subsystemIconImages[i] == null || _subsystemIconLabels[i] == null)
                    return false;
            }

            return true;
        }

        private void SetIconLabel(int index, ReadOnlySpan<char> labelChars)
        {
            if (!HasBoundIconSlots() || (uint)index >= 4u)
                return;

            TMP_Text label = _subsystemIconLabels[index];
            int safeLength = CopySpan(_statusBuffer, 0, labelChars);
            label.SetCharArray(_statusBuffer, 0, safeLength);
        }

        private static void ConfigureText(TMP_Text text, float fontSize, TextAlignmentOptions alignment, TextWrappingModes wrappingMode, TextOverflowModes overflowMode)
        {
            if (text == null)
                return;

            if (text.font == null)
                text.font = TMP_Settings.defaultFontAsset;

            text.fontSize = fontSize;
            text.color = s_onlineColor;
            text.raycastTarget = false;
            text.alignment = alignment;
            text.textWrappingMode = wrappingMode;
            text.overflowMode = overflowMode;
            TMP_TextRegistry.EnsureRegistered(text);
        }
}
}
