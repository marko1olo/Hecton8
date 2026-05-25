using Hecton.Localization;
using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned diegetic Hecton-OS boot log shown after save-load handoff and PDA reboot recovery.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton OS Boot Manager")]
    public sealed class HectonOSBootManager : MonoBehaviour, ITickable, IUpdatable, IPDAEventListener, IPDAIntrusionEventListener, IGlobalRegistryHotSwapListener
    {
        private enum BootReason : byte
        {
            LoadGame = 0,
            IntrusionRecovery = 1,
            PressureRecovery = 2
        }

        private enum SequenceState : byte
        {
            Hidden = 0,
            Typing = 1,
            Hold = 2,
            Fade = 3
        }

        private const float CharacterRevealRate = 96f;
        private const float HoldDuration = 1.35f;
        private const float FadeSharpness = 5.5f;
        private const float FatalPressureTrigger01 = 0.18f;
        private const float HiddenAlphaCutoff = 0.01f;
        private const float OverlayWidth = 920f;
        private const float OverlayHeight = 312f;
        private const int BootPayloadCharCapacity = 1024;
        private const string OverlayName = "HectonOSBootManagerOverlay";
        private const string DefaultBootHeader = "HECTON-OS // BIOS HANDOFF";
        private const string DefaultLoadVector = "LOAD HANDOFF";
        private const string DefaultIntrusionVector = "EMI RECOVERY";
        private const string DefaultRecoveryVector = "PRESSURE RECOVERY";
        private const string DefaultRecoverySlot = "RECOVERY CACHE";
        private const string DefaultUnknownZone = "UNKNOWN ZONE";
        private const string DefaultUnknownStatus = "NOMINAL";
        private const string DefaultOkStatus = "OK";
        private const string DefaultFailedStatus = "FAILED";
        private const string DefaultDegradedStatus = "DEGRADED";
        private const float IntegrityFailureThreshold01 = 0.94f;
        private const float IntegrityDegradedThreshold01 = 0.82f;
        private static readonly int MetersKeyHash = LocHash.Compute(LocalizationKeys.HUD_UNIT_METERS);
        private static readonly int AtmosphereKeyHash = LocHash.Compute(LocalizationKeys.HUD_ATM);
        private static readonly int OxygenKeyHash = LocHash.Compute(LocalizationKeys.HUD_O2);
        private static readonly int PowerKeyHash = LocHash.Compute(LocalizationKeys.HUD_PWR);
        private static readonly int HullKeyHash = LocHash.Compute(LocalizationKeys.HUD_HULL);
        private static readonly int PressureKeyHash = LocHash.Compute(LocalizationKeys.HUD_PRESSURE);
        private static readonly int DepthKeyHash = LocHash.Compute(LocalizationKeys.HUD_DEPTH);

        [Header("── Font ──────────────────")]
        [Tooltip("Optional TMP font override for the boot log. Leave empty to use the readable fallback resolver.")]
        [SerializeField] private TMP_FontAsset font;

        private RectTransform _overlayRoot;
        private CanvasGroup _overlayGroup;
        private TextMeshProUGUI _consoleLabel;
        private bool _uiBuilt;
        private bool _tickRegistered;
        private bool _awaitingLoadBoot;
        private bool _fatalPressureLatched;
        private float _stateTimer;
        private float _visibleCharacterProgress;
        private int _visibleCharacterTarget;
        private string _queuedSlotName = string.Empty;
        private SequenceState _state;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private ILocalizationTextReadModel _localization;
        private DepthZoneDirector _depthZoneDirector;
        private bool _hotSwapRegistered;
        private uint _lastSessionLifecycleSequence;
        private readonly char[] _sequencePayloadBuffer = new char[BootPayloadCharCapacity]; // COLD ALLOC: char[1024] — Hecton-OS boot TMP payload buffer — owner: HectonOSBootManager

        private struct BootTextWriter
        {
            private readonly char[] _buffer;
            public int Length;

            public BootTextWriter(char[] buffer)
            {
                _buffer = buffer;
                Length = 0;
            }

            public void Append(ReadOnlySpan<char> text)
            {
                if (_buffer == null || text.Length <= 0 || Length >= _buffer.Length)
                    return;

                int writable = math.min(text.Length, _buffer.Length - Length);
                text.Slice(0, writable).CopyTo(_buffer.AsSpan(Length, writable));
                Length += writable;
            }

            public void Append(char value)
            {
                if (_buffer == null || Length >= _buffer.Length)
                    return;

                _buffer[Length++] = value;
            }

            public void AppendLine()
            {
                Append('\n');
            }

            public void AppendLine(ReadOnlySpan<char> text)
            {
                Append(text);
                AppendLine();
            }

            public void AppendUpperInvariant(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                for (int i = 0; i < value.Length; i++)
                    Append(ToAsciiUpperInvariant(value[i]));
            }

            private static char ToAsciiUpperInvariant(char value)
            {
                return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
            }

            public void AppendInt(int value)
            {
                if (value == int.MinValue)
                {
                    Append("-2147483648".AsSpan());
                    return;
                }

                if (value < 0)
                {
                    Append('-');
                    value = -value;
                }

                Span<char> digits = stackalloc char[10];
                int count = 0;
                do
                {
                    digits[count++] = (char)('0' + (value % 10));
                    value /= 10;
                }
                while (value > 0 && count < digits.Length);

                for (int i = count - 1; i >= 0; i--)
                    Append(digits[i]);
            }
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            font = LocalizedFontResolver.ResolveReadableFont(font, _localization);
            EnsureUiBuilt();
            RebindOwnerSubscriptions();
            PDAIntrusionEvents.Register(this);
            PDAEvents.Register(this);
            RegisterToTickManager();

            if (ShouldArmLoadBootFromContext())
                _awaitingLoadBoot = true;

            TryStartPendingLoadBoot();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            PDAIntrusionEvents.Unregister(this);
            PDAEvents.Unregister(this);
            UnbindOwnerSubscriptions();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            PDAIntrusionEvents.Unregister(this);
            PDAIntrusionEvents.AssertUnregistered(this, nameof(HectonOSBootManager));
            PDAEvents.Unregister(this);
            PDAEvents.AssertUnregistered(this, nameof(HectonOSBootManager));
            UnbindOwnerSubscriptions();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            ProcessSessionLifecycleSignals();
            ConsumeFatalPressureSignals();

            if (_consoleLabel == null || _overlayGroup == null || _state == SequenceState.Hidden)
                return;

            switch (_state)
            {
                case SequenceState.Typing:
                    _visibleCharacterProgress += deltaTime * CharacterRevealRate;
                    int visibleCharacters = math.min(_visibleCharacterTarget, (int)math.floor(_visibleCharacterProgress));
                    if (_consoleLabel.maxVisibleCharacters != visibleCharacters)
                        _consoleLabel.maxVisibleCharacters = visibleCharacters;

                    if (visibleCharacters >= _visibleCharacterTarget)
                    {
                        _state = SequenceState.Hold;
                        _stateTimer = HoldDuration;
                    }
                    break;

                case SequenceState.Hold:
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f)
                        _state = SequenceState.Fade;
                    break;

                case SequenceState.Fade:
                    _overlayGroup.alpha = math.lerp(_overlayGroup.alpha, 0f, FastDecayBlend(FadeSharpness, deltaTime));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                    }
                    break;
            }
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0.1f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private void ProcessSessionLifecycleSignals()
        {
            ReadOnlySpan<SessionLifecycleSignal> signals = SignalBus<SessionLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SessionLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastSessionLifecycleSequence))
                    continue;

                _lastSessionLifecycleSequence = signal.Sequence;
                if (signal.Kind == SessionLifecycleSignal.KindGameLoaded)
                    HandleGameLoaded();
                else if (signal.Kind == SessionLifecycleSignal.KindPlayerSpawned)
                    HandlePlayerSpawned(in signal);
            }
        }

        private void HandleGameLoaded()
        {
            _queuedSlotName = string.Empty;
            _awaitingLoadBoot = true;
            TryStartPendingLoadBoot();
        }

        private void HandlePlayerSpawned(in SessionLifecycleSignal signal)
        {
            ulong ownerEntityId = EntityId.ToULong(gameObject.GetEntityId());
            if (signal.PlayerEntityId == 0ul || signal.PlayerEntityId != ownerEntityId)
                return;

            RebindOwnerSubscriptions();
            if (ShouldArmLoadBootFromContext())
                _awaitingLoadBoot = true;

            TryStartPendingLoadBoot();
        }

        private void HandleIntrusionRebootCompleted()
        {
            StartSequence(BootReason.IntrusionRecovery, DefaultRecoverySlot);
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType == PDAEventType.Closed)
                HandlePdaClosed(payload.DurationSeconds);
        }

        public void OnPDAIntrusionEvent(in PDAIntrusionEventPayload payload)
        {
            if ((PDAIntrusionEventType)payload.EventType == PDAIntrusionEventType.RebootCompleted)
                HandleIntrusionRebootCompleted();
        }

        private void HandlePdaClosed(float _)
        {
            if (_state == SequenceState.Hidden)
                return;

            HideOverlay();
        }

        private void ConsumeFatalPressureSignals()
        {
            ReadOnlySpan<PlayerFatalPressureSignal> signals = SignalBus<PlayerFatalPressureSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                HandleFatalPressureSequence(signals[i].Intensity01);
        }

        private void HandleFatalPressureSequence(float intensity)
        {
            if (intensity <= 0.001f)
            {
                _fatalPressureLatched = false;
                return;
            }

            if (_fatalPressureLatched || intensity < FatalPressureTrigger01)
                return;

            _fatalPressureLatched = true;
            StartSequence(BootReason.PressureRecovery, DefaultRecoverySlot);
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwners();

        }

        private void UnbindOwnerSubscriptions()
        {
        }

        private bool ResolveOwners()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            return _survivalSystem != null || _playerMovement != null;
        }

        private bool ShouldArmLoadBootFromContext()
        {
            GameStartContext context = GameStartContextHolder.Current;
            return context.IsValid && context.StartMode == GameStartMode.LoadGame;
        }

        private void TryStartPendingLoadBoot()
        {
            if (!_awaitingLoadBoot || !ResolveOwners())
                return;

            string slotName = !string.IsNullOrWhiteSpace(_queuedSlotName)
                ? _queuedSlotName
                : GameStartContextHolder.Current.TargetSaveSlot;
            StartSequence(BootReason.LoadGame, slotName);
            _awaitingLoadBoot = false;
        }

        private void StartSequence(BootReason reason, string slotName)
        {
            EnsureUiBuilt();
            if (_consoleLabel == null || _overlayGroup == null)
                return;

            int payloadLength = BuildSequenceText(_sequencePayloadBuffer, reason, slotName);
            _consoleLabel.SetCharArray(_sequencePayloadBuffer, 0, payloadLength);
            _visibleCharacterTarget = payloadLength;
            _visibleCharacterProgress = 0f;
            _consoleLabel.maxVisibleCharacters = 0;
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _state = SequenceState.Typing;
            _stateTimer = 0f;
            RegisterToTickManager();
        }

        private int BuildSequenceText(char[] destination, BootReason reason, string slotName)
        {
            BootTextWriter writer = new BootTextWriter(destination);
            ResolveOwners();

            ILocalizationTextReadModel manager = _localization;
            SurvivalStats stats = _survivalSystem != null ? _survivalSystem.Stats : null;
            DepthZoneDirector depthZoneDirector = _depthZoneDirector;
            DepthZoneProfile currentZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;

            ReadOnlySpan<char> metersLabel = ResolveLocalizedSpan(manager, MetersKeyHash, "m".AsSpan());
            ReadOnlySpan<char> atmLabel = ResolveLocalizedSpan(manager, AtmosphereKeyHash, "ATM".AsSpan());
            ReadOnlySpan<char> o2Label = ResolveLocalizedSpan(manager, OxygenKeyHash, "O2".AsSpan());
            ReadOnlySpan<char> powerLabel = ResolveLocalizedSpan(manager, PowerKeyHash, "PWR".AsSpan());
            ReadOnlySpan<char> hullLabel = ResolveLocalizedSpan(manager, HullKeyHash, "HULL".AsSpan());
            ReadOnlySpan<char> pressureLabel = ResolveLocalizedSpan(manager, PressureKeyHash, "PRESSURE".AsSpan());
            ReadOnlySpan<char> depthLabel = ResolveLocalizedSpan(manager, DepthKeyHash, "DEPTH".AsSpan());

            float maxOxygen = stats != null ? stats.MaxOxygen : 0f;
            float maxEnergy = stats != null ? stats.MaxEnergy : 0f;
            float maxIntegrity = stats != null ? stats.MaxIntegrity : 0f;
            float safeDepth = stats != null ? stats.SafeDepth : 0f;
            float liveDepth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            float livePressure = _survivalSystem != null ? _survivalSystem.Pressure : 0f;
            float liveIntegrity = _survivalSystem != null ? _survivalSystem.Integrity : 0f;
            float integrityNormalized = _survivalSystem != null ? _survivalSystem.IntegrityNormalized : 0f;
            ReadOnlySpan<char> zoneName = currentZone != null
                ? currentZone.ResolveDisplayNameSpan(manager)
                : DefaultUnknownZone.AsSpan();
            ReadOnlySpan<char> bootVector = ResolveBootVector(reason).AsSpan();
            bool hasStats = stats != null;
            bool hasZone = currentZone != null;
            ReadOnlySpan<char> memoryStatus = (hasStats ? DefaultOkStatus : DefaultFailedStatus).AsSpan();
            ReadOnlySpan<char> dataStatus = (hasZone ? DefaultOkStatus : DefaultDegradedStatus).AsSpan();
            ReadOnlySpan<char> localizationStatus = (manager != null ? DefaultOkStatus : DefaultDegradedStatus).AsSpan();
            ReadOnlySpan<char> hullStatus = ResolveHullIntegrityStatus(integrityNormalized, _survivalSystem, reason).AsSpan();
            ReadOnlySpan<char> pressureStatus = ResolvePressureBusStatus(_survivalSystem).AsSpan();

            writer.AppendLine(DefaultBootHeader.AsSpan());
            writer.Append("BOOT VECTOR ....... ".AsSpan());
            writer.AppendLine(bootVector);
            writer.Append("SAVE SLOT ......... ".AsSpan());
            AppendSlotValue(ref writer, slotName);
            writer.AppendLine();
            writer.Append("CHECKING MEMORY ... ".AsSpan());
            writer.Append(memoryStatus);
            writer.Append(" [".AsSpan());
            writer.Append(o2Label);
            writer.Append(' ');
            AppendRounded(ref writer, maxOxygen);
            writer.Append(" | ".AsSpan());
            writer.Append(powerLabel);
            writer.Append(' ');
            AppendRounded(ref writer, maxEnergy);
            writer.Append(" | ".AsSpan());
            writer.Append(hullLabel);
            writer.Append(' ');
            AppendRounded(ref writer, maxIntegrity);
            writer.Append(']');
            writer.AppendLine();
            writer.Append("MOUNTING ABYSSAL DATA ... ".AsSpan());
            writer.Append(dataStatus);
            writer.Append(" [".AsSpan());
            writer.Append(depthLabel);
            writer.Append(' ');
            AppendRounded(ref writer, liveDepth);
            writer.Append(' ');
            writer.Append(metersLabel);
            writer.Append(" | ".AsSpan());
            writer.Append(zoneName);
            writer.Append(']');
            writer.AppendLine();
            writer.Append("LOADING LOCALIZATION MODULES ... ".AsSpan());
            writer.Append(localizationStatus);
            writer.Append(" [LANG ".AsSpan());
            AppendLanguageTag(ref writer, manager);
            writer.Append(']');
            writer.AppendLine();
            writer.Append("CALIBRATING HULL INTEGRITY ... ".AsSpan());
            writer.Append(hullStatus);
            writer.Append(" [".AsSpan());
            writer.Append(hullLabel);
            writer.Append(' ');
            AppendRounded(ref writer, liveIntegrity);
            writer.Append(" / ".AsSpan());
            AppendRounded(ref writer, maxIntegrity);
            writer.Append(']');
            writer.AppendLine();
            writer.Append("SYNCING PRESSURE BUS ... ".AsSpan());
            writer.Append(pressureStatus);
            writer.Append(" [".AsSpan());
            writer.Append(pressureLabel);
            writer.Append(' ');
            AppendFixedOne(ref writer, livePressure);
            writer.Append(' ');
            writer.Append(atmLabel);
            writer.Append(" | SAFE ".AsSpan());
            AppendRounded(ref writer, safeDepth);
            writer.Append(' ');
            writer.Append(metersLabel);
            writer.Append(']');
            return writer.Length;
        }

        private static void AppendSlotValue(ref BootTextWriter writer, string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                writer.Append(DefaultRecoverySlot.AsSpan());
                return;
            }

            writer.AppendUpperInvariant(slotName);
        }

        private static void AppendLanguageTag(ref BootTextWriter writer, ILocalizationTextReadModel manager)
        {
            if (manager == null)
            {
                writer.Append("FALLBACK".AsSpan());
                return;
            }

            switch ((GameLanguage)manager.ActiveLanguageId)
            {
                case GameLanguage.English: writer.Append("ENGLISH".AsSpan()); break;
                case GameLanguage.Russian: writer.Append("RUSSIAN".AsSpan()); break;
                case GameLanguage.German: writer.Append("GERMAN".AsSpan()); break;
                case GameLanguage.French: writer.Append("FRENCH".AsSpan()); break;
                case GameLanguage.Spanish: writer.Append("SPANISH".AsSpan()); break;
                case GameLanguage.Italian: writer.Append("ITALIAN".AsSpan()); break;
                case GameLanguage.PortugueseBrazilian: writer.Append("PORTUGUESEBRAZILIAN".AsSpan()); break;
                case GameLanguage.Polish: writer.Append("POLISH".AsSpan()); break;
                case GameLanguage.Turkish: writer.Append("TURKISH".AsSpan()); break;
                case GameLanguage.Ukrainian: writer.Append("UKRAINIAN".AsSpan()); break;
                case GameLanguage.ChineseSimplified: writer.Append("CHINESESIMPLIFIED".AsSpan()); break;
                case GameLanguage.ChineseTraditional: writer.Append("CHINESETRADITIONAL".AsSpan()); break;
                case GameLanguage.Japanese: writer.Append("JAPANESE".AsSpan()); break;
                case GameLanguage.Korean: writer.Append("KOREAN".AsSpan()); break;
                case GameLanguage.Hindi: writer.Append("HINDI".AsSpan()); break;
                case GameLanguage.Indonesian: writer.Append("INDONESIAN".AsSpan()); break;
                case GameLanguage.Arabic: writer.Append("ARABIC".AsSpan()); break;
                default: writer.Append("UNKNOWN".AsSpan()); break;
            }
        }

        private static void AppendRounded(ref BootTextWriter writer, float value)
        {
            writer.AppendInt(Mathf.RoundToInt(value));
        }

        private static void AppendFixedOne(ref BootTextWriter writer, float value)
        {
            int scaled = Mathf.RoundToInt(value * 10f);
            if (scaled < 0)
            {
                writer.Append('-');
                scaled = -scaled;
            }

            writer.AppendInt(scaled / 10);
            writer.Append('.');
            writer.Append((char)('0' + (scaled % 10)));
        }

        private static string ResolveBootVector(BootReason reason)
        {
            switch (reason)
            {
                case BootReason.IntrusionRecovery:
                    return DefaultIntrusionVector;

                case BootReason.PressureRecovery:
                    return DefaultRecoveryVector;

                default:
                    return DefaultLoadVector;
            }
        }

        private static string ResolveHullIntegrityStatus(float integrityNormalized, HectonSurvivalSystem survivalSystem, BootReason reason)
        {
            if (survivalSystem == null)
                return DefaultFailedStatus;

            if (integrityNormalized < IntegrityDegradedThreshold01)
                return DefaultFailedStatus;

            if (reason == BootReason.PressureRecovery ||
                integrityNormalized < IntegrityFailureThreshold01 ||
                survivalSystem.IsBeyondSafeDepth)
            {
                return DefaultDegradedStatus;
            }

            return DefaultOkStatus;
        }

        private static string ResolvePressureBusStatus(HectonSurvivalSystem survivalSystem)
        {
            if (survivalSystem == null)
                return DefaultFailedStatus;

            return survivalSystem.IsBeyondSafeDepth
                ? DefaultDegradedStatus
                : DefaultOkStatus;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    font = LocalizedFontResolver.ResolveReadableFont(font, _localization);
                    if (_consoleLabel != null && font != null)
                        _consoleLabel.font = font;
                    break;
                case GlobalRegistryServiceSlot.DepthZoneRuntime:
                    _depthZoneDirector = currentService as DepthZoneDirector;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _localization = GlobalRegistry.LocalizationText;
            _depthZoneDirector = GlobalRegistry.DepthZone;
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

        private void HideOverlay()
        {
            _state = SequenceState.Hidden;
            _stateTimer = 0f;
            _visibleCharacterProgress = 0f;
            _visibleCharacterTarget = 0;

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
                _overlayGroup.blocksRaycasts = false;
                _overlayGroup.interactable = false;
            }

            if (_consoleLabel != null)
                _consoleLabel.maxVisibleCharacters = int.MaxValue;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
            {
                GameObject overlayObject = new GameObject(
                    OverlayName,
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                overlayObject.layer = targetCanvas.gameObject.layer;

                overlayObject.TryGetComponent(out _overlayRoot);
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0f, 1f);
            _overlayRoot.anchorMax = new Vector2(0f, 1f);
            _overlayRoot.pivot = new Vector2(0f, 1f);
            _overlayRoot.anchoredPosition = new Vector2(48f, -52f);
            _overlayRoot.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            if (!_overlayRoot.TryGetComponent(out _overlayGroup))
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;

            if (!_overlayRoot.TryGetComponent(out Image background))
                background = _overlayRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.06f, 0.08f, 0.82f);
            background.raycastTarget = false;

            ClearChildren(_overlayRoot);

            GameObject textObject = new GameObject("ConsoleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = _overlayRoot.gameObject.layer;
            textObject.TryGetComponent(out RectTransform textRoot);
            textRoot.SetParent(_overlayRoot, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(24f, 20f);
            textRoot.offsetMax = new Vector2(-24f, -20f);

            textObject.TryGetComponent(out _consoleLabel);
            if (font != null)
                _consoleLabel.font = font;

            _consoleLabel.fontSize = 22f;
            _consoleLabel.color = new Color(0.70f, 0.96f, 0.88f, 1f);
            _consoleLabel.alignment = TextAlignmentOptions.TopLeft;
            _consoleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _consoleLabel.overflowMode = TextOverflowModes.Overflow;
            _consoleLabel.maxVisibleCharacters = int.MaxValue;
            TMP_TextRegistry.EnsureRegistered(_consoleLabel);

            _uiBuilt = true;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return overlay != null && overlay.TryGetComponent(out Canvas canvas) ? canvas : null;
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(ILocalizationTextReadModel manager, int keyHash, ReadOnlySpan<char> fallback)
        {
            return manager != null
                ? manager.GetRawSpanOrFallback(keyHash, fallback)
                : fallback;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
