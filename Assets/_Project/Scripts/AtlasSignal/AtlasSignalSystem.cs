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
//   • ISlowTickable — таймер без Update().
//   • Никаких new/LINQ в hot path.
//   • Shader.SetGlobalFloat для визуального отклика биолюминесценции.
// ============================================================================

using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Narrative;
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

        private Transform _playerTransform;
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

        private const int FormalDetectionRevealStage = 2;
        private const int IdentityRevealStage = 3;
        private const int FullDecodeRevealStage = 4;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string SignalFirstDetectedLog = "[AtlasSignal] Signal first detected.";
        private const string SignalPulseLog = "[AtlasSignal] Pulse emitted.";
        private const string SignalDecodedLog = "[AtlasSignal] Signal decoded.";
        private const string RevealStageUnlockedLog = "[AtlasSignal] Reveal stage unlocked.";
        private static readonly uint _AudioLogRuntimeMissingWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.AudioLogRuntimeMissing"));
        private static readonly uint _EncryptedLogFallbackWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.EncryptedLogFallback"));
        private static readonly uint _DuplicateRuntimeWarningHash = unchecked((uint)LocHash.Compute("AtlasSignal.DuplicateRuntime"));
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
        public int CurrentRevealStage => _maxRevealStageUnlocked;

        /// <summary>
        /// Направление к ядру Атлас-6 от текущей позиции игрока.
        /// Используется сканером для навигации.
        /// </summary>
        public Vector3 DirectionToCore
        {
            get
            {
                if (_playerTransform == null) return Vector3.down;
                return SignalStrengthSystem.CalculateDirectionToCore(_playerTransform.position, atlasCorePosWorld);
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
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            _pulseTimer += 0.5f; // SlowTick ~0.5s

            float rawStrength = CalculateRawStrength();
            int previousRevealStage = _maxRevealStageUnlocked;
            int desiredRevealStage = ResolveDesiredRevealStage(ResolveCurrentDepthMeters());
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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается когда игрок достигает ядра и расшифровывает сигнал.
        /// </summary>
        public void DecodeSignal(string messageId)
        {
            AtlasSignalEvents.RaiseDecoded(messageId);
            if (messageId == SignalFullyDecodedDiscoveryId)
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
            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private float ResolveCurrentDepthMeters()
        {
            BiomeMatrixDirector biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            if (biomeMatrixDirector != null)
                return biomeMatrixDirector.CurrentDepthMeters;

            return math.max(0f, -_playerTransform.position.y);
        }

        private float CalculateRawStrength()
        {
            return SignalStrengthSystem.CalculateStrength(_playerTransform.position, atlasCorePosWorld, maxSignalRange);
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

            if (!narrativeDirector.HasDiscovery(SignalIdentityDiscoveryId))
                NarrativeEvents.RaiseDiscoveryMade(SignalIdentityDiscoveryId);

            _identityDiscoverySynchronized = true;
        }

        private void TryEnsureFullDecodeDiscoveryPublished()
        {
            if (_fullDecodeDiscoverySynchronized || _maxRevealStageUnlocked < FullDecodeRevealStage)
                return;

            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (narrativeDirector != null && narrativeDirector.HasDiscovery(SignalFullyDecodedDiscoveryId))
            {
                _fullDecodeDiscoverySynchronized = true;
                return;
            }

            NarrativeEvents.RaiseDiscoveryMade(SignalFullyDecodedDiscoveryId);
            _fullDecodeDiscoverySynchronized = true;
        }

        private void TryQueueEncryptedLog(int revealStage)
        {
            string logId;
            switch (revealStage)
            {
                case 2:
                    if (_stage2LogQueued)
                        return;
                    _stage2LogQueued = true;
                    logId = stage2EncryptedLogId;
                    break;

                case 3:
                    if (_stage3LogQueued)
                        return;
                    _stage3LogQueued = true;
                    logId = stage3EncryptedLogId;
                    break;

                case 4:
                    if (_stage4LogQueued)
                        return;
                    _stage4LogQueued = true;
                    logId = stage4EncryptedLogId;
                    break;

                default:
                    return;
            }

            if (string.IsNullOrWhiteSpace(logId))
                return;

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs != null && audioLogs.TryPlayLogById(logId))
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                audioLogs == null ? _AudioLogRuntimeMissingWarningHash : _EncryptedLogFallbackWarningHash,
                _AtlasSignalContextHash,
                revealStage);
            NarrativeEvents.RaiseDiscoveryMade(logId);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFirstDetected()
        {
            Debug.Log(SignalFirstDetectedLog);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalPulse()
        {
            if (Time.time < _nextSignalLogTime)
                return;

            _nextSignalLogTime = Time.time + 5f;
            Debug.Log(SignalPulseLog);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalDecoded()
        {
            Debug.Log(SignalDecodedLog);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRevealStageUnlocked()
        {
            Debug.Log(RevealStageUnlockedLog);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
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

        public static float CalculateStrength(Vector3 playerRuntimePosition, Vector3 coreRuntimePosition, float maxRangeMeters)
        {
            float safeRange = math.max(0.001f, maxRangeMeters);
            float distance = CalculateDistanceMeters(playerRuntimePosition, coreRuntimePosition);
            if (distance >= safeRange)
                return 0f;

            return math.saturate(1f - (distance / safeRange));
        }

        public static int CalculateStrengthBand(Vector3 playerRuntimePosition, Vector3 coreRuntimePosition, float maxRangeMeters)
        {
            return StrengthToBand(CalculateStrength(playerRuntimePosition, coreRuntimePosition, maxRangeMeters));
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

        public static float CalculateDistanceMeters(Vector3 playerRuntimePosition, Vector3 coreRuntimePosition)
        {
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerRuntimePosition);
            AbsoluteUniversePosition coreAup = AbsoluteUniversePosition.FromRuntimePosition(coreRuntimePosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            return distanceSq > 0d ? (float)math.sqrt(distanceSq) : 0f;
        }

        public static Vector3 CalculateDirectionToCore(Vector3 playerRuntimePosition, Vector3 coreRuntimePosition)
        {
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerRuntimePosition);
            AbsoluteUniversePosition coreAup = AbsoluteUniversePosition.FromRuntimePosition(coreRuntimePosition);
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
