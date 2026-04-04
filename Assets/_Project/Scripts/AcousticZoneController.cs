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
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)] // После FluidEngine (-5000), до большинства систем
    public sealed class AcousticZoneController : MonoBehaviour, ITickable
    {
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

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Transition Settings ───────────────────────")]
        [Tooltip("Время перехода между snapshot'ами (секунды).\n" +
                 "2.0 = плавный кроссфейд, имитирующий откачку воды.\n" +
                 "0.5 = быстрый переход для тестирования.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Время перехода при входе в воду (может быть быстрее,\n" +
                 "т.к. 'вода заполняет шлюз' мгновеннее, чем 'откачка').")]
        [SerializeField] private float underwaterTransitionDuration = 1.5f;

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

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsInterior;
        [SerializeField] private bool _debugPlayerFound;
        [SerializeField] private int  _debugTransitionCount;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Последнее известное состояние: true = интерьер (сухая зона).
        /// Используется для edge detection. -1-like: первый кадр
        /// определяет начальное состояние без запуска перехода.
        /// </summary>
        private bool _lastIsInterior;

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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true если игрок сейчас в сухой зоне (интерьер базы).
        /// false если в воде.
        /// </summary>
        public bool IsInterior => _lastIsInterior;

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
            DontDestroyOnLoad(gameObject);

            _stateInitialized = false;
            _registeredToTickManager = false;
        }

        private void OnEnable()
        {
            TryRegister();
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

            // ── Валидация snapshot'ов ──
            if (underwaterSnapshot == null)
            {
                Debug.LogWarning(
                    "[AcousticZoneController] UnderwaterSnapshot not assigned! " +
                    "No audio transition will occur.", this);
            }

            if (baseInteriorSnapshot == null)
            {
                Debug.LogWarning(
                    "[AcousticZoneController] BaseInteriorSnapshot not assigned! " +
                    "No audio transition will occur.", this);
            }

            // ── Установка начального snapshot без перехода ──
            ApplyInitialSnapshot();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance == null) return;

            if (_registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
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
            bool isInterior = playerBuoyancy.IsInAir;

            // ── Первый кадр: установить начальное состояние без перехода ──
            if (!_stateInitialized)
            {
                _lastIsInterior = isInterior;
                _stateInitialized = true;
                UpdateDiagnostics(isInterior);
                return;
            }

            // ── Edge detection: переход только при СМЕНЕ состояния ──
            if (isInterior == _lastIsInterior)
                return;

            // ══════════════════════════════════════════════
            //  TRANSITION DETECTED!
            // ══════════════════════════════════════════════

            _lastIsInterior = isInterior;

            if (isInterior)
            {
                // ── ВОДА → СУША (вход в базу / шлюз откачал воду) ──
                TransitionToInterior();
            }
            else
            {
                // ── СУША → ВОДА (выход из базы / шлюз заполнился) ──
                TransitionToUnderwater();
            }

            // ── Событие для внешних систем ──
            OnAcousticZoneChanged?.Invoke(isInterior);

            UpdateDiagnostics(isInterior);
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
            // ── Snapshot transition ──
            if (baseInteriorSnapshot != null)
            {
                baseInteriorSnapshot.TransitionTo(transitionDuration);
            }

            // ── Переходный звук ──
            PlayTransitionSound(waterDrainSound);

            Debug.Log(
                $"[AcousticZoneController] 🏠 → Interior (dry zone). " +
                $"Transition: {transitionDuration}s");
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
        private void TransitionToUnderwater()
        {
            // ── Snapshot transition ──
            if (underwaterSnapshot != null)
            {
                underwaterSnapshot.TransitionTo(underwaterTransitionDuration);
            }

            // ── Переходный звук ──
            PlayTransitionSound(waterFillSound);

            Debug.Log(
                $"[AcousticZoneController] 🌊 → Underwater. " +
                $"Transition: {underwaterTransitionDuration}s");
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

            bool isInterior = playerBuoyancy.IsInAir;
            _lastIsInterior = isInterior;
            _stateInitialized = true;

            if (isInterior)
            {
                if (baseInteriorSnapshot != null)
                    baseInteriorSnapshot.TransitionTo(0f);
            }
            else
            {
                if (underwaterSnapshot != null)
                    underwaterSnapshot.TransitionTo(0f);
            }

            UpdateDiagnostics(isInterior);

            Debug.Log(
                $"[AcousticZoneController] Initial zone: " +
                $"{(isInterior ? "Interior" : "Underwater")}");
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

            SpatialAudioManager sam = SpatialAudioManager.Instance;
            if (sam == null) return;

            sam.PlayStatic2D(clip, transitionVolume);
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
            if (isInterior == _lastIsInterior && _stateInitialized)
                return; // Уже в нужной зоне

            _lastIsInterior = isInterior;
            _stateInitialized = true;

            if (isInterior)
                TransitionToInterior();
            else
                TransitionToUnderwater();

            OnAcousticZoneChanged?.Invoke(isInterior);
            UpdateDiagnostics(isInterior);
        }

        /// <summary>
        /// Устанавливает BuoyancyObject игрока в рантайме.
        /// Вызывается при респавне игрока или смене контроллера.
        /// </summary>
        public void SetPlayerBuoyancy(BuoyancyObject buoyancy)
        {
            playerBuoyancy = buoyancy;
            _stateInitialized = false; // Переинициализация при следующем Tick
            UpdatePlayerFoundDiagnostic();
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(bool isInterior)
        {
            _debugIsInterior = isInterior;
            _debugTransitionCount++;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdatePlayerFoundDiagnostic()
        {
            _debugPlayerFound = playerBuoyancy != null;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (transitionDuration < 0f) transitionDuration = 0f;
            if (underwaterTransitionDuration < 0f) underwaterTransitionDuration = 0f;
            if (transitionVolume < 0f) transitionVolume = 0f;
            if (transitionVolume > 1f) transitionVolume = 1f;
        }
#endif
    }
}
