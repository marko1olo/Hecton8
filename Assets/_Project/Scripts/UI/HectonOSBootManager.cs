using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.World;
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
    public sealed class HectonOSBootManager : MonoBehaviour, ITickable
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

        private void OnEnable()
        {
            font = LocalizedFontResolver.ResolveReadableFont(font);
            EnsureUiBuilt();
            SubscribeToEventBus();
            RebindOwnerSubscriptions();
            PDAIntrusionManager.OnRebootCompleted += HandleIntrusionRebootCompleted;
            PDAEvents.OnClosed += HandlePdaClosed;

            if (ShouldArmLoadBootFromContext())
                _awaitingLoadBoot = true;

            TryStartPendingLoadBoot();
        }

        private void OnDisable()
        {
            PDAIntrusionManager.OnRebootCompleted -= HandleIntrusionRebootCompleted;
            PDAEvents.OnClosed -= HandlePdaClosed;
            UnbindOwnerSubscriptions();
            UnsubscribeFromEventBus();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            PDAIntrusionManager.OnRebootCompleted -= HandleIntrusionRebootCompleted;
            PDAEvents.OnClosed -= HandlePdaClosed;
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
            if (playerSpawnedEvent == null || playerSpawnedEvent.PlayerObject != gameObject)
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

            _consoleLabel.text = BuildSequenceText(reason, slotName);
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

        private string BuildSequenceText(BootReason reason, string slotName)
        {
            ResolveOwners();

            LocalizationManager manager = LocalizationManager.Instance;
            SurvivalStats stats = _survivalSystem != null ? _survivalSystem.Stats : null;
            DepthZoneProfile currentZone = DepthZoneDirector.Instance != null ? DepthZoneDirector.Instance.CurrentZone : null;

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
            string slotValue = string.IsNullOrWhiteSpace(slotName)
                ? DefaultRecoverySlot
                : slotName.ToUpperInvariant();
            bool hasStats = stats != null;
            bool hasZone = currentZone != null;
            string memoryStatus = hasStats ? DefaultOkStatus : DefaultFailedStatus;
            string dataStatus = hasZone ? DefaultOkStatus : DefaultDegradedStatus;
            string localizationStatus = manager != null ? DefaultOkStatus : DefaultDegradedStatus;
            string hullStatus = ResolveHullIntegrityStatus(integrityNormalized, _survivalSystem, reason);
            string pressureStatus = ResolvePressureBusStatus(_survivalSystem);
            string languageTag = manager != null ? manager.CurrentLanguage.ToString().ToUpperInvariant() : "FALLBACK";

            System.Text.StringBuilder builder = StringBuilderPool.Get();
            builder.AppendLine(DefaultBootHeader);
            builder.Append("BOOT VECTOR ....... ").AppendLine(bootVector);
            builder.Append("SAVE SLOT ......... ").AppendLine(slotValue);
            builder.Append("CHECKING MEMORY ... ").Append(memoryStatus)
                .Append(" [")
                .Append(o2Label).Append(' ').Append(maxOxygen.ToString("0"))
                .Append(" | ")
                .Append(powerLabel).Append(' ').Append(maxEnergy.ToString("0"))
                .Append(" | ")
                .Append(hullLabel).Append(' ').Append(maxIntegrity.ToString("0"))
                .Append(']')
                .AppendLine();
            builder.Append("MOUNTING ABYSSAL DATA ... ").Append(dataStatus)
                .Append(" [")
                .Append(depthLabel).Append(' ').Append(liveDepth.ToString("0")).Append(' ').Append(metersLabel)
                .Append(" | ")
                .Append(zoneName)
                .Append(']')
                .AppendLine();
            builder.Append("LOADING LOCALIZATION MODULES ... ").Append(localizationStatus)
                .Append(" [LANG ")
                .Append(languageTag)
                .Append(']')
                .AppendLine();
            builder.Append("CALIBRATING HULL INTEGRITY ... ").Append(hullStatus)
                .Append(" [")
                .Append(hullLabel).Append(' ').Append(liveIntegrity.ToString("0"))
                .Append(" / ")
                .Append(maxIntegrity.ToString("0"))
                .Append(']')
                .AppendLine();
            builder.Append("SYNCING PRESSURE BUS ... ").Append(pressureStatus)
                .Append(" [")
                .Append(pressureLabel).Append(' ').Append(livePressure.ToString("0.0")).Append(' ').Append(atmLabel)
                .Append(" | SAFE ")
                .Append(safeDepth.ToString("0")).Append(' ').Append(metersLabel)
                .Append(']');

            string sequenceText = builder.ToString();
            StringBuilderPool.Return(builder);
            return sequenceText;
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
            if (_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

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
            SuitHUDV4CanvasOverlay overlay = Object.FindAnyObjectByType<SuitHUDV4CanvasOverlay>();
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return Object.FindAnyObjectByType<Canvas>();
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
