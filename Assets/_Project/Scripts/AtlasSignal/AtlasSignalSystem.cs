// ============================================================================
// HECTON-8 — AtlasSignalSystem.cs
// Система пульса сигнала Атлас-6.
//
// ЛОР (лор3 Блок З):
//   Слух среди скавенджеров: "На Гектоне-8 есть сигнал, который повторяется
//   каждые 11:23". Ритм 11:23 — время перебора всех вариантов "спасения колонии".
//   Чем ближе к ядру — тем яснее "содержание" сигнала:
//   не слова, а эмоциональный паттерн: отчаяние, надежда, безумие.
//
// МЕХАНИКА:
//   • Пульс каждые 683 секунды (11 мин 23 сек).
//   • Сила сигнала = 1 - (dist / maxSignalRange).
//   • Сканер получает usable bearing only after late identity-stage lock.
//   • Quest handoff идёт через discovery-chain, а не через ранний raw detect.
//   • Интегрируется с HectonDirectorAI (narrative beat).
//
// ZERO GC:
//   • ISlowTickable — timer without per-frame polling.
//   • Никаких new/LINQ в hot path.
//   • Shader.SetGlobalFloat для визуального отклика биолюминесценции.
// ============================================================================

using Conditional = System.Diagnostics.ConditionalAttribute;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class AtlasSignalSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Signal Parameters ──────────────────────")]
        [Tooltip("Период пульса в секундах (683 = 11 мин 23 сек).")]
        [SerializeField] private float pulsePeriodSeconds = 683f;

        [Tooltip("Максимальная дальность обнаружения сигнала (метры).")]
        [SerializeField] private float maxSignalRange = 8000f;

        [Tooltip("Позиция ядра Атлас-6 в мировых координатах.")]
        [SerializeField] private Vector3 atlasCorePosWorld = new Vector3(0f, -5000f, 0f);

        [Tooltip("Минимальная сила сигнала для обнаружения сканером.")]
        [SerializeField, Range(0f, 1f)] private float detectionThreshold = 0.05f;

        [Header("── Late Manifestation ─────────────────────")]
        [Tooltip("Atlas stays dormant until the first-hour spine has already handed the player to deeper route/module play.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToManifest = FirstHourMilestone.FirstModule;

        [Tooltip("Before full manifestation, Atlas may leak only a weak rhythmic ghost-beat once the player has already proven deeper commitment.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToGhostManifest = FirstHourMilestone.FirstCraft;

        [Tooltip("Depth where the first rhythmic Atlas beat can cut through the water.")]
        [SerializeField] private float revealStage1Depth = 180f;

        [Tooltip("Depth where the rhythm stops reading as noise and starts reading as pattern.")]
        [SerializeField] private float revealStage2Depth = 450f;

        [Tooltip("Depth where the signal starts yielding content fragments instead of pure rhythm.")]
        [SerializeField] private float revealStage3Depth = 1200f;

        [Tooltip("Depth where the carrier becomes stable enough for a true late-game lock on the source.")]
        [SerializeField] private float revealStage4Depth = 2600f;

        [Header("── Shader Integration ────────────────────")]
        [Tooltip("Публиковать силу сигнала в шейдер для биолюминесцентного отклика.")]
        [SerializeField] private bool publishToShader = true;

        [Header("Encrypted Log Unlocks")]
        [SerializeField] private string stage2EncryptedLogId = "captain_last_broadcast";
        [SerializeField] private string stage3EncryptedLogId = "atlas6_terminal_sector3";
        [SerializeField] private string stage4EncryptedLogId = "biologist_samples";

        // ══════════════════════════════════════════════════════════
        //  SERVICE AUTHORITY
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private HectonPlayerMovement _playerMovement;
        private AbsoluteUniversePosition _atlasCoreAup;
        private Vector3 _atlasCoreAupSource;
        private float _pulseTimer;
        private float _currentStrength;
        private float _lastPublishedStrength;
        private int _currentStrengthBand;
        private bool _signalEverDetected;
        private int _maxRevealStageUnlocked;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _ghostManifestationAnnounced;
        private bool _identityDiscoverySynchronized;
        private bool _fullDecodeDiscoverySynchronized;
        private bool _stage2LogQueued;
        private bool _stage3LogQueued;
        private bool _stage4LogQueued;
        private bool _atlasCoreAupCached;
        private uint _stage2EncryptedLogHash;
        private uint _stage3EncryptedLogHash;
        private uint _stage4EncryptedLogHash;
        private uint _stage2EncryptedLogDiscoveryHash;
        private uint _stage3EncryptedLogDiscoveryHash;
        private uint _stage4EncryptedLogDiscoveryHash;

        private const int FormalDetectionRevealStage = 2;
        private const int IdentityRevealStage = 3;
        private const int FullDecodeRevealStage = 4;
        private const double SlowTickBudgetMilliseconds = 0.2d;
        private const float AtlasRevealPingDurationSeconds = 0.09f;
        private const float AtlasRevealPingTransmission01 = 0.72f;
        private const float AtlasRevealPingLowPassCutoffHz = 4200f;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string SignalFirstDetectedLog = "[AtlasSignal] Signal first detected.";
        private const string SignalPulseLog = "[AtlasSignal] Pulse emitted.";
        private const string SignalDecodedLog = "[AtlasSignal] Signal decoded.";
        private const string RevealStageUnlockedLog = "[AtlasSignal] Reveal stage unlocked.";
        private static readonly uint _signalIdentityDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalIdentityDiscoveryId);
        private static readonly uint _signalFullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _signalFullyDecodedMessageHash = AtlasSignalEvents.ComputeMessageHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _AudioLogRuntimeMissingWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.AudioLogRuntimeMissing"));
        private static readonly uint _EncryptedLogFallbackWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.EncryptedLogFallback"));
        private static readonly uint _DuplicateRuntimeWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.DuplicateRuntime"));
        private static readonly uint _SlowTickBudgetWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.SlowTickBudgetExceeded"));
        private static readonly uint _AtlasSignalContextHash = unchecked((uint)LocHash.Compute("AtlasSignalSystem"));

        private static readonly int _ShaderSignalStrength =
            Shader.PropertyToID("_AtlasSignalStrength");

        // Throttle log — static field, не в hot path
        private static float _nextSignalLogTime;

        private const float StrengthEpsilon = 0.01f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float CurrentStrength => _currentStrength;
        public int CurrentStrengthBand => _currentStrengthBand;
        public bool IsDetected =>
            _maxRevealStageUnlocked >= FormalDetectionRevealStage &&
            _currentStrength >= detectionThreshold;
        public Vector3 AtlasCorePosition => atlasCorePosWorld;

        public AbsoluteUniversePosition AtlasCoreAup => ResolveAtlasCoreAup();
        public int CurrentRevealStage => _maxRevealStageUnlocked;

        /// <summary>
        /// Направление к ядру Атлас-6 от текущей позиции игрока.
        /// Используется сканером для навигации.
        /// </summary>
        public Vector3 DirectionToCore
        {
            get
            {
                if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                    return Vector3.down;

                AbsoluteUniversePosition coreAup = ResolveAtlasCoreAup();
                return SignalStrengthSystem.CalculateDirectionToCore(in playerAup, in coreAup);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 8;
        public int LoadPriority => 8;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            CacheEncryptedLogHashes();
            TryRegisterService();
            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();

        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            long solveStartTicks = Stopwatch.GetTimestamp();
            try
            {
                SlowTickCore();
            }
            finally
            {
                PublishSlowTickBudgetIfNeeded(solveStartTicks);
            }
        }

        private void SlowTickCore()
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                ClearLiveSignalState();
                return;
            }

            _pulseTimer += 0.5f; // SlowTick ~0.5s

            AbsoluteUniversePosition coreAup = ResolveAtlasCoreAup();
            float rawStrength = CalculateRawStrength(in playerAup, in coreAup);
            int previousRevealStage = _maxRevealStageUnlocked;
            int desiredRevealStage = ResolveDesiredRevealStage(ResolveCurrentDepthMeters(in playerAup));
            if (desiredRevealStage > _maxRevealStageUnlocked)
                _maxRevealStageUnlocked = desiredRevealStage;

            float newStrength = math.min(rawStrength, ResolveRevealStrengthCap(_maxRevealStageUnlocked));
            _currentStrengthBand = math.min(
                SignalStrengthSystem.StrengthToBand(newStrength),
                math.clamp(_maxRevealStageUnlocked, 0, FullDecodeRevealStage));

            // Публикуем изменение силы
            if (math.abs(newStrength - _lastPublishedStrength) > StrengthEpsilon)
            {
                _currentStrength = newStrength;
                _lastPublishedStrength = newStrength;
                AtlasSignalEvents.RaiseStrengthChanged(newStrength);

                // Первое обнаружение
                if (!_signalEverDetected &&
                    newStrength >= detectionThreshold &&
                    _maxRevealStageUnlocked >= FormalDetectionRevealStage)
                {
                    _signalEverDetected = true;
                    AtlasSignalEvents.RaiseDetected(atlasCorePosWorld);
                    LogSignalFirstDetected();
                }

                // Шейдер
                if (publishToShader)
                    Shader.SetGlobalFloat(_ShaderSignalStrength, newStrength);
            }

            if (_maxRevealStageUnlocked > previousRevealStage)
                HandleRevealStageUnlocked(_maxRevealStageUnlocked, newStrength);

            TryEnsureIdentityDiscoveryPublished();

            // Пульс
            if (_maxRevealStageUnlocked <= 0)
                return;

            if (_pulseTimer < pulsePeriodSeconds)
                return;

            _pulseTimer = 0f;
            float pulseIntensity = _currentStrength;
            AtlasSignalEvents.RaisePulse(pulseIntensity);

            LogSignalPulse();
        }

        private void ClearLiveSignalState()
        {
            bool hadLiveStrength =
                math.abs(_currentStrength) > StrengthEpsilon ||
                math.abs(_lastPublishedStrength) > StrengthEpsilon ||
                _currentStrengthBand != 0;

            if (!hadLiveStrength)
                return;

            _currentStrength = 0f;
            _lastPublishedStrength = 0f;
            _currentStrengthBand = 0;
            AtlasSignalEvents.RaiseStrengthChanged(0f);

            if (publishToShader)
                Shader.SetGlobalFloat(_ShaderSignalStrength, 0f);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается когда игрок достигает ядра и расшифровывает сигнал.
        /// </summary>
        public void DecodeSignal(string messageId)
        {
            DecodeSignal(AtlasSignalEvents.ComputeMessageHash(messageId));
        }

        public void DecodeSignal(uint messageHash)
        {
            if (messageHash == 0u)
                return;

            AtlasSignalEvents.RaiseDecoded(messageHash);
            if (messageHash == _signalFullyDecodedMessageHash)
            {
                if (_maxRevealStageUnlocked < FullDecodeRevealStage)
                    _maxRevealStageUnlocked = FullDecodeRevealStage;

                TryEnsureFullDecodeDiscoveryPublished();
            }

            LogSignalDecoded();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayer()
        {
            _playerMovement = null;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                ResolvePlayer();
                if (_playerMovement == null)
                {
                    playerAup = default;
                    return false;
                }
            }

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        private AbsoluteUniversePosition ResolveAtlasCoreAup()
        {
            if (!_atlasCoreAupCached || _atlasCoreAupSource != atlasCorePosWorld)
            {
                _atlasCoreAupSource = atlasCorePosWorld;
                _atlasCoreAup = AbsoluteUniversePosition.FromRuntimePosition(atlasCorePosWorld);
                _atlasCoreAupCached = true;
            }

            return _atlasCoreAup;
        }

        private float ResolveCurrentDepthMeters(in AbsoluteUniversePosition playerAup)
        {
            BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (biomeMatrixDirector != null)
                return biomeMatrixDirector.CurrentDepthMeters;

            double absoluteY = playerAup.ToAbsoluteDouble3().y;
            return math.max(0f, (float)-absoluteY);
        }

        private float CalculateRawStrength(in AbsoluteUniversePosition playerAup, in AbsoluteUniversePosition coreAup)
        {
            return SignalStrengthSystem.CalculateStrength(in playerAup, in coreAup, maxSignalRange);
        }

        private int ResolveDesiredRevealStage(float currentDepthMeters)
        {
            if (CanManifestAtlas())
            {
                if (currentDepthMeters >= revealStage4Depth)
                    return 4;

                if (currentDepthMeters >= revealStage3Depth)
                    return 3;

                if (currentDepthMeters >= revealStage2Depth)
                    return 2;

                if (currentDepthMeters >= revealStage1Depth)
                    return 1;

                return 0;
            }

            if (CanManifestGhostBeat() && currentDepthMeters >= revealStage1Depth)
                return 1;

            return 0;
        }

        private float ResolveRevealStrengthCap(int revealStage)
        {
            return revealStage switch
            {
                1 => 0.08f,
                2 => 0.34f,
                3 => 0.78f,
                4 => 1f,
                _ => 0f
            };
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.AtlasSignal != null && !ReferenceEquals(GlobalRegistry.AtlasSignal, this))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_DuplicateRuntimeWarningHash, _AtlasSignalContextHash, 1f);
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAtlasSignalRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignal, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.AtlasSignal, this))
                GlobalRegistry.UnregisterAtlasSignalRuntime(this);

            _serviceRegistered = false;
        }

        private bool CanManifestAtlas()
        {
            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestoneToManifest);
        }

        private bool CanManifestGhostBeat()
        {
            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector == null)
                return false;

            if (firstHourDirector.IsMilestoneComplete(minimumMilestoneToManifest))
                return false;

            return firstHourDirector.IsMilestoneComplete(minimumMilestoneToGhostManifest);
        }

        private void HandleRevealStageUnlocked(int revealStage, float manifestedStrength)
        {
            if (manifestedStrength <= 0f)
                return;

            _pulseTimer = 0f;
            AtlasSignalEvents.RaisePulse(manifestedStrength);
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                atlasCorePosWorld,
                math.saturate(manifestedStrength),
                AtlasRevealPingDurationSeconds,
                AtlasRevealPingTransmission01,
                AtlasRevealPingLowPassCutoffHz,
                ProceduralAudioPingKind.Sonar);

            switch (revealStage)
            {
                case 1:
                    if (!CanManifestAtlas() && !_ghostManifestationAnnounced)
                    {
                        _ghostManifestationAnnounced = true;
                    }
                    break;

                case 2:
                    if (!_signalEverDetected && manifestedStrength >= detectionThreshold)
                    {
                        _signalEverDetected = true;
                        AtlasSignalEvents.RaiseDetected(atlasCorePosWorld);
                    }

                    TryQueueEncryptedLog(2);
                    NotificationEvents.PushInfo(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_2,
                        "WEAK RHYTHMIC PATTERN CONFIRMED. CONTACT STILL UNSTABLE."));
                    break;

                case 3:
                    TryEnsureIdentityDiscoveryPublished();
                    TryQueueEncryptedLog(3);
                    NotificationEvents.PushWarning(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_3,
                        "THE SIGNAL IS STARTING TO RETURN CONTENT FRAGMENTS. DEPTH IS CLEANING THE BEARING."));
                    break;

                case 4:
                    TryEnsureFullDecodeDiscoveryPublished();
                    TryQueueEncryptedLog(4);
                    NotificationEvents.PushWarning(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_4,
                        "CARRIER STABLE. THE SIGNAL CAN NOW BE DRIVEN ALL THE WAY TO THE SOURCE."));
                    break;
            }

            LogRevealStageUnlocked();
        }

        private void TryEnsureIdentityDiscoveryPublished()
        {
            if (_identityDiscoverySynchronized || _maxRevealStageUnlocked < IdentityRevealStage)
                return;

            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (narrativeDirector == null)
                return;

            if (!narrativeDirector.HasDiscovery(_signalIdentityDiscoveryHash))
                NarrativeEvents.RaiseDiscoveryMade(_signalIdentityDiscoveryHash);

            _identityDiscoverySynchronized = true;
        }

        private void TryEnsureFullDecodeDiscoveryPublished()
        {
            if (_fullDecodeDiscoverySynchronized || _maxRevealStageUnlocked < FullDecodeRevealStage)
                return;

            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (narrativeDirector != null && narrativeDirector.HasDiscovery(_signalFullyDecodedDiscoveryHash))
            {
                _fullDecodeDiscoverySynchronized = true;
                return;
            }

            NarrativeEvents.RaiseDiscoveryMade(_signalFullyDecodedDiscoveryHash);
            _fullDecodeDiscoverySynchronized = true;
        }

        private void TryQueueEncryptedLog(int revealStage)
        {
            uint logHash;
            uint fallbackLogHash;
            switch (revealStage)
            {
                case 2:
                    if (_stage2LogQueued)
                        return;
                    _stage2LogQueued = true;
                    logHash = _stage2EncryptedLogHash;
                    fallbackLogHash = _stage2EncryptedLogDiscoveryHash;
                    break;

                case 3:
                    if (_stage3LogQueued)
                        return;
                    _stage3LogQueued = true;
                    logHash = _stage3EncryptedLogHash;
                    fallbackLogHash = _stage3EncryptedLogDiscoveryHash;
                    break;

                case 4:
                    if (_stage4LogQueued)
                        return;
                    _stage4LogQueued = true;
                    logHash = _stage4EncryptedLogHash;
                    fallbackLogHash = _stage4EncryptedLogDiscoveryHash;
                    break;

                default:
                    return;
            }

            if (logHash == 0u)
                return;

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs != null)
            {
                if (audioLogs.TryPlayLogByHash(logHash))
                    return;

                if ((audioLogs.GetRecoveredEncryptedBits(logHash) & 0xFu) != 0xFu)
                    return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                audioLogs == null ? _AudioLogRuntimeMissingWarningHash : _EncryptedLogFallbackWarningHash,
                _AtlasSignalContextHash,
                revealStage);
            NarrativeEvents.RaiseDiscoveryMade(fallbackLogHash);
        }

        private void CacheEncryptedLogHashes()
        {
            _stage2EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage2EncryptedLogId);
            _stage3EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage3EncryptedLogId);
            _stage4EncryptedLogHash = QuestFlagHashKernel.ComputeStableHash(stage4EncryptedLogId);
            _stage2EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage2EncryptedLogId);
            _stage3EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage3EncryptedLogId);
            _stage4EncryptedLogDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(stage4EncryptedLogId);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFirstDetected()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(SignalFirstDetectedLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalPulse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.time < _nextSignalLogTime)
                return;

            _nextSignalLogTime = Time.time + 5f;
            Debug.Log(SignalPulseLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalDecoded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(SignalDecodedLog);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRevealStageUnlocked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(RevealStageUnlockedLog);
#endif
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        private static void PublishSlowTickBudgetIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - solveStartTicks;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= SlowTickBudgetMilliseconds)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _SlowTickBudgetWarningHash,
                _AtlasSignalContextHash,
                (float)elapsedMilliseconds);
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.atlasSignalDetected = _signalEverDetected;
            data.atlasSignalPulseTimer = _pulseTimer;
            data.atlasSignalRevealStage = _maxRevealStageUnlocked;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _signalEverDetected = data.atlasSignalDetected;
            _pulseTimer = data.atlasSignalPulseTimer;
            _maxRevealStageUnlocked = math.clamp(data.atlasSignalRevealStage, 0, 4);
            _ghostManifestationAnnounced = _maxRevealStageUnlocked > 0 && !_signalEverDetected;
            _identityDiscoverySynchronized = _maxRevealStageUnlocked >= IdentityRevealStage;
            _fullDecodeDiscoverySynchronized = _maxRevealStageUnlocked >= FullDecodeRevealStage;
            if (_signalEverDetected && _maxRevealStageUnlocked < FormalDetectionRevealStage)
                _maxRevealStageUnlocked = FormalDetectionRevealStage;

            if (_maxRevealStageUnlocked >= FormalDetectionRevealStage)
                _signalEverDetected = true;

            _stage2LogQueued = _maxRevealStageUnlocked >= 2;
            _stage3LogQueued = _maxRevealStageUnlocked >= 3;
            _stage4LogQueued = _maxRevealStageUnlocked >= 4;
        }
    }

    internal static class SignalStrengthSystem
    {
        private const float StrengthBandOneThreshold = 0.001f;
        private const float StrengthBandTwoThreshold = 0.25f;
        private const float StrengthBandThreeThreshold = 0.5f;
        private const float StrengthBandFourThreshold = 0.75f;

        public static float CalculateStrength(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup,
            float maxRangeMeters)
        {
            double safeRange = math.max(0.001f, maxRangeMeters);
            double safeRangeSq = safeRange * safeRange;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            if (distanceSq >= safeRangeSq)
                return 0f;

            return math.saturate((float)(1d - distanceSq / safeRangeSq));
        }

        public static int CalculateStrengthBand(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup,
            float maxRangeMeters)
        {
            return StrengthToBand(CalculateStrength(in playerAup, in coreAup, maxRangeMeters));
        }

        public static int StrengthToBand(float strength01)
        {
            float strength = math.saturate(strength01);
            if (strength < StrengthBandOneThreshold)
                return 0;
            if (strength < StrengthBandTwoThreshold)
                return 1;
            if (strength < StrengthBandThreeThreshold)
                return 2;
            if (strength < StrengthBandFourThreshold)
                return 3;

            return 4;
        }

        public static double CalculateDistanceSqMeters(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup)
        {
            return AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
        }

        public static Vector3 CalculateDirectionToCore(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup)
        {
            double3 delta = coreAup.ToAbsoluteDouble3() - playerAup.ToAbsoluteDouble3();
            double lengthSq = math.lengthsq(delta);
            if (lengthSq <= 0.000001d)
                return Vector3.down;

            double invLength = math.rsqrt(lengthSq);
            return new Vector3(
                (float)(delta.x * invLength),
                (float)(delta.y * invLength),
                (float)(delta.z * invLength));
        }
    }
}
