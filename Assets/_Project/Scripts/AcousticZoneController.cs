// ============================================================================
// HECTON-8 — AcousticZoneController.cs
// Управление акустическими зонами: плавный переход аудио между
// открытым океаном и сухими зонами внутри модулей базы.
//
// АРХИТЕКТУРА:
//   • Синглтон, ITickable — проверка состояния игрока каждый кадр.
//   • Edge detection: переход запускается ТОЛЬКО при смене состояния
//     (вода → суша или суша → вода). Один bool-comparison per frame.
//   • AudioMixerSnapshot.TransitionTo — плавный кроссфейд пресетов.
//   • SpatialAudioManager — воспроизведение переходных звуков (drain/fill).
//
// СТОЛП 1 — ТЕХНОЛОГИЧЕСКИЙ УЮТ:
//   Внутри базы: тишина, гул генераторов, шаги по металлу.
//   Снаружи: давящий низкочастотный гул, бульканье, эхо глубины.
//   Контраст создаётся через AudioMixer Snapshots:
//     UnderwaterSnapshot: Low-Pass Filter (LPF) на Master,
//       Reverb (Large Hall), приглушённые высокие частоты.
//     BaseInteriorSnapshot: LPF снят, Reverb (Small Room / Metallic),
//       чистые средние частоты, лёгкий механический гул.
//
// ИНТЕГРАЦИЯ:
//   • Читает BuoyancyObject.IsInAir через кэшированную ссылку.
//   • BuoyancyObject.IsInAir = true → игрок внутри сухого модуля.
//   • BuoyancyObject.IsInAir = false → игрок в воде.
//   • Ленивый resolve игрока через SceneBootstrap (один раз).
//
// TRANSITION FLOW:
//   FixedTick: BuoyancyObject.IsInAir changes
//     → Tick: AcousticZoneController detects edge
//       → snapshot.TransitionTo(transitionDuration)
//       → SpatialAudioManager.PlayStatic2D(transitionClip)
//       → Optional: event OnAcousticZoneChanged(isInterior)
//
// ZERO GC:
//   • Tick: один bool comparison + edge detection. Zero alloc.
//   • TransitionTo: Unity internal, no managed alloc.
//   • PlayStatic2D: пул 2D-голосов SpatialAudioManager.
//   • Ленивый resolve игрока через SceneBootstrap.
//   • Нет Update, нет корутин, нет LINQ.
//
// CPU COST:
//   ~0.0001ms per Tick (one bool read + comparison).
//   Transition itself is handled by Unity AudioMixer internally.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)] // После FluidEngine (-5000), до большинства систем
    public sealed class AcousticZoneController : MonoBehaviour, ITickable
    {
#if UNITY_EDITOR
        private const string DefaultWaterDrainSoundPath = "Assets/_Project/Audio/Movement/swimming -onwater.wav";
        private const string DefaultWaterFillSoundPath = "Assets/_Project/Audio/Movement/swimming - underwater.ogg";
        private const string DefaultMasterMixerPath = "Assets/_Project/MasterMixer.mixer";
#endif

        private enum AcousticZoneState : byte
        {
            Surface = 0,
            Underwater = 1,
            Interior = 2
        }

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static AcousticZoneController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnAcousticZoneChanged = null;
        }

        public static AcousticZoneController Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GLOBAL EVENT — ACOUSTIC ZONE CHANGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when player transitions between acoustic zones.
        /// Parameter: true = interior (dry, inside base), false = underwater.
        ///
        /// Subscribers (future):
        ///   - Ambient sound layers (start/stop underwater drone).
        ///   - Footstep system (switch to metal footsteps).
        ///   - Music system (switch between exploration/safety tracks).
        ///   - VFX (water droplets on helmet when exiting water).
        /// </summary>
        public static event Action<bool> OnAcousticZoneChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SNAPSHOTS
        // ══════════════════════════════════════════════════════════

        [Header("── AudioMixer Snapshots ──────────────────────")]
        [Tooltip("Snapshot для подводной среды.\n" +
                 "Настройки: Low-Pass Filter, Reverb (Large Hall),\n" +
                 "приглушённые высокие, усиленные низкие.")]
        [SerializeField] private AudioMixerSnapshot underwaterSnapshot;

        [Tooltip("Snapshot для интерьера базы.\n" +
                 "Настройки: LPF снят, Reverb (Small Room),\n" +
                 "чистые средние, лёгкий механический гул.")]
        [SerializeField] private AudioMixerSnapshot baseInteriorSnapshot;

        [Tooltip("Optional MasterMixer asset used to auto-resolve authored snapshot refs by name in cold path/editor.")]
        [SerializeField] private AudioMixer masterMixer;

        [SerializeField] private AudioMixerSnapshot surfaceSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceRainSnapshot;
        [SerializeField] private AudioMixerSnapshot surfaceStormSnapshot;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Settings ───────────────────────")]
        [Tooltip("Время перехода между snapshot'ами (секунды).\n" +
                 "2.0 = плавный кроссфейд, имитирующий откачку воды.\n" +
                 "0.5 = быстрый переход для тестирования.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Время перехода в интерьер базы. Даёт отдельный control над dry-zone LPF/reverb response.")]
        [SerializeField] private float interiorTransitionDuration = 2.0f;

        [Tooltip("Время перехода к обычному surface snapshot без погодного перебленда.")]
        [SerializeField] private float surfaceTransitionDuration = 2.0f;

        [Tooltip("Время перехода при входе в воду (может быть быстрее,\n" +
                 "т.к. 'вода заполняет шлюз' мгновеннее, чем 'откачка').")]
        [SerializeField] private float underwaterTransitionDuration = 1.5f;

        [Tooltip("Время weather-перебленда для Surface/Rain/Storm snapshots.")]
        [SerializeField] private float surfaceWeatherTransitionDuration = 1.0f;

        [Tooltip("Вес Rain snapshot в Surface weather mix. Управляет perceived wet layer без правки кода.")]
        [SerializeField, Range(0f, 1f)] private float surfaceRainSnapshotWeight = 0.55f;

        [Tooltip("Вес Storm snapshot в Surface weather mix. Управляет интенсивностью storm wet layer.")]
        [SerializeField, Range(0f, 1f)] private float surfaceStormSnapshotWeight = 0.8f;

        [Header("── Exterior State Stability ───────────────────────")]
        [Tooltip("Глубина входа в подводное акустическое состояние.\n" +
                 "Держится выше визуального порога, чтобы акустика не дрожала на ряби у поверхности.")]
        [SerializeField] private float acousticEnterUnderwaterDepth = SurfaceStateUtility.EnterUnderwaterDepth;

        [Tooltip("Глубина выхода из подводного акустического состояния.\n" +
                 "Должна быть ниже enter-порога, чтобы сохранить hysteresis.")]
        [SerializeField] private float acousticExitUnderwaterDepth = SurfaceStateUtility.ExitUnderwaterDepth;
        [SerializeField, Range(0.1f, 1f)] private float acousticEnterImmersionRatio = 0.82f;
        [SerializeField, Range(0.05f, 0.95f)] private float acousticExitImmersionRatio = 0.6f;
        [SerializeField] private float acousticForceUnderwaterDepth = 1.1f;

        [Tooltip("Минимальное время подтверждения для переключения между Surface и Underwater.\n" +
                 "Interior переключается без задержки.")]
        [SerializeField] private float exteriorTransitionDebounce = 0.35f;

        [Tooltip("Минимальное время удержания внешнего акустического состояния после уже совершенного перехода.\n" +
                 "Не дает Surface/Underwater щелкать на пограничной болтанке у поверхности.")]
        [SerializeField] private float exteriorTransitionHoldTime = 1.25f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION SOUNDS
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Audio ──────────────────────────")]
        [Tooltip("Звук откачки воды (вход в сухую зону).\n" +
                 "Воспроизводится через SpatialAudioManager.PlayStatic2D\n" +
                 "(2D, 'внутри шлема'). Длительность ~2-3 секунды.")]
        [SerializeField] private AudioClip waterDrainSound;

        [Tooltip("Звук заполнения водой (выход в океан).\n" +
                 "Бульканье + давление + шипение.")]
        [SerializeField] private AudioClip waterFillSound;

        [Tooltip("Громкость переходных звуков [0..1].")]
        [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER REFERENCE
        // ══════════════════════════════════════════════════════════

        [Header("── Player ────────────────────────────────────")]
        [Tooltip("BuoyancyObject на игроке. Если не назначен —\n" +
                 "ищется автоматически по тегу 'Player' при старте.")]
        [SerializeField] private BuoyancyObject playerBuoyancy;

        [Tooltip("Опциональная ссылка на loop AudioSource с подводным эмбиентом на игроке.\n" +
                 "Если не задана — контроллер лениво ищет первый 2D loop/playOnAwake source под player root.")]
        [SerializeField] private AudioSource playerUnderwaterAmbientSource;

        [Header("── Biome Ambient Response ─────────────────────────")]
        [Tooltip("Опциональная ссылка на BiomeMatrixDirector. Если не задана — контроллер лениво резолвит runtime owner.")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        [Tooltip("Период повторной попытки резолва BiomeMatrixDirector в cold/runtime path.")]
        [SerializeField] private float biomeMatrixResolveRetryInterval = 1f;

        [Tooltip("Множитель громкости подводного loop в calm biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float calmAmbientVolumeScale = 0.84f;

        [Tooltip("Множитель громкости подводного loop в lively biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float livelyAmbientVolumeScale = 1.05f;

        [Tooltip("Множитель громкости подводного loop в mixed/neutral biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float mixedAmbientVolumeScale = 0.94f;

        [Tooltip("Множитель громкости подводного loop в hostile biome.")]
        [SerializeField, Range(0.25f, 1.5f)] private float hostileAmbientVolumeScale = 0.72f;

        [Tooltip("Множитель pitch подводного loop в calm biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmAmbientPitchScale = 1.02f;

        [Tooltip("Множитель pitch подводного loop в lively biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyAmbientPitchScale = 1.01f;

        [Tooltip("Множитель pitch подводного loop в mixed/neutral biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedAmbientPitchScale = 0.96f;

        [Tooltip("Множитель pitch подводного loop в hostile biome.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileAmbientPitchScale = 0.90f;

        [Header("── Soundscape Tier Response ────────────────────")]
        // Existing underwater acoustic owner consumes depth-band context directly.
        [Tooltip("Опциональная ссылка на SoundscapeSystem. Если не задана — контроллер лениво резолвит runtime owner.")]
        [SerializeField] private SoundscapeSystem soundscapeSystem;

        [Tooltip("Период повторной попытки резолва SoundscapeSystem в cold/runtime path.")]
        [SerializeField] private float soundscapeResolveRetryInterval = 1f;

        [Tooltip("Множитель громкости подводного loop в shallow tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float shallowTierAmbientVolumeScale = 1f;

        [Tooltip("Множитель громкости подводного loop в twilight tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float twilightTierAmbientVolumeScale = 0.94f;

        [Tooltip("Множитель громкости подводного loop в darkness tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float darknessTierAmbientVolumeScale = 0.88f;

        [Tooltip("Множитель громкости подводного loop в abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float abyssTierAmbientVolumeScale = 0.82f;

        [Tooltip("Множитель громкости подводного loop в deep abyss tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float deepAbyssTierAmbientVolumeScale = 0.74f;

        [Tooltip("Множитель громкости подводного loop в thermal tier.")]
        [SerializeField, Range(0.25f, 1.5f)] private float thermalTierAmbientVolumeScale = 0.86f;

        [Tooltip("Множитель pitch подводного loop в shallow tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float shallowTierAmbientPitchScale = 1f;

        [Tooltip("Множитель pitch подводного loop в twilight tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float twilightTierAmbientPitchScale = 0.97f;

        [Tooltip("Множитель pitch подводного loop в darkness tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float darknessTierAmbientPitchScale = 0.93f;

        [Tooltip("Множитель pitch подводного loop в abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float abyssTierAmbientPitchScale = 0.88f;

        [Tooltip("Множитель pitch подводного loop в deep abyss tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float deepAbyssTierAmbientPitchScale = 0.82f;

        [Tooltip("Множитель pitch подводного loop в thermal tier.")]
        [SerializeField, Range(0.5f, 1.5f)] private float thermalTierAmbientPitchScale = 0.9f;

        [Header("── Listener Fallback Processing ─────────────")]
        [Tooltip("If mixer snapshot authoring is incomplete, apply listener-level low-pass/reverb fallback so underwater/interior contrast still exists.")]
        [SerializeField] private bool enableSourceLevelAcousticFallback = true;

        [Tooltip("Fallback low-pass cutoff for underwater listener processing.")]
        [SerializeField, Range(500f, 22000f)] private float underwaterFallbackLowPassCutoff = 1100f;

        [Tooltip("Fallback low-pass cutoff for interior listener processing.")]
        [SerializeField, Range(5000f, 22000f)] private float interiorFallbackLowPassCutoff = 16000f;

        [Tooltip("Fallback reverb preset for interior listener processing.")]
        [SerializeField] private AudioReverbPreset interiorFallbackReverbPreset = AudioReverbPreset.Room;

        [Tooltip("Fallback interior reverb dry level. Exposed so sound design can retune dry/wet balance without code changes.")]
        [SerializeField, Range(-10000f, 0f)] private float interiorFallbackReverbDryLevel = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsInterior;
        [SerializeField] private bool _debugIsUnderwater;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private int  _debugTransitionCount;
        [SerializeField] private string _debugFaunaMood;
        [SerializeField] private string _debugAmbientSummary;
        [SerializeField] private string _debugSnapshotCoverage;
        [SerializeField] private string _debugMixerCoverage;
        [SerializeField] private float _debugAmbientVolume;
        [SerializeField] private float _debugAmbientPitch;
        [SerializeField] private string _debugSoundscapeTier;
        [SerializeField] private float _debugSoundscapeVolumeScale = 1f;
        [SerializeField] private float _debugSoundscapePitchScale = 1f;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Последнее известное состояние: true = интерьер (сухая зона).
        /// Используется для edge detection. -1-like: первый кадр
        /// определяет начальное состояние без запуска перехода.
        /// </summary>
        private AcousticZoneState _lastZone;

        /// <summary>
        /// Флаг: начальное состояние уже определено.
        /// false = первый Tick ещё не прошёл.
        /// Предотвращает ложный переход при старте.
        /// </summary>
        private bool _stateInitialized;

        /// <summary>
        /// Registration tracking для GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;
        private float _nextPlayerResolveTime;
        private const float PlayerResolveRetryInterval = 1f;
        private float _nextBiomeMatrixResolveTime;
        private float _nextSoundscapeResolveTime;
        private HectonAtmosphereManager _atmosphereManager;
        private HectonPlayerMovement _playerMovement;
        private bool _fallbackUnderwaterState;
        private bool _acousticUnderwaterState;
        private bool _hasPendingExteriorZone;
        private float _pendingExteriorZoneResolveTime;
        private AcousticZoneState _pendingExteriorZone;
        private float _nextExteriorTransitionAllowedTime;
        private bool _hasCachedExteriorZone;
        private AcousticZoneState _cachedExteriorZone;
        private List<AudioSource> _playerAudioSources;
        private AudioSource _cachedAmbientSource;
        private AudioListener _cachedPlayerAudioListener;
        private AudioLowPassFilter _listenerLowPassFilter;
        private AudioReverbFilter _listenerReverbFilter;
        private bool _ambientSourceDefaultsCaptured;
        private bool _listenerFallbackDefaultsCaptured;
        private float _ambientSourceBaseVolume = 1f;
        private float _ambientSourceBasePitch = 1f;
        private float _listenerLowPassBaseCutoff = 22000f;
        private float _listenerLowPassBaseResonance = 1f;
        private AudioReverbPreset _listenerReverbBasePreset = AudioReverbPreset.Off;
        private float _listenerReverbBaseDryLevel;
        private HectonBiomeMatrixProfile _lastBiomeProfileForAmbient;
        private int _currentAmbientSurvivalPressure;
        private int _currentAmbientRewardPull;
        private string _currentAmbientSummary;
        private float _currentAmbientVolumeScale = 1f;
        private float _currentAmbientPitchScale = 1f;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _currentSoundscapeVolumeScale = 1f;
        private float _currentSoundscapePitchScale = 1f;
        private float _surfacePrecipitationIntensity;
        private float _surfaceElectricalActivity;
        private bool _snapshotBindingsResolved;
        private bool _warnedMissingInteriorSnapshot;
        private bool _warnedMissingUnderwaterSnapshot;
        private bool _warnedMissingSurfaceSnapshotSet;
        private bool _warnedMissingSnapshotCoverage;
        private bool _warnedIncompleteMixerSnapshotAuthoring;
        private bool _warnedMissingMixerEffectGraph;
        private int _validatedMixerSnapshotCount;
        private bool _validatedMixerHasNamedCoverage;
        private bool _validatedMixerHasEffectGraph;
        private bool _usingSourceLevelAcousticFallback;
        // COLD ALLOC: AudioMixerSnapshot[3] — surface weather snapshot blend targets — owner: AcousticZoneController
        private readonly AudioMixerSnapshot[] _surfaceBlendSnapshots = new AudioMixerSnapshot[3];
        // COLD ALLOC: float[3] — surface weather snapshot blend weights — owner: AcousticZoneController
        private readonly float[] _surfaceBlendWeights = new float[3];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true если игрок сейчас в сухой зоне (интерьер базы).
        /// false если в воде.
        /// </summary>
        public bool IsInterior => _lastZone == AcousticZoneState.Interior;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _stateInitialized = false;
            _registeredToTickManager = false;
            // COLD ALLOC: List<AudioSource>[8] — reused player-local audio scan buffer — owner: AcousticZoneController
            _playerAudioSources = new List<AudioSource>(8);

#if UNITY_EDITOR
            TryAssignEditorAuthoringDefaults();
#endif
        }

        private void OnEnable()
        {
            TryRegister();
            HectonAtmosphereManager.OnStateChanged += HandleAtmosphereStateChanged;
            SoundscapeEvents.OnTierChanged += HandleSoundscapeTierChanged;
            ResolveBiomeMatrixDirector(true);
            RefreshSoundscapeTierContext(true);
        }

        private void Start()
        {
            // ── Ленивый поиск игрока ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(true);
            }

            // ── Deferred registration ──
            if (!_registeredToTickManager)
            {
                TryRegister();
            }

            if (!_registeredToTickManager)
            {
                Debug.LogError(
                    "[AcousticZoneController] GameTickManager not found at Start(). " +
                    "Acoustic transitions will NOT work.", this);
            }

            ResolvePlayerAmbientSource();
            ResolvePlayerListenerFilters();
            ResolveBiomeMatrixDirector(true);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(true);
            EnsureSnapshotBindings();
            RefreshAtmosphereZoneCache();

            // ── Установка начального snapshot без перехода ──
            ApplyInitialSnapshot();
        }

        private void OnDisable()
        {
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;
            SoundscapeEvents.OnTierChanged -= HandleSoundscapeTierChanged;
            ResetSourceLevelAcousticFallback();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            HectonAtmosphereManager.OnStateChanged -= HandleAtmosphereStateChanged;
            SoundscapeEvents.OnTierChanged -= HandleSoundscapeTierChanged;
            ResetSourceLevelAcousticFallback();

            if (_instance == this)
            {
                _instance = null;
                OnAcousticZoneChanged = null;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager) return;

            GameTickManager gtm = GameTickManager.Instance;
            if (gtm == null) return;

            gtm.Register((ITickable)this);
            _registeredToTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GameTickManager gtm = GameTickManager.Instance;
            if (gtm != null)
                gtm.Unregister((ITickable)this);

            _registeredToTickManager = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — ACOUSTIC ZONE DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет состояние игрока каждый кадр.
        /// Edge detection: переход запускается ТОЛЬКО при смене
        /// IsInAir (false→true или true→false).
        ///
        /// CPU cost: ~0.0001ms (один bool read + comparison).
        /// Нет аллокаций, нет сложной логики.
        ///
        /// Почему ITickable а не ISlowTickable:
        ///   Аудио-переход должен начинаться МГНОВЕННО при смене зоны.
        ///   Задержка 0.5с (SlowTick) заметна игроку — звук "запаздывает"
        ///   относительно визуального перехода через шлюз.
        ///   Один bool per frame — ничтожная нагрузка даже на MX350.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ── Ленивый поиск игрока (если ещё не найден) ──
            if (playerBuoyancy == null)
            {
                FindPlayerBuoyancy(false);
                if (playerBuoyancy == null)
                    return; // Игрок не найден — skip
            }

            // ── Unity destroyed object check ──
            if ((object)playerBuoyancy == null || playerBuoyancy == null)
            {
                playerBuoyancy = null;
                return;
            }

            // ── Текущее состояние ──
            AcousticZoneState currentZone = ResolveCurrentZone();
            currentZone = ResolveStableZone(currentZone);
            RefreshBiomeAmbientContext();
            RefreshSoundscapeTierContext(false);
            UpdateAmbientLoopMix(currentZone);

            // ── Первый кадр: установить начальное состояние без перехода ──
            if (!_stateInitialized)
            {
                ApplyInitialSnapshot(currentZone);
                return;
            }

            // ── Edge detection: переход только при СМЕНЕ состояния ──
            if (currentZone == _lastZone)
                return;

            // ══════════════════════════════════════════════
            //  TRANSITION DETECTED!
            // ══════════════════════════════════════════════

            _lastZone = currentZone;
            if (currentZone != AcousticZoneState.Interior)
                _nextExteriorTransitionAllowedTime = Time.unscaledTime + exteriorTransitionHoldTime;

            ApplyZoneTransition(currentZone);

            // ── Событие для внешних систем ──
            OnAcousticZoneChanged?.Invoke(currentZone == AcousticZoneState.Interior);

            UpdateDiagnostics(currentZone);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Плавный переход в интерьер базы.
        ///
        /// AudioMixerSnapshot.TransitionTo(timeToReach):
        ///   Плавно переводит AudioMixer в состояние snapshot'а
        ///   за указанное время. Unity внутренне интерполирует
        ///   ВСЕ параметры mixer'а (volume, LPF cutoff, reverb wet, etc.).
        ///   Zero GC — нативная операция.
        ///
        /// Переходный звук (waterDrainSound):
        ///   Воспроизводится через PlayStatic2D (2D, "внутри шлема").
        ///   Имитирует шипение откачиваемой воды из шлюза.
        ///   Длительность клипа должна примерно совпадать с transitionDuration.
        /// </summary>
        private void TransitionToInterior()
        {
            ApplyAmbientLoopState(AcousticZoneState.Interior);
            TransitionToResolvedSnapshot(AcousticZoneState.Interior, interiorTransitionDuration);

            // ── Переходный звук ──
            PlayTransitionSound(waterDrainSound);

            LogDiagnostic($"[AcousticZoneController] Interior (dry zone). Transition: {interiorTransitionDuration}s");
        }

        /// <summary>
        /// Плавный переход в подводную среду.
        ///
        /// underwaterTransitionDuration может быть короче transitionDuration,
        /// т.к. "заполнение водой" физически быстрее, чем "откачка".
        /// Это создаёт асимметричный, более реалистичный переход:
        ///   Вход в базу: 2.0с (медленная откачка, шипение)
        ///   Выход в воду: 1.5с (быстрое заполнение, бульканье)
        /// </summary>
        private void TransitionToSurface()
        {
            ApplyAmbientLoopState(AcousticZoneState.Surface);
            TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceTransitionDuration);

            LogDiagnostic($"[AcousticZoneController] Surface/open air. Transition: {surfaceTransitionDuration}s");
        }

        private void TransitionToUnderwater()
        {
            ApplyAmbientLoopState(AcousticZoneState.Underwater);
            TransitionToResolvedSnapshot(AcousticZoneState.Underwater, underwaterTransitionDuration);

            // ── Переходный звук ──
            PlayTransitionSound(waterFillSound);

            LogDiagnostic($"[AcousticZoneController] Underwater. Transition: {underwaterTransitionDuration}s");
        }

        /// <summary>
        /// Устанавливает начальный snapshot БЕЗ перехода (мгновенно).
        /// Вызывается в Start() для корректного начального состояния.
        ///
        /// TransitionTo(0f) — мгновенное переключение (Unity поддерживает 0).
        /// </summary>
        private void ApplyInitialSnapshot()
        {
            if (playerBuoyancy == null) return;

            ApplyInitialSnapshot(ResolveCurrentZone());
        }

        private void ApplyInitialSnapshot(AcousticZoneState zone)
        {
            _lastZone = zone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = 0f;
            ApplyAmbientLoopState(zone);
            TransitionToResolvedSnapshot(zone, 0f);

            UpdateDiagnostics(zone);

            LogDiagnostic($"[AcousticZoneController] Initial zone: {zone}");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TRANSITION SOUND
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Воспроизводит переходный звук через SpatialAudioManager.
        /// 2D (PlayStatic2D) — звук "внутри шлема", не позиционный.
        ///
        /// Null-safe для clip и SpatialAudioManager.
        /// </summary>
        private void PlayTransitionSound(AudioClip clip)
        {
            if (clip == null) return;

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager sam))
                return;

            sam.PlayStatic2D(clip, transitionVolume);
        }

        private AudioMixerSnapshot ResolveSurfaceSnapshot()
        {
            EnsureSnapshotBindings();

            if (!HasAnyResolvedSnapshotCoverage())
                return null;

            if (_surfaceElectricalActivity >= 0.55f && surfaceStormSnapshot != null)
                return surfaceStormSnapshot;

            if (_surfacePrecipitationIntensity >= 0.2f && surfaceRainSnapshot != null)
                return surfaceRainSnapshot;

            if (surfaceSnapshot != null)
                return surfaceSnapshot;

            if (baseInteriorSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to BaseInteriorSnapshot. Author Surface/SurfaceRain/SurfaceStorm snapshots in MasterMixer.");
                return baseInteriorSnapshot;
            }

            if (underwaterSnapshot != null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSurfaceSnapshotSet,
                    "[AcousticZoneController] Surface snapshot set missing. Falling back to UnderwaterSnapshot because no dry/exterior snapshot is authored.");
                return underwaterSnapshot;
            }

            LogSnapshotFallbackWarningOnce(
                ref _warnedMissingSurfaceSnapshotSet,
                "[AcousticZoneController] Surface snapshot set missing and no fallback snapshot exists. Surface acoustic transitions will keep the previous mixer state.");
            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ленивый resolve BuoyancyObject на текущем игроке через SceneBootstrap.
        /// Вызывается один раз. Если игрок ещё не готов — повторяет позже в Tick.
        ///
        /// TryGetComponent — zero GC.
        /// TryGetComponent — zero GC.
        /// </summary>
        private void FindPlayerBuoyancy(bool force)
        {
            if (!force && Time.unscaledTime < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = Time.unscaledTime + PlayerResolveRetryInterval;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                playerTransform.TryGetComponent(out playerBuoyancy);
                playerTransform.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(playerTransform);
                ResolvePlayerListenerFilters(playerTransform);
            }

            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — MANUAL CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Принудительный переход в указанную зону.
        /// Используется из внешних систем (скриптовые сцены, читы, тесты).
        ///
        /// Пример: AcousticZoneController.Instance.ForceZone(true); // Interior
        /// </summary>
        /// <param name="isInterior">true = интерьер, false = подводная.</param>
        public void ForceZone(bool isInterior)
        {
            AcousticZoneState forcedZone = isInterior
                ? AcousticZoneState.Interior
                : AcousticZoneState.Underwater;

            if (forcedZone == _lastZone && _stateInitialized)
                return; // Уже в нужной зоне

            _lastZone = forcedZone;
            _stateInitialized = true;
            _hasPendingExteriorZone = false;
            _nextExteriorTransitionAllowedTime = forcedZone == AcousticZoneState.Interior
                ? 0f
                : Time.unscaledTime + exteriorTransitionHoldTime;

            ApplyZoneTransition(forcedZone);

            OnAcousticZoneChanged?.Invoke(isInterior);
            UpdateDiagnostics(forcedZone);
        }

        /// <summary>
        /// Устанавливает BuoyancyObject игрока в рантайме.
        /// Вызывается при респавне игрока или смене контроллера.
        /// </summary>
        public void SetPlayerBuoyancy(BuoyancyObject buoyancy)
        {
            playerBuoyancy = buoyancy;
            _playerMovement = null;
            playerUnderwaterAmbientSource = null;
            _cachedPlayerAudioListener = null;
            _listenerLowPassFilter = null;
            _listenerReverbFilter = null;
            _listenerFallbackDefaultsCaptured = false;
            _hasPendingExteriorZone = false;
            if (buoyancy != null)
            {
                buoyancy.TryGetComponent(out _playerMovement);
                ResolvePlayerAmbientSource(buoyancy.transform);
                ResolvePlayerListenerFilters(buoyancy.transform);
            }
            _stateInitialized = false; // Переинициализация при следующем Tick
            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(AcousticZoneState zone)
        {
            _debugIsInterior = zone == AcousticZoneState.Interior;
            _debugIsUnderwater = zone == AcousticZoneState.Underwater;
            _debugTransitionCount++;
            _debugFaunaMood = ResolveAmbientMoodLabel();
            _debugAmbientSummary = string.IsNullOrWhiteSpace(_currentAmbientSummary) ? "None" : _currentAmbientSummary;
            _debugSnapshotCoverage = BuildSnapshotCoverageSummary();
            _debugMixerCoverage = BuildMixerCoverageSummary();
            _debugAmbientVolume = _ambientSourceBaseVolume * _currentAmbientVolumeScale * _currentSoundscapeVolumeScale;
            _debugAmbientPitch = _ambientSourceBasePitch * _currentAmbientPitchScale * _currentSoundscapePitchScale;
            _debugSoundscapeTier = _currentSoundscapeTier.ToString();
            _debugSoundscapeVolumeScale = _currentSoundscapeVolumeScale;
            _debugSoundscapePitchScale = _currentSoundscapePitchScale;
            if (_usingSourceLevelAcousticFallback)
                _debugMixerCoverage += " | ListenerFallback";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdatePlayerFoundDiagnostic()
        {
            _debugPlayerFound = playerBuoyancy != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogDiagnostic(string message)
        {
            Debug.Log(message, this);
        }

        private AcousticZoneState ResolveCurrentZone()
        {
            if (playerBuoyancy != null && playerBuoyancy.IsInDryZone)
                return AcousticZoneState.Interior;

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
            {
                _acousticUnderwaterState = ResolveMovementDrivenExteriorState(movement);

                return _acousticUnderwaterState
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
            }

            if (_hasCachedExteriorZone)
                return _cachedExteriorZone;

            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere != null)
            {
                AcousticZoneState zone = atmosphere.CurrentState == EnvironmentState.UNDERWATER
                    ? AcousticZoneState.Underwater
                    : AcousticZoneState.Surface;
                _cachedExteriorZone = zone;
                _hasCachedExteriorZone = true;
                return zone;
            }

            _fallbackUnderwaterState =
                SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    ResolvePlayerDepthFallback(),
                    _fallbackUnderwaterState,
                    acousticEnterUnderwaterDepth,
                    acousticExitUnderwaterDepth);

            return _fallbackUnderwaterState
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
        }

        private bool ResolveMovementDrivenExteriorState(HectonPlayerMovement movement)
        {
            float depth = Mathf.Max(0f, movement.CurrentDepth);
            float immersion = Mathf.Clamp01(movement.WaterImmersionRatio);
            bool headSubmerged = movement.IsPlayerSubmerged;

            if (headSubmerged || depth >= acousticForceUnderwaterDepth)
                return true;

            if (_acousticUnderwaterState)
            {
                if (immersion <= acousticExitImmersionRatio && depth <= acousticExitUnderwaterDepth)
                    return false;

                return depth > acousticExitUnderwaterDepth || immersion > acousticExitImmersionRatio;
            }

            if (depth < acousticEnterUnderwaterDepth)
                return false;

            return immersion >= acousticEnterImmersionRatio;
        }

        private AcousticZoneState ResolveStableZone(AcousticZoneState candidateZone)
        {
            if (!_stateInitialized)
                return candidateZone;

            if (candidateZone == AcousticZoneState.Interior || _lastZone == AcousticZoneState.Interior)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            if (candidateZone == _lastZone)
            {
                _hasPendingExteriorZone = false;
                return candidateZone;
            }

            float now = Time.unscaledTime;
            if (now < _nextExteriorTransitionAllowedTime)
            {
                _hasPendingExteriorZone = false;
                return _lastZone;
            }

            if (!_hasPendingExteriorZone || _pendingExteriorZone != candidateZone)
            {
                _pendingExteriorZone = candidateZone;
                _pendingExteriorZoneResolveTime = now + exteriorTransitionDebounce;
                _hasPendingExteriorZone = true;
                return _lastZone;
            }

            if (now < _pendingExteriorZoneResolveTime)
                return _lastZone;

            _hasPendingExteriorZone = false;
            return candidateZone;
        }

        private float ResolvePlayerDepthFallback()
        {
            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
                return movement.CurrentDepth;

            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere != null && atmosphere.CurrentState == EnvironmentState.UNDERWATER)
                return acousticEnterUnderwaterDepth;

            return 0f;
        }

        private void HandleAtmosphereStateChanged(EnvironmentState state)
        {
            _cachedExteriorZone = state == EnvironmentState.UNDERWATER
                ? AcousticZoneState.Underwater
                : AcousticZoneState.Surface;
            _hasCachedExteriorZone = true;
        }

        private void ResolveBiomeMatrixDirector(bool force)
        {
            if (biomeMatrixDirector != null)
                return;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextBiomeMatrixResolveTime)
                return;

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            _nextBiomeMatrixResolveTime = currentTime + biomeMatrixResolveRetryInterval;
        }

        private void ResolveSoundscapeSystem(bool force)
        {
            if (soundscapeSystem != null)
                return;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextSoundscapeResolveTime)
                return;

            soundscapeSystem = SoundscapeSystem.Instance;
            _nextSoundscapeResolveTime = currentTime + soundscapeResolveRetryInterval;
        }

        private void RefreshSoundscapeTierContext(bool force)
        {
            ResolveSoundscapeSystem(force);

            SoundscapeTier tier = soundscapeSystem != null
                ? soundscapeSystem.CurrentTier
                : SoundscapeTier.Shallow;

            ApplySoundscapeTierContext(tier);
        }

        private void ApplySoundscapeTierContext(SoundscapeTier tier)
        {
            _currentSoundscapeTier = tier;
            _currentSoundscapeVolumeScale = shallowTierAmbientVolumeScale;
            _currentSoundscapePitchScale = shallowTierAmbientPitchScale;

            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    _currentSoundscapeVolumeScale = twilightTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = twilightTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Darkness:
                    _currentSoundscapeVolumeScale = darknessTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = darknessTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Abyss:
                    _currentSoundscapeVolumeScale = abyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = abyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.DeepAbyss:
                    _currentSoundscapeVolumeScale = deepAbyssTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = deepAbyssTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Thermal:
                    _currentSoundscapeVolumeScale = thermalTierAmbientVolumeScale;
                    _currentSoundscapePitchScale = thermalTierAmbientPitchScale;
                    break;

                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    break;
            }
        }

        private void HandleSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            ApplySoundscapeTierContext(newTier);
        }

        private void RefreshBiomeAmbientContext()
        {
            ResolveBiomeMatrixDirector(false);

            HectonBiomeMatrixProfile profile = biomeMatrixDirector != null
                ? biomeMatrixDirector.CurrentProfile
                : null;

            if (ReferenceEquals(profile, _lastBiomeProfileForAmbient))
                return;

            _lastBiomeProfileForAmbient = profile;
            _currentAmbientSurvivalPressure = 0;
            _currentAmbientRewardPull = 0;
            _currentAmbientSummary = null;
            _currentAmbientVolumeScale = 1f;
            _currentAmbientPitchScale = 1f;

            if (profile == null)
                return;

            _currentAmbientSurvivalPressure = profile.survivalPressure;
            _currentAmbientRewardPull = profile.rewardPull;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            if (familyProfile != null)
            {
                HectonFaunaFamilyProfile faunaFamilyProfile = familyProfile.faunaFamilyProfile;
                if (faunaFamilyProfile != null)
                    _currentAmbientSummary = faunaFamilyProfile.ambienceSummary;
            }

            if (_currentAmbientSurvivalPressure >= 4)
            {
                _currentAmbientVolumeScale = hostileAmbientVolumeScale;
                _currentAmbientPitchScale = hostileAmbientPitchScale;
                return;
            }

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
            {
                _currentAmbientVolumeScale = livelyAmbientVolumeScale;
                _currentAmbientPitchScale = livelyAmbientPitchScale;
                return;
            }

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
            {
                _currentAmbientVolumeScale = calmAmbientVolumeScale;
                _currentAmbientPitchScale = calmAmbientPitchScale;
                return;
            }

            _currentAmbientVolumeScale = mixedAmbientVolumeScale;
            _currentAmbientPitchScale = mixedAmbientPitchScale;
        }

        private void ApplyZoneTransition(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    TransitionToInterior();
                    break;

                case AcousticZoneState.Surface:
                    TransitionToSurface();
                    break;

                default:
                    TransitionToUnderwater();
                    break;
            }
        }

        private HectonAtmosphereManager ResolveAtmosphereManager()
        {
            if (_atmosphereManager == null)
                _atmosphereManager = HectonAtmosphereManager.Instance;

            return _atmosphereManager;
        }

        private void RefreshAtmosphereZoneCache()
        {
            HectonAtmosphereManager atmosphere = ResolveAtmosphereManager();
            if (atmosphere == null)
            {
                _hasCachedExteriorZone = false;
                return;
            }

            HandleAtmosphereStateChanged(atmosphere.CurrentState);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement == null && playerBuoyancy != null)
                playerBuoyancy.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private AudioSource ResolvePlayerAmbientSource()
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
            {
                CacheAmbientSourceDefaults(playerUnderwaterAmbientSource);
                return playerUnderwaterAmbientSource;
            }

            playerUnderwaterAmbientSource = null;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                ResolvePlayerAmbientSource(playerTransform);

            return playerUnderwaterAmbientSource;
        }

        private void ResolvePlayerAmbientSource(Transform playerTransform)
        {
            if ((object)playerUnderwaterAmbientSource != null && playerUnderwaterAmbientSource != null)
                return;

            if (playerTransform == null || _playerAudioSources == null)
                return;

            _playerAudioSources.Clear();
            playerTransform.GetComponentsInChildren(true, _playerAudioSources);

            int count = _playerAudioSources.Count;
            for (int i = 0; i < count; i++)
            {
                AudioSource candidate = _playerAudioSources[i];
                if (candidate == null || candidate.clip == null)
                    continue;

                if (!candidate.loop || candidate.spatialBlend > 0.01f)
                    continue;

                if (!candidate.playOnAwake && !candidate.isPlaying)
                    continue;

                playerUnderwaterAmbientSource = candidate;
                CacheAmbientSourceDefaults(candidate);
                return;
            }
        }

        private void CacheAmbientSourceDefaults(AudioSource ambientSource)
        {
            if (ambientSource == null)
                return;

            if (_cachedAmbientSource == ambientSource && _ambientSourceDefaultsCaptured)
                return;

            _cachedAmbientSource = ambientSource;
            _ambientSourceBaseVolume = ambientSource.volume;
            _ambientSourceBasePitch = ambientSource.pitch;
            _ambientSourceDefaultsCaptured = true;
        }

        private void ResolvePlayerListenerFilters()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                ResolvePlayerListenerFilters(playerTransform);
        }

        private void ResolvePlayerListenerFilters(Transform playerTransform)
        {
            if (playerTransform == null)
                return;

            AudioListener listener = _cachedPlayerAudioListener;
            if ((object)listener == null || listener == null)
            {
                if (!playerTransform.TryGetComponent(out listener))
                    listener = playerTransform.GetComponentInChildren<AudioListener>(true);

                _cachedPlayerAudioListener = listener;
            }

            if ((object)listener == null || listener == null)
                return;

            if (!_listenerFallbackDefaultsCaptured)
            {
                if (!listener.TryGetComponent(out _listenerLowPassFilter))
                {
                    _listenerLowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>(); // COLD ALLOC: AudioLowPassFilter[1] — listener fallback acoustic filtering — owner: AcousticZoneController
                    _listenerLowPassFilter.enabled = false;
                }

                if (!listener.TryGetComponent(out _listenerReverbFilter))
                {
                    _listenerReverbFilter = listener.gameObject.AddComponent<AudioReverbFilter>(); // COLD ALLOC: AudioReverbFilter[1] — listener fallback acoustic reverb — owner: AcousticZoneController
                    _listenerReverbFilter.enabled = false;
                }

                _listenerLowPassBaseCutoff = _listenerLowPassFilter.cutoffFrequency;
                _listenerLowPassBaseResonance = _listenerLowPassFilter.lowpassResonanceQ;
                _listenerReverbBasePreset = _listenerReverbFilter.reverbPreset;
                _listenerReverbBaseDryLevel = _listenerReverbFilter.dryLevel;
                _listenerFallbackDefaultsCaptured = true;
            }
        }

        private void UpdateAmbientLoopMix(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource();
            if (ambientSource == null)
                return;

            CacheAmbientSourceDefaults(ambientSource);

            float targetVolume = _ambientSourceBaseVolume;
            float targetPitch = _ambientSourceBasePitch;

            if (zone == AcousticZoneState.Underwater)
            {
                targetVolume *= _currentAmbientVolumeScale;
                targetPitch *= _currentAmbientPitchScale;
                targetVolume *= _currentSoundscapeVolumeScale;
                targetPitch *= _currentSoundscapePitchScale;
            }

            if (Mathf.Abs(ambientSource.volume - targetVolume) > 0.01f)
                ambientSource.volume = targetVolume;

            if (Mathf.Abs(ambientSource.pitch - targetPitch) > 0.01f)
                ambientSource.pitch = targetPitch;
        }

        private string ResolveAmbientMoodLabel()
        {
            if (_currentAmbientSurvivalPressure >= 4)
                return "Hostile";

            if (_currentAmbientRewardPull >= 4 && _currentAmbientSurvivalPressure <= 2)
                return "Lively";

            if (_currentAmbientSurvivalPressure <= 2 && _currentAmbientRewardPull <= 2)
                return "Calm";

            if (_currentAmbientSurvivalPressure <= 0 && _currentAmbientRewardPull <= 0)
                return "None";

            return "Mixed";
        }

        internal void SetSurfaceWeatherMix(float precipitationIntensity, float electricalActivity)
        {
            _surfacePrecipitationIntensity = Mathf.Clamp01(precipitationIntensity);
            _surfaceElectricalActivity = Mathf.Clamp01(electricalActivity);

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        internal void ClearSurfaceWeatherMix()
        {
            _surfacePrecipitationIntensity = 0f;
            _surfaceElectricalActivity = 0f;

            if (_stateInitialized && _lastZone == AcousticZoneState.Surface)
                TransitionToResolvedSnapshot(AcousticZoneState.Surface, surfaceWeatherTransitionDuration);
        }

        private void ApplyAmbientLoopState(AcousticZoneState zone)
        {
            AudioSource ambientSource = ResolvePlayerAmbientSource();
            if (ambientSource == null)
            {
                ApplySourceLevelAcousticFallback(zone);
                return;
            }

            bool shouldBeAudible = zone == AcousticZoneState.Underwater;
            bool shouldMute = !shouldBeAudible;

            if (ambientSource.mute != shouldMute)
                ambientSource.mute = shouldMute;

            if (shouldBeAudible && !ambientSource.isPlaying && ambientSource.clip != null)
                ambientSource.Play();

            UpdateAmbientLoopMix(zone);
            ApplySourceLevelAcousticFallback(zone);
        }

        private void ApplySourceLevelAcousticFallback(AcousticZoneState zone)
        {
            if (!ShouldUseSourceLevelAcousticFallback())
            {
                ResetSourceLevelAcousticFallback();
                return;
            }

            ResolvePlayerListenerFilters();
            if (!_listenerFallbackDefaultsCaptured ||
                (object)_listenerLowPassFilter == null || _listenerLowPassFilter == null ||
                (object)_listenerReverbFilter == null || _listenerReverbFilter == null)
            {
                return;
            }

            _usingSourceLevelAcousticFallback = true;

            switch (zone)
            {
                case AcousticZoneState.Underwater:
                    _listenerLowPassFilter.enabled = true;
                    _listenerLowPassFilter.cutoffFrequency = ResolveUnderwaterFallbackCutoff();
                    _listenerLowPassFilter.lowpassResonanceQ = 1.1f;
                    _listenerReverbFilter.enabled = false;
                    _listenerReverbFilter.reverbPreset = _listenerReverbBasePreset;
                    _listenerReverbFilter.dryLevel = _listenerReverbBaseDryLevel;
                    break;

                case AcousticZoneState.Interior:
                    _listenerLowPassFilter.enabled = true;
                    _listenerLowPassFilter.cutoffFrequency = interiorFallbackLowPassCutoff;
                    _listenerLowPassFilter.lowpassResonanceQ = 1f;
                    _listenerReverbFilter.enabled = true;
                    _listenerReverbFilter.reverbPreset = interiorFallbackReverbPreset;
                    _listenerReverbFilter.dryLevel = interiorFallbackReverbDryLevel;
                    break;

                default:
                    ResetSourceLevelAcousticFallback();
                    break;
            }
        }

        private bool ShouldUseSourceLevelAcousticFallback()
        {
            if (!enableSourceLevelAcousticFallback)
                return false;

            EnsureSnapshotBindings();
            return !_validatedMixerHasEffectGraph || _validatedMixerSnapshotCount <= 1;
        }

        private float ResolveUnderwaterFallbackCutoff()
        {
            switch (_currentSoundscapeTier)
            {
                case SoundscapeTier.DeepAbyss:
                    return 650f;

                case SoundscapeTier.Abyss:
                    return 800f;

                case SoundscapeTier.Darkness:
                    return 950f;

                case SoundscapeTier.Twilight:
                    return 1250f;

                case SoundscapeTier.Thermal:
                    return 900f;

                default:
                    return underwaterFallbackLowPassCutoff;
            }
        }

        private void ResetSourceLevelAcousticFallback()
        {
            if (!_listenerFallbackDefaultsCaptured)
            {
                _usingSourceLevelAcousticFallback = false;
                return;
            }

            if ((object)_listenerLowPassFilter != null && _listenerLowPassFilter != null)
            {
                _listenerLowPassFilter.cutoffFrequency = _listenerLowPassBaseCutoff;
                _listenerLowPassFilter.lowpassResonanceQ = _listenerLowPassBaseResonance;
                _listenerLowPassFilter.enabled = false;
            }

            if ((object)_listenerReverbFilter != null && _listenerReverbFilter != null)
            {
                _listenerReverbFilter.reverbPreset = _listenerReverbBasePreset;
                _listenerReverbFilter.dryLevel = _listenerReverbBaseDryLevel;
                _listenerReverbFilter.enabled = false;
            }

            _usingSourceLevelAcousticFallback = false;
        }

        // ══════════════════════════════════════════════════════════
        //  SNAPSHOT BINDING / FALLBACKS
        // ══════════════════════════════════════════════════════════

        private void EnsureSnapshotBindings()
        {
            if (_snapshotBindingsResolved)
                return;

            _snapshotBindingsResolved = true;

            if (masterMixer == null)
                return;

            ResolveSnapshotBinding(ref underwaterSnapshot, "Underwater", "UnderwaterSnapshot");
            ResolveSnapshotBinding(ref baseInteriorSnapshot, "BaseInterior", "BaseInteriorSnapshot");
            ResolveSnapshotBinding(ref surfaceSnapshot, "Surface", "SurfaceSnapshot");
            ResolveSnapshotBinding(ref surfaceRainSnapshot, "SurfaceRain", "SurfaceRainSnapshot");
            ResolveSnapshotBinding(ref surfaceStormSnapshot, "SurfaceStorm", "SurfaceStormSnapshot");

            if (underwaterSnapshot == null &&
                baseInteriorSnapshot == null &&
                surfaceSnapshot == null &&
                surfaceRainSnapshot == null &&
                surfaceStormSnapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] MasterMixer is assigned but no authored acoustic snapshots were resolved by name. Expected names include Underwater/UnderwaterSnapshot, BaseInterior/BaseInteriorSnapshot, Surface/SurfaceSnapshot, SurfaceRain/SurfaceRainSnapshot, SurfaceStorm/SurfaceStormSnapshot.");
            }

#if UNITY_EDITOR
            ValidateMixerAuthoringCoverage();
#endif
        }

        private void ResolveSnapshotBinding(
            ref AudioMixerSnapshot snapshot,
            string primaryName,
            string alternateName)
        {
            if (snapshot != null || masterMixer == null)
                return;

            snapshot = masterMixer.FindSnapshot(primaryName);
            if (snapshot == null && !string.IsNullOrEmpty(alternateName))
                snapshot = masterMixer.FindSnapshot(alternateName);
        }

        private void TransitionToResolvedSnapshot(AcousticZoneState zone, float duration)
        {
            EnsureSnapshotBindings();

            if (zone == AcousticZoneState.Surface &&
                TryTransitionSurfaceSnapshotBlend(duration))
            {
                LogDiagnostic("[AcousticZoneController] Snapshot activated: SurfaceBlend");
                return;
            }

            AudioMixerSnapshot snapshot = ResolveSnapshotForZone(zone);
            if (snapshot == null)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingSnapshotCoverage,
                    "[AcousticZoneController] No valid snapshot could be resolved for the requested acoustic zone. Mixer state will remain unchanged.");
                return;
            }

            snapshot.TransitionTo(Mathf.Max(0f, duration));
            LogDiagnostic($"[AcousticZoneController] Snapshot activated: {snapshot.name}");
        }

        private bool TryTransitionSurfaceSnapshotBlend(float duration)
        {
            if (masterMixer == null || surfaceSnapshot == null)
                return false;

            int snapshotCount = 0;
            float totalWeight = 0f;

            _surfaceBlendSnapshots[snapshotCount] = surfaceSnapshot;
            _surfaceBlendWeights[snapshotCount] = 1f;
            totalWeight += 1f;
            snapshotCount++;

            if (surfaceRainSnapshot != null && _surfacePrecipitationIntensity >= 0.2f)
            {
                float rainWeight = Mathf.Clamp01(_surfacePrecipitationIntensity) * surfaceRainSnapshotWeight;
                if (rainWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceRainSnapshot;
                    _surfaceBlendWeights[snapshotCount] = rainWeight;
                    totalWeight += rainWeight;
                    snapshotCount++;
                }
            }

            if (surfaceStormSnapshot != null && _surfaceElectricalActivity >= 0.55f)
            {
                float stormWeight = Mathf.Clamp01(_surfaceElectricalActivity) * surfaceStormSnapshotWeight;
                if (stormWeight > 0.001f)
                {
                    _surfaceBlendSnapshots[snapshotCount] = surfaceStormSnapshot;
                    _surfaceBlendWeights[snapshotCount] = stormWeight;
                    totalWeight += stormWeight;
                    snapshotCount++;
                }
            }

            if (snapshotCount <= 1 || totalWeight <= 0.001f)
                return false;

            for (int i = 0; i < snapshotCount; i++)
                _surfaceBlendWeights[i] /= totalWeight;

            for (int i = snapshotCount; i < _surfaceBlendSnapshots.Length; i++)
            {
                _surfaceBlendSnapshots[i] = null;
                _surfaceBlendWeights[i] = 0f;
            }

            float transitionTime = Mathf.Max(0f, surfaceWeatherTransitionDuration > 0f ? surfaceWeatherTransitionDuration : duration);
            masterMixer.TransitionToSnapshots(_surfaceBlendSnapshots, _surfaceBlendWeights, transitionTime);
            return true;
        }

        private AudioMixerSnapshot ResolveSnapshotForZone(AcousticZoneState zone)
        {
            switch (zone)
            {
                case AcousticZoneState.Interior:
                    if (baseInteriorSnapshot != null)
                        return baseInteriorSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingInteriorSnapshot,
                        "[AcousticZoneController] BaseInteriorSnapshot missing. Falling back to exterior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? underwaterSnapshot;

                case AcousticZoneState.Surface:
                    return ResolveSurfaceSnapshot();

                default:
                    if (underwaterSnapshot != null)
                        return underwaterSnapshot;

                    if (!HasAnyResolvedSnapshotCoverage())
                        return null;

                    LogSnapshotFallbackWarningOnce(
                        ref _warnedMissingUnderwaterSnapshot,
                        "[AcousticZoneController] UnderwaterSnapshot missing. Falling back to surface/interior snapshot coverage.");
                    return ResolveSurfaceSnapshot() ?? baseInteriorSnapshot;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogSnapshotFallbackWarningOnce(ref bool warnedFlag, string message)
        {
            if (warnedFlag)
                return;

            warnedFlag = true;
            Debug.LogWarning(message, this);
        }

        private bool HasAnyResolvedSnapshotCoverage()
        {
            return underwaterSnapshot != null ||
                   baseInteriorSnapshot != null ||
                   surfaceSnapshot != null ||
                   surfaceRainSnapshot != null ||
                   surfaceStormSnapshot != null;
        }

        private string BuildSnapshotCoverageSummary()
        {
            return string.Concat(
                underwaterSnapshot != null ? "UW " : "uw- ",
                baseInteriorSnapshot != null ? "INT " : "int- ",
                surfaceSnapshot != null ? "SURF " : "surf- ",
                surfaceRainSnapshot != null ? "RAIN " : "rain- ",
                surfaceStormSnapshot != null ? "STORM" : "storm-");
        }

        private string BuildMixerCoverageSummary()
        {
            if (masterMixer == null)
                return "Mixer: None";

            return string.Concat(
                "Mixer snapshots=", _validatedMixerSnapshotCount.ToString(),
                " named=", _validatedMixerHasNamedCoverage ? "yes" : "no",
                " fx=", _validatedMixerHasEffectGraph ? "yes" : "no");
        }

#if UNITY_EDITOR
        private void Reset()
        {
            TryAssignEditorAuthoringDefaults();
        }

        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (transitionDuration < 0f) transitionDuration = 0f;
            if (interiorTransitionDuration < 0f) interiorTransitionDuration = 0f;
            if (surfaceTransitionDuration < 0f) surfaceTransitionDuration = 0f;
            if (underwaterTransitionDuration < 0f) underwaterTransitionDuration = 0f;
            if (surfaceWeatherTransitionDuration < 0f) surfaceWeatherTransitionDuration = 0f;
            if (acousticEnterUnderwaterDepth < 0f) acousticEnterUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth < 0f) acousticExitUnderwaterDepth = 0f;
            if (acousticExitUnderwaterDepth > acousticEnterUnderwaterDepth) acousticExitUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (acousticEnterImmersionRatio < 0.1f) acousticEnterImmersionRatio = 0.1f;
            if (acousticEnterImmersionRatio > 1f) acousticEnterImmersionRatio = 1f;
            if (acousticExitImmersionRatio < 0.05f) acousticExitImmersionRatio = 0.05f;
            if (acousticExitImmersionRatio > acousticEnterImmersionRatio) acousticExitImmersionRatio = acousticEnterImmersionRatio;
            if (acousticForceUnderwaterDepth < acousticEnterUnderwaterDepth) acousticForceUnderwaterDepth = acousticEnterUnderwaterDepth;
            if (exteriorTransitionDebounce < 0f) exteriorTransitionDebounce = 0f;
            if (exteriorTransitionHoldTime < 0f) exteriorTransitionHoldTime = 0f;
            if (transitionVolume < 0f) transitionVolume = 0f;
            if (transitionVolume > 1f) transitionVolume = 1f;
            if (interiorFallbackReverbDryLevel < -10000f) interiorFallbackReverbDryLevel = -10000f;
            if (interiorFallbackReverbDryLevel > 0f) interiorFallbackReverbDryLevel = 0f;
            _snapshotBindingsResolved = false;
            ResetAuthoringWarnings();
            TryAssignEditorAuthoringDefaults();
            EnsureSnapshotBindings();
        }

        private void TryAssignEditorAuthoringDefaults()
        {
            if (masterMixer == null)
                masterMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(DefaultMasterMixerPath);

            if (waterDrainSound == null)
                waterDrainSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterDrainSoundPath);

            if (waterFillSound == null)
                waterFillSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterFillSoundPath);
        }

        private void ResetAuthoringWarnings()
        {
            _warnedMissingInteriorSnapshot = false;
            _warnedMissingUnderwaterSnapshot = false;
            _warnedMissingSurfaceSnapshotSet = false;
            _warnedMissingSnapshotCoverage = false;
            _warnedIncompleteMixerSnapshotAuthoring = false;
            _warnedMissingMixerEffectGraph = false;
            _validatedMixerSnapshotCount = 0;
            _validatedMixerHasNamedCoverage = false;
            _validatedMixerHasEffectGraph = false;
        }

        private void ValidateMixerAuthoringCoverage()
        {
            if (masterMixer == null)
                return;

            string mixerAssetPath = UnityEditor.AssetDatabase.GetAssetPath(masterMixer);
            if (string.IsNullOrEmpty(mixerAssetPath))
                return;

            UnityEngine.Object[] mixerSubAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(mixerAssetPath);
            if (mixerSubAssets == null || mixerSubAssets.Length <= 0)
                return;

            int snapshotCount = 0;
            bool hasNamedCoverage = false;
            bool hasNonAttenuationEffect = false;

            for (int i = 0; i < mixerSubAssets.Length; i++)
            {
                UnityEngine.Object subAsset = mixerSubAssets[i];
                if (subAsset == null)
                    continue;

                Type subAssetType = subAsset.GetType();
                if (subAssetType == null)
                    continue;

                string typeName = subAssetType.Name;
                if (typeName == "AudioMixerSnapshotController")
                {
                    snapshotCount++;
                    string snapshotName = subAsset.name;
                    if (snapshotName == "Underwater" ||
                        snapshotName == "UnderwaterSnapshot" ||
                        snapshotName == "BaseInterior" ||
                        snapshotName == "BaseInteriorSnapshot" ||
                        snapshotName == "Surface" ||
                        snapshotName == "SurfaceSnapshot" ||
                        snapshotName == "SurfaceRain" ||
                        snapshotName == "SurfaceRainSnapshot" ||
                        snapshotName == "SurfaceStorm" ||
                        snapshotName == "SurfaceStormSnapshot")
                    {
                        hasNamedCoverage = true;
                    }

                    continue;
                }

                if (typeName != "AudioMixerEffectController")
                    continue;

                SerializedObject effectSerializedObject = new SerializedObject(subAsset);
                SerializedProperty effectNameProperty = effectSerializedObject.FindProperty("m_EffectName");
                if (effectNameProperty == null)
                    continue;

                string effectName = effectNameProperty.stringValue;
                if (!string.IsNullOrEmpty(effectName) && effectName != "Attenuation")
                    hasNonAttenuationEffect = true;
            }

            _validatedMixerSnapshotCount = snapshotCount;
            _validatedMixerHasNamedCoverage = hasNamedCoverage;
            _validatedMixerHasEffectGraph = hasNonAttenuationEffect;

            if (snapshotCount <= 1 || !hasNamedCoverage)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedIncompleteMixerSnapshotAuthoring,
                    $"[AcousticZoneController] MasterMixer snapshot authoring is incomplete. Snapshot count={snapshotCount}. Expected named coverage includes Underwater, BaseInterior, Surface, SurfaceRain, and SurfaceStorm.");
            }

            if (!hasNonAttenuationEffect)
            {
                LogSnapshotFallbackWarningOnce(
                    ref _warnedMissingMixerEffectGraph,
                    "[AcousticZoneController] MasterMixer effect graph has no authored acoustic processing beyond Attenuation. Underwater/interior transitions need LPF/reverb-style processing to create real contrast.");
            }
        }
#endif
    }
}
