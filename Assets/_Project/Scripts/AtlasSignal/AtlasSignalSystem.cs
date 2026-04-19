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
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
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

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static AtlasSignalSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private float _pulseTimer;
        private float _currentStrength;
        private float _lastPublishedStrength;
        private bool _signalEverDetected;
        private int _maxRevealStageUnlocked;
        private bool _registered;
        private bool _ghostManifestationAnnounced;
        private bool _identityDiscoverySynchronized;

        private const int FormalDetectionRevealStage = 2;
        private const int IdentityRevealStage = 3;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";

        private static readonly int _ShaderSignalStrength =
            Shader.PropertyToID("_AtlasSignalStrength");

        // Throttle log — static field, не в hot path
        private static float _nextSignalLogTime;

        private const float StrengthEpsilon = 0.01f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float CurrentStrength => _currentStrength;
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
                Vector3 toCore = atlasCorePosWorld - _playerTransform.position;
                float mag = toCore.magnitude;
                return mag > 0.001f ? toCore / mag : Vector3.down;
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
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

            float newStrength = Mathf.Min(rawStrength, ResolveRevealStrengthCap(_maxRevealStageUnlocked));

            // Публикуем изменение силы
            if (Mathf.Abs(newStrength - _lastPublishedStrength) > StrengthEpsilon)
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
                    LogSignalFirstDetected(newStrength);
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

            LogSignalPulse(pulseIntensity,
                Vector3.Distance(_playerTransform.position, atlasCorePosWorld));
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

            LogSignalDecoded(messageId);
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

            return Mathf.Max(0f, -_playerTransform.position.y);
        }

        private float CalculateRawStrength()
        {
            float dist = Vector3.Distance(_playerTransform.position, atlasCorePosWorld);
            if (dist >= maxSignalRange)
                return 0f;

            return 1f - (dist / maxSignalRange);
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
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private bool CanManifestAtlas()
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestoneToManifest);
        }

        private bool CanManifestGhostBeat()
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
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

                    NotificationEvents.PushInfo(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_2,
                        "WEAK RHYTHMIC PATTERN CONFIRMED. CONTACT STILL UNSTABLE."));
                    break;

                case 3:
                    TryEnsureIdentityDiscoveryPublished();
                    NotificationEvents.PushWarning(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_3,
                        "THE SIGNAL IS STARTING TO RETURN CONTENT FRAGMENTS. DEPTH IS CLEANING THE BEARING."));
                    break;

                case 4:
                    NotificationEvents.PushWarning(ResolveLocalized(
                        LocalizationKeys.ATLAS_SIGNAL_REVEAL_STAGE_4,
                        "CARRIER STABLE. THE SIGNAL CAN NOW BE DRIVEN ALL THE WAY TO THE SOURCE."));
                    break;
            }

            LogRevealStageUnlocked(revealStage, manifestedStrength);
        }

        private void TryEnsureIdentityDiscoveryPublished()
        {
            if (_identityDiscoverySynchronized || _maxRevealStageUnlocked < IdentityRevealStage)
                return;

            HectonNarrativeDirector narrativeDirector = HectonNarrativeDirector.Instance;
            if (narrativeDirector == null)
                return;

            if (!narrativeDirector.HasDiscovery(SignalIdentityDiscoveryId))
                NarrativeEvents.RaiseDiscoveryMade(SignalIdentityDiscoveryId);

            _identityDiscoverySynchronized = true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFirstDetected(float strength)
        {
            Debug.Log($"[AtlasSignal] Signal first detected. Strength: {strength:F2}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalPulse(float pulseIntensity, float distanceToCore)
        {
            if (Time.time < _nextSignalLogTime)
                return;

            _nextSignalLogTime = Time.time + 5f;
            Debug.Log($"[AtlasSignal] Pulse intensity: {pulseIntensity:F2} (dist to core: {distanceToCore:F0}m)");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalDecoded(string messageId)
        {
            Debug.Log($"[AtlasSignal] Signal decoded: {messageId}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRevealStageUnlocked(int revealStage, float manifestedStrength)
        {
            Debug.Log($"[AtlasSignal] Reveal stage {revealStage} unlocked. Manifested strength: {manifestedStrength:F2}");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
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
            _maxRevealStageUnlocked = Mathf.Clamp(data.atlasSignalRevealStage, 0, 4);
            _ghostManifestationAnnounced = _maxRevealStageUnlocked > 0 && !_signalEverDetected;
            _identityDiscoverySynchronized = _maxRevealStageUnlocked < IdentityRevealStage;
            if (_signalEverDetected && _maxRevealStageUnlocked < FormalDetectionRevealStage)
                _maxRevealStageUnlocked = FormalDetectionRevealStage;

            if (_maxRevealStageUnlocked >= FormalDetectionRevealStage)
                _signalEverDetected = true;
        }
    }
}
