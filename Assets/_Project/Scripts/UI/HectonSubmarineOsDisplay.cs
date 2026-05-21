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
        private static readonly char[] s_emptyChars = System.Array.Empty<char>();
        private static readonly char[] s_statusNominal = "LVL 0 // NOMINAL".ToCharArray();
        private static readonly char[] s_statusCaution = "LVL 1 // CAUTION".ToCharArray();
        private static readonly char[] s_statusDanger = "LVL 2 // DANGER".ToCharArray();
        private static readonly char[] s_statusEvacuate = "LVL 3 // EVACUATE".ToCharArray();
        private static readonly char[] s_logPrefixOk = "[OK] ".ToCharArray();
        private static readonly char[] s_logPrefixWarn = "[WARN] ".ToCharArray();
        private static readonly char[] s_logPrefixCrit = "[CRIT] ".ToCharArray();
        private static readonly char[] s_iconEngines = "ENG".ToCharArray();
        private static readonly char[] s_iconLifeSupport = "AIR".ToCharArray();
        private static readonly char[] s_iconLights = "LGT".ToCharArray();
        private static readonly char[] s_iconSonar = "SNR".ToCharArray();
        private static readonly char[] s_activeDrones512 = "Active Drones: 512".ToCharArray();
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
        private static readonly char[] s_logHostileDroneDetected = "[CRIT] HOSTILE DRONE DETECTED".ToCharArray();
        private static readonly char[] s_logBusPower = "BUS POWER ".ToCharArray();
        private static readonly char[] s_logOxygen = "OXYGEN ".ToCharArray();
        private static readonly char[] s_logHullPressure = "HULL PRESSURE ".ToCharArray();
        private static readonly char[] s_logKpaSuffix = "KPA".ToCharArray();

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

        private RectTransform _root;
        private TMP_Text _statusLabel;
        private TMP_Text _metricLabel;
        private TMP_Text _droneFleetLabel;
        private TMP_Text _logLabel;
        private RectTransform _engineHeatBarFill;
        private Image _engineHeatBarImage;
        private Image[] _subsystemIconImages;
        private TMP_Text[] _subsystemIconLabels;
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

            if (!targetCanvas.gameObject.TryGetComponent(out HectonSubmarineOsDisplay display))
                display = targetCanvas.gameObject.AddComponent<HectonSubmarineOsDisplay>(); // COLD ALLOC: HectonSubmarineOsDisplay[1] — HUD-owned submarine OS overlay — owner: HectonSubmarineOsDisplay

            return display;
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
            EnsureUiBuilt(allowCreate: true);
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
            EnsureUiBuilt(allowCreate: true);
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
            if (!EnsureUiBuilt(allowCreate: false))
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
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        private void TryRegister()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
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

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
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
                char[] source = ResolveStatusChars(emergencyLevel);
                int safeLength = math.min(source.Length, _statusBuffer.Length);
                for (int i = 0; i < safeLength; i++)
                    _statusBuffer[i] = source[i];

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

            _droneFleetLabel.SetCharArray(s_activeDrones512, 0, s_activeDrones512.Length);
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
            if (_subsystemIconImages == null || _subsystemIconLabels == null)
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
            _subsystemIconImages[index].color = color;
            if (_subsystemIconLabels[index] != null)
                _subsystemIconLabels[index].color = color;
        }

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_root != null)
                return true;

            if (!allowCreate)
                return false;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return false;

            GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] — submarine OS overlay root — owner: HectonSubmarineOsDisplay
            rootObject.transform.SetParent(targetCanvas.transform, false);
            rootObject.TryGetComponent(out _root);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = new Vector2(34f, -128f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            rootObject.TryGetComponent(out Image panelImage);
            panelImage.color = s_panelColor;
            panelImage.raycastTarget = false;

            _statusLabel = CreateText("Status", _root, new Vector2(14f, -12f), new Vector2(280f, 24f), 19f);
            _metricLabel = CreateText("Metrics", _root, new Vector2(14f, -38f), new Vector2(492f, 20f), 13f);
            CreateEngineHeatBar(_root);
            _droneFleetLabel = CreateText("DroneFleet", _root, new Vector2(14f, -60f), new Vector2(320f, 20f), 16f);
            _logLabel = CreateText("Log", _root, new Vector2(14f, -92f), new Vector2(356f, 264f), 15f);
            _logLabel.alignment = TextAlignmentOptions.TopLeft;
            _logLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _logLabel.overflowMode = TextOverflowModes.Overflow;
            _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _metricLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _droneFleetLabel.textWrappingMode = TextWrappingModes.NoWrap;

            _subsystemIconImages = new Image[4]; // COLD ALLOC: Image[4] — subsystem monochrome icon image refs — owner: HectonSubmarineOsDisplay
            _subsystemIconLabels = new TMP_Text[4]; // COLD ALLOC: TMP_Text[4] — subsystem icon labels — owner: HectonSubmarineOsDisplay
            CreateIconSlot(0, s_iconEngines, new Vector2(-156f, -16f));
            CreateIconSlot(1, s_iconLifeSupport, new Vector2(-78f, -16f));
            CreateIconSlot(2, s_iconLights, new Vector2(0f, -16f));
            CreateIconSlot(3, s_iconSonar, new Vector2(78f, -16f));

            InvalidateSnapshotRenderCaches();
            RefreshStatusLabels();
            RefreshMetricsLabel();
            RefreshDroneFleetLabel();
            RefreshLogLabel();
            return true;
        }

        private void CreateIconSlot(int index, char[] labelChars, Vector2 anchoredPosition)
        {
            GameObject iconObject = new GameObject("SubsystemIcon", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] — subsystem icon root — owner: HectonSubmarineOsDisplay
            iconObject.transform.SetParent(_root, false);
            iconObject.TryGetComponent(out RectTransform iconRect);
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(1f, 1f);
            iconRect.anchoredPosition = anchoredPosition;
            iconRect.sizeDelta = new Vector2(IconWidth, IconHeight);
            iconObject.TryGetComponent(out Image iconImage);
            iconImage.color = s_offlineColor;
            iconImage.raycastTarget = false;
            _subsystemIconImages[index] = iconImage;

            TMP_Text label = CreateText("Label", iconRect, new Vector2(0f, 0f), new Vector2(IconWidth, IconHeight), 14f);
            label.alignment = TextAlignmentOptions.Center;
            label.SetCharArray(labelChars, 0, labelChars.Length);
            _subsystemIconLabels[index] = label;
        }

        private void CreateEngineHeatBar(Transform parent)
        {
            GameObject backObject = new GameObject("EngineHeatBar", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] - engine heat 1D opaque bar background - owner: HectonSubmarineOsDisplay
            backObject.transform.SetParent(parent, false);
            backObject.TryGetComponent(out RectTransform backRect);
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(362f, -61f);
            backRect.sizeDelta = new Vector2(HeatBarWidth, HeatBarHeight);
            backObject.TryGetComponent(out Image backImage);
            backImage.color = s_heatBarBackColor;
            backImage.raycastTarget = false;

            GameObject fillObject = new GameObject("EngineHeatFill", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] - engine heat 1D opaque bar fill - owner: HectonSubmarineOsDisplay
            fillObject.transform.SetParent(backRect, false);
            fillObject.TryGetComponent(out _engineHeatBarFill);
            _engineHeatBarFill.anchorMin = new Vector2(0f, 1f);
            _engineHeatBarFill.anchorMax = new Vector2(0f, 1f);
            _engineHeatBarFill.pivot = new Vector2(0f, 1f);
            _engineHeatBarFill.anchoredPosition = Vector2.zero;
            _engineHeatBarFill.sizeDelta = new Vector2(0f, HeatBarHeight);
            fillObject.TryGetComponent(out _engineHeatBarImage);
            _engineHeatBarImage.color = s_heatBarFillColor;
            _engineHeatBarImage.raycastTarget = false;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(HectonTextNode)); // COLD ALLOC: GameObject[1] — runtime TMP owner for submarine OS display — owner: HectonSubmarineOsDisplay
            textObject.transform.SetParent(parent, false);
            textObject.TryGetComponent(out RectTransform rect);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            textObject.TryGetComponent(out TextMeshProUGUI text);
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

            if (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance == null)
                return null;

            SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TryGetComponent(out Canvas canvas);
            return canvas;
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
                    cursor = AppendChars(destination, cursor, s_logPrefixWarn);
                    cursor = AppendChars(destination, cursor, s_logBusPower);
                    return AppendPercent(destination, cursor, _snapshot.PowerNormalized);

                case HectonSubmarineOsLogCode.LifeSupportCritical:
                    cursor = AppendChars(destination, cursor, s_logPrefixCrit);
                    cursor = AppendChars(destination, cursor, s_logOxygen);
                    return AppendPercent(destination, cursor, _snapshot.OxygenNormalized);

                case HectonSubmarineOsLogCode.HullPressureHigh:
                    cursor = AppendChars(destination, cursor, s_logPrefixWarn);
                    cursor = AppendChars(destination, cursor, s_logHullPressure);
                    cursor = AppendInt(destination, cursor, (int)math.round(_snapshot.MaxPressureKPa));
                    return AppendChars(destination, cursor, s_logKpaSuffix);

                default:
                    if (!TryResolveLogChars(code, out char[] chars, out int length))
                        return 0;

                    return AppendRange(destination, cursor, chars, 0, length);
            }
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
                case HectonSubmarineOsLogCode.HostileDroneDetected:
                    return s_logHostileDroneDetected;
                default:
                    return s_logReactorStable;
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

        private static int AppendChars(char[] destination, int cursor, char[] source)
        {
            return AppendRange(destination, cursor, source, 0, source != null ? source.Length : 0);
        }

        private static int AppendPercent(char[] destination, int cursor, float normalizedValue)
        {
            return AppendPercentValue(destination, cursor, ToPercent(normalizedValue));
        }

        private static int AppendPercentValue(char[] destination, int cursor, int percent)
        {
            int safeCursor = math.clamp(cursor, 0, destination.Length);
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
            int safeCursor = math.clamp(cursor, 0, destination.Length);
            Span<char> writableSpan = new Span<char>(destination, safeCursor, destination.Length - safeCursor);
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
    }
}
