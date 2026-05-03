using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.World;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned diegetic Hecton-OS boot log shown after save-load handoff and PDA reboot recovery.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton OS Boot Manager")]
    public sealed class HectonOSBootManager : MonoBehaviour, ITickable, IUpdatable, IPDAEventListener, IPDAIntrusionEventListener
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
        private HectonEventSubscription _gameLoadedSubscription;
        private HectonEventSubscription _playerSpawnedSubscription;
        private readonly StringBuilder _sequenceBuilder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] — Hecton-OS boot sequence formatting buffer — owner: HectonOSBootManager

        private void OnEnable()
        {
            font = LocalizedFontResolver.ResolveReadableFont(font);
            EnsureUiBuilt();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
            PDAIntrusionEvents.Register(this);
            PDAEvents.Register(this);

            if (ShouldArmLoadBootFromContext())
                _awaitingLoadBoot = true;

            TryStartPendingLoadBoot();
        }

        private void OnDisable()
        {
            PDAIntrusionEvents.Unregister(this);
            PDAEvents.Unregister(this);
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            PDAIntrusionEvents.Unregister(this);
            PDAIntrusionEvents.AssertUnregistered(this, nameof(HectonOSBootManager));
            PDAEvents.Unregister(this);
            PDAEvents.AssertUnregistered(this, nameof(HectonOSBootManager));
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_consoleLabel == null || _overlayGroup == null || _state == SequenceState.Hidden)
                return;

            switch (_state)
            {
                case SequenceState.Typing:
                    _visibleCharacterProgress += deltaTime * CharacterRevealRate;
                    int visibleCharacters = Mathf.Min(_visibleCharacterTarget, Mathf.FloorToInt(_visibleCharacterProgress));
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
                    _overlayGroup.alpha = Mathf.Lerp(_overlayGroup.alpha, 0f, 1f - Mathf.Exp(-FadeSharpness * deltaTime));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                        UnregisterFromTickManager();
                    }
                    break;
            }
        }

        private void HandleGameLoaded(GameLoadedEvent gameLoadedEvent)
        {
            _queuedSlotName = gameLoadedEvent != null ? gameLoadedEvent.SlotName : string.Empty;
            _awaitingLoadBoot = true;
            TryStartPendingLoadBoot();
        }

        private void HandlePlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            ulong ownerEntityId = EntityId.ToULong(gameObject.GetEntityId());
            if (playerSpawnedEvent == null || playerSpawnedEvent.PlayerEntityId != ownerEntityId)
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
            UnregisterFromTickManager();
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

        private void SubscribeToEventBus()
        {
            if (_gameLoadedSubscription == null)
                _gameLoadedSubscription = HectonEventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded, "ui.boot-sequence");

            if (_playerSpawnedSubscription == null)
                _playerSpawnedSubscription = HectonEventBus.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned, "ui.boot-sequence");
        }

        private void UnsubscribeFromEventBus()
        {
            _gameLoadedSubscription?.Dispose();
            _gameLoadedSubscription = null;
            _playerSpawnedSubscription?.Dispose();
            _playerSpawnedSubscription = null;
        }

        private void RebindOwnerSubscriptions()
        {
            UnbindOwnerSubscriptions();
            ResolveOwners();

            if (_playerMovement != null)
                _playerMovement.OnFatalPressureSequence += HandleFatalPressureSequence;
        }

        private void UnbindOwnerSubscriptions()
        {
            if (_playerMovement != null)
                _playerMovement.OnFatalPressureSequence -= HandleFatalPressureSequence;
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

            BuildSequenceText(_sequenceBuilder, reason, slotName);
            _consoleLabel.SetText(_sequenceBuilder);
            _consoleLabel.ForceMeshUpdate();
            _visibleCharacterTarget = _consoleLabel.textInfo.characterCount;
            _visibleCharacterProgress = 0f;
            _consoleLabel.maxVisibleCharacters = 0;
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _state = SequenceState.Typing;
            _stateTimer = 0f;
            RegisterToTickManager();
        }

        private void BuildSequenceText(StringBuilder builder, BootReason reason, string slotName)
        {
            builder.Clear();
            ResolveOwners();

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            SurvivalStats stats = _survivalSystem != null ? _survivalSystem.Stats : null;
            DepthZoneDirector depthZoneDirector = GlobalRegistry.DepthZone;
            DepthZoneProfile currentZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;

            string metersLabel = ResolveLocalized(manager, LocalizationKeys.HUD_UNIT_METERS, "m");
            string atmLabel = ResolveLocalized(manager, LocalizationKeys.HUD_ATM, "ATM");
            string o2Label = ResolveLocalized(manager, LocalizationKeys.HUD_O2, "O2");
            string powerLabel = ResolveLocalized(manager, LocalizationKeys.HUD_PWR, "PWR");
            string hullLabel = ResolveLocalized(manager, LocalizationKeys.HUD_HULL, "HULL");
            string pressureLabel = ResolveLocalized(manager, LocalizationKeys.HUD_PRESSURE, "PRESSURE");
            string depthLabel = ResolveLocalized(manager, LocalizationKeys.HUD_DEPTH, "DEPTH");

            float maxOxygen = stats != null ? stats.MaxOxygen : 0f;
            float maxEnergy = stats != null ? stats.MaxEnergy : 0f;
            float maxIntegrity = stats != null ? stats.MaxIntegrity : 0f;
            float safeDepth = stats != null ? stats.SafeDepth : 0f;
            float liveDepth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            float livePressure = _survivalSystem != null ? _survivalSystem.Pressure : 0f;
            float liveIntegrity = _survivalSystem != null ? _survivalSystem.Integrity : 0f;
            float integrityNormalized = _survivalSystem != null ? _survivalSystem.IntegrityNormalized : 0f;
            string zoneName = currentZone != null ? currentZone.DisplayNameOrFallback : DefaultUnknownZone;
            string bootVector = ResolveBootVector(reason);
            bool hasStats = stats != null;
            bool hasZone = currentZone != null;
            string memoryStatus = hasStats ? DefaultOkStatus : DefaultFailedStatus;
            string dataStatus = hasZone ? DefaultOkStatus : DefaultDegradedStatus;
            string localizationStatus = manager != null ? DefaultOkStatus : DefaultDegradedStatus;
            string hullStatus = ResolveHullIntegrityStatus(integrityNormalized, _survivalSystem, reason);
            string pressureStatus = ResolvePressureBusStatus(_survivalSystem);

            builder.AppendLine(DefaultBootHeader);
            builder.Append("BOOT VECTOR ....... ").AppendLine(bootVector);
            builder.Append("SAVE SLOT ......... ");
            AppendSlotValue(builder, slotName);
            builder.AppendLine();
            builder.Append("CHECKING MEMORY ... ").Append(memoryStatus)
                .Append(" [")
                .Append(o2Label).Append(' ');
            AppendRounded(builder, maxOxygen);
            builder
                .Append(" | ")
                .Append(powerLabel).Append(' ');
            AppendRounded(builder, maxEnergy);
            builder
                .Append(" | ")
                .Append(hullLabel).Append(' ');
            AppendRounded(builder, maxIntegrity);
            builder
                .Append(']')
                .AppendLine();
            builder.Append("MOUNTING ABYSSAL DATA ... ").Append(dataStatus)
                .Append(" [")
                .Append(depthLabel).Append(' ');
            AppendRounded(builder, liveDepth);
            builder.Append(' ').Append(metersLabel)
                .Append(" | ")
                .Append(zoneName)
                .Append(']')
                .AppendLine();
            builder.Append("LOADING LOCALIZATION MODULES ... ").Append(localizationStatus)
                .Append(" [LANG ");
            AppendLanguageTag(builder, manager);
            builder.Append(']').AppendLine();
            builder.Append("CALIBRATING HULL INTEGRITY ... ").Append(hullStatus)
                .Append(" [")
                .Append(hullLabel).Append(' ');
            AppendRounded(builder, liveIntegrity);
            builder.Append(" / ");
            AppendRounded(builder, maxIntegrity);
            builder
                .Append(']')
                .AppendLine();
            builder.Append("SYNCING PRESSURE BUS ... ").Append(pressureStatus)
                .Append(" [")
                .Append(pressureLabel).Append(' ');
            AppendFixedOne(builder, livePressure);
            builder.Append(' ').Append(atmLabel)
                .Append(" | SAFE ");
            AppendRounded(builder, safeDepth);
            builder.Append(' ').Append(metersLabel).Append(']');
        }

        private static void AppendSlotValue(StringBuilder builder, string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                builder.Append(DefaultRecoverySlot);
                return;
            }

            for (int i = 0; i < slotName.Length; i++)
                builder.Append(char.ToUpperInvariant(slotName[i]));
        }

        private static void AppendLanguageTag(StringBuilder builder, LocalizationManager manager)
        {
            if (manager == null)
            {
                builder.Append("FALLBACK");
                return;
            }

            switch (manager.CurrentLanguage)
            {
                case GameLanguage.English: builder.Append("ENGLISH"); break;
                case GameLanguage.Russian: builder.Append("RUSSIAN"); break;
                case GameLanguage.German: builder.Append("GERMAN"); break;
                case GameLanguage.French: builder.Append("FRENCH"); break;
                case GameLanguage.Spanish: builder.Append("SPANISH"); break;
                case GameLanguage.Italian: builder.Append("ITALIAN"); break;
                case GameLanguage.PortugueseBrazilian: builder.Append("PORTUGUESEBRAZILIAN"); break;
                case GameLanguage.Polish: builder.Append("POLISH"); break;
                case GameLanguage.Turkish: builder.Append("TURKISH"); break;
                case GameLanguage.Ukrainian: builder.Append("UKRAINIAN"); break;
                case GameLanguage.ChineseSimplified: builder.Append("CHINESESIMPLIFIED"); break;
                case GameLanguage.ChineseTraditional: builder.Append("CHINESETRADITIONAL"); break;
                case GameLanguage.Japanese: builder.Append("JAPANESE"); break;
                case GameLanguage.Korean: builder.Append("KOREAN"); break;
                case GameLanguage.Hindi: builder.Append("HINDI"); break;
                case GameLanguage.Indonesian: builder.Append("INDONESIAN"); break;
                case GameLanguage.Arabic: builder.Append("ARABIC"); break;
                default: builder.Append("UNKNOWN"); break;
            }
        }

        private static void AppendRounded(StringBuilder builder, float value)
        {
            builder.Append(Mathf.RoundToInt(value));
        }

        private static void AppendFixedOne(StringBuilder builder, float value)
        {
            int scaled = Mathf.RoundToInt(value * 10f);
            if (scaled < 0)
            {
                builder.Append('-');
                scaled = -scaled;
            }

            builder.Append(scaled / 10);
            builder.Append('.');
            builder.Append(scaled % 10);
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
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

                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0f, 1f);
            _overlayRoot.anchorMax = new Vector2(0f, 1f);
            _overlayRoot.pivot = new Vector2(0f, 1f);
            _overlayRoot.anchoredPosition = new Vector2(48f, -52f);
            _overlayRoot.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            _overlayGroup = _overlayRoot.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;

            Image background = _overlayRoot.GetComponent<Image>();
            if (background == null)
                background = _overlayRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.06f, 0.08f, 0.82f);
            background.raycastTarget = false;

            ClearChildren(_overlayRoot);

            GameObject textObject = new GameObject("ConsoleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = _overlayRoot.gameObject.layer;
            RectTransform textRoot = textObject.GetComponent<RectTransform>();
            textRoot.SetParent(_overlayRoot, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(24f, 20f);
            textRoot.offsetMax = new Vector2(-24f, -20f);

            _consoleLabel = textObject.GetComponent<TextMeshProUGUI>();
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

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static string ResolveLocalized(LocalizationManager manager, string key, string fallback)
        {
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
